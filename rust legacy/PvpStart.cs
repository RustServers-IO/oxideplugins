using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core.Plugins;


/////////////////////////////////////////////////////////////////////////////////////////////
//////////////_///_/////////////_/////////_///////___////_ /////////////////_//_//_/////////
///////_//_///( )/( )///////////( )///////( )////(  _`\/(_ )///////////////( )( )( )///////
/////_( )( )_ | |/'/'///_//////_| |///_ _ | |/')/| |_) ) | |///_/_///_///_ | || || |//////
////(_  ..  _)| , <////'_`\  /'_` | /'_` )| , <//| ,__/' | |///'_` )( ) ( )| || || |/////
////(_      _)| |\`\ ( (_) )( (_| |( (_| || |\`\/| |/////| | ( (_| || (_) || || || |////
//////(_)(_)  (_) (_)`\___/'`\__,_)`\__,_)(_) (_)(_)////(___)`\__,_)`\__, |(_)(_)(_)///
////////////////////////////////////////////////////////////////////( )_| |(_)(_)(_)//
////////////////////////////////////////////////////////////////////`\___////////////
 
namespace Oxide.Plugins
{
    [Info("PvpStart", "#KodakPlay!!!", "1.0.5")]

    class PvpStart : RustLegacyPlugin
    {
		private readonly static Dictionary<string, ItemDataBlock> dataBock = new Dictionary<string, ItemDataBlock>();
		readonly static List<string> listWeaponsMods = new List<string>(new string[]{"revolver","9mm pistol","Hunting Bow","shotgun","mp5a4","m4","bolt action rifle"});
	
		static Dictionary<string, object> kits = new Dictionary<string, object>();
		static Dictionary<string, object> kitsP2 = new Dictionary<string, object>();
		static Dictionary<string, object> saveInventory = new Dictionary<string, object>();
		static Dictionary<NetUser,bool> online = new Dictionary<NetUser, bool>();
		static Dictionary<string, object> locationsTeleports = new Dictionary<string, object>();
		static Dictionary<string, object> locationsTeleports2 = new Dictionary<string, object>();
		static List<P250arena> PlayersInP250 = new List<P250arena>();
		class P250arena : MonoBehaviour	{
			public PlayerClient player;
			public int Kills	= 0;
			public int Deaths	= 0;
			public int kit		= 0;
			public void ResetStats(){ 
				Kills		= 0;
				Deaths		= 0;
				kit		= 0;
			}
			void Awake(){
				this.player = GetComponent<PlayerClient>();
			}
		}
		
		[PluginReference] Plugin Kits;
		[PluginReference] Plugin Death;
		System.Random random = new System.Random(); 
        RustServerManagement management;	
		
		static bool arenassystem 				= true;
		static bool kitRandom					= false;

		
		static string chatPrefix 				= "Epic-PVP";
        const string permiAdmin 				= "PvpStart.use";
		static string modeWeapon				= "Holo Sight"; 
		static string modeWeapon1				= "Flashlight Mod";
		static string PrefixP250 				= "P250"; //
		static float timeSpawn					= 0.01f;
		
		void OnServerInitialized()   {
			management = RustServerManagement.Get();			
			CheckCfg<Dictionary<string, object>>("Settings: Locations Teleports", ref locationsTeleports);
			CheckCfg<Dictionary<string, object>>("Settings: Locations Teleports2", ref locationsTeleports2);
			CheckCfg<Dictionary<string, object>>("Settings: Kits", ref kits);
			CheckCfg<Dictionary<string, object>>("Settings: Kits p2", ref kitsP2);
			CheckCfg<string>("Settings: Mode Weapon", ref modeWeapon);
			CheckCfg<string>("Settings: Mode1 Weapon", ref modeWeapon1);
			CheckCfg<bool>("Settings: Arena Teleports", ref arenassystem);
			permission.RegisterPermission(permiAdmin, this);
			dataBock.Clear();
			foreach(var item in DatablockDictionary.All)
			dataBock.Add(item.name.ToLower(), item);
			SetupLang();
			SaveConfig();	
            InitiateTime(true);
        }
		
		void InitiateTime(bool settime)    {
            env.daylength = 999999999f;
        }

