/*<summary> Plugin Ranks:
Permissions Ranks:
Admin rcon or permission ranks.admin.admin

Commands Players:
/rank => View your rank stats.
/rstats => View your stats.
/tops => Tops of rank stats.
/rkits => See items kits points.
/buykit => Buy kit points.
/givepoints (Player) (Points) => Give your points.

Commands Admins:
/rank (PlayerNameOrId) => View the player rank (online offline player).
/rstats (PlayerNameOrId) => View stats about a player (online offline player).
/rkits add (KitName) (CostKit) => Add the kit to the sales list.
/rkits remove (KitName) => Remove a kit from the list buy kit.
/rkits clear => Clean all shopping list kits.
/givepoints (PlayerName Or all) (Option Amount Points)=> Give points a player.
/givelevel (PlayerName Or all) (Option Amount) => Give level a player.
</summary>*/
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using System;


namespace Oxide.Plugins{
	[Info("Ranks", "tugamano", "0.1.6")]
	class Ranks : RustLegacyPlugin{


		//Confgs plugin.
		static string chatName				= "Ranks";
		const string permissionRanks		= "ranks.admin";
		//Confgs Killing players or animals.
		static bool giveXpKillAnimals		= true;
		static bool giveXpKillPlayers		= true;
		static bool takeXpDeaths			= true;
		static bool takeXpSuicides			= true;
		//Confgs percentage take xp by deaths or suicides.
		static int percentageTakeXpDeaths	= 12;
		static int percentageTakeXpSuicides	= 6;
		//Confgs give rank name x and x levels.
		static int giveRankLevels 			= 10;
		//Confgs level up and points per level.
		static int xpUpLevel				= 100;
		static int pointsLevelUp			= 1;
		//Confgs max tops list.
		static int maxTopsList				= 5;
		//Confgs give xp killer animals.
		static int giveXpMutantBear			= 100;
		static int giveXpMutantWolf			= 90;
		static int giveXpBear				= 80;
		static int giveXpWolf				= 70;
		static int giveXpStag				= 60;
		static int giveXpChicken			= 40;
		static int giveXpRabbit				= 40;
		static int giveXpBoar				= 50;


		readonly static Dictionary<string, ItemDataBlock> DataBock = new Dictionary<string, ItemDataBlock>();
		static Dictionary<string, object> KitsPoints = new Dictionary<string, object>();
		static Dictionary<string, StoredData> Data = new Dictionary<string, StoredData>();
		static Dictionary<string, int> RanksNamesLevels	= GetRanksNamesLevels();
		static Dictionary<string, int> XpBodyParts = GetXpBodyParts();


		static Dictionary<string,int> GetXpBodyParts(){
			var dict = new Dictionary<string, int>();
			dict.Add("head", 120);
			dict.Add("neck", 120);
			dict.Add("chest", 100);
			dict.Add("torso", 100);
			dict.Add("hip", 100);
			dict.Add("left calve", 80);
			dict.Add("right calve", 80);
			dict.Add("right shoulder", 80);
			dict.Add("left shoulder", 80);
			dict.Add("right bicep", 100);
			dict.Add("left bicep", 100);
			dict.Add("right foot", 60);
			dict.Add("left foot", 60);
			dict.Add("right wrist", 60);
			dict.Add("left wrist", 60);
			dict.Add("right ankle", 60);
			dict.Add("left ankle", 60);
			return dict;
		}


		static Dictionary<string,int> GetRanksNamesLevels(){
			var dict = new Dictionary<string, int>();
			dict.Add("Recruit", giveRankLevels);
			dict.Add("Soldier", giveRankLevels * 2);
			dict.Add("Cabo", giveRankLevels * 3);
			dict.Add("Sergeant", giveRankLevels * 4);
			dict.Add("Lieutenant", giveRankLevels * 5);
			dict.Add("Captain", giveRankLevels * 6);
			dict.Add("Major", giveRankLevels * 7);
			dict.Add("Geral", giveRankLevels * 8);
			dict.Add("Marshal", giveRankLevels * 9);
			dict.Add("HeroOfWar", giveRankLevels * 10);
			return dict;
		}


		StoredData data;
		class StoredData{
			public string name {get; set;}
			public string rank {get; set;}
			public int xp {get; set;}
			public int level {get; set;}
			public int points {get; set;}
			public int suicides {get; set;}
			public int kills {get; set;}
			public int deaths {get; set;}
			public int killerAnimals {get; set;}
			public int rifle{get; set;}
			public int m4 {get; set;}
			public int mp5a4 {get; set;}
			public int shotgun {get; set;}
			public int p250 {get; set;}
			public int pistol {get; set;}
			public int revolver {get; set;}
			public int huntingbow {get; set;}
			public int hatchet {get; set;}
		}


