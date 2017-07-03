/*<summary> Plugin MoneySystem:
Permissions MoneySystem:
Admin rcon or permission moneysystem.admin
Permissions Money Premium:
admin 
mod
vip
donator

Commands Players:
/money Or /bank => See your money stats.
/money tops => List tops of richest players.
/money give (PlayerName) (Amount) => Give money to a player.
/bank deposit (Amount) => Deposit money in the bank.
/bank take (Amount) => Take money from the bank.
/shop => View list of shops Or view items in the shop.
/buy (ItemName) (Amount) => Buy an item in the Shops.
/sell (ItemName) (Amount) => Sell an item in the Shops.
/commerce (PlayerName) (ItemName) (Amount) (Price)=> Make an offer.
/acceptcommerce => Accept offer.

Commands Admins:
/money look => View a player's money stats.
/money give all (Amount) => Give money to all players.
/money take (PlayerName) (AmountTake) => Take money of a player.
/money set (PlayerName) (AmountSet) => Set money of a player.
/ashop add (ShopName) (ItemName) (BuyPrice) (SalePrice) => Add item Shops.
/ashop remove (ItemName) => Remove item in shop/s.
/ashop remove shop (ShopName) => Remove Shop.
/ashop clear => Clear all shops.
/ashop location => Add location radius shops.
/ashop clearlocation => Clear all locations shops.
</summary>*/

using System.Linq;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using System;


namespace Oxide.Plugins{
		[Info("Money System", "tugamano", "1.0.0")]
		class MoneySystem : RustLegacyPlugin{


		// Confgs Plugin.
		[PluginReference] Plugin Location;
		static Plugins.Timer timerTrade;
		static Plugins.Timer timerAutoGiveMoney;
		private static List<string> permissionsPremium = new List<string>(new string[]{"admin", "mod", "vip", "donator"});
		private const string permissionMoney	= "moneysystem.admin";
		static string chatName					= "Money";
		static string symbolMoney				= "$";
		// Confgs commerce.
		static bool commerce					= true;
		static float timeDurationOfferTrade		= 30f;
		// Confgs Shops.
		static bool activatedLocationsShops		= false;
		static int maxShops						= 25;
		static int maxItemsPerShop				= 25;
		// Confgs max tops list.
		static int maxTopsList					= 5;
		// Confgs bank deposit take money.
		static bool bank						= true;
		static int minDepositTakeMoneyBank		= 500;
		static int percentageCostkWorksBank		= 15;
		// Confgs auto give money.
		static bool autoGiveMoney				= true;
		static float timeAutoGiveMoney			= 10f;
		static int amountAutoGiveMoneyPremium	= 500;
		static int amountAutoGiveMoneyNormal	= 250;
		// Confgs give money for kill/deaths/suicides.
		static bool giveMoneyByKillPlayer		= true;
		static bool giveMoneyByKillAnimals		= true;
		static bool takeMoneyByDeaths			= true;
		static bool takeMoneyBySuicide			= true;
		static int percentageTakeMoneyBySuicide	= 4;
		static int percentageTakeMoneyByDeaths	= 8;
		// Confgs give money for killing animals.
		static int moneyByKillMutantBear		= 100;
		static int moneyByKillMutantWolf		= 90;
		static int moneyByKillBear				= 80;
		static int moneyByKillWolf				= 70;
		static int moneyByKillStag				= 60;
		static int moneyByKillChicken			= 40;
		static int moneyByKillRabbit			= 40;
		static int moneyByKillBoar				= 50;


		static readonly Dictionary<string, ItemDataBlock> DataBock = new Dictionary<string, ItemDataBlock>();
		static Dictionary<string, StoredData> MoneyData = new Dictionary<string, StoredData>();
		static Dictionary<string, object> LocationShops = new Dictionary<string, object>();
		static Dictionary<NetUser, Trade> Commerce = new Dictionary<NetUser, Trade>();
		static Dictionary<string, Shop> Shops = new Dictionary<string, Shop>();


		void ShopsMoneySystem(){
			Shops.Clear();
			// Add shop resources.
			AddItemShop("resources", "animal fat", 20, 8);
			AddItemShop("resources", "leather", 18, 7);
			AddItemShop("resources", "blood", 17, 6);
			AddItemShop("resources", "wood", 20, 8);
			AddItemShop("resources", "metal ore", 10, 3);
			AddItemShop("resources", "sulfur ore", 15, 700);
			AddItemShop("resources", "sulfur", 20, 8);
			AddItemShop("resources", "metal fragments", 15, 4);
			AddItemShop("resources", "gunpowder", 15, 4);
			// Add shop weapons.
			AddItemShop("weapons", "bolt action rifle", 12000, 700);
			AddItemShop("weapons", "m4", 10000, 600);
			AddItemShop("weapons", "mp5a4", 9000, 500);
			AddItemShop("weapons", "shotgun", 8000, 400);
			AddItemShop("weapons", "p250", 7000, 300);
			AddItemShop("weapons", "9mm pistol", 6000, 200);
			AddItemShop("weapons", "revolver", 4000, 100);
			// Add shop ammo.
			AddItemShop("ammo", "556 ammo", 20, 8);
			AddItemShop("ammo", "9mm ammo", 15, 7);
			AddItemShop("ammo", "shotgun shells", 10, 6);
			// Add shop mods.
			AddItemShop("mods", "holo sight", 1500, 100);
			AddItemShop("mods", "laser sight", 1500, 100);
			AddItemShop("mods", "silencer", 1500, 100);
			AddItemShop("mods", "flashlight mod", 1500, 100);
			Puts("New shops settings have been created.");
		}


