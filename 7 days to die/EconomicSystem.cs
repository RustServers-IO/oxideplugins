using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;
using System.Data;
using UnityEngine;
using Oxide.Core;
using System.IO;

namespace Oxide.Plugins
{
    [Info("EconomicSystem", "OpenFunRus", 1.2)]
    [Description("EconomicSystem by OpenFunRus")]
	class EconomicSystem : SevenDaysPlugin
    {
		public int zombie01 => Config.Get<int>("zombie01");
		public int zombieferal => Config.Get<int>("zombieferal");
		public int zombieBoe => Config.Get<int>("zombieBoe");
		public int zombieJoe => Config.Get<int>("zombieJoe");
		public int zombieMoe => Config.Get<int>("zombieMoe");
		public int zombieArlene => Config.Get<int>("zombieArlene");
		public int zombieScreamer => Config.Get<int>("zombieScreamer");
		public int zombieDarlene => Config.Get<int>("zombieDarlene");
		public int zombieMarlene => Config.Get<int>("zombieMarlene");
		public int zombieYo => Config.Get<int>("zombieYo");
		public int zombieSteve => Config.Get<int>("zombieSteve");
		public int zombieSteveCrawler => Config.Get<int>("zombieSteveCrawler");
		public int snowzombie => Config.Get<int>("snowzombie");
		public int spiderzombie => Config.Get<int>("spiderzombie");
		public int burntzombie => Config.Get<int>("burntzombie");
		public int zombieNurse => Config.Get<int>("zombieNurse");
		public int fatzombiecop => Config.Get<int>("fatzombiecop");
		public int hornet => Config.Get<int>("hornet");
		public int zombiedog => Config.Get<int>("zombiedog");
		public int zombieBear => Config.Get<int>("zombieBear");
		public int animalStag => Config.Get<int>("animalStag");
		public int animalBear => Config.Get<int>("animalBear");
		public int animalRabbit => Config.Get<int>("animalRabbit");
		public int animalChicken => Config.Get<int>("animalChicken");
		public int animalPig => Config.Get<int>("animalPig");
		public int other => Config.Get<int>("other");	
		public int PayHome => Config.Get<int>("Pay for teleport to Home");
		public int PayPoint => Config.Get<int>("Pay for teleport to Point");
		public int PayPlayer => Config.Get<int>("Pay for teleport to Player");
		public int PaySetHome => Config.Get<int>("Pay for add/set Home");
		public int PayDelHome => Config.Get<int>("Pay for delete Home");
		public bool LevelDiscount => Config.Get<bool>("Discount for player Level");
		public string HelpCommand => Config.Get<string>("Help command");
		
		protected override void LoadDefaultConfig()
		{ 
			PrintWarning("Creating a new configuration file.");
			Config.Clear();
			Config["zombie01"] = 10;
			Config["zombieferal"] = 10;
			Config["zombieBoe"] = 10;
			Config["zombieJoe"] = 10;
			Config["zombieMoe"] = 10;
			Config["zombieArlene"] = 10;
			Config["zombieScreamer"] = 10;
			Config["zombieDarlene"] = 10;
			Config["zombieMarlene"] = 10;
			Config["zombieYo"] = 10;
			Config["zombieSteve"] = 10;
			Config["zombieSteveCrawler"] = 10;
			Config["snowzombie"] = 10;
			Config["spiderzombie"] = 10;
			Config["burntzombie"] = 10;
			Config["zombieNurse"] = 10;
			Config["fatzombiecop"] = 10;
			Config["hornet"] = 10;
			Config["zombiedog"] = 10;
			Config["zombieBear"] = 10;
			Config["animalStag"] = 10;
			Config["animalBear"] = 10;
			Config["animalRabbit"] = 10;
			Config["animalChicken"] = 10;
			Config["animalPig"] = 10;
			Config["other"] = 10;			
			Config["Pay for teleport to Home"] = 50;
			Config["Pay for teleport to Point"] = 50;
			Config["Pay for teleport to Player"] = 50;
			Config["Pay for add/set Home"] = 50;
			Config["Pay for delete Home"] = 50;
			Config["Discount for player Level"] = true;
			Config["Help command"] = "help";
			SaveConfig();
		}
		
