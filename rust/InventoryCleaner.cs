using System;
using System.Collections.Generic;
using UnityEngine;
using Rust;
using System.Text;

namespace Oxide.Plugins
{
    [Info("Admin Inventory Cleaner", "TheDoc - Uprising Servers", "1.3.0", ResourceId = 973)]
    class InventoryCleaner : RustPlugin
    {
        void SendChatMessage(BasePlayer player, string message, string args = null) => PrintToChat(player, $"{message}", args);

        void Init() => PluginSetup();

        [ChatCommand("cleaninv")]
        void cmdChatCleanInv(BasePlayer player, string command, string[] args)
        {
            if (IsAllowed(player, "inventorycleaner.allowed") && player != null)
            {
                if (args.Length == 0)
                {
                    player.inventory.Strip();
                    SendChatMessage(player, "<color=lime>Inventory Cleaner</color>: Your Complete Inventory is now clean!");
                    return;
                }
                if (args.Length == 1)
                {
                    switch (args[0])
                    {
                        case "help":
                            var sb = new StringBuilder();
                            sb.Append("<size=22><color=lime>Inventory Cleaner by TheDoc</color></size> v" + Version + " <color=#ce422b>http://www.uprisingserver.com</color>\n\n");
                            sb.Append("<color=#ff0000>Warning:</color> Once items removed they are GONE !").Append("\n\n");
                            sb.Append("<color=lime>Available commands</color> :").Append("\n");
                            sb.Append("  ").Append("<color=#74c6ff>/cleaninv</color> - Strip you naked, all inv gone!").Append("\n");
                            sb.Append("  ").Append("<color=#74c6ff>/cleaninv belt</color> - Remove all items on your Action Belt!").Append("\n");
                            sb.Append("  ").Append("<color=#74c6ff>/cleaninv main</color> - Remove all items on your Main Inventory!").Append("\n");
                            sb.Append("  ").Append("<color=#74c6ff>/cleaninv both</color> - Remove all items on your Main Inventory & Action Belt!").Append("\n");
                            SendChatMessage(player, sb.ToString());
                            break;
                        case "belt":
                            player.inventory.containerBelt.Kill();
                            SendChatMessage(player, "<color=lime>Inventory Cleaner</color>: Your Belt is now clean!");
                            break;
                        case "main":
                            player.inventory.containerMain.Kill();
                            SendChatMessage(player, "<color=lime>Inventory Cleaner</color>: Your Main Inventory is now clean!");
                            break;
                        case "both":
                            player.inventory.containerBelt.Kill();
                            player.inventory.containerMain.Kill();
                            SendChatMessage(player, "<color=lime>Inventory Cleaner</color>: Your Belt and Main Inventory is now clean!");
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        void PluginSetup()
        {
            LoadPermissions();
        }

        void LoadPermissions()
        {
            if (!permission.PermissionExists("inventorycleaner.allowed")) permission.RegisterPermission("inventorycleaner.allowed", this);
        }

        bool IsAllowed(BasePlayer player, string perm)
        {
            if (permission.UserHasPermission(player.userID.ToString(), perm)) return true;
            SendChatMessage(player, "You are <color=red>Not Allowed</color> To Use this command!");
            return false;
        }
    }
}
