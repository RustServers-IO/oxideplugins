/*MiniEvents Contains 3 Simple Mini Events N1: Lottery Of Number N2: Lottery Of Players N3: Math Questions.
Player commands in detail:
/enter (Number) => Enter event lottery of number.
/enter => Enter event lottery of players.

Permission MiniEvents:
Admin rcon or permission minievents.admin

Admin commands in detail:
/esystem autoevents => Enable or disable auto events.
/esystem eventnow => Event start now (Works even if the auto events are false).
/esystem math => Enable or disable math questions.
/esystem lottery => Enable or disable event lottery.
/esystem lottery players => Enable or disable event lottery of players (If false lottery is by number).
/esystem randomevents => Enable or disable random events.
/ecfg tagchat (TagName) => Choose tag chat.
/ecfg minimumplayers (NumberPlayers) => Choose players minimun to start event.
/ecfg timeautoevents (Number Of Minute/s) => Choose time auto events.
/ecfg timeenterlottery (Number Of Minute/s) => Choose time to enter lottery. 
/ecfg timecloseanswersmath (Number Of Minute/s) => Choose time close answers math.
/ecfg maxnumberlottery (Number) => Choose max number lottery of number.*/
using System.Collections.Generic;
using System;
using Oxide.Core.Plugins;
using Oxide.Core;
using System.Linq;

namespace Oxide.Plugins
{
	[Info("MiniEvents", "tugamano (Lottery idea by P0LENT4)", "0.0.2")]
	class MiniEvents : RustLegacyPlugin
	{
		/// <summary> Plugin MiniEvents:
		/// Lottery idea by P0LENT4.
		/// Math idea by tugamano.
		/// Work done by tugamano.
		/// </summary>
		// Settings of plugin.
		static Plugins.Timer timerAutoEvents;
		static Plugins.Timer timerAnswersMath;
		static System.Random random = new System.Random();
		const string permissionMiniEvents	= "minievents.admin";
		static string tagChat				= "minievents";
		// Settings of events.
		static bool autoEvents				= true;
		static bool randomEvents			= true;
		static float timeAutoEvents			= 15f;
		static int minimumPlayersEvent		= 5;
		// Settings of event lottery.
		static bool autoLottery				= false;
		static bool openLottery				= false;
		static bool lotteryOfPlayers		= false;
		static float timeToEnterLottery		= 3f;
		static int maximumNumberLottery		= 30;
		// Settings of event Math.
		static bool autoMath				= true;
		static bool openMath				= false;
		static int numberQuestion			= 0;
		static float timeCloseAnswersMath	= 3f;


		// Dictionary players in lottery.
		static Dictionary<NetUser, int> playersInLottery = new Dictionary<NetUser, int>();
		// Dictionarys prizes lotterys.
		static Dictionary<string, int> prizeLotteryOfNumber = GetPrizeLotteryOfNumber();
		static Dictionary<string, int> prizeLotteryOfPlayers = GetPrizeLotteryOfPlayers();


		static Dictionary<string, int> GetPrizeLotteryOfNumber()
		{
			var newDict = new Dictionary<string, int>();
			newDict.Add("M4", 1);
			newDict.Add("Holo sight", 1);
			newDict.Add("556 Ammo", 250);
			newDict.Add("Kevlar Helmet", 1);
			newDict.Add("Kevlar Vest", 1);
			newDict.Add("Kevlar Pants", 1);
			newDict.Add("Kevlar Boots", 1);
			newDict.Add("Explosive Charge", 2);
			return newDict;
		}


		static Dictionary<string, int> GetPrizeLotteryOfPlayers()
		{
			var newDict = new Dictionary<string, int>();
			newDict.Add("P250", 1);
			newDict.Add("Holo sight", 1);
			newDict.Add("9mm Ammo", 150);
			newDict.Add("Leather Helmet", 1);
			newDict.Add("Leather Vest", 1);
			newDict.Add("Leather Pants", 1);
			newDict.Add("Leather Boots", 1);
			return newDict;
		}


