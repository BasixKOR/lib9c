namespace Lib9c.Tests.TableData
{
    using System;
    using Nekoyume.TableData;
    using Xunit;

    public class WorldBossRankingRewardSheetTest
    {
        private const string Csv =
            @"id,boss_id,ranking_min,ranking_max,rate_min,rate_max,rune_1_id,rune_1_qty,rune_2_id,rune_2_qty,rune_3_id,rune_3_qty,crystal,item_1_id,item_1_qty,item_2_id,item_2_qty
1,900001,1,1,0,0,10001,3500,10002,1200,10003,300,10000000,600201,100,500000,1000
2,900001,2,2,0,0,10001,2200,10002,650,10003,150,7500000,600201,100,500000,1000
3,900001,3,3,0,0,10001,1450,10002,450,10003,100,5000000,600201,100,500000,1000
4,900001,4,10,0,0,10001,1000,10002,330,10003,70,2500000,600201,100,500000,1000
5,900001,11,100,0,0,10001,560,10002,150,10003,40,1500000,600201,100,500000,1000
6,900001,0,0,1,30,10001,370,10002,105,10003,25,500000,600201,50,500000,100
7,900001,0,0,31,50,10001,230,10002,60,10003,10,250000,600201,50,500000,100
8,900001,0,0,51,70,10001,75,10002,20,10003,5,125000,600201,50,500000,100
9,900001,0,0,71,100,10001,40,10002,10,0,0,100000,600201,50,500000,100
10,900002,1,1,0,0,10011,3500,10012,1200,10013,300,10000000,600201,100,500000,1000
11,900002,2,2,0,0,10011,2200,10012,650,10013,150,7500000,600201,100,500000,1000
12,900002,3,3,0,0,10011,1450,10012,450,10013,100,5000000,600201,100,500000,1000
13,900002,4,10,0,0,10011,1000,10012,330,10013,70,2500000,600201,100,500000,1000
14,900002,11,100,0,0,10011,560,10012,150,10013,40,1500000,600201,100,500000,1000
15,900002,0,0,1,30,10011,370,10012,105,10013,25,500000,600201,50,500000,100
16,900002,0,0,31,50,10011,230,10012,60,10013,10,250000,600201,50,500000,100
17,900002,0,0,51,70,10011,75,10012,20,10013,5,125000,600201,50,0,0
18,900002,0,0,71,100,10011,40,10012,10,0,0,100000,600201,50,,
";

        [Fact]
        public void Set()
        {
            var sheet = new WorldBossRankingRewardSheet();
            sheet.Set(Csv);
            var row = sheet[1];
            Assert.Equal(1, row.Id);
            Assert.Equal(900001, row.BossId);
            Assert.Equal(1, row.RankingMin);
            Assert.Equal(1, row.RankingMax);
            Assert.Equal(0, row.RateMin);
            Assert.Equal(0, row.RateMax);
            Assert.Equal(3, row.Runes.Count);
            Assert.Equal(10000000, row.Crystal);
            Assert.Equal(600201, row.Materials[0].itemId);
            Assert.Equal(100, row.Materials[0].quantity);
            Assert.Equal(500000, row.Materials[1].itemId);
            Assert.Equal(1000, row.Materials[1].quantity);
        }

        [Theory]
        [InlineData(900001, 1, 0, 1)]
        [InlineData(900001, 1000, 1, 6)]
        [InlineData(900002, 1, 0, 10)]
        [InlineData(900002, 1000, 1, 15)]
        public void FindRow(int bossId, int ranking, int rate, int expected)
        {
            var sheet = new WorldBossRankingRewardSheet();
            sheet.Set(Csv);
            var row = sheet.FindRow(bossId, ranking, rate);
            Assert.Equal(expected, row.Id);
        }

        [Fact]
        public void FIndRow_Throw_ArgumentException()
        {
            var sheet = new WorldBossRankingRewardSheet();
            Assert.Throws<ArgumentException>(() => sheet.FindRow(900003, 0, 0));
        }

        [Theory]
        [InlineData(900001, 1, 0, 6)]
        [InlineData(900002, 600, 60, 5)]
        [InlineData(900002, 800, 80, 4)]
        public void GetRewards(int bossId, int ranking, int rate, int expected)
        {
            var sheet = new WorldBossRankingRewardSheet();
            sheet.Set(Csv);
            var row = sheet.FindRow(bossId, ranking, rate);
            var tableSheets = new TableSheets(TableSheetsImporter.ImportSheets());
            var rewards = row.GetRewards(tableSheets.RuneSheet, tableSheets.MaterialItemSheet);

            Assert.Equal(expected, rewards.Count);
            Assert.All(rewards, fav => Assert.True(fav.Sign > 0));
        }
    }
}
