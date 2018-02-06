using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using System;

// CRIADOR: MCNOVINHO08
// CREATOR: MCNOVINHO08
// VERSION : 1.0.7
// DESCRIPTION: A SMALL BUT VERY EFFECTIVE SYSTEM OF WARNINGS, INVESTING BANIR THE DIRECT PLAYER WHY NOT SAY IT.

namespace Oxide.Plugins{
        [Info("AdminWarn", "mcnovinho08", "1.0.7")]
        class AdminWarn : RustLegacyPlugin{


        static string chatPrefix;
        static bool WarnSystem;

        static int WarnMax;

        static string permissionWarn;
        static string permissionAdministration;
        static string permissionVerification;

        static bool commandWarn;
        static bool commandAdmin;
        static bool commandCheck;

        void OnServerInitialized()
        {
            SetupConfig();
            SetupPermissions();
            Lang();
            SetupChatCommands();

            return;
        }

        void SetupConfig()
        {
            permissionWarn = Config.Get<string>("Settings", "permissionWarn");
            permissionAdministration = Config.Get<string>("Settings", "permissionAdministration");
            permissionVerification = Config.Get<string>("Settings", "permissionVerification");

            chatPrefix = Config.Get<string>("Settings", "chatPrefix");
            WarnSystem = Config.Get<bool>("Settings", "WarnSystem");

            WarnMax = Config.Get<int>("Settings", "WarnMax");
            commandWarn = Config.Get<bool>("Settings", "commandWarn");
            commandAdmin = Config.Get<bool>("Settings", "commandAdmin");
            commandCheck = Config.Get<bool>("Settings", "commandCheck");

        }

        void SetupPermissions()
        {
            permission.RegisterPermission(permissionAdministration, this);
            permission.RegisterPermission(permissionVerification, this);
            permission.RegisterPermission(permissionWarn, this);
            return;
        }

        protected override void LoadDefaultConfig()
        {
            Config["Settings"] = new Dictionary<string, object>
            {
                {"permissionWarn", "adminwarn.warn"},
                {"permissionAdministration", "adminwarn.all"},
                {"permissionVerification", "adminwarn.verification"},

                {"chatPrefix", "AdminWarn"},
                {"WarnMax", 3},
                {"WarnSystem", true},
                {"commandWarn", true},
                {"commandAdmin", true},
                {"commandCheck", true}
            };
        }

        void SetupChatCommands()
        {
            if (commandWarn)
                cmd.AddChatCommand("warn", this, "cmdWarnPlayer");

            if (commandAdmin)
                cmd.AddChatCommand("awad", this, "cmdAdminCommands");

            if (commandCheck)
                cmd.AddChatCommand("wplayer", this, "cmdVerificarPlayer");
        }

        private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}

        string GetMessage(string key, string steamid = null) => lang.GetMessage(key, this, steamid);
        void Lang(){

            // english
            lang.RegisterMessages(new Dictionary<string, string>
            {

                {"FailIcon", "✘"},
                {"SucessIcon", "✔"},

                {"Online", "Online"},
                {"Offline", "Offline"},

                {"NoPermission", "You are not allowed to use this command!"},
                {"InvalidPlayer", "Invalid player!"},
                {"SystemStatus", "The warning system is {0}"},
                {"Space", "=-=-=-=-=-=-=-= AdminWarn =-=-=-=-=-=-=-=" },

                {"WarnYou", "You can not tell yourself."},
                {"WarnPlayer", "[color orange]{0} [color clear] was warned by the administrator [color orange] {1}"},
                {"WarnPlayerPrivate", "You have been warned by the administrator, {0} / {1}"},
                {"WarnPunition", "[color orange]{0}[color clear] was banned for reaching the maximum limit of notices!"},
                {"WarnInformations", "PlayerName: [color orange]{0}[color clear], PlayerID:[color orange] {1}[color clear], PlayerIP: [color orange]{2}[color clear], Warns: [color orange]{3}[color clear]/[color orange]{4}"},
                {"WarnsInvalid", "{0} Could not be found in the database!"},
                {"WarnClear", "All player warnings have been removed!"},
                {"WarnClearPlayer", "All warnings from player {0} have been removed!"},
                {"WarnsNull", "The player has no warnings!"},

                {"AdminCommands", "Use: /warn < playerName > - To warn the player"},
                {"AdminCommands2", "Use: /wplayer < playerName > - To check the player's warnings"},
                {"AdminCommands3", "Use: /awad <onof | player | clearall > - Admin commands "}

            }, this);

            // pt-br
            lang.RegisterMessages(new Dictionary<string, string>
			{
				
                {"FailIcon", "✘"},
                {"SucessIcon", "✔"},

                {"Online", "Online"},
                {"Offline", "Offline"},

                {"NoPermission", "Você não tem permissão para usar este comando!"},
                {"InvalidPlayer", "Jogador invalido!"},
                {"SystemStatus", "O sistema de avisos esta {0}"},
                {"Space", "=-=-=-=-=-=-=-= AdminWarn =-=-=-=-=-=-=-=" },

                {"WarnYou", "Você não pode avisar a si proprio."},
                {"WarnPlayer", "[color orange]{0}[color clear] foi avisado pelo administrador [color orange]{1}"},
                {"WarnPlayerPrivate", "Você foi avisado pelo administrador, {0}/{1}"},
                {"WarnPunition", "[color orange]{0}[color clear] foi banido, por chegar ao limite maximo de avisos!"},
                {"WarnInformations", "PlayerName: [color orange]{0}[color clear], PlayerID:[color orange] {1}[color clear], PlayerIP: [color orange]{2}[color clear], Warns: [color orange]{3}[color clear]/[color orange]{4}"},
                {"WarnsInvalid", "{0} não foi encontrado no banco de dados!"},
                {"WarnClear", "Todos os avisos dos jogadores foram removidos!"},
                {"WarnClearPlayer", "Todos os avisos do jogador {0} foram removidos!"},
                {"WarnsNull", "O jogador não possui avisos!"},

                {"AdminCommands", "Use: /warn < playerName > - para avisar o jogador"},
                {"AdminCommands2", "Use: /wplayer < playerName > - para verificar os avisos do jogador"},
                {"AdminCommands3", "Use: /awad <onof | player | clearall > - comandos de administrador "}

            }, this, "pt_br");
			return;
        }

