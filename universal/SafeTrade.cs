using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;       //Regex
using Oxide.Core;                           //Interface
using Oxide.Core.Configuration;             //DynamicConfigFile
using Oxide.Core.Libraries;                 //Permission
using Oxide.Core.Plugins;                   //Plugin
using UnityEngine;
using Assets.Scripts.Core;

namespace Oxide.Plugins
{
    #region Changelog
    /*

    Changelog 1.1.0

    Fixed:
        *
    Added:
        * Config option to choose penalty mode (Drop nothing, backpack, everything). This was not fully tested. If you find any bug or not desired behaviour report it and use the previous version instead.
    Removed:
        * 
    Changed:
        * 
    */
    #endregion Changelog
    [Info("SafeTrade", "SouZa", "1.1.0", ResourceId = 1896)]
    [Description("Allows players to trade safely without scams. Can use SafeTrade Zone.")]
    public class SafeTrade : HurtworldPlugin
    {
        #region Plugin References
        [PluginReference("AlertAreas")]
        private Plugin AlertAreas;
        [PluginReference("ExtTeleport")]
        private Plugin ExtTeleport;
        [PluginReference("HurtArena")]
        private Plugin HurtArena;

        #endregion Plugin References

        #region Enums

        internal enum ELogType
        {
            Info,
            Warning,
            Error
        }

        enum TradeInfo
        {
            HasRequest,
            IsRequesting,
            Null,
            Trading,
            PartnerIsReady,
            MyselfIsReady
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

            //Helpers Common methods

            //Permissions

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

            //Configs

            public void SetConfig(bool replace, params object[] args)
            {
                var stringArgs = ObjectToStringArray(args.Take(args.Length - 1).ToArray());
                if (replace || _config.Get(stringArgs) == null)
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
                        $"Couldn't read from config file: {ArrayToString(stringArgs, "/")}");
                    return defaultVal;
                }
                return (T)Convert.ChangeType(_config.Get(stringArgs.ToArray()), typeof(T));
            }

            public string[] ObjectToStringArray(object[] args)
            {
                return args.DefaultIfEmpty().Select(a => a.ToString()).ToArray();
            }

            public string ArrayToString(string[] array, string separator)
            {
                return string.Join(separator, array);
            }

            //PlayerSession

            public bool IsValidSession(PlayerSession session)
            {
                return session?.SteamId != null && session.IsLoaded && session.Name != null && session.Identity != null &&
                       session.WorldPlayerEntity?.transform?.position != null;
            }

            public string GetPlayerId(PlayerSession session)
            {
                return session.SteamId.ToString();
            }

            public ulong GetPlayerId_ulong(PlayerSession session)
            {
                return ulong.Parse(session.SteamId.ToString());
            }

            //Cell Ownership

            public List<PlayerIdentity> GetCellOwners(Vector3 position)
            {
                var cell = ConstructionUtilities.GetOwnershipCell(position);
                if (cell >= 0)
                {
                    OwnershipStakeServer stake;
                    if (ConstructionManager.Instance.OwnershipCells.TryGetValue(cell, out stake))
                    {
                        if (stake?.AuthorizedPlayers != null)
                        {
                            return stake.AuthorizedPlayers.ToList();
                        }
                    }
                }
                return new List<PlayerIdentity>();
            }

            public List<OwnershipStakeServer> GetStakesFromPlayer(PlayerSession session)
            {
                var stakes = Resources.FindObjectsOfTypeAll<OwnershipStakeServer>();
                if (stakes != null)
                {
                    return
                        stakes.Where(
                            s =>
                                !s.IsDestroying && s.gameObject != null && s.gameObject.activeSelf &&
                                s.AuthorizedPlayers.Contains(session.Identity)).ToList();
                }
                return new List<OwnershipStakeServer>();
            }

            //Conversions

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

            public string Color(string text, string color)
            {
                switch (color)
                {
                    case "myRed":
                        return "<color=#ff0000ff>" + text + "</color>";

                    case "myGreen":
                        return "<color=#00ff00ff>" + text + "</color>";

                    case "myBlue":
                        return "<color=#00ffffff>" + text + "</color>";

                    case "chatBlue":
                        return "<color=#6495be>" + text + "</color>";

                    default:
                        return "<color=" + color + ">" + text + "</color>";
                }
            }

