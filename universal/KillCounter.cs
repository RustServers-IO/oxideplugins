using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Reflection;
using System.Text;

namespace Oxide.Plugins
{
    [Info("KillCounter", "SouZa and Mr. Blue", "1.0.8", ResourceId = 18063)]
    [Description("Creates a kill count for each player. Displays on the death notice.")]

    class KillCounter : HurtworldPlugin
    {
        #region Plugin References
        [PluginReference("HWClans")]
        private Plugin HWClans;
        [PluginReference("CustomDeathMessages")]
        private Plugin CustomDeathMessages;
        #endregion Plugin References

        #region Enums
        enum ELogType
        {
            Info,
            Warning,
            Error
        }
        #endregion

        #region Variables
        private static Dictionary<ulong, int> data = new Dictionary<ulong, int>();
        public string PermissionPrefix { get; set; }
        #endregion Variables

        #region Methods

        protected override void LoadDefaultConfig()
        {
            Log(ELogType.Warning, "No config file found, generating a new one.");
        }

        private new void LoadConfig()
        {
            SetConfig(false, "Settings", "Reset_all_onLoad", false);
            SetConfig(false, "Settings", "KillCount_Exclusion", "SameStake", true);
            SetConfig(false, "Settings", "KillCount_Exclusion", "SameClan", true);
            SaveConfig();
        }