		private StoredData GetPlayerData(string ID){
			if(!Data.TryGetValue(ID, out data)){
				data = new StoredData();
				Data.Add(ID, data);
			}
			return data;
		}


		private string GetPlayerKeyData(string args){
			foreach (var pair in Data.ToList()){
				if(pair.Value.name.ToLower() == args.ToLower() || pair.Key == args)
				return pair.Key;
			}
			return null;
		}


		private void OnRemovePlayerData(string ID){
			Data.Remove(ID);
			SaveData();
		}


		//Messages Lang API.
		string GetMessage(string key, string Id = null) => lang.GetMessage(key, this, Id);
		void LoadDefaultMessages(){
			var message = new Dictionary<string, string>{
				{"NoHaveAcess", "You are [color red]not allowed [color clear]to use this command."},
				{"NotFoundPlayer", "Player [color red]not found[color clear]!"},
				{"NotFoundPlayerData", "Player [color red]{0} [color clear]not found data!"},
				{"NotHavePoints", "You not have [color red]enough Points[color clear]!"},
				{"HelpAdmins", "Use /rhelp admin => See commands admins."},
				{"HelpAdmins1", "============================= [color lime]Helps Rank Admins [color clear]============================="},
				{"HelpAdmins2", "/rank (PlayerNameOrId) => View the player rank (online offline player)."},
				{"HelpAdmins3", "/rstats (PlayerNameOrId) => View stats about a player (online offline player)."},
				{"HelpAdmins4", "/rkits add (KitName) (CostKit) => Add the kit to the sales list."},
				{"HelpAdmins5", "/rkits remove (KitName) => Remove a kit from the list buy kit."},
				{"HelpAdmins6", "/rkits clear => Clean all shopping list kits."},
				{"HelpAdmins7", "/givepoints (PlayerName Or all) (Option Amount)=> Give points a player."},
				{"HelpAdmins8", "/givelevel (PlayerName Or all) (Option Amount) => Give level a player."},
				{"HelpAdmins9", "============================================================================="},
				{"HelpPlayers", "Use /rhelp player => See commands players."},
				{"HelpPlayers1", "============= [color lime]Helps Rank Players [color clear]============="},
				{"HelpPlayers2", "/rank => View your rank stats."},
				{"HelpPlayers3", "/rstats => View your stats."},
				{"HelpPlayers4", "/tops => Tops of rank stats."},
				{"HelpPlayers5", "/rkits => See items kits points."},
				{"HelpPlayers6", "/buykit => Buy kit points."},
				{"HelpPlayers7", "/givepoints (Player) (Points) => Give your points."},
				{"HelpPlayers8", "==========================================="},
				{"GiveXp", "[color red]{0} [color clear]+ [color Orange]{1} [color clear] Xp [color lime]{2}[color clear]/[color red]{3} [color clear] Level [color lime]{4}[color clear]."},
				{"TakeXp", "[color red]{0} [color clear]- [color Orange]{1} [color clear] Xp [color lime]{2}[color clear]/[color red]{3} [color clear] Level [color lime]{4}[color clear]."},
				{"LevelUp", "{0} You Level Up {1} + {2} Point/s."},
				{"GiveRankName", "[color red]{0} [color clear]he was promoted rank [color lime]{1}[color clear]."},
				{"AdminKitsHelp", "Use /rkits (add || remove || clear) => Admins shopping kits commands."},
				{"AdminKits", "Kit cost must be [color red]more than 0[color clear]!"},
				{"AdminKits1", "Add Kit Name {0} Cost Points {1}."},
				{"AdminKits2", "Kit {0} Removed."},
				{"AdminKits3", "Kit {0} Not found to list!"},
				{"AdminKits4", "Cleaned all the kits points!"},
				{"ListKits", "==== Kit/s [color red]Name/s [color clear]===="},
				{"ListKits1", "/kitlist [color lime]{0}"},
				{"ListKits2", "===================="},
				{"ListKits3", "========  /buykit [color lime]{0}  [color clear]========"},
				{"ListKits4", "Item: [color lime]{0} [color clear]Amount: [color red]{1}[color clear]."},
				{"NotHaveKits", "Not have [color red]kits [color red]!"},
				{"BuyKit", "========= [color lime]Kit/s Name/s [color clear]========="},
				{"BuyKit1", "/buykit [color lime]{0} [color clear]Price: [color red]{1} [color clear]Point/s."},
				{"BuyKit2", "=============================="},
				{"BuyKit3", "You bought Kit [color lime]{0}  [color clear]Amount [color Orange]{1} [color clear]Cost: [color red]{2} [color clear]Point/s."},
				{"BuyKit4", "You not have enough Points!"},
				{"BuyKit5", "Kit [color red]{0} [color clear]not found!"},
				{"Rank", "======================== [color red]Rank System [color clear]========================"},
				{"Rank1", "Rank  [color lime]{0}  [color clear]||  level:  [color lime]{1}  [color clear]||  Xp:  [color lime]{2}  [color clear]||  Points:  [color lime]{3}"},
				{"Rank2", "Xp Missing:  [color lime]{0}[color clear]/[color red]{1}   [color clear]||  Process Level: [color lime]{2}"},
				{"Rank3", "Rank:  [color lime]{0}  [color clear]||  Nest Rank:  [color lime]{1}   [color clear]||   Level Next Rank:  [color lime]{2}"},
				{"Rank4", "============================================================"},
				{"Stats", "======================= [color red]Stats Pvp [color clear]======================="},
				{"Stats1", "Stats: [color lime]{0} [color clear] ||  Score: [color lime]{1} [color clear] ||  Ratio: [color lime]{2}"},
				{"Stats2", "Killed: [color lime]{0} [color clear] ||  Animals Killed:  [color lime]{1} [color clear] ||  Death/s: [color lime]{2} [color clear]||  Suicide/s: [color lime]{3}"},
				{"Stats3", "Bolt: [color lime]{0} [color clear]Kill/s  [color clear]||  M4: [color lime]{1} [color clear]Kill/s  [color clear]||  MP5A4: [color lime]{2} [color clear]Kill/s"},
				{"Stats4", "Shotgun: [color lime]{0} [color clear]Kill/s  ||  P250: [color lime]{1} [color clear]Kill/s  ||  Pistol: [color lime]{2} [color clear]Kill/s"},
				{"Stats5", "Revolver: [color lime]{0} [color clear]Kill/s  ||  Hunting: [color lime]{1} [color clear]Kill/s  ||  Hatchet: [color lime]{2} [color clear]Kill/s "},
				{"Stats6", "======================================================="},
				{"TopsHelp", "/top [color lime]pvp [color clear]/top [color lime]weapons [color clear]/top [color lime]suicides [color clear]/top [color lime]rank"},
				{"Tops", "==================  Max Tops Pvp [color red]{0}[color clear]/[color lime]{1} [color clear]Player/s  =================="},
				{"Tops1", "Place  [color lime]{0}  [color red]{1}  [color clear]Score  [color lime]{2}  [color clear]Animal Killed  [color lime]{3}  [color clear][color clear]Kill/s  [color lime]{4}  [color clear]Death/s  [color red]{5}"},
				{"Tops2", "============================================================="},
				{"Tops3", "========================  Max Tops Kills Weapons [color lime]{0}[color clear]/[color red]{1} [color clear]Player/s  ========================"},
				{"Tops4", "Place  [color lime]{0}  [color red]{1}"},
				{"Tops5", "=================================================================================="},
				{"Tops6", "======  Max Tops Suicides [color lime]{0}[color clear]/[color red]{1}  [color clear]Player/s  ======"},
				{"Tops7", "Place  [color lime]{0}  [color red]{1} [color clear]Suicides  [color lime]{2}  [color clear]Deaths  [color red]{3}"},
				{"Tops8", "========================================="},
				{"Tops9", "=======  [color Lime]Tops Rank [color red]{0}[color clear]/[color Lime]{1} [color clear]Player/s ======="},
				{"Tops10", "Place  [color lime]{0}  [color red]{1} [color clear]Rank  [color lime]{2}  [color clear]Level  [color red]{3}"},
				{"Tops11", "========================================"},
				{"GivePoints", "[color red]{0} [color clear]gived all [color lime]{1} [color clear]point/s."},
				{"GivePoints1", "[color red]{0} [color clear]gived [color lime]{1} [color clear]point/s to [color cyan]{2}[color clear]."},
				{"GiveLevel", "[color red]{0} [color clear]gived all [color lime]{1} [color clear]level/s."},
				{"GiveLevel1", "[color red]{0} [color clear]gived [color lime]{1} [color clear]level/s to [color cyan]{2}[color clear]."}
			};
			lang.RegisterMessages(message, this);
		}


