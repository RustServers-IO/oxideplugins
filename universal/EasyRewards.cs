using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("EasyRewards", "Lizzaran", "1.0.0")]
    [Description("Give players rewards for votes.")]
    internal class EasyRewards : HurtworldPlugin
    {
        #region Classes

        internal class Helpers
        {
            private readonly DynamicConfigFile _config;
            private readonly Action<ELogType, string> _log;
            private readonly Permission _permission;
            private readonly HurtworldPlugin _plugin;

            public Helpers(DynamicConfigFile config, HurtworldPlugin plugin,
                Permission permission, Action<ELogType, string> log)
            {
                _config = config;
                _plugin = plugin;
                _permission = permission;
                _log = log;
            }

            public string PermissionPrefix { get; set; }

            public bool HasPermission(PlayerSession session, params string[] paramArray)
            {
                var permission = ArrayToString(paramArray, ".");
                return _permission.UserHasPermission(GetPlayerId(session),
                    permission.StartsWith(PermissionPrefix) ? $"{permission}" : $"{PermissionPrefix}.{permission}");
            }

            public void RegisterPermission(params string[] paramArray)
            {
                var permission = ArrayToString(paramArray, ".");
                _permission.RegisterPermission(
                    permission.StartsWith(PermissionPrefix) ? $"{permission}" : $"{PermissionPrefix}.{permission}",
                    _plugin);
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
                        $"Couldn't read from config file: {ArrayToString(stringArgs, "/")}");
                    return defaultVal;
                }
                return (T) Convert.ChangeType(_config.Get(stringArgs.ToArray()), typeof (T));
            }

            public string[] ObjectToStringArray(object[] args)
            {
                return args.DefaultIfEmpty().Select(a => a.ToString()).ToArray();
            }

            public string ArrayToString(string[] array, string separator)
            {
                return string.Join(separator, array);
            }

            public string GetPlayerId(PlayerSession session)
            {
                return session.SteamId.ToString();
            }

            public bool IsPlayerDead(PlayerSession session)
            {
                return
                    (session?.WorldPlayerEntity?.GetComponent<EntityStats>()?
                        .GetFluidEffect(EEntityFluidEffectType.Health)?
                        .GetValue() ?? -1f) <= 0;
            }

            public bool EnumTryParse<T>(string value, out T result) where T : struct, IConvertible
            {
                var retValue = value != null && EnumIsDefined(typeof (T), value, StringComparison.OrdinalIgnoreCase);
                result = retValue
                    ? (T) Enum.Parse(typeof (T), value, true)
                    : default(T);
                return retValue;
            }

            public bool EnumIsDefined(Type type, string name, StringComparison comp)
            {
                return Enum.GetNames(type).Any(e => e.Equals(name, comp));
            }
        }

        #endregion Classes

        #region Enums

        internal enum ELogType
        {
            Info,
            Warning,
            Error
        }

        internal enum ERewardMode
        {
            OneTime,
            Cycle,
            RepeatLast
        }

        #endregion Enums

        #region Variables

        private string _apiKey;
        private Helpers _helpers;
        private WebRequests _webRequests;
        private readonly Dictionary<string, int> _votes = new Dictionary<string, int>();
        private readonly Dictionary<int, Dictionary<int, int>> _rewards = new Dictionary<int, Dictionary<int, int>>();
        private readonly Dictionary<string, DateTime> _lastChecks = new Dictionary<string, DateTime>();
        private ERewardMode _rewardMode;

        #endregion Variables

        #region Methods

        // ReSharper disable once UnusedMember.Local
        private void Loaded()
        {
            _webRequests = Interface.GetMod().GetLibrary<WebRequests>("WebRequests");
            _helpers = new Helpers(Config, this, permission, Log)
            {
                PermissionPrefix = Regex.Replace(Title, "[^0-9a-zA-Z]+", string.Empty).ToLower()
            };

            LoadConfig();
            LoadPermissions();
            LoadData();
            LoadMessages();

            if (string.IsNullOrEmpty(_apiKey))
            {
                Log(ELogType.Warning, "No API Key specified!");
            }

            var rewardMode = _helpers.GetConfig("OneTime", "Mode");
            if (!_helpers.EnumTryParse(rewardMode, out _rewardMode))
            {
                _rewardMode = ERewardMode.OneTime;
                Log(ELogType.Error, $"The Reward Mode \"{rewardMode}\" isn't valid. Using the mode \"OneTime\" instead.");
            }
        }

        private void LoadMessages()
        {
            #region Messages

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Misc - No Permission", "You don't have the permission to use this command."},
                {"Vote - Unknown Error", "An unknown error occured. Please try again later."},
                {"Vote - Could Not Claim", "Your vote couldn't be claimed. Please try again later."},
                {"Vote - None", "You have no new rewards, please vote for our server to receive rewards."},
                {"Vote - Already Claimed", "You have already claimed your reward in the last 24 hours."},
                {"Vote - All Rewards Already", "You have already claimed all your rewards."},
                {"Vote - Cooldown", "You need to wait '{seconds}' seconds before checking again."},
                {"Vote - Dead", "You need to be alive to claim your rewards."},
                {"Vote - Rewards Header", "Get rewards for daily votes."},
                {"Vote - Rewards", "Reward: {reward} when you have reached '{vote}' votes."},
                {"Vote - Rewards Last", "Last Reward: {reward} when you have reached '{vote}' votes."},
                {"Vote - Rewards Next", "Next Reward: {reward} when you have reached '{vote}' votes."},
                {"Vote - Thanks", "Thanks for voting '{count}' times. You have received your reward."}
            }, this);

            #endregion Messages
        }

        protected override void LoadDefaultConfig()
        {
            Log(ELogType.Warning, "No config file found, generating a new one.");
        }

        private new void LoadConfig()
        {
            #region Config

            _helpers.SetConfig("Mode", ERewardMode.OneTime.ToString());
            _helpers.SetConfig("API Key", string.Empty);
            _helpers.SetConfig("Auto Check", false);
            _helpers.SetConfig("Cooldown", 600);

            _helpers.SetConfig("Rewards", new Dictionary<string, object>
            {
                {
                    "1", new Dictionary<string, object>
                    {
                        {"44", 20}
                    }
                },
                {
                    "2", new Dictionary<string, object>
                    {
                        {"44", 40}
                    }
                },
                {
                    "3", new Dictionary<string, object>
                    {
                        {"44", 60},
                        {"22", 60}
                    }
                }
            });

            _helpers.SetConfig("Multiplicators", new Dictionary<string, object>
            {
                {$"{_helpers.PermissionPrefix}.multiplicator.three", 3}
            });

            _apiKey = _helpers.GetConfig(string.Empty, "API Key");

            var rewards =
                _helpers.GetConfig(new Dictionary<string, object> {{"1", new Dictionary<string, int> {{"44", 20}}}},
                    "Rewards");
            foreach (var vote in rewards)
            {
                int voteCount;
                if (!int.TryParse(vote.Key, out voteCount) || voteCount <= 0)
                {
                    continue;
                }
                _rewards[voteCount] = new Dictionary<int, int>();
                var rewardsDic = vote.Value as Dictionary<string, object>;
                if (rewardsDic != null)
                {
                    foreach (var reward in rewardsDic)
                    {
                        int rewardId;
                        int rewardCount;
                        if (!int.TryParse(reward.Key, out rewardId))
                        {
                            continue;
                        }
                        if (!int.TryParse(reward.Value.ToString(), out rewardCount))
                        {
                            continue;
                        }
                        _rewards[voteCount][rewardId] = rewardCount;
                    }
                }
            }

            #endregion Config

            SaveConfig();
        }

        private void LoadPermissions()
        {
            _helpers.RegisterPermission("use");

            foreach (
                var dicPair in
                    _helpers.GetConfig(
                        new Dictionary<string, object> {{$"{_helpers.PermissionPrefix}.multiplicator.three", 3}},
                        "Multiplicators"))
            {
                _helpers.RegisterPermission(dicPair.Key);
            }
        }

        private void LoadData()
        {
            var fileSystem = Interface.GetMod().DataFileSystem;
            foreach (var vote in fileSystem.ReadObject<Dictionary<string, int>>("EasyRewards"))
            {
                _votes[vote.Key] = vote.Value;
            }
            SaveData();
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("EasyRewards", _votes);
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

        private void CheckVote(PlayerSession session)
        {
            Action<int, string> callback = (code, response) =>
            {
                if (string.IsNullOrEmpty(response) || code != 200)
                {
                    hurt.SendChatMessage(session,
                        lang.GetMessage("Vote - Unknown Error", this, _helpers.GetPlayerId(session)));
                    Log(ELogType.Warning,
                        $"{code}: An error occured while waiting for an response from hurtworld-servers.net for {session.Name}");
                    return;
                }
                int voteState;
                if (!int.TryParse(response, out voteState))
                {
                    hurt.SendChatMessage(session,
                        lang.GetMessage("Vote - Unknown Error", this, _helpers.GetPlayerId(session)));
                    Log(ELogType.Warning, $"Couldn't parse the response from hurtworld-servers.net for {session.Name}");
                    return;
                }
                switch (voteState)
                {
                    case 0:
                        hurt.SendChatMessage(session,
                            lang.GetMessage("Vote - None", this, _helpers.GetPlayerId(session)));
                        return;
                    case 2:
                        hurt.SendChatMessage(session,
                            lang.GetMessage("Vote - Already Claimed", this, _helpers.GetPlayerId(session)));
                        return;
                    case 1:
                        ClaimVote(session);
                        break;
                }
            };
            _webRequests.EnqueueGet(
                $"http://hurtworld-servers.net/api/?object=votes&element=claim&key={_apiKey}&steamid={session.SteamId}",
                callback, this);
        }

        private void ClaimVote(PlayerSession session)
        {
            Action<int, string> callback = (code, response) =>
            {
                if (string.IsNullOrEmpty(response) || code != 200)
                {
                    hurt.SendChatMessage(session,
                        lang.GetMessage("Vote - Unknown Error", this, _helpers.GetPlayerId(session)));
                    Log(ELogType.Warning,
                        $"{code}: An error occured while waiting for an response from hurtworld-servers.net for {session.Name}");
                    return;
                }
                int voteState;
                if (!int.TryParse(response, out voteState))
                {
                    hurt.SendChatMessage(session,
                        lang.GetMessage("Vote - Unknown Error", this, _helpers.GetPlayerId(session)));
                    Log(ELogType.Warning, $"Couldn't parse the response from hurtworld-servers.net for {session.Name}");
                    return;
                }
                switch (voteState)
                {
                    case 0:
                        hurt.SendChatMessage(session,
                            lang.GetMessage("Vote - Could Not Claim", this, _helpers.GetPlayerId(session)));
                        Log(ELogType.Warning, $"Couldn't claim the vote for {session.Name}");
                        return;
                    case 1:
                        var playerId = _helpers.GetPlayerId(session);
                        if (!_votes.ContainsKey(playerId))
                        {
                            _votes[playerId] = 0;
                        }
                        _votes[playerId] = _votes[playerId] + 1;
                        GiveReward(session);
                        SaveData();
                        break;
                }
            };
            _webRequests.EnqueueGet(
                $"http://hurtworld-servers.net/api/?action=post&object=votes&element=claim&key={_apiKey}&steamid={session.SteamId}",
                callback, this);
        }

        private void GiveReward(PlayerSession session)
        {
            var totalVotes = _votes[_helpers.GetPlayerId(session)];
            var votes = totalVotes;
            var highestVote = _rewards.Keys.Max();
            switch (_rewardMode)
            {
                case ERewardMode.OneTime:
                    if (votes > highestVote)
                    {
                        hurt.SendChatMessage(session,
                            lang.GetMessage("Vote - All Rewards Already", this, _helpers.GetPlayerId(session)));
                        return;
                    }
                    break;
                case ERewardMode.Cycle:
                    if (votes > highestVote)
                    {
                        votes = votes - highestVote*(votes/highestVote);
                    }
                    break;
                case ERewardMode.RepeatLast:
                    if (votes > highestVote)
                    {
                        votes = highestVote;
                    }
                    break;
            }

            var rewards = _rewards[votes];
            var playerInventory = session.WorldPlayerEntity?.GetComponent<PlayerInventory>();
            if (playerInventory != null)
            {
                var itemManager = Singleton<GlobalItemManager>.Instance;
                if (itemManager != null)
                {
                    var multiplicator = GetConfigMultiplicator(session);
                    foreach (var reward in rewards)
                    {
                        var newItem = itemManager.GetItem(reward.Key);
                        if (newItem != null)
                        {
                            itemManager.GiveItem(newItem, reward.Value*multiplicator, playerInventory);
                        }
                    }
                }
            }

            hurt.SendChatMessage(session,
                lang.GetMessage("Vote - Thanks", this, _helpers.GetPlayerId(session))
                    .Replace("{count}", totalVotes.ToString()));
        }

        // ReSharper disable once UnusedMember.Local
        private void OnPlayerSpawn(PlayerSession session)
        {
            if (_helpers.GetConfig(false, "Auto Check") && _helpers.HasPermission(session, "use") &&
                !_helpers.IsPlayerDead(session))
            {
                var cooldown = _helpers.GetConfig(600, "Cooldown");
                var playerId = _helpers.GetPlayerId(session);
                if (!_lastChecks.ContainsKey(playerId) || DateTime.Now.AddSeconds(-cooldown) > _lastChecks[playerId])
                {
                    _lastChecks[playerId] = DateTime.Now;
                    CheckVote(session);
                }
            }
        }

        private string GetItemName(int id)
        {
            return GlobalItemManager.Instance.GetItem(id).GetNameKey().Split('/').Last();
        }

        private int GetConfigMultiplicator(PlayerSession session)
        {
            var multiplicator = 1;
            var multiplicators =
                _helpers.GetConfig(
                    new Dictionary<string, object> {{$"{_helpers.PermissionPrefix}.multiplicator.one", 1}},
                    "Multiplicators");
            return
                (from dicPair in multiplicators
                    where _helpers.HasPermission(session, dicPair.Key)
                    select Convert.ToInt32(dicPair.Value))
                    .Concat(new[] {multiplicator}).Max();
        }

        #endregion Methods

        #region Commands

        // ReSharper disable UnusedMember.Local
        // ReSharper disable UnusedParameter.Local

        [ChatCommand("rewards")]
        private void CommandRewards(PlayerSession session, string command, string[] args)
        {
            if (!_helpers.HasPermission(session, "use"))
            {
                hurt.SendChatMessage(session,
                    lang.GetMessage("Misc - No Permission", this, _helpers.GetPlayerId(session)));
                return;
            }
            var playerId = _helpers.GetPlayerId(session);
            var votes = _rewards.OrderBy(r => r.Key);
            var totalVotes = _votes.ContainsKey(playerId) ? _votes[playerId] : 0;
            var highestVote = _rewards.Keys.Max();
            var voteCount = totalVotes > highestVote && _rewardMode == ERewardMode.Cycle
                ? totalVotes - highestVote*(totalVotes/highestVote)
                : (totalVotes > highestVote && _rewardMode == ERewardMode.RepeatLast ? highestVote : totalVotes);
            hurt.SendChatMessage(session, lang.GetMessage("Vote - Rewards Header", this, playerId));
            foreach (var vote in votes)
            {
                var rewardText = vote.Value.Aggregate(string.Empty,
                    (current, reward) => current + $"{reward.Value} x {GetItemName(reward.Key)}, ");
                rewardText = rewardText.Remove(rewardText.Length - 2, 2);
                string rewardMessage;
                switch (_rewardMode)
                {
                    case ERewardMode.OneTime:
                        rewardMessage = totalVotes > 0 && voteCount == vote.Key
                            ? "Vote - Rewards Last"
                            : (voteCount + 1 == vote.Key ? "Vote - Rewards Next" : "Vote - Rewards");
                        break;
                    case ERewardMode.Cycle:
                        rewardMessage = totalVotes > 0 && voteCount == vote.Key
                            ? "Vote - Rewards Last"
                            : ((voteCount + 1 > highestVote ? 1 : voteCount + 1) == vote.Key
                                ? "Vote - Rewards Next"
                                : "Vote - Rewards");
                        break;
                    case ERewardMode.RepeatLast:
                        rewardMessage = voteCount == highestVote && highestVote == vote.Key || voteCount + 1 == vote.Key
                            ? "Vote - Rewards Next"
                            : (totalVotes > 0 && voteCount == vote.Key ? "Vote - Rewards Last" : "Vote - Rewards");
                        break;
                    default:
                        rewardMessage = "Vote - Rewards";
                        break;
                }
                hurt.SendChatMessage(session,
                    lang.GetMessage(rewardMessage, this, playerId)
                        .Replace("{vote}", vote.Key.ToString())
                        .Replace("{reward}", rewardText));
            }
        }

        [ChatCommand("getreward")]
        private void CommandGetReward(PlayerSession session, string command, string[] args)
        {
            if (!_helpers.HasPermission(session, "use"))
            {
                hurt.SendChatMessage(session,
                    lang.GetMessage("Misc - No Permission", this, _helpers.GetPlayerId(session)));
                return;
            }
            if (_helpers.IsPlayerDead(session))
            {
                hurt.SendChatMessage(session,
                    lang.GetMessage("Vote - Dead", this, _helpers.GetPlayerId(session)));
                return;
            }
            var cooldown = _helpers.GetConfig(600, "Cooldown");
            var playerId = _helpers.GetPlayerId(session);
            if (!_lastChecks.ContainsKey(playerId) || DateTime.Now.AddSeconds(-cooldown) > _lastChecks[playerId])
            {
                _lastChecks[playerId] = DateTime.Now;
                CheckVote(session);
            }
            else
            {
                hurt.SendChatMessage(session,
                    lang.GetMessage("Vote - Cooldown", this, _helpers.GetPlayerId(session))
                        .Replace("{seconds}",
                            ((int)
                                Math.Ceiling((_lastChecks[playerId] - DateTime.Now.AddSeconds(-cooldown)).TotalSeconds))
                                .ToString()));
            }
        }

        [ChatCommand("getrewards")]
        private void CommandGetRewards(PlayerSession session, string command, string[] args)
        {
            CommandGetReward(session, command, args);
        }

        // ReSharper restore UnusedParameter.Local
        // ReSharper restore UnusedMember.Local

        #endregion Commands
    }
}