		// List questions answers prizes.
		static List<string[]> questionsAnswersPrizes = GetQuestionsAnswersPrizes();
		static List<string[]> GetQuestionsAnswersPrizes()
		{
			var newList = new List<string[]>();
			newList.Add(new string[] {"7 x 9 x 19", "1197", "Wood", "250"});
			newList.Add(new string[] {"120 ÷ 6 + 347", "367", "M4", "1"});
			newList.Add(new string[] {"83 - 25 x 8", "464", "9mm Ammo", "150"});
			newList.Add(new string[] {"467 + 224 + 23", "714", "P250", "1"});
			newList.Add(new string[] {"780 ÷ 13 ÷ 6", "10", "556 Ammo", "150"});
			newList.Add(new string[] {"977 - 617 - 327", "33", "Sulfur", "250"});
			newList.Add(new string[] {"147 x 3 x 11", "4851", "Explosive Charge", "2"});
			newList.Add(new string[] {"642 ÷ 5 ÷ 3", "32,1", "Kevlar Vest", "5"});
			newList.Add(new string[] {"37 + 195 + 38", "270", "MP5A4", "1"});
			newList.Add(new string[] {"19 x 17 x 3", "969", "Low Quality Metal", "150"});
			newList.Add(new string[] {"617 - 296 - 113", "208", "Animal Fat", "250"});
			newList.Add(new string[] {"126 ÷ 8 + 323", "338,75", "Wood Planks", "250"});
			newList.Add(new string[] {"53 - 19 x 20", "680", "Shotgun Shells", "150"});
			newList.Add(new string[] {"437 + 254 + 73", "764", "Holo sight", "3"});
			newList.Add(new string[] {"9 x 13 x 4", "468", "Shotgun", "1"});
			newList.Add(new string[] {"17 x 6 + 437", "539", "Low Grade Fuel", "250"});
			newList.Add(new string[] {"127 ÷ 4 + 670", "701,75", "Small Rations", "50"});
			newList.Add(new string[] {"242 - 79 - 37", "126", "Large Medkit", "50"});
			newList.Add(new string[] {"87 + 647 + 311", "1045", "F1 Grenade", "6"});
			newList.Add(new string[] {"4 x 17 x 38", "2584", "Explosives", "25"});
			newList.Add(new string[] {"630 ÷ 6 ÷ 4", "26,25", "Metal Fragments", "250"});
			newList.Add(new string[] {"920 ÷ 8 + 497", "612", "Metal Pillar", "50"});
			newList.Add(new string[] {"93 - 27 x 8", "528", "Metal Door", "10"});
			newList.Add(new string[] {"954 + 347 - 709", "592", "Sulfur Ore", "250"});
			newList.Add(new string[] {"206 - 179 - 18", "9", "Kevlar Helmet", "5"});
			newList.Add(new string[] {"17 + 697 + 118", "832", "Wood", "250"});
			newList.Add(new string[] {"987 ÷ 7 ÷ 3", "47", "Gunpowder", "250"});
			newList.Add(new string[] {"703 - 79 x 3", "1872", "Cloth", "250"});
			newList.Add(new string[] {"199 + 195 + 197", "591", "Leather", "250"});
			newList.Add(new string[] {"37 x 19 + 445", "1148", "Kevlar Pants", "5"});
			return newList;
		}


