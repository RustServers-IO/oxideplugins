/*<summary> Plugin ReservedNames:
Permissions ReservedNames:
Admin rcon or permission reservednames.admin
Permissions Use:
reservednames.admin
reservednames.use

Commands Admins:
/reserved name => Enable or disable.
/reserved word => Enable or disable.
/reserved changename => Enable or disable.
/reservedata add (Name) => Add name.
/reservedata remove (Name) => Remove name.
</summary>*/
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;


namespace Oxide.Plugins{
	[Info("ReservedNames", "tugamano", "0.0.4")]
	[Description("Reserved Names or Resever Word of Names.")]
	class ReservedNames : RustLegacyPlugin{


		private const string permissionAdmin = "reservednames.admin";
		private const string permissionUse   = "reservednames.use";
		static string chatName		= "ReservedNames";
		static bool reservedName	= true;
		static bool changeName		= false;
		static bool reservedWord	= true;
		static float timeNotices	= 20;


		StoredData Data;
		class StoredData{
			public Dictionary<string, string> Names = new Dictionary<string, string>();
		}


		private void OnRemovePlayerData(string ID){
			if(Data.Names.ContainsKey(ID)){
				Data.Names.Remove(ID);
				SaveData();
			}
		}


		// Messages Lang API.
		string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
		void LoadDefaultMessages(){
			var message = new Dictionary<string, string>{
				{"NotHaveAcess", "You are not [color red]allowed [color clear]to use this command."},
				{"NotFoundPlayerData", "Player [color red]{0} [color clear]not found data!"},
				{"ChangeName"," Forbidden to change name! Should connect with your name {0}!"},
				{"ReservedName", "Name {0} reserved change you name!"},
				{"ReservedWord", "Word {0} reserved change you name!"},
				{"HelpAdmins", "======= [color lime]Helps {0} [color clear]======="},
				{"HelpAdmins1", "/reserved name => Enable or disable."},
				{"HelpAdmins2", "/reserved word => Enable or disable."},
				{"HelpAdmins3", "/reserved changename => Enable or disable."},
				{"HelpAdmins4", "/reservedata add (Name) => Add name."},
				{"HelpAdmins5", "/reservedata remove (Name) => Remove name."},
				{"HelpAdmins6", "================================="},
				{"ReservedCfg", "Success name {0} add to list reseved."},
				{"ReservedCfg1", "Success name: {0} removed."},
				{"Reserved", "Reserved names is now {0}."},
				{"Reserved1", "Reserved word is now {0}."},
				{"Reserved2", "Change name is now {0}."}
			};
			lang.RegisterMessages(message, this);
		}


		void LoadData(){Data = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("Reserved(Names)");}
		void SaveData(){Interface.GetMod().DataFileSystem.WriteObject("Reserved(Names)", Data);}
		void OnServerSave(){SaveData();}
		void Unload(){SaveData();}
		void Loaded(){LoadData();}


		private void OnServerInitialized(){
			CheckCfg<string>("Settings: Chat Name", ref chatName);
			CheckCfg<bool>("Settings: Reserved Name", ref reservedName);
			CheckCfg<bool>("Settings: Reserved Word", ref reservedWord);
			CheckCfg<bool>("Settings: Change Name", ref changeName);
			CheckCfg<float>("Settings: Time Notices", ref timeNotices);
			SaveConfig();
			permission.RegisterPermission(permissionAdmin, this);
			permission.RegisterPermission(permissionUse, this);
			LoadDefaultMessages();
		}


		protected override void LoadDefaultConfig(){} 
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}


		private bool AcessAdmins(NetUser netuser){
			if(netuser.CanAdmin())return true; 
			if(permission.UserHasPermission(netuser.playerClient.userID.ToString(), permissionAdmin)) return true;
			return false;
		}


		private bool AcessUsers(NetUser netuser){
			if(netuser.CanAdmin())return true; 
			if(permission.UserHasPermission(netuser.playerClient.userID.ToString(), permissionAdmin)) return true;
			if(permission.UserHasPermission(netuser.playerClient.userID.ToString(), permissionUse)) return true;
			return false;
		}


