using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("InventoryGuardian", "k1lly0u", "0.1.1", ResourceId = 1878)]
    public class InventoryGuardian : HideHoldOutPlugin
    {
        #region Fields
        ServerManager sm = GameObject.Find("ServerManager").GetComponent<ServerManager>();

        private static readonly FieldInfo ChatNetViewField = typeof(ChatManager).GetField("Chat_NetView", BindingFlags.NonPublic | BindingFlags.Instance);
        public static uLink.NetworkView ChatNetView = ChatNetViewField.GetValue(NetworkController.NetManager_.chatManager) as uLink.NetworkView;
        
        IGData igData;
        private DynamicConfigFile Inventory_Data;
        #endregion

        #region Oxide Hooks
        void Loaded() => Inventory_Data = Interface.Oxide.DataFileSystem.GetFile("Inventory-Guardian");
        void OnServerInitialized()
        {
            lang.RegisterMessages(messages, this);
            LoadVariables();
            LoadData();
            RegisterPermisions();
            SaveLoop();
        }       
        void OnPlayerConnected(PlayerInfos player)
        {
            if (igData.IsActivated)
                if (igData.Inventorys.ContainsKey(player.account_id))
                    if (igData.Inventorys[player.account_id].RestoreOnce)
                    {
                        if (!player.connected)
                            timer.Once(3, () => { OnPlayerConnected(player); return; });
                        else RestoreInventory(player);
                    }
        }
        void OnPlayerDisconnected(PlayerInfos player)
        {
            if (igData.IsActivated)
                SaveInventory(player);
        }        
        #endregion

        #region Functions
        private void SendReply(PlayerInfos player, string msg) => ChatNetView.RPC("NET_Receive_msg", player.NetPlayer, new object[] { "\r\n" + msg, chat_msg_type.standard, player.account_id });
               
        List<PlayerInfos> GetOnlinePlayers()
        {
            List<PlayerInfos> players = new List<PlayerInfos>();
            foreach (var entry in sm.Connected_Players)
                if (entry != null)
                    if (entry.connected)
                        players.Add(entry);
            return players;
        }
        List<PlayerInfos> GetAllPlayers()
        {
            List<PlayerInfos> players = new List<PlayerInfos>();
            foreach (var entry in sm.Connected_Players)
                if (entry != null)
                    if (entry.account_id != "0")
                        players.Add(entry);
            return players;
        }
        private void RestoreAll()
        {
            var list = GetOnlinePlayers();
            foreach (var player in list)
                RestoreInventory(player);
        }
        private void SaveAll()
        {
            var list = GetAllPlayers();
            foreach (var player in list)                            
                SaveInventory(player);                        
            SaveData();
        }
        private void RemoveAll()
        {
            igData.Inventorys.Clear();
            SaveData();
        }
        private PlayerInfos FindPlayer(PlayerInfos fplayer, string name)
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
            if (foundPlayers.Count == 0)
            {
                if (fplayer != null)
                    MSG(fplayer, "", GM("noPlayers", fplayer));
                return null;
            }
            if (foundPlayers.Count > 1)
            {
                if (fplayer != null)
                    MSG(fplayer, "", GM("multiPlayers", fplayer));
                return null;
            }

            return foundPlayers[0];            
        }
        #endregion

        #region Messaging
        private string GM(string key, PlayerInfos player) => lang.GetMessage(key, this, player.account_id);
        private void MSG(PlayerInfos player, string message, string key = "", bool title = false)
        {
            message = configData.Messages_MainColor + key + "</color>" + configData.Messages_MsgColor + message + "</color>";
            if (title)
                message = configData.Messages_MainColor + Title + ": </color>" + message;
            SendReply(player, message);
        }
        #endregion

        #region Inventory
        private bool SaveInventory(PlayerInfos player)
        {
            Puts("saving :" + player.Nickname);
            if (string.IsNullOrEmpty(player.inventory)) return false;
            if (!igData.Inventorys.ContainsKey(player.account_id))
                igData.Inventorys.Add(player.account_id, new PlayerData { Inventory = player.inventory });
            igData.Inventorys[player.account_id].Inventory = player.inventory;
            return true;
        }
        private bool RemoveInventory(PlayerInfos player)
        {
            if (igData.Inventorys.ContainsKey(player.account_id))
            {
                Puts("removing :" + player.Nickname);
                igData.Inventorys.Remove(player.account_id);
                return true;
            }
            return false;
        }
        private bool RestoreInventory(PlayerInfos player)
        {
            if (igData.Inventorys.ContainsKey(player.account_id))
            {
                Puts("restoring :" + player.Nickname);
                if (igData.Inventorys[player.account_id].RestoreOnce)
                    igData.Inventorys[player.account_id].RestoreOnce = false;
                player.inventory = igData.Inventorys[player.account_id].Inventory;
                return true;
            }
            return false;
        }
        
        #endregion

        #region Permissions
        private void RegisterPermisions()
        {
            permission.RegisterPermission("inventoryguardian.admin", this);
            permission.RegisterPermission("inventoryguardian.use", this);
        }
        private bool IsAdmin(PlayerInfos player)
        {
            if (permission.UserHasPermission(player.account_id, "inventoryguardian.admin") || player.isADMIN)
                return true;
            return false;
        }
        private bool IsUser(PlayerInfos player)
        {
            if (permission.UserHasPermission(player.account_id, "inventoryguardian.use") || IsAdmin(player))
                return true;
            return false;
        }
        #endregion

        #region Chat Commands
        [ChatCommand("ig")]
        private void cmdInvGuard(PlayerInfos player, string command, string[] args)
        {
            if (args == null || args.Length == 0)
            {
                if (IsUser(player))
                {
                    MSG(player, GM("saveP2", player), GM("saveP1", player));
                    MSG(player, GM("restP2", player), GM("restP1", player));
                    MSG(player, GM("delP2", player), GM("delP1", player));
                    MSG(player, GM("strpP2", player), GM("strpP1", player));
                }
                if (IsAdmin(player))
                {
                    MSG(player, GM("saveA2", player), GM("saveA1", player));
                    MSG(player, GM("saveT2", player), GM("saveT1", player));
                    MSG(player, GM("restA2", player), GM("restA1", player));
                    MSG(player, GM("restT2", player), GM("restT1", player));
                    MSG(player, GM("delA2", player), GM("delA1", player));
                    MSG(player, GM("delT2", player), GM("delT1", player));
                    MSG(player, GM("strpA2", player), GM("strpA1", player));
                    MSG(player, GM("strpT2", player), GM("strpT1", player));
                    MSG(player, GM("toggle2", player), GM("toggle1", player));
                }
                return;
            }
            if (args.Length >= 1)
            {
                if (!igData.IsActivated)
                {
                    if (args[0].ToLower() == "toggle")
                        if (IsAdmin(player))
                        {
                            igData.IsActivated = true;
                            SaveData();
                            MSG(player, "", GM("haveEnabled", player));
                            return;
                        }
                    MSG(player, "", GM("disabled", player));
                    return;
                }
                else if (igData.IsActivated)
                    switch (args[0].ToLower())
                    {
                        case "autorestore":
                            if (IsAdmin(player))
                            {
                                foreach (var entry in igData.Inventorys)
                                    entry.Value.RestoreOnce = true;
                                MSG(player, "", GM("autorestore", player));
                                return;
                            }
                            else MSG(player, GM("noPerm", player), "", true);
                            return;
                        case "save":
                            if (IsAdmin(player))
                            {
                                if (args.Length == 2)
                                {
                                    if (args[1].ToLower() == "all")
                                    {
                                        SaveAll();
                                        MSG(player, "", GM("savedAll", player));
                                        return;
                                    }
                                    PlayerInfos target = FindPlayer(player, args[1]);
                                    if (target != null)
                                    {
                                        if (SaveInventory(target))
                                        {
                                            MSG(player, "", string.Format(GM("savedPlayer", player), target.Nickname));
                                            return;
                                        }
                                        MSG(player, "", string.Format(GM("errorSave", player), target.Nickname));
                                    }
                                    return;
                                }
                            }
                            else if (IsUser(player))
                            {
                                if (SaveInventory(player))
                                {
                                    MSG(player, "", GM("savedOwn", player));
                                    return;
                                }
                                MSG(player, "", GM("errorSaveOwn", player));
                            }
                            else MSG(player, GM("noPerm", player), "", true);
                            return;
                        case "restore":
                            if (IsAdmin(player))
                            {
                                if (args.Length == 2)
                                {
                                    if (args[1].ToLower() == "all")
                                    {
                                        RestoreAll();
                                        MSG(player, "", GM("restoredAll", player));
                                        return;
                                    }
                                    PlayerInfos target = FindPlayer(player, args[1]);
                                    if (target != null)
                                    {
                                        if (RestoreInventory(target))
                                        {
                                            MSG(player, "", string.Format(GM("restoredPlayer", player), target.Nickname));
                                            return;
                                        }
                                        MSG(player, "", string.Format(GM("noTInv", player), target.Nickname));
                                    }
                                    return;
                                }
                            }
                            else if (IsUser(player))
                            {
                                if (RestoreInventory(player))
                                {
                                    MSG(player, "", GM("restoredOwn", player));
                                    return;
                                }
                                MSG(player, "", GM("noInv", player));
                            }
                            else MSG(player, GM("noPerm", player), "", true);
                            return;
                        case "delete":
                            if (IsAdmin(player))
                            {
                                if (args.Length == 2)
                                {
                                    if (args[1].ToLower() == "all")
                                    {
                                        RemoveAll();
                                        MSG(player, "", GM("remAll", player));
                                    }
                                    PlayerInfos target = FindPlayer(player, args[1]);
                                    if (target != null)
                                    {
                                        if (RemoveInventory(target))
                                        {
                                            MSG(player, "", string.Format(GM("remTInv", player), target.Nickname));
                                            return;
                                        }
                                        MSG(player, "", string.Format(GM("noTInv", player), target.Nickname));
                                    }
                                    return;
                                }
                            }
                            else if (IsUser(player))
                            {
                                if (RemoveInventory(player))
                                {
                                    MSG(player, "", GM("remSelf", player));
                                    return;
                                }
                                MSG(player, "", GM("noInv", player));
                            }
                            else MSG(player, GM("noPerm", player), "", true);
                            return;
                        
                        case "toggle":
                            if (IsAdmin(player))
                            {
                                if (igData.IsActivated)
                                {
                                    igData.IsActivated = false;
                                    SaveData();
                                    MSG(player, "", GM("togOff", player));
                                    return;
                                }
                            }
                            return;                                            
                        case "strip":
                            if (IsAdmin(player))
                            {
                                if (args.Length == 2)
                                {
                                    if (args[1].ToLower() == "all")
                                    {
                                        foreach(var p in GetOnlinePlayers())                                        
                                            p.inventory = "0_0_0:";                                        
                                        MSG(player, "", GM("strpAll", player));
                                        return;
                                    }
                                    PlayerInfos target = FindPlayer(player, args[1]);
                                    if (target != null)
                                    {
                                        target.inventory = "0_0_0:";
                                        MSG(player, "", string.Format(GM("strpTInv", player), target.Nickname));
                                    }
                                    return;
                                }
                            }
                            else if (IsUser(player))
                            {
                                player.inventory = "0_0_0:";
                                MSG(player, "", GM("strpSelf", player));
                            }
                            else MSG(player, GM("noPerm", player), "", true);
                            return;
                        
                    }
            }
        }
        #endregion      

        #region Classes
        class IGData
        {
            public bool IsActivated = true;
            public Dictionary<string, PlayerData> Inventorys = new Dictionary<string, PlayerData>();
        }       
        class PlayerData
        {
            public string Inventory;
            public bool RestoreOnce = false;
        }
        #endregion

        #region Data Management
        void SaveData()
        {
            Inventory_Data.WriteObject(igData);
            Puts("Saved data");
        }
        void SaveLoop()
        {
            timer.Once(900, () => { SaveData(); SaveLoop(); });
        }
        void LoadData()
        {
            try
            {
                igData = Inventory_Data.ReadObject<IGData>();
            }
            catch
            {
                Puts("Couldn't load player data, creating new datafile");
                igData = new IGData();
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public string Messages_MainColor { get; set; }
            public string Messages_MsgColor { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Messages_MainColor = "<color=#C4FF00>",
                Messages_MsgColor = "<color=#939393>"
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Lang
        Dictionary<string, string> messages = new Dictionary<string, string>
        {
            {"noPlayers", "No players found" },
            {"multiPlayers", "Multiple players found with that name." },
            {"saveP1", "/ig save"},
            {"saveP2", " - Save your inventory"},
            {"restP1", "/ig restore"},
            {"restP2", " - Restore your inventory"},
            {"delP1", "/ig delete"},
            {"delP2", " - Delete your saved inventory"},
            {"strpP1", "/ig strip"},
            {"strpP2", " - Strip your current inventory"},
            {"saveA1", "/ig save all"},
            {"saveA2", " - Save all inventory's"},
            {"saveT1", "/ig save <playername>"},
            {"saveT2", " - Save <playername>'s inventory"},
            {"restT1", "/ig restore <playername>"},
            {"restT2", " - Restore <playername>'s inventory"},
            {"restA1", "/ig restore all"},
            {"restA2", " - Restore all inventory's"},
            {"delA1", "/ig delete all"},
            {"delA2", " - Delete all inventory's"},
            {"delT1", "/ig delete <playername>"},
            {"delT2", " - Delete <playername>'s saved inventory"},
            {"strpA1", "/ig strip all"},
            {"strpA2", " - Strip all current inventory's"},
            {"strpT1", "/ig strip <playername>"},
            {"strpT2", " - Strip <playername>'s current inventory"},
            {"toggle1", "/ig toggle"},
            {"toggle2", " - Toggles InventoryGuardian on/off"},
            {"haveEnabled", "You have enabled Inventory Guardian"},
            {"disabled", "Inventory Guardian is currently disabled"},
            {"savedAll", "You have successfully saved all player inventorys"},
            {"savedPlayer", "You have successfully saved {0}'s inventory"},
            {"errorSave", "The was a error saving {0}'s inventory"},
            {"savedOwn", "You have successfully saved your inventory"},
            {"errorSaveOwn", "The was a error saving your inventory"},
            {"noPerm", "You do not have permission to use this command"},
            {"restoredAll", "You have successfully restored all player inventorys"},
            {"restoredPlayer", "You have successfully restored {0}'s inventory"},
            {"noTInv", "{0} does not have a saved inventory"},
            {"restoredOwn", "You have successfully restored your inventory"},
            {"noInv", "You do not have a saved inventory"},
            {"remAll", "You have successfully removed all saved player inventorys"},
            {"remTInv", "You have successfully removed {0}'s inventory"},
            {"remSelf", "You have successfully removed your saved inventory"},
            {"togOff", "You have disabled Inventory Guardian"},
            {"strpAll", "You have successfully stripped all saved player inventorys"},
            {"strpTInv", "You have successfully stripped {0}'s inventory"},
            {"strpSelf", "You have successfully stripped your inventory"},
            {"autorestore", "You have activated the 1 time automatic inventory restore mode"}
        };
        #endregion
    }
}