		//Mensagens lang api.
		string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
		void LoadDefaultMessages()
		{
			var message = new Dictionary<string, string>
			{
				{"OnPlayerChat", "[color red]{0} [color clear]answered well the mathematical question! Prize  [color lime]{1}  [color clear]Amount  [color red]{2}"},
				{"AutoStartGameMath", "Auto event math how much does [color red]{0} [color clear]¿?? have [color lime]{1} [color clear]minute/s to respond."},
				{"AutoStartGameMath1", "Time [color red]ended [color clear]to answer the math question!"},
				{"EventLottery", "Lottery raffle player opened will start in [color red] {0} [color clear] minute/s to participate use [color lime]/enter"},
				{"EventLottery1", "Lottery raffle number opened will start in [color red] {0} [color clear] minute/s to participate use [color lime]/enter [color cyan](Number)"},
				{"EventLottery2", "Lottery event can not start no have minimum [color red]{0}  [color clear]player/s!"},
				{"EventLottery3", "[color red]{0} [color clear]won in the lottery of players."},
				{"EventLottery4", "[color red]{0} [color clear]won lottery of number [color lime]{1}[color clear]."},
				{"EventLottery5", "No one has hit the lottery number chosen number [color red]{0}[color clear]."},
				{"PrizeInInventory", "Prize in your inventory!"},
				{"HelpAdmins", "===================== [color lime]Helps Admins [color clear]====================="},
				{"HelpAdmins1", "/esystem (autoevents || eventnow || math) => Configurations."},
				{"HelpAdmins2", "/esystem (lottery || lottery players || randomevents) => Configurations."},
				{"HelpAdmins3", "/ecfg (tagchat || minimumplayers || timeautoevents) => Systems."},
				{"HelpAdmins4", "/ecfg (timeenterlottery || timecloseanswersmath || maxnumberlottery) => Systems."},
				{"HelpAdmins5", "======================================================"},
				{"EventsConfig", "Use /ecfg tagchat (TagName) => Change tag chat."},
				{"EventsConfig1", "Tag chat chosen {0}."},
				{"EventsConfig2", "Use /ecfg minimumplayers (Number) => Choose player minimum to start the event."},
				{"EventsConfig3", "Minimum of players to start event now is {0}."},
				{"EventsConfig4", "Use /ecfg timeautoevents (NumberMinute/s) => Change time auto events."},
				{"EventsConfig5", "Time auto events events now is {0} minute/s."},
				{"EventsConfig6", "Use /ecfg timeenterlottery (NumberMinute/s) => Choosing time for the player can enter the lottery."},
				{"EventsConfig7", "Time for the player can enter the lottery now is {0} minute/s."},
				{"EventsConfig8", "Use /ecfg timecloseanswersmath (NumberMinute/s) => Choose time to answer the math event question."},
				{"EventsConfig9", "Time to answer the math event now is {0} minute/s."},
				{"EventsConfig10", "Use /ecfg maxnumberlottery (Number) => Choose max number lottery of number."},
				{"EventsConfig11", "Max number lottery of number now is {0}."},
				{"EventsSystems", "{0} [color red]disabled [color clear]auto events."},
				{"EventsSystems1", "{0} [color lime]activated [color clear]auto events."},
				{"EventsSystems2", "Lottery of players now is {0}."},
				{"EventsSystems3", "Lottery event now is {0}, event Math now is {1}."},
				{"EventsSystems4", "Math event now is {0}, event lottery now is {1}."},
				{"EventsSystems5", "Random Events is {0}."},
				{"NoHaveAcess", "You are not allowed to use this command."},
				{"EnterLottery", "Entry into lottery event is [color red]closed[color clear]!"},
				{"EnterLottery1", "You are already  [color red]attending [color clear]the lottery event!"},
				{"EnterLottery2", "[color red]{0} [color clear]joined the lottery of players [color lime]/enter [color clear]players in the event [color red]{1}[color clear]."},
				{"EnterLottery3", "Use /enter [color red](Number) [color clear]=> Participate event lottery number."},
				{"EnterLottery4", "You must choose a lottery number from [color lime]0 [color clear]to [color red]{0} [color clear]maximum."},
				{"EnterLottery5", "[color red]{0} [color clear]joined the lottery of number [color lime]{1} [color clear]/enter (Number) players in the event [color red]{2}[color clear]."}
			}; 
			lang.RegisterMessages(message, this);
		}


		void OnServerInitialized()
		{
			CheckCfg<string>("Settings: Tag Chat", ref tagChat);
			CheckCfg<bool>("Settings: Auto Events", ref autoEvents);
			CheckCfg<bool>("Settings: Random Events", ref randomEvents);
			CheckCfg<float>("Settings: Time Auto Events", ref timeAutoEvents);
			CheckCfg<int>("Settings: Minimum Players Event", ref minimumPlayersEvent);
			CheckCfg<bool>("Settings: Auto Lottery", ref autoLottery);
			CheckCfg<bool>("Settings: Lottery Of Players", ref lotteryOfPlayers);
			CheckCfg<float>("Settings: Time To Enter Lottery", ref timeToEnterLottery);
			CheckCfg<int>("Settings: Maximum Number Lottery", ref maximumNumberLottery);
			CheckCfg<bool>("Settings: Auto Math", ref autoMath);
			CheckCfg<float>("Settings: Time Close Answers Math", ref timeCloseAnswersMath);
			CheckCfg<Dictionary<string, int>>("Settings: Prize Lottery Of Number", ref prizeLotteryOfNumber);
			CheckCfg<Dictionary<string, int>>("Settings: Prize Lottery Of Players", ref prizeLotteryOfPlayers);
			CheckCfg<List<string[]>>("Settings: Questions Answers Prizes", ref questionsAnswersPrizes);
			permission.RegisterPermission(permissionMiniEvents, this);
			SaveConfig();
			LoadDefaultMessages();
			AutoStartEvents();
		}


