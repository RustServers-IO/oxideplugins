using System;
using System.Linq;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("AngryTime", "Tori1157", "1.0.5")]
    [Description("Check & set time via commands")]

    class AngryTime : CovalencePlugin
    {
        #region Loading

        private bool Changed;

        private string MessagePrefix;
        private string MessagePrefixColor;

        private void Init()
        {
            permission.RegisterPermission("angrytime.admin", this);

            LoadVariables();
        }

        private void LoadVariables()
        {
            MessagePrefix = Convert.ToString(GetConfig("Options", "Message Prefix", "Angry Time"));
            MessagePrefixColor = Convert.ToString(GetConfig("Options", "Message Prefix Color", "#ffa500"));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //////////////////////
                // ---- ERROR ----- //
                //////////////////////

                ["No Permission"] = "[#add8e6]{player}[/#] you do not have permission to use the [#00ffff]{command}[/#] command.",

                ["Incorrect Parameter Chat"] = "Parameter [#add8e6]{parameter}[/#] is invalid or written wrong.",
                ["Incorrect Parameter Console"] = "Parameter {parameter} is invalid or written wrong.",

                ["Invalid Syntax Set Chat"] = "Invalid syntax!  |  /time set 10 [#00ffff](0 > 23)[/#]",
                ["Invalid Syntax Set Console"] = "Invalid Syntax!  |  time set 10 (0 > 23)",

                //["Invalid Syntax Add Chat"] = "Invalid syntax!  |  /time add 10 [#00ffff](1 > 23)[/#]",
                //["Invalid Syntax Add Console"] = "Invalid syntax!  |  time add 10 (1 > 23)",

                ["Invalid Time Set Chat"] = "[#add8e6]{time}[/#] is not a valid number  |  /time set 10:30 \n[#00ffff](01 > 23 Hours : 01 > 59 Minutes)[/#]",
                ["Invalid Time Set Console"] = "{time} is not a valid number  |  time set 10:30 (01 > 23 Hours : 01 > 59 Minutes)",

                //["Invalid Time Add Chat"] = "[#add8e6]{time}[/#] is not a number  |  /time add \n[#00ffff](1 > 23)[/#]",
                //["Invalid Time Add Console"] = "{time} is not a correct number  |  time add 1 (1 > 23)",

                ["Invalid Time Length Chat"] = "{time} is too short, need to be a four digit number  |  [#00ffff]2359[/#] - [#00ffff]23:59[/#]",
                ["Invalid Time Length Console"] = "{time} is too short, need to be a four digit number  |  2359 - 23:59",

                /////////////////////////
                // ----- CONFIRM ----- //
                /////////////////////////

                //["Time Added Chat"] = "You have added [#add8e6]{time}[/#] hours",
                //["Time Added Console"] = "You have added {time} hours",

                ["Time Changed Chat"] = "You have changed the time to [#add8e6]{time}[/#]",
                ["Time Changed Console"] = "You have changed the time to {time}",

                /////////////////////////////
                // ----- INFORMATION ----- //
                /////////////////////////////

                ["Current Game Time Chat"] = "Current game time is [#00ffff]{0}[/#]",
                ["Current Game Time Console"] = "Current game time is {0}",

                ["Time Help Command Chat Player"] = "- [#ffa500]/time help[/#] [i](Displays this message)[/i]\n- [#ffa500]/time[/#] [i](This will display the current time and date in-game)[/i]",
                //["Time Help Command Chat Admin"] = "- [#ffa500]/time set 10[/#] [i](This will set the time to a whole number [#00ffff][+12](01 > 23 Hours : 01 > 59 Minutes)[/+][/#])[/i]\n- [#ffa500]/time add 1[/#] [i](This will add one hour to the current time [#00ffff][+12](1 > 23)[/+][/#])[/i]\n- [#ffa500]/time help[/#] [i](Displays this message)[/i]\n- [#ffa500]/time[/#] [i](This will display the current time and date in-game)[/i]",
                ["Time Help Command Chat Admin"] = "- [#ffa500]/time set 10[/#] [i](This will set the time to a whole number [#00ffff][+12](01 > 23 Hours : 01 > 59 Minutes)[/+][/#])[/i]\n- [#ffa500]/time help[/#] [i](Displays this message)[/i]\n- [#ffa500]/time[/#] [i](This will display the current time and date in-game)[/i]",
                ["Time Help Command Console"] = "\n- time set 10 (This will set the time to a whole number(10:00))\n- time add 1 (This will add one hour to the current time (1 > 23))\n- time (This will display the current time and date in-game)",

            }, this);
        }

        #endregion

        #region Commands

        [Command("time")]
        private void TimeCommand(IPlayer player, string command, string[] args)
        {
            #region Default
            var HasPerm = (player.HasPermission("angrytime.admin"));

            if (args.Length == 0)
            {
                if (!player.IsServer)
                {
                    SendChatMessage(player, MessagePrefix, Lang("Current Game Time Chat", player.Id, server.Time));
                    return;
                }

                SendConsoleMessage(player, MessagePrefix, Lang("Current Game Time Console", player.Id, server.Time));
                return;
            }
            
            var CommandArg = args[0].ToLower();
            var CommandInfo = (command + " " + args[0]);

            var CaseArgs = (new List<object>
            {
                "set", "help"
            });

            if (!CaseArgs.Contains(CommandArg))
            {
                if (!player.IsServer)
                {
                    SendChatMessage(player, MessagePrefix, lang.GetMessage("Incorrect Parameter Chat", this, player.Id).Replace("{parameter}", CommandArg));
                    return;
                }

                SendConsoleMessage(player, MessagePrefix, lang.GetMessage("Incorrect Parameter Console", this, player.Id).Replace("{parameter}", CommandArg));
                return;
            }
            #endregion

            switch (CommandArg)
            {
                #region Set
                case "set":

                    if (!HasPerm && !player.IsServer)
                    {
                        SendChatMessage(player, MessagePrefix, lang.GetMessage("No Permission", this, player.Id).Replace("{player}", player.Name).Replace("{command}", command));
                        return;
                    }

                    if (args.Length != 2)
                    {
                        if (!player.IsServer)
                        {
                            SendChatMessage(player, MessagePrefix, lang.GetMessage("Invalid Syntax Set Chat", this, player.Id));
                            return;
                        }

                        SendConsoleMessage(player, MessagePrefix, lang.GetMessage("Invalid Syntax Set Console", this, player.Id));
                        return;
                    }

                    // Checking to see if the parameter put in is a number
                    double number1;
                    string TimeParameter1 = args[1];
                    if (!double.TryParse(TimeParameter1, out number1))
                    {
                        if (!player.IsServer)
                        {
                            SendChatMessage(player, MessagePrefix, lang.GetMessage("Invalid Time Set Chat", this, player.Id).Replace("{time}", TimeParameter1));
                            return;
                        }

                        SendConsoleMessage(player, MessagePrefix, lang.GetMessage("Invalid Time Set Console", this, player.Id).Replace("{time}", TimeParameter1));
                        return;
                    }

                    var CleanClock = args[1].Replace(":", "");

                    if (args[1].Length <= 3)
                    {
                        if (!player.IsServer)
                        {
                            SendChatMessage(player, MessagePrefix, lang.GetMessage("Invalid Time Length Chat", this, player.Id).Replace("{time}", TimeParameter1));
                            return;
                        }

                        SendConsoleMessage(player, MessagePrefix, lang.GetMessage("Invalid Time Length Console", this, player.Id).Replace("{time}", TimeParameter1));
                        return;
                    }

                    var SplitHour = CleanClock.Substring(0, 2);
                    var SplitMinute = CleanClock.Substring(2, 2);

                    var ConvertHour = Convert.ToInt32(SplitHour);
                    var ConvertMinute = Convert.ToInt32(SplitMinute);

                    var ClockInText = SplitHour + ":" + SplitMinute + ":00";

                    if (ConvertHour >= 24 || ConvertMinute >= 60)
                    {
                        if (!player.IsServer)
                        {
                            SendChatMessage(player, MessagePrefix, lang.GetMessage("Invalid Time Set Chat", this, player.Id).Replace("{time}", TimeParameter1));
                            return;
                        }

                        SendConsoleMessage(player, MessagePrefix, lang.GetMessage("Invalid Time Set Console", this, player.Id).Replace("{time}", TimeParameter1));
                        return;
                    }

                    server.Time = server.Time.Date + TimeSpan.Parse(SplitHour + ":" + SplitMinute + ":00");
                    server.Time.AddHours(1);

                    if (!player.IsServer)
                    {
                        SendChatMessage(player, MessagePrefix, lang.GetMessage("Time Changed Chat", this, player.Id).Replace("{time}", ClockInText));
                        return;
                    }

                    SendConsoleMessage(player, MessagePrefix, lang.GetMessage("Time Changed Console", this, player.Id).Replace("{time}", ClockInText));

                return;
                #endregion

                #region Add
                /*case "add":

                    if (!HasPerm && !player.IsServer)
                    {
                        SendChatMessage(player, "Angry Time", covalence.FormatText(lang.GetMessage("No Permission", this, player.Id).Replace("{player}", player.Name).Replace("{command}", command)));
                        return;
                    }

                    if (args.Length != 2)
                    {
                        if (!player.IsServer)
                        {
                            SendChatMessage(player, "Angry Time", covalence.FormatText(lang.GetMessage("Invalid Syntax Add Chat", this, player.Id)));
                            return;
                        }

                        SendConsoleMessage(player, "Angry Time", covalence.FormatText(lang.GetMessage("Invalid Syntax Add Console", this, player.Id)));
                        return;
                    }

                    // Checking to see if the parameter put in is a number
                    double number2;
                    string TimeParameter2 = args[1];
                    if (!double.TryParse(TimeParameter2, out number2))
                    {
                        if (!player.IsServer)
                        {
                            SendChatMessage(player, "Angry Time", covalence.FormatText(lang.GetMessage("Invalid Time Add Chat", this, player.Id).Replace("{time}", TimeParameter2)));
                            return;
                        }

                        SendConsoleMessage(player, "Angry Time", covalence.FormatText(lang.GetMessage("Invalid Time Add Console", this, player.Id).Replace("{time}", TimeParameter2)));
                        return;
                    }

                    var ConvertTime = Convert.ToInt32(TimeParameter2);
                    decimal MathTime = ConvertTime / 100m;
                    var UsableTime = Math.Round(MathTime, 2);

                    server.Command("env.addtime " + UsableTime);

                    if (!player.IsServer)
                    {
                        SendChatMessage(player, "Angry Time", covalence.FormatText(lang.GetMessage("Time Added Chat", this, player.Id).Replace("{time}", TimeParameter2)));
                        return;
                    }

                    SendConsoleMessage(player, "Angry Time", covalence.FormatText(lang.GetMessage("Time Added Console", this, player.Id).Replace("{time}", TimeParameter2)));

                return;*/
                #endregion

                #region Help
                case "help":

                    if (!player.IsServer)
                    {
                        if (!HasPerm)
                        {
                            SendInfoMessage(player, MessagePrefix, lang.GetMessage("Time Help Command Chat Player", this, player.Id));
                            return;
                        }

                        SendInfoMessage(player, MessagePrefix, lang.GetMessage("Time Help Command Chat Admin", this, player.Id));
                        return;
                    }

                    SendConsoleMessage(player, MessagePrefix, lang.GetMessage("Time Help Command Console", this, player.Id));

                return;
                #endregion
            }
        }

        #endregion

        #region Helpers

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);


        private object GetConfig(string menu, string datavalue, object defaultValue)
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

        #endregion

        #region Messages

        private void SendConsoleMessage(IPlayer player, string prefix, string msg)
        {
            player.Reply(prefix + ": " + msg);
        }

        private void SendChatMessage(IPlayer player, string prefix, string msg)
        {
            player.Reply("[" + MessagePrefixColor + "]" + prefix + "[/#]: " + msg);
        }

        private void SendInfoMessage(IPlayer player, string prefix, string msg)
        {
            player.Reply("[+18][" + MessagePrefixColor + "]" + prefix + "[/#][/+]\n\n" + msg);
        }

        #endregion
    }
}