		private void OnPlayerConnected(NetUser netuser){
			timer.Once(3f, ()=>{
				if(netuser.playerClient == null)return;
				string Name = netuser.displayName.ToString();
				string ID = netuser.userID.ToString();
				if(!AcessUsers(netuser)){
					if(reservedName || reservedWord){
						foreach (var pair in Data.Names.ToList()){
							if(reservedName){
								if(pair.Value.ToLower() == Name.ToLower()){
									if(pair.Key != ID){
										rust.Notice(netuser, string.Format(GetMessage("ReservedName", ID), Name), "!", timeNotices);
										netuser.Kick(NetError.Facepunch_Kick_RCON, true);
										return;
									}
								}
							}
							if(reservedWord){
								if(Name.ToLower().Contains(pair.Value.ToLower())){
									if(pair.Key != ID){
										rust.Notice(netuser, string.Format(GetMessage("ReservedWord", ID), pair.Value), "!", timeNotices);
										netuser.Kick(NetError.Facepunch_Kick_RCON, true);
										return;
									}
								}
							}
						}
					}
					if(changeName){
						if(Data.Names.ContainsKey(ID)){
							if(Data.Names[ID] != Name){
								rust.Notice(netuser, string.Format(GetMessage("ChangeName", ID), Data.Names[ID]), "!", timeNotices);
								netuser.Kick(NetError.Facepunch_Kick_RCON, true);
								return;
							}
						}
					}
				}
				if(Data.Names.ContainsKey(ID)){
					if(Data.Names[ID] != Name){
						Data.Names[ID] = Name;
						SaveData();
					}
				}
				else if(!Data.Names.ContainsKey(ID)){
					Data.Names.Add(ID, Name);
					SaveData();
				}
			});
		} 


		private void cmdHelpAdmins(NetUser netuser){
			string ID = netuser.userID.ToString();
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("HelpAdmins", ID), chatName));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins1", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins2", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins3", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins4", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins5", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins6", ID));
		}


		[ChatCommand("reservedata")]
		void cmdAdminClear(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveAcess", ID)); return;} 
			if(args.Length == 0){cmdHelpAdmins(netuser);return;}
			switch (args[0].ToLower()){
			case "add":
				if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins4", ID));return;}
				string nameAdd = args[1].ToString();
				if(Data.Names.ContainsKey(nameAdd))
				Data.Names.Remove(nameAdd);
				Data.Names.Add(nameAdd, nameAdd);
				rust.Notice(netuser, string.Format(GetMessage("ReservedCfg", ID), nameAdd));
				SaveData();
				break;
			case "remove":
				if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins5", ID));return;}
				string name = args[1].ToString();
				foreach (var pair in Data.Names.ToList()){
					if(pair.Value.ToLower() == name.ToLower()){
						Data.Names.Remove(pair.Key);
						rust.Notice(netuser, string.Format(GetMessage("ReservedCfg1", ID), name));
						SaveData();
						return;
					}
				}
				rust.Notice(netuser, string.Format(GetMessage("NotFoundPlayerData", ID), name));
				break;
				default:{
					cmdHelpAdmins(netuser);
					break;
				}
			}
		}


		[ChatCommand("reserved")]
		void cmdReservedNames(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveAcess", ID)); return;} 
			if(args.Length == 0){cmdHelpAdmins(netuser);return;}
			switch (args[0].ToLower()){
			case "name":
				if(reservedName)
				reservedName = false;
				else
				reservedName = true;
				Config["Settings: Reserved Name"] = reservedName;
				rust.Notice(netuser, string.Format(GetMessage("Reserved", ID), reservedName));
				break;
			case "word":
				if(reservedWord)
				reservedWord = false;
				else
				reservedWord = true;
				Config["Settings: Reserved Word"] = reservedWord;
				rust.Notice(netuser, string.Format(GetMessage("Reserved1", ID), reservedWord));
				break;
			case "changename":
				if(changeName)
				changeName = false;
				else
				changeName = true;
				Config["Settings: Change Name"] = changeName;
				rust.Notice(netuser, string.Format(GetMessage("Reserved2", ID), changeName));
				break;
				default:{
					cmdHelpAdmins(netuser);
					break;
				}
			}
			SaveConfig();
		}
	}
}