		void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				{"[FF8000] You got {0}$ for kill {1}. Your balance - {2}$ [FFFFFF]", "[FF8000] You got {0}$ for kill {1}. Your balance - {2}$ [FFFFFF]"},
                {"[FF8000] You have - {0}$ [FFFFFF]", "[FF8000] You have - {0}$ [FFFFFF]"},
				{"[FF8000] Player not found. [FFFFFF]", "[FF8000] Player not found. [FFFFFF]"},
				{"[FF8000] You send money to {0} ({1}$). Your balance - {2}$ [FFFFFF]", "[FF8000] You send money to {0} ({1}$). Your balance - {2}$ [FFFFFF]"},
				{"[FF8000] You give money to {0} ({1}$)[FFFFFF]", "[FF8000] You give money to {0} ({1}$)[FFFFFF]"},
				{"[FF8000] You got money from {0} ({1}$). Your balance - {2}$ [FFFFFF]", "[FF8000] You got money from {0} ({1}$). Your balance - {2}$ [FFFFFF]"},
				{"[FF8000] You are not Admin. [FFFFFF]", "[FF8000] You are not Admin. [FFFFFF]"},
				{"[FF8000] You have enough money. [FFFFFF]", "[FF8000] You have enough money. [FFFFFF]"},
				{"[FF8000] The amount must be greater than zero. [FFFFFF]", "[FF8000] The amount must be greater than zero. [FFFFFF]"},
				{"[FF8000] You teleported to Home. Your balance - {0}$ [FFFFFF]", "[FF8000] You teleported to Home. Your balance - {0}$ [FFFFFF]"},
				{"[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", "[FF8000] You do not have enough money. Need {0}$ [FFFFFF]"},
				{"[FF8000] You are in private zome. Leave area and use this command. [FFFFFF]", "[FF8000] You are in private zome. Leave area and use this command. [FFFFFF]"},
				{"[FF8000] Your Home point has been added. Your balance - {0}$ [FFFFFF]", "[FF8000] Your Home point has been added. Your balance - {0}$ [FFFFFF]"},
				{"[FF8000] You have saved Home. [FFFFFF]", "[FF8000] You have saved Home. [FFFFFF]"},
				{"[FF8000] Your Home has been removed. Your balance - {0}$ [FFFFFF]", "[FF8000] Your Home has been removed. Your balance - {0}$ [FFFFFF]"},
				{"[FF8000] You not have saved Home. Use /sethome to add Home point. [FFFFFF]", "[FF8000] You not have saved Home. Use /sethome to add Home point. [FFFFFF]"},
				{"[FF8000] Teleport point {0} has been added.[FFFFFF]", "[FF8000] Teleport point {0} has been added.[FFFFFF]"},
				{"[FF8000] You are teleported to {0}. Your balance - {1}$ [FFFFFF]", "[FF8000] You are teleported to {0}. Your balance - {1}$ [FFFFFF]"},
				{"[FF8000] Point {0} not found. [FFFFFF]", "[FF8000] Point {0} not found. [FFFFFF]"},
				{"[FF8000] Point {0} has been delete. [FFFFFF]", "[FF8000] Point {0} has been delete. [FFFFFF]"},	
				{"[FF8000] Item added to shop. [FFFFFF]", "[FF8000] Item added to shop. [FFFFFF]"},
				{"[FF8000] Number: {0}. Item name: {1}. Translate name: {2}. Pay: {3}$. [FFFFFF]", "[FF8000] Number: {0}. Item name: {1}. Translate name: {2}. Pay: {3}$. [FFFFFF]"},
				{"[FF8000] Item {0} not found in game. [FFFFFF]", "[FF8000] Item {0} not found in game. [FFFFFF]"},
				{"[FF8000] Item number {0} has been delete. [FFFFFF]", "[FF8000] Item number {0} has been delete. [FFFFFF]"},
				{"[FF8000] Item number {0} not found. [FFFFFF]", "[FF8000] Item number {0} not found. [FFFFFF]"},
				{"[FF8000]___________________________________________[FFFFFF]", "[FF8000]___________________________________________[FFFFFF]"},
				{"[FF8000] {0}. {1} - {2}$ [FFFFFF]", "[FF8000] {0}. {1} - {2}$ [FFFFFF]"},
				{"[FF8000] Page 1 of {0}. [FFFFFF]", "[FF8000] Page 1 of {0}. [FFFFFF]"},
				{"[FF8000] Use '/page {0}' to list more items. [FFFFFF]", "[FF8000] Use '/page {0}' to list more items. [FFFFFF]"},
				{"[FF8000] Page {0} not found. Use /shop to list items. [FFFFFF]", "[FF8000] Page {0} not found. Use /shop to list items. [FFFFFF]"},
				{"[FF8000] {0} is not number. Use /shop to list items. [FFFFFF]", "[FF8000] {0} is not number. Use /shop to list items. [FFFFFF]"},
				{"[FF8000] You buy {0} ({1}) for {2}$ [FFFFFF]", "[FF8000] You buy {0} ({1}) for {2}$ [FFFFFF]"},
				{"[FF8000] Item {0} not found in shop. [FFFFFF]", "[FF8000] Item {0} not found in shop. [FFFFFF]"},
				{"[FF8000] Use '/page 2' to list more items. [FFFFFF]", "[FF8000] Use '/page 2' to list more items. [FFFFFF]"},	
				{"[FF8000] Use '/buy 1' to buy one item or '/buy 1 2' to buy two items.[FFFFFF]", "[FF8000] Use '/buy 1' to buy one item or '/buy 1 2' to buy two items.[FFFFFF]"},
				{"[FF8000] {0}. {1} ({2} {3}, {4} {5}). Use '/tp {1}'[FFFFFF]", "[FF8000] {0}. {1} ({2} {3}, {4} {5}). Use '/tp {1}'[FFFFFF]"},
				{"[FF8000] Use '/buy 1' or '/buy 1 2' to buy two items.[FFFFFF]", "[FF8000] Use '/buy 1' or '/buy 1 2' to buy two items.[FFFFFF]"},
				{"[FF8000] Use '/givemoney <name> <number>' if you want send money to player. [FFFFFF]", "[FF8000] Use '/givemoney <name> <number>' if you want send money to player. [FFFFFF]"},
				{"[FF8000] Use '/gm <name> <number>' if you want send money to player. [FFFFFF]", "[FF8000] Use '/gm <name> <number>' if you want send money to player. [FFFFFF]"},
				{"[FF8000] Use '/sendmoney <name> <number>' if you want send money to player. [FFFFFF]", "[FF8000] Use '/sendmoney <name> <number>' if you want send money to player. [FFFFFF]"},
				{"[FF8000] Use '/sm <name> <number>' if you want send money to player. [FFFFFF]", "[FF8000] Use '/sm <name> <number>' if you want send money to player. [FFFFFF]"},
				{"[FF8000] Use '/settp <name>' if you want add teleport point. [FFFFFF]", "[FF8000] Use '/settp <name>' if you want add teleport point. [FFFFFF]"},
				{"[FF8000] Use '/addtp <name>' if you want add teleport point. [FFFFFF]", "[FF8000] Use '/addtp <name>' if you want add teleport point. [FFFFFF]"},
				{"[FF8000] Use '/tp <name>' if you want teleport to point. [FFFFFF]", "[FF8000] Use '/tp <name>' if you want teleport to point. [FFFFFF]"},
				{"[FF8000] Use '/listtp' to list avaible points. [FFFFFF]", "[FF8000] Use '/listtp' to list avaible points. [FFFFFF]"},
				{"[FF8000] Use '/deltp <name>' if you want delete teleport point. [FFFFFF]", "[FF8000] Use '/deltp <name>' if you want delete teleport point. [FFFFFF]"},
				{"[FF8000] Use '/removetp <name>' if you want delete teleport point. [FFFFFF]", "[FF8000] Use '/removetp <name>' if you want delete teleport point. [FFFFFF]"},
				{"[FF8000] Use '/tf <name>' if you want teleport to friend. [FFFFFF]", "[FF8000] Use '/tf <name>' if you want teleport to friend. [FFFFFF]"},
				{"[FF8000] Use '/ttf <name>' if you want teleport to friend. [FFFFFF]", "[FF8000] Use '/ttf <name>' if you want teleport to friend. [FFFFFF]"},
				{"[FF8000] Use '/teleporttofriend <name>' if you want teleport to friend. [FFFFFF]", "[FF8000] Use '/teleporttofriend <name>' if you want teleport to friend. [FFFFFF]"},
				{"[FF8000] Use '/shopadd <ItemName> <price> <TranslateName>' if you want add Item to shop. [FFFFFF]", "[FF8000] Use '/shopadd <ItemName> <price> <TranslateName>' if you want add Item to shop. [FFFFFF]"},
				{"[FF8000] Use '/sa <ItemName> <price> <TranslateName>' if you want add Item to shop. [FFFFFF]", "[FF8000] Use '/sa <ItemName> <price> <TranslateName>' if you want add Item to shop. [FFFFFF]"},
				{"[FF8000] Use '/shopdel <number>' if you want delete Item in shop. [FFFFFF]", "[FF8000] Use '/shopdel <number>' if you want delete Item in shop. [FFFFFF]"},
				{"[FF8000] Use '/sd <number>' if you want delete Item in shop. [FFFFFF]", "[FF8000] Use '/sd <number>' if you want delete Item in shop. [FFFFFF]"},
				{"[FF8000] Use '/page <number>' if you want list shop. [FFFFFF]", "[FF8000] Use '/page <number>' if you want list shop. [FFFFFF]"},
				{"[FF8000] Use '/buy <number>' if you want buy Item in shop. [FFFFFF]", "[FF8000] Use '/buy <number>' if you want buy Item in shop. [FFFFFF]"},
				{"[FF8000] Commands: /money, /balance, /sendmoney, /sm, /home, /sethome, /delhome, /tp, /teleport, /listtp, /ltp, /tf, /ttf, /teleporttofriend, /help, /shop, /buy, /page [FFFFFF]", "[FF8000] Commands: /money, /balance, /sendmoney, /sm, /home, /sethome, /delhome, /tp, /teleport, /listtp, /ltp, /tf, /ttf, /teleporttofriend, /help, /shop, /buy, /page [FFFFFF]"},
				{"[FF8000] Admin Commands: /givemoney, /gm, /settp, /addtp, /deltp, /removetp, /shopadd, /sa, /shopdel, /sd [FFFFFF]", "[FF8000] Admin Commands: /givemoney, /gm, /settp, /addtp, /deltp, /removetp, shopadd, /sa, /shopdel, /sd [FFFFFF]"}
				
				
				
            }, this);
        }
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
		
		class StoredData
		{
			public Dictionary<string, ShopMoney> ShopMoney  = new Dictionary<string, ShopMoney>();
			public Dictionary<string, PlayerHomes> Homes  = new Dictionary<string, PlayerHomes>();
			public Dictionary<string, AddTeleport> AddTeleport  = new Dictionary<string, AddTeleport>();
			public Dictionary<string, ShopItems> ShopItems  = new Dictionary<string, ShopItems>();
			public StoredData(){}
		}
		
		class ShopMoney
		{
			public string Money;
			public ShopMoney(){}
		}
		
		class PlayerHomes
		{
			  public string Name;
			  public string HomeX;
			  public string HomeY;
			  public string HomeZ;
			  public PlayerHomes(){}
		}
		
		class AddTeleport
		{
			  public string Name;
			  public string TpX;
			  public string TpZ;
			  public string LocX;
			  public string LocZ;
			  public AddTeleport(){}
		}
		
		class ShopItems
		{
			public string Pay;
			public string Name;
			public string NameItem;
			public string Number;
			public ShopItems(){}
		}
		
		StoredData storedData;
		
		void SaveData()
		{    
			Interface.Oxide.DataFileSystem.WriteObject(this.Title, storedData);
		}

		void Loaded()
		{
			storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Title);
			LoadDefaultMessages();
		}
		void OnEntityDeath(Entity entity, DamageResponse response)
		{
			try
			{
				if (entity != null && (entity as EntityEnemy || entity as EntityAnimal))
				{
					var entityId = response.Source.getEntityId();
					var eID = ConsoleHelper.ParseParamEntityIdToEntity(entityId.ToString());
					if (eID != null && eID as EntityPlayer)
					{
						int kill = 0;
						string entclass = entity.entityType.ToString();
						string ename = EntityClass.list[entity.entityClass].entityClassName;
						ClientInfo _cInfo = ConsoleHelper.ParseParamIdOrName(entityId.ToString());
						if(ename == "zombie01"){kill = zombie01; ename = "Develop Zombie";}
						else if(ename == "zombieferal"){kill = zombieferal; ename = "Feral Zombie";}
						else if(ename == "zombieBoe"){kill = zombieBoe; ename = "Zombie Boe";}
						else if(ename == "zombieJoe"){kill = zombieJoe; ename = "Zombie Joe";}
						else if(ename == "zombieMoe"){kill = zombieMoe; ename = "Zombie Moe";}
						else if(ename == "zombieArlene"){kill = zombieArlene; ename = "Zombie Arlene";}
						else if(ename == "zombieScreamer"){kill = zombieScreamer; ename = "Screamer Zombie";}
						else if(ename == "zombieDarlene"){kill = zombieDarlene; ename = "Zombie Darlene";}
						else if(ename == "zombieMarlene"){kill = zombieMarlene; ename = "Zombie Marlene";}
						else if(ename == "zombieYo"){kill = zombieYo; ename = "Zombie Yo";}
						else if(ename == "zombieSteve"){kill = zombieSteve; ename = "Zombie Steve";}
						else if(ename == "zombieSteveCrawler"){kill = zombieSteveCrawler; ename = "Zombie Steve Cravler";}
						else if(ename == "snowzombie"){kill = snowzombie; ename = "Snow Zombie";}
						else if(ename == "spiderzombie"){kill = spiderzombie; ename = "Spider Zombie";}
						else if(ename == "burntzombie"){kill = burntzombie; ename = "Birnt Zombie";}
						else if(ename == "zombieNurse"){kill = zombieNurse;ename = "Zombie Nurse";}
						else if(ename == "fatzombiecop"){kill = fatzombiecop; ename = "Zombie Cop";}
						else if(ename == "hornet"){kill = hornet; ename = "Hornet";}
						else if(ename == "zombiedog"){kill = zombiedog; ename = "Zombie Dog";}
						else if(ename == "zombieBear"){kill = zombieBear; ename = "Zombie Bear";}
						else if(ename == "animalStag"){kill = animalStag; ename = "Stag";}
						else if(ename == "animalBear"){kill = animalBear; ename = "Bear";}
						else if(ename == "animalRabbit"){kill = animalRabbit; ename = "Rabbit";}
						else if(ename == "animalChicken"){kill = animalChicken; ename = "Chicken";}
						else if(ename == "animalPig"){kill = animalPig; ename = "Pig";}
						else{ename = "Unknown"; kill = other;}
						if(!storedData.ShopMoney.ContainsKey(_cInfo.playerId))
						{
							storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
							storedData.ShopMoney[_cInfo.playerId].Money = "0";
							ShopMoney shop = storedData.ShopMoney[_cInfo.playerId];
							int money = Int32.Parse(shop.Money);
							money = money + kill;
							storedData.ShopMoney[_cInfo.playerId].Money = money.ToString();
							SaveData();
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You got {0}$ for kill {1}. Your balance - {2}$ [FFFFFF]", _cInfo.playerId), kill, ename, money), "Server", false, "", false));
						}
						else
						{
							ShopMoney shop = storedData.ShopMoney[_cInfo.playerId];
							int money = Int32.Parse(shop.Money);
							money = money + kill;
							storedData.ShopMoney[_cInfo.playerId].Money = money.ToString();
							SaveData();
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You got {0}$ for kill {1}. Your balance - {2}$ [FFFFFF]", _cInfo.playerId), kill, ename, money), "Server", false, "", false));
						}
					}
				}
			}
			catch
			{
			}
		}
		private object OnPlayerChat(ClientInfo _cInfo, string message)
		{
			if (!string.IsNullOrEmpty(message) && message.StartsWith("/") && !string.IsNullOrEmpty(_cInfo.playerName) )
			{
				EntityPlayer _player = GameManager.Instance.World.Players.dict[_cInfo.entityId];
				string pp = (int)_player.position.x + "," +(int)_player.position.y + "," + (int)_player.position.z;
				Vector3i posit = Vector3i.Parse(pp);
				bool LandProtectionPlayer = GameManager.Instance.World.CanPlaceBlockAt (posit,GameManager.Instance.GetPersistentPlayerList ().GetPlayerData (_cInfo.playerId));
				message = message.Replace("/", "");
				string mesg = message;
				message = message.ToLower();
				if ( message == "money" || message == "balance")
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if(!storedData.ShopMoney.ContainsKey(_cInfo.playerId))
					{
						storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
						storedData.ShopMoney[_cInfo.playerId].Money = "0";
						SaveData();
					}
					message = message.Replace("money", "");
					ShopMoney shop = storedData.ShopMoney[_cInfo.playerId];
					string money = shop.Money;
					_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You have - {0}$ [FFFFFF]", _cInfo.playerId), money), "Server", false, "", false));
					return true;
				}
				if ( message.StartsWith("givemoney ") || message.StartsWith("gm "))
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if ( message.StartsWith("givemoney "))
					{
						message = message.Replace("givemoney ", "");
					}
					else
					{
						message = message.Replace("gm ", "");
					}
					if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
					{
						string nikname = message.Split(' ')[0];
						string _value = message.Replace(nikname + " ", "");
						string value = _value.Split(' ')[0];
						string[] buycount = message.Split(' ');
						int countbuy = 0;
						foreach (string _messagebuy in buycount){countbuy++;}
						if (countbuy == 3)
						{
							nikname = message.Split(' ')[0] + " " + message.Split(' ')[1];
							_value = message.Replace(nikname + " ", "");
							value = _value.Split(' ')[0];
						}
						else if (countbuy == 4)
						{
							nikname = message.Split(' ')[0] + " " + message.Split(' ')[1] + " " + message.Split(' ')[2];
							_value = message.Replace(nikname + " ", "");
							value = _value.Split(' ')[0];
						}
						ClientInfo _targetInfo = ConsoleHelper.ParseParamIdOrName(nikname);
						if (_targetInfo == null)
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Player {0} not found. [FFFFFF]", _cInfo.playerId), nikname), "Server", false, "", false));
						}
						else
						{
							if(!storedData.ShopMoney.ContainsKey(_targetInfo.playerId))
							{
								int valuepay = Int32.Parse(value);
								storedData.ShopMoney.Add(_targetInfo.playerId, new ShopMoney());
								storedData.ShopMoney[_targetInfo.playerId].Money = valuepay.ToString();
								int money = valuepay;
								SaveData();
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You give money to {0} ({1}$)[FFFFFF]", _cInfo.playerId), _targetInfo.playerName, valuepay.ToString(), money), "Server", false, "", false));
								_targetInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You got money from {0} ({1}$). Your balance - {2}$ [FFFFFF]", _cInfo.playerId), _cInfo.playerName, valuepay.ToString(), money), "Server", false, "", false));
							}
							else
							{
								ShopMoney shop = storedData.ShopMoney[_targetInfo.playerId];
								int money = Int32.Parse(shop.Money);
								int valuepay = Int32.Parse(value);
								money = money + valuepay;
								storedData.ShopMoney[_targetInfo.playerId].Money = money.ToString();
								SaveData();
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You give money to {0} ({1}$)[FFFFFF]", _cInfo.playerId), _targetInfo.playerName, valuepay.ToString(), money), "Server", false, "", false));
								_targetInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You got money from {0} ({1}$). Your balance - {2}$ [FFFFFF]", _cInfo.playerId), _cInfo.playerName, valuepay.ToString(), money), "Server", false, "", false));
							}
						}
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You are not Admin. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					return true;
				}
				if ( message.StartsWith("sendmoney ") || message.StartsWith("sm "))
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if ( message.StartsWith("sendmoney "))
					{
						message = message.Replace("sendmoney ", "");
					}
					else
					{
						message = message.Replace("sm ", "");
					}
					string nikname = message.Split(' ')[0];
					string _value = message.Replace(nikname + " ", "");
					string value = _value.Split(' ')[0];
					string[] buycount = message.Split(' ');
					int countbuy = 0;
					foreach (string _messagebuy in buycount){countbuy++;}
					if (countbuy == 3)
					{
						nikname = message.Split(' ')[0] + " " + message.Split(' ')[1];
						_value = message.Replace(nikname + " ", "");
						value = _value.Split(' ')[0];
					}
					else if (countbuy == 4)
					{
						nikname = message.Split(' ')[0] + " " + message.Split(' ')[1] + " " + message.Split(' ')[2];
						_value = message.Replace(nikname + " ", "");
						value = _value.Split(' ')[0];
					}
					ClientInfo _targetInfo = ConsoleHelper.ParseParamIdOrName(nikname);
					if (_targetInfo == null)
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Player not found. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					else
					{
						ShopMoney shop = storedData.ShopMoney[_cInfo.playerId];
						int _money = Int32.Parse(shop.Money);
						int valuepay = Int32.Parse(value);
						if(valuepay > 0)
						{
							if(_money > valuepay)
							{
								if(!storedData.ShopMoney.ContainsKey(_targetInfo.playerId))
								{
									storedData.ShopMoney.Add(_targetInfo.playerId, new ShopMoney());
									storedData.ShopMoney[_targetInfo.playerId].Money = value;
									SaveData();
									int money = Int32.Parse(value);
									_money = _money - valuepay;
									storedData.ShopMoney[_cInfo.playerId].Money = _money.ToString();
									SaveData();
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You send money to {0} ({1}$). Your balance - {2}$ [FFFFFF]", _cInfo.playerId), _targetInfo.playerName, valuepay.ToString(), _money), "Server", false, "", false));
								_targetInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You got money from {0} ({1}$). Your balance - {2}$ [FFFFFF]", _cInfo.playerId), _cInfo.playerName, valuepay.ToString(), money), "Server", false, "", false));
								}
								else
								{
								ShopMoney targetshop = storedData.ShopMoney[_targetInfo.playerId];
								int money = Int32.Parse(targetshop.Money);
								money = money + valuepay;
								_money = _money - valuepay;
								storedData.ShopMoney[_targetInfo.playerId].Money = money.ToString();
								SaveData();
								storedData.ShopMoney[_cInfo.playerId].Money = _money.ToString();
								SaveData();
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You send money to {0} ({1}$). Your balance - {2}$ [FFFFFF]", _cInfo.playerId), _targetInfo.playerName, valuepay.ToString(), _money), "Server", false, "", false));
								_targetInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You got money from {0} ({1}$). Your balance - {2}$ [FFFFFF]", _cInfo.playerId), _cInfo.playerName, valuepay.ToString(), money), "Server", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You have enough money. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
							}
						}
						else
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] The amount must be greater than zero. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
						}
					}
					return true;
				}
				if ( message == "home" )
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if(LandProtectionPlayer)
					{
						if (storedData.Homes.ContainsKey(_cInfo.playerId))
						{
							if(storedData.ShopMoney.ContainsKey(_cInfo.playerId))
							{
								ShopMoney PlayerDataMoney = storedData.ShopMoney[_cInfo.playerId]; 
								int money = Int32.Parse(PlayerDataMoney.Money);
								int PH = PayHome;
								if (LevelDiscount && _player.Level < 100)
								{
									PH = (PayHome/100) * _player.Level;
								}
								if (money >= PH)
								{
									money = money - PH;
									PlayerHomes home = storedData.Homes[_cInfo.playerId];
									_player.position.x = float.Parse(home.HomeX);
									_player.position.y = float.Parse(home.HomeY);
									_player.position.z = float.Parse(home.HomeZ);									
									storedData.ShopMoney.Remove(_cInfo.playerId);
									storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
									storedData.ShopMoney[_cInfo.playerId].Money = money.ToString();
									SaveData();
									NetPackageTeleportPlayer pkg = new NetPackageTeleportPlayer(new Vector3(_player.position.x, _player.position.y, _player.position.z));
									_cInfo.SendPackage(pkg);
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You teleported to Home. Your balance - {0}$ [FFFFFF]", _cInfo.playerId), money), "Server", false, "", false));
								}
								else
								{
									money = PH - money;
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", _cInfo.playerId), money), "Server", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", _cInfo.playerId), PayHome), "Server", false, "", false));
							}
						}
						else
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You not have saved Home. Use /sethome to add Home point. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
						}
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You are in private zome. Leave area and use this command. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					return true;
				}
				if ( message == "sethome" )
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if(LandProtectionPlayer)
					{
						if(!storedData.Homes.ContainsKey(_cInfo.playerId))
						{
							if(storedData.ShopMoney.ContainsKey(_cInfo.playerId))
							{
								ShopMoney PlayerDataMoney = storedData.ShopMoney[_cInfo.playerId];
								int money = Int32.Parse(PlayerDataMoney.Money);
								int PSH = PaySetHome;
								if (LevelDiscount && _player.Level < 100)
								{
									PSH = (PaySetHome/100) * _player.Level;
								}
								if (money >= PSH)
								{
									money = money - PSH;
									storedData.ShopMoney.Remove(_cInfo.playerId);
									storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
									storedData.ShopMoney[_cInfo.playerId].Money = money.ToString();
									storedData.Homes.Add(_cInfo.playerId, new PlayerHomes());
									storedData.Homes[_cInfo.playerId].Name = _player.EntityName;
									storedData.Homes[_cInfo.playerId].HomeX = _player.position.x.ToString();
									storedData.Homes[_cInfo.playerId].HomeY = _player.position.y.ToString();
									storedData.Homes[_cInfo.playerId].HomeZ = _player.position.z.ToString();
									SaveData();
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Your Home point has been added. Your balance - {0}$ [FFFFFF]", _cInfo.playerId), money), "Server", false, "", false));
								}
								else
								{
									money = PSH - money;
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", _cInfo.playerId), money), "Server", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", _cInfo.playerId), PaySetHome), "Server", false, "", false));
							}
						}
						else
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You have saved Home. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
						}
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You are in private zome. Leave area and use this command. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					return true;
				}
				if ( message == "delhome" )
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					Puts(_cInfo.playerName + ": /"+ mesg);
					if(storedData.Homes.ContainsKey(_cInfo.playerId))
					{
						if(storedData.ShopMoney.ContainsKey(_cInfo.playerId))
						{
							ShopMoney PlayerDataMoney = storedData.ShopMoney[_cInfo.playerId];
							int money = Int32.Parse(PlayerDataMoney.Money);
							int PDH = PayDelHome;
							if (LevelDiscount && _player.Level < 100)
							{
								PDH = (PayDelHome/100) * _player.Level;
							}
							if (money >= PDH)
							{
								money = money - PDH;
								storedData.ShopMoney.Remove(_cInfo.playerId);
								storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
								storedData.ShopMoney[_cInfo.playerId].Money = money.ToString();
								storedData.Homes.Remove(_cInfo.playerId);
								SaveData();
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Your Home has been removed. Your balance - {0}$ [FFFFFF]", _cInfo.playerId), money), "Server", false, "", false));
							}
							else
							{
								money = PDH - money;
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", _cInfo.playerId), money), "Server", false, "", false));
							}
						}
						else
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", _cInfo.playerId), PayDelHome), "Server", false, "", false));
						}
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You not have saved Home. Use /sethome to add Home point. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					return true;
				}
				if ( message.StartsWith("settp ") || message.StartsWith("addtp ") )
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if ( message.StartsWith("settp "))
					{
						message = message.Replace("settp ", "");
					}
					else
					{
						message = message.Replace("addtp ", "");
					}						
					if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
					{
						storedData.AddTeleport.Add(message, new AddTeleport());
						storedData.AddTeleport[message].Name = message.ToString();
						storedData.AddTeleport[message].TpX = _player.position.x.ToString();
						storedData.AddTeleport[message].TpZ = _player.position.z.ToString();
						SaveData();
						if (_player.position.x > 0)
						{
							string Loc = "E";
							storedData.AddTeleport[message].LocX = Loc;
							SaveData();
						}
						else
						{
							string Loc = "W";
							storedData.AddTeleport[message].LocX = Loc;
							SaveData();
						}
						if (_player.position.z > 0)
						{
							string Loc = "N";
							storedData.AddTeleport[message].LocZ = Loc;
							SaveData();
						}
						else
						{
							string Loc = "S";
							storedData.AddTeleport[message].LocZ = Loc;
							SaveData();
						}
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Teleport point {0} has been added.[FFFFFF]", _cInfo.playerId), message), "Server", false, "", false));
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You are not Admin. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					return true;
				}
				if ( message.StartsWith("tp ") ||  message.StartsWith("teleport "))
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if(LandProtectionPlayer)
					{
						if ( message.StartsWith("tp "))
						{
							message = message.Replace("tp ", "");
						}
						else
						{
							message = message.Replace("teleport ", "");
						}
						if(storedData.AddTeleport.ContainsKey(message))
						{
							if(storedData.ShopMoney.ContainsKey(_cInfo.playerId))
							{
								ShopMoney PlayerDataMoney = storedData.ShopMoney[_cInfo.playerId];
								int money = Int32.Parse(PlayerDataMoney.Money);
								int PP = PayPoint;
								if (LevelDiscount && _player.Level < 100)
								{
									PP = (PayPoint/100) * _player.Level;
								}
								if (money >= PP)
								{
									money = money - PP;
									storedData.ShopMoney.Remove(_cInfo.playerId);
									storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
									storedData.ShopMoney[_cInfo.playerId].Money = money.ToString();
									AddTeleport Tp = storedData.AddTeleport[message];
									_player.position.x = float.Parse(Tp.TpX);
									_player.position.y = -1;
									_player.position.z = float.Parse(Tp.TpZ);
									SaveData();
									NetPackageTeleportPlayer pkg = new NetPackageTeleportPlayer(new Vector3(_player.position.x, _player.position.y, _player.position.z));
									_cInfo.SendPackage(pkg);
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You are teleported to {0}. Your balance - {1}$ [FFFFFF]", _cInfo.playerId), message, money), "Server", false, "", false));
								}
								else
								{
									money = PP - money;
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", _cInfo.playerId), money), "Server", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", _cInfo.playerId), PayPoint), "Server", false, "", false));
							}
						}
						else
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Point {0} not found. [FFFFFF]", _cInfo.playerId), message), "Server", false, "", false));
						}
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You are in private zome. Leave area and use this command. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					return true;
				}
				if ( message.StartsWith("deltp ") || message.StartsWith("removetp ") )
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if ( message.StartsWith("deltp "))
					{
						message = message.Replace("deltp ", "");
					}
					else
					{
						message = message.Replace("removetp ", "");
					}
					if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
					{
						storedData.AddTeleport.Remove(message);
						SaveData();
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Point {0} has been delete. [FFFFFF]", _cInfo.playerId), message), "Server", false, "", false));
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Point {0} not found. [FFFFFF]", _cInfo.playerId), message), "Server", false, "", false));
					}
					return true;
				}
				if (( message == "listtp" ) || ( message == "ltp" ))
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					int i = 0;
					foreach (var TPR in storedData.AddTeleport.Values)
					{
						i = i + 1;
						float _x = float.Parse(TPR.TpX);
						float _z = float.Parse(TPR.TpZ);
						if (_x < 0){_x = _x * -1;}
						if (_z < 0){_z = _z * -1;}
						int x = (int) Math.Round (_x);
						int z = (int) Math.Round (_z);
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] {0}. {1} ({2} {3}, {4} {5}). Use '/tp {1}'[FFFFFF]", _cInfo.playerId), i, TPR.Name, z.ToString(), TPR.LocZ, x.ToString(), TPR.LocX), "Server", false, "", false));
					}
					return true;
				}
				if ( message.StartsWith("tf ") || message.StartsWith("ttf ") || message.StartsWith("teleporttofriend "))
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if (message.StartsWith("tf "))
					{
						message = message.Replace("tf ", "");
					}
					if (message.StartsWith("ttf "))
					{
						message = message.Replace("ttf ", "");
					}
					if (message.StartsWith("teleporttofriend "))
					{
						message = message.Replace("teleporttofriend ", "");
					}
					ClientInfo _targetInfo = ConsoleHelper.ParseParamIdOrName(message);
					if(_targetInfo != null)
					{
						EntityPlayer _target = GameManager.Instance.World.Players.dict[_targetInfo.entityId];
						string _pp = (int)_target.position.x + "," +(int)_target.position.y + "," + (int)_target.position.z;
						Vector3i _posit = Vector3i.Parse(_pp);
						bool _LandProtectionPlayer = GameManager.Instance.World.CanPlaceBlockAt (_posit,GameManager.Instance.GetPersistentPlayerList ().GetPlayerData (_targetInfo.playerId));
						bool friend = _player.IsFriendsWith(_target);
						if(friend)
						{
							if(LandProtectionPlayer)
							{
								if(storedData.ShopMoney.ContainsKey(_cInfo.playerId))
								{
									ShopMoney PlayerDataMoney = storedData.ShopMoney[_cInfo.playerId];
									int money = Int32.Parse(PlayerDataMoney.Money);
									int PF = PayPlayer;
									if (LevelDiscount && _player.Level < 100)
									{
										PF = (PayPlayer/100) * _player.Level;
									}
									if (money >= PF)
									{
										if(_LandProtectionPlayer)
										{
											money = money - PF;
											storedData.ShopMoney.Remove(_cInfo.playerId);
											storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
											storedData.ShopMoney[_cInfo.playerId].Money = money.ToString();
											SaveData();
											NetPackageTeleportPlayer pkg = new NetPackageTeleportPlayer(new Vector3(_target.position.x, _target.position.y, _target.position.z));
											_cInfo.SendPackage(pkg);
											_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You are teleported to {0}. Your balance - {1}$ [FFFFFF]", _cInfo.playerId), message, money), "Server", false, "", false));
										}
										else
										{
											_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Your friend in Private Zone. He need to liave area, if you want teleport. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
										}
									}
									else
									{
										money = PF - money;
										_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", _cInfo.playerId), money), "Server", false, "", false));
									}
								}
								else
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", _cInfo.playerId), PayPlayer), "Server", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You are in private zome. Leave area and use this command. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
							}
						}
						else
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Player {0} is not your friend. [FFFFFF]", _cInfo.playerId), message), "Server", false, "", false));
						}
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Player not found. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					return true;
				}
				if ( message == HelpCommand )
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Commands: /money, /balance, /sendmoney, /sm, /home, /sethome, /delhome, /tp, /teleport, /listtp, /ltp, /tf, /ttf, /teleporttofriend, /help, /shop, /buy, /page [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Admin Commands: /givemoney, /gm, /settp, /addtp, /deltp, /removetp, /shopadd, /sa, /shopdel, /sd [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					return true;
				}
				if ( mesg.StartsWith("shopadd ") || mesg.StartsWith("sa "))
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
					{
						if ( mesg.StartsWith("shopadd "))
						{
							mesg = mesg.Replace("shopadd ", "");
						}
						else
						{
							mesg = mesg.Replace("sa ", "");
						}
						String[] itembuy = mesg.Split(new Char [] {' '});
						string itemname = itembuy[0];
						string itempay = "10";
						string translatename = itembuy[0];
						string[] buycount = mesg.Split(' ');
						int countbuy = 0;
						foreach (string _messagebuy in buycount){countbuy++;}
						if (countbuy > 2)
						{
							translatename = mesg.Replace(itembuy[0]+" "+itembuy[1]+" ", "");
						}
						if (countbuy >= 2)
						{
							int n;
							if (int.TryParse(itembuy[1], out n))
							{
								itempay = itembuy[1];
							}
							else
							{
								itempay = "10";
							}
						}
						int result = 0;
						NGuiInvGridCreativeMenu cm = new NGuiInvGridCreativeMenu ();
	                    foreach (ItemStack invF in cm.GetAllItems()) 
						{
							ItemClass ib = ItemClass.list [invF.itemValue.type];
	                        string name = ib.GetItemName ();
	                        if (name == itemname) 
							{
								result = 1;
								break;
							}
						}
						foreach (ItemStack invF in cm.GetAllBlocks()) 
						{
							ItemClass ib = ItemClass.list [invF.itemValue.type];
							string name = ib.GetItemName ();
							if (name == itemname) 
							{
								result = 1;
								break;
							}
	                    }
						if (result == 1)
						{
							int ctg = 1;
							for (int i = ctg; i <= ctg + 10000; i++)
							{
								string _i = i.ToString();
								if(!storedData.ShopItems.ContainsKey(_i))
								{
									storedData.ShopItems.Add(_i, new ShopItems());
									storedData.ShopItems[_i].Pay = itempay;
									storedData.ShopItems[_i].Name = itemname;
									storedData.ShopItems[_i].NameItem = translatename;
									storedData.ShopItems[_i].Number = _i;
									SaveData();
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Item added to shop. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Number: {0}. Item name: {1}. Translate name: {2}. Pay: {3}$. [FFFFFF]", _cInfo.playerId), _i, itemname, translatename, itempay), "Server", false, "", false));
									break;
								}
							}
						}
						else
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Item {0} not found in game. [FFFFFF]", _cInfo.playerId), itemname), "Server", false, "", false));
						}
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You are not Admin. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					return true;
				}
				if ( message.StartsWith("shopdel ") || ( message.StartsWith("sd ")))
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
					{
						if ( message.StartsWith("shopdel "))
						{
							message = message.Replace("shopdel ", "");
						}
						else
						{
							message = message.Replace("sd ", "");
						}
						if(storedData.ShopItems.ContainsKey(message))
						{
							storedData.ShopItems.Remove(message);
							SaveData();
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Item number {0} has been delete. [FFFFFF]", _cInfo.playerId), message), "Server", false, "", false));
						}
						else
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Item number {0} not found. [FFFFFF]", _cInfo.playerId), message), "Server", false, "", false));
						}
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You are not Admin. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					return true;
				}
				if ( message =="shop")
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000]___________________________________________[FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					int ctg = 1;
					
					for (int i = ctg; i <= ctg + 9; i++)
					{
						string _i = i.ToString();
						if(storedData.ShopItems.ContainsKey(_i))
						{
							int pay = Int32.Parse(storedData.ShopItems[_i].Pay);
							string _item = storedData.ShopItems[_i].Name;
							ItemValue value = new ItemValue(ItemClass.GetItem(_item).type, true);
							if (LevelDiscount && value.HasQuality && _player.Level < 100)
							{
								pay = (pay/100) * _player.Level;
							}
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0}. {1} - {2}$ [FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, pay), "Server", false, "", false));
						}
					}
					int clt = 0;
					for (int i = ctg; i <= ctg + 9999; i++)
					{
						string _i = i.ToString();
						if(storedData.ShopItems.ContainsKey(_i))
						{
							clt = clt + 1;
						}
					}
					int page = clt/10;
                    page = page + 1;
					_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000]___________________________________________[FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Page 1 of {0}. [FFFFFF]", _cInfo.playerId), page), "Server", false, "", false));
					if (page > 1)
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/page 2' to list more items. [FFFFFF]", _cInfo.playerId), page), "Server", false, "", false));
					}
					_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/buy 1' to buy one item or '/buy 1 2' to buy two items.[FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000]___________________________________________[FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					return true;
				}
				if ( message.StartsWith("page "))
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					message = message.Replace("page ", "");
					int n;
					if (int.TryParse(message, out n))
					{
						int ctg = 1;
						int clt = 0;
						for (int i = ctg; i <= ctg + 9999; i++)
						{
							string _i = i.ToString();
							if(storedData.ShopItems.ContainsKey(_i))
							{
								clt = clt + 1;
							}
						}
						int page = clt/10;
						page = page + 1;
						int _page = Int32.Parse(message);
						if (page >= _page)
						{
							int rb = (Int32.Parse(message)-1)*10;
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000]___________________________________________[FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
							for (int i = rb; i <= rb + 9; i++)
							{
								string _i = i.ToString();
								if(storedData.ShopItems.ContainsKey(_i))
								{
									int pay = Int32.Parse(storedData.ShopItems[_i].Pay);
									string _item = storedData.ShopItems[_i].Name;
									ItemValue value = new ItemValue(ItemClass.GetItem(_item).type, true);
									if (LevelDiscount && value.HasQuality && _player.Level < 100)
									{
										pay = (pay/100) * _player.Level;
									}
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0}. {1} - {2}$ [FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, pay), "Server", false, "", false));
								}
							}
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000]___________________________________________[FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Page {0} of {1}. [FFFFFF]", _cInfo.playerId), message, page), "Server", false, "", false));
							if (page > _page)
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/page {0}' to list more items. [FFFFFF]", _cInfo.playerId), _page + 1), "Server", false, "", false));
							}
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/buy 1' or '/buy 1 2' to buy two items.[FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000]___________________________________________[FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
						}
						else
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Page {0} not found. Use /shop to list items. [FFFFFF]", _cInfo.playerId), message), "Server", false, "", false));
						}
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] {0} is not number. Use /shop to list items. [FFFFFF]", _cInfo.playerId), message), "Server", false, "", false));
					}
					return true;
				}
				if ( message.StartsWith("buy ") )
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					message = message.Replace("buy ", "");
					if(!storedData.ShopMoney.ContainsKey(_cInfo.playerId))
					{
						storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
						storedData.ShopMoney[_cInfo.playerId].Money = "0";
						SaveData();
					}
					String[] itembuy = message.Split(new Char [] {' ', ',', '.', ':', '\t' });
					if(storedData.ShopItems.ContainsKey(itembuy[0]))
					{
						ShopMoney shopmoney = storedData.ShopMoney[_cInfo.playerId];
						ShopItems shopitem = storedData.ShopItems[itembuy[0]];
						int money = Int32.Parse(shopmoney.Money);
						int pay = Int32.Parse(shopitem.Pay);
						string _item = shopitem.Name;
						ItemValue value = new ItemValue(ItemClass.GetItem(_item).type, true);
						if (LevelDiscount && value.HasQuality && _player.Level < 100)
						{
							pay = (pay/100) * _player.Level;
						}
						int colitem = 1;
						string[] buycount = message.Split(' ');
						int countbuy = 0;
						foreach (string _messagebuy in buycount){countbuy++;}
						if(countbuy == 2){pay = pay * Int32.Parse(itembuy[1]); colitem = Int32.Parse(itembuy[1]);}
						if (money >= pay)
						{
							if (value.HasQuality)
							{
								if (_player.Level < 5){value.Quality = UnityEngine.Random.Range(10, 50);}
								if (_player.Level >= 5 && _player.Level < 10){value.Quality = UnityEngine.Random.Range(50, 100);}
								if (_player.Level >= 10 && _player.Level < 15){value.Quality = UnityEngine.Random.Range(100, 150);}
								if (_player.Level >= 15 && _player.Level < 20){value.Quality = UnityEngine.Random.Range(150, 200);}
								if (_player.Level >= 20 && _player.Level < 30){value.Quality = UnityEngine.Random.Range(200, 250);}
								if (_player.Level >= 30 && _player.Level < 40){value.Quality = UnityEngine.Random.Range(250, 300);}
								if (_player.Level >= 40 && _player.Level < 50){value.Quality = UnityEngine.Random.Range(300, 350);}
								if (_player.Level >= 50 && _player.Level < 60){value.Quality = UnityEngine.Random.Range(350, 400);}
								if (_player.Level >= 60 && _player.Level < 70){value.Quality = UnityEngine.Random.Range(400, 450);}
								if (_player.Level >= 70 && _player.Level < 80){value.Quality = UnityEngine.Random.Range(450, 500);}
								if (_player.Level >= 80 && _player.Level < 90){value.Quality = UnityEngine.Random.Range(500, 550);}
								if (_player.Level >= 90 && _player.Level < 100){value.Quality = UnityEngine.Random.Range(550, 600);}
								if (_player.Level >= 100){value.Quality = 600;}
							}
							if (ItemClass.list[value.type].HasParts)
							{
								for (int i = 0; i < value.Parts.Length; i++)
								{
									int result = 1;
									if (_player.Level < 5){result = UnityEngine.Random.Range(10, 50);}
									if (_player.Level >= 5 && _player.Level < 10){result = UnityEngine.Random.Range(50, 100);}
									if (_player.Level >= 10 && _player.Level < 15){result = UnityEngine.Random.Range(100, 150);}
									if (_player.Level >= 15 && _player.Level < 20){result = UnityEngine.Random.Range(150, 200);}
									if (_player.Level >= 20 && _player.Level < 30){result = UnityEngine.Random.Range(200, 250);}
									if (_player.Level >= 30 && _player.Level < 40){result = UnityEngine.Random.Range(250, 300);}
									if (_player.Level >= 40 && _player.Level < 50){result = UnityEngine.Random.Range(300, 350);}
									if (_player.Level >= 50 && _player.Level < 60){result = UnityEngine.Random.Range(350, 400);}
									if (_player.Level >= 60 && _player.Level < 70){result = UnityEngine.Random.Range(400, 450);}
									if (_player.Level >= 70 && _player.Level < 80){result = UnityEngine.Random.Range(450, 500);}
									if (_player.Level >= 80 && _player.Level < 90){result = UnityEngine.Random.Range(500, 550);}
									if (_player.Level >= 90 && _player.Level < 100){result = UnityEngine.Random.Range(550, 600);}
									if (_player.Level >= 100){result = 600;}
									ItemValue valuePart = value.Parts[i];
									valuePart.Quality = result;
									value.Parts[i] = valuePart;
								}
							}
							ItemStack stack = new ItemStack(value, colitem);
							GameManager.Instance.ItemDropServer(stack, _player.GetPosition(), Vector3.zero, -1, 0.1f);
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You buy {0} ({1}) for {2}$ [FFFFFF]", _cInfo.playerId), shopitem.NameItem, colitem, pay), "Server", false, "", false));
							money = money - pay;
							int LastId = GameManager.Instance.World.Entities.list.Count - 1;
							Entity BuyEntity = GameManager.Instance.World.Entities.list[LastId];
							_cInfo.SendPackage(new NetPackageEntityCollect(BuyEntity.entityId, _cInfo.entityId));
							storedData.ShopMoney.Remove(_cInfo.playerId);
							storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
							storedData.ShopMoney[_cInfo.playerId].Money = money.ToString();
							SaveData();
						}
						else
						{
							money = pay - money;
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] You do not have enough money. Need {0}$ [FFFFFF]", _cInfo.playerId), money), "Server", false, "", false));
						}
					}
					else
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Item {0} not found in shop. [FFFFFF]", _cInfo.playerId), itembuy[0]), "Server", false, "", false));
					}
					return true;
				}
				if (( message =="givemoney") || ( message == "gm") || ( message == "sendmoney") || ( message == "sm") || ( message == "settp") || ( message == "addtp") || ( message == "tp") || ( message == "deltp") || ( message == "removetp") || ( message == "tf") || ( message == "ttf") || ( message == "teleporttofriend") || ( message == "shopadd") || ( message == "sa") || ( message == "shopdel") || ( message == "sd") || ( message == "page") || ( message == "buy") )
				{
					Puts(_cInfo.playerName + ": /"+ mesg);
					if ( message =="givemoney")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/givemoney <name> <number>' if you want send money to player. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="gm")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/gm <name> <number>' if you want send money to player. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="sendmoney")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/sendmoney <name> <number>' if you want send money to player. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="sendmoney")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/sm <name> <number>' if you want send money to player. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="settp")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/settp <name>' if you want add teleport point. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="addtp")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/addtp <name>' if you want add teleport point. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="tp")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/tp <name>' if you want teleport to point. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/listtp' to list avaible points. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="deltp")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/deltp <name>' if you want delete teleport point. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="removetp")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/removetp <name>' if you want delete teleport point. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="tf")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/tf <name>' if you want teleport to friend. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="ttf")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/ttf <name>' if you want teleport to friend. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="teleporttofriend")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/teleporttofriend <name>' if you want teleport to friend. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="shopadd")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/shopadd <ItemName> <price> <TranslateName>' if you want add Item to shop. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="sa")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/sa <ItemName> <price> <TranslateName>' if you want add Item to shop. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="shopdel")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/shopdel <number>' if you want delete Item in shop. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="sd")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/sd <number>' if you want delete Item in shop. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="page")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/page <number>' if you want list shop. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					if ( message =="buy")
					{
						_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(GetMessage("[FF8000] Use '/buy <number>' if you want buy Item in shop. [FFFFFF]", _cInfo.playerId)), "Server", false, "", false));
					}
					return true;
				}
		
				return null;
			}
			return null;
		}
		void OnServerSave() => SaveData();
		void Unload() => SaveData();
	}
}