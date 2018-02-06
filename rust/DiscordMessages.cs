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
    [Info("DiscordMessages", "Slut", "1.8.2", ResourceId = 2486)]
    class DiscordMessages : CovalencePlugin
    {

        [PluginReference] private Plugin BetterChatMute;
        #region Classes

        public class StoredData
        {
            public Dictionary<string, PlayerData> Players = new Dictionary<string, PlayerData>();
        }
        public class PlayerData
        {
            public int reports { get; set; }
            public DateTime reportCooldown { get; set; }
            public DateTime messageCooldown { get; set; }
            public bool ReportDisabled { get; set; }
            public PlayerData()
            {
                ReportDisabled = false;
                reports = 0;
                reportCooldown = DateTime.MinValue;
                messageCooldown = DateTime.MinValue;
            }
        }

        public class SavedMessages
        {
            public string url { get; set; }
            public string payload { get; set; }
            public float time { get; set; }
            public Action<int> callback { get; set; }

            public SavedMessages(string url, string payload, float time, Action<int> callback)
            {
                this.url = url;
                this.payload = payload;
                this.time = time;
                this.callback = callback;
            }
        }
        public class FancyMessage
        {
            public string content { get; set; }
            public bool tts { get; set; }
            public Embeds[] embeds { get; set; }

            public class Embeds
            {
                public string title { get; set; }
                public int color { get; set; }
                public List<Fields> fields { get; set; }
                public Embeds(string title, int color, List<Fields> fields)
                {
                    this.title = title;
                    this.color = color;
                    this.fields = fields;
                }
            }

            public FancyMessage(string content, bool tts, Embeds[] embeds)
            {
                this.content = content;
                this.tts = tts;
                this.embeds = embeds;
            }

            public string toJSON() => JsonConvert.SerializeObject(this);
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

        public enum CooldownType { ReportCooldown, MessageCooldown }
        private StoredData storedData;
        List<SavedMessages> savedmessages = new List<SavedMessages>();

        #endregion

        #region Config Variables

        private string BanURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private string ReportURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private string MuteURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private string MessageURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private string ChatURL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private bool ReportEnabled = true;
        private bool ReportLogToConsole = false;
        private bool ChatEnabled = false;
        private bool ChatTTS = false;
        private bool BanEnabled = true;
        private bool MessageEnabled = true;
        private bool MessageLogToConsole = false;
        private bool MessageSuggestAlias = false;
        private string MessageAlert = "";
        private string ReportAlert = "";
        private bool MuteEnabled = true;
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

            LoadData();
            LoadConfiguration();
            RegisterPermissions();
            if (!BanEnabled && !ReportEnabled && !MessageEnabled && !MuteEnabled && !ChatEnabled)
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
            if (MuteURL == "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks" && MuteEnabled || BetterChatMute?.IsLoaded == true)
            {
                if (BetterChatMute == null)
                {
                    PrintWarning("Mutes enabled but not setup correctly!");
                    MuteEnabled = false;
                }
            }
            if (ChatURL == "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks" && ChatEnabled)
            {
                PrintWarning("Chat enabled but webhook not setup!");
                ChatEnabled = false;
            }
            RegisterCommands();
            CheckHooks();
        }
        private void CheckHooks()
        {
            if (!ChatEnabled)
            {
                Unsubscribe(nameof(OnUserChat));
            }
            if (!MuteEnabled)
            {
                Unsubscribe(nameof(OnBetterChatMuted));
                Unsubscribe(nameof(OnBetterChatTimeMuted));
            }
        }
        private void Unload() => SaveData();
        private void OnServerSave() => SaveData();

        private void LoadConfiguration()
        {
            CheckCfg<string>("Bans - Webhook URL", ref BanURL);
            CheckCfg<bool>("Bans - Enabled", ref BanEnabled);
            CheckCfg<bool>("Bans - Broadcast to server", ref Announce);
            CheckCfg<bool>("Mutes - Enabled", ref MuteEnabled);
            CheckCfg<string>("Mutes - Webhook URL", ref MuteURL);
            CheckCfg<string>("Reports - Webhook URL", ref ReportURL);
            CheckCfg<bool>("Reports - Enabled", ref ReportEnabled);
            CheckCfg<string>("Reports - Alert Role", ref ReportAlert);
            CheckCfg<int>("Reports - Cooldown", ref ReportCooldown);
            CheckCfg<bool>("Player Chat - Enabled", ref ChatEnabled);
            CheckCfg<string>("Player Chat - Webhook URL", ref ChatURL);
            CheckCfg<bool>("Player Chat - TTS Enabled (Text To Speech)", ref ChatTTS);
            CheckCfg<bool>("Message - Enabled", ref MessageEnabled);
            CheckCfg<string>("Message - Webhook URL", ref MessageURL);
            CheckCfg<int>("Message - Cooldown", ref MessageCooldown);
            CheckCfg<string>("Message - Alert Role", ref MessageAlert);
            CheckCfg<int>("Message - Embed Color (DECIMAL)", ref MessageColor);
            CheckCfg<int>("Reports - Embed Color (DECIMAL)", ref ReportColor);
            CheckCfg<int>("Ban - Embed Color (DECIMAL)", ref BanColor);
            CheckCfg<int>("Mute - Embed Color (DECIMAL)", ref MuteColor);
            CheckCfg<bool>("Message - Log Message command to Console", ref MessageLogToConsole);
            CheckCfg<bool>("Reports - Log Report command to Console", ref ReportLogToConsole);
            CheckCfg<bool>("Message - Enable /suggest alias", ref MessageSuggestAlias);

            SaveConfig();
        }

        private void RegisterCommands()
        {
            if (ReportEnabled)
            {
                AddCovalenceCommand("report", "ReportCommand", "discordmessages.report");
                AddCovalenceCommand(new string[] { "reportadmin", "ra" }, "ReportAdminCommand", "discordmessages.admin");
            }
            if (BanEnabled) AddCovalenceCommand("ban", "BanCommand", "discordmessages.ban");
            if (MessageEnabled)
                if (MessageSuggestAlias)
                {
                    AddCovalenceCommand(new string[] { "message", "suggest" }, "MessageCommand", "discordmessages.message");
                }
                else
                {
                    AddCovalenceCommand("message", "MessageCommand", "discordmessages.message");
                }
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission("discordmessages.ban", this);
            permission.RegisterPermission("discordmessages.report", this);
            permission.RegisterPermission("discordmessages.message", this);
            permission.RegisterPermission("discordmessages.admin", this);
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
                ["NoReports"] = "{0} has not been reported yet!",
                ["ReportDisallowed"] = "You have been blacklisted from reporting players.",
                ["ReportAccessChanged"] = "Report feature for {0} is now {1}",
                ["ReportReset"] = "You have reset the report count for {0}",
                ["Cooldown"] = "You must wait {0} seconds to use this command again.",
                ["AlreadyBanned"] = "{0} is already banned!",
                ["NoPermission"] = "You do not have permision for this command!",
                ["Disabled"] = "This feature is currently disabled.",
                ["Failed"] = "Your report failed to send, contact the server owner.",
                ["ToSelf"] = "You cannot perform this action on yourself.",
                ["ReportTooShort"] = "Your report was too short! Please be more descriptive.",
                ["PlayerChatFormat"] = "**{0}:** {1}",
                ["BanPrefix"] = "Banned: {0}",
                ["Embed_ReportPlayer"] = "Reporter",
                ["Embed_ReportTarget"] = "Reported",
                ["Embed_ReportCount"] = "Times Reported",
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
                ["Embed_MuteTime"] = "Time",
                ["Embed_MuteReason"] = "Reason"
            }, this, "en");
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Generating new config.");
        }

        private string GetLang(string key, string id = null, params object[] args)
        {
            if (args.Length > 0)
                return string.Format(lang.GetMessage(key, this, id), args);
            else return lang.GetMessage(key, this, id);
        }

        private void SendMessage(IPlayer player, string message)
        {
            player.Reply(message);
        }

        #endregion

        #region API
        private void API_SendFancyMessage(string webhookURL, string embedName, string json, string content = null, int embedColor = 3329330)
        {
            FancyMessage message = new FancyMessage(content, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(embedName, embedColor, JsonConvert.DeserializeObject<List<Fields>>(json)) });
            var payload = message.toJSON();
            Request(webhookURL, payload, (Callback) =>
            {
                if (!(Callback == 200 || Callback == 204 || Callback == 429))
                {
                    PrintError($"FAILED TO SEND REQUEST CODE {Callback}");
                }
            });
        }
        private void API_SendTextMessage(string webhookURL, string content, bool tts = false)
        {
            FancyMessage message = new FancyMessage(content, tts, null);
            var payload = message.toJSON();
            Request(webhookURL, payload, (Callback) =>
            {
                if (!(Callback == 200 || Callback == 204 || Callback == 429))
                {
                    PrintError($"FAILED TO SEND REQUEST CODE {Callback}");
                }
            });
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
                Request(savedmessages[0].url, savedmessages[0].payload, savedmessages[0].callback);
                _timer = timer.Once(savedmessages[0].time, () => RateTimer());
                return;
            }
        }

        private void Request(string url, string payload, Action<int> callback = null)
        {
            bool exists = savedmessages.Exists(x => x.payload == payload);
            webrequest.Enqueue(url, payload, (code, response) =>
                {
                    if (code != 200 && code != 204)
                    {
                        if (response != null)
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds = float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                                savedmessages.Add(new SavedMessages(url, payload, seconds, callback));
                                if (_timer == null || _timer.Destroyed)
                                {
                                    RateTimer();
                                }
                            }
                            else
                            {
                                PrintWarning($"Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        else
                        {
                            PrintWarning($"Discord didn't respond (down?) Code: {code}");
                        }
                    }
                    else if (exists == true)
                    {
                        savedmessages.RemoveAt(0);
                    }
                    try
                    {
                        callback?.Invoke(code);
                    }
                    catch (Exception ex)
                    {
                        Interface.Oxide.LogException("[DiscordMessages] Request callback raised an exception!", ex);
                    }
                }, this, Core.Libraries.RequestMethod.POST);
        }

        #endregion

        #region PlayerChat

        private object OnUserChat(IPlayer player, string message)
        {
            if (!ChatEnabled)
            {
                return null;
            }
            HandleMessage(player.Name, message);
            return null;
        }
        private void HandleMessage(string name, string message)
        {
            string discordMessage = GetLang("PlayerChatFormat", null, name, message);
            FancyMessage dmessage = new FancyMessage(discordMessage, ChatTTS, null);
            var payload = dmessage.toJSON();
            Request(ChatURL, payload);
        }
        #endregion

        #region Message

        private void MessageCommand(IPlayer player, string command, string[] args)
        {
            if (!MessageEnabled)
                return;
            if (args.Length < 1)
            {
                SendMessage(player, GetLang("MessageSyntax", player.Id));
                return;
            }
            if (OnCooldown(player, CooldownType.MessageCooldown))
            {
                var time = (storedData.Players[player.Id].messageCooldown.AddSeconds(MessageCooldown) - DateTime.UtcNow).Seconds;
                SendMessage(player, GetLang("Cooldown", player.Id, time));

                return;
            }
            var text = string.Join(" ", args.ToArray());
            List<Fields> fields = new List<Fields>();
            fields.Add(new Fields(GetLang("Embed_MessagePlayer"), $"[{ player.Name }](https://steamcommunity.com/profiles/{player.Id})", true));
            fields.Add(new Fields(GetLang("Embed_MessageMessage"), text, false));
            FancyMessage message = new FancyMessage(MessageAlert, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(GetLang("Embed_MessageTitle"), MessageColor, fields) });
            var payload = message.toJSON();
            Request(MessageURL, payload, (Callback) =>
            {
                if (Callback == 200 || Callback == 204)
                {
                    SendMessage(player, GetLang("MessageSent", player.Id));
                    if (storedData.Players.ContainsKey(player.Id))
                        storedData.Players[player.Id].messageCooldown = DateTime.UtcNow;
                    else
                    {
                        storedData.Players.Add(player.Id, new PlayerData());
                        storedData.Players[player.Id].messageCooldown = DateTime.UtcNow;
                    }
                    if (MessageLogToConsole)
                    {
                        Puts($"MESSAGE ({player.Name}/{player.Id}) : {text}");
                    }
                }
                else if (Callback != 429)
                {
                    SendMessage(player, GetLang("MessageNotSent", player.Id));
                }

            });
        }

        #endregion
        #region Report
        private void ReportAdminCommand(IPlayer player, string command, string[] args)
        {
            var target = GetPlayer(args[1], player, false);
            if (target == null)
            {
                player.Reply(GetLang("NotFound", player.Id, args[1]));
                return;
            }
            switch (args[0])
            {
                case "enable":
                    if (storedData.Players.ContainsKey(target.Id))
                    {
                        storedData.Players[target.Id].ReportDisabled = false;
                    }
                    player.Reply(GetLang("ReportAccessChanged", player.Id, target.Name, "enabled"));
                    return;
                case "disable":
                    if (storedData.Players.ContainsKey(target.Id))
                    {
                        storedData.Players[target.Id].ReportDisabled = true;
                    }
                    else
                    {
                        storedData.Players.Add(target.Id, new PlayerData { ReportDisabled = true });
                    }
                    player.Reply(GetLang("ReportAccessChanged", player.Id, target.Name, "disabled"));
                    return;
                case "reset":
                    if (storedData.Players.ContainsKey(target.Id))
                    {
                        if (storedData.Players[target.Id].reports != 0)
                        {
                            storedData.Players[target.Id].reports = 0;
                            player.Reply(GetLang("ReportReset", player.Id, target.Name));
                            return;
                        }
                    }
                    player.Reply(GetLang("NoReports", player.Id, target.Name));
                    return;
            }

        }
        private void ReportCommand(IPlayer player, string command, string[] args)
        {
            if ((player.Name == "Server Console") | !player.IsConnected)
                return;
            if (ReportEnabled == false)
            {
                SendMessage(player, GetLang("Disabled", player.Id));
                return;
            }
            if (storedData.Players.ContainsKey(player.Id))
            {
                if (storedData.Players[player.Id].ReportDisabled)
                {
                    SendMessage(player, GetLang("ReportDisallowed", player.Id));
                    return;
                }
            }
            else
            {
                storedData.Players.Add(player.Id, new PlayerData());
            }
            if (OnCooldown(player, CooldownType.ReportCooldown))
            {
                var time = (storedData.Players[player.Id].reportCooldown.AddSeconds(ReportCooldown) - DateTime.UtcNow).Seconds;
                SendMessage(player, GetLang("Cooldown", player.Id, time));
                return;
            }
            if (args.Length < 2)
            {
                SendMessage(player, GetLang("ReportSyntax", player.Id));
                return;
            }
            List<string> reason = args.Skip(1).ToList();
            var target = GetPlayer(args[0], player, true);

            if (target != null)
            {
                if (player.Equals(target))
                {
                    SendMessage(player, GetLang("ToSelf", player.Id));
                    return;
                }
                string[] targetName = target.Name.Split(' ');
                if (targetName.Length > 1)
                {
                    for (int x = 0; x < targetName.Length - 1; x++)
                    {
                        if (reason[x].Equals(targetName[x + 1]))
                        {
                            reason.RemoveAt(x);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                if (reason.Count < 1)
                {
                    SendMessage(player, GetLang("ReportTooShort", player.Id));
                    return;
                }
                string finalReason = string.Join(" ", reason.ToArray());
                if (storedData.Players.ContainsKey(target.Id))
                {
                    storedData.Players[target.Id].reports++;
                }
                else
                {
                    storedData.Players.Add(target.Id, new PlayerData());
                    storedData.Players[target.Id].reports++;
                }
                var status = target.IsConnected ? lang.GetMessage("Online", null) : lang.GetMessage("Offline", null);
                List<Fields> fields = new List<Fields>();
                fields.Add(new Fields(GetLang("Embed_ReportTarget"), $"[{target.Name}](https://steamcommunity.com/profiles/{target.Id})", true));
                fields.Add(new Fields(GetLang("Embed_ReportPlayer"), $"[{player.Name}](https://steamcommunity.com/profiles/{player.Id})", true));
                fields.Add(new Fields(GetLang("Embed_ReportStatus"), status, true));
                fields.Add(new Fields(GetLang("Embed_ReportReason"), finalReason, false));
                fields.Add(new Fields(GetLang("Embed_ReportCount"), storedData.Players[target.Id].reports.ToString(), true));
                FancyMessage message = new FancyMessage(ReportAlert, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(GetLang("Embed_MessageTitle"), ReportColor, fields) });
                Request(ReportURL, message.toJSON(), (Callback) =>
                {
                    if (Callback == 200 || Callback == 204)
                    {
                        SendMessage(player, GetLang("ReportSent", player.Id));
                        if (storedData.Players.ContainsKey(player.Id))
                            storedData.Players[player.Id].reportCooldown = DateTime.UtcNow;
                        else
                        {
                            storedData.Players.Add(player.Id, new PlayerData());
                            storedData.Players[player.Id].reportCooldown = DateTime.UtcNow;
                        }
                        if (ReportLogToConsole)
                        {
                            Puts($"REPORT ({player.Name}/{player.Id}) -> ({target.Name}/{target.Id}): {finalReason}");
                        }
                    }
                    else if (Callback != 429)
                    {
                        SendMessage(player, GetLang("ReportNotSent", player.Id));
                    }
                });
            }
        }

        #endregion

        #region Mutes

        string FormatTime(TimeSpan time) => $"{(time.Days == 0 ? string.Empty : $"{time.Days} day(s)")}{(time.Days != 0 && time.Hours != 0 ? $", " : string.Empty)}{(time.Hours == 0 ? string.Empty : $"{time.Hours} hour(s)")}{(time.Hours != 0 && time.Minutes != 0 ? $", " : string.Empty)}{(time.Minutes == 0 ? string.Empty : $"{time.Minutes} minute(s)")}{(time.Minutes != 0 && time.Seconds != 0 ? $", " : string.Empty)}{(time.Seconds == 0 ? string.Empty : $"{time.Seconds} second(s)")}";

        private void OnBetterChatTimeMuted(IPlayer target, IPlayer player, TimeSpan expireDate, string reason) => SendMute(target, player, expireDate, true, reason);

        private void OnBetterChatMuted(IPlayer target, IPlayer player, string reason) => SendMute(target, player, TimeSpan.Zero, false, reason);

        private void SendMute(IPlayer target, IPlayer player, TimeSpan expireDate, bool timed, string reason)
        {
            if (!MuteEnabled)
                return;
            if (target == null || player == null)
                return;
            List<Fields> fields = new List<Fields>();
            fields.Add(new Fields(GetLang("Embed_MuteTarget"), $"[{target.Name}](https://steamcommunity.com/profiles/{target.Id})", true));
            fields.Add(new Fields(GetLang("Embed_MutePlayer"), !player.Id.Equals("server_console") ? $"[{player.Name}](https://steamcommunity.com/profiles/{player.Id})" : player.Name, true));
            fields.Add(new Fields(GetLang("Embed_MuteTime"), timed ? FormatTime(expireDate) : "Permanent", true));
            if (!string.IsNullOrEmpty(reason))
            {
                fields.Add(new Fields(GetLang("Embed_MuteReason"), reason, false));
            }
            FancyMessage message = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(GetLang("Embed_MuteTitle"), MuteColor, fields) });
            Request(MuteURL, message.toJSON());
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
            var target = GetPlayer(args[0], player, false);
            if (target != null)
            {
                if (target == player)
                {
                    SendMessage(player, GetLang("ToSelf", player.Id));
                    return;
                }
                ExecuteBan(target, player, reason);
            }
            else
            {
#if RUST
                ExectueBanNotExists(args[0], player, reason);
#else
                player.Reply(GetLang("NotFound", player.Id, args[0]));
#endif
            }
        }

        private void ExecuteBan(IPlayer target, IPlayer player, string reason)
        {
#if RUST
            var exists = ServerUsers.Get(ulong.Parse(target.Id));
            if (exists != null && exists.group == ServerUsers.UserGroup.Banned)
            {
                SendMessage(player, GetLang("AlreadyBanned", player.Id, target.Name));
                return;
            }
#endif
            target.Ban(GetLang("BanPrefix", target.Id) + reason);
            if (Announce) server.Broadcast(GetLang("BanMessage", null, target.Name, reason));
            SendBanMessage(target.Name, target.Id, reason, player.Name, player.Id);
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


        private void SendBanMessage(string name, string bannedId, string reason, string sourceName, string sourceId)
        {
            List<Fields> fields = new List<Fields>();
            fields.Add(new Fields(GetLang("Embed_BanTarget"), $"[{name}](https://steamcommunity.com/profiles/{bannedId})", true));
            fields.Add(new Fields(GetLang("Embed_BanPlayer"), sourceId != null && !sourceId.Equals("server_console") ? $"[{sourceName}](https://steamcommunity.com/profiles/{sourceId})" : sourceName, true));
            fields.Add(new Fields(GetLang("Embed_BanReason"), reason, false));
            FancyMessage message = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(GetLang("Embed_BanTitle"), ReportColor, fields) });
            Request(BanURL, message.toJSON());
        }

        #endregion

        #region Helpers

        private bool OnCooldown(IPlayer player, CooldownType type)
        {
            if (storedData.Players.ContainsKey(player.Id))
            {
                if (type == CooldownType.ReportCooldown)
                {
                    if (storedData.Players[player.Id].reportCooldown.AddSeconds(ReportCooldown) > DateTime.UtcNow)
                    {
                        return true;
                    }
                    else if (type == CooldownType.MessageCooldown)
                    {
                        if (storedData.Players[player.Id].messageCooldown.AddSeconds(MessageCooldown) > DateTime.UtcNow)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        private void LoadData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        private IPlayer GetPlayer(string nameOrID, IPlayer player, bool sendError)
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
                        if (sendError)
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
                var parsed = (T)Convert.ChangeType(s, typeof(T));
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