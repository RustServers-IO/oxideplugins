// Reference: UnityEngine.UI

using System;
using System.Collections.Generic;
using uLink;

namespace Oxide.Plugins
{

    [Info("Message of the day", "Gray - GrayHeartGames", 1.0)]
    public class MOTD : HurtworldPlugin
    {



        protected override void LoadDefaultConfig()
        {

            PrintWarning("Creating a new configuration file.");
            Config["MOTDSettings", "MOTDTitle"] = "MOTD";
            Config["MOTDSettings", "MOTDColor"] = "#ff9933";
            Config["MOTDmessages", "1"] = "Welcome to GrayHeartGames";
            Config["MOTDmessages", "2"] = "Join out TeamSpeak at Voice.GrayHeartGames.com";
            SaveConfig();

        }


        private void OnPlayerConnected(PlayerIdentity identity, NetworkPlayer player, NetworkMessageInfo info, string command, string[] args)
        {

            string MOTDColor = Config["MOTDSettings", "MOTDColor"].ToString();
            string MOTDTitle = Config["MOTDSettings", "MOTDTitle"].ToString();

            ChatManager.Instance?.AppendChatboxServerSingle("<color=" + MOTDColor + ">" + MOTDTitle + "</color> " + Config["MOTDmessages","1"], player);

        }


        [ChatCommand("motd")]
        void MotdCommand(PlayerIdentity identity, NetworkMessageInfo info, string command, string[] args)
        {

            string MOTDColor = Config["MOTDSettings", "MOTDColor"].ToString();
            string MOTDTitle = Config["MOTDSettings", "MOTDTitle"].ToString();

            ChatManager.Instance?.AppendChatboxServerSingle("<color=" + MOTDColor + ">" + MOTDTitle + "</color> " + Config["MOTDmessages", "1"], info.sender);

        }



    }

}
