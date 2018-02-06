using System.Collections.Generic;
using Assets.Scripts.Core;
using System.Linq;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("UnlimitedAmmo", "LaserHydra", "1.2.0", ResourceId = 1619)]
    [Description("Allows you to have unlimited ammo.")]
    class UnlimitedAmmo : HurtworldPlugin
    {
        List<ulong> players = new List<ulong>();
        List<PlayerSession> active = new List<PlayerSession>();

        #region Hooks

        void Loaded()
        {
            LoadMessages();
            RegisterPerm("use");
            LoadData(out players);

            foreach (PlayerSession player in GameManager.Instance.GetSessions().Values)
                OnPlayerConnected(player);

            timer.Repeat(2f, 0, () =>
            {
                foreach (PlayerSession player in active)
                    if (player.IsLoaded)
                        FillAmmo(player);
            });
        }

        void OnPlayerConnected(PlayerSession player)
        {
            if (players.Contains(player.SteamId.m_SteamID))
                active.Add(player);
        }

        void OnPlayerDisconnected(PlayerSession player)
        {
            if (active.Contains(player))
                active.Remove(player);
        }

        #endregion

        #region Loading

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Permission", "You don't have permission to use this command."},
                {"Enabled", "You now have unlimited ammo!"},
                {"Disabled", "You no longer have unlimited ammo!"}
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("toggleammo")]
        void cmdToggleAmmo(PlayerSession player)
        {
            if (!HasPerm(player.SteamId, "use"))
            {
                SendChatMessage(player, GetMsg("No Permission", player.SteamId));
                return;
            }

            if (active.Contains(player))
            {
                players.Remove(player.SteamId.m_SteamID);
                active.Remove(player);

                SendChatMessage(player, GetMsg("Disabled", player.SteamId));
            }
            else
            {
                players.Add(player.SteamId.m_SteamID);
                active.Add(player);

                SendChatMessage(player, GetMsg("Enabled", player.SteamId));
            }

            SaveData(players);
        }

        #endregion

        #region Ammo / Item Related

        void FillAmmo(PlayerSession player)
        {
            if (!player.IsLoaded || !player.Player.isConnected)
                return;

            NetworkEntityComponentBase netEntity = player.WorldPlayerEntity.GetComponent<NetworkEntityComponentBase>();

            if (netEntity == null)
                return;

            EquippedHandlerBase equippedHandler = netEntity.GetComponent<EquippedHandlerBase>();

            if (equippedHandler == null)
                return;

            EquippedHandlerServer equippedHandlerServer = equippedHandler as EquippedHandlerServer;

            if (equippedHandlerServer == null)
                return;

            ItemInstance equippedItem = equippedHandler.GetEquippedItem();

            if (equippedItem == null)
                return;

            GunItem gunItem = equippedItem.Item as GunItem;
            BowItem bowItem = equippedItem.Item as BowItem;

            if ((bowItem != null || gunItem != null) && equippedHandlerServer != null)
            {
                if (gunItem != null)
                {
                    AutomaticGunItem aGunItem = gunItem as AutomaticGunItem;
                    GunItemEquippedState gunEquipState = gunItem.EquippedState(equippedHandler);

                    equippedItem.AuxData = Convert.ToByte(gunItem.GetClipSize());
                    equippedHandlerServer.AuxSync();
                }
                else
                {
                    PlayerInventory inventory = player.WorldPlayerEntity.GetComponent<PlayerInventory>();

                    if (!inventory.HasItem(bowItem.GetAmmoType().ItemId, 1))
                        GiveItem(player, bowItem.GetAmmoType(), 1);
                }
            }
        }

        void GiveItem(PlayerSession player, IItem item, int amount)
        {
            PlayerInventory inventory = player.WorldPlayerEntity.GetComponent<PlayerInventory>();
            ItemInstance itemInstance = new ItemInstance(item, amount);

            inventory.GiveItemServer(itemInstance);
        }

        #endregion

        #region General Methods

        #region Player Finding

        PlayerSession GetPlayer(string searchedPlayer, PlayerSession player)
        {
            foreach (PlayerSession current in GameManager.Instance.GetSessions().Values)
                if (current != null && current.Name != null && current.IsLoaded && current.Name.ToLower() == searchedPlayer.ToLower())
                    return current;

            List<PlayerSession> foundPlayers =
                (from current in GameManager.Instance.GetSessions().Values
                 where current != null && current.Name != null && current.IsLoaded && current.Name.ToLower().Contains(searchedPlayer.ToLower())
                 select current).ToList();

            switch (foundPlayers.Count)
            {
                case 0:
                    SendChatMessage(player, "The player can not be found.");
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    List<string> playerNames = (from current in foundPlayers select current.Name).ToList();
                    string players = string.Join(",", playerNames.ToArray());
                    SendChatMessage(player, "Multiple matching players found: \n" + players);
                    break;
            }

            return null;
        }

        #endregion

        #region Config Helpers

        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList<string>();
            stringArgs.RemoveAt(args.Length - 1);

            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

        T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList<string>();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError($"The plugin failed to read something from the config: {string.Join("/", stringArgs.ToArray())}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }

        #endregion

        #region Datafile Helpers

        void LoadData<DataType>(out DataType data, string filename = null) => data = Interface.Oxide.DataFileSystem.ReadObject<DataType>(filename ?? Title.Replace(" ", string.Empty));

        void SaveData<DataType>(DataType data, string filename = null) => Interface.Oxide.DataFileSystem.WriteObject(filename ?? Title.Replace(" ", string.Empty), data);

        #endregion

        #region Message Helpers

        string GetMsg(string key, object userID = null)
        {
            return lang.GetMessage(key, this, userID.ToString());
        }

        #endregion

        #region Permission Helpers

        void RegisterPerm(params string[] permArray)
        {
            string perm = string.Join(".", permArray);

            permission.RegisterPermission($"{PermissionPrefix}.{perm}", this);
        }

        bool HasPerm(object uid, params string[] permArray)
        {
            uid = uid.ToString();
            string perm = string.Join(".", permArray);

            return permission.UserHasPermission(uid.ToString(), $"{PermissionPrefix}.{perm}");
        }

        string PermissionPrefix
        {
            get
            {
                return this.Title.Replace(" ", "").ToLower();
            }
        }

        #endregion

        #region Chat Message Helpers

        void BroadcastChat(string prefix, string msg = null) => hurt.BroadcastChat(msg == null ? prefix : "<color=#C4FF00>" + prefix + "</color>: " + msg);

        void SendChatMessage(PlayerSession player, string prefix, string msg = null) => hurt.SendChatMessage(player, msg == null ? prefix : "<color=#C4FF00>" + prefix + "</color>: " + msg);

        #endregion

        #endregion
    }
}
