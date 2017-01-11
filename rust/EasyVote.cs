﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using static System.Convert;

using Rust;
using UnityEngine;

using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("EasyVote", "Exel80", "1.2.6", ResourceId = 2102)]
    [Description("Making voting super easy and smooth!")]
    class EasyVote : RustPlugin
    {
        // Special thanks to MJSU, for all hes efforts what he have done so far!
        // http://oxidemod.org/members/mjsu.99205/

        #region Initializing
        private bool DEBUG = false; // Dev mod
        private bool Voted = false; // If voted, overide NoRewards.
        private bool NoRewards = false; // If no voted, then print "NoRewards"
        private DateTime Cooldown = DateTime.Now; // Datetime cooldown
        private List<int> numberMax = new List<int>();
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        // {"Claim reward GET URL", "Vote status GET URL", "Server vote link to chat URL"}
        string[] RustServers = { "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key={0}&steamid={1}",
            "https://rust-servers.net/api/?object=votes&element=claim&key={0}&steamid={1}", "http://rust-servers.net/server/{0}" };
        string[] TopRustServers = { "http://api.toprustservers.com/api/put?plugin=voter&key={0}&uid={1}",
            "http://api.toprustservers.com/api/get?plugin=voter&key={0}&uid={1}", "http://toprustservers.com/server/{0}" };
        string[] TopServeurs = { "https://api.top-serveurs.net/votes?server_token={0}&steam_id={1}",
            "https://api.top-serveurs.net/votes/check?server_token={0}&steam_id={1}", "https://top-serveurs.net/srv/{0}" };
        string[] BeancanIO = { "http://beancan.io/vote/put/{0}/{1}", "http://beancan.io/vote/get/{0}/{1}", "http://beancan.io/server/{0}" };

        private void Loaded()
        {
            #region Permissions
            permission.RegisterPermission("EasyVote.Admin", this);
            #endregion
            #region Language Setup
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ClaimError"] = "Something went wrong! We got <color=red>{0} error</color> from <color=yellow>{1}</color>. Please try again later!",
                ["ClaimReward"] = "You just received your vote reward(s). Enjoy!",
                ["EarnReward"] = "When you are voted. Type <color=yellow>/claim</color> to earn your reward(s)!",
                ["RewardList"] = "<color=cyan>Player reward, when voted</color> <color=orange>{0}</color> <color=cyan>time(s).</color>",
                ["Received"] = "You have received {0}x {1}",
                ["Highest"] = "<color=cyan>The player with the highest number of votes per month gets a free</color> <color=yellow>{0}</color><color=cyan> rank for 1 month.</color> <color=yellow>/vote</color> Vote now to get free rank!",
                ["HighestCongrats"] = "<color=yellow>{0}</color> <color=cyan>was highest voter past month</color><color=cyan>. He earned free</color> <color=yellow>{1}</color> <color=cyan>rank for 1 month. Vote now to earn it next month!</color>",
                ["ThankYou"] = "Thank you for voting {0} time(s)",
                ["NoRewards"] = "You do not have any new rewards avaliable \n Please type <color=yellow>/vote</color> and go to the website to vote and receive your reward",
                ["RemeberClaim"] = "You haven't yet claimed your reward from voting server! Use <color=cyan>/claim</color> to claim your reward! \n You have to claim your reward in <color=yellow>24h</color>! Otherwise it will be gone!",
                ["GlobalClaimAnnouncment"] = "<color=yellow>{0}</color><color=cyan> has voted </color><color=yellow>{1}</color><color=cyan> time(s) and just received their rewards. Find out where to vote by typing</color><color=yellow> /vote</color>\n<color=cyan>To see a list of avaliable rewards type</color><color=yellow> /reward list</color>",
                ["Voted"] = "You have voted <color=yellow>{0}</color> time! Thank you mate :)",
                ["Admin"] = "Usage: /vote [test]",
                ["money"] = "{0} has been desposited into your account",
                ["rp"] = "You have gained {0} reward points",
                ["addgroup"] = "You have been added to group {0} {1}",
                ["grantperm"] = "You have been given permission {0} {1}",
                ["zlvl-wc"] = "You have gained {0} woodcrafting level(s)",
                ["zlvl-mg"] = "You have gained {0} mining level(s)",
                ["zlvl-s"] = "You have gained {0} skinning level(s)",
                ["zlvl-c"] = "You have gained {0} crafting level(s)"
            }, this);
            #endregion

            _storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("EasyVote");
            LoadConfigValues();
            BuildNumberMax();

            // Global announcement about HighestVote every 5min
            if (_config.Settings["HighestVoter"]?.ToLower() == "true"
                && (_config.Settings["HighestVoterRewardGroup"]?.ToString() != String.Empty || _config.Settings["HighestVoterInterval"] != String.Empty))
            {
                // Try parse string to float from (Config > Settings > HighestVoterInterval)
                float configTime;
                float.TryParse(_config.Settings["HighestVoterInterval"], out configTime);

                // Convert minutes to secods
                float time = configTime * 60;
                timer.Every(time, () => { PrintToChat(Lang("Highest", null, _config.Settings["HighestVoterRewardGroup"])); });
            }

            // Checking if month is changed
            NextMonth();
        }
        #endregion

        #region Announcment
        void OnPlayerSleepEnded(BasePlayer player)
        {
            // Checking if month is changed
            NextMonth();

            // Global Announcment highest voter
            if (player.userID == _storedData.highestVoter)
            {
                if (_config.Settings["HighestVoter"]?.ToLower() == "true"
                    && _config.Settings["HighestVoterRewardGroup"]?.ToLower() != String.Empty
                    && _storedData.announcemented != 1)
                {
                    PrintToChat(Lang("HighestCongrats", player.UserIDString, player.displayName, _config.Settings["HighestVoterRewardGroup"].ToString()));
                    setGroup(player.UserIDString, _config.Settings["HighestVoterRewardGroup"].ToString());
                    _storedData.setAnnouncemented(1);
                    Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);
                }
            }

            // if Announcment is true, check player status when his SleepEnded.
            if (_config.Settings["Announcment"]?.ToLower() == "true")
            {
                if (IsEmpty(_config.VoteSettings["RustServersID"].ToString())
                    && IsEmpty(_config.VoteSettings["RustServersKEY"].ToString()))
                {
                    _Debug(player, $"Check {player.displayName} vote status from RustServers");

                    string _BroadcastServer = String.Format(RustServers[1], _config.VoteSettings["RustServersKEY"], player.userID);
                    webrequest.EnqueueGet(_BroadcastServer, (code, response) => CheckStatus(code, response, player), this);
                }
                if (IsEmpty(_config.VoteSettings["TopRustServersID"].ToString())
                    && IsEmpty(_config.VoteSettings["TopRustServersKEY"].ToString()))
                {
                    _Debug(player, $"Check {player.displayName} vote status from TopRustServers");

                    string _BroadcastServer = String.Format(TopRustServers[1], _config.VoteSettings["TopRustServersKEY"], player.userID);
                    webrequest.EnqueueGet(_BroadcastServer, (code, response) => CheckStatus(code, response, player), this);
                }
                if (IsEmpty(_config.VoteSettings["BeancanID"].ToString())
                    && IsEmpty(_config.VoteSettings["BeancanKEY"].ToString()))
                {
                    _Debug(player, $"Check {player.displayName} vote status from BeancanIO");

                    string _BroadcastServer = String.Format(BeancanIO[1], _config.VoteSettings["BeancanKEY"], player.userID);
                    webrequest.EnqueueGet(_BroadcastServer, (code, response) => CheckStatus(code, response, player), this);
                }
                if (IsEmpty(_config.VoteSettings["TopServeursID"].ToString())
                    && IsEmpty(_config.VoteSettings["TopServeursKEY"].ToString()))
                {
                    _Debug(player, $"Check {player.displayName} vote status from TopServeurs");

                    string _BroadcastServer = String.Format(TopServeurs[1], _config.VoteSettings["TopServeursKEY"], player.userID);
                    webrequest.EnqueueGet(_BroadcastServer, (code, response) => CheckStatus(code, response, player), this);
                }
            }
        }
        #endregion

        #region Commands
        [ChatCommand("vote")]
        void cmdVote(BasePlayer player, string command, string[] args)
        {
            if (hasPermission(player, "EasyVote.Admin"))
            {
                if (args?.Length != 0)
                {
                    string CommandsStr = args[0].ToLower();

                    switch (CommandsStr)
                    {
                        case "test":
                            {
                                //int CommandsValue = 0;

                                //try { CommandsValue = Convert.ToInt16(args[1]); }
                                //catch (Exception ex) { Chat(player, Lang("Admin", player.UserIDString)); return; }

                                RewardHandler(player);
                            }
                            break;
                        default:
                            {
                                Chat(player, Lang("Admin", player.UserIDString));
                            }
                            break;
                    }
                    return;
                }
            }

            // Making sure that ID or KEY isn't Empty
            if (IsEmpty(_config.VoteSettings["RustServersID"].ToString())
                && IsEmpty(_config.VoteSettings["RustServersKEY"].ToString()))
                Chat(player, $"<color=silver>{String.Format(RustServers[2], _config.VoteSettings["RustServersID"])}</color>");

            if (IsEmpty(_config.VoteSettings["TopRustServersID"].ToString())
                && IsEmpty(_config.VoteSettings["TopRustServersKEY"].ToString()))
                Chat(player, $"<color=silver>{String.Format(TopRustServers[2], _config.VoteSettings["TopRustServersID"])}</color>");

            if (IsEmpty(_config.VoteSettings["BeancanID"].ToString())
                && IsEmpty(_config.VoteSettings["BeancanKEY"].ToString()))
                Chat(player, $"<color=silver>{String.Format(BeancanIO[2], _config.VoteSettings["BeancanID"])}</color>");

            if (IsEmpty(_config.VoteSettings["TopServeursID"].ToString())
                && IsEmpty(_config.VoteSettings["TopServeursKEY"].ToString()))
                Chat(player, $"<color=silver>{String.Format(TopServeurs[2], _config.VoteSettings["TopServeursID"])}</color>");

            try
            {
                var info = new PlayerData(player);
                Chat(player, Lang("Voted", player.UserIDString, _storedData.Players[info.id].voted));
            }
            catch (Exception ex) { }

            Chat(player, Lang("EarnReward", player.UserIDString));
        }
        [ChatCommand("claim")]
        void cmdClaim(BasePlayer player, string command, string[] args)
        {
            var timeout = 5500f; // Timeout (in milliseconds)

            if (IsEmpty(_config.VoteSettings["RustServersKEY"].ToString()))
            {
                string _format = String.Format(RustServers[0], _config.VoteSettings["RustServersKEY"], player.userID);
                webrequest.EnqueueGet(_format, (code, response) => ClaimReward(code, response, player, "RustServers"), this, null, timeout);
                _Debug(player, _format);
            }
            if (IsEmpty(_config.VoteSettings["TopRustServersKEY"].ToString()))
            {
                string _format = String.Format(TopRustServers[0], _config.VoteSettings["TopRustServersKEY"], player.userID);
                webrequest.EnqueueGet(_format, (code, response) => ClaimReward(code, response, player, "TopRustServers"), this, null, timeout);
                _Debug(player, _format);
            }
            if (IsEmpty(_config.VoteSettings["BeancanKEY"].ToString()))
            {
                string _format = String.Format(BeancanIO[0], _config.VoteSettings["BeancanKEY"], player.userID);
                webrequest.EnqueueGet(_format, (code, response) => ClaimReward(code, response, player, "BeancanIO"), this, null, timeout);
                _Debug(player, _format);
            }
            if (IsEmpty(_config.VoteSettings["TopServeursKEY"].ToString()))
            {
                string _format = String.Format(TopServeurs[0], _config.VoteSettings["TopServeursKEY"], player.userID);
                webrequest.EnqueueGet(_format, (code, response) => ClaimReward(code, response, player, "TopServeurs"), this, null, timeout);
                _Debug(player, _format);
            }

            timer.Once(1.85f, () =>
            {
                if (NoRewards && !Voted)
                    Chat(player, $"{Lang("NoRewards", player.UserIDString)}");
            });
        }
        [ChatCommand("reward")]
        void cmdReward(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (args[0] == "list")
                    rewardList(player);
            }
            catch (Exception ex) { /*PrintWarning($"Something intresting happen when player try use /reward list command.\n\n{ex.ToString()}");*/ }

        }
        #endregion

        #region Reward Handler
        private void RewardHandler(BasePlayer player)
        {
            var info = new PlayerData(player);

            // Check that player is in "database".
            if (!_storedData.Players.ContainsKey(info.id))
                checkPlayer(player);

            // Add +1 vote to player.
            addVote(player, info);

            // Get how many time player has voted.
            int voted = _storedData.Players[info.id].voted;

            // Take closest number from rewardNumbers
            int? closest = (int?)numberMax.Aggregate((x, y) => Math.Abs(x - voted) < Math.Abs(y - voted)
                    ? (x > voted ? y : x)
                    : (y > voted ? x : y));

            if (closest > voted)
            {
                _Debug(player, $"Closest ({closest}) number was bigger then voted number ({voted})");
                _Debug(player, $"Closest ({closest}) is now 0!");
                closest = 0;
            }

            _Debug(player, $"Reward Number: {closest} Voted: {voted}");

            // and here the magic happens.
            foreach (KeyValuePair<string, List<string>> kvp in _config.Reward)
            {
                if (closest != 0)
                {
                    // Loop for all rewards.
                    if (kvp.Key.ToString() == $"vote")
                    {
                        _Debug(player, "Founded 'vote' data in config!");
                    }

                    if (kvp.Key.ToString() == $"vote{closest}")
                    {
                        Chat(player, $"{Lang("ThankYou", player.UserIDString, voted)}");
                        foreach (string reward in kvp.Value)
                        {
                            // Split reward to variable and value.
                            string[] valueSplit = reward.Split(':');
                            string variable = valueSplit[0];
                            string value = valueSplit[1].Replace(" ", "");

                            // Checking variables and run console command.
                            // If variable not found, then try give item.
                            if (_config.Variables.ContainsKey(variable))
                            {
                                _Debug(player, $"{getCmdLine(player, variable, value)}");
                                rust.RunServerCommand(getCmdLine(player, variable, value));

                                if (!value.Contains("-"))
                                    Chat(player, $"{Lang(variable, player.UserIDString, value)}");
                                else
                                {
                                    string[] _value = value.Split('-');
                                    Chat(player, $"{Lang(variable, player.UserIDString, _value[0], _value[1])}");
                                }

                                _Debug(player, $"Ran command {String.Format(variable, value)}");
                                continue;
                            }
                            else
                            {
                                try
                                {
                                    Item itemToReceive = ItemManager.CreateByName(variable, ToInt32(value));
                                    _Debug(player, $"Received item {itemToReceive.info.displayName.translated} {value}");
                                    //If the item does not end up in the inventory
                                    //Drop it on the ground for them
                                    if (!player.inventory.GiveItem(itemToReceive, player.inventory.containerMain))
                                        itemToReceive.Drop(player.GetDropPosition(), player.GetDropVelocity());

                                    Chat(player, $"{Lang("Received", player.UserIDString, value, itemToReceive.info.displayName.translated)}");
                                }
                                catch (Exception e) { PrintWarning($"{e}"); }
                            }
                        }
                    }
                }
            }
            if (_config.Settings["GlobalClaimAnnouncment"]?.ToLower() == "true")
                PrintToChat($"{Lang("GlobalClaimAnnouncment", player.UserIDString, player.displayName, voted)}");
        }
        private string getCmdLine(BasePlayer player, string str, string value)
        {
            var output = String.Empty;
            string playerid = player.UserIDString;
            string playername = player.displayName;

            // Checking if value contains => -
            if (!value.Contains('-'))
                output = _config.Variables[str].ToString()
                    .Replace("{playerid}", playerid)
                    .Replace("{playername}", '"' + playername + '"')
                    .Replace("{value}", value);
            else
            {
                string[] splitValue = value.Split('-');
                output = _config.Variables[str].ToString()
                    .Replace("{playerid}", playerid)
                    .Replace("{playername}", '"' + playername + '"')
                    .Replace("{value}", splitValue[0])
                    .Replace("{value2}", splitValue[1]);
            }
            return $"{output}";
        }
        #endregion

        #region Configuration Defaults
        PluginConfig DefaultConfig()
        {
            var defaultConfig = new PluginConfig
            {
                Settings = new Dictionary<string, string>
                {
                    { PluginSettings.Prefix, "<color=cyan>[EasyVote]</color>" },
                    { PluginSettings.Announcment, "true" },
                    { PluginSettings.GlobalClaimAnnouncment, "true" },
                    { PluginSettings.HighestVoter, "false" },
                    { PluginSettings.HighestVoterInterval, "10" },
                    { PluginSettings.HighestVoterRewardGroup, "hero" },
                },
                VoteSettings = new Dictionary<string, string>
                {
                    { PluginVoteSettings.RustServersID, "" },
                    { PluginVoteSettings.RustServersKEY, "" },
                    { PluginVoteSettings.TopRustServersID, "" },
                    { PluginVoteSettings.TopRustServersKEY, "" },
                    { PluginVoteSettings.BeancanID, "" },
                    { PluginVoteSettings.BeancanKEY, "" },
                    { PluginVoteSettings.TopServeursID, "" },
                    { PluginVoteSettings.TopServeursKEY, "" }
                },
                Reward = new Dictionary<string, List<string>>
                {
                    { "vote1", new List<string>() { "supply.signal: 1" } },
                    { "vote3", new List<string>() { "supply.signal: 1", "money: 250" } },
                    { "vote6", new List<string>() { "supply.signal: 1", "money: 500" } }
                },
                Variables = new Dictionary<string, string>
                {
                    ["money"] = "eco.c deposit {playerid} {value}",
                    ["rp"] = "sr add {playerid} {value}",
                    ["addgroup"] = "addgroup {playerid} {value} {value2}",
                    ["grantperm"] = "grantperm {playerid} {value} {value2}",
                    ["zlvl-wc"] = "zlvl {playername} WC +{value}",
                    ["zlvl-mg"] = "zlvl {playername} MG +{value}",
                    ["zlvl-s"] = "zlvl {playername} S +{value}"
                }
            };
            return defaultConfig;
        }
        #endregion

        #region Configuration Setup
        private bool configChanged;
        private PluginConfig _config;

        protected override void LoadDefaultConfig() => Config.WriteObject(DefaultConfig(), true);

        class PluginSettings
        {
            public const string Prefix = "Prefix";
            public const string Announcment = "Announcment";
            public const string GlobalClaimAnnouncment = "GlobalClaimAnnouncment";
            public const string HighestVoterInterval = "HighestVoterInterval";
            public const string HighestVoter = "HighestVoter";
            public const string HighestVoterRewardGroup = "HighestVoterRewardGroup";
        }
        class PluginVoteSettings
        {
            public const string RustServersID = "RustServersID";
            public const string RustServersKEY = "RustServersKEY";
            public const string TopRustServersID = "TopRustServersID";
            public const string TopRustServersKEY = "TopRustServersKEY";
            public const string BeancanID = "BeancanID";
            public const string BeancanKEY = "BeancanKEY";
            public const string TopServeursID = "TopServeursID";
            public const string TopServeursKEY = "TopServeursKEY";
        }
        class PluginConfig
        {
            public Dictionary<string, string> Settings { get; set; }
            public Dictionary<string, string> VoteSettings { get; set; }
            public Dictionary<string, List<string>> Reward { get; set; }
            public Dictionary<string, string> Variables { get; set; }
        }
        void LoadConfigValues()
        {
            _config = Config.ReadObject<PluginConfig>();
            var defaultConfig = DefaultConfig();
            Merge(_config.Settings, defaultConfig.Settings);
            Merge(_config.VoteSettings, defaultConfig.VoteSettings);
            Merge(_config.Reward, defaultConfig.Reward, true);
            Merge(_config.Variables, defaultConfig.Variables);

            if (!configChanged) return;
            PrintWarning("Configuration file updated!");
            Config.WriteObject(_config);
        }
        void Merge<T1, T2>(IDictionary<T1, T2> current, IDictionary<T1, T2> defaultDict, bool rewardFilter = false)
        {
            foreach (var pair in defaultDict)
            {
                if (rewardFilter) continue;
                if (current.ContainsKey(pair.Key)) continue;
                current[pair.Key] = pair.Value;
                configChanged = true;
            }
            var oldPairs = defaultDict.Keys.Except(current.Keys).ToList();
            foreach (var oldPair in oldPairs)
            {
                if (rewardFilter) continue;
                configChanged = true;
            }
        }
        #endregion

        #region Webrequests
        void ClaimReward(int code, string response, BasePlayer player, string url)
        {
            _Debug(player, $"Code: {code}, Response: {response}");

            if (code != 200)
            {
                PrintWarning("Error: {0} - Couldn't get an answer for {1} ({2})", code, player.displayName, url);
                Chat(player, $"{Lang("ClaimError", player.UserIDString, code, url)}");
                return;
            }

            if (response?.ToString() == "1")
            {
                RewardHandler(player);
                Voted = true;
                return;
            }

            NoRewards = true;
        }
        void CheckStatus(int code, string response, BasePlayer player)
        {
            _Debug(player, $"Code: {code}, Response: {response}");

            if (response?.ToString() == "1" && code == 200)
                Chat(player, Lang("RemeberClaim", player.UserIDString));
        }
        #endregion

        #region Storing
        class StoredData
        {
            public Dictionary<string, PlayerData> Players = new Dictionary<string, PlayerData>();
            public int month = DateTime.Now.Month;
            public ulong highestVoter = 0;
            public int announcemented = 0;
            public StoredData() { }

            public void AddHighestVoter(ulong steamID = 0)
            {
                int steamIDs;
                if (!int.TryParse(steamID.ToString(), out steamIDs))
                {
                    highestVoter = ToUInt64(steamID);
                    return;
                }

                highestVoter = ToUInt64(steamIDs);
            }
            public void setAnnouncemented(int val)
            {
                announcemented = val;
            }
        }
        class PlayerData
        {
            public string id;
            public int voted;

            public PlayerData() { }

            public PlayerData(BasePlayer player)
            {
                id = player.UserIDString;
                voted = 0;
            }
            public void AddVote(int numbr)
            {
                voted = numbr;
            }
        }
        StoredData _storedData;
        #endregion

        #region Other
        #region Builder
        private void BuildNumberMax()
        {
            foreach (KeyValuePair<string, List<string>> kvp in _config.Reward)
            {
                int rewardNumber;
                // Remove alphabetic and leave only number.
                if (!int.TryParse(kvp.Key.Replace("vote", ""), out rewardNumber))
                {
                    Puts($"Invalid vote config format \"{kvp.Key}\"");
                    continue;
                }
                numberMax.Add(rewardNumber);
            }
        }
        #endregion
        #region Helper
        public void Chat(BasePlayer player, string str) => SendReply(player, $"{_config.Settings["Prefix"]} " + str);
        public void _Debug(BasePlayer player, string msg)
        {
            if (DEBUG)
                Puts($"[Debug] {player.displayName} - {msg}");
        }
        private void NextMonth()
        {
            if (Cooldown > DateTime.Now)
                return;
            else
                Cooldown = DateTime.Now.AddMinutes(1);

            // If it's a new month wipe the saved votes
            if (_storedData.month != DateTime.Now.Month)
            {
                PrintWarning("New month detected. Wiping user votes");
                Interface.GetMod().DataFileSystem.WriteObject("EasyVote.bac", _storedData); // Save backup

                if (_storedData.highestVoter != 0) // Remove latest HighestVoter from the "reward group"
                    delGroup(_storedData.highestVoter.ToString(), _config.Settings["HighestVoterRewardGroup"]);

                ulong op = getHighestVoter(); // Get highest voter then null storedata
                _storedData = new StoredData(); // Set new storedata

                addHighestVoter(op); // Add highest voter
                Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData); // Write wiped data
            }
        }
        private void rewardList(BasePlayer player)
        {
            StringBuilder rewardList = new StringBuilder();
            rewardList.Clear(); // Making sure that rewardList is empty.

            int lineCounter = 0; // Count lines
            int lineSplit = 2; // Value when split reward list.

            foreach (KeyValuePair<string, List<string>> kvp in _config.Reward)
            {
                // If lineCounter is less then lineSplit.
                if (lineCounter <= lineSplit)
                {
                    int voteNumber;
                    if (!int.TryParse(kvp.Key.Replace("vote", ""), out voteNumber))
                    {
                        PrintWarning($"Invalid vote config format \"{kvp.Key}\"");
                        continue;
                    }
                    rewardList.Append(Lang("RewardList", null, voteNumber)).AppendLine();

                    var valueList = String.Join(Environment.NewLine, kvp.Value.ToArray());
                    rewardList.Append(valueList).AppendLine();
                    lineCounter++;
                }
                // If higher, then send rewardList to player and empty it.
                else
                {
                    SendReply(player, rewardList.ToString());
                    rewardList.Clear();
                    lineCounter = 0;

                    int voteNumber;
                    if (!int.TryParse(kvp.Key.Replace("vote", ""), out voteNumber))
                    {
                        PrintWarning($"Invalid vote config format \"{kvp.Key}\"");
                        continue;
                    }

                    rewardList.Append(Lang("RewardList", null, voteNumber)).AppendLine();
                    var valueList = String.Join(Environment.NewLine, kvp.Value.ToArray());
                    rewardList.Append(valueList).AppendLine();
                }
            }

            // This section is for making sure all rewards will be displayed.
            SendReply(player, rewardList.ToString());
            rewardList.Clear();
            lineCounter = 0;
        }
        public int getCountRewards()
        {
            foreach (KeyValuePair<string, List<string>> kvp in _config.Reward) return kvp.Key.Count();
            return 0;
        }
        public bool IsEmpty(string s)
        {
            if (s != String.Empty) return true;
            return false;
        }
        public bool hasPermission(BasePlayer player, string perm)
        {
            if (player.IsAdmin()) return true;
            if (permission.UserHasPermission(player.UserIDString, perm)) return true;
            return false;
        }
        public bool isGroup(string id, string group)
        {
            if (permission.GetUserGroups(id).Contains(group)) return true;
            return false;
        }
        public void setGroup(string id, string group)
        {
            if (permission.GroupExists(group))
                permission.AddUserGroup(id, group);
            else
                PrintWarning($"Cant set \"{group}\" group to the player (ID: {id}). Make sure that you write group name right!");
        }
        public void delGroup(string id, string group)
        {
            if (permission.GroupExists(group))
                permission.RemoveUserGroup(id, group);
            else
                PrintWarning($"Cant delete \"{group}\" group to the player (ID: {id}). Make sure that you write group name right!");
        }
        #endregion
        #region Storing Helper
        void checkPlayer(BasePlayer player)
        {
            var info = new PlayerData(player);
            if (!_storedData.Players.ContainsKey(info.id))
            {
                _storedData.Players.Add(info.id, info);
                Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);
            }
        }
        void addVote(BasePlayer player, PlayerData info)
        {
            if (_storedData.Players.ContainsKey(info.id))
            {
                int voted = _storedData.Players[info.id].voted;
                _storedData.Players[info.id].AddVote(voted + 1);
                Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);
            }
        }
        ulong getHighestVoter()
        {
            // Checking that HighestVoter is true
            // Null checking
            if (_config.Settings["HighestVoter"]?.ToLower() != "true"
                || _storedData.Players?.ToList().Count() == 0)
                return ToUInt64(0);

            // Making new list
            Dictionary<string, int> players = new Dictionary<string, int>();

            // Adding data (id, voted) to players list
            foreach (var kvp in _storedData.Players.ToList())
                players.Add(kvp.Key, kvp.Value.voted);

            // Take highest voted player id
            var max = players.Aggregate((l, r) => l.Value > r.Value ? l : r);
            if (DEBUG) Puts($"[Debug] {ToUInt64(max.Key)} : {max.Value}");

            return ToUInt64(max.Key);
        }
        void addHighestVoter(ulong steamID)
        {
            if (steamID != 0)
            {
                _storedData.AddHighestVoter(steamID);
                Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);
            }
        }
        #endregion
        #endregion

        #region API
        // I just leave this comment here.

        //[HookMethod("AddPoints")]
        //public object AddPoints(object userID, int amount)
        //{ }
        #endregion
    }
}
