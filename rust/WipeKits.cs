using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("WipeKits", "Ryan", "1.0.3")]
    [Description("Puts a configurable cooldown on each kit depending on their kitname.")]
    internal class WipeKits : RustPlugin
    {
        // Credit to Visagalis for finding SaveRestore.SaveCreatedTime
        private double SecsSinceWipe() => (DateTime.UtcNow.ToLocalTime() - SaveRestore.SaveCreatedTime.ToLocalTime()).TotalSeconds;

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #region Config

        private ConfigFile _Config;

        public class ConfigFile
        {
            [JsonProperty(PropertyName = "Kit Names & Cooldowns - Cooldowns (minutes)")]
            public Dictionary<string, float> Kits;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    Kits = new Dictionary<string, float>()
                    {
                        ["kitname1"] = 5,
                        ["kitname2"] = 5
                    }
                };
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            _Config = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _Config = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(_Config);

        #endregion Config

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
                // Can't use command
                ["Cmd_CantUse"] = "The server's just wiped! Try again in {0}",
            }, this);
        }

        #endregion Lang

        private string GetFormattedMsg(float cooldown)
        {
            TimeSpan timeSpan = GetNextKitTime(cooldown);

            if (timeSpan == null) return null;

            if (Math.Floor(timeSpan.TotalDays) >= 1)
                return string.Format(timeSpan.Days > 1 ? Lang("Msg_DaysFormat", null, timeSpan.Days, timeSpan.Hours) : Lang("Msg_DayFormat", null, timeSpan.Days, timeSpan.Hours));

            if (Math.Floor(timeSpan.TotalMinutes) > 60)
                return string.Format(timeSpan.Hours > 1 ? Lang("Msg_HoursFormat", null, timeSpan.Hours, timeSpan.Minutes) : Lang("Msg_HourFormat", null, timeSpan.Hours, timeSpan.Minutes));

            if (Math.Floor(timeSpan.TotalSeconds) > 60)
                return string.Format(timeSpan.Minutes > 1 ? Lang("Msg_MinsFormat", null, timeSpan.Minutes, timeSpan.Seconds) : Lang("Msg_MinFormat", null, timeSpan.Minutes, timeSpan.Seconds));

            return Lang("Msg_SecsFormat", null, timeSpan.Seconds);
        }

        private TimeSpan GetNextKitTime(float cooldown)
        {
            var timeSince = TimeSpan.FromSeconds(SecsSinceWipe());

            if (timeSince.TotalSeconds > cooldown * 60)
                return TimeSpan.Zero;

            double timeUntil = (cooldown * 60) - Math.Floor(timeSince.TotalSeconds);
            return TimeSpan.FromSeconds(timeUntil);
        }

        private object OnPlayerCommand(ConsoleSystem.Arg args)
        {
            if (args.cmd.FullName.ToLower() == "chat.say")
            {
                var player = args.Connection.player as BasePlayer;
                if (player.IsAdmin) return null;
                if (args.GetString(0).ToLower().StartsWith("/kit"))
                {
                    foreach (var kitname in _Config.Kits)
                    {
                        if (args.GetString(0).ToLower() == "/kit " + kitname.Key.ToLower())
                        {
                            if (GetNextKitTime(kitname.Value).TotalSeconds <= 0)
                                return null;
                            PrintToChat(player, Lang("Cmd_CantUse", player.UserIDString, GetFormattedMsg(kitname.Value)));
                            return true;
                        }
                    }
                }
            }
            return null;
        }
    }
}