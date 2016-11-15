﻿using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Healer", "Wulf/lukespragg", "2.4.0", ResourceId = 658)]
    [Description("Allows players with permission to heal themselves or others")]

    class Healer : CovalencePlugin
    {
        #region Initialization

        readonly Hash<string, float> cooldowns = new Hash<string, float>();
        const string permUse = "healer.use";

        int cooldown;
        int maxAmount;

        protected override void LoadDefaultConfig()
        {
            // Settings
            Config["Cooldown (Seconds, 0 to Disable)"] = cooldown = GetConfig("Cooldown (Seconds, 0 to Disable)", 30);
            Config["Maximum Heal Amount (1 - Infinity)"] = maxAmount = GetConfig("Maximum Heal Amount (1 - Infinity)", 100);

            // Cleanup
            Config.Remove("MaxAmount");
            Config.Remove("UsageCooldown");

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
            permission.RegisterPermission(permUse, this);
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Usage: {0} <amount> <name or id> (target optional)",
                ["Cooldown"] = "Wait a bit before attempting to use '{0}' again",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerNotFound"] = "Player '{0}' was not found",
                ["PlayerWasHealed"] = "{0} was healed {1}",
                ["PlayersHealed"] = "All players have been healed {0}!",
                ["YouWereHealed"] = "You were healed {0}"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Utilisation : {0} <montant> <nom ou id> (objectif en option)",
                ["Cooldown"] = "Attendre un peu avant de tenter de réutiliser « {0} »",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["PlayerNotFound"] = "Player « {0} » n’a pas été trouvée",
                ["PlayerWasHealed"] = "{0} a été guéri {1}",
                ["PlayersHealed"] = "Tous les joueurs ont été guéris {0} !",
                ["YouWereHealed"] = "Vous avez été guéri {0}"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Verwendung: {0} <Betrag> <Name oder Id> (Ziel optional)",
                ["Cooldown"] = "Noch ein bisschen warten Sie, bevor Sie '{0}' wieder verwenden",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["PlayerNotFound"] = "Player '{0}' wurde nicht gefunden",
                ["PlayerWasHealed"] = "{0} wurde geheilt {1}",
                ["PlayersHealed"] = "Alle Spieler sind geheilt worden {0}!",
                ["YouWereHealed"] = "Sie wurden geheilt {0}"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Использование: {0} <сумма> <имя или id> (цель необязательно)",
                ["Cooldown"] = "Подождите немного, прежде чем использовать «{0}» снова",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["PlayerNotFound"] = "Игрок «{0}» не найден",
                ["PlayerWasHealed"] = "{0} был исцелен {1}",
                ["PlayersHealed"] = "Все игроки были исцелены {0}!",
                ["YouWereHealed"] = "Вы были зарубцевавшиеся {0}"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Uso: {0} <cantidad> <nombre o id> (destino opcional)",
                ["Cooldown"] = "Esperar un poco antes de intentar volver a utilizar '{0}'",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["PlayerNotFound"] = "Jugador '{0}' no se encontró",
                ["PlayerWasHealed"] = "{0} es {1} curado",
                ["PlayersHealed"] = "Todos los jugadores han sido sanados {0}!",
                ["YouWereHealed"] = "Fuiste sanado {0}"
            }, this, "es");
        }

        #endregion

        #region Healing

        bool IsAllowed(IPlayer player, string command, string[] args)
        {
            // Check if player has permission or is the server console
            if (!permission.UserHasPermission(player.Id, permUse) && player.Id != "server_console")
            {
                // Send not allowed message to player
                player.Reply(Lang("NotAllowed", player.Id, command));
                return false;
            }

            // Check if no arguments are given or if command is "healall"
            if (args.Length == 0 && command != "healall")
            {
                // Send command usage message to player
                player.Reply(Lang("CommandUsage", player.Id, command));
                return false;
            }

            // Check if player doesn't have a cooldown, add if so
            if (!cooldowns.ContainsKey(player.Id)) cooldowns.Add(player.Id, 0f);

            // Check if player has no cooldown, allow if so
            if (cooldown == 0 || !(cooldowns[player.Id] + cooldown > Interface.Oxide.Now)) return true;

            // Send cooldown message to player
            player.Reply(Lang("Cooldown", player.Id, command));
            return false;
        }

        void Heal(IPlayer player, float amount)
        {
#if RUST
            // Rust-specific healing functionality
            var basePlayer = player.Object as BasePlayer;
            basePlayer.metabolism.bleeding.value = 0;
            basePlayer.metabolism.calories.value += amount;
            basePlayer.metabolism.dirtyness.value = 0;
            basePlayer.metabolism.hydration.value += amount;
            basePlayer.metabolism.oxygen.value = 1;
            basePlayer.metabolism.poison.value = 0;
            basePlayer.metabolism.radiation_level.value = 0;
            basePlayer.metabolism.radiation_poison.value = 0;
            basePlayer.metabolism.wetness.value = 0;
            basePlayer.StopWounded();
#endif
            // Heal the player by given amount
            player.Heal(amount);
        }

        #endregion

        #region Commands

        [Command("heal")]
        void HealCommand(IPlayer player, string command, string[] args)
        {
            // Check if player is allowed to use command
            if (!IsAllowed(player, command, args)) return;

            // Grab any amount if given
            float amount;
            var amountGiven = float.TryParse(args[0], out amount);

            // Check for valid arguments and target
            IPlayer target;
            if (args.Length >= 2 && amountGiven) target = players.FindPlayer(args[1]) ?? player;
            else target = players.FindPlayer(args[0]) ?? player;

            // Check if player is the server console or isn't connected
            if (target.Id == "server_console" || !target.IsConnected)
            {
                // Send player not found message to player
                var name = args.Length >= 2 && amountGiven ? args[1] : !amountGiven ? args[0] : target.Name;
                player.Reply(Lang("PlayerNotFound", player.Id, name));
                return;
            }

            // Check if heal amount is over maximum amount allowed
            if (amount > maxAmount || amount.Equals(0)) amount = maxAmount;

            // Heal target player by given amount
            Heal(target, amount);

            // Update cooldown time
            cooldowns[player.Id] = Interface.Oxide.Now;

            // Send healed message to target player
            target.Message(Lang("YouWereHealed", player.Id, amount));

            // Skip message if target player is player using command
            if (!Equals(target, player)) player.Reply(Lang("PlayerWasHealed", player.Id, target.Name, amount));
        }

        [Command("healall")]
        void HealAllCommand(IPlayer player, string command, string[] args)
        {
            // Check if player is allowed to use command
            if (!IsAllowed(player, command, args)) return;

            // Grab any amount if given
            float amount = 0;
            if (args.Length > 0) float.TryParse(args[0], out amount);

            // Check if heal amount is over maximum amount allowed
            if (amount > maxAmount || amount.Equals(0)) amount = maxAmount;

            // Loop through all connected players
            foreach (var target in players.Connected)
            {
                // Heal target player by given amount
                Heal(target, amount);

                // Update cooldown time
                cooldowns[player.Id] = Interface.Oxide.Now;

                // Send healed message to target player
                target.Message(Lang("YouWereHealed", player.Id, amount));
            }

            // Send players healed message to player
            player.Reply(Lang("PlayersHealed", player.Id, amount));
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