		string GetMessage(string key, string steamid = null) => lang.GetMessage(key, this, steamid);
		void LoadDefaultMessages(){
			var messages = new Dictionary<string, string>{
				{"NotHaveAcess", "You are [color red]not allowed [color clear]to use this command."},
				{"NotHaveLocations", "Has no location of shops [color red]created[color clear]!"},
				{"PlayerNotFound", "Player [color red]not [color clear]found!"},
				{"PlayerDataNotFound", "Player does [color red]not exist [color clear]or be registered!"},
				{"ItemNotFoundShops", "Item [color red]{0} [color clear]not found in shops!"},
				{"ItemNotFoundDataBock", "Item [color red]{0} [color clear]not found in data bock!"},
				{"NotHaveMoney", "You do not have [color red]{0} {1} [color clear]!"},
				{"HaveMaxMoneyBank", "You already have the [color lime]maximum amount [color clear]of money allowed in the bank."},
				{"HelpAdmins", "======================== [color lime]Help Admins Money Admins [color clear]========================"},
				{"HelpAdmins1", "Use /money look => View a player's money stats."},
				{"HelpAdmins2", "Use /money give all (Amount) => Give money to all players."},
				{"HelpAdmins3", "Use /money take (PlayerName) (AmountTake) => Take money of a player."},
				{"HelpAdmins4", "Use /money set (PlayerName) (AmountSet) => Set money of a player."},
				{"HelpAdmins5", "Use /ashop add (ShopName) (ItemName) (BuyPrice) (SalePrice) => Add item Shops."},
				{"HelpAdmins6", "Use /ashop remove (ItemName) => Remove item in shop/s."},
				{"HelpAdmins7", "Use /ashop remove shop (ShopName) => Remove Shop."},
				{"HelpAdmins8", "Use /ashop clear => Clear all shops."},
				{"HelpAdmins9", "Use /ashop location (Radius) => Add location radius shops."},
				{"HelpAdmins10", "Use /ashop clearlocations => Clear all locations shops."},
				{"HelpAdmins11", "========================================================================"},
				{"HelpPlayers", "========================= [color lime]Help Players Money [color clear]========================="},
				{"HelpPlayers1", "Use /money Or /bank => See your money stats."},
				{"HelpPlayers2", "Use /money tops => List tops of richest players."},
				{"HelpPlayers3", "Use /money give (PlayerName) (Amount) => Give money to a player."},
				{"HelpPlayers4", "Use /bank deposit (Amount) => Deposit money in the bank."},
				{"HelpPlayers5", "Use /bank take (Amount) => Take money from the bank."},
				{"HelpPlayers6", "Use /shop => View list of shops Or view items in the shop."},
				{"HelpPlayers7", "Use /buy (ItemName) (Amount) => Buy an item in the Shops."},
				{"HelpPlayers8", "Use /sell (ItemName) (Amount) => Sell an item in the Shops."},
				{"HelpPlayers9", "Use /commerce (PlayerName) (ItemName) (Amount) (Price)=> Make an offer."},
				{"HelpPlayers10", "Use /acceptcommerce => Accept offer."},
				{"HelpPlayers11", "==================================================================="},
				{"StatsMoney", "======== [color lime]Stats Money [color clear]========"},
				{"StatsMoney1", "Stats Money for [color red]{0}"},
				{"StatsMoney2", "Money in bank [color lime]{0} {1}"},
				{"StatsMoney3", "Has in itself [color cyan]{0} {1}"},
				{"StatsMoney4", "Total money [color lime]{0} {1}"},
				{"StatsMoney5", "==========================="},
				{"TopsRichs", "============= [color lime]Tops Rich Players [color clear]============="},
				{"TopsRichs1", "Place [color lime]{0}  [color red]{1}  [color clear]total money [color lime]{2} {3}"},
				{"TopsRichs2", "=========================================="},
				{"Shops", "There are [color red]no shops open [color clear]in the shops."},
				{"Shops1", "=========== [color lime]Shops [color clear]==========="},
				{"Shops2", "/shop [color Lime]{0} [color clear]Items [color red]{1}"},
				{"Shops3", "============================"},
				{"Shops4", "Shop [color red]{0} [color clear]not found!"},
				{"Shops5", "Has no items in this Shops."},
				{"Shops6", "============= [color lime]Shop Items [color clear]============="},
				{"Shops7", "Shop [color lime]{0} [color clear]Items [color red]{1}"},
				{"Shops8", "Item [color lime]{0} [color clear]Sell [color lime]{1} {2}"},
				{"Shops9", "Item [color lime]{0} [color clear]Buy [color cyan]{1} {2}"},
				{"Shops10", "Item [color lime]{0} [color clear]Buy [color red]{1} {3} [color clear]Sell [color lime]{2} {3}"},
				{"Shops11", "====================================="},
				{"AdminShop", "Use /ashop (add Or remove Or clear) => Cmds Shops."},
				{"AdminShop1", "Success shop was cleaned!"},
				{"AdminShop2", "Successfully all locations of the shops were cleaned!"},
				{"AddItemShop", "Reached the [color red]maximum number [color clear]of shops created!"},
				{"AddItemShop1", "Shop reached[color red] maximum of [color clear]items create a new one."},
				{"AddItemShop2", "Success add {0} to shop {1} buy {2} {4} sell {3} {4}"},
				{"RemoveShop", "Success shop {0} removed for shop!"},
				{"RemoveShop1", "Success item {0} removed for shop!"},
				{"AddLocationsShops", "Success creat new location shops number {0} radius {1}."},
				{"LockLocationShops", "You are [color red]{0} [color clear]meter/s from the shops! location [color lime]{1} [color clear]Max [color lime]{2} [color clear]meter/s to use it."},
				{"Bank", "Bank system is [color red]disabled[color clear]."},
				{"Bank1", "Use /bank (see Or deposit Or take) => Cmds bank money."},
				{"Bank2", "You can only deposit in the bank sums of money equal to or more than [color red]{0} {1}"},
				{"Bank3", "You do not have [color red]{0} {1}"},
				{"Bank4", "You have deposited [color lime]{0} {1} [color clear]in the bank."},
				{"Bank5", "You can only take in the bank sums of money equal to or more than [color red]{0} {1}"},
				{"Bank6", "You do not have [color red]{0} {2} [color clear] money in bank [color lime]{1} {2}"},
				{"Bank7", "You have take [color lime]{0} {2} [color clear]of the bank's work cost [color red]{1} {2}"},
				{"Buy", "Amount buy must be [color red]more than 0[color clear]!"},
				{"Buy1", "Item [color red]{0} [color clear]not for buy!"},
				{"Buy2", "You bought [color lime]{0} [color clear]quantity [color cyan]{1}  [color clear]cost [color red]{2} {3}"},
				{"Sell", "Item [color red]{0} [color clear]is not for sell!"},
				{"Sell1", "Amount of sale must be [color red]more than 0[color clear]!"},
				{"Sell2", "You do not have any [color red]{0} [color clear]in your inventory!"},
				{"Sell3", "You sold [color lime]{0} [color clear]Amount [color Orange]{1} [color clear]Total Gain [color red]{2} {3}"},
				{"AutoGiveMoney", "Auto give money premium [color lime]+ {0} {1}"},
				{"AutoGiveMoney1", "Auto give money normal [color lime]+ {0} {1}"},
				{"GiveMoney", "[color lime]+ [color cyan]{0} {2} [color clear]Total Money [color lime]{1} {2}"},
				{"GiveMoney1", "[color red]{0} [color clear]gived to all [color lime]{2} {3}"},
				{"GiveMoney2", "You can not give money to [color red]yourself!"},
				{"GiveMoney3", "You can not give less than [color red]1 {0}"},
				{"GiveMoney4", "You do not have [color red]{0} {1}."},
				{"GiveMoney5", "[color red]{0} [color clear]gived to [color cyan]{1} [color lime]{2} {3}"},
				{"SetMoney", "Set {0} {3} from the {1} Have total {2} {3}"},
				{"TakeMoney", "[color red]- {0} {2} [color clear]Total Money [color lime]{1} {2}"},
				{"TakeMoney1", "Taking {0} {3} from the {1} Have total {2} {3}"},
				{"CommerceOff", "Commerce [color red]is off."},
				{"CommerceCanceled", "[color red]{0} [color celar]left server! Trade canceled!"},
				{"Commerce", "Can not make an offer [color red]to you!"},
				{"Commerce1", "This player was in trade with another player"},
				{"Commerce2", "Amount of item must be [color red]more than 0[color clear]!"},
				{"Commerce3", "Amount of price should not be [color red]less than 0[color clear]!"},
				{"Commerce4", "You do not have any [color red]{0} [color clear]in your inventory!"},
				{"Commerce5", "You made an offer to [color red]{0} [color clear]Item [color lime]{1} [color clear]Amount [color red]{2} [color clear]Buy [color lime]{3} {4}"},
				{"Commerce6", "[color red]{0} [color clear]made an offer to you! Item [color red]{1} [color clear]Amount [color lime]{2} [color clear]Buy [color red]{3} {4}"},
				{"Commerce7", "You have [color red]{0} [color clear]seg/s to accept [color lime]/acceptcommerce [color clear]the offer."},
				{"Commerce8", "[color red]{0} [color clear]did not accept his offer! [color clear]Trade canceled."},
				{"Commerce9", "Time to accept trade [color red]{0} exhausted! [color clear]Trade canceled."},
				{"AcceptCommerce", "Player is [color red]not connected [color clear]offer can not proceed."},
				{"AcceptCommerce1", "You accepted [color red]{0} [color clear] offer! [color lime]{1} [color clear]is in your inventory."},
				{"AcceptCommerce2", "[color red]{0} [color clear]accepted your offer! Total Gain [color lime]{1} {2}"},
				{"AcceptCommerce3", "You do not have any offer to accept!"}
			}; 
			lang.RegisterMessages(messages, this);
		}


