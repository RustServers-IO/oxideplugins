using System.Collections.Generic;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("ZoneManager", "Reneb / Nogrod", "2.4.6", ResourceId = 739)]
    public class ZoneManager : RustPlugin
    {
        #region Fields
        private const string permZone = "zonemanager.zone";
        private const string permIgnoreFlag = "zonemanager.ignoreflag.";

        [PluginReference] Plugin PopupNotifications, Spawns;

        private ZoneFlags disabledFlags = ZoneFlags.None;
        private DynamicConfigFile ZoneManagerData;
        private StoredData storedData;
        private Hash<string, Zone> zoneObjects = new Hash<string, Zone>();

        private readonly Dictionary<string, ZoneDefinition> ZoneDefinitions = new Dictionary<string, ZoneDefinition>();
        private readonly Dictionary<ulong, string> LastZone = new Dictionary<ulong, string>();
        private readonly Dictionary<BasePlayer, HashSet<Zone>> playerZones = new Dictionary<BasePlayer, HashSet<Zone>>();
        private readonly Dictionary<BaseCombatEntity, HashSet<Zone>> buildingZones = new Dictionary<BaseCombatEntity, HashSet<Zone>>();
        private readonly Dictionary<BaseNpc, HashSet<Zone>> npcZones = new Dictionary<BaseNpc, HashSet<Zone>>();
        private readonly Dictionary<ResourceDispenser, HashSet<Zone>> resourceZones = new Dictionary<ResourceDispenser, HashSet<Zone>>();
        private readonly Dictionary<BaseEntity, HashSet<Zone>> otherZones = new Dictionary<BaseEntity, HashSet<Zone>>();
        private readonly Dictionary<BasePlayer, ZoneFlags> playerTags = new Dictionary<BasePlayer, ZoneFlags>();
        
        private static readonly int playersMask = LayerMask.GetMask("Player (Server)");
        private static readonly FieldInfo decay = typeof(DecayEntity).GetField("decay", BindingFlags.Instance | BindingFlags.NonPublic);        
        private static readonly Collider[] colBuffer = (Collider[])typeof(Vis).GetField("colBuffer", (BindingFlags.Static | BindingFlags.NonPublic))?.GetValue(null);
        #endregion

        #region Config
        private bool usePopups = false;
        private bool Changed;
        private bool Initialized;
        private float AutolightOnTime;
        private float AutolightOffTime;
        private string prefix;
        private string prefixColor;

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private static bool GetBoolValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            value = value.Trim().ToLower();
            switch (value)
            {
                case "t":
                case "true":
                case "1":
                case "yes":
                case "y":
                case "on":
                    return true;
                default:
                    return false;
            }
        }       
        private void LoadVariables()
        {
            AutolightOnTime = Convert.ToSingle(GetConfig("AutoLights", "Lights On Time", "18.0"));
            AutolightOffTime = Convert.ToSingle(GetConfig("AutoLights", "Lights Off Time", "8.0"));
            usePopups = Convert.ToBoolean(GetConfig("Notifications", "Use Popup Notifications", true));
            prefix = Convert.ToString(GetConfig("Chat", "Prefix", "ZoneManager: "));
            prefixColor = Convert.ToString(GetConfig("Chat", "Prefix Color (hex)", "#d85540"));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }
        #endregion

        #region Data Management
        private class StoredData
        {
            public readonly HashSet<ZoneDefinition> ZoneDefinitions = new HashSet<ZoneDefinition>();
        }

        private void SaveData()
        {
            ZoneManagerData.WriteObject(storedData);
        }

        private void LoadData()
        {
            ZoneDefinitions.Clear();
            try
            {
                ZoneManagerData.Settings.NullValueHandling = NullValueHandling.Ignore;
                storedData = ZoneManagerData.ReadObject<StoredData>();
                Puts("Loaded {0} Zone definitions", storedData.ZoneDefinitions.Count);
            }
            catch
            {
                Puts("Failed to load StoredData");
                storedData = new StoredData();
            }
            ZoneManagerData.Settings.NullValueHandling = NullValueHandling.Include;
            foreach (var zonedef in storedData.ZoneDefinitions)
                ZoneDefinitions[zonedef.Id] = zonedef;
        }
        #endregion

        #region Zone Management
        #region Zone Component
        public class Zone : MonoBehaviour
        {
            public ZoneDefinition Info;            
            public ZoneFlags disabledFlags = ZoneFlags.None;
            public ZoneManager instance;

            public readonly HashSet<ulong> WhiteList = new HashSet<ulong>();
            public readonly HashSet<ulong> KeepInList = new HashSet<ulong>();

            private HashSet<BasePlayer> players = new HashSet<BasePlayer>();
            private HashSet<BaseCombatEntity> buildings = new HashSet<BaseCombatEntity>();

            private bool lightsOn;

            private readonly FieldInfo meshLookupField = typeof(MeshColliderBatch).GetField("meshLookup", BindingFlags.Instance | BindingFlags.NonPublic);

            private void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "Zone Manager";

                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }

            private void OnDestroy()
            {
                CancelInvoke();
                instance.OnZoneDestroy(this);
                instance = null;
                Destroy(gameObject);
            }

            public void SetInfo(ZoneDefinition info)
            {
                Info = info;
                if (Info == null) return;
                gameObject.name = $"Zone Manager({Info.Id})";
                transform.position = Info.Location;
                transform.rotation = Quaternion.Euler(Info.Rotation);
                UpdateCollider();

                gameObject.SetActive(Info.Enabled);
                enabled = Info.Enabled;

                RegisterPermission();
                InitializeAutoLights();
                InitializeRadiation();
                InitializeComfort();

                if (IsInvoking("CheckEntites")) CancelInvoke("CheckEntites");
                InvokeRepeating("CheckEntites", 10f, 10f);
            }

            private void UpdateCollider()
            {
                var sphereCollider = gameObject.GetComponent<SphereCollider>();
                var boxCollider = gameObject.GetComponent<BoxCollider>();
                if (Info.Size != Vector3.zero)
                {
                    if (sphereCollider != null) Destroy(sphereCollider);
                    if (boxCollider == null)
                    {
                        boxCollider = gameObject.AddComponent<BoxCollider>();
                        boxCollider.isTrigger = true;
                    }
                    boxCollider.size = Info.Size;
                }
                else
                {
                    if (boxCollider != null) Destroy(boxCollider);
                    if (sphereCollider == null)
                    {
                        sphereCollider = gameObject.AddComponent<SphereCollider>();
                        sphereCollider.isTrigger = true;
                    }
                    sphereCollider.radius = Info.Radius;
                }
            }           
            private void RegisterPermission()
            {
                if (!string.IsNullOrEmpty(Info.Permission) && !instance.permission.PermissionExists(Info.Permission))
                    instance.permission.RegisterPermission(Info.Permission, instance);
            }
            private void InitializeAutoLights()
            {
                if (instance.HasZoneFlag(this, ZoneFlags.AlwaysLights))
                {
                    if (IsInvoking("CheckAlwaysLights")) CancelInvoke("CheckAlwaysLights");
                    InvokeRepeating("CheckAlwaysLights", 5f, 60f);
                }
                else if (instance.HasZoneFlag(this, ZoneFlags.AutoLights))
                {
                    var currentTime = GetSkyHour();

                    if (currentTime > instance.AutolightOffTime && currentTime < instance.AutolightOnTime)
                        lightsOn = true;
                    else
                        lightsOn = false;
                    if (IsInvoking("CheckLights")) CancelInvoke("CheckLights");
                    InvokeRepeating("CheckLights", 5f, 30f);
                }
            }
            private void InitializeRadiation()
            {
                var radiation = gameObject.GetComponent<TriggerRadiation>();                                
                if (Info.Radiation > 0)
                {
                    radiation = radiation ?? gameObject.AddComponent<TriggerRadiation>();
                    radiation.RadiationAmountOverride = Info.Radiation;
                    radiation.radiationSize = Info.Radius;
                    radiation.interestLayers = playersMask;
                    radiation.enabled = Info.Enabled;
                }
                else if (radiation != null)
                {
                    radiation.RadiationAmountOverride = 0;
                    radiation.radiationSize = 0;
                    radiation.interestLayers = playersMask;
                    radiation.enabled = false;
                }
            }
            private void InitializeComfort()
            {
                var comfort = gameObject.GetComponent<TriggerComfort>();
                if (Info.Comfort > 0)
                {
                    comfort = comfort ?? gameObject.AddComponent<TriggerComfort>();
                    comfort.baseComfort = Info.Comfort;
                    comfort.triggerSize = Info.Radius;
                    comfort.interestLayers = playersMask;
                    comfort.enabled = Info.Enabled;
                }
                else if (comfort != null)
                {
                    comfort.baseComfort = 0;
                    comfort.triggerSize = 0;
                    comfort.interestLayers = playersMask;
                    comfort.enabled = false;
                }
            }
            private void CheckEntites()
            {
                if (instance == null) return;
                var oldPlayers = players;
                players = new HashSet<BasePlayer>();
                int entities;
                if (Info.Size != Vector3.zero)
                    entities = Physics.OverlapBoxNonAlloc(Info.Location, Info.Size/2, colBuffer, Quaternion.Euler(Info.Rotation), playersMask);
                else
                    entities = Physics.OverlapSphereNonAlloc(Info.Location, Info.Radius, colBuffer, playersMask);
                for (var i = 0; i < entities; i++)
                {
                    var player = colBuffer[i].GetComponentInParent<BasePlayer>();
                    colBuffer[i] = null;
                    if (player != null)
                    {
                        if (players.Add(player) && !oldPlayers.Contains(player))
                            instance.OnPlayerEnterZone(this, player);
                    }
                }
                foreach (var player in oldPlayers)
                {
                    if (!players.Contains(player))
                        instance.OnPlayerExitZone(this, player);
                }
            }

            private void CheckLights()
            {
                if (instance == null) return;
                var currentTime = GetSkyHour();
                if (currentTime > instance.AutolightOffTime && currentTime < instance.AutolightOnTime)
                {
                    if (!lightsOn) return;
                    foreach (var building in buildings)
                    {
                        var oven = building as BaseOven;
                        if (oven != null && !oven.IsInvoking("Cook"))
                        {
                            oven.SetFlag(BaseEntity.Flags.On, false);
                            continue;
                        }
                        var door = building as Door;
                        if (door != null && door.PrefabName.Contains("shutter"))
                            door.SetFlag(BaseEntity.Flags.Open, true);
                    }
                    foreach (var player in players)
                    {
                        if (player.userID >= 76560000000000000L || player.inventory?.containerWear?.itemList == null) continue; //only npc
                        var items = player.inventory.containerWear.itemList;
                        foreach (var item in items)
                        {
                            if (!item.info.shortname.Equals("hat.miner") && !item.info.shortname.Equals("hat.candle")) continue;
                            item.SwitchOnOff(false, player);
                            player.inventory.ServerUpdate(0f);
                            break;
                        }
                    }
                    lightsOn = false;
                }
                else
                {
                    if (lightsOn) return;
                    foreach (var building in buildings)
                    {
                        var oven = building as BaseOven;
                        if (oven != null && !oven.IsInvoking("Cook"))
                        {
                            oven.SetFlag(BaseEntity.Flags.On, true);
                            continue;
                        }
                        var door = building as Door;
                        if (door != null && door.PrefabName.Contains("shutter"))
                            door.SetFlag(BaseEntity.Flags.Open, false);
                    }
                    var fuel = ItemManager.FindItemDefinition("lowgradefuel");
                    foreach (var player in players)
                    {
                        if (player.userID >= 76560000000000000L || player.inventory?.containerWear?.itemList == null) continue; // only npc
                        var items = player.inventory.containerWear.itemList;
                        foreach (var item in items)
                        {
                            if (!item.info.shortname.Equals("hat.miner") && !item.info.shortname.Equals("hat.candle")) continue;
                            if (item.contents == null) item.contents = new ItemContainer();
                            var array = item.contents.itemList.ToArray();
                            for (var i = 0; i < array.Length; i++)
                                array[i].Remove(0f);
                            var newItem = ItemManager.Create(fuel, 100);
                            newItem.MoveToContainer(item.contents);
                            item.SwitchOnOff(true, player);
                            player.inventory.ServerUpdate(0f);
                            break;
                        }
                    }
                    lightsOn = true;
                }
            }

            private void CheckAlwaysLights()
            {
                if (instance == null) return;
                foreach (var building in buildings)
                {
                    var oven = building as BaseOven;
                    if (oven == null || oven.IsInvoking("Cook")) continue;
                    oven.SetFlag(BaseEntity.Flags.On, true);
                }
            }

            public void OnEntityKill(BaseCombatEntity entity)
            {
                var player = entity as BasePlayer;
                if (player != null)
                    players.Remove(player);
                else if (entity != null && !(entity is LootContainer) && !(entity is BaseHelicopter) && !(entity is BaseNpc))
                    buildings.Remove(entity);
            }

            private void CheckCollisionEnter(Collider col)
            {
                if (instance == null) return;
                if (instance.HasZoneFlag(this, ZoneFlags.NoDecay))
                {
                    var decayEntity = col.GetComponentInParent<DecayEntity>();
                    if (decayEntity != null)
                        typeof(DecayEntity).GetField("decay", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(decayEntity, null);         
                }
                var resourceDispenser = col.GetComponentInParent<ResourceDispenser>();
                if (resourceDispenser != null) 
                {
                    instance.OnResourceEnterZone(this, resourceDispenser);
                    return;
                }
                var entity = col.GetComponentInParent<BaseEntity>();
                if (entity == null) return;
                var npc = entity as BaseNpc;
                if (npc != null)
                {
                    instance.OnNpcEnterZone(this, npc);
                    return;
                }
                var combatEntity = entity as BaseCombatEntity;
                if (combatEntity != null && !(entity is LootContainer) && !(entity is BaseHelicopter))
                {
                    buildings.Add(combatEntity);
                    instance.OnBuildingEnterZone(this, combatEntity);
                }
                else
                {
                    instance.OnOtherEnterZone(this, entity);
                }
            }

            private void CheckCollisionLeave(Collider col)
            {
                if (instance.HasZoneFlag(this, ZoneFlags.NoDecay))
                {
                    var decayEntity = col.GetComponentInParent<DecayEntity>();
                    if (decayEntity != null)
                        typeof(DecayEntity).GetField("decay", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(decayEntity, PrefabAttribute.server.Find<Decay>(decayEntity.prefabID));
                }
                var resourceDispenser = col.GetComponentInParent<ResourceDispenser>();
                if (resourceDispenser != null)
                {
                    instance.OnResourceExitZone(this, resourceDispenser);
                    return;
                }
                var entity = col.GetComponentInParent<BaseEntity>();
                if (entity == null) return;
                var npc = entity as BaseNpc;
                if (npc != null)
                {
                    instance.OnNpcExitZone(this, npc);
                    return;
                }
                var combatEntity = entity as BaseCombatEntity;
                if (combatEntity != null && !(entity is LootContainer) && !(entity is BaseHelicopter))
                {
                    buildings.Remove(combatEntity);
                    instance.OnBuildingExitZone(this, combatEntity);
                }
                else
                {
                    instance.OnOtherExitZone(this, entity);
                }
            }

            private void OnTriggerEnter(Collider col)
            {
                var player = col.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    if (!players.Add(player)) return;
                    instance.OnPlayerEnterZone(this, player);
                }
                else if (!col.transform.CompareTag("MeshColliderBatch"))
                    CheckCollisionEnter(col);
                else
                {
                    var colliderBatch = col.GetComponent<MeshColliderBatch>();
                    if (colliderBatch == null) return;
                    var colliders = ((MeshColliderLookup) meshLookupField.GetValue(colliderBatch)).src.data;
                    var bounds = gameObject.GetComponent<Collider>().bounds;
                    foreach (var instance in colliders)
                        if (instance.collider && instance.collider.bounds.Intersects(bounds))
                            CheckCollisionEnter(instance.collider);
                }
            }

            private void OnTriggerExit(Collider col)
            {
                var player = col.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    if (!players.Remove(player)) return;
                    instance.OnPlayerExitZone(this, player);
                }
                else if(!col.transform.CompareTag("MeshColliderBatch"))
                    CheckCollisionLeave(col);
                else
                {
                    var colliderBatch = col.GetComponent<MeshColliderBatch>();
                    if (colliderBatch == null) return;
                    var colliders = ((MeshColliderLookup)meshLookupField.GetValue(colliderBatch)).src.data;
                    var bounds = gameObject.GetComponent<Collider>().bounds;
                    foreach (var instance in colliders)
                        if (instance.collider && instance.collider.bounds.Intersects(bounds))
                            CheckCollisionLeave(instance.collider);
                }
            }
        }
        #endregion

        #region Zone Definition
        public class ZoneDefinition
        {
            public string Name;
            public float Radius;
            public float Radiation;
            public float Comfort;
            public Vector3 Location;
            public Vector3 Size;
            public Vector3 Rotation;
            public string Id;
            public string EnterMessage;            
            public string LeaveMessage;
            public string Permission;
            public string EjectSpawns;
            public bool Enabled = true;
            public ZoneFlags Flags;

            public ZoneDefinition()
            {
            }

            public ZoneDefinition(Vector3 position)
            {
                Radius = 20f;
                Location = position;
            }

        }
        #endregion

        private void OnZoneDestroy(Zone zone)
        {
            HashSet<Zone> zones;
            foreach (var key in playerZones.Keys.ToArray())
                if (playerZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnPlayerExitZone(zone, key);
            foreach (var key in buildingZones.Keys.ToArray())
                if (buildingZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnBuildingExitZone(zone, key);
            foreach (var key in npcZones.Keys.ToArray())
                if (npcZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnNpcExitZone(zone, key);
            foreach (var key in resourceZones.Keys.ToArray())
                if (resourceZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnResourceExitZone(zone, key);
            foreach (var key in otherZones.Keys.ToArray())
            {
                if (!otherZones.TryGetValue(key, out zones))
                {
                    Puts("Zone: {0} Entity: {1} ({2}) {3}", zone.Info.Id, key.GetType(), key.net?.ID, key.IsDestroyed);
                    continue;
                }
                if (zones.Contains(zone))
                    OnOtherExitZone(zone, key);
            }
        }
        #endregion

        #region Flags
        [Flags]
        public enum ZoneFlags : ulong
        {
            None = 0L,
            AutoLights = 1UL,
            Eject = 1UL << 1,
            PvpGod = 1UL << 2,
            PveGod = 1UL << 3,
            SleepGod = 1UL << 4,
            UnDestr = 1UL << 5,
            NoBuild = 1UL << 6,
            NoTp = 1UL << 7,
            NoChat = 1UL << 8,
            NoGather = 1UL << 9,
            NoPve = 1UL << 10,
            NoWounded = 1UL << 11,
            NoDecay = 1UL << 12,
            NoDeploy = 1UL << 13,
            NoKits = 1UL << 14,
            NoBoxLoot = 1UL << 15,
            NoPlayerLoot = 1UL << 16,
            NoCorpse = 1UL << 17,
            NoSuicide = 1UL << 18,
            NoRemove = 1UL << 19,
            NoBleed = 1UL << 20,
            KillSleepers = 1UL << 21,
            NpcFreeze = 1UL << 22,
            NoDrown = 1UL << 23,
            NoStability = 1UL << 24,
            NoUpgrade = 1UL << 25,
            EjectSleepers = 1UL << 26,
            NoPickup = 1UL << 27,
            NoCollect = 1UL << 28,
            NoDrop = 1UL << 29,
			Kill = 1UL << 30,
            NoCup = 1UL << 31,
            AlwaysLights = 1UL << 32,
            NoTrade = 1UL << 33,
            NoShop = 1UL << 34,
            NoSignUpdates = 1UL << 35,
            NoOvenToggle = 1UL << 36,
            NoLootSpawns = 1UL << 37,
            NoNPCSpawns = 1UL << 38,
            NoVending = 1UL << 39,
            NoStash = 1UL << 40,
            NoCraft = 1UL << 41,
            NoHeliTargeting = 1UL << 42,
            NoTurretTargeting = 1UL << 43
        }

        private bool HasZoneFlag(Zone zone, ZoneFlags flag)
        {
            if ((disabledFlags & flag) == flag) return false;
            return (zone.Info.Flags & ~zone.disabledFlags & flag) == flag;
        }
        private static bool HasAnyFlag(ZoneFlags flags, ZoneFlags flag)
        {
            return (flags & flag) != ZoneFlags.None;
        }
        private static bool HasAnyZoneFlag(Zone zone)
        {
            return (zone.Info.Flags & ~zone.disabledFlags) != ZoneFlags.None;
        }
        private static void AddZoneFlag(ZoneDefinition zone, ZoneFlags flag)
        {
            zone.Flags |= flag;
        }
        private static void RemoveZoneFlag(ZoneDefinition zone, ZoneFlags flag)
        {
            zone.Flags &= ~flag;
        }
        #endregion

        #region Oxide Hooks       
        private void Loaded()
        {
            lang.RegisterMessages(Messages, this);
            ZoneManagerData = Interface.Oxide.DataFileSystem.GetFile("ZoneManager");
            ZoneManagerData.Settings.Converters = new JsonConverter[] { new StringEnumConverter(), new UnityVector3Converter(), };

            permission.RegisterPermission(permZone, this);            
            foreach (var flag in Enum.GetValues(typeof(ZoneFlags)))
                permission.RegisterPermission(permIgnoreFlag + flag.ToString().ToLower(), this);
            
            LoadData();
            LoadVariables();           
        }
       
        private void Unload()
        {
            for (int i = zoneObjects.Count - 1; i >= 0; i--)
            {
                var zone = zoneObjects.ElementAt(i);
                UnityEngine.Object.Destroy(zone.Value);
                zoneObjects.Remove(zone.Key);
            }
           
            var collectibleEntities = Resources.FindObjectsOfTypeAll<CollectibleEntity>();
            for (var i = 0; i < collectibleEntities.Length; i++)
            {
                var collider = collectibleEntities[i].GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.Destroy(collider);
            }
        }

        private void OnTerrainInitialized()
        {
            if (Initialized) return;
            SetupCollectibleEntity();
            foreach (var zoneDefinition in ZoneDefinitions.Values)
                NewZone(zoneDefinition);
            Initialized = true;
        }

        private void OnServerInitialized()
        {
            if (Initialized) return;
            //var values = Enum.GetValues(typeof(ZoneFlags)).Cast<ZoneFlags>();
            //foreach (var flagse in values)
            //{
            //    Puts("{0} {1}", flagse, (ulong)flagse);
            //}            
            
            timer.In(1, () => {
                SetupCollectibleEntity();
                foreach (var zoneDefinition in ZoneDefinitions.Values)
                    NewZone(zoneDefinition);
            });
            Initialized = true;
        }
        
        private void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            var player = planner.GetOwnerPlayer();
            if (player == null) return;
            if (HasPlayerFlag(player, ZoneFlags.NoBuild) && !CanBypass(player, ZoneFlags.NoBuild) && !isAdmin(player))
            {
                gameobject.GetComponentInParent<BaseCombatEntity>().Kill(BaseNetworkable.DestroyMode.Gib);
                SendMessage(player, msg("noBuild", player.UserIDString));
            }
        }

        private object OnStructureUpgrade(BuildingBlock buildingBlock, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoUpgrade) && !CanBypass(player, ZoneFlags.NoUpgrade) && !isAdmin(player))
            {
                SendMessage(player, msg("noUpgrade", player.UserIDString));
                return false;
            }
            return null;
        }
        
        private void OnItemDeployed(Deployer deployer, BaseEntity deployedEntity)
        {
            var player = deployer.GetOwnerPlayer();
            if (player == null) return;
            if (HasPlayerFlag(player, ZoneFlags.NoDeploy) && !CanBypass(player, ZoneFlags.NoDeploy) && !isAdmin(player))
            {
                deployedEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                SendMessage(player, msg("noDeploy", player.UserIDString));
            }
            else if (HasPlayerFlag(player, ZoneFlags.NoCup) && deployedEntity.PrefabName.Contains("cupboard") && !CanBypass(player, ZoneFlags.NoCup) && !isAdmin(player))
            {                
                deployedEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                SendMessage(player, msg("noCup", player.UserIDString));
            }
        }

        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity ownerEntity, float delta)
        {
            var player = ownerEntity as BasePlayer;
            if (player == null) return;
            if (metabolism.bleeding.value > 0 && HasPlayerFlag(player, ZoneFlags.NoBleed) && !CanBypass(player, ZoneFlags.NoBleed))
                metabolism.bleeding.value = 0f;
            if (metabolism.oxygen.value < 1 && HasPlayerFlag(player, ZoneFlags.NoDrown) && !CanBypass(player, ZoneFlags.NoDrown))
                metabolism.oxygen.value = 1;
        }
       
        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return null;
            if (HasPlayerFlag(arg.Player(), ZoneFlags.NoChat) && !CanBypass(arg.Player(), ZoneFlags.NoChat))
            {
                SendMessage(arg.Player(), msg("noChat", arg.Player().UserIDString));
                return false;
            }
            return null;
        }
       
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return null;
            if (arg.cmd?.Name == null) return null;
            if (arg.cmd.Name == "kill" && HasPlayerFlag(arg.Player(), ZoneFlags.NoSuicide) && !CanBypass(arg.Player(), ZoneFlags.NoSuicide))
            {
                SendMessage(arg.Player(), msg("noSuicide", arg.Player().UserIDString));
                return false;
            }
            return null;
        }
                
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.KillSleepers) && !CanBypass(player, ZoneFlags.KillSleepers) && !isAdmin(player))
            {
                player.Die();
                return;
            }            

            if (HasPlayerFlag(player, ZoneFlags.EjectSleepers) && !CanBypass(player, ZoneFlags.EjectSleepers) && !isAdmin(player))
            {
                HashSet<Zone> zones;
                if (!playerZones.TryGetValue(player, out zones) || zones.Count == 0) return;
                foreach (var zone in zones)
                {
                    if (HasZoneFlag(zone, ZoneFlags.EjectSleepers))
                    {
                        EjectPlayer(zone, player);
                        break;
                    }
                }
            }
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            var disp = hitinfo?.HitEntity?.GetComponent<ResourceDispenser>();
            if (disp == null || hitinfo.Weapon.GetComponent<BaseMelee>() == null)
                return;
                       
            HashSet<Zone> resourceZone;
            if (!resourceZones.TryGetValue(disp, out resourceZone))
                return;

            foreach (var zone in resourceZone)
            {
                if (HasZoneFlag(zone, ZoneFlags.NoGather) && !CanBypass(attacker, ZoneFlags.NoGather))
                {
                    SendMessage(attacker, msg("noGather", attacker.UserIDString));
                    hitinfo.HitEntity = null;
                    break;
                }
            }
        }
       
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity == null || entity.GetComponent<ResourceDispenser>() != null) return;
            var player = entity as BasePlayer;
            if (player != null)
            {
                var target = hitinfo.Initiator as BasePlayer;
                if (player.IsSleeping() && HasPlayerFlag(player, ZoneFlags.SleepGod) && !CanBypass(player, ZoneFlags.SleepGod))
                {
                    CancelDamage(hitinfo);
                }
                else if (target != null)
                {
                    if (target.userID < 76560000000000000L) return;
                    if (HasPlayerFlag(player, ZoneFlags.PvpGod) && !CanBypass(player, ZoneFlags.PvpGod))
                        CancelDamage(hitinfo);
                    else if (HasPlayerFlag(target, ZoneFlags.PvpGod) && !CanBypass(target, ZoneFlags.PvpGod))
                        CancelDamage(hitinfo);
                }
                else if (HasPlayerFlag(player, ZoneFlags.PveGod) && !CanBypass(player, ZoneFlags.PveGod))
                    CancelDamage(hitinfo);
                else if (hitinfo.Initiator is FireBall && HasPlayerFlag(player, ZoneFlags.PvpGod) && !CanBypass(player, ZoneFlags.PvpGod))
                    CancelDamage(hitinfo);
                return;
            }
            var npcai = entity as BaseNpc;
            if (npcai != null)
            {
                HashSet<Zone> zones;
                if (!npcZones.TryGetValue(npcai, out zones)) return;
                foreach (var zone in zones)
                {
                    if (HasZoneFlag(zone, ZoneFlags.NoPve))
                    {
                        if (hitinfo.InitiatorPlayer != null && CanBypass(hitinfo.InitiatorPlayer, ZoneFlags.NoPve)) continue;
                        CancelDamage(hitinfo);
                        break;
                    }
                }
                return;
            }
            if (!(entity is LootContainer) && !(entity is BaseHelicopter))
            {
                var resource = entity.GetComponent<ResourceDispenser>();
                HashSet<Zone> zones;
                if (!buildingZones.TryGetValue(entity, out zones) && (resource == null || !resourceZones.TryGetValue(resource, out zones))) return;
                foreach (var zone in zones)
                {
                    if (HasZoneFlag(zone, ZoneFlags.UnDestr))
                    {
                        if (hitinfo.InitiatorPlayer != null && CanBypass(hitinfo.InitiatorPlayer, ZoneFlags.UnDestr)) continue;
                        CancelDamage(hitinfo);
                        break;
                    }
                }
            }           
        }

        private void OnEntityKill(BaseNetworkable networkable)
        {
            var entity = networkable as BaseEntity;
            if (entity == null) return;
            var resource = entity.GetComponent<ResourceDispenser>();
            if (resource != null)
            {
                HashSet<Zone> zones;
                if (resourceZones.TryGetValue(resource, out zones))
                    OnResourceExitZone(null, resource, true);
                return;
            }
            var player = entity as BasePlayer;
            if (player != null)
            {
                HashSet<Zone> zones;
                if (playerZones.TryGetValue(player, out zones))
                    OnPlayerExitZone(null, player, true);
                return;
            }
            var npc = entity as BaseNpc;
            if (npc != null)
            {
                HashSet<Zone> zones;
                if (npcZones.TryGetValue(npc, out zones))
                    OnNpcExitZone(null, npc, true);
                return;
            }
            var building = entity as BaseCombatEntity;
            if (building != null && !(entity is LootContainer) && !(entity is BaseHelicopter))
            {
                HashSet<Zone> zones;
                if (buildingZones.TryGetValue(building, out zones))
                    OnBuildingExitZone(null, building, true);
            }
            else
            {
                HashSet<Zone> zones;
                if (otherZones.TryGetValue(entity, out zones))
                    OnOtherExitZone(null, entity, true);
            }
        }
        
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is BaseCorpse)
            {
                timer.Once(2f, () =>
                {
                    HashSet<Zone> zones;
                    if (entity == null || entity.IsDestroyed || !entity.GetComponent<ResourceDispenser>() || !resourceZones.TryGetValue(entity.GetComponent<ResourceDispenser>(), out zones)) return;
                    foreach (var zone in zones)
                    {
                        if (HasZoneFlag(zone, ZoneFlags.NoCorpse) && !CanBypass((entity as BaseCorpse).OwnerID, ZoneFlags.NoCorpse))
                        {
                            entity.Kill(BaseNetworkable.DestroyMode.None);
                            break;
                        }
                    }
                });
            }
            else if (entity is BuildingBlock && zoneObjects != null)
            {
                var block = (BuildingBlock)entity;
                if (EntityHasFlag(entity as BuildingBlock, "NoStability") && !CanBypass((entity as BuildingBlock).OwnerID, ZoneFlags.NoStability))                
                    block.grounded = true;                
            } 
            else if (entity is LootContainer || entity is JunkPile || entity is BaseNpc)            
                timer.In(2, ()=> CheckSpawnedEntity(entity));            
        }
        
        private object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            return OnLootPlayerInternal(looter, target) ? null : (object) false;
        }

        private void OnLootPlayer(BasePlayer looter, BasePlayer target)
        {
            OnLootPlayerInternal(looter, target);
        }

        private bool OnLootPlayerInternal(BasePlayer looter, BasePlayer target)
        {
            if ((HasPlayerFlag(looter, ZoneFlags.NoPlayerLoot) && !CanBypass(looter, ZoneFlags.NoPlayerLoot)) || (target != null && HasPlayerFlag(target, ZoneFlags.NoPlayerLoot) && !CanBypass(target, ZoneFlags.NoPlayerLoot)))
            {
                SendMessage(looter, msg("noLoot", looter.UserIDString));
                NextTick(looter.EndLooting);
                return false;
            }
            return true;
        }

        private void OnLootEntity(BasePlayer looter, BaseEntity target)
        {
            if (target is BaseCorpse)
                OnLootPlayerInternal(looter, null);
            else if (HasPlayerFlag(looter, ZoneFlags.NoBoxLoot) && !CanBypass(looter, ZoneFlags.NoBoxLoot))
            {
                if ((target as StorageContainer)?.transform.position == Vector3.zero) return;
                SendMessage(looter, msg("noLoot", looter.UserIDString));
                timer.Once(0.01f, looter.EndLooting);
            }
        }

        private object CanBeWounded(BasePlayer player, HitInfo hitinfo)
        {
            return HasPlayerFlag(player, ZoneFlags.NoWounded) && !CanBypass(player, ZoneFlags.NoWounded) ? (object) false : null;
        }

        object CanUpdateSign(BasePlayer player, Signage sign)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoSignUpdates) && !CanBypass(player, ZoneFlags.NoSignUpdates))
            {
                SendMessage(player, msg("noSignUpdates", player.UserIDString));
                return false;
            }
            return null;
        }

        object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoOvenToggle) && !CanBypass(player, ZoneFlags.NoOvenToggle))
            {
                SendMessage(player, msg("noOvenToggle", player.UserIDString));
                return false;
            }
            return null;
        }

        object CanPickupEntity(BaseCombatEntity entity, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoPickup) && !CanBypass(player, ZoneFlags.NoPickup))
            {
                SendMessage(player, msg("noPickup", player.UserIDString));
                return false;
            }
            return null;
        }
        object CanUseVending(VendingMachine machine, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoVending) && !CanBypass(player, ZoneFlags.NoVending))
            {
                SendMessage(player, msg("noVending", player.UserIDString));
                return false;
            }
            return null;
        }
        object CanHideStash(StashContainer stash, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoStash) && !CanBypass(player, ZoneFlags.NoStash))
            {
                SendMessage(player, msg("noStash", player.UserIDString));
                return false;
            }
            return null;
        }
        object CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            var player = itemCrafter.GetComponent<BasePlayer>();
            if (player != null)
            {
                if (HasPlayerFlag(player, ZoneFlags.NoCraft) && !CanBypass(player, ZoneFlags.NoCraft))
                {
                    SendMessage(player, msg("noCraft", player.UserIDString));
                    return false;
                }
            }
            return null;
        }
        object CanPickupLock(BasePlayer player, BaseLock baseLock)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoPickup) && !CanBypass(player, ZoneFlags.NoPickup))
            {
                SendMessage(player, msg("noPickup", player.UserIDString));
                return false;
            }
            return null;
        }
        object CanBeTargeted(BaseCombatEntity entity, MonoBehaviour behaviour)
        {
            var player = entity.ToPlayer();
            if (player != null)
            {
                if (behaviour is AutoTurret || behaviour is FlameTurret || behaviour is GunTrap)
                {
                    if (HasPlayerFlag(player, ZoneFlags.NoTurretTargeting) && !CanBypass(player, ZoneFlags.NoTurretTargeting))
                        return false;
                }

                else if (behaviour is HelicopterTurret)
                {
                    if (HasPlayerFlag(player, ZoneFlags.NoHeliTargeting) && !CanBypass(player, ZoneFlags.NoHeliTargeting))
                    {
                        var turret = behaviour as HelicopterTurret;
                        turret.ClearTarget();
                        turret._heliAI.SetTargetDestination(turret._heliAI.GetRandomPatrolDestination());
                        return false;
                    }                  
                }
            }
            return null;
        }
        object OnHelicopterTarget(HelicopterTurret turret, BaseCombatEntity entity)
        {
            var player = entity.ToPlayer();
            if (player != null)
            {
                if (HasPlayerFlag(player, ZoneFlags.NoHeliTargeting) && !CanBypass(player, ZoneFlags.NoHeliTargeting))
                    return false;
            }
            return null;
        }

        #endregion

        #region External Plugin Hooks        
        private object canRedeemKit(BasePlayer player)
        {
            return HasPlayerFlag(player, ZoneFlags.NoKits) && !CanBypass(player, ZoneFlags.NoKits) ? "You may not redeem a kit inside this area" : null;
        }

        private object CanTeleport(BasePlayer player)
        {
            return HasPlayerFlag(player, ZoneFlags.NoTp) && !CanBypass(player, ZoneFlags.NoTp) ? "You may not teleport in this area" : null;
        }

        private object canRemove(BasePlayer player)
        {
            return HasPlayerFlag(player, ZoneFlags.NoRemove) && !CanBypass(player, ZoneFlags.NoRemove) ? "You may not use the remover tool in this area" : null;
        }

        private bool CanChat(BasePlayer player)
        {
            return HasPlayerFlag(player, ZoneFlags.NoChat) && !CanBypass(player, ZoneFlags.NoChat) ? false : true;
        }

        private object CanTrade(BasePlayer player)
        {
            return HasPlayerFlag(player, ZoneFlags.NoTrade) && !CanBypass(player, ZoneFlags.NoTrade) ? "You may not trade in this area" : null;
        }

        private object canShop(BasePlayer player)
        {
            return HasPlayerFlag(player, ZoneFlags.NoShop) && !CanBypass(player, ZoneFlags.NoShop) ? "You may not use the store in this area" : null;
        }
        #endregion

        #region Zone Editing
        private void UpdateZoneDefinition(ZoneDefinition zone, string[] args, BasePlayer player = null)
        {
            for (var i = 0; i < args.Length; i = i + 2)
            {
                object editvalue;
                switch (args[i].ToLower())
                {
                    case "name":
                        editvalue = zone.Name = args[i + 1];
                        break;
                    case "id":
                        editvalue = zone.Id = args[i + 1];
                        break;
                    case "comfort":
                        editvalue = zone.Comfort = Convert.ToSingle(args[i + 1]);
                        break;
                    case "radiation":
                        editvalue = zone.Radiation = Convert.ToSingle(args[i + 1]);                        
                        break;
                    case "radius":
                        editvalue = zone.Radius = Convert.ToSingle(args[i + 1]);
                        break;
                    case "rotation":
                        object rotation = Convert.ToSingle(args[i + 1]);
                        if (rotation is float)
                            zone.Rotation = Quaternion.AngleAxis((float)rotation, Vector3.up).eulerAngles;
                        else
                        {
                            zone.Rotation = player?.GetNetworkRotation() ?? Vector3.zero;
                            zone.Rotation.x = 0;
                        }
                        editvalue = zone.Rotation;
                        break;
                    case "location":
                        if (player != null && args[i + 1].Equals("here", StringComparison.OrdinalIgnoreCase))
                        {
                            editvalue = zone.Location = player.transform.position;
                            break;
                        }
                        var loc = args[i + 1].Trim().Split(' ');
                        if (loc.Length == 3)
                            editvalue = zone.Location = new Vector3(Convert.ToSingle(loc[0]), Convert.ToSingle(loc[1]), Convert.ToSingle(loc[2]));
                        else
                        {
                            if (player != null) SendMessage(player, "Invalid location format, use: \"x y z\" or here");
                            continue;
                        }
                        break;
                    case "size":
                        var size = args[i + 1].Trim().Split(' ');
                        if (size.Length == 3)
                            editvalue = zone.Size = new Vector3(Convert.ToSingle(size[0]), Convert.ToSingle(size[1]), Convert.ToSingle(size[2]));
                        else
                        {
                            if (player != null) SendMessage(player, "Invalid size format, use: \"x y z\"");
                            continue;
                        }
                        break;
                    case "enter_message":
                        editvalue = zone.EnterMessage = args[i + 1];
                        break;
                    case "leave_message":
                        editvalue = zone.LeaveMessage = args[i + 1];
                        break;
                    case "permission":
                        string permission = args[i + 1];
                        if (!permission.StartsWith("zonemanager."))
                            permission = $"zonemanager.{permission}";
                        editvalue = zone.Permission = permission;
                        break;
                    case "ejectspawns":
                        editvalue = zone.EjectSpawns = args[i + 1];
                        break;
                    case "enabled":
                    case "enable":
                        editvalue = zone.Enabled = GetBoolValue(args[i + 1]);
                        break;
                    default:
                        try
                        {
                            var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), args[i], true);
                            var boolValue = GetBoolValue(args[i + 1]);
                            editvalue = boolValue;
                            if (boolValue) AddZoneFlag(zone, flag);
                            else RemoveZoneFlag(zone, flag);
                        }
                        catch
                        {
                            if (player != null) SendMessage(player, $"Unknown zone flag: {args[i]}");
                            continue;
                        }
                        break;
                }
                if (player != null) SendMessage(player, $"{args[i]} set to {editvalue}");
            }
        }
        #endregion

        #region API        
        private bool CreateOrUpdateZone(string zoneId, string[] args, Vector3 position = default(Vector3))
        {
            ZoneDefinition zonedef;
            if (!ZoneDefinitions.TryGetValue(zoneId, out zonedef))
                zonedef = new ZoneDefinition { Id = zoneId, Radius = 20 };
            else
                storedData.ZoneDefinitions.Remove(zonedef);
            UpdateZoneDefinition(zonedef, args);

            if (position != default(Vector3))
                zonedef.Location = position;

            ZoneDefinitions[zoneId] = zonedef;
            storedData.ZoneDefinitions.Add(zonedef);
            SaveData();

            if (zonedef.Location == null) return false;
            RefreshZone(zoneId);
            return true;
        }

        private bool EraseZone(string zoneId)
        {
            ZoneDefinition zone;
            if (!ZoneDefinitions.TryGetValue(zoneId, out zone)) return false;

            storedData.ZoneDefinitions.Remove(zone);
            ZoneDefinitions.Remove(zoneId);
            SaveData();
            RefreshZone(zoneId);
            return true;
        }

        private List<string> ZoneFieldListRaw()
        {
            var list = new List<string> { "name", "ID", "radiation", "radius", "rotation", "size", "Location", "enter_message", "leave_message" };
            list.AddRange(Enum.GetNames(typeof(ZoneFlags)));
            return list;
        }

        private Dictionary<string, string> ZoneFieldList(string zoneId)
        {
            var zone = GetZoneByID(zoneId);
            if (zone == null) return null;
            var fieldlistzone = new Dictionary<string, string>
            {
                { "name", zone.Info.Name },
                { "ID", zone.Info.Id },
                { "comfort", zone.Info.Comfort.ToString() },
                { "radiation", zone.Info.Radiation.ToString() },
                { "radius", zone.Info.Radius.ToString() },
                { "rotation", zone.Info.Rotation.ToString() },
                { "size", zone.Info.Size.ToString() },
                { "Location", zone.Info.Location.ToString() },
                { "enter_message", zone.Info.EnterMessage },
                { "leave_message", zone.Info.LeaveMessage },
                { "permission", zone.Info.Permission },
                { "ejectspawns", zone.Info.EjectSpawns }
            };

            var values = Enum.GetValues(typeof(ZoneFlags));
            foreach (var value in values)
                fieldlistzone[Enum.GetName(typeof(ZoneFlags), value)] = HasZoneFlag(zone, (ZoneFlags)value).ToString();
            return fieldlistzone;
        }

        private List<ulong> GetPlayersInZone(string zoneId)
        {
            var players = new List<ulong>();
            foreach (var pair in playerZones)
                players.AddRange(pair.Value.Where(zone => zone.Info.Id == zoneId).Select(zone => pair.Key.userID));
            return players;
        }

        private bool isPlayerInZone(string zoneId, BasePlayer player)
        {
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones)) return false;
            return zones.Any(zone => zone.Info.Id == zoneId);
        }

        private bool AddPlayerToZoneWhitelist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            AddToWhitelist(targetZone, player);
            return true;
        }

        private bool AddPlayerToZoneKeepinlist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            AddToKeepinlist(targetZone, player);
            return true;
        }

        private bool RemovePlayerFromZoneWhitelist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            RemoveFromWhitelist(targetZone, player);
            return true;
        }

        private bool RemovePlayerFromZoneKeepinlist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            RemoveFromKeepinlist(targetZone, player);
            return true;
        }

        private object GetZoneRadius(string zoneID) => GetZoneByID(zoneID)?.Info.Radius;
        private object GetZoneSize(string zoneID) => GetZoneByID(zoneID)?.Info.Size;
        private object GetZoneName(string zoneID) => GetZoneByID(zoneID)?.Info.Name;
        private object CheckZoneID(string zoneID) => GetZoneByID(zoneID)?.Info.Id;
        private object GetZoneIDs() => zoneObjects.Keys.ToArray();
        private Vector3 GetZoneLocation(string zoneId) => GetZoneByID(zoneId)?.Info.Location ?? Vector3.zero;
        private void AddToWhitelist(Zone zone, BasePlayer player) { zone.WhiteList.Add(player.userID); }
        private void RemoveFromWhitelist(Zone zone, BasePlayer player) { zone.WhiteList.Remove(player.userID); }
        private void AddToKeepinlist(Zone zone, BasePlayer player) { zone.KeepInList.Add(player.userID); }
        private void RemoveFromKeepinlist(Zone zone, BasePlayer player) { zone.KeepInList.Remove(player.userID); }

        private void AddDisabledFlag(string flagString)
        {
            try
            {
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                disabledFlags |= flag;
            }
            catch
            {
            }
        }

        private void RemoveDisabledFlag(string flagString)
        {
            try
            {
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                disabledFlags &= ~flag;
            }
            catch
            {
            }
        }

        private void AddZoneDisabledFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                zone.disabledFlags |= flag;
                UpdateAllPlayers();
            }
            catch
            {
            }
        }

        private void RemoveZoneDisabledFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                zone.disabledFlags &= ~flag;
                UpdateAllPlayers();
            }
            catch
            {
            }
        }

        private bool HasFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                if (HasZoneFlag(zone, flag))
                    return true;
            }
            catch
            {
            }
            return false;
        }

        private void AddFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                AddZoneFlag(zone.Info, flag);
            }
            catch
            {
            }
        }

        private void RemoveFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                RemoveZoneFlag(zone.Info, flag);
            }
            catch
            {
            }
        }

        private Zone GetZoneByID(string zoneId)
        {
            return zoneObjects.ContainsKey(zoneId) ? zoneObjects[zoneId] : null;
        }

        private void NewZone(ZoneDefinition zonedef)
        {
            if (zonedef == null) return;
            var newZone = new GameObject().AddComponent<Zone>();
            newZone.instance = this;
            newZone.SetInfo(zonedef);

            if (!zoneObjects.ContainsKey(zonedef.Id))
                zoneObjects.Add(zonedef.Id, newZone);
            else zoneObjects[zonedef.Id] = newZone;
        }

        private void RefreshZone(string zoneId)
        {
            var zone = GetZoneByID(zoneId);

            if (zone != null)            
                UnityEngine.Object.Destroy(zone);
            
            ZoneDefinition zoneDef;
            if (ZoneDefinitions.TryGetValue(zoneId, out zoneDef))
                NewZone(zoneDef);
        }

        private void UpdateAllPlayers()
        {
            var players = playerTags.Keys.ToArray();
            for (var i = 0; i < players.Length; i++)
                UpdateFlags(players[i]);
        }

        private void UpdateFlags(BasePlayer player)
        {
            playerTags.Remove(player);
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones) || zones.Count == 0) return;
            var newFlags = ZoneFlags.None;
            foreach (var zone in zones)
                newFlags |= zone.Info.Flags & ~zone.disabledFlags;
            playerTags[player] = newFlags;
        }

        private bool HasPlayerFlag(BasePlayer player, ZoneFlags flag)
        {
            if ((disabledFlags & flag) == flag) return false;
            ZoneFlags tags;
            if (!playerTags.TryGetValue(player, out tags)) return false;
            return (tags & flag) == flag;
        }

        private static BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrIdOrIp)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrIdOrIp)
                    return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString == nameOrIdOrIp)
                    return sleepingPlayer;
                if (sleepingPlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return sleepingPlayer;
            }
            return null;
        }

        private BasePlayer FindPlayerByRadius(Vector3 position, float rad)
        {
            var cachedColliders = Physics.OverlapSphere(position, rad, playersMask);
            return cachedColliders.Select(collider => collider.GetComponentInParent<BasePlayer>()).FirstOrDefault(player => player != null);
        }

        private void CheckExplosivePosition(TimedExplosive explosive)
        {
            if (explosive == null) return;
            foreach (var zone in zoneObjects)
            {
                if (!HasZoneFlag(zone.Value, ZoneFlags.UnDestr)) continue;
                if (Vector3.Distance(explosive.GetEstimatedWorldPosition(), zone.Value.transform.position) > zone.Value.Info.Radius) continue;
                explosive.KillMessage();
                break;
            }
        }

        private string[] GetPlayerZoneIDs(BasePlayer player)
        {
            HashSet<Zone> zones;
            if (playerZones.TryGetValue(player, out zones))
                return zones.Select(x => x.Info.Id).ToArray();
            return null;
        }

        private bool EntityHasFlag(BaseEntity entity, string flagString)
        {
            var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
            HashSet<Zone> zones;

            if (entity is BasePlayer)
                playerZones.TryGetValue(entity as BasePlayer, out zones);
            else if (entity is BaseNpc)
                npcZones.TryGetValue(entity as BaseNpc, out zones);
            else if (entity.GetComponent<ResourceDispenser>())
                resourceZones.TryGetValue(entity.GetComponent<ResourceDispenser>(), out zones);
            else if (entity is BaseCombatEntity)
                buildingZones.TryGetValue(entity as BaseCombatEntity, out zones);
            else otherZones.TryGetValue(entity, out zones);

            if (zones != null)
            {
                foreach (var zone in zones)
                {
                    if (HasZoneFlag(zone, flag))
                        return true;
                }
            }
            return false;
        }
        #endregion

        #region Helpers
        private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return rotation * (point - pivot) + pivot;
        }

        private static float GetSkyHour()
        {
            return TOD_Sky.Instance.Cycle.Hour;
        }

        private static void CancelDamage(HitInfo hitinfo)
        {
            hitinfo.damageTypes = new DamageTypeList();
            hitinfo.DoHitEffects = false;
            hitinfo.HitMaterial = 0;
        }

        private void ShowZone(BasePlayer player, string zoneId)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return;
            if (targetZone.Info.Size != Vector3.zero)
            {
                var center = targetZone.Info.Location;
                var rotation = Quaternion.Euler(targetZone.Info.Rotation);
                var size = targetZone.Info.Size / 2;
                var point1 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), center, rotation);
                var point2 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), center, rotation);
                var point3 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), center, rotation);
                var point4 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), center, rotation);
                var point5 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), center, rotation);
                var point6 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), center, rotation);
                var point7 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), center, rotation);
                var point8 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), center, rotation);

                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point2);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point3);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point5);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point2);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point3);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point8);

                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point5, point6);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point5, point7);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point6, point2);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point8, point6);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point8, point7);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point7, point3);
            }
            else
                player.SendConsoleCommand("ddraw.sphere", 10f, Color.blue, targetZone.Info.Location, targetZone.Info.Radius);
        }

        private void SetupCollectibleEntity()
        {
            var collectibleEntities = Resources.FindObjectsOfTypeAll<CollectibleEntity>();
            for (var i = 0; i < collectibleEntities.Length; i++)
            {
                var collectibleEntity = collectibleEntities[i];
                var collider = collectibleEntity.GetComponent<Collider>() ?? collectibleEntity.gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
            }
        }

        private void CheckSpawnedEntity(BaseNetworkable entity)
        {
            if (entity == null || entity.IsDestroyed) return;
            HashSet<Zone> zones = null;
            ZoneFlags flag = ZoneFlags.None;

            if (entity is LootContainer || entity is JunkPile)
            {
                flag = ZoneFlags.NoLootSpawns;
                otherZones.TryGetValue(entity as BaseEntity, out zones);
            }
            else if (entity is BaseNpc)
            {
                flag = ZoneFlags.NoNPCSpawns;
                npcZones.TryGetValue(entity as BaseNpc, out zones);
            }

            if (flag == ZoneFlags.None || zones == null) return;
            foreach (var zone in zones)
            {
                if (HasZoneFlag(zone, flag))
                    entity.Kill(BaseNetworkable.DestroyMode.None);
            }
        }
        #endregion

        #region Entity Management
        private void OnPlayerEnterZone(Zone zone, BasePlayer player)
        {
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones))
                playerZones[player] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;
            UpdateFlags(player);

            if ((!string.IsNullOrEmpty(zone.Info.Permission) && !permission.UserHasPermission(player.UserIDString, zone.Info.Permission)) || (HasZoneFlag(zone, ZoneFlags.Eject) && !CanBypass(player, ZoneFlags.Eject) && !isAdmin(player)))
            {
                EjectPlayer(zone, player);
                SendMessage(player, msg("eject", player.UserIDString));
                return;
            }

            if (!string.IsNullOrEmpty(zone.Info.EnterMessage))
            {
                if (PopupNotifications != null && usePopups)
                    PopupNotifications.Call("CreatePopupNotification", string.Format(zone.Info.EnterMessage, player.displayName), player);
                else
                    SendMessage(player, zone.Info.EnterMessage, player.displayName);
            }
            
            Interface.Oxide.CallHook("OnEnterZone", zone.Info.Id, player);
            if (HasPlayerFlag(player, ZoneFlags.Kill))
            {
                if (CanBypass(player, ZoneFlags.Kill) || isAdmin(player)) return;
                SendMessage(player, msg("kill", player.UserIDString));
                player.Die();
            }
        }

        private void OnPlayerExitZone(Zone zone, BasePlayer player, bool all = false)
        {
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones)) return;
            if (!all)
            {
                zone.OnEntityKill(player);
                if (!zones.Remove(zone)) return;
                if (zones.Count <= 0) playerZones.Remove(player);
                if (!string.IsNullOrEmpty(zone.Info.LeaveMessage))
                {
                    if (PopupNotifications != null && usePopups)
                        PopupNotifications.Call("CreatePopupNotification", string.Format(zone.Info.LeaveMessage, player.displayName), player);
                    else
                        SendMessage(player, zone.Info.LeaveMessage, player.displayName);
                }
                if (zone.KeepInList.Contains(player.userID)) AttractPlayer(zone, player);
                Interface.Oxide.CallHook("OnExitZone", zone.Info.Id, player);
            }
            else
            {
                foreach (var zone1 in zones)
                {
                    if (!string.IsNullOrEmpty(zone1.Info.LeaveMessage))
                    {
                        if (PopupNotifications != null && usePopups)
                            PopupNotifications.Call("CreatePopupNotification", string.Format(zone1.Info.LeaveMessage, player.displayName), player);
                        else
                            SendMessage(player, zone1.Info.LeaveMessage, player.displayName);
                    }
                    if (zone1.KeepInList.Contains(player.userID)) AttractPlayer(zone1, player);
                    Interface.Oxide.CallHook("OnExitZone", zone1.Info.Id, player);
                }
                playerZones.Remove(player);
            }
            UpdateFlags(player);
        }

        private void OnResourceEnterZone(Zone zone, ResourceDispenser entity)
        {
            HashSet<Zone> zones;
            if (!resourceZones.TryGetValue(entity, out zones))
                resourceZones[entity] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;
        }

        private void OnResourceExitZone(Zone zone, ResourceDispenser resource, bool all = false)
        {
            HashSet<Zone> zones;
            if (!resourceZones.TryGetValue(resource, out zones)) return;
            if (!all)
            {
                if (!zones.Remove(zone)) return;
                if (zones.Count <= 0) resourceZones.Remove(resource);
            }
            else
                resourceZones.Remove(resource);
        }

        private void OnNpcEnterZone(Zone zone, BaseNpc entity)
        {
            HashSet<Zone> zones;
            if (!npcZones.TryGetValue(entity, out zones))
                npcZones[entity] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;

            if (HasZoneFlag(zone, ZoneFlags.NpcFreeze))            
                entity.CancelInvoke(new Action(entity.TickAi)); 
        }

        private void OnNpcExitZone(Zone zone, BaseNpc entity, bool all = false)
        {
            HashSet<Zone> zones;
            if (!npcZones.TryGetValue(entity, out zones)) return;
            if (!all)
            {
                if (!zones.Remove(zone)) return;
                if (zones.Count <= 0) npcZones.Remove(entity);
            }
            else 
            {
                foreach (var zone1 in zones)
                {
                    if (!HasZoneFlag(zone1, ZoneFlags.NpcFreeze)) continue;
                    entity.InvokeRandomized(new Action(entity.TickAi), 0.1f, 0.1f, 0.00500000035f);                   
                }
                npcZones.Remove(entity);
            }
        }
        private void OnBuildingEnterZone(Zone zone, BaseCombatEntity entity)
        {
            HashSet<Zone> zones;
            if (!buildingZones.TryGetValue(entity, out zones))
                buildingZones[entity] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;
            if (HasZoneFlag(zone, ZoneFlags.NoStability) && !CanBypass(entity.OwnerID, ZoneFlags.NoStability))
            {
                var block = entity as StabilityEntity;
                if (block != null) block.grounded = true;
            }
            if (HasZoneFlag(zone, ZoneFlags.NoPickup) && !CanBypass(entity.OwnerID, ZoneFlags.NoPickup))
            {
                var door = entity as Door;
                if (door == null) return;
                door.pickup.enabled = false;
            }
        }

        private void OnBuildingExitZone(Zone zone, BaseCombatEntity entity, bool all = false)
        {
            HashSet<Zone> zones;
            if (!buildingZones.TryGetValue(entity, out zones)) return;
            var stability = false;
            var pickup = false;
            if (!all)
            {
                zone.OnEntityKill(entity);
                if (!zones.Remove(zone)) return;
                stability = HasZoneFlag(zone, ZoneFlags.NoStability);
                pickup = HasZoneFlag(zone, ZoneFlags.NoPickup);
                if (zones.Count <= 0) buildingZones.Remove(entity);
            }
            else
            {
                foreach (var zone1 in zones)
                {
                    zone1.OnEntityKill(entity);
                    stability |= HasZoneFlag(zone1, ZoneFlags.NoStability);
                    pickup |= HasZoneFlag(zone1, ZoneFlags.NoPickup);
                }
                buildingZones.Remove(entity);
            }
            if (stability)
            {
                var block = entity as StabilityEntity;
                if (block == null) return;
                var prefab = GameManager.server.FindPrefab(PrefabAttribute.server.Find<Construction>(block.prefabID).fullName);
                block.grounded = prefab?.GetComponent<StabilityEntity>()?.grounded ?? false;
            }
            if (pickup)
            {
                var door = entity as Door;
                if (door == null) return;
                var prefab = GameManager.server.FindPrefab(PrefabAttribute.server.Find<Construction>(door.prefabID).fullName);
                door.pickup.enabled = prefab?.GetComponent<Door>()?.pickup.enabled ?? true;
            }
        }

        private void OnOtherEnterZone(Zone zone, BaseEntity entity)
        {
            HashSet<Zone> zones;
            if (!otherZones.TryGetValue(entity, out zones))
                otherZones[entity] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;
            var collectible = entity as CollectibleEntity;
            if (collectible != null && HasZoneFlag(zone, ZoneFlags.NoCollect))
            {
                collectible.itemList = null;
            }
            var worldItem = entity as WorldItem;
            if (worldItem != null)
            {
                if (HasZoneFlag(zone, ZoneFlags.NoDrop))
                    timer.Once(2f, () =>
                    {
                        if (worldItem.IsDestroyed) return;
                        worldItem.KillMessage();
                    });
                else if (HasZoneFlag(zone, ZoneFlags.NoPickup))
                    worldItem.allowPickup = false;
            }
        }

        private void OnOtherExitZone(Zone zone, BaseEntity entity, bool all = false)
        {
            HashSet<Zone> zones;
            if (!otherZones.TryGetValue(entity, out zones)) return;
            var pickup = false;
            var collect = false;
            if (!all)
            {
                if (!zones.Remove(zone)) return;
                pickup = HasZoneFlag(zone, ZoneFlags.NoPickup);
                collect = HasZoneFlag(zone, ZoneFlags.NoCollect);
                if (zones.Count <= 0) otherZones.Remove(entity);
            }
            else
            {
                foreach (var zone1 in zones)
                {
                    pickup |= HasZoneFlag(zone1, ZoneFlags.NoPickup);
                    collect |= HasZoneFlag(zone1, ZoneFlags.NoCollect);
                }
                otherZones.Remove(entity);
            }
            if (collect)
            {
                var collectible = entity as CollectibleEntity;
                if (collectible != null && collectible.itemList == null)
                    collectible.itemList = GameManager.server.FindPrefab(entity).GetComponent<CollectibleEntity>().itemList;
            }
            if (pickup)
            {
                var worldItem = entity as WorldItem;
                if (worldItem != null && !worldItem.allowPickup)
                    worldItem.allowPickup = GameManager.server.FindPrefab(entity).GetComponent<WorldItem>().allowPickup;
            }
        }        
        #endregion

        #region Player Management
        private void EjectPlayer(Zone zone, BasePlayer player)
        {
            if (zone.WhiteList.Contains(player.userID) || zone.KeepInList.Contains(player.userID)) return;
            Vector3 newPos = Vector3.zero;
            if (!string.IsNullOrEmpty(zone.Info.EjectSpawns) && Spawns)
            {
                object success = Spawns.Call("GetRandomSpawn", zone.Info.EjectSpawns);
                if (success is Vector3)               
                    newPos = (Vector3)success;
            }
            if (newPos == Vector3.zero)
            {
                float dist;
                if (zone.Info.Size != Vector3.zero)
                    dist = zone.Info.Size.x > zone.Info.Size.z ? zone.Info.Size.x : zone.Info.Size.z;
                else
                    dist = zone.Info.Radius;
                newPos = zone.transform.position + (player.transform.position - zone.transform.position).normalized * (dist + 5f);
                newPos.y = TerrainMeta.HeightMap.GetHeight(newPos);
            }
            player.MovePosition(newPos);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
            player.SendNetworkUpdateImmediate();
        }

        private void AttractPlayer(Zone zone, BasePlayer player)
        {
            float dist;
            if (zone.Info.Size != Vector3.zero)
                dist = zone.Info.Size.x > zone.Info.Size.z ? zone.Info.Size.x : zone.Info.Size.z;
            else
                dist = zone.Info.Radius;
            var newPos = zone.transform.position + (player.transform.position - zone.transform.position).normalized * (dist - 5f);
            newPos.y = TerrainMeta.HeightMap.GetHeight(newPos);
            player.MovePosition(newPos);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
            player.SendNetworkUpdateImmediate();
        }

        private bool isAdmin(BasePlayer player)
        {
            if (player?.net?.connection == null) return true;
            return player.net.connection.authLevel > 0;
        }

        private bool HasPermission(BasePlayer player, string permname)
        {
            return isAdmin(player) || permission.UserHasPermission(player.UserIDString, permname);
        }
        private bool CanBypass(object player, ZoneFlags flag) => permission.UserHasPermission(player is BasePlayer ? (player as BasePlayer).UserIDString : player.ToString(), permIgnoreFlag + flag);
        #endregion

        #region Commands
        [ChatCommand("zone_add")]
        private void cmdChatZoneAdd(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone)) { SendMessage(player, "You don't have access to this command"); return; }
            var newzoneinfo = new ZoneDefinition(player.transform.position) { Id = UnityEngine.Random.Range(1, 99999999).ToString() };
            NewZone(newzoneinfo);
            if (ZoneDefinitions.ContainsKey(newzoneinfo.Id)) storedData.ZoneDefinitions.Remove(ZoneDefinitions[newzoneinfo.Id]);
            ZoneDefinitions[newzoneinfo.Id] = newzoneinfo;
            LastZone[player.userID] = newzoneinfo.Id;
            storedData.ZoneDefinitions.Add(newzoneinfo);
            SaveData();
            ShowZone(player, newzoneinfo.Id);
            SendMessage(player, "New Zone created, you may now edit it: " + newzoneinfo.Location);
        }
        [ChatCommand("zone_reset")]
        private void cmdChatZoneReset(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone)) { SendMessage(player, "You don't have access to this command"); return; }
            ZoneDefinitions.Clear();
            storedData.ZoneDefinitions.Clear();
            SaveData();
            Unload();
            SendMessage(player, "All Zones were removed");
        }
        [ChatCommand("zone_remove")]
        private void cmdChatZoneRemove(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone)) { SendMessage(player, "You don't have access to this command"); return; }
            if (args.Length == 0) { SendMessage(player, "/zone_remove XXXXXID"); return; }
            ZoneDefinition zoneDef;
            if (!ZoneDefinitions.TryGetValue(args[0], out zoneDef)) { SendMessage(player, "This zone doesn't exist"); return; }
            storedData.ZoneDefinitions.Remove(zoneDef);
            ZoneDefinitions.Remove(args[0]);
            SaveData();
            RefreshZone(args[0]);
            SendMessage(player, "Zone " + args[0] + " was removed");
        }
        [ChatCommand("zone_stats")]
        private void cmdChatZoneStats(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone)) { SendMessage(player, "You don't have access to this command"); return; }

            SendMessage(player, "Players: {0}", playerZones.Count);
            SendMessage(player, "Buildings: {0}", buildingZones.Count);
            SendMessage(player, "Npcs: {0}", npcZones.Count);
            SendMessage(player, "Resources: {0}", resourceZones.Count);
            SendMessage(player, "Others: {0}", otherZones.Count);
        }
        [ChatCommand("zone_edit")]
        private void cmdChatZoneEdit(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone)) { SendMessage(player, "You don't have access to this command"); return; }
            string zoneId;
            if (args.Length == 0)
            {
                HashSet<Zone> zones;
                if (!playerZones.TryGetValue(player, out zones) || zones.Count != 1)
                {
                    SendMessage(player, "/zone_edit XXXXXID");
                    return;
                }
                zoneId = zones.First().Info.Id;
            }
            else
                zoneId = args[0];
            if (!ZoneDefinitions.ContainsKey(zoneId)) { SendMessage(player, "This zone doesn't exist"); return; }
            LastZone[player.userID] = zoneId;
            SendMessage(player, "Editing zone ID: " + zoneId);
            ShowZone(player, zoneId);
        }
        [ChatCommand("zone_player")]
        private void cmdChatZonePlayer(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone)) { SendMessage(player, "You don't have access to this command"); return; }
            var targetPlayer = player;
            if (args != null && args.Length > 0)
            {
                targetPlayer = FindPlayer(args[0]);
                if (targetPlayer == null)
                {
                    SendMessage(player, "Player not found");
                    return;
                }
            }
            ZoneFlags tags;
            playerTags.TryGetValue(targetPlayer, out tags);
            SendMessage(player, $"=== {targetPlayer.displayName} ===");
            SendMessage(player, $"Flags: {tags}");
            SendMessage(player, "========== Zone list ==========");
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(targetPlayer, out zones) || zones.Count == 0) { SendMessage(player, "empty"); return; }
            foreach (var zone in zones)
                SendMessage(player, $"{zone.Info.Id}: {zone.Info.Name} - {zone.Info.Location}");
            UpdateFlags(targetPlayer);
        }
        [ChatCommand("zone_list")]
        private void cmdChatZoneList(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone)) { SendMessage(player, "You don't have access to this command"); return; }
            SendMessage(player, "========== Zone list ==========");
            if (ZoneDefinitions.Count == 0) { SendMessage(player, "empty"); return; }
            foreach (var pair in ZoneDefinitions)
                SendMessage(player, $"{pair.Key}: {pair.Value.Name} - {pair.Value.Location}");
        }
        [ChatCommand("zone")]
        private void cmdChatZone(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone)) { SendMessage(player, "You don't have access to this command"); return; }
            string zoneId;
            if (!LastZone.TryGetValue(player.userID, out zoneId)) { SendMessage(player, "You must first say: /zone_edit XXXXXID"); return; }

            var zoneDefinition = ZoneDefinitions[zoneId];
            if (args.Length < 1)
            {
                SendMessage(player, "/zone <option/flag> <value>");
                SendReply(player, $"<color={prefixColor}>Zone Name:</color> {zoneDefinition.Name}");
                SendReply(player, $"<color={prefixColor}>Zone Enabled:</color> {zoneDefinition.Enabled}");
                SendReply(player, $"<color={prefixColor}>Zone ID:</color> {zoneDefinition.Id}");
                SendReply(player, $"<color={prefixColor}>Comfort:</color> {zoneDefinition.Comfort}");
                SendReply(player, $"<color={prefixColor}>Radiation:</color> {zoneDefinition.Radiation}");
                SendReply(player, $"<color={prefixColor}>Radius:</color> {zoneDefinition.Radius}");
                SendReply(player, $"<color={prefixColor}>Location:</color> {zoneDefinition.Location}");
                SendReply(player, $"<color={prefixColor}>Size:</color> {zoneDefinition.Size}");
                SendReply(player, $"<color={prefixColor}>Rotation:</color> {zoneDefinition.Rotation}");
                SendReply(player, $"<color={prefixColor}>Enter Message:</color> {zoneDefinition.EnterMessage}");
                SendReply(player, $"<color={prefixColor}>Leave Message:</color> {zoneDefinition.LeaveMessage}");
                SendReply(player, $"<color={prefixColor}>Permission:</color> {zoneDefinition.Permission}");
                SendReply(player, $"<color={prefixColor}>Eject Spawnfile:</color> {zoneDefinition.EjectSpawns}");
                SendReply(player, $"<color={prefixColor}>Flags:</color> {zoneDefinition.Flags}");               
                ShowZone(player, zoneId);
                return;
            }
            if (args.Length % 2 != 0) { SendMessage(player, "Value missing..."); return; }
            UpdateZoneDefinition(zoneDefinition, args, player);
            RefreshZone(zoneId);
            SaveData();
            ShowZone(player, zoneId);
        }

        [ConsoleCommand("zone")]
        private void ccmdZone(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!HasPermission(player, permZone)) { SendMessage(player, "You don't have access to this command"); return; }
            var zoneId = arg.GetString(0);
            ZoneDefinition zoneDefinition;
            if (!arg.HasArgs(3) || !ZoneDefinitions.TryGetValue(zoneId, out zoneDefinition)) { SendMessage(player, "Zone ID not found or too few arguments supplied: zone <zoneid> <arg> <value>"); return; }

            var args = new string[arg.Args.Length - 1];
            Array.Copy(arg.Args, 1, args, 0, args.Length);
            UpdateZoneDefinition(zoneDefinition, args, player);
            RefreshZone(zoneId);            
        }
        private void SendMessage(BasePlayer player, string message, params object[] args)
        {
            if (player != null)
            {
                if (args.Length > 0) message = string.Format(message, args);
                SendReply(player, $"<color={prefixColor}>{prefix}</color>{message}");
            }
            else
                Puts(message);
        }
        #endregion

        #region Localization
        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["noBuild"] = "You are not allowed to build in this area!",
            ["noUpgrade"] = "You are not allowed to upgrade structures in this area!",
            ["noDeploy"] = "You are not allowed to deploy items in this area!",
            ["noCup"] = "You are not allowed to deploy cupboards in this area!",
            ["noChat"] = "You are not allowed to chat in this area!",
            ["noSuicide"] = "You are not allowed to suicide in this area!",
            ["noGather"] = "You are not allowed to gather in this area!",
            ["noLoot"] = "You are not allowed loot in this area!",
            ["noSignUpdates"] = "You can not update signs in this area!",
            ["noOvenToggle"] = "You can not toggle ovens and lights in this area!",
            ["noPickup"] = "You can not pick up objects in this area!",
            ["noVending"] = "You can not use vending machines in this area!",
            ["noStash"] = "You can not hide a stash in this area!",
            ["noCraft"] = "You can not craft in this area!",
            ["eject"] = "You are not allowed in this area!",
            ["kill"] = "Access to this area is restricted!"
        };
        #endregion

        #region Vector3 Json Converter
        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                var o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }
        #endregion
    }
}
