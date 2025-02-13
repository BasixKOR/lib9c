namespace Lib9c.Tests.Model.Item
{
    using System;
    using System.Collections.Generic;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.State;
    using Nekoyume.TableData;
    using Xunit;
    using static SerializeKeys;

    public class EquipmentTest
    {
        private readonly EquipmentItemSheet.Row _equipmentRow;

        public EquipmentTest()
        {
            var tableSheets = new TableSheets(TableSheetsImporter.ImportSheets());
            _equipmentRow = tableSheets.EquipmentItemSheet.First;
        }

        public static Equipment CreateFirstEquipment(
            TableSheets tableSheets,
            Guid guid = default,
            long requiredBlockIndex = default)
        {
            var row = tableSheets.EquipmentItemSheet.First;
            Assert.NotNull(row);

            return new Equipment(row, guid == default ? Guid.NewGuid() : guid, requiredBlockIndex);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(14176747520000)] // Max exp. of equipment
        public void Serialize(long exp)
        {
            Assert.NotNull(_equipmentRow);

            var costume = new Equipment(_equipmentRow, Guid.NewGuid(), 0);
            costume.Exp = exp;
            var serialized = costume.Serialize();
            var deserialized = new Equipment((Bencodex.Types.Dictionary)serialized);
            var reSerialized = deserialized.Serialize();

            if (exp > 0)
            {
                Assert.True(((Bencodex.Types.Dictionary)serialized).ContainsKey(EquipmentExpKey));
                Assert.True(((Bencodex.Types.Dictionary)serialized)[EquipmentExpKey].ToLong() > 0);
            }
            else
            {
                Assert.False(((Bencodex.Types.Dictionary)serialized).ContainsKey(EquipmentExpKey));
            }

            Assert.Equal(costume, deserialized);
            Assert.Equal(serialized, reSerialized);
        }

        [Fact]
        public void LevelUp()
        {
            var row = new EquipmentItemSheet.Row();
            row.Set(new List<string>() { "10100000", "Weapon", "0", "Normal", "0", "ATK", "1", "2", "10100000", });
            var equipment = (Equipment)ItemFactory.CreateItemUsable(row, default, 0, 0);

            Assert.Equal(1m, equipment.StatsMap.ATK);
            equipment.LevelUpV1();
            Assert.Equal(2m, equipment.StatsMap.ATK);
        }

        [Fact]
        public void GetHammerExp()
        {
            var costSheet = new EnhancementCostSheetV3();
            costSheet.Set(
                @"600301,EquipmentMaterial,1,0,0,100,0,0,0,0,0,0,0,0,0
600302,EquipmentMaterial,2,0,0,200,0,0,0,0,0,0,0,0,0
600303,EquipmentMaterial,3,0,0,300,0,0,0,0,0,0,0,0,0
600304,EquipmentMaterial,4,0,0,400,0,0,0,0,0,0,0,0,0
600305,EquipmentMaterial,5,0,0,500,0,0,0,0,0,0,0,0,0
");
            foreach (var costRow in costSheet.Values)
            {
                var id = costRow.Id;
                Assert.True(costRow.Exp > 0L);
                Assert.Equal(costRow.Exp, costSheet.GetHammerExp(id));
            }
        }
    }
}
