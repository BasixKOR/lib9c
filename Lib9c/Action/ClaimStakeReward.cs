using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Bencodex.Types;
using Lib9c;
using Lib9c.Abstractions;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.State;
using Nekoyume.Extensions;
using Nekoyume.Helper;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using static Lib9c.SerializeKeys;

namespace Nekoyume.Action
{
    /// <summary>
    /// Hard forked at https://github.com/planetarium/lib9c/pull/1458
    /// </summary>
    [ActionType(ActionTypeText)]
    public class ClaimStakeReward : GameAction, IClaimStakeReward, IClaimStakeRewardV1
    {
        private const string ActionTypeText = "claim_stake_reward3";

        /// <summary>
        /// This is the version 1 of the stake reward sheet.
        /// The version 1 is used for calculating the reward for the stake
        /// that is accumulated before the table patch.
        /// </summary>
        public static class V1
        {
            public const int MaxLevel = 5;
            public const string StakeRegularRewardSheetCsv =
                @"level,required_gold,item_id,rate,type
1,50,400000,10,Item
1,50,500000,800,Item
1,50,20001,6000,Rune
2,500,400000,8,Item
2,500,500000,800,Item
2,500,20001,6000,Rune
3,5000,400000,5,Item
3,5000,500000,800,Item
3,5000,20001,6000,Rune
4,50000,400000,5,Item
4,50000,500000,800,Item
4,50000,20001,6000,Rune
5,500000,400000,5,Item
5,500000,500000,800,Item
5,500000,20001,6000,Rune";

            public const string StakeRegularFixedRewardSheetCsv =
                @"level,required_gold,item_id,count
1,50,500000,1
2,500,500000,2
3,5000,500000,2
4,50000,500000,2
5,500000,500000,2";
        }

        // NOTE: Use this when the <see cref="StakeRegularFixedRewardSheet"/> or
        // <see cref="StakeRegularRewardSheet"/> is patched.
        // public static class V2
        // {
        // }

        private readonly ImmutableSortedDictionary<
            string,
            ImmutableSortedDictionary<int, IStakeRewardSheet>> _stakeRewardHistoryDict;

        internal Address AvatarAddress { get; private set; }

        Address IClaimStakeRewardV1.AvatarAddress => AvatarAddress;

        public ClaimStakeReward(Address avatarAddress) : this()
        {
            AvatarAddress = avatarAddress;
        }

        public ClaimStakeReward()
        {
            var regularRewardSheetV1 = new StakeRegularRewardSheet();
            regularRewardSheetV1.Set(V1.StakeRegularRewardSheetCsv);
            var fixedRewardSheetV1 = new StakeRegularFixedRewardSheet();
            fixedRewardSheetV1.Set(V1.StakeRegularFixedRewardSheetCsv);
            _stakeRewardHistoryDict =
                new Dictionary<string, ImmutableSortedDictionary<int, IStakeRewardSheet>>
                {
                    {
                        "StakeRegularRewardSheet", new Dictionary<int, IStakeRewardSheet>
                        {
                            { 1, regularRewardSheetV1 },
                        }.ToImmutableSortedDictionary()
                    },
                    {
                        "StakeRegularFixedRewardSheet",
                        new Dictionary<int, IStakeRewardSheet>
                        {
                            { 1, fixedRewardSheetV1 }
                        }.ToImmutableSortedDictionary()
                    },
                }.ToImmutableSortedDictionary();
        }

        private IAccountStateDelta ProcessReward(
            IActionContext context,
            IAccountStateDelta states,
            ref AvatarState avatarState,
            ItemSheet itemSheet,
            FungibleAssetValue stakedAmount,
            int itemRewardStep,
            int runeRewardStep,
            int currencyRewardStep,
            List<StakeRegularFixedRewardSheet.RewardInfo> fixedReward,
            List<StakeRegularRewardSheet.RewardInfo> regularReward)
        {
            var stakedCurrency = stakedAmount.Currency;

            // Regular Reward
            foreach (var reward in regularReward)
            {
                switch (reward.Type)
                {
                    case StakeRegularRewardSheet.StakeRewardType.Item:
                        var (quantity, _) = stakedAmount.DivRem(stakedCurrency * reward.Rate);
                        if (quantity < 1)
                        {
                            // If the quantity is zero, it doesn't add the item into inventory.
                            continue;
                        }

                        ItemSheet.Row row = itemSheet[reward.ItemId];
                        ItemBase item = row is MaterialItemSheet.Row materialRow
                            ? ItemFactory.CreateTradableMaterial(materialRow)
                            : ItemFactory.CreateItem(row, context.Random);
                        avatarState.inventory.AddItem(item, (int)quantity * itemRewardStep);
                        break;
                    case StakeRegularRewardSheet.StakeRewardType.Rune:
                        var runeReward = runeRewardStep *
                                         RuneHelper.CalculateStakeReward(stakedAmount, reward.Rate);
                        if (runeReward < 1 * RuneHelper.StakeRune)
                        {
                            continue;
                        }

                        states = states.MintAsset(AvatarAddress, runeReward);
                        break;
                    case StakeRegularRewardSheet.StakeRewardType.Currency:
                        if (string.IsNullOrEmpty(reward.CurrencyTicker))
                        {
                            throw new NullReferenceException("currency ticker is null or empty");
                        }

                        var rewardCurrency =
                            Currencies.GetMinterlessCurrency(reward.CurrencyTicker);
                        var rewardCurrencyQuantity =
                            stakedAmount.DivRem(reward.Rate * stakedAmount.Currency).Quotient;
                        if (rewardCurrencyQuantity <= 0)
                        {
                            continue;
                        }

                        states = states.MintAsset(
                            context.Signer,
                            rewardCurrencyQuantity * currencyRewardStep * rewardCurrency);
                        break;
                    default:
                        break;
                }
            }

            // Fixed Reward
            foreach (var reward in fixedReward)
            {
                ItemSheet.Row row = itemSheet[reward.ItemId];
                ItemBase item = row is MaterialItemSheet.Row materialRow
                    ? ItemFactory.CreateTradableMaterial(materialRow)
                    : ItemFactory.CreateItem(row, context.Random);
                avatarState.inventory.AddItem(item, reward.Count * itemRewardStep);
            }

            return states;
        }

