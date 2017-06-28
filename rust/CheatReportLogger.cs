using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("CheatReportLogger", "mvrb", "1.4.0", ResourceId = 2227)]
    [Description("Saves cheat reports from the F7 menu to a file and/or RCON.")]
    class CheatReportLogger : RustPlugin
    {
        private bool logToFile;
        private bool logToRCON;
        private bool logToSlack;
        private bool logToDiscord;
        private string slackChannel;
        private string notifyPermission;

        [PluginReference] Plugin Slack, Discord;

        protected override void LoadDefaultConfig()
        {
            Config["LogToFile"] = logToFile = GetConfig("LogToFile", true);
            Config["LogToRCON"] = logToRCON = GetConfig("LogToRCON", true);
            Config["LogToSlack"] = logToSlack = GetConfig("LogToSlack", false);
            Config["LogToDiscord"] = logToDiscord = GetConfig("LogToDiscord", false);
            Config["SlackChannel"] = slackChannel = GetConfig("SlackChannel", "slackchannel");
            Config["NotifyPermission"] = notifyPermission = GetConfig("NotifyPermission", "cheatreportlogger.notify");

            SaveConfig();
        }

        private void Init()
        {
            LoadDefaultConfig();
            RegisterPermissions();
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(notifyPermission, this);
        }

        private void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["RCON"] = "{0} [{1}] {2} used F7 to report {3}",
                ["File"] = "[{0}] {1} used F7 to report {2}"
            }, this);
        }

        private void OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return;

            var command = arg.cmd.FullName;
            var args = arg.GetString(0, "text");            

            if (command == "server.cheatreport")
            {
                string playerID = arg.Connection.userid.ToString();
                string playerName = arg.Connection.username;
                string suspectID = arg.FullString.Substring(1, 17);
                string suspectName = GetName(suspectID);
                string reason = arg.FullString.Replace(suspectID, "").Replace("\"", "");
                string date = DateTime.Now.ToString("yyyy-dd-MM H:mm:ss").ToString();

                if (logToRCON)
                    Puts(Lang("RCON", null, DateTime.Now.ToString("yyyy-dd-MM H:mm:ss"), arg.Connection.userid, arg.Connection.username, arg.FullString));
                if (logToFile)
                    LogToFile("Reports", Lang("File", null, arg.Connection.userid, arg.Connection.username, arg.FullString), this);
                if (logToSlack)
                    Slack?.Call("FancyMessage", $"{playerName} reported {suspectName} for {reason}", covalence.Players.FindPlayerById(playerID.ToString()) ?? null, slackChannel);
                if (logToDiscord)
                    Discord?.Call("SendMessage", $"{playerName} reported {suspectName} for {reason}", covalence.Players.FindPlayerById(playerID.ToString()) ?? null);

                foreach (var player in BasePlayer.activePlayerList.Where(x => permission.UserHasPermission(x.UserIDString, notifyPermission)))
                {
                    player.ChatMessage
                    (
                        "<color=#c8f463><size=20>Cheat Report</size></color>\n" +
                        $"{playerName} reported {suspectName} for {reason}"
                     );
                }
                    
            }
        }

        private string GetName(string id) { return RustCore.FindPlayer(id) ? RustCore.FindPlayer(id).displayName : covalence.Players.FindPlayer(id)?.Name; }

        private T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}