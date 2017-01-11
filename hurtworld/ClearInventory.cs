using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("ClearInventory", "Trentu", "1.0.2")]
    [Description("Clear the inventory of any player")]

    class ClearInventory : HurtworldPlugin
    {
        void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"ClearedInventory", "Cleared the inventory of '{name}'"},
                {"PermissionDenied", "You don't have permission to use '/clear'"},
                {"NoPlayerFound", "Can't find the player '{name}'"}
            };

            lang.RegisterMessages(messages, this);
        }

        void Loaded()
        {
            LoadDefaultMessages();
            permission.RegisterPermission("clearinventory.self", this);
            permission.RegisterPermission("clearinventory.other", this);
        }

        PlayerSession FindPlayer(string name, PlayerSession session)
        {
            var sessions = GameManager.Instance.GetSessions().Values.ToList();
            foreach (var player in sessions)
                if (player.Name == name) return player;

            hurt.SendChatMessage(session, lang.GetMessage("NoPlayerFound", this).Replace("{name}", name));
            return null;
        }

        void DoClearInventory(PlayerSession target, PlayerSession execute)
        {
            var pInventory = target.WorldPlayerEntity.GetComponent<PlayerInventory>();
            for (var i = 0; i < pInventory.Capacity; i++) pInventory.Items[i] = null;

            // Add zero coal to refresh the inventory
            var itemMgr = GlobalItemManager.Instance;
            itemMgr.GiveItem(target.Player, itemMgr.GetItem(20), 0);
            hurt.SendChatMessage(execute, lang.GetMessage("ClearedInventory", this).Replace("{name}", target.Name));
        }

        [ChatCommand("clear")]
        void ClearCommand(PlayerSession session, string command, string[] args)
        {
            if (permission.UserHasPermission(session.SteamId.ToString(), "clearinventory.self") && args.Length == 0)
            {
                DoClearInventory(session, session);
                return;
            }

            if (permission.UserHasPermission(session.SteamId.ToString(), "clearinventory.other"))
            {
                var target = FindPlayer(args[0], session);
                if (target != null) DoClearInventory(target, session);
                return;
            }

            hurt.SendChatMessage(session, lang.GetMessage("PermissionDenied", this));
        }
    }
}
