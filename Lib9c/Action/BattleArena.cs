using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Nekoyume.Arena;
using Nekoyume.Battle;
using Nekoyume.Extensions;
using Nekoyume.Helper;
using Nekoyume.Model.Arena;
using Nekoyume.Model.BattleStatus;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using static Lib9c.SerializeKeys;

namespace Nekoyume.Action
{
    /// <summary>
    /// Introduced at https://github.com/planetarium/lib9c/pull/1006
    /// </summary>
    [Serializable]
    [ActionType("battle_arena")]
    public class BattleArena : GameAction
    {
        public Address myAvatarAddress;
        public Address enemyAvatarAddress;
        public int championshipId;
        public int round;
        public int ticket;

        public List<Guid> costumes;
        public List<Guid> equipments;

        public readonly Dictionary<ArenaType, (int, int)> ScoreLimits =
            new Dictionary<ArenaType, (int, int)>()
            {
                { ArenaType.Season, (50, -25) },
                { ArenaType.Championship, (30, -25) }
            };

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            new Dictionary<string, IValue>()
            {
                ["myAvatarAddress"] = myAvatarAddress.Serialize(),
                ["enemyAvatarAddress"] = enemyAvatarAddress.Serialize(),
                ["championshipId"] = championshipId.Serialize(),
                ["round"] = round.Serialize(),
                ["ticket"] = ticket.Serialize(),
                ["costumes"] = new List(costumes
                    .OrderBy(element => element).Select(e => e.Serialize())),
                ["equipments"] = new List(equipments
                    .OrderBy(element => element).Select(e => e.Serialize())),
            }.ToImmutableDictionary();

        protected override void LoadPlainValueInternal(
            IImmutableDictionary<string, IValue> plainValue)
        {
            myAvatarAddress = plainValue["myAvatarAddress"].ToAddress();
            enemyAvatarAddress = plainValue["enemyAvatarAddress"].ToAddress();
            championshipId = plainValue["championshipId"].ToInteger();
            round = plainValue["round"].ToInteger();
            ticket = plainValue["ticket"].ToInteger();
            costumes = ((List)plainValue["costumes"]).Select(e => e.ToGuid()).ToList();
            equipments = ((List)plainValue["equipments"]).Select(e => e.ToGuid()).ToList();
        }

