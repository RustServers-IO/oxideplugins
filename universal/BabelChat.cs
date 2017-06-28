//#define DEBUG
// Requires: Babel

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Babel Chat", "Wulf/lukespragg", "1.0.2", ResourceId = 1964)]
    [Description("Translates chat messages to each player's language preference or server default")]
    public class BabelChat : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Force default server language (true/false)")]
            public bool ForceDefault;

            [JsonProperty(PropertyName = "Show original message (true/false)")]
            public bool ShowOriginal;

            [JsonProperty(PropertyName = "Use random name colors (true/false)")]
            public bool UseRandomColors;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    ForceDefault = false,
                    ShowOriginal = false,
                    UseRandomColors = false
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.ForceDefault == null) LoadDefaultConfig();
            }
            catch
            {
                LogWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Chat Translation

        [PluginReference]
        private Plugin Babel, BetterChat, UFilter;

        private static System.Random random = new System.Random();

        private void Translate(string message, string targetId, string senderId, Action<string> callback)
        {
            var to = config.ForceDefault ? lang.GetServerLanguage() : lang.GetLanguage(targetId);
            var from = lang.GetLanguage(senderId) ?? "auto";
#if DEBUG
            LogWarning($"To: {to}, From: {from}");
#endif
            Babel.Call("Translate", message, to, from, callback);
        }

        private void SendMessage(IPlayer target, IPlayer sender, string message)
        {
            var format = $"{sender.Name}: {message}";
            if (BetterChat != null)
                format = (string)BetterChat.Call("API_GetFormattedMessage", sender.Id, message);
            else if (config.UseRandomColors)
                format = covalence.FormatText($"[#{random.Next(0x1000000):X6}]{sender.Name}[/#]: {message}");
            else
                format = covalence.FormatText($"[{(sender.IsAdmin ? "#af5af5" : "#55aaff")}]{sender.Name}[/#]: {message}");
#if RUST
            var basePlayer = target.Object as BasePlayer;
            basePlayer.SendConsoleCommand("chat.add", sender.Id, format, 1.0);
#else
            target.Message(format);
#endif
        }

        private object OnUserChat(IPlayer player, string message)
        {
            if (UFilter != null)
            {
                var advertisements = (string[])UFilter.Call("Advertisements");
                if (advertisements != null && advertisements.Contains(message)) return null;
            }

            foreach (var target in players.Connected)
            {
#if !DEBUG
                if (player.Equals(target))
                {
                    SendMessage(player, player, message);
                    continue;
                }
#endif
                Action<string> callback = response =>
                {
                    if (config.ShowOriginal) response = $"{message}\n{response}";
                    SendMessage(target, player, response);
                };
                Translate(message, target.Id, player.Id, callback);
            }

            return BetterChat == null ? (object)true : null;
        }

        private bool OnBetterChat() => true;

        #endregion
    }
}
