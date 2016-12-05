/*
TODO:
- Add weapons option to replace on ammo empty
- Re-activate slot where item was replaced
- Refill candle hat fuel when empty
*/

using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ReplaceOnBroken", "Wulf/lukespragg", "2.1.1", ResourceId = 1173)]
    [Description("Replaces the active broken item with a not broken item if in inventory")]

    class ReplaceOnBroken : RustPlugin
    {
        #region Initialization

        const string permAllow = "replaceonbroken.allow";

        List<object> exclusions;
        bool usePermissions;

        protected override void LoadDefaultConfig()
        {
            Config["ItemExclusions"] = exclusions = GetConfig("ItemExclusions", new List<object> { "item.shortname", "otheritem.name" });
            Config["UsePermissions"] = usePermissions = GetConfig("UsePermissions", true);

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permAllow, this);
        }

        #endregion

        #region Item Replacement

        void OnLoseCondition(Item oldItem, ref float amount)
        {
            if (oldItem?.parent == null || !oldItem.parent.HasFlag(ItemContainer.Flag.IsPlayer)) return;
            if (oldItem.condition > amount || exclusions.Contains(oldItem.info.shortname)) return;

            var player = oldItem.parent.playerOwner;
            if (player == null) return;

            if (usePermissions && !permission.UserHasPermission(player.UserIDString, permAllow)) return;

            var main = player.inventory.containerMain;
            foreach (var newItem in main.itemList)
            {
                var newItemPosition = newItem.position;
                timer.Once(0.1f, () => oldItem.MoveToContainer(main, newItemPosition));
                break;
            }
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)System.Convert.ChangeType(Config[name], typeof(T));

        #endregion
    }
}
