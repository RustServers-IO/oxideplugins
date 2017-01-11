using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("StartupCommands", "Wulf/lukespragg", "1.0.1", ResourceId = 774)]
    [Description("Automatically runs configured commands on server startup")]

    class StartupCommands : CovalencePlugin
    {
        #region Initialization

        const string permAdmin = "startupcommands.admin";

        List<object> commands;

        protected override void LoadDefaultConfig()
        {
            Config["Commands"] = commands = GetConfig("Commands", new List<object> { "version", "oxide.version" });
            SaveConfig();
        }

        void OnServerInitialized()        {
            LoadDefaultConfig();
            LoadDefaultMessages();
            permission.RegisterPermission(permAdmin, this);

            foreach (var command in commands) server.Command(command.ToString());
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Command '{0}' was added to the startup command list",
                ["CommandListed"] = "Command '{0}' is already in the startup command list",
                ["CommandNotListed"] = "Command '{0}' is not in the startup command list",
                ["CommandRemoved"] = "Command '{0}' was removed from the startup command list",
                ["CommandUsage"] = "Usage: {0} <add | remove | list> <command>",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Commande « {0} » a été ajouté à la liste de commande de démarrage",
                ["CommandListed"] = "Commande « {0} » est déjà dans la liste de commande de démarrage",
                ["CommandNotListed"] = "Commande « {0} » n’est pas dans la liste de commande de démarrage",
                ["CommandRemoved"] = "Commande « {0} » a été supprimé de la liste de commande de démarrage",
                ["CommandUsage"] = "Utilisation : {0} <ajouter | supprimer | liste> <commande>",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Befehl '{0}' wurde auf der Startliste Befehl hinzugefügt",
                ["CommandListed"] = "Befehl '{0}' ist bereits in der Startliste Befehl",
                ["CommandNotListed"] = "Befehl '{0}' ist nicht in der Startliste Befehl",
                ["CommandRemoved"] = "Befehl '{0}' wurde aus der Startliste Befehl entfernt",
                ["CommandUsage"] = "Verbrauch: {0} <hinzufügen | entfernen | liste> <befehl>",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Команда «{0}» был добавлен в список команд запуска",
                ["CommandListed"] = "Команда «{0}» уже находится в списке команд запуска",
                ["CommandNotListed"] = "Команда «{0}» не включен в список команд запуска",
                ["CommandRemoved"] = "Команда «{0}» был удален из списка команд запуска",
                ["CommandUsage"] = "Использование: {0} <добавить | удалить | список> <команда>",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdded"] = "Comando '{0}' se añadió a la lista de comandos de inicio",
                ["CommandListed"] = "Comando '{0}' ya está en la lista de comandos de inicio",
                ["CommandNotListed"] = "Comando '{0}' no está en la lista de comandos de inicio",
                ["CommandRemoved"] = "Comando '{0}' se quitó de la lista de comandos de inicio",
                ["CommandUsage"] = "Uso: {0} <añadir | eliminar | lista> <comando>",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
            }, this, "es");
        }

        #endregion

        [Command("autocmd", "startcmd", "startupcmd")]
        void Command(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            var argCommand = string.Join(" ", args.Skip(1).Select(v => v.ToString()).ToArray());
            if (args.Length == 0 || args[0] != "list" && string.IsNullOrEmpty(argCommand))
            {
                player.Reply(Lang("CommandUsage", player.Id, command));
                return;
            }

            switch (args[0])
            {
                case "add":
                    if (commands.Contains(argCommand))
                    {
                        player.Reply(Lang("CommandListed", player.Id, argCommand));
                        break;
                    }

                    commands.Add(argCommand);
                    Config["Commands"] = commands;
                    SaveConfig();

                    player.Reply(Lang("CommandAdded", player.Id, argCommand));
                    break;

                case "remove":
                    if (!commands.Contains(argCommand))
                    {
                        player.Reply(Lang("CommandNotListed", player.Id, argCommand));
                        break;
                    }

                    commands.Remove(argCommand);
                    Config["Commands"] = commands;
                    SaveConfig();

                    player.Reply(Lang("CommandRemoved", player.Id, argCommand));
                    break;

                case "list":
                    player.Reply("Startup commands: " + string.Join(", ", commands.Cast<string>().ToArray()));
                    break;

                default:
                    player.Reply(Lang("CommandUsage", player.Id, command));
                    break;
            }
        }

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
