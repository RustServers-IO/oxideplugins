using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Facepunch;
using Network;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Duelist", "nivex", "0.1.15", ResourceId = 2520)]
    [Description("1v1 dueling event.")]
    public class Duelist : RustPlugin
    {
        [PluginReference]
        Plugin Kits, ZoneManager, Economics, ServerRewards, TruePVE;

        readonly static string hewwPrefab = "assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab";
        readonly static string heswPrefab = "assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab";
        readonly string invalidKit = "KZXCMASD9251203"; // invalidates the given kit
        static Duelist ins;
        bool init = false; // are we initialized properly? if not disable certain functionality
        bool resetDuelists = false; // if wipe is detected then assign awards and wipe VictoriesSeed / LossesSeed

        Dictionary<string, bool> deployables = new Dictionary<string, bool>();
        Dictionary<string, string> prefabs = new Dictionary<string, string>();
        Dictionary<ulong, List<BaseEntity>> duelEntities = new Dictionary<ulong, List<BaseEntity>>();
        static List<DuelingZone> duelingZones = new List<DuelingZone>(); // where all the fun is at
        Dictionary<string, List<ulong>> skinsCache = new Dictionary<string, List<ulong>>(); // used to randomize custom kit skins which skin id values are 0
        Dictionary<Vector3, float> managedZones = new Dictionary<Vector3, float>(); // blocked zones from zonemanager plugin
        SpawnFilter filter = new SpawnFilter(); // RandomDropPosition()
        List<Vector3> monuments = new List<Vector3>(); // positions of monuments on the server
        DynamicConfigFile duelsFile;
        static StoredData duelsData = new StoredData();
        Timer eventTimer; // timer to check for immunity and auto death time of duelers
        Timer announceTimer;

        readonly int waterMask = LayerMask.GetMask("Water"); // used to count water colliders when finding a random dueling zone on the map
        readonly int groundMask = LayerMask.GetMask("Terrain", "World", "Default"); // used to find dueling zone/set custom zone and create spawn points
        readonly int constructionMask = LayerMask.GetMask("Construction", "Deployed");
        readonly int wallMask = LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Deployed");
        readonly int blockedMask = LayerMask.GetMask("Player (Server)", "Prevent Building", "Construction", "Deployed", "Trigger"); // layers we won't be setting a zone within 50 meters of

        public class StoredData // don't edit this section or the datafile
        {
            public List<string> Allowed = new List<string>(); // list of users that allow duel requests
            public List<string> Restricted = new List<string>(); // list of users blocked from requesting a duel for 60 seconds
            public SortedDictionary<long, string> Queued = new SortedDictionary<long, string>(); // queued duelers sorted by timestamp and user id. first come first serve

            public Dictionary<string, string> Bans = new Dictionary<string, string>(); // users banned from dueling
            public Dictionary<string, string> Requests = new Dictionary<string, string>(); // users requesting a duel and to whom
            public Dictionary<string, string> Duelers = new Dictionary<string, string>(); // active duelers
            public Dictionary<string, long> Immunity = new Dictionary<string, long>(); // players immune to damage
            public Dictionary<string, long> Death = new Dictionary<string, long>(); // users id and timestamp of when they're to be executed

            public Dictionary<string, int> Losses = new Dictionary<string, int>(); // user id / losses for lifetime
            public Dictionary<string, int> LossesSeed = new Dictionary<string, int>(); // user id / losses for seed
            public Dictionary<string, int> Victories = new Dictionary<string, int>(); // user id / wins for lifetime
            public Dictionary<string, int> VictoriesSeed = new Dictionary<string, int>(); // user id / wins for seed

            public Dictionary<string, List<string>> BlockedUsers = new Dictionary<string, List<string>>(); // users and the list of players they blocked from requesting duels with
            public List<string> Chat = new List<string>(); // user ids of those who opted out of seeing duel death messages
            public Dictionary<string, BetInfo> Bets = new Dictionary<string, BetInfo>(); // active bets users have placed
            public Dictionary<string, List<BetInfo>> ClaimBets = new Dictionary<string, List<BetInfo>>(); // active bets users need to claim after winning a bet
            public Dictionary<string, string> Homes = new Dictionary<string, string>(); // user id and location of where they teleported from
            public Dictionary<string, string> Kits = new Dictionary<string, string>(); // userid and kit. give kit when they wake up inside of the dueling zone
            public Dictionary<string, string> CustomKits = new Dictionary<string, string>(); // userid and custom kit

            public Dictionary<string, float> Zones = new Dictionary<string, float>(); // custom zone id / radius
            public List<string> ZoneIds = new List<string>(); // the locations of each dueling zone
            public List<string> Spawns = new List<string>(); // custom spawn points

            public int TotalDuels = 0; // the total amount of duels ever played on the server
            public bool DuelsEnabled = false; // enable/disable dueling for all players (not admins)
            public float Radius = 0f; // the radius of the dueling zone

            public StoredData() { }
        }

        public class DuelKitItem
        {
            public string container;
            public int amount;
            public ulong skin;
            public int slot;
            public string shortname;
            public string ammo;
            public List<string> mods;

            public DuelKitItem() { }
        }

        public class BetInfo
        {
            public string trigger { get; set; } = null; // the trigger used to request this as a bet
            public int amount { get; set; } = 0; // amount the player bet
            public int itemid { get; set; } = 0; // the unique identifier of the item
            public int max { get; set; } = 0; // the maximum amount allowed to bet on this item

            public bool Equals(BetInfo bet) => bet.amount == this.amount && bet.itemid == this.itemid;

            public BetInfo() { }
        }

        public class DuelingZone // Thanks @Jake_Rich for helping me get this started!
        {
            private HashSet<BasePlayer> _players { get; set; } = new HashSet<BasePlayer>();
            private HashSet<BasePlayer> _waiting { get; set; } = new HashSet<BasePlayer>();
            private Vector3 _zonePos { get; set; }
            private List<Vector3> _duelSpawns { get; set; } = new List<Vector3>(); // random spawn points generated on the fly
            private int _kills { get; set; } = 0; // create a new zone randomly on the map when the counter hits the configured amount once all duels are finished

            public DuelingZone() { }

            public DuelingZone(Vector3 position)
            {
                _zonePos = position;

                var spawns = ins.CreateSpawnPoints(position); // create spawn points on the fly

                _duelSpawns.Clear();
                _duelSpawns.AddRange(spawns);
            }
            
            public float Distance(Vector3 position)
            {
                return Vector3.Distance(_zonePos, position);
            }

            public int Kills
            {
                get
                {
                    return _kills;
                }
                set
                {
                    _kills = value;
                }
            }

            public int TotalPlayers
            {
                get
                {
                    return _players.Count;
                }
            }

            public List<Vector3> Spawns
            {
                get
                {
                    var spawns = GetSpawnPoints(this); // get custom spawn points if any exist
                    
                    return spawns == null || spawns.Count < 2 ? _duelSpawns : spawns;
                }
            }

            public bool IsFull
            {
                get
                {
                    return TotalPlayers + _waiting.Count + 2 > playersPerZone;
                }
            }

            public bool AddWaiting(BasePlayer player, BasePlayer target)
            {
                if (IsWaiting(player) || IsWaiting(target) || IsFull)
                    return false;

                _waiting.Add(player);
                _waiting.Add(target);

                return true;
            }

            public bool IsWaiting(BasePlayer player)
            {
                return _waiting.Contains(player);
            }

            public void AddPlayer(BasePlayer player)
            {
                if (_waiting.Contains(player))
                    _waiting.Remove(player);

                _players.Add(player);
            }

            public void RemovePlayer(string playerId)
            {
                _players.RemoveWhere(player => player.UserIDString == playerId);
            }
            
            public bool HasPlayer(string playerId)
            {
                return _players.FirstOrDefault(player => player.UserIDString == playerId) != null;
            }
            
            public void Kill()
            {
                foreach (var player in _players.ToList())
                {
                    if (player == null)
                        continue;

                    if (IsDueling(player))
                    {
                        player.inventory.Strip();
                        ins.SendHome(player);
                        ins.ResetDuelist(player.UserIDString);
                    }
                }

                if (duelingZones.Contains(this))
                    duelingZones.Remove(this);

                _players.Clear();
            }

            public Vector3 Position
            {
                get
                {
                    return _zonePos;
                }
            }
        }
        
        object OnDangerousOpen(Vector3 treasurePos) => DuelTerritory(treasurePos) ? (object)false : null; // Dangerous Treasures hook; prevent treasure event from starting in dueling zones

        object OnPlayerDeathMessage(BasePlayer victim, HitInfo info) // private plugin hook
        {
            if (DuelTerritory(victim.transform.position))
                return false;

            return null;
        }

        void Init()
        {
            SubscribeHooks(false); // turn off all hooks immediately
        }

        void Loaded()
        {
            ins = this;
            LoadVariables();

            monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>().Select(monument => monument.transform.position).ToList();
            duelsFile = Interface.Oxide.DataFileSystem.GetFile(Name);

            try
            {
                duelsData = duelsFile.ReadObject<StoredData>();
            }
            catch { }

            if (duelsData == null)
                duelsData = new StoredData();
        }

        void OnServerInitialized()
        {
            foreach (var bet in duelingBets.ToList()) // 0.1.5 fix - check itemList after server has initialized
            {
                if (ItemManager.itemList.Find(def => def.itemid == bet.itemid) == null)
                {
                    Puts("Bet itemid {0} is invalid.", bet.itemid);
                    duelingBets.Remove(bet);
                }
            }

            if (duelingKits.Count == 0)
                Puts(msg("KitsNotFound")); // warn that no kits were found and use the provided custom kits instead

            if (useAnnouncement)
                announceTimer = timer.Repeat(1800f, 0, () => DuelAnnouncement()); // TODO: add configuration option to set the time

            eventTimer = timer.Repeat(1f, 0, () => CheckDuelistMortality()); // kill players who haven't finished their duel in time. remove temporary immunity for duelers when it expires

            if (resetDuelists) // map wipe detected - award duelers and reset the data for the seed only
            {
                ResetDuelists();
                resetDuelists = false;
            }

            if (BasePlayer.activePlayerList.Count == 0)
            {
                ResetTemporaryData();
                RemoveZeroStats();
            }

            if (ZoneManager != null)
                SetupZoneManager();

            init = true;
            SetupZones();

            if (duelsData.Zones.Count > 0 && customArenasNoBuilding)
                Subscribe(nameof(CanBuild));
        }

        void OnServerSave() => timer.Once(5f, () => SaveData());
        void OnNewSave(string filename) => resetDuelists = true;

        void SaveData()
        {
            if (duelsFile != null && duelsData != null)
            {
                duelsFile.WriteObject(duelsData);
            }
        }

        void Unload()
        {
            announceTimer?.Destroy();
            eventTimer?.Destroy();

            foreach (var zone in duelingZones.ToList())
            {
                RemoveEntities(zone);
                zone.Kill();
            }

            duelingZones.Clear();
            ResetTemporaryData();
        }

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Title == "Economics")
                Economics = plugin;
            else if (plugin.Title == "ServerRewards")
                ServerRewards = plugin;
            else if (plugin.Title == "Kits")
                Kits = plugin;
            else if (plugin.Title == "ZoneManager")
                ZoneManager = plugin;
            else if (plugin.Title == "TruePVE")
                TruePVE = plugin;
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Title == "Economics")
                Economics = null;
            else if (plugin.Title == "ServerRewards")
                ServerRewards = null;
            else if (plugin.Title == "Kits")
                Kits = null;
            else if (plugin.Title == "ZoneManager")
                ZoneManager = null;
            else if (plugin.Title == "TruePVE")
                TruePVE = null;
        }

        object CanNetworkTo(BaseNetworkable entity, BasePlayer target) // temp hook
        {
            if (!init)
                return null;

            var player = entity as BasePlayer ?? (entity as HeldEntity)?.GetOwnerPlayer(); // 0.1.3 fix: check if player is null. sigh.

            if (player == null || target == null || player == target || (visibleToAdmins && target.IsAdmin))
                return null;

            if (duelsData.Duelers.Count > 0)
            {
                if (duelsData.Duelers.ContainsKey(player.UserIDString))
                {
                    if (DuelTerritory(player.transform.position))
                    {
                        return duelsData.Duelers[player.UserIDString] == target.UserIDString ? null : (object)false;
                    }
                }
            }
            else
                Unsubscribe(nameof(CanNetworkTo)); // nothing else to do right now, unsubscribe the hook

            return null;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason) // temp hook
        {
            if (init && IsDueling(player))
            {
                OnDuelistLost(player);
                RemoveDuelist(player.UserIDString);
                ResetDuelist(player.UserIDString, false);
                SendHome(player);

                if (duelsData.Duelers.Count == 0)
                    Unsubscribe(nameof(OnPlayerDisconnected)); // nothing else to do right now, unsubscribe the hook
            }
        }

        void OnPlayerSleepEnded(BasePlayer player) // temp hook
        {
            if (!init)
                return;

            if (DuelTerritory(player.transform.position))
            {
                if (IsDueling(player)) // setup the player once they've successfully spawned inside of the dueling zone
                {
                    foreach (var zone in duelingZones)
                    {
                        if (zone.IsWaiting(player))
                        {
                            player.metabolism.calories.value = player.metabolism.calories.max;
                            player.metabolism.hydration.value = player.metabolism.hydration.max;

                            if (deathTime > 0)
                            {
                                player.ChatMessage(msg("ExecutionTime", player.UserIDString, deathTime));
                                duelsData.Death[player.UserIDString] = TimeStamp() + (deathTime * 60);
                            }

                            if (showWarning)
                                player.ChatMessage(msg("DuelWarning", player.UserIDString));

                            GivePlayerKit(player);
                            Metabolize(player);

                            if (useInvisibility)
                                Disappear(player);

                            zone.AddPlayer(player);
                            return;
                        }
                    }
                }
            }
            
            if (duelsData.Homes.Count == 0 && duelsData.Duelers.Count == 0) // nothing else to do right now, unsubscribe the hook
                Unsubscribe(nameof(OnPlayerSleepEnded));
        }

        void OnPlayerRespawned(BasePlayer player) // temp hook
        {
            if (init && DuelTerritory(player.transform.position) && !duelsData.Duelers.ContainsKey(player.UserIDString))
            {
                var spawnPoint = ServerMgr.FindSpawnPoint();
                int retries = 25;

                while (DuelTerritory(spawnPoint.pos) && --retries > 0)
                {
                    spawnPoint = ServerMgr.FindSpawnPoint();
                }

                Teleport(player, spawnPoint.pos);
            }
        }

        void OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue) // perm hook
        {
            if (IsDueling(player)) // this should prevent the wounded state from ever happening.
            {
                if (newValue < 6f)
                {
                    player.health = 6f;
                    player.inventory.Strip();
                    OnDuelistLost(player);
                }
            }
        }

        void OnDuelistLost(BasePlayer victim)
        {
            if (duelEntities.ContainsKey(victim.userID))
            {
                foreach (var e in duelEntities[victim.userID].ToList())
                    if (e != null && !e.IsDestroyed)
                        e.Kill();

                duelEntities.Remove(victim.userID);
            }

            if (!duelsData.Duelers.ContainsKey(victim.UserIDString)) // this should never happen
            {
                NextTick(() => SendHome(victim));
                return;
            }
            
            string attackerId = duelsData.Duelers[victim.UserIDString];
            var attacker = BasePlayer.activePlayerList.Find(p => p.UserIDString == attackerId);
            string attackerName = attacker?.displayName ?? GetDisplayName(attackerId); // get the attackers name. it's possible the victim died by other means so we'll null check everything

            if (duelsData.Death.ContainsKey(victim.UserIDString)) duelsData.Death.Remove(victim.UserIDString); // remove them from automatic deaths
            if (duelsData.Death.ContainsKey(attackerId)) duelsData.Death.Remove(attackerId);
            if (duelsData.Duelers.ContainsKey(victim.UserIDString)) duelsData.Duelers.Remove(victim.UserIDString); // unset their status as duelers
            if (duelsData.Duelers.ContainsKey(attackerId)) duelsData.Duelers.Remove(attackerId);

            if (victim.metabolism != null)
            {
                victim.metabolism.oxygen.min = 0;
                victim.metabolism.oxygen.max = 1;
                victim.metabolism.temperature.min = -100;
                victim.metabolism.temperature.max = 100;
                victim.metabolism.wetness.min = 0;
                victim.metabolism.wetness.max = 1;
            }

            victim.inventory.Strip(); // also strip the attacker below after verifying

            if (!duelsData.LossesSeed.ContainsKey(victim.UserIDString)) // add data entries
                duelsData.LossesSeed.Add(victim.UserIDString, 0);

            if (!duelsData.Losses.ContainsKey(victim.UserIDString))
                duelsData.Losses.Add(victim.UserIDString, 0);

            if (!duelsData.VictoriesSeed.ContainsKey(attackerId))
                duelsData.VictoriesSeed.Add(attackerId, 0);

            if (!duelsData.Victories.ContainsKey(attackerId))
                duelsData.Victories.Add(attackerId, 0);

            duelsData.LossesSeed[victim.UserIDString]++; // increment said entries
            duelsData.Losses[victim.UserIDString]++;
            duelsData.VictoriesSeed[attackerId]++;
            duelsData.Victories[attackerId]++;
            duelsData.TotalDuels++; // increment the total number of duels on the server

            int victimLossesSeed = duelsData.LossesSeed[victim.UserIDString]; // grab information from data
            int victimVictoriesSeed = duelsData.VictoriesSeed.ContainsKey(victim.UserIDString) ? duelsData.VictoriesSeed[victim.UserIDString] : 0;
            int attackerLossesSeed = duelsData.LossesSeed.ContainsKey(attackerId) ? duelsData.LossesSeed[attackerId] : 0;
            int attackerVictoriesSeed = duelsData.VictoriesSeed[attackerId];
            var bet = duelsData.Bets.ContainsKey(attackerId) && duelsData.Bets.ContainsKey(victim.UserIDString) && duelsData.Bets[attackerId].Equals(duelsData.Bets[victim.UserIDString]) && !IsAllied(victim, attacker) ? duelsData.Bets[attackerId] : null; // victim bet his attacker and lost, use later to add a claim for the attacker

            Puts(RemoveFormatting(msg("DuelDeathMessage", null, attackerName, attackerVictoriesSeed, attackerLossesSeed, victim.displayName, victimVictoriesSeed, victimLossesSeed, Math.Round(attacker.health, 2), bet != null ? msg("BetWon", null, bet.trigger, bet.amount) : ""))); // send message to console

            if (Interface.CallHook("OnDuelDeathMessage", attacker, victim) == null) // hook to block message, also a private hook to add currency for starting deathmatch events based on duels won
            {
                foreach (var target in BasePlayer.activePlayerList.Where(p => p?.displayName != null && !duelsData.Chat.Contains(p.UserIDString))) // customize each message using language api
                {
                    if (!broadcastDefeat && target.UserIDString != victim.UserIDString && target.UserIDString != attackerId)
                        continue;

                    string betWon = bet != null ? msg("BetWon", target.UserIDString, bet.trigger, bet.amount) : "";
                    string message = msg("DuelDeathMessage", target.UserIDString, attackerName, attackerVictoriesSeed, attackerLossesSeed, victim.displayName, victimVictoriesSeed, victimLossesSeed, Math.Round(attacker.health, 2), betWon);
                    target.ChatMessage(message);
                }
            }

            if (bet != null && attacker != null) // award the bet to the attacker
            {
                var claimBet = new BetInfo() { itemid = bet.itemid, amount = bet.amount * 2, trigger = bet.trigger };

                if (!duelsData.ClaimBets.ContainsKey(attackerId))
                    duelsData.ClaimBets.Add(attackerId, new List<BetInfo>());

                duelsData.ClaimBets[attackerId].Add(claimBet);
                duelsData.Bets.Remove(attackerId);
                duelsData.Bets.Remove(victim.UserIDString);

                Puts(msg("ConsoleBetWon", null, attacker.displayName, attacker.UserIDString, victim.displayName, victim.UserIDString));
                attacker.ChatMessage(msg("NotifyBetWon", attacker.UserIDString, szDuelChatCommand));
            }

            RemoveDuelist(attackerId);

            if (attacker != null)
            {
                if (attacker.metabolism != null)
                {
                    attacker.metabolism.oxygen.min = 0;
                    attacker.metabolism.oxygen.max = 1;
                    attacker.metabolism.temperature.min = -100;
                    attacker.metabolism.temperature.max = 100;
                    attacker.metabolism.wetness.min = 0;
                    attacker.metabolism.wetness.max = 1;
                    attacker.metabolism.bleeding.value = 0;
                }

                attacker.inventory.Strip();

                if (economicsMoney > 0.0)
                {
                    if (Economics != null)
                    {
                        Economics?.Call("Deposit", attacker.userID, economicsMoney);
                        attacker.ChatMessage(msg("EconomicsDeposit", attacker.UserIDString, economicsMoney));
                    }
                }

                if (serverRewardsPoints > 0)
                {
                    if (ServerRewards != null)
                    {
                        var success = ServerRewards?.Call("AddPoints", attacker.userID, serverRewardsPoints);

                        if (success != null && success is bool && (bool)success)
                            attacker.ChatMessage(msg("ServerRewardPoints", attacker.UserIDString, serverRewardsPoints));
                    }
                }
            }

            var zone = RemoveDuelist(victim.UserIDString);

            if (zoneCounter > 0 && zone != null) // if new zones are set to spawn every X duels then increment by 1
            {
                zone.Kills++;

                if (zone.TotalPlayers == 0 && zone.Kills >= zoneCounter)
                {
                    RemoveDuelZone(zone);
                    SetupDuelZone(); // x amount of duels completed. time to relocate and start all over! changing the dueling zones location keeps things mixed up and entertaining for everyone. especially when there's issues with terrain
                    SaveData();
                }
            }
            
            NextTick(() =>
            {
                SendHome(attacker);
                SendHome(victim);
            });
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo) // temp hook
        {
            if (!init || entity == null || hitInfo == null)
                return null;

            if (entity.PrefabName.Equals(heswPrefab) || entity.PrefabName.Equals(hewwPrefab))
            {
                if (DuelTerritory(entity.transform.position, 15f))
                {
                    if (hitInfo.damageTypes != null && hitInfo.damageTypes.Has(DamageType.Decay))
                        NextTick(() => entity?.Heal(entity._maxHealth));

                    return false;
                }
                
                if (customArenasUseWallProtection && ArenaTerritory(entity.transform.position, 15f))
                {
                    if (hitInfo.damageTypes != null && hitInfo.damageTypes.Has(DamageType.Decay))
                        NextTick(() => entity?.Heal(entity._maxHealth));

                    return false;
                }
            }

            if (ArenaTerritory(entity.transform.position))
            {
                if ((entity is BuildingBlock || entity.name.Contains("deploy")) && customArenasNoRaiding)
                    return false;

                if (entity is BasePlayer && customArenasNoPVP)
                    return false;
            }

            var victim = entity as BasePlayer;
            var attacker = hitInfo.Initiator as BasePlayer;
            
            if (hitInfo.Initiator is BaseNpc && DuelTerritory(hitInfo.PointStart))
            {
                var npc = hitInfo.Initiator as BaseNpc; // would like to teleport away but simply setting it's position isn't enough due to the navmesh changes

                if (npc != null)
                {
                    if (fleeNpc) // TODO: needs improved. takes several hits to cause the npc to flee
                    { // cannot set AttackTarget to null without throwing a NullReferenceException in console on the next hit
                        npc.SetAiFlag(BaseNpc.AiFlags.Sitting, true);
                        npc.CurrentBehaviour = BaseNpc.Behaviour.Flee;
                    }
                    else if (putToSleep) // works every time
                    {
                        npc.SetAiFlag(BaseNpc.AiFlags.Sleeping, true);
                        npc.CurrentBehaviour = BaseNpc.Behaviour.Sleep;
                    }
                    else if (killNpc) // rip npc
                        npc.Kill();

                    return false;
                }
            }

            if (attacker != null && IsDueling(attacker) && victim != null) // 0.1.8 check attacker then victim
            {
                if (duelsData.Duelers[attacker.UserIDString] != victim.UserIDString)
                {
                    return false; // prevent attacker from doing damage to others
                }
            }

            if (victim != null && IsDueling(victim))
            {
                if (duelsData.Immunity.ContainsKey(victim.UserIDString))
                    return false; // immunity timer

                if (hitInfo.Initiator is BaseHelicopter)
                    return false; // protect duelers from helicopters

                if (attacker != null && duelsData.Duelers[victim.UserIDString] != attacker.UserIDString)
                    return false; // prevent attacker from doing damage to others
                
                if (TruePVE != null) // handle damage differently by subtracting their health instead
                {
                    if (damagePercentageScale > 0f) // apply our damage multiplier
                        hitInfo.damageTypes.ScaleAll(damagePercentageScale);

                    victim.baseProtection.Scale(hitInfo.damageTypes, 1f); // apply protection to the player

                    float amount = hitInfo.damageTypes.Total(); // get the total amount

                    if (hitInfo.isHeadshot) // double it
                        amount *= 2;

                    victim.health -= amount; // subtract their health
                }
                else if (damagePercentageScale > 0f)
                    hitInfo.damageTypes.ScaleAll(damagePercentageScale);
                
                return null;
            }

            var pointStart = hitInfo.Initiator?.transform?.position ?? hitInfo.PointStart; // 0.1.6 border fix
            var pointEnd = entity?.transform?.position ?? hitInfo.PointEnd; // PointEnd shouldn't ever be used

            if (DuelTerritory(pointStart))
            {
                if (entity is BuildingBlock || entity.name.Contains("deploy"))
                    return false; // block damage to structures and deployables to the outside

                if (!DuelTerritory(pointEnd))
                    return false; // block all damage to the outside
            }

            if (!DuelTerritory(pointStart))
            {
                if (DuelTerritory(pointEnd))
                    return false; // block all damage to the inside
            }

            return null;
        }

        void OnEntitySpawned(BaseNetworkable entity) // temp hook
        {
            if (!init || entity == null)
                return;

            if (prefabs.Any(x => x.Key == entity.PrefabName) && DuelTerritory(entity.transform.position))
            {
                var e = entity.GetComponent<BaseEntity>();

                if (!duelEntities.ContainsKey(e.OwnerID))
                    duelEntities.Add(e.OwnerID, new List<BaseEntity>());

                duelEntities[e.OwnerID].Add(e);
            }

            if (entity is PlayerCorpse)
            {
                NextTick(() =>
                {
                    if (entity != null && !entity.IsDestroyed && DuelTerritory(entity.transform.position))
                    {
                        entity.Kill();
                    }
                });
            }
            else if (entity is WorldItem)
            {
                if (duelsData.Homes.Count > 0)
                {
                    if (DuelTerritory(entity.transform.position))
                    {
                        NextTick(() => // prevent rpc kick by using NextTick since we're also hooking OnItemDropped
                        {
                            if (entity != null && !entity.IsDestroyed) // we must check this or you will still be rpc kicked
                            {
                                var worldItem = entity as WorldItem;

                                if (worldItem != null && worldItem.item != null)
                                {
                                    if (!IsThrownWeapon(worldItem.item)) // allow throwing weapons because they're low tier items and we want to allow players to be able to pick them up again. TODO: add config option to toggle this
                                    {
                                        //Puts("Destroying {0} ...", entity.ShortPrefabName);
                                        entity.Kill(); // destroy items which are dropped on player death
                                    }
                                }
                            }
                        });

                        if (entity != null && !entity.IsDestroyed)
                        {
                            timer.Repeat(0.1f, 20, () => // track the item to make sure it wasn't thrown out of the dueling zone
                            {
                                if (entity != null && !entity.IsDestroyed) // verify another plugin didn't destroy it already
                                {
                                    if (!DuelTerritory(entity.transform.position)) // yep, someone threw it out of the dueling zone. so we'll prevent this from being abused now
                                    {
                                        entity.Kill(); // destroy items which are dropped from inside to outside of the zone
                                    }
                                }
                            });
                        }
                    }
                }
            }
        }

        object CanBuild(Planner plan, Construction prefab) // temp hook
        {
            if (!init)
                return null;

            var player = plan.GetOwnerPlayer(); // get the user holding the building plan

            if (player.IsAdmin)
                return null;

            var position = player.transform.position; // get the estimated position of where the player is trying to build at
            var buildPos = position + (player.eyes.BodyForward() * 4f);
            var up = buildPos + Vector3.up + new Vector3(0f, 0.6f, 0f);

            buildPos.y = Mathf.Max(position.y, up.y); // adjust the cursor position to our best estimate

            if (DuelTerritory(buildPos, buildingBlockExtensionRadius) || (customArenasNoBuilding && ArenaTerritory(buildPos, buildingBlockExtensionRadius))) // extend the distance slightly
            {
                if (deployables.Count > 0)
                {
                    var kvp = prefabs.FirstOrDefault(x => x.Key == prefab.fullName);

                    if (duelsData.Duelers.ContainsKey(player.UserIDString) && !string.IsNullOrEmpty(kvp.Value) && deployables.ContainsKey(kvp.Value) && deployables[kvp.Value])
                    {
                        return null;
                    }
                }

                player.ChatMessage(msg("Building is blocked!", player.UserIDString)); // no dice today bud
                return false;
            }

            return null;
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity) // stop all players from looting anything inside of dueling zones. this allows server owners to setup duels anywhere without worry.
        {
            if (!init)
                return;

            if (player != null && IsDueling(player))
                timer.Once(0.01f, player.EndLooting);

            if (duelsData.Duelers.Count == 0)
                Unsubscribe(nameof(OnLootEntity));
        }

        object OnCreateWorldProjectile(HitInfo info, Item item) // temp hook. prevents thrown items from becoming stuck in players when they respawn and requiring them to relog to remove them
        {
            if (!init)
                return null;

            if (duelsData.Duelers.Count == 0)
                Unsubscribe(nameof(OnCreateWorldProjectile));

            if (info?.HitEntity?.ToPlayer() != null && IsDueling(info.HitEntity.ToPlayer())) // any of this could be null so check everything
                return false; // block it

            if (info?.InitiatorPlayer != null && IsDueling(info.InitiatorPlayer))
                return false;

            return null;
        }

        void OnItemDropped(Item item, BaseEntity entity) // temp hook
        {
            if (!init || item?.GetOwnerPlayer() == null) // null checks
                return;
            
            var player = item.GetOwnerPlayer();

            if (!IsThrownWeapon(item) && IsDueling(player))
            {
                //Puts("Removing {0} ...", item.info.shortname);
                item?.Remove(0.01f); // do NOT allow players to drop items. this is a dueling zone. not a gift zone.
            }

            if (duelsData.Duelers.Count == 0) // nothing left to do here, unsubscribe the hook
                Unsubscribe(nameof(OnItemDropped));
        }

        object CanEventJoin(BasePlayer player) // EventManager
        {
            return init && IsDueling(player) ? msg("CannotEventJoin", player.UserIDString) : null;
        }

        object canRemove(BasePlayer player) // RemoverTool
        {
            return init && IsDueling(player) ? (object)false : null;
        }
        
        object CanTrade(BasePlayer player) // Trade
        {
            return init && IsDueling(player) ? (object)false : null;
        }

        object CanShop(BasePlayer player) // Shop and ServerRewards
        {
            return init && IsDueling(player) ? (object)false : null;
        }

        object canShop(BasePlayer player) // Shop and ServerRewards
        {
            return init && IsDueling(player) ? (object)false : null;
        }

        object CanBePenalized(BasePlayer player) // ZLevels Remastered
        {
            return init && DuelTerritory(player.transform.position) || duelsData.Duelers.ContainsKey(player.UserIDString) || ArenaTerritory(player.transform.position) ? (object)false : null;
        }

        object canTeleport(BasePlayer player) // 0.1.2: block teleport from NTeleportation plugin
        {
            return init && IsDueling(player) ? msg("CannotTeleport", player.UserIDString) : null;
        }

        object CanTeleport(BasePlayer player) // 0.1.2: block teleport from MagicTeleportation plugin
        {
            return init && IsDueling(player) ? msg("CannotTeleport", player.UserIDString) : null;
        }
        
        #region SpawnPoints
        void SendSpawnHelp(BasePlayer player)
        {
            player.ChatMessage(msg("SpawnCount", player.UserIDString, duelsData.Spawns.Count));
            player.ChatMessage(msg("SpawnAdd", player.UserIDString, szDuelChatCommand));
            player.ChatMessage(msg("SpawnHere", player.UserIDString, szDuelChatCommand));
            player.ChatMessage(msg("SpawnRemove", player.UserIDString, szDuelChatCommand, spRemoveOneMaxDistance));
            player.ChatMessage(msg("SpawnRemoveAll", player.UserIDString, szDuelChatCommand, spRemoveAllMaxDistance));
            player.ChatMessage(msg("SpawnWipe", player.UserIDString, szDuelChatCommand));
        }

        void AddSpawnPoint(BasePlayer player, bool useHit)
        {
            var spawn = player.transform.position;

            if (useHit)
            {
                RaycastHit hit;
                if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, groundMask))
                {
                    player.ChatMessage(msg("FailedRaycast", player.UserIDString));
                    return;
                }
                
                spawn = hit.point;                   
            }

            if (duelsData.Spawns.Contains(spawn.ToString()))
            {
                player.ChatMessage(msg("SpawnExists", player.UserIDString));
                return;
            }

            duelsData.Spawns.Add(spawn.ToString());
            player.SendConsoleCommand("ddraw.text", spDrawTime, Color.green, spawn, "+S");
            player.ChatMessage(msg("SpawnAdded", player.UserIDString, FormatPosition(spawn)));
        }

        void RemoveSpawnPoint(BasePlayer player)
        {
            float radius = spRemoveOneMaxDistance;
            var spawn = Vector3.zero;
            float dist = radius;

            foreach (var entry in duelsData.Spawns.ToList())
            {
                var _spawn = entry.ToVector3();
                float distance = Vector3.Distance(player.transform.position, _spawn);

                if (distance < dist)
                {
                    dist = distance;
                    spawn = _spawn;
                }
            }

            if (spawn != Vector3.zero)
            {
                duelsData.Spawns.Remove(spawn.ToString());
                player.SendConsoleCommand("ddraw.text", spDrawTime, Color.red, spawn, "-S");
                player.ChatMessage(msg("SpawnRemoved", player.UserIDString, 1));
            }
            else
                player.ChatMessage(msg("SpawnNoneFound", player.UserIDString, radius)); 
        }

        void RemoveSpawnPoints(BasePlayer player)
        {
            int count = 0;

            foreach (var entry in duelsData.Spawns.ToList())
            {
                var spawn = entry.ToVector3();

                if (Vector3.Distance(player.transform.position, spawn) <= spRemoveAllMaxDistance)
                {
                    count++;
                    duelsData.Spawns.Remove(entry);
                    player.SendConsoleCommand("ddraw.text", spDrawTime, Color.red, spawn, "-S");
                }
            }

            if (count == 0)
                player.ChatMessage(msg("SpawnNoneFound", player.UserIDString, spRemoveAllMaxDistance));
            else
                player.ChatMessage(msg("SpawnRemoved", player.UserIDString, count));
        }

        void WipeSpawnPoints(BasePlayer player)
        {
            if (duelsData.Spawns.Count == 0)
            {
                player.ChatMessage(msg("SpawnNoneExist", player.UserIDString));
                return;
            }

            var spawns = duelsData.Spawns.Select(spawn => spawn.ToVector3()).ToList();

            foreach (var spawn in spawns)
                player.SendConsoleCommand("ddraw.text", 30f, Color.red, spawn, "-S");

            int amount = duelsData.Spawns.Count();
            duelsData.Spawns.Clear();
            spawns.Clear();
            player.ChatMessage(msg("SpawnWiped", player.UserIDString, amount));            
        }

        static List<Vector3> GetSpawnPoints(DuelingZone zone)
        {
            return duelsData.Spawns.Select(entry => entry.ToVector3()).Where(spawn => zone.Distance(spawn) < zoneRadius).ToList();
        }

        string FormatPosition(Vector3 position)
        {
            string x = Math.Round(position.x, 2).ToString();
            string y = Math.Round(position.y, 2).ToString();
            string z = Math.Round(position.z, 2).ToString();

            return $"{x} {y} {z}";
        }
        #endregion
        
        void cmdQueue(BasePlayer player, string command, string[] args)
        {
            if (!init)
            {
                return;
            }

            if (Interface.CallHook("CanDuel", player) != null)
            {
                player.ChatMessage(msg("CannotDuel", player.UserIDString));
                return;
            }

            if (IsEventBanned(player.UserIDString))
            {
                player.ChatMessage(msg("Banned", player.UserIDString));
                return;
            }

            if (!duelsData.DuelsEnabled)
            {
                player.ChatMessage(msg("DuelsDisabled", player.UserIDString));
                if (!player.IsAdmin) return;
            }

            if (duelsData.ZoneIds.Count == 0 || duelingZones.Count == 0)
            {
                player.ChatMessage(msg("NoZoneExists", player.UserIDString));
                return;
            }

            if (!autoAllowAll && !duelsData.Allowed.Contains(player.UserIDString))
            {
                player.ChatMessage(msg("MustAllowDuels", player.UserIDString, szDuelChatCommand));
                return;
            }

            if (DuelTerritory(player.transform.position))
            {
                RemoveFromQueue(player.UserIDString);

                if (!player.IsAdmin && duelsData.Duelers.ContainsKey(player.UserIDString))
                {
                    player.ChatMessage(msg("AlreadyInADuel", player.UserIDString));
                    return;
                }
            }

            if (duelsData.Requests.ContainsKey(player.UserIDString) || duelsData.Requests.ContainsValue(player.UserIDString))
            {
                RemoveFromQueue(player.UserIDString);
                player.ChatMessage(msg("PendingRequest", player.UserIDString, szDuelChatCommand));
                return;
            }

            if (!IsNewman(player))
            {
                player.ChatMessage(msg("MustBeNaked", player.UserIDString));
                return;
            }

            if (!duelsData.Queued.ContainsValue(player.UserIDString))
            {
                var stamp = TimeStamp();

                if (duelsData.Queued.ContainsKey(stamp))
                    stamp++;

                if (!duelsData.Queued.ContainsKey(stamp))
                {
                    duelsData.Queued.Add(stamp, player.UserIDString);
                    player.ChatMessage(msg("InQueueSuccess", player.UserIDString));
                    CheckQueue();
                }
                else
                    player.ChatMessage(msg("TryQueueAgain", player.UserIDString));

                return;
            }

            if (RemoveFromQueue(player.UserIDString))
                player.ChatMessage(msg("NoLongerQueued", player.UserIDString));
        }

        void cmdLadder(BasePlayer player, string command, string[] args)
        {
            if (!init)
                return;

            bool onLadder = false;
            bool life = args.Any(arg => arg.ToLower().Contains("life"));
            var sorted = life ? duelsData.Victories.ToList<KeyValuePair<string, int>>() : duelsData.VictoriesSeed.ToList<KeyValuePair<string, int>>();
            sorted.Sort((x, y) => y.Value.CompareTo(x.Value));

            player.ChatMessage(msg(life ? "TopAll" : "Top", player.UserIDString, sorted.Count));

            for (int i = 0; i < 10; i++)
            {
                if (i >= sorted.Count)
                    break;

                if (sorted[i].Key == player.UserIDString)
                    onLadder = true; // 0.1.2: fix for ranks showing user on ladder twice

                string name = GetDisplayName(sorted[i].Key);
                int losses = 0;

                if (life)
                    losses = duelsData.Losses.ContainsKey(sorted[i].Key) ? duelsData.Losses[sorted[i].Key] : 0;
                else
                    losses = duelsData.LossesSeed.ContainsKey(sorted[i].Key) ? duelsData.LossesSeed[sorted[i].Key] : 0;

                double ratio = losses > 0 ? Math.Round((double)sorted[i].Value / (double)losses, 2) : sorted[i].Value;
                string message = msg("TopFormat", player.UserIDString, (i + 1).ToString(), name, sorted[i].Value, losses, ratio);
                player.SendConsoleCommand("chat.add", Convert.ToUInt64(sorted[i].Key), message, 1.0);
            }

            if (!onLadder && !life && duelsData.VictoriesSeed.ContainsKey(player.UserIDString))
            {
                int index = sorted.FindIndex(kvp => kvp.Key == player.UserIDString);
                int losses = duelsData.LossesSeed.ContainsKey(player.UserIDString) ? duelsData.LossesSeed[player.UserIDString] : 0;
                double ratio = losses > 0 ? Math.Round((double)duelsData.VictoriesSeed[player.UserIDString] / (double)losses, 2) : duelsData.VictoriesSeed[player.UserIDString];
                string message = msg("TopFormat", player.UserIDString, index, player.displayName, duelsData.VictoriesSeed[player.UserIDString], losses, ratio);
                player.SendConsoleCommand("chat.add", player.userID, message, 1.0);
            }

            if (!onLadder && life && duelsData.Victories.ContainsKey(player.UserIDString))
            {
                int index = sorted.FindIndex(kvp => kvp.Key == player.UserIDString);
                int losses = duelsData.Losses.ContainsKey(player.UserIDString) ? duelsData.Losses[player.UserIDString] : 0;
                double ratio = losses > 0 ? Math.Round((double)duelsData.Victories[player.UserIDString] / (double)losses, 2) : duelsData.Victories[player.UserIDString];
                string message = msg("TopFormat", player.UserIDString, index, player.displayName, duelsData.Victories[player.UserIDString], losses, ratio);
                player.SendConsoleCommand("chat.add", player.userID, message, 1.0);
            }

            if (!life) player.ChatMessage(msg("LadderLife", player.UserIDString, szDuelChatCommand));
            sorted.Clear();
            sorted = null;
        }
        
        private void ccmdDuel(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            string id = arg.Player()?.UserIDString ?? null;

            if (arg.HasArgs(1))
            {
                switch (arg.Args[0].ToLower())
                {
                    case "1":
                    case "enable":
                    case "on":
                        {
                            if (duelsData.DuelsEnabled)
                            {
                                arg.ReplyWith(msg("DuelsEnabledAlready", id));
                                return;
                            }

                            duelsData.DuelsEnabled = true;
                            arg.ReplyWith(msg("DuelsNowEnabled", id));
                            DuelAnnouncement();
                            SaveData();
                            return;
                        }
                    case "0":
                    case "disable":
                    case "off":
                        {
                            if (!duelsData.DuelsEnabled)
                            {
                                arg.ReplyWith(msg("DuelsDisabledAlready", id));
                                return;
                            }

                            duelsData.DuelsEnabled = false;

                            if (duelsData.Duelers.Count > 0)
                                arg.ReplyWith(msg("DuelsNowDisabled", id));
                            else
                                arg.ReplyWith(msg("DuelsNowDisabledEmpty", id));

                            foreach (var entry in duelsData.Duelers.ToList())
                            {
                                if (duelsData.Homes.ContainsKey(entry.Key))
                                {
                                    var target = BasePlayer.activePlayerList.Find(p => p.UserIDString == entry.Key);

                                    if (target != null)
                                    {
                                        if (DuelTerritory(target.transform.position))
                                        {
                                            target.inventory.Strip();
                                            SendHome(target);
                                        }
                                    }
                                }

                                RemoveDuelist(entry.Key);
                                ResetDuelist(entry.Key);
                            }

                            SaveData();
                            return;
                        }
                    case "new":
                        {
                            if (duelsData.ZoneIds.Count >= zoneAmount)
                            {
                                arg.ReplyWith(msg("ZoneLimit", id, zoneAmount));
                                return;
                            }

                            if (SetupDuelZone() != Vector3.zero)
                            {
                                arg.ReplyWith(msg("ZoneCreated", id));
                            }
                            return;
                        }
                    default:
                        {
                            arg.ReplyWith(string.Format("{0} on|off|new", szDuelChatCommand));
                            break;
                        }
                }
            }
            else
                arg.ReplyWith(string.Format("{0} on|off|new", szDuelChatCommand));
        }

        void cmdDuel(BasePlayer player, string command, string[] args)
        {
            if (!init)
            {
                return;
            }

            if (IsEventBanned(player.UserIDString))
            {
                player.ChatMessage(msg("Banned", player.UserIDString));
                return;
            }

            if (args.Length >= 1 && args[0] == "ladder")
            {
                cmdLadder(player, command, args);
                return;
            }

            if (!duelsData.DuelsEnabled)
            {
                if (!args.Any(arg => arg.ToLower() == "on"))
                    player.ChatMessage(msg("DuelsDisabled", player.UserIDString));

                if (!player.IsAdmin)
                    return;                
            }

            bool noZone = duelsData.ZoneIds.Count == 0 || duelingZones.Count == 0;

            if (noZone)
            {
                if (!args.Any(arg => arg.ToLower() == "new"))
                    player.ChatMessage(msg("NoZoneExists", player.UserIDString));

                if (!player.IsAdmin)
                    return;
            }

            if (!noZone && !duelsData.DuelsEnabled)
                if (!args.Any(arg => arg.ToLower() == "on"))
                    player.ChatMessage(msg("DuelsMustBeEnabled", player.UserIDString, szDuelChatCommand));

            if (IsDueling(player) && !player.IsAdmin)
                return;

            if (args.Length == 0)
            {
                player.ChatMessage(msg("HelpDuels", player.UserIDString, duelsData.TotalDuels.ToString("N0")));
                player.ChatMessage(msg("HelpAllow", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpBlock", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpChallenge", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpAccept", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpCancel", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpChat", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpQueue", player.UserIDString, szQueueChatCommand));
                player.ChatMessage(msg("HelpLadder", player.UserIDString, szDuelChatCommand));
                
                if (allowBets)
                    player.ChatMessage(msg("HelpBet", player.UserIDString, szDuelChatCommand));

                if (player.IsAdmin)
                {
                    player.ChatMessage(msg("HelpDuelAdmin", player.UserIDString, szDuelChatCommand));
                    player.ChatMessage(msg("HelpDuelAdminRefundAll", player.UserIDString, szDuelChatCommand));
                }

                return;
            }

            switch (args[0].ToLower())
            {
                case "walls":
                    {
                        if (player.IsAdmin && player.net.connection.authLevel >= minWallAuthLevel)
                        {
                            if (args.Length >= 2)
                            {
                                float radius;
                                if (float.TryParse(args[1], out radius) && radius > 2f)
                                {
                                    if (radius > maxCustomWallRadius)
                                        radius = maxCustomWallRadius;

                                    RaycastHit hit;
                                    if (Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, wallMask))
                                    {
                                        string prefab = customArenasUseWooden ? hewwPrefab : heswPrefab;

                                        if (args.Any(arg => arg.ToLower().Contains("stone")))
                                            prefab = heswPrefab;
                                        else if (args.Any(arg => arg.ToLower().Contains("wood")))
                                            prefab = hewwPrefab;
                                        
                                        duelsData.Zones[hit.point.ToString()] = radius;
                                        CreateZoneWalls(hit.point, radius, prefab, player);

                                        if (duelsData.Zones.Count == 1 && customArenasNoBuilding)
                                            Subscribe(nameof(CanBuild));
                                    }
                                    else
                                        player.ChatMessage(msg("FailedRaycast", player.UserIDString));
                                }
                                else
                                    player.ChatMessage(msg("InvalidNumber", player.UserIDString, args[1]));
                            }
                            else
                            {
                                if (!RemoveCustomZoneWalls(player.transform.position))
                                    player.ChatMessage(msg("WallSyntax", player.UserIDString, szDuelChatCommand));

                                foreach(var entry in duelsData.Zones)
                                    player.SendConsoleCommand("ddraw.text", 30f, Color.yellow, entry.Key.ToVector3(), entry.Value);
                            }

                            return;
                        }
                    }
                    break;
                case "remove_all_walls":
                    {
                        if (player.IsAdmin)
                        {
                            int removed = 0;

                            foreach(var entity in BaseEntity.serverEntities.Where(e => e.PrefabName.Equals(heswPrefab) || e.PrefabName.Equals(hewwPrefab)).Cast<BaseEntity>().ToList())
                            {
                                if (entity.OwnerID > 0 && !entity.OwnerID.IsSteamId())
                                {
                                    entity.Kill();
                                    removed++;
                                }
                            }

                            player.ChatMessage(msg("RemovedXWalls", player.UserIDString, removed));
                            return;
                        }
                    }
                    break;
                case "0":
                case "disable":
                case "off":
                    {
                        if (player.IsAdmin)
                        {
                            if (!duelsData.DuelsEnabled)
                            {
                                player.ChatMessage(msg("DuelsDisabledAlready", player.UserIDString));
                                return;
                            }

                            duelsData.DuelsEnabled = false;

                            if (duelsData.Duelers.Count > 0)
                                player.ChatMessage(msg("DuelsNowDisabled", player.UserIDString));
                            else
                                player.ChatMessage(msg("DuelsNowDisabledEmpty", player.UserIDString));

                            foreach (var entry in duelsData.Duelers.ToList())
                            {
                                if (duelsData.Homes.ContainsKey(entry.Key))
                                {
                                    var target = BasePlayer.activePlayerList.Find(p => p.UserIDString == entry.Key);

                                    if (target != null)
                                    {
                                        if (DuelTerritory(target.transform.position))
                                        {
                                            target.inventory.Strip();
                                            SendHome(target);
                                        }
                                    }
                                }

                                RemoveDuelist(entry.Key);
                                ResetDuelist(entry.Key);
                            }

                            SaveData();
                            return;
                        }
                        break;
                    }
                case "1":
                case "enable":
                case "on":
                    {
                        if (player.IsAdmin)
                        {
                            if (duelsData.DuelsEnabled)
                            {
                                player.ChatMessage(msg("DuelsEnabledAlready", player.UserIDString));
                                return;
                            }

                            duelsData.DuelsEnabled = true;
                            player.ChatMessage(msg("DuelsNowEnabled", player.UserIDString));
                            DuelAnnouncement();
                            SaveData();
                            return;
                        }
                        break;
                    }
                case "custom":
                case "me":
                    {
                        if (player.IsAdmin)
                        {
                            if (duelsData.ZoneIds.Count >= zoneAmount)
                            {
                                player.ChatMessage(msg("ZoneLimit", player.UserIDString, zoneAmount));
                                return;
                            }

                            RaycastHit hit;

                            if (Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, wallMask))
                            {
                                if (DuelTerritory(hit.point, zoneRadius))
                                {
                                    player.ChatMessage(msg("ZoneExists", player.UserIDString));
                                    return;
                                }

                                var zone = SetupDuelZone(hit.point);
                                int i = 0;

                                foreach (var spawn in zone.Spawns)
                                    player.SendConsoleCommand("ddraw.text", 30f, Color.yellow, spawn, ++i);
                            }
                            else
                                player.ChatMessage(msg("FailedRaycast", player.UserIDString));

                            return;
                        }
                    }
                    break;
                case "remove":
                    {
                        if (player.IsAdmin)
                        {
                            if (duelingZones.Count > 0)
                            {
                                var zone = GetDuelZone(player.transform.position);

                                if (zone == null)
                                {
                                    player.ChatMessage(msg("NoZoneFound", player.UserIDString));
                                    return;
                                }

                                if (spAutoRemove && duelsData.Spawns.Count > 0)
                                {
                                    foreach(var spawn in zone.Spawns.ToList())
                                    {
                                        if (duelsData.Spawns.Contains(spawn.ToString()))
                                        {
                                            duelsData.Spawns.Remove(spawn.ToString());
                                        }
                                    }
                                }

                                RemoveDuelZone(zone);
                                player.ChatMessage(msg("RemovedZone", player.UserIDString));
                            }
                            else
                                player.ChatMessage(msg("NoZonesExist", player.UserIDString));

                            return;
                        }
                    }
                    break;
                case "removeall":
                    {
                        if (player.IsAdmin)
                        {
                            if (duelingZones.Count > 0)
                            {
                                foreach (var zone in duelingZones.ToList())
                                {
                                    if (spAutoRemove && duelsData.Spawns.Count > 0)
                                    {
                                        foreach (var spawn in zone.Spawns.ToList())
                                        {
                                            if (duelsData.Spawns.Contains(spawn.ToString()))
                                            {
                                                duelsData.Spawns.Remove(spawn.ToString());
                                            }
                                        }
                                    }

                                    player.ChatMessage(msg("RemovedZoneAt", player.UserIDString, zone.Position));
                                    RemoveDuelZone(zone);
                                }
                            }
                            else
                                player.ChatMessage(msg("NoZonesExist", player.UserIDString));
                        }
                    }
                    break;
                case "spawns":
                    {
                        if (player.IsAdmin)
                        {
                            if (args.Length >= 2)
                            {
                                switch (args[1].ToLower())
                                {
                                    case "add":
                                        AddSpawnPoint(player, true);
                                        break;
                                    case "here":
                                        AddSpawnPoint(player, false);
                                        break;
                                    case "remove":
                                        RemoveSpawnPoint(player);
                                        break;
                                    case "removeall":
                                        RemoveSpawnPoints(player);
                                        break;
                                    case "wipe":
                                        WipeSpawnPoints(player);
                                        break;
                                    default:
                                        SendSpawnHelp(player);
                                        break;
                                }

                                return;
                            }
                            else
                                SendSpawnHelp(player);

                            int i = 0;
                            float dist = float.MaxValue;
                            DuelingZone destZone = null;

                            foreach (var zone in duelingZones)
                            {
                                if (zone.Distance(player.transform.position) > zoneRadius + 200f)
                                    continue;

                                float distance = zone.Distance(player.transform.position);

                                if (distance < dist)
                                {
                                    dist = distance;
                                    destZone = zone;
                                }
                            }

                            if (destZone != null)
                            {
                                foreach (var spawn in destZone.Spawns)
                                {
                                    player.SendConsoleCommand("ddraw.text", 30f, Color.yellow, spawn, ++i);
                                }
                            }

                            return;
                        }
                    }
                    break;
                case "new":
                    {
                        if (player.IsAdmin)
                        {
                            if (duelsData.ZoneIds.Count >= zoneAmount)
                            {
                                player.ChatMessage(msg("ZoneLimit", player.UserIDString, zoneAmount));
                                return;
                            }

                            if (SetupDuelZone() != Vector3.zero)
                            {
                                player.ChatMessage(msg("ZoneCreated", player.UserIDString));

                                if (args.Length == 2 && args[1] == "tp")
                                {
                                    var zonePos = duelingZones[duelingZones.Count - 1].Position;
                                    Player.Teleport(player, zonePos);
                                }
                            }

                            return;
                        }
                    }
                    break;
                case "tp":
                    {
                        if (player.IsAdmin)
                        {
                            float dist = float.MaxValue;
                            var dest = Vector3.zero;

                            foreach (var zone in duelingZones)
                            {
                                float distance = zone.Distance(player.transform.position);

                                if (duelingZones.Count > 1 && distance < zoneRadius * 4f) // move admin to the next nearest zone
                                    continue;

                                if (distance < dist)
                                {
                                    dist = distance;
                                    dest = zone.Position;
                                }
                            }

                            if (dest != Vector3.zero)
                                Player.Teleport(player, dest);
                        }
                    }
                    break;
                case "save":
                    {
                        if (player.IsAdmin)
                        {
                            SaveData();
                            player.ChatMessage(msg("DataSaved", player.UserIDString));
                            return;
                        }
                    }
                    break;
                case "ban":
                    {
                        if (player.IsAdmin && args.Length >= 2)
                        {
                            string targetId = args[1].IsSteamId() ? args[1] : FindPlayer(args[1])?.UserIDString ?? null;

                            if (string.IsNullOrEmpty(targetId))
                            {
                                player.ChatMessage(msg("PlayerNotFound", player.UserIDString, args[1]));
                                return;
                            }

                            if (!duelsData.Bans.ContainsKey(targetId))
                            {
                                duelsData.Bans.Add(targetId, player.UserIDString);
                                player.ChatMessage(msg("AddedBan", player.UserIDString, targetId));
                            }
                            else
                            {
                                duelsData.Bans.Remove(targetId);
                                player.ChatMessage(msg("RemovedBan", player.UserIDString, targetId));
                            }

                            SaveData();
                            return;
                        }
                    }
                    break;
                case "announce":
                    {
                        if (player.IsAdmin)
                        {
                            DuelAnnouncement();
                            return;
                        }
                    }
                    break;
                case "claim":
                    {
                        if (!duelsData.ClaimBets.ContainsKey(player.UserIDString))
                        {
                            player.ChatMessage(msg("NoBetsToClaim", player.UserIDString));
                            return;
                        }

                        foreach (var bet in duelsData.ClaimBets[player.UserIDString].ToList())
                        {
                            var item = ItemManager.CreateByItemID(bet.itemid, bet.amount);

                            if (!item.MoveToContainer(player.inventory.containerMain, -1))
                            {
                                var position = player.transform.position;
                                item.Drop((position + new Vector3(0f, 1f, 0f)) + (position / 2f), (position + new Vector3(0f, 0.2f, 0f)) * 8f); // Credit: Slack comment by @visagalis
                            }

                            string message = msg("PlayerClaimedBet", player.UserIDString, item.info.shortname, item.amount);

                            player.ChatMessage(message);
                            Puts("{0} ({1}) - {2}", player.displayName, player.UserIDString, message);
                            duelsData.ClaimBets[player.UserIDString].Remove(bet);

                            if (duelsData.ClaimBets[player.UserIDString].Count == 0)
                            {
                                duelsData.ClaimBets.Remove(player.UserIDString);
                                player.ChatMessage(msg("AllBetsClaimed", player.UserIDString));
                            }
                        }
                    }
                    return;
                case "chat":
                    {
                        if (!duelsData.Chat.Contains(player.UserIDString))
                        {
                            duelsData.Chat.Add(player.UserIDString);
                            player.ChatMessage(msg("DuelChatOff", player.UserIDString));
                            return;
                        }

                        duelsData.Chat.Remove(player.UserIDString);
                        player.ChatMessage(msg("DuelChatOn", player.UserIDString));
                        return;
                    }
                case "queue":
                case "que":
                case "q":
                    {
                        cmdQueue(player, command, args);
                        return;
                    }
                case "kit":
                    {
                        string kits = string.Join(", ", (duelingKits.Count > 0 ? duelingKits.ToArray() : customKits.Count > 0 ? customKits.Keys.ToArray() : new string[0]));
                        
                        if (args.Length == 2 && !string.IsNullOrEmpty(kits))
                        {
                            if (customKits.Any(entry => entry.Key.Equals(args[1], StringComparison.CurrentCultureIgnoreCase)) ||  duelingKits.Any(entry => entry.Equals(args[1], StringComparison.CurrentCultureIgnoreCase)))
                            {
                                duelsData.CustomKits[player.UserIDString] = args[1];
                                player.ChatMessage(msg("KitSet", player.UserIDString, args[1]));
                            }
                            else
                                player.ChatMessage(msg("KitDoesntExist", player.UserIDString, args[1]));

                            return;
                        }

                        if (duelsData.CustomKits.ContainsKey(player.UserIDString))
                        {
                            duelsData.CustomKits.Remove(player.UserIDString);
                            player.ChatMessage(msg("ResetKit", player.UserIDString));
                        }

                        if (!string.IsNullOrEmpty(kits))
                            player.ChatMessage("Kits: " + kits);
                        else
                            player.ChatMessage(msg("KitsNotConfigured", player.UserIDString));
                    }
                    return;
                case "allow":
                    {
                        if (!duelsData.Allowed.Contains(player.UserIDString))
                        {
                            duelsData.Allowed.Add(player.UserIDString);
                            player.ChatMessage(msg("PlayerRequestsOn", player.UserIDString));
                            return;
                        }

                        duelsData.Allowed.Remove(player.UserIDString);
                        player.ChatMessage(msg("PlayerRequestsOff", player.UserIDString));
                        return;
                    }
                case "block":
                    {
                        if (args.Length >= 2)
                        {
                            var name = string.Join(" ", args.Skip(1).ToArray());
                            BasePlayer target = FindPlayer(name);

                            if (target == null)
                            {
                                player.ChatMessage(msg("PlayerNotFound", player.UserIDString, name));
                                return;
                            }

                            if (!duelsData.BlockedUsers.ContainsKey(player.UserIDString))
                            {
                                var blocked = new List<string>();
                                blocked.Add(target.UserIDString);
                                duelsData.BlockedUsers.Add(player.UserIDString, blocked);
                                player.ChatMessage(msg("BlockedRequestsFrom", player.UserIDString, target.displayName));
                                return;
                            }

                            if (duelsData.BlockedUsers[player.UserIDString].Contains(target.UserIDString))
                            {
                                duelsData.BlockedUsers[player.UserIDString].Remove(target.UserIDString);
                                player.ChatMessage(msg("UnblockedRequestsFrom", player.UserIDString, target.displayName));
                                return;
                            }

                            duelsData.BlockedUsers[player.UserIDString].Add(target.UserIDString);
                            player.ChatMessage(msg("BlockedRequestsFrom", player.UserIDString, target.displayName));
                            return;
                        }

                        if (duelsData.Allowed.Contains(player.UserIDString))
                        {
                            duelsData.Allowed.Remove(player.UserIDString);
                            player.ChatMessage(msg("PlayerRequestsOff", player.UserIDString));
                            return;
                        }

                        player.ChatMessage(msg("AlreadyBlocked", player.UserIDString));
                        return;
                    }
                case "bet":
                    {
                        if (!allowBets)
                        {
                            break;
                        }

                        if (duelingBets.Count == 0)
                        {
                            player.ChatMessage(msg("NoBetsConfigured", player.UserIDString));
                            return;
                        }

                        if (args.Length == 2)
                        {
                            switch (args[1].ToLower())
                            {
                                case "refundall":
                                    {
                                        if (player.IsAdmin)
                                        {
                                            if (duelsData.Bets.Count == 0)
                                            {
                                                player.ChatMessage(msg("NoBetsToRefund", player.UserIDString));
                                                return;
                                            }

                                            foreach (var kvp in duelsData.Bets.ToList())
                                            {
                                                var p = BasePlayer.Find(kvp.Key);
                                                if (!p) continue;

                                                Item item = ItemManager.CreateByItemID(kvp.Value.itemid, kvp.Value.amount);

                                                if (!item.MoveToContainer(p.inventory.containerMain, -1, true) && !item.MoveToContainer(p.inventory.containerBelt, -1, true))
                                                    continue;

                                                p.ChatMessage(msg("RefundAllPlayerNotice", p.UserIDString, item.info.displayName.translated, item.amount));
                                                player.ChatMessage(msg("RefundAllAdminNotice", player.UserIDString, p.displayName, p.UserIDString, item.info.displayName.english, item.amount));
                                                duelsData.Bets.Remove(kvp.Key);
                                            }

                                            if (duelsData.Bets.Count > 0) player.ChatMessage(msg("BetsRemaining", player.UserIDString, duelsData.Bets.Count));
                                            else player.ChatMessage(msg("AllBetsRefunded", player.UserIDString));
                                            SaveData();
                                            return;
                                        }
                                    }
                                    break;
                                case "forfeit":
                                    {
                                        if (allowBetRefund) // prevent operator error ;)
                                        {
                                            cmdDuel(player, command, new string[] { "bet", "refund" });
                                            return;
                                        }

                                        if (!allowBetForfeit)
                                        {
                                            player.ChatMessage(msg("CannotForfeit", player.UserIDString));
                                            return;
                                        }

                                        if (duelsData.Bets.ContainsKey(player.UserIDString))
                                        {
                                            if (duelsData.Requests.ContainsKey(player.UserIDString) || duelsData.Requests.ContainsValue(player.UserIDString))
                                            {
                                                player.ChatMessage(msg("CannotForfeitRequestDuel", player.UserIDString));
                                                return;
                                            }

                                            if (duelsData.Duelers.ContainsKey(player.UserIDString))
                                            {
                                                player.ChatMessage(msg("CannotForfeitInDuel", player.UserIDString));
                                                return;
                                            }

                                            duelsData.Bets.Remove(player.UserIDString);
                                            player.ChatMessage(msg("BetForfeit", player.UserIDString));
                                            SaveData();
                                        }
                                        else
                                            player.ChatMessage(msg("NoBetToForfeit", player.UserIDString));

                                        return;
                                    }
                                case "cancel":
                                case "refund":
                                    {
                                        if (!allowBetRefund && !player.IsAdmin)
                                        {
                                            player.ChatMessage(msg("CannotRefund", player.UserIDString));
                                            return;
                                        }

                                        if (duelsData.Bets.ContainsKey(player.UserIDString))
                                        {
                                            if (duelsData.Requests.ContainsKey(player.UserIDString) || duelsData.Requests.ContainsValue(player.UserIDString))
                                            {
                                                player.ChatMessage(msg("CannotRefundRequestDuel", player.UserIDString));
                                                return;
                                            }

                                            if (duelsData.Duelers.ContainsKey(player.UserIDString))
                                            {
                                                player.ChatMessage(msg("CannotRefundInDuel", player.UserIDString));
                                                return;
                                            }

                                            var bet = duelsData.Bets[player.UserIDString];

                                            Item item = ItemManager.CreateByItemID(bet.itemid, bet.amount);

                                            if (!item.MoveToContainer(player.inventory.containerMain, -1, true))
                                            {
                                                if (!item.MoveToContainer(player.inventory.containerBelt, -1, true))
                                                {
                                                    var position = player.transform.position;
                                                    item.Drop((position + new Vector3(0f, 1f, 0f)) + (position / 2f), (position + new Vector3(0f, 0.2f, 0f)) * 8f); // Credit: Slack comment by @visagalis
                                                }
                                            }

                                            duelsData.Bets.Remove(player.UserIDString);
                                            player.ChatMessage(msg("BetRefunded", player.UserIDString));
                                            SaveData();
                                        }
                                        else
                                            player.ChatMessage(msg("NoBetToRefund", player.UserIDString));

                                        return;
                                    }
                                default:
                                    break;
                            }
                        }

                        if (duelsData.Bets.ContainsKey(player.UserIDString))
                        {
                            var bet = duelsData.Bets[player.UserIDString];

                            player.ChatMessage(msg("AlreadyBetting", player.UserIDString, bet.trigger, bet.amount));

                            if (allowBetRefund)
                                player.ChatMessage(msg("ToRefundUse", player.UserIDString, szDuelChatCommand));
                            else if (allowBetForfeit)
                                player.ChatMessage(msg("ToForfeitUse", player.UserIDString, szDuelChatCommand));

                            return;
                        }

                        if (args.Length < 3)
                        {
                            player.ChatMessage(msg("AvailableBets", player.UserIDString));

                            foreach (var betInfo in duelingBets)
                                player.ChatMessage(string.Format("{0} (max: {1})", betInfo.trigger, betInfo.max));

                            player.ChatMessage(msg("BetSyntax", player.UserIDString, szDuelChatCommand));
                            return;
                        }

                        int betAmount;
                        if (!int.TryParse(args[2], out betAmount))
                        {
                            player.ChatMessage(msg("InvalidNumber", player.UserIDString, args[2]));
                            return;
                        }

                        if (betAmount > 500 && betAmount % 500 != 0)
                        {
                            player.ChatMessage(msg("MultiplesOnly", player.UserIDString));
                            return;
                        }

                        foreach (var betInfo in duelingBets)
                        {
                            if (betInfo.trigger.ToLower() == args[1].ToLower())
                            {
                                CreateBet(player, betAmount, betInfo);
                                return;
                            }
                        }

                        player.ChatMessage(msg("InvalidBet", player.UserIDString, args[1]));
                        return;
                    }
                case "accept":
                case "a":
                case "y":
                case "yes":
                    {
                        if (!autoAllowAll && !duelsData.Allowed.Contains(player.UserIDString))
                        {
                            player.ChatMessage(msg("MustAllowDuels", player.UserIDString, szDuelChatCommand));
                            return;
                        }

                        if (!duelsData.Requests.ContainsValue(player.UserIDString))
                        {
                            player.ChatMessage(msg("NoRequestsReceived", player.UserIDString));
                            return;
                        }

                        if (!IsNewman(player))
                        {
                            player.ChatMessage(msg("MustBeNaked", player.UserIDString));
                            return;
                        }

                        BasePlayer target = null;

                        foreach (var kvp in duelsData.Requests)
                        {
                            if (kvp.Value == player.UserIDString)
                            {
                                target = BasePlayer.Find(kvp.Key);

                                if (target == null || !target.IsConnected)
                                {
                                    player.ChatMessage(string.Format("DuelCancelledFor", player.UserIDString, GetDisplayName(kvp.Key)));
                                    duelsData.Requests.Remove(kvp.Key);
                                    return;
                                }

                                break;
                            }
                        }

                        if (duelsData.Restricted.Contains(player.UserIDString)) duelsData.Restricted.Remove(player.UserIDString);
                        if (duelsData.Restricted.Contains(target.UserIDString)) duelsData.Restricted.Remove(target.UserIDString);

                        if (!SelectZone(player, target))
                        {
                            player.ChatMessage(msg("AllZonesFull", player.UserIDString, duelingZones.Count, playersPerZone));
                            target.ChatMessage(msg("AllZonesFull", target.UserIDString, duelingZones.Count, playersPerZone));
                        }

                        return;
                    }
                case "cancel":
                    {
                        if (!autoAllowAll && !duelsData.Allowed.Contains(player.UserIDString))
                        {
                            player.ChatMessage(msg("MustAllowDuels", player.UserIDString, szDuelChatCommand));
                            return;
                        }

                        if (!duelsData.Requests.ContainsKey(player.UserIDString))
                        {
                            player.ChatMessage(msg("NoPendingRequests", player.UserIDString));
                            return;
                        }

                        var target = BasePlayer.Find(duelsData.Requests[player.UserIDString]);
                        target?.ChatMessage(msg("DuelCancelledWith", target.UserIDString, player.displayName));
                        duelsData.Requests.Remove(player.UserIDString);
                        player.ChatMessage(msg("DuelCancelComplete", player.UserIDString));
                        break;
                    }
                default:
                    {
                        if (!autoAllowAll && !duelsData.Allowed.Contains(player.UserIDString))
                        {
                            player.ChatMessage(msg("MustAllowDuels", player.UserIDString, szDuelChatCommand));
                            return;
                        }

                        if (duelsData.Restricted.Contains(player.UserIDString) && !player.IsAdmin)
                        {
                            player.ChatMessage(msg("MustWaitToRequestAgain", player.UserIDString, 1));
                            return;
                        }

                        if (duelsData.Duelers.Count >= 20)
                        {
                            player.ChatMessage(msg("MaxDuelsInProgress", player.UserIDString, 10));
                            return;
                        }

                        if (!IsNewman(player))
                        {
                            player.ChatMessage(msg("MustBeNaked", player.UserIDString));
                            return;
                        }

                        if (duelsData.Duelers.ContainsKey(player.UserIDString))
                        {
                            if (!DuelTerritory(player.transform.position))
                            {
                                duelsData.Duelers.Remove(player.UserIDString);
                                return;
                            }

                            player.ChatMessage(msg("AlreadyDueling", player.UserIDString));
                            return;
                        }

                        string name = string.Join(" ", args);
                        var target = FindPlayer(name);

                        if (target == null || target == player)
                        {
                            player.ChatMessage(msg("PlayerNotFound", player.UserIDString, name));
                            return;
                        }

                        if (duelsData.BlockedUsers.ContainsKey(target.UserIDString))
                        {
                            if (duelsData.BlockedUsers[target.UserIDString].Contains(player.UserIDString))
                            {
                                player.ChatMessage(msg("CannotRequestThisPlayer", player.UserIDString));
                                return;
                            }
                        }

                        if (IsDueling(target))
                        {
                            player.ChatMessage(msg("TargetAlreadyDueling", player.UserIDString, target.displayName));
                            return;
                        }

                        if (!autoAllowAll && !duelsData.Allowed.Contains(target.UserIDString))
                        {
                            player.ChatMessage(msg("NotAllowedYet", player.UserIDString, target.displayName, szDuelChatCommand));
                            return;
                        }

                        if (duelsData.Requests.ContainsKey(player.UserIDString))
                        {
                            player.ChatMessage(msg("MustWaitForAccept", player.UserIDString, GetDisplayName(duelsData.Requests[player.UserIDString])));
                            return;
                        }

                        if (duelsData.Requests.ContainsValue(target.UserIDString))
                        {
                            player.ChatMessage(msg("PendingRequestAlready", player.UserIDString));
                            return;
                        }

                        if (duelsData.Bets.ContainsKey(player.UserIDString) && !duelsData.Bets.ContainsKey(target.UserIDString))
                        {
                            var bet = duelsData.Bets[player.UserIDString];

                            player.ChatMessage(msg("TargetHasNoBet", player.UserIDString, target.displayName));
                            player.ChatMessage(msg("YourBet", player.UserIDString, bet.trigger, bet.amount));
                            return;
                        }

                        if (duelsData.Bets.ContainsKey(target.UserIDString) && !duelsData.Bets.ContainsKey(player.UserIDString))
                        {
                            var targetBet = duelsData.Bets[target.UserIDString];
                            player.ChatMessage(msg("MustHaveSameBet", player.UserIDString, target.displayName, targetBet.trigger, targetBet.amount));
                            return;
                        }

                        if (duelsData.Bets.ContainsKey(player.UserIDString) && duelsData.Bets.ContainsKey(target.UserIDString))
                        {
                            var playerBet = duelsData.Bets[player.UserIDString];
                            var targetBet = duelsData.Bets[target.UserIDString];

                            if (!playerBet.Equals(targetBet))
                            {
                                player.ChatMessage(msg("BetsDoNotMatch", player.UserIDString, playerBet.trigger, playerBet.amount, targetBet.trigger, targetBet.amount));
                                return;
                            }
                        }

                        if (Interface.CallHook("CanDuel", player) != null)
                        {
                            player.ChatMessage(msg("CannotDuel", player.UserIDString));
                            return;
                        }

                        duelsData.Requests.Add(player.UserIDString, target.UserIDString);
                        target.ChatMessage(msg("DuelRequested", target.UserIDString, player.displayName, szDuelChatCommand));
                        player.ChatMessage(msg("SentRequest", player.UserIDString, target.displayName));

                        if (RemoveFromQueue(player.UserIDString))
                            player.ChatMessage(msg("RemovedFromQueueRequest", player.UserIDString));

                        string targetName = target.displayName;
                        string playerId = player.UserIDString;

                        if (!duelsData.Restricted.Contains(playerId))
                            duelsData.Restricted.Add(playerId);

                        timer.In(60f, () =>
                        {
                            if (duelsData.Restricted.Contains(playerId))
                                duelsData.Restricted.Remove(playerId);

                            if (duelsData.Requests.ContainsKey(playerId))
                            {
                                if (player != null && !IsDueling(player))
                                    player.ChatMessage(msg("RequestTimedOut", playerId, targetName));

                                duelsData.Requests.Remove(playerId);
                            }
                        });

                        break;
                    }
                //
            } // end switch
        }

        BasePlayer FindPlayer(string strNameOrID)
        {
            var basePlayer = BasePlayer.activePlayerList.Find(x => x.UserIDString == strNameOrID);
            if (basePlayer)
            {
                return basePlayer;
            }
            var basePlayer2 = BasePlayer.activePlayerList.Find(x => x.displayName.Contains(strNameOrID, CompareOptions.OrdinalIgnoreCase));
            if (basePlayer2)
            {
                return basePlayer2;
            }

            return null;
        }

        void ResetTemporaryData() // keep our datafile cleaned up by removing entries which are temporary
        {
            if (duelsData == null)
                duelsData = new StoredData();

            duelsData.Duelers.Clear();
            duelsData.Requests.Clear();
            duelsData.Immunity.Clear();
            duelsData.Restricted.Clear();
            duelsData.Death.Clear();
            duelsData.Queued.Clear();
            duelsData.Homes.Clear();
            duelsData.Kits.Clear();
            SaveData();
        }
        
        DuelingZone RemoveDuelist(string playerId)
        {
            foreach (var zone in duelingZones)
            {
                if (zone.HasPlayer(playerId))
                {
                    zone.RemovePlayer(playerId);
                    return zone;
                }
            }

            return null;
        }

        void ResetDuelist(string targetId, bool removeHome = true) // remove a dueler from the datafile
        {
            if (duelsData.Immunity.ContainsKey(targetId))
                duelsData.Immunity.Remove(targetId);

            if (duelsData.Duelers.ContainsKey(targetId))
                duelsData.Duelers.Remove(targetId);

            if (duelsData.Requests.ContainsKey(targetId))
                duelsData.Requests.Remove(targetId);

            if (duelsData.Restricted.Contains(targetId))
                duelsData.Restricted.Remove(targetId);

            if (duelsData.Death.ContainsKey(targetId))
                duelsData.Death.Remove(targetId);

            if (duelsData.Homes.ContainsKey(targetId) && removeHome)
                duelsData.Homes.Remove(targetId);

            if (duelsData.Kits.ContainsKey(targetId))
                duelsData.Kits.Remove(targetId);

            if (duelingZones.Count > 0 )
                RemoveDuelist(targetId);

            RemoveFromQueue(targetId);
        }

        void RemoveZeroStats() // someone enabled duels but never joined one. remove them to keep the datafile cleaned up
        {
            foreach (string targetId in duelsData.Allowed.ToList())
            {
                if (!duelsData.Losses.ContainsKey(targetId) && !duelsData.Victories.ContainsKey(targetId)) // no all time stats
                {
                    ResetDuelist(targetId);
                    duelsData.Allowed.Remove(targetId);
                }
            }
        }
        
        void SetupZoneManager()
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
                            distance += Duelist.zoneRadius + 5f;
                            managedZones[position] = distance;
                        }
                    }
                }
            }
        }
        
        void SetupZones()
        {
            if (duelsData.ZoneIds.Count > zoneAmount) // zoneAmount was changed in the config file so remove existing zones until we're at the new cap
            {
                do
                {
                    duelsData.ZoneIds.RemoveAt(0);
                } while (duelsData.ZoneIds.Count > zoneAmount);
            }

            foreach (string id in duelsData.ZoneIds) // create all zones that don't already exist
            {
                SetupDuelZone(id.ToVector3());
            }

            if (autoSetup && duelsData.ZoneIds.Count < zoneAmount) // create each dueling zone that is missing. if this fails then console will be notified
            {
                int attempts = Math.Max(zoneAmount, 5); // 0.1.10 fix - infinite loop fix for when zone radius is too large to fit on the map
                int created = 0;
                do
                {
                    if (SetupDuelZone() != Vector3.zero)
                        created++;
                } while (duelsData.ZoneIds.Count < zoneAmount && --attempts > 0);

                if (attempts <= 0)
                {
                    if (created > 0)
                        Puts(msg("SupportCreated", null, created));
                    else
                        Puts(msg("SupportInvalidConfig"));
                }
            }
        }

        Vector3 SetupDuelZone() // starts the process of creating a new or existing zone and then setting up it's own spawn points around the circumference of the zone
        {
            var zone = FindDuelingZone(); // a complex process to search the map for a suitable area

            if (zone == Vector3.zero) // unfortunately we weren't able to find a location. this is likely due to an extremely high entity count. just try again.
            {
                //Puts(msg("FailedZone")); // notify 
                return Vector3.zero;
            }

            SetupDuelZone(zone);
            return zone;
        }

        DuelingZone SetupDuelZone(Vector3 zonePos)
        {
            if (!duelsData.ZoneIds.Contains(zonePos.ToString()))
            {
                duelsData.ZoneIds.Add(zonePos.ToString());
                SaveData();
            }

            var newZone = new DuelingZone(zonePos);

            duelingZones.Add(newZone);

            if (duelingZones.Count == 1)
            {
                Puts(msg("ZoneCreated"));
                Subscribe(nameof(OnPlayerRespawned));
                Subscribe(nameof(OnEntityTakeDamage));
                Subscribe(nameof(OnEntitySpawned));
                Subscribe(nameof(CanBuild));
            }

            CreateZoneWalls(newZone.Position, zoneRadius, zoneUseWoodenWalls ? hewwPrefab : heswPrefab);
            return newZone;
        }
        
        bool RemoveCustomZoneWalls(Vector3 center)
        {
            foreach (var entry in duelsData.Zones.ToList())
            {
                if (Vector3.Distance(entry.Key.ToVector3(), center) <= entry.Value)
                {
                    ulong ownerId = Convert.ToUInt64(Math.Abs(entry.Key.GetHashCode()));
                    RemoveZoneWalls(ownerId);
                    duelsData.Zones.Remove(entry.Key);
                    return true;
                }
            }

            return false;
        }

        void RemoveZoneWalls(ulong ownerId)
        {
            foreach (var entity in BaseEntity.serverEntities.Where(e => e.PrefabName.Equals(heswPrefab) || e.PrefabName.Equals(hewwPrefab)).Cast<BaseEntity>().ToList())
            {
                if (entity.OwnerID == ownerId)
                {
                    entity.Kill();
                }
            }
        }

        bool ZoneWallsExist(ulong ownerId)
        {
            foreach (var entity in BaseEntity.serverEntities.Where(e => e.PrefabName.Equals(heswPrefab) || e.PrefabName.Equals(hewwPrefab)).Cast<BaseEntity>())
            {
                if (entity.OwnerID == ownerId)
                {
                    return true;
                }
            }

            return false;
        }
        
        void CreateZoneWalls(Vector3 center, float zoneRadius, string prefab, BasePlayer player = null)
        {
            if (!useZoneWalls)
                return;

            var tick = DateTime.Now;
            ulong ownerId = Convert.ToUInt64(Math.Abs(center.ToString().GetHashCode()));

            if (ZoneWallsExist(ownerId))
                return;

            float maxHeight = -200f;
            float minHeight = 200f;
            int spawned = 0;
            int raycasts = Mathf.CeilToInt(360 / zoneRadius * 0.1375f);

            var positions = GetCircumferencePositions(center, zoneRadius, raycasts, 0f);
            
            foreach (var position in positions) // get our positions and perform the calculations for the highest and lowest points of terrain
            {
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(position.x, position.y + 200f, position.z), Vector3.down, out hit, Mathf.Infinity, wallMask)) // find the highest point
                {
                    maxHeight = Mathf.Max(hit.point.y, maxHeight); // calculate the highest point of terrain
                    minHeight = Mathf.Min(hit.point.y, minHeight); // calculate the lowest point of terrain
                    center.y = minHeight; // adjust the spawn point of our walls to that of the lowest point of terrain
                }
            }

            float gap = prefab == heswPrefab ? 0.3f : 0.5f;
            int stacks = Mathf.CeilToInt((maxHeight - minHeight) / 6f) + extraWallStacks; // get the amount of walls to stack onto each other to go above the highest point
            float next = 360 / zoneRadius - gap; // convert degrees into usable meters to get the distance apart for each wall and minimize the gap

            for (int i = 0; i < stacks; i++) // create our loop to spawn each layer
            {
                foreach (var position in GetCircumferencePositions(center, zoneRadius, next, center.y)) // get a list positions where each positions difference is the width of a high external stone wall. specify the height since we've already calculated what's required
                {
                    if (TerrainMeta.HeightMap.GetHeight(new Vector3(position.x, position.y + 6f, position.z)) > position.y + 6f) // 0.1.13 improved distance check underground
                        continue;

                    var entity = GameManager.server.CreateEntity(prefab, position, new Quaternion(), false);

                    if (entity)
                    {
                        entity.OwnerID = ownerId; // set a unique identifier so the walls can be easily removed later
                        entity.transform.LookAt(center, Vector3.up); // have each wall look at the center of the zone
                        entity.Spawn(); // spawn into the game
                        spawned++; // our counter
                    }
                    else
                        return; // invalid prefab, do nothing more
                }

                center.y += 6f; // increase the positions height by one high external stone wall's height
            }

            if (player == null)
                Puts(msg("GeneratedWalls", null, spawned, stacks, FormatPosition(center), (DateTime.Now - tick).TotalSeconds));
            else
                player.ChatMessage(msg("GeneratedWalls", player.UserIDString, spawned, stacks, FormatPosition(center), (DateTime.Now - tick).TotalSeconds));
        }

        void RemoveDuelZone(DuelingZone zone)
        {
            if (duelsData.ZoneIds.Contains(zone.Position.ToString()))
                duelsData.ZoneIds.Remove(zone.Position.ToString());

            RemoveEntities(zone);
            RemoveZoneWalls(Convert.ToUInt64(Math.Abs(zone.Position.ToString().GetHashCode())));
            duelingZones.Remove(zone);
            zone.Kill();

            if (duelingZones.Count == 0)
                SubscribeHooks(false);
        }
        
        void RemoveEntities(DuelingZone zone)
        {
            foreach (var entry in duelEntities.ToList())
            {
                foreach (var entity in entry.Value.ToList())
                {
                    if (entity == null || entity.IsDestroyed)
                        continue;

                    if (zone.Distance(entity.transform.position) <= zoneRadius + 1f)
                    {
                        duelEntities[entry.Key].Remove(entity);
                        entity.Kill();
                    }
                }
            }
        }

        DuelingZone GetDuelZone(Vector3 startPos, float offset = 0f)
        {
            foreach (var zone in duelingZones)
            {
                if (zone.Distance(startPos) <= zoneRadius + offset)
                {
                    if (duelsData.ZoneIds.Contains(zone.Position.ToString()))
                    {
                        return zone;
                    }
                }
            }

            return null;
        }

        void SendHome(BasePlayer player) // send a player home to a saved location from where they joined the duel
        {
            if (player != null && duelsData.Homes.ContainsKey(player.UserIDString))
            {
                if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                {
                    timer.Once(2f, () => SendHome(player));
                    return;
                }

                var homePos = duelsData.Homes[player.UserIDString].ToVector3();

                if (player.IsDead())
                {
                    player.RespawnAt(homePos, default(Quaternion));

                    if (playerHealth > 0f)
                        player.health = playerHealth;
                }
                else
                {
                    player.inventory.GiveDefaultItems();
                    Teleport(player, homePos);
                }

                duelsData.Homes.Remove(player.UserIDString);
            }
        }
        
        void Disappear(BasePlayer player) // credit Wulf.
        {
            var connections = BasePlayer.activePlayerList.Where(active => active != player && active.IsConnected).Select(active => active.net.connection).ToList();

            if (Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
                Net.sv.write.EntityID(player.net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }

            var item = player.GetActiveItem();
            if (item?.GetHeldEntity() != null && Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
                Net.sv.write.EntityID(item.GetHeldEntity().net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }
        }

        void CheckDuelistMortality()
        {
            if (duelsData.Immunity.Count > 0) // each player that spawns into a dueling zone is given immunity for X seconds. here we'll keep track of this and remove their immunities
            {
                var timeStamp = TimeStamp();

                foreach (var kvp in duelsData.Immunity.ToList())
                {
                    if (kvp.Value - timeStamp <= 0)
                    {
                        var target = BasePlayer.Find(kvp.Key);
                        duelsData.Immunity.Remove(kvp.Key);

                        if (target != null)
                            target.ChatMessage(msg("ImmunityFaded", target.UserIDString));
                    }
                }
            }

            if (duelsData.Death.Count > 0) // keep track of how long the match has been going on for, and if it's been too long then kill the player off.
            {
                var timeStamp = TimeStamp();

                foreach (var kvp in duelsData.Death.ToList())
                {
                    if (kvp.Value - timeStamp <= 0)
                    {
                        var target = BasePlayer.Find(kvp.Key);
                        duelsData.Death.Remove(kvp.Key);

                        if (!target || !IsDueling(target)) // target should always be dueling in this situation but we'll check anyway to be 100% certain incase of a bug now or later
                            continue;

                        target.inventory.Strip();
                        OnDuelistLost(target);
                    }
                }
            }
        }

        void SubscribeHooks(bool flag) // we're using lots of temporary and permanent hooks so we'll turn off the temporary hooks when the plugin is loaded, and unsubscribe to others inside of their hooks when they're no longer in use
        {
            if (!init || duelsData?.Duelers == null || duelsData.Duelers.Count == 0 || !flag)
            {
                Unsubscribe(nameof(OnPlayerDisconnected));
                Unsubscribe(nameof(CanNetworkTo));
                Unsubscribe(nameof(OnItemDropped));
                Unsubscribe(nameof(OnPlayerSleepEnded));
                Unsubscribe(nameof(OnCreateWorldProjectile));
                Unsubscribe(nameof(OnLootEntity));
                Unsubscribe(nameof(OnPlayerRespawned));
                Unsubscribe(nameof(OnEntityTakeDamage));
                Unsubscribe(nameof(OnEntitySpawned));
                Unsubscribe(nameof(CanBuild));
                return;
            }

            if (flag)
            {
                Subscribe(nameof(OnPlayerDisconnected));

                if (useInvisibility)
                    Subscribe(nameof(CanNetworkTo));

                Subscribe(nameof(OnItemDropped));
                Subscribe(nameof(OnPlayerSleepEnded));
                Subscribe(nameof(OnCreateWorldProjectile));
                Subscribe(nameof(OnLootEntity));
            }
        }

        // Helper methods which are essential for the plugin to function. Do not modify these.
        static bool DuelTerritory(Vector3 position, float offset = 0f)
        {
            return ins.init && duelingZones.Any(zone => zone.Distance(position) <= zoneRadius + offset);
        }

        bool ArenaTerritory(Vector3 position, float offset = 0f)
        {
            return init && duelsData.Zones.Any(zone => Vector3.Distance(zone.Key.ToVector3(), position) <= zone.Value + offset);
        }

        static bool IsDueling(BasePlayer player) => ins.init && duelsData != null && duelingZones.Count > 0 && player != null && duelsData.Duelers.ContainsKey(player.UserIDString) && DuelTerritory(player.transform.position);
        bool IsEventBanned(string targetId) => duelsData.Bans.ContainsKey(targetId);
        long TimeStamp() => (DateTime.Now.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks) / 10000000;
        string GetDisplayName(string targetId) => covalence.Players.FindPlayer(targetId)?.Name ?? targetId;
        void Log(string file, string message, bool timestamp = false) => LogToFile(file, $"[{DateTime.Now.ToString()}] {message}", this, timestamp);

        bool IsOnConstruction(Vector3 position) // check if an entity (a player) is on a structure or deployable, and adjust their position elsewhere if so
        {
            position.y += 1f;
            RaycastHit hit;

            return Physics.Raycast(position, Vector3.down, out hit, 1.5f, constructionMask);
        }

        bool Teleport(BasePlayer player, Vector3 destination)
        {
            if (!player || destination == Vector3.zero) // don't send a player to their death. this should never happen
                return false;

            if (player.IsWounded())
                player.StopWounded();

            player.metabolism.bleeding.value = 0;

            if (playerHealth > 0f)
                player.health = playerHealth;

            if (player.IsConnected)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
                if (!BasePlayer.sleepingPlayerList.Contains(player)) BasePlayer.sleepingPlayerList.Add(player);

                player.CancelInvoke("InventoryUpdate");
                player.inventory.crafting.CancelAll(true);
            }

            Player.Teleport(player, destination);

            if (player.IsConnected)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                player.UpdateNetworkGroup();
                player.SendNetworkUpdateImmediate(false);
                player.ClientRPCPlayer(null, player, "StartLoading", null, null, null, null, null);
                player.SendFullSnapshot();
            }
            else player.SendNetworkUpdateImmediate(false);

            return true;
        }

        bool IsThrownWeapon(Item item)
        {
            if (item == null)
                return false;

            if (item.info.category == ItemCategory.Weapon || item.info.category == ItemCategory.Tool)
            {
                if (item.info.stackable > 1)
                    return false;

                var weapon = item?.GetHeldEntity() as BaseProjectile;

                if (weapon == null)
                    return true;

                if (weapon.primaryMagazine.capacity > 0)
                    return false;
            }

            return false;
        }

        public Vector3 RandomDropPosition() // CargoPlane.RandomDropPosition()
        {
            var vector = Vector3.zero;
            float num = 100f, x = TerrainMeta.Size.x / 3f;
            do
            {
                vector = Vector3Ex.Range(-x, x);
            }
            while (filter.GetFactor(vector) == 0f && --num > 0f);
            vector.y = 0f;
            return vector;
        }

        public Vector3 FindDuelingZone()
        {
            var tick = DateTime.Now; // create a timestamp to see how long this process takes
            var position = Vector3.zero;
            int maxRetries = 500; // 0.1.9: increased due to rock collider detection. 0.1.10 rock collider detection removed but amount not changed
            int retries = maxRetries; // servers with high entity counts will require this

            if (managedZones.Count == 0 && ZoneManager != null)
                SetupZoneManager();

            do
            {
                position = RandomDropPosition();

                foreach (var monument in monuments)
                {
                    if (Vector3.Distance(position, monument) < 150f) // don't put the dueling zone inside of a monument. players will throw a shit fit
                    {
                        position = Vector3.zero;
                        break;
                    }
                }

                if (position == Vector3.zero)
                    continue;

                if (managedZones.Count > 0)
                {
                    foreach (var zone in managedZones)
                    {
                        if (Vector3.Distance(zone.Key, position) <= zone.Value)
                        {
                            position = Vector3.zero; // blocked by zone manager
                            break;
                        }
                    }
                }

                if (position == Vector3.zero)
                    continue;

                position.y = TerrainMeta.HeightMap.GetHeight(position) + 100f; // setup the hit

                RaycastHit hit;
                if (Physics.Raycast(position, Vector3.down, out hit, position.y, groundMask))
                {
                    position.y = Mathf.Max(hit.point.y, TerrainMeta.HeightMap.GetHeight(position)); // get the max height

                    var colliders = Pool.GetList<Collider>();
                    Vis.Colliders(position, zoneRadius, colliders, blockedMask, QueryTriggerInteraction.Collide); // get all colliders using the provided layermask

                    if (colliders.Count > 0) // if any colliders were found from the blockedMask then we don't want this as our dueling zone. retry.
                        position = Vector3.zero;

                    Pool.FreeList<Collider>(ref colliders);

                    if (position != Vector3.zero) // so far so good, let's measure the highest and lowest points of the terrain, and count the amount of water colliders
                    {
                        var positions = GetCircumferencePositions(position, zoneRadius - 15f, 1f, 0f); // gather positions around the purposed zone
                        float min = 200f;
                        float max = -200f;
                        int water = 0;

                        foreach (var pos in positions)
                        {
                            RaycastHit hit2;
                            if (Physics.Raycast(new Vector3(pos.x, pos.y + 100f, pos.z), Vector3.down, out hit2, 100.5f, waterMask)) // look for water
                                water++; // count the amount of water colliders

                            min = Mathf.Min(pos.y, min); // set the lowest and highest points of the terrain
                            max = Mathf.Max(pos.y, max);
                        }

                        if (max - min > maxIncline || position.y - min > maxIncline) // the incline is too steep to be suitable for a dueling zone, retry.
                            position = Vector3.zero;

                        if (water > positions.Count / 4) // too many water colliders, retry.
                            position = Vector3.zero;

                        positions.Clear();
                    }
                }
                else
                    position = Vector3.zero; // found water instead of land

                if (position == Vector3.zero)
                    continue;

                if (DuelTerritory(position, zoneRadius + 15f)) // check if position overlaps an existing zone
                {
                    position = Vector3.zero; // overlaps, retry.
                    continue;
                }
            }
            while (position == Vector3.zero && --retries > 0); // prevent infinite loops

            if (position != Vector3.zero)
                Puts(msg("FoundZone", null, maxRetries - retries, (DateTime.Now - tick).TotalMilliseconds)); // we found a dueling zone! return the position to be assigned, spawn the zone and the spawn points!

            return position;
        }

        List<Vector3> GetCircumferencePositions(Vector3 center, float radius, float next, float y) // as the name implies
        {
            var positions = new List<Vector3>();            
            float degree = 0f;

            while (degree < 360)
            {
                float angle = (float)(2 * Math.PI / 360) * degree;
                float x = center.x + radius * (float)Math.Cos(angle); 
                float z = center.z + radius * (float)Math.Sin(angle);
                var position = new Vector3(x, center.y, z);
                
                position.y = y == 0f ? TerrainMeta.HeightMap.GetHeight(position) : y;
                positions.Add(position);
                
                degree += next;                
            }

            return positions;
        }

        List<Vector3> CreateSpawnPoints(Vector3 center)
        {
            var positions = new List<Vector3>(); // 0.1.1 bugfix: spawn point height (y) wasn't being modified when indexing the below foreach list. instead, create a copy of each position and return a new list (cause: can't modify members of value types without changing the collection and invalidating the enumerator. bug: index the value type and change the value. result: list did not propagate)

            // create spawn points slightly inside of the dueling zone so they don't spawn inside of walls
            foreach (var position in GetCircumferencePositions(center, zoneRadius - 15f, 10f, 0f))
            {
                var hits = Physics.RaycastAll(new Vector3(position.x, position.y + 200f, position.z), Vector3.down, position.y + 201.5f);

                if (hits.Count() > 0) // low failure rate
                {
                    float y = TerrainMeta.HeightMap.GetHeight(position);
                    float waterY = -200f;

                    foreach (var hit in hits)
                    {
                        switch (LayerMask.LayerToName(hit.collider.gameObject.layer))
                        {
                            case "Construction":
                                if (!hit.GetEntity()) // 0.1.2 bugfix: spawn points floating when finding a collider with no entity
                                    continue;

                                y = Mathf.Max(hit.point.y, y);
                                break;
                            case "Deployed":
                                if (!hit.GetEntity()) // 0.1.2 ^
                                    continue;

                                y = Mathf.Max(hit.point.y, y);
                                break;
                            case "World":
                            case "Terrain":
                                y = Mathf.Max(hit.point.y, y);
                                break;
                            case "Water":
                                waterY = hit.point.y;
                                break;
                        }
                    }

                    if (avoidWaterSpawns && waterY > y)
                        continue;

                    positions.Add(new Vector3(position.x, y + 1f, position.z)); // slightly elevate the spawn point to avoid spawning in rocks
                }
            }

            return positions;
        }

        bool ResetDuelists() // reset all data for the wipe after assigning awards
        {
            if (AssignDuelists())
            {
                duelsData.ZoneIds.Clear();
                duelsData.Bets.Clear();
                duelsData.ClaimBets.Clear();
                duelsData.VictoriesSeed.Clear();
                duelsData.LossesSeed.Clear();
                duelsData.Spawns.Clear();
                duelsData.Zones.Clear();
                ResetTemporaryData();
            }

            return true;
        }

        bool AssignDuelists()
        {
            if (!recordStats || duelsData.VictoriesSeed.Count == 0)
                return true; // nothing to do here, return

            foreach (var target in covalence.Players.All) // remove player awards from previous wipe
            {
                if (permission.UserHasPermission(target.Id, duelistPerm))
                    permission.RevokeUserPermission(target.Id, duelistPerm);

                if (permission.UserHasGroup(target.Id, duelistGroup))
                    permission.RemoveUserGroup(target.Id, duelistGroup);
            }

            if (permsToGive < 1) // check now incase the user disabled awards later on
                return true;

            var duelists = duelsData.VictoriesSeed.ToList<KeyValuePair<string, int>>(); // sort the data
            duelists.Sort((x, y) => y.Value.CompareTo(x.Value));

            int added = 0;

            for (int i = 0; i < duelists.Count; i++) // now assign it
            {
                var target = covalence.Players.FindPlayerById(duelists[i].Key);

                if (target == null || target.IsBanned || target.IsAdmin)
                    continue;

                permission.GrantUserPermission(target.Id, duelistPerm.ToLower(), this);
                permission.AddUserGroup(target.Id, duelistGroup.ToLower());

                Log("awards", msg("Awards", null, target.Name, target.Id, duelists[i].Value), true);
                Puts(msg("Granted", null, target.Name, target.Id, duelistPerm, duelistGroup));

                if (++added >= permsToGive)
                    break;
            }

            if (added > 0)
            {
                string file = string.Format("{0}{1}{2}_{3}-{4}.txt", Interface.Oxide.LogDirectory, System.IO.Path.DirectorySeparatorChar, Name.Replace(" ", "").ToLower(), "awards", DateTime.Now.ToString("yyyy-MM-dd"));
                Puts(msg("Logged", null, file));
            }

            return true;
        }

        bool IsNewman(BasePlayer player) // count the players items. exclude rocks and torchs
        {
            int count = player.inventory.AllItems().Count();

            count -= player.inventory.GetAmount(3506021); // rock
            count -= player.inventory.GetAmount(110547964); // torch

            return count == 0;
        }
        
        static bool RemoveFromQueue(string targetId)
        {
            foreach(var kvp in duelsData.Queued)
            {
                if (kvp.Value == targetId)
                {
                    duelsData.Queued.Remove(kvp.Key);
                    return true;
                }
            }

            return false;
        }

        void CheckQueue()
        {
            if (duelsData.Queued.Count < 2 || !duelsData.DuelsEnabled)
                return;

            string playerId = duelsData.Queued.Values.ElementAt(0);
            string targetId = duelsData.Queued.Values.ElementAt(1);

            if (string.IsNullOrEmpty(playerId))
            {
                RemoveFromQueue(playerId);
                CheckQueue();
                return;
            }

            if (string.IsNullOrEmpty(targetId))
            {
                RemoveFromQueue(targetId);
                CheckQueue();
                return;
            }
            
            var player = BasePlayer.Find(playerId);
            var target = BasePlayer.Find(targetId);

            if (player == null || player.IsSleeping() || player.IsWounded() || player.IsDead())
            {
                if (RemoveFromQueue(playerId))
                    CheckQueue();

                return;
            }

            if (target == null || target.IsSleeping() || target.IsWounded() || target.IsDead())
            {
                if (RemoveFromQueue(targetId))
                    CheckQueue();

                return;
            }

            if (!IsNewman(player))
            {
                if (RemoveFromQueue(player.UserIDString))
                    player.ChatMessage(msg("MustBeNaked", player.UserIDString));

                return;
            }

            if (!IsNewman(target))
            {
                if (RemoveFromQueue(target.UserIDString))
                    target.ChatMessage(msg("MustBeNaked", player.UserIDString));

                return;
            }

            SelectZone(player, target);
        }

        bool SelectZone(BasePlayer player, BasePlayer target)
        {
            var zones = duelingZones.Where(zone => !zone.IsFull).ToList();

            do
            {
                var zone = zones.GetRandom();

                if (zone.AddWaiting(player, target))
                {
                    Initiate(player, target, false, zone);
                    return true;
                }

                zones.Remove(zone);
            } while (zones.Count > 0);

            return false;
        }

        void Initiate(BasePlayer player, BasePlayer target, bool checkInventory, DuelingZone destZone)
        {
            try
            {
                if (player == null || target == null || destZone == null)
                    return;

                if (duelsData.Requests.ContainsKey(player.UserIDString)) duelsData.Requests.Remove(player.UserIDString);
                if (duelsData.Requests.ContainsKey(target.UserIDString)) duelsData.Requests.Remove(target.UserIDString);

                if (checkInventory)
                {
                    if (!IsNewman(player))
                    {
                        player.ChatMessage(msg("MustBeNaked", player.UserIDString));
                        target.ChatMessage(msg("DuelMustBeNaked", target.UserIDString, player.displayName));
                        return;
                    }

                    if (!IsNewman(target))
                    {
                        target.ChatMessage(msg("MustBeNaked", player.UserIDString));
                        player.ChatMessage(msg("DuelMustBeNaked", player.UserIDString, target.displayName));
                        return;
                    }
                }

                var ppos = player.transform.position;
                var tpos = target.transform.position;

                if (IsOnConstruction(player.transform.position)) ppos.y += 1; // prevent player from becoming stuck or dying when teleported home
                if (IsOnConstruction(target.transform.position)) tpos.y += 1;

                duelsData.Homes[player.UserIDString] = ppos.ToString();
                duelsData.Homes[target.UserIDString] = tpos.ToString();

                var playerSpawn = destZone.Spawns.GetRandom();
                var targetSpawn = playerSpawn;
                float dist = -100f;

                foreach(var spawn in destZone.Spawns) // get the furthest spawn point away from the player and assign it to target
                {
                    float distance = Vector3.Distance(spawn, playerSpawn);

                    if (distance > dist)
                    {
                        dist = distance;
                        targetSpawn = spawn;
                    }
                }

                string kit = duelingKits.Count > 0 ? duelingKits.GetRandom() : invalidKit;

                if (kit == invalidKit || !IsKit(kit))
                    kit = customKits.Count > 0 ? customKits.ToList().GetRandom().Key : invalidKit;  

                if (duelsData.CustomKits.ContainsKey(player.UserIDString) && duelsData.CustomKits.ContainsKey(target.UserIDString))
                {
                    string playerKit = duelsData.CustomKits[player.UserIDString];
                    string targetKit = duelsData.CustomKits[target.UserIDString];

                    if (playerKit.Equals(targetKit, StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (customKits.Any(entry => entry.Key.Equals(playerKit, StringComparison.CurrentCultureIgnoreCase)))
                            kit = customKits.First(entry => entry.Key.Equals(playerKit, StringComparison.CurrentCultureIgnoreCase)).Key;
                        else if (duelingKits.Any(entry => entry.Equals(playerKit, StringComparison.CurrentCultureIgnoreCase)))
                            kit = duelingKits.First(entry => entry.Equals(playerKit, StringComparison.CurrentCultureIgnoreCase));
                    }
                }

                duelsData.Kits[player.UserIDString] = kit;
                duelsData.Kits[target.UserIDString] = kit;

                if (!Teleport(player, playerSpawn))
                {
                    ResetDuelist(player.UserIDString);
                    ResetDuelist(target.UserIDString);
                    target.inventory.Strip();
                    return;
                }

                if (!Teleport(target, targetSpawn))
                {
                    ResetDuelist(target.UserIDString);
                    ResetDuelist(player.UserIDString);
                    player.inventory.Strip();
                    return;
                }

                //Puts($"{player.displayName} and {target.displayName} have entered a duel.");

                RemoveFromQueue(player.UserIDString);
                RemoveFromQueue(target.UserIDString);

                if (immunityTime > 0)
                {
                    duelsData.Immunity[player.UserIDString] = TimeStamp() + immunityTime;
                    duelsData.Immunity[target.UserIDString] = TimeStamp() + immunityTime;
                }

                duelsData.Duelers[player.UserIDString] = target.UserIDString;
                duelsData.Duelers[target.UserIDString] = player.UserIDString;
                SubscribeHooks(true);

                player.ChatMessage(msg("NowDueling", player.UserIDString, target.displayName));
                target.ChatMessage(msg("NowDueling", target.UserIDString, player.displayName));

                return;
            }
            catch (Exception ex)
            {
                duelsData.DuelsEnabled = false;
                SaveData();

                Puts("---");
                Puts("Duels disabled: {0} --- {1}", ex.Message, ex.StackTrace);
                Puts("---");

                ResetDuelist(player.UserIDString);
                ResetDuelist(target.UserIDString);
                RemoveFromQueue(player.UserIDString);
                RemoveFromQueue(target.UserIDString);
            }
        }
        
        // not using api here since players may not be in a clan, or on any friend list. we will check manually to prevent abuse of the bet system so that all players can enjoy it
        // example of abuse: place bet, spawn at a base, join duel, win or suicide, claim bet = transfered loot across the map in no time at all

        bool IsAllied(BasePlayer player, BasePlayer target) => IsInSameClan(player, target) || IsAuthorizing(player, target) || IsBunked(player, target) || IsCodeAuthed(player, target) || IsInSameBase(player, target);

        bool IsInSameClan(BasePlayer player, BasePlayer target) // 1st method
        {
            if (player == null || target == null)
                return true; // do not allow bet

            if (player.displayName.StartsWith("[") && player.displayName.Contains("]"))
            {
                if (target.displayName.StartsWith("[") && target.displayName.Contains("]"))
                {
                    string attackerClan = player.displayName.Substring(0, player.displayName.IndexOf("]"));
                    string victimClan = target.displayName.Substring(0, target.displayName.IndexOf("]"));

                    return attackerClan == victimClan;
                }
            }

            return false;
        }

        bool IsAuthorizing(BasePlayer player, BasePlayer target) // 2nd method. thanks @psychotea for the linq suggestion
        {
            if (player.buildingPrivilege.Any(x => x.IsAuthed(target)))
                return true;

            if (target.buildingPrivilege.Any(x => x.IsAuthed(player)))
                return true;

            return false;
        }

        bool IsBunked(BasePlayer player, BasePlayer target) // 3rd method. thanks @i_love_code for helping with this too
        {
            var targetBags = SleepingBag.FindForPlayer(target.userID, true);

            if (targetBags.Count() > 0)
                foreach (var pbag in SleepingBag.FindForPlayer(player.userID, true))
                    if (targetBags.Any(tbag => Vector3.Distance(pbag.transform.position, tbag.transform.position) < 25f))
                        return true;

            return false;
        }

        bool IsCodeAuthed(BasePlayer player, BasePlayer target) // 4th method. nice and clean linq
        {
            foreach (var codelock in BaseEntity.serverEntities.Where(e => e is CodeLock).Cast<CodeLock>())
            {
                if (codelock.whitelistPlayers.Any(id => id == player.userID))
                {
                    if (codelock.whitelistPlayers.Any(id => id == target.userID))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool IsInSameBase(BasePlayer player, BasePlayer target) // 5th method
        {
            bool _sharesBase = false; // to free the pooled lists

            foreach(var priv in player.buildingPrivilege)
            {
                var colliders = Pool.GetList<Collider>();
                Vis.Colliders(priv.transform.position, 25f, colliders, constructionMask, QueryTriggerInteraction.Collide);

                foreach (var collider in colliders)
                {
                    var entity = collider.GetComponent<BaseEntity>();

                    if (!entity)
                        continue;

                    if (entity.OwnerID == target.userID)
                    {
                        _sharesBase = true;
                        break;
                    }
                }

                Pool.FreeList<Collider>(ref colliders); // free it.

                if (_sharesBase)
                    return true;
            }

            foreach (var priv in target.buildingPrivilege)
            {
                var colliders = Pool.GetList<Collider>();
                Vis.Colliders(priv.transform.position, 25f, colliders, constructionMask, QueryTriggerInteraction.Collide);

                foreach (var collider in colliders)
                {
                    var entity = collider.GetComponent<BaseEntity>();

                    if (!entity)
                        continue;

                    if (entity.OwnerID == player.userID)
                    {
                        _sharesBase = true;
                        break;
                    }
                }

                Pool.FreeList<Collider>(ref colliders);
            }

            return _sharesBase;
        }
        
        void Metabolize(BasePlayer player) // we don't want the elements to harm players since the zone can spawn anywhere on the map!
        {
            player.metabolism.temperature.min = 32; // immune to cold
            player.metabolism.temperature.max = 32;
            player.metabolism.temperature.value = 32;
            player.metabolism.oxygen.min = 1; // immune to drowning
            player.metabolism.oxygen.value = 1;
            player.metabolism.poison.value = 0; // if they ate raw meat
            player.metabolism.calories.value = 250;
            player.metabolism.hydration.value = 500;
            player.metabolism.wetness.max = 0;
            player.metabolism.wetness.value = 0;
            player.metabolism.SendChangesToClient();
        }

        bool IsKit(string kit)
        {
            return (bool)(Kits?.Call("isKit", kit) ?? false);
        }

        void GivePlayerKit(BasePlayer player)
        {
            if (!player || !duelsData.Kits.ContainsKey(player.UserIDString))
                return;

            string kit = duelsData.Kits[player.UserIDString];
            duelsData.Kits.Remove(player.UserIDString);

            player.inventory.Strip(); // remove rocks and torches

            if (kit != invalidKit && IsKit(kit))
            {
                Kits.Call("GiveKit", player, kit);
                return;
            }

            if (GiveCustomKit(player, kit))
                return;

            // welp, someone got delete happy in the config. use a basic kit instead
            player.inventory.GiveItem(ItemManager.CreateByItemID(-853695669, 1, 0)); // bow
            player.inventory.GiveItem(ItemManager.CreateByItemID(-420273765, 30, 0)); // arrows
            player.inventory.GiveItem(ItemManager.CreateByItemID(-2118132208, 1, 0)); // stone spear
            player.inventory.GiveItem(ItemManager.CreateByItemID(-337261910, 5, 0)); // bandage
            player.inventory.GiveItem(ItemManager.CreateByItemID(-789202811, 3, 0)); // medkit
            player.inventory.GiveItem(ItemManager.CreateByItemID(586484018, 4, 0)); // syringe
        }

        bool GiveCustomKit(BasePlayer player, string kit)
        {
            if (customKits.Count == 0 || !customKits.ContainsKey(kit))
            {
                return false;
            }
            
            foreach(var dki in customKits[kit])
            {
                Item item = ItemManager.CreateByName(dki.shortname, dki.amount, dki.skin);

                if (item == null)
                    continue;

                if (item.skin == 0 && useRandomSkins)
                {
                    var skins = GetItemSkins(item.info);

                    if (skins.Count > 0)
                        item.skin = skins.GetRandom();
                }

                if (dki.mods != null)
                {
                    foreach (string shortname in dki.mods)
                    {
                        Item mod = ItemManager.CreateByName(shortname, 1);

                        if (mod != null)
                            item.contents.AddItem(mod.info, 1);
                    }
                }

                var heldEntity = item.GetHeldEntity();

                if (heldEntity != null)
                {
                    if (item.skin != 0)
                        heldEntity.skinID = item.skin;

                    var weapon = heldEntity as BaseProjectile;

                    if (weapon != null)
                    {
                        if (!string.IsNullOrEmpty(dki.ammo))
                        {
                            var def = ItemManager.FindItemDefinition(dki.ammo);

                            if (def != null)
                                weapon.primaryMagazine.ammoType = def;
                        }

                        weapon.primaryMagazine.contents = 0; // unload the old ammo
                        weapon.SendNetworkUpdateImmediate(false); // update
                        weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity; // load new ammo
                    }
                }

                var container = dki.container == "belt" ? player.inventory.containerBelt : dki.container == "wear" ? player.inventory.containerWear : player.inventory.containerMain;

                item.MarkDirty();
                item.MoveToContainer(container, dki.slot <= 0 ? -1 : dki.slot, true);

                continue;
            }

            return true;
        }

        void DuelAnnouncement()
        {
            if (!duelsData.DuelsEnabled || !useAnnouncement)
                return;

            // too lazy to use another variable name
            {
                string console = msg("DuelAnnouncement");
                string disabled = msg("Disabled");

                console = console.Replace("{duelChatCommand}", !string.IsNullOrEmpty(szDuelChatCommand) ? szDuelChatCommand : disabled);
                console = console.Replace("{ladderCommand}", !string.IsNullOrEmpty(szDuelChatCommand) ? string.Format("{0} ladder", szDuelChatCommand) : disabled);
                console = console.Replace("{queueCommand}", !string.IsNullOrEmpty(szQueueChatCommand) ? szQueueChatCommand : disabled);

                if (allowBets)
                    console += msg("DuelAnnouncementBetsSuffix", null, szDuelChatCommand);

                Puts(RemoveFormatting(console));
            }

            foreach (var player in BasePlayer.activePlayerList.Where(p => p?.displayName != null))
            {
                string message = msg("DuelAnnouncement", player.UserIDString);
                string disabled = msg("Disabled", player.UserIDString);

                message = message.Replace("{duelChatCommand}", !string.IsNullOrEmpty(szDuelChatCommand) ? szDuelChatCommand : disabled);
                message = message.Replace("{ladderCommand}", !string.IsNullOrEmpty(szDuelChatCommand) ? string.Format("{0} ladder", szDuelChatCommand) : disabled);
                message = message.Replace("{queueCommand}", !string.IsNullOrEmpty(szQueueChatCommand) ? szQueueChatCommand : disabled);

                if (allowBets)
                    message += msg("DuelAnnouncementBetsSuffix", player.UserIDString, szDuelChatCommand);

                player.ChatMessage(string.Format("{0} <color=silver>{1}</color>", Prefix, message));
            }
        }

        bool CreateBet(BasePlayer player, int betAmount, BetInfo betInfo)
        {
            if (betAmount > betInfo.max) // adjust the bet to the maximum since they clearly want to do this
                betAmount = betInfo.max;

            int amount = player.inventory.GetAmount(betInfo.itemid);

            if (amount == 0) 
            {
                player.ChatMessage(msg("BetZero", player.UserIDString));
                return false;
            }

            if (amount < betAmount) // obviously they're just trying to see how this works. we won't adjust it here.
            {
                player.ChatMessage(msg("BetNotEnough", player.UserIDString));
                return false;
            }

            var takenItems = new List<Item>();
            int takenAmount = player.inventory.Take(takenItems, betInfo.itemid, betAmount);
            
            if (takenAmount == betAmount)
            {
                var bet = new BetInfo() { itemid = betInfo.itemid, amount = betAmount, trigger = betInfo.trigger };

                duelsData.Bets.Add(player.UserIDString, bet);

                string message = msg("BetPlaced", player.UserIDString, betInfo.trigger, betAmount);

                if (allowBetRefund)
                    message += msg("BetRefundSuffix", player.UserIDString, szDuelChatCommand);
                else if (allowBetForfeit)
                    message += msg("BetForfeitSuffix", player.UserIDString, szDuelChatCommand);

                player.ChatMessage(message);
                Puts("{0} bet {1} ({2})", player.displayName, betInfo.trigger, betAmount);

                foreach (Item item in takenItems.ToList())
                    item.Remove(0.1f);

                return true;
            }

            if (takenItems.Count > 0)
            {
                foreach (Item item in takenItems.ToList())
                    player.GiveItem(item, BaseEntity.GiveItemReason.Generic);

                takenItems.Clear();
            }

            return false;
        }

        List<ulong> GetItemSkins(ItemDefinition def)
        {
            if (!skinsCache.ContainsKey(def.shortname))
            {
                var skins = new List<ulong>();

                skins.AddRange(def.skins.Select(skin => Convert.ToUInt64(skin.id)));
                skins.AddRange(Rust.Workshop.Approved.All.Where(skin => !string.IsNullOrEmpty(skin.Skinnable.ItemName) && skin.Skinnable.ItemName == def.shortname).Select(skin => skin.WorkshopdId));

                if (skins.Contains(0uL))
                    skins.Remove(0uL);

                skinsCache.Add(def.shortname, skins);
            }

            return skinsCache[def.shortname];
        }

        #region Config
        private bool Changed;
        string szDuelChatCommand;
        string szQueueChatCommand;
        string duelistPerm = "duelist.dd";
        string duelistGroup = "duelist";
        static float zoneRadius;
        int deathTime;
        int immunityTime;
        int zoneCounter;
        List<string> duelingKits = new List<string>();
        List<BetInfo> duelingBets = new List<BetInfo>();
        bool recordStats = true;
        int permsToGive = 3;
        float maxIncline;
        bool allowBetForfeit;
        bool allowBetRefund;
        bool allowBets;
        bool putToSleep;
        bool killNpc;
        bool fleeNpc;
        bool useAnnouncement;
        bool autoSetup;
        bool broadcastDefeat;
        double economicsMoney;
        int serverRewardsPoints;
        float damagePercentageScale;
        int zoneAmount;
        static int playersPerZone;
        bool visibleToAdmins;
        float spDrawTime;
        float spRemoveOneMaxDistance;
        float spRemoveAllMaxDistance;
        bool spRemoveInRange;
        bool spAutoRemove;
        bool avoidWaterSpawns;
        bool useInvisibility;
        int extraWallStacks;
        bool useZoneWalls;
        int minWallAuthLevel;
        float maxCustomWallRadius;
        bool zoneUseWoodenWalls;
        bool customArenasUseWallProtection;
        bool customArenasNoRaiding;
        bool customArenasNoPVP;
        bool customArenasUseWooden;
        bool customArenasNoBuilding;
        float buildingBlockExtensionRadius;
        bool autoAllowAll;
        bool useRandomSkins;
        bool showWarning;
        float playerHealth;

        List<object> DeveloperKits
        {
            get
            {
                return new List<object>
                {
                    "m92", "lr300", "mp5a4", "battlefield", "semiauto", "sap", "EventAK", "dbs", "waterpipe", "melee", "eventbow"
                };
            }
        }
        
        List<object> DefaultBets
        {
            get
            {
                return new List<object>
                {
                    new Dictionary<string, object> { ["trigger"] = "stone", ["max"] = 50000, ["itemid"] = -892070738 },
                    new Dictionary<string, object> { ["trigger"] = "sulfur", ["max"] = 50000, ["itemid"] = -891243783 },
                    new Dictionary<string, object> { ["trigger"] = "fragment", ["max"] = 50000, ["itemid"] = 688032252 },
                    new Dictionary<string, object> { ["trigger"] = "charcoal", ["max"] = 50000, ["itemid"] = 1436001773 },
                    new Dictionary<string, object> { ["trigger"] = "gp", ["max"] = 25000, ["itemid"] = -1580059655 },
                    new Dictionary<string, object> { ["trigger"] = "hqm", ["max"] = 1000, ["itemid"] = 374890416 },
                    new Dictionary<string, object> { ["trigger"] = "c4", ["max"] = 10, ["itemid"] = 498591726 },
                    new Dictionary<string, object> { ["trigger"] = "rocket", ["max"] = 6, ["itemid"] = 1578894260 }
                };
            }
        }

        Dictionary<string, List<DuelKitItem>> customKits = new Dictionary<string, List<DuelKitItem>>();

        Dictionary<string, object> DefaultKits
        {
            get
            {
                return new Dictionary<string, object>
                {
                    ["Hunting Bow"] = new List<object>
                    {
                        new DuelKitItem() { shortname = "bow.hunting", amount = 1, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "arrow.wooden", amount = 50, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "spear.stone", amount = 1, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "bandage", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "syringe.medical", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "largemedkit", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "burlap.gloves", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "burlap.headwrap", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "burlap.shirt", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "burlap.shoes", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "burlap.trousers", amount = 1, skin = 0, container = "wear", slot = -1 },
                    },
                    ["Assault Rifle and Bolt Action Rifle"] = new List<object>
                    {
                        new DuelKitItem() { shortname = "rifle.ak", amount = 1, skin = 0, container = "belt", slot = -1, ammo = "ammo.rifle", mods = new List<string>() { "weapon.mod.lasersight" } },
                        new DuelKitItem() { shortname = "rifle.bolt", amount = 1, skin = 0, container = "belt", slot = -1, ammo = "ammo.rifle", mods = new List<string>() { "weapon.mod.lasersight", "weapon.mod.small.scope" } },
                        new DuelKitItem() { shortname = "largemedkit", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "bandage", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "syringe.medical", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "bearmeat.cooked", amount = 10, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "hoodie", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "metal.facemask", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "metal.plate.torso", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "pants", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "burlap.gloves", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "shoes.boots", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "ammo.rifle", amount = 200, skin = 0, container = "main", slot = -1 },
                        new DuelKitItem() { shortname = "weapon.mod.flashlight", amount = 1, skin = 0, container = "main", slot = -1 },
                        new DuelKitItem() { shortname = "weapon.mod.small.scope", amount = 1, skin = 0, container = "main", slot = -1 },
                    },
                    ["Semi-Automatic Pistol"] = new List<object>
                    {
                        new DuelKitItem() { shortname = "pistol.semiauto", amount = 1, skin = 0, container = "belt", slot = -1, ammo = "ammo.pistol", mods = new List<string>() { "weapon.mod.lasersight" } },
                        new DuelKitItem() { shortname = "largemedkit", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "bandage", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "syringe.medical", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "bearmeat.cooked", amount = 10, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "hoodie", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "metal.facemask", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "metal.plate.torso", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "pants", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "burlap.gloves", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "shoes.boots", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "ammo.pistol", amount = 200, skin = 0, container = "main", slot = -1 },
                        new DuelKitItem() { shortname = "weapon.mod.flashlight", amount = 1, skin = 0, container = "main", slot = -1 },
                    },
                    ["Pump Shotgun"] = new List<object>
                    {
                        new DuelKitItem() { shortname = "shotgun.pump", amount = 1, skin = 0, container = "belt", slot = -1, ammo = "ammo.shotgun.slug", mods = new List<string>() { "weapon.mod.lasersight" } },
                        new DuelKitItem() { shortname = "largemedkit", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "bandage", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "syringe.medical", amount = 5, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "bearmeat.cooked", amount = 10, skin = 0, container = "belt", slot = -1 },
                        new DuelKitItem() { shortname = "hoodie", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "metal.facemask", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "metal.plate.torso", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "pants", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "burlap.gloves", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "shoes.boots", amount = 1, skin = 0, container = "wear", slot = -1 },
                        new DuelKitItem() { shortname = "ammo.shotgun.slug", amount = 200, skin = 0, container = "main", slot = -1 },
                        new DuelKitItem() { shortname = "weapon.mod.flashlight", amount = 1, skin = 0, container = "main", slot = -1 },
                    },
                };
            }
        }

        protected string Prefix { get; } = "[ <color=#406B35>Duelist</color> ]: ";

        protected override void LoadDefaultMessages() // holy shit this took forever.
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Awards"] = "{0} ({1}) duels won {2}",
                ["Granted"] = "Granted {0} ({1}) permission {2} for group {3}",
                ["Logged"] = "Duelists have been logged to: {0}",
                ["Indestructible"] = "This object belongs to the server and is indestructible!",
                ["Building is blocked!"] = "<color=red>Building is blocked inside of dueling zones!</color>",
                ["KitsNotFound"] = "No valid kits were found in the config file. Using default duelist kits instead.",
                ["TopAll"] = "[ <color=#ffff00>Top Duelists Of All Time ({0})</color> ]:",
                ["Top"] = "[ <color=#ffff00>Top Duelists ({0})</color> ]:",
                ["NoLongerQueued"] = "You are no longer in queue for a duel.",
                ["TryQueueAgain"] = "Please try to queue again.",
                ["InQueueSuccess"] = "You are now in queue for a duel. You will teleport instantly when a match is available.",
                ["MustBeNaked"] = "<color=red>You must be naked before you can duel.</color>",
                ["PendingRequest"] = "You have a duel request pending already. You must accept this request, wait for it to time out, or use <color=orange>/{0} cancel</color>",
                ["AlreadyInADuel"] = "You cannot queue for a duel while already in a duel!",
                ["MustAllowDuels"] = "You must allow duels first! Type: <color=orange>/{0} allow</color>",
                ["DuelsDisabled"] = "Duels are disabled.",
                ["NoZoneExists"] = "No dueling zone exists.",
                ["Banned"] = "You are banned from duels.",
                ["FoundZone"] = "Took {0} tries ({1}ms) to get a dueling zone.",
                ["ImmunityFaded"] = "Your immunity has faded.",
                ["NotifyBetWon"] = "You have won your bet! To claim type <color=orange>/{0} claim</color>.",
                ["ConsoleBetWon"] = "{0} ({1}) won his bet against {2} ({3})!",
                ["DuelDeathMessage"] = "<color=silver><color=lime>{0}</color> (<color=lime>W</color>: <color=orange>{1}</color> / <color=red>L</color>: <color=orange>{2}</color>) has defeated <color=lime>{3}</color> (<color=lime>W</color>: <color=orange>{4}</color> / <color=red>L</color>: <color=orange>{5}</color>) in a duel with <color=green>{6}</color> health left.{7}</color>",
                ["BetWon"] = " Bet won: <color=lime>{0}</color> (<color=lime>{1}</color>)",
                ["ExecutionTime"] = "You have <color=red>{0} minutes</color> to win the duel before you are executed.",
                ["DuelWarning"] = "You can only fight the player you are dueling. Other duelers will be invisible to you, but their bullets will not be.",
                ["FailedZone"] = "Failed to create a dueling zone, please try again.",
                ["FailedSetup"] = "Failed to setup the zone, please try again.",
                ["FailedRaycast"] = "Look towards the ground, and try again.",
                ["BetPlaced"] = "Your bet {0} ({1}) has been placed.",
                ["BetForfeitSuffix"] = " Type <color=orange>/{0} bet forfeit</color> to forfeit your bet.",
                ["BetRefundSuffix"] = " Type <color=orange>/{0} bet refund</color> to refund your bet.",
                ["BetNotEnough"] = "Bet cancelled. You do not have enough to bet this amount!",
                ["BetZero"] = "Bet cancelled. You do not have this item in your inventory.",
                ["DuelAnnouncement"] = "Type <color=orange>/{duelChatCommand}</color> for information on the dueling system. See your standing on the leaderboard by using <color=orange>/{ladderCommand}</color>. Type <color=orange>/{queueCommand}</color> to enter the dueling queue now!",
                ["DuelAnnouncementBetsSuffix"] = " Feeling lucky? Use <color=orange>/{0} bet</color> to create a bet!",
                ["ZoneCreated"] = "Dueling zone created successfully.",
                ["RemovedZone"] = "Removed dueling zone.",
                ["RemovedBan"] = "Unbanned {0}",
                ["AddedBan"] = "Banned {0}",
                ["PlayerNotFound"] = "{0} not found. Try being more specific or use a steam id.",
                ["RequestTimedOut"] = "Request timed out to duel <color=lime>{0}</color>",
                ["RemovedFromQueueRequest"] = "You have been removed from the dueling queue since you have requested to duel another player.",
                ["RemovedFromDuel"] = "You have been removed from your duel.",
                ["SentRequest"] = "Sent request to duel <color=lime>{0}</color>. Request expires in 1 minute.",
                ["DuelRequested"] = "<color=lime>{0}</color> has requested a duel. You have 1 minute to type <color=orange>/{1} accept</color> to accept the duel.",
                ["BetsDoNotMatch"] = "Your bet {0} ({1}) does not match {2} ({3})",
                ["InvalidBet"] = "Invalid bet '{0}'",
                ["BetSyntax"] = "Syntax: /{0} bet <item> <amount> - resources must be refined",
                ["AvailableBets"] = "Available Bets:",
                ["MustHaveSameBet"] = "{0} is betting: {1} ({2}). You must have the same bet to duel this player.",
                ["NoBetsToRefund"] = "There are no bets to refund.",
                ["Disabled"] = "Disabled",
                ["HelpDuelBet"] = "<color=silver><color=orange>/{0} bet</color> - place a bet towards your next duel.</color>",
                ["HelpDuelAdmin"] = "<color=orange>Admin: /{0} on|off</color> - enable/disable duels",
                ["HelpDuelAdminRefundAll"] = "<color=orange>Admin: /{0} bet refundall</color> - refund all bets for all players",
                ["DuelsDisabledAlready"] = "Duels are already disabled!",
                ["DuelsNowDisabled"] = "Duels disabled. Sending duelers home.",
                ["DuelsEnabledAlready"] = "Duels are already enabled!",
                ["DuelsNowEnabled"] = "Duels enabled",
                ["NoBetsToClaim"] = "You have no bets to claim.",
                ["PlayerClaimedBet"] = "Claimed bet {0} ({1})",
                ["AllBetsClaimed"] = "You have claimed all of your bets.",
                ["DuelChatOff"] = "You will no longer see duel death messages.",
                ["DuelChatOn"] = "You will now see duel death messages.",
                ["PlayerRequestsOn"] = "Players may now request to duel you. You will be removed from this list if you do not duel.",
                ["PlayerRequestsOff"] = "Players may no longer request to duel you.",
                ["BlockedRequestsFrom"] = "Blocked duel requests from: <color=lime>{0}</color>",
                ["UnblockedRequestsFrom"] = "Removed block on duel requests from: <color=lime>{0}</color>",
                ["AlreadyBlocked"] = "You have already blocked players from requesting duels.",
                ["NoBetsConfigured"] = "No bets are configured.",
                ["RefundAllPlayerNotice"] = "Server administrator has refunded your bet: {0} ({1})",
                ["RefundAllAdminNotice"] = "Refunded {0} ({1}): {2} ({3})",
                ["BetsRemaining"] = "Bet items remaining in database: {0}",
                ["AllBetsRefunded"] = "All dueling bets refunded",
                ["CannotForfeit"] = "You cannot forfeit bets on this server.",
                ["CannotForfeitRequestDuel"] = "You cannot forfeit a bet while requesting a duel!",
                ["CannotForfeitInDuel"] = "You cannot forfeit a bet while dueling!",
                ["CannotRefundRequestDuel"] = "You cannot refund a bet while requesting a duel!",
                ["CannotRefundInDuel"] = "You cannot refund a bet while dueling!",
                ["BetForfeit"] = "You forfeit your bet!",
                ["NoBetToForfeit"] = "You do not have an active bet to forfeit.",
                ["NoBetToRefund"] = "You do not have an active bet to refund.",
                ["CannotRefund"] = "You cannot refund bets on this server.",
                ["BetRefunded"] = "You have refunded your bet.",
                ["AlreadyBetting"] = "You are already betting! Your bet: {0} ({1})",
                ["ToRefundUse"] = "To refund your bet, type: <color=orange>/{0} bet refund</color>",
                ["ToForfeitUse"] = "To forfeit your bet, type: <color=orange>/{0} bet forfeit</color>. Refunds are not allowed.",
                ["InvalidNumber"] = "Invalid number: {0}",
                ["MultiplesOnly"] = "Number must be a multiple of 500. ie: 500, 1000, 2000, 5000, 10000, 15000",
                ["NoRequestsReceived"] = "No players have requested a duel with you.",
                ["DuelCancelledFor"] = "<color=lime>{0}</color> has cancelled the duel!",
                ["NoPendingRequests"] = "You have no pending request to cancel.",
                ["DuelCancelledWith"] = "<color=lime>{0}</color> has cancelled the duel request.",
                ["DuelCancelComplete"] = "Duel request cancelled.",
                ["MustWaitToRequestAgain"] = "You must wait <color=red>{0} minute(s)</color> from the last time you requested a duel to request another.",
                ["MaxDuelsInProgress"] = "Maximum dueling matches in progress ({0}). Wait until a match ends, and try again.",
                ["AlreadyDueling"] = "You are already dueling another player!",
                ["CannotRequestThisPlayer"] = "You are not allowed to request duels with this player.",
                ["TargetAlreadyDueling"] = "<color=lime>{0}</color> is already dueling another player!",
                ["NotAllowedYet"] = "<color=lime>{0}</color> has not enabled duel requests yet. They must type <color=orange>/{0} allow</color>",
                ["MustWaitForAccept"] = "You have requested a duel with <color=lime>{0}</color> already. You must wait for this player to accept the duel.",
                ["PendingRequestAlready"] = "This player has a duel request pending already.",
                ["TargetHasNoBet"] = "You have an active bet going. <color=lime>{0}</color> must have the same bet to duel you.",
                ["YourBet"] = "Your bet: {0} ({1})",
                ["WoundedQueue"] = "You cannot duel while either player is wounded.",
                ["DuelMustBeNaked"] = "Duel cancelled: <color=lime>{0}</color> inventory is not empty.",
                ["LadderLife"] = "<color=#5A625B>Use <color=yellow>/{0} ladder life</color> to see all time stats</color>",
                ["EconomicsDeposit"] = "You have received <color=yellow>${0}</color>!",
                ["ServerRewardPoints"] = "You have received <color=yellow>{0} RP</color>!",
                ["DuelsMustBeEnabled"] = "Use '/{0} on' to enable dueling on the server.",
                ["DataSaved"] = "Data has been saved.",
                ["DuelsNowDisabledEmpty"] = "Duels disabled.",
                ["CannotTeleport"] = "You are not allowed to teleport from a dueling zone.",
                ["AllZonesFull"] = "All zones are currently full. Zones: {0}. Limit Per Zone: {1}",
                ["NoZoneFound"] = "No zone found. You must stand inside of the zone to remove it.",
                ["RemovedZoneAt"] = "Removed zone at {0}",
                ["CannotDuel"] = "You are not allowed to duel at the moment.",
                ["LeftZone"] = "<color=red>You were found outside of the dueling zone while dueling. Your items have been removed.</color>",
                ["SpawnAdd"] = "<color=orange>/{0} spawns add</color> - add a spawn point at the position you are looking at.",
                ["SpawnHere"] = "<color=orange>/{0} spawns here</color> - add a spawn point at your position.",
                ["SpawnRemove"] = "<color=orange>/{0} spawns remove</color> - removes the nearest spawn point within <color=orange>{1}m</color>.",
                ["SpawnRemoveAll"] = "<color=orange>/{0} spawns removeall</color> - remove all spawn points within <color=orange>{1}m</color>.",
                ["SpawnWipe"] = "<color=orange>/{0} spawns wipe</color> - wipe all spawn points.",
                ["SpawnWiped"] = "<color=red>{0}</color> spawns points wiped.",
                ["SpawnCount"] = "<color=green>{0}</color> spawn points in database.",
                ["SpawnNoneFound"] = "No custom spawn points found within <color=orange>{0}m</color>.",
                ["SpawnAdded"] = "Spawn point added at {0}",
                ["SpawnRemoved"] = "Removed <color=red>{0}</color> spawn(s)",
                ["SpawnExists"] = "This spawn point exists already.",
                ["SpawnNoneExist"] = "No spawn points exist.",
                ["ZoneExists"] = "A dueling zone already exists here.",
                ["ZoneLimit"] = "Zone limit reached ({0}). You must manually remove an existing zone before creating a new one.",
                ["CannotEventJoin"] = "You are not allowed to join this event while dueling.",
                ["KitDoesntExist"] = "This kit doesn't exist: {0}",
                ["KitSet"] = "Custom kit set to {0}. This kit will be used when both players have the same custom kit.",
                ["KitsNotConfigured"] = "No kits have been configured for dueling.",
                ["RemovedXWalls"] = "Removed {0} walls.",
                ["SupportCreated"] = "{0} new dueling zones were created, however the total amount was not met. Please lower the radius, increase Maximum Incline On Hills, or reload the plugin to try again.",
                ["SupportInvalidConfig"] = "Invalid zone radius detected in the configuration file for this map size. Please lower the radius, increase Maximum Incline On Hills, or reload the plugin to try again.",
                ["WallSyntax"] = "Use <color=orange>/{0} walls [radius] <wood|stone></color>, or stand inside of an existing area with walls and use <color=orange>/{0} walls</color> to remove them.",
                ["GeneratedWalls"] = "Generated {0} arena walls {1} high at {2} in {3}ms",
                ["ResetKit"] = "You are no longer using a custom kit.",
                ["HelpDuels"] = "<color=#183a0e><size=18>DUELIST ({0})</size></color><color=#5A625B>\nDuel other players.</color>",
                ["HelpAllow"] = "<color=#5A397A>/{0} allow</color><color=#5A625B> • Toggle requests for duels</color>",
                ["HelpBlock"] = "<color=#5A397A>/{0} block <name></color><color=#5A625B> • Toggle block requests for a player</color>",
                ["HelpChallenge"] = "<color=#5A397A>/{0} <name></color><color=#5A625B> • Challenge another player</color>",
                ["HelpAccept"] = "<color=#5A397A>/{0} accept</color><color=#5A625B> • Accept a challenge</color>", 
                ["HelpCancel"] = "<color=#5A397A>/{0} cancel</color><color=#5A625B> • Cancel your duel request</color>",
                ["HelpQueue"] = "<color=#5A397A>/{0}</color><color=#5A625B> • Join duel queue</color>",
                ["HelpChat"] = "<color=#5A397A>/{0} chat</color><color=#5A625B> • Toggle duel death messages</color>",
                ["HelpLadder"] = "<color=#5A397A>/{0} ladder</color><color=#5A625B> • Show top 10 duelists</color>",
                ["HelpBet"] = "<color=#5A397A>/{0} bet</color><color=#5A625B> • Place a bet towards a duel</color>",
                ["TopFormat"] = "<color=#666666><color=#5A625B>{0}.</color> <color=#00FF00>{1}</color> (<color=#008000>W:{2}</color> • <color=#ff0000>L:{3} </color> • <color=#4c0000>WLR:{4}</color>)</color>",
                ["NowDueling"] = "<color=#ff0000>You are now dueling <color=#00FF00>{0}</color>!</color>",
            }, this);
        }

        void LoadVariables()
        {
            putToSleep = Convert.ToBoolean(GetConfig("Animals", "Put To Sleep", true));
            fleeNpc = Convert.ToBoolean(GetConfig("Animals", "Flee [Not Instantenous!]", false));
            killNpc = Convert.ToBoolean(GetConfig("Animals", "Die Instantly", false));
            
            autoSetup = Convert.ToBoolean(GetConfig("Settings", "Auto Create Dueling Zone If Zone Does Not Exist", false));
            immunityTime = Convert.ToInt32(GetConfig("Settings", "Immunity Time", 10));
            deathTime = Convert.ToInt32(GetConfig("Settings", "Time To Duel In Minutes Before Death", 10));
            szDuelChatCommand = Convert.ToString(GetConfig("Settings", "Duel Command Name", "duel"));
            szQueueChatCommand = Convert.ToString(GetConfig("Settings", "Queue Command Name", "queue"));
            useAnnouncement = Convert.ToBoolean(GetConfig("Settings", "Allow Announcement", true));
            broadcastDefeat = Convert.ToBoolean(GetConfig("Settings", "Broadcast Defeat To All Players", true));
            damagePercentageScale = Convert.ToSingle(GetConfig("Settings", "Scale Damage Percent", 1f));
            useInvisibility = Convert.ToBoolean(GetConfig("Settings", "Use Invisibility", true));
            buildingBlockExtensionRadius = Convert.ToSingle(GetConfig("Settings", "Building Block Extension Radius", 20f));
            autoAllowAll = Convert.ToBoolean(GetConfig("Settings", "Disable Requirement To Allow Duels", false));
            useRandomSkins = Convert.ToBoolean(GetConfig("Settings", "Use Random Skins", true));
            showWarning = Convert.ToBoolean(GetConfig("Settings", "Show Warning Message", false));
            playerHealth = Convert.ToSingle(GetConfig("Settings", "Player Health After Duel [0 = disabled]", 100f));

            allowBetForfeit = Convert.ToBoolean(GetConfig("Betting", "Allow Bets To Be Forfeit", true));
            allowBetRefund = Convert.ToBoolean(GetConfig("Betting", "Allow Bets To Be Refunded", false));
            allowBets = Convert.ToBoolean(GetConfig("Betting", "Enabled", false));

            zoneRadius = Convert.ToSingle(GetConfig("Zone", "Zone Radius (Min: 50, Max: 300)", 50f));
            zoneCounter = Convert.ToInt32(GetConfig("Zone", "Create New Zone Every X Duels [0 = disabled]", 10));
            maxIncline = Convert.ToSingle(GetConfig("Zone", "Maximum Incline On Hills", 40f));
            zoneAmount = Convert.ToInt32(GetConfig("Zone", "Max Zones [Min 1]", 1));
            playersPerZone = Convert.ToInt32(GetConfig("Zone", "Players Per Zone [Multiple Of 2]", 10));
            visibleToAdmins = Convert.ToBoolean(GetConfig("Zone", "Players Visible To Admins", true));
            avoidWaterSpawns = Convert.ToBoolean(GetConfig("Zone", "Avoid Creating Automatic Spawn Points In Water", true));
            extraWallStacks = Convert.ToInt32(GetConfig("Zone", "Extra High External Wall Stacks", 2));
            useZoneWalls = Convert.ToBoolean(GetConfig("Zone", "Use Arena Wall Generation", true));
            minWallAuthLevel = Convert.ToInt32(GetConfig("Zone", "Minimum Auth Level For Custom Arena Walls", 1));
            maxCustomWallRadius = Convert.ToSingle(GetConfig("Zone", "Maximum Custom Wall Radius", 300f));
            zoneUseWoodenWalls = Convert.ToBoolean(GetConfig("Zone", "Use Wooden Walls", false));

            foreach (var itemDef in ItemManager.GetItemDefinitions().ToList())
            {
                var mod = itemDef.GetComponent<ItemModDeployable>();

                if (mod != null)
                {
                    bool externalWall = mod.entityPrefab.resourcePath.Contains("external") && mod.entityPrefab.resourcePath.Contains("wall");
                    bool barricade = mod.entityPrefab.resourcePath.Contains("barricade");

                    if (externalWall || barricade)
                    {
                        bool value = Convert.ToBoolean(GetConfig("Deployables", string.Format("Allow {0}", itemDef.displayName.translated), false));

                        if (!value)
                            continue;

                        deployables[itemDef.displayName.translated] = value;
                        prefabs[mod.entityPrefab.resourcePath] = itemDef.displayName.translated;
                    }
                }
            }

            if (zoneAmount < 1)
                zoneAmount = 1;

            if (playersPerZone < 2)
                playersPerZone = 2;
            else if (playersPerZone % 2 != 0)
                playersPerZone++;

            recordStats = Convert.ToBoolean(GetConfig("Ranked Ladder", "Enabled", true));

            if (recordStats)
            {
                permsToGive = Convert.ToInt32(GetConfig("Ranked Ladder", "Award Top X Players On Wipe", 3));

                if (!permission.PermissionExists(duelistPerm)) // prevent warning
                    permission.RegisterPermission(duelistPerm, this);

                if (!permission.GroupHasPermission(duelistGroup, duelistPerm))
                {
                    permission.CreateGroup(duelistGroup, duelistGroup, 0);
                    permission.GrantGroupPermission(duelistGroup, duelistPerm, this);
                }
            }

            var kits = GetConfig("Settings", "Kits", new List<object> { "kit_1", "kit_2", "kit_3" }) as List<object>;
            
            if (kits != null && kits.Count > 0)
            {
                foreach (string kit in kits.Cast<string>().ToList())
                {
                    if (!string.IsNullOrEmpty(kit) && !duelingKits.Contains(kit))
                    {
                        duelingKits.Add(kit); // 0.1.14 fix
                    }
                }
            }
            else
                duelingKits.AddRange(DeveloperKits.Cast<string>()); // developer trick. empty the list in the config to use this.

            var defaultKits = GetConfig("Custom Kits", "Kits", DefaultKits) as Dictionary<string, object>;

            if (defaultKits != null && defaultKits.Count > 0)
            {
                foreach (var kit in defaultKits)
                {
                    if (customKits.ContainsKey(kit.Key))
                        customKits.Remove(kit.Key);

                    customKits.Add(kit.Key, new List<DuelKitItem>());

                    if (kit.Value is List<object>) // list of DuelKitItem
                    {
                        var objects = kit.Value as List<object>;

                        if (objects != null && objects.Count > 0)
                        {
                            foreach(var entry in objects)
                            {
                                if (entry is Dictionary<string, object>)
                                {
                                    var items = entry as Dictionary<string, object>; // DuelKitItem
                                    string container = null;
                                    string shortname = null;
                                    string ammo = null;
                                    int amount = int.MinValue;
                                    ulong skin = ulong.MaxValue;
                                    int slot = int.MinValue;
                                    var mods = new List<string>();

                                    if (items != null && items.Count > 0)
                                    {
                                        foreach(var item in items) // DuelKitItem
                                        {
                                            var kvp = (KeyValuePair<string, object>)item;

                                            switch (kvp.Key.ToString())
                                            {
                                                case "container":
                                                    {
                                                        if (kvp.Value != null && kvp.Value.ToString().Length > 0)
                                                            container = kvp.Value.ToString();
                                                    }
                                                    break;
                                                case "shortname":
                                                    {
                                                        if (kvp.Value != null && kvp.Value.ToString().Length > 0)
                                                            shortname = kvp.Value.ToString();
                                                    }
                                                    break;
                                                case "amount":
                                                    {
                                                        int num;
                                                        if (int.TryParse(kvp.Value.ToString(), out num))
                                                            amount = num;
                                                    }
                                                    break;
                                                case "skin":
                                                    {
                                                        ulong num;
                                                        if (ulong.TryParse(kvp.Value.ToString(), out num))
                                                            skin = num;
                                                    }
                                                    break;
                                                case "slot":
                                                    {
                                                        int num;
                                                        if (int.TryParse(kvp.Value.ToString(), out num))
                                                            slot = num;
                                                    }
                                                    break;
                                                case "ammo":
                                                    {
                                                        if (kvp.Value != null && kvp.Value.ToString().Length > 0)
                                                            ammo = kvp.Value.ToString();
                                                    }
                                                    break;
                                                default:
                                                    {
                                                        if (kvp.Value is List<object>)
                                                        {
                                                            var _mods = kvp.Value as List<object>;

                                                            foreach(var mod in _mods)
                                                            {
                                                                if (mod != null && mod.ToString().Length > 0)
                                                                {
                                                                    if (!mods.Contains(mod.ToString()))
                                                                        mods.Add(mod.ToString());
                                                                }
                                                            }
                                                        }
                                                    }
                                                    break;
                                            }                                            

                                        }
                                    }
                                    
                                    if (shortname == null || container == null || amount == int.MinValue || skin == ulong.MaxValue || slot == int.MinValue)
                                    {
                                        continue; // missing a key. invalid item
                                    }
                                    
                                    customKits[kit.Key].Add(new DuelKitItem() { amount = amount, container = container, shortname = shortname, skin = skin, slot = slot, ammo = ammo, mods = mods.Count > 0 ? mods : null });
                                }
                            }                            
                        }
                    }
                }

                foreach (var kit in customKits.ToList())
                    if (kit.Value.Count == 0)
                        customKits.Remove(kit.Key);
            }

            var bets = GetConfig("Betting", "Bets", DefaultBets) as List<object>;

            if (bets != null && bets.Count > 0)
            {
                foreach (var bet in bets)
                {
                    if (bet is Dictionary<string, object>)
                    {
                        var dict = bet as Dictionary<string, object>;

                        if (dict.ContainsKey("trigger") && dict["trigger"] != null && dict["trigger"].ToString().Length > 0)
                        {
                            int max;
                            if (dict.ContainsKey("max") && dict["max"] != null && int.TryParse(dict["max"].ToString(), out max) && max > 0)
                            {
                                int itemid;
                                if (dict.ContainsKey("itemid") && dict["itemid"] != null && int.TryParse(dict["itemid"].ToString(), out itemid))
                                {
                                    duelingBets.Add(new BetInfo() { trigger = dict["trigger"].ToString(), itemid = itemid, max = max }); // 0.1.5 fix - remove itemlist find as it is null when server starts up and new config is created
                                }
                            }
                        }
                    }
                }
            }

            if (immunityTime < 0)
                immunityTime = 0;

            if (zoneRadius < 50f)
                zoneRadius = 50f;
            else if (zoneRadius > 300f)
                zoneRadius = 300f;

            if (!string.IsNullOrEmpty(szDuelChatCommand))
            {
                cmd.AddChatCommand(szDuelChatCommand, this, cmdDuel);
                cmd.AddConsoleCommand(szDuelChatCommand, this, nameof(ccmdDuel));
            }

            if (!string.IsNullOrEmpty(szQueueChatCommand))
                cmd.AddChatCommand(szQueueChatCommand, this, cmdQueue);

            economicsMoney = Convert.ToDouble(GetConfig("Rewards", "Economics Money [0 = disabled]", 0.0));
            serverRewardsPoints = Convert.ToInt32(GetConfig("Rewards", "ServerRewards Points [0 = disabled]", 0));

            spDrawTime = Convert.ToSingle(GetConfig("Spawns", "Draw Time", 30f));
            spRemoveOneMaxDistance = Convert.ToSingle(GetConfig("Spawns", "Remove Distance", 10f));
            spRemoveAllMaxDistance = Convert.ToSingle(GetConfig("Spawns", "Remove All Distance", zoneRadius));
            spRemoveInRange = Convert.ToBoolean(GetConfig("Spawns", "Remove In Duel Zone Only", false));
            spAutoRemove = Convert.ToBoolean(GetConfig("Spawns", "Auto Remove On Zone Removal", false));

            customArenasUseWallProtection = Convert.ToBoolean(GetConfig("Custom Arenas", "Indestructible Walls", true));
            customArenasNoRaiding = Convert.ToBoolean(GetConfig("Custom Arenas", "No Raiding", false));
            customArenasNoPVP = Convert.ToBoolean(GetConfig("Custom Arenas", "No PVP", false));
            customArenasUseWooden = Convert.ToBoolean(GetConfig("Custom Arenas", "Use Wooden Walls", false));
            customArenasNoBuilding = Convert.ToBoolean(GetConfig("Custom Arenas", "No Building", false));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
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
            string message = id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        string RemoveFormatting(string source) => source.Contains(">") ? System.Text.RegularExpressions.Regex.Replace(source, "<.*?>", string.Empty) : source;
        #endregion
    }
}