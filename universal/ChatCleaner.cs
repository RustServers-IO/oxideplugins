using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("ChatCleaner", "Wulf/lukespragg", "0.4.0", ResourceId = 1183)]
    [Description("Clears/resets a player's chat when joining the server and on command")]
    public class ChatCleaner : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Clean chat on connect (true/false)")]
            public bool CleanOnConnect;

            [JsonProperty(PropertyName = "Show chat cleaned message (true/false)")]
            public bool ShowChatCleaned;

            [JsonProperty(PropertyName = "Show welcome message (true/false)")]
            public bool ShowWelcome;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    CleanOnConnect = true,
                    ShowChatCleaned = true,
                    ShowWelcome = true
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.CleanOnConnect == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatCleaned"] = "Chat has been cleaned",
                ["CommandClean"] = "cleanchat",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["Welcome"] = "Welcome to {0}, {1}!"
            }, this);
        }

        #endregion

        #region Initialization

        private const string permUse = "chatcleaner.use";

        private void Init()
        {
            permission.RegisterPermission(permUse, this);

            AddCommandAliases("CommandClean", "CleanCommand");
        }

        #endregion

        #region Chat Cleaning

        // TODO: Add support for BetterChat

        private void OnUserConnected(IPlayer player)
        {
            if (!config.CleanOnConnect) return;

            player.Message(new String('\n', 300));
            if (config.ShowChatCleaned) Message(player, "ChatCleaned");
            if (config.ShowWelcome) Message(player, "Welcome", server.Name, player.Name);
        }

        private void CleanCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            player.Message(new String('\n', 300));
            if (config.ShowChatCleaned) Message(player, "ChatCleaned");
        }

        #endregion

        #region Helpers

        private void AddCommandAliases(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.Equals(key))) AddCovalenceCommand(message.Value, command);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));

        #endregion
    }
}
