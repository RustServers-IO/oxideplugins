using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("BetterChat Mute", "LaserHydra", "1.1.0", ResourceId = 2272)]
    [Description("Mute plugin, made for use with Better Chat")]
    internal class BetterChatMute : CovalencePlugin
    {
        private static Dictionary<string, MuteInfo> mutes = new Dictionary<string, MuteInfo>();
        private bool hasExpired = false;
        private bool globalMute = false;

        #region Classes

        public class MuteInfo
        {
            public DateTime ExpireDate = DateTime.MinValue;

            [JsonIgnore]
            public bool Timed => ExpireDate != DateTime.MinValue;

            [JsonIgnore]
            public bool Expired => Timed && ExpireDate < DateTime.UtcNow;

            public string Reason { get; set; }

            public static bool IsMuted(IPlayer player) => mutes.ContainsKey(player.Id);

            public static readonly DateTime NonTimedExpireDate = DateTime.MinValue;

            public MuteInfo()
            {
            }

            public MuteInfo(DateTime expireDate, string reason)
            {
                ExpireDate = expireDate;
                Reason = reason;
            }
        }

        #endregion

        #region Hooks

        private void Loaded()
        {
            permission.RegisterPermission("betterchatmute.permanent", this);

            LoadData(ref mutes);
            SaveData(mutes);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You don't have permission to use this command.",
                ["No Reason"] = "Unknown reason",
                ["Muted"] = "{player} was muted by {initiator}: {reason}.",
                ["Muted Time"] = "{player} was muted by {initiator} for {time}: {reason}.",
                ["Unmuted"] = "{player} was unmuted by {initiator}.",
                ["Not Muted"] = "{player} is currently not muted.",
                ["Mute Expired"] = "{player} is no longer muted.",
                ["Invalid Time Format"] = "Invalid time format. Example: 1d2h3m4s = 1 day, 2 hours, 3 min, 4 sec",
                ["Nobody Muted"] = "There is nobody muted at the moment.",
                ["Invalid Syntax Mute"] = "/mute <player|steamid> \"[reason]\" [time: 1d1h1m1s]",
                ["Invalid Syntax Unmute"] = "/unmute <player|steamid>",
                ["Player Name Not Found"] = "Could not find player with name '{name}'",
                ["Player ID Not Found"] = "Could not find player with ID '{id}'",
                ["Multiple Players Found"] = "Multiple matching players found: \n{matches}",
                ["Time Muted Player Joined"] = "{player} is temporarily muted. Remaining time: {time}",
                ["Time Muted Player Chat"] = "You may not chat, you are temporarily muted. Remaining time: {time}",
                ["Muted Player Joined"] = "{player} is permanently muted.",
                ["Muted Player Chat"] = "You may not chat, you are permanently muted.",
                ["Global Mute Enabled"] = "Global mute was enabled. Nobody can chat while global mute is active.",
                ["Global Mute Disabled"] = "Global mute was disabled. Everybody can chat again.",
                ["Global Mute Active"] = "Global mute is active, you may not chat."
            }, this);

            timer.Repeat(10, 0, () =>
            {
                List<string> expired = mutes.Where(m => m.Value.Expired).Select(m => m.Key).ToList();

                foreach (string id in expired)
                {
                    IPlayer player = players.FindPlayerById(id);

                    mutes.Remove(id);
                    PublicMessage("Mute Expired", new KeyValuePair<string, string>("player", player?.Name));

                    Interface.CallHook("OnBetterChatMuteExpired", player);

                    if (!hasExpired)
                        hasExpired = true;
                }

                if (hasExpired)
                {
                    SaveData(mutes);
                    hasExpired = false;
                }
            });
        }

        private object OnUserChat(IPlayer player, string message)
        {
            object result = HandleChat(player);

            if (result is bool && !(bool)result)
            {
                if (!MuteInfo.IsMuted(player) && globalMute)
                {
                    player.Reply(lang.GetMessage("Global Mute Active", this, player.Id));
                }
                else if (mutes[player.Id].Timed)
                {
                    player.Reply(
                        lang.GetMessage("Time Muted Player Chat", this, player.Id)
                            .Replace("{time}", FormatTime(mutes[player.Id].ExpireDate - DateTime.UtcNow))
                   );
                }
                else
                {
                    player.Reply(lang.GetMessage("Muted Player Chat", this, player.Id));
                } 
            }

            return result;
        }

        private object OnBetterChat(Dictionary<string, object> messageData) => HandleChat((IPlayer) messageData["Player"]);

        private void OnUserInit(IPlayer player)
        {
            UpdateMuteStatus(player);

            if (MuteInfo.IsMuted(player))
            {
                if (mutes[player.Id].Timed)
                    PublicMessage("Time Muted Player Joined",
                        new KeyValuePair<string, string>("player", player.Name), 
                        new KeyValuePair<string, string>("time", FormatTime(mutes[player.Id].ExpireDate - DateTime.UtcNow)));
                else
                    PublicMessage("Muted Player Joined", new KeyValuePair<string, string>("player", player.Name));
            }
        }

        #endregion

        #region API

        private void API_Mute(IPlayer target, IPlayer player, string reason = "", bool callHook = true, bool broadcast = true)
        {
            mutes[target.Id] = new MuteInfo(MuteInfo.NonTimedExpireDate, reason);
            SaveData(mutes);

            reason = string.IsNullOrEmpty(reason) ? lang.GetMessage("No Reason", this) : reason;

            if (callHook)
                Interface.CallHook("OnBetterChatMuted", target, player, reason);

            if (broadcast)
            {
                PublicMessage("Muted",
                    new KeyValuePair<string, string>("initiator", player.Name),
                    new KeyValuePair<string, string>("player", target.Name),
                    new KeyValuePair<string, string>("reason", reason));
            }
        }

        private void API_TimeMute(IPlayer target, IPlayer player, TimeSpan timeSpan, string reason = "", bool callHook = true, bool broadcast = true)
        {
            mutes[target.Id] = new MuteInfo(DateTime.UtcNow + timeSpan, reason);
            SaveData(mutes);

            reason = string.IsNullOrEmpty(reason) ? lang.GetMessage("No Reason", this) : reason;

            if (callHook)
                Interface.CallHook("OnBetterChatTimeMuted", target, player, timeSpan,
                    string.IsNullOrEmpty(reason) ? lang.GetMessage("No Reason", this) : reason);

            if (broadcast)
            {
                PublicMessage("Muted Time",
                    new KeyValuePair<string, string>("initiator", player.Name),
                    new KeyValuePair<string, string>("player", target.Name),
                    new KeyValuePair<string, string>("time", FormatTime(timeSpan)),
                    new KeyValuePair<string, string>("reason", reason));
            }
        }

        private bool API_Unmute(IPlayer target, IPlayer player, bool callHook = true, bool broadcast = true)
        {
            if (!MuteInfo.IsMuted(target))
                return false;

            mutes.Remove(target.Id);
            SaveData(mutes);

            if (callHook)
                Interface.CallHook("OnBetterChatUnmuted", target, player);

            if (broadcast)
            {
                PublicMessage("Unmuted",
                    new KeyValuePair<string, string>("initiator", player.Name),
                    new KeyValuePair<string, string>("player", target.Name));
            }

            return true;
        }

        private void API_SetGlobalMute(bool state)
        {
            globalMute = state;

            if (globalMute)
                PublicMessage("Global Mute Enabled");
            else
                PublicMessage("Global Mute Disabled");
        }

        private List<string> API_GetMuteList() => mutes.Keys.ToList();

        private bool API_GetGlobalMuteState() => globalMute;

        private bool API_IsMuted(IPlayer player) => mutes.ContainsKey(player.Id);

        #endregion

        #region Commands

        [Command("toggleglobalmute"), Permission("betterchatmute.use.global")]
        private void CmdGlobalMute(IPlayer player, string cmd, string[] args)
        {
            globalMute = !globalMute;

            if (globalMute)
                PublicMessage("Global Mute Enabled");
            else
                PublicMessage("Global Mute Disabled");
        }

        [Command("mutelist"), Permission("betterchatmute.use")]
        private void CmdMuteList(IPlayer player, string cmd, string[] args)
        {
            if (mutes.Count == 0)
                player.Reply(lang.GetMessage("Nobody Muted", this, player.Id));
            else
                player.Reply(string.Join(Environment.NewLine, mutes.Select(kvp => $"{players.FindPlayerById(kvp.Key).Name}: {FormatTime(kvp.Value.ExpireDate - DateTime.UtcNow)}").ToArray()));
        }

        [Command("mute"), Permission("betterchatmute.use")]
        private void CmdMute(IPlayer player, string cmd, string[] args)
        {
            if (args.Length == 0)
            {
                player.Reply(lang.GetMessage("Invalid Syntax Mute", this, player.Id));
                return;
            }

            IPlayer target;
            string reason = string.Empty;

            switch (args.Length)
            {
                case 1:
                case 2:
                    if (!permission.UserHasPermission(player.Id, "betterchatmute.permanent") && player.Id != "server_console")
                    {
                        player.Reply(lang.GetMessage("No Permission", this, player.Id));
                        return;
                    }

                    target = GetPlayer(args[0], player);

                    if (target == null)
                        return;

                    if (args.Length == 2)
                        reason = args[1];

                    reason = string.IsNullOrEmpty(reason) ? lang.GetMessage("No Reason", this) : reason;

                    mutes[target.Id] = new MuteInfo(MuteInfo.NonTimedExpireDate, reason);
                    SaveData(mutes);

                    Interface.CallHook("OnBetterChatMuted", target, player, reason);

                    PublicMessage("Muted",
                        new KeyValuePair<string, string>("initiator", player.Name),
                        new KeyValuePair<string, string>("player", target.Name),
                        new KeyValuePair<string, string>("reason", reason));

                    break;

                case 3:
                    target = GetPlayer(args[0], player);

                    if (target == null)
                        return;

                    TimeSpan timeSpan;

                    if (!TryParseTimeSpan(args[2], out timeSpan))
                    {
                        player.Reply(lang.GetMessage("Invalid Time Format", this, player.Id));
                        return;
                    }

                    reason = args[1];
                    reason = string.IsNullOrEmpty(reason) ? lang.GetMessage("No Reason", this) : reason;

                    mutes[target.Id] = new MuteInfo(DateTime.UtcNow + timeSpan, reason);
                    SaveData(mutes);

                    Interface.CallHook("OnBetterChatTimeMuted", target, player, timeSpan, reason);

                    PublicMessage("Muted Time",
                        new KeyValuePair<string, string>("initiator", player.Name),
                        new KeyValuePair<string, string>("player", target.Name),
                        new KeyValuePair<string, string>("time", FormatTime(timeSpan)),
                        new KeyValuePair<string, string>("reason", reason));

                    break;

                default:
                    player.Reply(lang.GetMessage("Invalid Syntax Mute", this, player.Id));
                    break;
            }
        }

        [Command("unmute"), Permission("betterchatmute.use")]
        private void CmdUnmute(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 1)
            {
                player.Reply(lang.GetMessage("Invalid Syntax Unmute", this, player.Id));
                return;
            }

            IPlayer target = GetPlayer(args[0], player);

            if (target == null)
                return;

            if (!MuteInfo.IsMuted(target))
            {
                player.Reply(lang.GetMessage("Not Muted", this, player.Id).Replace("{player}", target.Name));
                return;
            }

            mutes.Remove(target.Id);
            SaveData(mutes);

            Interface.CallHook("OnBetterChatUnmuted", target, player);

            PublicMessage("Unmuted",
                new KeyValuePair<string, string>("initiator", player.Name),
                new KeyValuePair<string, string>("player", target.Name));
        }

        #endregion

        #region Helpers

        private void PublicMessage(string key, params KeyValuePair<string, string>[] replacements)
        {
            string message = lang.GetMessage(key, this);

            foreach (var replacement in replacements)
                message = message.Replace($"{{{replacement.Key}}}", replacement.Value);

            server.Broadcast(message);
            Puts(message);
        }

        private object HandleChat(IPlayer player)
        {
            UpdateMuteStatus(player);

            var result = Interface.CallHook("OnBetterChatMuteHandle", player, MuteInfo.IsMuted(player) ? JObject.FromObject(mutes[player.Id]) : null);

            if (result != null)
                return null;

            if (MuteInfo.IsMuted(player))
                return false;

            if (globalMute && !permission.UserHasPermission(player.Id, "betterchatmute.use.global"))
                return false;

            return null;
        }

        private void UpdateMuteStatus(IPlayer player)
        {
            if (MuteInfo.IsMuted(player) && mutes[player.Id].Expired)
            {
                mutes.Remove(player.Id);
                SaveData(mutes);

                PublicMessage("Mute Expired", new KeyValuePair<string, string>("player", players.FindPlayerById(player.Id)?.Name));

                Interface.CallHook("OnBetterChatMuteExpired", player);
            }
        }

        private IPlayer GetPlayer(string nameOrID, IPlayer player)
        {
            if (IsParseableTo<ulong>(nameOrID) && nameOrID.StartsWith("7656119") && nameOrID.Length == 17)
            {
                IPlayer result = players.All.ToList().Find((p) => p.Id == nameOrID);

                if (result == null)
                    player.Reply(lang.GetMessage("Player ID Not Found", this, player.Id).Replace("{id}", nameOrID));

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in players.Connected)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    player.Reply(lang.GetMessage("Player Name Not Found", this, player.Id).Replace("{name}", nameOrID));
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    string[] names = (from current in foundPlayers select current.Name).ToArray();
                    player.Reply(lang.GetMessage("Multiple Players Found", this, player.Id).Replace("{matches}", string.Join(", ", names)));
                    break;
            }

            return null;
        }

        private bool IsParseableTo<T>(object s)
        {
            try
            {
                var parsed = (T)Convert.ChangeType(s, typeof(T));
                return true;
            }
            catch
            {
                return false;
            }
        }

        #region DateTime

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

        #endregion

        #region Data & Config Helper

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        private string DataFileName => Title.Replace(" ", "");

        private void LoadData<T>(ref T data, string filename = null) => data = Core.Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? DataFileName);

        private void SaveData<T>(T data, string filename = null) => Core.Interface.Oxide.DataFileSystem.WriteObject(filename ?? DataFileName, data);

        #endregion

        #endregion
    }
}