		protected override void LoadDefaultConfig(){} 
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}


		bool Access(NetUser netuser)
		{
			if (netuser.CanAdmin())return true;
			if(permission.UserHasPermission(netuser.playerClient.userID.ToString(), permissionMiniEvents)) return true;
			return false;
		}


		private void OnPlayerDisconnected(uLink.NetworkPlayer netPlayer){
			var netuser = netPlayer.GetLocalData() as NetUser;
			if(playersInLottery.ContainsKey(netuser))
			playersInLottery.Remove(netuser);
		}


		void AutoStartEvents()
		{
			if(autoEvents)
			timerAutoEvents = timer.Repeat(timeAutoEvents * 60, 0, () => StartEvents());
		}


		void OnPlayerChat(NetUser netuser, string chat)
		{
			if(autoMath && openMath && chat.Length < 6)
			{
				var listQuestions = questionsAnswersPrizes.ToList();
				var question = listQuestions[numberQuestion] as string[];
				if(chat == question[1])
				{
					openMath = false;
					timerAnswersMath.Destroy();
					rust.BroadcastChat(tagChat, string.Format(GetMessage("OnPlayerChat"), netuser.displayName, question[2], question[3]));
					rust.Notice(netuser, GetMessage("PrizeInInventory", netuser.userID.ToString()));
					GivePrize(netuser, question[2], Convert.ToInt32(question[3]));
				}
			}
		}


		void StartEvents()
		{
			if(PlayerClient.All.Count < minimumPlayersEvent)return;
			if(randomEvents)
			{
				if(!autoLottery && !lotteryOfPlayers)
				{
					autoLottery		 = true;
					lotteryOfPlayers = true;
				}
				else if(autoLottery && lotteryOfPlayers)
				lotteryOfPlayers	= false;
				else if(autoLottery && !lotteryOfPlayers)
				autoLottery			= false;
			}
			if(autoLottery)
			EventLottery(); 
			else if(autoMath){
				var listQuestions = questionsAnswersPrizes.ToList();
				int randomQuestion = random.Next(0, listQuestions.Count);
				var questionRandom = listQuestions[numberQuestion] as string[];
				if(questionRandom == null)
				{
					Puts( "Questions Number: " + randomQuestion.ToString() + " is nuul! I advise you to delete the config MiniEvents."); 
					return;
				}
				openMath = true;
				numberQuestion = randomQuestion;
				rust.BroadcastChat(tagChat, string.Format(GetMessage("AutoStartGameMath"), questionRandom[0], timeCloseAnswersMath));
				timerAnswersMath = timer.Once(timeCloseAnswersMath * 60, ()=>
				{ 
					openMath = false;
					rust.BroadcastChat(tagChat, GetMessage("AutoStartGameMath1"));
				});
			}
		}


		void EventLottery()
		{
			openLottery = true;
			if(lotteryOfPlayers)
			rust.BroadcastChat(tagChat, string.Format(GetMessage("EventLottery"), timeToEnterLottery));
			else
			rust.BroadcastChat(tagChat, string.Format(GetMessage("EventLottery1"), timeToEnterLottery));
			timer.Once(timeToEnterLottery * 60, ()=> 
			{
				if(playersInLottery.Count < minimumPlayersEvent)
				{
					openLottery = false;
					playersInLottery.Clear();
					rust.BroadcastChat(tagChat, string.Format(GetMessage("EventLottery2"), minimumPlayersEvent));
					return;
				}
				openLottery = false;
				var players = playersInLottery.ToList();
				if(lotteryOfPlayers)
				{
					int randomPlayers = random.Next(0, players.Count);
					var playerWiner = players[randomPlayers];
					rust.BroadcastChat(tagChat, string.Format(GetMessage("EventLottery3"), playerWiner.Key.displayName));
					if(prizeLotteryOfNumber.Count > 0)
					{
						foreach(var item in prizeLotteryOfPlayers)
						GivePrize(playerWiner.Key, item.Key, item.Value);
						rust.Notice(playerWiner.Key, GetMessage("PrizeInInventory", playerWiner.Key.userID.ToString()));
					}
					playersInLottery.Clear();
				}
				else
				{
					int randomNumber = random.Next(0, maximumNumberLottery);
					foreach(var player in players){
						if(player.Value == randomNumber)
						{
							rust.BroadcastChat(tagChat, string.Format(GetMessage("EventLottery4"), player.Key.displayName, randomNumber));
							if(prizeLotteryOfNumber.Count > 0)
							{
								foreach(var item in prizeLotteryOfNumber)
								GivePrize(player.Key, item.Key, item.Value);
								rust.Notice(player.Key, GetMessage("PrizeInInventory", player.Key.userID.ToString()));
							}
							playersInLottery.Clear();
							openLottery = false;
							return;
						}
					}
					openLottery = false;
					rust.BroadcastChat(tagChat, string.Format(GetMessage("EventLottery5"), randomNumber));
					playersInLottery.Clear();
				}
			});
		}