		void Loaded(){Data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, StoredData>>("Ranks");}
		void SaveData(){Interface.Oxide.DataFileSystem.WriteObject("Ranks", Data);}
		void OnServerInitialized(){
			CheckCfg<string>("Settings: Chat Name", ref chatName);
			CheckCfg<bool>("Settings: Give Xp Kill Animals", ref giveXpKillAnimals);
			CheckCfg<bool>("Settings: Give Xp Kill Players", ref giveXpKillPlayers);
			CheckCfg<bool>("Settings: Take Xp Deaths", ref takeXpDeaths);
			CheckCfg<bool>("Settings: Take Xp Suicides", ref takeXpSuicides);
			CheckCfg<int>("Settings: Percentage Take Xp Deaths", ref percentageTakeXpDeaths);
			CheckCfg<int>("Settings: Percentage Take Xp Suicides", ref percentageTakeXpSuicides);
			CheckCfg<int>("Settings: Max Tops List", ref maxTopsList);
			CheckCfg<int>("Settings: Give Rank Levels", ref giveRankLevels);
			CheckCfg<int>("Settings: Xp Up Level", ref xpUpLevel);
			CheckCfg<int>("Settings: Points Level Up", ref pointsLevelUp);
			CheckCfg<int>("Settings: Give Xp Mutant Bear", ref giveXpMutantBear);
			CheckCfg<int>("Settings: Give Xp Mutant Wolf", ref giveXpMutantWolf);
			CheckCfg<int>("Settings: Give Xp Bear", ref giveXpBear);
			CheckCfg<int>("Settings: Give Xp Wolf", ref giveXpWolf);
			CheckCfg<int>("Settings: Give Xp Stag", ref giveXpStag);
			CheckCfg<int>("Settings: Give Xp Chicken", ref giveXpChicken);
			CheckCfg<int>("Settings: Give Xp Rabbit", ref giveXpRabbit);
			CheckCfg<int>("Settings: Give Xp Boar", ref giveXpBoar);
			CheckCfg<Dictionary<string, object>>("Settings: Kits Points", ref KitsPoints);
			CheckCfg<Dictionary<string, int>>("Settings: Xp Body Parts", ref XpBodyParts);
			CheckCfg<Dictionary<string, int>>("Settings: Ranks Names", ref RanksNamesLevels);
			DataBock.Clear();
			foreach(var item in DatablockDictionary.All)
			DataBock.Add(item.name.ToLower(), item);
			permission.RegisterPermission("ranks.admin", this);
			LoadDefaultMessages();
			SaveConfig();
			Puts( "Account/s: " + Data.Count.ToString()); 
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
			if(permission.UserHasPermission(netuser.playerClient.userID.ToString(), "ranks.admin")) return true;
			return false;
		}


