using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using System.Linq;
using System;

 
namespace Oxide.Plugins{
		[Info("ArenaMiniGames", "tugamano", "1.0.1")]
		[Description("ArenaMiniGames 3 Types Of Games And A Arena.")]
		class ArenaMiniGames : RustLegacyPlugin{


		[PluginReference] Plugin ZoneManager;
		static Plugins.Timer timerCheckControllable;
		static System.Random random = new System.Random();
		static RustServerManagement management;
		const string permissionArenaMiniGames	= "arenaminigames.use";
		static string chatName					= "Arena";
		static string modeWeapon				= "Laser Sight"; 
		static string modeWeapon1				= "Flashlight Mod";
		static string nameNormal				= "Normal";
		static string nameLevel					= "Level";
		static string namePrize					= "Prize";
		static string nameInventory				= "Inventory";
		static bool systemArena					= true;  
		static bool gameRandomKits				= false;
		static bool gameKitLevels				= false;
		static bool awardKitRandom				= false;
		static bool autoResetarterGame 			= true;
		static bool autoChangedGame				= true;
		static bool giveHealthCalories			= true;
		static bool putsConnectDisconnect		= false;
		static double xpLevel					= 0.5; 
		static int levelWiner					= 15;
		static int killsWiner					= 15;
		static int maxPlayers					= 20;
		static int maxTops						= 5;
		static int timeTelepotsGame				= 10; 
		static float minDistanceStructures		= 7f;
		static float timeSpawnGodMode			= 3f;


		readonly static Dictionary<string, ItemDataBlock> DataBock = new Dictionary<string, ItemDataBlock>();
		static Dictionary<string, StoredData> Data = new Dictionary<string, StoredData>();
		static Dictionary<string, Vector3> TeleportBack = new Dictionary<string, Vector3>();
		static Dictionary<string, object> LocationsArena = new Dictionary<string, object>();
		static Dictionary<string, object> SaveInventory = new Dictionary<string, object>();
		static Dictionary<string, object> Kits = new Dictionary<string, object>();
		static Dictionary<string, object> KitsLevel = new Dictionary<string, object>();
		static Dictionary<string, object> KitsWiner = new Dictionary<string, object>();


		readonly static List<double> listInvalidXpLevel = new List<double>(new double[]{0.3, 0.4, 0.6, 0.7, 0.8, 0.9});
		readonly static List<string> listWeaponsMods = new List<string>(new string[]{"revolver","9mm pistol","p250","shotgun","mp5a4","m4","bolt action rifle"});


		static List<string> PlayingArenaExitServer = new List<string>();
		static List<ArenaGames> PlayersInArena = new List<ArenaGames>();
		class ArenaGames : MonoBehaviour{
			public PlayerClient player;
			public int Kills	= 0;
			public int Deaths	= 0;
			public int KitLevel	= 0;
			public int KitRandom= 0;
			public double Level	= 0.0;
			public void ResetStats(){
				Kills		= 0;
				Deaths		= 0;
				Level		= 0.0;
				KitLevel	= 0;
				KitRandom	= 0;
			}
			void Awake(){
				this.player = GetComponent<PlayerClient>();
			}
		}


		StoredData data;
		class StoredData{
			public string name {get; set;}
			public int kills {get; set;}
			public int deaths {get; set;}
			public int gamesWon {get; set;}
		}


