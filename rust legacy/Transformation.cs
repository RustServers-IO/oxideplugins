using System.Collections.Generic;
using System;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;


namespace Oxide.Plugins {
		[Info("Transformation", "Portugama ", "0.0.1")]
		class Transformation : RustLegacyPlugin{ 


		[PluginReference] Plugin Kits;
		static string chatName					= "Transformation";
		static string nameKitGodMode			= "God Mode";
		static string nameGiveKit				= "Kit";
		static string nameGiveInventory			= "Inventory";
		const string permTransformationAdmin	= "transformation.admin";
		const string permTransformationUser		= "transformation.users";


		private readonly static Dictionary<string, ItemDataBlock> dataBock = new Dictionary<string, ItemDataBlock>();
		static Dictionary<string, object> kits = new Dictionary<string, object>();
		static Dictionary<string, object> saveInventory = new Dictionary<string, object>();


		string GetMessage(string key, string steamid = null) => lang.GetMessage(key, this, steamid);
		void LoadDefaultMessages(){
			var message = new Dictionary<string, string>{
				{"NoHaveAcess", "You are not [color red]allowed [color clear]to use this command."},
				{"PlayerNotFound", "Player [color red]{0} [color clear]not found!"},
				{"NoHaveKits", "Does not have any kit in kits!"},
				{"KitNotFound", "Kit [color red]{0} [color clear]not found in kits!"},
				{"TransformationHelps", "=================== [color lime]Help Transformation [color clear]==================="},
				{"TransformationHelps1", "Use /a (OptimumKitName) => Transformation admin god mode or player."},
				{"TransformationHelps2", "========================================================"},
				{"SeeNamesKits", "====== [color lime]Names Kits [color clear]======"},
				{"SeeNamesKits1", "Kit name [color red]{0}"},
				{"SeeNamesKits2", "======================"},
				{"AdminGiveKit", "Use /ag (PlayerName) (KitName) => Give to kit a player."},
				{"AdminGiveKit1", "{0} gived you kit {1}."},
				{"AdminGiveKit2", "Gived to {0} kit {1}."},
				{"Transformation", "Inventory back!"},
				{"Transformation1", "[color red]{0} [color Clear]enter the [color cyan]player mode[color Clear]!"},
				{"Transformation2", "Gived kit {0} to you."},
				{"Transformation3", "[color red]{0} [color Clear]enter the [color lime]admin god mode[color Clear]!"},
				{"AdminKits", "Use /adminkits (see || add || remove || clear) => Cmds admins cfgs kits."},
				{"AdminKits1", "Use /akits add (KitName) => Add kit to kits."},
				{"AdminKits2", "Success add kit {0} to kits."},
				{"AdminKits3", "Use /akits remove (KitName) => Remove kit in kits."},
				{"AdminKits4", "Success kit {0} removed to kits."},
				{"AdminKits5", "Success kits was cleaned!"}
			}; 
			lang.RegisterMessages(message, this);
		}


		void OnServerInitialized(){
			dataBock.Clear();
			foreach(var item in DatablockDictionary.All)
			dataBock.Add(item.name.ToLower(), item);
			permission.RegisterPermission(permTransformationUser, this);
			permission.RegisterPermission(permTransformationAdmin, this);
			CheckCfg<string>("Settings: Chat Name", ref chatName);
			CheckCfg<string>("Settings: Name Kit God Mode", ref nameKitGodMode);
			CheckCfg<string>("Settings: Name Give Kit", ref nameGiveKit);
			CheckCfg<string>("Settings: Name Give Inventory", ref nameGiveInventory);
			CheckCfg<Dictionary<string, object>>("Settings: Kits", ref kits);
			LoadDefaultMessages();
			SaveConfig();
		}


		protected override void LoadDefaultConfig(){} 
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}


		bool AcessUsers(NetUser netuser){
			var ID = netuser.userID.ToString();
			if(netuser.CanAdmin())return true;
			if(permission.UserHasPermission(ID, permTransformationAdmin))return true;
			if(permission.UserHasPermission(ID, permTransformationUser))return true;
			return false;
		}


		bool AcessAdmin(NetUser netuser){
			if(netuser.CanAdmin())return true; 
			if(permission.UserHasPermission(netuser.userID.ToString(), permTransformationAdmin))return true;
			return false;
		}


		Dictionary<string, object> GetDictionaryKit(NetUser netuser, string kitName, string args){
			var ID = netuser.userID.ToString();
			var newdict = new Dictionary<string, object>();
			if(netuser.playerClient == null || netuser.playerClient.rootControllable == null)return newdict;
			if(kitName == nameGiveKit){
				if(kits.Count == 0)return newdict;
				if(!kits.ContainsKey(args))return newdict;
				newdict = kits[args] as Dictionary<string, object>;
				return newdict;
			}
			else if(kitName == nameGiveInventory){
				if(!saveInventory.ContainsKey(ID))return newdict;
				newdict = saveInventory[ID] as Dictionary<string, object>;
				return newdict;
			}
			return newdict;
		}


