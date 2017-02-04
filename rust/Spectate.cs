/*
 * TODO:
 * Fix weapons floating when spectating
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Spectate", "Wulf/lukespragg", "0.4.2", ResourceId = 1426)]
    [Description("Allows only players with permission to spectate")]

    class Spectate : CovalencePlugin
    {
        #region Initialization

        readonly MethodInfo entitySnapshot = typeof(BasePlayer).GetMethod("SendEntitySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly Dictionary<string, Vector3> lastPositions = new Dictionary<string, Vector3>();
        readonly Dictionary<string, string> spectating = new Dictionary<string, string>();

        const string permUse = "spectate.use";

        void Init()
        {
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
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NoValidTargets"] = "No valid spectate targets",
                ["PlayersOnly"] = "Command '{0}' can only be used by a player",
                ["SpectateSelf"] = "You cannot spectate yourself",
                ["SpectateStart"] = "Started spectating {0}",
                ["SpectateStop"] = "Stopped spectating {0}",
                ["TargetIsSpectating"] = "{0} is currently spectating another player"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["NoValidTargets"] = "Non valides spectate cibles",
                ["PlayersOnly"] = "Commande « {0} » seulement peut être utilisée que par un joueur",
                ["SpectateSelf"] = "Vous ne pouvez pas vous-même spectate",
                ["SpectateStart"] = "Commencé spectature {0}",
                ["SpectateStop"] = "Cessé de spectature {0}",
                ["TargetIsSpectating"] = "{0} est spectature actuellement un autre joueur"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["NoValidTargets"] = "Zuschauen Sie keine gültige Ziele",
                ["PlayersOnly"] = "Befehl '{0}' kann nur von einem Spieler verwendet werden",
                ["SpectateSelf"] = "Sie können nicht selbst als Zuschauer",
                ["SpectateStart"] = "Begann zuschauen {0}",
                ["SpectateStop"] = "Nicht mehr zuschauen {0}",
                ["TargetIsSpectating"] = "{0} ist derzeit ein anderer Spieler zuschauen"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["NoValidTargets"] = "Нет допустимых spectate целей",
                ["PlayersOnly"] = "Команда «{0}» может использоваться только игрок",
                ["SpectateSelf"] = "Вы не можете spectate себя",
                ["SpectateStart"] = "Начал spectating {0}",
                ["SpectateStop"] = "Остановлен spectating {0}",
                ["TargetIsSpectating"] = "{0} в настоящее время spectating другой игрок"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["NoValidTargets"] = "No válido espectador objetivos",
                ["PlayersOnly"] = "Comando '{0}' solo puede ser usado por un jugador",
                ["SpectateSelf"] = "Usted no puede sí mismo espectador",
                ["SpectateStart"] = "Comenzó a observar {0}",
                ["SpectateStop"] = "Dejado de ver {0}",
                ["TargetIsSpectating"] = "{0} está actualmente tenemos otro jugador"
            }, this, "es");
        }

        #endregion

        #region Chat Command

        [Command("spectate")]
        void SpectateCommand(IPlayer player, string command, string[] args)
        {
            if (player.Id == "server_console")
            {
                player.Reply(Lang("PlayersOnly", player.Id, command));
                return;
            }

            if (!player.HasPermission(permUse))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) return;

            if (!basePlayer.IsSpectating())
            {
                var target = BasePlayer.Find(string.Join(" ", args.Select(v => v.ToString()).ToArray()));
                if (target == null || target.IsDead())
                {
                    player.Reply(Lang("NoValidTargets", player.Id));
                    return;
                }

                if (ReferenceEquals(target, basePlayer))
                {
                    player.Reply(Lang("SpectateSelf", player.Id));
                    return;
                }

                if (target.IsSpectating())
                {
                    player.Reply(Lang("TargetIsSpectating", player.Id, target.displayName));
                    return;
                }

                // Store current location before spectating
                lastPositions.Add(player.Id, basePlayer.transform.position);

                // Prep player for spectate mode
                var heldEntity = basePlayer.GetActiveItem()?.GetHeldEntity() as HeldEntity;
                heldEntity?.SetHeld(false);

                // Put player in spectate mode
                basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
                basePlayer.gameObject.SetLayerRecursive(10);
                basePlayer.CancelInvoke("MetabolismUpdate");
                basePlayer.CancelInvoke("InventoryUpdate");
                basePlayer.ClearEntityQueue();
                entitySnapshot.Invoke(basePlayer, new object[] { target });
                basePlayer.gameObject.Identity();
                basePlayer.SetParent(target);
                basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
                player.Command("camoffset 0,1.3,0");

                // Notify player and store target name
                player.Reply(Lang("SpectateStart", player.Id, target.displayName));
                spectating.Add(player.Id, target.displayName);
            }
            else
            {
                // Restore player to normal mode
                player.Command("camoffset", "0,1,0");
                basePlayer.SetParent(null);
                basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
                basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
                basePlayer.gameObject.SetLayerRecursive(17);
                basePlayer.metabolism.Reset();
                basePlayer.InvokeRepeating("InventoryUpdate", 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));

                // Restore player to previous state
                basePlayer.StartSleeping();
                var heldEntity = basePlayer.GetActiveItem()?.GetHeldEntity() as HeldEntity;
                heldEntity?.SetHeld(true);

                // Teleport to original location after spectating
                if (lastPositions.ContainsKey(player.Id))
                {
                    var lastPosition = lastPositions[player.Id];
                    player.Teleport(lastPosition.x, lastPosition.y, lastPosition.z);
                    lastPositions.Remove(player.Id);
                }

                // Notify player and clear target name
                player.Reply(Lang("SpectateStop", player.Id, spectating[player.Id] ?? "?"));
                if (spectating.ContainsKey(player.Id)) spectating.Remove(player.Id);
            }
        }

        #endregion

        #region Game Hooks

        void OnUserConnected(IPlayer player)
        {
            if (!spectating.ContainsKey(player.Id)) return;
            player.Command("camoffset 0,1,0");
            spectating.Remove(player.Id);
        }

        void OnUserDisconnected(IPlayer player)
        {
            if (!spectating.ContainsKey(player.Id)) return;
            player.Command("camoffset 0,1,0");
            spectating.Remove(player.Id);
        }

        #endregion

        #region Helper Methods

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