        void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"no_permission", "You don't have permission to use this command.\nRequired: <color=orange>{perm}</color>."},
                {"kc_usage", "Type <color=orange>/kc</color> for proper usage."},
                {"player","<color=white>{Name} now has {Kills} kills.</color>"},
                {"playerkills","<color=white>You have {Kills} kill(s)!</color>"},
                {"playernokills","<color=white>You have no kills!</color>"},
                {"playertop","<color=white>========Top 5 players========</color>"},
                {"playertopline","<color=white>{Number}. {Name} has {Kills} kills.</color>"},
                {"player_offline", "<color=orange>{player}</color> is not online."},
                {"kc_reset_player", "<color=orange>{player}</color> kill count has been reseted."},
                {"kc_reset_all", "All players kill count have been reseted."},
                {"kc_commands_list", "<color=yellow>Available KillCounter Commands</color>"},
                {"kc_cmds", "<color=orange>/kc reset <player></color> - Resets a player kill count.{newcmd}<color=orange>/kc resetall</color> - Resets all players kill count."}
            };

            lang.RegisterMessages(messages, this);
        }

        void LoadData()
        {
            if (!GetConfig(false, "Settings", "Reset_all_onLoad"))
                data = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, int>>("KillCounter");
            else
            {
                data = new Dictionary<ulong, int>();
                SaveData();
            }
        }
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("KillCounter", data);
        }

        void LoadPermissions()
        {
            PermissionPrefix = Regex.Replace(Title, "[^0-9a-zA-Z]+", string.Empty).ToLower();
            RegisterPermission("mod");
            RegisterPermission("use");
        }

        void Loaded()
        {
            LoadConfig();
            LoadDefaultMessages();
            LoadData();
            LoadPermissions();
        }

        //Permissions

        public void RegisterPermission(params string[] paramArray)
        {
            var perms = ArrayToString(paramArray, ".");
            permission.RegisterPermission(
                perms.StartsWith(PermissionPrefix) ? $"{perms}" : $"{PermissionPrefix}.{perms}",
                this);
        }

        public bool HasPermission(PlayerSession session, params string[] paramArray)
        {
            var perms = ArrayToString(paramArray, ".");
            return permission.UserHasPermission(session.SteamId.m_SteamID.ToString(),
                perms.StartsWith(PermissionPrefix) ? $"{perms}" : $"{PermissionPrefix}.{perms}");
        }

        public string ArrayToString(string[] array, string separator)
        {
            return string.Join(separator, array);
        }

        //Configs

        void SetConfig(bool replace, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList<string>();
            stringArgs.RemoveAt(args.Length - 1);

            if (replace || Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

        public T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList<string>();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }

        string ListToString(List<string> list, int first, string seperator)
        {
            return String.Join(seperator, list.Skip(first).ToArray());
        }


        //Ownership

        public List<OwnershipStakeServer> GetStakesFromPlayer(PlayerSession session)
        {
            var stakes = Resources.FindObjectsOfTypeAll<OwnershipStakeServer>();
            if (stakes != null)
            {
                return
                    stakes.Where(
                        s =>
                            !s.IsDestroying && s.gameObject != null && s.gameObject.activeSelf &&
                            s.AuthorizedPlayers.Contains(session.Identity)).ToList();
            }
            return new List<OwnershipStakeServer>();
        }

        //Player Information
        bool IsValidSession(PlayerSession session)
        {
            return session != null && session?.SteamId != null && session.IsLoaded && session.Name != null && session.Identity != null &&
                   session.WorldPlayerEntity?.transform?.position != null;
        }

        string GetPlayerName(ulong steamid)
        {
            foreach (PlayerIdentity identity in GameManager.Instance.GetIdentifierMap().Values)
            {
                if (identity.SteamId.m_SteamID.Equals(steamid))
                {
                    return identity.Name.ToString();
                }
            }

            return "Unknown";
        }

        ulong GetSteamID(string identifier)
        {
            foreach (PlayerIdentity identity in GameManager.Instance.GetIdentifierMap().Values)
            {
                PlayerSession session = identity.ConnectedSession;
                if (IsValidSession(session) && session.Name.ToLower().Equals(identifier.ToLower()))
                {
                    return session.SteamId.m_SteamID;
                }
            }

            return ulong.MinValue;
        }

        //Others
        void Log(ELogType type, string message)
        {
            switch (type)
            {
                case ELogType.Info:
                    Puts(message);
                    break;
                case ELogType.Warning:
                    PrintWarning(message);
                    break;
                case ELogType.Error:
                    PrintError(message);
                    break;
            }
        }

        string GetNameOfObject(UnityEngine.GameObject obj)
        {
            var ManagerInstance = GameManager.Instance;
            return ManagerInstance.GetDescriptionKey(obj);
        }

        private bool isValidKill(PlayerIdentity victim, PlayerIdentity killer)
        {
            bool sameStake = GetConfig(false, "Settings", "KillCount_Exclusion", "SameStake");
            bool sameClan = GetConfig(false, "Settings", "KillCount_Exclusion", "SameClan");

            if (sameStake)
            {
                List<OwnershipStakeServer> victim_stakes = GetStakesFromPlayer(victim.ConnectedSession);
                List<OwnershipStakeServer> killer_stakes = GetStakesFromPlayer(killer.ConnectedSession);

                foreach (OwnershipStakeServer killer_stake in killer_stakes)
                {
                    if (victim_stakes.Contains(killer_stake))
                    {
                        return false;
                    }
                }
            }
            if (sameClan && HWClans != null)
            {
                ulong victim_clanID = (ulong)HWClans.Call("getClanId", victim.ConnectedSession);
                ulong killer_clanID = (ulong)HWClans.Call("getClanId", killer.ConnectedSession);

                if (victim_clanID == killer_clanID)
                {
                    if (victim_clanID != 0)
                        return false;
                }
            }

            return true;
        }

        #endregion Methods

        #region Chat Commands

        [ChatCommand("killtop")]
        void cmdKCKillTop(PlayerSession session, string command, string[] args)
        {
            if (!HasPermission(session, "use") && !session.IsAdmin)
            {
                Player.Message(session, lang.GetMessage("no_permission", this)
                        .Replace("{perm}", PermissionPrefix + ".use"));
                return;
            }

            Player.Message(session, lang.GetMessage("playertop", this));
            var top5 = data.OrderByDescending(pair => pair.Value).Take(5);
            var cycle = 1;
            foreach (var kills in top5)
            {
                string playername = GetPlayerName(kills.Key);
                Player.Message(session, lang.GetMessage("playertopline", this).Replace("{Number}", cycle.ToString()).Replace("{Name}", playername).Replace("{Kills}", kills.Value.ToString()));
                cycle++;
            }

        }
        [ChatCommand("kills")]
        void cmdKCKills(PlayerSession session, string command, string[] args)
        {
            if (!HasPermission(session, "use") && !session.IsAdmin)
            {
                Player.Message(session, lang.GetMessage("no_permission", this)
                        .Replace("{perm}", PermissionPrefix + ".use"));
                return;
            }
            
            if (!data.ContainsKey(session.SteamId.m_SteamID))
                Player.Message(session, lang.GetMessage("playernokills", this));
            else
            {
                var kills = data[session.SteamId.m_SteamID];
                Player.Message(session, lang.GetMessage("playerkills", this).Replace("{Kills}", kills.ToString()));
            }

        }



        [ChatCommand("kc")]
        void cmdKC(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!HasPermission(session, "mod") && !session.IsAdmin)
            {
                Player.Message(session, lang.GetMessage("no_permission", this)
                        .Replace("{perm}", PermissionPrefix + ".mod"));
                return;
            }

            if (args.Length == 0)
            {
                // [/kc]
                string[] kcCMDS = lang.GetMessage("kc_cmds", this).Split(new string[] { "{newcmd}" }, StringSplitOptions.RemoveEmptyEntries);

                Player.Message(session, lang.GetMessage("kc_commands_list", this));
                foreach (string cmd in kcCMDS)
                {
                    Player.Message(session, cmd);
                }
            }
            else if (args.Length == 1 && args[0] == "resetall")
            {
                data = new Dictionary<ulong, int>();
                SaveData();
                Player.Message(session, lang.GetMessage("kc_reset_all", this));
            }
            else if (args.Length == 2 && args[0] == "reset")
            {
                var player_steamID = GetSteamID(args[1]);

                if (player_steamID == ulong.MinValue)
                    Player.Message(session, lang.GetMessage("player_offline", this).Replace("{player}", args[1]));
                else
                {
                    var playerName = GameManager.Instance.GetIdentity(player_steamID).Name;
                    if (!data.ContainsKey(player_steamID))
                        data.Add(player_steamID, 0);

                    data[player_steamID] = 0;
                    SaveData();
                    Player.Message(session, lang.GetMessage("kc_reset_player", this).Replace("{player}", playerName));
                }
            }
            else
                Player.Message(session, lang.GetMessage("kc_usage", this));
        }
        #endregion Chat Commands

        #region Hooks
        string AddKill(PlayerSession playerSession, EntityEffectSourceData dataSource)
        {
            string victim_name = playerSession.Name;
            var victim_steamID = GetSteamID(victim_name);
            var victim_identity = GameManager.Instance.GetIdentity(victim_steamID);

            string killer_name = GetNameOfObject(dataSource.EntitySource);

            if (killer_name != "")
            {
                if (killer_name.Length >= 3)
                    killer_name = killer_name.Substring(0, killer_name.Length - 3);
                var killer_steamID = GetSteamID(killer_name);

                var killer_identity = GameManager.Instance.GetIdentity(killer_steamID);

                if (killer_identity == null)
                {
                    return null;
                }

                if (!data.ContainsKey(killer_steamID))
                    data.Add(killer_steamID, 0);

                if (isValidKill(victim_identity, killer_identity))
                    data[killer_steamID] += 1;

                SaveData();

                var kills = data[killer_steamID].ToString();
                return kills;
            }
            return null;
        }

        private void OnPlayerDeath(PlayerSession playerSession, EntityEffectSourceData dataSource)
        {
            if (CustomDeathMessages == null)
            {
                string killer_name = GetNameOfObject(dataSource.EntitySource);
                if (killer_name.Length >= 3)
                    killer_name = killer_name.Substring(0, killer_name.Length - 3);

                var killerkills = AddKill(playerSession, dataSource);
                if (killerkills != null)
                {
                    timer.Once(1f, () => {
                        Server.Broadcast(lang.GetMessage("player", this).Replace("{Name}", killer_name).Replace("{Kills}", killerkills));
                    });
                }
            }
        }
        #endregion Hooks
    }
}
