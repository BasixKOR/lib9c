using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bencodex.Types;
using Lib9c.Abstractions;
using Libplanet;
using Libplanet.Action;
using Libplanet.State;
using Nekoyume.Arena;
using Nekoyume.Extensions;
using Nekoyume.Helper;
using Nekoyume.Model.Arena;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.Rune;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Serilog;

namespace Nekoyume.Action
{
    /// <summary>
    /// Introduced at https://github.com/planetarium/lib9c/pull/1663
    /// </summary>
    [Serializable]
    [ActionType("join_arena3")]
    public class JoinArena : GameAction, IJoinArenaV1
    {
        public Address avatarAddress;
        public int championshipId;
        public int round;
        public List<Guid> costumes;
        public List<Guid> equipments;
        public List<RuneSlotInfo> runeInfos;

        Address IJoinArenaV1.AvatarAddress => avatarAddress;
        int IJoinArenaV1.ChampionshipId => championshipId;
        int IJoinArenaV1.Round => round;
        IEnumerable<Guid> IJoinArenaV1.Costumes => costumes;
        IEnumerable<Guid> IJoinArenaV1.Equipments => equipments;
        IEnumerable<IValue> IJoinArenaV1.RuneSlotInfos => runeInfos
            .Select(x => x.Serialize());

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            new Dictionary<string, IValue>()
            {
                ["avatarAddress"] = avatarAddress.Serialize(),
                ["championshipId"] = championshipId.Serialize(),
                ["round"] = round.Serialize(),
                ["costumes"] = new List(costumes
                    .OrderBy(element => element).Select(e => e.Serialize())),
                ["equipments"] = new List(equipments
                    .OrderBy(element => element).Select(e => e.Serialize())),
                ["runeInfos"] = runeInfos.OrderBy(x => x.SlotIndex).Select(x=> x.Serialize()).Serialize(),
            }.ToImmutableDictionary();

        protected override void LoadPlainValueInternal(
            IImmutableDictionary<string, IValue> plainValue)
        {
            avatarAddress = plainValue["avatarAddress"].ToAddress();
            championshipId = plainValue["championshipId"].ToInteger();
            round = plainValue["round"].ToInteger();
            costumes = ((List)plainValue["costumes"]).Select(e => e.ToGuid()).ToList();
            equipments = ((List)plainValue["equipments"]).Select(e => e.ToGuid()).ToList();
            runeInfos = plainValue["runeInfos"].ToList(x => new RuneSlotInfo((List)x));
        }

