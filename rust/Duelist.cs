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
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Duelist", "nivex", "0.1.23", ResourceId = 2520)]
    [Description("1v1 & TDM dueling event.")]
    class Duelist : RustPlugin
    {
        [PluginReference]
        Plugin Kits, ZoneManager, Economics, ServerRewards, LustyMap, Backpacks;

        readonly static string hewwPrefab = "assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab";
        readonly static string heswPrefab = "assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab";
        static Duelist ins;
        bool init = false; // are we initialized properly? if not disable certain functionality
        bool resetDuelists = false; // if wipe is detected then assign awards and wipe VictoriesSeed / LossesSeed

        static List<string> lustyMarkers = new List<string>();
        static Dictionary<string, AttackerInfo> tdmAttackers = new Dictionary<string, AttackerInfo>();
        static Dictionary<string, string> tdmKits = new Dictionary<string, string>();
        static HashSet<GoodVersusEvilMatch> tdmMatches = new HashSet<GoodVersusEvilMatch>();
        Dictionary<string, string> tdmRequests = new Dictionary<string, string>(); // users requesting a deathmatch and to whom
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

        readonly float differential = 15f;
        readonly int worldMask = LayerMask.GetMask("World");
        readonly int waterMask = LayerMask.GetMask("Water"); // used to count water colliders when finding a random dueling zone on the map
        readonly int groundMask = LayerMask.GetMask("Terrain", "World", "Default"); // used to find dueling zone/set custom zone and create spawn points
        readonly int constructionMask = LayerMask.GetMask("Construction", "Deployed");
        readonly int wallMask = LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Deployed");
        readonly int blockedMask = LayerMask.GetMask("Player (Server)", "Prevent Building", "Construction", "Deployed", "Trigger"); // layers we won't be setting a zone within 50 meters of

        SortedDictionary<string, string> boneTags = new SortedDictionary<string, string> { ["r_"] = "Right ", ["l_"] = "Left ", [".prefab"] = "", ["1"] = "", ["2"] = "", ["3"] = "", ["4"] = "", ["END"] = "", ["_"] = " ", ["."] = " ", };
        Dictionary<string, string> dataRequests = new Dictionary<string, string>(); // users requesting a duel and to whom
        static Dictionary<string, string> dataDuelists = new Dictionary<string, string>(); // active duelers
        static Dictionary<string, long> dataImmunity = new Dictionary<string, long>(); // players immune to damage
        static Dictionary<string, Vector3> dataImmunitySpawns = new Dictionary<string, Vector3>(); // players immune to damage
        Dictionary<string, long> dataDeath = new Dictionary<string, long>(); // users id and timestamp of when they're to be executed

        public class StoredData // don't edit this section or the datafile // TODO: move temporary tables outside of this class
        {
            public List<string> Allowed = new List<string>(); // list of users that allow duel requests
            public List<string> Restricted = new List<string>(); // list of users blocked from requesting a duel for 60 seconds
            public SortedDictionary<long, string> Queued = new SortedDictionary<long, string>(); // queued duelers sorted by timestamp and user id. first come first serve
            public Dictionary<string, string> Bans = new Dictionary<string, string>(); // users banned from dueling

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
            //public Dictionary<string, MatchInfo> Rematch = new Dictionary<string, MatchInfo>();

            public Dictionary<string, float> Zones = new Dictionary<string, float>(); // custom zone id / radius
            public List<string> ZoneIds = new List<string>(); // the locations of each dueling zone
            public List<string> Spawns = new List<string>(); // custom spawn points
            public Dictionary<string, List<string>> AutoGeneratedSpawns = new Dictionary<string, List<string>>();

            /*
            public Dictionary<string, int> MatchDeaths = new Dictionary<string, int>(); // player name & total deaths
            public Dictionary<string, int> MatchDeathsSeed = new Dictionary<string, int>(); // player name & deaths for the seed
            public Dictionary<string, int> MatchKills = new Dictionary<string, int>(); // player name & total kills
            public Dictionary<string, int> MatchKillsSeed = new Dictionary<string, int>(); // player name & kills for current seed
            public Dictionary<string, int> MatchVictories = new Dictionary<string, int>(); // player name & total wins
            public Dictionary<string, int> MatchVictoriesSeed = new Dictionary<string, int>(); // player name & wins for current seed
            public Dictionary<string, int> MatchLosses = new Dictionary<string, int>(); // player name & total losses
            public Dictionary<string, int> MatchLossesSeed = new Dictionary<string, int>(); // player name & losses for current seed
            */

            public int TotalDuels = 0; // the total amount of duels ever played on the server
            public bool DuelsEnabled = false; // enable/disable dueling for all players (not admins)
            public float Radius = 0f; // the radius of the dueling zone

            public StoredData() { }
        }

        /*public class MatchInfo
        {
            public int TeamSize { get; set; } = 2;
            public bool Public { get; set; } = false;
            public List<ulong> Rematch { get; set; } = new List<ulong>();
            public PlayerInfo() { }
        }*/

        public class AttackerInfo
        {
            public string AttackerName { get; set; } = "";
            public string BoneName { get; set; } = "";
            public string Distance { get; set; } = "";
            public string Weapon { get; set; } = "";

            public AttackerInfo() { }
        }

        public enum Team
        {
            Good = 0,
            Evil = 1,
            None = 2,
        }

        public class GoodVersusEvilMatch
        {
            private string _goodHostName { get; set; } = "";
            private string _evilHostName { get; set; } = "";
            private string _goodHostId { get; set; } = "";
            private string _evilHostId { get; set; } = "";
            private HashSet<BasePlayer> _good = new HashSet<BasePlayer>();
            private HashSet<BasePlayer> _evil = new HashSet<BasePlayer>();
            private HashSet<ulong> _goodKIA = new HashSet<ulong>();
            private HashSet<ulong> _evilKIA = new HashSet<ulong>();
            private HashSet<ulong> _banned = new HashSet<ulong>();
            private string _goodCode { get; set; } = "";
            private string _evilCode { get; set; } = "";
            private int _teamSize { get; set; } = 2;
            private bool _started { get; set; } = false;
            private bool _ended { get; set; } = false;
            private string _kit { get; set; } = "";
            private DuelingZone _zone { get; set; }
            private Timer _queueTimer { get; set; }
            private bool _enteredQueue { get; set; } = false;
            private bool _public { get; set; } = false;

            public void Setup(BasePlayer player, BasePlayer target)
            {
                _goodHostName = player.displayName;
                _goodHostId = player.UserIDString;
                _evilHostName = target.displayName;
                _evilHostId = target.UserIDString;
                _goodCode = UnityEngine.Random.Range(10000, 99999).ToString();
                _evilCode = UnityEngine.Random.Range(10000, 99999).ToString();

                if (minDeathmatchSize > _teamSize)
                    _teamSize = minDeathmatchSize;

                AddToGoodTeam(player);
                AddToEvilTeam(target);

                if (tdmKits.ContainsKey(player.UserIDString))
                {
                    Kit = tdmKits[player.UserIDString];
                    tdmKits.Remove(player.UserIDString);
                }
                else if (tdmKits.ContainsKey(target.UserIDString))
                {
                    Kit = tdmKits[target.UserIDString];
                    tdmKits.Remove(target.UserIDString);
                }
                else
                    Kit = ins.GetRandomKit();
                
                if (TeamSize > 1)
                {
                    player.ChatMessage(ins.msg("MatchOpened", player.UserIDString, ins.szMatchChatCommand, _goodCode));
                    target.ChatMessage(ins.msg("MatchOpened", target.UserIDString, ins.szMatchChatCommand, _evilCode));
                }

                ins.UpdateMatchUI();
            }

            public string Id
            {
                get
                {
                    return _goodHostId + _evilHostId;
                }
            }

            public string Versus
            {
                get
                {
                    return string.Format("{0} / {1} {2}v{2}", _goodHostName, _evilHostName, _teamSize);
                }
            }

            public bool IsPublic
            {
                get
                {
                    return _public;
                }
                set
                {
                    _public = value;
                    MessageAll(_public ? "MatchPublic" : "MatchPrivate");
                    ins.UpdateMatchUI();
                }
            }

            public int TeamSize
            {
                get
                {
                    return _teamSize;
                }
                set
                {
                    if (IsStarted)
                        return;

                    _teamSize = value;
                    MessageAll("MatchSizeChanged", _teamSize);
                    ins.UpdateMatchUI();
                }
            }

            public DuelingZone Zone
            {
                get
                {
                    return _zone;
                }
            }

            public bool IsFull()
            {
                return _good.Count == _teamSize && _evil.Count == _teamSize;
            }

            public bool IsFull(Team team)
            {
                return team == Team.Good ? _good.Count == _teamSize : _evil.Count == _teamSize;
            }

            public void MessageAll(string key, params object[] args)
            {
                MessageGood(key, args);
                MessageEvil(key, args);
            }

            public void MessageGood(string key, params object[] args)
            {
                foreach (var player in _good)
                {
                    player.ChatMessage(ins.msg(key, player.UserIDString, args != null ? args : new string[0]));
                }
            }

            public void MessageEvil(string key, params object[] args)
            {
                foreach (var player in _evil)
                {
                    player.ChatMessage(ins.msg(key, player.UserIDString, args != null ? args : new string[0]));
                }
            }

            public Team GetTeam(BasePlayer player)
            {
                return _good.Contains(player) ? Team.Good : _evil.Contains(player) ? Team.Evil : Team.None;
            }

            public bool IsHost(BasePlayer player)
            {
                return player.UserIDString == _goodHostId || player.UserIDString == _evilHostId;
            }

            public string GoodCode()
            {
                return _goodCode;
            }

            public bool GoodCode(string code)
            {
                return code.ToLower() == _goodCode.ToLower();
            }

            public string EvilCode()
            {
                return _evilCode;
            }

            public bool EvilCode(string code)
            {
                return code.ToLower() == _evilCode.ToLower();
            }

            public void SetCode(BasePlayer player, string code)
            {
                if (GetTeam(player) == Team.Evil)
                    _evilCode = code;
                else if (GetTeam(player) == Team.Good)
                    _goodCode = code;
            }

            public bool AddToGoodTeam(BasePlayer player)
            {
                return AddMatchPlayer(player, Team.Good);
            }

            public bool AddToEvilTeam(BasePlayer player)
            {
                return AddMatchPlayer(player, Team.Evil);
            }

            public bool AlliedToGoodHost(BasePlayer player) // requires player to be allied with the good team's host
            {
                return ins.IsAllied(player.UserIDString, _goodHostId);
            }

            public bool AlliedToEvilHost(BasePlayer player) // requires player to be allied with the evil team's host
            {
                return ins.IsAllied(player.UserIDString, _evilHostId);
            }

            public bool IsBanned(ulong targetId)
            {
                return _banned.Contains(targetId);
            }

            public bool Ban(BasePlayer target)
            {
                if (target.UserIDString == _goodHostId || target.UserIDString == _evilHostId || IsBanned(target.userID))
                    return false;

                _banned.Add(target.userID);
                RemoveMatchPlayer(target);
                return true;
            }

            public bool IsStarted
            {
                get
                {
                    return _started;
                }
                set
                {
                    _started = value;
                    ins.UpdateMatchUI();
                }
            }

            public bool IsOver
            {
                get
                {
                    return _ended;
                }
                set
                {
                    _ended = value;
                    ins.UpdateMatchUI();
                }
            }

            public bool Equals(GoodVersusEvilMatch match)
            {
                return match._good.Equals(this._good) && match._evil.Equals(this._evil);
            }

            public string Kit
            {
                get
                {
                    return _kit;
                }
                set
                {
                    _kit = value;
                    MessageAll("MatchKitSet", _kit);
                }
            }

            public string GetNames(Team team)
            {
                return string.Join(", ", team == Team.Good ? _good.Select(player => player.displayName).ToArray() : _evil.Select(player => player.displayName).ToArray());
            }

            public void GiveShirt(BasePlayer player)
            {
                Item item = ItemManager.CreateByName(teamShirt, 1, GetTeam(player) == Team.Evil ? teamEvilShirt : teamGoodShirt);

                if (item == null || item.info.category != ItemCategory.Attire)
                    return;

                foreach (Item wear in player.inventory.containerWear.itemList.ToList())
                {
                    if (wear.info.shortname.Contains("shirt"))
                    {
                        wear.RemoveFromContainer();
                        wear.Remove(0.01f);
                        break;
                    }
                }

                item.MoveToContainer(player.inventory.containerWear, -1, false);

                if (!player.inventory.containerWear.HasFlag(ItemContainer.Flag.IsLocked))
                    player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, true);
            }

            private bool AddMatchPlayer(BasePlayer player, Team team)
            {
                if (_started)
                {
                    player.ChatMessage(ins.msg("MatchStartedAlready", player.UserIDString));
                    return false;
                }

                _good.RemoveWhere(entry => entry == null);
                _evil.RemoveWhere(entry => entry == null);

                if (_banned.Contains(player.userID))
                    return false;

                if (!ins.IsNewman(player))
                {
                    player.ChatMessage(ins.msg("MustBeNaked", player.UserIDString));
                    return false;
                }

                switch (team)
                {
                    case Team.Good:
                        if (_good.Count == _teamSize)
                        {
                            player.ChatMessage(ins.msg("MatchTeamFull", player.UserIDString, _teamSize));
                            return false;
                        }

                        _good.Add(player);
                        MessageAll("MatchJoinedTeam", player.displayName, _goodHostName, _good.Count, _teamSize, _evilHostName, _evil.Count);
                        break;
                    case Team.Evil:
                        if (_evil.Count == _teamSize)
                        {
                            player.ChatMessage(ins.msg("MatchTeamFull", player.UserIDString, _teamSize));
                            return false;
                        }

                        _evil.Add(player);
                        MessageAll("MatchJoinedTeam", player.displayName, _evilHostName, _evil.Count, _teamSize, _goodHostName, _good.Count);
                        break;
                }

                if (_good.Count == _teamSize && _evil.Count == _teamSize)
                    Queue();

                return true;
            }

            public bool RemoveMatchPlayer(BasePlayer player)
            {
                if (player.inventory.containerWear.HasFlag(ItemContainer.Flag.IsLocked))
                    player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, false);

                ins.Metabolize(player, false);
                ins.RemoveEntities(player.userID);

                if (_good.Remove(player))
                {
                    if (_good.Count == 0)
                    {
                        if (!_started)
                            MessageAll("MatchNoPlayersLeft");
                        else
                            _goodKIA.Add(player.userID);

                        EndMatch(Team.Evil);
                        return true;
                    }
                    else if (_started)
                        _goodKIA.Add(player.userID);

                    if (player.UserIDString == _goodHostId)
                        AssignGoodHostId();

                    return true;
                }

                if (_evil.Remove(player))
                {
                    if (_evil.Count == 0)
                    {
                        if (!_started)
                            MessageAll("MatchNoPlayersLeft");
                        else
                            _evilKIA.Add(player.userID);

                        EndMatch(Team.Good);
                        return true;
                    }
                    else if (_started)
                        _evilKIA.Add(player.userID);

                    if (player.UserIDString == _evilHostId)
                        AssignEvilHostId();

                    return true;
                }

                return false;
            }

            private void AssignGoodHostId()
            {
                _good.RemoveWhere(entry => entry == null);

                if (_good.Count > 0)
                    _goodHostId = _good.First().UserIDString;
                else
                    EndMatch(Team.Evil);
            }

            private void AssignEvilHostId()
            {
                _evil.RemoveWhere(entry => entry == null);

                if (_evil.Count > 0)
                    _evilHostId = _evil.First().UserIDString;
                else
                    EndMatch(Team.Good);
            }

            private void AwardTeam(Team team)
            {
                if (teamEconomicsMoney > 0.0 || teamServerRewardsPoints > 0)
                {
                    if (Interface.CallHook("OnDuelAwardTeam", team == Team.Evil ? _evilKIA.ToList() : _goodKIA.ToList(), team == Team.Evil ? _goodKIA.ToList() : _evilKIA.ToList()) == null) // winners/losers
                    {
                        switch (team)
                        {
                            case Team.Evil:
                                {
                                    foreach (ulong playerId in _evilKIA)
                                    {
                                        AwardPlayer(playerId, teamEconomicsMoney, teamServerRewardsPoints);
                                    }
                                }
                                break;
                            case Team.Good:
                                {
                                    foreach (ulong playerId in _goodKIA)
                                    {
                                        AwardPlayer(playerId, teamEconomicsMoney, teamServerRewardsPoints);
                                    }
                                }
                                break;
                        }
                    }
                }

                _goodKIA.Clear();
                _evilKIA.Clear();
            }

            private void EndMatch(Team team)
            {
                if (!_ended && _started)
                {
                    AwardTeam(team);

                    foreach (var target in BasePlayer.activePlayerList.Where(p => p?.displayName != null))
                    {
                        if (duelsData.Chat.Contains(target.UserIDString) && !_goodKIA.Contains(target.userID) && !_evilKIA.Contains(target.userID))
                            continue;

                        target.ChatMessage(ins.msg("MatchDefeat", target.UserIDString, team == Team.Evil ? _evilHostName : _goodHostName, team == Team.Evil ? _goodHostName : _evilHostName, _teamSize));
                    }

                    ins.Puts(ins.msg("MatchDefeat", null, team == Team.Evil ? _evilHostName : _goodHostName, team == Team.Evil ? _goodHostName : _evilHostName, _teamSize));
                    IsOver = true;
                }

                this.End();
            }

            public void End()
            {
                if (_zone != null)
                    _zone.IsLocked = false;

                _queueTimer?.Destroy();

                foreach (var player in _good.Where(entry => entry != null))
                {
                    if (player.inventory.containerWear.HasFlag(ItemContainer.Flag.IsLocked))
                        player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, false);

                    if (_ended || _started)
                    {
                        player.inventory.Strip();
                        ins.Metabolize(player, false);
                        ins.SendHome(player);
                    }
                }

                foreach (var player in _evil.Where(entry => entry != null))
                {
                    if (player.inventory.containerWear.HasFlag(ItemContainer.Flag.IsLocked))
                        player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, false);

                    if (_ended || _started)
                    {
                        player.inventory.Strip();
                        ins.Metabolize(player, false);
                        ins.SendHome(player);
                    }
                }

                _good.Clear();
                _evil.Clear();

                if (tdmMatches.Contains(this))
                {
                    tdmMatches.Remove(this);
                    ins.UpdateMatchUI();
                }

                if (dataDuelists.Count == 0 && tdmMatches.Count == 0)
                    ins.Unsubscribe(nameof(OnPlayerHealthChange));
            }

            private void Queue()
            {
                bool _canStart = true;

                foreach (var player in _good)
                {
                    if (!ins.IsNewman(player))
                    {
                        player.ChatMessage(ins.msg("MustBeNaked", player.UserIDString));
                        MessageAll("MatchIsNotNaked", player.displayName);
                        _canStart = false;
                    }
                }

                foreach (var player in _evil)
                {
                    if (!ins.IsNewman(player))
                    {
                        player.ChatMessage(ins.msg("MustBeNaked", player.UserIDString));
                        MessageAll("MatchIsNotNaked", player.displayName);
                        _canStart = false;
                    }
                }

                if (!_canStart)
                {
                    _queueTimer = ins.timer.Once(30f, () => Queue());
                    return;
                }

                var zones = duelingZones.Where(zone => zone.TotalPlayers == 0 && !zone.IsLocked && zone.Spawns.Count >= (requireTeamSize ? TeamSize * 2 : 2)).ToList();

                if (zones == null || zones.Count == 0)
                {
                    if (!_enteredQueue)
                    {
                        MessageAll("MatchQueued");
                        _enteredQueue = true;
                    }

                    _queueTimer = ins.timer.Once(2f, () => Queue());
                    return;
                }

                _zone = zones.GetRandom();
                _queueTimer?.Destroy();
                Start();
            }

            private void Start()
            {
                ins.SubscribeHooks(true);

                var goodSpawn = _zone.Spawns.GetRandom();
                var evilSpawn = goodSpawn;
                float dist = -100f;

                foreach (var spawn in _zone.Spawns) // get the furthest spawn point away from the good team and assign it to the evil team
                {
                    float distance = Vector3.Distance(spawn, goodSpawn);

                    if (distance > dist)
                    {
                        dist = distance;
                        evilSpawn = spawn;
                    }
                }

                MessageGood("MatchStarted", GetNames(Team.Evil));
                MessageEvil("MatchStarted", GetNames(Team.Good));
                _zone.IsLocked = true;
                IsStarted = true;

                Spawn(_good, goodSpawn);
                Spawn(_evil, evilSpawn);
            }

            private void Spawn(HashSet<BasePlayer> players, Vector3 spawn)
            {
                foreach (var player in players)
                {
                    duelsData.Kits[player.UserIDString] = _kit;

                    var ppos = player.transform.position;

                    if (IsOnConstruction(player.transform.position)) ppos.y += 1; // prevent player from becoming stuck or dying when teleported home

                    duelsData.Homes[player.UserIDString] = ppos.ToString();

                    RemoveFromQueue(player.UserIDString);
                    ins.Teleport(player, spawn);

                    if (ins.immunityTime > 0)
                    {
                        dataImmunity[player.UserIDString] = ins.TimeStamp() + ins.immunityTime;
                        dataImmunitySpawns[player.UserIDString] = spawn;
                    }
                }
            }

            public GoodVersusEvilMatch() { }
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
            public string trigger { get; set; } = ""; // the trigger used to request this as a bet
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
            private bool _locked { get; set; } = false;

            public DuelingZone() { }

            public void Setup(Vector3 position)
            {
                _zonePos = position;

                var spawns = GetAutoSpawns(this);
                _duelSpawns.Clear();

                if (spawns.Count > 0)
                    _duelSpawns.AddRange(spawns);
            }

            public float Distance(Vector3 position)
            {
                position.y = _zonePos.y;
                return Vector3.Distance(_zonePos, position);
            }

            public bool IsLocked
            {
                get
                {
                    return _locked;
                }
                set
                {
                    _locked = value;
                }
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

            public List<BasePlayer> Players
            {
                get
                {
                    return _players.ToList();
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
                    return TotalPlayers + _waiting.Count + 2 > playersPerZone || _locked;
                }
            }

            public bool? AddWaiting(BasePlayer player, BasePlayer target)
            {
                if (IsFull)
                    return false;

                if (requiredDuelMoney > 0.0 && ins.Economics != null)
                {
                    double playerMoney = Convert.ToDouble(ins.Economics.Call("GetPlayerMoney", player.userID));
                    double targetMoney = Convert.ToDouble(ins.Economics.Call("GetPlayerMoney", target.userID));

                    if (playerMoney < requiredDuelMoney || targetMoney < requiredDuelMoney)
                    {
                        RemoveFromQueue(player.UserIDString);
                        RemoveFromQueue(target.UserIDString);
                        player.ChatMessage(ins.msg("MoneyRequired", player.UserIDString, requiredDuelMoney));
                        target.ChatMessage(ins.msg("MoneyRequired", target.UserIDString, requiredDuelMoney));
                        return null;
                    }

                    bool playerWithdrawn = Convert.ToBoolean(ins.Economics.Call("Withdraw", player.userID, requiredDuelMoney));
                    bool targetWithdrawn = Convert.ToBoolean(ins.Economics.Call("Withdraw", target.userID, requiredDuelMoney));

                    if (!playerWithdrawn || !targetWithdrawn)
                    {
                        RemoveFromQueue(player.UserIDString);
                        RemoveFromQueue(target.UserIDString);
                        return null;
                    }
                }

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
                foreach (var player in _players.ToList())
                {
                    if (player == null || player.UserIDString == playerId)
                    {
                        _players.Remove(player);
                        _waiting.Remove(player);
                        break;
                    }
                }
            }

            public bool HasPlayer(string playerId)
            {
                return _players.Any(player => player.UserIDString == playerId);
            }

            public void Kill()
            {
                foreach (var player in _players.ToList())
                    EjectPlayer(player);

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
            
            if (useAnnouncement)
                announceTimer = timer.Repeat(1800f, 0, () => DuelAnnouncement()); // TODO: add configuration option to set the time

            eventTimer = timer.Repeat(0.5f, 0, () => CheckDuelistMortality()); // kill players who haven't finished their duel in time. remove temporary immunity for duelers when it expires

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

            if (duelingZones.Count > 0 && autoEnable)
                duelsData.DuelsEnabled = true;

            UpdateStability();
            CheckArenaHooks(true);
            
            if (guiAutoEnable)
            {
                Subscribe(nameof(OnPlayerInit));

                foreach (var player in BasePlayer.activePlayerList)
                    OnPlayerInit(player);
            }
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

            foreach (var match in tdmMatches.ToList())
            {
                match.End();
            }

            tdmMatches.Clear();
            duelingZones.Clear();
            ResetTemporaryData();
            DestroyAllUI();
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
        }

        object CanNetworkTo(BaseNetworkable entity, BasePlayer target) // temp hook
        {
            if (!init)
                return null;

            var player = entity as BasePlayer ?? (entity as HeldEntity)?.GetOwnerPlayer(); // 0.1.3 fix: check if player is null

            if (player == null || target == null || player == target || (visibleToAdmins && target.IsAdmin))
                return null;

            if (dataDuelists.Count > 0)
            {
                if (dataDuelists.ContainsKey(player.UserIDString))
                {
                    if (DuelTerritory(player.transform.position))
                    {
                        return dataDuelists[player.UserIDString] == target.UserIDString ? null : (object)false;
                    }
                }
            }

            /*if (tdmMatches.Count > 0)
            {
                if (DuelTerritory(player.transform.position))
                {
                    var playerMatch = GetMatch(player);

                    if (playerMatch != null && playerMatch.IsStarted && !playerMatch.IsOver)
                    {
                        var targetMatch = GetMatch(target);

                        if (targetMatch == null || !playerMatch.Equals(targetMatch))
                        {
                            return false;
                        }
                    }
                }
            }*/

            if (dataDuelists.Count == 0 /*&& tdmMatches.Count == 0*/)
                Unsubscribe(nameof(CanNetworkTo)); // nothing else to do right now, unsubscribe the hook

            return null;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason) // temp hook
        {
            if (!init)
                return;

            if (lustyMarkers.Contains(player.UserIDString))
                lustyMarkers.Remove(player.UserIDString);

            if (IsDueling(player))
            {
                OnDuelistLost(player);
                RemoveDuelist(player.UserIDString);
                ResetDuelist(player.UserIDString, false);
                SendHome(player);
            }
            else if (InDeathmatch(player))
            {
                player.inventory.Strip();

                var match = GetMatch(player);
                match.RemoveMatchPlayer(player);
                SendHome(player);
            }

            if (dataDuelists.Count == 0 && tdmMatches.Count == 0)
                Unsubscribe(nameof(OnPlayerDisconnected)); // nothing else to do right now, unsubscribe the hook
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (!player || player.net == null)
                return;

            if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(1f, () => OnPlayerInit(player));
                return;
            }

            if (createUI.Contains(player.UserIDString))
                createUI.Remove(player.UserIDString);

            cmdDUI(player, "dui", new string[0]);
        }

        void OnPlayerSleepEnded(BasePlayer player) // temp hook
        {
            if (!init)
                return;

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
                            dataDeath[player.UserIDString] = TimeStamp() + (deathTime * 60);
                        }

                        if (showWarning)
                            player.ChatMessage(msg("DuelWarning", player.UserIDString));

                        GivePlayerKit(player);
                        Metabolize(player, true);

                        if (useInvisibility)
                            Disappear(player);

                        zone.AddPlayer(player);
                        return;
                    }
                }
            }
            else if (InDeathmatch(player))
            {
                var match = GetMatch(player);

                if (match != null)
                {
                    player.metabolism.calories.value = player.metabolism.calories.max;
                    player.metabolism.hydration.value = player.metabolism.hydration.max;

                    if (deathTime > 0)
                    {
                        player.ChatMessage(msg("ExecutionTime", player.UserIDString, deathTime));
                        dataDeath[player.UserIDString] = TimeStamp() + (deathTime * 60);
                    }

                    if (showWarning)
                        player.ChatMessage(msg("DuelWarning", player.UserIDString));

                    GivePlayerKit(player);
                    Metabolize(player, true);
                    match.GiveShirt(player);
                }

                return;
            }

            if (duelsData.Homes.Count == 0 && dataDuelists.Count == 0 && tdmMatches.Count == 0) // nothing else to do right now, unsubscribe the hook
                Unsubscribe(nameof(OnPlayerSleepEnded));
        }

        void OnPlayerRespawned(BasePlayer player) // temp hook
        {
            if (init && DuelTerritory(player.transform.position) && !dataDuelists.ContainsKey(player.UserIDString))
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
        
        void OnEntityKill(BaseNetworkable entity)
        {
            if (respawnWalls)
            {
                var e = entity?.GetComponent<BaseEntity>();

                if (e?.transform != null && e.name.Contains("wall.external.high"))
                {
                    RecreateZoneWall(e.PrefabName, e.transform.position, e.transform.rotation, e.OwnerID);
                }
            }
        }

        void RecreateZoneWall(string prefab, Vector3 pos, Quaternion rot, ulong ownerId)
        {
            bool duelWall = DuelTerritory(pos) && duelsData.ZoneIds.Any(entry => GetOwnerId(entry) == ownerId);
            bool arenaWall = ArenaTerritory(pos) && duelsData.Zones.Any(entry => GetOwnerId(entry.Key) == ownerId);

            if (duelWall || arenaWall)
            {
                var e = GameManager.server.CreateEntity(prefab, pos, rot, false);

                if (e != null)
                {
                    e.OwnerID = ownerId;
                    e.Spawn();
                    e.gameObject.SetActive(true);
                }
            }
        }

        void OnEntityDeath(BaseEntity entity, HitInfo hitInfo) // 0.1.16 temp hook - fix for player suiciding
        {
            if (respawnWalls && entity?.transform != null && entity.name.Contains("wall.external.high"))
            {
                RecreateZoneWall(entity.PrefabName, entity.transform.position, entity.transform.rotation, entity.OwnerID);
                return;
            }

            var victim = entity as BasePlayer;

            if (victim == null)
                return;

            if (IsDueling(victim))
            {
                victim.inventory.Strip();
                OnDuelistLost(victim);
            }
            else if (InDeathmatch(victim))
            {
                victim.inventory.Strip();

                var match = GetMatch(victim);
                match.RemoveMatchPlayer(victim);
                SendHome(victim);
            }           
        }

        void OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue) // temp hook
        {
            if (newValue < 6f)
            {
                if (IsDueling(player))
                {
                    player.health = 6f;
                    player.inventory.Strip();
                    OnDuelistLost(player);
                }
                else if (InDeathmatch(player))
                {
                    player.health = 6f;
                    player.inventory.Strip();

                    var match = GetMatch(player);

                    if (tdmAttackers.ContainsKey(player.UserIDString))
                    {
                        var info = tdmAttackers[player.UserIDString];

                        if (tdmServerDeaths)
                        {
                            foreach (var target in BasePlayer.activePlayerList.Where(p => p?.displayName != null))
                            {
                                if (duelsData.Chat.Contains(target.UserIDString) && target != player)
                                    continue;

                                target.ChatMessage(msg("MatchPlayerDefeated", target.UserIDString, player.displayName, info.AttackerName, info.Weapon, info.BoneName, info.Distance));
                            }
                        }
                        else if (tdmMatchDeaths)
                        {
                            match.MessageAll("MatchPlayerDefeated", player.displayName, info.AttackerName, info.Weapon, info.BoneName, info.Distance);
                        }

                        tdmAttackers.Remove(player.UserIDString);
                    }

                    match.RemoveMatchPlayer(player);
                    SendHome(player);
                }
            }
        }

        void OnDuelistLost(BasePlayer victim)
        {
            RemoveEntities(victim.userID);

            if (!dataDuelists.ContainsKey(victim.UserIDString))
            {
                NextTick(() => SendHome(victim));
                return;
            }

            string attackerId = dataDuelists[victim.UserIDString];
            var attacker = BasePlayer.activePlayerList.Find(p => p.UserIDString == attackerId);
            string attackerName = attacker?.displayName ?? GetDisplayName(attackerId); // get the attackers name. it's possible the victim died by other means so we'll null check everything

            if (dataDeath.ContainsKey(victim.UserIDString)) dataDeath.Remove(victim.UserIDString); // remove them from automatic deaths
            if (dataDeath.ContainsKey(attackerId)) dataDeath.Remove(attackerId);
            if (dataDuelists.ContainsKey(victim.UserIDString)) dataDuelists.Remove(victim.UserIDString); // unset their status as duelers
            if (dataDuelists.ContainsKey(attackerId)) dataDuelists.Remove(attackerId);

            victim.inventory.Strip(); // also strip the attacker below after verifying
            Metabolize(victim, false);

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
                foreach (var target in BasePlayer.activePlayerList.Where(p => p?.displayName != null)) // customize each message using language api
                {
                    if ((duelsData.Chat.Contains(target.UserIDString) || !broadcastDefeat) && target != victim && target != attacker)
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
            RemoveEntities(Convert.ToUInt64(attackerId));

            if (attacker != null)
            {
                attacker.inventory.Strip();
                Metabolize(attacker, false);
            }

            if (economicsMoney > 0.0 || serverRewardsPoints > 0)
                AwardPlayer(Convert.ToUInt64(attackerId), economicsMoney, serverRewardsPoints);

            var zone = RemoveDuelist(victim.UserIDString);

            if (zoneCounter > 0 && zone != null) // if new zones are set to spawn every X duels then increment by 1
            {
                zone.Kills++;

                if (zone.TotalPlayers == 0 && zone.Kills >= zoneCounter)
                {
                    RemoveDuelZone(zone);
                    SetupDuelZone(null); // x amount of duels completed. time to relocate and start all over! changing the dueling zones location keeps things mixed up and entertaining for everyone. especially when there's issues with terrain
                    SaveData();
                }
            }

            if (dataDuelists.Count == 0 && tdmMatches.Count == 0)
                Unsubscribe(nameof(OnPlayerHealthChange));

            NextTick(() =>
            {
                SendHome(attacker);
                SendHome(victim);
            });
        }
        
        void HealDamage(BaseCombatEntity entity)
        {
            timer.Once(1f, () =>
            {
                if (entity != null && !entity.IsDestroyed && entity.health < entity.MaxHealth())
                {
                    entity.health = entity.MaxHealth();
                    entity.SendNetworkUpdate();
                }
            });
        }

        void CancelDamage(HitInfo hitInfo)
        {
            if (hitInfo != null)
            {
                hitInfo.damageTypes = new DamageTypeList();
                hitInfo.DidHit = false;
                hitInfo.HitEntity = null;
                hitInfo.Initiator = null;
                hitInfo.DoHitEffects = false;
                hitInfo.HitMaterial = 0;
            }
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo) // temp hook
        {
            if (!init || entity == null || entity.net == null || entity.transform == null)
                return null;

            if (DuelTerritory(entity.transform.position, differential))
            {
                if (entity is BuildingBlock || entity.name.Contains("deploy") || entity.name.Contains("wall.external.high") || entity.name.Contains("building"))
                {
                    CancelDamage(hitInfo);
                    HealDamage(entity);
                    return false;
                }
            }

            if (ArenaTerritory(entity.transform.position, differential))
            {
                if (hitInfo?.damageTypes != null && hitInfo.damageTypes.Has(DamageType.Decay))
                {
                    CancelDamage(hitInfo);
                    HealDamage(entity);
                    return null;
                }

                if (entity.name.Contains("wall.external.high"))
                {
                    if (customArenasUseWallProtection)
                    {
                        CancelDamage(hitInfo);
                        HealDamage(entity);
                    }

                    return null;
                }

                if ((entity is BuildingBlock || entity.name.Contains("deploy") || entity.name.Contains("building")) && customArenasNoRaiding)
                {
                    CancelDamage(hitInfo);
                    HealDamage(entity);
                    return false;
                }

                if (entity is BasePlayer && customArenasNoPVP)
                {
                    CancelDamage(hitInfo);
                    return null;
                }
            }

            if (hitInfo == null || !hitInfo.hasDamage)
                return null;

            var victim = entity as BasePlayer;
            var attacker = hitInfo.Initiator as BasePlayer;

            if (victim != null && attacker != null && victim == attacker)  // allow player to suicide and self inflict
            {
                return null;
            }

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

            if (dataDuelists.Count > 0)
            {
                if (attacker?.transform != null && IsDueling(attacker) && victim != null) // 0.1.8 check attacker then victim
                {
                    if (dataDuelists[attacker.UserIDString] != victim.UserIDString)
                    {
                        return false; // prevent attacker from doing damage to others
                    }
                }

                if (victim != null && IsDueling(victim))
                {
                    if (dataImmunity.ContainsKey(victim.UserIDString))
                        return false; // immunity timer

                    if (hitInfo.Initiator is BaseHelicopter)
                        return false; // protect duelers from helicopters

                    if (attacker?.transform != null && dataDuelists[victim.UserIDString] != attacker.UserIDString)
                        return false; // prevent attacker from doing damage to others

                    if (damagePercentageScale > 0f)
                        hitInfo.damageTypes?.ScaleAll(damagePercentageScale);

                    return null;
                }
            }

            if (tdmMatches.Count > 0)
            {
                if (attacker?.transform != null && InDeathmatch(attacker) && victim != null)
                {
                    var match = GetMatch(attacker);

                    if (match.GetTeam(victim) == Team.None)
                        return false;

                    if (match.GetTeam(victim) == match.GetTeam(attacker) && !dmFF)
                        return false; // FF
                }

                if (victim != null && InDeathmatch(victim))
                {
                    if (dataImmunity.ContainsKey(victim.UserIDString))
                        return false; // immunity timer

                    if (hitInfo.Initiator is BaseHelicopter)
                        return false; // protect duelers from helicopters

                    if (attacker?.transform != null)
                    {
                        if (GetMatch(attacker) == null)
                            return false; // attacker isn't in a match
                        else if (victim.health == 6f)
                            return false;

                        if (tdmAttackers.ContainsKey(victim.UserIDString))
                            tdmAttackers.Remove(victim.UserIDString);

                        tdmAttackers.Add(victim.UserIDString, new AttackerInfo());
                        tdmAttackers[victim.UserIDString].AttackerName = attacker.displayName;
                        tdmAttackers[victim.UserIDString].Distance = Math.Round(Vector3.Distance(attacker.transform.position, victim.transform.position), 2).ToString();
                        tdmAttackers[victim.UserIDString].BoneName = FormatBone(hitInfo.boneName).TrimEnd();
                        tdmAttackers[victim.UserIDString].Weapon = attacker.GetActiveItem()?.info?.displayName?.english ?? hitInfo?.WeaponPrefab?.ShortPrefabName ?? "??";
                    }

                    if (damagePercentageScale > 0f)
                        hitInfo.damageTypes?.ScaleAll(damagePercentageScale);

                    return null;
                }
            }

            var pointStart = hitInfo.Initiator?.transform?.position ?? hitInfo.PointStart; // 0.1.6 border fix
            var pointEnd = entity?.transform?.position ?? hitInfo.PointEnd; // PointEnd shouldn't ever be used

            if (DuelTerritory(pointStart) && !DuelTerritory(pointEnd))
            {
                return false; // block all damage to the outside
            }

            if (!DuelTerritory(pointStart) && DuelTerritory(pointEnd))
            {
                return false; // block all damage to the inside
            }

            return null;
        }

        void OnEntitySpawned(BaseNetworkable entity) // temp hook
        {
            if (!init || entity == null)
                return;

            if (noStability && entity is BuildingBlock)
            {
                if (DuelTerritory(entity.transform.position) || ArenaTerritory(entity.transform.position))
                {
                    var block = entity as BuildingBlock;

                    if (block.OwnerID == 0 || permission.UserHasGroup(block.OwnerID.ToString(), "admin"))
                    {
                        block.grounded = true;
                        return;
                    }
                }
            }

            if (dataDuelists.Count == 0 && tdmMatches.Count == 0)
                return;

            if (prefabs.Any(x => x.Key == entity.PrefabName) && DuelTerritory(entity.transform.position))
            {
                var e = entity.GetComponent<BaseEntity>();
                // check if ownerid is dueling/in match
                if (!duelEntities.ContainsKey(e.OwnerID))
                    duelEntities.Add(e.OwnerID, new List<BaseEntity>());

                duelEntities[e.OwnerID].Add(e);
            }

            if (entity is PlayerCorpse || entity.name.Contains("item_drop_backpack"))
            {
                if (DuelTerritory(entity.transform.position))
                {
                    NextTick(() =>
                    {
                        if (entity != null && !entity.IsDestroyed)
                        {
                            entity.Kill();
                        }
                    });
                }
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

                    if (!string.IsNullOrEmpty(kvp.Value) && deployables.ContainsKey(kvp.Value) && deployables[kvp.Value])
                    {
                        if (dataDuelists.ContainsKey(player.UserIDString) || InMatch(player))
                        {
                            return null;
                        }
                    }
                }

                player.ChatMessage(msg("Building is blocked!", player.UserIDString));
                return false;
            }

            return null;
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity) // stop all players from looting anything inside of dueling zones. this allows server owners to setup duels anywhere without worry.
        {
            if (!init)
                return;

            if (player != null && (IsDueling(player) || InDeathmatch(player)))
                timer.Once(0.01f, player.EndLooting);

            if (dataDuelists.Count == 0 && tdmMatches.Count == 0)
                Unsubscribe(nameof(OnLootEntity));
        }

        object OnCreateWorldProjectile(HitInfo info, Item item) // temp hook. prevents thrown items from becoming stuck in players when they respawn and requiring them to relog to remove them
        {
            if (!init || info == null)
                return null;

            if (dataDuelists.Count == 0 && tdmMatches.Count == 0)
                Unsubscribe(nameof(OnCreateWorldProjectile));

            var victim = info.HitEntity as BasePlayer;
            var attacker = info.Initiator as BasePlayer;

            if (victim != null && (IsDueling(victim) || InDeathmatch(victim)))
                return false; // block it

            if (attacker != null && (IsDueling(attacker) || InDeathmatch(attacker)))
                return false;

            return null;
        }

        void OnItemDropped(Item item, BaseEntity entity) // temp hook
        {
            if (!init || item?.GetOwnerPlayer() == null) // null checks
                return;

            var player = item.GetOwnerPlayer();

            if (!IsThrownWeapon(item) && (IsDueling(player) || InDeathmatch(player)))
                item.Remove(0.01f); // do NOT allow players to drop items. this is a dueling zone. not a gift zone.

            if (dataDuelists.Count == 0 && tdmMatches.Count == 0) // nothing left to do here, unsubscribe the hook
                Unsubscribe(nameof(OnItemDropped));
        }

        object IsPrisoner(BasePlayer player) // Random Warps
        {
            return IsDueling(player) || InDeathmatch(player) ? (object)true : null;
        }

        object CanEventJoin(BasePlayer player) // EventManager
        {
            return IsDueling(player) || InDeathmatch(player) ? msg("CannotEventJoin", player.UserIDString) : null;
        }

        object canRemove(BasePlayer player) // RemoverTool
        {
            return init && DuelTerritory(player.transform.position, differential) ? (object)false : null;
        }

        object CanTrade(BasePlayer player) // Trade
        {
            return init && DuelTerritory(player.transform.position, differential) ? (object)false : null;
        }

        object CanBank(BasePlayer player)
        {
            return init && DuelTerritory(player.transform.position) ? msg("CannotBank", player.UserIDString) : null;
        }

        private object CanOpenBackpack(BasePlayer player)
        {
            return init && DuelTerritory(player.transform.position) ? msg("CommandNotAllowed", player.UserIDString) : null;
        }

        object canShop(BasePlayer player) // Shop and ServerRewards
        {
            return init && DuelTerritory(player.transform.position) ? msg("CannotShop", player.UserIDString) : null;
        }

        object CanShop(BasePlayer player)
        {
            return init && DuelTerritory(player.transform.position) ? msg("CannotShop", player.UserIDString) : null;
        }

        object CanBePenalized(BasePlayer player) // ZLevels Remastered
        {
            return init && (DuelTerritory(player.transform.position) || dataDuelists.ContainsKey(player.UserIDString) || ArenaTerritory(player.transform.position)) ? (object)false : null;
        }

        object canTeleport(BasePlayer player) // 0.1.2: block teleport from NTeleportation plugin
        {
            return init && DuelTerritory(player.transform.position) ? msg("CannotTeleport", player.UserIDString) : null;
        }

        object CanTeleport(BasePlayer player) // 0.1.2: block teleport from MagicTeleportation plugin
        {
            return init && DuelTerritory(player.transform.position) ? msg("CannotTeleport", player.UserIDString) : null;
        }

        object CanJoinTDMEvent(BasePlayer player)
        {
            return init && DuelTerritory(player.transform.position, differential) ? (object)false : null;
        }

        object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo) // TruePVE!!!! <3 @ignignokt84
        {
            return init && entity != null && entity is BasePlayer && DuelTerritory(entity.transform.position) ? (object)true : null;
        }

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (!init || !player || player.IsAdmin || !DuelTerritory(player.transform.position))
                return null;

            string text = arg.GetString(0, "text").ToLower();

            if (arg.cmd.FullName == "chat.say" && text.StartsWith("/"))
            {
                if (useBlacklistCommands && blacklistCommands.Any(entry => entry.StartsWith("/") ? text.StartsWith(entry) : text.Substring(1).StartsWith(entry)))
                {
                    player.ChatMessage(msg("CommandNotAllowed", player.UserIDString));
                    return false;
                }
                else if (useWhitelistCommands && !whitelistCommands.Any(entry => entry.StartsWith("/") ? text.StartsWith(entry) : text.Substring(1).StartsWith(entry)))
                {
                    player.ChatMessage(msg("CommandNotAllowed", player.UserIDString));
                    return false;
                }
            }

            return null;
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
                if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, wallMask))
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

        string FormatBone(string source)
        {
            if (string.IsNullOrEmpty(source))
                return "Chest";

            foreach (var entry in boneTags)
                source = source.Replace(entry.Key, entry.Value);

            return string.Join(" ", source.Split(' ').Select(str => str.SentenceCase()).ToArray());
        }

        string FormatPosition(Vector3 position)
        {
            string x = Math.Round(position.x, 2).ToString();
            string y = Math.Round(position.y, 2).ToString();
            string z = Math.Round(position.z, 2).ToString();

            return $"{x} {y} {z}";
        }
        #endregion

        void cmdTDM(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin && args.Length == 1 && args[0] == "showall" && tdmMatches.Count > 0)
            {
                foreach (var match in tdmMatches)
                {
                    player.ChatMessage(msg("InMatchListGood", player.UserIDString, match.GetNames(Team.Good)));
                    player.ChatMessage(msg("InMatchListEvil", player.UserIDString, match.GetNames(Team.Evil)));
                }

                return;
            }

            if (Interface.CallHook("CanDuel", player) != null)
            {
                player.ChatMessage(msg("CannotDuel", player.UserIDString));
                return;
            }

            if (!autoAllowAll && !duelsData.Allowed.Contains(player.UserIDString))
            {
                player.ChatMessage(msg("MustAllowDuels", player.UserIDString, szDuelChatCommand));
                return;
            }

            if (IsDueling(player))
            {
                player.ChatMessage(msg("AlreadyInADuel", player.UserIDString));
                return;
            }

            var deathmatch = tdmMatches.FirstOrDefault(x => x.GetTeam(player) != Team.None);

            if (deathmatch != null && deathmatch.IsStarted)
            {
                player.ChatMessage(msg("MatchStartedAlready", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                if (deathmatch == null)
                {
                    if (!autoAllowAll)
                        player.ChatMessage(msg("HelpAllow", player.UserIDString, szDuelChatCommand));

                    player.ChatMessage(msg("MatchChallenge0", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchChallenge2", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchChallenge3", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchAccept", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchCancel", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchLeave", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchSize", player.UserIDString, szMatchChatCommand, minDeathmatchSize));
                    player.ChatMessage(msg("MatchKickBan", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchSetCode", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchTogglePublic", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchKit", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("UI_Help", player.UserIDString, szUIChatCommand));
                }
                else
                {
                    player.ChatMessage(msg("MatchLeave", player.UserIDString, szMatchChatCommand));

                    if (!deathmatch.IsHost(player))
                        return;

                    player.ChatMessage(msg("MatchCancel", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchSize", player.UserIDString, szMatchChatCommand, minDeathmatchSize));
                    player.ChatMessage(msg("MatchKickBan", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchSetCode", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchTogglePublic", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("MatchKit", player.UserIDString, szMatchChatCommand));
                    player.ChatMessage(msg("InMatchListGood", player.UserIDString, deathmatch.GetNames(Team.Good)));
                    player.ChatMessage(msg("InMatchListEvil", player.UserIDString, deathmatch.GetNames(Team.Evil)));
                }

                return;
            }

            RemoveRequests(player);

            switch (args[0].ToLower())
            {
                case "kit":
                    {
                        if (deathmatch != null)
                        {
                            if (!deathmatch.IsHost(player))
                            {
                                player.ChatMessage(msg("MatchKitSet", player.UserIDString, deathmatch.Kit));
                                return;
                            }

                            if (args.Length == 2)
                            {
                                string kit = VerifiedKit(args[1]);

                                if (string.IsNullOrEmpty(kit))
                                {
                                    player.ChatMessage(msg("MatchChallenge0", player.UserIDString, szMatchChatCommand));
                                    player.ChatMessage(msg("KitDoesntExist", player.UserIDString, args[1]));

                                    string kits = string.Join(", ", VerifiedKits.ToArray());

                                    if (!string.IsNullOrEmpty(kits))
                                        player.ChatMessage("Kits: " + kits);
                                }
                                else
                                    deathmatch.Kit = kit;
                            }
                            else
                                player.ChatMessage(msg("MatchKit", player.UserIDString));
                        }
                        else
                            player.ChatMessage(msg("MatchDoesntExist", player.UserIDString, szMatchChatCommand));
                    }
                    return;
                case "kickban":
                    {
                        if (deathmatch != null)
                        {
                            if (!deathmatch.IsHost(player))
                            {
                                player.ChatMessage(msg("MatchNotAHost", player.UserIDString));
                                return;
                            }

                            if (args.Length == 2)
                            {
                                var target = FindPlayer(args[1]);

                                if (target != null)
                                {
                                    if (deathmatch.GetTeam(target) == deathmatch.GetTeam(player))
                                    {
                                        if (deathmatch.Ban(target))
                                            player.ChatMessage(msg("MatchBannedUser", player.UserIDString, target.displayName));
                                        else
                                            player.ChatMessage(msg("MatchCannotBan", player.UserIDString));
                                    }
                                    else
                                        player.ChatMessage(msg("MatchPlayerNotFound", player.UserIDString, target.displayName));
                                }
                                else
                                    player.ChatMessage(msg("PlayerNotFound", player.UserIDString, args[1]));
                            }
                            else
                                player.ChatMessage(msg("MatchKickBan", player.UserIDString));
                        }
                        else
                            player.ChatMessage(msg("MatchDoesntExist", player.UserIDString, szMatchChatCommand));
                    }
                    break;
                case "setcode":
                    {
                        if (deathmatch != null)
                        {
                            if (deathmatch.IsHost(player))
                            {
                                if (args.Length == 2)
                                    deathmatch.SetCode(player, args[1]);

                                if (deathmatch.GetTeam(player) == Team.Evil)
                                    player.ChatMessage(msg("MatchCodeIs", player.UserIDString, deathmatch.EvilCode()));
                                else
                                    player.ChatMessage(msg("MatchCodeIs", player.UserIDString, deathmatch.GoodCode()));
                            }
                            else
                                player.ChatMessage(msg("MatchNotAHost", player.UserIDString));
                        }
                        else
                            player.ChatMessage(msg("MatchDoesntExist", player.UserIDString, szMatchChatCommand));
                    }
                    break;
                case "cancel":
                case "decline":
                    {
                        if (deathmatch != null)
                        {
                            if (deathmatch.IsHost(player))
                            {
                                deathmatch.MessageAll("MatchCancelled", player.displayName);
                                deathmatch.End();

                                if (tdmMatches.Contains(deathmatch))
                                {
                                    tdmMatches.Remove(deathmatch);
                                    UpdateMatchUI();
                                }
                            }
                            else
                                player.ChatMessage(msg("MatchNotAHost", player.UserIDString));
                        }
                        else // also handle cancelling a match request
                        {
                            if (tdmRequests.ContainsValue(player.UserIDString))
                            {
                                var entry = tdmRequests.First(kvp => kvp.Value == player.UserIDString);
                                var target = BasePlayer.activePlayerList.Find(x => x.UserIDString == entry.Key);

                                player.ChatMessage(msg("MatchCancelled", player.UserIDString, player.displayName));
                                target?.ChatMessage(msg("MatchCancelled", target.UserIDString, player.displayName));
                                tdmRequests.Remove(entry.Key);
                                return;
                            }

                            if (tdmRequests.ContainsKey(player.UserIDString))
                            {
                                var target = BasePlayer.activePlayerList.Find(x => x.UserIDString == tdmRequests[player.UserIDString]);
                                player.ChatMessage(msg("MatchCancelled", player.UserIDString, player.displayName));
                                target?.ChatMessage(msg("MatchCancelled", player.UserIDString, player.displayName));
                                tdmRequests.Remove(player.UserIDString);
                                return;
                            }

                            player.ChatMessage(msg("MatchDoesntExist", player.UserIDString, szMatchChatCommand));
                        }
                    }
                    break;
                case "size":
                    {
                        if (deathmatch != null)
                        {
                            if (args.Length == 2)
                            {
                                if (args[1].All(char.IsDigit))
                                {
                                    if (deathmatch.IsHost(player))
                                    {
                                        int size = Convert.ToInt32(args[1]);

                                        if (size < minDeathmatchSize)
                                            size = deathmatch.TeamSize;

                                        if (size > maxDeathmatchSize)
                                            size = maxDeathmatchSize;

                                        if (deathmatch.TeamSize != size)
                                            deathmatch.TeamSize = size; // sends message to all players in the match
                                    }
                                    else
                                        player.ChatMessage(msg("MatchNotAHost", player.UserIDString));
                                }
                                else
                                    player.ChatMessage(msg("InvalidNumber", player.UserIDString, args[1]));
                            }
                            else
                                player.ChatMessage(msg("MatchSizeSyntax", player.UserIDString, szMatchChatCommand));
                        }
                        else
                            player.ChatMessage(msg("MatchDoesntExist", player.UserIDString, szMatchChatCommand));
                    }
                    break;
                case "accept":
                    {
                        if (!tdmRequests.ContainsValue(player.UserIDString))
                        {
                            player.ChatMessage(msg("MatchNoneRequested", player.UserIDString));
                            return;
                        }

                        var success = SetupTeams(tdmRequests.First(kvp => kvp.Value == player.UserIDString));

                        if (success == null) // not naked
                            return;

                        if (success is bool && !(bool)success)
                        {
                            player.ChatMessage(msg("MatchPlayerOffline", player.UserIDString));
                            return;
                        }
                    }
                    break;
                case "leave":
                    {
                        if (deathmatch != null)
                        {
                            deathmatch.RemoveMatchPlayer(player);
                            player.ChatMessage(msg("MatchPlayerLeft", player.UserIDString));
                        }
                        else
                            player.ChatMessage(msg("MatchDoesntExist", player.UserIDString, szMatchChatCommand));
                    }
                    break;
                case "any":
                    {
                        if (tdmMatches.Count == 0)
                        {
                            player.ChatMessage(msg("MatchNoMatchesExist", player.UserIDString, szMatchChatCommand));
                            return;
                        }

                        if (deathmatch != null)
                        {
                            deathmatch.RemoveMatchPlayer(player);
                            player.ChatMessage(msg("MatchPlayerLeft", player.UserIDString));
                        }

                        foreach (var match in tdmMatches)
                        {
                            if (!match.IsFull(Team.Good) && match.AlliedToGoodHost(player))
                            {
                                match.AddToGoodTeam(player);
                                return;
                            }

                            if (!match.IsFull(Team.Evil) && match.AlliedToEvilHost(player))
                            {
                                match.AddToEvilTeam(player);
                                return;
                            }

                            if (match.IsPublic)
                            {
                                if (!match.IsFull(Team.Good))
                                {
                                    match.AddToGoodTeam(player);
                                    return;
                                }

                                if (!match.IsFull(Team.Evil))
                                {
                                    match.AddToEvilTeam(player);
                                    return;
                                }
                            }
                        }

                        player.ChatMessage(msg("MatchNoTeamFoundAny", player.UserIDString, args[0]));
                    }
                    break;
                case "public":
                    {
                        if (deathmatch != null)
                        {
                            if (!deathmatch.IsHost(player))
                            {
                                player.ChatMessage(msg("MatchNotAHost", player.UserIDString));
                                return;
                            }

                            deathmatch.IsPublic = !deathmatch.IsPublic;
                        }
                        else
                            player.ChatMessage(msg("MatchDoesntExist", player.UserIDString, szMatchChatCommand));
                    }
                    break;
                default:
                    {
                        if (args.Length == 2)
                        {
                            string kit = VerifiedKit(args[1]);

                            if (string.IsNullOrEmpty(kit))
                            {
                                player.ChatMessage(msg("MatchChallenge0", player.UserIDString, szMatchChatCommand));
                                player.ChatMessage(msg("KitDoesntExist", player.UserIDString, args[1]));

                                string kits = string.Join(", ", VerifiedKits.ToArray());

                                if (!string.IsNullOrEmpty(kits))
                                    player.ChatMessage("Kits: " + kits);

                                return;
                            }

                            tdmKits[player.UserIDString] = kit;
                        }

                        var target = FindPlayer(args[0]);

                        if (target != null)
                        {
                            if (target == player)
                            {
                                player.ChatMessage(msg("PlayerNotFound", player.UserIDString, args[0]));
                                return;
                            }

                            if (deathmatch != null)
                            {
                                player.ChatMessage(msg("MatchCannotChallengeAgain", player.UserIDString));
                                return;
                            }

                            if (InMatch(target) || tdmRequests.ContainsValue(target.UserIDString))
                            {
                                player.ChatMessage(msg("MatchCannotChallenge", player.UserIDString, target.displayName));
                                return;
                            }

                            if (!IsNewman(player))
                            {
                                player.ChatMessage(msg("MustBeNaked", player.UserIDString));
                                return;
                            }

                            if (!IsNewman(target))
                            {
                                player.ChatMessage(msg("TargetMustBeNaked", player.UserIDString));
                                return;
                            }

                            RequestDeathmatch(player, target);
                            return;
                        }

                        if (tdmMatches.Count == 0)
                        {
                            player.ChatMessage(msg("MatchNoMatchesExist", player.UserIDString, szMatchChatCommand));
                            return;
                        }

                        if (deathmatch != null)
                        {
                            deathmatch.RemoveMatchPlayer(player);
                            player.ChatMessage(msg("MatchPlayerLeft", player.UserIDString));
                        }

                        foreach (var match in tdmMatches)
                        {
                            if (match.GoodCode(args[0]))
                            {
                                match.AddToGoodTeam(player);
                                return;
                            }

                            if (match.EvilCode(args[0]))
                            {
                                match.AddToEvilTeam(player);
                                return;
                            }
                        }

                        player.ChatMessage(msg("MatchNoTeamFoundCode", player.UserIDString, args[0]));

                    }
                    break;
            }
        }

        void RequestDeathmatch(BasePlayer player, BasePlayer target)
        {
            if (tdmRequests.ContainsKey(player.UserIDString))
            {
                tdmRequests.Remove(player.UserIDString);
            }

            target.ChatMessage(msg("MatchRequested", target.UserIDString, player.displayName, szMatchChatCommand));
            player.ChatMessage(msg("MatchRequestSent", player.UserIDString, target.displayName));

            string uid = player.UserIDString;

            tdmRequests.Add(uid, target.UserIDString);

            timer.Once(60f, () =>
            {
                if (tdmRequests.ContainsKey(uid))
                {
                    tdmRequests.Remove(uid);
                }
            });
        }

        bool? SetupTeams(KeyValuePair<string, string> kvp)
        {
            var player = BasePlayer.activePlayerList.Find(x => x.UserIDString == kvp.Key);
            var target = BasePlayer.activePlayerList.Find(x => x.UserIDString == kvp.Value);

            if (player == null || target == null)
                return false;

            if (tdmRequests.ContainsKey(kvp.Key))
                tdmRequests.Remove(kvp.Key);

            if (!IsNewman(player))
            {
                player.ChatMessage(msg("MustBeNaked", player.UserIDString));
                target.ChatMessage(msg("DuelMustBeNaked", target.UserIDString, player.displayName));
                return null;
            }

            if (!IsNewman(target))
            {
                target.ChatMessage(msg("MustBeNaked", target.UserIDString));
                player.ChatMessage(msg("DuelMustBeNaked", player.UserIDString, target.displayName));
                return null;
            }

            RemoveFromQueue(player.UserIDString);
            RemoveFromQueue(target.UserIDString);

            var newMatch = new GoodVersusEvilMatch();
            tdmMatches.Add(newMatch);
            newMatch.Setup(player, target);

            if (tdmMatches.Count == 1)
                SubscribeHooks(true);
            
            return true;
        }

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

            if (!autoAllowAll && !duelsData.Allowed.Contains(player.UserIDString))
            {
                player.ChatMessage(msg("MustAllowDuels", player.UserIDString, szDuelChatCommand));
                return;
            }

            if (InMatch(player))
            {
                player.ChatMessage(msg("MatchTeamed", player.UserIDString));
                return;
            }

            if (DuelTerritory(player.transform.position))
            {
                RemoveFromQueue(player.UserIDString);

                if (dataDuelists.ContainsKey(player.UserIDString))
                {
                    player.ChatMessage(msg("AlreadyInADuel", player.UserIDString));
                    return;
                }
            }

            RemoveRequests(player);

            if (!IsNewman(player))
            {
                player.ChatMessage(msg("MustBeNaked", player.UserIDString));
                return;
            }

            if (player.IsAdmin)
            {
                player.ChatMessage(msg("InQueueList", player.UserIDString));
                player.ChatMessage(string.Join(", ", duelsData.Queued.Select(kvp => GetDisplayName(kvp.Value)).ToArray()));
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

        void ccmdDuel(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            string id = arg.Player()?.UserIDString ?? null;

            if (arg.HasArgs(1))
            {
                switch (arg.Args[0].ToLower())
                {
                    case "removeall":
                        {
                            if (duelingZones.Count > 0 || duelsData.ZoneIds.Count > 0)
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

                                    EjectPlayers(zone);
                                    arg.ReplyWith(msg("RemovedZoneAt", id, zone.Position));
                                    RemoveDuelZone(zone);
                                }

                                duelsData.ZoneIds.Clear();
                                SaveData();
                            }
                            else
                                arg.ReplyWith(msg("NoZoneExists", id));
                        }
                        break;
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

                            if (dataDuelists.Count > 0)
                                arg.ReplyWith(msg("DuelsNowDisabled", id));
                            else
                                arg.ReplyWith(msg("DuelsNowDisabledEmpty", id));

                            foreach (var entry in dataDuelists.ToList())
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

                            if (SetupDuelZone(null) != Vector3.zero)
                            {
                                arg.ReplyWith(msg("ZoneCreated", id));
                            }
                            return;
                        }
                    default:
                        {
                            arg.ReplyWith(string.Format("{0} on|off|new|removeall", szDuelChatCommand));
                            break;
                        }
                }
            }
            else
                arg.ReplyWith(string.Format("{0} on|off|new|removeall", szDuelChatCommand));
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
                if (!args.Any(arg => arg.ToLower() == "new") && !args.Any(arg => arg.ToLower() == "removeall") && !args.Any(arg => arg.ToLower() == "custom"))
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

                if (!autoAllowAll)
                    player.ChatMessage(msg("HelpAllow", player.UserIDString, szDuelChatCommand));

                player.ChatMessage(msg("HelpBlock", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpChallenge", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpAccept", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpCancel", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpChat", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpQueue", player.UserIDString, szQueueChatCommand));
                player.ChatMessage(msg("HelpLadder", player.UserIDString, szDuelChatCommand));
                player.ChatMessage(msg("HelpKit", player.UserIDString, szDuelChatCommand));
                
                if (allowBets)
                    player.ChatMessage(msg("HelpBet", player.UserIDString, szDuelChatCommand));

                if (tdmEnabled)
                    player.ChatMessage(msg("HelpTDM", player.UserIDString, szMatchChatCommand));

                player.ChatMessage(msg("UI_Help", player.UserIDString, szUIChatCommand));

                if (player.IsAdmin)
                {
                    player.ChatMessage(msg("HelpDuelAdmin", player.UserIDString, szDuelChatCommand));
                    player.ChatMessage(msg("HelpDuelAdminRefundAll", player.UserIDString, szDuelChatCommand));
                }

                return;
            }

            switch (args[0].ToLower())
            {
                case "tdm":
                    {
                        if (tdmEnabled)
                        {
                            cmdTDM(player, command, args.Skip(1).ToArray());
                            return;
                        }
                    }
                    break;
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
                                        CreateZoneWalls(hit.point, radius, prefab, null, player);
                                        CheckArenaHooks();
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

                                foreach (var entry in duelsData.Zones)
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

                            foreach (var entity in BaseNetworkable.serverEntities.Where(e => e.name.Contains("wall.external.high")).Cast<BaseEntity>().ToList())
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

                            if (dataDuelists.Count > 0)
                                player.ChatMessage(msg("DuelsNowDisabled", player.UserIDString));
                            else
                                player.ChatMessage(msg("DuelsNowDisabledEmpty", player.UserIDString));

                            foreach (var entry in dataDuelists.ToList())
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

                                var zone = SetupDuelZone(hit.point, null);
                                int i = 0;

                                foreach (var spawn in zone.Spawns)
                                    player.SendConsoleCommand("ddraw.text", 30f, Color.yellow, spawn, ++i);

                                UpdateStability();
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
                                    foreach (var spawn in zone.Spawns.ToList())
                                    {
                                        if (duelsData.Spawns.Contains(spawn.ToString()))
                                        {
                                            duelsData.Spawns.Remove(spawn.ToString());
                                        }
                                    }
                                }

                                EjectPlayers(zone);
                                RemoveDuelZone(zone);
                                player.ChatMessage(msg("RemovedZone", player.UserIDString));
                            }

                            return;
                        }
                    }
                    break;
                case "removeall":
                    {
                        if (player.IsAdmin)
                        {
                            if (duelingZones.Count > 0 || duelsData.ZoneIds.Count > 0)
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

                                    EjectPlayers(zone);
                                    player.ChatMessage(msg("RemovedZoneAt", player.UserIDString, zone.Position));
                                    RemoveDuelZone(zone);
                                }

                                duelsData.ZoneIds.Clear();
                                SaveData();
                            }
                            else
                                player.ChatMessage(msg("NoZoneExists", player.UserIDString));
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

                            if (SetupDuelZone(null) != Vector3.zero)
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
                            var zones = duelingZones.Count > 3 && duelingZones.Any(zone => zone.TotalPlayers > 0) ? duelingZones.Where(zone => zone.TotalPlayers > 0).ToList() : duelingZones; // 0.1.17 if multiple zones then choose from active ones if any exist

                            foreach (var zone in zones)
                            {
                                float distance = zone.Distance(player.transform.position);

                                if (zones.Count > 1 && distance < zoneRadius * 4f) // move admin to the next nearest zone
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
                        string kits = string.Join(", ", VerifiedKits.ToArray());

                        if (args.Length == 2 && !string.IsNullOrEmpty(kits))
                        {
                            string kit = VerifiedKit(args[1]);

                            if (!string.IsNullOrEmpty(kit))
                            {
                                duelsData.CustomKits[player.UserIDString] = kit;
                                player.ChatMessage(msg("KitSet", player.UserIDString, kit));
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

                        foreach (var entry in dataRequests.ToList())
                        {
                            if (entry.Key == player.UserIDString || entry.Value == player.UserIDString)
                            {
                                dataRequests.Remove(entry.Key);
                            }
                        }
                        return;
                    }
                case "block":
                    {
                        if (args.Length >= 2)
                        {
                            var name = string.Join(" ", args.Skip(1).ToArray());
                            BasePlayer target = FindPlayer(name);

                            if (!target)
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

                            foreach (var entry in dataRequests.ToList())
                            {
                                if (entry.Key == player.UserIDString || entry.Value == player.UserIDString)
                                {
                                    dataRequests.Remove(entry.Key);
                                }
                            }

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
                                                var target = BasePlayer.activePlayerList.Find(x => x.UserIDString == kvp.Key);
                                                if (target == null) continue;

                                                Item item = ItemManager.CreateByItemID(kvp.Value.itemid, kvp.Value.amount);

                                                if (!item.MoveToContainer(target.inventory.containerMain, -1, true) && !item.MoveToContainer(target.inventory.containerBelt, -1, true))
                                                    continue;

                                                target.ChatMessage(msg("RefundAllPlayerNotice", target.UserIDString, item.info.displayName.translated, item.amount));
                                                player.ChatMessage(msg("RefundAllAdminNotice", player.UserIDString, target.displayName, target.UserIDString, item.info.displayName.english, item.amount));
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
                                            if (dataRequests.ContainsKey(player.UserIDString) || dataRequests.ContainsValue(player.UserIDString))
                                            {
                                                player.ChatMessage(msg("CannotForfeitRequestDuel", player.UserIDString));
                                                return;
                                            }

                                            if (dataDuelists.ContainsKey(player.UserIDString))
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
                                            if (dataRequests.ContainsKey(player.UserIDString) || dataRequests.ContainsValue(player.UserIDString))
                                            {
                                                player.ChatMessage(msg("CannotRefundRequestDuel", player.UserIDString));
                                                return;
                                            }

                                            if (dataDuelists.ContainsKey(player.UserIDString))
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

                        if (!dataRequests.ContainsValue(player.UserIDString))
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

                        foreach (var kvp in dataRequests)
                        {
                            if (kvp.Value == player.UserIDString)
                            {
                                target = BasePlayer.activePlayerList.Find(x => x.UserIDString == kvp.Key);

                                if (target == null || !target.IsConnected)
                                {
                                    player.ChatMessage(string.Format("DuelCancelledFor", player.UserIDString, GetDisplayName(kvp.Key)));
                                    dataRequests.Remove(kvp.Key);
                                    return;
                                }

                                break;
                            }
                        }

                        if (!IsNewman(target))
                        {
                            player.ChatMessage(msg("TargetMustBeNaked", player.UserIDString));
                            target.ChatMessage(msg("MustBeNaked", target.UserIDString));
                            return;
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
                case "decline":
                    {
                        if (!autoAllowAll && !duelsData.Allowed.Contains(player.UserIDString))
                        {
                            player.ChatMessage(msg("MustAllowDuels", player.UserIDString, szDuelChatCommand));
                            return;
                        }

                        if (dataRequests.ContainsValue(player.UserIDString))
                        {
                            var entry = dataRequests.First(kvp => kvp.Value == player.UserIDString);
                            var target = BasePlayer.activePlayerList.Find(x => x.UserIDString == entry.Key);
                            dataRequests.Remove(entry.Key);
                            target?.ChatMessage(msg("DuelCancelledWith", target.UserIDString, player.displayName));
                            player.ChatMessage(msg("DuelCancelComplete", player.UserIDString));
                            return;
                        }

                        if (dataRequests.ContainsKey(player.UserIDString))
                        {
                            var target = BasePlayer.activePlayerList.Find(x => x.UserIDString == dataRequests[player.UserIDString]);
                            target?.ChatMessage(msg("DuelCancelledWith", target.UserIDString, player.displayName));
                            player.ChatMessage(msg("DuelCancelComplete", player.UserIDString));
                            dataRequests.Remove(player.UserIDString);
                            return;
                        }

                        player.ChatMessage(msg("NoPendingRequests", player.UserIDString));
                        return;
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

                        if (!IsNewman(player))
                        {
                            player.ChatMessage(msg("MustBeNaked", player.UserIDString));
                            return;
                        }

                        if (IsDueling(player))
                        {
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

                        if (dataRequests.ContainsKey(player.UserIDString))
                        {
                            player.ChatMessage(msg("MustWaitForAccept", player.UserIDString, GetDisplayName(dataRequests[player.UserIDString])));
                            return;
                        }

                        if (dataRequests.ContainsValue(target.UserIDString))
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

                        dataRequests.Add(player.UserIDString, target.UserIDString);
                        target.ChatMessage(msg("DuelRequestReceived", target.UserIDString, player.displayName, szDuelChatCommand));
                        player.ChatMessage(msg("DuelRequestSent", player.UserIDString, target.displayName, szDuelChatCommand));

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

                            if (dataRequests.ContainsKey(playerId))
                            {
                                if (player != null && !IsDueling(player))
                                    player.ChatMessage(msg("RequestTimedOut", playerId, targetName));

                                dataRequests.Remove(playerId);
                            }
                        });

                        break;
                    }
                    //
            } // end switch
        }

        BasePlayer FindPlayer(string strNameOrID)
        {
            return BasePlayer.activePlayerList.Find(x => x.UserIDString == strNameOrID) ?? BasePlayer.activePlayerList.Find(x => x.displayName.Contains(strNameOrID, CompareOptions.OrdinalIgnoreCase)) ?? null;
        }

        void ResetTemporaryData() // keep our datafile cleaned up by removing entries which are temporary
        {
            if (duelsData == null)
                duelsData = new StoredData();

            dataDuelists.Clear();
            dataRequests.Clear();
            dataImmunity.Clear();
            dataImmunitySpawns.Clear();
            duelsData.Restricted.Clear();
            dataDeath.Clear();
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
            if (dataImmunity.ContainsKey(targetId))
                dataImmunity.Remove(targetId);

            if (dataImmunitySpawns.ContainsKey(targetId))
                dataImmunitySpawns.Remove(targetId);

            if (dataDuelists.ContainsKey(targetId))
                dataDuelists.Remove(targetId);

            if (dataRequests.ContainsKey(targetId))
                dataRequests.Remove(targetId);

            if (duelsData.Restricted.Contains(targetId))
                duelsData.Restricted.Remove(targetId);

            if (dataDeath.ContainsKey(targetId))
                dataDeath.Remove(targetId);

            if (duelsData.Homes.ContainsKey(targetId) && removeHome)
                duelsData.Homes.Remove(targetId);

            if (duelsData.Kits.ContainsKey(targetId))
                duelsData.Kits.Remove(targetId);

            if (duelingZones.Count > 0)
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
                int removed = 0;

                do
                {
                    removed += RemoveZoneWalls(GetOwnerId(duelsData.ZoneIds[0]));
                    duelsData.AutoGeneratedSpawns.Remove(duelsData.ZoneIds[0]);
                    duelsData.ZoneIds.RemoveAt(0);
                } while (duelsData.ZoneIds.Count > zoneAmount);

                if (removed > 0)
                    Puts(msg("RemovedXWallsCustom", null, removed));
            }

            var entities = BaseNetworkable.serverEntities.Where(e => e.name.Contains("wall.external.high")).Cast<BaseEntity>().ToList();

            foreach (string id in duelsData.ZoneIds) // create all zones that don't already exist
            {
                SetupDuelZone(id.ToVector3(), entities);
            }

            if (autoSetup && duelsData.ZoneIds.Count < zoneAmount) // create each dueling zone that is missing. if this fails then console will be notified
            {
                int attempts = Math.Max(zoneAmount, 5); // 0.1.10 fix - infinite loop fix for when zone radius is too large to fit on the map
                int created = 0;
                do
                {
                    if (SetupDuelZone(entities) != Vector3.zero)
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

            if (duelingZones.Count > 0)
                Puts(msg("ZonesSetup", null, duelingZones.Count));
        }

        Vector3 SetupDuelZone(List<BaseEntity> entities) // starts the process of creating a new or existing zone and then setting up it's own spawn points around the circumference of the zone
        {
            var zonePos = FindDuelingZone(); // a complex process to search the map for a suitable area

            if (zonePos == Vector3.zero) // unfortunately we weren't able to find a location. this is likely due to an extremely high entity count. just try again.
            {
                return Vector3.zero;
            }

            SetupDuelZone(zonePos, entities);
            return zonePos;
        }

        DuelingZone SetupDuelZone(Vector3 zonePos, List<BaseEntity> entities)
        {
            if (!duelsData.ZoneIds.Contains(zonePos.ToString()))
            {
                duelsData.ZoneIds.Add(zonePos.ToString());
                SaveData();
            }

            var newZone = new DuelingZone();

            newZone.Setup(zonePos);
            duelingZones.Add(newZone);

            if (duelingZones.Count == 1)
            {
                Subscribe(nameof(OnPlayerRespawned));
                Subscribe(nameof(OnEntityTakeDamage));
                Subscribe(nameof(OnEntitySpawned));
                Subscribe(nameof(CanBuild));
            }

            CreateZoneWalls(newZone.Position, zoneRadius, zoneUseWoodenWalls ? hewwPrefab : heswPrefab, entities);
            return newZone;
        }
        
        bool RemoveCustomZoneWalls(Vector3 center)
        {
            foreach (var entry in duelsData.Zones.ToList())
            {
                if (Vector3.Distance(entry.Key.ToVector3(), center) <= entry.Value)
                {
                    ulong ownerId = GetOwnerId(entry.Key);
                    duelsData.Zones.Remove(entry.Key);
                    RemoveZoneWalls(ownerId);
                    return true;
                }
            }

            return false;
        }

        int RemoveZoneWalls(ulong ownerId)
        {
            int removed = 0;

            foreach (var entity in BaseNetworkable.serverEntities.Where(e => e.name.Contains("wall.external.high")).Cast<BaseEntity>().ToList())
            {
                if (entity.OwnerID == ownerId)
                {
                    entity.Kill();
                    removed++;
                }
            }

            return removed;
        }

        bool ZoneWallsExist(ulong ownerId, List<BaseEntity> entities)
        {
            if (entities == null || entities.Count < 3)
                entities = BaseNetworkable.serverEntities.Where(e => e.name.Contains("wall.external.high")).Cast<BaseEntity>().ToList();

            foreach (var entity in entities)
            {
                if (entity.OwnerID == ownerId)
                {
                    return true;
                }
            }

            return false;
        }

        void CreateZoneWalls(Vector3 center, float zoneRadius, string prefab, List<BaseEntity> entities, BasePlayer player = null)
        {
            if (!useZoneWalls)
                return;

            var tick = DateTime.Now;
            ulong ownerId = GetOwnerId(center.ToString());

            if (ZoneWallsExist(ownerId, entities))
                return;

            float maxHeight = -200f;
            float minHeight = 200f;
            int spawned = 0;
            int raycasts = Mathf.CeilToInt(360 / zoneRadius * 0.1375f);

            var positions = GetCircumferencePositions(center, zoneRadius, raycasts, 0f);

            foreach (var position in positions) // get our positions and perform the calculations for the highest and lowest points of terrain
            {
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(position.x, position.y + 200f, position.z), Vector3.down, out hit, Mathf.Infinity, wallMask))
                {
                    maxHeight = Mathf.Max(hit.point.y, maxHeight); // calculate the highest point of terrain
                    minHeight = Mathf.Min(hit.point.y, minHeight); // calculate the lowest point of terrain
                    center.y = minHeight; // adjust the spawn point of our walls to that of the lowest point of terrain
                }
            }

            float gap = prefab == heswPrefab ? 0.3f : 0.5f; // the distance used so that each wall fits closer to the other so players cannot throw items between the walls
            int stacks = Mathf.CeilToInt((maxHeight - minHeight) / 6f) + extraWallStacks; // get the amount of walls to stack onto each other to go above the highest point
            float next = 360 / zoneRadius - gap; // the distance apart each wall will be from the other

            for (int i = 0; i < stacks; i++) // create our loop to spawn each stack
            {
                foreach (var position in GetCircumferencePositions(center, zoneRadius, next, center.y)) // get a list positions where each positions difference is the width of a high external stone wall. specify the height since we've already calculated what's required
                {
                    float groundHeight = TerrainMeta.HeightMap.GetHeight(new Vector3(position.x, position.y + 6f, position.z));

                    if (groundHeight > position.y + 9f) // 0.1.13 improved distance check underground
                        continue;

                    if (useLeastAmount && position.y - groundHeight > 18f)
                        continue;

                    var entity = GameManager.server.CreateEntity(prefab, position, default(Quaternion), false);

                    if (entity != null)
                    {
                        entity.OwnerID = ownerId; // set a unique identifier so the walls can be easily removed later
                        entity.transform.LookAt(center, Vector3.up); // have each wall look at the center of the zone
                        entity.Spawn(); // spawn into the game
                        entity.gameObject.SetActive(true); // 0.1.16: fix for animals and explosives passing through walls. set it active after it spawns otherwise AntiHack will throw ProjectileHack: Line of sight warnings each time the entity is hit
                        spawned++; // our counter
                    }
                    else
                        return; // invalid prefab, return or cause massive server lag

                    if (stacks == i - 1)
                    {
                        RaycastHit hit;
                        if (Physics.Raycast(new Vector3(position.x, position.y + 6f, position.z), Vector3.down, out hit, 12f, worldMask))
                        {
                            stacks++; // 0.1.16 fix where rocks could allow a boost in or out of the top of a zone
                        }
                    }
                }

                center.y += 6f; // increase the positions height by one high external stone wall's height
            }

            if (player == null)
                Puts(msg("GeneratedWalls", null, spawned, stacks, FormatPosition(center), (DateTime.Now - tick).TotalSeconds));
            else
                player.ChatMessage(msg("GeneratedWalls", player.UserIDString, spawned, stacks, FormatPosition(center), (DateTime.Now - tick).TotalSeconds));

            Subscribe(nameof(OnEntityTakeDamage));
        }

        static void EjectPlayers(DuelingZone zone)
        {
            foreach (var player in zone.Players)
            {
                EjectPlayer(player);
            }
        }

        static void EjectPlayer(BasePlayer player)
        {
            if (player == null)
                return;

            player.inventory.Strip();
            ins.ResetDuelist(player.UserIDString, false);
            ins.SendHome(player);
        }

        static List<Vector3> GetAutoSpawns(DuelingZone zone)
        {
            var spawns = new List<Vector3>();
            string key = zone.Position.ToString();

            if (duelsData.AutoGeneratedSpawns.ContainsKey(key))
            {
                if (duelsData.AutoGeneratedSpawns[key].Count > 0)
                {
                    spawns.AddRange(duelsData.AutoGeneratedSpawns[key].Select(spawn => spawn.ToVector3())); // use cached spawn points
                }
            }

            if (!duelsData.AutoGeneratedSpawns.ContainsKey(key))
            {
                duelsData.AutoGeneratedSpawns.Add(key, new List<string>());
            }

            if (spawns.Count < 2)
            {
                spawns = ins.CreateSpawnPoints(zone.Position); // create spawn points on the fly
            }

            duelsData.AutoGeneratedSpawns[key] = spawns.Select(spawn => spawn.ToString()).ToList();
            return spawns;
        }

        void RemoveDuelZone(DuelingZone zone)
        {
            string uid = zone.Position.ToString();

            foreach (var match in tdmMatches.ToList())
            {
                if (match.Zone != null && match.Zone == zone)
                {
                    match.End();
                }
            }

            duelsData.ZoneIds.Remove(uid);
            duelsData.AutoGeneratedSpawns.Remove(uid);
            RemoveEntities(zone);
            RemoveZoneWalls(GetOwnerId(uid));            
            zone.Kill();

            if (duelingZones.Count == 0)
            {
                SubscribeHooks(false);
                CheckArenaHooks();
            }
        }

        void RemoveEntities(ulong playerId)
        {
            if (duelEntities.ContainsKey(playerId))
            {
                foreach (var e in duelEntities[playerId].ToList())
                    if (e != null && !e.IsDestroyed)
                        e.Kill();

                duelEntities.Remove(playerId);
            }
        }

        void RemoveEntities(DuelingZone zone)
        {
            foreach (var entry in duelEntities.ToList())
            {
                foreach (var entity in entry.Value.ToList())
                {
                    if (entity == null || entity.IsDestroyed)
                    {
                        duelEntities[entry.Key].Remove(entity);
                        continue;
                    }

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
                if (player.IsDead() && !player.IsConnected && !respawnDead)
                {
                    duelsData.Homes.Remove(player.UserIDString);
                    return;
                }

                if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                {
                    timer.Once(2f, () => SendHome(player));
                    return;
                }

                RemoveEntities(player.userID);
                var homePos = duelsData.Homes[player.UserIDString].ToVector3();

                if (player.IsDead())
                {
                    player.RespawnAt(homePos, default(Quaternion));
                    player.inventory.Strip();

                    if (playerHealth > 0f)
                        player.health = playerHealth;
                }
                else
                {
                    player.inventory.Strip();
                    Teleport(player, homePos);
                }

                GiveRespawnLoot(player);
                duelsData.Homes.Remove(player.UserIDString);

                if (guiAutoEnable || createUI.Contains(player.UserIDString))
                    OnPlayerInit(player);
            }
        }

        void GiveRespawnLoot(BasePlayer player)
        {
            player.inventory.Strip();

            if (respawnLoot.Count > 0)
            {
                foreach (var entry in respawnLoot)
                {
                    Item item = ItemManager.CreateByName(entry.shortname, entry.amount, entry.skin);

                    if (item == null)
                        continue;

                    var container = entry.container == "wear" ? player.inventory.containerWear : entry.container == "belt" ? player.inventory.containerBelt : player.inventory.containerMain;

                    item.MoveToContainer(container, entry.slot);
                }
            }
        }

        void UpdateStability()
        {
            if (noStability)
            {
                Subscribe(nameof(OnEntitySpawned));

                foreach (var block in BaseCombatEntity.serverEntities.Where(e => e is BuildingBlock && (DuelTerritory(e.transform.position) || ArenaTerritory(e.transform.position))).Cast<BuildingBlock>().ToList())
                {
                    if (block.grounded)
                        continue;

                    if (block.OwnerID == 0 || permission.UserHasGroup(block.OwnerID.ToString(), "admin"))
                    {
                        block.grounded = true;
                    }
                }
            }
        }

        void CheckArenaHooks(bool message = false)
        {
            if (duelsData.Zones.Count > 0)
            {
                Subscribe(nameof(OnEntityTakeDamage));

                if (customArenasNoBuilding)
                    Subscribe(nameof(CanBuild));

                if (noStability)
                    Subscribe(nameof(OnEntitySpawned));

                if (message)
                    Puts(msg("ArenasSetup", null, duelsData.Zones.Count));
            }

            if (respawnWalls && (duelsData.Zones.Count > 0 || duelingZones.Count > 0))
            {
                Subscribe(nameof(OnEntityDeath));
                Subscribe(nameof(OnEntityKill));
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
            if (dataImmunity.Count > 0) // each player that spawns into a dueling zone is given immunity for X seconds. here we'll keep track of this and remove their immunities
            {
                var timeStamp = TimeStamp();

                foreach (var kvp in dataImmunity.ToList())
                {
                    var target = BasePlayer.activePlayerList.Find(x => x.UserIDString == kvp.Key);

                    if (kvp.Value - timeStamp <= 0)
                    {
                        dataImmunity.Remove(kvp.Key);
                        dataImmunitySpawns.Remove(kvp.Key);
                        target?.ChatMessage(msg("ImmunityFaded", target.UserIDString));
                    }
                    else
                    {
                        if (noMovement)
                        {
                            var dest = dataImmunitySpawns[target.UserIDString];
                            target.Teleport(dest);
                        }
                    }
                }
            }

            if (dataDeath.Count > 0) // keep track of how long the match has been going on for, and if it's been too long then kill the player off.
            {
                var timeStamp = TimeStamp();

                foreach (var kvp in dataDeath.ToList())
                {
                    if (kvp.Value - timeStamp <= 0)
                    {
                        var target = BasePlayer.activePlayerList.Find(x => x.UserIDString == kvp.Key);
                        dataDeath.Remove(kvp.Key);

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
            if (!init || duelsData == null || (dataDuelists.Count == 0 && tdmMatches.Count == 0) || !flag)
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
                Unsubscribe(nameof(OnPlayerHealthChange));
                Unsubscribe(nameof(OnEntityDeath));
                Unsubscribe(nameof(OnEntityKill));
                Unsubscribe(nameof(OnServerCommand));
                Unsubscribe(nameof(OnPlayerInit));
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
                Subscribe(nameof(OnPlayerHealthChange));
                Subscribe(nameof(OnEntityTakeDamage));
                Subscribe(nameof(OnEntityDeath));

                if (useBlacklistCommands || useWhitelistCommands)
                    Subscribe(nameof(OnServerCommand));

                if (respawnWalls)
                    Subscribe(nameof(OnEntityKill));
            }
        }

        // Helper methods which are essential for the plugin to function. Do not modify these.
        static bool DuelTerritory(Vector3 position, float offset = 0f)
        {
            if (!ins.init)
                return false;

            foreach (var zone in duelingZones) // 0.1.21: arena can be inside of the zone at any height
            {
                var zonePos = new Vector3(zone.Position.x, 0f, zone.Position.z);
                var currentPos = new Vector3(position.x, 0f, position.z);

                if (Vector3.Distance(zonePos, currentPos) <= zoneRadius + offset)
                    return true;
            }

            return false;
        }

        bool ArenaTerritory(Vector3 position, float offset = 0f)
        {
            if (!init)
                return false;

            foreach (var zone in duelsData.Zones) // 0.1.21: arena can be inside of the zone at any height
            {
                var zoneVector = zone.Key.ToVector3();
                var zonePos = new Vector3(zoneVector.x, 0f, zoneVector.z);
                var currentPos = new Vector3(position.x, 0f, position.z);

                if (Vector3.Distance(zonePos, currentPos) <= zone.Value + offset)
                    return true;
            }

            return false;
        }

        ulong GetOwnerId(string uid) => Convert.ToUInt64(Math.Abs(uid.GetHashCode()));
        static bool IsDueling(BasePlayer player) => ins.init && duelsData != null && duelingZones.Count > 0 && player != null && dataDuelists.ContainsKey(player.UserIDString) && DuelTerritory(player.transform.position);
        bool IsEventBanned(string targetId) => duelsData.Bans.ContainsKey(targetId);
        long TimeStamp() => (DateTime.Now.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks) / 10000000;
        string GetDisplayName(string targetId) => covalence.Players.FindPlayerById(targetId)?.Name ?? targetId;
        void Log(string file, string message, bool timestamp = false) => LogToFile(file, $"[{DateTime.Now.ToString()}] {message}", this, timestamp);
        bool InDeathmatch(BasePlayer player) => init && tdmMatches.Any(team => team.GetTeam(player) != Team.None) && DuelTerritory(player.transform.position);
        GoodVersusEvilMatch GetMatch(BasePlayer player) => tdmMatches.FirstOrDefault(team => team.GetTeam(player) != Team.None);
        bool InMatch(BasePlayer target) => tdmMatches.Any(team => team.GetTeam(target) != Team.None);

        static bool IsOnConstruction(Vector3 position)
        {
            position.y += 1f;
            RaycastHit hit;

            return Physics.Raycast(position, Vector3.down, out hit, 1.5f, ins.constructionMask) && hit.GetEntity() != null;
        }

        bool Teleport(BasePlayer player, Vector3 destination)
        {
            if (player == null || destination == Vector3.zero) // don't send a player to their death. this should never happen
                return false;

            if (DestroyUI(player) && !createUI.Contains(player.UserIDString))
                createUI.Add(player.UserIDString);

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

            //RemoveFromQueue(player.UserIDString);
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

            if (!lustyMarkers.Contains(player.UserIDString))
            {
                lustyMarkers.Add(player.UserIDString);
                LustyMap?.Call("DisableMaps", player);
            }
            else
            {
                lustyMarkers.Remove(player.UserIDString);
                LustyMap?.Call("EnableMaps", player);
            }

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
                    Vis.Colliders(position, zoneRadius + differential, colliders, blockedMask, QueryTriggerInteraction.Collide); // get all colliders using the provided layermask

                    if (colliders.Count > 0) // if any colliders were found from the blockedMask then we don't want this as our dueling zone. retry.
                        position = Vector3.zero;

                    Pool.FreeList<Collider>(ref colliders);

                    if (position != Vector3.zero) // so far so good, let's measure the highest and lowest points of the terrain, and count the amount of water colliders
                    {
                        var positions = GetCircumferencePositions(position, zoneRadius - differential, 1f, 0f); // gather positions around the purposed zone
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

                if (DuelTerritory(position, zoneRadius + differential)) // check if position overlaps an existing zone
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
            foreach (var position in GetCircumferencePositions(center, zoneRadius - differential, 10f, 0f))
            {
                var hits = Physics.RaycastAll(new Vector3(position.x, TerrainMeta.HighestPoint.y + 200f, position.z), Vector3.down, Mathf.Infinity);

                if (hits.Count() > 0) // low failure rate
                {
                    float y = TerrainMeta.HeightMap.GetHeight(position);

                    if (avoidWaterSpawns)
                    {
                        float waterLevel = TerrainMeta.WaterMap.GetHeight(position); // 0.1.16: better method to check water level

                        if (waterLevel - y > 0.8f)
                        {
                            continue;
                        }
                    }

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
                        }
                    }

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
                if (resetSeed)
                {
                    duelsData.VictoriesSeed.Clear();
                    duelsData.LossesSeed.Clear();
                }
                duelsData.Spawns.Clear();
                duelsData.AutoGeneratedSpawns.Clear();
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
                Puts(msg("Logged", null, string.Format("{0}{1}{2}_{3}-{4}.txt", Interface.Oxide.LogDirectory, System.IO.Path.DirectorySeparatorChar, Name.Replace(" ", "").ToLower(), "awards", DateTime.Now.ToString("yyyy-MM-dd"))));

            return true;
        }

        bool IsNewman(BasePlayer player) // count the players items. exclude rocks and torchs
        {
            if (bypassNewmans || saveRestoreEnabled)
                return true;

            int count = player.inventory.AllItems().Count();

            foreach (var entry in respawnLoot)
            {
                count -= GetAmount(player, entry.shortname);
            }

            return count == 0;
        }

        int GetAmount(BasePlayer player, string shortname)
        {
            var list = player.inventory.AllItems().Where(x => x.info.shortname == shortname.ToLower());
            int num = 0;
            foreach (Item current in list)
            {
                num += current.amount;
            }
            return num;
        }

        static bool RemoveFromQueue(string targetId)
        {
            foreach (var kvp in duelsData.Queued)
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

            var player = BasePlayer.activePlayerList.Find(x => x.UserIDString == playerId);
            var target = BasePlayer.activePlayerList.Find(x => x.UserIDString == targetId);

            if (player == null || !player.CanInteract() || InMatch(player))
            {
                if (RemoveFromQueue(playerId))
                    CheckQueue();

                return;
            }

            if (target == null || !player.CanInteract() || InMatch(player))
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
            var zones = duelingZones.Where(zone => !zone.IsFull && !zone.IsLocked && zone.Spawns.Count >= requiredMinSpawns && zone.Spawns.Count <= requiredMaxSpawns).ToList();

            do
            {
                var zone = zones.GetRandom();
                var success = zone.AddWaiting(player, target);

                if (success == null) // user must pay the duel entry fee first
                    return true;

                if ((bool)success)
                {
                    Initiate(player, target, false, zone);
                    return true;
                }

                zones.Remove(zone);
            } while (zones.Count > 0);

            return false;
        }

        string GetRandomKit()
        {
            VerifyKits();

            string kit = lpDuelingKits.Count > 0 ? lpDuelingKits.GetRandom() : null;

            if (hpDuelingKits.Count > 0 && UnityEngine.Random.Range(0.0f, 1.0f) > lesserKitChance)
                kit = hpDuelingKits.GetRandom();

            if (kit == null)
            {
                if (_hpDuelingKits.All(entry => customKits.Keys.Select(key => key.ToLower()).Contains(entry.ToLower()))) // 0.1.17 compatibility when adding custom kits to kits section
                {
                    if (_lpDuelingKits.All(entry => customKits.Keys.Select(key => key.ToLower()).Contains(entry.ToLower())))
                    {
                        kit = _lpDuelingKits.GetRandom();

                        if (UnityEngine.Random.Range(0.0f, 1.0f) > lesserKitChance)
                            kit = _hpDuelingKits.GetRandom();

                        return customKits.Keys.Single(entry => kit.Equals(entry, StringComparison.CurrentCultureIgnoreCase));
                    }
                }

                kit = customKits.Count > 0 ? customKits.ToList().GetRandom().Key : null;
            }

            return kit;
        }

        void Initiate(BasePlayer player, BasePlayer target, bool checkInventory, DuelingZone destZone)
        {
            try
            {
                if (player == null || target == null || destZone == null)
                    return;

                if (dataRequests.ContainsKey(player.UserIDString)) dataRequests.Remove(player.UserIDString);
                if (dataRequests.ContainsKey(target.UserIDString)) dataRequests.Remove(target.UserIDString);

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

                foreach (var spawn in destZone.Spawns) // get the furthest spawn point away from the player and assign it to target
                {
                    float distance = Vector3.Distance(spawn, playerSpawn);

                    if (distance > dist)
                    {
                        dist = distance;
                        targetSpawn = spawn;
                    }
                }

                string kit = GetRandomKit();

                if (duelsData.CustomKits.ContainsKey(player.UserIDString) && duelsData.CustomKits.ContainsKey(target.UserIDString))
                {
                    string playerKit = duelsData.CustomKits[player.UserIDString];
                    string targetKit = duelsData.CustomKits[target.UserIDString];

                    if (playerKit.Equals(targetKit, StringComparison.CurrentCultureIgnoreCase))
                    {
                        string verifiedKit = VerifiedKit(playerKit);

                        if (!string.IsNullOrEmpty(verifiedKit))
                        {
                            kit = verifiedKit;
                        }
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
                    dataImmunity[player.UserIDString] = TimeStamp() + immunityTime;
                    dataImmunity[target.UserIDString] = TimeStamp() + immunityTime;
                    dataImmunitySpawns[player.UserIDString] = playerSpawn;
                    dataImmunitySpawns[target.UserIDString] = targetSpawn;
                }

                dataDuelists[player.UserIDString] = target.UserIDString;
                dataDuelists[target.UserIDString] = player.UserIDString;
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

        bool IsAllied(string playerId, string targetId)
        {
            var player = BasePlayer.activePlayerList.Find(x => x.UserIDString == playerId);
            var target = BasePlayer.activePlayerList.Find(x => x.UserIDString == targetId);

            if (player == null || target == null)
                return false;

            return IsAllied(player, target);
        }

        bool IsAllied(BasePlayer player, BasePlayer target)
        {
            if (player.IsAdmin && target.IsAdmin)
                return false;

            return IsInSameClan(player, target) || IsAuthorizing(player, target) || IsBunked(player, target) || IsCodeAuthed(player, target) || IsInSameBase(player, target);
        }

        bool IsInSameClan(BasePlayer player, BasePlayer target) // 1st method
        {
            if (player == null || target == null)
                return true;

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
            foreach (var codelock in BaseNetworkable.serverEntities.Where(e => e is CodeLock).Cast<CodeLock>())
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

            foreach (var priv in player.buildingPrivilege)
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

        void Metabolize(BasePlayer player, bool set) // we don't want the elements to harm players since the zone can spawn anywhere on the map!
        {
            if (player?.metabolism == null)
                return;

            if (set)
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
            }
            else
            {
                player.metabolism.oxygen.min = 0;
                player.metabolism.oxygen.max = 1;
                player.metabolism.temperature.min = -100;
                player.metabolism.temperature.max = 100;
                player.metabolism.wetness.min = 0;
                player.metabolism.wetness.max = 1;
            }

            player.metabolism.SendChangesToClient();
        }

        bool IsKit(string kit)
        {
            return (bool)(Kits?.Call("isKit", kit) ?? false);
        }

        static void AwardPlayer(ulong playerId, double money, int points)
        {
            var player = BasePlayer.activePlayerList.Find(x => x.userID == playerId);

            if (money > 0.0)
            {
                if (ins.Economics != null)
                {
                    ins.Economics?.Call("Deposit", playerId, money);

                    if (player != null)
                        player.ChatMessage(ins.msg("EconomicsDeposit", player.UserIDString, money));
                }
            }

            if (points > 0)
            {
                if (ins.ServerRewards != null)
                {
                    var success = ins.ServerRewards?.Call("AddPoints", playerId, points);

                    if (player != null && success != null && success is bool && (bool)success)
                        player.ChatMessage(ins.msg("ServerRewardPoints", player.UserIDString, points));
                }
            }
        }

        void GivePlayerKit(BasePlayer player)
        {
            if (player == null || !duelsData.Kits.ContainsKey(player.UserIDString))
                return;

            string kit = duelsData.Kits[player.UserIDString];
            duelsData.Kits.Remove(player.UserIDString);

            player.inventory.Strip(); // remove rocks and torches

            if (kit != null && IsKit(kit))
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

            foreach (var dki in customKits[kit])
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

                if (useWorkshopSkins)
                    skins.AddRange(Rust.Workshop.Approved.All.Where(skin => !string.IsNullOrEmpty(skin.Skinnable.ItemName) && skin.Skinnable.ItemName == def.shortname).Select(skin => skin.WorkshopdId));

                if (skins.Contains(0uL))
                    skins.Remove(0uL);

                skinsCache.Add(def.shortname, skins);
            }

            return skinsCache[def.shortname];
        }

        void RemoveRequests(BasePlayer player)
        {
            if (dataRequests.ContainsKey(player.UserIDString) || dataRequests.ContainsValue(player.UserIDString))
            {
                foreach (var entry in dataRequests.ToList())
                {
                    if (entry.Key == player.UserIDString || entry.Value == player.UserIDString)
                    {
                        dataRequests.Remove(entry.Key);
                    }
                }
            }
        }

        #region UI Creation 
        List<string> createUI = new List<string>();
        List<string> duelistUI = new List<string>();
        List<string> kitsUI = new List<string>();
        List<string> matchesUI = new List<string>();

        [ConsoleCommand("UI_DuelistCommand")]
        void ccmdDuelistUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (!player || !arg.HasArgs())
                return;

            switch (arg.Args[0])
            {
                case "accept":
                    {
                        if (dataRequests.ContainsValue(player.UserIDString))
                        {
                            cmdDuel(player, szDuelChatCommand, new string[] { "accept" });
                            break;
                        }
                        else if (tdmRequests.ContainsValue(player.UserIDString))
                        {
                            cmdTDM(player, szMatchChatCommand, new string[] { "accept" });
                            break;
                        }

                        player.ChatMessage(msg("NoPendingRequests2", player.UserIDString));
                    }
                    break;
                case "decline":
                    {
                        if (dataRequests.ContainsKey(player.UserIDString) || dataRequests.ContainsValue(player.UserIDString))
                        {
                            cmdDuel(player, szDuelChatCommand, new string[] { "decline" });
                            break;
                        }

                        var deathmatch = tdmMatches.FirstOrDefault(x => x.GetTeam(player) != Team.None);

                        if (deathmatch != null || tdmRequests.ContainsValue(player.UserIDString) || tdmRequests.ContainsKey(player.UserIDString))
                        {
                            cmdTDM(player, szMatchChatCommand, new string[] { "decline" });
                            break;
                        }

                        player.ChatMessage(msg("NoPendingRequests", player.UserIDString));
                    }
                    break;
                case "closeui":
                    {
                        DestroyUI(player);
                    }
                    return;
                case "kits":
                    {
                        ToggleKitUI(player);
                    }
                    break;
                case "public":
                    {
                        cmdTDM(player, szMatchChatCommand, new string[] { "public" });
                    }
                    break;
                case "queue":
                    {
                        if (IsDueling(player) || InDeathmatch(player))
                            break;

                        cmdQueue(player, szQueueChatCommand, new string[0]);
                    }
                    break;
                case "tdm":
                    {
                        ToggleMatchUI(player);
                    }
                    break;
                case "kit":
                    {
                        if (arg.Args.Length != 2)
                            return;

                        var match = GetMatch(player);

                        if (match != null && match.IsHost(player))
                        {
                            if (!match.IsStarted)
                                match.Kit = arg.Args[1];

                            break;
                        }

                        if (duelsData.CustomKits.ContainsKey(player.UserIDString) && duelsData.CustomKits[player.UserIDString] == arg.Args[1])
                        {
                            duelsData.CustomKits.Remove(player.UserIDString);
                            player.ChatMessage(msg("ResetKit", player.UserIDString));
                            break;
                        }

                        string kit = VerifiedKit(arg.Args[1]);

                        if (string.IsNullOrEmpty(kit))
                            break;

                        duelsData.CustomKits[player.UserIDString] = kit;
                        player.ChatMessage(msg("KitSet", player.UserIDString, kit));
                    }
                    break;
                case "joinmatch":
                    {
                        if (arg.Args.Length != 2)
                            return;

                        if (IsDueling(player))
                            break;

                        var match = GetMatch(player);

                        if (match != null)
                        {
                            if (match.IsStarted)
                                break;

                            match.RemoveMatchPlayer(player);
                        }

                        var newMatch = tdmMatches.FirstOrDefault(x => x.Id == arg.Args[1] && x.IsPublic);

                        if (newMatch == null || newMatch.IsFull() || newMatch.IsStarted || newMatch.IsOver)
                        {
                            player.ChatMessage(msg("MatchNoLongerValid", player.UserIDString));
                            break;
                        }

                        if (newMatch.GetTeam(player) != Team.None)
                            break;

                        if (!newMatch.IsFull(Team.Good))
                            newMatch.AddToGoodTeam(player);
                        else
                            newMatch.AddToEvilTeam(player);

                        if (matchesUI.Contains(player.UserIDString))
                        {
                            CuiHelper.DestroyUi(player, "DuelistUI_Matches");
                            matchesUI.Remove(player.UserIDString);
                        }
                    }
                    break;
                case "size":
                    {
                        if (arg.Args.Length != 2 || !arg.Args[1].All(char.IsDigit))
                            break;
                        
                        cmdTDM(player, szMatchChatCommand, new string[] { "size", arg.Args[1] });
                    }
                    break;
                case "any":
                    {
                        cmdTDM(player, szMatchChatCommand, new string[] { "any" });
                    }
                    break;
            }

            RefreshUI(player);
        }

        void DestroyAllUI()
        {
            foreach(var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        bool DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "DuelistUI_Options");
            CuiHelper.DestroyUi(player, "DuelistUI_Kits");
            CuiHelper.DestroyUi(player, "DuelistUI_Matches");

            return duelistUI.Remove(player.UserIDString);
        }

        void cmdDUI(BasePlayer player, string command, string[] args)
        {
			DestroyUI(player);

            var buttons = new List<string> { "UI_Accept", "UI_Decline", "UI_Kits", "UI_Public", "UI_Queue", "UI_TDM", "UI_Any" };
            var element = UI.CreateElementContainer("DuelistUI_Options", "0 0 0 0.5", "0.915 0.148", "0.981 0.441", guiUseCursor);

            UI.CreateButton(ref element, "DuelistUI_Options", "0.29 0.49 0.69 0.5", "X", 14, "0.786 0.925", "1 1", "UI_DuelistCommand closeui");

            for (int number = 0; number < buttons.Count; number++)
            {
                var pos = UI.CalcButtonPos(number + 1, 2f);
                string uicommand = buttons[number].Replace("UI_", "").ToLower();
                string text = buttons[number].StartsWith("UI_") ? msg(buttons[number], player.UserIDString) : buttons[number];
                UI.CreateButton(ref element, "DuelistUI_Options", "0.29 0.49 0.69 0.5", text, 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_DuelistCommand {uicommand}");
            }

            if (!duelistUI.Contains(player.UserIDString))
                duelistUI.Add(player.UserIDString);

            CuiHelper.AddUi(player, element);
        }

        void RefreshUI(BasePlayer player)
        {
            cmdDUI(player, szUIChatCommand, new string[0]);

            if (kitsUI.Contains(player.UserIDString))
            {
                kitsUI.Remove(player.UserIDString);
                ToggleKitUI(player);
            }
            else if (matchesUI.Contains(player.UserIDString))
            {
                matchesUI.Remove(player.UserIDString);
                ToggleMatchUI(player);
            }
        }

        void ToggleMatchUI(BasePlayer player)
        {
            if (matchesUI.Contains(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, "DuelistUI_Matches");
                matchesUI.Remove(player.UserIDString);
                return;
            }

            if (kitsUI.Contains(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, "DuelistUI_Kits");
                kitsUI.Remove(player.UserIDString);
            }

            var element = UI.CreateElementContainer("DuelistUI_Matches", "0 0 0 0.5", "0.669 0.148", "0.903 0.541");
            var matches = tdmMatches.Where(x => x.IsPublic && !x.IsStarted && !x.IsFull()).ToList();

            for (int number = 0; number < matches.Count; number++)
            {
                var pos = UI.CalcButtonPos(number);
                UI.CreateButton(ref element, "DuelistUI_Matches", "0.29 0.49 0.69 0.5", matches[number].Versus, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_DuelistCommand joinmatch {matches[number].Id}");
            }

            var match = GetMatch(player);
            string teamSize = msg("UI_TeamSize", player.UserIDString);

            for (int size = Math.Max(2, minDeathmatchSize); size < maxDeathmatchSize + 1; size++)
            {
                var pos = UI.CalcButtonPos(size + matches.Count);
                string color = (match != null && match.TeamSize == size || size == minDeathmatchSize) ? "0.69 0.49 0.29 0.5" : "0.29 0.49 0.69 0.5";
                UI.CreateButton(ref element, "DuelistUI_Matches", color, teamSize + size.ToString(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_DuelistCommand size {size}");
            }

            if (matches.Count == 0)
                UI.CreateLabel(ref element, "DuelistUI_Matches", "1 1 1 1", msg("NoMatchesExistYet", player.UserIDString), 14, "0.047 0.73", "1 0.89");

            CuiHelper.AddUi(player, element);
            matchesUI.Add(player.UserIDString);
        }

        void ToggleKitUI(BasePlayer player)
        {
            if (kitsUI.Contains(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, "DuelistUI_Kits");
                kitsUI.Remove(player.UserIDString);
                return;
            }

            if (matchesUI.Contains(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, "DuelistUI_Matches");
                matchesUI.Remove(player.UserIDString);
            }

            var element = UI.CreateElementContainer("DuelistUI_Kits", "0 0 0 0.5", "0.669 0.148", "0.903 0.541");
            var kits = VerifiedKits;
            string kit = duelsData.CustomKits.ContainsKey(player.UserIDString) ? duelsData.CustomKits[player.UserIDString] : null;

            for (int number = 0; number < kits.Count; number++)
            {
                var pos = UI.CalcButtonPos(number);

                UI.CreateButton(ref element, "DuelistUI_Kits", kits[number] == kit ? "0.69 0.49 0.29 0.5" : "0.29 0.49 0.69 0.5", kits[number], 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_DuelistCommand kit {kits[number]}");
            }

            CuiHelper.AddUi(player, element);
            kitsUI.Add(player.UserIDString);
        }
        
        void UpdateKitUI()
        {
            foreach(string userId in kitsUI.ToList())
            {
                kitsUI.Remove(userId);
                var player = BasePlayer.activePlayerList.Find(x => x.UserIDString == userId);

                if (player != null)
                {
                    CuiHelper.DestroyUi(player, "DuelistUI_Kits");
                    ToggleKitUI(player);
                }                
            }
        }

        float lastMatchUpdateTick = 0f;

        void UpdateMatchUI()
        {
            if (lastMatchUpdateTick - Time.time > 0f)
            {
                timer.Once(0.1f, () => UpdateMatchUI());
                return;
            }

            lastMatchUpdateTick = Time.time + 0.5f;

            foreach (string userId in matchesUI.ToList())
            {
                matchesUI.Remove(userId);
                var player = BasePlayer.activePlayerList.Find(x => x.UserIDString == userId);

                if (player != null)
                {
                    CuiHelper.DestroyUi(player, "DuelistUI_Matches");
                    ToggleMatchUI(player);
                }
            }
        }

        public class UI // Credit: Absolut
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = cursor
                        },
                        new CuiElement().Parent,
                        panelName
                    }
                };
                return NewElement;
            }

            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = 1.0f, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            static public float[] CalcButtonPos(int number, float dMinOffset = 1f)
            {
                Vector2 position = new Vector2(0.03f, 0.889f);
                Vector2 dimensions = new Vector2(0.45f * dMinOffset, 0.1f);
                float offsetY = 0;
                float offsetX = 0;
                if (number >= 0 && number < 9)
                {
                    offsetY = (-0.01f - dimensions.y) * number;
                }
                if (number > 8 && number < 19)
                {
                    offsetY = (-0.01f - dimensions.y) * (number - 9);
                    offsetX = (0.04f + dimensions.x) * 1;
                }
                Vector2 offset = new Vector2(offsetX, offsetY);
                Vector2 posMin = position + offset;
                Vector2 posMax = posMin + dimensions;
                return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
            }
        }
        
        #endregion

        #region Config
        private bool Changed;
        string szMatchChatCommand;
        string szDuelChatCommand;
        string szQueueChatCommand;
        string duelistPerm = "duelist.dd";
        string duelistGroup = "duelist";
        static float zoneRadius;
        int deathTime;
        int immunityTime;
        int zoneCounter;
        static List<string> _hpDuelingKits = new List<string>();
        static List<string> _lpDuelingKits = new List<string>();
        static List<string> hpDuelingKits = new List<string>();
        static List<string> lpDuelingKits = new List<string>();
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
        static bool broadcastDefeat;
        double economicsMoney;
        static double requiredDuelMoney;
        int serverRewardsPoints;
        float damagePercentageScale;
        int zoneAmount;
        static int playersPerZone;
        bool visibleToAdmins;
        float spDrawTime;
        float spRemoveOneMaxDistance;
        float spRemoveAllMaxDistance;
        //bool spRemoveInRange;
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
        bool dmFF;
        static int minDeathmatchSize;
        int maxDeathmatchSize;
        bool autoEnable;
        static ulong teamGoodShirt;
        static ulong teamEvilShirt;
        static string teamShirt;
        static double teamEconomicsMoney;
        static int teamServerRewardsPoints;
        static float lesserKitChance;
        bool tdmEnabled;
        bool useLeastAmount;
        bool tdmServerDeaths;
        bool tdmMatchDeaths;
        List<string> whitelistCommands = new List<string>();
        bool useWhitelistCommands;
        List<string> blacklistCommands = new List<string>();
        bool useBlacklistCommands;
        bool bypassNewmans;
        bool saveRestoreEnabled;
        List<DuelKitItem> respawnLoot = new List<DuelKitItem>();
        bool respawnDead;
        bool resetSeed;
        bool noStability;
        bool noMovement;
        static bool requireTeamSize;
        static int requiredMinSpawns;
        static int requiredMaxSpawns;
        bool guiAutoEnable;
        bool guiUseCursor;
        string szUIChatCommand;
        bool useWorkshopSkins;
        bool respawnWalls;

        List<object> RespawnLoot
        {
            get
            {
                return new List<object>
                {
                    new DuelKitItem() { shortname = "rock", amount = 1, skin = 0, container = "belt", slot = -1 },
                    new DuelKitItem() { shortname = "torch", amount = 1, skin = 0, container = "belt", slot = -1 },
                };
            }
        }

        List<object> BlacklistedCommands
        {
            get
            {
                return new List<object> { "/tp", "/remove", "/bank", "/shop", "/event", "/rw", "/home", "/trade", };
            }
        }

        List<object> WhitelistedCommands
        {
            get
            {
                return new List<object> { "/report", "/pm", "/r", "/help", };
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

        static Dictionary<string, List<DuelKitItem>> customKits = new Dictionary<string, List<DuelKitItem>>();

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
                ["TopAll"] = "[ <color=#ffff00>Top Duelists Of All Time ({0})</color> ]:",
                ["Top"] = "[ <color=#ffff00>Top Duelists ({0})</color> ]:",
                ["NoLongerQueued"] = "You are no longer in queue for a duel.",
                ["TryQueueAgain"] = "Please try to queue again.",
                ["InQueueSuccess"] = "You are now in queue for a duel. You will teleport instantly when a match is available.",
                ["MustBeNaked"] = "<color=red>You must be naked before you can duel.</color>",
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
                ["MoneyRequired"] = "Both players must be able to pay an entry fee of <color=#008000>${0}</color> to duel.",
                ["CannotShop"] = "You are not allowed to shop while dueling.",
                ["DuelRequestSent"] = "Sent request to duel <color=lime>{0}</color>. Request expires in 1 minute. Use <color=orange>/{1} cancel</color> to cancel this request.",
                ["DuelRequestReceived"] = "<color=lime>{0}</color> has requested a duel. You have 1 minute to type <color=orange>/{1} accept</color> to accept the duel, or use <color=orange>/{1} decline</color> to decline immediately.",
                ["MatchQueued"] = "You have entered the deathmatch queue. The match will start when a dueling zone becomes available.",
                ["MatchTeamed"] = "You are not allowed to do this while on a deathmatch team.",
                ["MatchNoMatchesExist"] = "No matches exist. Challenge a player by using <color=orange>/{0} name</color>",
                ["MatchStarted"] = "Your match is starting versus: <color=yellow>{0}</color>",
                ["MatchStartedAlready"] = "Your match has already started. You must wait for it to end.",
                ["MatchPlayerLeft"] = "You have removed yourself from your deathmatch team.",
                ["MatchCannotChallenge"] = "{0} is already in a match.",
                ["MatchCannotChallengeAgain"] = "You can only challenge one player at a time.",
                ["MatchRequested"] = "<color=lime>{0}</color> has requested a deathmatch. Use <color=orange>/{1} accept</color> to accept this challenge.",
                ["MatchRequestSent"] = "Match request sent to <color=lime>{0}</color>.",
                ["MatchNoneRequested"] = "No one has challenged you to a deathmatch yet.",
                ["MatchPlayerOffline"] = "The player challenging you is no longer online.",
                ["MatchSizeChanged"] = "Deathmatch changed to <color=yellow>{0}v{0}</color>.",
                ["MatchOpened"] = "Your deathmatch is now open for private invitation. Friends may use <color=orange>/{0} any</color>, and players may use <color=orange>/{0} {1}</color> to join your team. Use <color=orange>/{0} public</color> to toggle invitations as public or private.",
                ["MatchCancelled"] = "{0} has cancelled the deathmatch.",
                ["MatchNotAHost"] = "You must be a host of a deathmatch to use this command.",
                ["MatchDoesntExist"] = "You are not in a deathmatch. Challenge a player by using <color=orange>/{0} name</color>.",
                ["MatchSizeSyntax"] = "Invalid syntax, use /{0} size #",
                ["MatchTeamFull"] = "Team is full ({0} players)",
                ["MatchJoinedTeam"] = "{0} has joined {1}'s team ({2}/{3} players). {4}'s team has {5}/{3} players.",
                ["MatchNoPlayersLeft"] = "No players are left on the opposing team. Match cancelled.",
                ["MatchChallenge2"] = "<color=#5A397A>/{0} any</color><color=#5A625B> • Join any match where a friend is the host</color>",
                ["MatchChallenge3"] = "<color=#5A397A>/{0} <code></color><color=#5A625B> • Join a match with the provided code</color>",
                ["MatchAccept"] = "<color=#5A397A>/{0} accept</color><color=#5A625B> • Accept a challenge</color>",
                ["MatchCancel"] = "<color=#5A397A>/{0} cancel</color><color=#5A625B> • Cancel your match request</color>",
                ["MatchLeave"] = "<color=#5A397A>/{0} cancel</color><color=#5A625B> • Leave your match</color>",
                ["MatchSize"] = "<color=#5A397A>/{0} size #</color><color=#5A625B> • Set your match size ({1}v{1}) [Hosts Only]</color>",
                ["MatchKickBan"] = "<color=#5A397A>/{0} kickban id/name</color><color=#5A625B> • Kickban a player from the match [Host Only]</color>",
                ["MatchSetCode"] = "<color=#5A397A>/{0} setcode [code]</color><color=#5A625B> • Change or see your code [Host Only]</color>",
                ["MatchTogglePublic"] = "<color=#5A397A>/{0} public</color><color=#5A625B> • Toggle match as public or private invitation [Host Only]</color>",
                ["MatchDefeat"] = "<color=silver><color=lime>{0}</color> has defeated <color=lime>{1}</color> in a <color=yellow>{2}v{2}</color> deathmatch!</color>",
                ["MatchIsNotNaked"] = "Match cannot start because <color=lime>{0}</color> is not naked. Next queue check in 30 seconds.",
                ["MatchCannotBan"] = "You cannot ban this player, or this player is already banned.",
                ["MatchBannedUser"] = "You have banned <color=lime>{0}</color> from your team.",
                ["MatchPlayerNotFound"] = "<color=lime>{0}</color> is not on your team.",
                ["MatchCodeIs"] = "Your code is: {0}",
                ["InQueueList"] = "Players in the queue:",
                ["HelpTDM"] = "<color=#5A397A>/{0}</color><color=#5A625B> • Create a team deathmatch</color>",
                ["InMatchListGood"] = "Good Team: {0}",
                ["InMatchListEvil"] = "Evil Team: {0}",
                ["MatchNoTeamFoundCode"] = "No team could be found for you with the provided code: {0}",
                ["MatchNoTeamFoundAny"] = "No team could be found with a friend as the host. Use a code instead.",
                ["MatchPublic"] = "Your match is now open to the public.",
                ["MatchPrivate"] = "Your match is now private and requires a code, or to be a friend to join.",
                ["CannotBank"] = "You are not allowed to bank while dueling.",
                ["TargetMustBeNaked"] = "<color=red>The person you are challenging must be naked before you can challenge them.</color>",
                ["MatchKit"] = "<color=#5A397A>/{0} kit <name></color><color=#5A625B> • Changes the kit used [Host Only]</color>",
                ["MatchKitSet"] = "Kit set to: <color=yellow>{0}</color>",
                ["MatchChallenge0"] = "<color=#5A397A>/{0} <name> [kitname]</color><color=#5A625B> • Challenge another player and set the kit if specified</color>",
                ["MatchPlayerDefeated"] = "<color=silver><color=lime>{0}</color> was killed by <color=lime>{1}</color> using <color=red>{2}</color> (<color=red>{3}: {4}m</color>)</color>",
                ["CommandNotAllowed"] = "You are not allowed to use this command right now.",
                ["HelpKit"] = "<color=#5A397A>/{0} kit</color><color=#5A625B> • Pick a kit</color>",
                ["RemovedXWallsCustom"] = "Removed {0} walls due to the deletion of zones which exceed the Max Zone cap.",
                ["ZonesSetup"] = "Initialized {0} existing dueling zones.",
                ["ArenasSetup"] = "{0} existing arenas are now protected.",
                ["NoPendingRequests2"] = "You have no pending request to accept.",
                ["MatchNoLongerValid"] = "You cannot join this match anymore.",
                ["NoMatchesExistYet"] = "No matches exist yet.",
                ["UI_Accept"] = "Accept",
                ["UI_Decline"] = "Decline",
                ["UI_Kits"] = "Kits",
                ["UI_Public"] = "Public",
                ["UI_Queue"] = "Queue",
                ["UI_TDM"] = "TDM",
                ["UI_TeamSize"] = "Set Team Size: ",
                ["UI_Any"] = "Any",
                ["UI_Help"] = "<color=#5A397A>/{0}</color><color=#5A625B> • Show Duelist User Interface</color>",
            }, this);
        }

        bool kitsVerified = false;

        void VerifyKits()
        {
            if (kitsVerified || Kits == null)
                return;

            foreach (string kit in lpDuelingKits.ToList())
                if (!IsKit(kit))
                    lpDuelingKits.Remove(kit);

            foreach (string kit in hpDuelingKits.ToList())
                if (!IsKit(kit))
                    hpDuelingKits.Remove(kit);

            kitsVerified = true;
        }
        
        List<string> VerifiedKits
        {
            get
            {
                VerifyKits();

                var list = new List<string>();

                if (hpDuelingKits.Count > 0)
                    list.AddRange(hpDuelingKits);

                if (lpDuelingKits.Count > 0)
                    list.AddRange(lpDuelingKits);

                if (list.Count == 0 && customKits.Count > 0)
                    list.AddRange(customKits.Select(kvp => kvp.Key));

                list.Sort();
                return list;
            }
        }

        string VerifiedKit(string kit)
        {
            string kits = string.Join(", ", VerifiedKits.ToArray());

            if (!string.IsNullOrEmpty(kits))
            {
                if (customKits.Any(entry => entry.Key.Equals(kit, StringComparison.CurrentCultureIgnoreCase)))
                {
                    return customKits.First(entry => entry.Key.Equals(kit, StringComparison.CurrentCultureIgnoreCase)).Key;
                }
                else if (hpDuelingKits.Any(entry => entry.Equals(kit, StringComparison.CurrentCultureIgnoreCase)))
                {
                    return hpDuelingKits.First(entry => entry.Equals(kit, StringComparison.CurrentCultureIgnoreCase));
                }
                else if (lpDuelingKits.Any(entry => entry.Equals(kit, StringComparison.CurrentCultureIgnoreCase)))
                {
                    return lpDuelingKits.First(entry => entry.Equals(kit, StringComparison.CurrentCultureIgnoreCase));
                }
            }

            return null;
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
            autoEnable = Convert.ToBoolean(GetConfig("Settings", "Auto Enable Dueling If Zone(s) Exist", false));
            bypassNewmans = Convert.ToBoolean(GetConfig("Settings", "Bypass Naked Check And Strip Items Anyway", false));
            respawnDead = Convert.ToBoolean(GetConfig("Settings", "Respawn Dead Players On Disconnect", true));
            resetSeed = Convert.ToBoolean(GetConfig("Settings", "Reset Temporary Ladder Each Wipe", true));
            noStability = Convert.ToBoolean(GetConfig("Settings", "No Stability On Structures", true));
            noMovement = Convert.ToBoolean(GetConfig("Settings", "No Movement During Immunity", false));
            respawnWalls = Convert.ToBoolean(GetConfig("Settings", "Respawn Zone Walls On Death", false));

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
            useLeastAmount = Convert.ToBoolean(GetConfig("Zone", "Create Least Amount Of Walls", false));

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
                    if (!string.IsNullOrEmpty(kit) && !hpDuelingKits.Contains(kit))
                    {
                        hpDuelingKits.Add(kit); // 0.1.14 fix
                        _hpDuelingKits.Add(kit); // 0.1.17 clone for Least Used Chance compatibility
                    }
                }
            }

            lesserKitChance = Convert.ToSingle(GetConfig("Settings", "Kits Least Used Chance", 0.25f));
            var lesserKits = GetConfig("Settings", "Kits Least Used", new List<object> { "kit_4", "kit_5", "kit_6" }) as List<object>;

            if (lesserKits != null && lesserKits.Count > 0)
            {
                foreach (string kit in lesserKits.Cast<string>().ToList())
                {
                    if (!string.IsNullOrEmpty(kit) && !lpDuelingKits.Contains(kit))
                    {
                        lpDuelingKits.Add(kit); // 0.1.16
                        _lpDuelingKits.Add(kit); // 0.1.17 clone for Least Used Chance compatibility
                    }
                }
            }

            useWorkshopSkins = Convert.ToBoolean(GetConfig("Custom Kits", "Use Workshop Skins", true));
            var defaultKits = GetConfig("Custom Kits", "Kits", DefaultKits) as Dictionary<string, object>;

            SetupCustomKits(defaultKits, ref customKits);

            var defaultRespawn = GetConfig("Respawn", "Items", RespawnLoot) as List<object>;

            SetupRespawnItems(defaultRespawn, ref respawnLoot);

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

            useBlacklistCommands = Convert.ToBoolean(GetConfig("Settings", "Blacklist Commands", false));
            blacklistCommands = (GetConfig("Settings", "Blacklisted Chat Commands", BlacklistedCommands) as List<object>).Where(o => o != null && o.ToString().Length > 0).Select(o => o.ToString().ToLower()).ToList();
            useWhitelistCommands = Convert.ToBoolean(GetConfig("Settings", "Whitelist Commands", false));
            whitelistCommands = (GetConfig("Settings", "Whitelisted Chat Commands", WhitelistedCommands) as List<object>).Where(o => o != null && o.ToString().Length > 0).Select(o => o.ToString().ToLower()).ToList();

            if (!string.IsNullOrEmpty(szDuelChatCommand))
            {
                cmd.AddChatCommand(szDuelChatCommand, this, cmdDuel);
                cmd.AddConsoleCommand(szDuelChatCommand, this, nameof(ccmdDuel));
                whitelistCommands.Add("/" + szDuelChatCommand.ToLower());
            }

            if (!string.IsNullOrEmpty(szQueueChatCommand))
                cmd.AddChatCommand(szQueueChatCommand, this, cmdQueue);

            economicsMoney = Convert.ToDouble(GetConfig("Rewards", "Economics Money [0 = disabled]", 0.0));
            serverRewardsPoints = Convert.ToInt32(GetConfig("Rewards", "ServerRewards Points [0 = disabled]", 0));
            requiredDuelMoney = Convert.ToDouble(GetConfig("Rewards", "Required Money To Duel", 0.0));

            spDrawTime = Convert.ToSingle(GetConfig("Spawns", "Draw Time", 30f));
            spRemoveOneMaxDistance = Convert.ToSingle(GetConfig("Spawns", "Remove Distance", 10f));
            spRemoveAllMaxDistance = Convert.ToSingle(GetConfig("Spawns", "Remove All Distance", zoneRadius));
            //spRemoveInRange = Convert.ToBoolean(GetConfig("Spawns", "Remove In Duel Zone Only", false));
            spAutoRemove = Convert.ToBoolean(GetConfig("Spawns", "Auto Remove On Zone Removal", false));

            customArenasUseWallProtection = Convert.ToBoolean(GetConfig("Custom Arenas", "Indestructible Walls", true));
            customArenasNoRaiding = Convert.ToBoolean(GetConfig("Custom Arenas", "No Raiding", false));
            customArenasNoPVP = Convert.ToBoolean(GetConfig("Custom Arenas", "No PVP", false));
            customArenasUseWooden = Convert.ToBoolean(GetConfig("Custom Arenas", "Use Wooden Walls", false));
            customArenasNoBuilding = Convert.ToBoolean(GetConfig("Custom Arenas", "No Building", false));

            dmFF = Convert.ToBoolean(GetConfig("Deathmatch", "Friendly Fire", true));
            minDeathmatchSize = Convert.ToInt32(GetConfig("Deathmatch", "Min Team Size", 2));
            maxDeathmatchSize = Convert.ToInt32(GetConfig("Deathmatch", "Max Team Size", 5));
            teamEvilShirt = Convert.ToUInt64(GetConfig("Deathmatch", "Evil Shirt Skin", 14177));
            teamGoodShirt = Convert.ToUInt64(GetConfig("Deathmatch", "Good Shirt Skin", 101));
            teamShirt = Convert.ToString(GetConfig("Deathmatch", "Shirt Shortname", "tshirt"));
            teamEconomicsMoney = Convert.ToDouble(GetConfig("Deathmatch", "Economics Money [0 = disabled]", 0.0));
            teamServerRewardsPoints = Convert.ToInt32(GetConfig("Deathmatch", "ServerRewards Points [0 = disabled]", 0));
            tdmEnabled = Convert.ToBoolean(GetConfig("Deathmatch", "Enabled", true));
            szMatchChatCommand = Convert.ToString(GetConfig("Deathmatch", "Chat Command", "tdm"));
            tdmServerDeaths = Convert.ToBoolean(GetConfig("Deathmatch", "Announce Deaths To Server", false));
            tdmMatchDeaths = Convert.ToBoolean(GetConfig("Deathmatch", "Announce Deaths To Match", true));

            requireTeamSize = Convert.ToBoolean(GetConfig("Advanced Options", "Require TDM Minimum Spawn Points To Be Equal Or Greater To The Number Of Players Joining", false));
            requiredMinSpawns = Convert.ToInt32(GetConfig("Advanced Options", "Require 1v1 Minimum Spawn Points To Be Equal Or Greater Than X", 2));
            requiredMaxSpawns = Convert.ToInt32(GetConfig("Advanced Options", "Require 1v1 Maximum Spawn Points To Be Less Than Or Equal To X", 200));

            if (requiredMinSpawns < 2)
                requiredMinSpawns = 2;

            if (requiredMaxSpawns < 2)
                requiredMaxSpawns = 2;

            if (tdmEnabled && !string.IsNullOrEmpty(szMatchChatCommand))
            {
                cmd.AddChatCommand(szMatchChatCommand, this, cmdTDM);
                whitelistCommands.Add("/" + szMatchChatCommand.ToLower());
            }

            guiAutoEnable = Convert.ToBoolean(GetConfig("User Interface", "Auto Enable GUI For Players", false));
            szUIChatCommand = Convert.ToString(GetConfig("User Interface", "Chat Command", "dui"));
            guiUseCursor = Convert.ToBoolean(GetConfig("User Interface", "Use Cursor", false));

            if (!string.IsNullOrEmpty(szUIChatCommand))
            {
                cmd.AddChatCommand(szUIChatCommand, this, cmdDUI);
            }

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        void SetupRespawnItems(List<object> list, ref List<DuelKitItem> source)
        {
            if (list == null || list.Count == 0 || !(list is List<object>))
            {
                return;
            }

            foreach (var entry in list)
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
                    foreach (var item in items) // DuelKitItem
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

                                        foreach (var mod in _mods)
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

                source.Add(new DuelKitItem() { amount = amount, container = container, shortname = shortname, skin = skin, slot = slot, ammo = ammo, mods = mods.Count > 0 ? mods : null });
            }
        }

        void SetupCustomKits(Dictionary<string, object> dict, ref Dictionary<string, List<DuelKitItem>> source)
        {
            if (dict == null && dict.Count == 0)
            {
                return;
            }

            foreach (var kit in dict)
            {
                if (source.ContainsKey(kit.Key))
                    source.Remove(kit.Key);

                source.Add(kit.Key, new List<DuelKitItem>());

                if (kit.Value is List<object>) // list of DuelKitItem
                {
                    var objects = kit.Value as List<object>;

                    if (objects != null && objects.Count > 0)
                    {
                        foreach (var entry in objects)
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
                                    foreach (var item in items) // DuelKitItem
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

                                                        foreach (var mod in _mods)
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

                                source[kit.Key].Add(new DuelKitItem() { amount = amount, container = container, shortname = shortname, skin = skin, slot = slot, ammo = ammo, mods = mods.Count > 0 ? mods : null });
                            }
                        }
                    }
                }
            }

            foreach (var kit in source.ToList())
                if (kit.Value.Count == 0)
                    source.Remove(kit.Key);
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
