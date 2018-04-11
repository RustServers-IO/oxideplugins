/*
TODO:
- Add daily limit option
- Add AppearWhileRunning option (player.IsRunning())
- Add AppearWhenDamaged option (player.IsWounded())
- Add restoring after reconnection (datafile/static dictionary)
- Fix player becoming visible when switching weapons? (need to verify)
*/

using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Rust;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Vanish", "Wulf/lukespragg (maintained by Jake_Rich)", "0.5.5")]
    [Description("Allows players with permission to become truly invisible")]
    public class Vanish : RustPlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            // TODO: Add config option to not show other vanished for custom group
            // TODO: Add config option to customize effect/sound prefab

            [JsonProperty(PropertyName = "Image URL for vanish icon (.png or .jpg)")]
            public string ImageUrlIcon { get; set; } = "http://i.imgur.com/Gr5G3YI.png";

            [JsonProperty(PropertyName = "Performance mode (true/false)")]
            public bool PerformanceMode { get; set; } = false;

            [JsonProperty(PropertyName = "Play sound effect (true/false)")]
            public bool PlaySoundEffect { get; set; } = true;

            [JsonProperty(PropertyName = "Show visual indicator (true/false)")]
            public bool ShowGuiIcon { get; set; } = true;

            [JsonProperty(PropertyName = "Vanish timeout (seconds, 0 to disable)")]
            public int VanishTimeout { get; set; } = 0;

            [JsonProperty(PropertyName = "Visible to admin (true/false)")]
            public bool VisibleToAdmin { get; set; } = false;

            //[JsonProperty(PropertyName = "Visible to moderators (true/false)")]
            //public bool VisibleToMods { get; set; } = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            PrintWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

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
                ["PlayersOnly"] = "Command '{0}' can only be used by a player",
                ["VanishDisabled"] = "You are no longer invisible!",
                ["VanishEnabled"] = "You have vanished from sight...",
                ["VanishTimedOut"] = "Vanish time limit reached!",
                ["NotAllowedPerm"] = "You are missing permissions! ({0})",
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string defaultEffect = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";
        private const string permAbilitiesInvulnerable = "vanish.abilities.invulnerable";
        private const string permAbilitiesPersistence = "vanish.abilities.persistence";
        private const string permAbilitiesTeleport = "vanish.abilities.teleport";
        private const string permAbilitiesWeapons = "vanish.abilities.weapons";
        private const string permDamageAnimals = "vanish.damage.animals";
        private const string permDamageBuildings = "vanish.damage.buildings";
        private const string permDamagePlayers = "vanish.damage.players";
        private const string permUse = "vanish.use";

        private void Init()
        {
            permission.RegisterPermission(permAbilitiesInvulnerable, this);
            permission.RegisterPermission(permAbilitiesPersistence, this);
            permission.RegisterPermission(permAbilitiesTeleport, this);
            permission.RegisterPermission(permAbilitiesWeapons, this);
            permission.RegisterPermission(permDamageAnimals, this);
            permission.RegisterPermission(permDamageBuildings, this);
            permission.RegisterPermission(permDamagePlayers, this);
            permission.RegisterPermission(permUse, this);

            AddLocalizedCommand("CommandVanish", "VanishCommand");

            if (config.ImageUrlIcon == null)
            {
                config.ImageUrlIcon = "http://i.imgur.com/Gr5G3YI.png";
            }

            Unsubscribe();
        }

        private void Subscribe()
        {
            if (config.PerformanceMode)
            {
                Unsubscribe(nameof(CanNetworkTo));
                Unsubscribe(nameof(OnPlayerTick));
            }
            else
            {
                Subscribe(nameof(CanNetworkTo));
                Subscribe(nameof(OnPlayerTick));
            }

            Subscribe(nameof(CanBeTargeted));
            Subscribe(nameof(CanBradleyApcTarget));
            Subscribe(nameof(OnNpcPlayerTarget));
            Subscribe(nameof(OnNpcTarget));
            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnPlayerSleepEnded));
            Subscribe(nameof(OnPlayerLand));
        }

        private void Unsubscribe()
        {
            Unsubscribe(nameof(CanNetworkTo));
            Unsubscribe(nameof(CanBeTargeted));
            Unsubscribe(nameof(CanBradleyApcTarget));
            Unsubscribe(nameof(OnNpcPlayerTarget));
            Unsubscribe(nameof(OnNpcTarget));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnPlayerSleepEnded));
            Unsubscribe(nameof(OnPlayerLand));
            Unsubscribe(nameof(OnPlayerTick));
        }

        #endregion Initialization

        #region Data Storage

        private class OnlinePlayer
        {
            public BasePlayer Player;
            public bool IsInvisible;
        }

        [OnlinePlayers]
        private Hash<BasePlayer, OnlinePlayer> onlinePlayers = new Hash<BasePlayer, OnlinePlayer>();

        #endregion Data Storage

        #region Commands

        private void VanishCommand(IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
            {
                player.Reply(Lang("PlayersOnly", player.Id, command));
                return;
            }

            if (!player.HasPermission(permUse))
            {
                Message(player, Lang("NotAllowedPerm", player.Id, permUse));
                return;
            }

            // TODO: Add optional command cooldown

            if (config.PlaySoundEffect)
            {
                Effect.server.Run(defaultEffect, basePlayer.transform.position); // TODO: Check if prefab/effect exists
            }

            if (IsInvisible(basePlayer))
            {
                Reappear(basePlayer);
            }
            else
            {
                Disappear(basePlayer);
            }
        }

        #endregion Commands

        #region Vanishing Act

        private void Disappear(BasePlayer basePlayer)
        {
            List<Connection> connections = new List<Connection>();
            foreach (BasePlayer target in BasePlayer.activePlayerList)
            {
                if (basePlayer == target || !target.IsConnected || config.VisibleToAdmin && target.IPlayer.IsAdmin)
                {
                    continue;
                }

                connections.Add(target.net.connection);
            }

            if (config.PerformanceMode)
            {
                basePlayer.limitNetworking = true;
            }

            HeldEntity heldEntity = basePlayer.GetHeldEntity();
            if (heldEntity != null)
            {
                heldEntity.SetHeld(false);
                heldEntity.UpdateVisiblity_Invis();
                heldEntity.SendNetworkUpdate();
            }

            if (Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
                Net.sv.write.EntityID(basePlayer.net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }

            basePlayer.UpdatePlayerCollider(false);

            if (config.ShowGuiIcon)
            {
                VanishGui(basePlayer);
            }

            onlinePlayers[basePlayer].IsInvisible = true;
            Message(basePlayer.IPlayer, "VanishEnabled");

            if (config.VanishTimeout > 0f) timer.Once(config.VanishTimeout, () =>
            {
                if (onlinePlayers[basePlayer].IsInvisible)
                {
                    Reappear(basePlayer);
                    Message(basePlayer.IPlayer, "VanishTimedOut");
                }
            });

            Subscribe();
        }

        // Hide from other players
        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            BasePlayer basePlayer = entity as BasePlayer ?? (entity as HeldEntity)?.GetOwnerPlayer();
            if (basePlayer == null || target == null || basePlayer == target || config.VisibleToAdmin && target.IsAdmin)
            {
                return null;
            }

            if (IsInvisible(basePlayer))
            {
                return false;
            }

            return null;
        }

        // Hide from helis/turrets
        private object CanBeTargeted(BaseCombatEntity entity)
        {
            BasePlayer basePlayer = entity as BasePlayer;
            if (basePlayer != null && IsInvisible(basePlayer))
            {
                return false;
            }

            return null;
        }

        // Hide from the bradley APC
        private object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            BasePlayer basePlayer = entity as BasePlayer;
            if (basePlayer != null && IsInvisible(basePlayer))
            {
                return false;
            }

            return null;
        }

        // Hide from the patrol helicopter
        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer basePlayer)
        {
            if (IsInvisible(basePlayer))
            {
                return false;
            }

            return null;
        }

        // Hide from scientist NPCs
        private object OnNpcPlayerTarget(NPCPlayerApex npc, BaseEntity entity)
        {
            BasePlayer basePlayer = entity as BasePlayer;
            if (basePlayer != null && IsInvisible(basePlayer))
            {
                return false;
            }

            return null;
        }

        // Hide from all other NPCs
        private object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            BasePlayer basePlayer = entity as BasePlayer;
            if (basePlayer != null && IsInvisible(basePlayer))
            {
                return false;
            }

            return null;
        }

        // Disappear when waking up if vanished
        private void OnPlayerSleepEnded(BasePlayer basePlayer)
        {
            if (IsInvisible(basePlayer)) // TODO: Add persistence permission check
            {
                Disappear(basePlayer);
                // TODO: Send message that still vanished
            }
        }

        // Prevent sound on player landing
        private object OnPlayerLand(BasePlayer player, float num)
        {
            if (IsInvisible(player))
            {
                return false;
            }

            return null;
        }

        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (IsInvisible(player))
            {
                // Allow vanished players to open boxes while vanished without noise
                if (permission.UserHasPermission(player.UserIDString, permAbilitiesInvulnerable) || player.IsImmortal())
                {
                    return true;
                }

                // Don't make lock sound if you're not authed while vanished
                CodeLock codeLock = baseLock as CodeLock;
                if (codeLock != null)
                {
                    if (!codeLock.whitelistPlayers.Contains(player.userID) && !codeLock.guestPlayers.Contains(player.userID))
                    {
                        return false;
                    }
                }
            }

            return null;
        }

        #endregion Vanishing Act

        #region Reappearing Act

        private void Reappear(BasePlayer basePlayer)
        {
            onlinePlayers[basePlayer].IsInvisible = false;
            basePlayer.SendNetworkUpdate();
            basePlayer.limitNetworking = false;

            HeldEntity heldEnity = basePlayer.GetHeldEntity();
            if (heldEnity != null)
            {
                heldEnity.UpdateVisibility_Hand();
                heldEnity.SendNetworkUpdate();
            }

            basePlayer.UpdatePlayerCollider(true);

            string gui;
            if (guiInfo.TryGetValue(basePlayer.userID, out gui))
            {
                CuiHelper.DestroyUi(basePlayer, gui);
            }

            Message(basePlayer.IPlayer, "VanishDisabled");
            if (onlinePlayers.Values.Count(p => p.IsInvisible) <= 0)
            {
                Unsubscribe(nameof(CanNetworkTo));
            }
        }

        #endregion Reappearing Act

        #region GUI Indicator

        private Dictionary<ulong, string> guiInfo = new Dictionary<ulong, string>();

        private void VanishGui(BasePlayer basePlayer)
        {
            string gui;
            if (guiInfo.TryGetValue(basePlayer.userID, out gui))
            {
                CuiHelper.DestroyUi(basePlayer, gui);
            }

            CuiElementContainer elements = new CuiElementContainer();
            guiInfo[basePlayer.userID] = CuiHelper.GetGuid();

            elements.Add(new CuiElement
            {
                Name = guiInfo[basePlayer.userID],
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = "1 1 1 0.3", // TODO: Add position config options
                        Url = config.ImageUrlIcon
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.175 0.017", // TODO: Add position config options
                        AnchorMax = "0.22 0.08"
                    }
                }
            });

            CuiHelper.AddUi(basePlayer, elements);
        }

        #endregion GUI Indicator

        #region Damage Blocking

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer basePlayer = info?.Initiator as BasePlayer ?? entity as BasePlayer;
            if (basePlayer == null || !basePlayer.IsConnected || !onlinePlayers[basePlayer].IsInvisible)
            {
                return null;
            }

            IPlayer player = basePlayer.IPlayer;

            // Block damage to animals
            if (entity is BaseNpc)
            {
                if (player.HasPermission(permDamageAnimals))
                {
                    return null;
                }

                Message(player, "CantHurtAnimals");
                return true;
            }

            // Block damage to buildings
            if (!(entity is BasePlayer))
            {
                if (player.HasPermission(permDamageBuildings))
                {
                    return null;
                }

                Message(player, "CantDamageBuilds");
                return true;
            }

            // Block damage to players
            if (info?.Initiator is BasePlayer)
            {
                if (player.HasPermission(permDamagePlayers))
                {
                    return null;
                }

                Message(player, "CantHurtPlayers");
                return true;
            }

            // Block damage to self
            if (basePlayer == info?.HitEntity)
            {
                if (player.HasPermission(permAbilitiesInvulnerable))
                {
                    info.damageTypes = new DamageTypeList();
                    info.HitMaterial = 0;
                    info.PointStart = Vector3.zero;
                    return true;
                }
            }

            return null;
        }

        #endregion Damage Blocking

        #region Weapon Blocking

        private void OnPlayerTick(BasePlayer basePlayer)
        {
            if (onlinePlayers[basePlayer].IsInvisible)
            {
                HeldEntity heldEntity = basePlayer.GetHeldEntity();
                if (heldEntity != null && basePlayer.IPlayer.HasPermission(permAbilitiesWeapons))
                {
                    heldEntity.SetHeld(false);
                }
            }
        }

        #endregion Weapon Blocking

        #region Teleport Blocking

        private object CanTeleport(BasePlayer basePlayer)
        {
            // Ignore for normal teleport plugins
            if (onlinePlayers[basePlayer] == null || !onlinePlayers[basePlayer].IsInvisible)
            {
                return null;
            }

            bool canTeleport = basePlayer.IPlayer.HasPermission(permAbilitiesTeleport);
            return !canTeleport ? Lang("CantUseTeleport", basePlayer.UserIDString) : null;
        }

        #endregion Teleport Blocking

        #region Persistence Handling

        private void OnPlayerInit(BasePlayer basePlayer)
        {
            // TODO: Persistence permission check and handling
        }

        #endregion Persistence Handling

        #region Cleanup

        private void Unload()
        {
            foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
            {
                string gui;
                if (guiInfo.TryGetValue(basePlayer.userID, out gui))
                {
                    CuiHelper.DestroyUi(basePlayer, gui);
                }
            }
        }

        #endregion Cleanup

        #region Helpers

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages.Where(m => m.Key.Equals(key)))
                {
                    if (!string.IsNullOrEmpty(message.Value))
                    {
                        AddCovalenceCommand(message.Value, command);
                    }
                }
            }
        }

        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        private bool IsInvisible(BasePlayer player) => onlinePlayers[player]?.IsInvisible ?? false;

        #endregion Helpers
    }
}
