namespace Lib9c.Tests.Action
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Libplanet;
    using Libplanet.Action;
    using Libplanet.Assets;
    using Libplanet.Crypto;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Arena;
    using Nekoyume.Model;
    using Nekoyume.Model.Arena;
    using Nekoyume.Model.EnumType;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.State;
    using Nekoyume.TableData;
    using Serilog;
    using Xunit;
    using Xunit.Abstractions;
    using static Lib9c.SerializeKeys;

    public class BattleArenaTest
    {
        private readonly Dictionary<string, string> _sheets;
        private readonly TableSheets _tableSheets;

        private readonly Address _agent1Address;
        private readonly Address _agent2Address;
        private readonly Address _avatar1Address;
        private readonly Address _avatar2Address;
        private readonly AvatarState _avatar1;
        private readonly AvatarState _avatar2;
        private readonly Currency _currency;
        private IAccountStateDelta _state;

        public BattleArenaTest(ITestOutputHelper outputHelper)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(outputHelper)
                .CreateLogger();

            _state = new State();

            _sheets = TableSheetsImporter.ImportSheets();
            var tableSheets = new TableSheets(_sheets);
            foreach (var (key, value) in _sheets)
            {
                _state = _state.SetState(Addresses.TableSheet.Derive(key), value.Serialize());
            }

            _tableSheets = new TableSheets(_sheets);
            _currency = new Currency("CRYSTAL", 18, minters: null);
            var rankingMapAddress = new PrivateKey().ToAddress();
            var (agent1State, avatar1State) = GetAgentStateWithAvatarState(
                _sheets,
                _tableSheets,
                rankingMapAddress);

            _agent1Address = agent1State.address;
            _avatar1 = avatar1State;
            _avatar1Address = avatar1State.address;

            var (agent2State, avatar2State) = GetAgentStateWithAvatarState(
                _sheets,
                _tableSheets,
                rankingMapAddress);
            _agent2Address = agent2State.address;
            _avatar2 = avatar2State;
            _avatar2Address = avatar2State.address;

            var weeklyAddressList = new List<Address>
            {
                _avatar1Address,
                _avatar2Address,
            };
            _state = _state
                .SetState(_agent1Address, agent1State.Serialize())
                .SetState(_avatar1Address.Derive(LegacyInventoryKey), _avatar1.inventory.Serialize())
                .SetState(_avatar1Address.Derive(LegacyWorldInformationKey), _avatar1.worldInformation.Serialize())
                .SetState(_avatar1Address.Derive(LegacyQuestListKey), _avatar1.questList.Serialize())
                .SetState(_avatar1Address, _avatar1.Serialize())
                .SetState(_agent2Address, agent2State.Serialize())
                .SetState(_avatar2Address, avatar2State.Serialize())
                .SetState(Addresses.GameConfig, new GameConfigState(_sheets[nameof(GameConfigSheet)]).Serialize());

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(outputHelper)
                .CreateLogger();
        }

        public static (AgentState AgentState, AvatarState AvatarState) GetAgentStateWithAvatarState(
            IReadOnlyDictionary<string, string> sheets,
            TableSheets tableSheets,
            Address rankingMapAddress)
        {
            var agentAddress = new PrivateKey().ToAddress();
            var agentState = new AgentState(agentAddress);

            var avatarAddress = agentAddress.Derive("avatar");
            var avatarState = new AvatarState(
                avatarAddress,
                agentAddress,
                0,
                tableSheets.GetAvatarSheets(),
                new GameConfigState(sheets[nameof(GameConfigSheet)]),
                rankingMapAddress)
            {
                worldInformation = new WorldInformation(
                    0,
                    tableSheets.WorldSheet,
                    Math.Max(
                        tableSheets.StageSheet.First?.Id ?? 1,
                        GameConfig.RequireClearedStageLevel.ActionsInRankingBoard)),
            };
            agentState.avatarAddresses.Add(0, avatarAddress);

            return (agentState, avatarState);
        }

        public (List<Guid> Equipments, List<Guid> Costumes) GetDummyItems(AvatarState avatarState)
        {
            var items = Doomfist.GetAllParts(_tableSheets, avatarState.level);
            foreach (var equipment in items)
            {
                avatarState.inventory.AddItem(equipment);
            }

            var equipments = items.Select(e => e.NonFungibleId).ToList();

            var random = new TestRandom();
            var costumes = new List<Guid>();
            if (avatarState.level >= GameConfig.RequireCharacterLevel.CharacterFullCostumeSlot)
            {
                var costumeId = _tableSheets
                    .CostumeItemSheet
                    .Values
                    .First(r => r.ItemSubType == ItemSubType.FullCostume)
                    .Id;

                var costume = (Costume)ItemFactory.CreateItem(
                    _tableSheets.ItemSheet[costumeId], random);
                avatarState.inventory.AddItem(costume);
                costumes.Add(costume.ItemId);
            }

            return (equipments, costumes);
        }

        public IAccountStateDelta JoinArena(Address signer, Address avatarAddress, long blockIndex, int championshipId, int round, IRandom random)
        {
            var preCurrency = 1000 * _currency;
            _state = _state.MintAsset(signer, preCurrency);

            var action = new JoinArena()
            {
                championshipId = championshipId,
                round = round,
                costumes = new List<Guid>(),
                equipments = new List<Guid>(),
                avatarAddress = avatarAddress,
            };

            _state = action.Execute(new ActionContext
            {
                PreviousStates = _state,
                Signer = signer,
                Random = random,
                Rehearsal = false,
                BlockIndex = blockIndex,
            });
            return _state;
        }

        [Theory]
        [InlineData(1, 1, 1, 3)]
        [InlineData(1, 1, 1, 4)]
        [InlineData(1, 1, 5, 0)]
        [InlineData(1, 1, 5, 1)]
        [InlineData(1, 2, 5, 0)]
        [InlineData(1, 2, 5, 1)]
        public void Execute(int championshipId, int round, int ticket, int randomSeed)
        {
            var arenaSheet = _state.GetSheet<ArenaSheet>();
            if (!arenaSheet.TryGetValue(championshipId, out var row))
            {
                throw new SheetRowNotFoundException(
                    nameof(ArenaSheet), $"championship Id : {championshipId}");
            }

            if (!row.TryGetRound(championshipId, round, out var roundData))
            {
                throw new RoundNotFoundByIdsException(
                    $"[{nameof(BattleArena)}] ChampionshipId({championshipId}) - round({round})");
            }

            var random = new TestRandom(randomSeed);
            _state = JoinArena(_agent1Address, _avatar1Address, roundData.StartBlockIndex, championshipId, round, random);
            _state = JoinArena(_agent2Address, _avatar2Address, roundData.StartBlockIndex, championshipId, round, random);

            var action = new BattleArena()
            {
                myAvatarAddress = _avatar1Address,
                enemyAvatarAddress = _avatar2Address,
                championshipId = championshipId,
                round = round,
                ticket = ticket,
                costumes = new List<Guid>(),
                equipments = new List<Guid>(),
            };

            var myScoreAdr = ArenaScore.DeriveAddress(_avatar1Address, championshipId, round);
            var enemyScoreAdr = ArenaScore.DeriveAddress(_avatar2Address, championshipId, round);
            var arenaInfoAdr = ArenaInformation.DeriveAddress(_avatar1Address, championshipId, round);
            if (!_state.TryGetArenaScore(myScoreAdr, out var beforeMyScore))
            {
                throw new ArenaScoreNotFoundException($"myScoreAdr : {myScoreAdr}");
            }

            if (!_state.TryGetArenaScore(enemyScoreAdr, out var beforeEnemyScore))
            {
                throw new ArenaScoreNotFoundException($"enemyScoreAdr : {enemyScoreAdr}");
            }

            if (!_state.TryGetArenaInformation(arenaInfoAdr, out var beforeInfo))
            {
                throw new ArenaInformationNotFoundException($"arenaInfoAdr : {arenaInfoAdr}");
            }

            Assert.Empty(_avatar1.inventory.Materials);

            _state = action.Execute(new ActionContext
            {
                PreviousStates = _state,
                Signer = _agent1Address,
                Random = random,
                Rehearsal = false,
                BlockIndex = roundData.StartBlockIndex + 1,
            });

            if (!_state.TryGetArenaScore(myScoreAdr, out var myAfterScore))
            {
                throw new ArenaScoreNotFoundException($"myScoreAdr : {myScoreAdr}");
            }

            if (!_state.TryGetArenaScore(enemyScoreAdr, out var enemyAfterScore))
            {
                throw new ArenaScoreNotFoundException($"enemyScoreAdr : {enemyScoreAdr}");
            }

            if (!_state.TryGetArenaInformation(arenaInfoAdr, out var afterInfo))
            {
                throw new ArenaInformationNotFoundException($"arenaInfoAdr : {arenaInfoAdr}");
            }

            var (myWinScore, myDefeatScore, enemyWinScore) =
                BattleArena.GetScores(beforeMyScore.Score, beforeEnemyScore.Score);

            var addMyScore = (afterInfo.Win * myWinScore) + (afterInfo.Lose * myDefeatScore);
            var addEnemyScore = afterInfo.Win * enemyWinScore;
            var expectedMyScore = Math.Max(beforeMyScore.Score + addMyScore, ArenaScore.ArenaScoreDefault);
            var expectedEnemyScore = Math.Max(beforeEnemyScore.Score + addEnemyScore, ArenaScore.ArenaScoreDefault);

            Assert.Equal(expectedMyScore, myAfterScore.Score);
            Assert.Equal(expectedEnemyScore, enemyAfterScore.Score);
            Assert.Equal(ArenaInformation.MaxTicketCount, beforeInfo.Ticket);
            Assert.Equal(0, beforeInfo.Win);
            Assert.Equal(0, beforeInfo.Lose);
            Assert.Equal(beforeInfo.Ticket - ticket, afterInfo.Ticket);
            Assert.Equal(beforeInfo.Ticket, afterInfo.Win + afterInfo.Lose + afterInfo.Ticket);

            var avatarState = _state.GetAvatarStateV2(_avatar1Address);
            var medalCount = 0;
            if (roundData.ArenaType != ArenaType.OffSeason)
            {
                var medalId = ArenaHelper.GetMedalItemId(championshipId, round);
                avatarState.inventory.TryGetItem(medalId, out var medal);
                Assert.Equal(afterInfo.Win, medal.count);
                medalCount = medal.count;
            }

            var materialCount = avatarState.inventory.Materials.Count();
            var high = (BattleArena.GetRewardCount(beforeMyScore.Score) * ticket) + medalCount;
            Assert.InRange(materialCount, 0, high);
        }
    }
}