		private void OnPlayerConnected(NetUser netuser){
			string Name = netuser.displayName.ToString();
			data = GetPlayerData(netuser.userID.ToString());
			if(data.rank == null){
				data.rank = "Novice";
				data.name = Name;
				SaveData();
			}
			if(data.name != Name){
				data.name = Name;
				SaveData();
			}
		} 


		private void OnKilled(TakeDamage takedamage, DamageEvent damage, object tags){
			if(!(takedamage is HumanBodyTakeDamage))return;
			NetUser Attacker = damage.attacker.client?.netUser ?? null;
			NetUser Victim = damage.victim.client?.netUser ?? null;
			if(Victim != null){
				data = GetPlayerData(Victim.userID.ToString());
				data.deaths++;
				int takeXp = 0;
				if(Victim == Attacker && takeXpSuicides){
					takeXp = data.xp / percentageTakeXpSuicides;
					data.suicides++;
					TakeXp(Victim, takeXp);
					return;
				}
				if(takeXpDeaths){
					takeXp = data.xp / percentageTakeXpDeaths;
					TakeXp(Victim, takeXp);
				}
				SaveData();
			}
		}


		private void OnPlayerDeath(TakeDamage takedamage, DamageEvent damage, object tags){
			NetUser Attacker = damage.attacker.client?.netUser ?? null;
			if(Attacker == null)return;
			string weapon = tags.GetProperty("weapon").ToString();
			string bodyParts = tags.GetProperty("bodypart").ToString();
			data = GetPlayerData(Attacker.userID.ToString());
			data.kills++;
			if(weapon == "Bolt Action Rifle")
			data.rifle++;
			else if(weapon == "M4")
			data.m4++;
			else if(weapon == "MP5A4")
			data.mp5a4++;
			else if(weapon == "Shotgun")
			data.shotgun++;
			else if(weapon == "P250")
			data.p250++;
			else if(weapon == "9mm Pistol")
			data.pistol++;
			else if(weapon == "Revolver")
			data.revolver++;
			else if(weapon == "Hunting Bow")
			data.huntingbow++;
			else if(weapon == "Hatchet" || weapon == "Stone Hatchet")
			data.hatchet++;
			if(giveXpKillPlayers){
				foreach(var pair in XpBodyParts){
					if(bodyParts == pair.Key)
					GiveXp(Attacker, pair.Value);
				}
			}
		}