		void GivePrize(NetUser netuser, string item, int amount)
		{
			ItemDataBlock Item = DatablockDictionary.GetByName(item);
			if(Item == null)return;
			Inventory inventario = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			inventario.AddItemAmount(Item, amount);
		}


		void HelpAdmins(NetUser netuser)
		{
			var id = netuser.userID.ToString();
			rust.SendChatMessage(netuser, tagChat, GetMessage("HelpAdmins", id));
			rust.SendChatMessage(netuser, tagChat, GetMessage("HelpAdmins1", id));
			rust.SendChatMessage(netuser, tagChat, GetMessage("HelpAdmins2", id));
			rust.SendChatMessage(netuser, tagChat, GetMessage("HelpAdmins3", id));
			rust.SendChatMessage(netuser, tagChat, GetMessage("HelpAdmins4", id));
			rust.SendChatMessage(netuser, tagChat, GetMessage("HelpAdmins5", id));
		}


		[ChatCommand("ecfg")]
		void cmdEventsConfigurations(NetUser netuser, string command, string[] args)
		{
			var id = netuser.userID.ToString();
			if(!Access(netuser)){rust.SendChatMessage(netuser, tagChat, GetMessage("NoHaveAcess", id));return;}
			if(args.Length == 0){HelpAdmins(netuser);return;}
			switch(args[0])
			{
				case "tagchat":
					if(args.Length < 2){rust.SendChatMessage(netuser, tagChat, GetMessage("EventsConfig", id));return;}
					tagChat = args[1].ToString();
					Config["Settings: Tag Chat"] = tagChat;
					rust.Notice(netuser, string.Format(GetMessage("EventsConfig1"), tagChat));
					break;
				case "minimumplayers":
					if(args.Length < 2){rust.SendChatMessage(netuser, tagChat, GetMessage("EventsConfig2", id));return;}
					minimumPlayersEvent = Convert.ToInt32(args[1]);
					Config["Settings: Minimum Players Event"] = minimumPlayersEvent;
					rust.Notice(netuser, string.Format(GetMessage("EventsConfig3"), minimumPlayersEvent));
					break;
				case "timeautoevents":
					if(args.Length < 2){rust.SendChatMessage(netuser, tagChat, GetMessage("EventsConfig4", id));return;}
					timeAutoEvents = Convert.ToSingle(args[1]);
					Config["Settings: Time Auto Events"] = timeAutoEvents;
					rust.Notice(netuser, string.Format(GetMessage("EventsConfig5"), timeAutoEvents));
					break;
				case "timeenterlottery":
					if(args.Length < 2){rust.SendChatMessage(netuser, tagChat, GetMessage("EventsConfig6", id));return;}
					timeToEnterLottery = Convert.ToSingle(args[1]);
					Config["Settings: Time To Enter Lottery"] = timeToEnterLottery;
					rust.Notice(netuser, string.Format(GetMessage("EventsConfig7"), timeToEnterLottery));
					break;
				case "timecloseanswersmath":
					if(args.Length < 2){rust.SendChatMessage(netuser, tagChat, GetMessage("EventsConfig8", id));return;}
					timeCloseAnswersMath = Convert.ToSingle(args[1]);
					Config["Settings: Time Close Answers Math"] = timeCloseAnswersMath;
					rust.Notice(netuser, string.Format(GetMessage("EventsConfig9"), timeCloseAnswersMath));
					break;
				case "maxnumberlottery":
					if(args.Length < 2){rust.SendChatMessage(netuser, tagChat, GetMessage("EventsConfig10", id));return;}
					maximumNumberLottery = Convert.ToInt32(args[1]);
					Config["Settings: Maximum Number Lottery"] = maximumNumberLottery;
					rust.Notice(netuser, string.Format(GetMessage("EventsConfig11"), maximumNumberLottery));
					break;
					default:{
					HelpAdmins(netuser);
					break;
				}
			}
			SaveConfig();
		}


