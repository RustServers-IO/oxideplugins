using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Logger", "Wulf/lukespragg", "2.1.3", ResourceId = 670)]
    [Description("Configurable logging of chat, commands, connections, and more")]
    public class Logger : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Log chat messages (true/false)")]
            public bool LogChat;

            [JsonProperty(PropertyName = "Log command usage (true/false)")]
            public bool LogCommands;

            [JsonProperty(PropertyName = "Log player connections (true/false)")]
            public bool LogConnections;

            [JsonProperty(PropertyName = "Log player disconnections (true/false)")]
            public bool LogDisconnections;

            [JsonProperty(PropertyName = "Log player respawns (true/false)")]
            public bool LogRespawns;
#if RUST
            [JsonProperty(PropertyName = "Log items crafted by players (true/false)")]
            public bool LogCrafting;

            [JsonProperty(PropertyName = "Log items dropped by players (true/false)")]
            public bool LogItemDrops;
#endif
            [JsonProperty(PropertyName = "Log output to console (true/false)")]
            public bool LogToConsole;

            [JsonProperty(PropertyName = "Excluded commands (full or short commands)")]
            public List<string> ExcludedCommands;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    LogChat = true,
                    LogCommands = true,
                    LogConnections = true,
                    LogDisconnections = true,
                    LogRespawns = true,
#if RUST
                    LogCrafting = true,
                    LogItemDrops = true,
#endif
                    LogToConsole = true,
                    ExcludedCommands = new List<string> { "help", "version", "chat.say", "craft.add", "craft.canceltask",
                        "global.kill", "global.respawn", "global.respawn_sleepingbag", "global.status", "global.wakeup",
                        "inventory.endloot", "inventory.unlockblueprint" }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.ExcludedCommands == null) LoadDefaultConfig();
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

        #region Initialization

        private void Init()
        {
            if (!config.LogChat) Unsubscribe("OnUserChat");
            if (!config.LogCommands) Unsubscribe("OnServerCommand");
            if (!config.LogConnections) Unsubscribe("OnUserConnected");
            if (!config.LogDisconnections) Unsubscribe("OnUserDisconnected");
            if (!config.LogRespawns) Unsubscribe("OnUserRespawned");
#if RUST
            if (!config.LogCrafting) Unsubscribe("OnItemCraftFinished");
            if (!config.LogItemDrops) Unsubscribe("OnItemAction");
#endif
        }

        #endregion Initialization

        #region Localization

        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ItemCrafted"] = "{0} ({1}) crafted {2} {3}",
                ["ItemDropped"] = "{0} ({1} dropped {2} {3}",
                ["PlayerCommand"] = "{0} ({1}) ran command: {2} {3}",
                ["PlayerConnected"] = "{0} ({1}) connected from {2}",
                ["PlayerDisconnected"] = "{0} ({1}) disconnected",
                ["PlayerRespawned"] = "{0} ({1}) respawned at {2}"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ItemCrafted"] = "{0} ({1}) ouvré {2} {3}",
                ["ItemDropped"] = "{0} ({1} a chuté {2} {3}",
                ["PlayerCommand"] = "{0} ({1}) a couru la commande : {3} {2}",
                ["PlayerConnected"] = "{0} ({1}) reliant {2}",
                ["PlayerDisconnected"] = "{0} ({1}) déconnecté",
                ["PlayerRespawned"] = "{0} ({1}) réapparaître à {2}"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ItemCrafted"] = "{0} ({1}) in Handarbeit {2} {3}",
                ["ItemDropped"] = "{0} ({1} fallen gelassen {2} {3}",
                ["PlayerCommand"] = "{0} ({1}) lief Befehl: {2} {3}",
                ["PlayerConnected"] = "{0} ({1}) {2} verbunden",
                ["PlayerDisconnected"] = "{0} ({1}) nicht getrennt",
                ["PlayerRespawned"] = "{0} ({1}) bereits am {2}"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ItemCrafted"] = "{0} ({1}) созданный {2} {3}",
                ["ItemDropped"] = "{0} ({1} упал {2} {3}",
                ["PlayerCommand"] = "{0} ({1}) прописал команду: {2} {3}",
                ["PlayerConnected"] = "{0} ({1}) подключился с IP: {2}",
                ["PlayerDisconnected"] = "{0} ({1}) отключился",
                ["PlayerRespawned"] = "{0} ({1}) возродился по {2}"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ItemCrafted"] = "{0} ({1}) hecho a mano {2} {3}",
                ["ItemDropped"] = "{0} ({1} cayó {2} {3}",
                ["PlayerCommand"] = "{0} ({1}) funcionó la consola: {2} {3}",
                ["PlayerConnected"] = "{0} ({1}) conectado de {2}",
                ["PlayerDisconnected"] = "{0} ({1}) desconectado",
                ["PlayerRespawned"] = "{0} ({1}) hizo en {2}"
            }, this, "es");
        }

        #endregion Localization

        #region Logging

        private void OnUserChat(IPlayer player, string message) => Log("chat", $"{player.Name} ({player.Id}): {message}");

        private void OnUserConnected(IPlayer player) => Log("connections", Lang("PlayerConnected", null, player.Name, player.Id, player.Address));

        private void OnUserDisconnected(IPlayer player) => Log("disconnections", Lang("PlayerDisconnected", null, player.Name, player.Id));

        private void OnUserRespawned(IPlayer player) => Log("respawns", Lang("PlayerRespawned", null, player.Name, player.Id, player.Position().ToString()));

#if RUST
        private void OnItemAction(Item item, string action)
        {
            var player = item.parent?.playerOwner;
            if (action.ToLower() != "drop" || player == null) return;

            Log("itemdrop", Lang("ItemDropped", null, player.displayName, player.UserIDString, item.amount, item.info.displayName.english));
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            var player = task.owner;
            if (player == null) return;

            Log("crafting", Lang("ItemCrafted", null, player.displayName, player.UserIDString, item.amount, item.info.displayName.english));
        }

        private void OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return;

            var command = arg.cmd.FullName;
            var args = arg.GetString(0);
            if (command != "chat.say" && !config.ExcludedCommands.Contains(command))
                Log("commands", Lang("PlayerCommand", null, arg.Connection.username, arg.Connection.userid, command, arg.FullString));
        }
#endif
        private void OnUserCommand(IPlayer player, string command, string[] args)
        {
            if (!config.ExcludedCommands.Contains(command))
                Log("commands", Lang("PlayerCommand", null, player.Name, player.Id, command, string.Join(" ", args)));
        }

        #endregion Logging

        #region Helpers

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Log(string filename, string key, params object[] args)
        {
            if (config.LogToConsole) Puts(Lang(key, null, args));
            LogToFile(filename, $"[{System.DateTime.Now}] {Lang(key, null, args)}", this);
        }

        #endregion Helpers
    }
}
