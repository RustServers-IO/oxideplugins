using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Assets.Scripts.Core;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoRaidZones", "Swat1801", "1.3.0")]
    [Description("No Raid Zones protects defined zones from raids.")]
    public class NoRaidZones : HurtworldPlugin
    {
        #region Enums

        internal enum ELogType
        {
            Info,
            Warning,
            Error
        }

        #endregion Enums

        #region Variables

        private Helpers _helpers;
        private readonly List<C4Log> _c4Logs = new List<C4Log>();
        private readonly List<Zone> _zones = new List<Zone>();
        private readonly List<CommandMapping> _mappings = new List<CommandMapping>();
        private bool _autoFillStakes;
        private const float InputDelay = 0.25f;

        #endregion Variables

        #region Commands

        [ChatCommand("nrz")]
        // ReSharper disable once UnusedMember.Local
        // ReSharper disable once UnusedParameter.Local
        private void CommandNoRaidZone(PlayerSession session, string command, string[] args)
        {
            if (!_helpers.HasPermission(session, "use"))
            {
                hurt.SendChatMessage(session, null,
                    lang.GetMessage("Misc - No Permission", this, session.SteamId.ToString()));
                return;
            }
            if (args.Length == 0)
            {
                hurt.SendChatMessage(session, null,
                    lang.GetMessage("Misc - Help", this, session.SteamId.ToString()).Replace("{name}", "/nrz help"));
                return;
            }
            var mappings =
                _mappings.Where(m => m.Parameter.Equals(args[0], StringComparison.OrdinalIgnoreCase)).ToList();
            if (!mappings.Any())
            {
                hurt.SendChatMessage(session, null,
                    lang.GetMessage("Misc - Help", this, session.SteamId.ToString()).Replace("{name}", "/nrz help"));
                return;
            }
            var parameters = args.Length > 0 ? args.Skip(1).ToArray() : args;
            var mapping = mappings.FirstOrDefault(m => m.ArgsLength == parameters.Length);
            if (mapping == null)
            {
                hurt.SendChatMessage(session, null,
                    lang.GetMessage("Misc - Syntax", this, session.SteamId.ToString())
                        .Replace("{syntax}",
                            string.Join("\n",
                                mappings.Select(
                                    m =>
                                        $"/nrz {m.Parameter}{(!string.IsNullOrEmpty(m.Syntax) ? " " + m.Syntax : string.Empty)}")
                                    .ToArray())));
                return;
            }
            mapping.Callback?.Invoke(session, parameters);
        }

        private void CommandAdd(PlayerSession session, string[] args)
        {
            var name = args.First();
            var existZone =
                _zones.FirstOrDefault(
                    z =>
                        z.SteamId.Equals(session.SteamId.ToString()) &&
                        z.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existZone != null)
            {
                hurt.SendChatMessage(session, null,
                    lang.GetMessage("Zone - Exists", this, session.SteamId.ToString())
                        .Replace("{name}", existZone.Name));
                return;
            }
            var cell = Zone.GetCell(session.WorldPlayerEntity.transform.position);
            if (cell <= 0)
            {
                hurt.SendChatMessage(session, null, lang.GetMessage("Zone - Not Found", this, session.SteamId.ToString()));
                return;
            }
            if (!Zone.IsCellOwnedByPlayer(session.Identity, cell))
            {
                hurt.SendChatMessage(session, null, lang.GetMessage("Zone - Not Owned", this, session.SteamId.ToString()));
                return;
            }
            if (_zones.Any(z => z.AllCells.Contains(cell)))
            {
                hurt.SendChatMessage(session, null, lang.GetMessage("Zone - Already", this, session.SteamId.ToString()));
                return;
            }
            var zone = new Zone(session.SteamId.ToString(), name, cell);
            zone.Update(_helpers.GetConfig(false, "Settings", "Add Extra Cell Layer"));
            if (_autoFillStakes)
            {
                zone.FillStakes();
            }
            _zones.Add(zone);
            hurt.SendChatMessage(session, null,
                lang.GetMessage("Zone - Added", this, session.SteamId.ToString())
                    .Replace("{name}", zone.Name)
                    .Replace("{count}", zone.Bounds.Count.ToString()));
            SaveData();
        }


        private void CommandUpdate(PlayerSession session, string[] args)
        {
            var name = args.First();
            var zone =
                _zones.FirstOrDefault(
                    z =>
                        z.SteamId.Equals(session.SteamId.ToString()) &&
                        z.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (zone == null)
            {
                hurt.SendChatMessage(session, null,
                    lang.GetMessage("Zone - Unknown", this, session.SteamId.ToString()).Replace("{name}", name));
                return;
            }
            if (zone.MainCell <= 0)
            {
                hurt.SendChatMessage(session, null, lang.GetMessage("Zone - Not Found", this, session.SteamId.ToString()));
                return;
            }
            if (!Zone.IsCellOwnedByPlayer(session.Identity, zone.MainCell))
            {
                hurt.SendChatMessage(session, null,
                    lang.GetMessage("Zone - Auto Removed", this, session.SteamId.ToString())
                        .Replace("{name}", zone.Name));
                _zones.Remove(zone);
                Log(ELogType.Info, $"The zone {zone.Name} got automatically removed.");
                SaveData();
                return;
            }
            zone.Update(_helpers.GetConfig(false, "Settings", "Add Extra Cell Layer"));
            if (_autoFillStakes)
            {
                zone.FillStakes();
            }
            hurt.SendChatMessage(session, null,
                lang.GetMessage("Zone - Updated", this, session.SteamId.ToString())
                    .Replace("{name}", zone.Name)
                    .Replace("{count}", zone.Bounds.Count.ToString()));
        }

        private void CommandRemove(PlayerSession session, string[] args)
        {
            var name = args.First();
            var zone =
                _zones.FirstOrDefault(
                    z =>
                        z.SteamId.Equals(session.SteamId.ToString()) &&
                        z.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (zone == null)
            {
                hurt.SendChatMessage(session, null,
                    lang.GetMessage("Zone - Unknown", this, session.SteamId.ToString()).Replace("{name}", name));
                return;
            }
            _zones.Remove(zone);
            hurt.SendChatMessage(session, null,
                lang.GetMessage("Zone - Removed", this, session.SteamId.ToString()).Replace("{name}", zone.Name));
            SaveData();
        }

        private void CommandList(PlayerSession session, string[] args)
        {
            var zones = _zones.Where(z => z.SteamId.Equals(session.SteamId.ToString())).ToList();
            hurt.SendChatMessage(session, null,
                zones.Any()
                    ? lang.GetMessage("Zone - List", this, session.SteamId.ToString())
                        .Replace("{names}", string.Join(",", zones.Select(z => z.Name).ToArray()))
                    : lang.GetMessage("Zone - None", this, session.SteamId.ToString()));
        }

        private void CommandHelp(PlayerSession session, string[] args)
        {
            hurt.SendChatMessage(session, null,
                lang.GetMessage("Misc - Commands", this, session.SteamId.ToString())
                    .Replace("{commands}",
                        string.Join("\n",
                            _mappings.Select(
                                m =>
                                    $"/nrz {m.Parameter}{(!string.IsNullOrEmpty(m.Syntax) ? " " + m.Syntax : string.Empty)}")
                                .ToArray())));
        }

        #endregion Commands

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
                var perms = string.Join(".", paramArray);
                _permission.RegisterPermission(
                    perms.StartsWith(PermissionPrefix) ? $"{perms}" : $"{PermissionPrefix}.{perms}",
                    _plugin);
            }

            public bool HasPermission(PlayerSession session, params string[] paramArray)
            {
                var perms = string.Join(".", paramArray);
                return _permission.UserHasPermission(session.SteamId.ToString(),
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

            public T GetConfig<T>(T defaultVal, params object[] args)
            {
                var stringArgs = ObjectToStringArray(args);
                if (_config.Get(stringArgs) == null)
                {
                    _log(ELogType.Error,
                        $"Couldn't read from config file: {string.Join("/", stringArgs)}");
                    return defaultVal;
                }
                return (T)Convert.ChangeType(_config.Get(stringArgs.ToArray()), typeof(T));
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
            public int GetPlayerHotbarRaidDrillAmount(PlayerSession session)
            {
                var rdrill = 0;
                var pInventory = session.WorldPlayerEntity?.GetComponent<PlayerInventory>();
                if (pInventory != null)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var item = pInventory.Items[i];
                        if (item?.Item != null && item.Item.ItemId == 318)
                        {
                            rdrill += item.StackSize;
                        }
                    }
                }
                return rdrill;
            }

            public Vector3 GetRaycastPosition(Vector3? position, Vector3? rotation)
            {
                if (position != null && rotation != null)
                {
                    RaycastHit rayHit;
                    if (Physics.Raycast((Vector3)position, (Vector3)rotation, out rayHit, 50))
                    {
                        return rayHit.point;
                    }
                }
                return Vector3.zero;
            }

            public string[] ObjectToStringArray(object[] args)
            {
                return args.DefaultIfEmpty().Select(a => a.ToString()).ToArray();
            }

            public PlayerIdentity GetIdentityBySteamId(string steamId)
            {
                ulong uSteamId;
                if (ulong.TryParse(steamId, out uSteamId))
                {
                    return GameManager.Instance?.GetIdentity(uSteamId);
                }
                return null;
            }

            public PlayerSession GetSessionBySteamId(string steamId)
            {
                return GameManager.Instance?.GetSessions()?.Values.FirstOrDefault(
                    s => IsValidSession(s) && s.SteamId.ToString().Equals(steamId, StringComparison.OrdinalIgnoreCase));
            }
        }

        internal class CommandMapping
        {
            public CommandMapping(string parameter, int argsLength, Action<PlayerSession, string[]> callback)
            {
                Parameter = parameter;
                ArgsLength = argsLength;
                Callback = callback;
            }

            public string Parameter { get; set; }
            public int ArgsLength { get; set; }
            public Action<PlayerSession, string[]> Callback { get; set; }

            public string Syntax { get; set; }
        }

        internal class C4Log
        {
            public C4Log(PlayerSession session, Vector3 position, string type)
            {
                Session = session;
                Position = position;
                Date = DateTime.Now;
                Type = type;
            }

            public PlayerSession Session { get; }
            public Vector3 Position { get; }
            public DateTime Date { get; }
            public String Type { get; }
        }

        internal class Zone
        {
            public Zone(string steamId, string name, int mainCell)
            {
                SteamId = steamId;
                Name = name;
                MainCell = mainCell;
                Bounds = new HashSet<Bounds>();
                Stakes = new HashSet<OwnershipStakeServer>();
                AllCells = new HashSet<int>();
            }

            public string SteamId { get; set; }
            public string Name { get; set; }

            [JsonProperty("Cell")]
            public int MainCell { get; }

            [JsonIgnore]
            public HashSet<int> AllCells { get; }

            [JsonIgnore]
            public HashSet<Bounds> Bounds { get; }

            [JsonIgnore]
            public HashSet<OwnershipStakeServer> Stakes { get; }

            public void Update(bool extraSurrounding)
            {
                ulong steamId;
                if (SteamId == null || !ulong.TryParse(SteamId, out steamId))
                {
                    throw new InvalidDataException(
                        $"The Steam ID ({SteamId ?? "NULL"}) for the zone '{Name}' is invalid.");
                }
                var identity = GameManager.Instance.GetIdentity(steamId);
                if (identity == null)
                {
                    throw new InvalidDataException($"Couldn't find the Identity for {SteamId} in the zone '{Name}'.");
                }
                if (MainCell <= 0)
                {
                    throw new InvalidDataException(
                        $"The Cell ({SteamId ?? "NULL"}) for the zone '{Name}' is invalid.");
                }
                var cluster = BuildConnectedOwnedCellCluster(identity, MainCell);
                Stakes.Clear();
                foreach (var cell in cluster)
                {
                    var stake = GetStakeOnCell(cell);
                    if (stake != null)
                    {
                        Stakes.Add(stake);
                    }
                }
                if (extraSurrounding)
                {
                    var surroundingCells = GetUniqueSurroundingUnownedCells(cluster);
                    foreach (var surroundingCell in surroundingCells)
                    {
                        cluster.Add(surroundingCell);
                    }
                }
                Bounds.Clear();
                AllCells.Clear();
                foreach (var cell in cluster)
                {
                    Bounds.Add(GetCellBounds(cell));
                    AllCells.Add(cell);
                }
            }

            public void FillStakes()
            {
                foreach (var stake in Stakes)
                {
                    if (stake != null && !stake.IsDestroying && (stake.gameObject?.activeSelf ?? false))
                    {
                        var inventory = stake.GetComponentByInterface<IStorable>();
                        if (inventory?.Items != null)
                        {
                            foreach (var item in inventory.Items.Where(i => i?.Item != null))
                            {
                                item.StackSize = 5;
                            }
                        }
                    }
                }
            }

            private HashSet<int> BuildConnectedOwnedCellCluster(PlayerIdentity identity, int cell)
            {
                if (!IsCellOwnedByPlayer(identity, cell))
                {
                    return new HashSet<int>();
                }
                var queue = new Queue<int>();
                var result = new HashSet<int>();
                queue.Enqueue(cell);
                result.Add(cell);

                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    var surroundedCells = GetSurroundingCells(p).Where(c => IsCellOwnedByPlayer(identity, c));
                    foreach (var newCell in surroundedCells)
                    {
                        if (!result.Contains(newCell))
                        {
                            queue.Enqueue(newCell);
                            result.Add(newCell);
                        }
                    }
                }
                return result;
            }

            private Bounds GetCellBounds(int cell)
            {
                var middle = (ConstructionUtilities.GetOwnershipCellMin(cell) +
                              ConstructionUtilities.GetOwnershipCellMax(cell)) / 2f;
                return new Bounds(new Vector3(middle.x, 0, middle.y),
                    new Vector3(ConstructionUtilities.OWNERSHIP_GRID_SIZE, float.MaxValue,
                        ConstructionUtilities.OWNERSHIP_GRID_SIZE));
            }

            private HashSet<int> GetUniqueSurroundingUnownedCells(HashSet<int> cells)
            {
                var result = new HashSet<int>();
                foreach (var cell in cells)
                {
                    var surroundingCells = GetSurroundingCells(cell).Where(IsCellUnowned);
                    foreach (
                        var surroundingCell in surroundingCells.Where(c => !cells.Contains(c) && !result.Contains(c)))
                    {
                        result.Add(surroundingCell);
                    }
                }
                return result;
            }

            private HashSet<int> GetSurroundingCells(int cell)
            {
                var gridMax2 = ConstructionUtilities.OWNERSHIP_GRID_MAX * 2;
                return new HashSet<int>
                {
                    cell + 1,
                    cell - 1,
                    cell + gridMax2,
                    cell + gridMax2 + 1,
                    cell + gridMax2 - 1,
                    cell - gridMax2,
                    cell - gridMax2 + 1,
                    cell - gridMax2 - 1
                };
            }

            private bool IsCellUnowned(int cell)
            {
                return GetStakeOnCell(cell) == null;
            }

            public static bool IsCellOwnedByPlayer(PlayerIdentity identity, int cell)
            {
                var stake = GetStakeOnCell(cell);
                return stake != null && stake.AuthorizedPlayers.Contains(identity);
            }

            private static OwnershipStakeServer GetStakeOnCell(int cell)
            {
                OwnershipStakeServer stake;
                return ConstructionManager.Instance.OwnershipCells.TryGetValue(cell, out stake) && !stake.IsDestroying &&
                       (stake.gameObject?.activeSelf ?? false)
                    ? stake
                    : null;
            }

            public static int GetCell(Vector3 position)
            {
                return ConstructionUtilities.GetOwnershipCell(position);
            }
        }

        #endregion Classes

        #region Methods

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
        private void Loaded()
        {
            _helpers = new Helpers(Config, this, permission, Log)
            {
                PermissionPrefix = Regex.Replace(Title, "[^0-9a-zA-Z]+", string.Empty).ToLower()
            };

            _mappings.AddRange(new List<CommandMapping>
            {
                new CommandMapping("add", 1, CommandAdd) {Syntax = "<zone>"},
                new CommandMapping("remove", 1, CommandRemove) {Syntax = "<zone>"},
                new CommandMapping("update", 1, CommandUpdate) {Syntax = "<zone>"},
                new CommandMapping("list", 0, CommandList),
                new CommandMapping("help", 0, CommandHelp)
            });

            LoadConfig();
            LoadPermissions();
            LoadData();
            LoadMessages();

            _autoFillStakes = _helpers.GetConfig(false, "Settings", "Auto Fill Stakes");

            if (_helpers.GetConfig(false, "Settings", "Show Notification"))
            {
                timer.Every(10, OnShortPulse);
            }
            if (_autoFillStakes || _helpers.GetConfig(false, "Settings", "Refund C4"))
            {
                timer.Every(3600, OnLongPulse);
            }
        }

        private void OnShortPulse()
        {
            if (!_zones.Any())
            {
                return;
            }
            var sessions = GameManager.Instance?.GetSessions()?.Values.Where(s => _helpers.IsValidSession(s));
            if (sessions != null)
            {
                var notification = lang.GetMessage("Zone - Notification", this);
                foreach (var session in sessions)
                {
                    if (
                        _zones.Any(
                            z =>
                                z.SteamId != session.SteamId.ToString() &&
                                z.Bounds.Any(b => b.Contains(session.WorldPlayerEntity.transform.position))))
                    {
                        AlertManager.Instance?.GenericTextNotificationServer(notification, session.Player);
                    }
                }
            }
        }

        private void OnLongPulse()
        {
            foreach (var zone in _zones.ToArray())
            {
                if (_autoFillStakes)
                {
                    zone.FillStakes();
                }
                var identity = _helpers.GetIdentityBySteamId(zone.SteamId);
                if (identity == null ||
                    !Zone.IsCellOwnedByPlayer(_helpers.GetIdentityBySteamId(zone.SteamId), zone.MainCell))
                {
                    var session = _helpers.GetSessionBySteamId(zone.SteamId);
                    if (session != null)
                    {
                        hurt.SendChatMessage(session, null,
                            lang.GetMessage("Zone - Auto Removed", this, session.SteamId.ToString())
                                .Replace("{name}", zone.Name));
                    }
                    _zones.Remove(zone);
                    Log(ELogType.Info, $"The zone {zone.Name} got automatically removed.");
                }
            }
            _c4Logs.RemoveAll(c => (DateTime.Now - c.Date).TotalSeconds > 60);
            SaveData();
        }

        protected override void LoadDefaultConfig()
        {
            Log(ELogType.Warning, "No config file found, generating a new one.");
        }

        private new void LoadConfig()
        {
            _helpers.SetConfig("Settings", new Dictionary<string, object>
            {
                {"Auto Fill Stakes", true},
                {"Add Extra Cell Layer", true},
                {"Show Notification", false},
                {"Refund C4", true}
            });

            SaveConfig();
        }

        private void LoadPermissions()
        {
            _helpers.RegisterPermission("use");
        }

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Misc - No Permission", "You don't have the permission to use this command."},
                {"Misc - Help", "Unknown command, type '{name}' to list all available commands."},
                {"Misc - Syntax", "Syntax: {syntax}"},
                {"Misc - Commands", "Commands: \n{commands}"},
                {"Zone - Not Found", "Couldn't find the cell."},
                {"Zone - Not Owned", "You don't own this cell."},
                {"Zone - Auto Removed", "You don't own the cell for '{name}' anymore and therefore got removed."},
                {"Zone - Already", "This cell is already part of another zone."},
                {"Zone - Added", "You have created the zone '{name}'. Its {count} cells big."},
                {"Zone - Removed", "You have removed the zone '{name}'."},
                {"Zone - Unknown", "The zone '{name}' couldn't be found."},
                {"Zone - List", "Zones:\n{names}"},
                {"Zone - Exists", "You already have a zone named '{name}'."},
                {"Zone - None", "You don't have any zones yet."},
                {"Zone - Updated", "You have updated the zone '{name}'. Its now {count} cells big."},
                {"Zone - Raid", "The player '{player}' tried to raid '{name}'."},
                {"Zone - Notification", "No Raid Zone"}
            }, this);
        }

        private void LoadData()
        {
            var zones = Interface.GetMod().DataFileSystem.ReadObject<List<Zone>>("NoRaidZones");
            if (zones != null)
            {
                _zones.AddRange(zones);
            }
            SaveData();
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("NoRaidZones", _zones);
        }

        // ReSharper disable UnusedMember.Local
        private void OnServerInitialized()
        {
            foreach (var zone in _zones.ToArray())
            {
                var identity = _helpers.GetIdentityBySteamId(zone.SteamId);
                if (identity == null || zone.MainCell <= 0 || !Zone.IsCellOwnedByPlayer(identity, zone.MainCell))
                {
                    _zones.Remove(zone);
                    Log(ELogType.Info, $"The zone {zone.Name} got automatically removed.");
                    continue;
                }
                zone.Update(_helpers.GetConfig(false, "Settings", "Add Extra Cell Layer"));
                if (_autoFillStakes)
                {
                    zone.FillStakes();
                }
            }
            SaveData();
        }

        private void OnEntitySpawned(NetworkViewData entity)
        {
            if (entity?.NetworkView == null || (entity.PrefabId != 20 && entity.PrefabId != 65))
            {
                return;
            }
            if (!_zones.Any(z => z.Bounds.Any(b => b.Contains(entity.InitialPosition))))
            {
                return;
            }
            Singleton<HNetworkManager>.Instance.NetDestroy(entity.NetworkView);
            if (entity.PrefabId == 20 && _helpers.GetConfig(true, "Settings", "Refund C4"))
            {
                timer.Once(InputDelay * 2.5f, delegate
                {
                    var c4Log =
                        _c4Logs.Where(
                            c =>
                                c.Type == "c4" && c.Session?.WorldPlayerEntity != null && (DateTime.Now - c.Date).TotalSeconds < 5 &&
                                Vector3.Distance(entity.InitialPosition, c.Position) < 20f)
                            .OrderBy(c => Vector3.Distance(entity.InitialPosition, c.Position)).FirstOrDefault();
                    if (c4Log != null)
                    {
                        AlertManager.Instance?.GenericTextNotificationServer(
                            lang.GetMessage("Zone - Notification", this, c4Log.Session.SteamId.ToString()),
                            c4Log.Session.Player);
                        var inventory = c4Log.Session.WorldPlayerEntity?.GetComponent<PlayerInventory>();
                        if (inventory != null)
                        {
                            Singleton<GlobalItemManager>.Instance?.GiveItem(EItemCode.C4Explosive, 1, inventory);
                        }
                        _c4Logs.Remove(c4Log);
                    }
                });
            }
            else if (entity.PrefabId == 65 && _helpers.GetConfig(true, "Settings", "Refund C4"))
            {
                timer.Once(InputDelay * 2.5f, delegate
                {
                    var c4Log =
                        _c4Logs.Where(
                            c =>
                                c.Type == "raiddrill" && c.Session?.WorldPlayerEntity != null && (DateTime.Now - c.Date).TotalSeconds < 5 &&
                                Vector3.Distance(entity.InitialPosition, c.Position) < 20f)
                            .OrderBy(c => Vector3.Distance(entity.InitialPosition, c.Position)).FirstOrDefault();
                    if (c4Log != null)
                    {
                        AlertManager.Instance?.GenericTextNotificationServer(
                            lang.GetMessage("Zone - Notification", this, c4Log.Session.SteamId.ToString()),
                            c4Log.Session.Player);
                        var inventory = c4Log.Session.WorldPlayerEntity?.GetComponent<PlayerInventory>();
                        if (inventory != null)
                        {
                            Singleton<GlobalItemManager>.Instance?.GiveItem(EItemCode.RaidDrill, 1, inventory);
                        }
                        _c4Logs.Remove(c4Log);
                    }
                });
            }
        }

        private void OnPlayerInput(PlayerSession session, InputControls input)
        {
            if (!input.PrimaryTrigger)
            {
                return;
            }
            if (!_helpers.IsValidSession(session) || !_zones.Any() || !_helpers.GetConfig(true, "Settings", "Refund C4"))
            {
                return;
            }
            var preC4Amount = _helpers.GetPlayerHotbarC4Amount(session);
            var preRaidDrillAmount = _helpers.GetPlayerHotbarRaidDrillAmount(session);
            if (preC4Amount <= 0 && preRaidDrillAmount <= 0)
            {
                return;
            }
            if (
                _c4Logs.Any(
                    c =>
                        c.Session != null && c.Session.SteamId.ToString().Equals(session.SteamId.ToString()) &&
                        (DateTime.Now - c.Date).TotalSeconds < 2))
            {
                return;
            }
            var playerPosition = session.WorldPlayerEntity.transform.position;
            var playerRotation = session.WorldPlayerEntity.GetComponentInChildren<CamPosition>()?.transform?.rotation *
                                 Vector3.forward;
            timer.Once(InputDelay, delegate
            {
                var postC4Amount = _helpers.GetPlayerHotbarC4Amount(session);
                var postRaidDrillAmount = _helpers.GetPlayerHotbarRaidDrillAmount(session);

                string type = preC4Amount > postC4Amount ? "c4" : preRaidDrillAmount > postRaidDrillAmount ? "raiddrill" : "none";
                
                if (type == "none")
                {
                    return;
                }
                var rayHit = _helpers.GetRaycastPosition(playerPosition, playerRotation);
                var position = rayHit.Equals(Vector3.zero) ? playerPosition : rayHit;
                if (_zones.Any(z => z.Bounds.Any(b => b.Contains(position))))
                {
                    _c4Logs.Add(new C4Log(session, position, type));
                }
            });
        }

        #endregion Methods
    }
}