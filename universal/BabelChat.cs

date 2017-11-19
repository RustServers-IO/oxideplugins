// Requires: Babel

using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("BabelChat", "Wulf/lukespragg", "1.1.3", ResourceId = 1964)]
    [Description("Translates chat messages to each player's language preference or server default")]
    public class BabelChat : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Force default server language (true/false)")]
            public bool ForceDefault;

            [JsonProperty(PropertyName = "Log chat messages (true/false)")]
            public bool LogChatMessages;

            [JsonProperty(PropertyName = "Show original message (true/false)")]
            public bool ShowOriginal;

            [JsonProperty(PropertyName = "Use random name colors (true/false)")]
            public bool UseRandomColors;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    ForceDefault = false,
                    LogChatMessages = true,
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

        #endregion Configuration

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
            var prefix = sender.Name;
            if (BetterChat != null)
            {
                message = (string)BetterChat.Call("API_GetFormattedMessage", sender, message);
                prefix = (string)BetterChat.Call("API_GetFormattedUsername", sender);
            }
            else if (config.UseRandomColors)
                prefix = covalence.FormatText($"[#{random.Next(0x1000000):X6}]{sender.Name}[/#]");
            else
                prefix = covalence.FormatText($"[{(sender.IsAdmin ? "#af5af5" : "#55aaff")}]{sender.Name}[/#]");
#if RUST
            var basePlayer = target.Object as BasePlayer;
            basePlayer.SendConsoleCommand("chat.add2", sender.Id, message, prefix);
#else
            target.Message(message, prefix);
#endif
            if (config.LogChatMessages)
            {
                LogToFile("log", message, this);
                Log($"{sender.Name}: {message}");
            }
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

        #endregion Chat Translation
    }
}
