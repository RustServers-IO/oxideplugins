using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;
using Random = UnityEngine.Random;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Raid Tracker", "nivex", "1.0.1"), Description("Add tracking devices to explosives for detailed raid logging.")]
    public class RaidTracker : RustPlugin
    {
        [PluginReference] private Plugin Discord, Slack, DiscordMessages, PopupNotifications;

        private bool init;
        private bool wipeData;
        private static RaidTracker ins;
        private static bool explosionsLogChanged;
        private static List<Dictionary<string, string>> dataExplosions;
        private static readonly int layerMask = LayerMask.GetMask("Construction", "Deployed");

        private readonly Dictionary<string, Color> attackersColor = new Dictionary<string, Color>();

        private DynamicConfigFile explosionsFile;
        private readonly List<BasePlayer> flagged = new List<BasePlayer>();
        private readonly List<string> limits = new List<string>();

        public class Fields
        {
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }

            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
        }

        public class EntityInfo
        {
            public string ShortPrefabName { get; set; }
            public ulong OwnerID { get; set; }
            public uint NetworkID { get; set; }
            public Vector3 Position { get; set; }
            public float Health { get; set; }
        }

        public class TrackingDevice : MonoBehaviour
        {
            private int entitiesHit;
            private BaseEntity entity;
            private string entityHit;
            private ulong entityOwner;
            private double millisecondsTaken;
            private Vector3 position;
            private Dictionary<Vector3, EntityInfo> prefabs;
            private bool updated;
            private string weapon;

            public string playerName { get; set; }
            public string playerId { get; set; }
            public Vector3 playerPosition { get; set; }

            private void Awake()
            {
                prefabs = new Dictionary<Vector3, EntityInfo>();
                entity = GetComponent<BaseEntity>();
                weapon = entity.ShortPrefabName;
                position = entity.transform.position;
            }

            private void Update()
            {
                var newPosition = entity.transform.position;

                if (newPosition == position)
                    return;

                if (Vector3.Distance(newPosition, Vector3.zero) < 5f) // entity moved to vector3.zero
                    return;

                var tick = DateTime.Now;
                position = newPosition;

                var colliders = Pool.GetList<Collider>();
                Vis.Colliders(position, 1f, colliders, layerMask, QueryTriggerInteraction.Collide);

                if (colliders.Count > 0)
                {
                    foreach (var collider in colliders)
                    {
                        var e = collider.gameObject?.ToBaseEntity() ?? null;

                        if (e == null)
                            continue;

                        if (!prefabs.ContainsKey(e.transform.position))
                        {
                            prefabs.Add(e.transform.position, new EntityInfo
                            {
                                NetworkID = e.net.ID,
                                OwnerID = e.OwnerID,
                                Position = e.transform.position,
                                ShortPrefabName = e.ShortPrefabName,
                                Health = e.Health()
                            });

                            updated = true;
                        }
                    }
                }

                Pool.FreeList(ref colliders);
                millisecondsTaken += (DateTime.Now - tick).TotalMilliseconds;
            }

            private void CheckHealth()
            {
                if (Vector3.Distance(position, Vector3.zero) < 5f) // entity moved to vector3.zero
                    return;

                int count = 0;
                var tick = DateTime.Now;
                var colliders = Pool.GetList<Collider>();
                Vis.Colliders(position, 1f, colliders, layerMask, QueryTriggerInteraction.Collide);

                if (colliders.Count > 0)
                {
                    foreach (var collider in colliders)
                    {
                        var e = collider.gameObject?.ToBaseEntity() ?? null;

                        if (e == null)
                            continue;

                        if (prefabs.ContainsKey(e.transform.position) && prefabs[e.transform.position].Health != e.Health())
                        {
                            prefabs[e.transform.position].Health = e.Health();
                            entitiesHit++;
                        }

                        count++;
                    }
                }

                Pool.FreeList(ref colliders);
                entitiesHit += prefabs.Count - count;
                millisecondsTaken += (DateTime.Now - tick).TotalMilliseconds;
            }

            private void OnDestroy()
            {
                ins.NextTick(() => {
                    var tick = DateTime.Now;

                    if (prefabs.Count > 0)
                    {
                        CheckHealth();

                        var sorted = prefabs.ToList();
                        sorted.Sort((x, y) => Vector3.Distance(x.Key, playerPosition).CompareTo(Vector3.Distance(y.Key, playerPosition)));

                        //foreach (var kvp in sorted) Debug.Log(string.Format("{0} {1}", kvp.Value, kvp.Key));

                        entityHit = sorted[0].Value.ShortPrefabName;
                        entityHit = ItemManager.FindItemDefinition(entityHit)?.displayName?.english ?? entityHit;
                        entityOwner = sorted[0].Value.OwnerID;

                        prefabs.Clear();
                        sorted.Clear();
                    }

                    if (string.IsNullOrEmpty(entityHit))
                    {
                        Destroy(this);
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
                        ["Attacker"] = playerName,
                        ["AttackerId"] = playerId,
                        ["StartPositionId"] = GetPositionID(playerPosition),
                        ["EndPositionId"] = GetPositionID(position),
                        ["Weapon"] = weapon,
                        ["EntitiesHit"] = entitiesHit.ToString(),
                        ["EntityHit"] = entityHit,
                        ["DeleteDate"] = _daysBeforeDelete > 0 ? DateTime.UtcNow.AddDays(_daysBeforeDelete).ToString() : DateTime.MinValue.ToString(),
                        ["LoggedDate"] = DateTime.UtcNow.ToString(),
                        ["EntityOwner"] = entityOwner.ToString()
                    };

                    dataExplosions.Add(explosion);
                    explosionsLogChanged = true;

                    var endPosStr = string.Format("{0} {1} {2}", Math.Round(position.x, 2), Math.Round(position.y, 2), Math.Round(position.z, 2));
                    string victim = entityOwner > 0 ? ins.covalence.Players.FindPlayerById(entityOwner.ToString())?.Name ?? entityOwner.ToString() : "No owner";
                    string message = ins.msg("ExplosionMessage").Replace("{AttackerName}", playerName).Replace("{AttackerId}", playerId).Replace("{EndPos}", endPosStr).Replace("{Distance}", Vector3.Distance(position, playerPosition).ToString("N2")).Replace("{Weapon}", weapon).Replace("{EntityHit}", entityHit).Replace("{VictimName}", victim).Replace("{OwnerID}", entityOwner.ToString());

                    if (_outputExplosionMessages)
                        Debug.Log(message);

                    if (_sendDiscordNotifications)
                        ins.Discord?.Call("SendMessage", message);

                    if (_sendSlackNotifications)
                    {
                        switch (_slackMessageStyle.ToLower())
                        {
                            case "message":
                                ins.Slack?.Call(_slackMessageStyle, message, _slackChannel);
                                break;
                            default:
                                ins.Slack?.Call(_slackMessageStyle, message, ins.covalence.Players.FindPlayerById(playerId), _slackChannel);
                                break;
                        }
                    }

                    if (_sendDiscordMessages)
                        ins.DiscordMessage(playerName, playerId, message);

                    foreach (var target in BasePlayer.activePlayerList)
                    {
                        if (target != null && ins.permission.UserHasPermission(target.UserIDString, "raidtracker.see"))
                        {
                            message = ins.msg("ExplosionMessage", target.UserIDString).Replace("{AttackerName}", playerName).Replace("{AttackerId}", playerId).Replace("{EndPos}", endPosStr).Replace("{Distance}", Vector3.Distance(position, playerPosition).ToString("N2")).Replace("{Weapon}", weapon).Replace("{EntityHit}", entityHit).Replace("{VictimName}", victim).Replace("{OwnerID}", entityOwner.ToString());

                            if (usePopups && ins.PopupNotifications != null)
                                ins.PopupNotifications.Call("CreatePopupNotification", message, target, popupDuration);
                            else
                                target.ChatMessage(message);
                        }
                    }

                    millisecondsTaken += (DateTime.Now - tick).TotalMilliseconds;

                    if (_showMillisecondsTaken)
                        Debug.Log(string.Format("Took {0}ms for tracking device operations", millisecondsTaken));

                    Destroy(this);
                });
            }
        }

        private void OnServerSave()
        {
            SaveExplosionData();
        }

        private void OnNewSave(string filename)
        {
            wipeData = true;
        }

        private void Unload()
        {
            var objects = UnityEngine.Object.FindObjectsOfType(typeof(TrackingDevice));

            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);

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

        private void OnServerInitialized()
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

            if (wipeData && _automateWipes)
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
                    if (dict.ContainsKey("DeleteDate")) // apply retroactive changes
                    {
                        if (_applyInactiveChanges)
                        {
                            if (_daysBeforeDelete > 0 && dict["DeleteDate"] == DateTime.MinValue.ToString())
                            {
                                dict["DeleteDate"] = DateTime.UtcNow.AddDays(_daysBeforeDelete).ToString();
                                explosionsLogChanged = true;
                            }
                            if (_daysBeforeDelete == 0 && dict["DeleteDate"] != DateTime.MinValue.ToString())
                            {
                                dict["DeleteDate"] = DateTime.MinValue.ToString();
                                explosionsLogChanged = true;
                            }
                        }

                        if (_applyActiveChanges && _daysBeforeDelete > 0 && dict.ContainsKey("LoggedDate"))
                        {
                            var deleteDate = DateTime.Parse(dict["DeleteDate"]);
                            var loggedDate = DateTime.Parse(dict["LoggedDate"]);
                            int days = deleteDate.Subtract(loggedDate).Days;

                            if (days != _daysBeforeDelete)
                            {
                                int daysLeft = deleteDate.Subtract(DateTime.UtcNow).Days;

                                if (daysLeft > _daysBeforeDelete)
                                {
                                    dict["DeleteDate"] = loggedDate.AddDays(_daysBeforeDelete).ToString();
                                    explosionsLogChanged = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        dict.Add("DeleteDate", _daysBeforeDelete > 0 ? DateTime.UtcNow.AddDays(_daysBeforeDelete).ToString() : DateTime.MinValue.ToString());
                        explosionsLogChanged = true;
                    }
                }
            }

            init = true;
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!init || player == null || entity?.net == null)
                return;

            if (entity.ShortPrefabName.Contains("f1") && !_trackF1)
                return;
            if (entity.ShortPrefabName.Contains("beancan") && !_trackBeancan)
                return;
            if (entity.ShortPrefabName.Contains("grenade.smoke"))
                return;
            if (!_trackF1 && !_trackBeancan && !entity.name.Contains("explosive"))
                return;

            var tracker = entity.gameObject.AddComponent<TrackingDevice>();
            var position = player.transform.position;
            position.y += player.GetHeight() - 0.25f;

            tracker.playerName = player.displayName;
            tracker.playerId = player.UserIDString;
            tracker.playerPosition = position;
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (!init || player == null || entity?.net == null)
                return;

            var tracker = entity.gameObject.AddComponent<TrackingDevice>();
            var position = player.transform.position;
            position.y += player.GetHeight() - 0.25f;

            tracker.playerName = player.displayName;
            tracker.playerId = player.UserIDString;
            tracker.playerPosition = position;
        }

        private bool IsNumeric(string value)
        {
            return !string.IsNullOrEmpty(value) && value.All(char.IsDigit);
        }

        private static string GetPositionID(Vector3 pos)
        {
            return pos.x + " " + pos.y + " " + pos.z;
        }

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
            int expired = dataExplosions.RemoveAll(x => x.ContainsKey("DeleteDate") && x["DeleteDate"] != DateTime.MinValue.ToString() && DateTime.Parse(x["DeleteDate"]) < DateTime.UtcNow);

            if (expired > 0)
                explosionsLogChanged = true;

            if (explosionsLogChanged)
            {
                explosionsFile.WriteObject(dataExplosions);
                explosionsLogChanged = false;
            }
        }

        public void DiscordMessage(string name, string playerId, string text)
        {
            var fields = new List<Fields>();
            fields.Add(new Fields(msg("Embed_MessagePlayer"), $"[{name}](https://steamcommunity.com/profiles/{playerId})", true));
            fields.Add(new Fields(msg("Embed_MessageMessage"), text, false));
            string content = JsonConvert.SerializeObject(fields);
            DiscordMessages?.Call("API_SendFancyMessage", _webhookUrl, msg("Embed_MessageTitle"), _messageColor, content);
        }

        private void cmdPX(BasePlayer player, string command, string[] args)
        {
            if (!_allowPlayerExplosionMessages && !_allowPlayerDrawing)
                return;

            if (!permission.UserHasPermission(player.UserIDString, _playerPerm) && !player.IsAdmin)
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
            var explosions = dataExplosions.FindAll(x => x.ContainsKey("EntityOwner") && x["EntityOwner"] == player.UserIDString && x["AttackerId"] != player.UserIDString && Vector3.Distance(player.transform.position, GetPosition(x["EndPositionId"])) <= _playerDistance);

            if (explosions == null || explosions.Count == 0)
            {
                player.ChatMessage(msg("None Owned", player.UserIDString));
                return;
            }

            player.ChatMessage(msg("Showing Owned", player.UserIDString));

            bool drawX = explosions.Count > _maxNamedExplosions;
            int shownExplosions = 0;
            var attackers = new Dictionary<string, string>();

            if (_showExplosionMessages && _maxMessagesToPlayer > 0)
                foreach (var x in explosions)
                    if (!attackers.ContainsKey(x["AttackerId"]))
                        attackers.Add(x["AttackerId"], ParseExplosions(explosions.Where(ex => ex["AttackerId"] == x["AttackerId"]).ToList()));

            if (_allowPlayerDrawing)
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
                        var startPos = GetPosition(x["StartPositionId"]);
                        var endPos = GetPosition(x["EndPositionId"]);
                        var endPosStr = string.Format("{0} {1} {2}", Math.Round(endPos.x, 2), Math.Round(endPos.y, 2), Math.Round(endPos.z, 2));

                        if (colors.Count == 0)
                            colors = new List<Color>
                            {
                                Color.blue,
                                Color.cyan,
                                Color.gray,
                                Color.green,
                                Color.magenta,
                                Color.red,
                                Color.yellow
                            };

                        var color = _colorByWeaponType ? (x["Weapon"].Contains("Rocket") ? Color.red : x["Weapon"].Equals("C4") ? Color.yellow : Color.blue) : attackersColor.ContainsKey(x["AttackerId"]) ? attackersColor[x["AttackerId"]] : colors[Random.Range(0, colors.Count - 1)];

                        attackersColor[x["AttackerId"]] = color;

                        if (colors.Contains(color))
                            colors.Remove(color);

                        if (_showConsoleMessages)
                        {
                            var explosion = msg("Explosions", player.UserIDString, ColorUtility.ToHtmlStringRGB(attackersColor[x["AttackerId"]]), x["Attacker"], x["AttackerId"], endPosStr, Math.Round(Vector3.Distance(startPos, endPos), 2), x["Weapon"], x["EntitiesHit"], x["EntityHit"]);
                            var victim = x.ContainsKey("EntityOwner") ? string.Format(" - Victim: {0} ({1})", covalence.Players.FindPlayerById(x["EntityOwner"])?.Name ?? "Unknown", x["EntityOwner"]) : "";

                            player.ConsoleMessage(explosion + victim);
                        }

                        if (_drawArrows && Vector3.Distance(startPos, endPos) > 1f)
                        {
                            player.SendConsoleCommand("ddraw.arrow", _invokeTime, color, startPos, endPos, 0.2);
                            player.SendConsoleCommand("ddraw.text", _invokeTime, color, startPos, x["Weapon"].Substring(0, 1));
                        }

                        player.SendConsoleCommand("ddraw.text", _invokeTime, color, endPos, drawX ? "X" : x["Attacker"]);
                    }
                }
                catch (Exception ex)
                {
                    _allowPlayerExplosionMessages = false;
                    _allowPlayerDrawing = false;
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

            if (_allowPlayerExplosionMessages)
            {
                if (attackers.Count > 0)
                    foreach (var kvp in attackers)
                        if (++shownExplosions < _maxMessagesToPlayer)
                            player.ChatMessage(string.Format("<color=#{0}>{1}</color>", ColorUtility.ToHtmlStringRGB(attackersColor.ContainsKey(kvp.Key) ? attackersColor[kvp.Key] : Color.red), kvp.Value));
            }

            player.ChatMessage(msg("Explosions Listed", player.UserIDString, explosions.Count, _playerDistance));
            colors.Clear();

            if (player.IsAdmin)
                return;

            var uid = player.UserIDString;

            limits.Add(uid);
            timer.Once(_playerRestrictionTime, () => limits.Remove(uid));
        }

        private void cmdX(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < _authLevel && !_authorized.Contains(player.UserIDString))
            {
                player.ChatMessage(msg("Not Allowed", player.UserIDString));
                return;
            }

            if (args.Length > 0)
            {
                if (args.Any(arg => arg.Contains("del") || arg.Contains("wipe")))
                {
                    if (!_allowManualWipe)
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
                                int deleted = dataExplosions.RemoveAll(x => x.ContainsKey("AttackerId") && x["AttackerId"] == args[1] || x.ContainsKey("Attacker") && x["Attacker"] == args[1]);

                                if (deleted > 0)
                                {
                                    player.ChatMessage(msg("Removed", player.UserIDString, deleted, args[1]));
                                    explosionsLogChanged = true;
                                    SaveExplosionData();
                                }
                                else
                                    player.ChatMessage(msg("None Found", player.UserIDString, _delRadius));
                            }
                            else
                                SendHelp(player);
                        }
                        return;
                    case "delm":
                        {
                            int deleted = dataExplosions.RemoveAll(x => Vector3.Distance(player.transform.position, GetPosition(x["EndPositionId"])) < _delRadius);

                            if (deleted > 0)
                            {
                                player.ChatMessage(msg("RemovedM", player.UserIDString, deleted, _delRadius));
                                explosionsLogChanged = true;
                                SaveExplosionData();
                            }
                            else
                                player.ChatMessage(msg("None In Radius", player.UserIDString, _delRadius));
                        }
                        return;
                    case "delbefore":
                        {
                            if (args.Length == 2)
                            {
                                DateTime deleteDate;
                                if (DateTime.TryParse(args[1], out deleteDate))
                                {
                                    int deleted = dataExplosions.RemoveAll(x => x.ContainsKey("LoggedDate") && DateTime.Parse(x["LoggedDate"]) < deleteDate);

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
                                    player.ChatMessage(msg("Invalid Date", player.UserIDString, _chatCommand, DateTime.UtcNow.ToString("yyyy-mm-dd HH:mm:ss")));
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
                                    int deleted = dataExplosions.RemoveAll(x => x.ContainsKey("LoggedDate") && DateTime.Parse(x["LoggedDate"]) > deleteDate);

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
                                    player.ChatMessage(msg("Invalid Date", player.UserIDString, _chatCommand, DateTime.UtcNow.ToString("yyyy-mm-dd HH:mm:ss")));
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
            int distance = args.Length == 1 && IsNumeric(args[0]) ? int.Parse(args[0]) : _defaultMaxDistance;
            var explosions = dataExplosions.FindAll(x => Vector3.Distance(player.transform.position, GetPosition(x["EndPositionId"])) <= distance);
            bool drawX = explosions.Count > _maxNamedExplosions;
            int shownExplosions = 0;
            var attackers = new Dictionary<string, string>();

            if (_showExplosionMessages && _maxMessagesToPlayer > 0)
                foreach (var x in explosions)
                    if (!attackers.ContainsKey(x["AttackerId"]))
                        attackers.Add(x["AttackerId"], ParseExplosions(explosions.Where(ex => ex["AttackerId"] == x["AttackerId"]).ToList()));

            foreach (var x in explosions)
            {
                var startPos = GetPosition(x["StartPositionId"]);
                var endPos = GetPosition(x["EndPositionId"]);
                var endPosStr = string.Format("{0} {1} {2}", Math.Round(endPos.x, 2), Math.Round(endPos.y, 2), Math.Round(endPos.z, 2));

                if (colors.Count == 0)
                    colors = new List<Color>
                    {
                        Color.blue,
                        Color.cyan,
                        Color.gray,
                        Color.green,
                        Color.magenta,
                        Color.red,
                        Color.yellow
                    };

                var color = _colorByWeaponType ? (x["Weapon"].Contains("Rocket") ? Color.red : x["Weapon"].Equals("C4") ? Color.yellow : Color.blue) : attackersColor.ContainsKey(x["AttackerId"]) ? attackersColor[x["AttackerId"]] : colors[Random.Range(0, colors.Count - 1)];

                attackersColor[x["AttackerId"]] = color;

                if (colors.Contains(color))
                    colors.Remove(color);

                if (_showConsoleMessages)
                {
                    var explosion = msg("Explosions", player.UserIDString, ColorUtility.ToHtmlStringRGB(color), x["Attacker"], x["AttackerId"], endPosStr, Math.Round(Vector3.Distance(startPos, endPos), 2), x["Weapon"], x["EntitiesHit"], x["EntityHit"]);
                    var victim = x.ContainsKey("EntityOwner") ? string.Format(" - Victim: {0} ({1})", covalence.Players.FindPlayerById(x["EntityOwner"])?.Name ?? "Unknown", x["EntityOwner"]) : "";

                    player.ConsoleMessage(explosion + victim);
                }

                if (_drawArrows && Vector3.Distance(startPos, endPos) > 1f)
                {
                    player.SendConsoleCommand("ddraw.arrow", _invokeTime, color, startPos, endPos, 0.2);
                    player.SendConsoleCommand("ddraw.text", _invokeTime, color, startPos, x["Weapon"].Substring(0, 1));
                }

                player.SendConsoleCommand("ddraw.text", _invokeTime, color, endPos, drawX ? "X" : x["Attacker"]);
            }

            if (attackers.Count > 0)
                foreach (var kvp in attackers)
                    if (++shownExplosions < _maxMessagesToPlayer)
                        player.ChatMessage(string.Format("<color=#{0}>{1}</color>", ColorUtility.ToHtmlStringRGB(attackersColor[kvp.Key]), kvp.Value));

            if (explosions.Count > 0)
                player.ChatMessage(msg("Explosions Listed", player.UserIDString, explosions.Count, distance));
            else
                player.ChatMessage(msg("None Found", player.UserIDString, distance));

            colors.Clear();
        }

        private string ParseExplosions(List<Dictionary<string, string>> explosions)
        {
            if (explosions.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            var weapons = explosions.Select(x => x["Weapon"]).Distinct();

            foreach (var weapon in weapons)
            {
                var targets = explosions.Where(x => x["Weapon"] == weapon).Select(x => x["EntityHit"]).Distinct();

                sb.Append(string.Format("{0}: [", weapon));

                foreach (var target in targets)
                    sb.Append(string.Format("{0} ({1}), ", target, explosions.Count(x => x["EntityHit"] == target && x["Weapon"] == weapon)));

                sb.Length = sb.Length - 2;
                sb.Append("], ");
            }

            sb.Length = sb.Length - 2;
            return string.Format("{0} used {1} explosives: {2}", explosions[0]["Attacker"], explosions.Count, sb.ToString());
        }

        #region Config

        private bool _changed;
        private int _authLevel;
        private int _defaultMaxDistance;
        private int _delRadius;
        private int _maxNamedExplosions;
        private int _invokeTime;
        private bool _showExplosionMessages;
        private int _maxMessagesToPlayer;
        private static bool _outputExplosionMessages;
        private bool _drawArrows;
        private static bool _showMillisecondsTaken;
        private bool _allowManualWipe;
        private bool _automateWipes;
        private bool _colorByWeaponType;
        private bool _applyInactiveChanges;
        private bool _applyActiveChanges;
        private static bool _sendDiscordNotifications;
        private static bool _sendSlackNotifications;
        private static string _slackMessageStyle;
        private static string _slackChannel;
        private string _chatCommand;
        private static List<object> _authorized;
        private static int _daysBeforeDelete;
        private bool _trackF1;
        private bool _trackBeancan;
        private bool _allowPlayerDrawing;
        private string _playerPerm;
        private string _szPlayerChatCommand;
        private float _playerDistance;
        private bool _allowPlayerExplosionMessages;
        private float _playerRestrictionTime;
        private bool _showConsoleMessages;
        private int _messageColor;
        private static bool _sendDiscordMessages;
        private static string _webhookUrl;
        private static bool usePopups;
        private static float popupDuration;

        private void LoadMessages()
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
                ["Embed_MessageTitle"] = "Player Message",
                ["Embed_MessagePlayer"] = "Player",
                ["Embed_MessageMessage"] = "Message",
                ["ExplosionMessage"] = "[Explosion] {AttackerName} ({AttackerId}) @ {EndPos} ({Distance}m) {Weapon}: {EntityHit} - Victim: {VictimName} ({OwnerID})"
            }, this);
        }

        private void SendHelp(BasePlayer player)
        {
            player.ChatMessage(string.Format("/{0} wipe - {1}", _chatCommand, msg("Help Wipe", player.UserIDString)));
            player.ChatMessage(string.Format("/{0} del id - {1}", _chatCommand, msg("Help Del Id", player.UserIDString)));
            player.ChatMessage(string.Format("/{0} delm - {1}", _chatCommand, msg("Help Delm", player.UserIDString, _delRadius)));
            player.ChatMessage(string.Format("/{0} delafter date - {1}", _chatCommand, msg("Help After", player.UserIDString, _chatCommand, DateTime.UtcNow.Subtract(new TimeSpan(_daysBeforeDelete, 0, 0, 0)).ToString())));
            player.ChatMessage(string.Format("/{0} delbefore date - {1}", _chatCommand, msg("Help Before", player.UserIDString, _chatCommand, DateTime.UtcNow.ToString())));
            player.ChatMessage(string.Format("/{0} <distance> - {1}", _chatCommand, msg("Help Distance", player.UserIDString)));
        }

        private void LoadVariables()
        {
            _authorized = GetConfig("Settings", "Authorized List", new List<object>()) as List<object>;

            if (_authorized != null)
            {
                foreach (var auth in _authorized.ToList())
                {
                    if (auth == null || !auth.ToString().IsSteamId())
                    {
                        PrintWarning("{0} is not a valid steam id. Entry removed.", auth == null ? "null" : auth);
                        _authorized.Remove(auth);
                    }
                }
            }

            _showConsoleMessages = Convert.ToBoolean(GetConfig("Settings", "Output Detailed Explosion Messages To Client Console", true));
            _authLevel = _authorized?.Count == 0 ? Convert.ToInt32(GetConfig("Settings", "Auth Level", 1)) : int.MaxValue;
            _defaultMaxDistance = Convert.ToInt32(GetConfig("Settings", "Show Explosions Within X Meters", 50));
            _delRadius = Convert.ToInt32(GetConfig("Settings", "Delete Radius", 50));
            _maxNamedExplosions = Convert.ToInt32(GetConfig("Settings", "Max Names To Draw", 25));
            _invokeTime = Convert.ToInt32(GetConfig("Settings", "Time In Seconds To Draw Explosions", 60f));
            _showExplosionMessages = Convert.ToBoolean(GetConfig("Settings", "Show Explosion Messages To Player", true));
            _maxMessagesToPlayer = Convert.ToInt32(GetConfig("Settings", "Max Explosion Messages To Player", 10));
            _outputExplosionMessages = Convert.ToBoolean(GetConfig("Settings", "Print Explosions To Server Console", true));
            _drawArrows = Convert.ToBoolean(GetConfig("Settings", "Draw Arrows", true));
            _showMillisecondsTaken = Convert.ToBoolean(GetConfig("Settings", "Print Milliseconds Taken To Track Explosives In Server Console", false));
            _automateWipes = Convert.ToBoolean(GetConfig("Settings", "Wipe Data When Server Is Wiped (Recommended)", true));
            _allowManualWipe = Convert.ToBoolean(GetConfig("Settings", "Allow Manual Deletions and Wipe of Explosion Data", true));
            _colorByWeaponType = Convert.ToBoolean(GetConfig("Settings", "Color By Weapon Type Instead Of By Player Name", false));
            _daysBeforeDelete = Convert.ToInt32(GetConfig("Settings", "Automatically Delete Each Explosion X Days After Being Logged", 0));
            _applyInactiveChanges = Convert.ToBoolean(GetConfig("Settings", "Apply Retroactive Deletion Dates When No Date Is Set", true));
            _applyActiveChanges = Convert.ToBoolean(GetConfig("Settings", "Apply Retroactive Deletion Dates When Days To Delete Is Changed", true));
            _sendDiscordNotifications = Convert.ToBoolean(GetConfig("Discord", "Send Notifications", false));
            _sendSlackNotifications = Convert.ToBoolean(GetConfig("Slack", "Send Notifications", false));
            _slackMessageStyle = Convert.ToString(GetConfig("Slack", "Message Style (FancyMessage, SimpleMessage, Message, TicketMessage)", "FancyMessage"));
            _slackChannel = Convert.ToString(GetConfig("Slack", "Channel", "general"));
            _chatCommand = Convert.ToString(GetConfig("Settings", "Explosions Command", "x"));

            _trackF1 = Convert.ToBoolean(GetConfig("Additional Tracking", "Track F1 Grenades", false));
            _trackBeancan = Convert.ToBoolean(GetConfig("Additional Tracking", "Track Beancan Grenades", false));

            _allowPlayerExplosionMessages = Convert.ToBoolean(GetConfig("Players", "Show Explosions", false));
            _allowPlayerDrawing = Convert.ToBoolean(GetConfig("Players", "Allow DDRAW", false));
            _playerPerm = Convert.ToString(GetConfig("Players", "Permission Name", "raidtracker.use"));
            _szPlayerChatCommand = Convert.ToString(GetConfig("Players", "Command Name", "px"));
            _playerDistance = Convert.ToSingle(GetConfig("Players", "Show Explosions Within X Meters", 50f));
            _playerRestrictionTime = Convert.ToSingle(GetConfig("Players", "Limit Command Once Every X Seconds", 60f));

            _messageColor = Convert.ToInt32(GetConfig("DiscordMessages", "Message - Embed Color (DECIMAL)", 3329330));
            _webhookUrl = Convert.ToString(GetConfig("DiscordMessages", "Message - Webhook URL", "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks"));
            _sendDiscordMessages = _webhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            usePopups = Convert.ToBoolean(GetConfig("PopupNotifications", "Use Popups", false));
            popupDuration = Convert.ToSingle(GetConfig("PopupNotifications", "Duration", 0f));

            if (_playerRestrictionTime < 0f)
            {
                _allowPlayerDrawing = false;
                _allowPlayerExplosionMessages = false;
            }

            if ((_allowPlayerExplosionMessages || _allowPlayerDrawing) && !string.IsNullOrEmpty(_szPlayerChatCommand) && !string.IsNullOrEmpty(_playerPerm))
            {
                permission.RegisterPermission(_playerPerm, this);
                cmd.AddChatCommand(_szPlayerChatCommand, this, cmdPX);
            }

            permission.RegisterPermission("raidtracker.see", this);

            if (_sendDiscordNotifications && !Discord)
                PrintWarning("Discord not loaded correctly, please check your plugin directory for Discord.cs file");

            if (_sendSlackNotifications && !Slack)
                PrintWarning("Slack not loaded correctly, please check your plugin directory for Slack.cs file");

            if (_sendDiscordMessages && !DiscordMessages)
                PrintWarning("DiscordMessages not loaded correctly, please check your plugin directory for DiscordMessages.cs file");

            if (!string.IsNullOrEmpty(_chatCommand))
            {
                cmd.AddChatCommand(_chatCommand, this, cmdX);
                //cmd.AddConsoleCommand(szChatCommand, this, "ccmdX");
            }

            if (_changed)
            {
                SaveConfig();
                _changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                _changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                _changed = true;
            }
            return value;
        }

        public string msg(string key, string id = null, params object[] args)
        {
            string message = id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        public string RemoveFormatting(string source)
        {
            return source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;
        }

        #endregion
    }
}