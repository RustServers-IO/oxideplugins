using System;
using System.Collections.Generic;
using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events.WorldEvents;
using CodeHatch.Networking.Events.WorldEvents.TimeEvents;
using CodeHatch.Networking.Events;
using CodeHatch.Common;
using Oxide.Core.Plugins;
using Oxide.Core;
using CodeHatch.UserInterface.Dialogues;
using System.Threading;

namespace Oxide.Plugins
{
    [Info("Voting System", "D-Kay", "1.4.1", ResourceId = 1190)]
    class VotingSystem : ReignOfKingsPlugin
    {
        #region Variables

        [PluginReference("LevelSystem")]
        Plugin LevelSystem;

        [PluginReference("GrandExchange")]
        Plugin GrandExchange;

        private bool UseYNCommands => GetConfig("UseYNCommands", true);
        private int VoteDuration => GetConfig("VoteDuration", 30);
        private int TimeVoteCooldown => GetConfig("TimeVoteCooldown", 600);
        private int WeatherVoteCooldown => GetConfig("WeatherVoteCooldown", 180);
        private bool UseStoreGold => GetConfig("UseStoreGold", false);
        private int RequiredStoreGold => GetConfig("RequiredStoreGold", 1000);
        private bool UseLevel => GetConfig("UseLevel", false);
        private int RequiredLevel => GetConfig("RequiredLevel", 3);
        private bool UsePermissions => GetConfig("UsePermissions", false);

        private bool CanCommenceVoteTime = true;
        private bool CanCommenceVoteWeather = true;
        private int Type = 0;

        List<Vote> CurrentVote = new List<Vote>();
        public class Vote
        {
            private Player _voter = null;
            private bool _choice = true;
            public Vote(Player player, bool choice)
            {
                _voter = player; _choice = choice;
            }
            public Player Voter { get { return _voter; } set { _voter = value; } }
            public bool Choice { get { return _choice; } set { _choice = value; } }
            public void Clear()
            {
                _voter = null; _choice = true;

            }
        }

        #endregion

        #region Save and Load data

        protected override void LoadDefaultConfig()
        {
            Config["UseYNCommands"] = UseYNCommands;
            Config["VoteDuration"] = VoteDuration;
            Config["TimeVoteCooldown"] = TimeVoteCooldown;
            Config["WeatherVoteCooldown"] = WeatherVoteCooldown;

            Config["UseStoreGold"] = UseStoreGold;
            Config["RequiredStoreGold"] = RequiredStoreGold;
            Config["UseLevel"] = UseLevel;
            Config["RequiredLevel"] = RequiredLevel;
            Config["UsePermissions"] = UsePermissions;
            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "voteDayStart", "{0} wants to change the [4444FF]time[FFFFFF] to [4444FF]day[FFFFFF]." },
                { "voteNightStart", "{0} wants to change the [4444FF]time[FFFFFF] to [4444FF]night[FFFFFF]." },
                { "voteClearStart", "{0} wants to [4444FF]clear[FFFFFF] the [4444FF]weather[FFFFFF]." },
                { "voteStormStart", "{0} wants to make it [4444FF]storm[FFFFFF]." },
                { "voteDayPassed", "The vote to set the time to day has passed. ({0}% of the votes were yes)" },
                { "voteNightPassed", "The vote to set the time to night has passed. ({0}% of the votes were yes)" },
                { "voteClearPassed", "The vote to set the weather to clear has passed. ({0}% of the votes were yes)" },
                { "voteStormPassed", "The vote to make it storm has passed. ({0}% of the votes were yes)" },
                { "voteDayFailed", "The vote to set the time to day has passed. ({0}% of the votes were yes)" },
                { "voteNightFailed", "The vote to set the time to night has passed. ({0}% of the votes were yes)" },
                { "voteClearFailed", "The vote to set the weather to clear has passed. ({0}% of the votes were yes)" },
                { "voteStormFailed", "The vote to make it storm has passed. ({0}% of the votes were yes)" },
                { "timeVoteReset", "The vote timer has reset and a new time vote can be started." },
                { "weatherVoteReset", "The vote timer has reset and a new weather vote can be started." },
                { "voteCommands", "[FFFFFF]Type [33CC33](/y)es[FFFFFF] or [FF0000](/n)o[FFFFFF] to participate in the vote." },
                { "voteDuration", "[FFFFFF]The vote will end in {0} seconds." },
                { "noOngoingVote", "There isn't an ongoing vote right now." },
                { "ongoingVote", "There is already an ongoing vote." },
                { "alreadyVoted", "You have already casted your vote." },
                { "voteYes", "{0} has voted [33CC33]yes[FFFFFF] to the current vote." },
                { "voteNo", "{0} has voted [ff0000]no[FFFFFF] to the current vote." },
                { "timeVoteCooldown", "There was a vote recently. There must be a {0} minutes delay between each time vote." },
                { "weatherVoteCooldown", "There was a vote recently. There must be a {0} minutes delay between each weather vote." },