		void Loaded(){MoneyData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, StoredData>>("MoneyData"); Shops = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Shop>>("ShopsData");}
		void SaveData(){Interface.Oxide.DataFileSystem.WriteObject("MoneyData", MoneyData); Interface.Oxide.DataFileSystem.WriteObject("ShopsData", Shops);}


		void OnServerInitialized(){
			CheckCfg<string>("Settings: Chat Name", ref chatName);
			CheckCfg<string>("Settings: Symbol Money", ref symbolMoney);
			CheckCfg<bool>("Settings: Commerce", ref commerce);
			CheckCfg<float>("Settings: Time Duration Offer Trade", ref timeDurationOfferTrade);
			CheckCfg<bool>("Settings: Activated Locations Shops", ref activatedLocationsShops);
			CheckCfg<int>("Settings: Maximum Number Shops", ref maxShops);
			CheckCfg<int>("Settings: Maximum Items In Shop", ref maxItemsPerShop);
			CheckCfg<int>("Settings: Max Tops List", ref maxTopsList);
			CheckCfg<bool>("Settings: Bank", ref bank);
			CheckCfg<int>("Settings: Min Deposit Take Money Bank", ref minDepositTakeMoneyBank);
			CheckCfg<int>("Settings: Percentage Cost Works Bank", ref percentageCostkWorksBank);
			CheckCfg<bool>("Settings: Auto Give Money", ref autoGiveMoney);
			CheckCfg<float>("Settings: Time Auto Give Money", ref timeAutoGiveMoney);
			CheckCfg<int>("Settings: Amount Auto Give Money Premium", ref amountAutoGiveMoneyPremium);
			CheckCfg<int>("Settings: Amount Auto Give Money Normal", ref amountAutoGiveMoneyNormal);
			CheckCfg<bool>("Settings: Give Money By Killing Players", ref giveMoneyByKillPlayer);
			CheckCfg<bool>("Settings: Give Money By Killing Animals", ref giveMoneyByKillAnimals);
			CheckCfg<bool>("Settings: Take Money By Deaths", ref takeMoneyByDeaths);
			CheckCfg<bool>("Settings: Take Money By Suicide", ref takeMoneyBySuicide);
			CheckCfg<int>("Settings: Percentage Take Money By Deaths", ref percentageTakeMoneyByDeaths);
			CheckCfg<int>("Settings: Percentage Take Money By Suicide", ref percentageTakeMoneyBySuicide);
			CheckCfg<int>("Settings: Money By Kill Mutant Bear", ref moneyByKillMutantBear);
			CheckCfg<int>("Settings: Money By Kill Mutant Wolf", ref moneyByKillMutantWolf);
			CheckCfg<int>("Settings: Money By Kill Bear", ref moneyByKillBear);
			CheckCfg<int>("Settings: Money By Kill Wolf", ref moneyByKillWolf);
			CheckCfg<int>("Settings: Money By Kill Stag", ref moneyByKillStag);
			CheckCfg<int>("Settings: Money By Kill Chicken", ref moneyByKillChicken);
			CheckCfg<int>("Settings: Money By Kill Rabbit", ref  moneyByKillRabbit);
			CheckCfg<int>("Settings: Money By Kill Boar", ref moneyByKillBoar);
			CheckCfg<Dictionary<string, object>>("Settings: Location Shops", ref LocationShops);
			SaveConfig();
			DataBock.Clear();
			foreach(var item in DatablockDictionary.All)
			DataBock.Add(item.name.ToLower(), item);
			permission.RegisterPermission(permissionMoney, this);
			if(autoGiveMoney)
			AutoGiveMoney();
			LoadDefaultMessages();
			if(Shops.Count == 0)
			ShopsMoneySystem();
		}


		protected override void LoadDefaultConfig(){}  
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}


		Shop  shop;
		public class Shop{
			public Dictionary<string, InfoItem> Items = new Dictionary<string, InfoItem>();
		}


		InfoItem  infoitem;
		public class InfoItem{
			public int buy {get; set;}
			public int sell {get; set;}
		}


		Trade  trade;
		public class Trade{
			public PlayerClient player{get; set;}
			public string item {get; set;}
			public int amount {get; set;}
			public int buy {get; set;}
		}


