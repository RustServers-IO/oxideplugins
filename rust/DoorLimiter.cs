using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Configuration;
using System.Reflection;
using System;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("DoorLimiter", "redBDGR", "1.0.3", ResourceId = 2334)]
    [Description("Only allow a certain number of people to use one door")]
    class DoorLimiter : CovalencePlugin
    {
        bool Changed = false;

        public bool silentMode = false;
        public bool saveTimerEnabled = true;
        public int authedPlayersAllowed = 5;
        public const string permissionName = "doorlimiter.exempt";
        public const string permissionNameADMIN = "doorlimiter.admin";
        public const string permissionNameREMOVE = "doorlimiter.remove";
        public float saveTime = 600.0f;

        Dictionary<uint, List<string>> cacheDictionary = new Dictionary<uint, List<string>>();

        FieldInfo _whitelistPlayers = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Instance | BindingFlags.NonPublic));

        #region Data

        private DynamicConfigFile DoorLimiterData;
        StoredData storedData;

        class StoredData
        {
            public Dictionary<uint, List<string>> DoorData = new Dictionary<uint, List<string>>();
        }

        void SaveData()
        {
            storedData.DoorData = cacheDictionary;
            DoorLimiterData.WriteObject(storedData);
        }

        void LoadData()
        {
            try
            {
                storedData = DoorLimiterData.ReadObject<StoredData>();
                cacheDictionary = storedData.DoorData;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        #endregion

        void Init()
        {
            DoorLimiterData = Interface.Oxide.DataFileSystem.GetFile("DoorLimiter");
            LoadData();
            permission.RegisterPermission(permissionName, this);
            permission.RegisterPermission(permissionNameADMIN, this);
            LoadVariables();
            DoLang();


            if (saveTimerEnabled)
                timer.Repeat(saveTime, 0, () =>
                {
                    SaveData();
                });
        }

        void Unload()
        {
            SaveData();
        }

        void DoLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Max Authorised"] = "There are already too many players authorized to this door!",
                ["Auth Successful"] = "You have been successfully authorized to this door!",
                ["Database Wipe"] = "GroupDoorLimit database successfully wiped",
                ["No Perms"] = "You are not allowed to use this command!",
                ["Invalid Syntax REMOVE"] = "Invalid syntax! /doorlimit remove <playername / id>",
                ["No Player Found"] = "No players with that name / steamid were found",
                ["No Entity Found"] = "No entity was found! make sure you are looking at a door",
                ["Entity Not Registered"] = "This door was not found in the database!",
                ["You Are Not The Owner"] = "You are not the owner of this door!",
                ["Player Not Authed To This Door"] = "The target player is not authorised to this door!",
                ["Player Removed"] = "The player was succesfully removed from the doors authorized list",
                ["doorlimit Help"] = "Type /doorlimit remove <playername / id> whilst looking at a door to remove them from the authorization list",

            }, this);
        }

        #region Handling

        object CanUseLockedEntity(BasePlayer player, BaseLock baselock)
        {
            if (player == null || baselock == null) return null;
            if (permission.UserHasPermission(player.UserIDString, permissionName)) return null;
            if (!(baselock.GetParentEntity() is BaseNetworkable)) return null;
            BaseNetworkable door = baselock.GetParentEntity() as BaseNetworkable;
            if (baselock.ShortPrefabName == "lock.code")
            {
                CodeLock codelock = (CodeLock)baselock;
                if (!(_whitelistPlayers.GetValue(codelock) as List<ulong>).Contains(player.userID)) return null;
                if (cacheDictionary.ContainsKey(door.net.ID))
                {
                    if (cacheDictionary[door.net.ID].Contains(player.UserIDString))
                        return null;

                    else
                    {
                        if (cacheDictionary[door.net.ID].Count >= authedPlayersAllowed)
                        {
                            if (!silentMode)
                                player.ChatMessage(msg("Max Authorised", player.UserIDString));
                            return false;
                        }
                        else
                        {
                            cacheDictionary[door.net.ID].Add(player.UserIDString);
                            if (!silentMode)
                                player.ChatMessage(msg("Auth Successful", player.UserIDString));
                            return null;
                        }
                    }
                }
                else
                {
                    cacheDictionary.Add(door.net.ID, new List<string>());
                    cacheDictionary[door.net.ID].Add(player.UserIDString);
                    return null;
                }
            }
            else
                return null;
        }

        void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            BaseEntity entity = gameObject.ToBaseEntity();
            var ownerID = entity.OwnerID;
            if (ownerID != 0)
            {
                if (entity.ShortPrefabName.Contains("door.hinged"))
                {
                    if (!cacheDictionary.ContainsKey(entity.net.ID))
                    {
                        BasePlayer player = BasePlayer.FindByID(ownerID);
                        cacheDictionary.Add(entity.net.ID, new List<string>());
                        cacheDictionary[entity.net.ID].Add(player.UserIDString);
                    }
                    else
                    {
                        BasePlayer player = BasePlayer.FindByID(ownerID);
                        cacheDictionary.Remove(entity.net.ID);
                        cacheDictionary.Add(entity.net.ID, new List<string>());
                        cacheDictionary[entity.net.ID].Add(player.UserIDString);
                    }
                }
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null) return;
            if (entity.ShortPrefabName.Contains("door.hinged"))
            {
                if (cacheDictionary.ContainsKey(entity.net.ID))
                    cacheDictionary.Remove(entity.net.ID);
            }
        }

        void OnStructureDemolish(BaseCombatEntity entity, BasePlayer player)
        {
            if (entity == null) return;
            if (entity.ShortPrefabName.Contains("door.hinged"))
            {
                if (cacheDictionary.ContainsKey(entity.net.ID))
                    cacheDictionary.Remove(entity.net.ID);
            }
        }

        #endregion

        void LoadVariables()
        {
            silentMode = Convert.ToBoolean(GetConfig("Settings", "Silent Mode", true));
            authedPlayersAllowed = Convert.ToInt32(GetConfig("Settings", "Authed Players Allowed", 5));
            saveTime = Convert.ToSingle(GetConfig("Settings", "Save Time", 600.0f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        [Command("wipedoordata")]
        void wipedoordataCMD(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(permissionNameADMIN) || player.IsAdmin)
            {
                cacheDictionary.Clear();
                Puts(msg("Database Wipe"));
            }
        }

        // used for manual / automated saving
        [ConsoleCommand("savedoordata")]
        void savedoordataCMD(ConsoleSystem.Arg args)
        {
            if (args.Connection != null) return;
            SaveData();
        }

        [ChatCommand("doorlimit")]
        void doorlimitCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameREMOVE))
            {
                player.ChatMessage(msg("No Perms", player.UserIDString));
                return;
            }

            if (args.Length == 2)
            {
                if (args[0] == "remove")
                {
                    RaycastHit hitInfo;
                    if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hitInfo, 3.0f))
                    {
                        player.ChatMessage(msg("No Entity Found", player.UserIDString));
                        return;
                    }
                    BaseEntity entity = hitInfo.transform.GetComponentInParent<BaseEntity>();

                    if (DoEntityChecks(entity, player) == false)
                        return;

                    BasePlayer targetplayer = FindPlayer(args[1]);
                    if (DoPlayerChecks(entity, player) == false)
                        return;

                    cacheDictionary[entity.net.ID].Remove(targetplayer.UserIDString);
                    player.ChatMessage(msg("Player Removed", player.UserIDString));
                }
                else
                {
                    player.ChatMessage(msg("Invalid Syntax REMOVE", player.UserIDString));
                    return;
                }
            }
            else
                DoDoorLimitHelp(player);
        }

        bool DoEntityChecks(BaseEntity entity, BasePlayer player)
        {
            if (entity == null)
            {
                player.ChatMessage(msg("No Entity Found", player.UserIDString));
                return false;
            }
            else if (!entity.ShortPrefabName.Contains("door.hinged"))
            {
                player.ChatMessage(msg("Not a Door", player.UserIDString));
                return false;
            }
            else if (!cacheDictionary.ContainsKey(entity.net.ID))
            {
                player.ChatMessage(msg("Entity Not Registered", player.UserIDString));
                return false;
            }
            else if (entity.OwnerID != player.userID)
            {
                player.ChatMessage(msg("You Are Not The Owner", player.UserIDString));
                return false;
            }
            else
                return true;
        }

        bool DoPlayerChecks(BaseEntity entity, BasePlayer player)
        {
            if (player == null)
            {
                player.ChatMessage(msg("No Player Found", player.UserIDString));
                return false;
            }
            else if (!cacheDictionary[entity.net.ID].Contains(player.UserIDString))
            {
                player.ChatMessage(msg("Player Not Authed To This Door", player.UserIDString));
                return false;
            }
            else
                return true;
        }

        void DoDoorLimitHelp(BasePlayer player)
        {
            player.ChatMessage(msg("doorlimit Help", player.UserIDString));
            return;
        }

        private static BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrId)
                    return activePlayer;
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

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}