		StoredData GetPlayerData(string ID){
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


		private void OnRemovePlayerData(string ID){
			if(Data.ContainsKey(ID)){
				Data.Remove(ID);
				SaveData();
			}
		}


		string GetMessage(string key, string steamid = null) => lang.GetMessage(key, this, steamid);
		void LoadDefaultMessages(){
			var message = new Dictionary<string, string>{
				{"OnKilledLevel", "Level: {0}"},
				{"OnKilledKills", "Killers: {0}"},
				{"NoHaveAcessAdmins", "You are [color red]not allowed [color clear]to use this command."},
				{"PlayerNotFound", "Player name [color red]not [color clear]found!"},
				{"GameOverLevel", "Game Over! [color red]{0} [color clear]Won Stats Level: {1}."},
				{"GameOverKills", "Game Over! [color red]{0} [color clear]Won Stats Killers: {1} Dead: {2}."},
				{"AutoStarter", "Auto [color lime]Starter [color clear]Arena! [color red]{0} [color clear]Player/s."},
				{"AutoChangeGame", "Auto [color red]changed [color clear]game chosen [color lime]Game Kit Random[color clear]."},
				{"AutoChangeGame1", "Auto [color red]changed [color clear]game chosen [color lime]Game Kit Level[color clear]."},
				{"AutoChangeGame2", "Auto [color red]changed [color clear]game chosen [color lime]Game One Kit[color clear]."},
				{"AutoCloseGame", "{0} has been [color red]closed[color clear]."},
				{"KitEquiped", "Equiped!"},
				{"KitAward", "Award in your inventory!"},
				{"InventoryBack", "Your inventory back!"},
				{"TopsGames", "Not have [color red]player/s [color clear]in {0}."},
				{"TopsGames1", "=====  Tops Max [color red]{0}[color clear]/[color lime]{1} [color clear]Player/s [color lime]Game {2}[color clear]  ======"},
				{"TopsGames2", "Place  [color lime]{0}  [color red]{1}  [color clear]Level  [color lime]{2}"},
				{"TopsGames3", "Place  [color lime]{0}  [color red]{1}  [color clear]Score  [color lime]{2}  [color clear]Kills  [color lime]{3}  [color clear]Deaths  [color red]{4}"},
				{"TopsGames4", "Still has [color red]no [color clear]saved stats."},
				{"TTopsGames5", "================= [color lime]Tops [color red]{0} [color lime]{1} [color clear]===================="},
				{"TopsGames6", "Place  [color lime]{0}  [color red]{1}  [color clear]Games won  [color lime]{2}  [color clear]Score  [color lime]{3}  [color clear]Kills  [color lime]{4}  [color clear]Deads  [color red]{5}"},
				{"TopsGames7", "=========================================================="},
				{"Help", "=== [color lime]Help {0} [color clear]==="},
				{"Help1", "[color lime]Join [color clear]Arena: /aj"},
				{"Help2", "[color red]Left [color clear]Arena: /al"},
				{"Help3", "[color Orange]Info [color clear]Arena: /ainfo"},
				{"Help4", "[color cyan]Stats [color clear]Arena: /astats"},
				{"Help5", "[color #C71585]Stats [color clear]Games: /astats games"},
				{"Help6", "[color red]Admin [color clear]Helps /acfg "},
				{"Help7", "[color red]Admin [color clear]Won Game /winner"},
				{"Help8", "========================="},
				{"HelpsAdmins", "=========================== [color lime]Helps Admins [color clear]==========================="},
				{"HelpsAdmins1", "/acfg chatname (NameChat) => To change chat name."},
				{"HelpsAdmins2", "/acfg maxPlayers (NumberMax) => To change max players."},
				{"HelpsAdmins3", "/acfg maxstats (NumberMax) => To change max tops list."},
				{"HelpsAdmins4", "/acfg xplevel (NumberXp) => To change xp to level up."},
				{"HelpsAdmins5", "/acfg levelwiner (NumberLevel) => To change level win."},
				{"HelpsAdmins6", "/acfg killswiner (NumberKill) => To change kill win."},
				{"HelpsAdmins7", "/asystem onof => Enable or disable system ArenaMiniGames."},
				{"HelpsAdmins8", "/asystem puts => Enable or disable puts players disconnect connect in game."},
				{"HelpsAdmins9", "/asystem gamekitrandom => Enable or disable game kit random."},
				{"HelpsAdmins10", "/asystem gamelevel => Enable or disable game kit level."},
				{"HelpsAdmins11", "/asystem awardrandom => Enable or disable award random."},
				{"HelpsAdmins12", "/asystem autoresetarter => Enable or disable auto resetarter game."},
				{"HelpsAdmins13", "/asystem autochangedgame => Enable or disable auto changed game."},
				{"HelpsAdmins14", "/asystem healthcalories => Enable or disable give health calories."},
				{"HelpsAdmins15", "/amods (Number Min:1 Max:5) => To change mods weapons."},
				{"HelpsAdmins16", "/alocation teleport => Add teleports game."},
				{"HelpsAdmins17", "/alocation zone => Add zone game."},
				{"HelpsAdmins18", "/akit kit => Add kit game normal."},
				{"HelpsAdmins19", "/akit kitlevel => Add kit game level."},
				{"HelpsAdmins20", "/akit kitwiner => Add kit winer."},
				{"HelpsAdmins21", "/aclear teleports => Clear all teleports game."},
				{"HelpsAdmins22", "/aclear kits => Clear all kits normal game."},
				{"HelpsAdmins23", "/aclear kitslevel => Clear all kits game level."},
				{"HelpsAdmins24", "/aclear kitswiner => Clear all stats data."},
				{"HelpsAdmins25", "/aclear stats (PlayerNamerId) => Remove player stats data."},
				{"HelpsAdmins26", "/aclear stats clear => Clear all stats data."},
				{"HelpsAdmins27", "================================================================"},
				{"Info", "================ [color lime]Informations {0} [color clear]================"},
				{"Info1", "System Arena: [color red]{0} [color lime]|| [color clear]Locations: [color red]{1}[color clear]."},
				{"Info2", "Players: [color red]{0} [color lime]|| [color clear]Max Players: [color red]{1} [color lime]|| [color clear]Max Tops: [color red]{2}[color clear]."},
				{"Info3", "Level Winer: [color red]{0} [color lime]|| [color clear]Xp Level: [color red]{1} [color lime]|| [color clear]Kills Winer: [color red]{2}[color clear]."},
				{"Info4", "Kits Kills: [color red]{0} [color lime]|| [color clear]Kits Level: [color red]{1} [color lime]|| [color clear]Kits Winer: [color red]{2}[color clear]."},
				{"Info5", "Mod 1: [color red]{0} [color lime]|| [color clear]Mod 2: [color red]{1}[color clear]."},
				{"Info6", "Health Calories: [color red]{0} [color lime]|| [color clear]Chat Name: [color red]{1}[color clear]."},
				{"Info7", "Auto Resetarter Game: [color red]{0} [color lime]|| [color clear]Auto Changed Game: [color red]{1}[color clear]."},
				{"Info8", "Game Level: [color red]{0} [color lime]|| [color clear]Game Random: [color red]{1} [color lime]|| [color clear]Award Random: [color red]{2}[color clear]."},
				{"Info9", "========================================================="},
				{"Configurations", "Success chat name changed to {0}."},
				{"Configurations1", "Success max players changed to {0}."},
				{"Configurations2", "Success Max tops list changed to {0}."},
				{"Configurations3", "Success xp level changed to {0}."},
				{"Configurations4", "Success level winer changed to {0}."},
				{"Configurations5", "Success kills winer changed to {0}."},
				{"System", "{0} [color red]close [color clear]{1}."},
				{"System1", "{0} [color lime]open [color clear]{1}."},
				{"System2", "Game Kit Random: {0}."},
				{"System3", "Game Level: {0}."},
				{"System4", "Award Random: {0}."},
				{"System5", "Auto Resetarter: {0}."},
				{"System6", "Auto Changed Game: {0}."},
				{"System7", "Give Health Calories: {0}."},
				{"System8", "Puts Connect Disconnect: {0}."},
				{"Modes", "Mod: {0} Mod: {1}."},
				{"Kits", "Sucess add kit game normal number: {0}."},
				{"Kits1", "Sucess add kit game level number {0}."},
				{"Kits2", "Sucess add kit winer number {0}."},
				{"Location", "Sucess add location game number {0}."},
				{"Location1", "You can not create zone arena without having plugin ZoneManager!"},
				{"Location2", "Success zone arena cred! Radius {0}."},
				{"Clear", "Success all stats data is clean!"},
				{"Clear1", "Not found stats of [color red]{0}[color clear]."},
				{"Clear2", "Success name {0} was cleared of stats."},
				{"Clear3", "Success All Locations Cleaned!"},
				{"Clear4", "Success All Kits Cleaned!"},
				{"Clear5", "Success All Kits Level Cleaned!"},
				{"Clear6", "Success All Kits Winer Cleaned!"},
				{"Winner", "[color red]{0} [color clear]not playing in the arena."},
				{"JoinGame", "Arena system is disabled!"},
				{"JoinGame1", "Arena have max players!"},
				{"JoinGame2", "You already in arena! Exit arena /al"},
				{"JoinGame3", "Arena does not have any teleports!"},
				{"JoinGame4", "You are standing to Close to a building!"},
				{"JoinGame5", "{0} seg/s join arena!"},
				{"JoinGame6", "{0} join arena /aj [color red]{1} [color clear]Player/s in arena."},
				{"ExitGame", "You are not playing in arena!"},
				{"ExitGame1", "{0} seg/s left arena!"},
				{"ExitGame2", "{0} leave arena /al [color red]{1} [color clear]Players in arena."},
				{"LeavingSever", "[color red]{0} [color clear]It was eliminated from the arena by leaving the server."},
				{"LeavingConnected", "You were killed for leaving the server inside the arena."}
			}; 
			lang.RegisterMessages(message, this);
		}


		void Loaded(){Data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, StoredData>>("Stats(ArenaGames)");}
		void SaveData(){Interface.Oxide.DataFileSystem.WriteObject("Stats(ArenaGames)", Data);}


		void OnServerInitialized(){
			management = RustServerManagement.Get();
			CheckCfg<bool>("Settings: System Arena", ref systemArena);
			CheckCfg<bool>("Settings: Game Random Kit", ref gameRandomKits);
			CheckCfg<bool>("Settings: Game Kits Level", ref gameKitLevels);
			CheckCfg<bool>("Settings: Award Random", ref awardKitRandom);
			CheckCfg<bool>("Settings: Auto Resetarter Game", ref autoResetarterGame);
			CheckCfg<bool>("Settings: Auto Changed Game", ref autoChangedGame);
			CheckCfg<bool>("Settings: Give Health Calories", ref giveHealthCalories);
			CheckCfg<bool>("Settings: Puts Connect Disconnect", ref putsConnectDisconnect);
			CheckCfg<string>("Settings: Chat Name", ref chatName);
			CheckCfg<string>("Settings: Mode Weapon", ref modeWeapon);
			CheckCfg<string>("Settings: Mode Weapon1", ref modeWeapon1);
			CheckCfg<int>("Settings: Max Players Arena", ref maxPlayers);
			CheckCfg<int>("Settings: Max List Stats", ref maxTops);
			CheckCfg<int>("Settings: Time Teleport Arena", ref timeTelepotsGame);
			CheckCfg<int>("Settings: Level Winer", ref levelWiner);
			CheckCfg<int>("Settings: Kills Winer", ref killsWiner);
			if(xpLevel > 1 || listInvalidXpLevel.Contains(xpLevel))xpLevel = 0.5;
			CheckCfg<double>("Settings: Xp Level", ref xpLevel);
			CheckCfg<Dictionary<string,object>>("Settings: Locations Arena", ref LocationsArena);
			CheckCfg<Dictionary<string,object>>("Settings: Kits Arena", ref Kits);
			CheckCfg<Dictionary<string,object>>("Settings: Kits Level", ref KitsLevel);
			CheckCfg<Dictionary<string,object>>("Settings: Kits Winer", ref KitsWiner);
			SaveConfig();
			LoadDefaultMessages();
			DataBock.Clear();
			foreach(var item in DatablockDictionary.All)
			DataBock.Add(item.name.ToLower(), item);
			permission.RegisterPermission(permissionArenaMiniGames, this);
		}


		protected override void LoadDefaultConfig(){} 
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}


