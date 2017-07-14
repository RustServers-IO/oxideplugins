using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("PermaMap", "redBDGR", "1.0.5", ResourceId = 2557)]
    [Description("Make sure that players always have access to a map")]

    class PermaMap : RustPlugin
    {
        const string permissionName = "permamap.use";

        void Init()
        {
            permission.RegisterPermission(permissionName, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Unable to craft"] = "You already have a map hidden in your inventory! press your map button to use it",
            }, this);
        }
        void OnPlayerRespawned(BasePlayer player) => DoMap(player);

        void OnPlayerInit(BasePlayer player)
        {
            if (player.inventory.containerBelt.GetSlot(6) == null)
                timer.Once(5f, () => DoMap(player));
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity is BasePlayer)) return;
            BasePlayer player = entity as BasePlayer;
            Item x = player.inventory.containerBelt.GetSlot(6);
            if (x == null)
                return;
            x.LockUnlock(false, player);
            x.RemoveFromContainer();
        }

        object CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            if (bp.name == "map.item")
            {
                BasePlayer player = itemCrafter.containers[0].GetOwnerPlayer();
                if (player == null)
                    return false;
                if (!permission.UserHasPermission(player.UserIDString, permissionName))
                    return null;
                player.ChatMessage(msg("Unable to craft", player.UserIDString));
                return false;
            }
            return null;
        }

        void DoMap(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
                return;
            player.inventory.containerBelt.capacity = 7;
            Item item = ItemManager.CreateByItemID(107868, 1);
            item.MoveToContainer(player.inventory.containerBelt, 6);
            item.LockUnlock(true, player);
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}