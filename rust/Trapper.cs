using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Trapper", "redBDGR", "1.0.4", ResourceId = 2417)]
    [Description("Adds a few new features to traps")]

    class Trapper : RustPlugin
    {
        [PluginReference] Plugin Friends;

        private bool Changed = false;
        private float resetTime = 5f;
        private bool hurtOwner = false;
        private bool hurtFriends = false;

        private const string permissionName = "trapper.auto";
        private const string permissionNameOWNER = "trapper.owner";
        private const string permissionNameFRIENDS = "trapper.friends";

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
            permission.RegisterPermission(permissionNameOWNER, this);
            permission.RegisterPermission(permissionNameFRIENDS, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            resetTime = Convert.ToSingle(GetConfig("Settings", "Reset Time", 5f));
            hurtOwner = Convert.ToBoolean(GetConfig("Settings", "Trigger for Owner", true));
            hurtFriends = Convert.ToBoolean(GetConfig("Settings", "Trigger for Friends", true));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        void Loaded()
        {
            if (hurtFriends)
                if (!Friends)
                {
                    PrintError("Friends.cs was not found! all friend functions of this plugin have been disabled");
                    hurtFriends = false;
                }
        }

        object OnTrapTrigger(BaseTrap _trap, GameObject obj)
        {
            if (_trap is BearTrap || _trap is Landmine)
            {
                BasePlayer player = FindPlayer(_trap.OwnerID.ToString());
                if (!player) return null;
                if (!hurtOwner || !hurtFriends)
                {
                    BasePlayer target = obj.GetComponent<BasePlayer>();
                    if (target)
                    {
                        if (hurtOwner)
                            if (target == player)
                                if (permission.UserHasPermission(target.UserIDString, permissionNameOWNER))
                                    return false;
                        if (hurtFriends)
                            if (Convert.ToBoolean(Friends?.CallHook("AreFriends", target.userID, player.userID)))
                                if (permission.UserHasPermission(player.UserIDString, permissionNameFRIENDS))
                                    return false;
                    }
                }

                if (_trap is BearTrap)
                    if (!player || permission.UserHasPermission(player?.UserIDString, permissionName))
                        timer.Once(resetTime, () => ((BearTrap)_trap).Arm());
            }
            return null;
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

        private static BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString == nameOrId)
                    return sleepingPlayer;
                if (sleepingPlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return sleepingPlayer;
            }
            return null;
        }
    }
}