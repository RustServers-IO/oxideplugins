using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("Kits", "k1lly0u", "0.1.11", ResourceId = 1872)]
    public class Kits : HideHoldOutPlugin
    {
        #region Fields
        ServerManager sm = GameObject.Find("ServerManager").GetComponent<ServerManager>();
        NetworkController nc = UnityEngine.Object.FindObjectOfType<NetworkController>();

        private static readonly FieldInfo ChatNetViewField = typeof(ChatManager).GetField("Chat_NetView", BindingFlags.NonPublic | BindingFlags.Instance);
        public static uLink.NetworkView ChatNetView = ChatNetViewField.GetValue(NetworkController.NetManager_.chatManager) as uLink.NetworkView;

        Kit kits;
        private DynamicConfigFile Kit_Data;

        Players pd;
        private DynamicConfigFile Player_Data;

        Dictionary<string, string> KitEditor = new Dictionary<string, string>();
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            Kit_Data = Interface.Oxide.DataFileSystem.GetFile("kit_data");
            Player_Data = Interface.Oxide.DataFileSystem.GetFile("kit_players");
        }
        void OnServerInitialized()
        {
            LoadData();
            RegisterPermissions();
            lang.RegisterMessages(messages, this);
            timer.Once(900, () => SaveLoop());
        }
        void Unload() => SaveData();
        private void RegisterPermissions()
        {
            permission.RegisterPermission("kits.admin", this);

            foreach (var kit in kits.Kits)
                if (!string.IsNullOrEmpty(kit.Value.Permission))
                    permission.RegisterPermission("kits." + kit.Value.Permission, this);
        }
        #endregion       
        void OnPlayerRespawned(PlayerInfos player)
        {
            if (kits.Kits.ContainsKey("autokit"))            
                GiveKit(player, "autokit");
        }

        #region Helper Functions
        private void SendReply(PlayerInfos player, string msg) => ChatNetView.RPC("NET_Receive_msg", player.NetPlayer, new object[] { "\r\n" + msg, chat_msg_type.standard, player.account_id });
        private string GetMsg(string key) => lang.GetMessage(key, this);

        private List<PlayerInfos> GetOnlinePlayers()
        {
            List<PlayerInfos> players = new List<PlayerInfos>();
            foreach (var entry in sm.Connected_Players)
                if (entry != null)
                    if (entry.connected)
                        players.Add(entry);
            return players;
        }
        private List<PlayerInfos> FindPlayers(string name)
        {
            var playerList = GetOnlinePlayers();
            List<PlayerInfos> foundPlayers = new List<PlayerInfos>();
            foreach (var player in playerList)
            {
                if (player.Nickname.ToLower().Contains(name.ToLower()))
                    foundPlayers.Add(player);
                else if (player.account_id == name)
                {
                    foundPlayers.Add(player);
                    break;
                }
            }
            return foundPlayers;
        }
        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;        
        #endregion

        #region Core Functions
        private void CanGiveKit(PlayerInfos player, string kitname)
        {
            if (!player.isADMIN)
            {
                if (!pd.playerData.ContainsKey(player.account_id))
                    pd.playerData.Add(player.account_id, new Dictionary<string, PlayerData>());
                var kit = kits.Kits[kitname];
                var playerData = pd.playerData[player.account_id];
                if (!playerData.ContainsKey(kitname))
                    playerData.Add(kitname, new PlayerData());
                bool cd = false;
                bool use = false;
                if (!string.IsNullOrEmpty(kit.Permission))
                    if (!permission.UserHasPermission(player.account_id, kit.Permission))
                    {
                        SendReply(player, GetMsg("noPermK"));
                        return;
                    }
                if (kit.Cooldown > 0)
                {
                    if (playerData[kitname].Cooldown > GrabCurrentTime() && playerData[kitname].Cooldown != 0)
                    {
                        SendReply(player, string.Format(GetMsg("cdTime"), (int)(GrabCurrentTime() - playerData[kitname].Cooldown) / 60));
                        return;
                    }
                    cd = true;
                }
                if (kit.MaxUse > 0)
                {
                    if (playerData[kitname].Used >= kit.MaxUse)
                    {
                        SendReply(player, string.Format(GetMsg("maxUsed"), kit.Name));
                        return;
                    }
                    use = true;
                }
                if (cd) playerData[kitname].Cooldown = GrabCurrentTime() + kit.Cooldown;
                if (use) playerData[kitname].Used++;
            }
            GiveKit(player, kitname);
        }
        private void GiveKit(PlayerInfos player, string kitname)
        {
            foreach (var item in kits.Kits[kitname].Items)
            {
                int ID = int.Parse(item.ItemID);
                int Amount = int.Parse(item.Amount);
                if (ID != 0)
                    nc.NetView.RPC("NET_ADMIN_ReceiveItem", player.NetPlayer, new object[] { ID, Amount });
            }
            SendReply(player, string.Format(GetMsg("giveKit"), kits.Kits[kitname].Name));
        }
        private List<ItemDef> GetItems(string inventory)
        {
            List<ItemDef> Items = new List<ItemDef>();
            string[] pieces = inventory.Replace("_", ":").Split(':');
            for (int i = 0; i < pieces.Length; i++)
            {
                ItemDef Item = new ItemDef();
                Item.ItemID = pieces[i];
                Item.Amount = pieces[i + 1];
                Item.Ammo = pieces[i + 2];
                Items.Add(Item);
                i = i + 2;
            }
            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                if (item.ItemID == "0")
                    Items.Remove(item);
            }
            return Items;
        }
        private void EditKit(PlayerInfos player, string kitname, string[] args)
        {
            KitData kit = kits.Kits[kitname];
            for (int i = 0; i < args.Length; i++)
            {
                object value = null;
                switch (args[i].ToLower())
                {                    
                    case "description":
                        value = kit.Description = args[++i];
                        break;
                    case "cooldown":
                        int cd;
                        if (int.TryParse(args[++i], out cd))
                            value = kit.Cooldown = cd;
                        break;
                    case "max":
                        int max;
                        if (int.TryParse(args[++i], out max))
                            value = kit.MaxUse = max;
                        break;
                    case "hide":
                        bool hide;
                        if (bool.TryParse(args[++i], out hide))
                            value = kit.Hide = hide;
                        break;
                    case "permission":
                        value = kit.Permission = args[++i];
                        break;
                    case "items":
                        kit.Items = GetItems(player.inventory);
                        break;
                    case "save":
                        SaveKits();
                        SendReply(player, string.Format(GetMsg("saveKit"), kitname));
                        KitEditor.Remove(player.account_id);
                        return;
                    default:
                        break;
                }
                if (args[i].ToLower() != "edit")
                    SendReply(player, $"Set {args[i - 1].ToLower()} to {value ?? "null"}");
            }
        }
        private void SendEditList(PlayerInfos player)
        {
            SendReply(player, GetMsg("edit1"));
            SendReply(player, GetMsg("edit2"));
            SendReply(player, GetMsg("edit3"));
            SendReply(player, GetMsg("edit4"));
            SendReply(player, GetMsg("edit5"));
            SendReply(player, GetMsg("edit6"));
            SendReply(player, GetMsg("edit7"));
            SendReply(player, GetMsg("edit8"));
        }
        private void SendHelpList(PlayerInfos player)
        {
            SendReply(player, GetMsg("help1"));
            SendReply(player, GetMsg("help2"));
            SendReply(player, GetMsg("help3"));
            SendReply(player, GetMsg("help4"));
            SendReply(player, GetMsg("help5"));
            SendReply(player, GetMsg("help6"));
            SendReply(player, GetMsg("help7"));
        }
        #endregion

        #region Chat Commands
        [ChatCommand("kit")]
        void cmdKit(PlayerInfos player, string command, string[] args)
        {
            if (args == null || args.Length == 0)
            {                
                SendReply(player, $"<color=#C4FF00>{Title}   v.{Version}</color>");
                SendReply(player, GetMsg("kitSyn"));
                foreach (var kit in kits.Kits)
                    if (kit.Value.Active)
                        if (!kit.Value.Hide)
                        {
                            string message = $"Name: <color=#C4FF00>{kit.Value.Name}</color>";
                            if (!string.IsNullOrEmpty(kit.Value.Description))
                                message = message + $", Description: <color=#C4FF00>{kit.Value.Description}</color>";
                            if (kit.Value.MaxUse != 0)
                                message = message + $", Max: <color=#C4FF00>{kit.Value.MaxUse}</color>";
                            if (kit.Value.Cooldown != 0)
                                message = message + $", Cooldown: <color=#C4FF00>{kit.Value.Cooldown/60} minute(s)</color>";
                            SendReply(player, message);
                        } 
                return;
            }
            if (args.Length > 0)
            {
                if (kits.Kits.ContainsKey(args[0].ToLower()))
                {
                    if (kits.Kits[args[0].ToLower()].Active)                        
                            CanGiveKit(player, args[0].ToLower());
                    else SendReply(player, string.Format(GetMsg("kitDeac"), args[0]));
                    return;
                }
                if (HasPerm(player))
                {
                    switch (args[0].ToLower())
                    {
                        #region add
                        case "add":
                            if (args.Length >= 2)
                            {
                                if (!kits.Kits.ContainsKey(args[1].ToLower()))
                                {
                                    kits.Kits.Add(args[1].ToLower(), new KitData { Active = false, Items = GetItems(player.inventory), Name = args[1] });
                                    if (KitEditor.ContainsKey(player.account_id))
                                        KitEditor[player.account_id] = args[1].ToLower();
                                    else KitEditor.Add(player.account_id, args[1].ToLower());
                                    SendEditList(player);
                                    SendReply(player, string.Format(GetMsg("newKit"), args[1]));
                                    return;
                                }
                                SendReply(player, string.Format(GetMsg("kitExists"), args[1]));
                                return;
                            }
                            return;
                        #endregion
                        #region edit
                        case "edit":
                            if (args.Length == 1)
                            {
                                SendEditList(player);
                                return;
                            }
                            if (args.Length > 1)
                            {
                                if (KitEditor.ContainsKey(player.account_id))
                                {
                                    EditKit(player, KitEditor[player.account_id], args);
                                    return;
                                }
                                if (kits.Kits.ContainsKey(args[1].ToLower()))
                                {
                                    if (KitEditor.ContainsKey(player.account_id))
                                        KitEditor[player.account_id] = args[1].ToLower();
                                    else KitEditor.Add(player.account_id, args[1].ToLower());
                                    SendEditList(player);
                                    SendReply(player, string.Format(GetMsg("kitEdit"), args[1]));
                                    return;
                                }
                                else
                                {
                                    SendReply(player, string.Format(GetMsg("noKit"), args[1]));
                                    return;
                                }
                            }                                         
                            return;
                        #endregion
                        #region activate
                        case "activate":
                            if (args.Length >= 2)
                            {
                                if (kits.Kits.ContainsKey(args[1].ToLower()))
                                {
                                    if (!kits.Kits[args[1].ToLower()].Active)
                                    {
                                        kits.Kits[args[1].ToLower()].Active = true;
                                        SendReply(player, string.Format(GetMsg("actSuc"), args[1]));
                                        SaveKits();
                                        return;
                                    }
                                    else
                                    {
                                        SendReply(player, string.Format(GetMsg("actEx"), args[1]));
                                        return;
                                    }
                                }
                                SendReply(player, string.Format(GetMsg("noKit"), args[1]));
                            }
                            return;
                        #endregion
                        #region deactivate
                        case "deactivate":
                            if (args.Length >= 2)
                            {
                                if (kits.Kits.ContainsKey(args[1].ToLower()))
                                {
                                    if (kits.Kits[args[1].ToLower()].Active)
                                    {
                                        kits.Kits[args[1].ToLower()].Active = false;
                                        SendReply(player, string.Format(GetMsg("deacSuc"), args[1]));
                                        SaveKits();
                                        return;
                                    }
                                    else
                                    {
                                        SendReply(player, string.Format(GetMsg("deacEx"), args[1]));
                                        return;
                                    }
                                }
                                SendReply(player, string.Format(GetMsg("noKit"), args[1]));
                            }
                            return;
                        #endregion
                        #region give
                        case "give":
                            if (args.Length >= 3)
                            {
                                if (!kits.Kits.ContainsKey(args[2].ToLower()))
                                {
                                    var players = FindPlayers(args[1]);
                                    if (players.Count == 0)
                                    {
                                        SendReply(player, string.Format(lang.GetMessage("noPlayers", this, player.account_id), args[1]));
                                        return;
                                    }
                                    if (players.Count > 1)
                                    {
                                        SendReply(player, string.Format(lang.GetMessage("multiPlayers", this, player.account_id), args[1]));
                                        return;
                                    }
                                    var target = players[0];
                                    GiveKit(target, args[2].ToLower());
                                    return;
                                }
                                SendReply(player, string.Format(GetMsg("noKit"), args[2]));
                            }
                            return;
                        #endregion
                        #region remove
                        case "remove":
                            if (args.Length >= 2)
                            {
                                if (kits.Kits.ContainsKey(args[1].ToLower()))
                                {
                                    kits.Kits.Remove(args[1].ToLower());
                                    SendReply(player, string.Format(GetMsg("remSuc"), args[1]));
                                    return;
                                }
                                SendReply(player, string.Format(GetMsg("noKit"), args[1]));
                            }
                            return;
                        #endregion
                        #region help
                        case "help":
                            SendHelpList(player);
                            return;
                            #endregion
                    }
                    SendReply(player, GetMsg("incSyn"));
                }
            }
        }
        private bool HasPerm(PlayerInfos player)
        {
            if (player.isADMIN) return true;
            if (permission.UserHasPermission(player.account_id.ToString(), "kits.admin")) return true;
            SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("noPerms", this, player.account_id.ToString()));
            return false;
        }
        #endregion

        #region Data Management
        void SaveData() => Player_Data.WriteObject(pd);        
        void SaveKits() => Kit_Data.WriteObject(kits);
        
        private void SaveLoop()
        {
            SaveData();
            timer.Once(900, () => SaveLoop());
        }
        void LoadData()
        {
            try
            {
                kits = Kit_Data.ReadObject<Kit>();
            }
            catch
            {
                Puts("Couldn't kit load data, creating new datafile");
                kits = new Kit();
            }
            try
            {
                pd = Player_Data.ReadObject<Players>();
            }
            catch
            {
                Puts("Couldn't load player data, creating new datafile");
                pd = new Players();
            }
        }
        #endregion

        #region Classes
        class Kit
        {
            public Dictionary<string, KitData> Kits = new Dictionary<string, KitData>();
        }
        class KitData
        {
            public bool Active;
            public string Name;
            public string Description = "";
            public int Cooldown = 0;
            public int MaxUse = 0;
            public bool Hide = false;
            public string Permission = "";
            public List<ItemDef> Items;
            
        }
        class ItemDef
        {
            public string ItemID;
            public string Amount;
            public string Ammo;
        }
        class Players
        {
            public Dictionary<string, Dictionary<string, PlayerData>> playerData = new Dictionary<string, Dictionary<string, Kits.PlayerData>>();
        }
        class PlayerData
        {
            public int Used;
            public double Cooldown;
        }
        #endregion

        #region Messaging
        Dictionary<string, string> messages = new Dictionary<string, string>
        {
            { "noPlayers", "Could not find any players with the name <color=#C4FF00>{0}</color>" },
            { "multiPlayers", "Multiple players found with the name <color=#C4FF00>{0}</color>" },
            { "incSyn", "Incorrect Syntax!" },
            { "title", "<color=#C4FF00>Kits:</color> " },
            { "noPerms", "<color=#C4FF00>You do not have permission to use this command!</color>" },
            {"noPermK", "<color=#C4FF00>You do not have permission to use this kit</color>" },
            {"cdTime", "You must wait another <color=#C4FF00>{0}</color> minute(s) before you can claim this kit again" },
            {"maxUsed", "You have reached your use limit for the kit <color=#C4FF00>{0}</color>" },
            {"giveKit", "You have recieved the kit <color=#C4FF00>{0}</color>" },
            {"saveKit", "Saved changes to kit <color=#C4FF00>{0}</color>" },
            {"edit1", "<color=#C4FF00>/kit edit <kitname></color> - Start editing a kit" },
            {"edit2", "<color=#C4FF00>/kit edit permission \"permission name\"</color> - Sets a permission needed to get this kit" },
            {"edit3", "<color=#C4FF00>/kit edit description \"enter description here\"</color> - Sets the kit description" },
            {"edit4", "<color=#C4FF00>/kit edit cooldown ##</color> - The amount of time between uses" },
            {"edit5", "<color=#C4FF00>/kit edit max ##</color> - The maximum amount of time a player can use the kit" },
            {"edit6", "<color=#C4FF00>/kit edit items</color> - Clears the current item list and replace with your inventory" },
            {"edit7", "<color=#C4FF00>/kit edit hide TRUE/FALSE</color> - Show or hide the kit from the kit list" },
            {"edit8", "<color=#C4FF00>/kit edit save</color> - Saves the changes you have made to the kit" },
            {"help1", "<color=#C4FF00>/kit add <kitname></color> - Add a new kit" },
            {"help2", "<color=#C4FF00>/kit activate <kitname></color> - Activate the kit" },
            {"help3", "<color=#C4FF00>/kit deactivate <kitname></color> - Deactivate the kit" },
            {"help4", "<color=#C4FF00>/kit edit</color> - Display information on editing kits" },
            {"help5", "<color=#C4FF00>/kit give <playername> <kitname></color> - Gives a player the specified kit" },
            {"help6", "<color=#C4FF00>/kit remove <kitname></color> - Removes a kit" },
            {"help7", "<color=#C4FF00>/kit help</color>" },
            {"kitSyn", "Syntax: <color=#C4FF00>/kit <kitname></color>" },
            {"kitDeac", "The kit <color=#C4FF00>{0}</color> is currently de-activated" },
            {"newKit", "You have created the kit <color=#C4FF00>{0}</color>. Before you can use it you must set the kit options using <color=#C4FF00>\"/kit edit\"</color>" },
            {"kitExists", "A kit with the name <color=#C4FF00>{0}</color> already exists" },
            {"kitEdit", "You are now editing the kit <color=#C4FF00>{0}</color>" },
            {"noKit", "Unable to find a kit with the name <color=#C4FF00>{0}</color>" },
            {"actSuc", "You have activated the kit <color=#C4FF00>{0}</color>" },
            {"actEx", "The kit <color=#C4FF00>{0}</color> is already activated" },
            {"deacSuc", "You have de-activated the kit <color=#C4FF00>{0}</color>" },
            {"deacEx", "The kit <color=#C4FF00>{0}</color> is already de-activated" },
            {"remSuc", "You have successfully removed the kit <color=#C4FF00>{0}</color>" }
        };
        #endregion
    }
}