            string RemoveTags(string phrase)
            {
                //	Forbidden formatting tags
                List<string> forbiddenTags = new List<string>{
                "</color>",
                "</size>",
                "<b>",
                "</b>",
                "<i>",
                "</i>"
                };

                //	Replace Color Tags
                phrase = new Regex("(<color=.+?>)").Replace(phrase, "");

                //	Replace Size Tags
                phrase = new Regex("(<size=.+?>)").Replace(phrase, "");

                foreach (string tag in forbiddenTags)
                    phrase = phrase.Replace(tag, "");

                return phrase;
            }
        }

        //Custom Classes

        public class PlayerTrade
        {
            public PlayerTrade(ulong id)
            {
                tradeID = id;
                items = new List<PlayerTradeItem>();
            }
            
            public ulong tradeID { get; set; }
            public List<PlayerTradeItem> items { get; set; }
            public bool isSet { get; set; }
            public bool isReady { get; set; }
            public bool hasAccepted { get; set; }

            public static int containsID(int itemID, List<PlayerTradeItem> items)
            {
                for (int i = 0; i<items.Count; i++)
                {
                    if (items[i].id == itemID)
                        return i;
                }
                return -1;
            }
        }

        public class PlayerTradeItem
        {
            public PlayerTradeItem(int id, string name, int stackSize)
            {
                this.id = id;
                this.name = name;
                this.stackSize = stackSize;
            }

            public int id { get; set; }
            public string name { get; set; }
            public int stackSize { get; set; }
        }

        #endregion Classes

        #region Variables

        private Helpers _helpers;
        private Dictionary<ulong, PlayerTrade> _trades = new Dictionary<ulong, PlayerTrade>();
        private string _msg_prefix;

        #endregion Variables

        #region Methods

        private void Loaded()
        {
            _helpers = new Helpers(Config, this, permission, Log)
            {
                PermissionPrefix = Regex.Replace(Title, "[^0-9a-zA-Z]+", string.Empty).ToLower()
            };

            LoadConfig();
            LoadPermissions();
            LoadMessages();
            
            _msg_prefix = lang.GetMessage("trade_prefix", this);
        }

        protected override void LoadDefaultConfig()
        {
            Log(ELogType.Warning, "No config file found, generating a new one.");
        }

        private new void LoadConfig()
        {
            #region Config
            _helpers.SetConfig(false, "Settings", "SafeTradeZone", "0-enabled", true);                            //Enable|Disable SafeTrade Zone (using "AlertAreas")
            _helpers.SetConfig(false, "Settings", "SafeTradeZone", "1-warp-enabled", false);                      //Enable|Disable SafeTrade Zone Warp. (Can disable and use "ExtTeleport" Warp instead)
            _helpers.SetConfig(false, "Settings", "SafeTradeZone", "2-warp-spawn", "0 0 0");                      //Location where players spawn (using customWarp from this plugin)
            _helpers.SetConfig(false, "Settings", "SafeTradeZone", "3-penalty-spawn", "0 0 0");    
            _helpers.SetConfig(false, "Settings", "SafeTradeZone", "4-penalty-drop-backpack", false);
            _helpers.SetConfig(false, "Settings", "SafeTradeZone", "4-penalty-drop-everything", false);
            

            SaveConfig();
            #endregion Config
        }

        private void LoadPermissions()
        {
            _helpers.RegisterPermission("use");
        }
        
        private void LoadMessages()
        {
            #region Messages
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"trade_usage", "Type <color=orange>/trade</color> for proper usage."},
                {"trade_admin_usage", "Type <color=orange>/tradeadmin</color> for proper usage."},
                {"zone_inside", "<color=lime>[You need to be inside the SafeTrade Zone.]</color>" },
                {"no_permission", "You don't have permission to use this command.\nRequired: <color=orange>{perm}</color>."},
                {"offline", "<color=orange>{playerName}</color> is offline."},
                {"multiplePlayers", "There are multiple players with {partial} on their name: {namesList}."},
                {"trade_prefix", "<color=yellow>[SafeTrade]</color>"},
                {"trade_broadcast_penalty", "<color=orange>{player}</color> tried to kill inside SafeTrade Zone and now <color=red>Burns in Hell</color>!"},
                {"trade_self", "You can't make a trade with yourself"},
                {"tradezone_player_outside", "<color=orange>{player}</color> is outside the <color=orange>SafeTrade Zone</color>."},
                {"tradezone_you_outside", "Must be inside <color=orange>SafeTrade Zone</color>."},
                {"trade_started", "A trade between you and <color=orange>{player}</color> has started."},
                {"trade_request_from_1", "You have a trade request from <color=orange>{player}</color>."},
                {"trade_request_from_2", "You have a trade request from <color=orange>{player}</color>. Accept or Cancel it first."},
                {"trade_no_request", "You do not have any trade request."},
                {"trade_requested_already", "You have already requested a trade with <color=orange>{player}</color>."},
                {"trade_requested_with_1", "You have requested a trade with <color=orange>{player}</color>."},
                {"trade_requested_with_2", "You have requested a trade with <color=orange>{player}</color>. Wait or Cancel it first."},
                {"trade_request_already", "<color=orange>{player}</color> already has a trade request. You have to wait."},
                {"trade_requested_1", "You requested a trade with <color=orange>{player}</color>."},
                {"trade_requested_2", "<color=orange>{player}</color> requested a trade with you."},
                {"trade_set_already", "Finish your current trade with <color=orange>{player}</color>."},
                {"trade_not_started", "You have not started a trade yet. Type <color=orange>/tri</color>."},
                {"trade_add_1", "You added new items to the trade."},
                {"trade_add_2", "<color=orange>{player}</color> added new items."},
                {"trade_null", "You do not have any trade requests."},
                {"trade_info_players", "You are trading with <color=orange>{player}</color>."},
                {"trade_info_player_Items", "<color=orange>{player}</color> Items: <color=orange>{itemlist}</color>"},
                {"trade_info_isReady", "<color=green>[Ready]</color> "},
                {"trade_info_notReady", "<color=red>[Not Ready]</color> "},
                {"trade_accepted_already", "You have already accepted the Trade. Type <color=orange>/tri</color>."},
                {"trade_accepted_you", "You have accepted the Trade. Waiting for <color=orange>{player}</color>."},
                {"trade_accepted_player", "<color=orange>{player}</color> has accepted the Trade. Waiting for you."},
                {"trade_completed", "The trade has been completed."},
                {"trade_declined_you", "You have declined <color=orange>{player}'s</color> trade request."},
                {"trade_declined_player", "<color=orange>{player}</color> has declined your trade request."},
                {"trade_cancel_you", "You have canceled the trade request sent to <color=orange>{player}</color>."},
                {"trade_cancel_player", "<color=orange>{player}</color> has canceled the trade request sent to you."},
                {"trade_cancel_current_you", "<color=orange>You</color> have canceled the current trade."},
                {"trade_cancel_current_player", "<color=orange>{player}</color> has canceled the current trade."},
                {"trade_cancel_logout", "<color=orange>{player}</color> has logged out. The trade is canceled."},
                {"trade_cancel_kill", "<color=orange>{killer}</color> has tried to kill <color=orange>{player}</color>. The trade is canceled."},
                {"tpr_insideZone_1", "You can't use <color=orange>/tpr</color>. Complete or cancel your trade first."},
                {"tpr_insideZone_2", "You can't teleport. <color=orange>{player}</color> need to finish his trade first."},
                {"tpa_insideZone_1", "You can't use <color=orange>/tpa</color> inside <color=orange>SafeTrade Zone</color>."},
                {"tpa_insideZone_2", "You can't accept the teleport. <color=orange>{player}</color> need to finish his trade first."},
                {"home_isTrading", "You can't use <color=orange>/home</color>. Complete or cancel your trade first."},
                {"warp_isTrading", "You can't <color=orange>warp</color>. Complete or cancel your trade first."},
                {"warp_notAvailable", "No warp available. You have to walk to the SafeTrade Zone."},
                {"warp_not_bothPlayer", "Both players can't warp. Trade is canceled."},
                {"warp_not_player", "<color=orange>{player}</color> can't warp. Trade is canceled."},
                {"trade_admin_zone_enabled", "<color=orange>SafeTrade Zone</color> is now <color=orange>{value}</color>."},
                {"trade_admin_warp_enabled", "<color=orange>SafeTrade Warp</color> is now <color=orange>{value}</color>."},
                {"trade_admin_warp_spawn", "<color=orange>SafeTrade Warp</color> is now set at <color=orange>{value}</color>."},
                {"trade_admin_penalty_spawn", "<color=orange>SafeTrade Penalty</color> is now set at <color=orange>{value}</color>."},
                {"trade_isHurtArenaParticipant", "You are an HurtArena participant. Can't request a trade."}

            }, this);
            #endregion Messages
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

        PlayerSession GetSession(ulong identifier)
        {
            var sessions = GameManager.Instance?.GetSessions()?.Values.Where(_helpers.IsValidSession).ToList();

            foreach(var session in sessions)
            {
                if (session.SteamId.m_SteamID == identifier)
                    return session;
            }

            return null;
        }

        PlayerSession GetSession(PlayerSession player, string identifier)
        {
            var sessions = GameManager.Instance?.GetSessions()?.Values.Where(_helpers.IsValidSession).ToList();

            PlayerSession result = null;
            string namesList = "";
            var counter = 0;

            foreach (var session in sessions)
            {
                if (session.Name.ToLower().Equals(identifier.ToLower()))
                {
                    counter = 1;
                    namesList = "";
                    result = session;
                    break;
                }
                else if (session.Name.ToLower().Contains(identifier.ToLower()))
                {
                    counter++;
                    namesList += (session.Name + ", ");
                    result = session;
                }
            }

            if (counter == 1)
            {
                return result;
            }
            else if (counter > 1)
            {
                if (namesList != "")
                    namesList = namesList.Substring(0, namesList.Length - 2);
                hurt.SendChatMessage(player, lang.GetMessage("multiplePlayers", this).Replace("{partial}", identifier).Replace("{namesList}", namesList));
                return null;
            }
            else
                return null;
        }

        ulong getMyTradePartnerID(ulong steamid)
        {
            foreach (KeyValuePair<ulong, PlayerTrade> kvp in _trades)
            {
                if (kvp.Key == steamid)
                    return kvp.Value.tradeID;
            }

            return ulong.MinValue;
        }

        void cancelTrade(PlayerSession mySession, PlayerSession partnerSession, ulong myID, ulong partnerID)
        {
            var im = GlobalItemManager.Instance;

            if (_trades.ContainsKey(myID))
            {
                foreach (PlayerTradeItem pti in _trades[myID].items)
                {
                    im.GiveItem(partnerSession.Player, im.GetItem(pti.id), pti.stackSize);
                }
                _trades.Remove(myID);
            }

            if (_trades.ContainsKey(partnerID))
            {
                foreach (PlayerTradeItem pti in _trades[partnerID].items)
                {
                    im.GiveItem(mySession.Player, im.GetItem(pti.id), pti.stackSize);
                }
                _trades.Remove(partnerID);
            }
        }

        void Heal(PlayerSession player)
        {
            EntityStats stats = player.WorldPlayerEntity.GetComponent<EntityStats>();
            stats.GetFluidEffect(EEntityFluidEffectType.ColdBar).SetValue(0f);
            stats.GetFluidEffect(EEntityFluidEffectType.Radiation).SetValue(0f);
            stats.GetFluidEffect(EEntityFluidEffectType.HeatBar).SetValue(0f);
            stats.GetFluidEffect(EEntityFluidEffectType.Dampness).SetValue(0f);
            stats.GetFluidEffect(EEntityFluidEffectType.Hungerbar).SetValue(0f);
            stats.GetFluidEffect(EEntityFluidEffectType.Nutrition).SetValue(100f);
            stats.GetFluidEffect(EEntityFluidEffectType.InternalTemperature).Reset(true);
            stats.GetFluidEffect(EEntityFluidEffectType.ExternalTemperature).Reset(true);
            stats.GetFluidEffect(EEntityFluidEffectType.Toxin).SetValue(0f);
            stats.GetFluidEffect(EEntityFluidEffectType.Health).SetValue(100f);
        }

        bool isUsingZone()
        {
            return _helpers.GetConfig(true, "Settings", "SafeTradeZone", "0-enabled");
        }

        bool playerIsTrading(PlayerSession session)
        {
            var myID = _helpers.GetPlayerId_ulong(session);
            if (_trades.ContainsKey(myID))
            {
                return true;
            }
            return false;
        }

        bool isWarpEnabled()
        {
            return _helpers.GetConfig(false, "Settings", "SafeTradeZone", "1-warp-enabled");
        }

        #endregion Methods

        #region CMDS
        [ChatCommand("trr")]
        void cmdTradeRequest(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, lang.GetMessage("trade_prefix", this), lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.PermissionPrefix + ".use"));
                return;
            }

            cmdTrade(session, command, args);
        }

        [ChatCommand("tra")]
        void cmdTradeAccept(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, lang.GetMessage("trade_prefix", this), lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.PermissionPrefix + ".use"));
                return;
            }

            cmdTrade(session, command, args);
        }

        [ChatCommand("tradd")]
        void cmdTradeAdd(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, lang.GetMessage("trade_prefix", this), lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.PermissionPrefix + ".use"));
                return;
            }

            cmdTrade(session, command, args);
        }

        [ChatCommand("tri")]
        void cmdTradeInfo(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, lang.GetMessage("trade_prefix", this), lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.PermissionPrefix + ".use"));
                return;
            }

            cmdTrade(session, command, args);
        }

        [ChatCommand("trc")]
        void cmdTradeCancel(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, lang.GetMessage("trade_prefix", this), lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.PermissionPrefix + ".use"));
                return;
            }

            cmdTrade(session, command, args);
        }

        [ChatCommand("tr")]
        void cmdTradeFinish(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, lang.GetMessage("trade_prefix", this), lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.PermissionPrefix + ".use"));
                return;
            }

            cmdTrade(session, command, args);
        }

        [ChatCommand("tradeadmin")]
        void cmdTradeAdmin(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, lang.GetMessage("trade_prefix", this), lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.PermissionPrefix + ".use"));
                return;
            }

            if(session.IsAdmin)
                cmdTrade(session, command, args);
        }

        [ChatCommand("tradezone")]
        void cmdTradeZone(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, lang.GetMessage("trade_prefix", this), lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.PermissionPrefix + ".use"));
                return;
            }

            if (session.IsAdmin)
                cmdTrade(session, command, args);
        }

        [ChatCommand("tradewarp")]
        void cmdTradeWarp(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, lang.GetMessage("trade_prefix", this), lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.PermissionPrefix + ".use"));
                return;
            }

            if (session.IsAdmin)
                cmdTrade(session, command, args);
        }

        [ChatCommand("tradespawn")]
        void cmdTradeSpawn(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, lang.GetMessage("trade_prefix", this), lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.PermissionPrefix + ".use"));
                return;
            }

            if (session.IsAdmin)
                cmdTrade(session, command, args);
        }

        [ChatCommand("tradepenalty")]
        void cmdTradePenalty(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, lang.GetMessage("trade_prefix", this), lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.PermissionPrefix + ".use"));
                return;
            }

            if (session.IsAdmin)
                cmdTrade(session, command, args);
        }

        [ChatCommand("trade")]
        void cmdTrade(PlayerSession session, string command, string[] args)
        {
            //Test permission
            if (!_helpers.HasPermission(session, "use") && !session.IsAdmin)
            {
                hurt.SendChatMessage
                (
                    session,
                    lang.GetMessage("trade_prefix", this),
                    lang.GetMessage("no_permission", this)
                        .Replace("{perm}", _helpers.Color(_helpers.PermissionPrefix + ".use", "orange"))
                );
                return;
            }

            //TradeStatus information
            TradeInfo tradeInfo = TradeInfo.Null;

            //My Information
            PlayerSession mySession = session;
            ulong myID = _helpers.GetPlayerId_ulong(mySession);

            //Trade Partner Information
            PlayerSession partnerSession = null;
            ulong partnerID = getMyTradePartnerID(myID);
            string partnerName = string.Empty;
            if (partnerID != ulong.MinValue)
            {
                partnerSession = GetSession(partnerID);
                partnerName = partnerSession?.Name;
            }

            if (_trades.ContainsKey(myID) && _trades[myID].isSet && !_trades[partnerID].isSet)
            {
                tradeInfo = TradeInfo.HasRequest;
            }
            if (_trades.ContainsKey(myID) && !_trades[myID].isSet && _trades[partnerID].isSet)
            {
                tradeInfo = TradeInfo.IsRequesting;
            }
            else if (!_trades.ContainsKey(myID))
            {
                tradeInfo = TradeInfo.Null;
            }
            if (_trades.ContainsKey(myID) && _trades[myID].isSet && _trades[partnerID].isSet)
            {
                tradeInfo = TradeInfo.Trading;
                if (_trades[myID].isReady && !_trades[partnerID].isReady)
                    tradeInfo = TradeInfo.PartnerIsReady;
                else if (!_trades[myID].isReady && _trades[partnerID].isReady)
                    tradeInfo = TradeInfo.MyselfIsReady;
            }

            var im = GlobalItemManager.Instance;

            switch (command)
            {
                case "trade":
                    // [/trade]
                    string[] tradeCMDS =
                    {
                        "<color=orange>/trr <player></color> - Request a new trade.",
                        "<color=orange>/tra</color> - Accept a trade request.",
                        "<color=orange>/tradd</color> - Add all items in hotbar for trade.",
                        "<color=orange>/tri</color> - Displays info about the current trade.",
                        "<color=orange>/trc</color> - Cancel any requested or accepted trade.",
                        "<color=orange>/tr</color> - Marks yourself ready for trade."
                    };

                    hurt.SendChatMessage(session, "<color=yellow>Available SafeTrade Commands</color>");
                    if(AlertAreas != null && isUsingZone())
                        hurt.SendChatMessage(session, lang.GetMessage("zone_inside", this));
                    foreach (string cmd in tradeCMDS)
                    {
                        hurt.SendChatMessage(session, cmd);
                    }
                    break;

                case "tradeadmin":
                    // [/tradeadmin]
                    string[] adminTradeCMDS =
                    {
                        "<color=orange>/tradezone true|false</color> - Enables|Disables SafeTrade Zone.",
                        "<color=orange>/tradewarp true|false</color> - Enables|Disables warp to zone.",
                        "<color=orange>/tradespawn</color> - Sets the Zone warp spawn.",
                        "<color=orange>/tradepenalty</color> - Sets the Zone penalty spawn."
                    };

                    if (session.IsAdmin)
                    {
                        hurt.SendChatMessage(session, "<color=yellow>Available SafeTrade-Admin Commands</color>");
                        foreach (string cmd in adminTradeCMDS)
                        {
                            hurt.SendChatMessage(session, cmd);
                        }
                    }
                    break;

                case "tradezone":
                    if(args.Length == 1)
                    {
                        if(args[0] != "true" && args[0] != "false")
                        {

                        }
                        else
                        {
                            bool setActive = args[0] == "true" ? true : false;
                            _helpers.SetConfig(true, "Settings", "SafeTradeZone", "0-enabled", setActive);
                            SaveConfig();
                            var value = setActive ? "enabled" : "disabled";
                            hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_admin_zone_enabled", this).Replace("{value}", value));
                        }
                    }
                    else
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_admin_usage", this));
                    break;

                case "tradewarp":
                    if (args.Length == 1)
                    {
                        if (args[0] != "true" && args[0] != "false")
                        {

                        }
                        else
                        {
                            bool setActive = args[0] == "true" ? true : false;
                            _helpers.SetConfig(true, "Settings", "SafeTradeZone", "1-warp-enabled", setActive);
                            SaveConfig();
                            var value = setActive ? "enabled" : "disabled";
                            hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_admin_warp_enabled", this).Replace("{value}", value));
                        }
                    }
                    else
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_admin_usage", this));
                    break;

                case "tradespawn":
                    if (args.Length == 0)
                    {
                        Vector3 newSpawn = session.WorldPlayerEntity.transform.position;
                        _helpers.SetConfig(true, "Settings", "SafeTradeZone", "2-warp-spawn", _helpers.Vector3ToString(newSpawn));
                        SaveConfig();
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_admin_warp_spawn", this).Replace("{value}", newSpawn+""));
                    }
                    else
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_admin_usage", this));
                    break;

                case "tradepenalty":
                    if (args.Length == 0)
                    {
                        Vector3 newSpawn = session.WorldPlayerEntity.transform.position;
                        _helpers.SetConfig(true, "Settings", "SafeTradeZone", "3-penalty-spawn", _helpers.Vector3ToString(newSpawn));
                        SaveConfig();
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_admin_penalty_spawn", this).Replace("{value}", newSpawn + ""));
                    }
                    else
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_admin_usage", this));
                    break;

                case "trr":
                    if(HurtArena != null && (bool)HurtArena.Call("isHurtArenaParticipant", session))
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_isHurtArenaParticipant", this));
                        return;
                    }
                    
                    if (args.Length == 1)
                    {
                        var argsName = args[0];
                        var argsSession = GetSession(session, argsName);
                        var argsID = ulong.MinValue;

                        if (argsSession != null)
                        {
                            argsName = argsSession.Name;
                            argsID = _helpers.GetPlayerId_ulong(argsSession);

                            if (myID == argsID)
                            {
                                hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_self", this));
                                return;
                            }

                            if (tradeInfo == TradeInfo.HasRequest)
                            {
                                hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_request_from_2", this).Replace("{player}", partnerName));
                                return;
                            }
                            else if (tradeInfo == TradeInfo.IsRequesting && argsID == partnerID)
                            {
                                hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_requested_already", this).Replace("{player}", partnerName));
                                return;
                            }
                            else if (tradeInfo == TradeInfo.IsRequesting && argsID != partnerID)
                            {
                                hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_requested_with_2", this).Replace("{player}", partnerName));
                                return;
                            }
                            else if (tradeInfo == TradeInfo.Null && _trades.ContainsKey(argsID))
                            {
                                //Player already has a trade Request. Have to wait.
                                hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_request_already", this).Replace("{player}", argsName));
                                return;
                            }
                            else if (tradeInfo == TradeInfo.Null)
                            {
                                //Request a trade
                                PlayerTrade myTrade = new PlayerTrade(myID);
                                PlayerTrade partnerTrade = new PlayerTrade(argsID);
                                myTrade.isSet = true;
                                _trades.Add(argsID, myTrade);
                                _trades.Add(myID, partnerTrade);
                                hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_requested_1", this).Replace("{player}", argsName));
                                hurt.SendChatMessage(argsSession, _msg_prefix, lang.GetMessage("trade_requested_2", this).Replace("{player}", mySession.Name));
                            }
                            else if (tradeInfo != TradeInfo.HasRequest && tradeInfo != TradeInfo.IsRequesting && tradeInfo != TradeInfo.Null)
                            {
                                //Trade already set.
                                hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_set_already", this).Replace("{player}", partnerName));
                                return;
                            }
                        }
                        else
                            hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("offline", this).Replace("{playerName}", argsName));
                    }
                    else
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_usage", this));
                    break;

                case "tra":
                    if (HurtArena != null && (bool)HurtArena.Call("isHurtArenaParticipant", session))
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_isHurtArenaParticipant", this));
                        return;
                    }
                    if (args.Length == 0)
                    {
                        if (tradeInfo == TradeInfo.IsRequesting)
                        {
                            hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_requested_with_2", this).Replace("{player}", partnerName));
                            return;
                        }
                        else if (tradeInfo == TradeInfo.Null)
                        {
                            hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_no_request", this).Replace("{player}", partnerName));
                            return;
                        }
                        else if (tradeInfo == TradeInfo.HasRequest)
                        {   //have a trade request from player

                            bool startTrade = true;

                            //Warp to SafeTrade Zone, if using
                            if (AlertAreas != null && isUsingZone())
                            {
                                bool myselfInsideZone = (bool)AlertAreas.Call("isInsideArea", session?.WorldPlayerEntity.transform.position, "SafeTradeZone");
                                bool partnerInsideZone = (bool)AlertAreas.Call("isInsideArea", partnerSession?.WorldPlayerEntity.transform.position, "SafeTradeZone");
                                
                                if (isWarpEnabled() && ( ExtTeleport == null || ExtTeleport.Version.ToString() != "1.0.3.1" ) )
                                {
                                    //Custom Teleport to SafeTrade Zone
                                    if (!myselfInsideZone)
                                        session.WorldPlayerEntity.transform.position = _helpers.StringToVector3(_helpers.GetConfig("0 0 0", "Settings", "SafeTradeZone", "2-warp-spawn"));
                                    if (!partnerInsideZone)
                                        partnerSession.WorldPlayerEntity.transform.position = _helpers.StringToVector3(_helpers.GetConfig("0 0 0", "Settings", "SafeTradeZone", "2-warp-spawn"));
                                }
                                else if (isWarpEnabled() && ExtTeleport != null && ExtTeleport.Version.ToString() == "1.0.3.1")
                                {
                                    bool zoneExists = (bool)ExtTeleport.Call("zoneExists", "SafeTradeZone");

                                    if (!zoneExists)
                                    {
                                        //No Warp available. Have to walk to SafeTradeZone
                                        if(!myselfInsideZone)
                                            hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("warp_notAvailable", this));
                                        if(!partnerInsideZone)
                                            hurt.SendChatMessage(partnerSession, _msg_prefix, lang.GetMessage("warp_notAvailable", this));
                                    }
                                    else
                                    {
                                        Vector3 warpPos = (Vector3)ExtTeleport.Call("getWarpPosition", "SafeTradeZone");
                                        bool sessionCanWarp = myselfInsideZone ? true : (bool)ExtTeleport.Call("canWarp", session, warpPos);
                                        bool partnerCanWarp = partnerInsideZone ? true : (bool)ExtTeleport.Call("canWarp", partnerSession, warpPos);

                                        if (sessionCanWarp && partnerCanWarp)
                                        {
                                            if (!myselfInsideZone)
                                                ExtTeleport.Call("CommandWarp", session, "warp", new string[] { "SafeTradeZone" });
                                            if (!partnerInsideZone)
                                                ExtTeleport.Call("CommandWarp", partnerSession, "warp", new string[] { "SafeTradeZone" });
                                        }
                                        else
                                        {
                                            if(!sessionCanWarp && !partnerCanWarp)
                                            {
                                                hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("warp_not_bothPlayer", this).Replace("{player}", partnerSession.Name));
                                                hurt.SendChatMessage(partnerSession, _msg_prefix, lang.GetMessage("warp_not_bothPlayer", this).Replace("{player}", session.Name));
                                            }
                                            else if (!sessionCanWarp)
                                            {
                                                hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("warp_not_player", this).Replace("{player}", session.Name));
                                                hurt.SendChatMessage(partnerSession, _msg_prefix, lang.GetMessage("warp_not_player", this).Replace("{player}", session.Name));
                                            }
                                            else if (!partnerCanWarp)
                                            {
                                                hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("warp_not_player", this).Replace("{player}", partnerSession.Name));
                                                hurt.SendChatMessage(partnerSession, _msg_prefix, lang.GetMessage("warp_not_player", this).Replace("{player}", partnerSession.Name));
                                            }
                                            startTrade = false;
                                            cancelTrade(session, partnerSession, myID, partnerID);
                                        }
                                    }
                                }
                                else
                                {
                                    //No Warp available. Have to walk to SafeTradeZone
                                    if(!myselfInsideZone)
                                        hurt.SendChatMessage(session, lang.GetMessage("warp_notAvailable", this));
                                    if(!partnerInsideZone)
                                        hurt.SendChatMessage(partnerSession, lang.GetMessage("warp_notAvailable", this));
                                }
                            }

                            if (startTrade)
                            {
                                _trades[partnerID].isSet = true;
                                hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_started", this).Replace("{player}", partnerName));
                                hurt.SendChatMessage(partnerSession, _msg_prefix, lang.GetMessage("trade_started", this).Replace("{player}", mySession.Name));
                            }
                            return;
                        }
                        else if (tradeInfo != TradeInfo.HasRequest && tradeInfo != TradeInfo.IsRequesting && tradeInfo != TradeInfo.Null)
                        {
                            //Trade already set.
                            hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_set_already", this).Replace("{player}", partnerName));
                            return;
                        }
                    }
                    else
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_usage", this));
                    break;

                case "tradd":
                    if (tradeInfo == TradeInfo.Null || tradeInfo == TradeInfo.HasRequest || tradeInfo == TradeInfo.IsRequesting)
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_not_started", this));
                        return;
                    }

                    //Test if inside SafeTrade Zone
                    if (AlertAreas != null && isUsingZone())
                    {
                        if (!(bool)AlertAreas.Call("isInsideArea", session?.WorldPlayerEntity.transform.position, "SafeTradeZone"))
                        {
                            hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("tradezone_you_outside", this));
                            return;
                        }
                        else if (!(bool)AlertAreas.Call("isInsideArea", partnerSession?.WorldPlayerEntity.transform.position, "SafeTradeZone"))
                        {
                            hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("tradezone_player_outside", this).Replace("{player}", partnerName));
                            return;
                        }
                    }
                    
                    PlayerInventory myInventory = session.WorldPlayerEntity.GetComponent<PlayerInventory>();
                    var myInventoryItems = myInventory.Items;
                    for (int i = 0; i < 8; i++)
                    {
                        var itemInstance = myInventoryItems[i];
                        if (itemInstance != null)
                        {
                            IItem item = itemInstance.Item;
                            int index = PlayerTrade.containsID(item.ItemId, _trades[partnerID].items);
                            if (index != -1)
                            {
                                _trades[partnerID].items[index].stackSize += itemInstance.StackSize;
                                itemInstance.ReduceStackSize(itemInstance.StackSize);
                            }
                            else
                            {
                                var itemName = LocalizationUtilities.CleanUp(item.GetNameKey());
                                PlayerTradeItem pti = new PlayerTradeItem(item.ItemId, itemName, itemInstance.StackSize);
                                _trades[partnerID].items.Add(pti);
                                itemInstance.ReduceStackSize(itemInstance.StackSize);
                            }
                        }
                    }
                    _trades[partnerID].isReady = false;
                    GlobalItemManager.Instance.GiveItem(session.Player, GlobalItemManager.Instance.GetItem(22), 0);

                    hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_add_1", this));
                    hurt.SendChatMessage(partnerSession, _msg_prefix, lang.GetMessage("trade_add_2", this).Replace("{player}", mySession.Name));
                    break;

                case "tri":
                    //op1
                    if (tradeInfo == TradeInfo.HasRequest)
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_request_from_1", this).Replace("{player}", partnerName));
                        return;
                    }

                    //op2
                    if (tradeInfo == TradeInfo.IsRequesting)
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_requested_with_1", this).Replace("{player}", partnerName));
                        return;
                    }

                    if (tradeInfo == TradeInfo.Null)
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_null", this));
                        return;
                    }

                    if (tradeInfo == TradeInfo.Trading || tradeInfo == TradeInfo.MyselfIsReady || tradeInfo == TradeInfo.PartnerIsReady)
                    {
                        string myItems_msg = string.Empty;
                        var myCounter = 0;
                        foreach (PlayerTradeItem pti in _trades[partnerID].items)
                        {
                            myCounter++;
                            myItems_msg += pti.stackSize + "x " + pti.name + " | ";
                        }
                        if (myCounter == 0)
                            myItems_msg = "(Empty)";
                        else if (!string.IsNullOrEmpty(myItems_msg))
                            myItems_msg = myItems_msg.Substring(0, myItems_msg.Length - 3);

                        string partnerItems_msg = string.Empty;
                        var partnercounter = 0;
                        foreach (PlayerTradeItem pti in _trades[myID].items)
                        {
                            partnercounter++;
                            partnerItems_msg += pti.stackSize + "x " + pti.name + " | ";
                        }
                        if (partnercounter == 0)
                            partnerItems_msg = "(Empty)";
                        else if (!string.IsNullOrEmpty(partnerItems_msg))
                            partnerItems_msg = partnerItems_msg.Substring(0, partnerItems_msg.Length - 3);

                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_info_players", this).Replace("{player}", partnerName));

                        var myselfIsReady = tradeInfo == TradeInfo.MyselfIsReady ? lang.GetMessage("trade_info_isReady", this) : lang.GetMessage("trade_info_notReady", this);
                        var partnerIsReady = tradeInfo == TradeInfo.PartnerIsReady ? lang.GetMessage("trade_info_isReady", this) : lang.GetMessage("trade_info_notReady", this);

                        hurt.SendChatMessage(session, myselfIsReady, lang.GetMessage("trade_info_player_Items", this).Replace("{itemlist}", myItems_msg).Replace("{player}", mySession.Name));
                        hurt.SendChatMessage(session, partnerIsReady, lang.GetMessage("trade_info_player_Items", this).Replace("{itemlist}", partnerItems_msg).Replace("{player}", partnerName));
                    }
                    break;

                case "trc":
                    if (tradeInfo == TradeInfo.Null)
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_not_started", this));
                    }
                    else if (tradeInfo == TradeInfo.HasRequest)
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_declined_you", this).Replace("{player}", partnerName));
                        hurt.SendChatMessage(partnerSession, _msg_prefix, lang.GetMessage("trade_declined_player", this).Replace("{player}", mySession.Name));
                    }
                    else if (tradeInfo == TradeInfo.IsRequesting)
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_cancel_you", this).Replace("{player}", partnerName));
                        hurt.SendChatMessage(partnerSession, _msg_prefix, lang.GetMessage("trade_cancel_player", this).Replace("{player}", mySession.Name));
                    }
                    else
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_cancel_current_you", this));
                        hurt.SendChatMessage(partnerSession, _msg_prefix, lang.GetMessage("trade_cancel_current_player", this).Replace("{player}", mySession.Name));
                    }

                    cancelTrade(mySession, partnerSession, myID, partnerID);

                    break;

                case "tr":
                    if (tradeInfo == TradeInfo.Null || tradeInfo == TradeInfo.HasRequest || tradeInfo == TradeInfo.IsRequesting)
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_not_started", this));
                        return;
                    }
                    
                    //Test if inside SafeTrade Zone
                    if (AlertAreas != null && isUsingZone())
                    {
                        if (!(bool)AlertAreas.Call("isInsideArea", session?.WorldPlayerEntity.transform.position, "SafeTradeZone"))
                        {
                            hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("tradezone_you_outside", this));
                            return;
                        }
                        else if (!(bool)AlertAreas.Call("isInsideArea", partnerSession?.WorldPlayerEntity.transform.position, "SafeTradeZone"))
                        {
                            hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("tradezone_player_outside", this).Replace("{player}", partnerName));
                            return;
                        }
                    }
                    
                    if (tradeInfo == TradeInfo.MyselfIsReady)
                    {
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_accepted_already", this));
                        return;
                    }

                    if (tradeInfo == TradeInfo.Trading)
                    {
                        _trades[partnerID].isReady = true;
                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_accepted_you", this).Replace("{player}", partnerName));
                        hurt.SendChatMessage(partnerSession, _msg_prefix, lang.GetMessage("trade_accepted_player", this).Replace("{player}", mySession.Name));
                        return;
                    }

                    if (tradeInfo == TradeInfo.PartnerIsReady)
                    {
                        foreach (PlayerTradeItem pti in _trades[myID].items)
                        {
                            im.GiveItem(session.Player, im.GetItem(pti.id), pti.stackSize);
                        }
                        foreach (PlayerTradeItem pti in _trades[partnerID].items)
                        {
                            im.GiveItem(partnerSession.Player, im.GetItem(pti.id), pti.stackSize);
                        }

                        hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_completed", this));
                        hurt.SendChatMessage(partnerSession, _msg_prefix, lang.GetMessage("trade_completed", this));

                        _trades.Remove(myID);
                        _trades.Remove(partnerID);

                        return;
                    }

                    break;

                default:
                    hurt.SendChatMessage(session, _msg_prefix, lang.GetMessage("trade_usage", this));
                    break;
            }
        }
        #endregion

        #region Hooks

        /*
        private void OnPlayerChat(PlayerSession session, string message) { }

        private void OnPlayerInput(PlayerSession session, InputControls input) { }

        private void OnPlayerConnected(PlayerSession player) { }

        private void OnPlayerInit(PlayerSession player) { }

        private void OnPlayerSpawn(PlayerSession session) { }
        */

        private object OnPlayerSuicide(PlayerSession session)
        {
            if (AlertAreas != null && isUsingZone())
            {
                if ((bool)AlertAreas.Call("isInsideArea", session.WorldPlayerEntity.transform.position, "SafeTradeZone"))
                {
                    return false;
                }
            }
            return null;
        }

        private object OnPlayerDeath(PlayerSession player, EntityEffectSourceData source)
        {
            if (AlertAreas != null && isUsingZone())
            {
                if ((bool)AlertAreas.Call("isInsideArea", player.WorldPlayerEntity.transform.position, "SafeTradeZone"))
                {
                    string sourceName = GameManager.Instance.GetDescriptionKey(source.EntitySource);
                    string killerName = sourceName.Replace("(P)", "");
                    if (string.IsNullOrEmpty(killerName))
                        return null;
                    PlayerSession killerSession = GetSession(player, killerName);
                    if (killerSession != null)
                    {
                        timer.Once(0.1f, () =>
                        {
                            Heal(player);
                        });

                        //Is Player Kill
                        //If in trade: Cancel trade and give both players their items back
                        var myID = player.SteamId.m_SteamID;

                        if (_trades.ContainsKey(myID))
                        {
                            var partnerID = _trades[myID].tradeID;
                            var partnerSession = GetSession(partnerID);

                            cancelTrade(player, partnerSession, myID, partnerID);

                            if(partnerSession.SteamId.m_SteamID != killerSession.SteamId.m_SteamID)
                                hurt.SendChatMessage(partnerSession, lang.GetMessage("trade_prefix", this), lang.GetMessage("trade_cancel_kill", this).Replace("{player}", player.Name).Replace("{killer}", killerName));
                        }
                        
                        bool dropBackpack = _helpers.GetConfig(false, "Settings", "SafeTradeZone", "4-penalty-drop-backpack");
                        bool dropEverything = _helpers.GetConfig(false, "Settings", "SafeTradeZone", "4-penalty-drop-everything");

                        if (dropBackpack || dropEverything)
                        {
                            PlayerInventory killerInventory = killerSession.WorldPlayerEntity.GetComponent<PlayerInventory>();
                            var killerItems = killerInventory.Items;
                            GameObject cache = null;
                            IStorable cacheInventory = null;

                            for (int i = 0; i < killerItems.Length; i++)
                            {
                                var itemInstance = killerItems[i];

                                if (i >= 0 && i <=15 && dropEverything)
                                {
                                    if (itemInstance != null)
                                    {
                                        if(cache == null)
                                        {
                                            cache = Singleton<HNetworkManager>.Instance?.NetInstantiate("LootCache", killerSession.WorldPlayerEntity.transform.position, Quaternion.identity, GameManager.GetSceneTime());
                                            cacheInventory = cache?.GetComponentByInterface<IStorable>();
                                            cacheInventory.Capacity = killerInventory.Capacity;
                                        }
                                        
                                        IItem item = itemInstance.Item;
                                        GlobalItemManager.Instance.GiveItem(item, itemInstance.StackSize, cacheInventory);
                                        itemInstance.ReduceStackSize(itemInstance.StackSize);
                                    }
                                }
                                else if (i >= 16)
                                {
                                    if (itemInstance != null)
                                    {
                                        if (cache == null)
                                        {
                                            cache = Singleton<HNetworkManager>.Instance?.NetInstantiate("LootCache", killerSession.WorldPlayerEntity.transform.position, Quaternion.identity, GameManager.GetSceneTime());
                                            cacheInventory = cache?.GetComponentByInterface<IStorable>();
                                            cacheInventory.Capacity = killerInventory.Capacity;
                                        }

                                        IItem item = itemInstance.Item;
                                        GlobalItemManager.Instance.GiveItem(item, itemInstance.StackSize, cacheInventory);
                                        itemInstance.ReduceStackSize(itemInstance.StackSize);
                                    }
                                }
                            }
                            killerInventory.Invalidate(false);
                        }
                        
                        string defaultPenaltySpawn = _helpers.Vector3ToString(PlayerSpawnManager.Instance.GetRandomPlayerSpawnPoint().GetSpawnPosition());

                        Vector3 penaltySpawn = _helpers.StringToVector3(_helpers.GetConfig(defaultPenaltySpawn, "Settings", "SafeTradeZone", "3-penalty-spawn"));
                        killerSession.WorldPlayerEntity.transform.position = penaltySpawn;
                        hurt.BroadcastChat(lang.GetMessage("trade_prefix", this) + " " + lang.GetMessage("trade_broadcast_penalty", this).Replace("{player}", killerSession.Name));
                    }
                    return false;
                }
            }
            return null;
        }

        private void OnPlayerDisconnected(PlayerSession session)
        {
            var myID = session.SteamId.m_SteamID;

            if (_trades.ContainsKey(myID))
            {
                var partnerID = _trades[myID].tradeID;
                var partnerSession = GetSession(partnerID);

                cancelTrade(session, partnerSession, myID, partnerID);

                hurt.SendChatMessage(partnerSession, lang.GetMessage("trade_prefix", this), lang.GetMessage("trade_cancel_logout", this).Replace("{player}", session.Name));
            }
        }
        
        #endregion Hooks
    }
}