        public override IAccountStateDelta Execute(IActionContext context)
        {
            context.UseGas(1);
            if (context.Rehearsal)
            {
                return context.PreviousStates;
            }

            var states = context.PreviousStates;
            CheckActionAvailable(ClaimStakeReward2.ObsoletedIndex, context);
            // TODO: Uncomment this when new version of action is created
            // CheckObsolete(ObsoletedIndex, context);
            var addressesHex = GetSignerAndOtherAddressesHex(context, AvatarAddress);
            if (!states.TryGetStakeState(context.Signer, out StakeState stakeState))
            {
                throw new FailedLoadStateException(
                    ActionTypeText,
                    addressesHex,
                    typeof(StakeState),
                    StakeState.DeriveAddress(context.Signer));
            }

            if (!stakeState.IsClaimable(context.BlockIndex, out _, out _))
            {
                throw new RequiredBlockIndexException(
                    ActionTypeText,
                    addressesHex,
                    context.BlockIndex);
            }

            if (!states.TryGetAvatarStateV2(
                    context.Signer,
                    AvatarAddress,
                    out var avatarState,
                    out var migrationRequired))
            {
                throw new FailedLoadStateException(
                    ActionTypeText,
                    addressesHex,
                    typeof(AvatarState),
                    AvatarAddress);
            }

            var sheets = states.GetSheets(sheetTypes: new[]
            {
                typeof(StakeRegularRewardSheet),
                typeof(ConsumableItemSheet),
                typeof(CostumeItemSheet),
                typeof(EquipmentItemSheet),
                typeof(MaterialItemSheet),
            });

            var currency = states.GetGoldCurrency();
            var stakedAmount = states.GetBalance(stakeState.address, currency);
            var stakeRegularRewardSheet = sheets.GetSheet<StakeRegularRewardSheet>();
            var level =
                stakeRegularRewardSheet.FindLevelByStakedAmount(context.Signer, stakedAmount);
            var itemSheet = sheets.GetItemSheet();
            stakeState.CalculateAccumulatedItemRewards(
                context.BlockIndex,
                out var itemV1Step,
                out var itemV2Step);
            stakeState.CalculateAccumulatedRuneRewards(
                context.BlockIndex,
                out var runeV1Step,
                out var runeV2Step);
            stakeState.CalculateAccumulatedCurrencyRewards(
                context.BlockIndex,
                out var currencyV2Step);
            if (itemV1Step > 0)
            {
                var v1Level = Math.Min(level, V1.MaxLevel);
                var regularFixedSheetV1Row = (StakeRegularFixedRewardSheet)_stakeRewardHistoryDict[
                    "StakeRegularFixedRewardSheet"][1];
                var fixedRewardV1 = regularFixedSheetV1Row[v1Level].Rewards;
                var regularSheetV1Row = (StakeRegularRewardSheet)_stakeRewardHistoryDict[
                    "StakeRegularRewardSheet"][1];
                var regularRewardV1 = regularSheetV1Row[v1Level].Rewards;
                states = ProcessReward(
                    context,
                    states,
                    ref avatarState,
                    itemSheet,
                    stakedAmount,
                    itemV1Step,
                    runeV1Step,
                    0,
                    fixedRewardV1,
                    regularRewardV1);
            }

            if (itemV2Step > 0)
            {
                var regularFixedReward =
                    states.TryGetSheet<StakeRegularFixedRewardSheet>(out var fixedRewardSheet)
                        ? fixedRewardSheet[level].Rewards
                        : new List<StakeRegularFixedRewardSheet.RewardInfo>();
                var regularReward = sheets.GetSheet<StakeRegularRewardSheet>()[level].Rewards;
                states = ProcessReward(
                    context,
                    states,
                    ref avatarState,
                    itemSheet,
                    stakedAmount,
                    itemV2Step,
                    runeV2Step,
                    currencyV2Step,
                    regularFixedReward,
                    regularReward);
            }

            stakeState.Claim(context.BlockIndex);

            if (migrationRequired)
            {
                states = states
                    .SetState(avatarState.address, avatarState.SerializeV2())
                    .SetState(
                        avatarState.address.Derive(LegacyWorldInformationKey),
                        avatarState.worldInformation.Serialize())
                    .SetState(
                        avatarState.address.Derive(LegacyQuestListKey),
                        avatarState.questList.Serialize());
            }

            return states
                .SetState(stakeState.address, stakeState.Serialize())
                .SetState(
                    avatarState.address.Derive(LegacyInventoryKey),
                    avatarState.inventory.Serialize());
        }

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            ImmutableDictionary<string, IValue>.Empty
                .Add(AvatarAddressKey, AvatarAddress.Serialize());

        protected override void LoadPlainValueInternal(
            IImmutableDictionary<string, IValue> plainValue)
        {
            AvatarAddress = plainValue[AvatarAddressKey].ToAddress();
        }
    }
}
