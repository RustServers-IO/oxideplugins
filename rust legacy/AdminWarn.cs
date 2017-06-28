using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using System;

namespace Oxide.Plugins{
        [Info("AdminWarn", "mcnovinho08", "1.0.1")]
        class AdminWarn : RustLegacyPlugin{

	    string ativado = "Actived";
	    string desativado = "Disabled";
		
		static string chatPrefix = "AdminWarn";
		
		static bool AdminSystem = true;
		
		static int MaxWarn = 3;
		
		const string permiAdmin = "adminwarn.use";		
	  
        void OnServerInitialized(){
		CheckCfg<string>("Settings: Chat Prefix:", ref chatPrefix);
		CheckCfg<int>("Settings: Max Warn:", ref MaxWarn);
		CheckCfg<bool>("Settings: System Status:", ref AdminSystem);
		CheckCfg<string>("Settings: ChatPrefix Enabled:", ref ativado);
		CheckCfg<string>("Settings: ChatPrefix Disabled:", ref desativado);
	    Lang();
		SaveConfig();
        }

		protected override void LoadDefaultConfig(){} 
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}
	
        void Lang(){
			
			// english
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"NoPermission", "You are not allowed to use this command!"},
				{"SystemOFF", "The System is currently off!"},
				{"AdminWarnMSG", "Wrong Command, Use /adw - to see commands!"},
				{"AdminWarnMSG1", "Player not found, or not online!"},
				{"AdminWarnMSG2", "Prefix changed to {0} successfully!"},
				{"AdminWarnMSG3", "The system is currently: {0}!"},
				{"AdminWarnMSG4", "Player [color orange] {0} [color clear] was [color red] Warned [color clear] administrator [color orange] {1}!"},
				{"AdminWarnMSG5", "You cleared everyone's warnings!"},
				{"AdminWarnMSG6", "You have successfully wiped warnings from {0}!"},
				
				{"AdminWarnBan", "Player [color orange] {0} [color clear] was banned, for raising the warning limit!"},
				
				{"DadosNull", "There are no Admin Warn data!"},
				{"AdminP", "Player: {0}, ID: {1}, Warns: {2}/{3"},
				{"AdminP1", "Player: [color orange]{0} [color clear]has no warnings"},
				{"AdminP2", "You can not tell yourself"},
				
