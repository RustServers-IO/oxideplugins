using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

                        //
                        //      Credit to the original author of this plugin, Scriptzyy
                        //

namespace Oxide.Plugins
{
    [Info("BarrelPoints", "redBDGR", "2.0.6", ResourceId = 2182)]
    [Description("Gives players extra rewards for destroying barrels")]

    class BarrelPoints : RustPlugin
    {
        [PluginReference] Plugin Economics; // http://oxidemod.org/plugins/economics.717/
        [PluginReference] Plugin ServerRewards; // http://oxidemod.org/plugins/serverrewards.1751/

        static Dictionary<string, object> _PermissionDic()
        {
            var x = new Dictionary<string, object>();
            x.Add("barrelpoints.default", 2.0);
            x.Add("barrelpoints.vip", 5.0);
            return x;
        }
        Dictionary<string, object> permissionList;
        Dictionary<string, int> playerInfo = new Dictionary<string, int>();
        List<uint> CrateCache = new List<uint>();

        bool Changed = false;
        bool LoadingQue = false;
        bool useEconomy = true;
        bool useServerRewards = false; // this is no disrespect to the author of ServerRewards, they are both amazing
        bool resetBarrelsOnDeath = true;
        bool sendNotificationMessage = true;
        bool useCrates = false;
        bool useBarrels = true;
        int givePointsEvery = 1;

        void Init() => LoadVariables();

        void Loaded()
        {
            foreach (var entry in permissionList)
                permission.RegisterPermission(entry.Key, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Economy Notice (Barrel)"] = "You received ${0} for destroying a barrel!",
                ["RP Notice (Barrel)"] = "You received {0} RP for destorying a barrel!",

                ["Economy Notice (Crate)"] = "You received ${0} for looting a crate!",
                ["RP Notice (Crate)"] = "You received {0} RP for looting a crate!",
            }, this);

            if (useEconomy)
                if (!Economics)
                {
                    PrintError("Economics.cs was not found! Disabling the economics setting until you reload me");
                    useEconomy = false;
                }
            if (useServerRewards)
                if (!ServerRewards)
                {
                    PrintError("ServerRewards.cs was not found! Disabling the RP setting until you reload me!");
                    useServerRewards = false;
                }
        }