		private void OnAnimalDeath(TakeDamage takedamage, DamageEvent damage, object tags){
			NetUser netuser = damage.attacker.client?.netUser ?? null;
			bool mutant = takedamage.ToString().Contains("Mutant");
			if(netuser == null)return;
			if(takedamage.GetComponent<BearAI>()){
				if(mutant)
				GiveXp(netuser, giveXpMutantBear);
				else
				GiveXp(netuser, giveXpBear);
			}
			else if(takedamage.GetComponent<WolfAI>()){
				if(mutant)
				GiveXp(netuser, giveXpMutantWolf);
				else
				GiveXp(netuser, giveXpWolf);
			}
			else if(takedamage.GetComponent<StagAI>())
			GiveXp(netuser, giveXpStag);
			else if(takedamage.GetComponent<ChickenAI>())
			GiveXp(netuser, giveXpChicken);
			else if(takedamage.GetComponent<RabbitAI>())
			GiveXp(netuser, giveXpRabbit);
			else if(takedamage.GetComponent<BoarAI>())
			GiveXp(netuser, giveXpBoar);
		}


		bool MaxMinLevel(string ID, int amount){
			data = GetPlayerData(ID);
			if(amount > 0)
			if((data.level + amount) >= int.MaxValue)return true;
			else
			if((data.level - amount) <= int.MinValue)return true;
			return false;
		}


		bool MaxMinXp(string ID, int amount){
			data = GetPlayerData(ID);
			if(amount > 0)
			if((data.xp + amount) >= int.MaxValue)return true;
			else
			if((data.xp - amount) <= int.MinValue)return true;
			return false;
		}


