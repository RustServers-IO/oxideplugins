/*
TODO:
- Add option to hide as a deployed entity of choice
- Add daily limit option
- Add option to not show other vanished for mods?
- Add option to vanish on connection if have permission
- Add option to vanish when sleeping if have permission
- Add AppearWhileRunning option (player.IsRunning())
- Add AppearWhenDamaged option (player.IsWounded())
- Add options for where to position status indicator
- Add restoring after reconnection (datafile/static dictionary)
- Fix CUI overlay overlapping HUD elements/inventory (if possible)
- Fix player becoming visible when switching weapons? (need to verify)
*/

using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Rust;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Vanish", "Wulf/lukespragg", "0.4.1", ResourceId = 1420)]
    [Description("Allows players with permission to become truly invisible")]
    public class Vanish : RustPlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Play sound effect (true/false)")]
            public bool PlaySoundEffect;

            [JsonProperty(PropertyName = "Show visual indicator (true/false)")]
            public bool ShowIndicator;

            [JsonProperty(PropertyName = "Show visual overlay (true/false)")]
            public bool ShowOverlay;

            [JsonProperty(PropertyName = "Vanish timeout (seconds, 0 to disable)")]
            public int VanishTimeout;

            [JsonProperty(PropertyName = "Visible to admin (true/false)")]
            public bool VisibleToAdmin;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    PlaySoundEffect = true,
                    ShowIndicator = true,
                    ShowOverlay = false,
                    VanishTimeout = 0,
                    VisibleToAdmin = false
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.PlaySoundEffect == null) LoadDefaultConfig();
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
                ["CantDamageBuilds"] = "You can't damage buildings while vanished",
                ["CantHurtAnimals"] = "You can't hurt animals while vanished",
                ["CantHurtPlayers"] = "You can't hurt players while vanished",
                ["CantUseTeleport"] = "You can't teleport while vanished",
                ["CommandVanish"] = "vanish",
                ["NotAllowed"] = "Sorry, you can't use '{0}' right now",
                ["VanishDisabled"] = "You are no longer invisible!",
                ["VanishEnabled"] = "You have vanished from sight...",
                ["VanishTimedOut"] = "Vanish timeout reached!"
            }, this);
        }

        #endregion

        #region Initialization

        private const string effectPrefab = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab"; // TODO: Config option
        //private const string permAlwaysHidden = "vanish.alwayshidden";
        private const string permDamageBuilds = "vanish.damagebuilds";
        private const string permInvulnerable = "vanish.invulnerable";
        private const string permHurtAnimals = "vanish.hurtanimals";
        private const string permHurtPlayers = "vanish.hurtplayers";
        private const string permTeleport = "vanish.teleport";
        //private const string permWeapons = "vanish.weapons";
        private const string permUse = "vanish.use";

        private void Init()
        {
            //permission.RegisterPermission(permAlwaysHidden, this);
            permission.RegisterPermission(permDamageBuilds, this);
            permission.RegisterPermission(permInvulnerable, this);
            permission.RegisterPermission(permHurtAnimals, this);
            permission.RegisterPermission(permHurtPlayers, this);
            permission.RegisterPermission(permTeleport, this);
            //permission.RegisterPermission(permWeapons, this);
            permission.RegisterPermission(permUse, this);

            AddCommandAliases("CommandVanish", "VanishCommand");

            Unsubscribe(nameof(CanNetworkTo));
            Unsubscribe(nameof(CanBeTargeted));
            Unsubscribe(nameof(CanBradleyApcTarget));
            Unsubscribe(nameof(OnNpcPlayerTarget));
            Unsubscribe(nameof(OnNpcTarget));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnPlayerSleepEnded));
        }

        #endregion

        #region Data Storage

        private class OnlinePlayer
        {
            public BasePlayer Player;
            public bool IsInvisible;
        }

        [OnlinePlayers]
        private Hash<BasePlayer, OnlinePlayer> onlinePlayers = new Hash<BasePlayer, OnlinePlayer>();
        private void OnPlayerInit() {} // Temporary 'fix' for [OnlinePlayers] and hook 'overloading'

        #endregion

        #region Commands

        private void VanishCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
            {
                // TODO: Only for players message
                return;
            }

            // TODO: Add command cooldown

            if (config.PlaySoundEffect) Effect.server.Run(effectPrefab, basePlayer.transform.position);
            if (IsInvisible(basePlayer)) Reappear(basePlayer);
            else Disappear(basePlayer);
        }

        #endregion

        #region Vanishing Act

        private void Disappear(BasePlayer player)
        {
            var connections = new List<Connection>();
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                if (player == basePlayer || !basePlayer.IsConnected) continue;
                if (config.VisibleToAdmin && IsAdmin(basePlayer)) continue;
                connections.Add(basePlayer.net.connection);
            }

            if (Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
                Net.sv.write.EntityID(player.net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }

            var held = player.GetHeldEntity();
            if (held != null && Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
                Net.sv.write.EntityID(held.net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }

            if (config.ShowOverlay || config.ShowIndicator) VanishGui(player);

            if (config.VanishTimeout > 0f) timer.Once(config.VanishTimeout, () =>
            {
                if (!onlinePlayers[player].IsInvisible) return;

                player.ChatMessage(Lang("VanishTimedOut", player.UserIDString));
                Reappear(player);
            });

            player.ChatMessage(Lang("VanishEnabled", player.UserIDString));
            onlinePlayers[player].IsInvisible = true;

            Subscribe(nameof(CanNetworkTo));
            Subscribe(nameof(CanBeTargeted));
            Subscribe(nameof(CanBradleyApcTarget));
            Subscribe(nameof(OnNpcPlayerTarget));
            Subscribe(nameof(OnNpcTarget));
            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnPlayerSleepEnded));
        }

        // Hide from other players
        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            var player = entity as BasePlayer ?? (entity as HeldEntity)?.GetOwnerPlayer();
            if (player == null || target == null || player == target) return null;
            if (config.VisibleToAdmin && IsAdmin(target)) return null;
            if (IsInvisible(player)) return false;

            return null;
        }

        // Hide from helis/turrets
        private object CanBeTargeted(BaseCombatEntity entity)
        {
            var player = entity as BasePlayer;
            if (player != null && IsInvisible(player)) return false;

            return null;
        }

        // Hide from the bradley APC
        private object CanBradleyApcTarget(BradleyAPC bradleyApc, BaseEntity entity)
        {
            var player = entity as BasePlayer;
            if (player != null && IsInvisible(player)) return false;

            return null;
        }

        // Hide from the patrol helicopter
        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if (IsInvisible(player)) return false;

            return null;
        }

        // Hide from scientist NPCs
        private object OnNpcPlayerTarget(NPCPlayerApex npc, BaseEntity entity)
        {
            var player = entity as BasePlayer;
            if (player != null && IsInvisible(player)) return 0f;

            return null;
        }

        // Hide from all other NPCs
        private object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            var player = entity as BasePlayer;
            if (player != null && IsInvisible(player)) return 0f;

            return null;
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (IsInvisible(player)) Disappear(player);
            // TODO: Notify if still vanished
        }

        #endregion

        #region Reappearing Act

        private void Reappear(BasePlayer player)
        {
            onlinePlayers[player].IsInvisible = false;

            player.SendNetworkUpdate();
            player?.GetHeldEntity()?.SendNetworkUpdate();

            string gui;
            if (guiInfo.TryGetValue(player.userID, out gui)) CuiHelper.DestroyUi(player, gui);

            player.ChatMessage(Lang("VanishDisabled", player.UserIDString));
            if (onlinePlayers.Values.Count(p => p.IsInvisible) <= 0) Unsubscribe(nameof(CanNetworkTo));
        }

        #endregion

        #region GUI Indicator/Overlay

        private Dictionary<ulong, string> guiInfo = new Dictionary<ulong, string>();

        private void VanishGui(BasePlayer player)
        {
            string gui;
            if (guiInfo.TryGetValue(player.userID, out gui)) CuiHelper.DestroyUi(player, gui);

            var elements = new CuiElementContainer();
            guiInfo[player.userID] = CuiHelper.GetGuid();

            if (config.ShowIndicator)
            {
                elements.Add(new CuiElement
                {
                    Name = guiInfo[player.userID],
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 0.3", Url = "http://i.imgur.com/Gr5G3YI.png" }, // TODO: Add config options
                        new CuiRectTransformComponent { AnchorMin = "0.175 0.017",  AnchorMax = "0.22 0.08" } // TODO: Add config options
                    }
                });
            }

            if (config.ShowOverlay)
            {
                elements.Add(new CuiElement
                {
                    Name = guiInfo[player.userID],
                    Components =
                    {
                        new CuiRawImageComponent { Sprite = "assets/content/ui/overlay_freezing.png" }, // TODO: Add config options
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } // TODO: Add config options
                    }
                });
            }

            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region Damage Blocking

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var player = (info?.Initiator as BasePlayer) ?? entity as BasePlayer;
            if (player == null || !player.IsConnected || !onlinePlayers[player].IsInvisible) return null;

            // Block damage to animals
            if (entity is BaseNpc)
            {
                if (!HasPerm(player.UserIDString, permHurtAnimals)) return null;

                player.ChatMessage(Lang("CantHurtAnimals", player.UserIDString));
                return true;
            }

            // Block damage to builds
            if (!(entity is BasePlayer))
            {
                if (!HasPerm(player.UserIDString, permDamageBuilds)) return null;

                player.ChatMessage(Lang("CantDamageBuilds", player.UserIDString));
                return true;
            }

            // Block damage to players
            if (info?.Initiator is BasePlayer)
            {
                if (!HasPerm(player.UserIDString, permHurtPlayers)) return null;

                player.ChatMessage(Lang("CantHurtPlayers", player.UserIDString));
                return true;
            }

            // Block damage to self
            if (HasPerm(player.UserIDString, permInvulnerable))
            {
                info.damageTypes = new DamageTypeList();
                info.HitMaterial = 0;
                info.PointStart = Vector3.zero;
                return true;
            }

            return null;
        }

        #endregion

        #region Weapon Blocking

        /*private void OnPlayerTick(BasePlayer player)
        {
            if (onlinePlayers[player].IsInvisible && player.GetHeldEntity() != null) // TODO: Add permission and check for
            {
                var heldEntity = player.GetHeldEntity() as HeldEntity;
                heldEntity?.SetHeld(false);
            }
        }*/

        #endregion

        #region Teleport Blocking

        private object CanTeleport(BasePlayer player)
        {
            if (onlinePlayers[player] == null) return null;
            return onlinePlayers[player].IsInvisible && !HasPerm(player.UserIDString, permTeleport) ? Lang("CantUseTeleport", player.UserIDString) : null;
        }

        #endregion

        #region Cleanup

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                string gui;
                if (guiInfo.TryGetValue(player.userID, out gui)) CuiHelper.DestroyUi(player, gui);
            }
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

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        private bool IsAdmin(BasePlayer player) => permission.UserHasGroup(player.UserIDString, "admin") || player.net?.connection?.authLevel > 0;

        private bool IsInvisible(BasePlayer player) => onlinePlayers[player]?.IsInvisible ?? false;

        #endregion
    }
}