		bool AcessAdmins(NetUser netuser){
			if(netuser.CanAdmin())return true; 
			if(permission.UserHasPermission(netuser.playerClient.userID.ToString(), permissionArenaMiniGames)) return true;
			return false;
		}


		object canMoney(NetUser netuser){
			if(netuser.playerClient.GetComponent<ArenaGames>()){return "Give take money disabled inside the game!";}
			return null;
		}


		object canRankXp(NetUser netuser){
			if(netuser.playerClient.GetComponent<ArenaGames>()){return "Give take xp disabled inside the game!";}
			return null;
		}


		object canClansPvp(NetUser netuser){
			if(netuser.playerClient.GetComponent<ArenaGames>()){return "Pvp clan is activated inside the arena!";}
			return null;
		}


		private void OnPlayerConnected(NetUser netuser){
			PlayerClient player = netuser.playerClient;
			string ID = player.userID.ToString();
			if(PlayingArenaExitServer.Contains(ID)){
				timerCheckControllable = timer.Repeat(1f, 0, () =>{
					if(player == null){timerCheckControllable.Destroy();return;}
					if(player.rootControllable != null){
						RemovePlayerGame(player);
						player.rootControllable.idMain.GetComponent<Inventory>().Clear();
						TakeDamage.KillSelf(player.controllable.GetComponent<Character>());
						PlayingArenaExitServer.Remove(ID);
						rust.Notice(netuser, GetMessage("LeavingConnected", ID), "!", 15);
						if(putsConnectDisconnect)
						Puts(player.userName.ToString() + " Was killed by leaving the server inside the arena!"); 
						timerCheckControllable.Destroy();
					}
				});
			}
			if(Data.ContainsKey(ID)){
				data = GetPlayerData(ID);
				string Name = netuser.displayName.ToString();
				if(data.name != Name){
					data.name = Name;
					SaveData();
				}
			}
		}


