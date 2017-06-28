using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Raid Tracker", "nivex", "0.1.13", ResourceId = 2458)]
    [Description("Add tracking devices to explosives for detailed raid logging.")]
    public class RaidTracker : RustPlugin
    {
        [PluginReference]
        Plugin Discord, Slack;

        static RaidTracker ins;

        private bool init = false;
        private bool wipeData = false;
        private static bool explosionsLogChanged = false;
        private Dictionary<string, Color> attackersColor = new Dictionary<string, Color>();
        private static List<Dictionary<string, string>> dataExplosions;
        private DynamicConfigFile explosionsFile;
        private static int layerMask = LayerMask.GetMask("Construction", "Deployed");
        private List<string> limits = new List<string>();
        List<BasePlayer> flagged = new List<BasePlayer>();

        private static readonly string szEntityOwner = "EntityOwner";
        private static readonly string szEntityHit = "EntityHit";
        private static readonly string szEntitiesHit = "EntitiesHit";
        private static readonly string szAttacker = "Attacker";
        private static readonly string szAttackerId = "AttackerId";
        private static readonly string szStartPositionId = "StartPositionId";
        private static readonly string szEndPositionId = "EndPositionId";
        private static readonly string szWeapon = "Weapon";
        private static readonly string szDeleteDate = "DeleteDate";
        private static readonly string szLoggedDate = "LoggedDate";

        class EntityInfo
        {
            public string ShortPrefabName { get; set; }
            public ulong OwnerID { get; set; }
            public uint NetworkID { get; set; }
            public Vector3 Position { get; set; }
            public EntityInfo() { }
        }

        public class TrackingDevice : MonoBehaviour
        {
            private BaseEntity entity;
            private Vector3 position;
            private string weapon;
            private string entityHit;
            private ulong entityOwner;
            private int entitiesHit;
            private float radius;
            private bool updated;
            private bool isRocket;
            private double millisecondsTaken;
            private Dictionary<Vector3, EntityInfo> prefabs;

            public string playerName { get; set; }
            public string playerId { get; set; }
            public Vector3 playerPosition { get; set; }

            void Awake()
            {
                prefabs = new Dictionary<Vector3, EntityInfo>();
                entity = GetComponent<BaseEntity>();
                weapon = entity.ShortPrefabName;
                isRocket = weapon.Contains("rocket");
                position = entity.GetEstimatedWorldPosition();
                radius = isRocket ? GetComponent<TimedExplosive>().explosionRadius : 0.5f;
                Update(); // for instantaneous explosions?
                //Debug.Log(entity.ShortPrefabName);
            }

            void Update()
            {
                var newPosition = entity.GetEstimatedWorldPosition();

                if (newPosition == position) // don't continue if the position hasn't changed. this usually only occurs once
                    return;

                if (Vector3.Distance(newPosition, Vector3.zero) < 5f) // entity moved to vector3.zero
                    return;

                var tick = DateTime.Now;
                position = newPosition;

                var colliders = Pool.GetList<Collider>();
                Vis.Colliders(position, radius, colliders, layerMask, QueryTriggerInteraction.Collide);

                if (colliders.Count > 0)
                {
                    foreach (var collider in colliders)
                    {
                        var e = collider.gameObject?.ToBaseEntity() ?? null;

                        if (e == null)
                            continue;

                        if (!prefabs.ContainsKey(e.transform.position))
                        {
                            prefabs.Add(e.transform.position, new EntityInfo() { NetworkID = e.net.ID, OwnerID = e.OwnerID, Position = e.transform.position, ShortPrefabName = e.ShortPrefabName });

                            updated = true;

                            if (isRocket)
                                entitiesHit++;
                            else
                                entitiesHit = 1;
                        }
                    }

                    Pool.FreeList<Collider>(ref colliders);
                }

                millisecondsTaken += (DateTime.Now - tick).TotalMilliseconds;
            }

            void OnDestroy()
            {
                var tick = DateTime.Now;

                if (prefabs.Count > 0)
                {
                    var sorted = prefabs.ToList<KeyValuePair<Vector3, EntityInfo>>();
                    sorted.Sort((x, y) => Vector3.Distance(x.Key, playerPosition).CompareTo(Vector3.Distance(y.Key, playerPosition)));

                    //foreach (var kvp in sorted) Debug.Log(string.Format("{0} {1}", kvp.Value, kvp.Key));

                    entityHit = sorted[0].Value.ShortPrefabName;
                    entityOwner = sorted[0].Value.OwnerID;

                    prefabs.Clear();
                    sorted.Clear();
                }

                if (string.IsNullOrEmpty(entityHit))
                {
                    GameObject.Destroy(this);
                    return;
                }

                if (weapon.Contains("timed"))
                    weapon = "C4";
                else if (weapon.Contains("satchel"))
                    weapon = "Satchel Charge";
                else if (weapon.Contains("basic"))
                    weapon = "Rocket";
                else if (weapon.Contains("hv"))
                    weapon = "High Velocity Rocket";
                else if (weapon.Contains("fire"))
                    weapon = "Incendiary Rocket";
                else if (weapon.Contains("beancan"))
                    weapon = "Beancan Grenade";
                else if (weapon.Contains("f1"))
                    weapon = "F1 Grenade";

                var explosion = new Dictionary<string, string>
                {
                    [szAttacker] = playerName,
                    [szAttackerId] = playerId,
                    [szStartPositionId] = GetPositionID(playerPosition),
                    [szEndPositionId] = GetPositionID(position),
                    [szWeapon] = weapon,
                    [szEntitiesHit] = entitiesHit.ToString(),
                    [szEntityHit] = entityHit,
                    [szDeleteDate] = daysBeforeDelete > 0 ? DateTime.UtcNow.AddDays(daysBeforeDelete).ToString() : DateTime.MinValue.ToString(),
                    [szLoggedDate] = DateTime.UtcNow.ToString(),
                    [szEntityOwner] = entityOwner.ToString()
                };

                dataExplosions.Add(explosion);
                explosionsLogChanged = true;

                var endPosStr = string.Format("{0} {1} {2}", Math.Round(position.x, 2), Math.Round(position.y, 2), Math.Round(position.z, 2));
                string victim = entityOwner > 0 ? ins.covalence.Players.FindPlayerById(entityOwner.ToString())?.Name ?? entityOwner.ToString() : "No owner";
                string message = string.Format("[Explosion] {0} ({1}) @ {2} ({3}m) {4}: {5} - Victim: {6} ({7})", playerName, playerId, endPosStr, Math.Round(Vector3.Distance(position, playerPosition), 2), weapon, entityHit, victim, entityOwner);

                if (outputExplosionMessages)
                    Debug.Log(message);

                if (sendDiscordNotifications)
                    ins.Discord?.Call("SendMessage", message);

                if (sendSlackNotifications)
                    ins.Slack?.Call(slackMessageStyle, message, ins.covalence.Players.FindPlayerById(playerId));

                millisecondsTaken += (DateTime.Now - tick).TotalMilliseconds;

                if (showMillisecondsTaken)
                    Debug.Log(string.Format("Took {0}ms for tracking device operations", millisecondsTaken));

                GameObject.Destroy(this);
            }
        }

        void OnServerSave() => SaveExplosionData();
        void OnNewSave(string filename) => wipeData = true;

        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(TrackingDevice));

            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);

            foreach (var target in flagged.ToList()) // in the event the plugin is unloaded while a player has an admin flag
            {
                if (target != null && target.IsConnected)
                {
                    if (target.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
                    {
                        target.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                        target.SendNetworkUpdateImmediate();
                    }
                }
            }

            SaveExplosionData();
            dataExplosions?.Clear();
            attackersColor?.Clear();
        }

        void OnServerInitialized()
        {
            if (init)
                return;

            ins = this;

            LoadMessages();
            LoadVariables();

            explosionsFile = Interface.Oxide.DataFileSystem.GetFile("RaidTracker");

            try
            {
                if (explosionsFile.Exists())
                    dataExplosions = explosionsFile.ReadObject<List<Dictionary<string, string>>>();
            }
            catch { }

            if (dataExplosions == null)
            {
                dataExplosions = new List<Dictionary<string, string>>();
                explosionsLogChanged = true;
            }

            if (wipeData && automateWipes)
            {
                int entries = dataExplosions.Count;
                dataExplosions.Clear();
                explosionsLogChanged = true;
                wipeData = false;
                SaveExplosionData();
                Puts("Wipe detected; wiped {0} entries.", entries);
            }

            if (dataExplosions.Count > 0)
            {
                foreach (var dict in dataExplosions.ToList())
                {
                    if (dict.ContainsKey(szDeleteDate)) // apply retroactive changes
                    {
                        if (applyInactiveChanges)
                        {
                            if (daysBeforeDelete > 0 && dict[szDeleteDate] == DateTime.MinValue.ToString())
                            {
                                dict[szDeleteDate] = DateTime.UtcNow.AddDays(daysBeforeDelete).ToString();
                                explosionsLogChanged = true;
                            }
                            if (daysBeforeDelete == 0 && dict[szDeleteDate] != DateTime.MinValue.ToString())
                            {
                                dict[szDeleteDate] = DateTime.MinValue.ToString();
                                explosionsLogChanged = true;
                            }
                        }

                        if (applyActiveChanges && daysBeforeDelete > 0 && dict.ContainsKey(szLoggedDate))
                        {
                            var deleteDate = DateTime.Parse(dict[szDeleteDate]);
                            var loggedDate = DateTime.Parse(dict[szLoggedDate]);
                            int days = deleteDate.Subtract(loggedDate).Days;

                            if (days != daysBeforeDelete)
                            {
                                int daysLeft = deleteDate.Subtract(DateTime.UtcNow).Days;

                                if (daysLeft > daysBeforeDelete)
                                {
                                    dict[szDeleteDate] = loggedDate.AddDays(daysBeforeDelete).ToString();
                                    explosionsLogChanged = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        dict.Add(szDeleteDate, daysBeforeDelete > 0 ? DateTime.UtcNow.AddDays(daysBeforeDelete).ToString() : DateTime.MinValue.ToString());
                        explosionsLogChanged = true;
                    }
                }
            }

            init = true;
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!init || player == null || entity?.net == null)
                return;

            if (entity.ShortPrefabName.Contains("f1") && !trackF1)
                return;
            else if (entity.ShortPrefabName.Contains("beancan") && !trackBeancan)
                return;
            else if (!trackF1 && !trackBeancan && !entity.name.Contains("explosive"))
                return;

            var tracker = entity.gameObject.AddComponent<TrackingDevice>();
            var position = player.transform.position;
            position.y += player.GetHeight() - 0.25f;

            tracker.playerName = player.displayName;
            tracker.playerId = player.UserIDString;
            tracker.playerPosition = position;
        }

        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (!init || player == null || entity?.net == null || entity.name.Contains("smoke"))
                return;

            var tracker = entity.gameObject.AddComponent<TrackingDevice>();
            var position = player.transform.position;
            position.y += player.GetHeight() - 0.25f;

            tracker.playerName = player.displayName;
            tracker.playerId = player.UserIDString;
            tracker.playerPosition = position;
        }

        private bool IsNumeric(string value) => !string.IsNullOrEmpty(value) && value.All(char.IsDigit);
        private static string GetPositionID(Vector3 pos) => pos.x + " " + pos.y + " " + pos.z;

        private Vector3 GetPosition(string positionId)
        {
            if (!string.IsNullOrEmpty(positionId))
            {
                var position = positionId.Split(' ');

                if (position.Length >= 3)
                    return new Vector3(float.Parse(position[0]), float.Parse(position[1]), float.Parse(position[2]));
            }

            return Vector3.zero;
        }

        private void SaveExplosionData()
        {
            int expired = dataExplosions.RemoveAll(x => x.ContainsKey(szDeleteDate) && x[szDeleteDate] != DateTime.MinValue.ToString() && DateTime.Parse(x[szDeleteDate]) < DateTime.UtcNow);

            if (expired > 0)
                explosionsLogChanged = true;

            if (explosionsLogChanged)
            {
                explosionsFile.WriteObject(dataExplosions);
                explosionsLogChanged = false;
            }
        }

        private void cmdPX(BasePlayer player, string command, string[] args)
        {
            if (!allowPlayerExplosionMessages && !allowPlayerDrawing)
                return;

            if (!permission.UserHasPermission(player.UserIDString, playerPerm) && !player.IsAdmin)
            {
                player.ChatMessage(msg("Not Allowed", player.UserIDString));
                return;
            }

            if (dataExplosions == null || dataExplosions.Count == 0)
            {
                player.ChatMessage(msg("None Logged", player.UserIDString));
                return;
            }

            if (limits.Contains(player.UserIDString))
                return;

            var colors = new List<Color>();
            var explosions = dataExplosions.FindAll(x => x.ContainsKey(szEntityOwner) && x[szEntityOwner] == player.UserIDString && x[szAttackerId] != player.UserIDString && Vector3.Distance(player.transform.position, GetPosition(x[szEndPositionId])) <= playerDistance);

            if (explosions == null || explosions.Count == 0)
            {
                player.ChatMessage(msg("None Owned", player.UserIDString));
                return;
            }

            player.ChatMessage(msg("Showing Owned", player.UserIDString));

            bool drawX = explosions.Count > maxNamedExplosions;
            int shownExplosions = 0;
            var attackers = new Dictionary<string, string>();
            
            if (showExplosionMessages && maxMessagesToPlayer > 0)
                foreach (var x in explosions)
                    if (!attackers.ContainsKey(x[szAttackerId]))
                        attackers.Add(x[szAttackerId], ParseExplosions(explosions.Where(ex => ex[szAttackerId] == x[szAttackerId]).ToList()));

            if (allowPlayerDrawing)
            {
                try
                {
                    if (player.net.connection.authLevel == 0 && !player.IsAdmin)
                    {
                        if (!flagged.Contains(player))
                        {
                            flagged.Add(player);
                            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                            player.SendNetworkUpdateImmediate();
                        }
                    }

                    foreach (var x in explosions)
                    {
                        var startPos = GetPosition(x[szStartPositionId]);
                        var endPos = GetPosition(x[szEndPositionId]);
                        var endPosStr = string.Format("{0} {1} {2}", Math.Round(endPos.x, 2), Math.Round(endPos.y, 2), Math.Round(endPos.z, 2));

                        if (colors.Count == 0)
                            colors = new List<Color>() { Color.blue, Color.cyan, Color.gray, Color.green, Color.magenta, Color.red, Color.yellow };

                        var color = colorByWeaponType ? (x[szWeapon].Contains("Rocket") ? Color.red : x[szWeapon].Equals("C4") ? Color.yellow : Color.blue) : attackersColor.ContainsKey(x[szAttackerId]) ? attackersColor[x[szAttackerId]] : colors[UnityEngine.Random.Range(0, colors.Count - 1)];

                        attackersColor[x[szAttackerId]] = color;

                        if (colors.Contains(color))
                            colors.Remove(color);

                        if (showConsoleMessages)
                        {
                            var explosion = msg("Explosions", player.UserIDString, ColorUtility.ToHtmlStringRGB(attackersColor[x[szAttackerId]]), x[szAttacker], x[szAttackerId], endPosStr, Math.Round(Vector3.Distance(startPos, endPos), 2), x[szWeapon], x[szEntitiesHit], x[szEntityHit]);
                            var victim = x.ContainsKey(szEntityOwner) ? string.Format(" - Victim: {0} ({1})", covalence.Players.FindPlayerById(x[szEntityOwner])?.Name ?? "Unknown", x[szEntityOwner]) : "";

                            player.ConsoleMessage(explosion + victim);
                        }

                        if (drawArrows && Vector3.Distance(startPos, endPos) > 1f)
                        {
                            player.SendConsoleCommand("ddraw.arrow", invokeTime, color, startPos, endPos, 0.2);
                            player.SendConsoleCommand("ddraw.text", invokeTime, color, startPos, x[szWeapon].Substring(0, 1));
                        }

                        player.SendConsoleCommand("ddraw.text", invokeTime, color, endPos, drawX ? "X" : x[szAttacker]);
                    }
                }
                catch (Exception ex)
                {
                    allowPlayerExplosionMessages = false;
                    allowPlayerDrawing = false;
                    Puts("cmdPX Exception: {0} --- {1}", ex.Message, ex.StackTrace);
                    Puts("Player functionality disabled!");
                }

                if (flagged.Contains(player))
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    player.SendNetworkUpdateImmediate();
                    flagged.Remove(player);
                }
            }

            if (allowPlayerExplosionMessages)
            {
                if (attackers.Count > 0)
                    foreach (var kvp in attackers)
                        if (++shownExplosions < maxMessagesToPlayer)
                            player.ChatMessage(string.Format("<color=#{0}>{1}</color>", ColorUtility.ToHtmlStringRGB(attackersColor.ContainsKey(kvp.Key) ? attackersColor[kvp.Key] : Color.red), kvp.Value));
            }

            player.ChatMessage(msg("Explosions Listed", player.UserIDString, explosions.Count, playerDistance));
            colors.Clear();

            if (player.IsAdmin)
                return;

            var uid = player.UserIDString;

            limits.Add(uid);
            timer.Once(playerRestrictionTime, () => limits.Remove(uid));
        }

        private void cmdX(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < authLevel && !authorized.Contains(player.UserIDString))
            {
                player.ChatMessage(msg("Not Allowed", player.UserIDString));
                return;
            }

            if (args.Length > 0)
            {
                if (args.Any(arg => arg.Contains("del") || arg.Contains("wipe")))
                {
                    if (!allowManualWipe)
                    {
                        player.ChatMessage(msg("No Manual Wipe", player.UserIDString));
                        return;
                    }
                }

                switch (args[0].ToLower())
                {
                    case "wipe":
                        {
                            dataExplosions.Clear();
                            explosionsLogChanged = true;
                            SaveExplosionData();
                            player.ChatMessage(msg("Wiped", player.UserIDString));
                        }
                        return;
                    case "del":
                        {
                            if (args.Length == 2)
                            {
                                int deleted = dataExplosions.RemoveAll(x => (x.ContainsKey(szAttackerId) && x[szAttackerId] == args[1]) || (x.ContainsKey(szAttacker) && x[szAttacker] == args[1]));

                                if (deleted > 0)
                                {
                                    player.ChatMessage(msg("Removed", player.UserIDString, deleted, args[1]));
                                    explosionsLogChanged = true;
                                    SaveExplosionData();
                                }
                                else
                                    player.ChatMessage(msg("None Found", player.UserIDString, delRadius));
                            }
                            else
                                SendHelp(player);
                        }
                        return;
                    case "delm":
                        {
                            int deleted = dataExplosions.RemoveAll(x => Vector3.Distance(player.transform.position, GetPosition(x[szEndPositionId])) < delRadius);

                            if (deleted > 0)
                            {
                                player.ChatMessage(msg("RemovedM", player.UserIDString, deleted, delRadius));
                                explosionsLogChanged = true;
                                SaveExplosionData();
                            }
                            else
                                player.ChatMessage(msg("None In Radius", player.UserIDString, delRadius));
                        }
                        return;
                    case "delbefore":
                        {
                            if (args.Length == 2)
                            {
                                DateTime deleteDate;
                                if (DateTime.TryParse(args[1], out deleteDate))
                                {
                                    int deleted = dataExplosions.RemoveAll(x => x.ContainsKey(szLoggedDate) && DateTime.Parse(x[szLoggedDate]) < deleteDate);

                                    if (deleted > 0)
                                    {
                                        player.ChatMessage(msg("Removed Before", player.UserIDString, deleted, deleteDate.ToString()));
                                        explosionsLogChanged = true;
                                        SaveExplosionData();
                                    }
                                    else
                                        player.ChatMessage(msg("None Dated Before", player.UserIDString, deleteDate.ToString()));
                                }
                                else
                                    player.ChatMessage(msg("Invalid Date", player.UserIDString, szChatCommand, DateTime.UtcNow.ToString("yyyy-mm-dd HH:mm:ss")));
                            }
                            else
                                SendHelp(player);
                        }
                        return;
                    case "delafter":
                        {
                            if (args.Length == 2)
                            {
                                DateTime deleteDate;
                                if (DateTime.TryParse(args[1], out deleteDate))
                                {
                                    int deleted = dataExplosions.RemoveAll(x => x.ContainsKey(szLoggedDate) && DateTime.Parse(x[szLoggedDate]) > deleteDate);

                                    if (deleted > 0)
                                    {
                                        player.ChatMessage(msg("Removed After", player.UserIDString, deleted, deleteDate.ToString()));
                                        explosionsLogChanged = true;
                                        SaveExplosionData();
                                    }
                                    else
                                        player.ChatMessage(msg("None Dated After", player.UserIDString, deleteDate.ToString()));
                                }
                                else
                                    player.ChatMessage(msg("Invalid Date", player.UserIDString, szChatCommand, DateTime.UtcNow.ToString("yyyy-mm-dd HH:mm:ss")));
                            }
                            else
                                SendHelp(player);
                        }
                        return;
                    case "help":
                        {
                            SendHelp(player);
                        }
                        return;
                }
            }

            if (dataExplosions == null || dataExplosions.Count == 0)
            {
                player.ChatMessage(msg("None Logged", player.UserIDString));
                return;
            }

            var colors = new List<Color>();
            int distance = args.Length == 1 && IsNumeric(args[0]) ? int.Parse(args[0]) : defaultMaxDistance;
            var explosions = dataExplosions.FindAll(x => Vector3.Distance(player.transform.position, GetPosition(x[szEndPositionId])) <= distance);
            bool drawX = explosions.Count > maxNamedExplosions;
            int shownExplosions = 0;
            var attackers = new Dictionary<string, string>();

            if (showExplosionMessages && maxMessagesToPlayer > 0)
                foreach (var x in explosions)
                    if (!attackers.ContainsKey(x[szAttackerId]))
                        attackers.Add(x[szAttackerId], ParseExplosions(explosions.Where(ex => ex[szAttackerId] == x[szAttackerId]).ToList()));

            foreach (var x in explosions)
            {
                var startPos = GetPosition(x[szStartPositionId]);
                var endPos = GetPosition(x[szEndPositionId]);
                var endPosStr = string.Format("{0} {1} {2}", Math.Round(endPos.x, 2), Math.Round(endPos.y, 2), Math.Round(endPos.z, 2));

                if (colors.Count == 0)
                    colors = new List<Color>() { Color.blue, Color.cyan, Color.gray, Color.green, Color.magenta, Color.red, Color.yellow };

                var color = colorByWeaponType ? (x[szWeapon].Contains("Rocket") ? Color.red : x[szWeapon].Equals("C4") ? Color.yellow : Color.blue) : attackersColor.ContainsKey(x[szAttackerId]) ? attackersColor[x[szAttackerId]] : colors[UnityEngine.Random.Range(0, colors.Count - 1)];

                attackersColor[x[szAttackerId]] = color;

                if (colors.Contains(color))
                    colors.Remove(color);

                if (showConsoleMessages)
                {
                    var explosion = msg("Explosions", player.UserIDString, ColorUtility.ToHtmlStringRGB(color), x[szAttacker], x[szAttackerId], endPosStr, Math.Round(Vector3.Distance(startPos, endPos), 2), x[szWeapon], x[szEntitiesHit], x[szEntityHit]);
                    var victim = x.ContainsKey(szEntityOwner) ? string.Format(" - Victim: {0} ({1})", covalence.Players.FindPlayerById(x[szEntityOwner])?.Name ?? "Unknown", x[szEntityOwner]) : "";

                    player.ConsoleMessage(explosion + victim);
                }

                if (drawArrows && Vector3.Distance(startPos, endPos) > 1f)
                {
                    player.SendConsoleCommand("ddraw.arrow", invokeTime, color, startPos, endPos, 0.2);
                    player.SendConsoleCommand("ddraw.text", invokeTime, color, startPos, x[szWeapon].Substring(0, 1));
                }

                player.SendConsoleCommand("ddraw.text", invokeTime, color, endPos, drawX ? "X" : x[szAttacker]);
            }

            if (attackers.Count > 0)
                foreach (var kvp in attackers)
                    if (++shownExplosions < maxMessagesToPlayer)
                        player.ChatMessage(string.Format("<color=#{0}>{1}</color>", ColorUtility.ToHtmlStringRGB(attackersColor[kvp.Key]), kvp.Value));

            if (explosions.Count > 0)
                player.ChatMessage(msg("Explosions Listed", player.UserIDString, explosions.Count, distance));
            else
                player.ChatMessage(msg("None Found", player.UserIDString, distance));

            colors.Clear();
        }

        string ParseExplosions(List<Dictionary<string, string>> explosions)
        {
            if (explosions.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            var weapons = explosions.Select(x => x[szWeapon]).Distinct();

            foreach (var weapon in weapons)
            {
                var targets = explosions.Where(x => x[szWeapon] == weapon).Select(x => x[szEntityHit]).Distinct();

                sb.Append(string.Format("{0}: [", weapon));

                foreach (var target in targets)
                    sb.Append(string.Format("{0} ({1}), ", target, explosions.Where(x => x[szEntityHit] == target && x[szWeapon] == weapon).Count()));

                sb.Length = sb.Length - 2;
                sb.Append("], ");
            }

            sb.Length = sb.Length - 2;
            return string.Format("{0} used {1} explosives: {2}", explosions[0][szAttacker], explosions.Count, sb.ToString());
        }

        #region Config
        private bool Changed;
        private int authLevel { get; set; }
        private int defaultMaxDistance { get; set; }
        private int delRadius { get; set; }
        private int maxNamedExplosions { get; set; }
        private int invokeTime { get; set; }
        private bool showExplosionMessages { get; set; }
        private int maxMessagesToPlayer { get; set; }
        private static bool outputExplosionMessages { get; set; }
        private bool drawArrows { get; set; }
        private static bool showMillisecondsTaken { get; set; }
        private bool allowManualWipe { get; set; }
        private bool automateWipes { get; set; }
        private bool colorByWeaponType { get; set; }
        private bool applyInactiveChanges { get; set; }
        private bool applyActiveChanges { get; set; }
        private static bool sendDiscordNotifications { get; set; }
        private static bool sendSlackNotifications { get; set; }
        private static string slackMessageStyle { get; set; }
        private string szChatCommand { get; set; }
        private static List<object> authorized { get; set; }
        private static int daysBeforeDelete { get; set; }
        private bool trackF1 { get; set; }
        private bool trackBeancan { get; set; }
        private bool allowPlayerDrawing { get; set; }
        private string playerPerm { get; set; }
        private string szPlayerChatCommand { get; set; }
        private float playerDistance { get; set; }
        private bool allowPlayerExplosionMessages { get; set; }
        private float playerRestrictionTime { get; set; }
        bool showConsoleMessages { get; set; }

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Not Allowed"] = "You are not allowed to use this command.",
                ["Wiped"] = "Explosion data wiped",
                ["Removed"] = "Removed <color=orange>{0}</color> explosions for <color=orange>{1}</color>",
                ["RemovedM"] = "Removed <color=orange>{0}</color> explosions within <color=orange>{1}m</color>",
                ["Removed After"] = "Removed <color=orange>{0}</color> explosions logged after <color=orange>{1}</color>",
                ["Removed Before"] = "Removed <color=orange>{0}</color> explosions logged before <color=orange>{1}</color>",
                ["None Logged"] = "No explosions logged",
                ["None Deleted"] = "No explosions found within <color=orange>{0}m</color>",
                ["Explosions"] = "<color=#{0}>{1} ({2})</color> @ {3} ({4}m) [{5}] Entities Hit: {6} Entity Hit: {7}",
                ["Explosions Listed"] = "<color=orange>{0}</color> explosions listed within <color=orange>{1}m</color>.",
                ["None Found"] = "No explosions detected within <color=orange>{0}m</color>. Try specifying a larger range.",
                ["None In Radius"] = "No explosions found within the delete radius (<color=orange>{0}m</color>).",
                ["None Dated After"] = "No explosions dated after: {0}",
                ["None Dated Before"] = "No explosions dated before: {0}",
                ["No Manual Wipe"] = "Server owner has disabled manual wipe of explosion data.",
                ["Invalid Date"] = "Invalid date specified. Example: /{0} deldate \"{1}\"",
                ["Cannot Use From Server Console"] = "You cannot use this command from the server console.",
                ["Help Wipe"] = "Wipe all explosion data",
                ["Help Del Id"] = "Delete all explosions for <color=orange>ID</color>",
                ["Help Delm"] = "Delete all explosions within <color=orange>{0}m</color>",
                ["Help After"] = "Delete all explosions logged after the date specified. <color=orange>Example</color>: /{0} delafter \"{1}\"",
                ["Help Before"] = "Delete all explosions logged before the date specified. <color=orange>Example</color>: /{0} delbefore \"{1}\"",
                ["Help Distance"] = "Show explosions from <color=orange>X</color> distance",
                ["None Owned"] = "No explosions found near entities you own or have owned.",
                ["Showing Owned"] = "Showing list of explosions to entities which you own or have owned...",
            }, this);
        }

        void SendHelp(BasePlayer player)
        {
            player.ChatMessage(string.Format("/{0} wipe - {1}", szChatCommand, msg("Help Wipe", player.UserIDString)));
            player.ChatMessage(string.Format("/{0} del id - {1}", szChatCommand, msg("Help Del Id", player.UserIDString)));
            player.ChatMessage(string.Format("/{0} delm - {1}", szChatCommand, msg("Help Delm", player.UserIDString, delRadius)));
            player.ChatMessage(string.Format("/{0} delafter date - {1}", szChatCommand, msg("Help After", player.UserIDString, szChatCommand, DateTime.UtcNow.Subtract(new TimeSpan(daysBeforeDelete, 0, 0, 0)).ToString())));
            player.ChatMessage(string.Format("/{0} delbefore date - {1}", szChatCommand, msg("Help Before", player.UserIDString, szChatCommand, DateTime.UtcNow.ToString())));
            player.ChatMessage(string.Format("/{0} <distance> - {1}", szChatCommand, msg("Help Distance", player.UserIDString)));
        }

        void LoadVariables()
        {
            authorized = GetConfig("Settings", "Authorized List", new List<object>()) as List<object>;

            foreach (var auth in authorized.ToList())
            {
                ulong targetId;
                if (auth == null || !ulong.TryParse(auth.ToString(), out targetId) || !targetId.IsSteamId())
                {
                    PrintWarning("{0} is not a valid steam id. Entry removed.", auth == null ? "null" : auth);
                    authorized.Remove(auth);
                }
            }

            showConsoleMessages = Convert.ToBoolean(GetConfig("Settings", "Output Detailed Explosion Messages To Client Console", true));
            authLevel = authorized.Count == 0 ? Convert.ToInt32(GetConfig("Settings", "Auth Level", 1)) : int.MaxValue;
            defaultMaxDistance = Convert.ToInt32(GetConfig("Settings", "Show Explosions Within X Meters", 50));
            delRadius = Convert.ToInt32(GetConfig("Settings", "Delete Radius", 50));
            maxNamedExplosions = Convert.ToInt32(GetConfig("Settings", "Max Names To Draw", 25));
            invokeTime = Convert.ToInt32(GetConfig("Settings", "Time In Seconds To Draw Explosions", 60f));
            showExplosionMessages = Convert.ToBoolean(GetConfig("Settings", "Show Explosion Messages To Player", true));
            maxMessagesToPlayer = Convert.ToInt32(GetConfig("Settings", "Max Explosion Messages To Player", 10));
            outputExplosionMessages = Convert.ToBoolean(GetConfig("Settings", "Print Explosions To Server Console", true));
            drawArrows = Convert.ToBoolean(GetConfig("Settings", "Draw Arrows", true));
            showMillisecondsTaken = Convert.ToBoolean(GetConfig("Settings", "Print Milliseconds Taken To Track Explosives In Server Console", false));
            automateWipes = Convert.ToBoolean(GetConfig("Settings", "Wipe Data When Server Is Wiped (Recommended)", true));
            allowManualWipe = Convert.ToBoolean(GetConfig("Settings", "Allow Manual Deletions and Wipe of Explosion Data", true));
            colorByWeaponType = Convert.ToBoolean(GetConfig("Settings", "Color By Weapon Type Instead Of By Player Name", false));
            daysBeforeDelete = Convert.ToInt32(GetConfig("Settings", "Automatically Delete Each Explosion X Days After Being Logged", 0));
            applyInactiveChanges = Convert.ToBoolean(GetConfig("Settings", "Apply Retroactive Deletion Dates When No Date Is Set", true));
            applyActiveChanges = Convert.ToBoolean(GetConfig("Settings", "Apply Retroactive Deletion Dates When Days To Delete Is Changed", true));
            sendDiscordNotifications = Convert.ToBoolean(GetConfig("Discord", "Send Notifications", false));
            sendSlackNotifications = Convert.ToBoolean(GetConfig("Slack", "Send Notifications", false));
            slackMessageStyle = Convert.ToString(GetConfig("Slack", "Message Style (FancyMessage, SimpleMessage, Message, TicketMessage)", "FancyMessage"));
            szChatCommand = Convert.ToString(GetConfig("Settings", "Explosions Command", "x"));

            trackF1 = Convert.ToBoolean(GetConfig("Additional Tracking", "Track F1 Grenades", false));
            trackBeancan = Convert.ToBoolean(GetConfig("Additional Tracking", "Track Beancan Grenades", false));

            allowPlayerExplosionMessages = Convert.ToBoolean(GetConfig("Players", "Show Explosions", false));
            allowPlayerDrawing = Convert.ToBoolean(GetConfig("Players", "Allow DDRAW", false));
            playerPerm = Convert.ToString(GetConfig("Players", "Permission Name", "raidtracker.use"));
            szPlayerChatCommand = Convert.ToString(GetConfig("Players", "Command Name", "px"));
            playerDistance = Convert.ToSingle(GetConfig("Players", "Show Explosions Within X Meters", 50f));
            playerRestrictionTime = Convert.ToSingle(GetConfig("Players", "Limit Command Once Every X Seconds", 60f));

            if (playerRestrictionTime < 0f)
            {
                allowPlayerDrawing = false;
                allowPlayerExplosionMessages = false;
            }

            if ((allowPlayerExplosionMessages || allowPlayerDrawing) && !string.IsNullOrEmpty(szPlayerChatCommand) && !string.IsNullOrEmpty(playerPerm))
            {
                if (!permission.PermissionExists(playerPerm))
                    permission.RegisterPermission(playerPerm, this);
                
                cmd.AddChatCommand(szPlayerChatCommand, this, cmdPX);
            }

            if (sendDiscordNotifications && !Discord)
                PrintWarning("Discord not loaded correctly, please check your plugin directory for Discord.cs file");

            if (sendSlackNotifications && !Slack)
                PrintWarning("Slack not loaded correctly, please check your plugin directory for Slack.cs file");

            if (!string.IsNullOrEmpty(szChatCommand))
            {
                cmd.AddChatCommand(szChatCommand, this, cmdX);
                cmd.AddConsoleCommand(szChatCommand, this, "ccmdX");
            }

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

        string msg(string key, string id = null, params object[] args) => string.Format(id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id), args);
        string RemoveFormatting(string source) => source.Contains(">") ? System.Text.RegularExpressions.Regex.Replace(source, "<.*?>", string.Empty) : source;

        #endregion
    }
}