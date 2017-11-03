// Make sure that everything works (like multiple server voting)
// Check all todos
// Double test everything
using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using System.Text;
using Oxide.Core.Libraries;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("EasyVote", "Exel80", "2.0.1", ResourceId = 2102)]
    [Description("Simple and smooth voting start by activating one scirpt.")]
    class EasyVote : RustPlugin
    {
        [PluginReference] private Plugin DiscordMessages;

        #region Initializing
        // Permissions
        private const bool DEBUG = false;
        private const string permUse = "EasyVote.Use";
        private const string permAdmin = "EasyVote.Admin";

        // Spam protect list
        Dictionary<ulong, bool> claimCooldown = new Dictionary<ulong, bool>();
        Dictionary<ulong, bool> checkCooldown = new Dictionary<ulong, bool>();

        // List received reward(s) one big list.
        StringBuilder rewardsString = new StringBuilder();

        // Global bools
        private bool NoReward = true;

        // List all vote sites.
        List<string> availableAPISites = new List<string>();
        StringBuilder _voteList = new StringBuilder();
        StringBuilder helpYou = new StringBuilder();
        private List<int> numberMax = new List<int>();

        void Loaded()
        {
            // Load storedata
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("EasyVote");

            // Check rewards and add them one big list
            BuildNumberMax();

            // Build helptext
            HelpText();
        }

        void Init()
        {
            // Load configs
            LoadConfigValues();
            LoadDefaultMessages();

            // Check available vote sites
            checkVoteSites();

            // Build StringBuilders
            voteList();

            // Regitering permissions
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permAdmin, this);
        }
        #endregion

        #region Localization
        string _lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ClaimError"] = "Something went wrong! Player <color=red>{0} got an error</color> from <color=yellow>{1}</color>. Please try again later!",
                ["ClaimReward"] = "You just received your vote reward(s). Enjoy!",
                ["ClaimPleaseWait"] = "Checking the voting websites. Please wait...",
                ["VoteList"] = "You have voted <color=yellow>{1}</color> time(s)!\n Leave another vote on these websites:\n{0}",
                ["EarnReward"] = "When you have voted, type <color=yellow>/claim</color> to claim your reward(s)!",
                ["RewardListFirstTime"] = "<color=cyan>Reward for voting for the first time.</color>",
                ["RewardListEverytime"] = "<color=cyan>Reward, which player will receive everytime they vote.</color>",
                ["RewardList"] = "<color=cyan>Reward for voting</color> <color=orange>{0}</color> <color=cyan>time(s).</color>",
                ["Received"] = "You have received {0}x {1}",
                ["ThankYou"] = "Thank you for voting! You have voted <color=yellow>{0}</color> time(s) Here is your reward for..\n{1}",
                ["NoRewards"] = "You do not have any new rewards available\n Please type <color=yellow>/vote</color> and go to one of the websites to vote and receive your reward",
                ["RemeberClaim"] = "You haven't claimed your reward from voting for the server yet! Use <color=yellow>/claim</color> to claim your reward!\n You have to claim your reward within <color=yellow>24h</color>! Otherwise it will be gone!",
                ["GlobalChatAnnouncments"] = "<color=yellow>{0}</color><color=cyan> has voted </color><color=yellow>{1}</color><color=cyan> time(s) and just received their rewards. Find out where you can vote by typing</color><color=yellow> /vote</color>\n<color=cyan>To see a list of avaliable rewards type</color><color=yellow> /reward list</color>",
                ["money"] = "<color=yellow>{0}$</color> has been desposited into your account",
                ["rp"] = "You have gained <color=yellow>{0}</color> reward points",
                ["tempaddgroup"] = "You have been temporality added to <color=yellow>{0}</color> group (Expire in {1})",
                ["tempgrantperm"] = "You have temporality granted <color=yellow>{0}</color> permission (Expire in {1})",
                ["zlvl-wc"] = "You have gained <color=yellow>{0}</color> woodcrafting level(s)",
                ["zlvl-m"] = "You have gained <color=yellow>{0}</color> mining level(s)",
                ["zlvl-s"] = "You have gained <color=yellow>{0}</color> skinning level(s)",
                ["zlvl-c"] = "You have gained <color=yellow>{0}</color> crafting level(s)",
                ["zlvl-*"] = "You have gained <color=yellow>{0}</color> in all level(s)",
                ["oxidegrantperm"] = "You have been granted <color=yellow>{0}</color> permission",
                ["oxiderevokeperm"] = "Your permission <color=yellow>{0}</color> has been revoked",
                ["oxidegrantgroup"] = "You have been added to <color=yellow>{0}</color> group",
                ["oxiderevokegroup"] = "You have been removed from <color=yellow>{0}</color> group"
            }, this);
        }
        #endregion

        #region Hooks
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            // If (for some reason) player is stuck in claimCooldown list.
            if (claimCooldown.ContainsKey(player.userID))
                claimCooldown.Remove(player.userID);
        }

        private void SendHelpText(BasePlayer player)
        {
            if (hasPermission(player, permUse))
                player.ChatMessage(helpYou.ToString());
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!hasPermission(player, permUse))
                return;

            // Check if player exist in cooldown list or not
            if (!checkCooldown.ContainsKey(player.userID))
                checkCooldown.Add(player.userID, false);
            else if (checkCooldown.ContainsKey(player.userID))
                return;

            var timeout = 5500f; // Timeout (in milliseconds)

            foreach (var site in availableAPISites.ToList())
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> kvp in _config.Servers)
                {
                    foreach (KeyValuePair<string, string> vp in kvp.Value)
                    {
                        if (vp.Key != site)
                            continue;

                        string[] idKeySplit = vp.Value.Split(':');
                        foreach (KeyValuePair<string, string> SitesApi in _config.VoteSitesAPI[site])
                        {
                            if (SitesApi.Key == PluginSettings.apiClaim)
                            {
                                // Formating api claim => {0} = Key & {1} Id
                                // Example: "http://rust-servers.net/api/?object=votes&element=claim&key= {0} &steamid= {1} ",
                                string _format = String.Format(SitesApi.Value, idKeySplit[1], player.userID);

                                // Send GET request to voteAPI site.
                                webrequest.Enqueue(_format, null, (code, response) => CheckStatus(code, response, player), this, RequestMethod.GET, null, timeout);

                                _Debug($"GET: {_format} =>\n Site: {site} Server: {kvp.Key} Id: {idKeySplit[0]}");
                            }
                        }
                    }
                }
            }
            // Wait 3.69 sec before execute this command.
            // Because need make sure that plugin webrequest all api sites.
            timer.Once(3.69f, () =>
            {
                if (checkCooldown[player.userID])
                {
                    Chat(player, $"{_lang("RemeberClaim", player.UserIDString)}");
                }

                // Remove player from cooldown list
                checkCooldown.Remove(player.userID);
            });
        }
        #endregion

        #region Commands
        [ChatCommand("vote")]
        void cmdVote(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, permUse))
                return;

            // Check how many time player has voted.
            int voted = 0;
            if (_storedData.Players.ContainsKey(player.UserIDString))
                voted = _storedData.Players[player.UserIDString].voted;

            Chat(player, _lang("VoteList", player.UserIDString, _voteList.ToString(), voted));
            Chat(player, _lang("EarnReward", player.UserIDString));
        }

        [ChatCommand("voteadmin")]
        void cmdVoteAdmin(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, permAdmin))
                return;

            // TODO: Add admin commands
            // addreward <reward name (only number)> <commands in ">
            // removereward <reward name (only number)>
            // editreward <reward name (only number)> => Show voteX rewards => /voteadmin +"asdasdasd" -"asdasd"
            // showcmds 
            // testapi <VoteSitesAPI (name)> <id> <key>
            // testreward <reward name (only number)>
        }

        [ChatCommand("claim")]
        void cmdClaim(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, permUse))
                return;

            // Check if player exist in cooldown list or not
            if (!claimCooldown.ContainsKey(player.userID))
                claimCooldown.Add(player.userID, false);
            else if (claimCooldown.ContainsKey(player.userID))
                return;

            var timeout = 5500f; // Timeout (in milliseconds)
            Chat(player, _lang("ClaimPleaseWait", player.UserIDString));

            foreach (var site in availableAPISites.ToList())
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> kvp in _config.Servers)
                {
                    foreach (KeyValuePair<string, string> vp in kvp.Value)
                    {
                        if (vp.Key != site)
                            continue;

                        string[] idKeySplit = vp.Value.Split(':');
                        foreach (KeyValuePair<string, string> SitesApi in _config.VoteSitesAPI[site])
                        {
                            if (SitesApi.Key == PluginSettings.apiClaim)
                            {
                                // Formating api claim => {0} = Key & {1} Id
                                // Example: "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key= {0} &steamid= {1} ",
                                string _format = String.Format(SitesApi.Value, idKeySplit[1], player.userID);

                                // Send GET request to voteAPI site.
                                webrequest.Enqueue(_format, null, (code, response) => ClaimReward(code, response, player, site, kvp.Key), this, RequestMethod.GET, null, timeout);

                                _Debug($"GET: {_format} =>\n Site: {site} Server: {kvp.Key} Id: {idKeySplit[0]}");
                            }
                        }
                    }
                }
            }

            // Wait 5.55 sec before execute this command.
            // Because need make sure that plugin webrequest all api sites.
            timer.Once(5.55f, () =>
            {
                if (!claimCooldown[player.userID])
                {
                    Chat(player, $"{_lang("NoRewards", player.UserIDString)}");
                }

                // Remove player from cooldown list
                claimCooldown.Remove(player.userID);
            });
        }

        [ChatCommand("reward")]
        void cmdReward(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, permUse))
                return;

            if (args?.Length > 1)
                return;

            if (args[0] == "list")
                rewardList(player);
        }
        #endregion

        #region Reward Handler
        private void RewardHandler(BasePlayer player, string serverName = null)
        {
            // Check that player is in "database".
            var playerData = new PlayerData();
            if (!_storedData.Players.ContainsKey(player.UserIDString))
            {
                _storedData.Players.Add(player.UserIDString, playerData);
                _storedData.Players[player.UserIDString].lastTime_Voted = DateTime.UtcNow;
                Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);
            }

            // Add +1 vote to player.
            _storedData.Players[player.UserIDString].voted++;
            _storedData.Players[player.UserIDString].lastTime_Voted = DateTime.UtcNow;
            Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);

            // Get how many time player has voted.
            int voted = _storedData.Players[player.UserIDString].voted;

            // Take closest number from rewardNumbers
            int? closest = null;
            try
            {
                closest = (int?)numberMax.Aggregate((x, y) => Math.Abs(x - voted) < Math.Abs(y - voted)
                        ? (x > voted ? y : x)
                        : (y > voted ? x : y));
            }
            catch (InvalidOperationException error) { PrintError($"Player {player.displayName} tried to claim a reward but this happened ...\n{error.ToString()}"); return; }

            if (closest > voted)
            {
                _Debug($"Closest ({closest}) number was bigger then voted number ({voted})");
                _Debug($"Closest ({closest}) is now 0!");
                closest = 0;
            }

            _Debug($"Reward Number: {closest} Voted: {voted}");

            // and here the magic happens. Loop for all rewards.
            foreach (KeyValuePair<string, List<string>> kvp in _config.Rewards)
            {
                // If first time voted
                if (kvp.Key.ToLower() == "first")
                {
                    if (voted > 1)
                        continue;

                    _Debug("Founded 'first' in config!");
                    GaveRewards(player, kvp.Value);
                    continue;
                }

                // Gave this reward everytime
                if (kvp.Key == "@")
                {
                    _Debug("Founded 'repeat reward' in config!");

                    GaveRewards(player, kvp.Value);
                    continue;
                }

                // Cumlative reward
                if (_config.Settings[PluginSettings.RewardIsCumulative].ToLower() == "true")
                {
                    if (kvp.Key.ToString().Contains("vote"))
                    {
                        // Tryparse vote number
                        int voteNumber;
                        if (!int.TryParse(kvp.Key.Replace("vote", ""), out voteNumber))
                            continue;

                        // All reward has now claimed
                        if (voteNumber > closest)
                            continue;

                        _Debug($" -> About to gave {kvp.Key} rewards");
                        GaveRewards(player, kvp.Value);
                    }
                    continue;
                }

                // Got closest vote
                if (kvp.Key.ToString() == $"vote{closest}")
                {
                    GaveRewards(player, kvp.Value);
                }

            }
            if (_config.Settings[PluginSettings.GlobalChatAnnouncments]?.ToLower() == "true")
                PrintToChat($"{_lang("GlobalChatAnnouncments", player.UserIDString, player.displayName, voted)}");

            // Send message to discord text channel.
            if (_config.Discord[PluginSettings.DiscordEnabled].ToLower() == "true")
            {
                List<Fields> fields = new List<Fields>();
                string json;

                fields.Add(new Fields("Voter", $"[{player.displayName}](https://steamcommunity.com/profiles/{player.userID})", true));
                fields.Add(new Fields("Voted", voted.ToString(), true));
                fields.Add(new Fields("Server", (serverName != null ? serverName : "[ UNKNOWN ]"), true));
                fields.Add(new Fields("Reward(s)", CleanHTML(rewardsString.ToString()), false));

                json = JsonConvert.SerializeObject(fields);
                DiscordMessages?.Call("API_SendFancyMessage", _config.Discord[PluginSettings.WebhookURL], _config.Discord[PluginSettings.Title], 0, json);

                // Send @here in channel, if alert is true.
                if (_config.Discord[PluginSettings.Alert].ToLower() == "true")
                    DiscordMessages?.Call("API_SendTextMessage", _config.Discord[PluginSettings.WebhookURL], "@here");
            }

            // Make sure that player has voted etc.
            if (rewardsString.Length > 1)
            {
                // Hookmethod: void onUserReceiveReward(BasePlayer player, int voted)
                Interface.CallHook("onUserReceiveReward", player, voted);

                // Send ThankYou to player
                Chat(player, $"{_lang("ThankYou", player.UserIDString, voted, rewardsString.ToString())}");
                rewardsString.Clear();
            }
        }

        /// <summary>
        /// Gave all rewards from List<string>
        /// </summary>
        /// <param name="player"></param>
        /// <param name="rewardValue"></param>
        private void GaveRewards(BasePlayer player, List<string> rewardValue)
        {
            foreach (string reward in rewardValue)
            {
                // Split reward to variable and value.
                string[] valueSplit = reward.Split(':');
                string commmand = valueSplit[0];
                string value = valueSplit[1].Replace(" ", "");

                // Checking variables and run console command.
                // If variable not found, then try give item.
                if (_config.Commands.ContainsKey(commmand))
                {
                    _Debug($"{getCmdLine(player, commmand, value)}");
                    rust.RunServerCommand(getCmdLine(player, commmand, value));

                    if (!value.Contains("-"))
                        rewardsString.Append($"- {_lang(commmand, player.UserIDString, value)}").AppendLine();
                    else
                    {
                        string[] _value = value.Split('-');
                        rewardsString.Append($"- {_lang(commmand, player.UserIDString, _value[0], _value[1])}").AppendLine();
                    }

                    _Debug($"Ran command {String.Format(commmand, value)}");
                    continue;
                }
                else
                {
                    try
                    {
                        Item itemToReceive = ItemManager.CreateByName(commmand, Convert.ToInt32(value));
                        _Debug($"Received item {itemToReceive.info.displayName.translated} {value}");
                        //If the item does not end up in the inventory
                        //Drop it on the ground for them
                        if (!player.inventory.GiveItem(itemToReceive, player.inventory.containerMain))
                            itemToReceive.Drop(player.GetDropPosition(), player.GetDropVelocity());

                        rewardsString.Append($"- {_lang("Received", player.UserIDString, value, itemToReceive.info.displayName.translated)}").AppendLine();
                    }
                    catch (Exception e) { PrintWarning($"{e}"); }
                }
            }
        }

        private string getCmdLine(BasePlayer player, string str, string value)
        {
            var output = String.Empty;
            string playerid = player.UserIDString;
            string playername = player.displayName;

            // Checking if value contains => -
            if (!value.Contains('-'))
                output = _config.Commands[str].ToString()
                    .Replace("{playerid}", playerid)
                    .Replace("{playername}", '"' + playername + '"')
                    .Replace("{value}", value);
            else
            {
                string[] splitValue = value.Split('-');
                output = _config.Commands[str].ToString()
                    .Replace("{playerid}", playerid)
                    .Replace("{playername}", '"' + playername + '"')
                    .Replace("{value}", splitValue[0])
                    .Replace("{value2}", splitValue[1]);
            }
            return $"{output}";
        }
        #endregion

        #region Storing
        class StoredData
        {
            public Dictionary<string, PlayerData> Players = new Dictionary<string, PlayerData>();
            public StoredData() { }
        }
        class PlayerData
        {
            public int voted;
            public DateTime lastTime_Voted;

            public PlayerData()
            {
                voted = 0;
                lastTime_Voted = DateTime.UtcNow;
            }
        }
        StoredData _storedData;
        #endregion

        #region Webrequests
        void ClaimReward(int code, string response, BasePlayer player, string url, string serverName = null)
        {
            _Debug($"Code: {code}, Response: {response}");

            if (code != 200)
            {
                PrintWarning("Error: {0} - Couldn't get an answer for {1} ({2})", code, player.displayName, url);
                Chat(player, $"{_lang("ClaimError", player.UserIDString, code, url)}");
                return;
            }

            switch (response)
            {
                case "1":
                    {
                        RewardHandler(player, serverName);
                        if (claimCooldown.ContainsKey(player.userID))
                            claimCooldown[player.userID] = true;
                    }
                    break;
            }
        }

        void CheckStatus(int code, string response, BasePlayer player)
        {
            _Debug($"Code: {code}, Response: {response}");

            if (response?.ToString() == "1" && code == 200)
            {
                if (!checkCooldown.ContainsKey(player.userID))
                {
                    checkCooldown.Add(player.userID, true);
                }
                checkCooldown[player.userID] = true;
            }
        }
        #endregion

        #region Configuration

        #region Configuration Defaults
        PluginConfig DefaultConfig()
        {
            var defaultConfig = new PluginConfig
            {
                Settings = new Dictionary<string, string>
                {
                    { PluginSettings.Prefix, "<color=cyan>[EasyVote]</color>" },
                    { PluginSettings.RewardIsCumulative, "false" },
                    { PluginSettings.GlobalChatAnnouncments, "true" },
                    { PluginSettings.LocalChatAnnouncments, "true" }
                },
                Discord = new Dictionary<string, string>
                {
                    { PluginSettings.DiscordEnabled, "false" },
                    { PluginSettings.Alert, "false" },
                    { PluginSettings.Title, "Vote" },
                    { PluginSettings.WebhookURL, "" }
                },
                Servers = new Dictionary<string, Dictionary<string, string>>
                {
                    { "ServerName1", new Dictionary<string, string>() { { "Beancan", "ID:KEY" }, { "RustServers", "ID:KEY" } } },
                    { "ServerName2", new Dictionary<string, string>() { { "Beancan", "ID:KEY" } } }
                },
                VoteSitesAPI = new Dictionary<string, Dictionary<string, string>>
                {
                    { "RustServers",
                       new Dictionary<string, string>() {
                           { PluginSettings.apiClaim, "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key={0}&steamid={1}" },
                           { PluginSettings.apiStatus, "http://rust-servers.net/api/?object=votes&element=claim&key={0}&steamid={1}" },
                           { PluginSettings.apiLink, "http://rust-servers.net/server/{0}" }
                       }
                    },
                    { "Beancan",
                       new Dictionary<string, string>() {
                           { PluginSettings.apiClaim, "http://beancan.io/vote/put/{0}/{1}" },
                           { PluginSettings.apiStatus, "http://beancan.io/vote/get/{0}/{1}" },
                           { PluginSettings.apiLink, "http://beancan.io/server/{0}" }
                       }
                    },
                },
                Rewards = new Dictionary<string, List<string>>
                {
                    { "first", new List<string>() { "oxidegrantperm: kits.starterkit" } },
                    { "@", new List<string>() { "supply.signal: 1", "zlvl-*: 1" } },
                    { "vote3", new List<string>() { "oxidegrantgroup: member" } },
                    { "vote6", new List<string>() { "money: 500", "tempaddgroup: vip-1d1h1m" } },
                    { "vote10", new List<string>() { "money: 1000", "rp: 50", "tempgrantperm: fauxadmin.allowed-5m" } }
                },
                Commands = new Dictionary<string, string>
                {
                    ["money"] = "eco.c deposit {playerid} {value}",
                    ["rp"] = "sr add {playerid} {value}",
                    ["oxidegrantperm"] = "oxide.grant user {playerid} {value}",
                    ["oxiderevokeperm"] = "oxide.revoke user {playerid} {value}",
                    ["oxidegrantgroup"] = "oxide.usergroup add {playerid} {value}",
                    ["oxiderevokegroup"] = "oxide.usergroup remove {playerid} {value}",
                    ["tempaddgroup"] = "addgroup {playerid} {value} {value2}",
                    ["tempgrantperm"] = "grantperm {playerid} {value} {value2}",
                    ["zlvl-c"] = "zl.lvl {playername} C +{value}",
                    ["zlvl-wc"] = "zl.lvl {playername} WC +{value}",
                    ["zlvl-m"] = "zl.lvl {playername} M +{value}",
                    ["zlvl-s"] = "zl.lvl {playername} S +{value}",
                    ["zlvl-*"] = "zl.lvl {playername} * +{value}",
                }
            };
            return defaultConfig;
        }
        #endregion

        private bool configChanged;
        private PluginConfig _config;

        protected override void LoadDefaultConfig() => Config.WriteObject(DefaultConfig(), true);

        class PluginSettings
        {
            public const string apiClaim = "API Claim Reward (GET URL)";
            public const string apiStatus = "API Vote status (GET URL)";
            public const string apiLink = "Vote link (URL)";
            public const string Title = "Title";
            public const string WebhookURL = "Discord webhook (URL)";
            public const string DiscordEnabled = "DiscordMessage Enabled (true / false)";
            public const string Alert = "Enable @here alert (true / false)";
            public const string Prefix = "Prefix";
            public const string RewardIsCumulative = "Vote rewards cumulative (true / false)";
            public const string GlobalChatAnnouncments = "Globally announcment in chat when player voted (true / false)";
            public const string LocalChatAnnouncments = "Send thank you message to player who voted (true / false)";
        }
        class PluginConfig
        {
            public Dictionary<string, string> Settings { get; set; }
            public Dictionary<string, string> Discord { get; set; }
            public Dictionary<string, Dictionary<string, string>> Servers { get; set; }
            public Dictionary<string, Dictionary<string, string>> VoteSitesAPI { get; set; }
            public Dictionary<string, List<string>> Rewards { get; set; }
            public Dictionary<string, string> Commands { get; set; }
        }
        void LoadConfigValues()
        {
            _config = Config.ReadObject<PluginConfig>();
            var defaultConfig = DefaultConfig();
            Merge(_config.Settings, defaultConfig.Settings);
            Merge(_config.Discord, defaultConfig.Discord);
            Merge(_config.Servers, defaultConfig.Servers, true);
            Merge(_config.VoteSitesAPI, defaultConfig.VoteSitesAPI, true);
            Merge(_config.Rewards, defaultConfig.Rewards, true);
            Merge(_config.Commands, defaultConfig.Commands, true);

            if (!configChanged) return;
            PrintWarning("Configuration file(s) updated!");
            Config.WriteObject(_config);
        }
        void Merge<T1, T2>(IDictionary<T1, T2> current, IDictionary<T1, T2> defaultDict, bool bypass = false)
        {
            foreach (var pair in defaultDict)
            {
                if (bypass) continue;
                if (current.ContainsKey(pair.Key)) continue;
                current[pair.Key] = pair.Value;
                configChanged = true;
            }
            var oldPairs = defaultDict.Keys.Except(current.Keys).ToList();
            foreach (var oldPair in oldPairs)
            {
                if (bypass) continue;
                configChanged = true;
            }
        }
        #endregion

        #region Helper 
        public void Chat(BasePlayer player, string str) => SendReply(player, $"{_config.Settings["Prefix"]} " + str);
        public void _Debug(string msg)
        {
            if (DEBUG)
                Puts($"[Debug] {msg}");
        }

        private void HelpText()
        {
            helpYou.Append("<color=cyan>EasyVote Commands ::</color>").AppendLine();
            helpYou.Append("<color=yellow>/vote</color> - Show the voting website(s)").AppendLine();
            helpYou.Append("<color=yellow>/claim</color> - Claim vote reward(s)").AppendLine();
            helpYou.Append("<color=yellow>/reward list</color> - Display all reward(s) what you can get from voting.");
        }

        private string CleanHTML(string input)
        {
            return Regex.Replace(input, @"<(.|\n)*?>", string.Empty);
        }

        private bool hasPermission(BasePlayer player, string perm)
        {
            if (player.IsAdmin) return true;
            if (permission.UserHasPermission(player.UserIDString, perm)) return true;
            return false;
        }

        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }

        private void rewardList(BasePlayer player)
        {
            StringBuilder rewardList = new StringBuilder();

            int lineCounter = 0; // Count lines
            int lineSplit = 2; // Value when split reward list.

            foreach (KeyValuePair<string, List<string>> kvp in _config.Rewards)
            {
                if (kvp.Key.ToLower() == "first")
                {
                    rewardList.Append(_lang("RewardListFirstTime", null)).AppendLine();

                    var valueList = String.Join(Environment.NewLine, kvp.Value.ToArray());
                    rewardList.Append(valueList).AppendLine();
                    lineCounter++;
                }

                if (kvp.Key == "@")
                {
                    rewardList.Append(_lang("RewardListEverytime", null)).AppendLine();

                    var valueList = String.Join(Environment.NewLine, kvp.Value.ToArray());
                    rewardList.Append(valueList).AppendLine();
                    lineCounter++;
                }

                // If lineCounter is less then lineSplit.
                if (lineCounter <= lineSplit)
                {
                    int voteNumber;
                    if (!int.TryParse(kvp.Key.Replace("vote", ""), out voteNumber))
                    {
                        if (!(kvp.Key.ToLower() != "first" || kvp.Key.ToLower() != "@"))
                            PrintWarning($"[RewardHandler] Invalid vote config format \"{kvp.Key}\"");

                        continue;
                    }
                    rewardList.Append(_lang("RewardList", null, voteNumber)).AppendLine();

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
                        if (!(kvp.Key.ToLower() != "first" || kvp.Key.ToLower() != "@"))
                            PrintWarning($"[RewardHandler] Invalid vote config format \"{kvp.Key}\"");

                        continue;
                    }

                    rewardList.Append(_lang("RewardList", null, voteNumber)).AppendLine();
                    var valueList = String.Join(Environment.NewLine, kvp.Value.ToArray());
                    rewardList.Append(valueList).AppendLine();
                }
            }

            // This section is for making sure all rewards will be displayed.
            SendReply(player, rewardList.ToString());
        }

        private void BuildNumberMax()
        {
            foreach (KeyValuePair<string, List<string>> kvp in _config.Rewards)
            {
                if (kvp.Key == "@")
                    continue;
                if (kvp.Key.ToLower() == "first")
                    continue;

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

        private void voteList()
        {
            List<string> temp = new List<string>();

            foreach (var site in availableAPISites.ToList())
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> kvp in _config.Servers)
                {
                    foreach (KeyValuePair<string, string> vp in kvp.Value)
                    {
                        if (vp.Key == site)
                        {
                            string[] idKeySplit = vp.Value.Split(':');
                            foreach (KeyValuePair<string, string> SitesApi in _config.VoteSitesAPI[site])
                            {
                                if (SitesApi.Key == PluginSettings.apiLink)
                                {
                                    _Debug($"Added {String.Format(SitesApi.Value, idKeySplit[0])} to the stringbuilder list.");
                                    temp.Add($"<color=silver>{String.Format(SitesApi.Value, idKeySplit[0])}</color>");
                                }
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < temp.Count; i++)
            {
                _voteList.Append(temp[i]);
                if (i != (temp.Count - 1))
                    _voteList.AppendLine();
            }
        }

        private void checkVoteSites()
        {
            // Double check that VoteSitesAPI isnt null
            if (_config.VoteSitesAPI.Count == 0)
            {
                PrintWarning("VoteSitesAPI is null in oxide/config/EasyVote.json !!!");
                return;
            }

            // Add key names to List<String> availableSites
            foreach (KeyValuePair<string, Dictionary<string, string>> kvp in _config.VoteSitesAPI)
            {
                bool pass = true;
                foreach (KeyValuePair<string, string> vp in kvp.Value)
                {
                    if (string.IsNullOrEmpty(vp.Value))
                    {
                        pass = false;
                        PrintWarning($"In '{kvp.Key}' value '{vp.Key}' is null (oxide/config/EasyVote.json). Disabled: {kvp.Key}");
                        continue;
                    }
                }

                if (pass)
                {
                    _Debug($"Added {kvp.Key} to the \"availableSites\" list");
                    availableAPISites.Add(kvp.Key);
                }
            }
        }
        #endregion

        #region API
        private string getHighestvoter()
        {
            string output = string.Empty;
            Dictionary<string, int> tempList = new Dictionary<string, int>();

            foreach (KeyValuePair<string, PlayerData> item in _storedData.Players)
                tempList.Add(item.Key, item.Value.voted);

            foreach (var item in tempList.OrderByDescending(key => key.Value).Take(1))
                output = item.Key;

            return output;
        }

        private string getLastvoter()
        {
            string output = string.Empty;
            Dictionary<string, DateTime> tempList = new Dictionary<string, DateTime>();

            foreach (KeyValuePair<string, PlayerData> item in _storedData.Players)
                tempList.Add(item.Key, item.Value.lastTime_Voted);

            foreach (var item in tempList.OrderBy(x => x.Value).Take(1))
                output = item.Key;

            return output;
        }

        private void resetData(bool backup = true)
        {
            string currentTime = DateTime.UtcNow.ToString("dd-MM-yyyy");

            // Backup
            if (backup)
                Interface.GetMod().DataFileSystem.WriteObject($"EasyVote-{currentTime}.bac", _storedData);

            // Set new storedata
            _storedData = new StoredData();

            // Write wiped data
            Interface.GetMod().DataFileSystem.WriteObject("EasyVote", _storedData);

            Puts($"Storedata reseted, backup made in oxide/data/EasyVote-{currentTime}.bac");
        }
        #endregion
    }
}