		StoredData  data;
		public class StoredData{
			public string name {get; set;}
			public int money {get; set;}
			public int bank {get; set;}
			public int TotalMoney(){
				return money + bank;
			}
		}


		StoredData GetPlayerData(string ID){
			if(!MoneyData.TryGetValue(ID, out data)){
				data = new StoredData();
				MoneyData.Add(ID, data);
			}
			return data;
		}


		string GetPlayerIdData(string args){
			foreach (var pair in MoneyData){
				if(pair.Value.name.ToLower() == args.ToLower() || pair.Key == args)
				return pair.Key;
			}
			return null;
		}


		private void OnRemovePlayerData(string ID){
			MoneyData.Remove(ID);
			SaveData();
		}


		bool AcessAdmins(NetUser netuser){
			if(netuser.CanAdmin())return true; 
			if(permission.UserHasPermission(netuser.userID.ToString(), permissionMoney))return true;
			return false;
		}


		bool AcessPremiumAccounts(NetUser netuser){
			if(netuser.CanAdmin())return true;
			if(permission.UserHasPermission(netuser.userID.ToString(), permissionMoney))return true;
			foreach(var permissionn in permissionsPremium)
			if(permission.UserHasPermission(netuser.userID.ToString(), permissionn))return true;
			return false;
		}


		bool MaxMinMoney(string ID, int amount){
			data = GetPlayerData(ID);
			if(amount > 0)
			if((data.money + amount) >= int.MaxValue)return true;
			else
			if((data.money - amount) <= int.MinValue)return true;
			return false;
		}


		bool MaxMinBankMoney(string ID, int amount){
			data = GetPlayerData(ID);
			if(amount > 0)
			if((data.bank + amount) >= int.MaxValue)return true;
			else
			if((data.bank - amount) <= int.MinValue)return true;
			return false;
		}


		bool IfPlayerIsInRadiusShop(NetUser netuser){
			if(LocationShops.Count == 0)return false;
			foreach(var pair in LocationShops){
				var location = LocationShops[pair.Key] as Dictionary<string, object>;
				if(location == null)continue;
				double distanceShop = Math.Floor(Vector3.Distance(netuser.playerClient.lastKnownPosition, new Vector3(Convert.ToSingle(location["x"]), Convert.ToSingle(location["y"]), Convert.ToSingle(location["z"]))));
				if(distanceShop <= (double)location["radius"])return true;
			}
			return false;
		}


		private void OnPlayerConnected(NetUser netuser){
			string ID = netuser.userID.ToString();
			string Name = netuser.displayName.ToString();
			data = GetPlayerData(ID);
			if(data.name != Name){
				data.name = Name;
				SaveData();
			}
		}


		private void OnPlayerDisconnected(uLink.NetworkPlayer netPlayer){
			NetUser netuser = netPlayer.GetLocalData() as NetUser;
			string ID = netuser.userID.ToString();
			if(Commerce.ContainsKey(netuser)){
				NetUser tragetUser = Commerce[netuser].player.netUser;
				if(tragetUser != null){
					GiveItem(tragetUser, Commerce[netuser].item, Commerce[netuser].amount);
					rust.SendChatMessage(tragetUser, chatName, string.Format(GetMessage("CommerceCanceled", tragetUser.userID.ToString()), netuser.displayName));
				}
				timerTrade.Destroy();
				Commerce.Remove(netuser);
			}
			else if(Commerce.Count > 0){
				foreach(var pair in Commerce.ToList()){
					NetUser tragetUser = Commerce[pair.Key].player.netUser;
					if(netuser == tragetUser){
						timerTrade.Destroy();
						Commerce.Remove(pair.Key);
						rust.SendChatMessage(pair.Key, chatName, string.Format(GetMessage("CommerceCanceled", pair.Key.userID.ToString()), netuser.displayName));
					}
				}
			}
		}


		private void OnKilled(TakeDamage takedamage, DamageEvent damage){
			NetUser Attacker = damage.attacker.client?.netUser ?? null;
			if(takedamage is ProtectionTakeDamage && damage.sender.gameObject.GetComponentInChildren<BasicWildLifeAI>() && Attacker != null && giveMoneyByKillAnimals)
			AnimalDeaths(Attacker, takedamage);
			if(!(takedamage is HumanBodyTakeDamage))return;
			NetUser Victim = damage.victim.client?.netUser ?? null;
			int amountGiveTakeMoney = 0;
			if(Attacker == Victim || Victim != null){
				data = GetPlayerData(Victim.userID.ToString());
				if(Attacker == Victim){
					if(takeMoneyBySuicide && data.money > percentageTakeMoneyBySuicide)
					TakeMoney(Victim, (data.money / percentageTakeMoneyBySuicide));
					return;
				}
				if(takeMoneyByDeaths && data.money > percentageTakeMoneyByDeaths){
					amountGiveTakeMoney = data.money / percentageTakeMoneyByDeaths;
					TakeMoney(Victim, amountGiveTakeMoney);
				}
				if(Attacker != null && giveMoneyByKillPlayer && amountGiveTakeMoney > 0){
					GiveMoney(Attacker, amountGiveTakeMoney);
				}
			}
		}


		private void AnimalDeaths(NetUser netuser, TakeDamage takedamage){
			bool mutant = takedamage.ToString().Contains("Mutant");
			if(takedamage.GetComponent<BearAI>()){
				if(mutant)
				GiveMoney(netuser, moneyByKillMutantBear);
				else
				GiveMoney(netuser, moneyByKillBear);
			}
			else if(takedamage.GetComponent<WolfAI>()){
				if(mutant)
				GiveMoney(netuser, moneyByKillMutantWolf);
				else
				GiveMoney(netuser, moneyByKillWolf);
			}
			else if(takedamage.GetComponent<StagAI>())
			GiveMoney(netuser, moneyByKillStag);
			else if(takedamage.GetComponent<ChickenAI>())
			GiveMoney(netuser, moneyByKillChicken);
			else if(takedamage.GetComponent<RabbitAI>())
			GiveMoney(netuser, moneyByKillRabbit);
			else if(takedamage.GetComponent<BoarAI>())
			GiveMoney(netuser, moneyByKillBoar);
		}