        Dictionary<string, PlayerWarns> WarnsPlayers = new Dictionary<string, PlayerWarns>();

        void Loaded() { WarnsPlayers = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PlayerWarns>>("AdminWarn"); }
        void SaveData() { Interface.Oxide.DataFileSystem.WriteObject("AdminWarn", WarnsPlayers); }

        PlayerWarns PlayerW;
        public class PlayerWarns
        {
            public string playerName { get; set; }
            public string playerID { get; set; }
            public string playerIP { get; set; }
            public int playerWarns { get; set; }
        }

        PlayerWarns GetPlayerData(string ID)
        {
            if (!WarnsPlayers.TryGetValue(ID, out PlayerW))
            {
                PlayerW = new PlayerWarns();
                WarnsPlayers.Add(ID, PlayerW);
            }
            return PlayerW;
        }

        void ADDWarn(NetUser netuser, NetUser target)
        {
            string ID = target.userID.ToString();
            var Ip = target.networkPlayer.externalIP;

            PlayerW = GetPlayerData(ID);
            
            if (PlayerW.playerID == null)
            {
                PlayerW.playerName = target.displayName;
                PlayerW.playerID = ID;
                PlayerW.playerIP = Ip;
                PlayerW.playerWarns++;
                rust.BroadcastChat(chatPrefix, string.Format(GetMessage("WarnPlayer"), target.displayName, netuser.displayName));
                rust.Notice(target, string.Format(GetMessage("WarnPlayerPrivate", ID), PlayerW.playerWarns, WarnMax), GetMessage("SucessIcon", ID));
            }
            else
            {
                PlayerW.playerWarns++;
                CheckWarns(target);
                rust.Notice(target, string.Format(GetMessage("WarnPlayerPrivate", ID), PlayerW.playerWarns, WarnMax), GetMessage("SucessIcon", ID));
                rust.BroadcastChat(chatPrefix, string.Format(GetMessage("WarnPlayer"), target.displayName, netuser.displayName));
            }
            SaveData();
        }


        void CheckWarns(NetUser netuser)
        {
            string ID = netuser.userID.ToString();
            var PlayerW = GetPlayerData(ID);

            if (PlayerW.playerWarns == WarnMax)
            {
                rust.BroadcastChat(chatPrefix, string.Format(GetMessage("WarnPunition"), netuser.displayName));
                timer.Once(0.1f, () =>
                {
                    netuser.Ban();
                    netuser.Kick(NetError.Facepunch_Kick_Ban, true);
                });
                SaveData();
            }
        }

        [ChatCommand("warn")]
        void cmdWarnPlayer(NetUser netuser, string command, string[] args)
        {
            string ID = netuser.userID.ToString();
            if (!WarnSystem) { rust.SendChatMessage(netuser, chatPrefix, string.Format(GetMessage("SystemStatus", ID), GetMessage("Offline", ID))); return; }

            ulong netUserID = netuser.userID;
            if (!(netuser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionWarn) || permission.UserHasPermission(netUserID.ToString(), permissionAdministration)))
            {
                rust.Notice(netuser, GetMessage("NoPermission", ID), GetMessage("FailIcon", ID));
                return;
            }

