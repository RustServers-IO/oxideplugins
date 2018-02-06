/*
 * TODO: Add GUI for Rust to show player frozen status
 * TODO: Handle offline players in freezeall/unfreezeall
 */

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Freeze", "Wulf/lukespragg", "2.1.0", ResourceId = 1432)]
    [Description("Stops one or more players on command and keeps them from moving")]
    public class Freeze : CovalencePlugin
    {
        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandFreeze"] = "freeze",
                ["CommandFreezeAll"] = "freezeall",
                ["CommandUnfreeze"] = "unfreeze",
                ["CommandUnfreezeAll"] = "unfreezeall",
                ["CommandUsage"] = "Usage: {0} <name or id>",
                ["NoPlayersFound"] = "No players found with '{0}'",
                ["NoPlayersFreeze"] = "No players to freeze",
                ["NoPlayersUnfreeze"] = "No players to unfreeze",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerFrozen"] = "{0} has been frozen",
                ["PlayerIsProtected"] = "{0} is protected and cannot be frozen",
                ["PlayerIsFrozen"] = "{0} is already frozen",
                ["PlayerNotFrozen"] = "{0} is not frozen",
                ["PlayerUnfrozen"] = "{0} has been unfrozen",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["PlayersFrozen"] = "All players have been frozen",
                ["PlayersUnfrozen"] = "All players have been unfrozen",
                ["YouAreFrozen"] = "You are frozen",
                ["YouWereUnfrozen"] = "You were unfrozen"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandFreeze"] = "gel",
                ["CommandFreezeAll"] = "figertous",
                ["CommandUnfreeze"] = "dégeler",
                ["CommandUnfreezeAll"] = "débloquertousles",
                ["CommandUsage"] = "Utilisation : {0} <nom ou id>",
                ["NoPlayersFound"] = "Aucun joueur trouvée « {0} »",
                ["NoPlayersFreeze"] = "Aucun joueur de geler",
                ["NoPlayersUnfreeze"] = "Pas de joueurs à débloquer",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["PlayerFrozen"] = "{0} est déjà gelé",
                ["PlayerIsProtected"] = "{0} est protégé et ne peut pas être gelé",
                ["PlayerIsFrozen"] = "{0} est déjà gelé",
                ["PlayerNotFrozen"] = "{0} n’est pas figée",
                ["PlayerUnfrozen"] = "{0} a été congelés",
                ["PlayersFrozen"] = "Tous les joueurs ont été gelés",
                ["PlayersFound"] = "Plusieurs joueurs ont été trouvées, veuillez préciser : {0}",
                ["PlayersUnfrozen"] = "Tous les joueurs ont été gelés",
                ["YouAreFrozen"] = "Vous êtes gelé",
                ["YouWereUnfrozen"] = "Vous ont été dégelés"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandFreeze"] = "einfrieren",
                ["CommandFreezeAll"] = "alleeinfrieren",
                ["CommandUnfreeze"] = "fixierungaufheben",
                ["CommandUnfreezeAll"] = "alletaue",
                ["CommandUsage"] = "Verbrauch: {0} <Name oder Id>",
                ["NoPlayersFound"] = "Keine Spieler mit '{0}' gefunden",
                ["NoPlayersFreeze"] = "Keine Spieler zu frieren",
                ["NoPlayersUnfreeze"] = "Keine Spieler Auftauen",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["PlayerFrozen"] = "{0} wurde eingefroren",
                ["PlayerIsProtected"] = "{0} ist geschützt und kann nicht eingefroren werden",
                ["PlayerIsFrozen"] = "{0} ist bereits eingefroren",
                ["PlayerNotFrozen"] = "{0} ist nicht gefroren",
                ["PlayerUnfrozen"] = "{0} ist nicht gefroren gewesen",
                ["PlayersFrozen"] = "Alle Spieler wurden eingefroren",
                ["PlayersFound"] = "Mehrere Spieler wurden gefunden, bitte angeben: {0}",
                ["PlayersUnfrozen"] = "Alle Spieler sind nicht gefroren gewesen.",
                ["YouAreFrozen"] = "Sie sind eingefroren",
                ["YouWereUnfrozen"] = "Du warst nicht fixierten"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandFreeze"] = "замораживание",
                ["CommandFreezeAll"] = "заморозитьвсе",
                ["CommandUnfreeze"] = "разморозить",
                ["CommandUnfreezeAll"] = "разблокироватьвсе",
                ["CommandUsage"] = "Использование: {0} <имя или id>",
                ["NoPlayersFound"] = "Игроки, не нашел с «{0}»",
                ["NoPlayersFreeze"] = "Нет игроков, чтобы заморозить",
                ["NoPlayersUnfreeze"] = "Нет игроков, чтобы разморозить",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["PlayerFrozen"] = "{0} был заморожен",
                ["PlayerIsProtected"] = "{0} является защищенным и не может быть заморожен",
                ["PlayerIsFrozen"] = "{0} уже заморожены",
                ["PlayerNotFrozen"] = "{0} не является замороженным",
                ["PlayerUnfrozen"] = "{0} был unfrozen",
                ["PlayersFrozen"] = "Все игроки были заморожены",
                ["PlayersFound"] = "Несколько игроков были найдены, пожалуйста укажите: {0}",
                ["PlayersUnfrozen"] = "Все игроки были разморожены",
                ["YouAreFrozen"] = "Вы замороженные",
                ["YouWereUnfrozen"] = "Вы были разморожены"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandFreeze"] = "congelación",
                ["CommandFreezeAll"] = "congelartodos",
                ["CommandUnfreeze"] = "liberar",
                ["CommandUnfreezeAll"] = "liberartodos",
                ["CommandUsage"] = "Uso: {0} <nombre o id>",
                ["NoPlayersFound"] = "No hay jugadores con '{0}'",
                ["NoPlayersFreeze"] = "No hay jugadores para congelar",
                ["NoPlayersUnfreeze"] = "No hay jugadores para liberar",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["PlayerFrozen"] = "{0} ha sido congelado",
                ["PlayerIsProtected"] = "{0} está protegida y no puede ser congelado",
                ["PlayerIsFrozen"] = "{0} está ya congelada",
                ["PlayerNotFrozen"] = "{0} no está congelados",
                ["PlayerUnfrozen"] = "{0} ha sido desbloqueado",
                ["PlayersFrozen"] = "Todos los jugadores han sido congelados",
                ["PlayersFound"] = "Varios jugadores fueron encontrados, por favor especifique: {0}",
                ["PlayersUnfrozen"] = "Todos los jugadores han sido las",
                ["YouAreFrozen"] = "Se congelan",
                ["YouWereUnfrozen"] = "Fueron sin congelar"
            }, this, "es");
        }

        #endregion Localization

        #region Initialization

        private readonly Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        private const string permFrozen = "freeze.frozen";
        private const string permProtect = "freeze.protect";
        private const string permUse = "freeze.use";

        private void Init()
        {
            permission.RegisterPermission(permFrozen, this);
            permission.RegisterPermission(permProtect, this);
            permission.RegisterPermission(permUse, this);

            AddLocalizedCommand("CommandFreeze", "FreezeCommand");
            AddLocalizedCommand("CommandFreezeAll", "FreezeAllCommand");
            AddLocalizedCommand("CommandUnfreeze", "UnfreezeCommand");
            AddLocalizedCommand("CommandUnfreezeAll", "UnfreezeAllCommand");
        }

        #endregion Initialization

        #region Freeze Command

        private void FreezeCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1)
            {
                Message(player, "CommandUsage", command);
                return;
            }

            var foundPlayers = players.FindPlayers(args[0]).Where(p => p.IsConnected).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return;
            }

            var target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null || !target.IsConnected)
            {
                Message(player, "PlayerNotFound", args[0]);
                return;
            }

            if (!target.HasPermission(permFrozen))
            {
                FreezePlayer(target);
                Message(target, "YouAreFrozen");
                Message(player, "PlayerFrozen", target.Name.Sanitize());
            }
            else
                Message(player, "PlayerIsFrozen", target.Name.Sanitize());
        }

        #endregion Freeze Command

        #region Freeze All Command

        private void FreezeAllCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            foreach (var target in players.Connected)
            {
                if (target.HasPermission(permProtect) || target.HasPermission(permFrozen)) continue;

                FreezePlayer(target);
                if (target.IsConnected) Message(target, "YouAreFrozen");
            }

            Message(player, players.Connected.Any() ? "PlayersFrozen" : "NoPlayersFreeze");
        }

        #endregion Freeze All Command

        #region Unfreeze Command

        private void UnfreezeCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1)
            {
                Message(player, "CommandUsage", command);
                return;
            }

            var foundPlayers = players.FindPlayers(args[0]).Where(p => p.IsConnected).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return;
            }

            var target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null || !target.IsConnected)
            {
                Message(player, "PlayerNotFound", args[0]);
                return;
            }

            if (target.HasPermission(permFrozen))
            {
                UnfreezePlayer(target);
                Message(target, "YouWereUnfrozen", target.Id);
                Message(player, "PlayerUnfrozen", target.Name.Sanitize());
            }
            else
                Message(player, "PlayerNotFrozen", target.Name.Sanitize());
        }

        #endregion Unfreeze Command

        #region Unfreeze All Command

        private void UnfreezeAllCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            foreach (var target in players.Connected)
            {
                if (!target.HasPermission(permFrozen)) continue;

                UnfreezePlayer(target);
                if (target.IsConnected) Message(target, "YouWereUnfrozen");
            }

            player.Reply(Lang(players.Connected.Any() ? "PlayersUnfrozen" : "NoPlayersUnfreeze"));
        }

        #endregion Unfreeze All Command

        #region Freeze Handling

        private void FreezePlayer(IPlayer player)
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

        private void UnfreezePlayer(IPlayer player)
        {
            player.RevokePermission(permFrozen);

            if (timers.ContainsKey(player.Id)) timers[player.Id].Destroy();
        }

        private void OnUserConnected(IPlayer player)
        {
            if (!player.HasPermission(permFrozen)) return;

            FreezePlayer(player);
            Log(Lang("PlayerFrozen", null, player.Name.Sanitize()));
        }

        private void OnServerInitialized()
        {
            foreach (var player in players.Connected)
            {
                if (!player.HasPermission(permFrozen)) continue;

                FreezePlayer(player);
                Log(Lang("PlayerFrozen", null, player.Name.Sanitize()));
            }
        }

        #endregion Freeze Handling

        #region Helpers

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.Equals(key)))
                    if (!string.IsNullOrEmpty(message.Value)) AddCovalenceCommand(message.Value, command);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));

        #endregion Helpers
    }
}
