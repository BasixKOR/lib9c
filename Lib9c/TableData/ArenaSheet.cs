using System;
using System.Collections.Generic;
using System.Linq;
using Nekoyume.Model.Arena;
using Nekoyume.Model.EnumType;

namespace Nekoyume.TableData
{
    using static TableExtensions;

    [Serializable]
    public class ArenaSheet : Sheet<int, ArenaSheet.Row>
    {
        public class RoundData
        {
            public int Id { get; }
            public int Round { get; }
            public ArenaType? ArenaType { get; }
            public long StartBlockIndex { get; }
            public long EndBlockIndex { get; }
            public int RequiredMedalCount { get; }
            public long EntranceFee { get; }
            public long DiscountedEntranceFee { get; }
            public decimal TicketPrice { get; }
            public decimal AdditionalTicketPrice { get; }

            public RoundData(int id, int round, ArenaType arenaType,
                long startBlockIndex, long endBlockIndex,
                int requiredMedalCount,
                long entranceFee, long discountedEntranceFee,
                decimal ticketPrice, decimal additionalTicketPrice)
            {
                Id = id;
                Round = round;
                ArenaType = arenaType;
                StartBlockIndex = startBlockIndex;
                EndBlockIndex = endBlockIndex;
                RequiredMedalCount = requiredMedalCount;
                EntranceFee = entranceFee;
                DiscountedEntranceFee = discountedEntranceFee;
                TicketPrice = ticketPrice;
                AdditionalTicketPrice = additionalTicketPrice;
            }
        }

        [Serializable]
        public class Row : SheetRow<int>
        {
            public override int Key => Id;
            public int Id { get; private set; }
            public List<RoundData> Round { get; private set; }

            public override void Set(IReadOnlyList<string> fields)
            {
                Id = ParseInt(fields[0]);
                var round = ParseInt(fields[1]);
                var arenaType = (ArenaType)Enum.Parse(typeof(ArenaType), fields[2]);
                var startIndex = ParseLong(fields[3]);
                var endIndex = ParseLong(fields[4]);
                var requiredWins = ParseInt(fields[5]);
                var entranceFee = ParseLong(fields[6]);
                var discountedEntranceFee = ParseLong(fields[7]);
                var ticketPrice = ParseDecimal(fields[8]);
                var additionalTicketPrice = ParseDecimal(fields[9]);
                Round = new List<RoundData>
                {
                    new RoundData(Id, round, arenaType, startIndex, endIndex,
                        requiredWins, entranceFee, discountedEntranceFee,
                        ticketPrice, additionalTicketPrice)
                };
            }

            public bool TryGetRound(int championshipId, int round, out RoundData roundData)
            {
                roundData = Round.FirstOrDefault(x =>
                    x.Id.Equals(championshipId) && x.Round.Equals(round));
                return !(roundData is null);
            }

            public bool IsTheRoundOpened(long blockIndex, int championshipId, int round)
            {
                var roundData = Round.FirstOrDefault(x => x.StartBlockIndex <= blockIndex &&
                                                      blockIndex <= x.EndBlockIndex);
                if (roundData is null)
                {
                    throw new RoundNotFoundByBlockIndexException(
                        $"{nameof(ArenaSheet)} : block index({blockIndex})");
                }

                return roundData.Id.Equals(championshipId) && roundData.Round.Equals(round);
            }
        }

        public ArenaSheet() : base(nameof(ArenaSheet))
        {
        }

        protected override void AddRow(int key, Row value)
        {
            if (!TryGetValue(key, out var row))
            {
                Add(key, value);

                return;
            }

            if (!value.Round.Any())
            {
                return;
            }

            row.Round.Add(value.Round[0]);
        }
    }
}
