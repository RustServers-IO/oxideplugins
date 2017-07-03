/*<summary> Plugin RemoveData:
Permissions RemoveData:
Admin rcon or permission removedata.admin

Commands Players:
/infodate => Information of dates.

Commands Admins:
/aremove cleanadata => Clean all data.
/aremove data (PlayerNameOrID) => Remove a player data.
/aremove banned => Remove players banned of data.
/aremove objects (PlayerNameOrId) => Remove objects a player.
/infodate (PlayerNameOrId) => Date information for a player.
/removedata removefordays => Enable disable remove for days default true.
/removedata removeobjects => Enable disabler remove objects default true.
/removedata removebanned => Enable disable remove players banned default true.
/removedata days (Day/s Default 5) => Choose day/s the player does not connect remove account.
</summary>*/
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using System;


namespace Oxide.Plugins{
	[Info("RemoveData", "Portugama ", "0.0.2")]
	class RemoveData : RustLegacyPlugin{


		static DateTime time = DateTime.Now;
		const string permissionRemoveData		= "removedata.admin";
		static string chatName					= "RemoveData";
		static bool removeAccountsForDays		= true;
		static bool removeStructuresObjects		= true;
		static bool removePlayersBanned			= true;
		static int daysRemoveAccounts			= 5;


		StoredData  data;
		public class StoredData{
			public string name {get; set;}
			public string lastDate {get; set;}
			public string firstDate {get; set;}
			public TimeSpan timePlayed {get; set;}
		}


		StoredData GetCreatPlayerData(string ID){
			if(!Data.TryGetValue(ID, out data)){
				data = new StoredData();
				Data.Add(ID, data);
			}
			return data;
		}


		string GetPlayerIdData(string args){
			foreach (var pair in Data){
				if(pair.Value.name.ToLower() == args.ToLower() || pair.Key == args)
				return pair.Key;
			}
			return null;
		}


		//Mensagens lang api.
		string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId); 
		void LoadDefaultMessages(){
			var message = new Dictionary<string, string>{
				{"NoHaveAcess", "You are [color red]not allowed [color clear]to use this command."},
				{"PlayerNotFound", "Player [color red]not [color clear]found!"},
				{"PlayerDataNotFound", "Player does [color red]not exist [color clear]or be registered!"},
				{"HelpsAdmins", "========================== [color lime]Helps Admins [color clear]=========================="},
				{"HelpsAdmins1", "Use /aremove cleanadata => Clean all data."},
				{"HelpsAdmins2", "Use /aremove data (PlayerNameOrID) => Remove a player data."},
				{"HelpsAdmins3", "Use /aremove banned => Remove players banned of data."},
				{"HelpsAdmins4", "Use /aremove objects (PlayerNameOrId) => Remove objects a player."},
				{"HelpsAdmins5", "Use /infodate (PlayerNameOrId) => Date information for a player."},
				{"HelpsAdmins6", "Use /removedata removefordays => Enable disable remove for days."},
				{"HelpsAdmins7", "Use /removedata removeobjects => Enable disabler remove objects."},
				{"HelpsAdmins8", "Use /removedata removebanned => Enable disable remove players banned."},
				{"HelpsAdmins9", "Use /removedata days (Day/s) => Choose days remove data."},
				{"HelpsAdmins10", "================================================================"},
				{"InfoDates", "=============== [color lime]Info Date [color clear]==============="},
				{"InfoDates1", "Date Of [color red]{0}"},
				{"InfoDates2", "Time played [color lime]{0}"},
				{"InfoDates3", "Date now [color lime]{0}"},
				{"InfoDates4", "Date of first connection [color lime]{0}"},
				{"InfoDates5", "Last cunnection [color lime]{0}"},
				{"InfoDates6", "Required connect [color red]{0}"},
				{"InfoDates7", "======================================"},
				{"AdminRemove", "Use /aremove (data Or banned Or objects Or cleanadata) => Cmds admin remove."},
				{"AdminRemove1", "Use /aremove player (PlayerNameOrId) => Remove player."},
				{"AdminRemove2", "Success removed player {0}."},
				{"AdminRemove3", "Use /aremove objects (PlayerNameOrId) => Remove objects a player."},
				{"AdminRemove4", "Success removed all objects {0}"},
				{"AdminRemove5", "{0} players banisheds removed."},
				{"AdminRemove6", "[color red]Not have data [color clear]players baneds."},
				{"AdminRemove7", "Success data is clean."},
				{"RemoveData", "Remove accounts for days is now {0}."},
				{"RemoveData1", "Remove structures objects is now {0}."},
				{"RemoveData2", "Remove players banned is now {0}."},
				{"RemoveData3", "Day/s the player does not connect remove accounts is now {0}."}
			}; 
			lang.RegisterMessages(message, this);
		}


