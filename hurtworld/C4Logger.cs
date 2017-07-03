using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Assets.Scripts.Core;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("C4Logger", "austinv900", "1.2.1", ResourceId = 1795)]
    [Description("C4Logger saves informations about the use of C4.")]
    public class C4Logger : HurtworldPlugin
    {
        #region Enums

        internal enum ELogType
        {
            Info,
            Warning,
            Error
        }

        #endregion Enums

        #region Classes

        internal class Helpers
        {
            private readonly DynamicConfigFile _config;
            private readonly Action<ELogType, string> _log;
            private readonly Permission _permission;
            private readonly HurtworldPlugin _plugin;

            public Helpers(DynamicConfigFile config, HurtworldPlugin plugin, Permission permission,
                Action<ELogType, string> log)
            {
                _config = config;
                _plugin = plugin;
                _permission = permission;
                _log = log;
            }

            public string PermissionPrefix { get; set; }

            public void RegisterPermission(params string[] paramArray)
            {
                var perms = ArrayToString(paramArray, ".");
                _permission.RegisterPermission(
                    perms.StartsWith(PermissionPrefix) ? $"{perms}" : $"{PermissionPrefix}.{perms}",
                    _plugin);
            }

            public bool HasPermission(PlayerSession session, params string[] paramArray)
            {
                var perms = ArrayToString(paramArray, ".");
                return _permission.UserHasPermission(GetPlayerId(session),
                    perms.StartsWith(PermissionPrefix) ? $"{perms}" : $"{PermissionPrefix}.{perms}");
            }

            public void SetConfig(params object[] args)
            {
                var stringArgs = ObjectToStringArray(args.Take(args.Length - 1).ToArray());
                if (_config.Get(stringArgs) == null)
                {
                    _config.Set(args);
                }
            }

            public bool IsValidSession(PlayerSession session)
            {
                return session?.SteamId != null && session.IsLoaded && session.Name != null && session.Identity != null &&
                       session.WorldPlayerEntity?.transform?.position != null;
            }

            public int GetPlayerHotbarC4Amount(PlayerSession session)
            {
                var c4 = 0;
                var pInventory = session.WorldPlayerEntity?.GetComponent<PlayerInventory>();
                if (pInventory != null)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var item = pInventory.Items[i];
                        if (item?.Item != null && item.Item.ItemId == 144)
                        {
                            c4 += item.StackSize;
                        }
                    }
                }
                return c4;
            }

            public int GetPlayerC4Amount(PlayerSession session)
            {
                var c4 = 0;
                var pInventory = session?.WorldPlayerEntity?.GetComponent<PlayerInventory>();
                if (pInventory != null)
                {
                    for (int i = 0, l = pInventory.Capacity; i < l; i++)
                    {
                        var item = pInventory.Items[i];
                        if (item?.Item != null && item.Item.ItemId == 144)
                        {
                            c4 += item.StackSize;
                        }
                    }
                }
                return c4;
            }

            public int GetPlayerDetonatorCapAmount(PlayerSession session)
            {
                var dcap = 0;
                var pInventory = session?.WorldPlayerEntity?.GetComponent<PlayerInventory>();
                if (pInventory != null)
                {
                    for (int i = 0, l = pInventory.Capacity; i < l; i++)
                    {
                        var item = pInventory.Items[i];
                        if (item?.Item != null && item.Item.ItemId == 231)
                        {
                            dcap += item.StackSize;
                        }
                    }
                }
                return dcap;
            }

            public IEnumerable<PlayerIdentity> GetCellOwners(Vector3 position)
            {
                var cell = ConstructionManager.Instance.GetOwnerStake(position);

                if (cell == null || cell.AuthorizedPlayers == null) yield break;

                foreach (var auth in cell.AuthorizedPlayers) yield return auth;
            }

            public IEnumerable<OwnershipStakeServer> GetStakesFromPlayer(PlayerSession session)
            {
                var stakes = Resources.FindObjectsOfTypeAll<OwnershipStakeServer>();
                if (stakes == null) yield break;

                foreach(var stake in stakes)
                {
                    if (stake.IsDestroying && stake.gameObject == null && !stake.gameObject.activeSelf && !stake.AuthorizedPlayers.Contains(session.Identity)) yield break;

                    yield return stake;
                }
            }

            public Vector3 GetRaycastPosition(Vector3? position, Vector3? rotation)
            {
                if (position != null && rotation != null)
                {
                    RaycastHit rayHit;
                    if (Physics.Raycast((Vector3) position, (Vector3) rotation, out rayHit, 50))
                    {
                        return rayHit.point;
                    }
                }
                return Vector3.zero;
            }

            public T GetConfig<T>(T defaultVal, params object[] args)
            {
                var stringArgs = ObjectToStringArray(args);
                if (_config.Get(stringArgs) == null)
                {
                    _log(ELogType.Error,
                        $"Couldn't read from config file: {ArrayToString(stringArgs, "/")}");
                    return defaultVal;
                }
                return (T) Convert.ChangeType(_config.Get(stringArgs.ToArray()), typeof (T));
            }

            public string Vector3ToString(Vector3 v3, int decimals = 2, string separator = " ")
            {
                return
                    $"{Math.Round(v3.x, decimals)}{separator}{Math.Round(v3.y, decimals)}{separator}{Math.Round(v3.z, decimals)}";
            }

            public Vector3 StringToVector3(string v3)
            {
                var split = v3.Split(' ').Select(Convert.ToSingle).ToArray();
                return split.Length == 3 ? new Vector3(split[0], split[1], split[2]) : Vector3.zero;
            }

            public string[] ObjectToStringArray(object[] args)
            {
                return args.DefaultIfEmpty().Select(a => a.ToString()).ToArray();
            }

            public string GetPlayerId(PlayerSession session)
            {
                return session.SteamId.ToString();
            }

            public string ArrayToString(string[] array, string separator)
            {
                return string.Join(separator, array);
            }
        }

        public class C4Log
        {
            public C4Log()
            {
                Targets = new List<C4LogTarget>();
                PlayersNear = new List<C4LogPlayer>();
            }

            public DateTime Date { get; set; }
            public string Position { get; set; }
            public C4LogPlayer Player { get; set; }
            public List<C4LogTarget> Targets { get; set; }

            [JsonProperty("Players Near")]
            public List<C4LogPlayer> PlayersNear { get; set; }
        }

        public class C4LogTarget : C4LogPlayer
        {
            public bool Online { get; set; }
        }

        public class C4LogPlayer
        {
            public string Name { get; set; }
            public string SteamId { get; set; }
            public int C4 { get; set; }
            public string Position { get; set; }
            public float Distance { get; set; }
            public C4LogSolidarity Solidarity { get; set; }
        }

        public class C4LogSolidarity
        {
            public bool Clan { get; set; }
            public bool Stake { get; set; }
        }

        #endregion Classes

        #region Variables

        [PluginReference("HWClans")]
