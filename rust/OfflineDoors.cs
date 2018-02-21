using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Offline Doors", "Slydelix", 1.1, ResourceId = 2782)]
    class OfflineDoors : RustPlugin
    {
        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"noperm", "You don't have permission to use this command."},
                {"turnedoff", "<color=silver>Turned <color=red>off</color> automatic door closing on disconnect</color>"},
                {"turnedon", "<color=silver>Turned <color=green>on</color> automatic door closing on disconnect, your doors will now close when you disconnect</color>"},
            }, this);
        }

        #endregion

        private string perm = "offlinedoors.use";
        private bool usePerms;

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config["Use permissions"] = usePerms = GetConfig("Use permissions", false);
            SaveConfig();
        }
            
        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion

        #region Data

        private class StoredData
        {
            public Dictionary<ulong, bool> players = new Dictionary<ulong, bool>();

            public StoredData()
            {
            }
        }

        private StoredData storedData;

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Name, storedData, true);

        #endregion

        #region Hooks

        void OnUserConnected(IPlayer player)
        {
            ulong ID = ulong.Parse(player.Id);

            if (!storedData.players.ContainsKey(ID))
            {
                storedData.players.Add(ID, true);
                SaveData();
            }
        }

        private void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Name);
            permission.RegisterPermission(perm, this);
            LoadDefaultConfig();
        }

        private void Loaded()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                //Default on
                if (!storedData.players.ContainsKey(player.userID))
                {
                    storedData.players.Add(player.userID, true);
                    SaveData();
                }
            }

            foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
            {
                if (!storedData.players.ContainsKey(player.userID))
                {
                    storedData.players.Add(player.userID, true);
                    SaveData();
                }
            }
        }

        private void Unload() => SaveData();

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (usePerms)
                if (!permission.UserHasPermission(player.UserIDString, perm)) return;

            if (!storedData.players.ContainsKey(player.userID))
            {
                storedData.players.Add(player.userID, true);
                SaveData();
            }

            if (!storedData.players[player.userID]) return;

            List<Door> list = Resources.FindObjectsOfTypeAll<Door>().Where(x => x.OwnerID == player.userID).ToList();

            if (list.Count == 0) return;

            foreach (var item in list)
                if (item.IsOpen()) item.CloseRequest();
        }
        #endregion

        #region Command

        [ChatCommand("ofd")]
        private void offlinedoorscmd(BasePlayer player, string command, string[] args)
        {
            if (usePerms)
            {
                if (!permission.UserHasPermission(player.UserIDString, perm))
                {
                    SendReply(player, lang.GetMessage("noperm", this, player.UserIDString));
                    return;
                }
            }

            if (!storedData.players.ContainsKey(player.userID))
            {
                storedData.players.Add(player.userID, false);
                SaveData();
                SendReply(player, lang.GetMessage("turnedoff", this, player.UserIDString));
                return;
            }

            if (storedData.players[player.userID])
            {
                storedData.players[player.userID] = false;
                SaveData();
                SendReply(player, lang.GetMessage("turnedoff", this, player.UserIDString));
                return;
            }

            storedData.players[player.userID] = true;
            SaveData();
            SendReply(player, lang.GetMessage("turnedon", this, player.UserIDString));
            return;
        }

        #endregion
    }
}