		static Dictionary<string, StoredData> Data = new Dictionary<string, StoredData>();
		void Loaded(){Data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, StoredData>>("Infos(RemoveData)");}
		void SaveData(){Interface.Oxide.DataFileSystem.WriteObject("Infos(RemoveData)", Data);}


		private void OnServerInitialized(){
			permission.RegisterPermission(permissionRemoveData, this);
			CheckCfg<string>("Settings: Chat Name", ref chatName);
			CheckCfg<bool>("Settings: Remove Accounts For Days", ref removeAccountsForDays);
			CheckCfg<bool>("Settings: Remove Structures Objects", ref removeStructuresObjects);
			CheckCfg<bool>("Settings: Remove Players Banned", ref removePlayersBanned);
			CheckCfg<int>("Settings: Days Remove Accounts", ref daysRemoveAccounts);
			SaveConfig();
			LoadDefaultMessages();
			if(removeAccountsForDays)
			RemovePlayerForTimeDays();
			if(removePlayersBanned){
				int players = RemoveBannedsPlayers();
				if(players > 0)
				Puts("Removed " + players + " player/s banished/s.");
			}
			Puts("Accounts " + Data.Count.ToString());
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
			if(permission.UserHasPermission(netuser.userID.ToString(), permissionRemoveData)) return true;
			return false;
		}


		private void OnPlayerConnected(NetUser netuser){
			string ID = netuser.userID.ToString();
			string Name = netuser.displayName.ToString();
			data = GetCreatPlayerData(ID);
			if(data.name != Name)
			data.name = Name;
			data.lastDate = time.ToString();
			if(data.firstDate == null)
			data.firstDate = time.ToString();
			SaveData();
		}


		private void OnPlayerDisconnected(uLink.NetworkPlayer netPlayer){
			NetUser netuser = netPlayer.GetLocalData() as NetUser;
			data = GetCreatPlayerData(netuser.userID.ToString());
			var seconds = netuser.SecondsConnected();
			TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
			data.timePlayed = data.timePlayed + timeSpan;
			data.lastDate = time.ToString();
			SaveData();
		}


		int RemoveBannedsPlayers(){ 
			int banned = 0;
			foreach(var i in Data){
				if(BanList.Contains(Convert.ToUInt64(i.Key))){
					banned++;
					RemovePlayerData(i.Key);
				}
			}
			return banned;
		}


		void RemovePlayerData(string ID){
			Core.Interface.CallHook("OnRemovePlayerData", ID);
			if(removeStructuresObjects);
			RemoveStructuresObjects(ID);
			Data.Remove(ID);
			SaveData();
		}


		void RemovePlayerForTimeDays(){
			foreach (var pair in Data.ToList()){
				if(pair.Value.lastDate == null){
					pair.Value.lastDate = time.ToString();
					SaveData();
					continue;
				}
				DateTime lastDateUser = DateTime.Parse(pair.Value.lastDate);
				DateTime date = lastDateUser.AddDays(daysRemoveAccounts);
				int result = DateTime.Compare(date, time);
				if(result < 0){
					Puts("Removed player " + pair.Value.name + " for not coming the server for " + daysRemoveAccounts + " day/s.");
					RemovePlayerData(pair.Key);
				}
			}
		}


		void RemoveStructuresObjects(string ID){
			foreach(StructureComponent comp in UnityEngine.Resources.FindObjectsOfTypeAll<StructureComponent>()){
				var structure = comp.GetComponent<StructureComponent>();
				var master = structure._master;
				if(master == null)continue;
				string ownerID = master.ownerID.ToString();
				if(ID == ownerID)
				TakeDamage.KillSelf(comp.GetComponent<IDMain>());
			}
			foreach(DeployableObject comp in UnityEngine.Resources.FindObjectsOfTypeAll<DeployableObject>()){
				var Object = comp.GetComponent<DeployableObject>();
				var carrier = Object._carrier;
				if(carrier != null)continue;
				string ownerID = Object.ownerID.ToString();
				if(ID == ownerID)
				TakeDamage.KillSelf(comp.GetComponent<IDMain>());
			}
		}


