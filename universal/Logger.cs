using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Logger", "Wulf/lukespragg", "2.0.3", ResourceId = 670)]
    [Description("Configurable logging of chat, commands, connections, and more")]

    class Logger : CovalencePlugin
    {
        #region Initialization

        List<object> cmdExclusions;

        bool logChat;
        bool logCommands;
        bool logConnections;
        bool logDisconnections;
        bool logRespawns;
        bool logToConsole;

        protected override void LoadDefaultConfig()
        {
            // Settings
            Config["Command Exclusions"] = cmdExclusions = GetConfig("Command Exclusions", new List<object>
            {
                "help", "version", "chat.say", "craft.add", "craft.canceltask", "global.kill", "global.respawn",
                "global.respawn_sleepingbag", "global.status", "global.wakeup", "inventory.endloot", "inventory.unlockblueprint"
            });

            // Options
            Config["Log Chat (true/false)"] = logChat = GetConfig("Log Chat (true/false)", false);
            Config["Log Commands (true/false)"] = logCommands = GetConfig("Log Commands (true/false)", true);
            Config["Log Connections (true/false)"] = logConnections = GetConfig("Log Connections (true/false)", true);
            Config["Log Disconnections (true/false)"] = logDisconnections = GetConfig("Log Disconnections (true/false)", true);
            Config["Log Respawns (true/false)"] = logRespawns = GetConfig("Log Respawns (true/false)", false);
            Config["Log to Console (true/false)"] = logToConsole = GetConfig("Log to Console (true/false)", true);

            // Cleanup
            Config.Remove("Exclusions");
            Config.Remove("LogChat");
            Config.Remove("LogCommands");
            Config.Remove("LogConnections");
            Config.Remove("LogRespawns");
            Config.Remove("LogToConsole");

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();

            if (!logChat) Unsubscribe("OnUserChat");
            if (!logCommands) Unsubscribe("OnServerCommand");
            if (!logConnections) Unsubscribe("OnUserConnected");
            if (!logDisconnections) Unsubscribe("OnUserDisconnected");
            if (!logRespawns) Unsubscribe("OnUserRespawned");
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Command"] = "{0} ({1}) ran command: {2} {3}",
                ["Connected"] = "{0} ({1}) connected from {2}",
                ["Disconnected"] = "{0} ({1}) disconnected",
                ["Respawned"] = "{0} ({1}) respawned at {2}"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Command"] = "{0} ({1}) a couru la commande : {3} {2}",
                ["Connected"] = "{0} ({1}) reliant {2}",
                ["Disconnected"] = "{0} ({1}) déconnecté",
                ["Respawned"] = "{0} ({1}) réapparaître à {2}"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Command"] = "{0} ({1}) lief Befehl: {2} {3}",
                ["Connected"] = "{0} ({1}) {2} verbunden",
                ["Disconnected"] = "{0} ({1}) nicht getrennt",
                ["Respawned"] = "{0} ({1}) bereits am {2}"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Command"] = "{0} ({1}) прописал команду: {2} {3}",
                ["Connected"] = "{0} ({1}) подключился с IP: {2}",
                ["Disconnected"] = "{0} ({1}) отключился",
                ["Respawned"] = "{0} ({1}) возродился по {2}"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Command"] = "{0} ({1}) funcionó la consola: {2} {3}",
                ["Connected"] = "{0} ({1}) conectado de {2}",
                ["Disconnected"] = "{0} ({1}) desconectado",
                ["Respawned"] = "{0} ({1}) hizo en {2}"
            }, this, "es");
        }

        #endregion

        #region Logging

        void OnUserChat(IPlayer player, string message)
        {
            if (logChat) Log("chat", $"{player.Name} ({player.Id}): {message}");
        }

        void OnUserConnected(IPlayer player)
        {
            if (logConnections) Log("connections", Lang("Connected", null, player.Name, player.Id, player.Address));
        }

        void OnUserDisconnected(IPlayer player)
        {
            if (logDisconnections) Log("disconnections", Lang("Disconnected", null, player.Name, player.Id));
        }

        void OnUserRespawned(IPlayer player)
        {
            if (logRespawns) Log("respawns", Lang("Respawned", null, player.Name, player.Id, player.Position().ToString()));
        }

#if RUST
        void OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (!logCommands || arg.Connection == null) return;

            var command = arg.cmd.FullName;
            var args = arg.GetString(0);

            if (args.StartsWith("/") && !cmdExclusions.Contains(args))
                Log("commands", Lang("Command", null, arg.Connection.username, arg.Connection.userid, args, null));
            if (command != "chat.say" && !cmdExclusions.Contains(command))
                Log("commands", Lang("Command", null, arg.Connection.username, arg.Connection.userid, command, arg.FullString));
        }
#else
        void OnUserCommand(IPlayer player, string command, string[] args)
        {
            if (logCommands && !cmdExclusions.Contains(command)) Log("commands", Lang("Command", null, player.Name, player.Id, command, string.Join(" ", args)));
        }
#endif

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        void Log(string filename, string text)
        {
            LogToFile(filename, $"[{DateTime.Now}] {text}", this);
            if (logToConsole) Puts(text);
        }

        #endregion
    }
}