            NetUser target = rust.FindPlayer(args[0]);
            string IDt = target.userID.ToString();
            string targetName = target.displayName;

            if (IDt == null || targetName == null) { rust.SendChatMessage(netuser, chatPrefix, GetMessage("InvalidPlayer", ID)); return; }
            if (netuser == target) { rust.SendChatMessage(netuser, chatPrefix, GetMessage("WarnYou", ID)); return; }

            ADDWarn(netuser, target);
        }

        void CheckInformations(NetUser netuser, NetUser target)
        {
            string ID = netuser.userID.ToString();

            ulong netUserID = netuser.userID;
            if (!(netuser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionVerification) || permission.UserHasPermission(netUserID.ToString(), permissionAdministration)))
            {
                rust.Notice(netuser, GetMessage("NoPermission", ID), GetMessage("FailIcon", ID));
                return;
            }

            string IDt = target.userID.ToString();
            string targetName = target.displayName;
            if (target == null || IDt == null) { rust.SendChatMessage(netuser, chatPrefix, GetMessage("InvalidPlayer", ID)); return; }

            var data = IDt;
            if (WarnsPlayers.ContainsKey(IDt))
            {
                var informations = GetPlayerData(data);
                rust.SendChatMessage(netuser, chatPrefix, GetMessage("Space", ID));
                rust.SendChatMessage(netuser, chatPrefix, string.Format(GetMessage("WarnInformations", ID), informations.playerName, informations.playerID, informations.playerIP, informations.playerWarns, WarnMax));
                rust.SendChatMessage(netuser, chatPrefix, GetMessage("Space", ID));

            }
            else
            {
                rust.SendChatMessage(netuser, chatPrefix, string.Format(GetMessage("WarnsInvalid", ID), targetName));
            }
        }

        [ChatCommand("wplayer")]
        void cmdVerificarPlayer(NetUser netuser, string command, string[] args)
        {
            string ID = netuser.userID.ToString();
            if (!WarnSystem) { rust.SendChatMessage(netuser, chatPrefix, string.Format(GetMessage("SystemStatus", ID), GetMessage("Offline", ID))); return; }
            NetUser target = rust.FindPlayer(args[0]);
            CheckInformations(netuser, target);
        }
    		
		[ChatCommand("awad")]
		void cmdAdminCommands(NetUser netuser, string command, string[] args){
            string ID = netuser.userID.ToString();
            ulong netUserID = netuser.userID;

            if (!(netuser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionAdministration)))
            {
                rust.Notice(netuser, GetMessage("NoPermission", ID), GetMessage("FailIcon", ID));
                return;
            }

            if (args.Length == 0) { HelpAdmins(netuser); }
			switch(args[0].ToLower()){
				case "onof":
					if(WarnSystem){
						WarnSystem = false;
						rust.BroadcastChat(chatPrefix, string.Format(GetMessage("SystemStatus", ID), GetMessage("Offline", ID)));
					}
					else{
						WarnSystem = true;
                        rust.BroadcastChat(chatPrefix, string.Format(GetMessage("SystemStatus", ID), GetMessage("Online", ID)));
                    }
					break;
				 case "clearall":
				 rust.SendChatMessage(netuser, chatPrefix, GetMessage("WarnClear", ID));
                    WarnsPlayers.Clear();
                    SaveData();
                 break;	
				 case "player":
		         NetUser targetuser = rust.FindPlayer(args[1]);
		         if (targetuser == null) { rust.SendChatMessage(netuser, chatPrefix, GetMessage("InvalidPlayer", ID)); return; }
                    //
                    string tarID = targetuser.userID.ToString();

                    if (WarnsPlayers.ContainsKey(tarID))
                    {
                        WarnsPlayers.Remove(tarID);
                        rust.SendChatMessage(netuser, chatPrefix, string.Format(GetMessage("WarnClearPlayer", ID), targetuser.displayName));
                        SaveData();
                    }
                    else
                    {
                        rust.SendChatMessage(netuser, chatPrefix, GetMessage("WarnsNull", ID));
                    }
                    //
				 break;
				default:{
					HelpAdmins(netuser);
					break;
				}
			}
			SaveConfig();
		}
		
		void HelpAdmins(NetUser netuser)
		{
		 string ID = netuser.userID.ToString();
            rust.SendChatMessage(netuser, chatPrefix, GetMessage("AdminCommands", ID));
            rust.SendChatMessage(netuser, chatPrefix, GetMessage("AdminCommands2", ID));
            rust.SendChatMessage(netuser, chatPrefix, GetMessage("AdminCommands3", ID));

        }

    }
}		