		private void HelpsAdmins(NetUser netuser){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID)); return;}
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins1", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins2", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins3", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins4", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins5", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins6", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins7", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins8", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins9", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins10", ID));
		}


		[ChatCommand("infodate")]
		private void cmdsInfoDates(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			string tragetID = ID;
			if(args.Length > 0){
				if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID)); return;}
				NetUser tragetUser = rust.FindPlayer(args[0]);
				if(tragetUser == null){
					tragetID =  GetPlayerIdData(args[0].ToString());
					if(tragetID == null){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("NotFoundPlayerOrData", ID), args[1]));return;}
				}
				else
				tragetID = tragetUser.userID.ToString();
			}
			data = GetCreatPlayerData(tragetID);
			DateTime userLastDate = DateTime.Parse(data.lastDate);
			DateTime date = userLastDate.AddDays(daysRemoveAccounts);
			rust.SendChatMessage(netuser, chatName, GetMessage("InfoDates", ID));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("InfoDates1", ID), data.name));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("InfoDates2", ID), data.timePlayed));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("InfoDates3", ID), time.ToString()));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("InfoDates4", ID), data.firstDate));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("InfoDates5", ID), data.lastDate));
			if(removeAccountsForDays)
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("InfoDates6", ID), date.ToString()));
			rust.SendChatMessage(netuser, chatName, GetMessage("InfoDates7", ID));
		}


		[ChatCommand("aremove")]
		private void cmdsAdminRemove(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID)); return;}
			if(args.Length == 0){HelpsAdmins(netuser);return;}
			string tragetID = null;
			NetUser tragetUser = null;
			switch (args[0].ToLower()){
				case "data":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("AdminRemove1", ID));return;}
					tragetUser = rust.FindPlayer(args[1]);
					if(tragetUser == null){
						tragetID =  GetPlayerIdData(args[1].ToString());
						if(tragetID == null){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("NotFoundPlayerOrData", ID), args[1]));return;}
					}
					else
					tragetID = tragetUser.userID.ToString();
					RemovePlayerData(tragetID);
					rust.Notice(netuser, string.Format(GetMessage("AdminRemove2", ID), args[1]));
					break;
				case "objects":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("AdminRemove3", ID));return;}
					tragetUser = rust.FindPlayer(args[1]);
					if(tragetUser == null){
						tragetID =  GetPlayerIdData(args[1].ToString());
						if(tragetID == null){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("NotFoundPlayerOrData", ID), args[1]));return;}
					}
					else
					tragetID = tragetUser.userID.ToString();
					RemoveStructuresObjects(tragetID);
					rust.Notice(netuser, string.Format(GetMessage("AdminRemove4", ID), args[1]));
					break;
				case "banned":
					int banished = RemoveBannedsPlayers();
					if(banished > 0)
					rust.Notice(netuser, string.Format(GetMessage("AdminRemove5", ID), banished));
					else
					rust.SendChatMessage(netuser, chatName, GetMessage("AdminRemove6", ID));
					break;
				case "cleanadata":
					foreach(var pair in Data)
					RemovePlayerData(pair.Key);
					rust.Notice(netuser, GetMessage("AdminRemove7", ID));
					break;
				default:{
					HelpsAdmins(netuser);
					break;
				}
			}
		}


		[ChatCommand("removedata")]
		private void cmdsRemoveData(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID)); return;}
			if(args.Length == 0){HelpsAdmins(netuser);return;}
			switch (args[0].ToLower()){
				case "removefordays":
					if(removeAccountsForDays)
					removeAccountsForDays = false;
					else
					removeAccountsForDays = true;
					Config["Settings: Remove Accounts For Days"] = removeAccountsForDays;
					rust.Notice(netuser, string.Format(GetMessage("RemoveData", ID), removeAccountsForDays));
					break;
				case "removeobjects":
					if(removeStructuresObjects)
					removeStructuresObjects = false;
					else
					removeStructuresObjects = true;
					Config["Settings: Remove Structures Objects"] = removeStructuresObjects;
					rust.Notice(netuser, string.Format(GetMessage("RemoveData1", ID), removeStructuresObjects));
					break;
				case "removebanned":
					if(removePlayersBanned)
					removePlayersBanned = false;
					else
					removePlayersBanned = true;
					Config["Settings: Remove Players Banned"] = removePlayersBanned;
					rust.Notice(netuser, string.Format(GetMessage("RemoveData2", ID), removePlayersBanned));
					break;
				case "days":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins9", ID));return;}
					if(!int.TryParse(args[1], out daysRemoveAccounts)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins9", ID));return;}
					Config["Settings: Days Remove Accounts"] = daysRemoveAccounts;
					rust.Notice(netuser, string.Format(GetMessage("RemoveData3", ID), daysRemoveAccounts));
					break;
				default:{
					HelpsAdmins(netuser);
					break;
				}
			}
			SaveConfig();
		}
	} 
 }
