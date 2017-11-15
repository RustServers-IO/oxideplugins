using System;
using System.Linq;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("AngryTime", "Tori1157", "1.1.1")]
    [Description("Check & set time via commands")]

    class AngryTime : CovalencePlugin
    {
        #region Loading

        private bool Changed;
        private bool realTime;

        private string messagePrefix;
        private string messagePrefixColor;

        private const string adminPermission = "angrytime.admin";

        private void Loaded()
        {
            if (realTime == true)
            {
                var ServerTimeHour = DateTime.Now.Hour;
                var ServerTimeMinute = DateTime.Now.Minute;
                var ServerTimeSecond = DateTime.Now.Second;

                // TODO: Have it so users can add hours

                timer.Repeat(1, 0, () =>
                {
                    server.Time = server.Time.Date + TimeSpan.Parse(ServerTimeHour + ":" + ServerTimeMinute + ":" + ServerTimeSecond);
                });
            }
        }

        private void Init()
        {
            permission.RegisterPermission("angrytime.admin", this);

            LoadVariables();
        }

        private void LoadVariables()
        {
            messagePrefix = Convert.ToString(GetConfig("Messaging", "Message Prefix", "Angry Time"));
            messagePrefixColor = Convert.ToString(GetConfig("Messaging", "Message Prefix Color", "#ffa500"));
            realTime = Convert.ToBoolean(GetConfig("Options", "Use Server Time", false));

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
                /// -- ERROR -- ///

                ["No Permission"] = "[#add8e6]{player}[/#] you do not have permission to use the [#00ffff]{command}[/#] command.",

                ["Incorrect Parameter"] = "Parameter [#add8e6]{parameter}[/#] is invalid or written wrong.",

                ["Invalid Syntax Set"] = "Invalid syntax!  |  /time set 10 [#00ffff](0 > 23)[/#]",

                //["Invalid Syntax Add Chat"] = "Invalid syntax!  |  /time add 10 [#00ffff](1 > 23)[/#]",

                ["Invalid Time Set"] = "[#add8e6]{time}[/#] is not a valid number  |  /time set 10:30 \n[#00ffff](01 > 23 Hours : 01 > 59 Minutes)[/#]",

                //["Invalid Time Add Chat"] = "[#add8e6]{time}[/#] is not a number  |  /time add \n[#00ffff](1 > 23)[/#]",

                ["Invalid Time Length"] = "[#add8e6]{time}[/#] is too short, need to be a four digit number  |  [#00ffff]2359[/#] - [#00ffff]23:59[/#]",

                /// -- CONFIRM -- ///

                //["Time Added Chat"] = "You have added [#add8e6]{time}[/#] hours",

                ["Time Changed"] = "You have changed the time to [#add8e6]{time}[/#]",

                /// -- INFO -- ///

                ["Current Game Time"] = "[#00ffff]{0}[/#]",

                ["Time Help Command Player"] = "- [#ffa500]/time help[/#] [i](Displays this message)[/i]\n- [#ffa500]/time[/#] [i](This will display the current time and date in-game)[/i]",
                //["Time Help Command Chat Admin"] = "- [#ffa500]/time set 10[/#] [i](This will set the time to a whole number [#00ffff][+12](01 > 23 Hours : 01 > 59 Minutes)[/+][/#])[/i]\n- [#ffa500]/time add 1[/#] [i](This will add one hour to the current time [#00ffff][+12](1 > 23)[/+][/#])[/i]\n- [#ffa500]/time help[/#] [i](Displays this message)[/i]\n- [#ffa500]/time[/#] [i](This will display the current time and date in-game)[/i]",
                ["Time Help Command Admin"] = "- [#ffa500]/time set 10[/#] [i](This will set the time to a whole number [#00ffff][+12](01 > 23 Hours : 01 > 59 Minutes)[/+][/#])[/i]\n- [#ffa500]/time help[/#] [i](Displays this message)[/i]\n- [#ffa500]/time[/#] [i](This will display the current time and date in-game)[/i]",
                ["Time Help Command Console"] = "\n- time set 10 (This will set the time to a whole number(10:00))\n- time add 1 (This will add one hour to the current time (1 > 23))\n- time (This will display the current time and date in-game)",

            }, this);
        }

        #endregion

        #region Commands

        [Command("time")]
        private void TimeCommand(IPlayer player, string command, string[] args)
        {
            #region Default
            var HasPerm = (player.HasPermission(adminPermission));

            if (args.Length == 0)
            {
                SendChatMessage(player, Lang("Current Game Time", player.Id, server.Time));
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
                SendChatMessage(player, lang.GetMessage("Incorrect Parameter", this, player.Id).Replace("{parameter}", CommandArg));
                return;
            }
            #endregion

            switch (CommandArg)
            {
                #region Set
                case "set":

                    if (!HasPerm && !player.IsServer)
                    {
                        SendChatMessage(player, lang.GetMessage("No Permission", this, player.Id).Replace("{player}", player.Name).Replace("{command}", command));
                        return;
                    }

                    if (args.Length != 2)
                    {
                        SendChatMessage(player, lang.GetMessage("Invalid Syntax Set", this, player.Id));
                        return;
                    }

                    // Checking to see if the parameter put in is a number
                    double Setnumber;
                    string TimeSetParameter = args[1];
                    if (!double.TryParse(TimeSetParameter, out Setnumber))
                    {
                        SendChatMessage(player, lang.GetMessage("Invalid Time Set", this, player.Id).Replace("{time}", TimeSetParameter));
                        return;
                    }

                    var CleanClock = args[1].Replace(":", "");

                    if (args[1].Length <= 3)
                    {
                        SendChatMessage(player, lang.GetMessage("Invalid Time Length", this, player.Id).Replace("{time}", TimeSetParameter));
                        return;
                    }

                    var SplitHour = CleanClock.Substring(0, 2);
                    var SplitMinute = CleanClock.Substring(2, 2);

                    var ConvertHour = Convert.ToInt32(SplitHour);
                    var ConvertMinute = Convert.ToInt32(SplitMinute);

                    var ClockInText = SplitHour + ":" + SplitMinute + ":00";

                    if (ConvertHour >= 24 || ConvertMinute >= 60)
                    {
                        SendChatMessage(player, lang.GetMessage("Invalid Time Set", this, player.Id).Replace("{time}", TimeSetParameter));
                        return;
                    }

                    server.Time = server.Time.Date + TimeSpan.Parse(SplitHour + ":" + SplitMinute + ":00");

                    SendChatMessage(player, lang.GetMessage("Time Changed", this, player.Id).Replace("{time}", ClockInText));

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
                            SendInfoMessage(player, lang.GetMessage("Time Help Command Player", this, player.Id));
                            return;
                        }

                        SendInfoMessage(player, lang.GetMessage("Time Help Command Admin", this, player.Id));
                        return;
                    }

                    SendChatMessage(player, lang.GetMessage("Time Help Command Console", this, player.Id));

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

        private void SendChatMessage(IPlayer player, string message)
        {
            player.Reply(message, covalence.FormatText("[" + messagePrefixColor + "]" + messagePrefix + "[/#]:"));
        }

        private void SendInfoMessage(IPlayer player, string message)
        {
            player.Reply(message, covalence.FormatText("[+18][" + messagePrefixColor + "]" + messagePrefix + "[/#][/+]\n\n"));
        }

        #endregion
    }
}