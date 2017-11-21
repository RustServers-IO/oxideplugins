using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using UnityEngine;
using Facepunch;
using Rust;

namespace Oxide.Plugins
{
    [Info("NoEscape", "rustservers.io", "1.1.4", ResourceId = 1394)]
    [Description("Prevent commands while raid and/or combat is occuring")]
    class NoEscape : RustPlugin
    {
        #region Setup & Configuration

        List<string> blockTypes = new List<string>()
        {
            "remove",
            "tp",
            "bank",
            "trade",
            "recycle",
            "shop",
            "bgrade",
            "build",
            "repair",
            "upgrade",
            "vend",
            "kit",
            "assignbed"
        };

        // COMBAT SETTINGS
        private bool combatBlock;
        private static float combatDuration;
        private bool combatOnHitPlayer;
        private bool combatOnTakeDamage;

        // RAID BLOCK SETTINGS
        private bool raidBlock;
        private static float raidDuration;
        private float raidDistance;
        private bool blockOnDamage;
        private bool blockOnDestroy;

        // RAID-ONLY SETTINGS
        private bool ownerCheck;
        private bool blockUnowned;
        private bool blockAll; // IGNORES ALL OTHER CHECKS
        private bool ownerBlock;
        private bool cupboardShare;
        private bool friendShare;
        private bool clanShare;
        private bool clanCheck;
        private bool friendCheck;
        private bool raiderBlock;
        private bool raiderFriendShare;
        private bool raiderClanShare;
        private List<string> raidDamageTypes;
        private List<string> combatDamageTypes;

        // RAID UNBLOCK SETTINGS
        private bool raidUnblockOnDeath;
        private bool raidUnblockOnWakeup;
        private bool raidUnblockOnRespawn;

        // COMBAT UNBLOCK SETTINGS
        private bool combatUnblockOnDeath;
        private bool combatUnblockOnWakeup;
        private bool combatUnblockOnRespawn;

        private float cacheTimer;

        // MESSAGES
        private bool raidBlockNotify;
        private bool combatBlockNotify;

        private bool useZoneManager;
        private bool zoneEnter;
        private bool zoneLeave;

        internal bool sendUINotification;
        internal bool sendChatNotification;

        private Dictionary<string, RaidZone> zones = new Dictionary<string, RaidZone>();
        private Dictionary<string, List<string>> memberCache = new Dictionary<string, List<string>>();
        private Dictionary<string, string> clanCache = new Dictionary<string, string>();
        private Dictionary<string, List<string>> friendCache = new Dictionary<string, List<string>>();
        private Dictionary<string, DateTime> lastClanCheck = new Dictionary<string, DateTime>();
        private Dictionary<string, DateTime> lastCheck = new Dictionary<string, DateTime>();
        private Dictionary<string, DateTime> lastFriendCheck = new Dictionary<string, DateTime>();
        Dictionary<string, bool> prefabBlockCache = new Dictionary<string, bool>();

        public static NoEscape plugin;

        [PluginReference]
        Plugin Clans;

        [PluginReference]
        Plugin Friends;

        [PluginReference]
        Plugin ZoneManager;

        private readonly int cupboardMask = LayerMask.GetMask("Deployed");
        private readonly int blockLayer = LayerMask.GetMask("Player (Server)");

        List<string> blockedPrefabs = new List<string>()
        {
            "door",
            "window.bars",
            "floor.ladder.hatch",
            "floor.frame",
            "wall.frame",
            "shutter",
            "external"
        };

        List<string> exceptionPrefabs = new List<string>()
        {
            "ladder.wooden"
        };

        private List<string> GetDefaultRaidDamageTypes()
        {
            return new List<DamageType>()
            {
                DamageType.Bullet,
                DamageType.Blunt,
                DamageType.Stab,
                DamageType.Slash,
                DamageType.Explosion,
                DamageType.Heat,
            }.Select(x => x.ToString()).ToList<string>();
        }

        private List<string> GetDefaultCombatDamageTypes()
        {
            return new List<DamageType>()
            {
                DamageType.Bullet,
                DamageType.Arrow,
                DamageType.Blunt,
                DamageType.Stab,
                DamageType.Slash,
                DamageType.Explosion,
                DamageType.Heat,
                DamageType.ElectricShock,
            }.Select(x => x.ToString()).ToList<string>();
        }

        protected override void LoadDefaultConfig()
        {
            Config["VERSION"] = Version.ToString();

            Config["raidBlock"] = true;
            Config["raidDuration"] = 300f; // 5 minutes
            Config["raidDistance"] = 100f;

            Config["blockOnDamage"] = true;
            Config["blockOnDestroy"] = false;

            Config["combatBlock"] = false;
            Config["combatDuration"] = 180f; // 3 minutes
            Config["combatOnHitPlayer"] = true;
            Config["combatOnTakeDamage"] = true;

            Config["blockUnowned"] = false;
            Config["ownerBlock"] = true;
            Config["cupboardShare"] = false;
            Config["clanShare"] = false;
            Config["friendShare"] = false;
            Config["raiderBlock"] = false;
            Config["raiderClanShare"] = false;
            Config["raiderFriendShare"] = false;
            Config["blockAll"] = false;
            Config["friendCheck"] = false;
            Config["clanCheck"] = false;
            Config["ownerCheck"] = true;

            Config["raidUnblockOnDeath"] = true;
            Config["raidUnblockOnWakeup"] = false;
            Config["raidUnblockOnRespawn"] = true;
            Config["combatUnblockOnDeath"] = true;
            Config["combatUnblockOnWakeup"] = false;
            Config["combatUnblockOnRespawn"] = true;

            Config["raidDamageTypes"] = GetDefaultRaidDamageTypes();
            Config["combatDamageTypes"] = GetDefaultCombatDamageTypes();
            Config["cacheMinutes"] = 1f;

            Config["useZoneManager"] = false;
            Config["zoneEnter"] = true;
            Config["zoneLeave"] = false;

            Config["raidBlockNotify"] = true;
            Config["combatBlockNotify"] = false;

            Config["sendUINotification"] = true;
            Config["sendChatNotification"] = true;

            Config["blockingPrefabs"] = blockedPrefabs;
            Config["exceptionPrefabs"] = exceptionPrefabs;

            Config["VERSION"] = Version.ToString();
        }