				{"AdminHelp", "=-=-=-=-=-=-=-=-= Commands =-=-=-=-=-=-=-=-="},
				{"AdminHelp1", "Use /warn <playername> - To warn the player"},
				{"AdminHelp2", "Use /awad <chattag> <new prefix> - To change the Plugin Prefix"},
				{"AdminHelp3", "Use /awad <onof> - To turn the system on or off"},
				{"AdminHelp4", "Use /awad <clear all> - To clear the plugin data!"},
				{"AdminHelp5", "Use /awad <clear player <playername>> - To clear player prompts!"},
				{"AdminHelp6", "Use /awad <wlog> - Check players with warnings!"},
				{"AdminHelp7", "Use /wplayer <playername> - Check if the player has warnings, and if so how many!"},
				{"AdminHelp8", "Use /adw - To view the plugin commands!"}


			}, this);

			// brazilian
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"NoPermission", "Você não tem permissão para usar este comando!"},
				{"SystemOFF", "O Sistema se encontra atualmente desligado!"},
				{"AdminWarnMSG", "Comando Errado, Use /adw - para ver os comandos!"},
				{"AdminWarnMSG1", "Jogador não encontrado, ou não esta online!"},
				{"AdminWarnMSG2", "Prefix alterado para {0} com sucesso!"},
				{"AdminWarnMSG3", "O sistema se encontra atualmente: {0}!"},
				{"AdminWarnMSG4", "Player [color orange]{0} [color clear]foi [color red]Avisado [color clear]pelo administrador [color orange]{1}!"},
				{"AdminWarnMSG5", "Você limpou os avisos de todo mundo!"},
				{"AdminWarnMSG6", "Você limpou os avisos de {0} com sucesso!"},
				
				{"AdminWarnBan", "Player [color orange]{0} [color clear]foi banido, por alçancar o limite de avisos!"},
				
				{"DadosNull", "Não existe dados do AdminWarn!"},
				{"AdminP", "Player: {0}, ID: {1}, Warns: {2}/{3"},
				{"AdminP1", "Player: [color orange]{0} [color clear]não possui avisos"},
				{"AdminP2", "Você não pode avisar a si proprio"},
				
				{"AdminHelp", "=-=-=-=-=-=-=-=-= Commands =-=-=-=-=-=-=-=-="},
				{"AdminHelp1", "Use /warn <playername> - para avisar o jogador"},
				{"AdminHelp2", "Use /awad <chattag> <new prefix> - Para alterar o Prefix do Plugin"},
				{"AdminHelp3", "Use /awad <onof> - para ligar ou desligar o sistema"},
				{"AdminHelp4", "Use /awad <clearall> - para limpar os dados do plugin!"},
				{"AdminHelp5", "Use /awad <player <playername>> - para limpar os avisos do jogador!"},
				{"AdminHelp6", "Use /awad <wlog> - verificar os jogadores com avisos!"},
				{"AdminHelp7", "Use /wplayer <playername> - verificar se o jogador possui avisos, e se sim quantos!"},
				{"AdminHelp8", "Use /adw - Para ver os comandos do plugin!"}

			}, this, "pt-br");
			return;
        }		
	  
		private Core.Configuration.DynamicConfigFile Data;
		void LoadData(){PlayerD = Interface.GetMod().DataFileSystem.ReadObject<PlayerData>("AdminWarn.Players");}
		void SaveData(){Interface.GetMod().DataFileSystem.WriteObject("AdminWarn.Players", PlayerD);}
		void OnServerSave(){SaveData();}
		void Unload(){SaveData();}
		void Loaded(){LoadData();}

		PlayerData PlayerD;
		class PlayerData
		{
			public List<string> PlayerInfo = new List<string>();
			public Dictionary<ulong, int> Warns = new Dictionary<ulong, int>();
		}  		
		
		[ChatCommand("warn")]
		void cmdWarn(NetUser netuser, string command, string[] args)
		{
		 string ID = netuser.userID.ToString();
		 if (!AcessAdmin(netuser)) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("NoPermission", this, ID)); return; }
		 if (!AdminSystem) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("SystemOFF", this, ID)); return; }
		 if (args.Length == 0) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminWarnMSG", this, ID)); return; }
		 
		 NetUser targetuser = rust.FindPlayer(args[0]);
		 if (targetuser == null) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminWarnMSG1", this, ID)); return; }
		 if (netuser == targetuser) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminP2", this, ID)); return; }
		 
		 string Name = targetuser.displayName;
		 string NameAdmin = netuser.displayName;
		 
		 AddWarn(targetuser);
		 MensagemWarn(Name, NameAdmin);
		 Punir(targetuser);
		}
		
		[ChatCommand("wplayer")]
		void cmdWPlayer(NetUser netuser, string command, string[] args)
		{
		 string ID = netuser.userID.ToString();
          NetUser target = rust.FindPlayer(args[0]);
		  if (target == null) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminWarnMSG1", this, ID)); return; }		
		
		 string NameT = target.displayName;
		 string IDT = target.userID.ToString();
		 
		 if (PlayerD.Warns.ContainsKey(target.userID)) 
	     {
	     int Avisos = Convert.ToInt32(PlayerD.Warns[target.userID]);
		 rust.SendChatMessage(netuser, chatPrefix, string.Format(lang.GetMessage("AdminP", this, ID), NameT, IDT, Avisos, MaxWarn));		 
		 }
		 else{
		 rust.SendChatMessage(netuser, chatPrefix, string.Format(lang.GetMessage("AdminP1", this, ID), NameT)); }
						
		}
		
		
		[ChatCommand("awad")]
		void cmdAdminCommands(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			ulong netuserid = netuser.userID;
            if (!AcessAdmin(netuser)) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("NoPermission", this, ID)); return; }
		    if(args.Length == 0) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminWarnMSG", this, ID)); return; }
			switch(args[0].ToLower()){
				case "chattag":
					if (args.Length < 2) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminWarnMSG", this, ID)); return; }
					chatPrefix = args[1].ToString();
					Config["Settings: Chat Prefix:"] = chatPrefix;
					rust.Notice(netuser, string.Format(lang.GetMessage("AdminWarnMSG2", this, ID), chatPrefix));
					break;
				case "onof":
					if(AdminSystem){
						AdminSystem = false;
						rust.BroadcastChat(chatPrefix, string.Format(lang.GetMessage("AdminWarnMSG3", this), desativado));
					}
					else{
						AdminSystem = true;
						rust.BroadcastChat(chatPrefix, string.Format(lang.GetMessage("AdminWarnMSG3", this), ativado));
					}
					Config["Settings: System Warns"] = AdminSystem;
					break;
				 case "clearall":
				 rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminWarnMSG5", this, ID));
				 ClearAll();
				 break;	
				 case "wlog":
				  string m = "";
			      foreach (string pr in PlayerD.PlayerInfo){
				  m = pr;
			      }
				  if (m == ""){
					rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("DadosNull", this, ID));
				  }
				  else {
				  rust.SendChatMessage(netuser, chatPrefix, string.Format(lang.GetMessage("{0}", this, ID), m)); }
				 break;
				 case "player":
		         NetUser targetuser = rust.FindPlayer(args[1]);
		         if (targetuser == null) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminWarnMSG1", this, ID)); return; }
				 //
				 Playering(targetuser, false);
				 PlayerD.Warns.Remove(targetuser.userID);
				 SaveData();
				 //
				 rust.SendChatMessage(netuser, chatPrefix, string.Format(lang.GetMessage("AdminWarnMSG6", this, ID), targetuser.displayName));
				 break;
				default:{
					HelpAdmins(netuser);
					break;
				}
			}
			SaveConfig();
		}
		
		[ChatCommand("adw")]
		void ComandoAdmin2(NetUser netuser, string command, string[] args)
		{
		string ID = netuser.userID.ToString();
        if (!AcessAdmin(netuser)) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("NoPermission", this, ID)); return; }
		HelpAdmins(netuser);
		}
		
		void HelpAdmins(NetUser netuser)
		{
		 string ID = netuser.userID.ToString();
		 rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminHelp", this, ID));
		 rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminHelp1", this, ID));
		 rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminHelp2", this, ID));
		 rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminHelp3", this, ID));
		 rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminHelp4", this, ID));
		 rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminHelp5", this, ID));
		 rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminHelp6", this, ID));
		 rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminHelp7", this, ID));
		 rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminHelp7", this, ID));
		 rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("AdminHelp", this, ID));
		}
		
			
		void MensagemWarn(string P, string PI)
		{
		  rust.BroadcastChat(chatPrefix, string.Format(lang.GetMessage("AdminWarnMSG4", this), P, PI));
		}
		
				
		void AddWarn(NetUser netuser) 
		{
			string ID = netuser.userID.ToString();	
			ulong userLong = netuser.userID;
			string Name = netuser.displayName;
			//
		    if(PlayerD.Warns.ContainsKey(userLong)) {
			Playering(netuser, false);
			PlayerD.Warns[userLong] += 1;
			Playering(netuser, true);
			}
			else {
        	PlayerD.Warns[userLong] = 1;
            Playering(netuser, false);
            Playering(netuser, true);
			}
			//
			SaveData();
		}
		
		void Playering(NetUser netuser, bool ff)
		{
			string ID = netuser.userID.ToString();
		    int Avisos = Convert.ToInt32(PlayerD.Warns[netuser.userID]);
			//
			string f = "";
		    f = string.Format("PlayerName: [color red]"+netuser.displayName+"[color clear] ID: [color red]"+ID+ "[color clear] Warns: [color red]"+Avisos+"[color clear]/[color red]"+MaxWarn);
			if (ff)
            PlayerD.PlayerInfo.Add(f);
            else
            PlayerD.PlayerInfo.Remove(f); 			
		}
		
		void ClearAll()
		{
			PlayerD.PlayerInfo.Clear();
			PlayerD.Warns.Clear();
			SaveData();
		}
		
		void Punir(NetUser netuser)
		{
		  int Avisos = Convert.ToInt32(PlayerD.Warns[netuser.userID]);
		  string Name = netuser.displayName;
		  if (Avisos == MaxWarn)
		  {
			  rust.BroadcastChat(chatPrefix, string.Format(lang.GetMessage("AdminWarnBan", this), Name));
			  timer.Once(1f, () => {
			  netuser.Ban();
			  netuser.Kick(NetError.Facepunch_Kick_Ban, true);
              Playering(netuser, false);
		      PlayerD.Warns.Remove(netuser.userID);
			  SaveData();
			  });
		  }

		}
			
	 	bool AcessAdmin(NetUser netuser){
		if(netuser.CanAdmin())return true; 
		if(permission.UserHasPermission(netuser.userID.ToString(), permiAdmin))return true;
		return false;
		}	

    }
}		