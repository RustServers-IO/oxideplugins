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
    [Info("Shop", "OpenFunRus", 1.2)]
    [Description("Shop system by OpenFun")]
	class Shop : SevenDaysPlugin
    {
		public bool KillZombie => Config.Get<bool>("Pay for zombie kill");
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
		public bool KillAnimal => Config.Get<bool>("Pay for animal kill");
		public int animalStag => Config.Get<int>("animalStag");
		public int animalBear => Config.Get<int>("animalBear");
		public int animalRabbit => Config.Get<int>("animalRabbit");
		public int animalChicken => Config.Get<int>("animalChicken");
		public int animalPig => Config.Get<int>("animalPig");
	
		protected override void LoadDefaultConfig()
		{ 
			PrintWarning("Creating a new configuration file.");
			Config.Clear();
			Config["Pay for zombie kill"] = true;
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
			Config["Pay for animal kill"] = true;
			Config["animalStag"] = 10;
			Config["animalBear"] = 10;
			Config["animalRabbit"] = 10;
			Config["animalChicken"] = 10;
			Config["animalPig"] = 10;
			SaveConfig();
		}		
		void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"HaveMoney", "You have"},
                {"BuyItem", "You buy"},
                {"ErrorBuyItem", "You dont have money"},
                {"ErrorItem", "Item not found"},
				{"ErrorAddItem", "Error add item"},
                {"NotAdmin", "You are not Admin"},
                {"ErrorDelItem", "Error del item"},
				{"NotFound", "Player not found"},
				{"ErrorSendMoney", "Error Send Money"},
				{"ShowMoney", "/money - show your money"},
				{"ShowBuy", "/buy <number> <value> - buy item"},
				{"ShowSendMoney", "/sendmoney <nickname> <value> - give money to player"},
				{"ShowShopList", "/shop - show list of item"},
				{"ShowShopAdd", "/shopadd <itemname> <money> <category> <translate> - add item to shop list"},
				{"ShowShopDel", "/shopadel <number> - del item in shop list"},
				{"ShowGiveMoney", "/givemoney <nickname> <value> - give money to player"},
				{"ShowGiveMoneyForKill", "You got"},
				{"ShowGiveMoneyForKill1", "for kill"},
				{"ShopAmmo", "/shopammo - show list ammo"},
				{"ShopWeapon", "/shopweapon - show list weapon"},
				{"ShopBuild", "/shopbuild - show list build"},
				{"ShopBlock", "/shopblock - show list block"},
				{"ShopRecipe", "/shoprecipe - show list recipe"},
				{"ShopArmor", "/shoparmor - show list armor"},
				{"ShopFood", "/shopfood - show list food"},
				{"ShopMedicine", "/shopmedicine - show list medicine"},
				{"ShopBook", "/shopbook - show list book"},
				{"ShopOther", "/shopother - show list other"},
				{"ShowItemAdded", "Item added to shop."},
				{"ShowItemName", "Name"},
				{"ShowItemPay", "Pay"},
				{"ShowCategory", "Category"},
				{"ShowTranslate", "Translate name"},
				{"ShowNumber", "Number"},
				{"NumberOrPay", "th. is for"},
				{"ShowDeleted", "Item is deleted."},
				{"ShowYouSend", "You send money to "},
				{"ShowYouGet", "You got money from"},
				
				
            }, this);
        }
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
	
		class StoredData
		{
			public Dictionary<string, ShopMoney> ShopMoney  = new Dictionary<string, ShopMoney>();
			public Dictionary<string, ShopItems> ShopItems  = new Dictionary<string, ShopItems>();
			
			public StoredData()
			{
			}
		}

		class ShopMoney
		{
			public string Money;
			public ShopMoney(){}
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
			var entityId = response.Source.getEntityId();
			var eID = ConsoleHelper.ParseParamEntityIdToEntity(entityId.ToString());
			if (eID != null)
			{
				int kill = 0;
				string entclass = entity.entityType.ToString();
				string ename = EntityClass.list[entity.entityClass].entityClassName;
				ClientInfo _cInfo = ConsoleHelper.ParseParamIdOrName(entityId.ToString());
				if (KillZombie)
				{
					if(ename == "zombie01"){kill = zombie01;}
					if(ename == "zombieferal"){kill = zombieferal;}
					if(ename == "zombieBoe"){kill = zombieBoe;}
					if(ename == "zombieJoe"){kill = zombieJoe;}
					if(ename == "zombieMoe"){kill = zombieMoe;}
					if(ename == "zombieArlene"){kill = zombieArlene;}
					if(ename == "zombieScreamer"){kill = zombieScreamer;}
					if(ename == "zombieDarlene"){kill = zombieDarlene;}
					if(ename == "zombieMarlene"){kill = zombieMarlene;}
					if(ename == "zombieYo"){kill = zombieYo;}
					if(ename == "zombieSteve"){kill = zombieSteve;}
					if(ename == "zombieSteveCrawler"){kill = zombieSteveCrawler;}
					if(ename == "snowzombie"){kill = snowzombie;}
					if(ename == "spiderzombie"){kill = spiderzombie;}
					if(ename == "burntzombie"){kill = burntzombie;}
					if(ename == "zombieNurse"){kill = zombieNurse;}
					if(ename == "fatzombiecop"){kill = fatzombiecop;}
					if(ename == "hornet"){kill = hornet;}
					if(ename == "zombiedog"){kill = zombiedog;}
					if(ename == "zombieBear"){kill = zombieBear;}
				}
				if(KillAnimal)
				{
					if(ename == "animalStag"){kill = animalStag;}
					if(ename == "animalBear"){kill = animalBear;}
					if(ename == "animalRabbit"){kill = animalRabbit;}
					if(ename == "animalChicken"){kill = animalChicken;}
					if(ename == "animalPig"){kill = animalPig;}
				}
					
				if(!storedData.ShopMoney.ContainsKey(_cInfo.playerId))
				{
					storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
					storedData.ShopMoney[_cInfo.playerId].Money = "0";
					ShopMoney shop = storedData.ShopMoney[_cInfo.playerId];
					int money = Int32.Parse(shop.Money);
					money = money + kill;
					storedData.ShopMoney[_cInfo.playerId].Money = money.ToString();
					SaveData();
					_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} {1}$ {2} {3} [FFFFFF]", "ShowGiveMoneyForKill", kill, GetMessage("ShowGiveMoneyForKill1", _cInfo.playerId), ename), "Shop", false, "", false));
				}
				else
				{
					ShopMoney shop = storedData.ShopMoney[_cInfo.playerId];
					int money = Int32.Parse(shop.Money);
					money = money + kill;
					storedData.ShopMoney[_cInfo.playerId].Money = money.ToString();
					SaveData();
					_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FF0000]{1}$[FF8000] {2} [FF0000]{3} [FFFFFF]", GetMessage("ShowGiveMoneyForKill", _cInfo.playerId), kill, GetMessage("ShowGiveMoneyForKill1", _cInfo.playerId), ename), "Shop", false, "", false));
				}
			}
		}
		void OnPlayerChat(ClientInfo _cInfo, string message)
		{
			if (!string.IsNullOrEmpty(message) && message.StartsWith("/") && !string.IsNullOrEmpty(_cInfo.playerName) )
			{
				EntityPlayer _player = GameManager.Instance.World.Players.dict[_cInfo.entityId];
				string _filter = "[ffffffff][/url][/b][/i][/u][/s][/sub][/sup][ff]";
				if (message.EndsWith(_filter))
				{
					message = message.Remove(message.Length - _filter.Length);
				}
				if (!string.IsNullOrEmpty(_cInfo.playerName))
				{
					if ( message.StartsWith("/") )
					{
						message = message.Replace("/", "");
						string mesg = message.ToLower();
						if ( message == "money" )
						{
							if(!storedData.ShopMoney.ContainsKey(_cInfo.playerId))
							{
								storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
								storedData.ShopMoney[_cInfo.playerId].Money = "0";
								SaveData();
							}
							message = message.Replace("money", "");
							ShopMoney shop = storedData.ShopMoney[_cInfo.playerId];
							string money = shop.Money;
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} - {1}$ [FFFFFF]", GetMessage("HaveMoney", _cInfo.playerId), money), "Shop", false, "", false));
						}
						if ( message.StartsWith("buy ") )
						{
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
								int colitem = 1;
								string[] buycount = message.Split(' ');
								int countbuy = 0;
								foreach (string _messagebuy in buycount){countbuy++;}
								if(countbuy == 2){pay = pay * Int32.Parse(itembuy[1]); colitem = Int32.Parse(itembuy[1]);}
								string _item = shopitem.Name;
								if (money >= pay)
								{
									ItemValue value = new ItemValue(ItemClass.GetItem(_item).type, true);
									if (value.HasQuality)
									{
										if (_player.Level < 5){value.Quality = 50;}
										if (_player.Level >= 5 && _player.Level < 10){value.Quality = 100;}
										if (_player.Level >= 10 && _player.Level < 15){value.Quality = 150;}
										if (_player.Level >= 15 && _player.Level < 20){value.Quality = 200;}
										if (_player.Level >= 20 && _player.Level < 30){value.Quality = 250;}
										if (_player.Level >= 30 && _player.Level < 40){value.Quality = 300;}
										if (_player.Level >= 40 && _player.Level < 50){value.Quality = 350;}
										if (_player.Level >= 50 && _player.Level < 60){value.Quality = 400;}
										if (_player.Level >= 60 && _player.Level < 70){value.Quality = 450;}
										if (_player.Level >= 70 && _player.Level < 80){value.Quality = 500;}
										if (_player.Level >= 80 && _player.Level < 90){value.Quality = 550;}
										if (_player.Level >= 90){value.Quality = 600;}
									}
									ItemStack stack = new ItemStack(value, colitem);
									GameManager.Instance.ItemDropServer(stack, _player.GetPosition(), Vector3.zero, -1, 50f);
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} {1} {2}{4} {3}$ [FFFFFF]", GetMessage("BuyItem", _cInfo.playerId), shopitem.NameItem, colitem, pay, GetMessage("NumberOrPay", _cInfo.playerId)), "Shop", false, "", false));
									money = money - pay;
									storedData.ShopMoney.Remove(_cInfo.playerId);
									storedData.ShopMoney.Add(_cInfo.playerId, new ShopMoney());
									storedData.ShopMoney[_cInfo.playerId].Money = money.ToString();
									SaveData();
								}
								else
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("ErrorBuyItem", _cInfo.playerId)), "Shop", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("ErrorItem", _cInfo.playerId)), "Shop", false, "", false));
							}
						}
						if ( mesg.StartsWith("shopadd ") )
						{
							if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
							{
								message = message.Replace("shopadd ", "");
								String[] itembuy = message.Split(new Char [] {' ', ',', '.', ':', '\t' });
								string itemname = itembuy[0];
								string itempay = itembuy[1];
								int ctg = 0;
								string[] buycount = message.Split(' ');
								string category = "NULL";
								string translatename = "NULL";
								int countbuy = 0;
								foreach (string _messagebuy in buycount){countbuy++;}
								if(countbuy == 2){category = "other"; translatename = itemname;}
								else if(countbuy == 3){category = itembuy[2]; translatename = itemname;}
								else if(countbuy == 4){category = itembuy[2]; translatename = itembuy[3];}
								else if(countbuy == 5){category = itembuy[2]; translatename = itembuy[3]+" "+itembuy[4];}
								else {category = itembuy[2]; translatename = itembuy[3]+" "+itembuy[4]+" "+itembuy[5];}
								if (countbuy >= 2 && countbuy <= 6)
								{
									if(category == "ammo"){ctg = 1;}
									else if(category == "weapon"){ctg = 101;}
									else if(category == "build"){ctg = 201;}
									else if(category == "block"){ctg = 301;}
									else if(category == "recipe"){ctg = 401;}
									else if(category == "armor"){ctg = 501;}
									else if(category == "food"){ctg = 601;}
									else if(category == "medicine"){ctg = 701;}
									else if(category == "book"){ctg = 801;}
									else {category = "other"; ctg = 901; translatename = itembuy[2]+" "+translatename;}
									for (int i = ctg; i <= ctg + 98; i++)
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
											_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("ShowItemAdded", _cInfo.playerId)), "Shop", false, "", false));
											_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0}: {1} [FFFFFF]", GetMessage("ShowItemName", _cInfo.playerId), itemname), "Shop", false, "", false));
											_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0}: {1} [FFFFFF]", GetMessage("ShowItemPay", _cInfo.playerId), itempay), "Shop", false, "", false));
											_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0}: {1} [FFFFFF]", GetMessage("ShowCategory", _cInfo.playerId), category), "Shop", false, "", false));
											_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0}: {1} [FFFFFF]", GetMessage("ShowTranslate", _cInfo.playerId), translatename), "Shop", false, "", false));
											_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0}: {1} [FFFFFF]", GetMessage("ShowNumber", _cInfo.playerId), _i), "Shop", false, "", false));
											break;
										}
									}
								}
								else
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("ErrorAddItem", _cInfo.playerId)), "Shop", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("NotAdmin", _cInfo.playerId)), "Shop", false, "", false));
							}
						}
						if ( mesg.StartsWith("shopdel ") )
						{
							if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
							{
								message = message.Replace("shopdel ", "");
								string item = message.Split(' ')[0];
								if(storedData.ShopItems.ContainsKey(item))
								{
									storedData.ShopItems.Remove(item);
									SaveData();
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("ShowDeleted", _cInfo.playerId)), "Shop", false, "", false));
								}
								else
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("ErrorDelItem", _cInfo.playerId)), "Shop", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("NotAdmin", _cInfo.playerId)), "Shop", false, "", false));
							}
						}
						if ( mesg.StartsWith("givemoney ") )
						{
							message = message.Replace("givemoney ", "");
							if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
							{
								string nikname = message.Split(' ')[0];
								string _value = message.Replace(nikname + " ", "");
								string value = _value.Split(' ')[0];
								ClientInfo _targetInfo = ConsoleHelper.ParseParamIdOrName(nikname);
								if (_targetInfo == null)
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("NotFound", _cInfo.playerId)), "Shop", false, "", false));
								}
								else
								{
									if(!storedData.ShopMoney.ContainsKey(_targetInfo.playerId))
									{
										int valuepay = Int32.Parse(value);
										storedData.ShopMoney.Add(_targetInfo.playerId, new ShopMoney());
										storedData.ShopMoney[_targetInfo.playerId].Money = valuepay.ToString();
										SaveData();
										_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} ({2}$)[FFFFFF]", GetMessage("ShowYouSend", _cInfo.playerId), _targetInfo.playerName, valuepay.ToString()), "Shop", false, "", false));
										_targetInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} ({2}$)[FFFFFF]", GetMessage("ShowYouGet", _cInfo.playerId), _cInfo.playerName, valuepay.ToString()), "Shop", false, "", false));
									}
									else
									{
										ShopMoney shop = storedData.ShopMoney[_targetInfo.playerId];
										int money = Int32.Parse(shop.Money);
										int valuepay = Int32.Parse(value);
										money = money + valuepay;
										storedData.ShopMoney[_targetInfo.playerId].Money = money.ToString();
										SaveData();
										_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} ({2}$)[FFFFFF]", GetMessage("ShowYouSend", _cInfo.playerId), _targetInfo.playerName, valuepay.ToString()), "Shop", false, "", false));
										_targetInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} ({2}$)[FFFFFF]", GetMessage("ShowYouGet", _cInfo.playerId), _cInfo.playerName, valuepay.ToString()), "Shop", false, "", false));
									}
								}
							}
						}
						if ( mesg.StartsWith("sendmoney ") )
						{
							message = message.Replace("sendmoney ", "");
							string nikname = message.Split(' ')[0];
							string _value = message.Replace(nikname + " ", "");
							string value = _value.Split(' ')[0];
							ClientInfo _targetInfo = ConsoleHelper.ParseParamIdOrName(nikname);
							if (_targetInfo == null)
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("NotFound", _cInfo.playerId)), "Shop", false, "", false));
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
											_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} ({2}$)[FFFFFF]", GetMessage("ShowYouSend", _cInfo.playerId), _targetInfo.playerName, valuepay.ToString()), "Shop", false, "", false));
											_targetInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} ({2}$)[FFFFFF]", GetMessage("ShowYouGet", _cInfo.playerId), _cInfo.playerName, valuepay.ToString()), "Shop", false, "", false));
										}
										else
										{
										ShopMoney targetshop = storedData.ShopMoney[_targetInfo.playerId];
										int money = Int32.Parse(targetshop.Money);
										money = money + valuepay;
										_money = _money - valuepay;
										storedData.ShopMoney[_targetInfo.playerId].Money = money.ToString();
										storedData.ShopMoney[_cInfo.playerId].Money = _money.ToString();
										SaveData();
										_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} ({2}$)[FFFFFF]", GetMessage("ShowYouSend", _cInfo.playerId), _targetInfo.playerName, valuepay.ToString()), "Shop", false, "", false));
										_targetInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} ({2}$)[FFFFFF]", GetMessage("ShowYouGet", _cInfo.playerId), _cInfo.playerName, valuepay.ToString()), "Shop", false, "", false));
										}
									}
									else
									{
										_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("ErrorBuyItem", _cInfo.playerId)), "Shop", false, "", false));
									}
								}
								else
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("ErrorSendMoney", _cInfo.playerId)), "Shop", false, "", false));
								}
							}
						}
						if ( mesg == "helpshop" || mesg == "shophelp")
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("ShowMoney", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("ShowBuy", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("ShowSendMoney", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("ShowShopList", _cInfo.playerId)), "Shop", false, "", false));
							if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("ShowShopAdd", _cInfo.playerId)), "Shop", false, "", false));
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("ShowShopDel", _cInfo.playerId)), "Shop", false, "", false));
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("ShowGiveMoney", _cInfo.playerId)), "Shop", false, "", false));
							}
						}
						if ( mesg == "shop" )
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}[FFFFFF]", GetMessage("ShopAmmo", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}[FFFFFF]", GetMessage("ShopWeapon", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}[FFFFFF]", GetMessage("ShopBuild", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}[FFFFFF]", GetMessage("ShopBlock", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}[FFFFFF]", GetMessage("ShopRecipe", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}[FFFFFF]", GetMessage("ShopArmor", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}[FFFFFF]", GetMessage("ShopFood", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}[FFFFFF]", GetMessage("ShopMedicine", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}[FFFFFF]", GetMessage("ShopBook", _cInfo.playerId)), "Shop", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}[FFFFFF]", GetMessage("ShopOther", _cInfo.playerId)), "Shop", false, "", false));
						}
						if ( mesg == "shopammo" )
						{
							int ctg = 1;
							for (int i = ctg; i <= ctg + 98; i++)
							{
								string _i = i.ToString();
								if(storedData.ShopItems.ContainsKey(_i))
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}. {1} - {2}$[FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, storedData.ShopItems[_i].Pay), "Shop", false, "", false));
								}
							}
						}
						if ( mesg == "shopweapon" )
						{
							int ctg = 101;
							for (int i = ctg; i <= ctg + 98; i++)
							{
								string _i = i.ToString();
								if(storedData.ShopItems.ContainsKey(_i))
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}. {1} - {2}$[FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, storedData.ShopItems[_i].Pay), "Shop", false, "", false));
								}
							}
						}
						if ( mesg == "shopbuild" )
						{
							int ctg = 201;
							for (int i = ctg; i <= ctg + 98; i++)
							{
								string _i = i.ToString();
								if(storedData.ShopItems.ContainsKey(_i))
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}. {1} - {2}$[FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, storedData.ShopItems[_i].Pay), "Shop", false, "", false));
								}
							}
						}
						if ( mesg == "shopblock" )
						{
							int ctg = 301;
							for (int i = ctg; i <= ctg + 98; i++)
							{
								string _i = i.ToString();
								if(storedData.ShopItems.ContainsKey(_i))
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}. {1} - {2}$[FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, storedData.ShopItems[_i].Pay), "Shop", false, "", false));
								}
							}
						}
						if ( mesg == "shoprecipe" )
						{
							int ctg = 401;
							for (int i = ctg; i <= ctg + 98; i++)
							{
								string _i = i.ToString();
								if(storedData.ShopItems.ContainsKey(_i))
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}. {1} - {2}$[FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, storedData.ShopItems[_i].Pay), "Shop", false, "", false));
								}
							}
						}
						if ( mesg == "shoparmor" )
						{
							int ctg = 501;
							for (int i = ctg; i <= ctg + 98; i++)
							{
								string _i = i.ToString();
								if(storedData.ShopItems.ContainsKey(_i))
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}. {1} - {2}$[FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, storedData.ShopItems[_i].Pay), "Shop", false, "", false));
								}
							}
						}
						if ( mesg == "shopfood" )
						{
							int ctg = 601;
							for (int i = ctg; i <= ctg + 98; i++)
							{
								string _i = i.ToString();
								if(storedData.ShopItems.ContainsKey(_i))
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}. {1} - {2}$[FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, storedData.ShopItems[_i].Pay), "Shop", false, "", false));
								}
							}
						}
						if ( mesg == "shopmedicine" )
						{
							int ctg = 701;
							for (int i = ctg; i <= ctg + 98; i++)
							{
								string _i = i.ToString();
								if(storedData.ShopItems.ContainsKey(_i))
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}. {1} - {2}$[FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, storedData.ShopItems[_i].Pay), "Shop", false, "", false));
								}
							}
						}
						if ( mesg == "shopbook" )
						{
							int ctg = 801;
							for (int i = ctg; i <= ctg + 98; i++)
							{
								string _i = i.ToString();
								if(storedData.ShopItems.ContainsKey(_i))
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}. {1} - {2}$[FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, storedData.ShopItems[_i].Pay), "Shop", false, "", false));
								}
							}
						}
						if ( mesg == "shopother" )
						{
							int ctg = 901;
							for (int i = ctg; i <= ctg + 98; i++)
							{
								string _i = i.ToString();
								if(storedData.ShopItems.ContainsKey(_i))
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000]{0}. {1} - {2}$[FFFFFF]", storedData.ShopItems[_i].Number, storedData.ShopItems[_i].NameItem, storedData.ShopItems[_i].Pay), "Shop", false, "", false));
								}
							}
						}
					}
				}
			}
		}
		void OnServerSave() => SaveData();
		void Unload() => SaveData();
	}
}