		[ChatCommand("esystem")]
		void cmdEventsSystems(NetUser netuser, string command, string[] args)
		{
			var id = netuser.userID.ToString();
			if(!Access(netuser)){rust.SendChatMessage(netuser, tagChat, GetMessage("NoHaveAcess", id));return;}
			if(args.Length == 0){HelpAdmins(netuser);return;}
			switch(args[0].ToLower())
			{
				case "autoevents":
					if(autoEvents)
					{
						autoEvents = false;
						timerAutoEvents.Destroy();
						rust.BroadcastChat(tagChat, string.Format(GetMessage("EventsSystems"), netuser.displayName));
					}
					else
					{
						autoEvents = true;
						AutoStartEvents();
						rust.BroadcastChat(tagChat, string.Format(GetMessage("EventsSystems1"), netuser.displayName));
					}
					Config["Settings: Auto Events"] = autoEvents;
					break;
				case "eventnow":
					StartEvents();
					break;
				case "lottery":
					if(args.Length > 1)
					{
						if(args[1].ToString() == "players")
						{
							if(lotteryOfPlayers)
							lotteryOfPlayers = false;
							else
							lotteryOfPlayers = true;
							rust.Notice(netuser, string.Format(GetMessage("EventsSystems2"), lotteryOfPlayers));
							Config["Settings: Lottery Of Players"] = lotteryOfPlayers;
							SaveConfig();
							return;
						}
					}
					if(autoLottery)
					{
						autoLottery = false;
						autoMath	= true;
					}
					
					else
					{
						autoLottery = true;
						autoMath	= false;
					}
					Config["Settings: Auto Math"] = autoMath;
					Config["Settings: Auto Lottery"] = autoLottery;
					rust.Notice(netuser, string.Format(GetMessage("EventsSystems3"), autoLottery, autoMath));
					break;
				case "math":
					if(autoMath)
					{
						autoMath	= false;
						autoLottery = true;
					}
					else
					{
						autoMath	= true;
						autoLottery = false;
					}
					Config["Settings: Auto Math"] = autoMath;
					Config["Settings: Auto Lottery"] = autoLottery;
					rust.Notice(netuser, string.Format(GetMessage("EventsSystems4"), autoMath, autoLottery));
					break;
				case "randomevents":
					if(randomEvents)
					randomEvents	= false;
					else
					randomEvents	= true;
					Config["Settings: Random Events"] = randomEvents;
					rust.Notice(netuser, string.Format(GetMessage("EventsSystems5"), randomEvents));
					break;
				default:{
					HelpAdmins(netuser);
					break;
				}
			}
			SaveConfig();
		}


		[ChatCommand("enter")]
		void cmdEnterLottery(NetUser netuser, string command, string[] args)
		{
			var id = netuser.userID.ToString();
			if(!openLottery){rust.SendChatMessage(netuser, tagChat, GetMessage("EnterLottery", id));return;}
			if(playersInLottery.ContainsKey(netuser)){rust.SendChatMessage(netuser, tagChat, GetMessage("EnterLottery1", id));return;}
			if(lotteryOfPlayers)
			{
				playersInLottery.Add(netuser, 0);
				rust.BroadcastChat(tagChat, string.Format(GetMessage("EnterLottery2"), netuser.displayName, playersInLottery.Count));
			}
			else
			{
				if(args.Length == 0){rust.SendChatMessage(netuser, tagChat, GetMessage("EnterLottery3", id));return;}
				var numberChosen =  Convert.ToInt32(args[0]);
				if(numberChosen > maximumNumberLottery){rust.SendChatMessage(netuser, tagChat, string.Format(GetMessage("EnterLottery4", id), maximumNumberLottery));return;}
				playersInLottery.Add(netuser, numberChosen);
				rust.BroadcastChat(tagChat, string.Format(GetMessage("EnterLottery5"), netuser.displayName, numberChosen, playersInLottery.Count));
			}
		}
	}
}