                { "noVotePermission", "You don't have the permission to use this vote." },
                { "notHighEnoughLevel", "You don't meet the level requirements to start a vote (Level {0})." },
                { "notEnoughGold", "You do not have enough gold to start a vote ({0} gold)." },
                { "startVoteForGold", "Do you want to start the vote for {0} gold?" },

                { "helpTitle", "[0000FF]Voting System[FFFFFF]" },
                { "helpDay", "[00FF00]/voteday[FFFFFF] - Will start a vote to set the time to day." },
                { "helpNight", "[00FF00]/votenight[FFFFFF] - Will start a vote to set the time to night." },
                { "helpWClear", "[00FF00]/votewclear[FFFFFF] - Will start a vote to clear he weather." },
                { "helpWHeavy", "[00FF00]/votewheavy[FFFFFF] - Will start a vote to make it storm." },
                { "helpYes", "[00FF00]/yes[FFFFFF] - Vote yes." },
                { "helpNo", "[00FF00]/no[FFFFFF] - Vote no." },
                { "helpYesAndY", "[00FF00]/y [FFFFFF]or [00FF00]/yes[FFFFFF] - Vote yes." },
                { "helpNoAndN", "[00FF00]/n [FFFFFF]or [00FF00]/no[FFFFFF] - Vote no." }
            }, this);
        }

        private void Loaded()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
            if (UseYNCommands)
            {
                cmd.AddChatCommand("y", this, "YesCommand");
                cmd.AddChatCommand("n", this, "NoCommand");
            }
            permission.RegisterPermission("VotingSystem.VoteDay", this);
            permission.RegisterPermission("VotingSystem.VoteNight", this);
            permission.RegisterPermission("VotingSystem.VoteWClear", this);
            permission.RegisterPermission("VotingSystem.VoteWHeavy", this);
        }

        #endregion

        #region Commands

        [ChatCommand("voteday")]
        private void VoteDayCommand(Player player)
        {
            CheckVoteRequirements(player, 1);
        }

        [ChatCommand("votenight")]
        private void VoteNightCommand(Player player)
        {
            CheckVoteRequirements(player, 2);
        }

        [ChatCommand("votewclear")]
        private void VoteWClearCommand(Player player)
        {
            CheckVoteRequirements(player, 3);
        }

        [ChatCommand("votewheavy")]
        private void VoteWHeavyCommand(Player player)
        {
            CheckVoteRequirements(player, 4);
        }

        [ChatCommand("no")]
        private void NoCommand(Player player)
        {
            addVote(player, false);
        }
        
        [ChatCommand("yes")]
        private void YesCommand (Player player)
        {
            addVote(player, true);
        }

        #endregion

        #region Functions

        private void VoteFinish()
        {
            int yes = 0; int no = 0;
            foreach (var vote in CurrentVote)
            {
                if (vote.Choice == true) yes++;
                else no++;
            }
            float YesPercent = ((float)yes / (yes + no)) * 100;
            if (YesPercent >= 50f)
            {
                string Percent = ((int)YesPercent).ToString();
                switch (Type)
                {
                    case 1:
                        PrintToChat(string.Format(GetMessage("voteDayPassed"), Percent));
                        EventManager.CallEvent(new TimeSetEvent(GameClock.Instance.HourOfSunriseStart, GameClock.Instance.DaySpeed));
                        break;
                    case 2:
                        PrintToChat(string.Format(GetMessage("voteNightPassed"), Percent));
                        EventManager.CallEvent(new TimeSetEvent(GameClock.Instance.HourOfSunsetStart, GameClock.Instance.DaySpeed));
                        break;
                    case 3:
                        PrintToChat(string.Format(GetMessage("voteClearPassed"), Percent));
                        EventManager.CallEvent(new WeatherSetEvent(Weather.WeatherType.Clear));
                        break;
                    case 4:
                        PrintToChat(string.Format(GetMessage("voteStormPassed"), Percent));
                        EventManager.CallEvent(new WeatherSetEvent(Weather.WeatherType.PrecipitateHeavy));
                        break;
                }
            }
            else
            {
                string Percent = YesPercent.ToString();
                switch (Type)
                {
                    case 1:
                        PrintToChat(string.Format(GetMessage("voteDayFailed"), Percent));
                        break;
                    case 2:
                        PrintToChat(string.Format(GetMessage("voteNightFailed"), Percent));
                        break;
                    case 3:
                        PrintToChat(string.Format(GetMessage("voteClearFailed"), Percent));
                        break;
                    case 4:
                        PrintToChat(string.Format(GetMessage("voteStormFailed"), Percent));
                        break;
                }
            }
            CurrentVote.Clear();
            if (Type == 1 || Type == 2)
            {
                timer.In(TimeVoteCooldown, VoteTimerResetTime);
            }
            else if (Type == 3 || Type == 4)
            {
                timer.In(WeatherVoteCooldown, VoteTimerResetWeather);
            }
        }

        private void VoteTimerResetTime()
        {
            CanCommenceVoteTime = true;
            PrintToChat(GetMessage("timeVoteReset"));
        }

        private void VoteTimerResetWeather()
        {
            CanCommenceVoteWeather = true;
            PrintToChat(GetMessage("weatherVoteReset"));
        }

        private void CheckVoteRequirements(Player player, int type)
        {
            if (CurrentVote.Count > 0)
            {
                SendReply(player, GetMessage("ongoingVote", player.Id.ToString()));
                return;
            }
            Type = type;

            if (UsePermissions)
            {
                switch (Type)
                {
                    case 1:
                        if (!player.HasPermission("VotingSystem.VoteDay")) { PrintToChat(player, GetMessage("noVotePermission", player.Id.ToString())); return; }
                        break;
                    case 2:
                        if (!player.HasPermission("VotingSystem.VoteNight")) { PrintToChat(player, GetMessage("noVotePermission", player.Id.ToString())); return; }
                        break;
                    case 3:
                        if (!player.HasPermission("VotingSystem.VoteWClear")) { PrintToChat(player, GetMessage("noVotePermission", player.Id.ToString())); return; }
                        break;
                    case 4:
                        if (!player.HasPermission("VotingSystem.VoteWHeavy")) { PrintToChat(player, GetMessage("noVotePermission", player.Id.ToString())); return; }
                        break;
                }
            }
            if (plugins.Exists("LevelSystem") && UseLevel)
            {
                if ((int)LevelSystem.Call("GetCurrentLevel", new object[] { player }) < RequiredLevel)
                {
                    PrintToChat(player, string.Format(GetMessage("notHighEnoughLevel", player.Id.ToString()), RequiredLevel.ToString()));
                    return;
                }
            }
            if (plugins.Exists("GrandExchange") && UseStoreGold)
            {
                Dictionary<ulong, int> _playerWallet = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, int>>("SavedTradeWalletById");
                if (_playerWallet[player.Id] < RequiredStoreGold)
                {
                    PrintToChat(player, string.Format(GetMessage("notEnoughGold", player.Id.ToString()), RequiredStoreGold.ToString()));
                    return;
                }
                if ((bool)GrandExchange.Call("CanRemoveGold", new object[] { player, RequiredStoreGold }))
                {
                    player.ShowConfirmPopup("Voting", string.Format(GetMessage("startVoteForGold", player.Id.ToString()), RequiredStoreGold.ToString()), "Yes", "No", (options, dialogue1, data) => removePlayerGold(player, options));
                }
            }
            else VoteStart(player);
        }

        private void removePlayerGold(Player player, Options options)
        {
            if (options == Options.Yes)
            {
                GrandExchange.Call("RemoveGold", new object[] { player, RequiredStoreGold });
                VoteStart(player);
            }
        }

        private void VoteStart(Player player)
        {
            if (Type == 1 || Type == 2)
            {
                if (CanCommenceVoteTime == false)
                {
                    SendReply(player, GetMessage("timeVoteCooldown", player.Id.ToString()), (TimeVoteCooldown / 60).ToString());
                    return;
                }
            }
            else if (Type == 3 || Type == 4)
            {
                if (CanCommenceVoteWeather == false)
                {
                    SendReply(player, GetMessage("weatherVoteCooldown", player.Id.ToString()), (WeatherVoteCooldown / 60).ToString());
                    return;
                }
            }
            CurrentVote.Add(new Vote(player, true));
            switch (Type)
            {
                case 1:
                    PrintToChat(string.Format(GetMessage("voteDayStart"), player.DisplayName));
                    break;
                case 2:
                    PrintToChat(string.Format(GetMessage("voteNightStart"), player.DisplayName));
                    break;
                case 3:
                    PrintToChat(string.Format(GetMessage("voteClearStart"), player.DisplayName));
                    break;
                case 4:
                    PrintToChat(string.Format(GetMessage("voteStormStart"), player.DisplayName));
                    break;
            }
            PrintToChat(GetMessage("voteCommands"));
            PrintToChat(string.Format(GetMessage("voteDuration"), VoteDuration.ToString()));
            if (Type == 1 || Type == 2)
            {
                CanCommenceVoteTime = false;
            }
            else if (Type == 3 || Type == 4)
            {
                CanCommenceVoteWeather = false;
            }
            timer.In(VoteDuration, VoteFinish);
        }

        private void addVote(Player player, bool voted)
        {
            if (CurrentVote.Count == 0)
            {
                SendReply(player, GetMessage("noOngoingVote", player.Id.ToString()));
                return;
            }
            foreach (var vote in CurrentVote)
            {
                if (vote.Voter == player)
                {
                    SendReply(player, GetMessage("alreadyVoted", player.Id.ToString()));
                    return;
                }
            }
            if (voted)
            {
                CurrentVote.Add(new Vote(player, true));
                PrintToChat(string.Format(GetMessage("voteYes"), player.DisplayName));
            }
            else
            {
                CurrentVote.Add(new Vote(player, false));
                PrintToChat(string.Format(GetMessage("voteNo"), player.DisplayName));
            }
        }

        #endregion

        #region Hooks

        /*private void OnChatCommand(Player player, string cmd, string[] args)
        {
            if (cmd == "help" && args.Length == 0)
            {
                PrintToChat(player, GetMessage("helpTitle", player.Id.ToString()));
                PrintToChat(player, GetMessage("helpDay", player.Id.ToString()));
                PrintToChat(player, GetMessage("helpNight", player.Id.ToString()));
                PrintToChat(player, GetMessage("helpWClear", player.Id.ToString()));
                PrintToChat(player, GetMessage("helpWHeavy", player.Id.ToString()));
                if (UseYNCommands)
                {
                    PrintToChat(player, GetMessage("helpYesAndY", player.Id.ToString()));
                    PrintToChat(player, GetMessage("helpNoAndN", player.Id.ToString()));
                }
                else
                {
                    PrintToChat(player, GetMessage("helpYes", player.Id.ToString()));
                    PrintToChat(player, GetMessage("helpNo", player.Id.ToString()));
                }
            }
        }*/

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}
