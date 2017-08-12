using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("DiscordMessages", "Slut", "1.5.0", ResourceId = 2486)]
    class DiscordMessages : CovalencePlugin
    {
        #region Classes
        public class Cooldowns
        {
            public DateTime reportCooldown { get; set; }
            public DateTime messageCooldown { get; set; }
        }

        public class SavedMessages {
            public string url { get; set; }
            public string payload { get; set; }
            public float time { get; set; }

            public SavedMessages(string url, string payload, float time)
            {
                this.url = url;
                this.payload = payload;
                this.time = time;
            }

        }
        public class FancyMessage {
            public string content { get; set; }
            public bool tts { get; set; }
            public Embeds[] embeds { get; set; }

            public class Embeds {
                public string title { get; set; }
                public int color { get; set; }
                public List<Fields> fields { get; set; }
                public Embeds(string title, int color, List<Fields> fields) {
                    this.title = title;
                    this.color = color;
                    this.fields = fields;
                }
            }

            public FancyMessage(string content, bool tts, Embeds[] embeds) {
                this.content = content;
                this.tts = tts;
                this.embeds = embeds;
            }

            public string toJSON(FancyMessage fancymessage) => JsonConvert.SerializeObject(fancymessage);
        }
        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }
        #endregion
        #region Variables
        [PluginReference] private Plugin BetterChatMute;

        List<SavedMessages> savedmessages = new List<SavedMessages>();
        private Dictionary<string, Cooldowns> cooldowns = new Dictionary<string, Cooldowns>();
        private DiscordMessages Plugin;

        #endregion

        #region Config Variables

        private string BanURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private string ReportURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private string MuteURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private string MessageURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private bool ReportEnabled = true;
        private bool BanEnabled = true;
        private bool MessageEnabled = true;
        private bool MessageAlert = false;
        private bool ReportAlert = false;
        private bool MuteEnabled;
        private bool Announce = true;
        private int ReportCooldown = 30;
        private int MessageCooldown = 15;
        private int ReportColor = 3329330;
        private int MuteColor = 3329330;
        private int BanColor = 3329330;
        private int MessageColor = 3329330;

        #endregion

        #region Hooks / Config

        private void Init()
        {
            LoadConfiguration();
            RegisterPermissions();
            if (!BanEnabled && !ReportEnabled && !MessageEnabled && !MuteEnabled)
            {
                PrintWarning("All functions are disabled. Please enable at least one.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            if (BanURL == "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks" && BanEnabled)
            {
                PrintWarning("Bans enabled but webhook not setup!");
                BanEnabled = false;
            }
            if (ReportURL == "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks" &&
                ReportEnabled)
            {
                PrintWarning("Reports enabled but webhook not setup!");
                ReportEnabled = false;
            }
            if (MessageURL == "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks" &&
                MessageEnabled)
            {
                PrintWarning("Message enabled but webhook not setup!");
                MessageEnabled = false;
            }
            if (MuteURL == "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks" &&
                MuteEnabled)
            {
                PrintWarning("Mutes enabled but webhook not setup!");
                MuteEnabled = false;
            }
            if (MuteURL == "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks" &&
                MuteEnabled || BetterChatMute?.IsLoaded == true)
                if (BetterChatMute == null)
                {
                    PrintWarning("Mutes enabled but not setup correctly!");
                    MuteEnabled = false;
                }
            RegisterCommands();
        }

        private void LoadConfiguration()
        {
            CheckCfg<string>("Bans - Webhook URL", ref BanURL);
            CheckCfg<bool>("Bans - Enabled", ref BanEnabled);
            CheckCfg<bool>("Bans - Broadcast to server", ref Announce);
            CheckCfg<bool>("Mutes - Enabled", ref MuteEnabled);
            CheckCfg<string>("Mutes - Webhook URL", ref MuteURL);
            CheckCfg<string>("Reports - Webhook URL", ref ReportURL);
            CheckCfg<bool>("Reports - Enabled", ref ReportEnabled);
            CheckCfg<bool>("Reports - Alert Channel", ref ReportAlert);
            CheckCfg<int>("Reports - Cooldown", ref ReportCooldown);
            CheckCfg<bool>("Message - Enabled", ref MessageEnabled);
            CheckCfg<string>("Message - Webhook URL", ref MessageURL);
            CheckCfg<int>("Message - Cooldown", ref MessageCooldown);
            CheckCfg<bool>("Message - Alert Channel", ref MessageAlert);
            CheckCfg<int>("Message - Embed Color (DECIMAL)", ref MessageColor);
            CheckCfg<int>("Reports - Embed Color (DECIMAL)", ref ReportColor);
            CheckCfg<int>("Ban - Embed Color (DECIMAL)", ref BanColor);
            CheckCfg<int>("Mute - Embed Color (DECIMAL)", ref MuteColor);

            SaveConfig();
        }

        private void RegisterCommands()
        {
            if (ReportEnabled) AddCovalenceCommand("report", "ReportCommand", "discordmessages.report");
            if (BanEnabled) AddCovalenceCommand("ban", "BanCommand", "discordmessages.ban");
            if (MessageEnabled) AddCovalenceCommand("message", "MessageCommand", "discordmessages.message");
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission("discordmessages.ban", this);
            permission.RegisterPermission("discordmessages.report", this);
            permission.RegisterPermission("discordmessages.message", this);
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
                ["ReportSyntax"] = "Syntax error. Please use /report \"name/id\" \"reason\"",
                ["BanSyntax"] = "Syntax error. Please use /ban \"name/id\" \"reason\"",
                ["MessageSyntax"] = "Syntax error. Please use /message \"your message\"",
                ["Multiple"] = "Multiple players found:\n{0}",
                ["BanMessage"] = "{0} was banned for {1}",
                ["ReportSent"] = "Your report has been sent!",
                ["MessageSent"] = "Your message has been sent!",
                ["NotFound"] = "Unable to find player {0}",
                ["Cooldown"] = "You must wait {0} seconds to use this command again.",
                ["AlreadyBanned"] = "{0} is already banned!",
                ["NoPermission"] = "You do not have permision for this command!",
                ["Disabled"] = "This feature is currently disabled.",
                ["Failed"] = "Your report failed to send, contact the server owner.",
                ["ToSelf"] = "You cannot perform this action on yourself.",
                ["BanPrefix"] = "Banned: {0}",
                ["Embed_ReportPlayer"] = "Reporter",
                ["Embed_ReportTarget"] = "Reported",
                ["Embed_ReportReason"] = "Reason",
                ["Embed_Online"] = "Online",
                ["Embed_Offline"] = "Offline",
                ["Embed_ReportStatus"] = "Status",
                ["Embed_ReportTitle"] = "Player Report",
                ["Embed_MuteTitle"] = "Player Muted",
                ["Embed_MuteTarget"] = "Player",
                ["Embed_MutePlayer"] = "Muted by",
                ["Embed_BanPlayer"] = "Banned by",
                ["Embed_BanTarget"] = "Player",
                ["Embed_BanReason"] = "Reason",
                ["Embed_BanTitle"] = "Player Ban",
                ["Embed_MessageTitle"] = "Player Message",
                ["Embed_MessagePlayer"] = "Player",
                ["Embed_MessageMessage"] = "Message",
                ["Embed_MuteTime"] = "Time"
            }, this, "en");
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Generating new config.");
        }

        private string GetLang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        private void SendMessage(IPlayer player, string message)
        {
            player.Reply(message);
        }

        #endregion

        #region API
        private void API_SendFancyMessage(string webhookURL, string embedName, int embedColor, List<Fields> fields)
        {
            if (embedColor == 0)
            {
                embedColor = 3329330;
            }
            FancyMessage message = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(embedName, 3329330, fields) });
            var payload = message.toJSON(message);
            SendPOST(webhookURL, payload);
        }
        #endregion

        #region Webrequest

        Timer _timer;
        private void RateTimer()
        {
            if (savedmessages.Count == 0)
            {
                _timer.Destroy();
                return;
            }
            else
            {
                SendPOST(savedmessages[0].url, savedmessages[0].payload);
                _timer = timer.Once(savedmessages[0].time, () => RateTimer());
                return;
            }
        }

        private void SendPOST(string url, string payload)
        {
            bool exists = savedmessages.Exists(x => x.payload == payload);
            webrequest.EnqueuePost(url, payload, (code, response) =>
            {
                if (response == null || (code != 200) & (code != 204))
                {
                    if (response != null)
                    {
                        JObject json = JObject.Parse(response);
                        if (json["message"].ToString().Contains("rate limit") && exists == false)
                        {
                            float seconds = float.Parse(Math.Ceiling((double) (int)json["retry_after"] / 1000).ToString());
                            savedmessages.Add(new SavedMessages(url, payload, seconds));
                            if (_timer == null || _timer.Destroyed)
                            {
                                RateTimer();
                            }
                        } else
                        {
                            PrintWarning($"Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                        }
                    } else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }
                else
                {
                    if (exists == true)
                    {
                        savedmessages.RemoveAt(0);
                    }
                }
            }, this);
        }

        #endregion

        #region Message

        private bool onMessageCooldown(IPlayer player)
        {
            if (cooldowns.ContainsKey(player.Id))
                if (cooldowns[player.Id].messageCooldown.AddSeconds(MessageCooldown) > DateTime.Now)
                {
                    return true;
                }
            return false;
        }

        private void MessageCommand(IPlayer player, string command, string[] args, Action<bool> callback)
        {
            if (!MessageEnabled)
                return;
            if (args.Length < 1)
            {
                SendMessage(player, GetLang("MessageSyntax", player.Id));
                return;
            }
            if (onMessageCooldown(player))
            {
                var time = (cooldowns[player.Id].messageCooldown.AddSeconds(MessageCooldown) - DateTime.Now).Seconds;
                SendMessage(player, GetLang("Cooldown", player.Id, time));

                return;
            }
            var text = string.Join(" ", args.ToArray());
            List<Fields> fields = new List<Fields>();
            fields.Add(new Fields(GetLang("Embed_MessagePlayer"), $"[{ player.Name }](https://steamcommunity.com/profiles/{player.Id})", true));
            fields.Add(new Fields(GetLang("Embed_MessageMessage"), text, false));
            FancyMessage message = new FancyMessage(MessageAlert == true ? "@here" : null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(GetLang("Embed_MessageTitle"), MessageColor, fields) });
            var payload = message.toJSON(message);
            SendPOST(MessageURL, payload);
            SendMessage(player, GetLang("MessageSent", player.Id));
            if (cooldowns.ContainsKey(player.Id))
            cooldowns[player.Id].messageCooldown = DateTime.Now;
            else
                cooldowns.Add(player.Id, new Cooldowns {messageCooldown = DateTime.Now});
        }

        #endregion


        #region Report

        private bool onReportCooldown(IPlayer player)
        {
            if (cooldowns.ContainsKey(player.Id))
                if (cooldowns[player.Id].reportCooldown.AddSeconds(ReportCooldown) > DateTime.Now)
                {
                    return true;
                }
            return false;
        }

        private void ReportCommand(IPlayer player, string command, string[] args, Action<bool> callback = null)
        {
            if ((player.Name == "Server Console") | !player.IsConnected)
                return;
            if (ReportEnabled == false)
            {
                SendMessage(player, GetLang("Disabled", player.Id));
                return;
            }
            if (onReportCooldown(player))
            {
                var time = (cooldowns[player.Id].reportCooldown.AddSeconds(ReportCooldown) - DateTime.Now).Seconds;
                SendMessage(player, GetLang("Cooldown", player.Id, time));

                return;
            }
            if (player == null)
                return;
            if (args.Length < 2)
            {
                SendMessage(player, GetLang("ReportSyntax", player.Id));
                return;
            }
            var target = GetPlayer(args[0], player);
            var reason = string.Join(" ", args.Skip(1).ToArray());

            if (target != null)
            {
                if (target.Equals(player))
                {
                    SendMessage(player, GetLang("ToSelf", player.Id));
                    return;
                }
                var status = target.IsConnected ? lang.GetMessage("Online", null) : lang.GetMessage("Offline", null);
                List<Fields> fields = new List<Fields>();
                fields.Add(new Fields(GetLang("Embed_ReportTarget"), $"[{target.Name}](https://steamcommunity.com/profiles/{target.Id})", true));
                fields.Add(new Fields(GetLang("Embed_ReportPlayer"), $"[{player.Name}](https://steamcommunity.com/profiles/{player.Id})", true));
                fields.Add(new Fields(GetLang("Embed_ReportStatus"), status, true));
                fields.Add(new Fields(GetLang("Embed_ReportReason"), reason, false));
                FancyMessage message = new FancyMessage(ReportAlert == true ? "@here": null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(GetLang("Embed_MessageTitle"), ReportColor, fields) });

                SendPOST(ReportURL, message.toJSON(message));
                SendMessage(player, GetLang("ReportSent", player.Id));
                if (cooldowns.ContainsKey(player.Id))
                    cooldowns[player.Id].reportCooldown = DateTime.Now;
                else
                    cooldowns.Add(player.Id, new Cooldowns { reportCooldown = DateTime.Now});
            }
        }

        #endregion

        #region Mutes

        string FormatTime(TimeSpan time) => $"{(time.Days == 0 ? string.Empty : $"{time.Days} day(s)")}{(time.Days != 0 && time.Hours != 0 ? $", " : string.Empty)}{(time.Hours == 0 ? string.Empty : $"{time.Hours} hour(s)")}{(time.Hours != 0 && time.Minutes != 0 ? $", " : string.Empty)}{(time.Minutes == 0 ? string.Empty : $"{time.Minutes} minute(s)")}{(time.Minutes != 0 && time.Seconds != 0 ? $", " : string.Empty)}{(time.Seconds == 0 ? string.Empty : $"{time.Seconds} second(s)")}";

        private void OnBetterChatTimeMuted(IPlayer target, IPlayer player, TimeSpan expireDate, Action<bool> callback) => SendMute(target, player, expireDate, true, callback);

        private void OnBetterChatMuted(IPlayer target, IPlayer player, Action<bool> callback) => SendMute(target, player, TimeSpan.Zero, false, callback);

        private void SendMute(IPlayer target, IPlayer player, TimeSpan expireDate, bool timed, Action<bool> callback)
        {
            if (!MuteEnabled)
                return;
            List<Fields> fields = new List<Fields>();
            fields.Add(new Fields(GetLang("Embed_MuteTarget"), $"[{target.Name}](https://steamcommunity.com/profiles/{target.Id})", true));
            fields.Add(new Fields(GetLang("Embed_MutePlayer"), !player.Id.Equals("server_console") ? $"[{player.Name}](https://steamcommunity.com/profiles/{player.Id})" : player.Name, true));
            fields.Add(new Fields(GetLang("Embed_MuteTime"), timed ? FormatTime(expireDate) : "Permanent", true));
            FancyMessage message = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(GetLang("Embed_MuteTitle"), MuteColor, fields) });
            SendPOST(MuteURL, message.toJSON(message));
        }

        #endregion

        #region Bans

        private void BanCommand(IPlayer player, string command, string[] args)
        {
            if (BanEnabled == false)
            {
                SendMessage(player, GetLang("Disabled", player.Id));
                return;
            }
            if (args.Length == 0)
            {
                SendMessage(player, GetLang("BanSyntax", player.Id));
                return;
            }
            var reason = args.Length == 1 ? "Banned" : string.Join(" ", args.Skip(1).ToArray());
            var target = GetPlayer(args[0], player);
            if (target != null)
            {
                if (target == player)
                {
                    SendMessage(player, GetLang("ToSelf", player.Id));
                    return;
                }
                ExecuteBan(target, player, reason);
            }
            else if (target == null)
            {
                ExectueBanNotExists(args[0], player, reason);
            }
        }

        private void ExecuteBan(IPlayer target, IPlayer player, string reason)
        {
            var exists = ServerUsers.Get(ulong.Parse(target.Id));
            if (exists != null && ServerUsers.Get(ulong.Parse(target.Id)).group == ServerUsers.UserGroup.Banned)
            {
                SendMessage(player, GetLang("AlreadyBanned", player.Id, target.Name));
            }
            else
            {
                ServerUsers.Set(ulong.Parse(target.Id), ServerUsers.UserGroup.Banned, target.Name, reason);
                ServerUsers.Save();
                if (Announce) server.Broadcast(GetLang("BanMessage", null, target.Name, reason));
                if (target.IsConnected)
                    target.Kick(GetLang("BanPrefix", target.Id, reason));
                SendBanMessage(target.Name, target.Id, reason, player.Name, player.Id);
            }
        }

        private void ExectueBanNotExists(string input, IPlayer player, string reason)
        {
            ulong output = 0;
            ulong.TryParse(input, out output);
            if (output.IsSteamId())
            {
                var exists = ServerUsers.Get(ulong.Parse(input));
                if (exists != null && ServerUsers.Get(output).group == ServerUsers.UserGroup.Banned)
                {
                    SendMessage(player, GetLang("AlreadyBanned", player.Id, output));
                }
                else
                {
                    ServerUsers.Set(output, ServerUsers.UserGroup.Banned, "Unnamed", reason);
                    ServerUsers.Save();
                    if (Announce) server.Broadcast(GetLang("BanMessage", null, "Unnamed", reason));
                    SendBanMessage("Unnamed", output.ToString(), reason, player.Name, player.Id);
                }
            }
        }

        private void SendBanMessage(string name, string bannedId, string reason, string sourceName, string sourceId, Action<bool> callback = null)
        {
            Puts(name + bannedId + reason + sourceName + sourceId);
            List<Fields> fields = new List<Fields>();
            fields.Add(new Fields(GetLang("Embed_BanTarget"), $"[{name}](https://steamcommunity.com/profiles/{bannedId})", true));
            fields.Add(new Fields(GetLang("Embed_BanPlayer"), sourceId != null && !sourceId.Equals("server_console") ? $"[{sourceName}](https://steamcommunity.com/profiles/{sourceId})" : sourceName, true));
            fields.Add(new Fields(GetLang("Embed_BanReason"), reason, false));
            FancyMessage message = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(GetLang("Embed_BanTitle"), ReportColor, fields) });
            SendPOST(BanURL, message.toJSON(message));
        }

        #endregion

        #region Heleprs

        private IPlayer GetPlayer(string nameOrID, IPlayer player)
        {
            if (IsParseableTo<ulong>(nameOrID) && nameOrID.StartsWith("7656119") && nameOrID.Length == 17)
            {
                var result = players.All.ToList().Find(p => p.Id == nameOrID);

                if (result == null)
                {
                    return null;
                }

                return result;
            }

            var foundPlayers = new List<IPlayer>();

            foreach (var current in players.Connected)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }
            if (foundPlayers.Count == 0)
                foreach (var all in players.All)
                {
                    if (all.Name.ToLower() == nameOrID.ToLower())
                        return all;

                    if (all.Name.ToLower().Contains(nameOrID.ToLower()))
                        foundPlayers.Add(all);
                }
            switch (foundPlayers.Count)
            {
                case 0:
                    if (!nameOrID.IsSteamId())
                        SendMessage(player, GetLang("NotFound", player.Id, nameOrID));
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    var names = (from current in foundPlayers select current.Name).ToArray();
                    SendMessage(player, GetLang("Multiple", player.Id, string.Join(", ", names)));
                    break;
            }

            return null;
        }

        private bool IsParseableTo<T>(object s)
        {
            try
            {
                var parsed = (T) Convert.ChangeType(s, typeof(T));
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}