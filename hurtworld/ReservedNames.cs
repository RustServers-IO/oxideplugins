// Reference: UnityEngine.UI
/*
 * The MIT License (MIT)
 * Copyright (c) 2015 Feramor
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 *
 *
 * 29. of January 2016
 * * Adaption of this Rust Plugin by vitaminZ as Hurtworld Plugin
 *
 */

//Microsoft NameSpaces

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Hurtworld.Libraries.Covalence;

//Oxide NameSpaces

//External NameSpaces
//using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Reserved Name", "vitaminZ", "0.0.8")]
    public class ReservedNames : HurtworldPlugin
    {
        private Dictionary<string, object> _userNames = new Dictionary<string, object>();
        private Dictionary<string, object> _users = new Dictionary<string, object>();
        public DynamicConfigFile ConfigFile;

        private void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Kicked_Nickname_Too_Short_Server_Log", "Kicking {0} because of a too short nickname"},
                {"Kicked_Nickname_Too_Short_To_Player", "Choose a nickname with at least 2 characters"},
                {"Kicked_Nickname_Empty_Server_Log", "Kicking {0} because of a empty nickname."},
                {
                    "Kicked_Nickname_Empty_To_Player",
                    "Your Nickname did not get submitted. Restart the game to fix this issue"
                },
                {"Kicked_Nickname_Not_Edited_Server_Log", "Kicking {0} because of a not edited LumaEmu.ini"},
                {"Kicked_Nickname_Not_Edited_To_Player", "Set a unique nickname in your LumaEmu.ini"},
                {
                    "Kicked_Nickname_Is_Reserved_Server_Log",
                    "Kicking {0} because the choosen nickname is reserved for another SteamID"
                },
                {"Kicked_Nickname_Is_Reserved_To_Player", "This name is reserved for another SteamID"},
                {"Default_Config_Loaded_Server_Log", "Default Config loaded"},
                {"Reserve_Time_Extended_Server_Log", "{0} leasing time extended"},
                {"New_SteamID_For_Nickname_Server_Log", "{0} replaced SteamID after {1} seconds"},
                {"Nickname_Added_Server_Log", "{0} added"}
            }, this);

            LoadConfig();
            ConfigFile = Interface.Oxide.DataFileSystem.GetDatafile("ReservedNames");

            if (ConfigFile["UserNames"] != null)
                if (((Dictionary<string, object>) ConfigFile["UserNames"]).Count != 0)
                    _userNames = (Dictionary<string, object>) ConfigFile["UserNames"];
            if (ConfigFile["Users"] != null)
                if (((Dictionary<string, object>) ConfigFile["Users"]).Count != 0)
                    _users = (Dictionary<string, object>) ConfigFile["Users"];

            //lets check every already connected player
            var sessions = GameManager.Instance.GetSessions();
            foreach (var tmpSession in sessions)
            {
                CheckPlayer(tmpSession.Value);
            }

            SaveData();
        }

        /// <summary>
        ///     Check if a player has a proper nickname and the right to use it
        /// </summary>
        /// <param name="currentSession">PlayerSession</param>
        /// <returns>return is just used to get out of the method if we have an invalid nickname</returns>
        private bool CheckPlayer(PlayerSession currentSession)
        {
            // make some checks to make sure that the connected user has kind of a valid Nickname
            if (currentSession.Name == "")
            {
                Puts(lang.GetMessage("Kicked_Nickname_Empty_Server_Log", this), currentSession.SteamId.ToString());
                ConsoleManager.Instance?.ExecuteCommand("kick " + currentSession.SteamId + " " + lang.GetMessage("Kicked_Nickname_Empty_To_Player", this));
                return false;
            }
            if (currentSession.Name.Length <= 1)
            {
                Puts(lang.GetMessage("Kicked_Nickname_Too_Short_Server_Log", this), currentSession.SteamId.ToString());
                ConsoleManager.Instance?.ExecuteCommand("kick " + currentSession.SteamId + " " + lang.GetMessage("Kicked_Nickname_Too_Short_To_Player", this));
                return false;
            }
            if (currentSession.Name.Contains("LumaEmu") || currentSession.Name.Contains("KnightsTable") ||
                currentSession.Name.Contains("Kortal") || currentSession.Name.Contains("Rusteo"))
            {
                Puts(lang.GetMessage("Kicked_Nickname_Not_Edited_Server_Log", this), currentSession.SteamId.ToString());
                ConsoleManager.Instance?.ExecuteCommand("kick " + currentSession.SteamId + " " + lang.GetMessage("Kicked_Nickname_Not_Edited_To_Player", this));
                return false;
            }

			//remove 0-9 chars from nicknames to avoid names like Hurt1, Hurt2 beeing seen as different namespace
			var cleanedCurrentSessionUserName = Regex.Replace(currentSession.Name.ToLower(), @"[\d-]", string.Empty);
			
            if (_userNames.Count > 0 && _userNames.ContainsKey(cleanedCurrentSessionUserName))
            {
                if (currentSession.SteamId.ToString() != _userNames[cleanedCurrentSessionUserName].ToString())
                {
                    var currentTimer =
                        Convert.ToInt64(
                            (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
                    var User = (Dictionary<string, object>) _users[_userNames[cleanedCurrentSessionUserName].ToString()];

                    if (currentTimer >= Convert.ToInt64(User["TIMER"].ToString()))
                    {
                        _users.Remove(_userNames[cleanedCurrentSessionUserName].ToString());
                        _userNames.Remove(cleanedCurrentSessionUserName);

                        var newUser = new Dictionary<string, object>();
                        newUser.Add("USERNAME", cleanedCurrentSessionUserName);
                        newUser.Add("TIMER",
                            (currentTimer +
                             Convert.ToInt64(
                                 (string) (((Dictionary<string, object>) Config["Settings"])["DeletionTime"]))).ToString
                                ());

                        if (_users.ContainsKey(currentSession.SteamId.ToString()))
                        {
                            var oldUserName = (Dictionary<string, object>) _users[currentSession.SteamId.ToString()];
                            _userNames.Remove(oldUserName["USERNAME"].ToString());
                            _users.Remove(currentSession.SteamId.ToString());
                        }

                        _users.Add(currentSession.SteamId.ToString(), newUser);
                        _userNames.Add(cleanedCurrentSessionUserName, currentSession.SteamId.ToString());

                        Puts(lang.GetMessage("New_SteamID_For_Nickname_Server_Log", this),
                            cleanedCurrentSessionUserName,
                            Convert.ToInt64((string) (((Dictionary<string, object>) Config["Settings"])["DeletionTime"]))
                                .ToString());
                    }
                    else
                    {
                        try
                        {
                            Puts(lang.GetMessage("Kicked_Nickname_Is_Reserved_Server_Log", this),
                                currentSession.SteamId.ToString());
                            ConsoleManager.Instance?.ExecuteCommand("kick " + currentSession.SteamId + " " + lang.GetMessage("Kicked_Nickname_Is_Reserved_To_Player", this));
                        }
                        catch
                        {
                            // Empty catch is very bad pratice. Dont do it kids!
                        }
                    }
                }
                else
                {
                    var currentTimer =
                        Convert.ToInt64(
                            (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
                    var User = (Dictionary<string, object>) _users[_userNames[currentSession.Name].ToString()];
                    User.Remove("TIMER");
                    User.Add("TIMER",
                        (currentTimer +
                         Convert.ToInt64((string) (((Dictionary<string, object>) Config["Settings"])["DeletionTime"])))
                            .ToString());

                    Puts(lang.GetMessage("Reserve_Time_Extended_Server_Log", this), currentSession.SteamId.ToString());
                }
            }
            else
            {
                var currentTimer =
                    Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
                var newUser = new Dictionary<string, object>();
                newUser.Add("USERNAME", currentSession.Name);
                newUser.Add("TIMER",
                    (currentTimer +
                     Convert.ToInt64((string) (((Dictionary<string, object>) Config["Settings"])["DeletionTime"])))
                        .ToString());
                if (_userNames.Count > 0 && _users.ContainsKey(currentSession.SteamId.ToString()))
                {
                    var oldUserName = (Dictionary<string, object>) _users[currentSession.SteamId.ToString()];
                    _userNames.Remove(oldUserName["USERNAME"].ToString());
                    _users.Remove(currentSession.SteamId.ToString());
                }
                _users.Add(currentSession.SteamId.ToString(), newUser);
                _userNames.Add(currentSession.Name, currentSession.SteamId.ToString());

                Puts(lang.GetMessage("Nickname_Added_Server_Log", this), currentSession.SteamId.ToString());
            }
            SaveData();
            return true;
        }

        /// <summary>
        ///     write our data to the safe file
        /// </summary>
        private void SaveData()
        {
            ConfigFile["UserNames"] = _userNames;
            ConfigFile["Users"] = _users;

            Interface.Oxide.DataFileSystem.SaveDatafile("ReservedNames");
        }

        /// <summary>
        ///     create a default config if this is our first time getting loaded
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            var newConfig = new Dictionary<string, object>();
            newConfig.Add("DeletionTime", "604800");
            Config["Settings"] = newConfig;

            Puts(lang.GetMessage("Nickname_Added_Server_Log", this));
        }

        /// <summary>
        ///     Check every new connecting player
        /// </summary>
        /// <param name="new_player"></param>
        private void OnPlayerConnected(PlayerSession new_player)
        {
            CheckPlayer(new_player);
        }
    }
}