#pragma warning disable 649
            private Plugin _pluginClans;
#pragma warning restore 649

        private readonly List<C4Log> _c4Logs = new List<C4Log>();
        private Helpers _helpers;

        #endregion Variables

        #region Methods

        // ReSharper disable once UnusedMember.Local
        private void Loaded()
        {
            _helpers = new Helpers(Config, this, permission, Log)
            {
                PermissionPrefix = Regex.Replace(Title, "[^0-9a-zA-Z]+", string.Empty).ToLower()
            };

            LoadConfig();
            LoadPermissions();
            LoadData();
            LoadMessages();
        }

        internal void Log(ELogType type, string message)
        {
            switch (type)
            {
                case ELogType.Info:
                    Puts(message);
                    break;
                case ELogType.Warning:
                    PrintWarning(message);
                    break;
                case ELogType.Error:
                    PrintError(message);
                    break;
            }
        }

        // ReSharper disable once UnusedMember.Local
        private void OnPlayerInput(PlayerSession session, InputControls input)
        {
            if (!input.PrimaryTrigger)
            {
                return;
            }
            if (!_helpers.IsValidSession(session))
            {
                return;
            }
            var preC4Amount = _helpers.GetPlayerHotbarC4Amount(session);
            if (preC4Amount <= 0)
            {
                return;
            }
            var playerPosition = session.WorldPlayerEntity.transform.position;
            var playerRotation = session.WorldPlayerEntity.GetComponentInChildren<CamPosition>()?.transform?.rotation*
                                 Vector3.forward;
            timer.Once(0.25f, delegate
            {
                var postC4Amount = _helpers.GetPlayerHotbarC4Amount(session);
                if (postC4Amount >= preC4Amount)
                {
                    return;
                }
                var sessions = GameManager.Instance?.GetSessions()?.Values.Where(_helpers.IsValidSession).ToList();
                if (sessions == null)
                {
                    return;
                }

                var rayHit = _helpers.GetRaycastPosition(playerPosition, playerRotation);
                var c4Position = rayHit.Equals(Vector3.zero) ? playerPosition : rayHit;
                var owners = _helpers.GetCellOwners(c4Position);

                if (_helpers.GetConfig(false, "Settings", "Filters", "Self Harm", "Enabled"))
                {
                    if (owners.Any(o => o.SteamId.ToString().Equals(session.SteamId.ToString())))
                    {
                        Log(ELogType.Info, $"Filtered ({session.SteamId}) - Self Harm");
                        return;
                    }
                }

                if (_helpers.GetConfig(false, "Settings", "Filters", "Duplicate", "Enabled"))
                {
                    var duplicateDistance = _helpers.GetConfig(6, "Settings", "Filters", "Duplicate", "Max Distance");
                    var duplicateSeconds = _helpers.GetConfig(3, "Settings", "Filters", "Duplicate",
                        "Max Time (Seconds)");

                    foreach (var log in from log in _c4Logs.ToArray()
                        where log.Player.SteamId.Equals(session.SteamId.ToString())
                        where log.Date.ToUniversalTime().AddSeconds(duplicateSeconds) <= DateTime.Now.ToUniversalTime()
                        let p = _helpers.StringToVector3(log.Position)
                        where !p.Equals(Vector3.zero) && Vector3.Distance(p, c4Position) <= duplicateDistance
                        select log)
                    {
                        Log(ELogType.Info, $"Filtered ({session.SteamId}) - Duplicate");
                        _c4Logs.Remove(log);
                    }
                }

                var radius = _helpers.GetConfig(100f, "Settings", "Players Near Radius");
                var playersNear =
                    sessions.Where(
                        s =>
                            !s.SteamId.ToString().Equals(session.SteamId.ToString()) &&
                            Vector3.Distance(s.WorldPlayerEntity.transform.position,
                                playerPosition) <= radius)
                        .ToList();
                var playerClanTag = GetPlayerClanTag(session);
                var playerStakes = _helpers.GetStakesFromPlayer(session);

                var c4Log = new C4Log
                {
                    Date = DateTime.Now.ToUniversalTime(),
                    Position = _helpers.Vector3ToString(c4Position),
                    Player = new C4LogPlayer
                    {
                        SteamId = session.SteamId.ToString(),
                        Name = session.Name,
                        C4 = _helpers.GetPlayerC4Amount(session),
                        Position = _helpers.Vector3ToString(playerPosition),
                        Distance = (float) Math.Round(Vector3.Distance(playerPosition, c4Position), 2),
                        Solidarity = new C4LogSolidarity
                        {
                            Clan = playerClanTag != null,
                            Stake = true
                        }
                    },
                    PlayersNear = new List<C4LogPlayer>(),
                    Targets = new List<C4LogTarget>()
                };

                foreach (var player in playersNear)
                {
                    c4Log.PlayersNear.Add(new C4LogPlayer
                    {
                        SteamId = player.SteamId.ToString(),
                        Name = player.Name,
                        C4 = _helpers.GetPlayerC4Amount(player),
                        Position = _helpers.Vector3ToString(player.WorldPlayerEntity.transform.position),
                        Distance =
                            (float)
                                Math.Round(Vector3.Distance(player.WorldPlayerEntity.transform.position, c4Position), 2),
                        Solidarity = new C4LogSolidarity
                        {
                            Clan = playerClanTag != null && GetPlayerClanTag(player).Equals(playerClanTag),
                            Stake = playerStakes.Any(s => s.AuthorizedPlayers.Contains(player.Identity))
                        }
                    });
                }

                foreach (var target in owners)
                {
                    var targetSession =
                        sessions.FirstOrDefault(s => s.SteamId.ToString().Equals(target.SteamId.ToString()));
                    var c4LogTarget = new C4LogTarget
                    {
                        SteamId = target.SteamId.ToString(),
                        Name = target.Name,
                        C4 = targetSession != null ? _helpers.GetPlayerC4Amount(targetSession) : -1,
                        Solidarity = new C4LogSolidarity
                        {
                            Clan =
                                playerClanTag != null && targetSession != null &&
                                GetPlayerClanTag(targetSession).Equals(playerClanTag),
                            Stake = playerStakes.Any(s => s.AuthorizedPlayers.Contains(target))
                        }
                    };
                    if (targetSession != null)
                    {
                        c4LogTarget.Online = true;
                        c4LogTarget.Position =
                            _helpers.Vector3ToString(targetSession.WorldPlayerEntity.transform.position);
                        c4LogTarget.Distance =
                            (float)
                                Math.Round(
                                    Vector3.Distance(targetSession.WorldPlayerEntity.transform.position, c4Position), 2);
                    }
                    c4Log.Targets.Add(c4LogTarget);
                }

                c4Log.PlayersNear = c4Log.PlayersNear.OrderBy(p => p.Distance).ToList();
                c4Log.Targets = c4Log.Targets.OrderBy(t => t.Distance).ToList();

                _c4Logs.Add(c4Log);

                if (_helpers.GetConfig(false, "Settings", "Filters", "Defuse", "Enabled"))
                {
                    var seconds = _helpers.GetConfig(30f, "Settings", "Filters", "Defuse", "Max Time (Seconds)");
                    Timer defuseTimer = null;
                    defuseTimer = timer.Repeat(3, (int) Math.Ceiling(seconds/3), delegate
                    {
                        if (c4Log?.Player != null && _helpers.GetPlayerC4Amount(session) > c4Log.Player.C4)
                        {
                            Log(ELogType.Info, $"Filtered ({c4Log.Player.SteamId}) - Defuse");
                            _c4Logs.Remove(c4Log);
                            c4Log = null;
                            // ReSharper disable AccessToModifiedClosure
                            if (defuseTimer != null)
                            {
                                if (!defuseTimer.Destroyed)
                                {
                                    defuseTimer.Destroy();
                                }
                                defuseTimer = null;
                            }
                            // ReSharper restore AccessToModifiedClosure
                            SaveData();
                        }
                    });
                }

                SaveData();

                var message = lang.GetMessage("C4 - Used", this)
                    .Replace("{name}", session.Name)
                    .Replace("{position}", _helpers.Vector3ToString(c4Position));
                foreach (var s in sessions)
                {
                    if (_helpers.HasPermission(s, "used"))
                    {
                        hurt.SendChatMessage(s, message);
                    }
                }
                Log(ELogType.Info, message);
            });
        }

        protected override void LoadDefaultConfig()
        {
            Log(ELogType.Warning, "No config file found, generating a new one.");
        }

        private new void LoadConfig()
        {
            _helpers.SetConfig("Settings", new Dictionary<string, object>
            {
                {"Players Near Radius", 100f},
                {"Max Age (Days)", 7f},
                {
                    "Filters", new Dictionary<string, object>
                    {
                        {
                            "Self Harm", new Dictionary<string, object>
                            {
                                {"Enabled", true}
                            }
                        },
                        {
                            "Duplicate", new Dictionary<string, object>
                            {
                                {"Enabled", true},
                                {"Max Time (Seconds)", 3},
                                {"Max Distance", 6}
                            }
                        },
                        {
                            "Defuse", new Dictionary<string, object>
                            {
                                {"Enabled", true},
                                {"Max Time (Seconds)", 30}
                            }
                        }
                    }
                }
            });

            SaveConfig();
        }

        // ReSharper disable once UnusedMember.Local
        private void OnPlayerSpawn(PlayerSession session)
        {
            timer.Once(3, delegate { OnPlayerConnection(session, true); });
        }

        // ReSharper disable once UnusedMember.Local
        private void OnPlayerDisconnected(PlayerSession session)
        {
            OnPlayerConnection(session, false);
        }

        private void OnPlayerConnection(PlayerSession session, bool connected)
        {
            if (!_helpers.IsValidSession(session))
            {
                return;
            }

            var c4Amount = _helpers.GetPlayerC4Amount(session);
            var dCapAmount = _helpers.GetPlayerDetonatorCapAmount(session);

            if (c4Amount > 0 || dCapAmount > 0)
            {
                var sessions = GameManager.Instance?.GetSessions()?.Values.Where(_helpers.IsValidSession).ToList();
                if (sessions == null)
                {
                    return;
                }
                var message = lang.GetMessage("C4 - " + (connected ? "Connected" : "Disconnected"), this)
                    .Replace("{name}", session.Name)
                    .Replace("{c4Count}", c4Amount.ToString())
                    .Replace("{dCapCount}", dCapAmount.ToString());
                foreach (var s in sessions)
                {
                    if (_helpers.HasPermission(s, "connection"))
                    {
                        hurt.SendChatMessage(s, message);
                    }
                }
                Log(ELogType.Info, message);
            }
        }

        private void LoadPermissions()
        {
            _helpers.RegisterPermission("used");
            _helpers.RegisterPermission("connection");
        }

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"C4 - Used", "Player '{name}' used C4 at '{position}'."},
                {"C4 - Disconnected", "Player '{name}' disconnected with:\nC4: {c4Count}\nDetonator Cap: {dCapCount}"},
                {"C4 - Connected", "Player '{name}' connected with {c4Count} x C4 and {dCapCount} x Detonator Cap."}
            }, this);
        }

        private void LoadData()
        {
            var c4Logs = Interface.GetMod().DataFileSystem.ReadObject<List<C4Log>>("C4Logger");
            if (c4Logs != null)
            {
                var maxAge = _helpers.GetConfig(999f, "Settings", "Max Age (Days)");
                c4Logs.RemoveAll(c => DateTime.Now.ToUniversalTime().AddDays(-maxAge) > c.Date.ToUniversalTime());
                _c4Logs.AddRange(c4Logs);
            }
            SaveData();
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("C4Logger", _c4Logs);
        }

        private string GetPlayerClanTag(PlayerSession session)
        {
            try
            {
                if (_pluginClans != null && _pluginClans.IsLoaded)
                {
                    var clanTag = _pluginClans.Call("getClanTag", session);
                    if (clanTag != null)
                    {
                        return clanTag.ToString();
                    }
                }
            }
            catch
            {
                // Ignored
            }
            return null;
        }

        #endregion Methods
    }
}