        public override IAccountStateDelta Execute(IActionContext context)
        {
            context.UseGas(1);
            var states = context.PreviousStates;
            if (context.Rehearsal)
            {
                return states;
            }

            var addressesHex = GetSignerAndOtherAddressesHex(context, avatarAddress);
            var started = DateTimeOffset.UtcNow;
            Log.Debug("{AddressesHex}JoinArena exec started", addressesHex);

            if (!states.TryGetAgentAvatarStatesV2(context.Signer, avatarAddress,
                    out var agentState, out var avatarState, out _))
            {
                throw new FailedLoadStateException(
                    $"[{nameof(JoinArena)}] Aborted as the avatar state of the signer failed to load.");
            }

            if (!avatarState.worldInformation.TryGetUnlockedWorldByStageClearedBlockIndex(
                    out var world))
            {
                throw new NotEnoughClearedStageLevelException(
                    $"{addressesHex}Aborted as NotEnoughClearedStageLevelException");
            }

            if (world.StageClearedId < GameConfig.RequireClearedStageLevel.ActionsInRankingBoard)
            {
                throw new NotEnoughClearedStageLevelException(
                    addressesHex,
                    GameConfig.RequireClearedStageLevel.ActionsInRankingBoard,
                    world.StageClearedId);
            }

            var sheets = states.GetSheets(
                sheetTypes: new[]
                {
                    typeof(ItemRequirementSheet),
                    typeof(EquipmentItemRecipeSheet),
                    typeof(EquipmentItemSubRecipeSheetV2),
                    typeof(EquipmentItemOptionSheet),
                    typeof(ArenaSheet),
                    typeof(RuneListSheet),
                });

            avatarState.ValidEquipmentAndCostume(costumes, equipments,
                sheets.GetSheet<ItemRequirementSheet>(),
                sheets.GetSheet<EquipmentItemRecipeSheet>(),
                sheets.GetSheet<EquipmentItemSubRecipeSheetV2>(),
                sheets.GetSheet<EquipmentItemOptionSheet>(),
                context.BlockIndex, addressesHex);

            // update rune slot
            var runeSlotStateAddress = RuneSlotState.DeriveAddress(avatarAddress, BattleType.Arena);
            var runeSlotState = states.TryGetState(runeSlotStateAddress, out List rawRuneSlotState)
                ? new RuneSlotState(rawRuneSlotState)
                : new RuneSlotState(BattleType.Arena);
            var runeListSheet = sheets.GetSheet<RuneListSheet>();
            runeSlotState.UpdateSlot(runeInfos, runeListSheet);
            states = states.SetState(runeSlotStateAddress, runeSlotState.Serialize());

            // update item slot
            var itemSlotStateAddress = ItemSlotState.DeriveAddress(avatarAddress, BattleType.Arena);
            var itemSlotState = states.TryGetState(itemSlotStateAddress, out List rawItemSlotState)
                ? new ItemSlotState(rawItemSlotState)
                : new ItemSlotState(BattleType.Arena);
            itemSlotState.UpdateEquipment(equipments);
            itemSlotState.UpdateCostumes(costumes);
            states = states.SetState(itemSlotStateAddress, itemSlotState.Serialize());

            var sheet = sheets.GetSheet<ArenaSheet>();
            if (!sheet.TryGetValue(championshipId, out var row))
            {
                throw new SheetRowNotFoundException(
                    nameof(ArenaSheet), $"championship Id : {championshipId}");
            }

            if (!row.TryGetRound(round, out var roundData))
            {
                throw new RoundNotFoundException(
                    $"[{nameof(JoinArena)}] ChampionshipId({row.ChampionshipId}) - round({round})");
            }

            // check fee

            var fee = ArenaHelper.GetEntranceFee(roundData, context.BlockIndex, avatarState.level);
            if (fee > 0 * CrystalCalculator.CRYSTAL)
            {
                var crystalBalance = states.GetBalance(context.Signer, CrystalCalculator.CRYSTAL);
                if (fee > crystalBalance)
                {
                    throw new NotEnoughFungibleAssetValueException(
                        $"required {fee}, but balance is {crystalBalance}");
                }

                var arenaAdr = ArenaHelper.DeriveArenaAddress(roundData.ChampionshipId, roundData.Round);
                states = states.TransferAsset(context.Signer, arenaAdr, fee);
            }

            // check medal
            if (roundData.ArenaType.Equals(ArenaType.Championship))
            {
                var medalCount = ArenaHelper.GetMedalTotalCount(row, avatarState);
                if (medalCount < roundData.RequiredMedalCount)
                {
                    throw new NotEnoughMedalException(
                        $"[{nameof(JoinArena)}] have({medalCount}) < Required Medal Count({roundData.RequiredMedalCount}) ");
                }
            }

            // create ArenaScore
            var arenaScoreAdr =
                ArenaScore.DeriveAddress(avatarAddress, roundData.ChampionshipId, roundData.Round);
            if (states.TryGetState(arenaScoreAdr, out List _))
            {
                throw new ArenaScoreAlreadyContainsException(
                    $"[{nameof(JoinArena)}] id({roundData.ChampionshipId}) / round({roundData.Round})");
            }

            var arenaScore = new ArenaScore(avatarAddress, roundData.ChampionshipId, roundData.Round);

            // create ArenaInformation
            var arenaInformationAdr =
                ArenaInformation.DeriveAddress(avatarAddress, roundData.ChampionshipId, roundData.Round);
            if (states.TryGetState(arenaInformationAdr, out List _))
            {
                throw new ArenaInformationAlreadyContainsException(
                    $"[{nameof(JoinArena)}] id({roundData.ChampionshipId}) / round({roundData.Round})");
            }

            var arenaInformation =
                new ArenaInformation(avatarAddress, roundData.ChampionshipId, roundData.Round);

            // update ArenaParticipants
            var arenaParticipantsAdr = ArenaParticipants.DeriveAddress(roundData.ChampionshipId, roundData.Round);
            var arenaParticipants = states.GetArenaParticipants(arenaParticipantsAdr, roundData.ChampionshipId, roundData.Round);
            arenaParticipants.Add(avatarAddress);

            // update ArenaAvatarState
            var arenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(avatarAddress);
            var arenaAvatarState = states.GetArenaAvatarState(arenaAvatarStateAdr, avatarState);
            arenaAvatarState.UpdateCostumes(costumes);
            arenaAvatarState.UpdateEquipment(equipments);

            var ended = DateTimeOffset.UtcNow;
            Log.Debug("{AddressesHex}JoinArena Total Executed Time: {Elapsed}", addressesHex, ended - started);
            return states
                .SetState(arenaScoreAdr, arenaScore.Serialize())
                .SetState(arenaInformationAdr, arenaInformation.Serialize())
                .SetState(arenaParticipantsAdr, arenaParticipants.Serialize())
                .SetState(arenaAvatarStateAdr, arenaAvatarState.Serialize())
                .SetState(context.Signer, agentState.Serialize());
        }
    }
}
