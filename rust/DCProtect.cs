using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DCProtect", "FireStorm78", "1.0.2", ResourceId = 2724)]
    [Description("Prevents players from looting other players that have disconnected for a specified time.")]
    class DCProtect : RustPlugin
    {

        #region Initialization

        //readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("DCProtect");
        List<string> PlayerList = new List<string>();

        void Init()
        {
            LoadDefaultConfig();
            //PlayerList = dataFile.ReadObject<List<string>>();
        }

        #endregion

        #region Configuration

        int DC_DelayInSeconds;
        int Start_DelayInSeconds;

        protected override void LoadDefaultConfig()
        {
            Config["DC_DelayInSeconds"] = DC_DelayInSeconds = GetConfig("DC_DelayInSeconds", 300);
            Config["Start_DelayInSeconds"] = Start_DelayInSeconds = GetConfig("Start_DelayInSeconds", 300);
            SaveConfig();
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "This player has just disconnected. They are protected from looting for '{0}' seconds.",
                ["ServerStart"] = "The server just started. All players are protected from looting for '{0}' seconds.",
                ["InvalidCommand"] = "[DCP] That is an invalid command.",
                ["ManualAdd"]= "[DCP] You are looking at player {0}|{1}. Manually adding protection.",
                ["ManualRemove"] = "[DCP] You are looking at player {0}|{1}. Manually removing protection.",
                ["ListTitle"] = "[DCP] Current Protected Player List",
                ["TimerComplete"] = "Timer Complete. Removing Player: {0}|{1}.",
                ["PlayerDisconnected"] = "Player Disconnected. Adding Player {0}|{1} to protection list.",
                ["LogViolation_DC"] = "Player {0}|{1} tried to loot {2}|{3} after they disconnected...tisk tisk.",
                ["LogViolation_ServerStart"] = "Player {0}|{1} tried to loot {2}|{3} after the server started...tisk tisk."
            }, this);
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private bool IsAdmin(BasePlayer player)
        {
            if (player == null) return false;
            if (player?.net?.connection == null) return true;
            return player.net.connection.authLevel > 0;
        }
        #endregion

        [ChatCommand("DCP")]
        private void cmdDCP(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "test":
                        cmdDCP_Test(player, command, args.Skip(1).ToArray());
                        break;
                    case "remove":
                        cmdDCP_Remove(player, command, args.Skip(1).ToArray());
                        break;
                    case "list":
                        cmdDCP_List(player, command, args.Skip(1).ToArray());
                        break;
                    default:
                        PrintToChat(player, Lang("InvalidCommand", player.UserIDString));
                        break;
                }
            }
        }

        private void cmdDCP_Test(BasePlayer player, string command, string[] args)
        {
            if (IsAdmin(player))
            {

                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, 10f))
                {
                    BaseEntity closestEntity = hit.GetEntity();

                    if (closestEntity is BasePlayer)
                    {
                        BasePlayer target = (BasePlayer) closestEntity;
                        PrintToChat(player, Lang("ManualAdd", player.UserIDString, target.displayName, target.UserIDString));

                        PlayerList.Add(target.UserIDString);
                        timer.Once(DC_DelayInSeconds, () =>
                        {
                            PrintWarning(Lang("TimerComplete", null, target.UserIDString, target.displayName));
                            PlayerList.Remove(target.UserIDString);
                        });
                    }                    
                }
            }
        }

        private void cmdDCP_Remove(BasePlayer player, string command, string[] args)
        {
            if (IsAdmin(player))
            {
                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, 10f))
                {
                    BaseEntity closestEntity = hit.GetEntity();
                    if (closestEntity is BasePlayer)
                    {
                        BasePlayer target = (BasePlayer)closestEntity;
                        PrintToChat(player, Lang("ManualRemove", player.UserIDString, target.displayName, target.UserIDString));
                        PlayerList.Remove(target.UserIDString);
                    }
                }
            }
        }

        private void cmdDCP_List(BasePlayer player, string command, string[] args)
        {
            if (IsAdmin(player))
            {
                PrintToChat(player, Lang("ListTitle", player.UserIDString));
                foreach (string item in PlayerList)
                {
                    SendReply(player, item);
                }
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            PrintWarning(Lang("PlayerDisconnected", null, player.UserIDString, player.displayName));
            PlayerList.Add(player.UserIDString);
            timer.Once(DC_DelayInSeconds, () =>
            {
                PrintWarning(Lang("TimerComplete", null, player.UserIDString, player.displayName));
                PlayerList.Remove(player.UserIDString);
            });
        }

        bool CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            if(Time.realtimeSinceStartup < Start_DelayInSeconds)
            {
                PrintWarning(Lang("LogViolation_ServerStart", null, looter.UserIDString, looter.displayName, target.UserIDString, target.displayName));
                PrintToChat(looter, Lang("ServerStart", looter.UserIDString, Start_DelayInSeconds));
                return false;
            }
            else if (PlayerList.Contains(target.UserIDString))
            {
                PrintWarning(Lang("LogViolation_DC", null, looter.UserIDString, looter.displayName, target.UserIDString, target.displayName));
                PrintToChat(looter, Lang("NotAllowed", looter.UserIDString, DC_DelayInSeconds));
                return false;
            }
            else return true;
        }
    }
}
