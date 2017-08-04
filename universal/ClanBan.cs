﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("ClanBan", "Slut", "1.2.0")]
    internal class ClanBan : CovalencePlugin
    {
        [PluginReference] private Plugin Clans, DiscordMessages, EnhancedBanSystem, BetterChatMute;

        private bool AnnounceToServer = true;
        private int type;

        private void Init()
        {
            LoadConfiguration();
            RegisterPermissions();
        }

        private void LoadConfiguration()
        {
            CheckCfg("Clan Ban - Ban Type (Disabled = 0, EBS = 1, Vanilla = 2, DiscordMessages = 3)", ref type);
            CheckCfg("Clan Ban - Announce To Server", ref AnnounceToServer);

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Generating new config.");
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission("clanban.ban", this);
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SyntaxError"] = "Syntax error.",
                ["Disabled"] = "This feature is disabled, please change the config.",
                ["PlayerBanned"] = "{0} was banned for {1}",
                ["PlayerUnbanned"] = "{0} was unbanned.",
                ["AlreadyBanned"] = "{0} is already banned.",
                ["AlreadyUnbanned"] = "{0} is already unbanned.",
                ["BanMessage"] = "The clan ({0}) was banned for {1}.",
                ["UnbanMessage"] = "The clan ({0}) was unbanned.",
                ["MuteMessage"] = "The clan ({0}) was muted permanently muted.",
                ["TimeMuteMessage"] = "The clan ({0}) was muted for {1}",
                ["UnmuteMessage"] = "The clan ({0}) was unmuted.",
                ["KickedMessage"] = "The clan ({0}) was kicked.",
                ["NoClan"] = "The clan ({0}) doesn't exist.",
                ["DefaultBanReason"] = "Your clan was banned from the server.",
                ["DefaultKickReason"] = "Your clan was kicked from the server.",
                ["NoValidFunction"] = "You must specify a function, /cb (function) (clan) <reason/time>"
            }, this, "en");
        }
        [Command("clanban", "cb"), Permission("clanban.ban")]
        private void ClanBanCommand(IPlayer player, string command, string[] args)
        {
            if (type == 0)
            {
                SendMessage(player, GetLang("Disabled", player.Id));
                return;
            }
            if (args.Length < 2)
            {
                SendMessage(player, GetLang("SyntaxError", player.Id));
                return;
            }
            string clan = args[1];
            if (GetClan(clan, player) != null)
            {
                switch (args[0])
                {
                    case "ban":
                        string reason = args.Length == 2 ? GetLang("DefaultBanReason", player.Id) : string.Join(" ", args.Skip(2).ToArray());
                        ProcessBan(player, GetClan(clan, player), reason);
                        break;
                    case "unban":
                        ProcessUnban(player, GetClan(clan, player));
                        break;
                    case "mute":
                        ProcessMute(player, GetClan(clan, player), args[2] != null ? args[2] : null);
                        break;
                    case "unmute":
                        ProcessUnmute(player, GetClan(clan, player));
                        break;
                    case "kick":
                        string reason1 = args.Length == 2 ? GetLang("DefaultKickReason", player.Id) : string.Join(" ", args.Skip(2).ToArray());
                        ProcessKick(player, GetClan(clan, player), reason1);
                        break;
                    default:
                        player.Reply(GetLang("NoValidFunction", player.Id));
                        break;
                }
            }
        }
        private JObject GetClan(string tag, IPlayer player)
        {
            JObject clan = (JObject)Clans.Call("GetClan", tag);
            if (clan == null)
            {
                SendMessage(player, string.Format(GetLang("NoClan", player.Id), tag));
                return null;
            }
            return clan;
        }
        private void ProcessBan(IPlayer player, JObject clan, string reason)
        {
            string reason1 = "CLAN BAN (" +clan["tag"] + ") : " + reason;
            foreach (var member in clan["members"])
            {
                var target = covalence.Players.FindPlayerById(Convert.ToString(member));
                if (type == 1)
                    EnhancedBanSystem.Call("BanPlayer", player.Name, target, reason1, 0.0);
                if (type == 2)
                {
                    var exists = ServerUsers.Get(ulong.Parse(target.Id));
                    if (exists != null && ServerUsers.Get(ulong.Parse(target.Id)).group == ServerUsers.UserGroup.Banned)
                    {
                        SendMessage(player, string.Format(GetLang("AlreadyBanned", player.Id), target.Name));
                        return;
                    }
                    ServerUsers.Set(ulong.Parse(target.Id), ServerUsers.UserGroup.Banned, target.Name, reason1);
                    ServerUsers.Save();
                    server.Broadcast(string.Format(GetLang("PlayerBanned", null), target.Name, reason));
                    if (target.IsConnected)
                        target.Kick(reason);
                }
                if (type == 3) DiscordMessages.Call("ExecuteBan", target, player, reason1);
            }
            if (AnnounceToServer)
                server.Broadcast(string.Format(GetLang("BanMessage", null), clan["tag"], reason));
        }
        private void ProcessUnban(IPlayer player, JObject clan)
        {
            foreach (var member in clan["members"])
            {
                var target = covalence.Players.FindPlayerById(Convert.ToString(member));
                if (type == 1) {
                    List<string> args = new List<string>();
                    args.Add(player.Id);
                    EnhancedBanSystem.Call("TryUnban", player.Name, args);
                }
                else
                {
                    var exists = ServerUsers.Get(ulong.Parse(target.Id));
                    if (exists != null && exists.group != ServerUsers.UserGroup.Banned)
                    {
                        SendMessage(player, string.Format(GetLang("AlreadyUnbanned", player.Id), target.Name));
                        return;
                    }
                    ServerUsers.Remove(ulong.Parse(target.Id));
                    ServerUsers.Save();
                    server.Broadcast(string.Format(GetLang("PlayerUnbanned", null), target.Name));
                }
            }
            if (AnnounceToServer)
                server.Broadcast(string.Format(GetLang("UnbanMessage", null), clan["tag"]));
        }

        string FormatTime(TimeSpan time) => $"{(time.Days == 0 ? string.Empty : $"{time.Days} day(s)")}{(time.Days != 0 && time.Hours != 0 ? $", " : string.Empty)}{(time.Hours == 0 ? string.Empty : $"{time.Hours} hour(s)")}{(time.Hours != 0 && time.Minutes != 0 ? $", " : string.Empty)}{(time.Minutes == 0 ? string.Empty : $"{time.Minutes} minute(s)")}{(time.Minutes != 0 && time.Seconds != 0 ? $", " : string.Empty)}{(time.Seconds == 0 ? string.Empty : $"{time.Seconds} second(s)")}";

        private bool TryParseTimeSpan(string source, out TimeSpan timeSpan)
        {
            int seconds = 0, minutes = 0, hours = 0, days = 0;

            Match s = new Regex(@"(\d+?)s", RegexOptions.IgnoreCase).Match(source);
            Match m = new Regex(@"(\d+?)m", RegexOptions.IgnoreCase).Match(source);
            Match h = new Regex(@"(\d+?)h", RegexOptions.IgnoreCase).Match(source);
            Match d = new Regex(@"(\d+?)d", RegexOptions.IgnoreCase).Match(source);

            if (s.Success)
                seconds = Convert.ToInt32(s.Groups[1].ToString());

            if (m.Success)
                minutes = Convert.ToInt32(m.Groups[1].ToString());

            if (h.Success)
                hours = Convert.ToInt32(h.Groups[1].ToString());

            if (d.Success)
                days = Convert.ToInt32(d.Groups[1].ToString());

            source = source.Replace(seconds + "s", string.Empty);
            source = source.Replace(minutes + "m", string.Empty);
            source = source.Replace(hours + "h", string.Empty);
            source = source.Replace(days + "d", string.Empty);

            if (!string.IsNullOrEmpty(source) || (!s.Success && !m.Success && !h.Success && !d.Success))
            {
                timeSpan = default(TimeSpan);
                return false;
            }

            timeSpan = new TimeSpan(days, hours, minutes, seconds);

            return true;
        }
        private void ProcessMute(IPlayer player, JObject clan, string time)
        {
            if (BetterChatMute != null)
            {
                TimeSpan _timespan;
                if (!TryParseTimeSpan(time, out _timespan))
                {
                    foreach (var member in clan["members"])
                    {
                        IPlayer target = covalence.Players.FindPlayerById(Convert.ToString(member));
                        BetterChatMute.Call("API_Mute", target, player, true, true);
                        if (AnnounceToServer)
                        {
                            server.Broadcast(string.Format(GetLang("MuteMessage", null), clan["tag"]));
                        }
                    }
                }
                else
                {
                    foreach (var member in clan["members"])
                    {
                        IPlayer target = covalence.Players.FindPlayerById(Convert.ToString(member));
                        BetterChatMute.Call("API_TimeMute", target, player, _timespan, true, false);
                        if (AnnounceToServer)
                        {
                            server.Broadcast(string.Format(GetLang("TimeMuteMessage", null), clan["tag"], FormatTime(_timespan)));
                        }
                    }
                }
            } else
            {
                player.Reply("BetterChatMute is not loaded!");
            }
        }
        private void ProcessUnmute(IPlayer player, JObject clan)
        {
            if (BetterChatMute != null)
            {
                foreach (var member in clan["members"])
                {
                    IPlayer target = covalence.Players.FindPlayerById(Convert.ToString(member));
                    BetterChatMute.Call("API_Unmute", target, player, true, false);
                }
                if (AnnounceToServer)
                {
                    server.Broadcast(string.Format(GetLang("UnmuteMessage", null), clan["tag"]));
                }
            }
            else
            {
                player.Reply("BetterChatMute is not loaded!");
            }
        }
        private void ProcessKick(IPlayer player, JObject clan, string reason)
        {
            foreach(var member in clan["members"])
            {
                IPlayer target = covalence.Players.FindPlayerById(Convert.ToString(member));
                target.Kick(reason);
            }
            if(AnnounceToServer)
            {
                server.Broadcast(string.Format(GetLang("KickedMessage", null), clan["tag"]));
            }
        }

        private void SendMessage(IPlayer player, string message)
        {
            player.Reply(message);
        }

        private string GetLang(string key, string id)
        {
            return lang.GetMessage(key, this, id);
        }
    }
}