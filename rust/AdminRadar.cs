using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("AdminRadar", "nivex", "4.0.3", ResourceId = 978)]
    [Description("ESP tool for Admins and Developers.")]
    public class AdminRadar: RustPlugin
    {
        readonly string permName = "adminradar.allowed";
        static AdminRadar ins;
        DynamicConfigFile dataFile;
        Dictionary<string, List<string>> filters;
        List<string> uiHidden;
        static StoredData storedData = new StoredData();
        static bool init = false; // make sure the server is initialized otherwise OnEntitySpawned can throw errors
        static Dictionary<string, string> guiInfo = new Dictionary<string, string>();
        static List<string> tags = new List<string>() { "ore", "cluster", "1", "2", "3", "4", "5", "6", "_", ".", "-", "deployed", "wooden", "large", "pile", "prefab", "collectable", "loot", "small" }; // strip these from names to reduce the size of the text and make it more readable
        static Dictionary<ulong, int> drawnObjects = new Dictionary<ulong, int>();
        Dictionary<ulong, Color> playersColor = new Dictionary<ulong, Color>();

        // to reduce server strain we'll cache all entities once. this process is extremely fast and efficient. any newly created or destroyed entities will be removed from it's respective cache. 
        // this excludes containers as content information needs to be current
        static Dictionary<Vector3, CachedInfo> cachedItems = new Dictionary<Vector3, CachedInfo>();
        static Dictionary<Vector3, CachedInfo> cachedBags = new Dictionary<Vector3, CachedInfo>();
        static Dictionary<Vector3, CachedInfo> cachedCollectibles = new Dictionary<Vector3, CachedInfo>();
        static Dictionary<Vector3, CachedInfo> cachedContainers = new Dictionary<Vector3, CachedInfo>();
        static Dictionary<PlayerCorpse, CachedInfo> cachedCorpses = new Dictionary<PlayerCorpse, CachedInfo>();
        static Dictionary<Vector3, CachedInfo> cachedOres = new Dictionary<Vector3, CachedInfo>();
        static Dictionary<Vector3, CachedInfo> cachedTC = new Dictionary<Vector3, CachedInfo>();
        static Dictionary<Vector3, CachedInfo> cachedTurrets = new Dictionary<Vector3, CachedInfo>();
        static List<BaseHelicopter> helisCache = new List<BaseHelicopter>();
        static Dictionary<ulong, SortedDictionary<long, Vector3>> trackers = new Dictionary<ulong, SortedDictionary<long, Vector3>>(); // player id, timestamp and player's position
        static Dictionary<ulong, Timer> trackerTimers = new Dictionary<ulong, Timer>();
        static List<BasePlayer> npcCache = new List<BasePlayer>();
        const float flickerDelay = 0.05f;

        bool IsRadar(BasePlayer player) => player.GetComponent<ESP>() != null;
        bool IsRadar(string id) => BasePlayer.activePlayerList.Find(x => x.UserIDString == id)?.GetComponent<ESP>() != null;
        static long TimeStamp() => (DateTime.Now.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks) / 10000000;

        class PlayerTracker : MonoBehaviour
        {
            BasePlayer player;
            ulong uid;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                uid = player.userID;
                InvokeRepeating("UpdateMovement", 0f, trackerUpdateInterval);
                UpdateMovement();
            }

            void UpdateMovement()
            {
                if (!player.IsConnected)
                {
                    GameObject.Destroy(this);
                    return;
                }

                if (!trackers.ContainsKey(uid))
                    trackers.Add(uid, new SortedDictionary<long, Vector3>());

                var currentStamp = TimeStamp();

                foreach (var stamp in trackers[uid].Keys.ToList()) // keep the dictionary from becoming enormous by removing entries which are too old
                    if (currentStamp - stamp > trackerAge)
                        trackers[uid].Remove(stamp);

                if (trackers[uid].Count > 1)
                {
                    var lastPos = trackers[uid].Values.ElementAt(trackers[uid].Count - 1); // get the last position the player was at

                    if (Vector3.Distance(lastPos, transform.position) <= 1f) // check the distance against the minimum requirement. without this the dictionary will accumulate thousands of entries
                        return;
                }

                trackers[uid][currentStamp] = transform.position;
                UpdateTimer();
            }

            void UpdateTimer()
            {
                if (trackerTimers.ContainsKey(uid))
                {
                    if (trackerTimers[uid] != null)
                    {
                        trackerTimers[uid].Reset();
                        return;
                    }
                    
                    trackerTimers.Remove(uid);
                }

                trackerTimers.Add(uid, ins.timer.Once(trackerAge, () =>
                {
                    if (trackers.ContainsKey(uid))
                        trackers.Remove(uid);

                    if (trackerTimers.ContainsKey(uid))
                        trackerTimers.Remove(uid);
                }));
            }

            void OnDestroy()
            {
                CancelInvoke("UpdateMovement");
                UpdateTimer();
                GameObject.Destroy(this);
            }
        }

        readonly string anchorMinTopLeft = "0 0.850";
        readonly string anchorMaxTopLeft = "0.148 1";
        readonly string anchorMinBottomRight = "0.667 0.020";
        readonly string anchorMaxBottomRight = "0.810 0.148";

        #region json 
        // TODO: Remove hardcoded json
        static string uiJson = @"[{
            ""name"": ""{guid}"",
            ""parent"": ""Hud"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Image"",
                ""color"": ""1 1 1 0""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""{anchorMin}"", 
                ""anchormax"": ""{anchorMax}""
              }
            ]
          },
          {
            ""name"": ""btnAll"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui all"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5"",
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.017 0.739"",
                ""anchormax"": ""0.331 0.957""
              }
            ]
          },
          {
            ""name"": ""lblAll"",
            ""parent"": ""btnAll"",
            ""components"": [
              {
                ""text"": ""All"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorAll}"",
                ""fontSize"": 10,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          },
          {
            ""name"": ""btnBags"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui bags"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.017 0.5"",
                ""anchormax"": ""0.331 0.717""
              }
            ]
          },
          {
            ""name"": ""lblBags"",
            ""parent"": ""btnBags"",
            ""components"": [
              {
                ""text"": ""Bags"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorBags}"",
                ""fontSize"": 10,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          },
          {
            ""name"": ""btnBox"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui box"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.017 0.261"",
                ""anchormax"": ""0.331 0.478""
              }
            ]
          },
          {
            ""name"": ""lblBox"",
            ""parent"": ""btnBox"",
            ""components"": [
              {
                ""text"": ""Boxes"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorBox}"",
                ""fontSize"": 10,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          },
          {
            ""name"": ""btnCollectables"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui col"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.017 0.022"",
                ""anchormax"": ""0.331 0.239""
              }
            ]
          },
          {
            ""name"": ""lblCollectables"",
            ""parent"": ""btnCollectables"",
            ""components"": [
              {
                ""text"": ""Collectibles"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorCol}"",
                ""fontSize"": 10,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          },
          {
            ""name"": ""btnDead"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui dead"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.343 0.739"",
                ""anchormax"": ""0.657 0.957""
              }
            ]
          },
          {
            ""name"": ""lblDead"",
            ""parent"": ""btnDead"",
            ""components"": [
              {
                ""text"": ""Dead"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorDead}"",
                ""fontSize"": 9,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          },
          {
            ""name"": ""btnLoot"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui loot"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.343 0.5"",
                ""anchormax"": ""0.657 0.717""
              }
            ]
          },
          {
            ""name"": ""lblLoot"",
            ""parent"": ""btnLoot"",
            ""components"": [
              {
                ""text"": ""Loot"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorLoot}"",
                ""fontSize"": 10,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          },
          {
            ""name"": ""btnNPC"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui npc"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.343 0.261"",
                ""anchormax"": ""0.657 0.478""
              }
            ]
          },
          {
            ""name"": ""lblNPC"",
            ""parent"": ""btnNPC"",
            ""components"": [
              {
                ""text"": ""NPC"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorNPC}"",
                ""fontSize"": 10,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          },
          {
            ""name"": ""btnOre"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui ore"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.343 0.022"",
                ""anchormax"": ""0.657 0.239""
              }
            ]
          },
          {
            ""name"": ""lblOre"",
            ""parent"": ""btnOre"",
            ""components"": [
              {
                ""text"": ""Ore"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorOre}"",
                ""fontSize"": 10,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          },
          {
            ""name"": ""btnSleepers"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui sleepers"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.669 0.739"",
                ""anchormax"": ""0.984 0.957""
              }
            ]
          },
          {
            ""name"": ""lblSleepers"",
            ""parent"": ""btnSleepers"",
            ""components"": [
              {
                ""text"": ""Sleepers"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorSleepers}"",
                ""fontSize"": 10,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          },
          {
            ""name"": ""btnStash"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui stash"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.669 0.5"",
                ""anchormax"": ""0.984 0.717""
              }
            ]
          },
          {
            ""name"": ""lblStash"",
            ""parent"": ""btnStash"",
            ""components"": [
              {
                ""text"": ""Stash"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorStash}"",
                ""fontSize"": 10,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          },
          {
            ""name"": ""btnTC"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui tc"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.669 0.261"",
                ""anchormax"": ""0.984 0.478""
              }
            ]
          },
          {
            ""name"": ""lblTC"",
            ""parent"": ""btnTC"",
            ""components"": [
              {
                ""text"": ""TC"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorTC}"",
                ""fontSize"": 10,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          },
          {
            ""name"": ""btnTurrets"",
            ""parent"": ""{guid}"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Button"",
                ""command"": ""espgui turrets"",
                ""close"": """",
                ""color"": ""0.29 0.49 0.69 0.5""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0.669 0.022"",
                ""anchormax"": ""0.984 0.239""
              }
            ]
          },
          {
            ""name"": ""lblTurrets"",
            ""parent"": ""btnTurrets"",
            ""components"": [
              {
                ""text"": ""Turrets"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{colorTurrets}"",
                ""fontSize"": 10,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          }
        ]";
        #endregion

        void Loaded()
        {
            permission.RegisterPermission(permName, this);
        }

        void OnServerInitialized()
        {
            ins = this;
            dataFile = Interface.Oxide.DataFileSystem.GetFile(Title);

            try
            {
                storedData = dataFile.ReadObject<StoredData>();
                filters = storedData.Filters;
                uiHidden = storedData.Hidden;
            }
            catch { }
            
            if (storedData == null)
                storedData = new StoredData();

            if (filters == null || filters.Count == 0)
                filters = new Dictionary<string, List<string>>();

            if (uiHidden == null)
                uiHidden = new List<string>();

            LoadVariables();

            if (!usePlayerTracker)
                Unsubscribe(nameof(OnPlayerSleepEnded));
            else
                foreach (var target in BasePlayer.activePlayerList)
                    Track(target);

            if (!drawBox && !drawText && !drawArrows)
            {
                Puts("Configuration does not have a chosen drawing method. Setting drawing method to text.");
                Config.Set("Drawing Methods", "Draw Text", true);
                Config.Save();
                drawText = true;
            }

            var tick = DateTime.Now;
            init = true;

            int cached = 0, total = 0;
            foreach (var e in BaseEntity.serverEntities)
            {
                if (AddToCache(e))
                    cached++;

                total++;
            }

            //Puts("Took {0}ms to cache {1}/{2} entities: {3} bags, {4} collectibles, {5} containers, {6} corpses, {7} ores, {8} tool cupboards, {9} turrets.", (DateTime.Now - tick).TotalMilliseconds, cached, total, cachedBags.Count, cachedCollectibles.Count, cachedContainers.Count, cachedCorpses.Count, cachedOres.Count, cachedTC.Count, cachedTurrets.Count);
            Puts("Took {0}ms to cache {1}/{2} entities", (DateTime.Now - tick).TotalMilliseconds, cached, total);
            SaveConfig();
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!trackAdmins && player.IsAdmin)
                return;

            Track(player);
        }

        void OnItemPickup(Item item, BasePlayer player)
        {
            if (cachedItems.Any(entry => entry.Value.Info.ToString() == item.uid.ToString()))
            {
                cachedItems.Remove(cachedItems.First(kvp => kvp.Value.Info.ToString() == item.uid.ToString()).Key);
            }
        }

        void Unload()
        {
            var espobjects = GameObject.FindObjectsOfType(typeof(ESP));

            if (espobjects != null)
                foreach (var gameObj in espobjects)
                    GameObject.Destroy(gameObj);

            var ptobjects = GameObject.FindObjectsOfType(typeof(PlayerTracker));

            if (ptobjects != null)
                foreach (var gameObj in ptobjects)
                    GameObject.Destroy(gameObj);

            var tobjects = GameObject.FindObjectsOfType(typeof(DropTracker));

            if (tobjects != null)
                foreach (var gameObj in tobjects)
                    GameObject.Destroy(gameObj);

            if (dataFile != null)
            {
                if (filters != null)
                    storedData.Filters = filters;

                if (uiHidden != null)
                    storedData.Hidden = uiHidden;

                dataFile.WriteObject(storedData);
            }

            foreach(var entry in trackerTimers.ToList())
                if (entry.Value != null && !entry.Value.Destroyed)
                    entry.Value.Destroy();

            playersColor.Clear();
            trackerTimers.Clear();
            trackers.Clear();
            filters?.Clear();
            uiHidden?.Clear();
            drawnObjects?.Clear();
            cachedOres?.Clear();
            cachedCorpses?.Clear();
            cachedContainers?.Clear();
            cachedBags?.Clear();
            cachedTC?.Clear();
            cachedTurrets?.Clear();
            cachedCollectibles?.Clear();
            helisCache?.Clear();
            tags?.Clear();
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info) => RemoveFromCache(entity);
        void OnEntityKill(BaseNetworkable entity) => RemoveFromCache(entity);
        void OnEntitySpawned(BaseNetworkable entity) => AddToCache(entity.GetComponent<BaseEntity>());

        class StoredData
        {
            public List<string> Visions = new List<string>();
            public List<string> OnlineBoxes = new List<string>();
            public Dictionary<string, List<string>> Filters = new Dictionary<string, List<string>>();
            public List<string> Hidden = new List<string>();
            public StoredData() { }
        }

        class CachedInfo
        {
            public string Name { get; set; }
            public object Info { get; set; }
            public double Size { get; set; }
            public CachedInfo() { }
        }

        class ESP : MonoBehaviour
        {
            BasePlayer player;
            BaseEntity source;
            public float maxDistance { get; set; }
            public float invokeTime { get; set; }

            public bool showAll { get; set; }
            public bool showBags { get; set; }
            public bool showBox { get; set; }
            public bool showCollectible { get; set; }
            public bool showDead { get; set; }
            public bool showLoot { get; set; }
            public bool showNPC { get; set; }
            public bool showOre { get; set; }
            public bool showSleepers { get; set; }
            public bool showStash { get; set; }
            public bool showTC { get; set; }
            public bool showTurrets { get; set; }

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                source = player;
            }

            void OnDestroy()
            {
                string gui;
                if (guiInfo.TryGetValue(player.UserIDString, out gui))
                {
                    CuiHelper.DestroyUi(player, gui);
                    guiInfo.Remove(player.UserIDString);
                }

                player.ChatMessage(ins.msg("Deactivated", player.UserIDString));
                GameObject.Destroy(this);
            }

            bool LatencyAccepted(DateTime tick)
            {
                if (latencyMs > 0)
                {
                    var ms = (DateTime.Now - tick).TotalMilliseconds;

                    if (ms > latencyMs)
                    {
                        player.ChatMessage(ins.msg("DoESP", player.UserIDString, ms, latencyMs));
                        return false;
                    }
                }

                return true;
            }

            void DoESP()
            {
                var tick = DateTime.Now;
                string error = "TRY";

                try
                {
                    error = "PLAYER";
                    if (!player.IsConnected)
                    {
                        GameObject.Destroy(this);
                        return;
                    }

                    error = "SOURCE";
                    source = player.IsSpectating() ? player.GetParentEntity() : player;

                    if (!(source is BasePlayer)) // compatibility for HideAndSeek plugin otherwise exceptions will be thrown
                        source = player;

                    if (player == source && (player.IsDead() || player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot)))
                        return;

                    drawnObjects[player.userID] = 0;

                    error = "HELI";
                    if (trackHelis && helisCache.Count > 0)
                    {
                        foreach (var heli in helisCache)
                        {
                            if (heli == null)
                                continue;

                            double currDistance = Math.Floor(Vector3.Distance(heli.transform.position, source.transform.position));
                            string heliHealth = heli.health > 1000 ? Math.Floor(heli.health).ToString("#,##0,K", CultureInfo.InvariantCulture) : Math.Floor(heli.health).ToString();
                            string info = showHeliRotorHealth ? string.Format("<color=red>{0}</color> (<color=yellow>{1}</color>/<color=yellow>{2}</color>)", heliHealth, Math.Floor(heli.weakspots[0].health), Math.Floor(heli.weakspots[1].health)) : string.Format("<color=red>{0}</color>", heliHealth);
                            
                            if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, null, heli.transform.position + new Vector3(0f, 2f, 0f), string.Format("<color=magenta>H</color> {0} <color=orange>{1}</color>", info, currDistance));
                            if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.magenta, heli.transform.position + new Vector3(0f, 1f, 0f), GetScale(currDistance));
                        }
                    }

                    if (!LatencyAccepted(tick)) // causing server lag, return. this shouldn't happen unless the server is already experiencing latency issues
                        return;

                    error = "ACTIVE";
                    foreach (var target in BasePlayer.activePlayerList.Where(target => target.IsConnected)) // was a prior bug so we'll be prepared if it happens again
                    {
                        double currDistance = Math.Floor(Vector3.Distance(target.transform.position, source.transform.position));

                        if (player == target || currDistance > maxDistance)
                            continue;

                        if (currDistance < playerDistance)
                        {
                            string displayName = target.displayName ?? target.UserIDString; // had this bug recently. squashing it now.

                            if (storedData.Visions.Contains(player.UserIDString)) DrawVision(player, target, invokeTime);
                            if (drawArrows) player.SendConsoleCommand("ddraw.arrow", invokeTime + flickerDelay, Color.red, target.transform.position + new Vector3(0f, target.transform.position.y + 10), target.transform.position, 1);
                            if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, null, target.transform.position + new Vector3(0f, 2f, 0f), string.Format("{0} <color=red>{1}</color> <color=orange>{2}</color>", displayName, Math.Floor(target.health), currDistance));
                            if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.red, target.transform.position + new Vector3(0f, 1f, 0f), target.GetHeight(target.modelState.ducked));
                        }
                        else player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.red, target.transform.position + new Vector3(0f, 1f, 0f), 5f);

                        if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                    }

                    if (!LatencyAccepted(tick))
                        return;

                    error = "TC";
                    if (showTC || showAll)
                    {
                        foreach (var tc in cachedTC)
                        {
                            double currDistance = Math.Floor(Vector3.Distance(tc.Key, source.transform.position));

                            if (currDistance < tcDistance && currDistance < maxDistance)
                            {
                                if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, Color.black, tc.Key + new Vector3(0f, 0.5f, 0f), string.Format("TC <color=orange>{0}</color>", currDistance));
                                if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.black, tc.Key + new Vector3(0f, 0.5f, 0f), tc.Value.Size);
                                if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                            }
                        }
                    }

                    if (!LatencyAccepted(tick))
                        return;

                    if (showBox || showLoot || showStash || showAll)
                    {
                        if (showLoot)
                        {
                            error = "DROPPEDITEM";
                            foreach (var item in cachedItems)
                            {
                                double currDistance = Math.Floor(Vector3.Distance(item.Key, source.transform.position));

                                if (currDistance < maxDistance)
                                {
                                    if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, Color.red, item.Key, string.Format("{0} <color=yellow>{1}</color>", item.Value.Name, item.Value.Size));
                                    if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.red, item.Key, 0.25f);
                                    if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                                }
                            }
                        }

                        error = "CONTAINERS";
                        foreach (var box in cachedContainers)
                        {
                            double currDistance = Math.Floor(Vector3.Distance(box.Key, source.transform.position));

                            if (currDistance > maxDistance)
                                continue;

                            bool sb = (showBox || showAll) && box.Value.Name.Contains("box") || box.Value.Name.Equals("heli_crate") || box.Value.Name.Equals("supply_drop");
                            bool sl = (showLoot || showAll) && box.Value.Name.Contains("loot") || box.Value.Name.Contains("crate_");
                            bool ss = (showStash || showAll) && box.Value.Name.Contains("stash");
                            var color = sb ? Color.magenta : sl ? Color.yellow : Color.white;

                            if ((sb && currDistance < boxDistance) || (sl && currDistance < lootDistance) || (ss && currDistance < stashDistance))
                            {
                                string contents = string.Empty;
                                uint uid;

                                if (box.Value.Info != null && uint.TryParse(box.Value.Info.ToString(), out uid))
                                {
                                    var container = BaseEntity.serverEntities.Find(uid) as StorageContainer;

                                    if (storedData.OnlineBoxes.Contains(player.UserIDString) && container.name.Contains("box"))
                                    {
                                        var owner = BasePlayer.activePlayerList.Find(x => x.userID == container.OwnerID);

                                        if (owner == null || !owner.IsConnected)
                                        {
                                            continue;
                                        }
                                    }

                                    if (container?.inventory?.itemList != null)
                                    {
                                        if (container.inventory.itemList.Count > 0)
                                        {
                                            if ((sl && showLootContents) || (container.ShortPrefabName.Equals("supply_drop") && showAirdropContents) || (container.ShortPrefabName.Contains("stash") && showStashContents))
                                                contents = string.Format("({0}) ", string.Join(", ", container.inventory.itemList.Select(item => string.Format("{0} ({1})", item.info.displayName.translated.ToLower(), item.amount)).ToArray()));
                                            else
                                                contents = string.Format("({0}) ", container.inventory.itemList.Count());
                                        }
                                    }
                                }

                                if (string.IsNullOrEmpty(contents) && !drawEmptyContainers)
                                    continue;

                                if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, color, box.Key + new Vector3(0f, 0.5f, 0f), string.Format("{0} {1}<color=orange>{2}</color>", _(box.Value.Name), contents, currDistance));
                                if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, color, box.Key + new Vector3(0f, 0.5f, 0f), GetScale(currDistance));
                                if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                            }
                        }
                    }

                    if (!LatencyAccepted(tick))
                        return;

                    error = "BAGS";
                    if (showBags || showAll)
                    {
                        foreach (var bag in cachedBags)
                        {
                            var currDistance = Math.Floor(Vector3.Distance(bag.Key, source.transform.position));

                            if (currDistance < bagDistance && currDistance < maxDistance)
                            {
                                if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, Color.magenta, bag.Key, string.Format("bag <color=orange>{0}</color>", currDistance));
                                if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.magenta, bag.Key, bag.Value.Size);
                                if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                            }
                        }
                    }

                    if (!LatencyAccepted(tick))
                        return;

                    error = "TURRETS";
                    if (showTurrets || showAll)
                    {
                        foreach (var turret in cachedTurrets)
                        {
                            var currDistance = Math.Floor(Vector3.Distance(turret.Key, source.transform.position));

                            if (currDistance < turretDistance && currDistance < maxDistance)
                            {
                                if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, Color.yellow, turret.Key + new Vector3(0f, 0.5f, 0f), string.Format("AT ({0}) <color=orange>{1}</color>", turret.Value.Info, currDistance));
                                if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.yellow, turret.Key + new Vector3(0f, 0.5f, 0f), turret.Value.Size);
                                if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                            }
                        }
                    }

                    if (!LatencyAccepted(tick))
                        return;

                    error = "SLEEPERS";
                    if (showSleepers || showAll)
                    {
                        foreach (var sleeper in BasePlayer.sleepingPlayerList)
                        {
                            double currDistance = Math.Floor(Vector3.Distance(sleeper.transform.position, source.transform.position));

                            if (currDistance > maxDistance)
                                continue;

                            if (currDistance < playerDistance)
                            {
                                if (drawArrows) player.SendConsoleCommand("ddraw.arrow", invokeTime + flickerDelay, Color.cyan, sleeper.transform.position + new Vector3(0f, sleeper.transform.position.y + 10), sleeper.transform.position, 1);
                                if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, sleeper.IsAlive() ? Color.cyan : Color.red, sleeper.transform.position, string.Format("{0} <color=red>{1}</color> <color=orange>{2}</color>", sleeper.displayName, Math.Floor(sleeper.health), currDistance));
                                if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, sleeper.IsAlive() ? Color.cyan : Color.red, sleeper.transform.position, GetScale(currDistance));
                            }
                            else player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.cyan, sleeper.transform.position + new Vector3(0f, 1f, 0f), 5f);

                            if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                        }
                    }

                    if (!LatencyAccepted(tick))
                        return;

                    error = "DEAD";
                    if (showDead || showAll)
                    {
                        foreach (var corpse in cachedCorpses)
                        {
                            if (corpse.Key == null)
                                continue;

                            double currDistance = Math.Floor(Vector3.Distance(source.transform.position, corpse.Key.transform.position));

                            if (currDistance < corpseDistance && currDistance < maxDistance)
                            {
                                if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, Color.yellow, corpse.Key.transform.position + new Vector3(0f, 0.25f, 0f), string.Format("{0} ({1})", corpse.Value.Name, corpse.Value.Info));
                                if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.yellow, corpse.Key, GetScale(currDistance));
                                if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                            }
                        }
                    }

                    if (!LatencyAccepted(tick))
                        return;

                    if (showNPC || showAll)
                    {
                        error = "NPCCACHE";
                        foreach (var target in npcCache.Where(target => target?.displayName != null))
                        {
                            double currDistance = Math.Floor(Vector3.Distance(target.transform.position, source.transform.position));

                            if (player == target || currDistance > maxDistance)
                                continue;

                            if (currDistance < playerDistance)
                            {
                                if (drawArrows) player.SendConsoleCommand("ddraw.arrow", invokeTime + flickerDelay, Color.blue, target.transform.position + new Vector3(0f, target.transform.position.y + 10), target.transform.position, 1);
                                if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, Color.blue, target.transform.position + new Vector3(0f, 2f, 0f), string.Format("{0} <color=red>{1}</color> <color=orange>{2}</color>", target.displayName, Math.Floor(target.health), currDistance));
                                if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.blue, target.transform.position + new Vector3(0f, 1f, 0f), target.GetHeight(target.modelState.ducked));
                            }
                            else player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.red, target.transform.position + new Vector3(0f, 1f, 0f), 5f);

                            if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                        }

                        error = "ANIMALS";
                        foreach (var npc in BaseEntity.serverEntities.Where(e => e is BaseNpc).Cast<BaseNpc>().ToList())
                        {
                            double currDistance = Math.Floor(Vector3.Distance(npc.transform.position, source.transform.position));

                            if (currDistance < npcDistance && currDistance < maxDistance)
                            {
                                if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, Color.blue, npc.transform.position + new Vector3(0f, 1f, 0f), string.Format("{0} <color=red>{1}</color>  <color=orange>{2}</color>", npc.ShortPrefabName, Math.Floor(npc.health), currDistance));
                                if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.blue, npc.transform.position + new Vector3(0f, 1f, 0f), npc.bounds.size.y);
                                if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                            }
                        }
                    }

                    if (!LatencyAccepted(tick))
                        return;

                    error = "ORE";
                    if (showOre || showAll)
                    {
                        foreach (var ore in cachedOres)
                        {
                            double currDistance = Math.Floor(Vector3.Distance(source.transform.position, ore.Key));

                            if (currDistance < oreDistance && currDistance < maxDistance)
                            {
                                object value = showResourceAmounts ? string.Format("({0})", ore.Value.Info) : string.Format("<color=orange>{0}</color>", currDistance);
                                if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, Color.yellow, ore.Key + new Vector3(0f, 1f, 0f), string.Format("{0} {1}", ore.Value.Name, value));
                                if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.yellow, ore.Key + new Vector3(0f, 1f, 0f), GetScale(currDistance));
                                if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                            }
                        }
                    }

                    if (!LatencyAccepted(tick))
                        return;

                    error = "COLLECTABLES";
                    if (showCollectible || showAll)
                    {
                        foreach (var col in cachedCollectibles)
                        {
                            var currDistance = Math.Floor(Vector3.Distance(col.Key, source.transform.position));

                            if (currDistance < colDistance && currDistance < maxDistance)
                            {
                                object value = showResourceAmounts ? string.Format("({0})", col.Value.Info) : string.Format("<color=orange>{0}</color>", currDistance);
                                if (drawText) player.SendConsoleCommand("ddraw.text", invokeTime + flickerDelay, Color.yellow, col.Key + new Vector3(0f, 1f, 0f), string.Format("{0} ({1})", col.Value.Name, value));
                                if (drawBox) player.SendConsoleCommand("ddraw.box", invokeTime + flickerDelay, Color.yellow, col.Key + new Vector3(0f, 1f, 0f), col.Value.Size);
                                if (objectsLimit > 0 && ++drawnObjects[player.userID] > objectsLimit) return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ins.Puts("Error @{0}: {1} --- {2}", error, ex.Message, ex.StackTrace);
                    player.ChatMessage(ins.msg("Exception", player.UserIDString));
                }
                finally
                {
                    if (!LatencyAccepted(tick))
                    {
                        var ms = (DateTime.Now - tick).TotalMilliseconds;
                        string message = ins.msg("DoESP", player.UserIDString, ms, latencyMs);
                        ins.Puts("{0} for {1} ({2})", message, player.displayName, player.UserIDString);
                        GameObject.Destroy(this);
                    }
                }
            }
        }

        static readonly int visionMask = LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Water", "Deployed", "Player (Server)");

        static void DrawVision(BasePlayer player, BasePlayer target, float invokeTime)
        {
            RaycastHit hit;

            if (!Physics.Raycast(target.eyes.HeadRay(), out hit, Mathf.Infinity, visionMask))
                return;

            player.SendConsoleCommand("ddraw.arrow", invokeTime + flickerDelay, Color.red, target.eyes.position + new Vector3(0f, 0.115f, 0f), hit.point, 0.15f);
        }

        static string _(string s)
        {
            foreach (string str in tags)
                s = s.Replace(str, "");

            return s;
        }

        void Track(BasePlayer player)
        {
            if (!player.gameObject.GetComponent<PlayerTracker>())
                player.gameObject.AddComponent<PlayerTracker>();

            if (trackerTimers.ContainsKey(player.userID))
            {
                trackerTimers[player.userID]?.Destroy();
                trackerTimers.Remove(player.userID);
            }
        }

        static void RemoveFromCache(BaseNetworkable entity)
        {
            if (!init || entity == null)
                return;

            if (cachedItems.ContainsKey(entity.transform.position))
            {
                cachedItems.Remove(entity.transform.position);
            }
            else if (entity is BasePlayer && npcCache.Contains(entity as BasePlayer))
            {
                npcCache.Remove(entity as BasePlayer);
            }
            else if (entity.GetComponent<BaseHelicopter>() && trackHelis)
            {
                var heli = entity.GetComponent<BaseHelicopter>();

                if (helisCache.Contains(heli))
                    helisCache.Remove(heli);
            }
            else if (cachedOres.ContainsKey(entity.transform.position))
                cachedOres.Remove(entity.transform.position);
            else if (entity is PlayerCorpse)
            {
                var corpse = entity as PlayerCorpse;

                if (cachedCorpses.ContainsKey(corpse))
                    cachedCorpses.Remove(corpse);
            }
            else if (cachedContainers.ContainsKey(entity.transform.position))
                cachedContainers.Remove(entity.transform.position);
            else if (cachedBags.ContainsKey(entity.transform.position))
                cachedBags.Remove(entity.transform.position);
            else if (cachedTC.ContainsKey(entity.transform.position))
                cachedTC.Remove(entity.transform.position);
            else if (cachedTurrets.ContainsKey(entity.transform.position))
                cachedTurrets.Remove(entity.transform.position);
            else if (cachedCollectibles.ContainsKey(entity.transform.position))
                cachedCollectibles.Remove(entity.transform.position);
        }

        class DropTracker : MonoBehaviour
        {
            DroppedItem droppedItem;
            Vector3 position;

            void Awake()
            {
                droppedItem = GetComponent<DroppedItem>();
                InvokeRepeating("Tracker", 0.1f, 0.1f);
                Tracker();
            }

            void Tracker()
            {
                if (position == transform.position)
                {
                    if (cachedItems.ContainsKey(position))
                        cachedItems.Remove(position);

                    cachedItems.Add(position, new CachedInfo() { Name = droppedItem.item.info.shortname, Size = droppedItem.item.amount, Info = droppedItem.item.uid });
                    GameObject.Destroy(this);
                    return;
                }

                position = transform.position;
            }

            void OnDestroy()
            {
                CancelInvoke("Tracker");
                GameObject.Destroy(this);
            }
        }

        static bool AddToCache(BaseNetworkable entity)
        {
            if (!init || entity == null || entity.IsDestroyed)
                return false;

            if (entity is DroppedItem)
            {
                if (Vector3.Distance(entity.transform.position, Vector3.zero) > 5f && !cachedItems.ContainsKey(entity.transform.position))
                {
                    var droppedItem = entity as DroppedItem;

                    if (!itemExceptions.Any(item => droppedItem.item.info.shortname.Contains(item)))
                    {
                        droppedItem.gameObject.AddComponent<DropTracker>();
                    }
                }
            }
            else if (entity is BasePlayer)
            {
                var player = entity as BasePlayer;

                if (!player.userID.IsSteamId() && !npcCache.Contains(player))
                {
                    npcCache.Add(player);
                }
            }
            else if (entity is BaseHelicopter && trackHelis)
            {
                helisCache.Add(entity.GetComponent<BaseHelicopter>());
            }
            else if (entity is BuildingPrivlidge)
            {
                if (!cachedTC.ContainsKey(entity.transform.position))
                {
                    cachedTC.Add(entity.transform.position, new CachedInfo() { Size = 3f });
                    return true;
                }
            }
            else if (entity is StorageContainer || entity is SupplyDrop)
            {
                if (cachedContainers.ContainsKey(entity.transform.position))
                    return false;

                if (entity.name.Contains("turret"))
                {
                    if (!cachedTurrets.ContainsKey(entity.transform.position))
                    {
                        cachedTurrets.Add(entity.transform.position, new CachedInfo() { Size = 1f, Info = entity.GetComponent<StorageContainer>()?.inventory?.itemList?.Select(item => item.amount).Sum() ?? 0 });
                        return true;
                    }
                }
                else if (entity.name.Contains("box") || entity.ShortPrefabName.Equals("heli_crate") || entity.ShortPrefabName.Equals("supply_drop") || entity.name.Contains("loot") || entity.name.Contains("crate_") || entity.name.Contains("stash"))
                {
                    cachedContainers.Add(entity.transform.position, new CachedInfo() { Name = entity.ShortPrefabName, Info = entity.net.ID });
                    return true;
                }
            }
            else if (entity is SleepingBag)
            {
                if (!cachedBags.ContainsKey(entity.transform.position))
                {
                    cachedBags.Add(entity.transform.position, new CachedInfo() { Size = 0.5f });
                    return true;
                }
            }
            else if (entity is PlayerCorpse)
            {
                var corpse = entity.GetComponent<PlayerCorpse>();

                if (!cachedCorpses.ContainsKey(corpse))
                {
                    int amount = 0;

                    if (corpse.containers != null)
                        foreach (var container in corpse.containers)
                            amount += container.itemList.Count;

                    cachedCorpses.Add(corpse, new CachedInfo() { Name = corpse.parentEnt?.ToString() ?? corpse.playerSteamID.ToString(), Info = amount });
                    return true;
                }
            }
            else if (entity is CollectibleEntity)
            {
                if (!cachedCollectibles.ContainsKey(entity.transform.position))
                {
                    cachedCollectibles.Add(entity.transform.position, new CachedInfo() { Name = _(entity.ShortPrefabName), Size = 0.5f, Info = Math.Ceiling(entity.GetComponent<CollectibleEntity>()?.itemList?.Select(item => item.amount).Sum() ?? 0) });
                    return true;
                }
            }
            else if (entity.name.Contains("-ore"))
            {
                if (!cachedOres.ContainsKey(entity.transform.position))
                {
                    cachedOres.Add(entity.transform.position, new CachedInfo() { Name = _(entity.ShortPrefabName), Info = Math.Ceiling(entity.GetComponentInParent<ResourceDispenser>()?.containedItems?.Select(item => item.amount).Sum() ?? 0) });
                    return true;
                }
            }

            return false;
        }

        static float GetScale(double v) => v <= 50 ? 1f : v > 50 && v <= 100 ? 2f : v > 100 && v <= 150 ? 2.5f : v > 150 && v <= 200 ? 3f : v > 200 && v <= 300 ? 4f : 5f;
        
        [ConsoleCommand("espgui")]
        void ccmdESPGUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (!player || !arg.HasArgs())
                return;

            cmdESP(player, "espgui", arg.Args);
        }

        bool HasAccess(BasePlayer player)
        {
            if (player.IsDeveloper)
                return true;

            if (authorized.Count > 0)
                return authorized.Contains(player.UserIDString);

            if (player.net.connection.authLevel >= authLevel)
                return true;

            if (permission.UserHasPermission(player.UserIDString, "fauxadmin.allowed") && permission.UserHasPermission(player.UserIDString, permName) && player.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
                return true;

            return false;
        }

        void cmdESP(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player))
            {
                player.ChatMessage(msg("NotAllowed", player.UserIDString));
                return;
            }

            if (args.Length == 1)
            {
                if (args[0] == "online")
                {
                    if (storedData.OnlineBoxes.Contains(player.UserIDString))
                    {
                        storedData.OnlineBoxes.Remove(player.UserIDString);
                        player.ChatMessage(msg("BoxesAll", player.UserIDString));
                    }
                    else
                    {
                        storedData.OnlineBoxes.Add(player.UserIDString);
                        player.ChatMessage(msg("BoxesOnlineOnly", player.UserIDString));
                    }
                    return;
                }

                if (args[0] == "vision")
                {
                    if (storedData.Visions.Contains(player.UserIDString))
                    {
                        storedData.Visions.Remove(player.UserIDString);
                        player.ChatMessage(msg("VisionOff", player.UserIDString));
                    }
                    else
                    {
                        storedData.Visions.Add(player.UserIDString);
                        player.ChatMessage(msg("VisionOn", player.UserIDString));
                    }
                    return;
                }
            }

            if (!filters.ContainsKey(player.UserIDString))
                filters.Add(player.UserIDString, args.ToList());

            if (args.Length == 0 && player.GetComponent<ESP>())
            {
                GameObject.Destroy(player.GetComponent<ESP>());
                return;
            }

            args = args.Select(arg => arg.ToLower()).ToArray();

            if (args.Length == 1)
            {
                if (args[0] == "tracker")
                {
                    if (!usePlayerTracker)
                    {
                        player.ChatMessage(msg("TrackerDisabled", player.UserIDString));
                        return;
                    }

                    if (trackers.Count == 0)
                    {
                        player.ChatMessage(msg("NoTrackers", player.UserIDString));
                        return;
                    }

                    var lastPos = Vector3.zero;
                    bool inRange = false;
                    var colors = new List<Color>();

                    foreach (var kvp in trackers)
                    {
                        lastPos = Vector3.zero;

                        if (trackers[kvp.Key].Count > 0)
                        {
                            if (colors.Count == 0)
                                colors = new List<Color>() { Color.blue, Color.cyan, Color.gray, Color.green, Color.magenta, Color.red, Color.yellow };

                            var color = playersColor.ContainsKey(kvp.Key) ? playersColor[kvp.Key] : colors[UnityEngine.Random.Range(0, colors.Count - 1)];

                            playersColor[kvp.Key] = color;

                            if (colors.Contains(color))
                                colors.Remove(color);

                            foreach (var entry in trackers[kvp.Key])
                            {
                                if (Vector3.Distance(entry.Value, player.transform.position) < maxTrackReportDistance)
                                {
                                    if (lastPos == Vector3.zero)
                                    {
                                        lastPos = entry.Value;
                                        continue;
                                    }

                                    if (Vector3.Distance(lastPos, entry.Value) < overlapDistance) // this prevents arrow lines from being tangled upon other arrow lines into a giant clusterfuck
                                        continue;

                                    player.SendConsoleCommand("ddraw.arrow", trackDrawTime, color, lastPos, entry.Value, 0.1f);
                                    lastPos = entry.Value;
                                    inRange = true;
                                }
                            }

                            if (lastPos != Vector3.zero)
                            {
                                string name = covalence.Players.FindPlayerById(kvp.Key.ToString()).Name;
                                player.SendConsoleCommand("ddraw.text", trackDrawTime, color, lastPos, string.Format("{0} ({1})", name, trackers[kvp.Key].Count));
                            }
                        }
                    }

                    if (!inRange)
                        player.ChatMessage(msg("NoTrackersInRange", player.UserIDString, maxTrackReportDistance));

                    return;
                }
                else if (args[0] == "help")
                {
                    /*foreach (var kvp in Config)
                    {
                        if (kvp.Value is Dictionary<string, object>)
                        {
                            var dict = kvp.Value as Dictionary<string, object>;
                            player.ChatMessage(string.Format("<color=orange>{0}</color>: {1}", kvp.Key, string.Join(", ", dict.Select(e => string.Format("<color=silver>{0}</color> (<color=green>{1}</color>)", e.Key, e.Value)).ToArray())));
                        }
                    }*/

                    player.ChatMessage(msg("Help1", player.UserIDString, "all, bag, box, col, dead, loot, npc, ore, stash, tc, turret"));
                    player.ChatMessage(msg("Help2", player.UserIDString, szChatCommand, "online"));
                    player.ChatMessage(msg("Help3", player.UserIDString, szChatCommand, "ui"));
                    player.ChatMessage(msg("Help4", player.UserIDString, szChatCommand, "tracker"));
                    player.ChatMessage(msg("Help5", player.UserIDString, szChatCommand));
                    player.ChatMessage(msg("Help6", player.UserIDString, szChatCommand));
                    player.ChatMessage(msg("PreviousFilter", player.UserIDString, command));
                    return;
                }
                else if (args[0].Contains("ui"))
                {
                    if (filters[player.UserIDString].Contains(args[0]))
                        filters[player.UserIDString].Remove(args[0]);

                    if (uiHidden.Contains(player.UserIDString))
                    {
                        uiHidden.Remove(player.UserIDString);
                        player.ChatMessage(msg("GUIShown", player.UserIDString));
                    }
                    else
                    {
                        uiHidden.Add(player.UserIDString);
                        player.ChatMessage(msg("GUIHidden", player.UserIDString));
                    }

                    args = filters[player.UserIDString].ToArray();
                }
                else if (args[0] == "f")
                    args = filters[player.UserIDString].ToArray();
            }

            if (command == "espgui")
            {
                string filter = filters[player.UserIDString].Find(f => f.Contains(args[0]) || args[0].Contains(f)) ?? args[0];

                if (filters[player.UserIDString].Contains(filter))
                    filters[player.UserIDString].Remove(filter);
                else
                    filters[player.UserIDString].Add(filter);

                args = filters[player.UserIDString].ToArray();
            }
            else
                filters[player.UserIDString] = args.ToList();

            var esp = player.GetComponent<ESP>() ?? player.gameObject.AddComponent<ESP>();
            float invokeTime, maxDistance, outTime, outDistance;

            if (args.Length > 0 && float.TryParse(args[0], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out outTime))
                invokeTime = outTime < 0.1f ? 0.1f : outTime;
            else
                invokeTime = defaultInvokeTime;

            if (args.Length > 1 && float.TryParse(args[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out outDistance))
                maxDistance = outDistance <= 0f ? defaultMaxDistance : outDistance;
            else
                maxDistance = defaultMaxDistance;

            esp.showAll = args.Any(arg => arg.Contains("all"));
            esp.showBags = args.Any(arg => arg.Contains("bag"));
            esp.showBox = args.Any(arg => arg.Contains("box"));
            esp.showCollectible = args.Any(arg => arg.Contains("col"));
            esp.showDead = args.Any(arg => arg.Contains("dead"));
            esp.showLoot = args.Any(arg => arg.Contains("loot"));
            esp.showNPC = args.Any(arg => arg.Contains("npc"));
            esp.showOre = args.Any(arg => arg.Contains("ore"));
            esp.showSleepers = args.Any(arg => arg.Contains("sleep"));
            esp.showStash = args.Any(arg => arg.Contains("stash"));
            esp.showTC = args.Any(arg => arg.Contains("tc"));
            esp.showTurrets = args.Any(arg => arg.Contains("turret"));

            string gui;
            if (guiInfo.TryGetValue(player.UserIDString, out gui))
            {
                CuiHelper.DestroyUi(player, gui);
                guiInfo.Remove(player.UserIDString);
            }

            if (!uiHidden.Contains(player.UserIDString))
            {
                string espUI = uiJson;

                espUI = espUI.Replace("{anchorMin}", alignTopLeft ? anchorMinTopLeft : anchorMinBottomRight);
                espUI = espUI.Replace("{anchorMax}", alignTopLeft ? anchorMaxTopLeft : anchorMaxBottomRight);
                espUI = espUI.Replace("{colorAll}", esp.showAll ? "255 0 0 1" : "1 1 1 1");
                espUI = espUI.Replace("{colorBags}", esp.showBags ? "255 0 0 1" : "1 1 1 1");
                espUI = espUI.Replace("{colorBox}", esp.showBox ? "255 0 0 1" : "1 1 1 1");
                espUI = espUI.Replace("{colorCol}", esp.showCollectible ? "255 0 0 1" : "1 1 1 1");
                espUI = espUI.Replace("{colorDead}", esp.showDead ? "255 0 0 1" : "1 1 1 1");
                espUI = espUI.Replace("{colorLoot}", esp.showLoot ? "255 0 0 1" : "1 1 1 1");
                espUI = espUI.Replace("{colorNPC}", esp.showNPC ? "255 0 0 1" : "1 1 1 1");
                espUI = espUI.Replace("{colorOre}", esp.showOre ? "255 0 0 1" : "1 1 1 1");
                espUI = espUI.Replace("{colorSleepers}", esp.showSleepers ? "255 0 0 1" : "1 1 1 1");
                espUI = espUI.Replace("{colorStash}", esp.showStash ? "255 0 0 1" : "1 1 1 1");
                espUI = espUI.Replace("{colorTC}", esp.showTC ? "255 0 0 1" : "1 1 1 1");
                espUI = espUI.Replace("{colorTurrets}", esp.showTurrets ? "255 0 0 1" : "1 1 1 1");

                guiInfo[player.UserIDString] = CuiHelper.GetGuid();
                CuiHelper.AddUi(player, espUI.Replace("{guid}", guiInfo[player.UserIDString]));
            }

            esp.invokeTime = invokeTime;
            esp.maxDistance = maxDistance;

            esp.CancelInvoke("DoESP");
            esp.Invoke("DoESP", invokeTime);
            esp.InvokeRepeating("DoESP", 0f, invokeTime);

            if (command == "espgui")
                return;

            player.ChatMessage(msg("Activated", player.UserIDString, invokeTime, maxDistance, command));
        }

        #region Config
        bool Changed;
        static bool drawText = true;
        static bool drawBox = false;
        static bool drawArrows = false;
        static int authLevel { get; set; }
        static float defaultInvokeTime { get; set; }
        static float defaultMaxDistance { get; set; }

        static float boxDistance { get; set; }
        static float playerDistance { get; set; }
        static float tcDistance { get; set; }
        static float stashDistance { get; set; }
        static float corpseDistance { get; set; }
        static float oreDistance { get; set; }
        static float lootDistance { get; set; }
        static float colDistance { get; set; }
        static float bagDistance { get; set; }
        static float npcDistance { get; set; }
        static float turretDistance { get; set; }
        static float latencyMs { get; set; }
        static int objectsLimit { get; set; }
        static bool showLootContents { get; set; }
        static bool showAirdropContents { get; set; }
        static bool showStashContents { get; set; }
        static bool drawEmptyContainers { get; set; }
        static bool showResourceAmounts { get; set; }
        static bool trackHelis { get; set; }
        static bool showHeliRotorHealth { get; set; }
        static bool usePlayerTracker { get; set; }
        static bool trackAdmins { get; set; }
        static float trackerUpdateInterval { get; set; }
        static float trackerAge { get; set; }
        static float maxTrackReportDistance { get; set; }
        static float trackDrawTime { get; set; }
        static float overlapDistance { get; set; }

        static string szChatCommand { get; set; }
        static List<object> authorized { get; set; }
        static List<string> itemExceptions { get; set; } = new List<string>();
        bool alignTopLeft { get; set; }

        List<object> ItemExceptions
        {
            get
            {
                return new List<object> { "bottle", "planner", "rock", "torch", "can.", "arrow." };
            }
        }

        void LoadVariables()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You are not allowed to use this command.",
                ["PreviousFilter"] = "To use your previous filter type <color=orange>/{0} f</color>",
                ["Activated"] = "ESP Activated - {0}s refresh - {1}m distance. Use <color=orange>/{2} help</color> for help.",
                ["Deactivated"] = "ESP Deactivated.",
                ["DoESP"] = "DoESP() took {0}ms (max: {1}ms) to execute!",
                ["TrackerDisabled"] = "Player Tracker is disabled.",
                ["NoTrackers"] = "No players have been tracked yet.",
                ["NoTrackersInRange"] = "No trackers in range ({0}m)",
                ["Exception"] = "ESP Tool: An error occured. Please check the server console.",
                ["GUIShown"] = "GUI will be shown",
                ["GUIHidden"] = "GUI will now be hidden",
                ["InvalidID"] = "{0} is not a valid steam id. Entry removed.",
                ["BoxesAll"] = "Now showing all boxes.",
                ["BoxesOnlineOnly"] = "Now showing online player boxes only.",
                ["Help1"] = "<color=orange>Available Filters</color>: {0}",
                ["Help2"] = "<color=orange>/{0} {1}</color> - Toggles showing online players boxes only when using the <color=red>box</color> filter.",
                ["Help3"] = "<color=orange>/{0} {1}</color> - Toggles quick toggle UI on/off",
                ["Help4"] = "<color=orange>/{0} {1}</color> - Draw on your screen the movement of nearby players. Must be enabled.",
                ["Help5"] = "e.g: <color=orange>/{0} 1 1000 box loot stash</color>",
                ["Help6"] = "e.g: <color=orange>/{0} 0.5 400 all</color>",
                ["VisionOn"] = "You will now see where players are looking.",
                ["VisionOff"] = "You will no longer see where players are looking.",
            }, this);

            authorized = GetConfig("Settings", "Restrict Access To Steam64 IDs", new List<object>()) as List<object>;

            foreach (var auth in authorized.ToList())
            {
                if (auth == null || !auth.ToString().IsSteamId())
                {
                    PrintWarning(msg("InvalidID", null, auth == null ? "null" : auth.ToString()));
                    authorized.Remove(auth);
                }
            }

            authLevel = authorized.Count == 0 ? Convert.ToInt32(GetConfig("Settings", "Restrict Access To Auth Level", 1)) : int.MaxValue;
            defaultMaxDistance = Convert.ToSingle(GetConfig("Settings", "Default Distance", 500.0));
            defaultInvokeTime = Convert.ToSingle(GetConfig("Settings", "Default Refresh Time", 5.0));
            latencyMs = Convert.ToInt32(GetConfig("Settings", "Latency Cap In Milliseconds (0 = no cap)", 1000.0));
            objectsLimit = Convert.ToInt32(GetConfig("Settings", "Objects Drawn Limit (0 = unlimited)", 250));
            itemExceptions = (GetConfig("Settings", "Dropped Item Exceptions", ItemExceptions) as List<object>).Cast<string>().ToList();
            alignTopLeft = Convert.ToBoolean(GetConfig("Settings", "Align GUI Top Left", false));

            showLootContents = Convert.ToBoolean(GetConfig("Options", "Show Barrel And Crate Contents", false));
            showAirdropContents = Convert.ToBoolean(GetConfig("Options", "Show Airdrop Contents", false));
            showStashContents = Convert.ToBoolean(GetConfig("Options", "Show Stash Contents", false));
            drawEmptyContainers = Convert.ToBoolean(GetConfig("Options", "Draw Empty Containers", true));
            showResourceAmounts = Convert.ToBoolean(GetConfig("Options", "Show Resource Amounts", true));

            drawArrows = Convert.ToBoolean(GetConfig("Drawing Methods", "Draw Arrows On Players", false));
            drawBox = Convert.ToBoolean(GetConfig("Drawing Methods", "Draw Boxes", false));
            drawText = Convert.ToBoolean(GetConfig("Drawing Methods", "Draw Text", true));

            npcDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Animals", 200));
            bagDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Sleeping Bags", 250));
            boxDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Boxes", 100));
            colDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Collectibles", 100));
            corpseDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Player Corpses", 200));
            playerDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Players", 500));
            lootDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Loot Containers", 150));
            oreDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Resources (Ore)", 200));
            stashDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Stashes", 250));
            tcDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Tool Cupboards", 100));
            turretDistance = Convert.ToSingle(GetConfig("Drawing Distances", "Turrets", 100));

            trackHelis = Convert.ToBoolean(GetConfig("Helicopters", "Track Helicopters", true));
            showHeliRotorHealth = Convert.ToBoolean(GetConfig("Helicopters", "Show Rotors Health", false));

            usePlayerTracker = Convert.ToBoolean(GetConfig("Player Movement Tracker", "Enabled", false));
            trackAdmins = Convert.ToBoolean(GetConfig("Player Movement Tracker", "Track Admins", false));
            trackerUpdateInterval = Convert.ToSingle(GetConfig("Player Movement Tracker", "Update Tracker Every X Seconds", 1f));
            trackerAge = Convert.ToInt32(GetConfig("Player Movement Tracker", "Positions Expire After X Seconds", 600));
            maxTrackReportDistance = Convert.ToSingle(GetConfig("Player Movement Tracker", "Max Reporting Distance", 200f));
            trackDrawTime = Convert.ToSingle(GetConfig("Player Movement Tracker", "Draw Time", 60f));
            overlapDistance = Convert.ToSingle(GetConfig("Player Movement Tracker", "Overlap Reduction Distance", 5f));
            
            szChatCommand = Convert.ToString(GetConfig("Settings", "Chat Command", "radar"));

            if (!string.IsNullOrEmpty(szChatCommand))
                cmd.AddChatCommand(szChatCommand, this, cmdESP);

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