		private void OnPlayerDisconnected(uLink.NetworkPlayer netplayer){
			PlayerClient player = ((NetUser)netplayer.GetLocalData()).playerClient;
			if(player.GetComponent<ArenaGames>()){
				RemovePlayerGame(player);
				PlayingArenaExitServer.Add(player.userID.ToString());
				rust.BroadcastChat(chatName, string.Format(GetMessage("LeavingSever"), player.userName));
				if(putsConnectDisconnect)
				Puts(player.userName.ToString() + " He left the server inside the arena."); 
			}
		}


		private void OnKilled(TakeDamage takedamage, DamageEvent damage){
			if(!(takedamage is HumanBodyTakeDamage)) return;
			NetUser Attacker = damage.attacker.client?.netUser ?? null;
			NetUser Victim = damage.victim.client?.netUser ?? null;
			if(Attacker == null || Victim == null)return;
			if(Attacker.playerClient.GetComponent<ArenaGames>() || Victim.playerClient.GetComponent<ArenaGames>()){
				var Inventory = Victim.playerClient.rootControllable.idMain.GetComponent<Inventory>();
				if(Victim != null || Victim == Attacker){
					Inventory.Clear();
					if(!gameKitLevels)
					Victim.playerClient.GetComponent<ArenaGames>().Deaths++;
					if(!gameKitLevels && gameRandomKits){
						Victim.playerClient.GetComponent<ArenaGames>().KitRandom++;
						if(Victim.playerClient.GetComponent<ArenaGames>().KitRandom >= Kits.Count)
						Victim.playerClient.GetComponent<ArenaGames>().KitRandom = 0;
					}
					if(Victim == Attacker)return;
					if(Attacker != null){
						if(giveHealthCalories){
							var rootControllable = Attacker.playerClient.rootControllable;
							var rootCharacter = rootControllable.rootCharacter;
							if(!rootControllable || !rootCharacter)return;
							Metabolism metabolism = Attacker.playerClient.rootControllable.GetComponent<Metabolism>();
							rootCharacter.takeDamage.health = 110;
							metabolism.AddCalories(3000);
						}
						if(gameKitLevels){
							Attacker.playerClient.GetComponent<ArenaGames>().Level = Convert.ToDouble(Attacker.playerClient.GetComponent<ArenaGames>().Level) + xpLevel;
							int[] LevelKillers = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15};
							foreach (int pair in LevelKillers){
								if(Attacker.playerClient.GetComponent<ArenaGames>().Level == pair){
									Attacker.playerClient.GetComponent<ArenaGames>().KitLevel++;
									if(Attacker.playerClient.GetComponent<ArenaGames>().KitLevel >= KitsLevel.Count)
									Attacker.playerClient.GetComponent<ArenaGames>().KitLevel = KitsLevel.Count -1;
									if(Attacker.playerClient.GetComponent<ArenaGames>().Level >= levelWiner){
										GameOver(Attacker);
										return;
									}
									GiveKit(Attacker, nameLevel);
								}
							}
							rust.InventoryNotice(Attacker,string.Format(GetMessage("OnKilledLevel", Attacker.userID.ToString()), Attacker.playerClient.GetComponent<ArenaGames>().Level.ToString()));
						}
						else{
							Attacker.playerClient.GetComponent<ArenaGames>().Kills++;
							rust.InventoryNotice(Attacker,string.Format(GetMessage("OnKilledKills", Attacker.userID.ToString()), Attacker.playerClient.GetComponent<ArenaGames>().Kills.ToString()));
							if(Attacker.playerClient.GetComponent<ArenaGames>().Kills >= killsWiner){
								GameOver(Attacker);
								return;
							}
						}
					}
				}
			}
		}


		void GameOver(NetUser netuser){
			string Name = netuser.displayName.ToString();
			int kills = netuser.playerClient.GetComponent<ArenaGames>().Kills;
			int deaths = netuser.playerClient.GetComponent<ArenaGames>().Deaths;
			data = GetPlayerData(netuser.userID.ToString());
			data.name = Name;
			data.kills = data.kills + kills;
			data.deaths = data.deaths + deaths;
			data.gamesWon++;
			SaveData();
			if(gameKitLevels)
			rust.BroadcastChat(chatName, string.Format(GetMessage("GameOverLevel"),  Name , netuser.playerClient.GetComponent<ArenaGames>().Level.ToString()));
			else
			rust.BroadcastChat(chatName, string.Format(GetMessage("GameOverKills"),  Name , kills.ToString(), deaths.ToString()));
			if(autoResetarterGame){
				if(autoChangedGame){
					if(!gameKitLevels && !gameRandomKits){
						gameKitLevels	= false;
						gameRandomKits	= true;
						rust.BroadcastChat(chatName, GetMessage("AutoChangeGame"));
					}
					else if(!gameKitLevels && gameRandomKits){
						gameKitLevels	= true;
						gameRandomKits	= false;
						rust.BroadcastChat(chatName, GetMessage("AutoChangeGame1"));
					}
					else if(gameKitLevels && !gameRandomKits){
						gameKitLevels	= false;
						gameRandomKits	= false;
						rust.BroadcastChat(chatName, GetMessage("AutoChangeGame2"));
					}
				}
				foreach(var pair in PlayersInArena.ToList()){
					if(pair.player == netuser.playerClient){
						ExitGame(netuser, false);
						GiveKit(netuser, namePrize);
					}
					else{
						pair.player.GetComponent<ArenaGames>().ResetStats();
						if(gameKitLevels)
						GiveKit(pair.player.netUser, nameLevel);
						else 
						GiveKit(pair.player.netUser, nameNormal);
					}
				}
				rust.BroadcastChat(chatName, string.Format(GetMessage("AutoStarter"), PlayersInArena.Count));
			}
			else{
				systemArena = false;
				foreach(var pair in PlayersInArena.ToList()){
					if(pair.player == netuser.playerClient){
						ExitGame(netuser, false);
						GiveKit(netuser, namePrize);
					}
					else
					ExitGame(pair.player.netUser, false);
				}
				PlayersInArena.Clear();
				Config["Settings: System Arena"] = systemArena;
				rust.BroadcastChat(chatName, string.Format(GetMessage("AutoCloseGame"), chatName));
				SaveConfig();
			}
		}