        public override IAccountStateDelta Execute(IActionContext context)
        {
            var states = context.PreviousStates;
            if (context.Rehearsal)
            {
                return states;
            }

            var addressesHex =
                GetSignerAndOtherAddressesHex(context, myAvatarAddress, enemyAvatarAddress);

            if (myAvatarAddress.Equals(enemyAvatarAddress))
            {
                throw new InvalidAddressException(
                    $"{addressesHex}Aborted as the signer tried to battle for themselves.");
            }

            if (!states.TryGetAvatarStateV2(context.Signer, myAvatarAddress,
                    out var avatarState, out var _))
            {
                throw new FailedLoadStateException(
                    $"{addressesHex}Aborted as the avatar state of the signer was failed to load.");
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
                containArenaSimulatorSheets: true,
                sheetTypes: new[]
                {
                    typeof(ArenaSheet),
                    typeof(ItemRequirementSheet),
                    typeof(EquipmentItemRecipeSheet),
                    typeof(EquipmentItemSubRecipeSheetV2),
                    typeof(EquipmentItemOptionSheet),
                    typeof(MaterialItemSheet),
                });

            avatarState.ValidEquipmentAndCostume(costumes, equipments,
                sheets.GetSheet<ItemRequirementSheet>(),
                sheets.GetSheet<EquipmentItemRecipeSheet>(),
                sheets.GetSheet<EquipmentItemSubRecipeSheetV2>(),
                sheets.GetSheet<EquipmentItemOptionSheet>(),
                context.BlockIndex, addressesHex);

            var arenaSheet = sheets.GetSheet<ArenaSheet>();
            if (!arenaSheet.TryGetValue(championshipId, out var arenaRow))
            {
                throw new SheetRowNotFoundException(nameof(ArenaSheet),
                    $"championship Id : {championshipId}");
            }

            if (!arenaRow.IsTheRoundOpened(context.BlockIndex, championshipId, round))
            {
                throw new RoundNotFoundByBlockIndexException(
                    $"{nameof(BattleArena)} : block index({context.BlockIndex}) - " +
                    $"championshipId({championshipId}) - round({round})");
            }

            if (!arenaRow.TryGetRound(championshipId, round, out var roundData))
            {
                throw new RoundNotFoundByIdsException(
                    $"[{nameof(BattleArena)}] ChampionshipId({championshipId}) - round({round})");
            }

            var arenaParticipantsAdr = ArenaParticipants.DeriveAddress(roundData.Id, roundData.Round);
            if (!states.TryGetArenaParticipants(arenaParticipantsAdr, out var arenaParticipants))
            {
                throw new ArenaParticipantsNotFoundException(
                    $"[{nameof(BattleArena)}] ChampionshipId({roundData.Id}) - round({roundData.Round})");
            }

            if (!arenaParticipants.AvatarAddresses.Contains(myAvatarAddress))
            {
                throw new AddressNotFoundInArenaParticipantsException(
                    $"[{nameof(BattleArena)}] my avatar address : {myAvatarAddress}");
            }

            if (!arenaParticipants.AvatarAddresses.Contains(enemyAvatarAddress))
            {
                throw new AddressNotFoundInArenaParticipantsException(
                    $"[{nameof(BattleArena)}] enemy avatar address : {enemyAvatarAddress}");
            }

            var myArenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(myAvatarAddress);
            if (!states.TryGetArenaAvatarState(myArenaAvatarStateAdr, out var myArenaAvatarState))
            {
                throw new ArenaAvatarStateNotFoundException(
                    $"[{nameof(BattleArena)}] my avatar address : {myAvatarAddress}");
            }

            var enemyArenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(enemyAvatarAddress);
            if (!states.TryGetArenaAvatarState(enemyArenaAvatarStateAdr,
                    out var enemyArenaAvatarState))
            {
                throw new ArenaAvatarStateNotFoundException(
                    $"[{nameof(BattleArena)}] enemy avatar address : {enemyAvatarAddress}");
            }

            var myArenaScoreAdr =
                ArenaScore.DeriveAddress(myAvatarAddress, roundData.Id, roundData.Round);
            if (!states.TryGetArenaScore(myArenaScoreAdr, out var myArenaScore))
            {
                throw new ArenaScoreNotFoundException(
                    $"[{nameof(BattleArena)}] my avatar address : {myAvatarAddress}" +
                    $" - ChampionshipId({roundData.Id}) - round({roundData.Round})");
            }

            var enemyArenaScoreAdr =
                ArenaScore.DeriveAddress(enemyAvatarAddress, roundData.Id, roundData.Round);
            if (!states.TryGetArenaScore(enemyArenaScoreAdr, out var enemyArenaScore))
            {
                throw new ArenaScoreNotFoundException(
                    $"[{nameof(BattleArena)}] enemy avatar address : {enemyAvatarAddress}" +
                    $" - ChampionshipId({roundData.Id}) - round({roundData.Round})");
            }

            var arenaInformationAdr = ArenaInformation.DeriveAddress(myAvatarAddress, roundData.Id, roundData.Round);
            if (!states.TryGetArenaInformation(arenaInformationAdr, out var arenaInformation))
            {
                throw new ArenaInformationNotFoundException(
                    $"[{nameof(BattleArena)}] my avatar address : {myAvatarAddress}" +
                    $" - ChampionshipId({roundData.Id}) - round({roundData.Round})");
            }

            if (!ArenaHelper.ValidateScoreDifference(ScoreLimits, roundData.ArenaType,
                    myArenaScore.Score, enemyArenaScore.Score))
            {
                var diff = enemyArenaScore.Score - myArenaScore.Score;
                throw new ValidateScoreDifferenceException(
                    $"[{nameof(BattleArena)}] Arena Type({roundData.ArenaType}) : " +
                    $"enemyScore({enemyArenaScore.Score}) - myScore({myArenaScore.Score}) = diff({diff})");
            }

            // update arena avatar state
            myArenaAvatarState.UpdateEquipment(equipments);
            myArenaAvatarState.UpdateCostumes(costumes);

            // simulate
            var enemyAvatarState = states.GetEnemyAvatarState(enemyAvatarAddress);
            var myDigest = new ArenaPlayerDigest(avatarState, myArenaAvatarState);
            var enemyDigest = new ArenaPlayerDigest(enemyAvatarState, enemyArenaAvatarState);
            var arenaSheets = sheets.GetArenaSimulatorSheets();
            var winCount = 0;
            var defeatCount = 0;
            var rewards = new List<ItemBase>();

            for (var i = 0; i < ticket; i++)
            {
                var simulator = new ArenaSimulator(context.Random, myDigest, enemyDigest, arenaSheets);
                simulator.Simulate();

                if (simulator.Result.Equals(BattleLog.Result.Win))
                {
                    winCount++;
                }
                else
                {
                    defeatCount++;
                }

                var reward = RewardSelector.Select(
                    context.Random,
                    sheets.GetSheet<WeeklyArenaRewardSheet>(),
                    sheets.GetSheet<MaterialItemSheet>(),
                    myDigest.Level,
                    maxCount:GetRewardCount(myArenaScore.Score));
                rewards.AddRange(reward);
            }

            // add reward
            foreach (var itemBase in rewards.OrderBy(x => x.Id))
            {
                avatarState.inventory.AddItem(itemBase);
            }

            // add medal
            if (roundData.ArenaType != ArenaType.OffSeason)
            {
                var materialSheet = sheets.GetSheet<MaterialItemSheet>();
                var medal = ArenaHelper.GetMedal(roundData.Id, roundData.Round, materialSheet);
                avatarState.inventory.AddItem(medal, count: winCount);
            }

            // update arena score
            var (myWinScore, myDefeatScore, enemyWinScore) = GetScores(myArenaScore.Score, enemyArenaScore.Score);
            var myScore = (myWinScore * winCount) + (myDefeatScore * defeatCount);
            myArenaScore.AddScore(myScore);
            enemyArenaScore.AddScore(enemyWinScore * winCount);

            // update arena information
            arenaInformation.UseTicket(ticket);
            arenaInformation.UpdateRecord(winCount, defeatCount);

            var inventoryAddress = myAvatarAddress.Derive(LegacyInventoryKey);
            var questListAddress = myAvatarAddress.Derive(LegacyQuestListKey);

            return states
                .SetState(myArenaAvatarStateAdr, myArenaAvatarState.Serialize())
                .SetState(myArenaScoreAdr, myArenaScore.Serialize())
                .SetState(enemyArenaScoreAdr, enemyArenaScore.Serialize())
                .SetState(arenaInformationAdr, arenaInformation.Serialize())
                .SetState(inventoryAddress, avatarState.inventory.Serialize())
                .SetState(questListAddress, avatarState.questList.Serialize())
                .SetState(myAvatarAddress, avatarState.SerializeV2());
        }

        public static (int, int, int) GetScores(int myScore, int enemyScore)
        {
            var (myWinScore, enemyWinScore) = ArenaScoreHelper.GetScore(
                myScore, enemyScore, BattleLog.Result.Win);

            var (myDefeatScore, _) = ArenaScoreHelper.GetScore(
                myScore, enemyScore, BattleLog.Result.Lose);

            return (myWinScore, myDefeatScore, enemyWinScore);
        }

        public static int GetRewardCount(int score)
        {
            if (score >= 1800)
            {
                return 6;
            }

            if (score >= 1400)
            {
                return 5;
            }

            if (score >= 1200)
            {
                return 4;
            }

            if (score >= 1100)
            {
                return 3;
            }

            if (score >= 1001)
            {
                return 2;
            }

            return 1;
        }

    }
}
