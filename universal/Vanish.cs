/*
TODO:
- Add daily limit option
- Add AppearWhileRunning option (player.IsRunning())
- Add AppearWhenDamaged option (player.IsWounded())
- Add restoring after reconnection (datafile/static dictionary)
- Fix player becoming visible when switching weapons? (need to verify)
*/

using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Vanish", "Wulf/lukespragg (maintained by Jake_Rich)", "0.5.1", ResourceId = 1420)]
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
            public string ImageUrlIcon;

            [JsonProperty(PropertyName = "Play sound effect (true/false)")]
            public bool PlaySoundEffect;

            [JsonProperty(PropertyName = "Show visual indicator (true/false)")]
            public bool ShowGuiIcon;

            [JsonProperty(PropertyName = "Vanish timeout (seconds, 0 to disable)")]
            public int VanishTimeout;

            [JsonProperty(PropertyName = "Visible to admin (true/false)")]
            public bool VisibleToAdmin;

            //[JsonProperty(PropertyName = "Visible to moderators (true/false)")]
            //public bool VisibleToMods;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    ImageUrlIcon = "http://i.imgur.com/Gr5G3YI.png",
                    PlaySoundEffect = true,
                    ShowGuiIcon = true,
                    VanishTimeout = 0,
                    VisibleToAdmin = false
                    //VisibleToMods = false
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
                ["PlayersOnly"] = "Command '{0}' can only be used by a player",
                ["VanishDisabled"] = "You are no longer invisible!",
                ["VanishEnabled"] = "You have vanished from sight...",
                ["VanishTimedOut"] = "Vanish time limit reached!",
                ["NotAllowedPerm"] = "You are missing permissions! ({0})",
            }, this);
        }

        #endregion

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

            AddCommandAliases("CommandVanish", "VanishCommand");

            if (config.ImageUrlIcon == null)
            {
                config.ImageUrlIcon = "http://i.imgur.com/Gr5G3YI.png";
            }

            Unsubscribe();
        }

        private void Subscribe()
        {
            Subscribe(nameof(CanNetworkTo));
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

        #endregion

        #region Commands

        private void VanishCommand(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
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

            if (config.PlaySoundEffect) Effect.server.Run(defaultEffect, basePlayer.transform.position); // TODO: Check if prefab/effect exists
            if (IsInvisible(basePlayer)) Reappear(basePlayer);
            else Disappear(basePlayer);
        }

        #endregion

        #region Vanishing Act

        private void Disappear(BasePlayer basePlayer)
        {
            var connections = new List<Connection>();
            foreach (var target in BasePlayer.activePlayerList)
            {
                if (basePlayer == target || !target.IsConnected) continue;
                if (config.VisibleToAdmin && target.IPlayer.IsAdmin) continue;

                connections.Add(target.net.connection);
            }

            var held = basePlayer.GetHeldEntity();
            if (held != null)
            {
                held.SetHeld(false);
                held.UpdateVisiblity_Invis();
                held.SendNetworkUpdate();
            }

            if (Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
                Net.sv.write.EntityID(basePlayer.net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }

            basePlayer.UpdatePlayerCollider(false);

            if (config.ShowGuiIcon) VanishGui(basePlayer);
            onlinePlayers[basePlayer].IsInvisible = true;
            Message(basePlayer.IPlayer, "VanishEnabled");

            if (config.VanishTimeout > 0f) timer.Once(config.VanishTimeout, () =>
            {
                if (!onlinePlayers[basePlayer].IsInvisible) return;

                Reappear(basePlayer);
                Message(basePlayer.IPlayer, "VanishTimedOut");
            });

            Subscribe();

            //Remove player from Grid so animals can't target it (HACKY SOLUTION)
            //Is good for now, as the only thing that uses this grid is AI, so removing it only prevents AI from finding player
            //Player is added back to grid when reappearing
            BaseEntity.Query.Server.RemovePlayer(basePlayer);
            Puts("Removed Player From Animal Grid");
        }

        // Hide from other players
        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            var basePlayer = entity as BasePlayer ?? (entity as HeldEntity)?.GetOwnerPlayer();
            if (basePlayer == null || target == null || basePlayer == target) return null;
            if (config.VisibleToAdmin && target.IPlayer.IsAdmin) return null;
            if (IsInvisible(basePlayer)) return false;

            return null;
        }

        // Hide from helis/turrets
        private object CanBeTargeted(BaseCombatEntity entity)
        {
            var basePlayer = entity as BasePlayer;
            if (basePlayer != null && IsInvisible(basePlayer)) return false;

            return null;
        }

        // Hide from the bradley APC
        private object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            var basePlayer = entity as BasePlayer;
            if (basePlayer != null && IsInvisible(basePlayer)) return false;

            return null;
        }

        // Hide from the patrol helicopter
        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer basePlayer)
        {
            if (IsInvisible(basePlayer)) return false;

            return null;
        }

        // Hide from scientist NPCs
        private object OnNpcPlayerTarget(NPCPlayerApex npc, BaseEntity entity)
        {
            var basePlayer = entity as BasePlayer;
            if (basePlayer != null && IsInvisible(basePlayer)) return 0f;

            return null;
        }

        // Hide from all other NPCs
        private object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            var basePlayer = entity as BasePlayer;
            if (basePlayer != null && IsInvisible(basePlayer)) return 0f;

            return null;
        }

        private void OnPlayerSleepEnded(BasePlayer basePlayer)
        {
            if (IsInvisible(basePlayer)) // TODO: Add persistence permission check
            {
                Disappear(basePlayer);
                // TODO: Send message that still vanished
            }
        }

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
                if (permission.UserHasPermission(player.UserIDString, permAbilitiesInvulnerable) || player.IsImmortal())
                {
                    return true;
                }
            }
            return null;
        }

        #endregion

        #region Reappearing Act

        private void Reappear(BasePlayer basePlayer)
        {
            onlinePlayers[basePlayer].IsInvisible = false;
            basePlayer.SendNetworkUpdate();

            var held = basePlayer.GetHeldEntity();
            if (held != null)
            {
                held.UpdateVisibility_Hand();
                held.SendNetworkUpdate();
            }

            basePlayer.UpdatePlayerCollider(true);

            string gui;
            if (guiInfo.TryGetValue(basePlayer.userID, out gui)) CuiHelper.DestroyUi(basePlayer, gui);
            Puts("Added Player From Animal Grid");
            //Add player back to Grid so AI can find it
            BaseEntity.Query.Server.AddPlayer(basePlayer);

            Message(basePlayer.IPlayer, "VanishDisabled");
            if (onlinePlayers.Values.Count(p => p.IsInvisible) <= 0) Unsubscribe(nameof(CanNetworkTo));
        }

        #endregion

        #region GUI Indicator

        private Dictionary<ulong, string> guiInfo = new Dictionary<ulong, string>();

        private void VanishGui(BasePlayer basePlayer)
        {
            string gui;
            if (guiInfo.TryGetValue(basePlayer.userID, out gui)) CuiHelper.DestroyUi(basePlayer, gui);

            var elements = new CuiElementContainer();
            guiInfo[basePlayer.userID] = CuiHelper.GetGuid();

            elements.Add(new CuiElement
            {
                Name = guiInfo[basePlayer.userID],
                Components =
                {
                    new CuiRawImageComponent { Color = "1 1 1 0.3", Url = config.ImageUrlIcon }, // TODO: Add position config options
                    new CuiRectTransformComponent { AnchorMin = "0.175 0.017",  AnchorMax = "0.22 0.08" } // TODO: Add position config options
                }
            });

            CuiHelper.AddUi(basePlayer, elements);
        }

        #endregion

        #region Damage Blocking

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var basePlayer = (info?.Initiator as BasePlayer) ?? entity as BasePlayer;
            if (basePlayer == null || !basePlayer.IsConnected || !onlinePlayers[basePlayer].IsInvisible) return null;

            var player = basePlayer.IPlayer;

            // Block damage to animals
            if (entity is BaseNpc)
            {
                if (player.HasPermission(permDamageAnimals)) return null;

                Message(player, "CantHurtAnimals");
                return true;
            }

            // Block damage to buildings
            if (!(entity is BasePlayer))
            {
                if (player.HasPermission(permDamageBuildings)) return null;

                Message(player, "CantDamageBuilds");
                return true;
            }

            // Block damage to players
            if (info?.Initiator is BasePlayer)
            {
                if (player.HasPermission(permDamagePlayers)) return null;

                Message(player, "CantHurtPlayers");
                return true;
            }

            if (basePlayer == info.HitEntity)
            {
                // Block damage to self
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

        #endregion

        #region Weapon Blocking

        private void OnPlayerTick(BasePlayer basePlayer)
        {
            if (!onlinePlayers[basePlayer].IsInvisible) return;

            var held = basePlayer.GetHeldEntity();
            if (held != null && basePlayer.IPlayer.HasPermission(permAbilitiesWeapons)) held.SetHeld(false);
        }

        #endregion

        #region Teleport Blocking

        private object CanTeleport(BasePlayer basePlayer)
        {
            if (onlinePlayers[basePlayer] == null)
            {
                return null;
            }

            //Ignore for normal teleport plugins
            if (!onlinePlayers[basePlayer].IsInvisible)
            {
                return null;
            }

            var canTeleport = basePlayer.IPlayer.HasPermission(permAbilitiesTeleport);
            return !canTeleport ? Lang("CantUseTeleport", basePlayer.UserIDString) : null;
        }

        #endregion

        #region Persistence Handling

        private void OnPlayerInit(BasePlayer basePlayer)
        {
            // TODO: Persistence permission check and handling
        }

        #endregion

        #region Cleanup

        private void Unload()
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                string gui;
                if (guiInfo.TryGetValue(basePlayer.userID, out gui)) CuiHelper.DestroyUi(basePlayer, gui);
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

        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        private bool IsInvisible(BasePlayer player) => onlinePlayers[player]?.IsInvisible ?? false;

        #endregion
    }
}
