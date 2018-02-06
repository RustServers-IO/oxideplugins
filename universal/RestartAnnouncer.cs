
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using CodeHatch.Build;
using Oxide.Core;
using Oxide.Core.Configuration;
using CodeHatch.Engine.Administration;
using CodeHatch.Engine.Networking;
using CodeHatch.Permissions;
using CodeHatch.Common;

namespace Oxide.Plugins
{
    [Info("Restart Timer", "D-Kay", "1.2")]
    public class RestartAnnouncer : ReignOfKingsPlugin
    {
        #region Variables
        private int timeRemaining;
        private int timerType;
        private bool timerRunning = false;
        private string message;
        private string reason;
        
        private List<object> defaultNotifyTimes = new List<object>()
        {
            1,
            2,
            3,
            4,
            5,
            10,
            15,
            20,
            30,
            45,
            60
        };

        private List<object> notifyTimes => GetConfig("NotifyTimes", defaultNotifyTimes);
        #endregion

        #region Save and Load data
        protected override void LoadDefaultConfig()
        {
            Config["NotifyTimes"] = notifyTimes;
            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "howToUseRestart", "Use /trestart (time in minutes) (optional reason)" },
                { "howToUseShutdown", "Use /tshutdown (time in minutes) (optional reason)" },
                { "presetMessage", "[[64CEE1]Server[FFFFFF]]: " },
                { "customRestartMessage", "Server will restart in {0} minute(s) because{1}." },
                { "customShutdownMessage", "Server will shutdown in {0} minute(s) because{1}." },
                { "defaultRestartMessage", "Server will restart in {0} minute(s) due to maintenance." },
                { "defaultShutdownMessage", "Server will shutdown in {0} minute(s) due to maintenance." },
                { "unauthorizedUsage", "Unauthorized to use this command." },
                { "ongoingTimer", "There is already a timer running."}
            }, this);
        }

        private void Loaded()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();

            permission.RegisterPermission("RestartAnnouncer.restart", this);
            permission.RegisterPermission("RestartAnnouncer.shutdown", this);
            
            timerType = 0;
            timeRemaining = -1;
            message = "";
            reason = "";
        }
        #endregion

        #region Commands
        [ChatCommand("trestart")]
        private void RestartTimerCommand(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("RestartAnnouncer.restart"))
            {
                PrintToChat(player, GetMessage("unauthorizedUsage", player.Id.ToString()));
                return;
            }
            if (timerRunning) { PrintToChat(player, GetMessage("ongoingTimer", player.Id.ToString())); return; }
            if (input.Length < 1) { PrintToChat(player, GetMessage("howToUseRestart", player.Id.ToString())); return; }
            else if (input.Length == 1) { message = GetMessage("defaultRestartMessage"); }
            else
            {
                message = GetMessage("customRestartMessage");
                foreach (var word in input)
                {
                    if (word != input[0]) reason += " " + word;
                }
            }
            int repeatTimes = Convert.ToInt32(input[0]);
            timeRemaining = Convert.ToInt32(input[0]);
            timerType = 1;
            sendMessage();
            timerRunning = true;
            timer.Repeat(60, repeatTimes, sendMessage);
        }
		
		[ChatCommand("tshutdown")]
        private void ShutdownTimerCommand(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("RestartAnnouncer.shutdown"))
            {
                PrintToChat(player, GetMessage("unauthorizedUsage", player.Id.ToString()));
                return;
            }
            if (timerRunning) { PrintToChat(player, GetMessage("ongoingTimer", player.Id.ToString())); return; }
            if (input.Length < 1) { PrintToChat(player, GetMessage("howToUseShutdown", player.Id.ToString())); return; }
            else if (input.Length == 1) { message = GetMessage("defaultShutdownMessage"); }
            else
            {
                message = GetMessage("customShutdownMessage");
                foreach (var word in input)
                {
                    if (word != input[0]) reason += " " + word;
                }
            }
            int repeatTimes = Convert.ToInt32(input[0]);
            timeRemaining = Convert.ToInt32(input[0]);
            timerType = 2;
            sendMessage();
            timerRunning = true;
            timer.Repeat(60, repeatTimes, sendMessage);
        }
        #endregion

        #region Functions
        private void sendMessage()
        {
            string chatMessage = "";
            if (timerType == 1)
            {
                chatMessage = GetMessage("presetMessage") + string.Format(message, timeRemaining.ToString(), reason);
            }
            else if (timerType == 2)
            {
                chatMessage = GetMessage("presetMessage") + string.Format(message, timeRemaining.ToString(), reason);
            }

            if (timeRemaining == 0)
            {
                if (timerType == 1)
                {
                    SocketAdminConsole.RestartAfterShutdown = true;
                }
                else if (timerType == 2)
                {
                    SocketAdminConsole.RestartAfterShutdown = false;
                }
                Server.Shutdown();
            }
            else
            {
                foreach (var notify in notifyTimes)
                {
                    if (Convert.ToInt32(notify) == timeRemaining)
                    {
                        PrintToChat(chatMessage);
                    }
                }
            }
            timeRemaining--;
        }
        #endregion

        #region Helpers
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        #endregion
    }
}
