using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;
using System;


namespace Oxide.Plugins{
	[Info("TeleportUsine", "mcnovinho08", "0.8.0")]
	[Description("Teleports To Usines")]

	class TeleportUsine : RustLegacyPlugin{

		[PluginReference]
		Plugin AdminControl;
		
		[PluginReference]
		Plugin MoneySystem;

		// Configurations plugin.
		static bool teleports				= true;
		static float TimeTeleport			= 5f;
		static float TimeAntiSpawnKill		= 10f;
		static string chatPrefix				= "TeleportUsine";
		static bool CostTeleport = false;
		static bool CostTeleportViP = false;
		static int CostTeleportPlayer = 500;
		static int CostTeleportPlayerVIP = 250;
		// Permissoes admin vip.
		const string permiTeleportAdmin		= "teleportusine.admin";
		const string permTeleportDellay		= "teleportusine.delay";
		
		static string nameInventory				= "Inventory";
		static string modeWeapon				= "Laser Sight"; 
		static string modeWeapon1				= "Flashlight Mod";
		
       static float TimeTeleportPlayers = 5;
       static List<string> OnTeleportPlayers = new List<string>();
	   
		readonly static Dictionary<string, ItemDataBlock> DataBock = new Dictionary<string, ItemDataBlock>();
		readonly static List<string> listWeaponsMods = new List<string>(new string[]{"revolver","9mm pistol","p250","shotgun","mp5a4","m4","bolt action rifle"});
	    static Dictionary<string, object> SaveInventory = new Dictionary<string, object>();

		
		//Names locations.
		static Dictionary<string, object[]> NamesLocations = GetNamesLocations();
		static Dictionary<string, object[]> GetNamesLocations(){
			var dict = new Dictionary<string, object[]>();
			dict.Add("small", new object[] { 6137f, 377f, -3571f });
			dict.Add("vale", new object[] { 4794f, 427f, -3802f });
			dict.Add("hangar", new object[] { 6718f, 350f, -3571f });
			dict.Add("big", new object[] { 5317f, 369f, -4727f });
			dict.Add("factory", new object[] { 6375f, 362f, -4415f });
			dict.Add("resource valley", new object[] { 5531f, 383f, -3552f });
			dict.Add("hacker mountain", new object[] { 5733f, 383f, -1847f });
			dict.Add("zombie hill", new object[] { 6393f, 383f, -3428f });
			dict.Add("civilian forest", new object[] { 6643f, 354f, -3858f });
			return dict;
		}


		string GetMessage(string key, string steamid = null) => lang.GetMessage(key, this, steamid);
		void LoadDefaultMessages(){
			var message = new Dictionary<string, string>{
				{"NoPermission", "Você não tem permissão para usar este comando!"},
				{"NoFoundTeleport", "Teleport [color red]{0} [color clear]não existe nos teleports!"},
				{"HelpsAdmins", "==================== [color lime]Helps Admins [color clear]===================="},
				{"HelpsAdmins1", "Use /teleport chatPrefix => Change chat tag name."},
				{"HelpsAdmins2", "Use /teleport onof => Enable or disable system teleports."},
				{"HelpsAdmins3", "Use /teleport add (NameTeleport) => Add new teleport."},
				{"HelpsAdmins4", "Use /teleport remove (NameTeleport) => Remove a teleport."},
				{"HelpsAdmins5", "Use /teleport clear (NameTeleport) => Clear all teleports."},
				{"HelpsAdmins6", "===================================================="},
				{"AdminTeleport", "Sucesso ao alterar o chat prefix para: {0}!"},
				{"AdminTeleport1", "[color red]{0} [color clear] Desativou os teleports."},
				{"AdminTeleport2", "[color red]{0} [color clear] Ativou os teleports."},
				{"AdminTeleport3", "Successfully teleport {0} has been added or teleports."},
				{"AdminTeleport4", "Success teleport {0} was added or teleports."},
				{"AdminTeleport5", "Successfully all teleports were cleaned!"},
				{"HelpLocationsNames", "===== [color lime]Helps Teleports [color clear]========"},
				{"HelpLocationsNames1", "Use /t [color red]\"[color cyan]{0}[color red]\""},
				{"HelpLocationsNames2", "==========================="},
				{"Teleport", "Teleports sistema se encontra [color red]desativado[color clear]."},
				{"TeleportDellay", "[color orange]Você tem que espera {0} minutos, para usar o teleport novamente."},
				{"InventoryBack", "Inventario Devolvido, AntiSpawnKill Removido."},
				{"Teleport1", "Teleportando você para {0} em {1} seg/s ✌"},
				{"Teleport2", "Teleportado para {0} ✌"}
			};
			lang.RegisterMessages(message, this);
		}


