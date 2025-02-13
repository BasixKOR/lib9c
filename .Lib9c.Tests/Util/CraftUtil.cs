namespace Lib9c.Tests.Util
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Libplanet.Action;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Model;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.State;
    using Nekoyume.Module;
    using Nekoyume.TableData;
    using static Lib9c.SerializeKeys;

    public static class CraftUtil
    {
        public static IWorld PrepareCombinationSlot(
            IWorld state,
            Address avatarAddress
        )
        {
            var allSlotState = new AllCombinationSlotState();
            for (var i = 0; i < AvatarState.CombinationSlotCapacity; i++)
            {
                var addr = CombinationSlotState.DeriveAddress(avatarAddress, i);
                var slotState = new CombinationSlotState(addr, i);
                allSlotState.AddSlot(slotState);
            }

            return state.SetCombinationSlotState(avatarAddress, allSlotState);
        }

        public static IWorld AddMaterialsToInventory(
            IWorld state,
            TableSheets tableSheets,
            Address avatarAddress,
            IEnumerable<EquipmentItemSubRecipeSheet.MaterialInfo> materialList,
            IRandom random
        )
        {
            var avatarState = state.GetAvatarState(avatarAddress);
            foreach (var material in materialList)
            {
                var materialRow = tableSheets.MaterialItemSheet[material.Id];
                var materialItem = ItemFactory.CreateItem(materialRow, random);
                avatarState.inventory.AddItem(materialItem, material.Count);
            }

            return state.SetAvatarState(avatarAddress, avatarState);
        }

        public static IWorld UnlockStage(
            IWorld state,
            TableSheets tableSheets,
            Address avatarAddress,
            int stage
        )
        {
            var avatarState = state.GetAvatarState(avatarAddress);
            avatarState.worldInformation = new WorldInformation(
                0,
                tableSheets.WorldSheet,
                Math.Max(stage, GameConfig.RequireClearedStageLevel.ItemEnhancementAction)
            );
            return state.SetAvatarState(avatarAddress, avatarState);
        }
    }
}
