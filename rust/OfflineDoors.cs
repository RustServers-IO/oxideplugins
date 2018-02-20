using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Offline Doors", "Slydelix", 1.0)]
    class OfflineDoors : RustPlugin
    {
        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"turnedoff", "<color=silver>Turned <color=red>off</color> automatic door closing on disconnect</color>"},
                {"turnedon", "<color=silver>Turned <color=green>on</color> automatic door closing on disconnect, your doors will now close when you disconnect</color>"},
            }, this);
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

        private void Loaded()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Name);

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