        void Loaded()
        {
            LoadMessages();
        }

        void Unload()
        {
            if(useZoneManager) 
                foreach (var zone in zones)
                    EraseZone(zone.Value.zoneid);

            var objects = GameObject.FindObjectsOfType(typeof(RaidBlock));
            if (objects != null)
                foreach (var gameObj in objects)
                    if (!((RaidBlock)gameObj).Active)
                        GameObject.Destroy(gameObj);

            objects = GameObject.FindObjectsOfType(typeof(CombatBlock));
            if (objects != null)
                foreach (var gameObj in objects)
                    if (!((CombatBlock)gameObj).Active)
                        GameObject.Destroy(gameObj);
        }

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Raid Blocked Message", "You may not do that while raid blocked ({time})"},
                {"Combat Blocked Message", "You may do that while a in combat ({time})"},
                {"Raid Block Complete", "You are no longer raid blocked."},
                {"Combat Block Complete", "You are no longer combat blocked."},
                {"Raid Block Notifier", "You are raid blocked for {time}"},
                {"Combat Block Notifier", "You are combat blocked for {time}"},
                {"Combat Block UI Message", "COMBAT BLOCK"},
                {"Raid Block UI Message", "RAID BLOCK"},
                {"Unit Seconds", "second(s)"},
                {"Unit Minutes", "minute(s)"},
                {"Prefix", string.Empty}
            }, this);
        }

        void CheckConfig()
        {
            if (Config["VERSION"] == null)
            {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig();
            }
            else if (GetConfig<string>("VERSION", string.Empty) != Version.ToString())
            {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig();
            }
        }

        protected void ReloadConfig()
        {
            Config["VERSION"] = Version.ToString();

            // NEW CONFIGURATION OPTIONS HERE
            Config["blockingPrefabs"] = GetConfig("blockingPrefabs", blockedPrefabs);
            Config["exceptionPrefabs"] = GetConfig("exceptionPrefabs", exceptionPrefabs);

            Config["cupboardShare"] = GetConfig("cupboardShare", false);
            Config["raidBlockNotify"] = GetConfig("raidBlockNotify", true);
            Config["combatBlockNotify"] = GetConfig("combatBlockNotify", false);

            Config["blockOnDamage"] = GetConfig("blockOnDamage", true);
            Config["blockOnDestroy"] = GetConfig("blockOnDestroy", false);

            Config["blockUnowned"] = GetConfig("blockUnowned", false);

            Config["raidBlock"] = GetConfig("raidBlock", true);
            Config["raidDuration"] = GetConfig("raidDuration", 300f); // 5 minutes
            Config["raidDistance"] = GetConfig("raidDistance", 100f);

            Config["combatBlock"] = GetConfig("combatBlock", false);
            Config["combatDuration"] = GetConfig("combatDuration", 180f); // 3 minutes
            Config["combatOnHitPlayer"] = GetConfig("combatOnHitPlayer", true);
            Config["combatOnTakeDamage"] = GetConfig("combatOnTakeDamage", true);

            Config["friendShare"] = GetConfig("friendShare", false);
            Config["raiderFriendShare"] = GetConfig("raiderFriendShare", false);
            Config["friendCheck"] = GetConfig("friendCheck", false);
            Config["raidUnblockOnDeath"] = GetConfig("raidUnblockOnDeath", true);
            Config["raidUnblockOnWakeup"] = GetConfig("raidUnblockOnWakeup", false);
            Config["raidUnblockOnRespawn"] = GetConfig("raidUnblockOnRespawn", true);
            Config["ownerCheck"] = GetConfig("ownerCheck", true);

            Config["combatUnblockOnDeath"] = GetConfig("combatUnblockOnDeath", true);
            Config["combatUnblockOnWakeup"] = GetConfig("combatUnblockOnWakeup", false);
            Config["combatUnblockOnRespawn"] = GetConfig("combatUnblockOnRespawn", true);

            Config["useZoneManager"] = GetConfig("useZoneManager", false);
            Config["zoneEnter"] = GetConfig("zoneEnter", true);
            Config["zoneLeave"] = GetConfig("zoneLeave", false);

            Config["raidDamageTypes"] = GetConfig("raidDamageTypes", GetDefaultRaidDamageTypes());
            Config["combatDamageTypes"] = GetConfig("combatDamageTypes", GetDefaultCombatDamageTypes());

            Config["sendUINotification"] = GetConfig("sendUINotification", true);
            Config["sendChatNotification"] = GetConfig("sendChatNotification", true);

            Config["cacheMinutes"] = GetConfig("cacheMinutes", 1f);
            // END NEW CONFIGURATION OPTIONS

            PrintToConsole("Upgrading configuration file");
            SaveConfig();
        }

        void OnServerInitialized()
        {
            NoEscape.plugin = this;

            permission.RegisterPermission("noescape.disable", this);

            foreach (string command in blockTypes)
            {
                permission.RegisterPermission("noescape.raid." + command + "block", this);
                permission.RegisterPermission("noescape.combat." + command + "block", this);
            }

            CheckConfig();

            blockedPrefabs = GetConfig("blockingPrefabs", blockedPrefabs);
            exceptionPrefabs = GetConfig("exceptionPrefabs", exceptionPrefabs);

            raidBlock = GetConfig("raidBlock", true);
            raidDuration = GetConfig("raidDuration", 50f);
            raidDistance = GetConfig("raidDistance", 100f);
            blockUnowned = GetConfig("blockUnowned", false);

            blockOnDamage = GetConfig("blockOnDamage", true);
            blockOnDestroy = GetConfig("blockOnDestroy", false);

            combatBlock = GetConfig("combatBlock", false);
            combatDuration = GetConfig("combatDuration", 180f);
            combatOnHitPlayer = GetConfig("combatOnHitPlayer", true);
            combatOnTakeDamage = GetConfig("combatOnTakeDamage", true);

            friendShare = GetConfig("friendShare", false);
            friendCheck = GetConfig("friendCheck", false);
            clanShare = GetConfig("clanShare", false);
            clanCheck = GetConfig("clanCheck", false);
            ownerCheck = GetConfig("ownerCheck", true);
            blockAll = GetConfig("blockAll", false);
            raiderBlock = GetConfig("raiderBlock", false);
            ownerBlock = GetConfig("ownerBlock", true);
            cupboardShare = GetConfig("cupboardShare", false);
            raiderClanShare = GetConfig("raiderClanShare", false);
            raiderFriendShare = GetConfig("raiderFriendShare", false);
            raidDamageTypes = GetConfig("raidDamageTypes", GetDefaultRaidDamageTypes());
            combatDamageTypes = GetConfig("combatDamageTypes", GetDefaultCombatDamageTypes());
            raidUnblockOnDeath = GetConfig("raidUnblockOnDeath", true);
            raidUnblockOnWakeup = GetConfig("raidUnblockOnWakeup", false);
            raidUnblockOnRespawn = GetConfig("raidUnblockOnRespawn", true);
            combatUnblockOnDeath = GetConfig("combatUnblockOnDeath", true);
            combatUnblockOnWakeup = GetConfig("combatUnblockOnWakeup", false);
            combatUnblockOnRespawn = GetConfig("combatUnblockOnRespawn", true);
            cacheTimer = GetConfig("cacheMinutes", 1f);

            useZoneManager = GetConfig("useZoneManager", false);
            zoneEnter = GetConfig("zoneEnter", true);
            zoneLeave = GetConfig("zoneLeave", false);

            raidBlockNotify = GetConfig("raidBlockNotify", true);
            combatBlockNotify = GetConfig("combatBlockNotify", false);

            sendUINotification = GetConfig("sendUINotification", true);
            sendChatNotification = GetConfig("sendChatNotification", true);

            if (clanShare || clanCheck || raiderClanShare)
            {
                if (!plugins.Exists("Clans"))
                {
                    clanShare = false;
                    clanCheck = false;
                    raiderClanShare = false;
                    PrintWarning("Clans not found! All clan options disabled. Cannot use clan options without this plugin. http://oxidemod.org/plugins/clans.2087/");
                }
            }

            if (friendShare || raiderFriendShare)
            {
                if (!plugins.Exists("Friends"))
                {
                    friendShare = false;
                    raiderFriendShare = false;
                    friendCheck = false;
                    PrintWarning("Friends not found! All friend options disabled. Cannot use friend options without this plugin. http://oxidemod.org/plugins/friends-api.686/");
                }
            }

            if (useZoneManager)
            {
                if (!plugins.Exists("ZoneManager"))
                {
                    useZoneManager = false;
                    PrintWarning("ZoneManager not found! All zone options disabled. Cannot use zone options without this plugin. http://oxidemod.org/plugins/zones-manager.739/");
                }
            }

            CleanHooks();
        }

        void CleanHooks()
        {
            if (!blockOnDestroy && !raidUnblockOnDeath && !combatUnblockOnDeath)
            {
                Unsubscribe("OnEntityDeath");
            }

            if (!raidUnblockOnWakeup && !combatUnblockOnWakeup)
            {
                Unsubscribe("OnPlayerSleepEnded");
            }

            if (!combatOnTakeDamage && !combatOnHitPlayer)
            {
                Unsubscribe("OnPlayerAttack");
            }

            if (!blockOnDamage)
            {
                Unsubscribe("OnEntityTakeDamage");
            }
        }

        #endregion

        #region Classes

        public class RaidZone
        {
            public string zoneid;
            public Vector3 position;
            public Timer timer;

            public RaidZone(string zoneid, Vector3 position)
            {
                this.zoneid = zoneid;
                this.position = position;
            }

            public float Distance(RaidZone zone)
            {
                return Vector3.Distance(position, zone.position);
            }

            public float Distance(Vector3 pos)
            {
                return Vector3.Distance(position, pos);
            }

            public RaidZone ResetTimer()
            {
                if (timer is Timer && !timer.Destroyed)
                    timer.Destroy();

                return this;
            }
        }

        public abstract class BlockBehavior : MonoBehaviour
        {
            protected BasePlayer player;
            public DateTime lastBlock = DateTime.MinValue;
            public DateTime lastNotification = DateTime.MinValue;
            internal DateTime lastUINotification = DateTime.MinValue;
            internal Timer timer;
            internal Action notifyCallback;

            internal abstract float Duration { get; }
            internal abstract CuiRectTransformComponent NotificationWindow { get; }
            internal abstract string notifyMessage { get; }
            internal string BlockName
            {
                get
                {
                    return GetType().Name;
                }
            }

            public bool Active
            {
                get
                {
                    if (lastBlock > DateTime.MinValue)
                    {
                        TimeSpan ts = DateTime.Now - lastBlock;
                        if (ts.TotalSeconds < Duration)
                        {
                            return true;
                        }
                    }

                    GameObject.Destroy(this);

                    return false;
                }
            }

            void Awake()
            {
                player = GetComponent<BasePlayer>();
            }

            void Destroy()
            {
                Stop();
            }

            void Update()
            {
                if (!plugin.sendUINotification) return;
                bool send = false;
                if (lastUINotification == DateTime.MinValue)
                {
                    lastUINotification = DateTime.Now;
                    send = true;
                }
                else
                {
                    TimeSpan ts = DateTime.Now - lastUINotification;
                    if (ts.TotalSeconds > 2)
                    {
                        send = true;
                    }
                    else
                    {
                        send = false;
                    }
                }

                if (player is BasePlayer && player.IsConnected)
                {
                    if (!Active)
                    {
                        CuiHelper.DestroyUi(player, "BlockMsg" + BlockName);
                    }

                    if (send && Active)
                    {
                        lastUINotification = DateTime.Now;
                        SendGUI();
                    }
                }
            }

            public void Stop()
            {
                if (notifyCallback is Action)
                    notifyCallback.Invoke();

                if (timer is Timer && !timer.Destroyed)
                    timer.Destroy();

                if (plugin.sendUINotification && player is BasePlayer && player.IsConnected)
                    CuiHelper.DestroyUi(player, "BlockMsg" + BlockName);

                GameObject.Destroy(this);
            }

            public void Notify(Action callback)
            {
                if (plugin.sendUINotification)
                    SendGUI();
                
                notifyCallback = callback;
                if (timer is Timer && !timer.Destroyed)
                    timer.Destroy();

                timer = plugin.timer.In(Duration, callback);
            }

            private string FormatTime(TimeSpan ts)
            {
                if (ts.Days > 0)
                    return string.Format("{0}D, {1}H", ts.Days, ts.Hours);

                if (ts.Hours > 0)
                    return string.Format("{0}H {1}M", ts.Hours, ts.Minutes);

                return string.Format("{0}M {1}S", ts.Minutes, ts.Seconds);
            }

            void SendGUI()
            {
                TimeSpan ts = lastBlock.AddSeconds(Duration) - DateTime.Now;

                string countDown = FormatTime(ts);
                CuiHelper.DestroyUi(player, "BlockMsg" + BlockName);
                var elements = new CuiElementContainer();
                var BlockMsg = elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.95 0 0.02 0.67"
                    },
                    RectTransform =
                    {
                        AnchorMax = NotificationWindow.AnchorMax,
                        AnchorMin = NotificationWindow.AnchorMin
                    }
                }, "Hud", "BlockMsg" + BlockName);
                elements.Add(new CuiElement
                {
                    Parent = BlockMsg,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Sprite = "assets/icons/explosion.png",
                            Color = "0.95 0 0.02 0.67"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0.13 1"
                        }
                    }
                });
                elements.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.15 0",
                        AnchorMax = "0.82 1"
                    },
                    Text =
                    {
                        Text = notifyMessage,
                        FontSize = 11,
                        Align = TextAnchor.MiddleLeft,
                    }
                }, BlockMsg);
                elements.Add(new CuiElement
                {
                    Name = "TimerPanel",
                    Parent = BlockMsg,
                    Components =
                        {
                        new CuiImageComponent
                        {
                            Color = "0 0 0 0.64",
                            ImageType = UnityEngine.UI.Image.Type.Filled
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.73 0",
                            AnchorMax = "1 1"
                        }
                    }
                });
                elements.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                    Text =
                    {
                        Text = countDown,
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                    }
                }, "TimerPanel");
                CuiHelper.AddUi(player, elements);
            }

            
        }

        public class CombatBlock : BlockBehavior
        {
            internal override float Duration
            {
                get
                {
                    return combatDuration;
                }
            }

            internal override string notifyMessage
            {
                get { return NoEscape.GetMsg("Combat Block UI Message", player); }
            }

            private CuiRectTransformComponent _notificationWindow = null;
            internal override CuiRectTransformComponent NotificationWindow
            {
                get
                {
                    if (_notificationWindow != null) { return _notificationWindow; }
                    return _notificationWindow = new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.87 0.35",
                        AnchorMax = "0.99 0.38"
                    };
                }
            }
        }

        public class RaidBlock : BlockBehavior
        {
            internal override float Duration
            {
                get
                {
                    return raidDuration;
                }
            }

            internal override string notifyMessage
            {
                get { return NoEscape.GetMsg("Raid Block UI Message", player); }
            }

            private CuiRectTransformComponent _notificationWindow = null;
            internal override CuiRectTransformComponent NotificationWindow
            {
                get
                {
                    if (_notificationWindow != null) { return _notificationWindow; }
                    return _notificationWindow = new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.87 0.39",
                        AnchorMax = "0.99 0.42"
                    };
                }
            }
        }
        #endregion

        #region Oxide Hooks

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!blockOnDamage) return;
            if (hitInfo == null || hitInfo.WeaponPrefab == null || hitInfo.Initiator == null || !IsEntityBlocked(entity) || hitInfo.Initiator.transform == null || hitInfo.Initiator.transform.position == null)
                return;
            if (!IsRaidDamage(hitInfo.damageTypes.GetMajorityDamageType())) return;

            StructureAttack(entity, hitInfo.Initiator, hitInfo.WeaponPrefab.ShortPrefabName, hitInfo.HitPositionWorld);
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            if (!combatBlock || !(hitInfo.HitEntity is BasePlayer)) return;
            if (!IsCombatDamage(hitInfo.damageTypes.GetMajorityDamageType())) return;

            BasePlayer target = hitInfo.HitEntity as BasePlayer;

            if (combatOnTakeDamage)
                StartCombatBlocking(target);

            if (combatOnHitPlayer)
                StartCombatBlocking(attacker);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (blockOnDestroy)
            {
                if (hitInfo == null || hitInfo.WeaponPrefab == null || hitInfo.Initiator == null || !IsRaidDamage(hitInfo.damageTypes.GetMajorityDamageType()) || !IsEntityBlocked(entity))
                    return;

                StructureAttack(entity, hitInfo.Initiator, hitInfo.WeaponPrefab.ShortPrefabName, hitInfo.HitPositionWorld);
            }

            if (entity.ToPlayer() == null) return;
            var player = entity.ToPlayer();
            RaidBlock raidBlocker;
            if (raidBlock && raidUnblockOnDeath && TryGetBlocker<RaidBlock>(player, out raidBlocker))
            {
                timer.In(0.3f, delegate()
                {
                    raidBlocker.Stop();
                });
            }

            CombatBlock combatBlocker;
            if (combatBlock && combatUnblockOnDeath && TryGetBlocker<CombatBlock>(player, out combatBlocker))
            {
                timer.In(0.3f, delegate()
                {
                    combatBlocker.Stop();
                });
            }
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            RaidBlock raidBlocker;
            if (raidBlock && raidUnblockOnWakeup && TryGetBlocker<RaidBlock>(player, out raidBlocker))
            {
                timer.In(0.3f, delegate()
                {
                    raidBlocker.Stop();
                });
            }

            CombatBlock combatBlocker;
            if (combatBlock && combatUnblockOnWakeup && TryGetBlocker<CombatBlock>(player, out combatBlocker))
            {
                timer.In(0.3f, delegate()
                {
                    combatBlocker.Stop();
                });
            }
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (raidBlock && raidUnblockOnRespawn && IsRaidBlocked(player))
            {
                timer.In(0.3f, delegate()
                {
                    StopBlocking(player);
                });
            }

            if (combatBlock && combatUnblockOnRespawn && IsCombatBlocked(player))
            {
                timer.In(0.3f, delegate()
                {
                    StopBlocking(player);
                });
            }
        }

        #endregion

        #region Block Handling

        void StructureAttack(BaseEntity targetEntity, BaseEntity sourceEntity, string weapon, Vector3 hitPosition)
        {
            BasePlayer source = null;

            if (sourceEntity.ToPlayer() is BasePlayer)
                source = sourceEntity.ToPlayer();
            else
            {
                ulong ownerID = sourceEntity.OwnerID;
                if (ownerID.IsSteamId())
                    source = BasePlayer.FindByID(ownerID);
                else 
                    return;
            }

            if (source == null)
                return;

            List<string> sourceMembers = null;

            if (targetEntity.OwnerID.IsSteamId() || (blockUnowned && !targetEntity.OwnerID.IsSteamId()))
            {
                if (clanCheck || friendCheck)
                    sourceMembers = getFriends(source.UserIDString);

                if (blockAll)
                {
                    BlockAll(source, targetEntity.transform.position, sourceMembers);
                }
                else
                {
                    if (ownerBlock)
                        OwnerBlock(source, sourceEntity, targetEntity.OwnerID, targetEntity.transform.position, sourceMembers);

                    if (raiderBlock)
                        RaiderBlock(source, targetEntity.OwnerID, targetEntity.transform.position, sourceMembers);
                }
            }
        }

        void BlockAll(BasePlayer source, Vector3 position, List<string> sourceMembers = null)
        {
            StartRaidBlocking(source, position);
            var nearbyTargets = Pool.GetList<BasePlayer>();
            Vis.Entities<BasePlayer>(position, raidDistance, nearbyTargets, blockLayer);
            if (nearbyTargets.Count > 0)
            {
                RaidBlock blocker;
                foreach (BasePlayer nearbyTarget in nearbyTargets)
                {
                    if (TryGetBlocker<RaidBlock>(nearbyTarget, out blocker) && blocker.Active)
                    {
                        StartRaidBlocking(nearbyTarget, position);
                    }
                    else if (ShouldBlockEscape(nearbyTarget.userID, source.userID, sourceMembers))
                    {
                        StartRaidBlocking(nearbyTarget, position);
                    }
                }
            }

            Pool.FreeList<BasePlayer>(ref nearbyTargets);
        }

        void OwnerBlock(BasePlayer source, BaseEntity sourceEntity, ulong target, Vector3 position, List<string> sourceMembers = null)
        {
            if(!ShouldBlockEscape(target, source.userID, sourceMembers))
                return;
            
            var targetMembers = new List<string>();

            if (clanShare || friendShare)
                targetMembers = getFriends(target.ToString());

            var nearbyTargets = Pool.GetList<BasePlayer>();
            Vis.Entities<BasePlayer>(position, raidDistance, nearbyTargets, blockLayer);
            if (cupboardShare)
                sourceMembers = CupboardShare(target.ToString(), position, sourceEntity, sourceMembers);

            if (nearbyTargets.Count > 0)
                foreach (BasePlayer nearbyTarget in nearbyTargets)
                    if (nearbyTarget.userID == target || ( targetMembers != null && targetMembers.Contains(nearbyTarget.UserIDString)))
                        StartRaidBlocking(nearbyTarget, position);

            Pool.FreeList<BasePlayer>(ref nearbyTargets);
        }

        List<string> CupboardShare(string owner, Vector3 position, BaseEntity sourceEntity, List<string> sourceMembers = null)
        {
            var nearbyCupboards = Pool.GetList<BuildingPrivlidge>();
            Vis.Entities<BuildingPrivlidge>(position, raidDistance, nearbyCupboards, cupboardMask);
            if (sourceMembers == null)
                sourceMembers = new List<string>();

            List<string> cupboardMembers = new List<string>();

            foreach (var cup in nearbyCupboards)
            {
                if (cup.CheckEntity(sourceEntity))
                {
                    bool ownerOrFriend = false;

                    if (owner == cup.OwnerID.ToString())
                        ownerOrFriend = true;

                    foreach (var member in sourceMembers)
                    {
                        if (member == cup.OwnerID.ToString())
                            ownerOrFriend = true;
                    }

                    if (ownerOrFriend)
                        foreach (var proto in cup.authorizedPlayers)
                            if (!sourceMembers.Contains(proto.userid.ToString()))
                                cupboardMembers.Add(proto.userid.ToString());
                }
            }

            sourceMembers.AddRange(cupboardMembers);
            Pool.FreeList<BuildingPrivlidge>(ref nearbyCupboards);

            return sourceMembers;
        }

        void RaiderBlock(BasePlayer source, ulong target, Vector3 position, List<string> sourceMembers = null)
        {
            if(!ShouldBlockEscape(target, source.userID, sourceMembers))
                return;

            var targetMembers = new List<string>();

            if ((raiderClanShare || raiderFriendShare) && sourceMembers == null)
                sourceMembers = getFriends(source.UserIDString);

            var nearbyTargets = Pool.GetList<BasePlayer>();
            Vis.Entities<BasePlayer>(position, raidDistance, nearbyTargets, blockLayer);
            if (nearbyTargets.Count > 0)
                foreach (BasePlayer nearbyTarget in nearbyTargets)
                    if (nearbyTarget == source || (sourceMembers != null && sourceMembers.Contains(nearbyTarget.UserIDString)))
                        StartRaidBlocking(nearbyTarget, position);

            Pool.FreeList<BasePlayer>(ref nearbyTargets);
        }

        #endregion

        #region API

        bool IsBlocked(string target)
        {
            var player = BasePlayer.Find(target);
            if (player is BasePlayer)
            {
                return IsBlocked(player);
            }

            return false;
        }

        bool IsBlocked(BasePlayer target)
        {
            if (IsBlocked<RaidBlock>(target) || IsBlocked<CombatBlock>(target))
                return true;

            return false;
        }

        public bool IsBlocked<T>(BasePlayer target) where T : BlockBehavior
        {
            T behavior;
            if (TryGetBlocker<T>(target, out behavior) && behavior.Active)
                return true;

            return false;
        }

        bool IsRaidBlocked(BasePlayer target)
        {
            return IsBlocked<RaidBlock>(target);
        }

        bool IsCombatBlocked(BasePlayer target)
        {
            return IsBlocked<CombatBlock>(target);
        }

        bool IsEscapeBlocked(string target)
        {
            var player = BasePlayer.Find(target);
            if (player is BasePlayer)
            {
                return IsBlocked(player);
            }

            return false;
        }

        bool IsRaidBlocked(string target)
        {
            var player = BasePlayer.Find(target);
            if (player is BasePlayer)
            {
                return IsBlocked<RaidBlock>(player);
            }

            return false;
        }

        bool IsCombatBlocked(string target)
        {
            var player = BasePlayer.Find(target);
            if (player is BasePlayer)
            {
                return IsBlocked<CombatBlock>(player);
            }

            return false;
        }

        bool ShouldBlockEscape(ulong target, ulong source, List<string> sourceMembers = null)
        {
            if (target == source)
            {
                if ((ownerBlock || raiderBlock) && (!ownerCheck))
                    return true;

                return false;
            }

            if (sourceMembers is List<string> && sourceMembers.Count > 0 && sourceMembers.Contains(target.ToString()))
                return false;

            return true;
        }

        //[ChatCommand("bblocked")]
        //void cmdBBlocked(BasePlayer player, string command, string[] args)
        //{
        //    StartCombatBlocking(player);
        //    StartRaidBlocking(player);
        //}

        void StartRaidBlocking(BasePlayer target, bool createZone = true)
        {
            StartRaidBlocking(target, target.transform.position, createZone);
        }

        void StartRaidBlocking(BasePlayer target, Vector3 position, bool createZone = true)
        {
            if(HasPerm(target.UserIDString, "disable")) {
                return;
            }
            if (position == null) {
                position = target.transform.position;
            }
            if (target.gameObject == null) return;
            var raidBlocker = target.gameObject.GetComponent<RaidBlock>();
            if(raidBlocker == null) {
                raidBlocker = target.gameObject.AddComponent<RaidBlock>();
            }

            raidBlocker.lastBlock = DateTime.Now;

            if (raidBlockNotify)
                SendBlockMessage(target, raidBlocker, "Raid Block Notifier", "Raid Block Complete");

            if (useZoneManager && createZone && (zoneEnter || zoneLeave))
                CreateRaidZone(position);
        }

        void StartCombatBlocking(BasePlayer target)
        {
            if(HasPerm(target.UserIDString, "disable")) {
                return;
            }
            if (target.gameObject == null) return;
            var combatBlocker = target.gameObject.GetComponent<CombatBlock>();
            if (combatBlocker == null)
            {
                combatBlocker = target.gameObject.AddComponent<CombatBlock>();
            }

            combatBlocker.lastBlock = DateTime.Now;

            if (combatBlockNotify)
                SendBlockMessage(target, combatBlocker, "Combat Block Notifier", "Combat Block Complete");
        }

        void StopBlocking(BasePlayer target)
        {
            if (IsRaidBlocked(target))
                StopBlocking<RaidBlock>(target);
            if (IsCombatBlocked(target))
                StopBlocking<CombatBlock>(target);
        }

        public void StopBlocking<T>(BasePlayer target) where T : BlockBehavior
        {
            if (target.gameObject == null) return;
            var block = target.gameObject.GetComponent<T>();
            if (block is BlockBehavior)
                block.Stop();
        }

        void ClearRaidBlockingS(string target)
        {
            StopRaidBlocking(target);
        }

        void StopRaidBlocking(string target)
        {
            var player = BasePlayer.Find(target);
            if (player is BasePlayer && IsRaidBlocked(player))
                StopBlocking<RaidBlock>(player);
        }

        void StopCombatBlocking(string target)
        {
            var player = BasePlayer.Find(target);
            if (player is BasePlayer && IsRaidBlocked(player))
                StopBlocking<CombatBlock>(player);
        }

        void ClearCombatBlocking(string target)
        {
            StopCombatBlocking(target);
        }

        #endregion

        #region Zone Handling
        void EraseZone(string zoneid)
        {
            ZoneManager.CallHook("EraseZone", zoneid);
            zones.Remove(zoneid);
        }

        void ResetZoneTimer(RaidZone zone)
        {
            zone.ResetTimer().timer = timer.In(raidDuration, delegate()
            {
                EraseZone(zone.zoneid);
            });
        }

        void CreateRaidZone(Vector3 position)
        {
            var zoneid = position.ToString();

            RaidZone zone;
            if (zones.TryGetValue(zoneid, out zone))
            {
                ResetZoneTimer(zone);
                return;
            }
            else
            {
                foreach (var nearbyZone in zones)
                {
                    if (nearbyZone.Value.Distance(position) < (raidDistance / 2))
                    {
                        ResetZoneTimer(nearbyZone.Value);
                        return;
                    }
                }
            }

            ZoneManager.CallHook("CreateOrUpdateZone", zoneid, new string[] {
                "radius",
                raidDistance.ToString()
            }, position);

            zones.Add(zoneid, zone = new RaidZone(zoneid, position));

            ResetZoneTimer(zone);
        }

        [HookMethod("OnEnterZone")]
        void OnEnterZone(string zoneid, BasePlayer player)
        {
            if (!zoneEnter) return;
            if (!zones.ContainsKey(zoneid)) return;

            StartRaidBlocking(player, player.transform.position, false);
        }

        [HookMethod("OnExitZone")]
        void OnExitZone(string zoneid, BasePlayer player)
        {
            if (!zoneLeave) return;
            if (!zones.ContainsKey(zoneid)) return;

            if (IsRaidBlocked(player))
            {
                StopBlocking<RaidBlock>(player);
            }
        }
        #endregion

        #region Friend/Clan Integration

        public List<string> getFriends(string player)
        {
            var players = new List<string>();
            if (player == null)
                return players;

            if (friendShare || raiderFriendShare || friendCheck)
            {
                var friendList = getFriendList(player);
                if (friendList != null)
                    players.AddRange(friendList);
            }

            if (clanShare || raiderClanShare || clanCheck)
            {
                var members = getClanMembers(player);
                if (members != null)
                    players.AddRange(members);
            }
            return players;
        }

        public List<string> getFriendList(string player)
        {
            object friends_obj = null;
            DateTime lastFriendCheckPlayer;
            var players = new List<string>();

            if (lastFriendCheck.TryGetValue(player, out lastFriendCheckPlayer))
            {
                if ((DateTime.Now - lastFriendCheckPlayer).TotalMinutes <= cacheTimer && friendCache.TryGetValue(player, out players)) 
                {
                    return players;
                }
                else
                {
                    friends_obj = Friends?.CallHook("IsFriendOfS", player);
                    lastFriendCheck[player] = DateTime.Now;
                }
            }
            else
            {
                friends_obj = Friends?.CallHook("IsFriendOfS", player);
                lastFriendCheck.Add(player, DateTime.Now);
            }

            if (friends_obj == null)
                return players;

            string[] friends = friends_obj as string[];
            
            foreach (string fid in friends)
                players.Add(fid);

            if (friendCache.ContainsKey(player))
                friendCache[player] = players;
            else
                friendCache.Add(player, players);

            return players;
        }

        public List<string> getClanMembers(string player)
        {
            string tag = null;
            DateTime lastClanCheckPlayer;
            string lastClanCached;
            if (lastClanCheck.TryGetValue(player, out lastClanCheckPlayer) && clanCache.TryGetValue(player, out lastClanCached))
            {
                if ((DateTime.Now - lastClanCheckPlayer).TotalMinutes <= cacheTimer)
                    tag = lastClanCached;
                else
                {
                    tag = Clans.Call<string>("GetClanOf", player);
                    clanCache[player] = tag;
                    lastClanCheck[player] = DateTime.Now;
                }
            }
            else
            {
                tag = Clans.Call<string>("GetClanOf", player);
                if (lastClanCheck.ContainsKey(player))
                    lastClanCheck.Remove(player);

                if (clanCache.ContainsKey(player))
                    clanCache.Remove(player);

                clanCache.Add(player, tag);
                lastClanCheck.Add(player, DateTime.Now);
            }

            if (tag == null)
                return null;

            List<string> lastMemberCache;
            if (memberCache.TryGetValue(tag, out lastMemberCache))
                return lastMemberCache;

            var clan = GetClan(tag);

            if (clan == null)
                return null;

            return CacheClan(clan);
        }

        JObject GetClan(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }
            return Clans.Call<JObject>("GetClan", tag);
        }

        List<string> CacheClan(JObject clan)
        {
            string tag = clan["tag"].ToString();
            List<string> players = new List<string>();
            foreach (string memberid in clan["members"])
            {
                if (clanCache.ContainsKey(memberid))
                    clanCache[memberid] = tag;
                else
                    clanCache.Add(memberid, tag);

                players.Add(memberid);
            }

            if (memberCache.ContainsKey(tag))
                memberCache[tag] = players;
            else
                memberCache.Add(tag, players);

            if (lastCheck.ContainsKey(tag))
                lastCheck[tag] = DateTime.Now;
            else
                lastCheck.Add(tag, DateTime.Now);

            return players;
        }

        [HookMethod("OnClanCreate")]
        void OnClanCreate(string tag)
        {
            var clan = GetClan(tag);
            if (clan != null)
            {
                CacheClan(clan);
            }
            else
            {
                PrintWarning("Unable to find clan after creation: " + tag);
            }
        }

        [HookMethod("OnClanUpdate")]
        void OnClanUpdate(string tag)
        {
            var clan = GetClan(tag);
            if (clan != null)
            {
                CacheClan(clan);
            }
            else
            {
                PrintWarning("Unable to find clan after update: " + tag);
            }
        }

        [HookMethod("OnClanDestroy")]
        void OnClanDestroy(string tag)
        {
            if (lastCheck.ContainsKey(tag))
            {
                lastCheck.Remove(tag);
            }

            if (memberCache.ContainsKey(tag))
            {
                memberCache.Remove(tag);
            }
        }

        #endregion

        #region Permission Checking & External API Handling

        bool HasPerm(string userid, string perm)
        {
            return permission.UserHasPermission(userid, "noescape." + perm);
        }

        bool CanRaidCommand(BasePlayer player, string command)
        {
            return raidBlock && HasPerm(player.UserIDString, "raid." + command + "block") && IsRaidBlocked(player);
        }

        bool CanRaidCommand(string playerID, string command)
        {
            return raidBlock && HasPerm(playerID, "raid." + command + "block") && IsRaidBlocked(playerID);
        }

        bool CanCombatCommand(BasePlayer player, string command)
        {
            return combatBlock && HasPerm(player.UserIDString, "combat." + command + "block") && IsCombatBlocked(player);
        }

        bool CanCombatCommand(string playerID, string command)
        {
            return combatBlock && HasPerm(playerID, "combat." + command + "block") && IsCombatBlocked(playerID);
        }

        object CanDo(string command, BasePlayer player)
        {
            if (CanRaidCommand(player, command))
                return GetMessage<RaidBlock>(player, "Raid Blocked Message", raidDuration);
            else if (CanCombatCommand(player, command))
                return GetMessage<CombatBlock>(player, "Combat Blocked Message", combatDuration);

            return null;
        }

        object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            var result = CanDo("repair", player);
            if (result is string)
            {
                if (entity.health < entity.MaxHealth())
                {
                    return null;
                }
                SendReply(player, result.ToString());
                return true;
            }

            return null;
        }

        object OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            var result = CanDo("upgrade", player);
            if (result is string)
            {
                SendReply(player, result.ToString());
                return true;
            }

            return null;
        }

        object canRedeemKit(BasePlayer player)
        {
            return CanDo("kit", player);
        }

        object CanUseVending(VendingMachine machine, BasePlayer player)
        {
            var result = CanDo("vend", player);
            if (result is string)
            {
                SendReply(player, result.ToString());
                return true;
            }

            return null;
        }

        object CanBuild(Planner plan, Construction prefab)
        {
            var player = plan.GetOwnerPlayer();
            var result = CanDo("build", player);
            if (result is string)
            {
                if (isEntityException(prefab.fullName))
                {
                    return null;
                }

                SendReply(player, result.ToString());
                return true;
            }

            return null;
        }

        object CanAssignBed(SleepingBag bag, BasePlayer player, ulong targetPlayerId)
        {
            var result = CanDo("assignbed", player);
            if (result is string)
            {
                SendReply(player, result.ToString());
                return true;
            }

            return null;
        }

        object CanBank(BasePlayer player)
        {
            return CanDo("bank", player);
        }

        object CanTrade(BasePlayer player)
        {
            return CanDo("trade", player);
        }

        object canRemove(BasePlayer player)
        {
            return CanDo("remove", player);
        }

        object canShop(BasePlayer player)
        {
            return CanDo("shop", player);
        }

        object CanShop(BasePlayer player)
        {
            return CanDo("shop", player);
        }

        object CanTeleport(BasePlayer player)
        {
            return CanDo("tp", player);
        }

        object canTeleport(BasePlayer player) // ALIAS FOR MagicTeleportation
        {
            return CanTeleport(player);
        }

        object CanRecycleCommand(BasePlayer player)
        {
            return CanDo("recycle", player);
        }

        object CanAutoGrade(BasePlayer player, int grade, BuildingBlock buildingBlock, Planner planner)
        {
            if (CanRaidCommand(player, "bgrade") || CanCombatCommand(player, "bgrade"))
                return -1;
            return null;
        }

        #endregion

        #region Messages

        void SendBlockMessage(BasePlayer target, BlockBehavior blocker, string langMessage, string completeMessage)
        {
            var send = false;
            if (blocker.lastNotification != DateTime.MinValue)
            {
                TimeSpan diff = DateTime.Now - blocker.lastNotification;
                if (diff.TotalSeconds >= (blocker.Duration/2))
                    send = true;
            }
            else
                send = true;

            if (send)
            {
                if (sendChatNotification)
                    SendReply(target, GetPrefix(target.UserIDString) + GetMsg(langMessage, target.UserIDString).Replace("{time}", GetCooldownTime(blocker.Duration, target.UserIDString)));

                blocker.lastNotification = DateTime.Now;
            }

            blocker.Notify(delegate()
            {
                blocker.notifyCallback = null;
                if (target.IsConnected && sendChatNotification)
                    SendReply(target, GetPrefix(target.UserIDString) + GetMsg(completeMessage, target.UserIDString));
            });
        }

        string GetCooldownTime(float f, string userID)
        {
            if (f > 60)
                return Math.Round(f / 60, 1) + " " + GetMsg("Unit Minutes", userID);

            return f + " " + GetMsg("Unit Seconds", userID);
        }

        public string GetMessage(BasePlayer player)
        {
            if (IsRaidBlocked(player))
                return GetMessage<RaidBlock>(player, "Raid Blocked Message", raidDuration);
            else if (IsCombatBlocked(player))
                return GetMessage<CombatBlock>(player, "Combat Blocked Message", combatDuration);

            return null;
        }

        public string GetPrefix(string player)
        {
            string prefix = GetMsg("Prefix", player);
            if (!string.IsNullOrEmpty(prefix))
            {
                return prefix + ": ";
            }

            return string.Empty;
        }

        public string GetMessage<T>(BasePlayer player, string blockMsg, float duration) where T : BlockBehavior
        {
            T blocker;
            if (duration > 0 && TryGetBlocker<T>(player, out blocker))
            {
                var ts = DateTime.Now - blocker.lastBlock;
                var unblocked = Math.Round((duration / 60) - Convert.ToSingle(ts.TotalMinutes), 2);

                if (ts.TotalMinutes <= duration)
                {
                    if (unblocked < 1)
                    {
                        var timelefts = Math.Round(Convert.ToDouble(duration) - ts.TotalSeconds);
                        return GetPrefix(player.UserIDString) + GetMsg(blockMsg, player).Replace("{time}", timelefts.ToString() + " " + GetMsg("Unit Seconds", player));
                    }
                    else
                        return GetPrefix(player.UserIDString) + GetMsg(blockMsg, player).Replace("{time}", unblocked.ToString() + " " + GetMsg("Unit Minutes", player));
                }
            }

            return null;
        }

        #endregion

        #region Utility Methods

        bool TryGetBlocker<T>(BasePlayer player, out T blocker) where T : BlockBehavior
        {
            blocker = null;
            if (player.gameObject == null) return false;
            if ((blocker = player.gameObject.GetComponent<T>()) != null)
                return true;

            return false;
        }

        public bool isEntityException(string prefabName) {
            var result = false;

            foreach (string p in exceptionPrefabs)
            {
                if (prefabName.IndexOf(p) != -1)
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        public bool IsEntityBlocked(BaseCombatEntity entity)
        {
            if (entity is BuildingBlock)
            {
                if (((BuildingBlock)entity).grade == BuildingGrade.Enum.Twigs)
                    return false;

                return true;
            }

            var prefabName = entity.ShortPrefabName;
            var result = false;
            if (prefabBlockCache.TryGetValue(prefabName, out result))
                return result;

            result = false;

            foreach (string p in blockedPrefabs)
            {
                if (prefabName.IndexOf(p) != -1)
                {
                    result = true;
                    break;
                }
            }

            
            prefabBlockCache.Add(prefabName, result);
            return result;
        }

        bool IsRaidDamage(DamageType dt)
        {
            return raidDamageTypes.Contains(dt.ToString());
        }

        bool IsCombatDamage(DamageType dt)
        {
            return combatDamageTypes.Contains(dt.ToString());
        }

        T GetConfig<T>(string key, T defaultValue)
        {
            try
            {
                var val = Config[key];
                if (val == null)
                    return defaultValue;
                if (val is List<object>)
                {
                    var t = typeof(T).GetGenericArguments()[0];
                    if (t == typeof(String))
                    {
                        var cval = new List<string>();
                        foreach (var v in val as List<object>)
                            cval.Add((string)v);
                        val = cval;
                    }
                    else if (t == typeof(int))
                    {
                        var cval = new List<int>();
                        foreach (var v in val as List<object>)
                            cval.Add(Convert.ToInt32(v));
                        val = cval;
                    }
                }
                else if (val is Dictionary<string, object>)
                {
                    var t = typeof(T).GetGenericArguments()[1];
                    if (t == typeof(int))
                    {
                        var cval = new Dictionary<string, int>();
                        foreach (var v in val as Dictionary<string, object>)
                            cval.Add(Convert.ToString(v.Key), Convert.ToInt32(v.Value));
                        val = cval;
                    }
                }
                return (T)Convert.ChangeType(val, typeof(T));
            }
            catch (Exception ex)
            {
                PrintWarning("Invalid config value: " + key + " (" + ex.Message + ")");
                return defaultValue;
            }
        }

        static string GetMsg(string key, object user = null)
        {
            if (user is BasePlayer)
            {
                user = ((BasePlayer)user).UserIDString;
            }
            return plugin.lang.GetMessage(key, plugin, user == null ? null : user.ToString());
        }

        #endregion
    }
}