		void OnPlayerSpawn(PlayerClient player){
			if(systemArena){
				if(player.GetComponent<ArenaGames>()){ 
					timer.Once(3f, ()=>{
						if(player.rootControllable == null)return;
						player.rootControllable.rootCharacter.takeDamage.SetGodMode(true);
						timer.Once(timeSpawnGodMode, ()=>{
							if(player.rootControllable == null)return;
							player.rootControllable.rootCharacter.takeDamage.SetGodMode(false);
						});
						if(gameKitLevels)
						GiveKit(player.netUser, nameLevel);
						else GiveKit(player.netUser, nameNormal);
						TeleportPlayerToGame(player.netUser);
					});
				}
			}
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
			if(args == nameNormal){
				if(Kits.Count == 0)return newdict;
				if(!gameRandomKits)
				netuser.playerClient.GetComponent<ArenaGames>().KitRandom = 0;
				else{
					if(netuser.playerClient.GetComponent<ArenaGames>().KitRandom >= Kits.Count)
					netuser.playerClient.GetComponent<ArenaGames>().KitRandom = 0;
				}
				newdict = (Kits[netuser.playerClient.GetComponent<ArenaGames>().KitRandom.ToString()]) as Dictionary<string, object>;
				rust.InventoryNotice(netuser, GetMessage("KitEquiped", ID));
				inv.Clear();
				return newdict;
			}
			if(args == nameLevel){
				if(KitsLevel.Count == 0)return newdict;
				newdict = (KitsLevel[netuser.playerClient.GetComponent<ArenaGames>().KitLevel.ToString()]) as Dictionary<string, object>;
				rust.InventoryNotice(netuser, GetMessage("KitEquiped", ID));
				inv.Clear();
				return newdict;
			}
			else if(args == namePrize){
				if(KitsWiner.Count == 0)return newdict;
				var numberRandomWiner = random.Next(0, KitsWiner.Count);
				if(!awardKitRandom)numberRandomWiner = 0;
				newdict = (KitsWiner[numberRandomWiner.ToString()]) as Dictionary<string, object>;
				rust.Notice(netuser, GetMessage("KitAward", ID));
				return newdict;
			}
			else if(args == nameInventory){
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


		object GiveItem(Inventory inventory, string itemname, int amount, Inventory.Slot.Preference pref){
			if(!DataBock.ContainsKey(itemname)) return false;
			ItemDataBlock datablock = DataBock[itemname];
			inventory.AddItemAmount(DataBock[itemname], amount, pref);
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


		private void HelpsAdmins(NetUser netuser){
			string ID = netuser.userID.ToString();
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("HelpsAdmins", ID), chatName));
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
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins11", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins12", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins13", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins14", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins15", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins16", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins17", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins18", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins19", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins20", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins21", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins22", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins23", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins24", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins25", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins26", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins27", ID));
		}


		bool IfNearStructure(NetUser userx){
			PlayerClient playerclient = userx.playerClient;
			Vector3 lastPosition = playerclient.lastKnownPosition;
			UnityEngine.Object[] structObjs = Resources.FindObjectsOfTypeAll(typeof(StructureComponent));
			if(Vector3.Distance(GetClosestStructure(structObjs, lastPosition).transform.position, lastPosition) < minDistanceStructures)
			return true;
			else
			return false;
		}


		StructureComponent GetClosestStructure(UnityEngine.Object[] structObjs, Vector3 pos){
			StructureComponent theComponent = null;
			float minDistance = Mathf.Infinity;
			for(int i = 0; i < structObjs.Length; i++){
				StructureComponent component = (StructureComponent)structObjs[i];
				float distance = Vector3.Distance(component.transform.position, pos);
				if(distance < minDistance){
					theComponent = component;
					minDistance = distance;
				}
			}
			return theComponent;
		}


		void TeleportPlayerToGame(NetUser netuser){
			if(LocationsArena.Count == 0)return;
			int numberTeleport = random.Next(0, LocationsArena.Count);
			var location = LocationsArena[numberTeleport.ToString()] as Dictionary<string, object>;
			if(netuser.playerClient == null || location == null)return;
			management.TeleportPlayerToWorld(netuser.playerClient.netPlayer, new Vector3(Convert.ToSingle(location["x"]), Convert.ToSingle(location["y"]) + 5f, Convert.ToSingle(location["z"])));
		}


		void JoinGame(NetUser netuser){
			var items = GetInventory(netuser);
			var inventoryPlayer = new Dictionary<string, object>();
			inventoryPlayer.Add("items", items);
			SaveInventory.Add(netuser.userID.ToString(), inventoryPlayer);
			PlayersInArena.Add(netuser.playerClient.gameObject.AddComponent<ArenaGames>());
			TeleportPlayerToGame(netuser);
			if(gameKitLevels)
			GiveKit(netuser, nameLevel);
			else GiveKit(netuser, nameNormal);
			if(ZoneManager != null)ZoneManager.Call("AddPlayerToZoneKeepinlist", "arenaminigames", netuser.playerClient);
			rust.BroadcastChat(chatName, string.Format(GetMessage("JoinGame6"), netuser.displayName, PlayersInArena.Count));
		}


		void RemovePlayerGame(PlayerClient player){
			GameObject.Destroy(player.GetComponent<ArenaGames>());
			PlayersInArena.Remove(player.GetComponent<ArenaGames>());
			SaveInventory.Remove(player.userID.ToString());
			TeleportBack.Remove(player.userID.ToString());
			if(ZoneManager != null){ZoneManager.Call("RemovePlayerFromZoneKeepinlist", "arenaminigames", player);}
		}


		void ExitGame (NetUser netuser, bool message){
			string ID = netuser.userID.ToString();
			GiveKit(netuser, nameInventory);
			if(TeleportBack.ContainsKey(ID))
			management.TeleportPlayerToWorld(netuser.playerClient.netPlayer, TeleportBack[ID]);
			RemovePlayerGame(netuser.playerClient);
			if(message)
			rust.BroadcastChat(chatName, string.Format(GetMessage("ExitGame2"), netuser.displayName, PlayersInArena.Count));
		}


		[ChatCommand("ahelp")]
		void cmdHelpsArenaMiniGames(NetUser netuser, string command, string[] args){ 
			string ID = netuser.userID.ToString();
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Help", ID), chatName));
			rust.SendChatMessage(netuser, chatName, GetMessage("Help1", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("Help2", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("Help3", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("Help4", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("Help5", ID));
			if(AcessAdmins(netuser)){
				rust.SendChatMessage(netuser, chatName, GetMessage("Help6", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("Help7", ID));
			}
			rust.SendChatMessage(netuser, chatName, GetMessage("Help8", ID));
		}


		[ChatCommand("ainfo")]
		void cmdInfosArenaMiniGames(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Info", ID), chatName));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Info1", ID), systemArena, LocationsArena.Count));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Info2", ID), PlayersInArena.Count ,maxPlayers, maxTops));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Info3", ID), levelWiner, xpLevel, killsWiner));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Info4", ID), Kits.Count, KitsLevel.Count, KitsWiner.Count));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Info5", ID), modeWeapon, modeWeapon1));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Info6", ID), giveHealthCalories, chatName));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Info7", ID), autoResetarterGame, autoChangedGame));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Info8", ID), gameKitLevels, gameRandomKits, awardKitRandom));
			rust.SendChatMessage(netuser, chatName, GetMessage("Info9", ID));
		}


		[ChatCommand("astats")]
		void cmdTopsGames(NetUser netuser, string command, string[] args){ 
			string ID = netuser.userID.ToString();
			if(args.Length > 0 && args[0].ToString() == "games"){
				var topsGame = Data.Values.OrderByDescending(a => a.gamesWon).ToList();
				if(topsGame.Count < 1){rust.SendChatMessage(netuser, chatName, GetMessage("TopsGames", ID));return;}
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("TopsGames1", ID), maxTops, chatName));
				for(int i = 0; i < maxTops; i++){
					if(i >= topsGame.Count)break;
					var score = topsGame[i].kills - topsGame[i].deaths;
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("TopsGames2", ID), i + 1,  topsGame[i].name, topsGame[i].gamesWon, score, topsGame[i].kills, topsGame[i].deaths));
				}
				rust.SendChatMessage(netuser, chatName, GetMessage("TopsGames3", ID));
			}
			else{
				if(PlayersInArena.Count == 0){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("TopsGames4", ID), chatName));return;}
				var topsGame = PlayersInArena.OrderByDescending(a => a.Kills).ToList();
				if(gameKitLevels)
				topsGame = PlayersInArena.OrderByDescending(a => a.Level).ToList();
				string Game = "One Kit";
				if(gameKitLevels)Game = "";
				if(!gameKitLevels && gameRandomKits)Game = "Random Kit";
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("TopsGames5", ID), maxTops.ToString(), PlayersInArena.Count.ToString(), Game));
				for (int i = 0; i < maxTops; i++){
					if(i >= topsGame.Count)break;
					if(gameKitLevels)
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("TopsGames6", ID), i + 1, topsGame[i].player.userName, topsGame[i].Level));
					else{
						int score = topsGame[i].Kills - topsGame[i].Deaths;
						rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("TopsGames7", ID), i + 1, topsGame[i].player.userName, score, topsGame[i].Kills, topsGame[i].Deaths));
					}
				}
			}
		}


		[ChatCommand("winner")]
		void cmdGiveWinner(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			PlayerClient player = netuser.playerClient;
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcessAdmins", ID)); return;} 
			if(args.Length > 0){
				NetUser tragetUser = rust.FindPlayer(args[0]);
				if(tragetUser == null){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("PlayerNotFound", ID), args[0]));return;}
				player = tragetUser.playerClient;
			}
			if(player.GetComponent<ArenaGames>()){
				player.GetComponent<ArenaGames>().Level = levelWiner;
				player.GetComponent<ArenaGames>().Kills = killsWiner;
				player.GetComponent<ArenaGames>().Deaths = killsWiner / 2;
				GameOver(player.netUser);
			}
			else
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Winner", ID), player.userName));
		}


		[ChatCommand("aj")]
		void cmdJoinGame(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!systemArena){rust.Notice(netuser, GetMessage("JoinGame", ID));return;}
			else if(PlayersInArena.Count > maxPlayers){rust.Notice(netuser, GetMessage("JoinGame1", ID));return;}
			else if(netuser.playerClient.GetComponent<ArenaGames>()){rust.Notice(netuser, GetMessage("JoinGame2", ID));return;}
			else if(LocationsArena.Count == 0){rust.Notice(netuser, GetMessage("JoinGame3", ID));return;} 
			else if(IfNearStructure(netuser)){rust.Notice(netuser, GetMessage("JoinGame4", ID));return;}
			RemovePlayerGame(netuser.playerClient);
			TeleportBack.Add(ID, netuser.playerClient.lastKnownPosition);
			rust.Notice(netuser, string.Format(GetMessage("JoinGame5", ID), timeTelepotsGame));
			timer.Once(timeTelepotsGame, ()=>{
				if(netuser.playerClient == null){
					TeleportBack.Remove(ID);
					return;
				}
				JoinGame(netuser);
			});
		}


		[ChatCommand("al")]
		void cmdExitGame(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!netuser.playerClient.GetComponent<ArenaGames>()){rust.Notice(netuser, GetMessage("ExitGame", ID));return;}
			rust.Notice(netuser, string.Format(GetMessage("ExitGame1", ID), timeTelepotsGame));
			timer.Once(timeTelepotsGame, ()=>{
				if(netuser.playerClient == null)return;
				ExitGame(netuser, true);
			});
		}


		[ChatCommand("acfg")]
		void cmdArenaMiniGamesConfigurations(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcessAdmins", ID)); return;} 
			if(args.Length == 0){HelpsAdmins(netuser);return;}
			switch (args[0].ToLower()){
				case "chatname":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins1", ID));return;}
					chatName = args[1].ToString();
					Config["Settings: Chat Name"] = chatName;
					rust.Notice(netuser, string.Format(GetMessage("Configurations", ID), chatName));
					break;
				case "maxPlayers":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins2", ID));return;}
					if(!int.TryParse(args[1], out maxPlayers)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins2", ID));return;}
					Config["Settings: Max Players Arena"] = maxPlayers;
					rust.Notice(netuser, string.Format(GetMessage("Configurations1", ID), maxPlayers));
					break;
				case "maxstats":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins3", ID));return;}
					if(!int.TryParse(args[1], out maxTops)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins3", ID));return;}
					Config["Settings: Max List Stats"] = maxTops;
					rust.Notice(netuser, string.Format(GetMessage("Configurations2", ID), maxTops));
					break;
				case "xplevel":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins4", ID));return;}
					if(!double.TryParse(args[1], out xpLevel)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins4", ID));return;}
					if(xpLevel > 1 || listInvalidXpLevel.Contains(xpLevel))xpLevel = 0.5;
					Config["Settings: Xp Level"] = xpLevel;
					rust.Notice(netuser, string.Format(GetMessage("Configurations3", ID), xpLevel));
					break;
				case "levelwiner":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins5", ID));return;}
					if(!int.TryParse(args[1], out levelWiner)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins5", ID));return;}
					if(levelWiner > 15)levelWiner = 15;
					Config["Settings: Level Winer"] = levelWiner;
					rust.Notice(netuser, string.Format(GetMessage("Configurations4", ID), levelWiner));
					break;
				case "killswiner":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins6", ID));return;}
					if(!int.TryParse(args[1], out killsWiner)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins6", ID));return;}
					Config["Settings: Kills Winer"] = killsWiner;
					rust.Notice(netuser, string.Format(GetMessage("Configurations5", ID), killsWiner));
					break;
				default:
				{
					HelpsAdmins(netuser);
					break;
				}
			}
			SaveConfig();
		}


		[ChatCommand("asystem")]
		void cmdArenaMiniGamesSystem(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcessAdmins", ID)); return;} 
			if(args.Length == 0){HelpsAdmins(netuser);return;}
			switch (args[0].ToLower()){
				case "onof":
					if(systemArena){
						systemArena = false;
						foreach(var pair in PlayersInArena.ToList())
						ExitGame(pair.player.netUser, false);
						rust.BroadcastChat(chatName, string.Format(GetMessage("System"), netuser.displayName, chatName));
					}
					else{
						systemArena = true;
						rust.BroadcastChat(chatName, string.Format(GetMessage("System1"), netuser.displayName, chatName));
					}
					Config["Settings: System Arena"] = systemArena;
					break;
				case "gamekitrandom":
					if(gameRandomKits)
					gameRandomKits = false;
					else gameRandomKits = true;
					Config["Settings: Game Random Kit"] = gameRandomKits;
					rust.Notice(netuser, string.Format(GetMessage("System2", ID), gameRandomKits));
					break;
				case "gamelevel":
					if(gameKitLevels) 
					gameKitLevels = false;
					else gameKitLevels = true;
					Config["Settings: Game Kits Level"] = gameKitLevels;
					rust.Notice(netuser, string.Format(GetMessage("System3", ID), gameKitLevels));
					break;
				case "awardrandom":
					if(awardKitRandom) 
					awardKitRandom = false;
					else awardKitRandom = true;
					Config["Settings: Award Random"] = awardKitRandom;
					rust.Notice(netuser, string.Format(GetMessage("System4", ID), awardKitRandom));
					break;
				case "autoresetarter":
					if(autoResetarterGame) 
					autoResetarterGame = false;
					else autoResetarterGame = true;
					Config["Settings: Auto Resetarter Game"] = autoResetarterGame;
					rust.Notice(netuser, string.Format(GetMessage("System5", ID), autoResetarterGame));
					break;
				case "autochangedgame":
					if(autoChangedGame) 
					autoChangedGame = false;
					else autoChangedGame = true;
					Config["Settings: Auto Changed Game"] = autoChangedGame;
					rust.Notice(netuser, string.Format(GetMessage("System6", ID), autoChangedGame));
					break;
				case "healthcalories":
					if(giveHealthCalories) 
					giveHealthCalories = false;
					else giveHealthCalories = true;
					Config["Settings: Give Health Calories"] = giveHealthCalories;
					rust.Notice(netuser, string.Format(GetMessage("System7", ID), giveHealthCalories));
					break;
				case "puts":
					if(putsConnectDisconnect)
					putsConnectDisconnect = false;
					else putsConnectDisconnect = true;
					Config["Settings: Puts Connect Disconnect"] = putsConnectDisconnect;
					rust.Notice(netuser, string.Format(GetMessage("System8", ID), putsConnectDisconnect));
					break;
				default:{
					HelpsAdmins(netuser);
					break;
				}
			}
			SaveConfig();
		}


		[ChatCommand("amods")]
		void cmdArenaMiniGamesModes(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcessAdmins", ID)); return;} 
			if(args.Length == 0){HelpsAdmins(netuser);return;}
			switch (Convert.ToInt32(args[0])){
				case 1:
					modeWeapon = "Holo sight";
					modeWeapon1 = "Laser Sight";
					rust.Notice(netuser, string.Format(GetMessage("Modes", ID), modeWeapon, modeWeapon1));
					break;
				case 2:
					modeWeapon = "Silencer";
					modeWeapon1 = "Laser Sight";
					rust.Notice(netuser, string.Format(GetMessage("Modes", ID), modeWeapon, modeWeapon1));
					break;
				case 3:
					modeWeapon = "Flashlight Mod";
					modeWeapon1 = "Laser Sight";
					rust.Notice(netuser, string.Format(GetMessage("Modes", ID), modeWeapon, modeWeapon1));
					break;
				case 4:
					modeWeapon = "Silencer";
					modeWeapon1 = "Laser Sight";
					rust.Notice(netuser, string.Format(GetMessage("Modes", ID), modeWeapon, modeWeapon1));
					break;
				case 5:
					modeWeapon = "Holo sight";
					modeWeapon1 = "Silencer";
					rust.Notice(netuser, string.Format(GetMessage("Modes", ID), modeWeapon, modeWeapon1));
					break;
				default:{
					HelpsAdmins(netuser);
					break;
				}
			}
			Config["Settings: Mode Weapon"] = modeWeapon;
			Config["Settings: Mode Weapon1"] = modeWeapon1;
			SaveConfig();
		}


		[ChatCommand("akit")]
		void cmdArenaMiniGamesKits(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcessAdmins", ID)); return;} 
			if(args.Length == 0){HelpsAdmins(netuser);return;}
			switch (args[0].ToLower()){
				case "kit":
					var KitsItems = GetInventory(netuser);
					var Kit = new Dictionary<string, object>();
					Kit.Add("items", KitsItems);
					Kits.Add(Kits.Count.ToString(), Kit);
					Config["Settings: Kits Arena"] = Kits;
					rust.Notice(netuser, string.Format(GetMessage("Kits", ID), Kits.Count));
					break;
				case "kitlevel":
					var KitLevelsItems = GetInventory(netuser);
					var KitLevel = new Dictionary<string, object>();
					KitLevel.Add("items", KitLevelsItems);
					KitsLevel.Add(KitsLevel.Count.ToString(), KitLevel);
					Config["Settings: Kits Level"] = KitsLevel;
					rust.Notice(netuser, string.Format(GetMessage("Kits1", ID), KitsLevel.Count));
					break;
				case "kitwiner":
					var WinerKitsItems = GetInventory(netuser);
					var WinerKit = new Dictionary<string, object>();
					WinerKit.Add("items", WinerKitsItems);
					KitsWiner.Add(KitsWiner.Count.ToString(), WinerKit);
					Config["Settings: Kits Winer"] = KitsWiner;
					rust.Notice(netuser, string.Format(GetMessage("Kits2", ID), KitsWiner.Count));
					break;
				default:{
					HelpsAdmins(netuser);
					break;
				}
			}
			SaveConfig();
		}


		[ChatCommand("aclear")]
		void cmdArenaMiniGamesClear(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcessAdmins", ID));return;} 
			if(args.Length == 0){HelpsAdmins(netuser);return;}
			switch (args[0].ToLower()){
				case "stats":
					if(args.Length < 2){
						rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins25", ID));
						rust.SendChatMessage(netuser, chatName, GetMessage("HelpsAdmins26", ID));
						return;
					}
					if(args[1].ToString() == "clear"){
						Data.Clear();
						rust.SendChatMessage(netuser, chatName, GetMessage("Clear", ID));
					}
					else{
						string userID = GetPlayerIdData(args[1].ToString());
						if(userID == null){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Clear1", ID), args[1]));return;}
						Data.Remove(userID);
						rust.Notice(netuser, string.Format(GetMessage("Clear2", ID), args[1]));
					}
					SaveData();
					break;
				case "teleports":
					LocationsArena.Clear();
					Config["Settings: Locations Arena"] = LocationsArena;
					rust.Notice(netuser, GetMessage("Clear3", ID));
					break;
				case "kits":
					Kits.Clear();
					Config["Settings: Kits Arena"] = Kits;
					rust.Notice(netuser, GetMessage("Clear4", ID));
					break;
				case "KitsLevel":
					KitsLevel.Clear();
					Config["Settings: Kits Level"] = KitsLevel;
					rust.Notice(netuser, GetMessage("Clear5", ID));
					break;
				case "KitsWiner":
					KitsWiner.Clear();
					Config["Settings: Kits Winer"] = KitsWiner;
					rust.Notice(netuser, GetMessage("Clear6", ID));
					break;
				default:{
					HelpsAdmins(netuser);
					break;
				}
			}
			SaveConfig();
		}


		[ChatCommand("alocation")]
		void cmdArenaMiniGamesLocation(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcessAdmins", ID)); return;} 
			if(args.Length == 0){HelpsAdmins(netuser);return;}
			switch (args[0].ToLower()){
				case "teleport":
					var location = new Dictionary<string, object>();
					location.Add("x", netuser.playerClient.lastKnownPosition.x.ToString());
					location.Add("y", netuser.playerClient.lastKnownPosition.y.ToString());
					location.Add("z", netuser.playerClient.lastKnownPosition.z.ToString());
					LocationsArena.Add(LocationsArena.Count.ToString(), location);
					Config["Settings: Locations Arena"] = LocationsArena;
					rust.Notice(netuser, string.Format(GetMessage("Location", ID), LocationsArena.Count));
					break;
				case "zone":
					if(ZoneManager == null){rust.Notice(netuser, GetMessage("Location1", ID)); return;}
					string radius = "50";
					if(args.Length > 1)
					radius = args[1].ToString();
					string[] ZoneArgs = new string[] {"name", "arenaminigames", "eject", "true", "radius", radius, "nosuicide", "true", "nokits", "true", "notp", "true"};
					ZoneManager.Call("CreateOrUpdateZone", "arenaminigames", ZoneArgs, netuser.playerClient.lastKnownPosition);
					rust.Notice(netuser, string.Format(GetMessage("Location2", ID), radius));
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