        protected override void LoadDefaultConfig()  { }
		private void CheckCfg<T>(string Key, ref T var)    {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }
		
		bool Acess(NetUser netuser)	{
			if(netuser.CanAdmin())return true; 
			if(permission.UserHasPermission(netuser.playerClient.userID.ToString(), permiAdmin)) return true;
			return false;
		}
		
        string GetMessage(string key, string Id = null) => lang.GetMessage(key, this, Id);
        void SetupLang()    {
            var message = new Dictionary<string, string>{
			{"NoPermission", "Você não tem permissão para usar este comando!"},
			{"ArenasOffline", "O sistema de arenas se encontra atualmente: [color red]desligado [color clear]!"},
			
			{"EntrarP250", "[color orange]{0} [color clear]entrou na arena [color red]{1} !"},
			
			{"EntrouNaArena", "[color orange]{0} [color clear]entrou na arena [color red]{1} !"},
			{"AntiSpawnKill", "[color orange]Você tem 5 segundos de AntiSpawnKill!"},
			{"AntiSpawnKillOff", "[color orange]AntiSpawnKill acabou!"},
			
			{"KitInventario", "[color orange]Verifique no seu inventario!"},
			{"KitReceived", "Kit recebido com sucesso!"},
			
			{"ArenaHelp", "=-=-=-=-=-=-=-=-= Arena Comandos =-=-=-=-=-=-=-=-="},
			{"ArenaHelp1", "Use /save_spawn <padrao|p250|> - para editar locais de spawn"},
			{"ArenaHelp2", "Use /ekit <padrao|p250|> - para editar algum kit"},
			{"ArenaHelp3", "Use /eclear <kits|spawns|> - para deletar algo"},
			{"ArenaHelp4", "Use /emods <1 |2 |3 |4 |5> - para mudar os mods"},
			{"ArenaHelp5", "=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-="},
			
			{"Clear", "Voce limpou todos os kits"},
			{"Clear1", "Voce limpou todos os teleportes"},
			
			{"SEt", "Voce setou o kit"},
			{"SaveSpawn", "Voce setou o ponto "},
						
			{"Modes", "Mods novos para as armas: {0} / {1}."},
			
			};
			lang.RegisterMessages(message, this);

        }
		
		void Arena(NetUser netuser)	{
			var id = netuser.userID.ToString();
			timer.Once(0.1f, () => {
				netuser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(true);
				rust.SendChatMessage(netuser, chatPrefix, GetMessage("AntiSpawnKill", id));
				var Inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
				Inv.Clear();
			});
			timer.Once(0.2f, () => {
				rust.BroadcastChat(chatPrefix, string.Format(GetMessage("EntrouNaArena", id), netuser.displayName, PrefixP250));	
				TeleportPlayer(netuser);	
			});
			timer.Once(0.3f, () => {
				rust.InventoryNotice(netuser, GetMessage("KitReceived", id));
				rust.SendChatMessage(netuser, chatPrefix, GetMessage("KitInventario", id));
				GiveKit(netuser, "kitp250");
			});
			timer.Once(5f, () => { 
				rust.SendChatMessage(netuser, chatPrefix, GetMessage("AntiSpawnKillOff", id));
				netuser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(false);
			});
		}
		
