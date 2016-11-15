﻿/*
TODO:
- Add distance/radius restriction for slapping
- Fix players being kicked by Rust's anti-hack
- Prevent players from being slapped through walls
*/

using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Slap", "Wulf/lukespragg", "1.2.0", ResourceId = 1458)]
    [Description("Sometimes players just need to be slapped around a bit")]

    class Slap : CovalencePlugin
    {
        #region Initialization

        readonly Hash<string, float> cooldowns = new Hash<string, float>();
        const string permUse = "slap.use";

        int cooldown;
        int damageAmount;

        protected override void LoadDefaultConfig()
        {
            // Settings
            Config["Cooldown (Seconds, 0 to Disable)"] = cooldown = GetConfig("Cooldown (Seconds, 0 to Disable)", 30);
            Config["Damage Amount (0 to Disable)"] = damageAmount = GetConfig("Damage Amount (0 to Disable)", 5);

            // Cleanup
            Config.Remove("DamageAmount");
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
                ["CommandUsage"] = "Usage: {0} <name or id> <amount> (amount is optional)",
                ["Cooldown"] = "Wait a bit before attempting to slap again", // TODO: Use {0} for slap
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerNotFound"] = "Player '{0}' was not found",
                ["PlayerSlapped"] = "{0} got slapped!",
                ["YouGotSlapped"] = "You got slapped!"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Utilisation : {0} <nom ou id> <amount> (montant est facultatif)",
                ["Cooldown"] = "Attendre un peu avant de tenter de frapper à nouveau",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["PlayerNotFound"] = "Player « {0} » n’a pas été trouvée",
                ["PlayerSlapped"] = "{0} a giflé !",
                ["YouGotSlapped"] = "Vous l’a giflé !"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Verbrauch: {0} <Name oder Id> <amount> (Betrag ist optional)",
                ["Cooldown"] = "Noch ein bisschen warten Sie, bevor Sie versuchen, wieder zu schlagen",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["PlayerNotFound"] = "Player '{0}' wurde nicht gefunden",
                ["PlayerSlapped"] = "{0} hat geschlagen!",
                ["YouGotSlapped"] = "Sie bekam schlug!"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Использование: {0} <имя или id> <amount> (сумма необязательно)",
                ["Cooldown"] = "Подождите немного, прежде чем снова пощечину",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["PlayerNotFound"] = "Игрок «{0}» не найден",
                ["PlayerSlapped"] = "{0} получил ударил!",
                ["YouGotSlapped"] = "Вы получили ударил!"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Uso: {0} <nombre o id> <amount> (la cantidad es opcional)",
                ["Cooldown"] = "Esperar un poco antes de intentar otra vez la palmada",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["PlayerNotFound"] = "Jugador '{0}' no se encontró",
                ["PlayerSlapped"] = "{0} tiene una bofetada!",
                ["YouGotSlapped"] = "Usted consiguió una bofetada!"
            }, this, "es");
        }

        #endregion

        #region Slap Command

        [Command("slap")]
        void SlapCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length == 0)
            {
                player.Reply(Lang("CommandUsage", player.Id, command));
                return;
            }

            if (!cooldowns.ContainsKey(player.Id)) cooldowns.Add(player.Id, 0f);
            if (cooldown != 0 && cooldowns[player.Id] + cooldown > Interface.Oxide.Now)
            {
                player.Reply(Lang("Cooldown", player.Id));
                return;
            }

            var target = players.FindPlayer(args[0]);
            if (target == null)
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0]));
                return;
            }

            timer.Repeat(0.6f, args.Length == 2 ? Convert.ToInt32(args[1]) : 1, () => SlapPlayer(target));
            if (!target.Equals(player)) player.Reply(Lang("PlayerSlapped", player.Id, target.Name));
            target.Message(Lang("YouGotSlapped", target.Id));
            cooldowns[player.Id] = Interface.Oxide.Now;
        }

        #endregion

        #region Slapping

        void SlapPlayer(IPlayer player)
        {
            var r = new System.Random();
            var pos = player.Position();
#if RUST
            var basePlayer = (BasePlayer)player.Object;

            var flinches = new[]
            {
                BaseEntity.Signal.Flinch_Chest,
                BaseEntity.Signal.Flinch_Head,
                BaseEntity.Signal.Flinch_Stomach
            };
            var flinch = flinches[r.Next(flinches.Length)];
            basePlayer.SignalBroadcast(flinch, string.Empty, null);

            var effects = new[]
            {
                "headshot",
                "headshot_2d",
                "impacts/slash/clothflesh/clothflesh1",
                "impacts/stab/clothflesh/clothflesh1"
            };
            var effect = effects[r.Next(effects.Length)];
            Effect.server.Run($"assets/bundled/prefabs/fx/{effect}.prefab", basePlayer.transform.position, UnityEngine.Vector3.zero);
#endif
            if (damageAmount > 0f) player.Hurt(damageAmount);
            player.Teleport(pos.X + r.Next(-3, 3), pos.Y + r.Next(1, 3), pos.Z + r.Next(-3, 3));
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}

