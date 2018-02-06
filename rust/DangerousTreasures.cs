using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Dangerous Treasures", "nivex", "1.1.3", ResourceId = 2479)]
    [Description("Event with treasure chests.")]
    public class DangerousTreasures : RustPlugin
    {
        [PluginReference] Plugin LustyMap, ZoneManager, Economics, ServerRewards, Map, GUIAnnouncements;

        static DangerousTreasures ins;
        static bool unloading = false;
        bool init = false;
        bool wipeChestsSeed = false;
        SpawnFilter filter = new SpawnFilter();
        ItemDefinition boxDef;
        static ItemDefinition rocketDef;
        const string boxShortname = "box.wooden.large";
        const string fireRocketShortname = "ammo.rocket.fire";
        const string basicRocketShortname = "ammo.rocket.basic";
        const string boxPrefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        const string spherePrefab = "assets/prefabs/visualization/sphere.prefab";
        const string fireballPrefab = "assets/bundled/prefabs/oilfireballsmall.prefab";
        static string rocketResourcePath;
        DynamicConfigFile dataFile;
        StoredData storedData = new StoredData();

        static int playerMask = LayerMask.GetMask("Player (Server)");
        static int blockedMask = LayerMask.GetMask(new[] { "Player (Server)", "Trigger", "Prevent Building" });
        static int heightMask = LayerMask.GetMask(new[] { "Terrain", "World", "Default", "Construction", "Deployed" });
        static int twdMask = LayerMask.GetMask(new[] { "Terrain", "World", "Default" });
        static int twwMask = LayerMask.GetMask(new[] { "Terrain", "World", "Water" });
        static int worldMask = LayerMask.GetMask("World");
        List<int> BlockedLayers = new List<int> { (int)Layer.Water, (int)Layer.Construction, (int)Layer.Trigger, (int)Layer.Prevent_Building, (int)Layer.Deployed, (int)Layer.Tree };

        List<Vector3> monuments = new List<Vector3>(); // positions of monuments on the server
        static List<uint> newmanProtections = new List<uint>();
        static List<FireBall> spawnedFireballs = new List<FireBall>();
        List<ulong> indestructibleWarnings = new List<ulong>(); // indestructible messages limited to once every 10 seconds
        List<ulong> drawGrants = new List<ulong>(); // limit draw to once every 15 seconds by default
        Dictionary<Vector3, float> managedZones = new Dictionary<Vector3, float>();

        static Dictionary<uint, MapInfo> mapMarkers = new Dictionary<uint, MapInfo>();
        static Dictionary<uint, string> lustyMarkers = new Dictionary<uint, string>();
        Dictionary<string, List<ulong>> skinsCache = new Dictionary<string, List<ulong>>();
        Dictionary<string, List<ulong>> workshopskinsCache = new Dictionary<string, List<ulong>>();
        static Dictionary<uint, TreasureChest> treasureChests = new Dictionary<uint, TreasureChest>();
        static Dictionary<uint, string> treasureLooters = new Dictionary<uint, string>();
        static Dictionary<uint, List<NPCPlayer>> npcs = new Dictionary<uint, List<NPCPlayer>>();

        class MapInfo
        {
            public string Url;
            public string IconName;
            public Vector3 Position;

            public MapInfo() { }
        }

        class TreasureItem
        {
            public object shortname { get; set; } = string.Empty;
            public object amount { get; set; } = 0;
            public object skin { get; set; } = 0uL;
            public TreasureItem() { }
        }

        class PlayerInfo
        {
            public int StolenChestsTotal { get; set; } = 0;
            public int StolenChestsSeed { get; set; } = 0;
            public PlayerInfo() { }
        }

        class StoredData
        {
            public double SecondsUntilEvent = double.MinValue;
            public int TotalEvents = 0;
            public readonly Dictionary<string, PlayerInfo> Players = new Dictionary<string, PlayerInfo>();
            public List<uint> Markers = new List<uint>();
            public StoredData() { }
        }

        class GuidanceSystem : MonoBehaviour
        {
            TimedExplosive missile;
            ServerProjectile projectile;
            BaseEntity target;
            float rocketSpeedMulti = 2f;
            Timer launch;
            Vector3 launchPos;
            List<ulong> exclude = new List<ulong>();

            void Awake()
            {
                missile = GetComponent<TimedExplosive>();
                projectile = missile.GetComponent<ServerProjectile>();

                launchPos = missile.transform.position;
                launchPos.y = TerrainMeta.HeightMap.GetHeight(launchPos);

                projectile.gravityModifier = 0f;
                projectile.speed = 5f;
                projectile.InitializeVelocity(Vector3.up);

                missile.explosionRadius = 0f;
                missile.timerAmountMin = timeUntilExplode;
                missile.timerAmountMax = timeUntilExplode;
                missile.damageTypes = new List<DamageTypeEntry>(); // no damage
                missile.Spawn();

                if (!missile)
                    return;

                launch = ins.timer.Once(targettingTime, () =>
                {
                    if (!missile || missile.IsDestroyed)
                        return;

                    var list = new List<BaseEntity>();
                    var colliders = Pool.GetList<Collider>();
                    Vis.Colliders(launchPos, eventRadius + missileDetectionDistance, colliders, playerMask, QueryTriggerInteraction.Collide);

                    foreach (var collider in colliders)
                    {
                        var player = collider.GetComponentInParent<BasePlayer>();

                        if (!player || player is NPCPlayer || !player.CanInteract())
                            continue;

                        if (ignoreFlying && player.IsFlying)
                            continue;

                        if (exclude.Contains(player.userID) || newmanProtections.Contains(player.net.ID))
                            continue;

                        list.Add(player); // acquire a player target 
                    }

                    Pool.FreeList<Collider>(ref colliders);

                    if (list.Count > 0)
                    {
                        SetTarget(list.GetRandom()); // pick a random player
                        list.Clear();
                    }
                    else if (!targetChest)
                    {
                        missile.Kill();
                        return;
                    }

                    projectile.speed = rocketSpeed * rocketSpeedMulti;
                    InvokeHandler.InvokeRepeating(this, GuideMissile, 0.1f, 0.1f);
                });
            }

            public void SetTarget(BaseEntity target)
            {
                this.target = target;
            }

            public void Exclude(List<ulong> list)
            {
                if (list != null && list.Count > 0)
                {
                    exclude.Clear();
                    exclude.AddRange(list);
                }
            }

            void GuideMissile()
            {
                if (!target || !missile || missile.IsDestroyed)
                    return;

                if (target.IsDestroyed)
                {
                    if (missile != null && !missile.IsDestroyed)
                        missile.Kill();

                    return;
                }

                if (Vector3.Distance(target.transform.position, missile.transform.position) <= 1f)
                {
                    missile.Explode();
                    return;
                }

                var direction = (target.transform.position - missile.transform.position) + Vector3.down; // direction to guide the missile
                projectile.InitializeVelocity(direction); // guide the missile to the target's position
            }

            void OnDestroy()
            {
                exclude.Clear();
                launch?.Destroy();
                InvokeHandler.CancelInvoke(this, GuideMissile);
                GameObject.Destroy(this);
            }
        }

        class TreasureChest : MonoBehaviour
        {
            public StorageContainer container;
            public bool started = false;
            long _unlockTime;
            bool lerped = false;
            public Vector3 containerPos;
            uint uid;
            int countdownTime;
            float posMulti = 3f;
            float sphereMulti = 2f;
            float claimTime;
            float lastStayTick;
            Vector3 lastFirePos;

            Dictionary<ulong, long> fireticks = new Dictionary<ulong, long>();
            List<FireBall> fireballs = new List<FireBall>();
            List<ulong> players = new List<ulong>();
            List<ulong> newmans = new List<ulong>();
            List<ulong> traitors = new List<ulong>();
            List<uint> protects = new List<uint>();
            List<TimedExplosive> missiles = new List<TimedExplosive>();
            List<int> times = new List<int>();
            List<SphereEntity> spheres = new List<SphereEntity>();
            List<Vector3> missilePositions = new List<Vector3>();
            List<Vector3> firePositions = new List<Vector3>();
            Timer destruct, unlock, countdown, announcement;

            void Awake()
            {
                container = GetComponent<StorageContainer>();
                containerPos = container.transform.position;
                uid = container.net.ID;

                gameObject.layer = (int)Layer.Reserved1;
                var collider = gameObject.GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
                collider.center = Vector3.zero;
                collider.radius = eventRadius;
                collider.isTrigger = true;
                collider.enabled = true;

                lastStayTick = Time.time;

                if (useSpheres && amountOfSpheres > 0)
                {
                    for (int i = 0; i < amountOfSpheres; i++)
                    {
                        if (!useSpheres)
                            break;

                        var sphere = GameManager.server.CreateEntity(spherePrefab, containerPos, new Quaternion(), true) as SphereEntity;

                        if (sphere != null)
                        {
                            sphere.currentRadius = 1f;
                            sphere.Spawn();
                            sphere.LerpRadiusTo(eventRadius * sphereMulti, 5f);
                            spheres.Add(sphere);
                        }
                        else
                        {
                            ins.Puts(ins.msg(szInvalidConstant, null, spherePrefab));
                            useSpheres = false;
                            lerped = true; // bug fix for prefab being invalid, otherwise event will not continue
                        }
                    }

                    if (useSpheres)
                        InvokeHandler.InvokeRepeating(this, LerpUpdate, 0.1f, 0.1f);
                }
                else
                    lerped = true;

                if (useRocketOpener)
                {
                    var positions = GetRandomPositions(containerPos, eventRadius * posMulti, numRockets, 0f);

                    foreach (var position in positions)
                        CreateRocket(position, containerPos, Vector3.down);

                    positions.Clear();
                }

                if (useFireballs)
                {
                    firePositions = GetRandomPositions(containerPos, eventRadius, 25, containerPos.y + 25f);

                    if (firePositions.Count > 0)
                        InvokeHandler.InvokeRepeating(this, SpawnFire, 0.1f, secondsBeforeTick);
                }

                if (launchMissiles)
                {
                    missilePositions = GetRandomPositions(containerPos, eventRadius, 25, 1);

                    if (missilePositions.Count > 0)
                    {
                        InvokeHandler.InvokeRepeating(this, LaunchMissile, 0.1f, missileFrequency);
                        LaunchMissile();
                    }
                }
            }

            void LerpUpdate()
            {
                foreach (var sphere in spheres)
                {
                    if (sphere != null && sphere.currentRadius == sphere.lerpRadius)
                    {
                        InvokeHandler.CancelInvoke(this, LerpUpdate);
                        lerped = true;
                        break;
                    }
                }
            }

            void OnTriggerEnter(Collider col)
            {
                if (started)
                    return;

                var player = col.GetComponentInParent<BasePlayer>();

                if (!player || player is NPCPlayer || players.Contains(player.userID))
                    return;

                player.ChatMessage(ins.msg(useFireballs ? szProtected : szUnprotected, player.UserIDString));
                players.Add(player.userID);
            }

            void OnTriggerExit(Collider col)
            {
                if (!newmanProtect)
                    return;

                var player = col.GetComponentInParent<BasePlayer>();

                if (!player)
                    return;

                if (protects.Contains(player.net.ID))
                {
                    if (newmanProtections.Contains(player.net.ID))
                        newmanProtections.Remove(player.net.ID);

                    protects.Remove(player.net.ID);
                    player.ChatMessage(ins.msg(szNewmanProtectFade, player.UserIDString));
                }
            }

            void OnTriggerStay(Collider col)
            {
                if (!useFireballs && !newmanMode && !newmanProtect)
                    return;

                if (started || !lerped || Time.time - lastStayTick < 0.1f)
                    return;

                lastStayTick = Time.time;

                var player = col.GetComponentInParent<BasePlayer>();

                if (!player || player is NPCPlayer)
                    return;

                if (newmanMode || newmanProtect)
                {
                    int count = player.inventory.AllItems().Where(item => item.info.shortname != "torch" && item.info.shortname != "rock" && player.IsHostileItem(item))?.Count() ?? 0;

                    if (count == 0)
                    {
                        if (newmanMode && !newmans.Contains(player.userID) && !traitors.Contains(player.userID))
                        {
                            player.ChatMessage(ins.msg(szNewmanEnter, player.UserIDString));
                            newmans.Add(player.userID);
                        }

                        if (newmanProtect && !newmanProtections.Contains(player.net.ID) && !protects.Contains(player.net.ID) && !traitors.Contains(player.userID))
                        {
                            player.ChatMessage(ins.msg(szNewmanProtect, player.UserIDString));
                            newmanProtections.Add(player.net.ID);
                            protects.Add(player.net.ID);
                        }

                        if (!traitors.Contains(player.userID))
                            return;
                    }

                    if (newmans.Contains(player.userID))
                    {
                        player.ChatMessage(ins.msg(useFireballs ? szNewmanTraitorBurn : szNewmanTraitor, player.UserIDString));
                        newmans.Remove(player.userID);

                        if (!traitors.Contains(player.userID))
                            traitors.Add(player.userID);

                        if (newmanProtections.Contains(player.net.ID))
                            newmanProtections.Remove(player.net.ID);

                        if (protects.Contains(player.net.ID))
                            protects.Remove(player.net.ID);
                    }
                }

                if (!useFireballs)
                    return;

                var stamp = TimeStamp();

                if (!fireticks.ContainsKey(player.userID))
                    fireticks[player.userID] = stamp + secondsBeforeTick;

                if (fireticks[player.userID] - stamp <= 0)
                {
                    fireticks[player.userID] = stamp + secondsBeforeTick;
                    SpawnFire(player.transform.position);
                }
            }

            void KillNpc()
            {
                foreach (var entry in npcs.ToList())
                {
                    if (entry.Key == uid)
                    {
                        foreach (var npc in entry.Value.ToList())
                        {
                            if (npc != null && !npc.IsDestroyed)
                            {
                                npc.Kill();
                            }
                        }

                        npcs.Remove(entry.Key);
                    }
                }
            }

            void OnDestroy()
            {
                ins.RemoveLustyMarker(uid);
                ins.RemoveMapMarker(uid);
                DestroyLauncher();
                DestroySphere();
                DestroyFire();
                fireticks?.Clear();
                players?.Clear();
                times?.Clear();
                unlock?.Destroy();
                destruct?.Destroy();
                countdown?.Destroy();
                announcement?.Destroy();
                KillNpc();

                if (treasureChests.ContainsKey(uid))
                {
                    treasureChests.Remove(uid);

                    if (!unloading && treasureChests.Count == 0) // 0.1.21 - nre fix
                    {
                        ins.SubscribeHooks(false);
                    }
                }

                GameObject.Destroy(this);
            }

            public void LaunchMissile()
            {
                if (string.IsNullOrEmpty(rocketResourcePath))
                    launchMissiles = false;

                if (!launchMissiles)
                {
                    DestroyLauncher();
                    return;
                }

                if (!lerped)
                    return;

                var missilePos = missilePositions.GetRandom();
                float y = TerrainMeta.HeightMap.GetHeight(missilePos) + 15f;
                missilePos.y = 200f;

                RaycastHit hit;
                if (Physics.Raycast(missilePos, Vector3.down, out hit, heightMask)) // don't want the missile to explode before it leaves its spawn location
                    missilePos.y = Mathf.Max(hit.point.y, y);

                var missile = GameManager.server.CreateEntity(rocketResourcePath, missilePos, new Quaternion(), true) as TimedExplosive;

                if (!missile)
                {
                    launchMissiles = false;
                    return;
                }

                missiles.Add(missile);

                foreach (var entry in missiles.ToList())
                    if (entry == null || entry.IsDestroyed)
                        missiles.Remove(entry);

                var gs = missile.gameObject.AddComponent<GuidanceSystem>();

                gs.Exclude(newmans);
                gs.SetTarget(container);
            }

            void SpawnFire()
            {
                var firePos = firePositions.GetRandom();
                int retries = firePositions.Count;

                while (Vector3.Distance(firePos, lastFirePos) < eventRadius * 0.35f && --retries > 0)
                {
                    firePos = firePositions.GetRandom();
                }

                SpawnFire(firePos);
                lastFirePos = firePos;
            }

            void SpawnFire(Vector3 firePos)
            {
                if (!useFireballs || !lerped)
                    return;

                if (fireballs.Count >= 6) // limit fireballs
                {
                    foreach (var entry in fireballs)
                    {
                        if (entry != null && !entry.IsDestroyed)
                            entry.Kill();

                        fireballs.Remove(entry);

                        if (spawnedFireballs.Contains(entry))
                            spawnedFireballs.Remove(entry);

                        break;
                    }
                }

                var fireball = GameManager.server.CreateEntity(fireballPrefab, firePos, new Quaternion(), true) as FireBall;

                if (fireball == null)
                {
                    ins.Puts(ins.msg(szInvalidConstant, null, fireballPrefab));
                    useFireballs = false;

                    if (firePositions.Count > 0)
                    {
                        InvokeHandler.CancelInvoke(this, SpawnFire);
                        firePositions.Clear();
                    }

                    return;
                }

                fireball.Spawn();
                fireballs.Add(fireball);
            }

            public void Destruct()
            {
                unlock?.Destroy();
                destruct?.Destroy();

                if (container != null)
                {
                    container.inventory?.Clear();

                    if (!container.IsDestroyed)
                        container.Kill();
                }
            }

            void Unclaimed()
            {
                if (!started)
                    return;

                float time = claimTime - TimeStamp();

                if (time < 60f)
                    return;

                string eventPos = FormatGridReference(containerPos);

                foreach (var target in BasePlayer.activePlayerList)
                    target.ChatMessage(ins.msg(szDestroyingTreasure, target.UserIDString, eventPos, ins.FormatTime(time, target.UserIDString), szDistanceChatCommand));
            }

            public long UnlockTime
            {
                get
                {
                    return this._unlockTime;
                }
            }

            public void SetUnlockTime(float time)
            {
                var posStr = FormatGridReference(containerPos);
                countdownTime = Convert.ToInt32(time);
                _unlockTime = Convert.ToInt64(TimeStamp() + time);

                unlock = ins.timer.Once(time, () =>
                {
                    if (container.HasFlag(BaseEntity.Flags.Locked))
                        container.SetFlag(BaseEntity.Flags.Locked, false);

                    if (container.HasFlag(BaseEntity.Flags.OnFire))
                        container.SetFlag(BaseEntity.Flags.OnFire, false);

                    if (showStarted)
                        foreach (var target in BasePlayer.activePlayerList)
                            SendDangerousMessage(target, containerPos, ins.msg(szEventStarted, target.UserIDString, posStr));

                    if (destructTime > 0f)
                        destruct = ins.timer.Once(destructTime, () => Destruct());

                    DestroyFire();
                    DestroySphere();
                    DestroyLauncher();
                    started = true;

                    if (useUnclaimedAnnouncements)
                    {
                        claimTime = TimeStamp() + destructTime;
                        announcement = ins.timer.Repeat(unclaimedInterval * 60f, 0, () => Unclaimed());
                    }
                });

                if (useCountdown && countdownTimes.Count > 0)
                {
                    if (times.Count == 0)
                        times.AddRange(countdownTimes);

                    countdown = ins.timer.Repeat(1f, 0, () =>
                    {
                        countdownTime--;

                        if (started || times.Count == 0)
                        {
                            countdown?.Destroy();
                            return;
                        }

                        if (times.Contains(countdownTime))
                        {
                            string eventPos = FormatGridReference(containerPos);

                            foreach (var target in BasePlayer.activePlayerList)
                                SendDangerousMessage(target, containerPos, ins.msg(szEventCountdown, target.UserIDString, eventPos, ins.FormatTime(countdownTime, target.UserIDString)));

                            times.Remove(countdownTime);
                        }
                    });
                }
            }

            public void DestroyLauncher()
            {
                if (missilePositions.Count > 0)
                {
                    InvokeHandler.CancelInvoke(this, LaunchMissile);
                    missilePositions.Clear();
                }

                if (missiles.Count > 0)
                {
                    foreach (var entry in missiles)
                        if (entry != null && !entry.IsDestroyed)
                            entry.Kill();

                    missiles.Clear();
                }
            }

            public void DestroySphere()
            {
                foreach (var sphere in spheres)
                    if (sphere != null && !sphere.IsDestroyed)
                        sphere.Kill();
            }

            public void DestroyFire()
            {
                if (firePositions.Count > 0)
                {
                    InvokeHandler.CancelInvoke(this, SpawnFire);
                    firePositions.Clear();
                }

                if (useFireballs)
                {
                    foreach (var fireball in fireballs)
                    {
                        if (fireball != null && !fireball.IsDestroyed)
                            fireball.Kill();

                        if (spawnedFireballs.Contains(fireball))
                            spawnedFireballs.Remove(fireball);
                    }

                    fireballs.Clear();
                }

                foreach (var protect in protects)
                    if (newmanProtections.Contains(protect))
                        newmanProtections.Remove(protect);

                traitors.Clear();
                newmans.Clear();
                protects.Clear();
            }
        }

        void OnNewSave(string filename) => wipeChestsSeed = true;

        void Init()
        {
            SubscribeHooks(false);
        }

        void OnServerInitialized()
        {
            if (init)
                return;

            ins = this;
            dataFile = Interface.Oxide.DataFileSystem.GetFile(Title.Replace(" ", ""));

            try
            {
                storedData = dataFile.ReadObject<StoredData>();
            }
            catch { }

            if (storedData?.Players == null)
                storedData = new StoredData();

            LoadVariables();

            if (chestLoot.Count == 0)
            {
                Puts(msg(szUnloading, null, "Treasure"));
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            if (automatedEvents)
            {
                if (storedData.SecondsUntilEvent != double.MinValue)
                    if (storedData.SecondsUntilEvent - TimeStamp() > eventIntervalMax) // Allows users to lower max event time
                        storedData.SecondsUntilEvent = double.MinValue;

                eventTimer = timer.Repeat(1f, 0, () => CheckSecondsUntilEvent());
            }

            if (wipeChestsSeed && storedData.Players.Count > 0)
            {
                var ladder = storedData.Players.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.StolenChestsSeed).ToList<KeyValuePair<string, int>>();

                if (AssignTreasureHunters(ladder))
                {
                    foreach (var kvp in storedData.Players.ToList())
                        storedData.Players[kvp.Key].StolenChestsSeed = 0;

                    SaveData();
                    wipeChestsSeed = false;
                }
            }

            if (useRocketOpener)
            {
                rocketDef = ItemManager.FindItemDefinition(useFireRockets ? fireRocketShortname : basicRocketShortname);

                if (!rocketDef)
                {
                    ins.Puts(ins.msg(szInvalidConstant, null, useFireRockets ? fireRocketShortname : basicRocketShortname));
                    useRocketOpener = false;
                }
                else
                    rocketResourcePath = rocketDef.GetComponent<ItemModProjectile>().projectileObject.resourcePath;
            }

            boxDef = ItemManager.FindItemDefinition(boxShortname);

            if (!boxDef)
            {
                Puts(msg(szInvalidConstant, null, boxShortname));
                useRandomBoxSkin = false;
            }

            if (ZoneManager != null)
            {
                var zoneIds = ZoneManager?.Call("GetZoneIDs");

                if (zoneIds != null && zoneIds is string[])
                {
                    foreach (var zoneId in (string[])zoneIds)
                    {
                        var zoneLoc = ZoneManager?.Call("GetZoneLocation", zoneId);

                        if (zoneLoc is Vector3 && (Vector3)zoneLoc != Vector3.zero)
                        {
                            var position = (Vector3)zoneLoc;
                            var zoneRadius = ZoneManager?.Call("GetZoneRadius", zoneId);
                            float distance = 0f;

                            if (zoneRadius is float && (float)zoneRadius > 0f)
                            {
                                distance = (float)zoneRadius;
                            }
                            else
                            {
                                var zoneSize = ZoneManager?.Call("GetZoneSize", zoneId);
                                if (zoneSize is Vector3 && (Vector3)zoneSize != Vector3.zero)
                                {
                                    var size = (Vector3)zoneSize;
                                    distance = Mathf.Max(size.x, size.y);
                                }
                            }

                            if (distance > 0f)
                            {
                                distance += eventRadius + 5f;
                                managedZones[position] = distance;
                            }
                        }
                    }
                }

                if (managedZones.Count > 0)
                    Puts("Blocking events at zones: ", string.Join(", ", managedZones.Select(zone => string.Format("{0} ({1}m)", FormatGridReference(zone.Key), zone.Value)).ToArray()));
            }

            monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>().Select(monument => monument.transform.position).ToList();

            if (includeWorkshopBox || includeWorkshopTreasure)
                webrequest.Enqueue("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", null, GetWorkshopIDs, this, Core.Libraries.RequestMethod.GET);

            init = true;
            RemoveAllTemporaryMarkers();
        }

        void Unload()
        {
            if (!init)
                return;

            unloading = true;

            if (treasureChests.Count > 0)
            {
                foreach (var entry in treasureChests)
                {
                    var container = BaseEntity.serverEntities.Find(entry.Key) as StorageContainer;

                    if (container == null)
                        continue;

                    Puts(msg(szDestroyed, null, container.transform.position));
                    container.inventory?.Clear();

                    if (!container.IsDestroyed)
                        container.Kill();
                }
            }

            var objects = GameObject.FindObjectsOfType(typeof(TreasureChest)); // this isn't needed but doesn't hurt to be here

            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);

            if (spawnedFireballs.Count > 0)
            {
                foreach (var entry in spawnedFireballs)
                    if (entry != null && !entry.IsDestroyed)
                        entry.Kill();

                spawnedFireballs.Clear();
            }

            if (lustyMarkers.Count > 0)
                foreach (var entry in lustyMarkers.ToList())
                    RemoveLustyMarker(entry.Key);

            if (mapMarkers.Count > 0)
                foreach (var entry in mapMarkers.ToList())
                    RemoveMapMarker(entry.Key);

            BlockedLayers.Clear();
            chestLoot?.Clear();
            eventTimer?.Destroy();
            indestructibleWarnings.Clear();
            countdownTimes.Clear();
            skinsCache.Clear();
            workshopskinsCache.Clear();
            RemoveAllTemporaryMarkers();
        }

        private object OnNpcPlayerTarget(NPCPlayerApex npc, BaseEntity entity)
        {
            var player = entity as BasePlayer;
            if (player != null && newmanProtections.Contains(player.net.ID)) return 0f;

            return null;
        }

        void OnPlayerDie(BasePlayer player)
        {
            var npc = player as NPCPlayer;

            if (npc == null || !npcs.Any(entry => entry.Value.Contains(npc)))
                return;

            player.svActiveItemID = 0;
            player.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!init || entity == null || (!(entity is FireBall)) && !(entity is BaseLock) && !(entity is NPCPlayerCorpse))
                return;

            if (entity is NPCPlayerCorpse)
            {
                if (spawnsDespawnInventory)
                {
                    var corpse = entity as NPCPlayerCorpse;

                    foreach (var entry in npcs)
                    {
                        foreach (var npc in entry.Value)
                        {
                            if (npc.userID == corpse.playerSteamID)
                            {
                                NextTick(() =>
                                {
                                    corpse.containers[0].Clear();
                                    corpse.containers[1].Clear();
                                    corpse.containers[2].Clear();
                                });

                                break;
                            }
                        }
                    }
                }

                return;
            }

            NextTick(() =>
            {
                if (entity == null || entity.transform == null)
                    return;

                foreach (var entry in treasureChests)
                {
                    if (Vector3.Distance(entity.transform.position, entry.Value.containerPos) <= eventRadius + 25f) // some extra distance for fireballs that travel outside the sphere, usually on hills
                    {
                        if (entity is FireBall)
                        {
                            var fireball = entity as FireBall;
                            ModifyFireball(fireball);
                        }
                        else if (entity is BaseLock || entity is CodeLock)
                        {
                            entity.KillMessage();
                        }

                        break;
                    }
                }
            });
        }

        object CanBuild(Planner plan, Construction prefab)
        {
            if (!init || plan?.GetOwnerPlayer() == null)
                return null;

            var player = plan.GetOwnerPlayer();

            if (player.IsAdmin)
                return null;

            foreach (var entry in treasureChests)
            {
                if (Vector3.Distance(player.transform.position, entry.Value.containerPos) <= eventRadius)
                {
                    player.ChatMessage(msg(szBuildingBlocked, player.UserIDString));
                    return false;
                }
            }

            return null;
        }

        object CanAcceptItem(ItemContainer container, Item item)
        {
            if (!init || container?.entityOwner?.net == null)
                return null;

            return treasureChests.ContainsKey(container.entityOwner.net.ID) ? (object)ItemContainer.CanAcceptResult.CannotAccept : null;
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!init || player == null || entity?.net == null || !(entity is StorageContainer) || !treasureChests.ContainsKey(entity.net.ID))
                return;

            if (treasureLooters.ContainsKey(entity.net.ID))
                treasureLooters.Remove(entity.net.ID);

            treasureLooters.Add(entity.net.ID, player.UserIDString);
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container?.entityOwner == null || !(container.entityOwner is StorageContainer))
                return;

            NextTick(() =>
            {
                var box = container?.entityOwner as StorageContainer;

                if (box?.net == null || !treasureChests.ContainsKey(box.net.ID))
                    return;

                var looter = item.GetOwnerPlayer();

                if (looter != null)
                {
                    if (treasureLooters.ContainsKey(box.net.ID))
                        treasureLooters.Remove(box.net.ID);

                    treasureLooters.Add(box.net.ID, looter.UserIDString);
                }

                if (box.inventory.itemList.Count == 0)
                {
                    if (looter == null && treasureLooters.ContainsKey(box.net.ID))
                        looter = BasePlayer.activePlayerList.Find(x => x.UserIDString == treasureLooters[box.net.ID]);

                    if (looter != null)
                    {
                        if (recordStats)
                        {
                            if (!storedData.Players.ContainsKey(looter.UserIDString))
                                storedData.Players.Add(looter.UserIDString, new PlayerInfo());

                            storedData.Players[looter.UserIDString].StolenChestsTotal++;
                            storedData.Players[looter.UserIDString].StolenChestsSeed++;
                            SaveData();
                        }

                        var posStr = FormatGridReference(looter.transform.position);
                        Puts(msg(szEventThief, null, posStr, looter.displayName));

                        foreach (var target in BasePlayer.activePlayerList)
                            SendDangerousMessage(target, looter.transform.position, msg(szEventThief, target.UserIDString, posStr, looter.displayName));

                        looter.EndLooting();

                        if (useEconomics && economicsMoney > 0)
                        {
                            if (Economics != null)
                            {
                                Economics?.Call("Deposit", looter.UserIDString, economicsMoney);
                                looter.ChatMessage(msg(szEconomicsDeposit, looter.UserIDString, economicsMoney));
                            }
                        }

                        if (useServerRewards && serverRewardsPoints > 0)
                        {
                            if (ServerRewards != null)
                            {
                                var success = ServerRewards?.Call("AddPoints", looter.userID, serverRewardsPoints);

                                if (success != null && success is bool && (bool)success)
                                    looter.ChatMessage(msg(szRewardPoints, looter.UserIDString, serverRewardsPoints));
                            }
                        }
                    }

                    ins.RemoveLustyMarker(box.net.ID);
                    ins.RemoveMapMarker(box.net.ID);

                    if (!box.IsDestroyed)
                        box.Kill();

                    if (treasureChests.Count == 0)
                        SubscribeHooks(false);
                }
            });
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!init || entity?.net == null)
                return;

            bool newman = newmanProtections.Contains(entity.net.ID);
            bool chest = treasureChests.ContainsKey(entity.net.ID);

            if (newman || chest)
            {
                var attacker = hitInfo?.InitiatorPlayer ?? null;

                if (attacker)
                {
                    var uid = attacker.userID;

                    if (!indestructibleWarnings.Contains(uid))
                    {
                        indestructibleWarnings.Add(uid);
                        timer.Once(10f, () => indestructibleWarnings.Remove(uid));
                        attacker.ChatMessage(msg(chest ? szIndestructible : szNewmanProtected, attacker.UserIDString));
                    }
                }

                hitInfo?.damageTypes?.ScaleAll(0f);
            }
        }

        void SaveData()
        {
            if (storedData != null)
            {
                dataFile.WriteObject(storedData);
            }
        }

        void SubscribeHooks(bool flag)
        {
            if (flag)
            {
                if (init)
                {
                    if (useFireballs || spawnNpcs)
                    {
                        Subscribe(nameof(OnEntitySpawned));
                        Subscribe(nameof(OnPlayerDie));
                    }

                    Subscribe(nameof(OnEntityTakeDamage));
                    Subscribe(nameof(OnItemRemovedFromContainer));
                    Subscribe(nameof(OnLootEntity));
                    Subscribe(nameof(CanAcceptItem));
                    Subscribe(nameof(CanBuild));
                }
            }
            else
            {
                Unsubscribe(nameof(OnEntitySpawned));
                Unsubscribe(nameof(OnEntityTakeDamage));
                Unsubscribe(nameof(OnItemRemovedFromContainer));
                Unsubscribe(nameof(OnLootEntity));
                Unsubscribe(nameof(CanAcceptItem));
                Unsubscribe(nameof(CanBuild));
                Unsubscribe(nameof(OnPlayerDie));
            }
        }

        static void ModifyFireball(FireBall fireball)
        {
            fireball.enableSaving = false;

            if (BaseEntity.saveList.Contains(fireball))
                BaseEntity.saveList.Remove(fireball);

            fireball.damagePerSecond = fbDamagePerSecond;
            fireball.generation = fbGeneration;
            fireball.lifeTimeMax = fbLifeTimeMax;
            fireball.lifeTimeMin = fbLifeTimeMin;
            fireball.radius = fbRadius;
            fireball.tickRate = fbTickRate;
            fireball.waterToExtinguish = fbWaterToExtinguish;
            fireball.SendNetworkUpdate();
            fireball.Think();

            spawnedFireballs.Add(fireball);
            float lifeTime = UnityEngine.Random.Range(fbLifeTimeMin, fbLifeTimeMax);
            ins.timer.Once(lifeTime, () => fireball?.Extinguish());
        }

        static void CreateRocket(Vector3 rocketPos, Vector3 targetPos, Vector3 offset)
        {
            if (!useRocketOpener || rocketDef == null)
                return;

            var rocket = GameManager.server.CreateEntity(rocketDef.GetComponent<ItemModProjectile>().projectileObject.resourcePath, rocketPos, new Quaternion(), true) as TimedExplosive;

            if (!rocket)
            {
                ins.Puts(ins.msg(szInvalidConstant, null, "Rocket Creation"));
                useRocketOpener = false;
                return;
            }

            var projectile = rocket.GetComponent<ServerProjectile>();
            var direction = (targetPos - rocketPos) + offset;

            projectile.gravityModifier = 0f;
            projectile.speed = rocketSpeed;
            projectile.InitializeVelocity(direction);

            rocket.timerAmountMin = 10f;
            rocket.timerAmountMax = 20f;
            //rocket.damageTypes = new List<DamageTypeEntry>(); // no damage

            foreach (var type in rocket.damageTypes)
            {
                type.amount = rocketDamageAmount;
            }

            rocket.Spawn();
        }

        static List<Vector3> GetRandomPositions(Vector3 destination, float radius, int amount, float y)
        {
            var positions = new List<Vector3>();

            if (amount <= 0)
                return positions;

            int retries = 100;
            float space = (radius / amount); // space each rocket out from one another

            for (int i = 0; i < amount; i++)
            {
                var position = destination + UnityEngine.Random.insideUnitSphere * radius;

                position.y = y != 0f ? y : UnityEngine.Random.Range(100f, 200f);

                var match = positions.FirstOrDefault(p => Vector3.Distance(p, position) < space);

                if (match != Vector3.zero)
                {
                    if (--retries < 0)
                        break;

                    i--;
                    continue;
                }

                retries = 100;
                positions.Add(position);
            }

            return positions;
        }

        public Vector3 GetEventPosition()
        {
            var eventPos = Vector3.zero;
            int maxRetries = 100;

            do
            {
                eventPos = GetSafeDropPosition(RandomDropPosition());

                if (Interface.CallHook("OnDangerousOpen", eventPos) != null)
                {
                    eventPos = Vector3.zero;
                    continue;
                }

                if (managedZones.Count > 0)
                {
                    foreach (var zone in managedZones)
                    {
                        if (Vector3.Distance(zone.Key, eventPos) <= zone.Value)
                        {
                            eventPos = Vector3.zero; // blocked by zone manager
                            break;
                        }
                    }
                }

                foreach (var monument in monuments)
                {
                    if (Vector3.Distance(eventPos, monument) < 150f) // don't put the treasure chest near a monument
                    {
                        eventPos = Vector3.zero;
                        break;
                    }
                }
            } while (eventPos == Vector3.zero && --maxRetries > 0);

            return eventPos;
        }

        public Vector3 GetSafeDropPosition(Vector3 position)
        {
            RaycastHit hit;
            position.y += 200f;

            if (Physics.Raycast(position, Vector3.down, out hit))
            {
                if (hit.collider?.gameObject == null)
                    return Vector3.zero;

                if (!BlockedLayers.Contains(hit.collider.gameObject.layer))
                {
                    position.y = Mathf.Max(hit.point.y, TerrainMeta.HeightMap.GetHeight(position));

                    var colliders = Pool.GetList<Collider>();
                    Vis.Colliders(position, eventRadius, colliders, blockedMask, QueryTriggerInteraction.Collide);

                    bool blocked = colliders.Count > 0;

                    Pool.FreeList<Collider>(ref colliders);

                    if (!blocked)
                        return position;
                }
            }

            return Vector3.zero;
        }

        public Vector3 RandomDropPosition() // CargoPlane.RandomDropPosition()
        {
            var vector = Vector3.zero;
            float num = 100f, x = TerrainMeta.Size.x / 3f;
            do
            {
                vector = Vector3Ex.Range(-x, x);
            }
            while (filter.GetFactor(vector) == 0f && (num -= 1f) > 0f);
            vector.y = 0f;
            return vector;
        }

        static long TimeStamp() => (DateTime.Now.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks) / 10000000;

        void GetWorkshopIDs(int code, string response)
        {
            if (response != null && code == 200)
            {
                var items = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response).items;

                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.itemshortname) || string.IsNullOrEmpty(item.workshopdownload))
                        continue;

                    if (!workshopskinsCache.ContainsKey(item.itemshortname))
                        workshopskinsCache.Add(item.itemshortname, new List<ulong>());

                    workshopskinsCache[item.itemshortname].Add(Convert.ToUInt64(item.workshopdownload));
                }
            }
        }

        List<ulong> GetItemSkins(ItemDefinition def)
        {
            if (!skinsCache.ContainsKey(def.shortname))
            {
                var skins = new List<ulong>();
                skins.AddRange(def.skins.Select(skin => Convert.ToUInt64(skin.id)));

                if ((def.shortname == boxShortname && includeWorkshopBox) || (def.shortname != boxShortname && includeWorkshopTreasure))
                {
                    if (workshopskinsCache.ContainsKey(def.shortname))
                    {
                        skins.AddRange(workshopskinsCache[def.shortname]);
                        workshopskinsCache.Remove(def.shortname);
                    }
                }

                if (skins.Contains(0uL))
                    skins.Remove(0uL);

                skinsCache.Add(def.shortname, skins);
            }

            return skinsCache[def.shortname];
        }

        public Vector3 GetNewEvent()
        {
            var position = TryOpenEvent();
            if (position != Vector3.zero)
                Puts(msg(szEventAt, null, FormatGridReference(position)));

            return position;
        }

        public Vector3 TryOpenEvent(BasePlayer player = null)
        {
            var eventPos = Vector3.zero;

            if (player)
            {
                RaycastHit hit;

                if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, heightMask))
                {
                    return Vector3.zero;
                }

                eventPos = hit.point;
            }
            else
            {
                var randomPos = GetEventPosition();

                if (randomPos == Vector3.zero)
                {
                    return Vector3.zero;
                }

                eventPos = randomPos;
            }

            var container = GameManager.server.CreateEntity(boxPrefab, eventPos, new Quaternion(), true) as StorageContainer;

            if (!container)
            {
                Puts(msg(szInvalidConstant, null, boxPrefab));
                return Vector3.zero;
            }

            if (presetSkin != 0uL)
                container.skinID = presetSkin;
            else if (useRandomBoxSkin && boxDef != null)
            {
                var skins = GetItemSkins(boxDef);

                if (skins.Count > 0)
                    container.skinID = skins.GetRandom();
            }

            container.SetFlag(BaseEntity.Flags.Locked, true);
            container.SetFlag(BaseEntity.Flags.OnFire, true);
            container.Spawn();

            var treasureChest = container.gameObject.AddComponent<TreasureChest>();

            if (chestLoot.Count > 0)
            {
                var indices = new List<int>();
                int index = 0, indicer = 100, attempts = treasureAmount * chestLoot.Count; // prevent infinite loops

                do
                {
                    if (chestLoot.Count > 1)
                    {
                        do
                        {
                            index = UnityEngine.Random.Range(0, chestLoot.Count - 1);
                            if (!indices.Contains(index))
                            {
                                indices.Add(index);
                                indicer = 100;
                                break;
                            }
                        } while (--indicer > 0);
                    }

                    int amount = Convert.ToInt32(chestLoot[index].amount);

                    if (amount < 1)
                        amount = 1;

                    ulong skin = Convert.ToUInt64(chestLoot[index].skin);
                    Item item = ItemManager.CreateByName(chestLoot[index].shortname.ToString(), amount, skin);

                    if (item == null)
                        continue;

                    if (item.info.stackable > 1 && !item.hasCondition)
                    {
                        item.amount = GetPercentIncreasedAmount(amount);
                        //Puts("{0}: {1} -> {2}", item.info.shortname, amount, item.amount);
                    }

                    if (useRandomTreasureSkins && skin == 0)
                    {
                        var skins = GetItemSkins(item.info);

                        if (skins.Count > 0)
                        {
                            skin = skins.GetRandom();
                            item.skin = skin;
                        }
                    }

                    if (skin != 0 && item.GetHeldEntity())
                        item.GetHeldEntity().skinID = skin;

                    item.MarkDirty();
                    item.MoveToContainer(container.inventory, -1, true);
                } while (container.inventory.itemList.Count < treasureAmount && --attempts > 0);
            }

            container.enableSaving = false;
            BaseEntity.saveList.Remove(container);

            uint uid = container.net.ID;
            float unlockTime = UnityEngine.Random.Range(unlockTimeMin, unlockTimeMax);

            SubscribeHooks(true);
            treasureChests.Add(uid, treasureChest);
            treasureChest.SetUnlockTime(unlockTime);

            var posStr = FormatGridReference(container.transform.position);
            ins.Puts("{0}: {1}", posStr, string.Join(", ", container.inventory.itemList.Select(item => string.Format("{0} ({1})", item.info.displayName.translated, item.amount)).ToArray()));

            foreach (var target in BasePlayer.activePlayerList)
            {
                double distance = Math.Round(Vector3.Distance(target.transform.position, container.transform.position), 2);
                string unlockStr = FormatTime(unlockTime, target.UserIDString);
                string message = msg(szEventOpened, target.UserIDString, posStr, unlockStr, distance, szDistanceChatCommand);

                if (showOpened)
                {
                    SendDangerousMessage(target, container.transform.position, message);
                }

                if (useGUIAnnouncements && GUIAnnouncements != null && distance <= guiDrawDistance)
                {
                    GUIAnnouncements?.Call("CreateAnnouncement", message, guiTintColor, guiTextColor, target);
                }

                if (useRocketOpener && showBarrage)
                    SendDangerousMessage(target, container.transform.position, msg(szEventBarrage, target.UserIDString, numRockets));

                if (drawTreasureIfNearby && autoDrawDistance > 0f && distance <= autoDrawDistance)
                    DrawText(target, container.transform.position, msg(szTreasureChest, target.UserIDString, distance));
            }

            var position = container.transform.position;
            storedData.TotalEvents++;
            SaveData();

            if (useLustyMap)
                AddLustyMarker(position, uid);

            if (Map)
                AddMapMarker(position, uid);

            timer.Once(unlockTime + destructTime, () => RemoveMapMarker(uid));
            timer.Once(unlockTime + destructTime, () => RemoveLustyMarker(uid));
            SpawnNPCS(position, uid);

            return position;
        }

        // Facepunch.RandomUsernames
        public static string Get(ulong v) //credit Fujikura.
        {
            return Facepunch.RandomUsernames.Get((int)(v % 2147483647uL));
        }

        BaseEntity InstantiateSci(Vector3 position, Quaternion rotation, bool murd) //Spawn population spam fix - credit Fujikura/BotSpawn
        {
            string prefabname = "assets/prefabs/npc/scientist/scientist.prefab";
            if (murd == true)
            {
                prefabname = "assets/prefabs/npc/murderer/murderer.prefab";
            }

            var prefab = GameManager.server.FindPrefab(prefabname);
            if (prefab == null)
                return null;

            GameObject gameObject = Instantiate.GameObject(prefab, position, rotation);
            if (gameObject == null)
                return null;

            gameObject.name = prefabname;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);
            if (gameObject.GetComponent<Spawnable>())
                UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            BaseEntity component = gameObject.GetComponent<BaseEntity>();
            return component;
        }

        static void SpawnNPCS(Vector3 pos, uint uid)
        {
            if (spawnNpcsAmount < 1 || spawnNpcsAmount > 25 || !spawnNpcs)
                return;

            var spawnpoints = GetRandomPositions(pos, eventRadius, spawnNpcsAmount, 0f);
            var rot = new Quaternion(1f, 0f, 0f, 1f);

            for (int i = 0; i < spawnNpcsAmount; i++)
            {
                if (spawnpoints.Count < 1)
                    return;

                var ppos = spawnpoints.GetRandom();
                ppos.y = GetGroundPosition(ppos);

                SpawnNPC(ppos, rot, spawnNpcsBoth ? UnityEngine.Random.Range(0.1f, 1.0f) > 0.5f : spawnNpcsMurderers, uid);
                spawnpoints.Remove(ppos);
            }
        }

        static float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);

            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity, heightMask))
                return Mathf.Max(hit.point.y, y);

            return y;
        }

        static void SpawnNPC(Vector3 pos, Quaternion rot, bool murd, uint uid)
        {
            var entity = ins.InstantiateSci(pos, rot, murd);

            if (entity == null)
                return;

            var npc = entity as NPCPlayer;

            if (npc == null)
                return;

            var apex = entity.GetComponent<NPCPlayerApex>();
            var epos = entity.transform.position;

            apex.Spawn();
            apex.GuardPosition = epos;
            apex.Stats.AggressionRange = 250f;
            apex.Stats.VisionRange = 250f;
            apex.Stats.MaxRoamRange = eventRadius;
            apex.GetComponent<FacepunchBehaviour>().CancelInvoke(new Action(apex.RadioChatter));

            if (spawnNpcsRandomNames)
                npc.displayName = Get(npc.userID);

            if (!npcs.ContainsKey(uid))
                npcs.Add(uid, new List<NPCPlayer>());

            if (!npcs[uid].Contains(npc))
                npcs[uid].Add(npc);

            ins.timer.Once(10f, () => UpdateDestination(apex, epos, uid));
        }

        static void UpdateDestination(NPCPlayerApex apex, Vector3 pos, uint uid)
        {
            if (apex == null || apex.IsDestroyed)
                return;

            if (apex.GetNavAgent.isOnNavMesh)
                apex.GetNavAgent.SetDestination(pos);
            else
                apex.SetDestination(pos);

            if (IsInRock(apex.transform.position) || Vector3.Distance(apex.transform.position, pos) > eventRadius)
            {
                var player = apex.GetComponent<BasePlayer>();
                var spawnpoint = GetRandomPositions(treasureChests.ContainsKey(uid) ? treasureChests[uid].containerPos : pos, 15f, 5, 0f).GetRandom();

                spawnpoint.y = GetGroundPosition(spawnpoint);
                //apex.StopMoving();
                apex.Pause();
                player.ServerPosition = spawnpoint;
                apex.GuardPosition = spawnpoint;
                //apex.RandomMove();
                apex.Resume();
                ins.timer.Once(10f, () => UpdateDestination(apex, spawnpoint, uid));
                return;
            }

            ins.timer.Once(10f, () => UpdateDestination(apex, pos, uid));
        }

        static bool IsInRock(Vector3 pos)
        {
            pos.y += 200f;
            var hits = Physics.RaycastAll(pos, Vector3.down, pos.y + 1f, worldMask);
            pos.y -= 200f;
            foreach (var hit in hits)
            {
                if (hit.collider.name.Contains("rock"))
                {
                    return hit.point.y - 0.1f > pos.y;
                }
            }
            return false;
        }

        void CheckSecondsUntilEvent()
        {
            var eventInterval = UnityEngine.Random.Range(eventIntervalMin, eventIntervalMax);
            var stamp = TimeStamp();

            if (storedData.SecondsUntilEvent == double.MinValue) // first time users
            {
                storedData.SecondsUntilEvent = stamp + eventInterval;
                Puts(msg(szAutomated, null, FormatTime(eventInterval), DateTime.Now.AddSeconds(eventInterval).ToString()));
                Puts(msg(szFirstTimeUse));
                SaveData();
                return;
            }

            if (storedData.SecondsUntilEvent - stamp <= 0)
            {
                if (BasePlayer.activePlayerList.Count >= playerLimit)
                {
                    var eventPos = GetNewEvent();

                    if (eventPos == Vector3.zero) // for servers with high entity counts
                    {
                        int retries = 3;

                        do
                        {
                            eventPos = GetNewEvent();
                        } while (eventPos == Vector3.zero && --retries > 0);
                    }

                    storedData.SecondsUntilEvent = stamp + eventInterval;
                    Puts(msg(szAutomated, null, FormatTime(eventInterval), DateTime.Now.AddSeconds(eventInterval).ToString()));
                    SaveData();
                }
            }
        }

        public static string FormatGridReference(Vector3 position) // Credit: Jake_Rich
        {
            if (showXZ)
                return string.Format("{0} {1}", position.x.ToString("N2"), position.z.ToString("N2"));

            Vector2 roundedPos = new Vector2(World.Size / 2 + position.x, World.Size / 2 - position.z);
            string grid = $"{NumberToLetter((int)(roundedPos.y / 150))}{(int)(roundedPos.x / 150)}";

            return grid;
        }

        public static string NumberToLetter(int num) // Credit: Jake_Rich
        {
            int num2 = Mathf.FloorToInt((float)(num / 26));
            int num3 = num % 26;
            string text = string.Empty;
            if (num2 > 0)
            {
                for (int i = 0; i < num2; i++)
                {
                    text += Convert.ToChar(65 + i);
                }
            }
            return text + Convert.ToChar(65 + num3).ToString();
        }

        string FormatTime(double seconds, string id = null)
        {
            if (seconds == 0)
            {
                if (BasePlayer.activePlayerList.Count < playerLimit)
                    return msg(szNotEnough, null, playerLimit);
                else
                    return string.Format("0 {0}", msg(szTimeSeconds, id));
            }

            var ts = TimeSpan.FromSeconds(seconds);
            var format = new List<string>();

            if (ts.Days > 0)
                format.Add(string.Format("{0} {1}", ts.Days, ts.Days <= 1 ? msg(szTimeDay, id) : msg(szTimeDays, id)));

            if (ts.Hours > 0)
                format.Add(string.Format("{0} {1}", ts.Hours, ts.Hours <= 1 ? msg(szTimeHour, id) : msg(szTimeHours, id)));

            if (ts.Minutes > 0)
                format.Add(string.Format("{0} {1}", ts.Minutes, ts.Minutes <= 1 ? msg(szTimeMinute, id) : msg(szTimeMinutes, id)));

            if (ts.Seconds > 0)
                format.Add(string.Format("{0} {1}", ts.Seconds, ts.Seconds <= 1 ? msg(szTimeSecond, id) : msg(szTimeSeconds, id)));

            if (format.Count > 1)
                format[format.Count - 1] = msg(szTimeAnd, id, format[format.Count - 1]);

            return string.Join(", ", format.ToArray());
        }

        bool AssignTreasureHunters(List<KeyValuePair<string, int>> ladder)
        {
            foreach (var target in covalence.Players.All.Where(p => p != null))
            {
                if (permission.UserHasPermission(target.Id, ladderPerm))
                    permission.RevokeUserPermission(target.Id, ladderPerm);

                if (permission.UserHasGroup(target.Id, ladderGroup))
                    permission.RemoveUserGroup(target.Id, ladderGroup);
            }

            if (!recordStats)
                return true;

            foreach (var entry in ladder.ToList())
                if (entry.Value < 1)
                    ladder.Remove(entry);

            ladder.Sort((x, y) => y.Value.CompareTo(x.Value));

            int permsGiven = 0;

            for (int i = 0; i < ladder.Count; i++)
            {
                var target = covalence.Players.FindPlayerById(ladder[i].Key);

                if (target == null || target.IsBanned || target.IsAdmin)
                    continue;

                permission.GrantUserPermission(target.Id, ladderPerm.ToLower(), this);
                permission.AddUserGroup(target.Id, ladderGroup.ToLower());

                LogToFile("treasurehunters", DateTime.Now.ToString() + " : " + msg(szLogStolen, null, target.Name, target.Id, ladder[i].Value), this, true);
                Puts(msg(szLogGranted, null, target.Name, target.Id, ladderPerm, ladderGroup));

                if (++permsGiven >= permsToGive)
                    break;
            }

            if (permsGiven > 0)
            {
                string file = string.Format("{0}{1}{2}_{3}-{4}.txt", Interface.Oxide.LogDirectory, System.IO.Path.DirectorySeparatorChar, Name.Replace(" ", "").ToLower(), "treasurehunters", DateTime.Now.ToString("yyyy-MM-dd"));
                Puts(msg(szLogSaved, null, file));
            }

            return true;
        }

        void AddMapMarker(Vector3 position, uint uid)
        {
            mapMarkers[uid] = new MapInfo { IconName = lustyMapIconName, Position = position, Url = lustyMapIconFile };
            Map?.Call("ApiAddPointUrl", lustyMapIconFile, lustyMapIconName, position);
            storedData.Markers.Add(uid);
        }

        void RemoveMapMarker(uint uid)
        {
            if (!mapMarkers.ContainsKey(uid))
                return;

            var mapInfo = mapMarkers[uid];
            Map?.Call("ApiRemovePointUrl", mapInfo.Url, mapInfo.IconName, mapInfo.Position);
            mapMarkers.Remove(uid);
            storedData.Markers.Remove(uid);
        }

        void AddLustyMarker(Vector3 pos, uint uid)
        {
            string name = string.Format("{0}_{1}", lustyMapIconName, storedData.TotalEvents).ToLower();

            LustyMap?.Call("AddTemporaryMarker", pos.x, pos.z, name, lustyMapIconFile, lustyMapRotation);
            lustyMarkers[uid] = name;
            storedData.Markers.Add(uid);
        }

        void RemoveLustyMarker(uint uid)
        {
            if (!lustyMarkers.ContainsKey(uid))
                return;

            LustyMap?.Call("RemoveTemporaryMarker", lustyMarkers[uid]);
            lustyMarkers.Remove(uid);
            storedData.Markers.Remove(uid);
        }

        void RemoveAllTemporaryMarkers()
        {
            if (storedData.Markers.Count == 0)
                return;

            if (LustyMap)
            {
                foreach (uint marker in storedData.Markers)
                {
                    LustyMap?.Call("RemoveMarker", marker.ToString());
                }
            }

            if (Map)
            {
                foreach (uint marker in storedData.Markers.ToList())
                {
                    RemoveMapMarker(marker);
                }
            }

            storedData.Markers.Clear();
            SaveData();
        }

        void RemoveAllMarkers()
        {
            int removed = 0;

            for (int i = 0; i < storedData.TotalEvents + 1; i++)
            {
                string name = string.Format("{0}_{1}", lustyMapIconName, i).ToLower();

                if ((bool)(LustyMap?.Call("RemoveMarker", name) ?? false))
                {
                    removed++;
                }
            }

            storedData.Markers.Clear();

            if (removed > 0)
            {
                Puts("Removed {0} existing markers", removed);
            }
            else
                Puts("No markers found");
        }

        void DrawText(BasePlayer player, Vector3 drawPos, string text)
        {
            if (!player || !player.IsConnected || drawPos == Vector3.zero || string.IsNullOrEmpty(text) || drawTime < 1f)
                return;

            bool isAdmin = player.IsAdmin;

            try
            {
                if (grantDraw && !player.IsAdmin)
                {
                    var uid = player.userID;

                    if (!drawGrants.Contains(uid))
                    {
                        drawGrants.Add(uid);
                        timer.Once(drawTime, () => drawGrants.Remove(uid));
                    }

                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    player.SendNetworkUpdateImmediate();
                }

                if (player.IsAdmin || drawGrants.Contains(player.userID))
                    player.SendConsoleCommand("ddraw.text", drawTime, Color.yellow, drawPos, text);
            }
            catch (Exception ex)
            {
                grantDraw = false;
                Puts("DrawText Exception: {0} --- {1}", ex.Message, ex.StackTrace);
                Puts("Disabled drawing for players!");
            }

            if (!isAdmin)
            {
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    player.SendNetworkUpdateImmediate();
                }
            }
        }

        void AddItem(BasePlayer player, string[] args)
        {
            if (args.Length >= 2)
            {
                string shortname = args[0];
                var itemDef = ItemManager.FindItemDefinition(shortname);

                if (itemDef == null)
                {
                    player.ChatMessage(msg("InvalidItem", player.UserIDString, shortname, szDistanceChatCommand));
                    return;
                }

                int amount;
                if (int.TryParse(args[1], out amount))
                {
                    if (itemDef.stackable == 1 || (itemDef.condition.enabled && itemDef.condition.max > 0f) || amount < 1)
                        amount = 1;

                    float skin = 0uL;

                    if (args.Length == 3)
                    {
                        float num;
                        if (float.TryParse(args[2], out num))
                            skin = num;
                        else
                            player.ChatMessage(msg("InvalidValue", player.UserIDString, args[2]));
                    }

                    chestLoot.RemoveAll(entry => entry.shortname.ToString() == shortname);
                    chestLoot.Add(new TreasureItem() { amount = amount, shortname = shortname, skin = skin });

                    var newLoot = new List<object>();
                    newLoot.AddRange(chestLoot.Cast<object>());

                    Config["Treasure", "Loot"] = newLoot;
                    Config.Save();
                    player.ChatMessage(msg("AddedItem", player.UserIDString, shortname, amount, skin));
                }
                else
                    player.ChatMessage(msg("InvalidValue", player.UserIDString, args[2]));

                return;
            }

            player.ChatMessage(msg("InvalidItem", player.UserIDString, args.Length >= 1 ? args[0] : "?", szDistanceChatCommand));
        }

        void cmdTreasureHunter(BasePlayer player, string command, string[] args)
        {
            if (drawGrants.Contains(player.userID))
                return;

            if (args.Length >= 1 && (args[0].ToLower() == "ladder" || args[0].ToLower() == "lifetime") && recordStats)
            {
                if (storedData.Players.Count == 0)
                {
                    player.ChatMessage(msg(szLadderInsufficient, player.UserIDString));
                    return;
                }

                if (args.Length == 2 && args[1] == "resetme")
                    if (storedData.Players.ContainsKey(player.UserIDString))
                        storedData.Players[player.UserIDString].StolenChestsSeed = 0;

                int rank = 0;
                var ladder = storedData.Players.ToDictionary(k => k.Key, v => args[0].ToLower() == "ladder" ? v.Value.StolenChestsSeed : v.Value.StolenChestsTotal).Where(kvp => kvp.Value > 0).ToList<KeyValuePair<string, int>>();
                ladder.Sort((x, y) => y.Value.CompareTo(x.Value));

                player.ChatMessage(msg(args[0].ToLower() == "ladder" ? szLadder : szLadderTotal, player.UserIDString));

                foreach (var kvp in ladder)
                {
                    string name = covalence.Players.FindPlayerById(kvp.Key)?.Name ?? kvp.Key;
                    string value = kvp.Value.ToString("N0");

                    player.ChatMessage(string.Format("<color=lightblue>{0}</color>. <color=silver>{1}</color> (<color=yellow>{2}</color>)", ++rank, name, value));

                    if (rank >= 10)
                        break;
                }

                return;
            }

            if (recordStats)
                player.ChatMessage(msg(szEventWins, player.UserIDString, storedData.Players.ContainsKey(player.UserIDString) ? storedData.Players[player.UserIDString].StolenChestsSeed : 0, szDistanceChatCommand));

            if (args.Length >= 1 && player.IsAdmin)
            {
                if (args[0] == "markers")
                {
                    RemoveAllMarkers();
                    return;
                }
                if (args[0] == "resettime")
                {
                    storedData.SecondsUntilEvent = double.MinValue;
                    return;
                }

                if (args[0] == "tp" && treasureChests.Count > 0)
                {
                    float dist = float.MaxValue;
                    var dest = Vector3.zero;

                    foreach (var entry in treasureChests)
                    {
                        var v3 = Vector3.Distance(entry.Value.containerPos, player.transform.position);

                        if (treasureChests.Count > 1 && v3 < 25f) // 0.2.0 fix - move admin to the next nearest chest
                            continue;

                        if (v3 < dist)
                        {
                            dist = v3;
                            dest = entry.Value.containerPos;
                        }
                    }

                    if (dest != Vector3.zero)
                        player.Teleport(dest);
                }

                if (args[0].ToLower() == "additem")
                {
                    AddItem(player, args.Skip(1).ToArray());
                    return;
                }
            }

            if (treasureChests.Count == 0)
            {
                double time = storedData.SecondsUntilEvent - TimeStamp();

                if (time < 0)
                    time = 0;

                player.ChatMessage(msg(szEventNext, player.UserIDString, FormatTime(time, player.UserIDString)));
                return;
            }

            foreach (var chest in treasureChests)
            {
                double distance = Math.Round(Vector3.Distance(player.transform.position, chest.Value.containerPos), 2);
                string posStr = FormatGridReference(chest.Value.containerPos);
                long unlockTime = chest.Value.UnlockTime - TimeStamp();

                player.ChatMessage(unlockTime > 0 ? msg(szEventInfo, player.UserIDString, FormatTime(unlockTime, player.UserIDString), posStr, distance, szDistanceChatCommand) : msg(szEventStartedInfo, player.UserIDString, posStr, distance, szDistanceChatCommand));
                DrawText(player, chest.Value.containerPos, msg(szTreasureChest, player.UserIDString, distance));
            }
        }

        void ccmdDangerousTreasures(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;

            if (!arg.IsAdmin && (player && !permission.UserHasPermission(player.UserIDString, permName)))
            {
                arg.ReplyWith(msg(szNoPermission, player?.UserIDString ?? null));
                return;
            }

            if (arg.HasArgs() && arg.Args.Length == 1 && arg.Args[0].ToLower() == "help")
            {
                arg.ReplyWith(msg(szHelp, player?.UserIDString ?? null, szEventChatCommand));
                return;
            }

            if (treasureChests.Count >= maxEvents)
            {
                arg.ReplyWith(msg(szMaxManualEvents, player?.UserIDString ?? null, maxEvents));
                return;
            }

            var position = GetNewEvent();

            if (position != Vector3.zero)
            {
                if (arg.HasArgs() && arg.Args.Length == 1 && arg.Args[0].ToLower() == "tp" && player && player.IsAdmin)
                    player.Teleport(position);
            }
            else
            {
                if (position == Vector3.zero)
                    arg.ReplyWith(msg(szEventFail, player?.UserIDString ?? null));
                else
                    ins.Puts(ins.msg(szInvalidConstant, null, boxPrefab));
            }
        }

        void cmdDangerousTreasures(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permName) && !player.IsAdmin)
            {
                player.ChatMessage(msg(szNoPermission, player.UserIDString));
                return;
            }

            if (args.Length == 1 && args[0].ToLower() == "help")
            {
                player.ChatMessage(msg(szHelp, player.UserIDString, szEventChatCommand));
                return;
            }

            if (treasureChests.Count >= maxEvents)
            {
                player.ChatMessage(msg(szMaxManualEvents, player.UserIDString, maxEvents));
                return;
            }

            var position = TryOpenEvent(args.Length == 1 && args[0] == "me" && player.IsAdmin ? player : null);
            if (position != Vector3.zero)
            {
                if (args.Length == 1 && args[0].ToLower() == "tp" && player.IsAdmin)
                {
                    player.Teleport(position);
                }
            }
            else
            {
                player.ChatMessage(msg(szEventFail, player.UserIDString));
            }
        }

        #region Config
        const string szEventInfo = "pInfo";
        const string szEventStartedInfo = "pAlready";
        const string szEventStarted = "pStarted";
        const string szEventOpened = "pOpened";
        const string szEventBarrage = "pBarrage";
        const string szEventNext = "pNext";
        const string szEventThief = "pThief";
        const string szEventWins = "pWins";
        const string szLadder = "Ladder";
        const string szLadderTotal = "Ladder Total";
        const string szLadderInsufficient = "Ladder Insufficient Players";
        const string szPrefix = "Prefix";
        const string szTreasureChest = "Treasure Chest";
        const string szNoPermission = "No Permission";
        const string szBuildingBlocked = "Building is blocked!";
        const string szMaxManualEvents = "Max Manual Events";
        const string szProtected = "Dangerous Zone Protected";
        const string szUnprotected = "Dangerous Zone Unprotected";
        const string szEventFail = "Manual Event Failed";
        const string szHelp = "Help";
        const string szEventAt = "Event At";
        const string szAutomated = "Next Automated Event";
        const string szNotEnough = "Not Enough Online";
        const string szInvalidConstant = "Invalid Constant";
        const string szDestroyed = "Destroyed Treasure Chest";
        const string szIndestructible = "Indestructible";
        const string szFirstTimeUse = "View Config";
        const string szNewmanEnter = "Newman Enter";
        const string szNewmanTraitor = "Newman Traitor";
        const string szNewmanTraitorBurn = "Newman Traitor Burn";
        const string szNewmanProtected = "Newman Protected";
        const string szNewmanProtect = "Newman Protect";
        const string szNewmanProtectFade = "Newman Protect Fade";
        const string szLogStolen = "Log Stolen";
        const string szLogGranted = "Log Granted";
        const string szLogSaved = "Log Saved";
        const string szTimeDay = "TimeDay";
        const string szTimeDays = "TimeDays";
        const string szTimeHour = "TimeHour";
        const string szTimeHours = "TimeHours";
        const string szTimeMinute = "TimeMinute";
        const string szTimeMinutes = "TimeMinutes";
        const string szTimeSecond = "TimeSecond";
        const string szTimeSeconds = "TimeSeconds";
        const string szTimeAnd = "TimeAndFormat";
        const string szEventCountdown = "pCountdown";
        const string szInvalidEntry = "InvalidEntry";
        const string szInvalidKey = "InvalidKey";
        const string szInvalidValue = "InvalidValue";
        const string szUnloading = "Unloading";
        const string szRestartDetected = "RestartDetected";
        const string szDestroyingTreasure = "pDestroyingTreasure";
        const string szEconomicsDeposit = "EconomicsDeposit";
        const string szRewardPoints = "ServerRewardPoints";

        bool Changed;
        List<TreasureItem> chestLoot = new List<TreasureItem>();
        bool showPrefix;
        static bool useRocketOpener;
        static bool useFireRockets;
        static int numRockets;
        static float rocketSpeed;
        static bool useFireballs;
        static float fbDamagePerSecond;
        static float fbLifeTimeMin;
        static float fbLifeTimeMax;
        static float fbRadius;
        static float fbTickRate;
        static float fbGeneration;
        static int fbWaterToExtinguish;
        static int secondsBeforeTick;
        static int maxEvents;
        static int treasureAmount;
        static float unlockTimeMin;
        static float unlockTimeMax;
        static bool automatedEvents;
        static float eventIntervalMin;
        static float eventIntervalMax;
        static Timer eventTimer;
        static bool useSpheres;
        static bool useRandomBoxSkin;
        static ulong presetSkin;
        static bool includeWorkshopBox;
        string permName;
        string szEventChatCommand;
        static string szDistanceChatCommand;
        string szEventConsoleCommand;
        bool grantDraw;
        static float drawTime;
        static bool showStarted;
        bool showOpened;
        bool showBarrage;
        int playerLimit;
        static bool newmanMode;
        static bool newmanProtect;
        static float destructTime;
        static bool launchMissiles;
        static bool targetChest;
        static float missileFrequency;
        static float targettingTime;
        static float timeUntilExplode;
        static bool ignoreFlying;
        static bool recordStats;
        static string ladderPerm;
        static string ladderGroup;
        static int permsToGive;
        bool useLustyMap;
        string lustyMapIconName;
        string lustyMapIconFile;
        float lustyMapRotation;
        static bool useCountdown;
        static List<int> countdownTimes = new List<int>();
        static float eventRadius;
        bool useRandomTreasureSkins;
        bool includeWorkshopTreasure;
        static bool useUnclaimedAnnouncements;
        static float unclaimedInterval;
        bool drawTreasureIfNearby;
        float autoDrawDistance;
        static int amountOfSpheres;
        bool pctIncreasesDayLoot;
        bool usingDayOfWeekLoot;
        Dictionary<DayOfWeek, decimal> pctIncreases = new Dictionary<DayOfWeek, decimal>();
        static float missileDetectionDistance;
        bool useEconomics;
        bool useServerRewards;
        double economicsMoney;
        double serverRewardsPoints;
        decimal percentLoss;
        bool useGUIAnnouncements;
        string guiTintColor;
        string guiTextColor;
        float guiDrawDistance;
        static float rocketDamageAmount;
        static bool spawnNpcs;
        static bool spawnNpcsMurderers;
        static bool spawnNpcsBoth;
        static int spawnNpcsAmount;
        static bool spawnsDespawnInventory;
        static bool showXZ;
        static bool spawnNpcsRandomNames;

        List<object> DefaultTimesInSeconds
        {
            get
            {
                return new List<object> { "120", "60", "30", "15" };
            }
        }

        List<object> DefaultTreasure
        {
            get
            {
                return new List<object>
                {
                    new TreasureItem { shortname = "ammo.pistol", amount = 40, skin = 0 },
                    new TreasureItem { shortname = "ammo.pistol.fire", amount = 40, skin = 0 },
                    new TreasureItem { shortname = "ammo.pistol.hv", amount = 40, skin = 0 },
                    new TreasureItem { shortname = "ammo.rifle", amount = 60, skin = 0 },
                    new TreasureItem { shortname = "ammo.rifle.explosive", amount = 60, skin = 0 },
                    new TreasureItem { shortname = "ammo.rifle.hv", amount = 60, skin = 0 },
                    new TreasureItem { shortname = "ammo.rifle.incendiary", amount = 60, skin = 0 },
                    new TreasureItem { shortname = "ammo.shotgun", amount = 24, skin = 0 },
                    new TreasureItem { shortname = "ammo.shotgun.slug", amount = 40, skin = 0 },
                    new TreasureItem { shortname = "survey.charge", amount = 20, skin = 0 },
                    new TreasureItem { shortname = "metal.refined", amount = 150, skin = 0 },
                    new TreasureItem { shortname = "bucket.helmet", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "cctv.camera", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "coffeecan.helmet", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "explosive.timed", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "metal.facemask", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "metal.plate.torso", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "mining.quarry", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "pistol.m92", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "rifle.ak", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "rifle.bolt", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "rifle.lr300", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "smg.2", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "smg.mp5", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "smg.thomspon", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "supply.signal", amount = 1, skin = 0 },
                    new TreasureItem { shortname = "targeting.computer", amount = 1, skin = 0 },
                };
            }
        }

        Dictionary<string, Dictionary<string, string>> GetMessages()
        {
            return new Dictionary<string, Dictionary<string, string>>
            {
                {szNoPermission, new Dictionary<string, string>() {
                    {"en", "You do not have permission to use this command."},
                }},
                {szBuildingBlocked, new Dictionary<string, string>() {
                    {"en", "<color=red>Building is blocked near treasure chests!</color>"},
                }},
                {szMaxManualEvents, new Dictionary<string, string>() {
                    {"en", "Maximum number of manual events <color=red>{0}</color> has been reached!"},
                }},
                {szProtected, new Dictionary<string, string>() {
                    {"en", "<color=red>You have entered a dangerous zone protected by a fire aura! You must leave before you die!</color>"},
                }},
                {szUnprotected, new Dictionary<string, string>() {
                    {"en", "<color=red>You have entered a dangerous zone!</color>"},
                }},
                {szEventFail, new Dictionary<string, string>() {
                    {"en", "Event failed to start! Unable to obtain a valid position. Please try again."},
                }},
                {szHelp, new Dictionary<string, string>() {
                    {"en", "/{0} <tp> - start a manual event, and teleport to the position if TP argument is specified and you are an admin."},
                }},
                {szEventStarted, new Dictionary<string, string>() {
                    {"en", "The event has started at <color=yellow>{0}</color>! The protective fire aura has been obliterated!</color>"},
                }},
                {szEventOpened, new Dictionary<string, string>() {
                    {"en", "An event has opened at <color=yellow>{0}</color>! Event will start in <color=yellow>{1}</color>. You are <color=orange>{2}m</color> away. Use <color=orange>/{3}</color> for help.</color>"},
                }},
                {szEventBarrage, new Dictionary<string, string>() {
                    {"en", "A barrage of <color=yellow>{0}</color> rockets can be heard at the location of the event!</color>"},
                }},
                {szEventInfo, new Dictionary<string, string>() {
                    {"en", "Event will start in <color=yellow>{0}</color> at <color=yellow>{1}</color>. You are <color=orange>{2}m</color> away.</color>"},
                }},
                {szEventStartedInfo, new Dictionary<string, string>() {
                    {"en", "The event has already started at <color=yellow>{0}</color>! You are <color=orange>{1}m</color> away.</color>"},
                }},
                {szEventNext, new Dictionary<string, string>() {
                    {"en", "No events are open. Next event in <color=yellow>{0}</color></color>"},
                }},
                {szEventThief, new Dictionary<string, string>() {
                    {"en", "The treasures at <color=yellow>{0}</color> have been stolen by <color=yellow>{1}</color>!</color>"},
                }},
                {szEventWins, new Dictionary<string, string>()
                {
                    {"en", "You have stolen <color=yellow>{0}</color> treasure chests! View the ladder using <color=orange>/{1} ladder</color> or <color=orange>/{1} lifetime</color></color>"},
                }},
                {szLadder, new Dictionary<string, string>()
                {
                    {"en", "<color=yellow>[ Top 10 Treasure Hunters (This Wipe) ]</color>:"},
                }},
                {szLadderTotal, new Dictionary<string, string>()
                {
                    {"en", "<color=yellow>[ Top 10 Treasure Hunters (Lifetime) ]</color>:"},
                }},
                {szLadderInsufficient, new Dictionary<string, string>()
                {
                    {"en", "<color=yellow>No players are on the ladder yet!</color>"},
                }},
                {szEventAt, new Dictionary<string, string>() {
                    {"en", "Event at {0}"},
                }},
                {szAutomated, new Dictionary<string, string>() {
                    {"en", "Next automated event in {0} at {1}"},
                }},
                {szNotEnough, new Dictionary<string, string>() {
                    {"en", "Not enough players online ({0} minimum)"},
                }},
                {szTreasureChest, new Dictionary<string, string>() {
                    {"en", "Treasure Chest <color=orange>{0}m</color>"},
                }},
                {szInvalidConstant, new Dictionary<string, string>() {
                    {"en", "Invalid constant {0} - please notify the author!"},
                }},
                {szDestroyed, new Dictionary<string, string>() {
                    {"en", "Destroyed a left over treasure chest at {0}"},
                }},
                {szIndestructible, new Dictionary<string, string>() {
                    {"en", "<color=red>Treasure chests are indestructible!</color>"},
                }},
                {szFirstTimeUse, new Dictionary<string, string>() {
                    {"en", "Please view the config if you haven't already."},
                }},
                {szNewmanEnter, new Dictionary<string, string>() {
                    {"en", "<color=red>To walk with clothes is to set one-self on fire. Tread lightly.</color>"},
                }},
                {szNewmanTraitorBurn, new Dictionary<string, string>() {
                    {"en", "<color=red>Tempted by the riches you have defiled these grounds. Vanish from these lands or PERISH!</color>"},
                }},
                {szNewmanTraitor, new Dictionary<string, string>() {
                    {"en", "<color=red>Tempted by the riches you have defiled these grounds. Vanish from these lands!</color>"},
                }},
                {szNewmanProtected, new Dictionary<string, string>() {
                    {"en", "<color=red>This newman is temporarily protected on these grounds!</color>"},
                }},
                {szNewmanProtect, new Dictionary<string, string>() {
                    {"en", "<color=red>You are protected on these grounds. Do not defile them.</color>"},
                }},
                {szNewmanProtectFade, new Dictionary<string, string>() {
                    {"en", "<color=red>Your protection has faded.</color>"},
                }},
                {szLogStolen, new Dictionary<string, string>() {
                    {"en", "{0} ({1}) chests stolen {2}"},
                }},
                {szLogGranted, new Dictionary<string, string>() {
                    {"en", "Granted {0} ({1}) permission {2} for group {3}"},
                }},
                {szLogSaved, new Dictionary<string, string>() {
                    {"en", "Treasure Hunters have been logged to: {0}"},
                }},
                {szPrefix, new Dictionary<string, string>() {
                    {"en", "<color=silver>[ <color=#406B35>Dangerous Treasures</color> ] "},
                }},
                {szTimeDay, new Dictionary<string, string>() {
                    {"en", "day"},
                }},
                {szTimeDays, new Dictionary<string, string>() {
                    {"en", "days"},
                }},
                {szTimeHour, new Dictionary<string, string>() {
                    {"en", "hour"},
                }},
                {szTimeHours, new Dictionary<string, string>() {
                    {"en", "hours"},
                }},
                {szTimeMinute, new Dictionary<string, string>() {
                    {"en", "minute"},
                }},
                {szTimeMinutes, new Dictionary<string, string>() {
                    {"en", "minutes"},
                }},
                {szTimeSecond, new Dictionary<string, string>() {
                    {"en", "second"},
                }},
                {szTimeSeconds, new Dictionary<string, string>() {
                    {"en", "seconds"},
                }},
                {szTimeAnd, new Dictionary<string, string>() {
                    {"en", "and {0}"},
                }},
                {szEventCountdown, new Dictionary<string, string>()
                {
                    {"en", "Event at <color=yellow>{0}</color> will start in <color=yellow>{1}</color>!</color>"},
                }},
                {szInvalidEntry, new Dictionary<string, string>()
                {
                    {"en", "Entry is missing key: {0}"},
                }},
                {szInvalidKey, new Dictionary<string, string>()
                {
                    {"en", "Invalid entry: {0} ({1})"},
                }},
                {szInvalidValue, new Dictionary<string, string>()
                {
                    {"en", "Invalid value: {0}"},
                }},
                {szUnloading, new Dictionary<string, string>()
                {
                    {"en", "No valid loot found in the config file under {0}! Unloading plugin..."},
                }},
                {szRestartDetected, new Dictionary<string, string>()
                {
                    {"en", "Restart detected. Next event in {0} minutes."},
                }},
                {szDestroyingTreasure, new Dictionary<string, string>()
                {
                    {"en", "The treasure at <color=yellow>{0}</color> will be destroyed by fire in <color=yellow>{1}</color> if not looted! Use <color=orange>/{2}</color> to find this chest.</color>"},
                }},
                {szEconomicsDeposit, new Dictionary<string, string>()
                {
                    {"en", "You have received <color=yellow>${0}</color> for stealing the treasure!"},
                }},
                {szRewardPoints, new Dictionary<string, string>()
                {
                    {"en", "You have received <color=yellow>{0} RP</color> for stealing the treasure!"},
                }},
                {"InvalidItem", new Dictionary<string, string>()
                {
                    {"en", "Invalid item shortname: {0}. Use /{1} additem <shortname> <amount> [skin]"},
                }},
                {"AddedItem", new Dictionary<string, string>()
                {
                    {"en", "Added item: {0} amount: {1}, skin: {2}"},
                }},
            };
        }

        void RegisterMessages()
        {
            var compiledLangs = new Dictionary<string, Dictionary<string, string>>();

            foreach (var line in GetMessages())
            {
                foreach (var translate in line.Value)
                {
                    if (!compiledLangs.ContainsKey(translate.Key))
                        compiledLangs[translate.Key] = new Dictionary<string, string>();

                    compiledLangs[translate.Key][line.Key] = translate.Value;
                }
            }

            foreach (var cLangs in compiledLangs)
                lang.RegisterMessages(cLangs.Value, this, cLangs.Key);
        }

        void LoadVariables()
        {
            RegisterMessages();

            useRocketOpener = Convert.ToBoolean(GetConfig("Rocket Opener", "Enabled", true));

            if (useRocketOpener)
            {
                numRockets = Convert.ToInt32(GetConfig("Rocket Opener", "Rockets", 10));
                useFireRockets = Convert.ToBoolean(GetConfig("Rocket Opener", "Use Fire Rockets", false));
                rocketSpeed = Convert.ToSingle(GetConfig("Rocket Opener", "Speed", 25f).ToString());
            }

            useFireballs = Convert.ToBoolean(GetConfig("Fireballs", "Enabled", true));

            if (useFireballs)
            {
                fbDamagePerSecond = Convert.ToSingle(GetConfig("Fireballs", "Damage Per Second", 10f).ToString());
                fbLifeTimeMax = Convert.ToSingle(GetConfig("Fireballs", "Lifetime Max", 10f).ToString());
                fbLifeTimeMin = Convert.ToSingle(GetConfig("Fireballs", "Lifetime Min", 7.5f).ToString());
                fbRadius = Convert.ToSingle(GetConfig("Fireballs", "Radius", 1f).ToString());
                fbTickRate = Convert.ToSingle(GetConfig("Fireballs", "Tick Rate", 1f).ToString());
                fbGeneration = Convert.ToSingle(GetConfig("Fireballs", "Generation", 5f).ToString());
                fbWaterToExtinguish = Convert.ToInt32(GetConfig("Fireballs", "Damage Per Second", 25));
                secondsBeforeTick = Convert.ToInt32(GetConfig("Fireballs", "Spawn Every X Seconds", 5));
            }

            grantDraw = Convert.ToBoolean(GetConfig("Events", "Grant DDRAW temporarily to players", true));

            if (grantDraw)
                drawTime = Convert.ToSingle(GetConfig("Events", "Grant Draw Time", 15f).ToString());

            if (Config.Get("Treasure") != null)
            {
                if (Config.Get("Treasure", "Items") != null) // update existing items to the new format for versions below 0.1.12
                {
                    var oldTreasure = Config.Get("Treasure", "Items") as Dictionary<string, object>;
                    var newTreasure = new List<object>();

                    foreach (var kvp in oldTreasure)
                        newTreasure.Add(new TreasureItem { shortname = kvp.Key, amount = kvp.Value, skin = 0 });

                    Config.Remove("Treasure");
                    Config.Set("Treasure", "Loot", newTreasure);
                }
            }

            percentLoss = Convert.ToDecimal(GetConfig("Treasure", "Minimum Percent Loss", 0m));

            if (percentLoss > 0)
                percentLoss /= 100m;

            pctIncreasesDayLoot = Convert.ToBoolean(GetConfig("Treasure", "Percent Increase When Using Day Of Week Loot", false));

            foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
            {
                pctIncreases[day] = Convert.ToDecimal(GetConfig("Treasure", string.Format("Percent Increase On {0}", day.ToString()), 0m));
                var treasures = GetConfig("Treasure", string.Format("Day Of Week Loot {0}", day.ToString()), new List<object>()) as List<object>;

                if (treasures != null && treasures.Count > 0 && DateTime.Now.DayOfWeek == day)
                    SetTreasure(treasures, true);
            }

            if (chestLoot.Count == 0)
            {
                var treasures = GetConfig("Treasure", "Loot", DefaultTreasure) as List<object>;

                if (treasures != null && treasures.Count > 0)
                    SetTreasure(treasures, false);
            }

            useRandomTreasureSkins = Convert.ToBoolean(GetConfig("Treasure", "Use Random Skins", false));
            includeWorkshopTreasure = Convert.ToBoolean(GetConfig("Treasure", "Include Workshop Skins", false));

            unlockTimeMin = Convert.ToSingle(GetConfig("Unlock Time", "Min Seconds", 300f).ToString());
            unlockTimeMax = Convert.ToSingle(GetConfig("Unlock Time", "Max Seconds", 480f).ToString());

            useRandomBoxSkin = Convert.ToBoolean(GetConfig("Skins", "Use Random Skin", true));
            presetSkin = Convert.ToUInt64(GetConfig("Skins", "Preset Skin", 0uL));
            includeWorkshopBox = Convert.ToBoolean(GetConfig("Skins", "Include Workshop Skins", true));

            automatedEvents = Convert.ToBoolean(GetConfig("Events", "Automated", true));
            eventIntervalMin = Convert.ToSingle(GetConfig("Events", "Every Min Seconds", 3600f).ToString());
            eventIntervalMax = Convert.ToSingle(GetConfig("Events", "Every Max Seconds", 7200f).ToString());
            maxEvents = Convert.ToInt32(GetConfig("Events", szMaxManualEvents, 1));
            treasureAmount = Convert.ToInt32(GetConfig("Events", "Amount Of Items To Spawn", 6));
            useSpheres = Convert.ToBoolean(GetConfig("Events", "Use Spheres", true));
            amountOfSpheres = Convert.ToInt32(GetConfig("Events", "Amount Of Spheres", 5));
            playerLimit = Convert.ToInt32(GetConfig("Events", "Player Limit For Event", 0));
            eventRadius = Convert.ToSingle(GetConfig("Events", "Fire Aura Radius (Advanced Users Only)", 25f));

            if (eventRadius < 10f)
                eventRadius = 10f;

            if (eventRadius > 150f)
                eventRadius = 150f;

            permName = Convert.ToString(GetConfig("Settings", "Permission Name", "dangeroustreasures.use"));

            if (!string.IsNullOrEmpty(permName) && !permission.PermissionExists(permName))
                permission.RegisterPermission(permName, this);

            szEventChatCommand = Convert.ToString(GetConfig("Settings", "Event Chat Command", "dtevent"));

            if (!string.IsNullOrEmpty(szEventChatCommand))
                cmd.AddChatCommand(szEventChatCommand, this, cmdDangerousTreasures);

            szDistanceChatCommand = Convert.ToString(GetConfig("Settings", "Distance Chat Command", "dtd"));

            if (!string.IsNullOrEmpty(szDistanceChatCommand))
                cmd.AddChatCommand(szDistanceChatCommand, this, cmdTreasureHunter);

            szEventConsoleCommand = Convert.ToString(GetConfig("Settings", "Event Console Command", "dtevent"));

            if (!string.IsNullOrEmpty(szEventConsoleCommand))
                cmd.AddConsoleCommand(szEventConsoleCommand, this, nameof(ccmdDangerousTreasures));

            showBarrage = Convert.ToBoolean(GetConfig("Event Messages", "Show Barrage Message", true));
            showOpened = Convert.ToBoolean(GetConfig("Event Messages", "Show Opened Message", true));
            showStarted = Convert.ToBoolean(GetConfig("Event Messages", "Show Started Message", true));
            showPrefix = Convert.ToBoolean(GetConfig("Event Messages", "Show Prefix", true));

            newmanMode = Convert.ToBoolean(GetConfig("Newman Mode", "Protect Nakeds From Fire Aura", false));
            newmanProtect = Convert.ToBoolean(GetConfig("Newman Mode", "Protect Nakeds From Other Harm", false));

            destructTime = Convert.ToSingle(GetConfig("Events", "Time To Loot", 900f).ToString());

            launchMissiles = Convert.ToBoolean(GetConfig("Missile Launcher", "Enabled", false));
            targetChest = Convert.ToBoolean(GetConfig("Missile Launcher", "Target Chest If No Player Target", false));
            missileFrequency = Convert.ToSingle(GetConfig("Missile Launcher", "Spawn Every X Seconds", 15f).ToString());
            targettingTime = Convert.ToSingle(GetConfig("Missile Launcher", "Acquire Time In Seconds", 10f).ToString());
            timeUntilExplode = Convert.ToSingle(GetConfig("Missile Launcher", "Life Time In Seconds", 60f).ToString());
            ignoreFlying = Convert.ToBoolean(GetConfig("Missile Launcher", "Ignore Flying Players", false));
            missileDetectionDistance = Convert.ToSingle(GetConfig("Missile Launcher", "Detection Distance", 15f));
            rocketDamageAmount = Convert.ToSingle(GetConfig("Missile Launcher", "Damage Per Missile", 0.0f));

            if (missileDetectionDistance < 1f)
                missileDetectionDistance = 1f;
            else if (missileDetectionDistance > 100f)
                missileDetectionDistance = 100f;

            recordStats = Convert.ToBoolean(GetConfig("Ranked Ladder", "Enabled", true));
            ladderPerm = Convert.ToString(GetConfig("Ranked Ladder", "Permission Name", "dangeroustreasures.th"));
            ladderGroup = Convert.ToString(GetConfig("Ranked Ladder", "Group Name", "treasurehunter"));
            permsToGive = Convert.ToInt32(GetConfig("Ranked Ladder", "Award Top X Players On Wipe", 3));

            if (string.IsNullOrEmpty(ladderPerm))
                ladderPerm = "dangeroustreasures.th";

            if (string.IsNullOrEmpty(ladderGroup))
                ladderGroup = "treasurehunter";

            useLustyMap = Convert.ToBoolean(GetConfig("Lusty Map", "Enabled", true));

            if (useLustyMap)
            {
                lustyMapIconFile = Convert.ToString(GetConfig("Lusty Map", "Icon File", "http://i.imgur.com/XoEMTJj.png"));
                lustyMapIconName = Convert.ToString(GetConfig("Lusty Map", "Icon Name", "dtchest"));
                lustyMapRotation = Convert.ToSingle(GetConfig("Lusty Map", "Icon Rotation", 0f));

                if (lustyMapIconFile == "special")
                {
                    lustyMapIconFile = "http://i.imgur.com/XoEMTJj.png";
                }

                if (string.IsNullOrEmpty(lustyMapIconFile) || string.IsNullOrEmpty(lustyMapIconName))
                    useLustyMap = false;
            }

            if (!string.IsNullOrEmpty(ladderPerm))
            {
                if (!permission.PermissionExists(ladderPerm))
                    permission.RegisterPermission(ladderPerm, this);

                if (!string.IsNullOrEmpty(ladderGroup))
                {
                    permission.CreateGroup(ladderGroup, ladderGroup, 0);
                    permission.GrantGroupPermission(ladderGroup, ladderPerm, this);
                }
            }

            useCountdown = Convert.ToBoolean(GetConfig("Countdown", "Use Countdown Before Event Starts", false));
            var times = GetConfig("Countdown", "Time In Seconds", DefaultTimesInSeconds) as List<object>;

            if (useCountdown)
            {
                foreach (var entry in times)
                {
                    int time;
                    if (entry != null && int.TryParse(entry.ToString(), out time) && time > 0)
                        countdownTimes.Add(time);
                }

                if (countdownTimes.Count == 0)
                    useCountdown = false;
            }

            useUnclaimedAnnouncements = Convert.ToBoolean(GetConfig("Unlooted Announcements", "Enabled", false));
            unclaimedInterval = Convert.ToSingle(GetConfig("Unlooted Announcements", "Notify Every X Minutes (Minimum 1)", 3f));

            if (unclaimedInterval < 1f)
                unclaimedInterval = 1f;

            drawTreasureIfNearby = Convert.ToBoolean(GetConfig("Events", "Auto Draw On New Event For Nearby Players", false));
            autoDrawDistance = Convert.ToSingle(GetConfig("Events", "Auto Draw Minimum Distance", 300f));

            if (autoDrawDistance < 0f)
                autoDrawDistance = 0f;
            else if (autoDrawDistance > ConVar.Server.worldsize)
                autoDrawDistance = ConVar.Server.worldsize;

            useEconomics = Convert.ToBoolean(GetConfig("Rewards", "Use Economics", false));
            economicsMoney = Convert.ToDouble(GetConfig("Rewards", "Economics Money", 20.0));
            useServerRewards = Convert.ToBoolean(GetConfig("Rewards", "Use ServerRewards", false));
            serverRewardsPoints = Convert.ToDouble(GetConfig("Rewards", "ServerRewards Points", 20.0));

            useGUIAnnouncements = Convert.ToBoolean(GetConfig("GUIAnnouncements", "Enabled", false));
            guiTextColor = Convert.ToString(GetConfig("GUIAnnouncements", "Text Color", "White"));
            guiTintColor = Convert.ToString(GetConfig("GUIAnnouncements", "Banner Tint Color", "Black"));
            guiDrawDistance = Convert.ToSingle(GetConfig("GUIAnnouncements", "Maximum Distance", 300f));

            spawnNpcs = Convert.ToBoolean(GetConfig("NPCs", "Enabled", false));
            spawnNpcsAmount = Convert.ToInt32(GetConfig("NPCs", "Amount To Spawn", 2));
            spawnNpcsBoth = Convert.ToBoolean(GetConfig("NPCs", "Spawn Murderers And Scientists", false));
            spawnNpcsMurderers = Convert.ToBoolean(GetConfig("NPCs", "Spawn Murderers", false));
            spawnsDespawnInventory = Convert.ToBoolean(GetConfig("NPCs", "Despawn Inventory On Death", true));
            spawnNpcsRandomNames = Convert.ToBoolean(GetConfig("NPCs", "Generate Random Names", true));
            showXZ = Convert.ToBoolean(GetConfig("Settings", "Show X Z Coordinates", false));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        int GetPercentIncreasedAmount(int amount)
        {
            if (usingDayOfWeekLoot && !pctIncreasesDayLoot)
            {
                if (percentLoss > 0m)
                    amount = UnityEngine.Random.Range(Convert.ToInt32(amount - (amount * percentLoss)), amount);

                return amount;
            }

            decimal percentIncrease = pctIncreases.ContainsKey(DateTime.Now.DayOfWeek) ? pctIncreases[DateTime.Now.DayOfWeek] : 0m;

            if (percentIncrease > 1m)
                percentIncrease /= 100;

            if (percentIncrease > 0m)
            {
                amount = Convert.ToInt32(amount + (amount * percentIncrease));

                if (percentLoss > 0m)
                    amount = UnityEngine.Random.Range(Convert.ToInt32(amount - (amount * percentLoss)), amount);
            }

            return amount;
        }

        void SetTreasure(List<object> treasures, bool dayOfWeekLoot)
        {
            if (treasures != null && treasures.Count > 0)
            {
                chestLoot.Clear();
                usingDayOfWeekLoot = dayOfWeekLoot;

                foreach (var entry in treasures)
                {
                    if (entry is Dictionary<string, object>)
                    {
                        var dict = entry as Dictionary<string, object>;

                        if (dict.ContainsKey("shortname") && dict.ContainsKey("amount") && dict.ContainsKey("skin"))
                        {
                            int amount;
                            if (int.TryParse(dict["amount"].ToString(), out amount))
                            {
                                ulong skin;
                                if (ulong.TryParse(dict["skin"].ToString(), out skin))
                                {
                                    if (dict["shortname"] != null && dict["shortname"].ToString().Length > 0)
                                        chestLoot.Add(new TreasureItem() { shortname = dict["shortname"], amount = amount, skin = skin });
                                    else
                                        Puts(msg(szInvalidValue, null, dict["shortname"] == null ? "null" : dict["shortname"]));
                                }
                                else
                                    Puts(msg(szInvalidValue, null, dict["skin"]));
                            }
                            else
                                Puts(msg(szInvalidValue, null, dict["amount"]));
                        }
                        else
                        {
                            foreach (var kvp in dict.Where(e => !e.Key.Equals("shortname") && !e.Key.Equals("amount") && !e.Key.Equals("skin")))
                                Puts(msg(szInvalidKey, null, kvp.Key, kvp.Value));

                            if (!dict.ContainsKey("amount"))
                                Puts(msg(szInvalidEntry, null, "amount"));

                            if (!dict.ContainsKey("shortname"))
                                Puts(msg(szInvalidEntry, null, "shortname"));

                            if (!dict.ContainsKey("skin"))
                                Puts(msg(szInvalidEntry, null, "skin"));
                        }
                    }
                }
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
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

        string msg(string key, string id = null, params object[] args)
        {
            string translate = lang.GetMessage(key, this, id);

            if (translate == key) // message does not exist?
                translate = lang.GetMessage(key, this, null);

            string message = string.IsNullOrEmpty(id) ? RemoveFormatting(translate) : translate;

            if (!string.IsNullOrEmpty(id) && key.Length > 0 && key[0] == 'p' && showPrefix)
            {
                string prefix = lang.GetMessage(szPrefix, this, id);

                if (prefix == szPrefix) // message does not exist?
                    prefix = lang.GetMessage(key, this, null);

                message = prefix + message;
            }

            if (!showPrefix && key.Length > 0 && key[0] == 'p')
                message = "<color=silver>" + message;

            int indices = message.Count(c => c == '{');
            return indices > 0 ? string.Format(message, args) : message;
        }

        string RemoveFormatting(string source) => source.Contains(">") ? System.Text.RegularExpressions.Regex.Replace(source, "<.*?>", string.Empty) : source;

        static void SendDangerousMessage(BasePlayer player, Vector3 eventPos, string message)
        {
            if (Interface.CallHook("OnDangerousMessage", player, eventPos, message) == null)
                player.ChatMessage(message);
        }
        #endregion
    }
}