		void HelpAdmins(NetUser netuser)	{
			var id = netuser.userID.ToString();
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("ArenaHelp", id));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("ArenaHelp1", id));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("ArenaHelp2", id));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("ArenaHelp3", id));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("ArenaHelp4", id));
			rust.SendChatMessage(netuser, chatPrefix, GetMessage("ArenaHelp5", id));
		} 
 
		
		void TeleportPlayer(NetUser netuser)	{
			if(locationsTeleports.Count == 0)return;
			int numberTeleport = random.Next(0, locationsTeleports.Count);
			var location = locationsTeleports[numberTeleport.ToString()] as Dictionary<string, object>;
			if(netuser.playerClient == null || location == null)return;
			management.TeleportPlayerToWorld(netuser.playerClient.netPlayer, new Vector3(Convert.ToSingle(location["x"]), Convert.ToSingle(location["y"]) + 5f, Convert.ToSingle(location["z"])));
		}
		
		void TeleportPlayer2(NetUser netuser)	{
			if(locationsTeleports2.Count == 0)return;
			int numberTeleport = random.Next(0, locationsTeleports.Count);
			var location = locationsTeleports2[numberTeleport.ToString()] as Dictionary<string, object>;
			if(netuser.playerClient == null || location == null)return;
			management.TeleportPlayerToWorld(netuser.playerClient.netPlayer, new Vector3(Convert.ToSingle(location["x"]), Convert.ToSingle(location["y"]) + 5f, Convert.ToSingle(location["z"])));
		}
		
		
		
		void OnPlayerDisconected(uLink.NetworkPlayer networkPlayer)	{
			NetUser netuser = (NetUser)networkPlayer.GetLocalData();
			online[netuser] = true;
		}
		


		void OnPlayerSpawn(PlayerClient player)	{
			if(player.GetComponent<P250arena>()){ 
				timer.Once(2f, ()=>{
					if(player.rootControllable == null)return;
					player.rootControllable.rootCharacter.takeDamage.SetGodMode(true);
					timer.Once(5f, ()=>{
						if(player.rootControllable == null)return;
						player.rootControllable.rootCharacter.takeDamage.SetGodMode(false);
					});
					TeleportPlayer(player.netUser);
					var Inv = player.netUser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
					Inv.Clear();
					var message = GetMessage("Go, Go, Go | P250 spawn", player.netUser.userID.ToString());
					rust.Notice(player.netUser, message, "ッ" );
					GiveKit(player.netUser, "kitp250");
				});
			}
			var messages = GetMessage("Go, Go, Go", player.netUser.userID.ToString());
			rust.Notice(player.netUser, messages, "ッ" );
			timer.Once(timeSpawn,()=> GiveSpawns(player.netUser));
		}
		
		void GiveSpawns(NetUser player)	{ 
			TeleportPlayer2(player);
			GiveKit(player, "kitpadrao");
		}
		

		//////////////////
		//// Gives
		/////////////////
		Dictionary<string, object> GetInventory(NetUser netuser)	{
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
		
		Dictionary<string, object> GetDictionaryKit(NetUser netuser, string args)	{
			var ID = netuser.userID.ToString();
			var newdict = new Dictionary<string, object>();
			if(netuser.playerClient == null || netuser.playerClient.rootControllable == null)return newdict;
			var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			if(args == "kitpadrao"){
				if(kits.Count == 0)return newdict;
				if(kitRandom)
				newdict = kits[netuser.playerClient.GetComponent<P250arena>().kit.ToString()] as Dictionary<string, object>;
				else
				newdict = kits["0"] as Dictionary<string, object>;
				rust.InventoryNotice(netuser, GetMessage("KitReceived", ID));
				rust.SendChatMessage(netuser, chatPrefix, GetMessage("KitInventario", ID));
				inv.Clear();
				return newdict;
			}
			else if(args == "kitp250"){
				if(kitsP2.Count == 0)return newdict;
				var randoma = random.Next(0, kitsP2.Count);
				randoma = 0;
				newdict = (kitsP2[randoma.ToString()]) as Dictionary<string, object>;
				return newdict;
			}
			return newdict;
		}
		


		public object GiveKit(NetUser netuser, string args)	{
			var kit = GetDictionaryKit(netuser, args);
			if(kit.Count == 0)return false;
			var kitsitems = kit["items"] as Dictionary<string, object>;
			if(kitsitems == null)return false;
			var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			var wearList = kitsitems["wear"] as List<object>;
			var mainList = kitsitems["main"] as List<object>;
			var beltList = kitsitems["belt"] as List<object>;
			Inventory.Slot.Preference pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Armor,false,Inventory.Slot.KindFlags.Belt);
			if(wearList.Count > 0){
				pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Armor, false, Inventory.Slot.KindFlags.Belt);
				foreach (object items in wearList){
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
					foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>){
						if(listWeaponsMods.Contains((string)pair.Key) && args != "Inventory")
						InventoryBelt(netuser, (string)pair.Key, (int)pair.Value, new[] {modeWeapon, modeWeapon1});
						else
						GiveItem(inv, (string)pair.Key, (int)pair.Value, pref);
					}
					
				}
			}
			return true;
		}
		
		public object GiveItem(Inventory inventory, string itemname, int amount, Inventory.Slot.Preference pref)	{
			if(!dataBock.ContainsKey(itemname)) return false;
			ItemDataBlock datablock = dataBock[itemname];
			inventory.AddItemAmount(dataBock[itemname], amount, pref);
			return true;
		}
		
		void InventoryBelt(NetUser netuser, string weapon, int amount, string[] modes)	{
			var bullets = 30;
			if(netuser.playerClient == null || netuser.playerClient.rootControllable == null)return;
			var inventory = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			if(!dataBock.ContainsKey(weapon.ToLower()))return;
			var weapondata = dataBock[weapon.ToLower()];
			var item = inventory.AddItem(weapondata, Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Belt, false, Inventory.Slot.Kind.Belt), amount) as IWeaponItem;
			if(item == null)return;
			item.SetUses(bullets);
			item.SetTotalModSlotCount(4);
			foreach (var Mode in modes){
				if(!dataBock.ContainsKey(Mode.ToLower()))continue;
				var attachmentdata = dataBock[Mode.ToLower()] as ItemModDataBlock;
				item.AddMod(attachmentdata);
			}
		}
		
		////////////////
		//// Commands
		///////////////
		
		[ChatCommand("ekit")]
		void cmdEkit(NetUser netuser, string command, string[] args)	{
			var ID = netuser.userID.ToString();
			if(!Acess(netuser)){rust.SendChatMessage(netuser, chatPrefix, GetMessage("NoPermission", ID));return;} 
			if(args.Length != 1){HelpAdmins(netuser);return;}
			switch (args[0].ToLower()){
				case "padrao":
					var kitItems = GetInventory(netuser);
					var kit = new Dictionary<string, object>();
					kit.Add("items", kitItems);
					kits.Add(kits.Count.ToString(), kit);
					Config["Settings: Kits"] = kits;
					rust.Notice(netuser, string.Format(GetMessage("SEt", ID) + kits.Count));
					SaveConfig();
					break;
				case "p250":
					var kitp2Items = GetInventory(netuser);
					var kitp2 = new Dictionary<string, object>();
					kitp2.Add("items",kitp2Items);
					kitsP2.Add(kitsP2.Count.ToString(), kitp2);
					Config["Settings: Kits p2"] = kitsP2;
					rust.Notice(netuser, string.Format(GetMessage("SEt", ID) + kitsP2.Count));
					SaveConfig();
					break;
				default:{
					HelpAdmins(netuser);  
					break;
				}
			}
		}
		
		[ChatCommand("eclear")]   
		void cmdEclear(NetUser netuser, string command, string[] args)	{
			var ID = netuser.userID.ToString();
			if(!Acess(netuser)){rust.SendChatMessage(netuser, chatPrefix, GetMessage("NoPermission", ID));return;} 
			if(args.Length != 1){HelpAdmins(netuser);return;}
			switch (args[0].ToLower()){
				case "kits":
					kits.Clear();
					Config["Settings: Kits"] = kits;
					kitsP2.Clear();
					Config["Settings: Kits p2"] = kitsP2;
					rust.Notice(netuser, GetMessage("Clear", ID));
					break;
				case "spawns":
					locationsTeleports.Clear();
					Config["Settings: Locations Teleports"] = locationsTeleports;
					locationsTeleports2.Clear();
					Config["Settings: Locations Teleports2"] = locationsTeleports2;
					rust.Notice(netuser, GetMessage("Clear1", ID));
					break;
				default:{
					HelpAdmins(netuser);
					break;
				}
			}
			SaveConfig();
		}
		
		[ChatCommand("save_spawn")]
		void cmdEsave(NetUser netuser, string command, string[] args)	{
			var ID = netuser.userID.ToString();
			if(!Acess(netuser)){rust.SendChatMessage(netuser, chatPrefix, GetMessage("NoPermission", ID));return;} 
			if(args.Length != 1){HelpAdmins(netuser);return;}
			switch (args[0].ToLower()){
			case "p250":
				var location = new Dictionary<string, object>();
				location.Add("x", netuser.playerClient.lastKnownPosition.x.ToString());
				location.Add("y", netuser.playerClient.lastKnownPosition.y.ToString());
				location.Add("z", netuser.playerClient.lastKnownPosition.z.ToString());
				locationsTeleports.Add(locationsTeleports.Count.ToString(), location);
				Config["Settings: Locations Teleports"] = locationsTeleports;
				rust.Notice(netuser, string.Format(GetMessage("SaveSpawn", ID) + locationsTeleports.Count));
				break;
			case "padrao": 
				var location2 = new Dictionary<string, object>();
				location2.Add("x", netuser.playerClient.lastKnownPosition.x.ToString());
				location2.Add("y", netuser.playerClient.lastKnownPosition.y.ToString());
				location2.Add("z", netuser.playerClient.lastKnownPosition.z.ToString());
				locationsTeleports2.Add(locationsTeleports2.Count.ToString(), location2);
				Config["Settings: Locations Teleports2"] = locationsTeleports2;
				rust.Notice(netuser, string.Format(GetMessage("SaveSpawn", ID) + locationsTeleports2.Count));
				break;
			}
			SaveConfig();
			
		}
		
		[ChatCommand("emods")]
		void cmdEModes(NetUser netuser, string command, string[] args){
			var ID = netuser.userID.ToString();
			if(!Acess(netuser)){rust.SendChatMessage(netuser, chatPrefix, GetMessage("NoPermission", ID));return;} 
			if(args.Length != 1){HelpAdmins(netuser);return;}
			switch (Convert.ToInt32(args[0])){
				case 1:
					modeWeapon	= "Holo sight";
					modeWeapon1	= "Laser Sight";
					rust.Notice(netuser, string.Format(GetMessage("Modes", ID), modeWeapon, modeWeapon1));
					break;
				case 2:
					modeWeapon	= "Silencer";
					modeWeapon1	= "Laser Sight";
					rust.Notice(netuser, string.Format(GetMessage("Modes", ID), modeWeapon, modeWeapon1));
					break;
				case 3:
					modeWeapon	= "Flashlight Mod";
					modeWeapon1	= "Laser Sight";
					rust.Notice(netuser, string.Format(GetMessage("Modes", ID), modeWeapon, modeWeapon1));
					break;
				case 4:
					modeWeapon	= "Silencer";
					modeWeapon1	= "Laser Sight";
					rust.Notice(netuser, string.Format(GetMessage("Modes", ID), modeWeapon, modeWeapon1));
					break;
				case 5:
					modeWeapon	= "Holo sight";
					modeWeapon1	= "Silencer";
					rust.Notice(netuser, string.Format(GetMessage("Modes", ID), modeWeapon, modeWeapon1));
					break;
				default:{
					HelpAdmins(netuser);
					break;
				}
			}
			Config["Settings: Mode Weapon"]  = modeWeapon;
			Config["Settings: Mode1 Weapon"] = modeWeapon1;
			SaveConfig();
		}
		
		[ChatCommand("p250")]
		void cmdEp250(NetUser netuser, string command, string[] args)	{
			var id = netuser.userID.ToString();
			if (!arenassystem) { rust.SendChatMessage(netuser, chatPrefix, GetMessage("ArenasOffline", id)); return; }
			if(locationsTeleports.Count == 0){ rust.SendChatMessage(netuser, chatPrefix, GetMessage("ArenasOffline", id)); return; }
			if (online.ContainsKey(netuser)) 
			{
				if (online[netuser])
				{
					online[netuser] = false;
					PlayersInP250.Add(netuser.playerClient.gameObject.AddComponent<P250arena>());
					Arena(netuser);
				} 
				else 
				{
					online[netuser] = true;
					GameObject.Destroy(netuser.playerClient.GetComponent<P250arena>());
					PlayersInP250.Remove(netuser.playerClient.GetComponent<P250arena>());
					var rootControllable = netuser.playerClient.rootControllable;
					Metabolism morte = rootControllable.GetComponent<Metabolism>();
					morte.AddRads(999999999999);
				}
			} else {
				online[netuser] = false;
				PlayersInP250.Add(netuser.playerClient.gameObject.AddComponent<P250arena>());
				Arena(netuser);
			}
		}	
		
	}
}		