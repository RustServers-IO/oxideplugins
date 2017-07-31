﻿using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("StartupCommands", "Wulf/lukespragg", "1.0.7", ResourceId = 774)]
    [Description("Automatically runs configured commands on server startup")]
    public class StartupCommands : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Commands")]
            public List<string> Commands;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    Commands = new List<string> { "version", "oxide.version" }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.Commands == null) LoadDefaultConfig();
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

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Command '{0}' was added to the startup command list",
                ["CommandAlias"] = "autocmd",
                ["CommandListed"] = "Command '{0}' is already in the startup command list",
                ["CommandNotListed"] = "Command '{0}' is not in the startup command list",
                ["CommandRemoved"] = "Command '{0}' was removed from the startup command list",
                ["CommandUsage"] = "Usage: {0} <add | remove | list> <command>",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["StartupCommands"] = "Startup commands: {0}"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Commande « {0} » a été ajouté à la liste de commande de démarrage",
                ["CommandAlias"] = "commandeauto",
                ["CommandListed"] = "Commande « {0} » est déjà dans la liste de commande de démarrage",
                ["CommandNotListed"] = "Commande « {0} » n’est pas dans la liste de commande de démarrage",
                ["CommandRemoved"] = "Commande « {0} » a été supprimé de la liste de commande de démarrage",
                ["CommandUsage"] = "Utilisation : {0} <add | remove | list> <commande>",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["StartupCommands"] = "Commandes de démarrage: {0}"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Befehl '{0}' wurde auf der Startliste Befehl hinzugefügt",
                ["CommandAlias"] = "autobefehl",
                ["CommandListed"] = "Befehl '{0}' ist bereits in der Startliste Befehl",
                ["CommandNotListed"] = "Befehl '{0}' ist nicht in der Startliste Befehl",
                ["CommandRemoved"] = "Befehl '{0}' wurde aus der Startliste Befehl entfernt",
                ["CommandUsage"] = "Verbrauch: {0} <add | remove | list> <befehl>",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["StartupCommands"] = "Startbefehle: {0}"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Команда «{0}» был добавлен в список команд запуска",
                ["CommandAlias"] = "командуauto",
                ["CommandListed"] = "Команда «{0}» уже находится в списке команд запуска",
                ["CommandNotListed"] = "Команда «{0}» не включен в список команд запуска",
                ["CommandRemoved"] = "Команда «{0}» был удален из списка команд запуска",
                ["CommandUsage"] = "Использование: {0} <add | remove | list> <команда>",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["StartupCommands"] = "При запуске команды: {0}"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Comando '{0}' se añadió a la lista de comandos de inicio",
                ["CommandAlias"] = "comandodeauto",
                ["CommandListed"] = "Comando '{0}' ya está en la lista de comandos de inicio",
                ["CommandNotListed"] = "Comando '{0}' no está en la lista de comandos de inicio",
                ["CommandRemoved"] = "Comando '{0}' se quitó de la lista de comandos de inicio",
                ["CommandUsage"] = "Uso: {0} <add | remove | list> <comando>",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["StartupCommands"] = "Comandos de inicio de: {0}"
            }, this, "es");
        }

        #endregion

        #region Initialization

        private const string permAdmin = "startupcommands.admin";

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permAdmin, this);

            AddCommandAliases("CommandAlias", "StartupCommand");
            AddCovalenceCommand("startcmd", "StartupCommand");
            AddCovalenceCommand("startupcmd", "StartupCommand");

            foreach (var command in config.Commands) server.Command(command);
        }

        #endregion

        #region Commands

        private void StartupCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1 || args.Length < 2 && args[0].ToLower() != "list")
            {
                Message(player, "CommandUsage", command);
                return;
            }

            var argCommand = string.Join(" ", args.Skip(1).Select(v => v).ToArray());
            switch (args[0].ToLower())
            {
                case "+":
                case "add":
                    if (config.Commands.Contains(argCommand))
                    {
                        Message(player, "CommandListed", argCommand);
                        break;
                    }

                    config.Commands.Add(argCommand);
                    SaveConfig();

                    Message(player, "CommandAdded", argCommand);
                    break;

                case "-":
                case "del":
                case "remove":
                    if (!config.Commands.Contains(argCommand))
                    {
                        Message(player, "CommandNotListed", argCommand);
                        break;
                    }

                    config.Commands.Remove(argCommand);
                    SaveConfig();

                    Message(player, "CommandRemoved", argCommand);
                    break;

                case "list":
                    Message(player, "StartupCommands", string.Join(", ", config.Commands.Cast<string>().ToArray()));
                    break;

                default:
                    Message(player, "CommandUsage", command);
                    break;
            }
        }

        #endregion

        #region Helpers

        private void AddCommandAliases(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.StartsWith(key))) AddCovalenceCommand(message.Value, command);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));

        #endregion
    }
}
