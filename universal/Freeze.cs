/*
 * TODO:
 * Add GUI for Rust to show player frozen status
 * Handle offline players in freezeall/unfreezeall
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Freeze", "Wulf/lukespragg", "2.0.1", ResourceId = 1432)]
    [Description("Stops a player or players in their tracks and keep them from moving")]

    class Freeze : CovalencePlugin
    {
        #region Initialization

        readonly Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        const string permFrozen = "freeze.frozen";
        const string permProtect = "freeze.protect";
        const string permUse = "freeze.use";

        void Init()
        {
            LoadDefaultMessages();
            permission.RegisterPermission(permFrozen, this);
            permission.RegisterPermission(permProtect, this);
            permission.RegisterPermission(permUse, this);
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Usage: {0} <name or id>",
                ["NoPlayersFreeze"] = "No players to freeze",
                ["NoPlayersUnfreeze"] = "No players to unfreeze",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerFrozen"] = "{0} has been frozen",
                ["PlayerIsProtected"] = "{0} is protected and cannot be frozen",
                ["PlayerIsFrozen"] = "{0} is already frozen",
                ["PlayerNotFound"] = "No players were found with that name or ID",
                ["PlayerNotFrozen"] = "{0} is not frozen",
                ["PlayerUnfrozen"] = "{0} has been unfrozen",
                ["PlayersFrozen"] = "All players have been frozen",
                ["PlayersUnfrozen"] = "All players have been unfrozen",
                ["YouAreFrozen"] = "You are frozen",
                ["YouWereUnrozen"] = "You were unfrozen"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Utilisation : {0} <nom ou id>",
                ["NoPlayersFreeze"] = "Aucun joueur de geler",
                ["NoPlayersUnfreeze"] = "Pas de joueurs à débloquer",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["PlayerFrozen"] = "{0} est déjà gelé",
                ["PlayerIsProtected"] = "{0} est protégé et ne peut pas être gelé",
                ["PlayerIsFrozen"] = "{0} est déjà gelé",
                ["PlayerNotFound"] = "Aucun joueur ne trouvées avec ce nom ou ID",
                ["PlayerNotFrozen"] = "{0} n’est pas figée",
                ["PlayerUnfrozen"] = "{0} a été congelés",
                ["PlayersFrozen"] = "Tous les joueurs ont été gelés",
                ["PlayersUnfrozen"] = "Tous les joueurs ont été gelés",
                ["YouAreFrozen"] = "Vous êtes gelé",
                ["YouWereUnrozen"] = "Vous ont été dégelés"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Verbrauch: {0} <Name oder Id>",
                ["NoPlayersFreeze"] = "Keine Spieler zu frieren",
                ["NoPlayersUnfreeze"] = "Keine Spieler Auftauen",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["PlayerFrozen"] = "{0} wurde eingefroren",
                ["PlayerIsProtected"] = "{0} ist geschützt und kann nicht eingefroren werden",
                ["PlayerIsFrozen"] = "{0} ist bereits eingefroren",
                ["PlayerNotFound"] = "Keine Spieler fanden sich mit diesem Name oder ID",
                ["PlayerNotFrozen"] = "{0} ist nicht gefroren",
                ["PlayerUnfrozen"] = "{0} ist nicht gefroren gewesen",
                ["PlayersFrozen"] = "Alle Spieler wurden eingefroren",
                ["PlayersUnfrozen"] = "Alle Spieler sind nicht gefroren gewesen.",
                ["YouAreFrozen"] = "Sie sind eingefroren",
                ["YouWereUnrozen"] = "Du warst nicht fixierten"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Использование: {0} <имя или id>",
                ["NoPlayersFreeze"] = "Нет игроков, чтобы заморозить",
                ["NoPlayersUnfreeze"] = "Нет игроков, чтобы разморозить",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["PlayerFrozen"] = "{0} был заморожен",
                ["PlayerIsProtected"] = "{0} является защищенным и не может быть заморожен",
                ["PlayerIsFrozen"] = "{0} уже заморожены",
                ["PlayerNotFound"] = "Игроки не были найдены с этим именем или идентификатором",
                ["PlayerNotFrozen"] = "{0} не является замороженным",
                ["PlayerUnfrozen"] = "{0} был unfrozen",
                ["PlayersFrozen"] = "Все игроки были заморожены",
                ["PlayersUnfrozen"] = "Все игроки были разморожены",
                ["YouAreFrozen"] = "Вы замороженные",
                ["YouWereUnrozen"] = "Вы были разморожены"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Uso: {0} <nombre o id>",
                ["NoPlayersFreeze"] = "No hay jugadores para congelar",
                ["NoPlayersUnfreeze"] = "No hay jugadores para liberar",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["PlayerFrozen"] = "{0} ha sido congelado",
                ["PlayerIsProtected"] = "{0} está protegida y no puede ser congelado",
                ["PlayerIsFrozen"] = "{0} está ya congelada",
                ["PlayerNotFound"] = "No hay jugadores se encontraron con nombre o ID",
                ["PlayerNotFrozen"] = "{0} no está congelados",
                ["PlayerUnfrozen"] = "{0} ha sido desbloqueado",
                ["PlayersFrozen"] = "Todos los jugadores han sido congelados",
                ["PlayersUnfrozen"] = "Todos los jugadores han sido las",
                ["YouAreFrozen"] = "Se congelan",
                ["YouWereUnrozen"] = "Fueron sin congelar"
            }, this, "es");
        }

        #endregion

        #region Freeze Command

        [Command("freeze")]
        void FreezeCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("CommandUsage", player.Id, command));
                return;
            }

            var target = players.FindPlayer(args[0]);
            if (target == null || !target.IsConnected)
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            if (target.HasPermission(permFrozen))
            {
                player.Reply(Lang("PlayerIsProtected", player.Id, target.Name));
                return;
            }

            if (!target.HasPermission(permFrozen))
            {
                FreezePlayer(target);
                target.Message(Lang("YouAreFrozen", target.Id));
                player.Reply(Lang("PlayerFrozen", player.Id, target.Name.Sanitize()));
            }
            else
            {
                player.Reply(Lang("PlayerIsFrozen", player.Id, target.Name.Sanitize()));
            }
        }

        #endregion

        #region Unfreeze Command

        [Command("unfreeze")]
        void UnfreezeCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("CommandUsage", player.Id, command));
                return;
            }

            var target = players.FindPlayer(args[0]);
            if (target == null || !target.IsConnected)
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            if (target.HasPermission(permFrozen))
            {
                UnfreezePlayer(target);
                target.Message(Lang("YouWereUnfrozen", target.Id));
                player.Reply(Lang("PlayerUnfrozen", player.Id, target.Name.Sanitize()));
            }
            else
            {
                player.Reply(Lang("PlayerNotFrozen", player.Id, target.Name.Sanitize()));
            }
        }

        #endregion

        #region Freeze All Command

        [Command("freezeall")]
        void FreezeAllCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            foreach (var target in players.Connected)
            {
                if (target.HasPermission(permProtect) || target.HasPermission(permFrozen)) continue;

                FreezePlayer(target);
                if (target.IsConnected) target.Message(Lang("YouAreFrozen", target.Id));
            }

            player.Reply(Lang(players.Connected.Any() ? "PlayersFrozen" : "NoPlayersFreeze", player.Id));
        }

        #endregion

        #region Unfreeze All Command

        [Command("unfreezeall")]
        void UnfreezeAllCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            foreach (var target in players.Connected)
            {
                if (!target.HasPermission(permFrozen)) continue;

                UnfreezePlayer(target);
                if (target.IsConnected) target.Message(Lang("YouWereUnfrozen", target.Id));
            }

            player.Reply(Lang(players.Connected.Any() ? "PlayersUnfrozen" : "NoPlayersUnfreeze", player.Id));
        }

        #endregion

        #region Freeze Handling

        void FreezePlayer(IPlayer player)
        {
            player.GrantPermission(permFrozen);

            var pos = player.Position();
            timers[player.Id] = timer.Every(0.01f, () =>
            {
                if (!player.IsConnected)
                {
                    timers[player.Id].Destroy();
                    return;
                }

                if (!player.HasPermission(permFrozen)) UnfreezePlayer(player);
                else player.Teleport(pos.X, pos.Y, pos.Z);
            });
        }

        void UnfreezePlayer(IPlayer player)
        {
            player.RevokePermission(permFrozen);

            if (timers.ContainsKey(player.Id)) timers[player.Id].Destroy();
        }

        void OnUserConnected(IPlayer player)
        {
            if (!player.HasPermission(permFrozen)) return;

            FreezePlayer(player);
            Log(Lang("PlayerFrozen", null, player.Name.Sanitize()));
        }

        void OnServerInitialized()
        {
            foreach (var player in players.Connected)
            {
                if (!player.HasPermission(permFrozen)) continue;

                FreezePlayer(player);
                Log(Lang("PlayerFrozen", null, player.Name.Sanitize()));
            }
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