		void AutoGiveMoney(){
			timerAutoGiveMoney = timer.Repeat(timeAutoGiveMoney * 60, 0, () =>{
				foreach(PlayerClient player in PlayerClient.All){
					NetUser netuser = player.netUser;
					string ID = netuser.userID.ToString();
					if(AcessPremiumAccounts(netuser)){
						rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("AutoGiveMoney", ID), amountAutoGiveMoneyPremium, symbolMoney));
						GiveMoney(netuser, amountAutoGiveMoneyPremium);
					}
					else{
						rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("AutoGiveMoney1", ID), amountAutoGiveMoneyNormal, symbolMoney));
						GiveMoney(netuser, amountAutoGiveMoneyNormal);
					}
				}
			});
		}


		void GiveMoney(NetUser netuser, int amount){
			object thereturn = Interface.GetMod().CallHook("canMoney", new object[] {netuser});
			if(thereturn != null)return; 
			string ID = netuser.userID.ToString();
			if(MaxMinMoney(ID, amount))return;
			data = GetPlayerData(ID);
			data.money = data.money + amount;
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("GiveMoney", ID), amount, GetTotalMoney(ID), symbolMoney));
			SaveData();
		}


		void SetMoney(string ID, int amount){
			if(MaxMinMoney(ID, amount))return;
			data = GetPlayerData(ID);
			data.money = amount;
			data.bank = amount;
			SaveData();
		}


		void TakeMoney(NetUser netuser, int amount){
			object thereturn = Interface.GetMod().CallHook("canMoney", new object[] {netuser});
			if(thereturn != null)return; 
			string ID = netuser.userID.ToString();
			data = GetPlayerData(ID);
			int takeAmount = 0;
			if(data.money >= amount){
				data.money = data.money - amount;
			}
			else{
				if(MaxMinBankMoney(ID, amount))return;
				takeAmount = data.money;
				data.money = 0;
				amount = amount - takeAmount;
				data.bank = data.bank - amount;
			}
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("TakeMoney", ID), amount, data.money, symbolMoney));
			SaveData();
		}


		int GetTotalMoney(string ID){
			data = GetPlayerData(ID);
			return data.TotalMoney();
		}


		void GiveItem(NetUser netuser, string item, int amount){
			var inventory = rust.GetInventory(netuser);
			if(!DataBock.ContainsKey(item.ToLower())) return;
			inventory.AddItemAmount(DataBock[item.ToLower()], amount, Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Default, false, Inventory.Slot.Kind.Default));
		}


		int TakeItem(NetUser netuser, string item, int amount){
			IInventoryItem Item;
			int amountTake = 0;
			var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			for (int i = 0; i < 40; i++){
				if(inv.GetItem(i, out Item)){
					if(Item.datablock.name.ToString().ToLower() == item.ToLower()){
						int quality = Item.datablock._splittable?(int)Item.uses :1;
						if(quality >= amount){
							if(quality == amount)
							inv.RemoveItem(Item.slot);
							else
							Item.SetUses(quality - amount);
							return amount;
						}
						else{
							amountTake = amountTake + quality;
							inv.RemoveItem(Item.slot);
							if(amountTake >= amount)return amountTake;
						}
					}
				}
			}
			return amountTake;
		}


		bool HaveItemShops(string item){
			foreach(var pair in Shops){
				shop = Shops[pair.Key];
				if(shop.Items.ContainsKey(item))return true;
			}
			return false;
		}


		KeyValuePair<int, int> GetInfoItemShop(string item){
			foreach(var pair in Shops){
				shop = Shops[pair.Key];
				if(shop.Items.ContainsKey(item)){
					infoitem = shop.Items[item];
					return new KeyValuePair<int, int>(infoitem.buy, infoitem.sell);
				}
			}
			return new KeyValuePair<int, int>(0, 0);
		}


		void AddItemShop(string shopname, string item, int buy, int sell){
			infoitem = new InfoItem();
			infoitem.buy = buy;
			infoitem.sell = sell;
			RemoveItemShop(item);
			if(Shops.ContainsKey(shopname)){
				shop = Shops[shopname];
				if(shop.Items.ContainsKey(item))
				shop.Items.Remove(item);
				shop.Items.Add(item, infoitem);
			}
			else{
				shop = new Shop();
				shop.Items.Add(item, infoitem);
				Shops.Add(shopname, shop);
			}
			SaveData();
		}


		void RemoveItemShop(string item){
			item = item.ToLower();
			foreach(var i in Shops.ToList()){
				shop = Shops[i.Key];
				if(shop.Items.ContainsKey(item)){
					shop.Items.Remove(item);
					if(shop.Items.Count == 0)
					Shops.Remove(i.Key);
					SaveData();
				}
			}
		}


		void CmdGetStatsMoney(NetUser netuser, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveAcess", ID)); return;}
			if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins1", ID));return;}
			NetUser tragetUser = rust.FindPlayer(args[1]);
			string tragetID = string.Empty;
			if(tragetUser == null){
				tragetID =  GetPlayerIdData(args[1].ToString());
				if(tragetID == null){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("NotFoundPlayerOrData", ID), args[1].ToString()));return;}
			}
			else
			tragetID = tragetUser.userID.ToString();
			CmdStatsMoney(netuser, tragetID);
		}


		void CmdStatsMoney(NetUser netuser, string ID){
			data = GetPlayerData(ID);
			rust.SendChatMessage(netuser, chatName, GetMessage("StatsMoney", ID));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("StatsMoney1", ID), data.name));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("StatsMoney2", ID), data.bank, symbolMoney));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("StatsMoney3", ID), data.money, symbolMoney));
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("StatsMoney4", ID), data.TotalMoney(), symbolMoney));
			rust.SendChatMessage(netuser, chatName, GetMessage("StatsMoney5", ID));
		}


		void CmdTopsRichs(NetUser netuser){
			string ID = netuser.userID.ToString();
			rust.SendChatMessage(netuser, chatName, GetMessage("TopsRichs", ID));
			var listStats = MoneyData.Values.OrderByDescending(a => a.TotalMoney()).ToList();
			for(int i = 0; i < maxTopsList; i++){
				if(i >= listStats.Count)break;
				string place = Convert.ToString(i + 1);
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("TopsRichs1", ID), place, listStats[i].name, listStats[i].TotalMoney(), symbolMoney));
			}
			rust.SendChatMessage(netuser, chatName, GetMessage("TopsRichs2", ID));
		}


		void CmdGiveMoney(NetUser netuser, string[] args){
			string ID = netuser.userID.ToString();
			data = GetPlayerData(ID);
			if(args.Length < 2){
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers3", ID));
				if(AcessAdmins(netuser))
				rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins2", ID));
				return;
			}
			int amount = 1;
			if(args[1].ToString() == "all"){
				if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveAcess", ID)); return;}
				if(args.Length > 2){
					if(!int.TryParse(args[2], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins2", ID));return;}
				}
				rust.BroadcastChat(chatName, string.Format(GetMessage("GiveMoney1"), netuser.displayName, amount, symbolMoney));
				foreach(PlayerClient player in PlayerClient.All)
				GiveMoney(player.netUser, amount);
				return;
			}
			NetUser tragetUser = rust.FindPlayer(args[1]);
			if(tragetUser == null){rust.SendChatMessage(netuser, chatName, GetMessage("PlayerNotFound", ID));return;}
			if(args.Length > 2){
				if(!int.TryParse(args[2], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers3", ID));return;}
			}
			if(!AcessAdmins(netuser)){
				if(netuser == tragetUser){rust.SendChatMessage(netuser, chatName, GetMessage("GiveMoney2", ID));return;}
				if(amount < 1){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("GiveMoney3", ID), symbolMoney));return;}
				if(data.money < amount){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("GiveMoney4", ID), amount, symbolMoney));return;}
				TakeMoney(netuser, amount);
			}
			rust.BroadcastChat(chatName, string.Format(GetMessage("GiveMoney5"), netuser.displayName, tragetUser.displayName, amount, symbolMoney));
			GiveMoney(tragetUser, amount);
		}


		void CmdSetMoney(NetUser netuser, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveAcess", ID)); return;}
			if(args.Length < 3){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins4", ID));return;}
			NetUser tragetUser = rust.FindPlayer(args[1]);
			int amount = 1;
			if(!int.TryParse(args[2], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins4", ID));return;}
			string tragetID = tragetUser.userID.ToString();
			if(tragetUser == null){
				tragetID = GetPlayerIdData(args[1].ToString());
				if(tragetID == null){rust.SendChatMessage(netuser, chatName, GetMessage("PlayerDataNotFound", ID));return;}
				else{
					data = GetPlayerData(tragetID);
					SetMoney(tragetID, amount);
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("SetMoney", ID), amount, data.name, data.TotalMoney(), symbolMoney));
					return;
				}
			}
			data = GetPlayerData(tragetID);
			SetMoney(tragetID, amount);
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("SetMoney", ID), amount, data.name, data.TotalMoney(), symbolMoney));
		}


		void CmdTakeMoney(NetUser netuser, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveAcess", ID)); return;}
			if(args.Length < 3){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins3", ID));return;}
			NetUser tragetUser = rust.FindPlayer(args[1]);
			int amount = 1;
			if(!int.TryParse(args[2], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins3", ID));return;}
			if(tragetUser == null){
				string tragetID = GetPlayerIdData(args[1].ToString());
				if(tragetID == null){rust.SendChatMessage(netuser, chatName, GetMessage("PlayerDataNotFound", ID));return;}
				else{
					int takeAmount = amount;
					data = GetPlayerData(tragetID);
					if(data.money >= amount)
					data.money = data.money - amount;
					else{
						if(MaxMinBankMoney(tragetID, amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins3", ID));return;}
						takeAmount = takeAmount - data.money;
						data.money = 0;
						data.bank = data.bank - takeAmount;
					}
					SaveData();
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("TakeMoney1", ID), amount, data.name, data.TotalMoney(), symbolMoney));
				}
			}
			else{
				data = GetPlayerData(tragetUser.userID.ToString());
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("TakeMoney1", ID), amount, data.name, data.TotalMoney(), symbolMoney));
				TakeMoney(tragetUser, amount);
			}
		}


		void CmdAddItemShop(NetUser netuser, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveAcess", ID)); return;}
			if(Shops.Count > maxShops){rust.SendChatMessage(netuser, chatName, GetMessage("AddItemShop", ID));return;}
			if(args.Length < 4){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins5", ID));return;}
			string shopName = args[1].ToString().ToLower();
			string item = args[2].ToString().ToLower();
			if(!DataBock.ContainsKey(item)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("ItemNotFound", ID), item));return;}
			int buy = 0;
			int sell = 0;
			if(!int.TryParse(args[3], out buy)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins5", ID));return;}
			if(args.Length > 4){
				if(!int.TryParse(args[4], out sell)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins5", ID));return;}
			}
			if(buy <= 0 && sell <= 0 || buy > 0 && sell > buy){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins5", ID)); return;}
			if(Shops.ContainsKey(shopName)){
				shop = Shops[shopName];
				if(shop.Items.Count > maxItemsPerShop){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("AddItemShop1", ID), item));return;}
			}
			AddItemShop(shopName, item, buy, sell);
			rust.Notice(netuser, string.Format(GetMessage("AddItemShop2", ID), item, shopName, buy, sell, symbolMoney));
		}


		void CmdRemoveShop(NetUser netuser, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveAcess", ID)); return;}
			if(args.Length < 2){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins6", ID));return;}
			if(args[1].ToString().ToLower() == "shop"){
				if(args.Length < 3){rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins7", ID));return;}
				string shopName = args[2].ToString().ToLower();
				if(Shops.ContainsKey(shopName)){
					Shops.Remove(shopName);
					rust.Notice(netuser, string.Format(GetMessage("RemoveShop", ID), shopName));
				}
				else{rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("ItemNotFoundShops", ID), shopName));return;}
			}
			else{
				string item = args[1].ToString().ToLower();
				if(!DataBock.ContainsKey(item)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("ItemNotFoundDataBock", ID), item));return;}
				if(HaveItemShops(item)){
					RemoveItemShop(item);
					rust.Notice(netuser, string.Format(GetMessage("RemoveShop1", ID), item));
				}
				else
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("ItemNotFoundShops", ID), item));
			}
		}


		void CmdAddLocationsShops(NetUser netuser, string[] args){
			string ID = netuser.userID.ToString();
			double radius = 50;
			if(args.Length > 1){
				if(!double.TryParse(args[1], out radius)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers7", ID));return;}
			}
			var location = new Dictionary<string, object>();
			location.Add("x", netuser.playerClient.lastKnownPosition.x.ToString());
			location.Add("y", netuser.playerClient.lastKnownPosition.y.ToString());
			location.Add("z", netuser.playerClient.lastKnownPosition.z.ToString());
			location.Add("radius", radius);
			LocationShops.Add(LocationShops.Count.ToString(), location);
			Config["Settings: Location Shops"] = LocationShops;
			rust.Notice(netuser, string.Format(GetMessage("AddLocationsShops", ID), LocationShops.Count, radius));
			SaveConfig();
		}


		void LockLocationShops(NetUser netuser){
			if(LocationShops.Count == 0)return;
			foreach(var pair in LocationShops){
				var location = LocationShops[pair.Key] as Dictionary<string, object>;
				if(location == null)continue;
				Vector3  shopsLocation = new Vector3(Convert.ToSingle(location["x"]), Convert.ToSingle(location["y"]), Convert.ToSingle(location["z"]));
				string localName = Location?.Call("FindLocationName", shopsLocation) as string;
				double distanceShop = Math.Floor(Vector3.Distance(netuser.playerClient.lastKnownPosition, shopsLocation));
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("LockLocationShops", netuser.userID.ToString()), distanceShop, localName, location["radius"].ToString()));
			}
		}


		private void CmdHelpAdmins(NetUser netuser){
			string ID = netuser.userID.ToString(); 
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveAcess", ID)); return;}
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins1", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins2", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins3", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins4", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins5", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins6", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins7", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins8", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins9", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins10", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpAdmins11", ID));
		}


		private void CmdHelpPlayers(NetUser netuser){
			string ID = netuser.userID.ToString();
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers1", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers2", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers3", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers4", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers5", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers7", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers8", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers6", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers9", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers10", ID));
			rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers11", ID));
		}


		[ChatCommand("money")]
		void cmdMoney(NetUser netuser, string command, string[] args){
			if(args.Length == 0){CmdStatsMoney(netuser, netuser.userID.ToString()); return;}
			switch (args[0].ToLower()){
				case "help":
					CmdHelpPlayers(netuser);
					break;
				case "ahelp":
					CmdHelpAdmins(netuser);
					break;
				case "tops":
					CmdTopsRichs(netuser);
					break;
				case "give":
					CmdGiveMoney(netuser, args);
					break;
				case "look":
					CmdGetStatsMoney(netuser, args);
					break;
				case "take":
					CmdTakeMoney(netuser, args);
					break;
				case "set":
					CmdSetMoney(netuser, args);
					break;
				default:{
					CmdHelpPlayers(netuser);
					break;
				}
			}
		}


		[ChatCommand("bank")]
		void cmdBank(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!bank){rust.SendChatMessage(netuser, chatName, GetMessage("Bank", ID));return;}
			if(args.Length == 0){rust.SendChatMessage(netuser, chatName, GetMessage("Bank1", ID));return;}
			data = GetPlayerData(ID);
			int amount = minDepositTakeMoneyBank;
			switch (args[0].ToLower()){
				case "see":
					CmdStatsMoney(netuser, ID);
					break;
				case "deposit":
					if(args.Length > 1){
						if(!int.TryParse(args[1], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers4", ID));return;}
					}
					if(amount < minDepositTakeMoneyBank){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Bank2", ID), minDepositTakeMoneyBank, symbolMoney));return;}
					if(data.money < amount){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Bank3", ID), amount, symbolMoney));return;}
					if(MaxMinBankMoney(ID, amount)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("HaveMaxMoneyBank", ID), amount, symbolMoney));return;}
					data.money = data.money - amount;
					data.bank = data.bank + amount;
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Bank4", ID), amount, symbolMoney));
					CmdStatsMoney(netuser, ID);
					break;
				case "take":
					if(args.Length > 1){
						if(!int.TryParse(args[1], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers5", ID));return;}
					}
					if(amount < minDepositTakeMoneyBank){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Bank5", ID), minDepositTakeMoneyBank, symbolMoney));return;}
					int costBankWorks = amount / percentageCostkWorksBank;
					int totalTake = amount + costBankWorks;
					if(data.bank < totalTake){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Bank6", ID), totalTake, data.bank, symbolMoney));return;}
					data.bank = data.bank - totalTake;
					data.money = data.money + amount;
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Bank7", ID), amount, costBankWorks, symbolMoney));
					CmdStatsMoney(netuser, ID);
					break;
				default:{
					rust.SendChatMessage(netuser, chatName, GetMessage("Bank", ID));
					break;
				}
			}
			SaveData();
		}


		[ChatCommand("ashop")]
		void cmdAdminShop(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!AcessAdmins(netuser)){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveAcess", ID)); return;}
			if(args.Length == 0){rust.SendChatMessage(netuser, chatName, GetMessage("AdminShop", ID));return;}
			switch (args[0].ToLower()){
				case "add":
					CmdAddItemShop(netuser, args);
					break;
				case "remove":
					CmdRemoveShop(netuser, args);
					break;
				case "clear":
					Shops.Clear();
					SaveData();
					rust.Notice(netuser, GetMessage("AdminShop1", ID));
					break;
				case "location":
					CmdAddLocationsShops(netuser, args);
					break;
				case "clearlocation":
					LocationShops.Clear();
					Config["Settings: Location Shops"] = LocationShops;
					rust.Notice(netuser, GetMessage("AdminShop2", ID));
					SaveConfig();
					break;
				default:{
					rust.SendChatMessage(netuser, chatName, GetMessage("AdminShop", ID));
					break;
				}
			}
		}


		[ChatCommand("shop")]
		void cmdShop(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(args.Length == 0){
				if(Shops.Count == 0){rust.SendChatMessage(netuser, chatName, GetMessage("Shops", ID));return;}
				rust.SendChatMessage(netuser, chatName, GetMessage("Shops1", ID));
				foreach(var pair in Shops.OrderBy(a => a.Key)){
					shop = Shops[pair.Key];
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Shops2", ID), pair.Key, shop.Items.Count));
				}
				rust.SendChatMessage(netuser, chatName, GetMessage("Shops3", ID));
			}
			else{
				string shopName = args[0].ToLower().ToString();
				if(!Shops.ContainsKey(shopName)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Shops4", ID), shopName));return;}
				shop = Shops[shopName];
				if(shop == null){rust.SendChatMessage(netuser, chatName, GetMessage("Shops5", ID));return;}
				rust.SendChatMessage(netuser, chatName, GetMessage("Shops6", ID));
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Shops7", ID), shopName, shop.Items.Count));
				foreach(var item in shop.Items.OrderBy(a => a.Key)){
					infoitem = shop.Items[item.Key];
					int buy = infoitem.buy;
					int sell = infoitem.sell;
					if(buy == 0 && sell > 0)
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Shops8", ID), item.Key, sell, symbolMoney));
					else if(sell == 0 && buy > 0)
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Shops9", ID), item.Key, buy, symbolMoney));
					else if(sell > 0 && buy > 0)
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Shops10", ID), item.Key, buy, sell, symbolMoney));
				}
				rust.SendChatMessage(netuser, chatName, GetMessage("Shops11", ID));
			}
		}


		[ChatCommand("commerce")]
		void cmdCommerce(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!commerce){rust.SendChatMessage(netuser, chatName, GetMessage("CommerceOff", ID));return;}
			if(args.Length < 4){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers9", ID));return;}
			NetUser tragetUser = rust.FindPlayer(args[0]);
			if(tragetUser == null){rust.SendChatMessage(netuser, chatName, GetMessage("PlayerNotFound", ID));return;}
			if(tragetUser == netuser){rust.SendChatMessage(netuser, chatName, GetMessage("Commerce", ID));return;}
			if(Commerce.ContainsKey(tragetUser)){rust.SendChatMessage(netuser, chatName, GetMessage("Commerce1", ID));return;}
			string item = args[1].ToLower().ToString();
			if(!DataBock.ContainsKey(item)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("ItemNotFoundDataBock", ID), item));return;}
			int amount = 1;
			int buy = 1;
			if(!int.TryParse(args[2], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers9", ID));return;}
			if(amount <= 0){rust.SendChatMessage(netuser, chatName, GetMessage("Commerce2", ID));return;}
			if(!int.TryParse(args[3], out buy)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers9", ID));return;}
			if(buy < 0){rust.SendChatMessage(netuser, chatName, GetMessage("Commerce3", ID));return;}
			int amountSell = TakeItem(netuser, item, amount);
			if(amountSell < 1){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Commerce4", ID), item));return;}
			trade = new Trade();
			trade.player = netuser.playerClient;
			trade.item = item;
			trade.amount = amountSell;
			trade.buy = buy;
			Commerce.Add(tragetUser, trade);
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Commerce5", ID), tragetUser.displayName, item, amountSell, buy, symbolMoney));
			rust.SendChatMessage(tragetUser, chatName, string.Format(GetMessage("Commerce6", ID), netuser.displayName, item, amountSell, buy, symbolMoney));
			rust.SendChatMessage(tragetUser, chatName, string.Format(GetMessage("Commerce7", ID), timeDurationOfferTrade));
			timerTrade = timer.Once(timeDurationOfferTrade, ()=>{
				Commerce.Remove(tragetUser);
				if(netuser.playerClient != null){
					GiveItem(netuser, item, amountSell);
					rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Commerce8", ID), tragetUser.displayName));
					rust.SendChatMessage(tragetUser, chatName, string.Format(GetMessage("Commerce9", ID), netuser.displayName));
				}
			});
		}


		[ChatCommand("acceptcommerce")]
		void cmdAcceptCommerce(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(!commerce){rust.SendChatMessage(netuser, chatName, GetMessage("CommerceOff", ID));return;}
			data = GetPlayerData(ID);
			if(Commerce.ContainsKey(netuser)){
				string item = Commerce[netuser].item;
				int amount = Commerce[netuser].amount;
				int buy = Commerce[netuser].buy;
				if(data.TotalMoney() < buy){rust.Notice(netuser, string.Format(GetMessage("NotHaveMoney", ID), buy, symbolMoney)); return;}
				NetUser tragetUser = Commerce[netuser].player.netUser;
				if(tragetUser == null){rust.SendChatMessage(netuser, chatName, GetMessage("AcceptCommerce", ID));return;}
				timerTrade.Destroy();
				GiveItem(netuser, item, amount);
				rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("AcceptCommerce1", ID), tragetUser.displayName, item));
				rust.SendChatMessage(tragetUser, chatName, string.Format(GetMessage("AcceptCommerce2", ID), netuser.displayName, buy, symbolMoney));
				TakeMoney(netuser, buy);
				GiveMoney(tragetUser, buy);
				Commerce.Remove(netuser);
			}
			else{rust.Notice(netuser, GetMessage("AcceptCommerce3", ID)); return;}
		}


		[ChatCommand("buy")]
		void cmdBuy(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(activatedLocationsShops){
				if(LocationShops.Count == 0){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveLocations", ID));return;}
				if(!IfPlayerIsInRadiusShop(netuser)){LockLocationShops(netuser); return;}
			}
			if(args.Length == 0){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers7", ID));return;}
			data = GetPlayerData(ID);
			string item = args[0].ToLower().ToString();
			if(!DataBock.ContainsKey(item)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("ItemNotFoundDataBock", ID), item));return;}
			if(!HaveItemShops(item)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("ItemNotFoundShops", ID), item));return;}
			var infoItem = GetInfoItemShop(item);
			int cost = infoItem.Key;
			int amount = 1;
			if(args.Length > 1){
				if(!int.TryParse(args[1], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers7", ID));return;}
			}
			cost = cost * amount;
			if(amount <= 0){rust.SendChatMessage(netuser, chatName, GetMessage("Buy", ID));return;}
			if(cost <= 0){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Buy1", ID), item));return;}
			if(data.TotalMoney() < cost){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("NotHaveMoney", ID), cost, symbolMoney));return;}
			GiveItem(netuser, item, amount);
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Buy2", ID), item, amount, cost, symbolMoney));
			TakeMoney(netuser, cost);
		}


		[ChatCommand("sell")]
		void cmdSell(NetUser netuser, string command, string[] args){
			string ID = netuser.userID.ToString();
			if(activatedLocationsShops){
				if(LocationShops.Count == 0){rust.SendChatMessage(netuser, chatName, GetMessage("NotHaveLocations", ID));return;}
				if(!IfPlayerIsInRadiusShop(netuser)){LockLocationShops(netuser); return;}
			}
			if(args.Length == 0){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers8", ID));return;}
			data = GetPlayerData(ID);
			string item = args[0].ToString().ToLower();
			if(!DataBock.ContainsKey(item)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("ItemNotFoundDataBock", ID), item));return;}
			if(!HaveItemShops(item)){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("ItemNotFoundShops", ID), item));return;}
			var infoItem = GetInfoItemShop(item);
			int sell = infoItem.Value;
			int amount = 1;
			if(sell <= 0){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Sell", ID), item));return;}
			if(args.Length > 1){
				if(!int.TryParse(args[1], out amount)){rust.SendChatMessage(netuser, chatName, GetMessage("HelpPlayers8", ID));return;}
			}
			if(amount <= 0){rust.SendChatMessage(netuser, chatName, GetMessage("Sell1", ID));return;}
			int amountSell = TakeItem(netuser, item, amount);
			if(amountSell <= 0){rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Sell2", ID), item));return;}
			int totalGain = sell * amountSell;
			rust.SendChatMessage(netuser, chatName, string.Format(GetMessage("Sell3", ID), item, amountSell, totalGain, symbolMoney));
			GiveMoney(netuser, totalGain);
		}
	}
}