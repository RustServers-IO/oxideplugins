using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using System.Reflection;
using System.Linq;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Plugins;
using Oxide.Core.Libraries;
using Newtonsoft.Json;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("HooksExtended", "Calytic @ RustServers.IO", "0.0.11", ResourceId = 2239)]
    public class HooksExtended : RustPlugin
    {
        #region Variables
        private List<uint> WoodMaterial = new List<uint>() {
            3655341
        };

        private List<uint> RockMaterial = new List<uint>() {
            3506021,
            3712324229
        };

        private List<uint> MetalMaterial = new List<uint>()
        {
            103787271,
            4214819287
        };

        private List<uint> SnowMaterial = new List<uint>()
        {
            3535235
        };

        private List<uint> GrassMaterial = new List<uint>() {
            98615734
        };

        private List<uint> SandMaterial = new List<uint>() {
            3522692
        };

        private Vector3 eyesPosition = new Vector3(0f, 0.5f, 0f);
        private static readonly FieldInfo modelStateField = typeof(BasePlayer).GetField("modelState", BindingFlags.Instance | BindingFlags.NonPublic);
        private static int playerLayer = LayerMask.GetMask("Player (Server)");
        private static int useLayer = LayerMask.GetMask(new string[] { "Player (Server)", "Construction", "Deployed", "Tree", "Resource", "Terrain" });
        private readonly FieldInfo serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        private List<BasePlayer> inputCooldown = new List<BasePlayer>();
        private Dictionary<BasePlayer, List<MonoBehaviour>> spotCooldown = new Dictionary<BasePlayer, List<MonoBehaviour>>();
        int spottingMask = LayerMask.GetMask(new string[] { "Player (Server)", "AI" });
        public PluginSettings settings;

        [OnlinePlayers]
        Hash<BasePlayer, PlayerProfile> onlinePlayers = new Hash<BasePlayer, PlayerProfile>();
        #endregion

        #region Boilerplate

        class PlayerProfile
        {
            public BasePlayer Player;
            public ProfileMetabolism Metabolism;

            private ModelState _modelState;

            public ModelState modelState
            {
                get
                {
                    if (_modelState is ModelState)
                    {
                        return _modelState;
                    }

                    return _modelState = (ModelState)modelStateField.GetValue(this.Player);
                }
            }

            public bool wasDucked;
            public bool wasDrowning;
            public bool wasSprinting;
            public Item activeItem;
        }

        class ProfileMetabolism
        {
            public enum MetaAction
            {
                Start,
                Stop
            }

            float wetness;
            float radiation_poison;
            float radiation_level;
            float poison;
            float comfort;
            float bleeding;
            float oxygen;

            public ProfileMetabolism(PlayerMetabolism metabolism)
            {
                this.Set(metabolism);
            }

            private void Set(PlayerMetabolism metabolism)
            {
                this.wetness = metabolism.wetness.value;
                this.radiation_poison = metabolism.radiation_poison.value;
                this.radiation_level = metabolism.radiation_level.value;
                this.poison = metabolism.poison.value;
                this.comfort = metabolism.comfort.value;
                this.bleeding = metabolism.bleeding.value;
            }

            public Dictionary<string, MetaAction> DetectChange(PlayerMetabolism metabolism)
            {
                Dictionary<string, MetaAction> actions = new Dictionary<string, MetaAction>();

                if (metabolism.wetness.value != wetness)
                {
                    if (metabolism.wetness.value == metabolism.wetness.min)
                    {
                        actions.Add("Wetness", MetaAction.Stop);
                    }
                    else if (wetness == metabolism.wetness.min)
                    {
                        actions.Add("Wetness", MetaAction.Start);
                    }
                }

                if (metabolism.poison.value != poison)
                {
                    if (metabolism.poison.value == metabolism.poison.min)
                    {
                        actions.Add("Poison", MetaAction.Stop);
                    }
                    else if (poison == metabolism.poison.min)
                    {
                        actions.Add("Poison", MetaAction.Start);
                    }
                }

                if (metabolism.oxygen.value != oxygen)
                {
                    if (metabolism.oxygen.value == metabolism.oxygen.min)
                    {
                        actions.Add("Drowning", MetaAction.Stop);
                    }
                    else if (oxygen == metabolism.oxygen.min)
                    {
                        actions.Add("Drowning", MetaAction.Start);
                    }
                }

                if (metabolism.radiation_level.value != radiation_level)
                {
                    if (metabolism.radiation_level.value == metabolism.radiation_level.min)
                    {
                        actions.Add("Radiation", MetaAction.Stop);
                    }
                    else if (radiation_level == metabolism.radiation_level.min)
                    {
                        actions.Add("Radiation", MetaAction.Start);
                    }
                }

                if (metabolism.radiation_poison.value != radiation_poison)
                {
                    if (metabolism.radiation_poison.value == metabolism.radiation_poison.min)
                    {
                        actions.Add("RadiationPoison", MetaAction.Stop);
                    }
                    else if (radiation_poison == metabolism.radiation_poison.min)
                    {
                        actions.Add("RadiationPoison", MetaAction.Start);
                    }
                }

                if (metabolism.comfort.value != comfort)
                {
                    if (metabolism.comfort.value == metabolism.comfort.min)
                    {
                        actions.Add("Comfort", MetaAction.Stop);
                    }
                    else if (comfort == metabolism.comfort.min)
                    {
                        actions.Add("Comfort", MetaAction.Start);
                    }
                }

                if (metabolism.bleeding.value != bleeding)
                {
                    if (metabolism.bleeding.value == metabolism.bleeding.min)
                    {
                        actions.Add("Bleeding", MetaAction.Stop);
                    }
                    else if (bleeding == metabolism.bleeding.min)
                    {
                        actions.Add("Bleeding", MetaAction.Start);
                    }
                }

                this.Set(metabolism);

                return actions;
            }
        }

        public class PluginSettings
        {
            public HookSettings HookSettings;
            public string VERSION;
        }

        public class HookSettings
        {
            public bool OnPlayerTick = false;
            public bool OnPlayerAttack = false;
            public bool OnRunPlayerMetabolism = false;
            public bool OnItemDeployed = false;
            public bool OnEntityTakeDamage = false;
            public bool OnConsumeFuel = false;
            public bool OnEntityDeath = false;
            public bool OnItemAddedToContainer = false;
            public bool OnItemRemovedFromContainer = false;
            public bool OnPlayerInput = false;
            public bool OnItemCraft = false;
            public bool OnItemCraftCancelled = false;
            public bool OnItemCraftFinished = false;
            public bool OnEntitySpawned = false;
        }

        #endregion

        #region Initialization & Configuration

        void Init()
        {
            UnsubscribeAll();
        }

        void OnServerInitialized()
        {
            if (settings == null)
            {
                LoadConfigValues();
            }

            SubscribeHooks();
        }

        void UnsubscribeAll()
        {
            Unsubscribe(nameof(OnPlayerTick));
            Unsubscribe(nameof(OnPlayerAttack));
            Unsubscribe(nameof(OnRunPlayerMetabolism));
            Unsubscribe(nameof(OnItemDeployed));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnConsumeFuel));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnItemAddedToContainer));
            Unsubscribe(nameof(OnItemRemovedFromContainer));
            Unsubscribe(nameof(OnPlayerInput));
            Unsubscribe(nameof(OnItemCraft));
            Unsubscribe(nameof(OnItemCraftCancelled));
            Unsubscribe(nameof(OnItemCraftFinished));
            Unsubscribe(nameof(OnEntitySpawned));
        }

        void SubscribeHooks()
        {
            if (settings == null)
            {
                PrintError("Settings invalid");
                return;
            }

            if (settings.HookSettings == null)
            {
                PrintError("Hook Settings invalid");
                return;
            }

            if (settings.HookSettings.OnPlayerTick) Subscribe(nameof(OnPlayerTick));
            if (settings.HookSettings.OnPlayerAttack) Subscribe(nameof(OnPlayerAttack));
            if (settings.HookSettings.OnRunPlayerMetabolism) Subscribe(nameof(OnRunPlayerMetabolism));
            if (settings.HookSettings.OnItemDeployed) Subscribe(nameof(OnItemDeployed));
            if (settings.HookSettings.OnEntityTakeDamage) Subscribe(nameof(OnEntityTakeDamage));
            if (settings.HookSettings.OnConsumeFuel) Subscribe(nameof(OnConsumeFuel));
            if (settings.HookSettings.OnEntityDeath) Subscribe(nameof(OnEntityDeath));
            if (settings.HookSettings.OnItemAddedToContainer) Subscribe(nameof(OnItemAddedToContainer));
            if (settings.HookSettings.OnItemRemovedFromContainer) Subscribe(nameof(OnItemRemovedFromContainer));
            if (settings.HookSettings.OnPlayerInput) Subscribe(nameof(OnPlayerInput));
            if (settings.HookSettings.OnItemCraft) Subscribe(nameof(OnItemCraft));
            if (settings.HookSettings.OnItemCraftCancelled) Subscribe(nameof(OnItemCraftCancelled));
            if (settings.HookSettings.OnItemCraftFinished) Subscribe(nameof(OnItemCraftFinished));
            if (settings.HookSettings.OnEntitySpawned) Subscribe(nameof(OnEntitySpawned));
        }

        void EnableHook(string hookName, bool save = true)
        {
            ConfigureHook(hookName, true, save);
        }

        void EnableHooks(params string[] hookNames)
        {
            foreach (string hookName in hookNames)
            {
                EnableHook(hookName, false);
            }

            SaveSettings();
            UnsubscribeAll();
            SubscribeHooks();
        }

        void DisableHook(string hookName, bool save = true)
        {
            ConfigureHook(hookName, false, save);
        }

        void DisableHooks(params string[] hookNames)
        {
            foreach (string hookName in hookNames)
            {
                DisableHook(hookName, false);
            }

            SaveSettings();
            UnsubscribeAll();
            SubscribeHooks();
        }

        void ConfigureHook(string hookName, bool setting, bool save = true)
        {
            switch (hookName)
            {
                case "OnPlayerTick":
                    settings.HookSettings.OnPlayerTick = setting;
                    break;
                case "OnPlayerAttack":
                    settings.HookSettings.OnPlayerAttack = setting;
                    break;
                case "OnRunPlayerMetabolism":
                    settings.HookSettings.OnRunPlayerMetabolism = setting;
                    break;
                case "OnItemDeployed":
                    settings.HookSettings.OnItemDeployed = setting;
                    break;
                case "OnEntityTakeDamage":
                    settings.HookSettings.OnEntityTakeDamage = setting;
                    break;
                case "OnConsumeFuel":
                    settings.HookSettings.OnConsumeFuel = setting;
                    break;
                case "OnEntityDeath":
                    settings.HookSettings.OnEntityDeath = setting;
                    break;
                case "OnItemAddedToContainer":
                    settings.HookSettings.OnItemAddedToContainer = setting;
                    break;
                case "OnItemRemovedFromContainer":
                    settings.HookSettings.OnItemRemovedFromContainer = setting;
                    break;
                case "OnPlayerInput":
                    settings.HookSettings.OnPlayerInput = setting;
                    break;
                case "OnItemCraft":
                    settings.HookSettings.OnItemCraft = setting;
                    break;
                case "OnItemCraftCancelled":
                    settings.HookSettings.OnItemCraftCancelled = setting;
                    break;
                case "OnItemCraftFinished":
                    settings.HookSettings.OnItemCraftFinished = setting;
                    break;
                case "OnEntitySpawned":
                    settings.HookSettings.OnEntitySpawned = setting;
                    break;
            }

            if (save)
            {
                SaveSettings();
                UnsubscribeAll();
                SubscribeHooks();
            }
        }

        protected override void LoadDefaultConfig()
        {
            settings = new PluginSettings()
            {
                HookSettings = new HookSettings(),
                VERSION = Version.ToString()
            };
            SaveSettings();
        }

        protected void SaveSettings()
        {
            Config.WriteObject(settings, true);
        }

        void LoadConfigValues()
        {
            settings = Config.ReadObject<PluginSettings>();
        }

        #endregion

        #region Extended Hooks
        /// <summary>
        /// ON CATEGORY CRAFT
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        private object OnItemCraft(ItemCraftTask task)
        {
            string category = task.blueprint.targetItem.category.ToString();

            return Interface.Oxide.CallHook("On" + category + "Craft", task);
        }

        /// <summary>
        /// ON CATEGORY CRAFT CANCELLED
        /// </summary>
        /// <param name="task"></param>
        private void OnItemCraftCancelled(ItemCraftTask task)
        {
            string category = task.blueprint.targetItem.category.ToString();

            Interface.Oxide.CallHook("On" + category + "CraftCancelled", task);
        }

        /// <summary>
        /// ON CATEGORY CRAFT FINISHED
        /// </summary>
        /// <param name="task"></param>
        /// <param name="item"></param>
        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            string category = item.info.category.ToString();

            Interface.Oxide.CallHook("On" + category + "CraftFinished", task, item);
        }

        /// <summary>
        /// ON ACTIVATE
        /// ON DEACTIVATE
        /// ON DUCK
        /// ON STAND
        /// ON BEGIN SPRINT
        /// ON END SPRINT
        /// </summary>
        /// <param name="player"></param>
        private void OnPlayerTick(BasePlayer player)
        {
            Item item = onlinePlayers[player].Player.GetActiveItem();
            if (item != null && item != onlinePlayers[player].activeItem)
            {
                Interface.CallHook("OnItemActivate", player, item);
                onlinePlayers[player].activeItem = item;
            }
            else if (item == null)
            {
                if (onlinePlayers[player].activeItem != null)
                {
                    Interface.CallHook("OnItemDeactivate", player, item);
                }
                onlinePlayers[player].activeItem = item;
            }

            if (onlinePlayers[player].modelState.ducked)
            {
                if (!onlinePlayers[player].wasDucked)
                {
                    onlinePlayers[player].wasDucked = true;
                    Interface.CallHook("OnPlayerDuck", player);
                }
            }
            else
            {
                if (onlinePlayers[player].wasDucked)
                {
                    onlinePlayers[player].wasDucked = false;
                    Interface.CallHook("OnPlayerStand", player);
                }
            }

            if (onlinePlayers[player].modelState.sprinting)
            {
                if (!onlinePlayers[player].wasSprinting)
                {
                    onlinePlayers[player].wasSprinting = true;
                    Interface.CallHook("OnStartSprint", player);
                }
            }
            else
            {
                if (onlinePlayers[player].wasSprinting)
                {
                    onlinePlayers[player].wasSprinting = false;
                    Interface.CallHook("OnStopSprint", player);
                }
            }
        }

        /// <summary>
        /// ON HIT RESOURCE
        /// ON HIT WOOD
        /// ON HIT ROCK
        /// </summary>
        /// <param name="attacker"></param>
        /// <param name="info"></param>
        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info.Weapon == null)
            {
                return;
            }

            if (info.HitEntity != null)
            {
                var resourceDispenser = info.HitEntity.GetComponentInParent<ResourceDispenser>();
                if (resourceDispenser != null)
                {
                    Interface.CallHook("OnHitResource", attacker, info);
                    return;
                }

                if (info.HitEntity.name.Contains("junkpile"))
                {
                    Interface.CallHook("OnHitJunk", attacker, info);
                    return;
                }
            }
            if (WoodMaterial.Contains(info.HitMaterial))
            {
                Interface.CallHook("OnHitWood", attacker, info);
            }
            else if (RockMaterial.Contains(info.HitMaterial))
            {
                Interface.CallHook("OnHitRock", attacker, info);
            }
            else if (MetalMaterial.Contains(info.HitMaterial))
            {
                Interface.CallHook("OnHitMetal", attacker, info);
            }
            else if (SnowMaterial.Contains(info.HitMaterial))
            {
                Interface.CallHook("OnHitSnow", attacker, info);
            }
            else if (GrassMaterial.Contains(info.HitMaterial))
            {
                Interface.CallHook("OnHitGrass", attacker, info);
            }
            else if (SandMaterial.Contains(info.HitMaterial))
            {
                Interface.CallHook("OnHitSand", attacker, info);
            }
        }

        /// <summary>
        /// ON START WETNESS
        /// ON STOP WETNESS
        /// ON START POISON
        /// ON STOP POISON
        /// ON START RADIATION
        /// ON STOP RADIATION
        /// ON START RADIATION POISON
        /// ON STOP RADIATION POISON
        /// ON START COMFORT
        /// ON STOP COMFORT
        /// ON START BLEEDING
        /// ON STOP BLEEDING
        /// </summary>
        /// <param name="metabolism"></param>
        /// <param name="source"></param>
        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity source)
        {
            if (source is BasePlayer)
            {
                BasePlayer player = (BasePlayer)source;
                PlayerProfile profile;
                if (onlinePlayers.TryGetValue(player, out profile)) {
                    if (profile.Metabolism == null)
                    {
                        profile.Metabolism = new ProfileMetabolism(metabolism);
                        return;
                    }

                    Dictionary<string, ProfileMetabolism.MetaAction> changes = profile.Metabolism.DetectChange(metabolism);

                    foreach(KeyValuePair<string, ProfileMetabolism.MetaAction> kvp in changes) {
                        if (kvp.Value == ProfileMetabolism.MetaAction.Start)
                        {
                            Interface.CallHook("OnStart" + kvp.Key, player);
                        }
                        else
                        {
                            Interface.CallHook("OnStop" + kvp.Key, player);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// ON CUPBOARD DEPLOYED
        /// ON TURRET DEPLOYED
        /// ON DOOR DEPLOYED
        /// ON SLEEPING BAG DEPLOYED
        /// ON STOCKING DEPLOYED
        /// ON BARRICADE DEPLOYED
        /// ON CONTAINER DEPLOYED
        /// ON SIGN DEPLOYED
        /// ON FURNACE DEPLOYED
        /// ON CAMPFIRE DEPLOYED
        /// ON LIGHT DEPLOYED
        /// </summary>
        /// <param name="deployer"></param>
        /// <param name="entity"></param>
        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            BasePlayer player = deployer.GetOwnerPlayer();
            if (entity is BuildingPrivlidge)
            {
                Interface.Oxide.CallHook("OnCupboardDeployed", player, deployer, entity);
            }
            else if (entity is AutoTurret)
            {
                Interface.Oxide.CallHook("OnTurretDeployed", player, deployer, entity);
            }
            else if (entity is Door)
            {
                Interface.Oxide.CallHook("OnDoorDeployed", player, deployer, entity);
            }
            else if (entity is SleepingBag)
            {
                Interface.Oxide.CallHook("OnSleepingBagDeployed", player, deployer, entity);
            }
            else if (entity is Stocking)
            {
                Interface.Oxide.CallHook("OnStockingDeployed", player, deployer, entity);
            }
            else if (entity is Barricade)
            {
                Interface.Oxide.CallHook("OnBarricadeDeployed", player, deployer, entity);
            }
            else if (entity is StorageContainer)
            {
                Interface.Oxide.CallHook("OnContainerDeployed", player, deployer, entity);
            }
            else if (entity is Signage)
            {
                Interface.Oxide.CallHook("OnSignDeployed", player, deployer, entity);
            }
            else if (entity is BaseOven)
            {
                if (entity.name.Contains("furnace"))
                {
                    Interface.Oxide.CallHook("OnFurnaceDeployed", player, deployer, entity);
                }
                else if (entity.name.Contains("campfire"))
                {
                    Interface.Oxide.CallHook("OnCampfireDeployed", player, deployer, entity);
                }
                else if (entity is CeilingLight || entity.name.Contains("lantern"))
                {
                    Interface.Oxide.CallHook("OnLightDeployed", player, deployer, entity);
                }
            }
        }

        /// <summary>
        /// ON ANIMAL ATTACK
        /// ON HELICOPTER ATTACK
        /// ON STRUCTURE DAMAGE
        /// ON PLAYER DAMAGE
        /// ON TURRET DAMAGE
        /// ON HELICOPTER DAMAGE
        /// ON CUPBOARD DAMAGE
        /// ON CORPSE DAMAGE
        /// ON SLEEPING BAG DAMAGE
        /// ON NPC DAMAGE
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="info"></param>
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info.Initiator != null && info.Initiator is BaseNPC)
            {
                Interface.CallHook("OnAnimalAttack", entity, (BaseNPC)info.Initiator, info);
            }

            if (info.Initiator != null && info.Initiator.name.Contains("patrolhelicopter.prefab") && !info.Initiator.name.Contains("gibs"))
            {
                Interface.CallHook("OnHelicopterAttack", entity, info.Initiator, info);
            }

            if (entity is BuildingBlock)
            {
                Interface.CallHook("OnStructureDamage", (BuildingBlock)entity, info);
            }
            else if (entity is BasePlayer)
            {
                Interface.CallHook("OnPlayerDamage", (BasePlayer)entity, info);
            }
            else if (entity is AutoTurret)
            {
                Interface.CallHook("OnTurretDamage", (AutoTurret)entity, info);
            }
            else if (entity is BaseHelicopter)
            {
                Interface.CallHook("OnHelicopterDamage", (BaseHelicopter)entity, info);
            }
            else if (entity is BuildingPrivlidge)
            {
                Interface.CallHook("OnCupboardDamage", (BuildingPrivlidge)entity, info);
            }
            else if (entity is BaseCorpse)
            {
                Interface.CallHook("OnCorpseDamage", (BaseCorpse)entity, info);
            }
            else if (entity is SleepingBag)
            {
                Interface.CallHook("OnSleepingBagDamage", (SleepingBag)entity, info);
            }
            else if (entity is BaseNPC)
            {
                Interface.CallHook("OnNPCDamage", (BaseNPC)entity, info);
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is SupplyDrop)
            {
                Interface.Oxide.CallHook("OnSupplyDropSpawned", entity);
            }
            else if (entity is BaseHelicopter)
            {
                Interface.Oxide.CallHook("OnHelicopterSpawned", entity);
            }
            else if (entity is HelicopterDebris)
            {
                Interface.Oxide.CallHook("OnHelicopterDebrisSpawned", entity);
            }
            else if (entity is BaseNPC)
            {
                Interface.Oxide.CallHook("OnNPCSpawned", entity);
            }
            else if (entity is LootContainer)
            {
                Interface.Oxide.CallHook("OnLootContainerSpawned", entity);
            }
            else if (entity is BuildingPrivlidge)
            {
                Interface.Oxide.CallHook("OnCupboardSpawned", entity);
            }
            else if (entity is AutoTurret)
            {
                Interface.Oxide.CallHook("OnTurretSpawned", entity);
            }
        }

        /// <summary>
        /// ON COOK FURNACE
        /// ON COOK FIRE
        /// ON FUEL LIGHT
        /// </summary>
        /// <param name="oven"></param>
        /// <param name="fuel"></param>
        /// <param name="burnable"></param>
        private void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (oven.name.Contains("furnace"))
            {
                Interface.CallHook("OnCookFurnace", oven, fuel, burnable);
            }
            else if (oven.name.Contains("campfire"))
            {
                Interface.CallHook("OnCookFire", oven, fuel, burnable);
            }
            else if (oven.name.Contains("light") || oven.name.Contains("lantern"))
            {
                Interface.CallHook("OnFuelLight", oven, fuel, burnable);
            }
        }

        /// <summary>
        /// ON STRUCTURE DEATH
        /// ON PLAYER DEATH
        /// ON TURRET DEATH
        /// ON HELICOPTER DEATH
        /// ON CUPBOARD DEATH
        /// ON SLEEPING BAG DEATH
        /// ON NPC DEATH
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="info"></param>
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BuildingBlock)
            {
                Interface.CallHook("OnStructureDeath", (BuildingBlock)entity, info);
            }
            else if (entity is BasePlayer)
            {
                Interface.CallHook("OnPlayerDeath", (BasePlayer)entity, info);
            }
            else if (entity is AutoTurret)
            {
                Interface.CallHook("OnTurretDeath", (AutoTurret)entity, info);
            }
            else if (entity is BaseHelicopter)
            {
                Interface.CallHook("OnHelicopterDeath", (BaseHelicopter)entity, info);
            }
            else if (entity is BuildingPrivlidge)
            {
                Interface.CallHook("OnCupboardDeath", (BuildingPrivlidge)entity, info);
            }
            else if (entity is BaseCorpse)
            {
                Interface.CallHook("OnCorpseDeath", (BaseCorpse)entity, info);
            }
            else if (entity is SleepingBag)
            {
                Interface.CallHook("OnSleepingBagDeath", (SleepingBag)entity, info);
            }
            else if (entity is BaseNPC)
            {
                Interface.CallHook("OnNPCDeath", (BaseNPC)entity, info);
            }
        }

        /// <summary>
        /// ON EQUIP
        /// </summary>
        /// <param name="container"></param>
        /// <param name="item"></param>
        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.playerOwner;
            if (!(player is BasePlayer))
            {
                return;
            }

            if (player.inventory.containerWear == container)
            {
                Interface.CallHook("OnEquip", player, item);
            }
            else if (player.inventory.containerBelt == container && item.CanBeHeld())
            {
                Interface.CallHook("OnEquip", player, item);
            }
        }

        /// <summary>
        /// ON UNEQUIP
        /// </summary>
        /// <param name="container"></param>
        /// <param name="item"></param>
        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.playerOwner;
            if (!(player is BasePlayer))
            {
                return;
            }

            if (player.inventory.containerWear == container)
            {
                Interface.CallHook("OnUnequip", player, item);
            }
            else if (player.inventory.containerBelt == container && item.CanBeHeld())
            {
                Interface.CallHook("OnUnequip", player, item);
            }
        }

        /// <summary>
        /// ON SPOT PLAYER
        /// ON SPOT NPC
        /// ON SPOT TURRET
        /// ON SPOT HELICOPTER
        /// ON SPOT RESOURCE
        /// ON USE PLAYER
        /// ON USE TERRAIN
        /// ON USE NPC
        /// ON USE BUILDING
        /// ON USE CUPBOARD
        /// ON USE SLEEPINGBAG
        /// ON USE PLANT
        /// ON USE RESOURCE
        /// </summary>
        /// <param name="player"></param>
        /// <param name="input"></param>
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.IsDown(BUTTON.FIRE_SECONDARY) && !this.inputCooldown.Contains(player))
            {
                TriggerSpotting(player, input);
            }

            if (input.WasJustPressed(BUTTON.USE))
            {
                TriggerUse(player, input);
            }

            if (input.WasJustPressed(BUTTON.JUMP))
            {
                Interface.Oxide.CallHook("OnPlayerJump", player);
            }
        }

        #endregion

        #region Helpers
        void TriggerUse(BasePlayer player, InputState input)
        {
            Quaternion currentRot;
            TryGetPlayerView(player, out currentRot);
            var hitpoints = Physics.RaycastAll(new Ray(player.eyes.position, currentRot * Vector3.forward), 5f, useLayer, QueryTriggerInteraction.Collide);
            GamePhysics.Sort(hitpoints);
            for (var i = 0; i < hitpoints.Length; i++)
            {
                var hit = hitpoints[i];
                var target = hit.collider;
                if (target.name == "Terrain")
                {
                    Interface.Oxide.CallHook("OnUseTerrain", player, target);
                    return;
                }

                if (target.name == "MeshColliderBatch")
                {
                    target = RaycastHitEx.GetCollider(hit);
                }

                BaseEntity targetEntity;
                ResourceDispenser targetDispenser;

                if ((targetEntity = target.GetComponentInParent<BasePlayer>()) != null)
                {
                    Interface.Oxide.CallHook("OnUsePlayer", player, targetEntity);
                    break;
                }
                else if ((targetEntity = target.GetComponentInParent<BaseNPC>()) != null)
                {
                    Interface.Oxide.CallHook("OnUseNPC", player, targetEntity);
                    break;
                }
                else if ((targetEntity = target.GetComponentInParent<BuildingBlock>()) != null)
                {
                    Interface.Oxide.CallHook("OnUseBuilding", player, targetEntity);
                    break;
                }
                else if ((targetEntity = target.GetComponentInParent<SleepingBag>()) != null)
                {
                    Interface.Oxide.CallHook("OnUseSleepingBag", player, targetEntity);
                    break;
                }
                else if ((targetEntity = target.GetComponentInParent<PlantEntity>()) != null)
                {
                    Interface.Oxide.CallHook("OnUsePlant", player, targetEntity);
                    break;
                }
                else if ((targetDispenser = target.GetComponentInParent<ResourceDispenser>()) != null)
                {
                    Interface.Oxide.CallHook("OnUseResource", player, targetDispenser);
                    break;
                }
            }
        }

        void TriggerSpotting(BasePlayer player, InputState input)
        {
            Item activeItem = player.GetActiveItem();
            if (activeItem == null)
            {
                return;
            }

            if (activeItem.info.category != ItemCategory.Weapon)
            {
                return;
            }

            this.inputCooldown.Add(player);
            bool spot = false;

            RaycastHit hit;
            if (Physics.Raycast(player.eyes.position, Quaternion.Euler(input.current.aimAngles) * Vector3.forward, out hit, 2000, spottingMask))
            {
                BaseEntity hitEntity = hit.GetEntity();
                ResourceDispenser dispenser = hitEntity.GetComponentInParent<ResourceDispenser>();
                if(hitEntity == null) {
                    return;
                }

                if (hitEntity is BasePlayer)
                {
                    spot = true;
                }
                else if (hitEntity is BaseNPC)
                {
                    spot = true;
                }
                else if (hitEntity is AutoTurret)
                {
                    spot = true;
                }
                else if (hitEntity is BaseHelicopter)
                {
                    spot = true;
                }
                else if (dispenser is ResourceDispenser)
                {
                    spot = true;
                }

                if (spot)
                {
                    SpotTarget(player, hitEntity);
                }
            }

            timer.Once(1, delegate()
            {
                this.inputCooldown.Remove(player);
            });
        }

        void SpotTarget(BasePlayer player, BaseEntity hitEntity)
        {
            MonoBehaviour target = hitEntity as MonoBehaviour;
            ResourceDispenser dispenser = hitEntity.GetComponentInParent<ResourceDispenser>();

            float distanceTo = player.Distance(hitEntity);

            if (!spotCooldown.ContainsKey(player))
            {
                spotCooldown.Add(player, new List<MonoBehaviour>());
            }

            if (!spotCooldown[player].Contains(target))
            {
                spotCooldown[player].Add(target);

                if (hitEntity is BaseNPC)
                {
                    Interface.Oxide.CallHook("OnSpotNPC", player, hitEntity, distanceTo);
                }
                else if (hitEntity is BasePlayer)
                {
                    Interface.Oxide.CallHook("OnSpotPlayer", player, hitEntity, distanceTo);
                }
                else if (hitEntity is AutoTurret)
                {
                    Interface.Oxide.CallHook("OnSpotTurret", player, hitEntity, distanceTo);
                }
                else if (hitEntity is BaseHelicopter)
                {
                    Interface.Oxide.CallHook("OnSpotHelicopter", player, hitEntity, distanceTo);
                }
                else if (dispenser is ResourceDispenser)
                {
                    Interface.Oxide.CallHook("OnSpotResource", player, hitEntity, distanceTo);
                }

                timer.Once(6, delegate()
                {
                    this.spotCooldown[player].Remove(target);
                });
            }
        }

        bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            viewAngle = new Quaternion(0f, 0f, 0f, 0f);
            var input = serverinput.GetValue(player) as InputState;
            if (input.current == null) return false;
            viewAngle = Quaternion.Euler(input.current.aimAngles);
            return true;
        }
        #endregion
    }
}