		void GiveXp(NetUser netuser, int amount){
			object thereturn = Interface.GetMod().CallHook("canRankXp", new object[] {netuser});
			if(thereturn != null)return;
			string ID = netuser.userID.ToString();
			if(MaxMinLevel(ID, amount))return;
			data = GetPlayerData(ID);
			int amountXplevel = xpUpLevel;
			if(data.level > 1)
			amountXplevel = xpUpLevel * data.level;
			data.xp = data.xp + amount;
			if(data.xp >= amountXplevel){
				data.xp = data.xp - amountXplevel;
				GiveRankLevel(netuser, 1);
			}
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("GiveXp", ID), data.rank, amount, data.xp, amountXplevel, data.level));
			SaveData();
		}


		void TakeXp(NetUser netuser, int amount){
			object thereturn = Interface.GetMod().CallHook("canRankXp", new object[] {netuser});
			string ID = netuser.userID.ToString();
			if(MaxMinLevel(ID, amount))return;
			data = GetPlayerData(ID);
			int amountXplevel = xpUpLevel;
			if(data.level > 1)
			amountXplevel = xpUpLevel * data.level;
			data.xp = data.xp - amount;
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("TakeXp", ID), data.rank, amount, data.xp, amountXplevel, data.level));
			SaveData();
		}


		void GiveRankLevel(NetUser netuser, int level){
			string ID = netuser.userID.ToString();
			int pointsLevel = GetPointsLevel(netuser);
			data = GetPlayerData(ID);
			data.level = data.level + level;
			GivePoints(ID, pointsLevel);
			rust.Notice(netuser, string.Format(GetMessage("LevelUp", ID), data.rank, data.level, pointsLevel), "!", 10);
			foreach (var pair in RanksNamesLevels){
				if(data.level == pair.Value){
					data.rank = pair.Key;
					rust.BroadcastChat(chatName, string.Format(GetMessage("GiveRankName", ID), netuser.displayName, pair.Key));
				}
			}
			SaveData();
		}


		void GivePoints(string ID, int amount){
			data = GetPlayerData(ID);
			data.points = data.points + amount;
			SaveData();
		}


		int GetPointsLevel(NetUser netuser){
			data = GetPlayerData(netuser.userID.ToString());
			var ranks = RanksNamesLevels.ToList();
			for(int i = 0; i < ranks.Count; i++){
				if(data.rank == ranks[i].Key)
				return pointsLevelUp * i;
			}
			return pointsLevelUp;
		}



		Dictionary<string, object> GetNewKitPoints(NetUser netuser){
			var kitItems = new Dictionary<string, object>();
			var Items = new List<object>();
			IInventoryItem item;
			var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			for (int i = 0; i < 30; i++){
				if(inv.GetItem(i, out item)){
					var newObject = new Dictionary<string, object>();
					newObject.Add(item.datablock.name.ToString().ToLower(), item.datablock._splittable?(int)item.uses :1);
					if (i>=0 && i<30)
					Items.Add(newObject);
				}
			}
			kitItems.Add("main", Items);
			return kitItems;
		}


		object GiveKitPoints(NetUser netuser, string kit){
			if(KitsPoints.Count == 0)return false;
			var kitPoints = (KitsPoints[kit]) as Dictionary<string, object>;
			var kitItems = kitPoints["items"] as Dictionary<string, object>; 
			var mainList =  kitItems["main"] as List<object>;
			if (mainList.Count > 0){
				foreach (object items in mainList){
					foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>){
						GiveItem(netuser, (string)pair.Key, (int)pair.Value);
					}
				}
			}
			return true;
		}


		private void GiveItem(NetUser netuser, string item, int amount){
			var inventory = rust.GetInventory(netuser);
			if (!DataBock.ContainsKey(item.ToLower())) return;
			inventory.AddItemAmount(DataBock[item.ToLower()], amount, Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Default, false, Inventory.Slot.Kind.Default));
		}


		KeyValuePair<string, int> GetNestRankNameLevel(string ID){
			data = GetPlayerData(ID);
			for (int i = 1; i < giveRankLevels++; i++){
				foreach (var pair in RanksNamesLevels){
					if((pair.Value) == (data.level + i))
					return new KeyValuePair<string, int>(pair.Key, i);
				}
			}
			return new KeyValuePair<string, int>("Max Rank", 0);
		}


		static string GetPercentageString(double ratio){
			return ratio.ToString("0.0 [color clear]%");
		}


		[ChatCommand("rhelp")]
		void cmdRankHelps(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(args.Length == 0){
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers", ID));
				if(AcessAdmins(netuser))
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins", ID));
				return;
			}
			switch (args[0].ToLower()){
			case "player":
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers1", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers2", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers3", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers4", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers5", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers6", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers7", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers8", ID));
				break;
			case "admin":
				if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID));return;}
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins1", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins2", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins3", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins4", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins5", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins6", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins7", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins8", ID));
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins9", ID));
				break;
				default:{
					rust.SendChatMessage(netuser, chatName, GetMessage("RankHelps", ID));
					break;
				}
			}
		}


		[ChatCommand("rkits")]
		void cmdAdminKits(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID));return;}
			if(args.Length == 0){rust.SendChatMessage(netuser, chatName, GetMessage("AdminKitsHelp", ID));return;}
			string kitName = string.Empty;
			switch (args[0].ToLower()){
				case "add":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins4", ID));return;}
					int cost = 10;
					kitName = args[1].ToString().ToLower();
					if(args.Length > 2){
						if(!int.TryParse(args[2], out cost)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins8", ID));return;}
					}
					if(cost < 1){rust.SendChatMessage(netuser, chatName, GetMessage("AdminKits", ID));return;}
					var getKitItems = GetNewKitPoints(netuser);
					if(KitsPoints.ContainsKey(kitName))
					KitsPoints.Remove(kitName);
					var kit = new Dictionary<string, object>();
					kit.Add("items", getKitItems);
					kit.Add("cost", cost);
					KitsPoints.Add(kitName, kit);
					rust.Notice(netuser, string.Format(GetMessage("AdminKits1", ID), kitName, cost));
					break;
				case "remove":
					if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins5", ID));return;}
					kitName = args[1].ToString().ToLower();
					if(KitsPoints.ContainsKey(kitName)){
						KitsPoints.Remove(kitName);
						rust.Notice(netuser, string.Format(GetMessage("AdminKits2", ID), kitName));
					}
					else
					rust.Notice(netuser, string.Format(GetMessage("AdminKits3", ID), kitName));
					break;
				case "clear":
					KitsPoints.Clear();
					rust.Notice(netuser, GetMessage("AdminKits4", ID));
					break;
					default:{
						rust.SendChatMessage(netuser, chatName, GetMessage("AdminKitsHelp", ID));
						break;
					}
				}
				Config["Settings: Kits Points"] = KitsPoints;
				SaveConfig();
			}


		[ChatCommand("rkits")]
		void cmdListKits(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(KitsPoints.Count == 0){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveKits", ID));return;}
			if(args.Length == 0){
				rust.SendChatMessage(netuser, chatName, GetMessage("ListKits", ID));
				foreach (var pair in KitsPoints)
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("ListKits1", ID), pair.Key));
				rust.SendChatMessage(netuser, chatName,  GetMessage("ListKits2", ID));
				return;
			}
			string kitName = string.Empty;
			if(args.Length > 0)
			kitName = args[0].ToString().ToLower();
			if(!KitsPoints.ContainsKey(kitName)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Kit [color red]{0} [color clear]does not exist in kits points!", ID), kitName)); return;}
			var kitPoints = (KitsPoints[kitName]) as Dictionary<string, object>;
			var kitPointsItems = kitPoints["items"] as Dictionary<string, object>;
			var listItems = kitPointsItems["main"] as List<object>;
			rust.SendChatMessage(netuser, chatName, string.Format( GetMessage("ListKits3", ID), kitName));
			foreach (object items in listItems){
				foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>)
				rust.SendChatMessage(netuser, chatName, string.Format( GetMessage("ListKits4", ID), (string)pair.Key, (int)pair.Value));
			}
		}


		[ChatCommand("buykit")]
		void cmdBuyKitPoints(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(args.Length == 0){
				if(KitsPoints.Count == 0){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveKits", ID));return;}
				rust.SendChatMessage(netuser, chatName, GetMessage("BuyKit", ID));
				foreach (var pair in KitsPoints){
					var costKit = (KitsPoints[pair.Key]) as Dictionary<string, object>;
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("BuyKit1", ID), pair.Key, costKit["cost"]));
				}
				rust.SendChatMessage(netuser, chatName, GetMessage("BuyKit2", ID));
				return;
			}
			data = GetPlayerData(ID);
			int amount = 1;
			string kitName = args[0].ToString().ToLower();
			if(args.Length > 1){
				if(!int.TryParse(args[1], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers6", ID));return;}
			}
			if(!KitsPoints.ContainsKey(kitName)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("BuyKit5", ID), kitName));return;}
			var costKitPoints = (KitsPoints[kitName]) as Dictionary<string, object>;
			int cost = Convert.ToInt32(costKitPoints["cost"]) * amount;
			if(data.points < cost){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("BuyKit4", ID), kitName));return;}
			data.points = data.points - cost;
			for(int i = 0; i < amount; i++)
			GiveKitPoints(netuser, kitName);
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("BuyKit3", ID), kitName, amount, cost));
		}


		[ChatCommand("rank")]
		void cmdRank(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			string userID = ID;
			if(args.Length > 0){
				if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID)); return;} 
				NetUser tragetUser = rust.FindPlayer(args[0]);
				if(tragetUser == null){
					userID = GetPlayerKeyData(args[0].ToString());
					if(userID == null){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("NotFoundPlayerData", ID), args[0]));return;}
				}
				else
				userID = tragetUser.userID.ToString();
			}
			data = GetPlayerData(userID);
			var nameLevelNestRank = GetNestRankNameLevel(ID);
			string processLevel = GetPercentageString((double)data.xp / (xpUpLevel * data.level));
			int xpMissing = xpUpLevel - data.xp;
			int xpLevel =  xpUpLevel;
			if(data.level > 0){
				xpMissing = ((xpUpLevel * data.level) - data.xp);
				xpLevel =  xpUpLevel * data.level;
			}
			rust.SendChatMessage(netuser, chatName, GetMessage("Rank", ID));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Rank1", ID), data.name, data.level, data.xp, data.points));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Rank2", ID), xpMissing, xpLevel, processLevel));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Rank3", ID), data.rank, nameLevelNestRank.Key, nameLevelNestRank.Value));
			rust.SendChatMessage(netuser, chatName, GetMessage("Rank4", ID));
		}


		[ChatCommand("rstats")]
		void cmdStats(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			string userID = ID;
			if(args.Length > 0){
				if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID)); return;} 
				NetUser tragetUser = rust.FindPlayer(args[0]);
				if(tragetUser == null){
					userID = GetPlayerKeyData(args[0].ToString());
					if(userID == null){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("NotFoundPlayerData", ID), args[0]));return;}
				}
				else
				userID = tragetUser.userID.ToString();
			}
			data = GetPlayerData(userID);
			int score = data.kills - data.deaths;
			string ratio = GetPercentageString((double)data.deaths / data.kills);
			rust.SendChatMessage(netuser, chatName, GetMessage("Stats", ID));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Stats1", ID), data.name, score, ratio));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Stats2", ID), data.kills, data.killerAnimals, data.deaths, data.suicides));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Stats3", ID), data.rifle, data.m4, data.mp5a4));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Stats4", ID), data.shotgun, data.p250, data.pistol));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Stats5", ID), data.revolver, data.huntingbow, data.hatchet));
			rust.SendChatMessage(netuser, chatName, GetMessage("Stats6", ID));
			
		}


		[ChatCommand("tops")]
		void cmdTops(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(args.Length == 0){rust.SendChatMessage(netuser, chatName, GetMessage("TopsHelp", ID));return;}
			switch (args[0].ToLower()){
			case "pvp":
				var topKills = Data.Values.OrderByDescending(a => a.kills).ToList();
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Tops", ID), maxTopsList, Data.Count));
				for(int i = 0; i < maxTopsList; i++){
					if(i >= topKills.Count)break;
					int score = topKills[i].kills - topKills[i].deaths;
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Tops1", ID), i + 1, topKills[i].name, score,  topKills[i].killerAnimals, topKills[i].kills, topKills[i].deaths));
				}
				rust.SendChatMessage(netuser, chatName, GetMessage("Tops2", ID));
				break;
			case "weapons":
				var statsWeapons = new Dictionary<string, string>();
				foreach (var pair in Data.ToList())
				statsWeapons.Add(pair.Value.name, "  [color clear]Rifle:  [color lime]" + pair.Value.rifle.ToString() + "  [color clear]M4:  [color lime]" + pair.Value.m4.ToString() + "  [color clear]MP5A4:  [color lime]" + pair.Value.mp5a4.ToString() + "  [color clear]Shotgun:  [color lime]" + pair.Value.shotgun.ToString() + 
				"  [color clear]P250  [color lime]" + pair.Value.p250.ToString() + "  [color clear]Pistol:  [color lime]" + pair.Value.pistol.ToString() + "  [color clear]Hunting:  [color lime]" + pair.Value.huntingbow.ToString() + "  [color clear]Hatchet:  [color lime]" + pair.Value.hatchet.ToString());
				var topsWeapons = statsWeapons.OrderByDescending(a => a.Value).ToList();
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Tops3", ID), maxTopsList, Data.Count ));
				for(int i = 0; i < maxTopsList; i++){
					if(i >= topsWeapons.Count)break;
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Tops4", ID), i + 1, topsWeapons[i].Key));
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("{0}", ID), topsWeapons[i].Value));
				}
				rust.SendChatMessage(netuser, chatName, GetMessage("Tops5", ID));
				break;
			case "suicides":
				var topSuicides = Data.Values.OrderByDescending(a => a.suicides).ToList();
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Tops6", ID), maxTopsList, Data.Count ));
				for(int i = 0; i < maxTopsList; i++){
					if(i >= topSuicides.Count)break;
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Tops7", ID), i + 1, topSuicides[i].name , topSuicides[i].suicides, topSuicides[i].deaths));
				}
				rust.SendChatMessage(netuser, chatName, GetMessage("Tops8", ID));
				break;
			case "rank":
				var topRank = Data.Values.OrderByDescending(a => a.level).ToList();
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Tops9", ID), maxTopsList, Data.Count ));
				for(int i = 0; i < maxTopsList; i++){
					if(i >= topRank.Count)break;
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Tops10", ID), i + 1, topRank[i].name , topRank[i].rank, topRank[i].level));
				}
				rust.SendChatMessage(netuser, chatName, GetMessage("Tops11", ID));
				break;
				default:{
					rust.SendChatMessage(netuser, chatName, GetMessage("TopsHelp", ID));
					break;
				}
			}
		}


		[ChatCommand("givepoints")]
		void cmdGivePoints(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			int amount = 1;
			if(args.Length > 0 && args[0].ToString() == "all"){
				if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID));return;}
				if(args.Length > 1){
					if(!int.TryParse(args[1], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins7", ID));return;}
				}
				foreach(PlayerClient player in PlayerClient.All)
				GivePoints(player.userID.ToString(), amount);
				rust.BroadcastChat(chatName, string.Format(GetMessage("GivePoints"), netuser.displayName, amount));
				return;
			}
			if(args.Length < 2){
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers7", ID));
				if(AcessAdmins(netuser))
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins7", ID));
				return;
			}
			data = GetPlayerData(ID);
			NetUser tragetUser = rust.FindPlayer(args[0]);
			if(tragetUser == null){rust.SendChatMessage(netuser, chatName, GetMessage("NotFoundPlayer", ID));return;}
			if(!int.TryParse(args[1], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers7", ID));return;}
			if(!AcessAdmins(netuser)){
				if(data.points < amount){rust.SendChatMessage(netuser, chatName, GetMessage("NotHavePoints", ID));return;}
				data.points = data.points - amount;
			}
			GivePoints(tragetUser.userID.ToString(), amount);
			rust.BroadcastChat(chatName, string.Format(GetMessage("GivePoints1"), netuser.displayName, amount, tragetUser.displayName));
		}


		[ChatCommand("givelevel")]
		void cmdGiveLevel(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NoHaveAcess", ID));return;}
			if(args.Length == 0){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins8", ID));return;}
			int amount = 1;
			if(args[0].ToString() == "all"){
				if(args.Length > 1){
					if(!int.TryParse(args[1], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins8", ID));return;}
				}
				foreach (PlayerClient player in PlayerClient.All)
				GiveRankLevel(player.netUser, amount);
				rust.BroadcastChat(chatName, string.Format(GetMessage("GiveLevel"), netuser.displayName, amount));
				return;
			}
			NetUser tragetUser = rust.FindPlayer(args[0]);
			if(tragetUser == null){rust.SendChatMessage(netuser, chatName, GetMessage("NotFoundPlayer", ID));return;}
			if(args.Length > 1){
				if(!int.TryParse(args[1], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins8", ID));return;}
			}
			GiveRankLevel(tragetUser, amount);
			rust.BroadcastChat(chatName, string.Format(GetMessage("GiveLevel1"), netuser.displayName, amount, data.name));
		}
	}
}