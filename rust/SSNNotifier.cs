using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("SSNNotifier", "Umlaut", "0.0.8")]
    class SSNNotifier : RustPlugin
    {
        // Types defenition

        enum TimeRange
        {
            Hour = 0,
            Day = 1,
            Week = 2,
            Month = 3,
            Year = 4
        }

        public class JsonPlayer
        {
            [JsonProperty("steamid")]
            public ulong steamid { get; set; }

            [JsonProperty("display_name")]
            public string displayName { get; set; }

            public JsonPlayer(ulong _steamid, string _displayName)
            {
                steamid = _steamid;
                displayName = _displayName;
            }
        }

        public class JsonOnlinePlayer
        {
            [JsonProperty("player")]
            public JsonPlayer player { get; set; }

            [JsonProperty("ip_address")]
            public string ipAddress { get; set; }

            public JsonOnlinePlayer(ulong _steamid, string _displayName, string _ipAddress)
            {
                player = new JsonPlayer(_steamid, _displayName);
                ipAddress = _ipAddress;
            }
        }

        public class JsonPlayerBan
        {
            [JsonProperty("player")]
            public JsonPlayer player { get; set; }

            [JsonProperty("reason")]
            public string reason { get; set; }

            public JsonPlayerBan(ulong _steamid, string _displayName, string _reason)
            {
                player = new JsonPlayer(_steamid, _displayName);
                reason = _reason;
            }
        }

        public class JsonPlayerMute
        {
            [JsonProperty("player")]
            public JsonPlayer player { get; set; }

            [JsonProperty("reason")]
            public string reason { get; set; }

            public JsonPlayerMute(ulong _steamid, string _displayName, string _reason)
            {
                player = new JsonPlayer(_steamid, _displayName);
                reason = _reason;
            }
        }

        public class JsonPlayerChatMessage
        {
            [JsonProperty("player")]
            public JsonPlayer player { get; set; }

            [JsonProperty("message")]
            public string message { get; set; }

            public JsonPlayerChatMessage(BasePlayer _player, string _message)
            {
                player = new JsonPlayer(_player.userID, _player.displayName);
                message = _message;
            }
        }

        public class JsonItemDefinition
        {
            [JsonProperty("itemid")]
            public int itemid { get; set; }

            [JsonProperty("display_name")]
            public string displayName { get; set; }

            public JsonItemDefinition(ItemDefinition itemDefinition)
            {
                itemid = itemDefinition.itemid;
                displayName = ItemManager.CreateByItemID(itemid).info.displayName.english;
            }
        }

        public class JsonBroadcastMessages
        {
            [JsonProperty("interval")]
            public int interval { get; set; }

            [JsonProperty("messages")]
            public string[] messages { get; set; }
        }

        public class JsonServerStatus
        {
            [JsonProperty("online_players")]
            public JsonOnlinePlayer[] onlinePlayers { get; set; }
        }

        public class JsonMurder
        {
            [JsonProperty("victim_player")]
            public JsonPlayer victimPlayer { get; set; }

            [JsonProperty("killer_player")]
            public JsonPlayer killerPlayer { get; set; }

            [JsonProperty("weapon_item_definition")]
            public JsonItemDefinition weaponItemDefinition { get; set; }

            [JsonProperty("distance")]
            public double distance { get; set; }

            [JsonProperty("is_headshot")]
            public bool isHeadshot { get; set; }

            [JsonProperty("is_sleeping")]
            public bool isSleeping { get; set; }
        }

        class BanItem
        {
            public string timestamp;
            public string reason;

            public BanItem()
            {
                timestamp = "";
                reason = "";
            }
        }

        class MuteItem
        {
            public string timestamp = "";
            public string reason = "";
            public TimeRange level = TimeRange.Hour;

            public DateTime untilDatetime()
            {
                return DateTime.ParseExact(timestamp, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture) + timeSpan();
            }

            TimeSpan timeSpan()
            {
                switch (level)
                {
                    case TimeRange.Hour: return new TimeSpan(0, 1, 0, 0);
                    case TimeRange.Day: return new TimeSpan(1, 0, 0, 0);
                    case TimeRange.Week: return new TimeSpan(7, 1, 0, 0);
                    case TimeRange.Month: return new TimeSpan(28, 1, 0, 0);
                    case TimeRange.Year: return new TimeSpan(365, 1, 0, 0);
                    default: return new TimeSpan();
                }
            }
        }

        class ConfigData
        {
            public bool print_errors = true;
            public string server_name = "insert here name of your server";
            public string server_password = "insert here password of your server";

            public Dictionary<string, string> Messages = new Dictionary<string, string>();

            public Dictionary<ulong, BanItem> BannedPlayers = new Dictionary<ulong, BanItem>();
            public Dictionary<ulong, MuteItem> MutedPlayers = new Dictionary<ulong, MuteItem>();
            public HashSet<string> PlayersSyncAllowedServers = new HashSet<string>();
        }

        // Object vars

        ConfigData m_configData;
        WebRequests m_webRequests = Interface.GetMod().GetLibrary<WebRequests>("WebRequests");

        public string m_host = "survival-servers-network.com";
        public string m_port = "80";

        Dictionary<ulong, string> m_playersNames;
        Dictionary<ulong, List<ulong>> m_contextPlayers = new Dictionary<ulong, List<ulong>>();

        int m_broadcastMessagesInterval = 0;
        int m_broadcastMessagesCurrentIndex = 0;
        string[] m_broadcastMessages;
        Timer m_broadcastTimer;

        //

        void LoadConfig()
        {
            try
            {
                m_configData = Config.ReadObject<ConfigData>();
                if (m_configData.BannedPlayers == null)
                {
                    m_configData.BannedPlayers = new Dictionary<ulong, BanItem>();
                }
                if (m_configData.MutedPlayers == null)
                {
                    m_configData.MutedPlayers = new Dictionary<ulong, MuteItem>();
                }
                if (m_configData.PlayersSyncAllowedServers == null)
                {
                    m_configData.PlayersSyncAllowedServers = new HashSet<string>();
                }
                InsertDefaultMessages();
                SaveConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        void SaveConfig()
        {
            Config.WriteObject<ConfigData>(m_configData, true);
        }

        void LoadDynamic()
        {
            try
            {
                m_playersNames = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, string>>("PlayersNames");
                if (m_playersNames == null)
                {
                    m_playersNames = new Dictionary<ulong, string>();
                }
            }
            catch
            {
                m_playersNames = new Dictionary<ulong, string>();
            }
        }

        void SaveDynamic()
        {
            Interface.GetMod().DataFileSystem.WriteObject("PlayersNames", m_playersNames);
        }

        public void InsertDefaultMessage(string key, string message)
        {
            if (!m_configData.Messages.ContainsKey(key))
            {
                m_configData.Messages.Add(key, message);
            }
        }

        void InsertDefaultMessages()
        {
            InsertDefaultMessage("all_online_players_count", "All online players <color=cyan>%count</color>.");
            InsertDefaultMessage("invalid_arguments", "Invalid arguments.");
            InsertDefaultMessage("player_not_found", "Player not found.");
            InsertDefaultMessage("players_line", "<color=cyan>%number)</color> %player");
            InsertDefaultMessage("wellcome", "");

            InsertDefaultMessage("invalid_arguments", "Invalid arguments.");
            InsertDefaultMessage("have_not_permission", "You have not permission.");
            InsertDefaultMessage("player_not_found", "Player not found.");

            InsertDefaultMessage("player_was_not_banned", "Player <color=cyan>%player_name</color>(<color=cyan>%player_steamid</color>) was not banned.");
            InsertDefaultMessage("player_is_banned_already", "Player <color=cyan>%player_name</color>(<color=cyan>%player_steamid</color>) is banned already by reason \"<color=cyan>%reason</color>\".");
            InsertDefaultMessage("player_was_banned", "Player <color=cyan>%player_name</color>(<color=cyan>%player_steamid</color>) was banned by reason \"<color=cyan>%reason</color>\".");
            InsertDefaultMessage("player_was_unbanned", "Player <color=cyan>%player_name</color>(<color=cyan>%player_steamid</color>) was unbanned.");

            InsertDefaultMessage("player_was_not_muted", "Player <color=cyan>%player_name</color>(<color=cyan>%player_steamid</color>) was not muted.");
            InsertDefaultMessage("player_is_muted_already", "Player <color=cyan>%player_name</color>(<color=cyan>%player_steamid</color>) is muted already by reason \"<color=cyan>%reason</color> until <color=cyan>%until_datetime</color>(for <color=cyan>%level</color>)");
            InsertDefaultMessage("player_was_muted", "Player <color=cyan>%player_name</color>(<color=cyan>%player_steamid</color>) was muted by reason \"<color=cyan>%reason</color>\" until <color=cyan>%until_datetime</color>(for <color=cyan>%level</color>)");
            InsertDefaultMessage("player_was_unmuted", "Player <color=cyan>%player_name</color>(<color=cyan>%player_steamid</color>) was unmuted.");
            InsertDefaultMessage("player_save_unspent_xp_less_zero", "You can save your profile when unspent xp is greater or equal 1.");
            InsertDefaultMessage("player_save_server_now_allowed", "This server is not allowed for syncronization.");

            InsertDefaultMessage("broadcast_messages_count", "Count of broadcast messages: <color=cyan>%count</color>.");

            foreach (var timeRange in Enum.GetValues(typeof(TimeRange)))
            {
                InsertDefaultMessage(timeRange.ToString(), timeRange.ToString());
            }
        }

        // Hooks

        void Loaded()
        {
            LoadConfig();
            LoadDynamic();

            NotifyServerStatus();
            NotifyBroadcastMessagesUpdate();

            timer.Repeat(60, 0, () => SaveDynamic());
            timer.Repeat(60, 0, () => NotifyServerStatus());
            timer.Repeat(600, 0, () => NotifyBroadcastMessagesUpdate());

            checkPermission("SSNNotifier.mute");
            checkPermission("SSNNotifier.unmute");
            checkPermission("SSNNotifier.ban");
            checkPermission("SSNNotifier.unban");
            checkPermission("SSNNotifier.broadcast_messages_update");
        }

        void checkPermission(string _permission)
        {
            if (!permission.PermissionExists(_permission))
            {
                permission.RegisterPermission(_permission, this);
            }
        }

        private void Unload()
        {
            SaveDynamic();
        }

        void LoadDefaultConfig()
        {
            m_configData = new ConfigData();
            InsertDefaultMessages();
            Config.WriteObject(m_configData, true);
        }

        // Players hooks

        object CanClientLogin(Network.Connection connection)
        {
            ulong userID = connection.userid;
            if (m_configData.BannedPlayers.ContainsKey(userID))
            {
                string playerName = PlayerName(userID);

                string message = m_configData.Messages["player_was_banned"];
                message = message.Replace("%player_name", playerName);
                message = message.Replace("%player_steamid", userID.ToString());
                message = message.Replace("%reason", m_configData.BannedPlayers[userID].reason);

                return message;
            }

            return true;
        }

        void OnPlayerInit(BasePlayer player)
        {
            NotifyServerStatus();

            m_playersNames[player.userID] = player.displayName;
            if (m_configData.Messages["wellcome"].Length != 0)
                player.ChatMessage(m_configData.Messages["wellcome"]);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            NotifyServerStatus();
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null || hitInfo.Initiator == null)
            {
                return;
            }

            BasePlayer playerVictim = entity as BasePlayer;
            BasePlayer playerKiller = hitInfo.Initiator as BasePlayer;

            if (playerVictim == null || playerKiller == null || playerVictim == playerKiller)
            {
                return;
            }

            double distance = Math.Sqrt(
                Math.Pow(playerVictim.transform.position.x - playerKiller.transform.position.x, 2) +
                Math.Pow(playerVictim.transform.position.y - playerKiller.transform.position.y, 2) +
                Math.Pow(playerVictim.transform.position.z - playerKiller.transform.position.z, 2));

            NotifyPlayerKill(playerVictim, playerKiller, hitInfo.Weapon.GetItem().info, distance, hitInfo.isHeadshot, playerVictim.IsSleeping());
        }

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            string message = "";
            foreach (string line in arg.Args)
            {
                message += line + " ";
            }
            message = message.Trim();

            if (m_configData.MutedPlayers.ContainsKey(player.userID))
            {
                MuteItem muteItem = m_configData.MutedPlayers[player.userID];

                if (muteItem.untilDatetime() > DateTime.Now)
                {
                    message = m_configData.Messages["player_was_muted"];
                    message = message.Replace("%player_name", player.displayName);
                    message = message.Replace("%player_steamid", player.userID.ToString());
                    message = message.Replace("%reason", muteItem.reason);
                    message = message.Replace("%until_datetime", muteItem.untilDatetime().ToString("yyyy-MM-dd HH:mm:ss"));
                    message = message.Replace("%level", m_configData.Messages[muteItem.level.ToString()]);

                    player.ChatMessage(message);
                    return "handled";
                }
            }

            if (message != "" && message[0] != '/')
            {
                NotifyPlayerChatMessage(player, message);
            }

            return null;
        }

        // Chat commands

        [ChatCommand("players")]
        void cmdChatPlayers(BasePlayer player, string command, string[] args)
        {
            string filter = "";
            int linesCount = 0;

            // 

            if (args.Length == 1)
            {
                if (!int.TryParse(args[0], out linesCount))
                {
                    filter = args[0];
                }
            }
            else if(args.Length == 2)
            {
                filter = args[0];
                if (!int.TryParse(args[1], out linesCount))
                {
                    player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                }
            }
            else if (args.Length != 0)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
            }


            // Filtering by name

            List<BasePlayer> players = new List<BasePlayer>();
            foreach (BasePlayer currentPlayer in BasePlayer.activePlayerList)
            {
                if (filter != "" && !currentPlayer.displayName.Contains(filter, System.Globalization.CompareOptions.IgnoreCase))
                {
                    continue;
                }
                players.Add(currentPlayer);
            }

            // Sorting by name

            for (int f = 0; f < players.Count - 1; ++f)
            {
                for (int j = f + 1; j < players.Count; ++j)
                {
                    if (players[f].displayName.CompareTo(players[j].displayName) > 0)
                    {
                        BasePlayer tmpPlayer = players[f];
                        players[f] = players[j];
                        players[j] = tmpPlayer;
                    }
                }
            }

            // Context list

            int i = 0;
            List<ulong> contextPlayers = new List<ulong>();

            if (linesCount == 0)
            {
                foreach (BasePlayer currentPlayer in players)
                {
                    contextPlayers.Add(currentPlayer.userID);
                    player.ChatMessage(m_configData.Messages["players_line"].Replace("%number", (++i).ToString()).Replace("%player", currentPlayer.displayName) );
                }
            }
            else
            {
                List<BasePlayer> cPlayers = new List<BasePlayer>(players);
                int playerPerLine = (int)Math.Ceiling((double)players.Count/(double)linesCount);
                int index = 0;
                while (cPlayers.Count != 0)
                {
                    string line = "";
                    for (int z = 0; z < playerPerLine && cPlayers.Count != 0; ++z)
                    {
                        contextPlayers.Add(cPlayers[0].userID);
                        line += m_configData.Messages["players_line"].Replace("%number", (++index).ToString()).Replace("%player", cPlayers[0].displayName);
                        line += " ";
                        cPlayers.RemoveAt(0);
                    }
                    player.ChatMessage(line);
                }
            }
            player.ChatMessage(m_configData.Messages["all_online_players_count"].Replace("%count", BasePlayer.activePlayerList.Count.ToString()));
            SetContextPlayers(player.userID, contextPlayers);
        }

        [ChatCommand("ban")]
        void cmdBan(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNNotifier.ban"))
            {
                player.ChatMessage(m_configData.Messages["have_not_permission"]);
                return;
            }

            if (args.Length < 2)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            ulong userId = UserIdByAlias(player.userID, args[0]);

            if (userId == 0)
            {
                player.ChatMessage(m_configData.Messages["player_not_found"]);
                return;
            }
            string playerName = PlayerName(userId);
            string reason = "";
            for (int i = 1; i < args.Length; ++i)
            {
                reason += args[i];
                if (i < args.Length - 1)
                {
                    reason += " ";
                }
            }
            if (m_configData.BannedPlayers.ContainsKey(userId))
            {
                BanItem banItem = m_configData.BannedPlayers[userId];
                string message = m_configData.Messages["player_is_banned_already"];
                message = message.Replace("%player_name", playerName);
                message = message.Replace("%player_steamid", userId.ToString());
                message = message.Replace("%reason", banItem.reason);
                player.ChatMessage(message);
            }
            else
            {
                BanItem banItem = new BanItem();
                banItem.reason = reason;
                banItem.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                m_configData.BannedPlayers[userId] = banItem;
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "banid", userId, playerName, reason);
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "server.writecfg");
                SaveConfig();

                string message = m_configData.Messages["player_was_banned"];
                message = message.Replace("%player_name", playerName);
                message = message.Replace("%player_steamid", userId.ToString());
                message = message.Replace("%reason", banItem.reason);
                PrintToChat(message);

                BasePlayer targetPlayer = BasePlayer.FindByID(userId);
                if (targetPlayer != null)
                {
                    targetPlayer.Kick(message);
                }

                NotifyPlayerBan(userId, playerName, reason);
            }
        }

        [ChatCommand("unban")]
        void cmdUnban(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNNotifier.unban"))
            {
                player.ChatMessage(m_configData.Messages["have_not_permission"]);
                return;
            }

            if (args.Length != 1)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            ulong userID = UserIdByAlias(player.userID, args[0]);
            if (userID == 0)
            {
                player.ChatMessage(m_configData.Messages["player_not_found"]);
                return;
            }

            string playerName = PlayerName(userID);

            if (m_configData.BannedPlayers.ContainsKey(userID))
            {
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "unban", userID);
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "server.writecfg");

                m_configData.BannedPlayers.Remove(userID);
                SaveConfig();

                string message = m_configData.Messages["player_was_unbanned"];
                message = message.Replace("%player_name", playerName);
                message = message.Replace("%player_steamid", userID.ToString());
                PrintToChat(message);
            }
            else
            {
                string message = m_configData.Messages["player_was_not_banned"];
                message = message.Replace("%player_name", playerName);
                message = message.Replace("%player_steamid", userID.ToString());
                player.ChatMessage(message);
            }
        }

        [ChatCommand("bans")]
        void cmdChatBans(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 1)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            List<ulong> contextPlayers = new List<ulong>();
            foreach (ulong userID in m_configData.BannedPlayers.Keys)
            {
                string playerName = PlayerName(userID);
                if (args.Length == 1 && !playerName.Contains(args[0], System.Globalization.CompareOptions.IgnoreCase))
                {
                    continue;
                }

                BanItem banItem = m_configData.BannedPlayers[userID];
                contextPlayers.Add(userID);

                string message = m_configData.Messages["player_was_banned"];
                message = message.Replace("%player_name", playerName);
                message = message.Replace("%player_steamid", userID.ToString());
                message = message.Replace("%reason", banItem.reason);

                player.ChatMessage(contextPlayers.Count.ToString() + ") " + banItem.timestamp + " " + message);
            }
            SetContextPlayers(player.userID, contextPlayers);
        }

        [ChatCommand("mute")]
        void cmdChatMute(BasePlayer player, string command, string[] args)
        {
            string message;
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNNotifier.mute"))
            {
                player.ChatMessage(m_configData.Messages["have_not_permission"]);
                return;
            }

            if (args.Length < 2)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            ulong userID = UserIdByAlias(player.userID, args[0]);
            if (userID == 0)
            {
                player.ChatMessage(m_configData.Messages["player_not_found"]);
                return;
            }

            string playerName = PlayerName(userID);

            string reason = "";
            for (int i = 1; i < args.Length; ++i)
            {
                reason += args[i];
                if (i < args.Length - 1)
                {
                    reason += " ";
                }
            }

            MuteItem muteItem;
            if (m_configData.MutedPlayers.ContainsKey(userID))
            {
                muteItem = m_configData.MutedPlayers[userID];
                if (muteItem.untilDatetime() > DateTime.Now)
                {
                    message = m_configData.Messages["player_is_muted_already"];
                    message = message.Replace("%player_name", playerName);
                    message = message.Replace("%player_steamid", userID.ToString());
                    message = message.Replace("%reason", muteItem.reason);
                    message = message.Replace("%until_datetime", muteItem.untilDatetime().ToString("yyyy-MM-dd HH:mm:ss"));
                    message = message.Replace("%level", m_configData.Messages[muteItem.level.ToString()]);

                    player.ChatMessage(message);
                    return;
                }

                int intLevel = (int)muteItem.level + 1;
                if (intLevel > (int)TimeRange.Year)
                {
                    intLevel = (int)TimeRange.Year;
                }
                muteItem.level = (TimeRange)intLevel;
            }
            else
            {
                muteItem = new MuteItem();
            }

            muteItem.reason = reason;
            muteItem.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            m_configData.MutedPlayers[userID] = muteItem;
            SaveConfig();

            message = m_configData.Messages["player_was_muted"];
            message = message.Replace("%player_name", playerName);
            message = message.Replace("%player_steamid", userID.ToString());
            message = message.Replace("%reason", muteItem.reason);
            message = message.Replace("%until_datetime", muteItem.untilDatetime().ToString("yyyy-MM-dd HH:mm:ss"));
            message = message.Replace("%level", m_configData.Messages[muteItem.level.ToString()]);

            PrintToChat(message);

            NotifyPlayerMute(userID, playerName, reason);
        }

        [ChatCommand("unmute")]
        void cmdChatUnnute(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNNotifier.unmute"))
            {
                player.ChatMessage(m_configData.Messages["have_not_permission"]);
                return;
            }

            if (args.Length != 1)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            ulong userID = UserIdByAlias(player.userID, args[0]);
            if (userID == 0)
            {
                player.ChatMessage(m_configData.Messages["player_not_found"]);
                return;
            }

            string playerName = PlayerName(userID);

            if (m_configData.MutedPlayers.ContainsKey(userID))
            {
                MuteItem muteItem = m_configData.MutedPlayers[userID];
                if (muteItem.level == TimeRange.Hour)
                {
                    m_configData.MutedPlayers.Remove(userID);
                }
                else
                {
                    muteItem.level = (TimeRange)((int)muteItem.level - 1);
                }
                SaveConfig();

                string message = m_configData.Messages["player_was_unmuted"];
                message = message.Replace("%player_name", playerName);
                message = message.Replace("%player_steamid", userID.ToString());

                PrintToChat(message);
            }
            else
            {
                string message = m_configData.Messages["player_was_not_muted"];
                message = message.Replace("%player_name", playerName);
                message = message.Replace("%player_steamid", userID.ToString());

                player.ChatMessage(message);
            }
        }

        [ChatCommand("mutes")]
        void cmdChatMutes(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 1)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            List<ulong> contextPlayers = new List<ulong>();
            foreach (ulong userID in m_configData.MutedPlayers.Keys)
            {
                string playerName = PlayerName(userID);
                if (args.Length == 1 && !playerName.Contains(args[0], System.Globalization.CompareOptions.IgnoreCase))
                {
                    continue;
                }

                MuteItem muteItem = m_configData.MutedPlayers[userID];
                contextPlayers.Add(userID);

                string message = m_configData.Messages["player_was_muted"];
                message = message.Replace("%player_name", playerName);
                message = message.Replace("%player_steamid", userID.ToString());
                message = message.Replace("%reason", muteItem.reason);
                message = message.Replace("%until_datetime", muteItem.untilDatetime().ToString("yyyy-MM-dd HH:mm:ss"));
                message = message.Replace("%level", m_configData.Messages[muteItem.level.ToString()]);

                player.ChatMessage(contextPlayers.Count.ToString() + ") " + muteItem.timestamp + " " + message);
            }
            SetContextPlayers(player.userID, contextPlayers);
        }

        [ChatCommand("broadcast_messages")]
        void cmdChatBroadcastMessages(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 0)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }
            player.ChatMessage(m_configData.Messages["broadcast_messages_count"].Replace("%count", m_broadcastMessages.Length.ToString()));
            for (int i = 0; i < m_broadcastMessages.Length; ++i)
            {
                player.ChatMessage((i + 1).ToString() + ") " + m_broadcastMessages[i]);
            }
        }

        [ChatCommand("broadcast_messages_update")]
        void cmdChatBroadcastMessagesUpdate(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNNotifier.broadcast_messages_update"))
            {
                player.ChatMessage(m_configData.Messages["have_not_permission"]);
                return;
            }

            if (args.Length > 0)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }
            NotifyBroadcastMessagesUpdate();
        }

        //

        private ulong UserIdByAlias(ulong contextId, string alias)
        {
            if (alias.Length == 17)
            {
                ulong userId;
                if (ulong.TryParse(alias, out userId))
                {
                    return userId;
                }
            }
            int index;
            if (int.TryParse(alias, out index))
            {
                if (m_contextPlayers.ContainsKey(contextId) && (index - 1) < m_contextPlayers[contextId].Count)
                {
                    return m_contextPlayers[contextId][index - 1];
                }
            }
            return 0;
        }

        private void SetContextPlayers(ulong context, List<ulong> players)
        {
            m_contextPlayers[context] = players;
        }

        private string PlayerName(ulong userID)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.userID == userID)
                {
                    return player.displayName;
                }
            }

            if (m_playersNames.ContainsKey(userID))
            {
                return m_playersNames[userID];
            }
            else
            {
                return "unknown";
            }
        }

        private string GetServerName()
        {
            return m_configData.server_name;
        }

        // Web request/response

        private void SendWebRequest(string subUrl, string body, Action<int, string> callback)
        {
            string requestUrl = "http://%host:%port/api/%server_name/%suburl".Replace("%host", m_host).Replace("%port", m_port).Replace("%suburl", subUrl).Replace("%server_name", m_configData.server_name);

            Dictionary<string, string> headers = new Dictionary<string, string>();
            
            byte[] data = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(body + m_configData.server_password));
            StringBuilder sBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            headers.Add("salt", sBuilder.ToString());
            m_webRequests.EnqueuePost(requestUrl, body, callback, this, headers);
        }

        private void ReceiveWebResponse(int code, string response)
        {
            if (response == null)
            {
                if (m_configData.print_errors)
                {
                    Puts("Couldn't get an answer from SSN service.");
                }
            }
            else if (code != 200)
            {
                if (m_configData.print_errors)
                {
                    Puts("SSN error (%code): %text".Replace("%code", code.ToString()).Replace("%text", response));
                }
            }
        }

        private void ReceiveWebResponseBroadcastMessages(int code, string response)
        {
            if (response == null)
            {
                if (m_configData.print_errors)
                {
                    Puts("Couldn't get an answer from SSN service.");
                }
            }
            else if (code != 200)
            {
                if (m_configData.print_errors)
                {
                    Puts("SSN error (%code): %text".Replace("%code", code.ToString()).Replace("%text", response));
                }
            }

            JsonBroadcastMessages jsonBroadcastMessages = JsonConvert.DeserializeObject<JsonBroadcastMessages>(response);
            if (m_broadcastMessagesInterval != jsonBroadcastMessages.interval)
            {
                m_broadcastMessagesInterval = jsonBroadcastMessages.interval;
                if (m_broadcastMessagesInterval > 0)
                {
                    if (m_broadcastTimer == null)
                    {
                        m_broadcastTimer = timer.Repeat(m_broadcastMessagesInterval, 0, () => BroadcastNextMessage());
                    }
                    else
                    {
                        m_broadcastTimer.Reset(m_broadcastMessagesInterval, 0);
                    }
                }
            }
            m_broadcastMessages = jsonBroadcastMessages.messages;
        }

        private void BroadcastNextMessage()
        {
            if (m_broadcastMessagesCurrentIndex >= m_broadcastMessages.Length)
            {
                m_broadcastMessagesCurrentIndex = 0;
            }
            if (m_broadcastMessages.Length != 0)
            {
                PrintToChat(m_broadcastMessages[m_broadcastMessagesCurrentIndex++]);
            }
        }

        // Notifiers

        private void NotifyPlayerKill(BasePlayer victimPlayer, BasePlayer killerPlayer, ItemDefinition itemDefinition, double distance, bool isHeadshot, bool isSleeping)
        {
            JsonMurder jsonMurder = new JsonMurder();
            jsonMurder.victimPlayer = new JsonPlayer(victimPlayer.userID, victimPlayer.displayName);
            jsonMurder.killerPlayer = new JsonPlayer(killerPlayer.userID, killerPlayer.displayName);
            jsonMurder.weaponItemDefinition = new JsonItemDefinition(itemDefinition);
            jsonMurder.distance = distance;
            jsonMurder.isHeadshot = isHeadshot;
            jsonMurder.isSleeping = isSleeping;
            SendWebRequest("player/kill", JsonConvert.SerializeObject(jsonMurder), (code, response) => ReceiveWebResponse(code, response));
        }

        private void NotifyPlayerChatMessage(BasePlayer player, string messageText)
        {
            SendWebRequest("player/chat_message", JsonConvert.SerializeObject(new JsonPlayerChatMessage(player, messageText)), (code, response) => ReceiveWebResponse(code, response));
        }

        private void NotifyPlayerBan(ulong steamid, string displayName, string reason)
        {
            SendWebRequest("player/ban", JsonConvert.SerializeObject(new JsonPlayerBan(steamid, displayName, reason)), (code, response) => ReceiveWebResponse(code, response));
        }

        private void NotifyPlayerMute(ulong steamid, string displayName, string reason)
        {
            SendWebRequest("player/mute", JsonConvert.SerializeObject(new JsonPlayerMute(steamid, displayName, reason)), (code, response) => ReceiveWebResponse(code, response));
        }

        private void NotifyServerStatus()
        {
            JsonServerStatus jsonServerStatus = new JsonServerStatus();
            jsonServerStatus.onlinePlayers = new JsonOnlinePlayer[BasePlayer.activePlayerList.Count];
            for (int i = 0; i < BasePlayer.activePlayerList.Count; ++i)
            {
                jsonServerStatus.onlinePlayers[i] = new JsonOnlinePlayer(BasePlayer.activePlayerList[i].userID, BasePlayer.activePlayerList[i].displayName, BasePlayer.activePlayerList[i].net.connection.ipaddress.Split(':')[0]);
            }
            SendWebRequest("server/status", JsonConvert.SerializeObject(jsonServerStatus), (code, response) => ReceiveWebResponse(code, response));
        }

        private void NotifyBroadcastMessagesUpdate()
        {
            SendWebRequest("broadcast_messages", "", (code, response) => ReceiveWebResponseBroadcastMessages(code, response));
        }

    }
}