		object GiveKit(NetUser netuser, string kit, string args){
			var kitConfig = GetDictionaryKit(netuser, kit, args);
			if(kitConfig.Count == 0)return false;
			var kitList = kitConfig["items"] as Dictionary<string, object>;
			if(kitList == null)return false;
			var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			inv.Clear();
			var wearList = kitList["wear"] as List<object>;
			var mainList = kitList["main"] as List<object>;
			var beltList = kitList["belt"] as List<object>;
			Inventory.Slot.Preference pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Armor,false,Inventory.Slot.KindFlags.Belt);
			if(wearList.Count > 0){
				pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Armor, false, Inventory.Slot.KindFlags.Belt);
				foreach (object items in wearList){
					object kk = true;
					foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>)
					GiveItem(inv, (string)pair.Key, (int)pair.Value, pref);
				}
			}
			if(mainList.Count > 0){
				pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Default, false, Inventory.Slot.KindFlags.Belt);
				foreach (object items in mainList){
					foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>)
					GiveItem(inv, (string)pair.Key, (int)pair.Value, pref);
				}
			}
			if(beltList.Count > 0){
				pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Belt, false, Inventory.Slot.KindFlags.Belt);
				foreach (object items in beltList){
					foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>)
					GiveItem(inv, (string)pair.Key, (int)pair.Value, pref);
				}
			}
			return true;
		}


		object GiveItem(Inventory inventory, string itemname, int amount, Inventory.Slot.Preference pref){
			if (!dataBock.ContainsKey(itemname)) return false;
			ItemDataBlock datablock = dataBock[itemname];
			inventory.AddItemAmount(dataBock[itemname], amount, pref);
			return true;
		}


		private void SeeNamesKits(NetUser netuser){
			string ID = netuser.userID.ToString();
			if(kits.Count == 0){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveKits", ID));return;}
			rust.SendChatMessage(netuser, chatName, GetMessage("SeeNamesKits", ID));
			foreach(var pair in kits)
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("SeeNamesKits1", ID), pair.Key));
			rust.SendChatMessage(netuser, chatName, GetMessage("SeeNamesKits2", ID));
		}


		[ChatCommand("thelp")]
		private void cmdTransformationHelps(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			rust.SendChatMessage(netuser, chatName, GetMessage("TransformationHelps", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("TransformationHelps1", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("AdminGiveKit", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("AdminKits", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("TransformationHelps2", ID));
		}


		[ChatCommand("ag")]
		private void cmdAdminGiveKit(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmin(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID)); return;} 
			if(kits.Count == 0){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveKits", ID));return;}
			if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("AdminGiveKit", ID));return;}
			NetUser user = rust.FindPlayer(args[0]);
			if(user == null){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("PlayerNotFound", ID), args[0].ToString()));return;}
			string kitName = args[1].ToString();
			if(kits.ContainsKey(kitName)){
				GiveKit(user, nameGiveKit, kitName);
				rust.Notice(user, string.Format(GetMessage("AdminGiveKit1", ID), netuser.displayName, kitName));
				rust.Notice(netuser, string.Format(GetMessage("AdminGiveKit2", ID), user.displayName, kitName));
			}
			else{
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("KitNotFound", ID), kitName));
				SeeNamesKits(netuser);
			}
		}


		[ChatCommand("a")]
		private void cmdTransformation(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessUsers(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID)); return;} 
			if(kits.Count == 0){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveKits", ID));return;}
			if(saveInventory.ContainsKey(ID)){
				GiveKit(netuser, nameGiveInventory, ID);
				saveInventory.Remove(ID);
				netuser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(false);
				rust.Notice(netuser, GetMessage("Transformation", ID));
				rust.BroadcastChat(chatName, string.Format(GetMessage("Transformation1"), netuser.displayName));
			}
			else{
				string kitName;
				if(args.Length > 0)
				kitName = args[0].ToString();
				else
				kitName = nameKitGodMode;
				if(!kits.ContainsKey(kitName)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("KitNotFound", ID), kitName));SeeNamesKits(netuser);return;}
				var getKit = Kits?.Call("GetNewKitFromPlayer", netuser);
				var kitItems = new Dictionary<string, object>();
				kitItems.Add("items", getKit);
				if(saveInventory.ContainsKey(ID))
				saveInventory.Remove(ID);
				saveInventory.Add(ID, kitItems);
				GiveKit(netuser, nameGiveKit, kitName);
				netuser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(true);
				rust.Notice(netuser, string.Format(GetMessage("Transformation2", ID), kitName));
				rust.BroadcastChat(chatName, string.Format(GetMessage("Transformation3"), netuser.displayName));
			}
		}


		[ChatCommand("adminkits")]
		private void cmdAdminKits(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmin(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID)); return;} 
			if(args.Length == 0){rust.SendChatMessage(netuser, chatName, GetMessage("AdminKits", ID));return;}
			string kitName;
			switch (args[0].ToLower()){
				case "see":
					SeeNamesKits(netuser);
					break;
				case "add":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("AdminKits1", ID));return;}
					kitName = args[1].ToString();
					var getKit = Kits?.Call("GetNewKitFromPlayer", netuser);
					var kitItems = new Dictionary<string, object>();
					kitItems.Add("items", getKit);
					if(kits.ContainsKey(kitName))
					kits.Remove(kitName);
					if(kits.Count == 0)
					kitName = nameKitGodMode;
					kits.Add(kitName, kitItems);
					rust.Notice(netuser, string.Format(GetMessage("AdminKits2", ID), kitName));
					break;
				case "remove":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("AdminKits3", ID));return;}
					kitName = args[1].ToString();
					if(kits.ContainsKey(kitName)){
						kits.Remove(kitName);
						rust.Notice(netuser, string.Format(GetMessage("AdminKits4", ID), kitName));
					}
					else
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("NoHaveKits", ID), kitName));
					break;
				case "clear":
					kits.Clear();
					rust.Notice(netuser, GetMessage("AdminKits5", ID));
					break;
				default:{
					rust.SendChatMessage(netuser, chatName, GetMessage("AdminKits", ID));
					break;
				}
			}
			Config["Settings: Kits"] = kits;
			SaveConfig();
		}
	}
}