        void OnPluginLoaded(Plugin name)
        {
            if (name.Name == "Economics" || name.Name == "ServerRewards")
            {
                if (LoadingQue)
                    return;
                else
                {
                    Puts("A plugin dependency was detected as being loaded / reloaded... I will automatically reload myself incase of any changes in 3 seconds");
                    LoadingQue = true;
                    timer.Once(3f, () =>
                    {
                        rust.RunServerCommand("reload BarrelPoints");
                        LoadingQue = false;
                    });
                }
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            permissionList = (Dictionary<string, object>)GetConfig("Settings", "Permission List", _PermissionDic());
            useEconomy = Convert.ToBoolean(GetConfig("Settings", "Use Economics", true));
            useServerRewards = Convert.ToBoolean(GetConfig("Settings", "Use ServerRewards", false));
            sendNotificationMessage = Convert.ToBoolean(GetConfig("Settings", "Send Notification Message", true));
            givePointsEvery = Convert.ToInt32(GetConfig("Settings", "Give Points Every x Barrels", 1));
            resetBarrelsOnDeath = Convert.ToBoolean(GetConfig("Settings", "Reset Barrel Count on Death", true));
            useBarrels = Convert.ToBoolean(GetConfig("Settings", "Give Points For Barrels", true));
            useCrates = Convert.ToBoolean(GetConfig("Settings", "Give Points For Crates", false));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!useBarrels)
                return;
            if (entity.ShortPrefabName == "loot-barrel-1" || entity.ShortPrefabName == "loot-barrel-2" || entity.ShortPrefabName == "loot_barrel_1" || entity.ShortPrefabName == "loot_barrel_2" || entity.ShortPrefabName == "oil_barrel")
            {
                if (!info.Initiator) return;
                if (!(info.Initiator is BasePlayer)) return;
                BasePlayer player = info.InitiatorPlayer;
                if (player == null) return;
                string userPermission = GetPermissionName(player);
                if (userPermission == null) return;

                // Checking for number of barrels hit
                if (!playerInfo.ContainsKey(player.UserIDString))
                    playerInfo.Add(player.UserIDString, 0);
                if (playerInfo[player.UserIDString] == givePointsEvery - 1)
                {
                    // Section that gives the player their money
                    if (useEconomy)
                    {
                        Economics?.CallHook("Deposit", player.userID, Convert.ToDouble(permissionList[userPermission]));
                        if (sendNotificationMessage)
                            player.ChatMessage(string.Format(msg("Economy Notice (Barrel)", player.UserIDString), permissionList[userPermission].ToString()));
                    }
                    if (useServerRewards)
                    {
                        ServerRewards?.Call("AddPoints", new object[] { player.userID, Convert.ToInt32(permissionList[userPermission]) });
                        if (sendNotificationMessage)
                            player.ChatMessage(string.Format(msg("RP Notice (Barrel)", player.UserIDString), permissionList[userPermission].ToString()));
                    }
                    playerInfo[player.UserIDString] = 0;
                }
                else
                    playerInfo[player.UserIDString]++;
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (!useCrates)
                return;
            if (entity.ShortPrefabName == "crate_mine" || entity.ShortPrefabName == "crate_normal" || entity.ShortPrefabName == "crate_normal_2" || entity.ShortPrefabName == "crate_normal_2_food" || entity.ShortPrefabName == "crate_normal_2_medical" || entity.ShortPrefabName == "crate_tools" || entity.ShortPrefabName == "heli_crate")
            {
                if (CrateCache.Contains(entity.net.ID))
                    CrateCache.Remove(entity.net.ID);
            }
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!useCrates)
                return;
            if (entity.ShortPrefabName == "crate_mine" || entity.ShortPrefabName == "crate_normal" || entity.ShortPrefabName == "crate_normal_2" || entity.ShortPrefabName == "crate_normal_2_food" || entity.ShortPrefabName == "crate_normal_2_medical" || entity.ShortPrefabName == "crate_tools" || entity.ShortPrefabName == "heli_crate")
            {
                if (CrateCache.Contains(entity.net.ID))
                    return;
                CrateCache.Add(entity.net.ID);
                string userPermission = GetPermissionName(player);
                if (userPermission == null) return;
                if (useEconomy)
                {
                    Economics?.CallHook("Deposit", player.userID, Convert.ToDouble(permissionList[userPermission]));
                    if (sendNotificationMessage)
                        player.ChatMessage(string.Format(msg("Economy Notice (Crate)", player.UserIDString), permissionList[userPermission].ToString()));
                }
                if (useServerRewards)
                {
                    ServerRewards?.Call("AddPoints", new object[] { player.userID, Convert.ToInt32(permissionList[userPermission]) });
                    if (sendNotificationMessage)
                        player.ChatMessage(string.Format(msg("RP Notice (Crate)", player.UserIDString), permissionList[userPermission].ToString()));
                }
            }
        }

        void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (resetBarrelsOnDeath)
                if (playerInfo.ContainsKey(player.UserIDString))
                    playerInfo[player.UserIDString] = 0;
        }

        string GetPermissionName(BasePlayer player)
        {
            Dictionary<string, int> perms = new Dictionary<string, int>();
            KeyValuePair<string, int> _perms = new KeyValuePair<string, int>(null, 0);
            foreach (var entry in permissionList)
                if (permission.UserHasPermission(player.UserIDString, entry.Key))
                    perms.Add(entry.Key, Convert.ToInt32(entry.Value));
            foreach (var entry in perms)
                if (Convert.ToInt32(entry.Value) > _perms.Value)
                    _perms = new KeyValuePair<string, int>(entry.Key, Convert.ToInt32(entry.Value));
            if (_perms.Key == null)
                return null;
            else
                return _perms.Key;
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}