		void Init(){
			CheckCfg<string>("Settings: Chat Tag", ref chatPrefix);
			CheckCfg<bool>("Settings: Teleports", ref teleports);
			CheckCfg<bool>("Settings: Cost Teleport Player TRUE|FALSE", ref CostTeleport);
			CheckCfg<bool>("Settings: Cost Teleport VIP TRUE|FALSE", ref CostTeleportViP);
			CheckCfg<int>("Settings: Cost Teleport VIP", ref CostTeleportPlayerVIP);
			CheckCfg<int>("Settings: Cost Teleport Player", ref CostTeleportPlayer);
			CheckCfg<bool>("Settings: Teleports", ref teleports);
			CheckCfg<float>("Settings: Time Teleport", ref TimeTeleport);
			CheckCfg<float>("Settings: Time AntiSpawnKill", ref TimeAntiSpawnKill);
			CheckCfg<float>("Settings: TimeTeleportPlayers ", ref TimeTeleportPlayers);
			CheckCfg<Dictionary<string, object[]>>("Settings: Names Locations", ref NamesLocations);
			permission.RegisterPermission(permiTeleportAdmin, this);
			permission.RegisterPermission(permTeleportDellay, this);
			DataBock.Clear();
			foreach(var item in DatablockDictionary.All)
			DataBock.Add(item.name.ToLower(), item);
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

		void OnPlayerConnected(NetUser netuser)
		{
			string ID = netuser.userID.ToString();
			if(OnTeleportPlayers.Contains(ID))
		        {
		       	OnTeleportPlayers.Remove(ID);
		       	return;
		       }
		}

		bool AcessAdmin(NetUser netuser){
			if(netuser.CanAdmin())return true; 
			if(permission.UserHasPermission(netuser.userID.ToString(), permiTeleportAdmin))return true;
			return false;
		}
			 
		bool AcessDellay(NetUser netuser){
			if(netuser.CanAdmin())return true;
			if(permission.UserHasPermission(netuser.userID.ToString(), permiTeleportAdmin))return true;
			if(permission.UserHasPermission(netuser.userID.ToString(), permTeleportDellay))return true;
			return false;
		}


		private void HelpsAdmins(NetUser netuser){
			string ID = netuser.userID.ToString();
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpsAdmins", ID));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpsAdmins1", ID));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpsAdmins2", ID));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpsAdmins3", ID));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpsAdmins4", ID));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpsAdmins5", ID));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpsAdmins6", ID));
		}


		void HelpLocationsNames(NetUser netuser){
			string ID = netuser.userID.ToString();
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpLocationsNames", ID));
			foreach(var pair in NamesLocations)
			rust.SendChatMessage(netuser, chatPrefix, string.Format(GetMessage("HelpLocationsNames1", ID), pair.Key));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpLocationsNames2", ID));
		}

		Dictionary<string, object> GetInventory(NetUser netuser){
			var kitsitems = new Dictionary<string, object>();
			var wearList = new List<object>();
			var mainList = new List<object>();
			var beltList = new List<object>();
			IInventoryItem item;
			var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			for (int i = 0; i < 40; i++){
				if(inv.GetItem(i, out item)){
				var newObject = new Dictionary<string, object>();
				newObject.Add(item.datablock.name.ToString().ToLower(), item.datablock._splittable?(int)item.uses :1);
				if(i>=0 && i<30)
				mainList.Add(newObject);
				else if(i>=30 && i < 36)
				beltList.Add(newObject);
				else
				wearList.Add(newObject);
				}
			}
			inv.Clear();
			kitsitems.Add("wear", wearList);
			kitsitems.Add("main", mainList);
			kitsitems.Add("belt", beltList);
			return kitsitems;
		}

		Dictionary<string, object> GetKit(NetUser netuser, string args){
			string ID = netuser.userID.ToString();
			var newdict = new Dictionary<string, object>();
			if(netuser.playerClient == null || netuser.playerClient.rootControllable == null)return newdict;
			var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			if(args == nameInventory){
				if(!SaveInventory.ContainsKey(ID))return newdict;
				newdict = (SaveInventory[ID]) as Dictionary<string, object>;
				rust.InventoryNotice(netuser, GetMessage("InventoryBack", ID));
				inv.Clear();
				return newdict;
			}
			return newdict;
		}
		
		object GiveKit(NetUser netuser, string args){
			var kitConfig = GetKit(netuser, args);
			if(kitConfig.Count == 0)return false;
			var kitsitems = kitConfig["items"] as Dictionary<string, object>;
			if(kitsitems == null)return false;
			var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			var wearList = kitsitems["wear"] as List<object>;
			var mainList = kitsitems["main"] as List<object>;
			var beltList = kitsitems["belt"] as List<object>;
			Inventory.Slot.Preference pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Armor,false,Inventory.Slot.KindFlags.Belt);
			if(wearList.Count > 0){
				pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Armor, false, Inventory.Slot.KindFlags.Belt);
				foreach (object items in wearList){
					foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>){
						GiveItem(inv, (string)pair.Key, (int)pair.Value, pref);
					}
				}
			}
			if(mainList.Count > 0){
				pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Default, false, Inventory.Slot.KindFlags.Belt);
				foreach (object items in mainList){
					foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>){
						GiveItem(inv, (string)pair.Key, (int)pair.Value, pref);
					}
				}
			}
			if(beltList.Count > 0){
				pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Belt, false, Inventory.Slot.KindFlags.Belt);
				foreach (object items in beltList){
					foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>){
						if(listWeaponsMods.Contains((string)pair.Key) && args != nameInventory)
						GiveWeaponMods(netuser, (string)pair.Key, (int)pair.Value, new[] {modeWeapon, modeWeapon1});
						else
						GiveItem(inv, (string)pair.Key, (int)pair.Value, pref);
					}
				}
			}
			return true;
		}

		void GiveWeaponMods(NetUser netuser, string weapon, int amount, string[] modes){
			var bullets = 30;
			if(netuser.playerClient == null || netuser.playerClient.rootControllable == null)return;
			var inventory = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			if(!DataBock.ContainsKey(weapon.ToLower())) return;
			var weapondata = DataBock[weapon.ToLower()];
			var item = inventory.AddItem(weapondata, Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Belt, false, Inventory.Slot.Kind.Belt), amount) as IWeaponItem;
			if(item == null) return;
			item.SetUses(bullets);
			item.SetTotalModSlotCount(4);
			foreach (var Mode in modes){
				if(!DataBock.ContainsKey(Mode.ToLower()))continue;
				var attachmentdata = DataBock[Mode.ToLower()] as ItemModDataBlock;
				item.AddMod(attachmentdata);
			}
		}

		object GiveItem(Inventory inventory, string itemname, int amount, Inventory.Slot.Preference pref){
			if(!DataBock.ContainsKey(itemname)) return false;
			ItemDataBlock datablock = DataBock[itemname];
			inventory.AddItemAmount(DataBock[itemname], amount, pref);
			return true;
		}

		void TeleportPlayer(NetUser netuser, Vector3 location){
			var management = RustServerManagement.Get();
			management.TeleportPlayerToWorld(netuser.playerClient.netPlayer, location);
		}


		void GodMode(NetUser netuser, bool set){
			if(set)
			netuser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(true);
			else
			netuser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(false); 
		}

		[ChatCommand("t")]
		void cmdTeleport(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!teleports && !AcessAdmin(netuser)){rust.SendChatMessage(netuser, chatPrefix, GetMessage("Teleport", ID)); return;}
			if(args.Length == 0){ HelpLocationsNames(netuser); return;}
			string nameLocation = args[0].ToString().ToLower();
			if(NamesLocations.ContainsKey(nameLocation)){
				object[] objectLocation = NamesLocations[nameLocation];
				if(objectLocation.Length < 2)return;
				Vector3 location = new Vector3((float)objectLocation[0], (float)objectLocation[1], (float)objectLocation[2]);
				// TEMPO PARA USAR O COMANDO NOVAMENTE!
				if(OnTeleportPlayers.Contains(ID)){rust.SendChatMessage(netuser, chatPrefix, string.Format(GetMessage("TeleportDellay"), TimeTeleportPlayers)); return;}
			    OnTeleportPlayers.Add(ID);
				timer.Once(TimeTeleportPlayers * 60, ()=>{OnTeleportPlayers.Remove(ID);});
				// SALVAR O INVENTARIO DO JOGADOR
		        var items = GetInventory(netuser);
		        var inventoryPlayer = new Dictionary<string, object>();
		        inventoryPlayer.Add("items", items);
		        SaveInventory.Add(netuser.userID.ToString(), inventoryPlayer);	
				//
				if(!AcessDellay(netuser))
			    {
					rust.Notice(netuser, string.Format(GetMessage("Teleport2", ID), nameLocation));
					if(netuser.playerClient == null)return;
					if (CostTeleportViP) {
                    object thereturn = (object)MoneySystem?.Call("canMoney", new object[] {netuser});
                    if(thereturn != null)return;//
                    if(MoneySystem == null){rust.Notice(netuser, GetMessage("MoneySystemNull", ID)); return;}
                    int totalMoney = (int)MoneySystem?.Call("GetTotalMoney", ID);
                    if(totalMoney < CostTeleportPlayerVIP){ rust.Notice(netuser, string.Format(GetMessage("MoneyInvalid", ID), CostTeleportPlayerVIP)); return;}
                    MoneySystem?.Call("TakeMoney", netuser, CostTeleportPlayerVIP);
				    }
					TeleportPlayer(netuser, location);
					GodMode(netuser, true);
					timer.Once(TimeAntiSpawnKill, () =>{
					if(netuser.playerClient == null)return;
					GodMode(netuser, false);
					timer.Once(1f, () => {
					GiveKit(netuser, nameInventory);
		            SaveInventory.Remove(netuser.userID.ToString()); });
					});
				}
				else {
					rust.Notice(netuser, string.Format(GetMessage("Teleport1", ID), nameLocation, TimeTeleport));
					if (CostTeleport){
                    object thereturn = (object)MoneySystem?.Call("canMoney", new object[] {netuser});
                    if(thereturn != null)return;//
                    if(MoneySystem == null){rust.Notice(netuser, GetMessage("MoneySystemNull", ID)); return;}
                    int totalMoney = (int)MoneySystem?.Call("GetTotalMoney", ID);
                    if(totalMoney < CostTeleportPlayer){ rust.Notice(netuser, string.Format(GetMessage("MoneyInvalid", ID), CostTeleportPlayer)); return;}
                    MoneySystem?.Call("TakeMoney", netuser, CostTeleportPlayer);						
					}
					timer.Once(TimeTeleport, () =>{
					if(netuser.playerClient == null)return;
					TeleportPlayer(netuser, location);
					GodMode(netuser, true);
					});					
					timer.Once(TimeAntiSpawnKill, () =>{
					if(netuser.playerClient == null)return;
					GodMode(netuser, false); 
					timer.Once(1f, () => {
					GiveKit(netuser, nameInventory);
		            SaveInventory.Remove(netuser.userID.ToString()); });
					});
				}
			}
			else
			HelpLocationsNames(netuser);
		}


		[ChatCommand("teleport")]
		void cmdAdminTeleport(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			string nameLocation = string.Empty;
			bool IsAdmin = (bool)AdminControl?.Call("IsAdmin", netuser);
			{
				if (!(netuser.CanAdmin() || IsAdmin || permission.UserHasPermission(ID, permiTeleportAdmin)))
			{
				rust.SendChatMessage(netuser, chatPrefix, GetMessage("NoPermission", ID));
				return;
			}
			if(args.Length == 0) { HelpsAdmins(netuser); return; }
			switch(args[0].ToLower()){
				case "chatPrefix":
					if (args.Length < 2) { rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpsAdmins1", ID)); return; }
					chatPrefix = args[1].ToString();
					Config["Settings: Chat Tag"] = chatPrefix;
					rust.Notice(netuser, string.Format(GetMessage("AdminTeleport", ID), chatPrefix));
					break;
				case "onof":
					if(teleports){
						teleports = false;
						rust.BroadcastChat(chatPrefix, string.Format(GetMessage("AdminTeleport1", ID), netuser.displayName));
					}
					else{
						teleports = true;
						rust.BroadcastChat(chatPrefix, string.Format(GetMessage("AdminTeleport2", ID), netuser.displayName));
					}
					Config["Settings: Teleports"] = teleports;
					break;
				case "add":
					if(args.Length < 2) { rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpsAdmins3", ID)); return; }
					nameLocation = args[1].ToString().ToLower();
					Vector3 location = netuser.playerClient.lastKnownPosition;
					if(NamesLocations.ContainsKey(nameLocation))
					NamesLocations.Remove(nameLocation);
					NamesLocations.Add(nameLocation, new object[] {location.x, location.y, location.z} );
					rust.Notice(netuser, string.Format(GetMessage("AdminTeleport3", ID), nameLocation));
					Config["Settings: Names Locations"] = NamesLocations;
					break;
				case "remove":
					if(args.Length < 2) { rust.SendChatMessage(netuser, chatPrefix, GetMessage("HelpsAdmins4", ID)); return; }
					nameLocation = args[1].ToString().ToLower();
					if(NamesLocations.ContainsKey(nameLocation)){
						NamesLocations.Remove(nameLocation);
						rust.Notice(netuser, string.Format(GetMessage("AdminTeleport4", ID), nameLocation));
						Config["Settings: Names Locations"] = NamesLocations;
					}
					else
					rust.SendChatMessage(netuser, chatPrefix, string.Format(GetMessage("NoFoundTeleport", ID), nameLocation));
					break;
				case "clear":
					NamesLocations.Clear();
					rust.Notice(netuser, GetMessage("AdminTeleport5", ID));
					Config["Settings: Names Locations"] = NamesLocations;
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
}