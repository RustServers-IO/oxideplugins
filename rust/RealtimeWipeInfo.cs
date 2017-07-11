using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("RealtimeWipeInfo", "Ryan", "1.0.8", ResourceId = 2473)]
    [Description("Updates title dynamically depending on wipe time as well as preventing wipe chat spam.")]
    internal class RealtimeWipeInfo : RustPlugin
    {
        #region Other

        [PluginReference] private Plugin BetterChat;

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private Dictionary<string, Timer> Timers = new Dictionary<string, Timer>();

        // Credit to Visagalis for finding SaveRestore.SaveCreatedTime
        private double SecsSinceWipe() => (DateTime.UtcNow.ToLocalTime() - SaveRestore.SaveCreatedTime.ToLocalTime()).TotalSeconds;

        #endregion Other

        #region Config

        private ConfigFile _Config;

        // Credit to Mughisi for the Config example he posted in the uMod slack
        public class ConfigFile
        {
            [JsonProperty(PropertyName = "Title configuration")]
            public TitleConfig TitleConfig { get; set; }

            [JsonProperty(PropertyName = "Phrase configuration")]
            public PhraseConfig PhraseConfig { get; set; }

            [JsonProperty(PropertyName = "Wipe msg configuration")]
            public WipeInfoConfig WipeInfoConfig { get; set; }

            [JsonProperty(PropertyName = "Description settings")]
            public Desc Description { get; set; }

            [JsonProperty(PropertyName = "General plugin settings")]
            public GeneralSettings GeneralSettings { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    TitleConfig = new TitleConfig
                    {
                        EnableTitle = false,
                        Title = "My Untitled Rust Server - Wiped {0}",
                        Interval = 5,
                        Time = true,
                        Date = false,
                        DateFormat = "d/M"
                    },
                    PhraseConfig = new PhraseConfig
                    {
                        Phrases = new Dictionary<string, bool>()
                        {
                            { "wipe", false },
                            { "when wipe?", true },
                            { "wiped?", true },
                        },
                        DateFormat = "d/M",
                        EnablePhrases = false,
                        Time = true,
                        Date = false,
                    },
                    WipeInfoConfig = new WipeInfoConfig
                    {
                        UseWipeSchedule = true,
                        WipeSchedule = 7,
                        WipeFormat = "dddd d/M",
                    },
                    Description = new Desc
                    {
                        DescriptionFormat = "dddd d/M",
                        EnableDescription = false,
                        Interval = 5,
                        SeedSize = true,
                        Description = "This is your description \nYou should paste your description from your server.cfg here \nPut {0} where you want the plugins description addition to be"
                    },
                    GeneralSettings = new GeneralSettings
                    {
                        UseConnectMsgs = false
                    }
                };
            }
        }

        public class TitleConfig
        {
            [JsonProperty(PropertyName = "Full Title/Hostname")]
            public string Title { get; set; }

            [JsonProperty(PropertyName = "Date format")]
            public string DateFormat { get; set; }

            [JsonProperty(PropertyName = "Title Refresh Interval (minutes)")]
            public float Interval { get; set; }

            [JsonProperty(PropertyName = "Enable Title Refresh")]
            public bool EnableTitle { get; set; }

            [JsonProperty(PropertyName = "Enable the use of time in the title")]
            public bool Time { get; set; }

            [JsonProperty(PropertyName = "Enable the use of date in the title")]
            public bool Date { get; set; }
        }

        public class PhraseConfig
        {
            [JsonProperty(PropertyName = "Enable Phrases")]
            public bool EnablePhrases { get; set; }

            [JsonProperty(PropertyName = "Phrases")]
            public Dictionary<string, bool> Phrases { get; set; }

            [JsonProperty(PropertyName = "Date format")]
            public string DateFormat { get; set; }

            [JsonProperty(PropertyName = "Enable the use of time in the phrase reply")]
            public bool Time { get; set; }

            [JsonProperty(PropertyName = "Enable the use of date in the phrase reply")]
            public bool Date { get; set; }
        }

        public class WipeInfoConfig
        {
            [JsonProperty(PropertyName = "Use wipe schedule in phrase reply")]
            public bool UseWipeSchedule { get; set; }

            [JsonProperty(PropertyName = "Wipe schedule (days)")]
            public int WipeSchedule { get; set; }

            [JsonProperty(PropertyName = "Next wipe format")]
            public string WipeFormat { get; set; }
        }

        public class Desc
        {
            [JsonProperty(PropertyName = "Description Refresh Interval (minutes)")]
            public float Interval { get; set; }

            [JsonProperty(PropertyName = "Enable Description Refresh")]
            public bool EnableDescription { get; set; }

            [JsonProperty(PropertyName = "Use seed and mapsize")]
            public bool SeedSize { get; set; }

            [JsonProperty(PropertyName = "Description date format")]
            public string DescriptionFormat { get; set; }

            [JsonProperty(PropertyName = "Full server description")]
            public string Description { get; set; }
        }

        public class GeneralSettings
        {         
            [JsonProperty(PropertyName = "Send a last wipe message to a player when they connect")]
            public bool UseConnectMsgs { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Please setup your config, all features are disabled until you do!");
            _Config = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _Config = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(_Config);

        #endregion Config

        #region Data

        public class StoredData
        {
            public string Hostname;
            public string Description;

            public StoredData()
            {
                Hostname = ConVar.Server.hostname;
                Description = ConVar.Server.description;
            }
        }

        private StoredData storedData;

        #endregion Data

        #region Lang

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // Message
                ["Msg_DayFormat"] = "<color=orange>{0}</color> day and <color=orange>{1}</color> hours",
                ["Msg_DaysFormat"] = "<color=orange>{0}</color> days and <color=orange>{1}</color> hours",
                ["Msg_HourFormat"] = "<color=orange>{0}</color> hour and <color=orange>{1}</color> minutes",
                ["Msg_HoursFormat"] = "<color=orange>{0}</color> hours and <color=orange>{1}</color> minutes",
                ["Msg_MinFormat"] = "<color=orange>{0}</color> minute and <color=orange>{1}</color> seconds",
                ["Msg_MinsFormat"] = "<color=orange>{0}</color> minutes and <color=orange>{1}</color> seconds",
                ["Msg_SecsFormat"] = "<color=orange>{0}</color> seconds",
                // Title
                ["Title_DayFormat"] = "{0} day ago",
                ["Title_DaysFormat"] = "{0} days ago",
                ["Title_HourFormat"] = "{0} hour ago",
                ["Title_HoursFormat"] = "{0} hrs ago",
                ["Title_RecentFormat"] = "JUST NOW!",
                // Description
                ["Description_Lastwipe"] = "The last wipe was on {0}",
                ["Description_Nextwipe"] = "The next wipe will be on {0} ({1} day wipe schedule)",
                ["Description_SeedSize"] = "The map size is: {0} and the seed is {1}",
                // Phrase reply
                ["Phrase_TimeReply"] = "The last wipe was {0} ago",
                ["Phrase_DateReply"] = "The last wipe was on {0}",
                ["Phrase_DateTimeReply"] = "The last wipe was on {0} ({1} ago)",
                ["Phrase_NextWipe"] = "The next wipe will be on <color=orange>{0}</color> (<color=orange>{1}</color> day wipe schedule)"
            }, this);
        }

        #endregion Lang

        #region Plugin Hooks

        private void Init() => SaveConfig();

        private void OnServerInitialized()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
            {
                PrintWarning("Generating data file containing your original hostname and description...");
                Interface.Oxide.DataFileSystem.WriteObject(Name, new StoredData());
            }

            // 15 is the max amount of characters GetFormattedTitle() can output, taken 3 away for the {0}. Rust's max hostname chars is 64
            if (_Config.TitleConfig.Title.Length > 52)
                PrintWarning($"Your title exceeds the suggested amount of chacters! ({_Config.TitleConfig.Title.Length + 12} should be 64!)");

            // Thanks to Nivex for his contribution here
            if (!_Config.PhraseConfig.EnablePhrases)
            {
                Unsubscribe(nameof(OnBetterChat));
                Unsubscribe(nameof(OnPlayerChat));
            }
            else if(BetterChat)
            {
                bool isSupported = new Version($"{BetterChat.Version.Major}.{BetterChat.Version.Minor}.{BetterChat.Version.Patch}") < new Version("5.0.6") ? false : true;
                if (!isSupported)
                {
                    PrintWarning("This plugin is only compatable with BetterChat version 5.0.6 or greater!");
                    Unsubscribe(nameof(OnBetterChat));
                }
            }

            if (_Config.TitleConfig.EnableTitle)
            {
                if (_Config.TitleConfig.Interval < 1)
                {
                    PrintWarning("This plugin doesn't support a timer of less than 1 minute.");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
                ApplyTitle();
                var titleTimer = timer.Repeat(_Config.TitleConfig.Interval * 60, 0, () =>
                {
                    ApplyTitle();
                });
                if (!Timers.ContainsKey("TitleTimer"))
                    Timers.Add("TitleTimer", titleTimer);
            }
            if(_Config.Description.EnableDescription)
            {
                if (_Config.Description.Interval < 1)
                {
                    PrintWarning("This plugin doesn't support a timer of less than 1 minute.");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
                ConVar.Server.description = FormatDescription();
                var desctimer = timer.Repeat(_Config.Description.Interval * 60, 0, () =>
                {
                    ConVar.Server.description = FormatDescription();
                });
                if (!Timers.ContainsKey("DescriptionTimer"))
                    Timers.Add("DescriptionTimer", desctimer);
            }
        }

        private void Unload()
        {
            if (Timers.ContainsKey("TitleTimer"))
                Timers["TitleTimer"].Destroy();
            if (Timers.ContainsKey("DescriptionTimer"))
                Timers["DescriptionTimer"].Destroy();

            if (!ConVar.Admin.ServerInfo().Restarting)
            {
                PrintWarning("Resetting servers hostname/description to originals.");
                ConVar.Server.hostname = storedData.Hostname;
                ConVar.Server.description = storedData.Description;
            }
        }

        #endregion Plugin Hooks

        #region Formatting

        private string GetFormattedMsgTime()
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(SecsSinceWipe());
            if (timeSpan == null) return null;

            if (Math.Floor(timeSpan.TotalDays) >= 1)
                return string.Format(timeSpan.Days > 1 ? Lang("Msg_DaysFormat", null, timeSpan.Days, timeSpan.Hours) : Lang("Msg_DayFormat", null, timeSpan.Days, timeSpan.Hours));
            if (Math.Floor(timeSpan.TotalMinutes) > 60)
                return string.Format(timeSpan.Hours > 1 ? Lang("Msg_HoursFormat", null, timeSpan.Hours, timeSpan.Minutes) : Lang("Msg_HourFormat", null, timeSpan.Hours, timeSpan.Minutes));
            if (Math.Floor(timeSpan.TotalSeconds) > 60)
                return string.Format(timeSpan.Minutes > 1 ? Lang("Msg_MinsFormat", null, timeSpan.Minutes, timeSpan.Seconds) : Lang("Msg_MinFormat", null, timeSpan.Minutes, timeSpan.Seconds));
            return Lang("Msg_SecsFormat", null, timeSpan.Seconds);
        }

        private string FormatDescription()
        {
            string output;
            if (_Config.WipeInfoConfig.UseWipeSchedule)
            {
                output = string.Format(Lang("Description_Lastwipe", null, SaveRestore.SaveCreatedTime.ToLocalTime().ToString(_Config.Description.DescriptionFormat)) + "\n" +
                    Lang("Description_Nextwipe", null, SaveRestore.SaveCreatedTime.ToLocalTime().AddDays(_Config.WipeInfoConfig.WipeSchedule).ToString(_Config.Description.DescriptionFormat), _Config.WipeInfoConfig.WipeSchedule));
            }
            else
                output = Lang("Description_Lastwipe", null, $"{SaveRestore.SaveCreatedTime.ToLocalTime().AddDays(_Config.WipeInfoConfig.WipeSchedule).ToString(_Config.Description.DescriptionFormat)}");
            if (_Config.Description.SeedSize)
                output = string.Format("{0}\n{1}", output, Lang("Description_SeedSize", null, ConVar.Server.worldsize, ConVar.Server.seed));
            return string.Format(_Config.Description.Description, output);
        }

        private string FormatMsg(BasePlayer player)
        {
            if (_Config.PhraseConfig.Time && !_Config.PhraseConfig.Date)
            {
                if (_Config.WipeInfoConfig.UseWipeSchedule)
                    return string.Format(Lang("Phrase_TimeReply", player.UserIDString, GetFormattedMsgTime()) + "\n" + Lang("Phrase_NextWipe", player.UserIDString, SaveRestore.SaveCreatedTime.ToLocalTime().AddDays(_Config.WipeInfoConfig.WipeSchedule).ToString(_Config.WipeInfoConfig.WipeFormat), _Config.WipeInfoConfig.WipeSchedule));
                return Lang("Phrase_TimeReply", player.UserIDString, GetFormattedMsgTime());
            }
            if (_Config.PhraseConfig.Date && !_Config.PhraseConfig.Time)
            {
                if (_Config.WipeInfoConfig.UseWipeSchedule)
                    return string.Format(Lang("Phrase_DateReply", player.UserIDString, SaveRestore.SaveCreatedTime.ToLocalTime().ToString(_Config.PhraseConfig.DateFormat)) + "\n" + Lang("Phrase_NextWipe", player.UserIDString, SaveRestore.SaveCreatedTime.ToLocalTime().AddDays(_Config.WipeInfoConfig.WipeSchedule).ToString(_Config.WipeInfoConfig.WipeFormat), _Config.WipeInfoConfig.WipeSchedule));
                return Lang("Phrase_DateReply", player.UserIDString, SaveRestore.SaveCreatedTime.ToLocalTime().ToString(_Config.PhraseConfig.DateFormat));
            }
            if (_Config.PhraseConfig.Date && _Config.PhraseConfig.Time)
            {
                if (_Config.WipeInfoConfig.UseWipeSchedule)
                    return string.Format(Lang("Phrase_DateTimeReply", player.UserIDString, SaveRestore.SaveCreatedTime.ToLocalTime().ToString(_Config.PhraseConfig.DateFormat), GetFormattedMsgTime()) + "\n" + Lang("Phrase_NextWipe", player.UserIDString, SaveRestore.SaveCreatedTime.AddDays(_Config.WipeInfoConfig.WipeSchedule).ToString(_Config.WipeInfoConfig.WipeFormat), _Config.WipeInfoConfig.WipeSchedule));
                return Lang("Phrase_DateTimeReply", player.UserIDString, SaveRestore.SaveCreatedTime.ToLocalTime().ToString(_Config.PhraseConfig.DateFormat), GetFormattedMsgTime()) + $"{SaveRestore.SaveCreatedTime.ToLocalTime().AddDays(2).ToString("ddd d/M")}";
            }
            return null;
        }

        #endregion Formatting

        #region Title

        private void ApplyTitle() => ConVar.Server.hostname = string.Format(_Config.TitleConfig.Title, GetFormattedTitle()).Truncate(64);

        private string GetFormattedTitle()
        {
            if (_Config.TitleConfig.Time && !_Config.TitleConfig.Date)
            {
                var timeSince = TimeSpan.FromSeconds(SecsSinceWipe());

                if (Math.Floor(timeSince.TotalDays) >= 1)
                    return string.Format(timeSince.Days > 1 ? Lang("Title_DaysFormat", null, timeSince.Days) : Lang("Title_DayFormat", null, timeSince.Days));

                if (Math.Floor(timeSince.TotalMinutes) > 60)
                    return string.Format(timeSince.Hours > 1 ? Lang("Title_HoursFormat", null, timeSince.Hours) : Lang("Title_HourFormat", null, timeSince.Hours));

                return Lang("Title_RecentFormat");
            }
            if (_Config.TitleConfig.Date && !_Config.TitleConfig.Time)
            {
                return SaveRestore.SaveCreatedTime.ToString(_Config.TitleConfig.DateFormat);
            }
            if (_Config.TitleConfig.Date && _Config.TitleConfig.Time)
            {
                var timeSince = TimeSpan.FromSeconds(SecsSinceWipe());

                if (Math.Floor(timeSince.TotalDays) >= 1)
                    return string.Format(timeSince.Days > 1 ? string.Format(SaveRestore.SaveCreatedTime.ToLocalTime().ToString(_Config.TitleConfig.DateFormat) + " " + Lang("Title_DaysFormat", null, timeSince.Days)) : string.Format(SaveRestore.SaveCreatedTime.ToString(_Config.TitleConfig.DateFormat) + " " + Lang("Title_DayFormat", null, timeSince.Days)));

                if (Math.Floor(timeSince.TotalMinutes) > 60)
                    return string.Format(timeSince.Hours > 1 ? string.Format(SaveRestore.SaveCreatedTime.ToLocalTime().ToString(_Config.TitleConfig.DateFormat) + " " + Lang("Title_HoursFormat", null, timeSince.Hours)) : string.Format(SaveRestore.SaveCreatedTime.ToString(_Config.TitleConfig.DateFormat) + " " + Lang("Title_HourFormat", null, timeSince.Hours)));

                return string.Format(SaveRestore.SaveCreatedTime.ToLocalTime().ToString(_Config.TitleConfig.DateFormat) + " " + Lang("Title_RecentFormat"));
            }
            return null;
        }

        #endregion Title

        #region Phrases

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player.IsAdmin) return null;
            foreach (var phrase in _Config.PhraseConfig.Phrases)
            {
                if (arg.FullString.ToLower().Contains(phrase.Key.ToLower()))
                {
                    PrintToChat(player, FormatMsg(player));
                    if (phrase.Value)
                        return false;
                }
            }
            return null;
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            var player = data["Player"] as IPlayer;
            var bPlayer = player.Object as BasePlayer;
            if (bPlayer.IsAdmin) return null;
            foreach (var phrase in _Config.PhraseConfig.Phrases)
            {
                if (data["Text"].ToString().ToLower().Contains(phrase.Key.ToLower()))
                    if(phrase.Value)
                        return true;
            }
            return null;
        }

        #endregion Phrases

        #region ConnectMsgs

        void OnPlayerInit(BasePlayer player)
        {
            if (!_Config.GeneralSettings.UseConnectMsgs) return;
            timer.Once(5f, () =>
            {
                PrintToChat(player, FormatMsg(player));
            });
        }

        #endregion ConnectMsgs
    }
}