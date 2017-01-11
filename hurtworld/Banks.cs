using Oxide.Core;
using Oxide.Game.Hurtworld.Libraries;
using Oxide.Core.Extensions;

using System.Collections.Generic;
using System;
using System.Linq;
using Steamworks;
using Newtonsoft.Json;
using uLink;

namespace Oxide.Plugins
{
    [Info("Banks", "RD156", "1.0.3")]
    [Description("Bank with tax, Account and Pocket")]

    class Banks : HurtworldPlugin
	{
		General_Banks general_Banks;
		// Base
		void Init() {}
		void Loaded()
		{
			LoadDataBanks();
			LoadMessages();
		}
		void OnServerSave() => SaveDataBanks();
		void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Player not found", "<color=#e00702>Player not found :</color>"},
				{"Have permission Admin", "<color=#FF1000>You don't Have permission :( </color>"},
				{"Help Money Config name", "<color=#FF1000>/money_config name [name of money]</color>"},
				{"Help Money Config transfert", "<color=#FF1000>/money_config transfert [Little Tranferts Tax] [Big Tranferts Tax] [Limit between Little and Big Tax]</color>"},
				{"Help Money Config deposit", "<color=#FF1000>/money_config deposit [Little Tranferts Tax] [Big Tranferts Tax] [Limit between Little and Big Tax]</color>"},
				{"Help Money Config withdrawal", "<color=#FF1000>/money_config withdrawal [Little Tranferts Tax] [Big Tranferts Tax] [Limit between Little and Big Tax]</color>"},
				{"Tax Withdrawal", "<color=#40E047>Tax of withdrawal :</color>"},
				{"Tax Deposit", "Tax of deposit"},
				{"Tax Tranferts", "<color=#40E047>Tax of tranferts :</color>"},
				{"You have", "<color=#40E047>You have : </color>"},
				{"You have not", "<color=#FF1000>You don't have : </color>"},
				{"In Pocket", " in your pocket."},
				{"In Account", " in your Account."},
				{"Pocket", "Pocket"},
				{"Account", "Account"},
				{"More pocket", "<color=#40E047>You have : </color> {Money} {Money_name} more in your pocket."},
				{"Less pocket", "<color=#40E047>You have : </color> {Money} {Money_name} less in your pocket."},
				{"Less not pocket", "<color=#FF1000>You don't have : </color> {Money} {Money_name} in your pocket."},
				{"More current", "<color=#40E047>You have : </color> {Money} {Money_name} more in your Account."},
				{"Less current", "<color=#40E047>You have : </color> {Money} {Money_name} less in your Account."},
				{"Less not current", "<color=#FF1000>You don't have : </color> {Money} {Money_name} in your Account."},
				{"Death lost", "<color=#FF1000>You have death and you have lose {Money} {Money_name} in your pocket. </color>"},
				{"Death win", "<color=#40E047>You have kill {Player} and you have win {Money} {Money_name}.</color>"}
            }, this);
        }
				
		string GetNameOfObject(UnityEngine.GameObject obj){
			var ManagerInstance = GameManager.Instance;
			return ManagerInstance.GetDescriptionKey(obj);
		}
		
		void OnPlayerDeath(PlayerSession session, EntityEffectSourceData source)
		{
			ulong userID_ulong;
			string userID;
			ulong userID_ulong_other;
			string userID_other;
			int old = 0;
			int old_other = 0;
			PlayerSession session_other = null;
			string other_name = "";
			string name = "";
			
			userID_ulong = session.SteamId.m_SteamID;
			userID = userID_ulong.ToString();
			
			if (general_Banks.List_accounts.ContainsKey(userID) == true)
			{
				old = (int) general_Banks.List_accounts[userID].poket;
				general_Banks.List_accounts[userID].poket = 0;
				hurt.SendChatMessage(session, $"{Get_message("Death lost", session.SteamId.ToString()).Replace("{Money}",old.ToString()).Replace("{Money_name}",general_Banks.name_money)}");
				other_name = GetNameOfObject(source.EntitySource);
				name = session.Name;
				if(other_name != "")
				{
					other_name = other_name.Substring(0, other_name.Length - 3);
					session_other = FindSession(other_name);
					if (session_other != null)
					{
						userID_ulong_other = session_other.SteamId.m_SteamID;
						userID_other = userID_ulong_other.ToString();
						old_other = (int) general_Banks.List_accounts[userID_other].poket;
						general_Banks.List_accounts[userID_other].poket= old_other + old;
						hurt.SendChatMessage(session_other, $"{Get_message("Death win", session.SteamId.ToString()).Replace("{Player}",name).Replace("{Money}",old.ToString()).Replace("{Money_name}",general_Banks.name_money)}");
					}
				}
				SaveDataBanks();
			}
		}
		//OBJ
		class Account
		{
			public int current = 0;
			public int poket = 0;
			public Account(){}
		}
		class General_Banks
        {
			public int startMoneyPocket = 0;
			public int startMoneyCurrent = 0;
			
			public int tax_big_tranfert = 10;
			public int tax_little_tranfert = 25;
			public int limit_big_little_transfert = 1000;
			
			public int tax_big_deposit = 10;
			public int tax_little_deposit = 25;
			public int limit_big_little_deposit = 1000;
			
			public int tax_big_withdrawal = 10;
			public int tax_little_withdrawal = 25;
			public int limit_big_little_withdrawal = 1000;
			
			public string name_money = "Dollars";
            public Dictionary<string, Account> List_accounts = new Dictionary<string, Account>();
        }
		//Set -- Get
		string get_name_money(){return (general_Banks.name_money);}
		int get_startMoneyCurrent(){return (general_Banks.startMoneyCurrent);}
		int get_startMoneyPocket(){return (general_Banks.startMoneyPocket);}
		
		// Message
		
		string Get_message(string key, string id)
		{
			return lang.GetMessage(key, this, id);
		}
		
		//Command
		private void money(PlayerSession session_my, string command, string[] args)
		{
			ulong userID_ulong;
			string userID;
			PlayerSession session_other = null;
			
			if (args != null && args.Length >= 1)
			{
				session_other = FindSession(args[0]);
				if (session_other == null)
				{
					
					hurt.SendChatMessage(session_my, $"{Get_message("Player not found", session_my.SteamId.ToString())}");
					return ;
				}
			}
			if (session_other != null)
			{
				userID_ulong = session_other.SteamId.m_SteamID;
				userID = userID_ulong.ToString();
			}
			else
			{
				userID_ulong = session_my.SteamId.m_SteamID;
				userID = userID_ulong.ToString();
			}
			display_money(userID, session_my);
		}
		private void give(PlayerSession session_my, string command, string[] args)
		{
			PlayerSession session_other;
			int money_move = 0;
			int result = -2;
			if (args != null && args.Length >= 2)
			{
				session_other = FindSession(args[0]);
				if (session_other != null)
				{
					try
					{
						money_move = int.Parse(args[1]);
					}
					catch(Exception ex)
					{
						 money_move = 0;
					}
					result = lost_money_poket(money_move, session_my);
					if (result == 1)
						add_money_poket(money_move, session_other);
				}
				else
				{
					hurt.SendChatMessage(session_my, $"{Get_message("Player not found", session_my.SteamId.ToString())}");
				}
			}
			else
			{
				hurt.SendChatMessage(session_my, $"{Get_message("Player not found", session_my.SteamId.ToString())}");
			}
		}
		private void transfert(PlayerSession session_my, string command, string[] args)
		{
			PlayerSession session_other;
			int money_move = 0;
			int result = -2;
			int money_with_tax = 0;
			if (args != null && args.Length >= 2)
			{
				session_other = FindSession(args[0]);
				if (session_other != null)
				{
					try
					{
						money_move = int.Parse(args[1]);
					}
					catch(Exception ex)
					{
						 money_move = 0;
					}
					money_with_tax = calcul_tax_transfert(money_move, session_my);
					result = lost_money_current(money_move, session_my);
					if (result == 1)
						add_money_current(money_with_tax, session_other);
				}
				else
				{
					hurt.SendChatMessage(session_my, $"{Get_message("Player not found", session_my.SteamId.ToString())}");
				}
			}
			else
			{
				hurt.SendChatMessage(session_my, $"{Get_message("Player not found", session_my.SteamId.ToString())}");
			}
		}
		private void money_create(PlayerSession session_my, string command, string[] args)
		{
			int money = 0;
			
			if (session_my.IsAdmin == true)
			{
				if (args != null && args.Length >= 1)
				{
					try
					{
						money = int.Parse(args[0]);
					}
					catch(Exception ex)
					{
						money = 0;
					}
					if (money >= 0)
						add_money_poket(money, session_my);
				}
				SaveDataBanks();
			}
			else
			{
				hurt.SendChatMessage(session_my, $"{Get_message("Have permission Admin", session_my.SteamId.ToString())}");
			}
		}
		private void money_remove(PlayerSession session_my, string command, string[] args)
		{
			int money = 0;
			
			if (session_my.IsAdmin == true)
			{
				if (args != null && args.Length >= 1)
				{
					try
					{
						money = int.Parse(args[0]);
					}
					catch(Exception ex)
					{
						money = 0;
					}
					if (money >= 0)
						lost_money_poket(money, session_my);
				}
				SaveDataBanks();
			}
			else
			{
				hurt.SendChatMessage(session_my, $"{Get_message("Have permission Admin", session_my.SteamId.ToString())}");
			}
		}
		private void withdrawal(PlayerSession session_my, string command, string[] args)
		{
			int money = 0;
			int money_with_tax = 0;
			
			if (args != null && args.Length >= 1)
			{
				try
				{
					money = int.Parse(args[0]);
				}
				catch(Exception ex)
				{
					money = 0;
				}
				money_with_tax = calcul_tax_withdrawal(money, session_my);
				if (lost_money_current(money, session_my) > 0)
				{
					add_money_poket(money_with_tax, session_my);
				}
			}
		}
		private void deposit(PlayerSession session_my, string command, string[] args)
		{
			int money = 0;
			int money_with_tax = 0;
			
			if (args != null && args.Length >= 1)
			{
				try
				{
					money = int.Parse(args[0]);
				}
				catch(Exception ex)
				{
					money = 0;
				}
				money_with_tax = calcul_tax_deposit(money, session_my);
				if (lost_money_poket(money, session_my) > 0)
				{
					add_money_current(money_with_tax, session_my);
				}
			}
		}
		private void money_config(PlayerSession session_my, string command, string[] args)
		{
			int money = 0;
			
			if (session_my.IsAdmin == true)
			{
				if (args != null && args.Length >= 2)
				{
					if ((args[0] == "name" || args[0] == "Name") && args[1] != null)
					{
						general_Banks.name_money = args[1];
					}
					else if (args[0] == "transfert")
						money_config_transfert(args, session_my);
					else if (args[0] == "deposit")
						money_config_deposit(args, session_my);
					else if (args[0] == "withdrawal")
						money_config_withdrawal(args, session_my);
					else
						money_config_help(session_my);
				}
				else
					money_config_help(session_my);
			}
			else
			{
				hurt.SendChatMessage(session_my, $"{Get_message("Have permission Admin", session_my.SteamId.ToString())}");
			}
		}
		//Function
		private void money_config_transfert(string[] args, PlayerSession session)
		{
			int little;
			int big;
			int limit;
			
			if (args.Length > 4)
			{
				try
				{
					little = int.Parse(args[1]);
					big = int.Parse(args[2]);
					limit = int.Parse(args[3]);
				}
				catch(Exception ex)
				{
					little = 25;
					big = 10;
					limit = 1000;
				}
				general_Banks.tax_big_tranfert = big;
				general_Banks.tax_little_tranfert = little;
				general_Banks.limit_big_little_transfert = limit;
				SaveDataBanks();
			}
			else
				money_config_help(session);
		}
		private void money_config_deposit(string[] args, PlayerSession session)
		{
			int little;
			int big;
			int limit;
			
			if (args.Length > 4)
			{
				try
				{
					little = int.Parse(args[1]);
					big = int.Parse(args[2]);
					limit = int.Parse(args[3]);
				}
				catch(Exception ex)
				{
					little = 25;
					big = 10;
					limit = 1000;
				}
				general_Banks.tax_big_deposit = big;
				general_Banks.tax_little_deposit = little;
				general_Banks.limit_big_little_deposit = limit;
				SaveDataBanks();
			}
			else
				money_config_help(session);
		}
		private void money_config_withdrawal(string[] args, PlayerSession session)
		{
			int little;
			int big;
			int limit;
			
			if (args.Length > 4)
			{
				try
				{
					little = int.Parse(args[1]);
					big = int.Parse(args[2]);
					limit = int.Parse(args[3]);
				}
				catch(Exception ex)
				{
					little = 25;
					big = 10;
					limit = 1000;
				}
				general_Banks.tax_big_withdrawal = big;
				general_Banks.tax_little_withdrawal = little;
				general_Banks.limit_big_little_withdrawal = limit;
				SaveDataBanks();
			}
			else
				money_config_help(session);
		}
		private void money_config_help(PlayerSession session)
		{
			hurt.SendChatMessage(session, $"{Get_message("Help Money Config name", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Get_message("Help Money Config transfert", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Get_message("Help Money Config deposit", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Get_message("Help Money Config withdrawal", session.SteamId.ToString())}");
		}
		private int calcul_tax_withdrawal(int money, PlayerSession session)
		{
			if (money <= 0)
				return (0);
			if (general_Banks.tax_big_withdrawal < 0 || general_Banks.tax_big_withdrawal > 100)
				return (0);
			if (general_Banks.tax_little_withdrawal < 0 || general_Banks.tax_little_withdrawal > 100)
				return (0);
			if (general_Banks.limit_big_little_withdrawal < 0)
				return (0);
			if (money < general_Banks.limit_big_little_withdrawal)
			{
				money = money - (money * general_Banks.tax_little_withdrawal / 100);
				money = money - 1;
			}
			else if (money >= general_Banks.limit_big_little_withdrawal)
			{
				money = money - (money * general_Banks.tax_big_withdrawal / 100);
				money = money - 1;
				hurt.SendChatMessage(session, $"{Get_message("Tax Withdrawal", session.SteamId.ToString())} " + general_Banks.tax_big_withdrawal + "%");
			}
			if (money < 0)
				return (0);
			else
				return (money);
			return (money);
		}
		private int calcul_tax_deposit(int money, PlayerSession session)
		{
			if (money <= 0)
				return (0);
			if (general_Banks.tax_big_deposit < 0 || general_Banks.tax_big_deposit > 100)
				return (0);
			if (general_Banks.tax_little_deposit < 0 || general_Banks.tax_little_deposit > 100)
				return (0);
			if (general_Banks.limit_big_little_deposit < 0)
				return (0);
			if (money < general_Banks.limit_big_little_deposit)
			{
				money = money - (money * general_Banks.tax_little_deposit / 100);
				money = money - 1;
				hurt.SendChatMessage(session, $"{Get_message("Tax Withdrawal", session.SteamId.ToString())}: " + general_Banks.tax_little_deposit + "%");
			}
			else if (money >= general_Banks.limit_big_little_deposit)
			{
				money = money - (money * general_Banks.tax_big_deposit / 100);
				money = money - 1;
				hurt.SendChatMessage(session, $"{Get_message("Tax Withdrawal", session.SteamId.ToString())}: " + general_Banks.tax_big_deposit + "%");
			}
			if (money < 0)
				return (0);
			else
				return (money);
		}
		private int calcul_tax_transfert(int money, PlayerSession session)
		{
			if (money <= 0)
				return (0);
			if (general_Banks.tax_big_tranfert < 0 || general_Banks.tax_big_tranfert > 100)
				return (0);
			if (general_Banks.tax_little_tranfert < 0 || general_Banks.tax_little_tranfert > 100)
				return (0);
			if (general_Banks.limit_big_little_transfert < 0)
				return (0);
			if (money < general_Banks.limit_big_little_transfert)
			{
				money = money - (money * general_Banks.tax_little_tranfert / 100);
				money = money - 1;
				hurt.SendChatMessage(session, $"{Get_message("Tax Tranferts", session.SteamId.ToString())} " + general_Banks.tax_little_tranfert + "%");
			}
			else if (money >= general_Banks.limit_big_little_transfert)
			{
				money = money - (money * general_Banks.tax_big_tranfert / 100);
				money = money - 1;
				hurt.SendChatMessage(session, $"{Get_message("Tax Tranferts", session.SteamId.ToString())} " + general_Banks.tax_big_tranfert + "%");
			}
			if (money < 0)
				return (0);
			else
				return (money);
		}
		int add_money_poket(int number, PlayerSession session)
		{
			string userID;
			if (number < 0)
				return (-1);
			userID = session.SteamId.m_SteamID.ToString();
			int cash = 0;
			if (general_Banks.List_accounts.ContainsKey(userID) == true)
			{
				cash = (int) general_Banks.List_accounts[userID].poket;
				
				general_Banks.List_accounts[userID].poket = cash + number;
				hurt.SendChatMessage(session, $"{Get_message("More pocket", session.SteamId.ToString()).Replace("{Money}",number.ToString()).Replace("{Money_name}",general_Banks.name_money)}");
				SaveDataBanks();
				return (1);
				
			}
			else
			{
				return (-1);
			}
		}
		int lost_money_poket(int number, PlayerSession session)
		{
			string userID;
			
			if (number < 0)
				return (-1);
			userID = session.SteamId.m_SteamID.ToString();
			int cash = 0;
			if (general_Banks.List_accounts.ContainsKey(userID) == true)
			{
				cash = (int) general_Banks.List_accounts[userID].poket;
				if (cash >= number)
				{
					general_Banks.List_accounts[userID].poket = cash - number;
					hurt.SendChatMessage(session, $"{Get_message("Less pocket", session.SteamId.ToString()).Replace("{Money}",number.ToString()).Replace("{Money_name}",general_Banks.name_money)}");
					SaveDataBanks();
					return (1);
				}
				else
				{
					hurt.SendChatMessage(session, $"{Get_message("Less not pocket", session.SteamId.ToString()).Replace("{Money}",number.ToString()).Replace("{Money_name}",general_Banks.name_money)}");
					return (0);
				}
			}
			else
			{
				return (-1);
			}
		}
		int add_money_current(int number, PlayerSession session)
		{
			string userID;
			if (number < 0)
				return (-1);
			userID = session.SteamId.m_SteamID.ToString();
			int cash = 0;
			if (general_Banks.List_accounts.ContainsKey(userID) == true)
			{
				cash = (int) general_Banks.List_accounts[userID].current;
				
				general_Banks.List_accounts[userID].current = cash + number;
				hurt.SendChatMessage(session, $"{Get_message("More current", session.SteamId.ToString()).Replace("{Money}",number.ToString()).Replace("{Money_name}",general_Banks.name_money)}");
				SaveDataBanks();
				return (1);
				
			}
			else
			{
				return (-1);
			}
		}
		int lost_money_current(int number, PlayerSession session)
		{
			string userID;
			
			if (number < 0)
				return (-1);
			userID = session.SteamId.m_SteamID.ToString();
			int cash = 0;
			if (general_Banks.List_accounts.ContainsKey(userID) == true)
			{
				cash = (int) general_Banks.List_accounts[userID].current;
				if (cash >= number)
				{
					general_Banks.List_accounts[userID].current = cash - number;
					hurt.SendChatMessage(session, $"{Get_message("Less current", session.SteamId.ToString()).Replace("{Money}",number.ToString()).Replace("{Money_name}",general_Banks.name_money)}");
					SaveDataBanks();
					return (1);
				}
				else
				{
					hurt.SendChatMessage(session, $"{Get_message("Less not current", session.SteamId.ToString()).Replace("{Money}",number.ToString()).Replace("{Money_name}",general_Banks.name_money)}");
					return (0);
				}
			}
			else
			{
				return (-1);
			}
		}
		void display_money(string userID, PlayerSession session)
		{
			int cash = 0;
			int current = 0;
			
			if (general_Banks.List_accounts.ContainsKey(userID) == true)
			{
				cash = general_Banks.List_accounts[userID].poket;
				current = general_Banks.List_accounts[userID].current;
			}
			else
			{
				Account new_compte = new Account();
				new_compte.current = general_Banks.startMoneyCurrent;
				new_compte.poket = general_Banks.startMoneyPocket;
				general_Banks.List_accounts[userID] = new_compte;
				cash = general_Banks.startMoneyPocket;
				current = general_Banks.startMoneyCurrent;
			}
			hurt.SendChatMessage(session, $"<color=#40E047>{Get_message("Account", session.SteamId.ToString())}</color>: " + current.ToString());
			hurt.SendChatMessage(session, $"<color=#40E047>{Get_message("Pocket", session.SteamId.ToString())}</color>: " + cash.ToString());
		}
		private PlayerSession FindSession(string nameOrIdOrIp)
        {
            var sessions = GameManager.Instance.GetSessions();
            PlayerSession session = null;
            foreach (var i in sessions)
            {
                if (nameOrIdOrIp.Equals(i.Value.Name, StringComparison.OrdinalIgnoreCase) ||
                    nameOrIdOrIp.Equals(i.Value.SteamId.ToString()) || nameOrIdOrIp.Equals(i.Key.ipAddress))
                {
                    session = i.Value;
                    break;
                }
            }
            return session;
        }
		
		// Load and Save
		public void SaveDataBanks()
		{
			Interface.Oxide.DataFileSystem.WriteObject("Banks", general_Banks);
		}
		public void LoadDataBanks()
		{
			var get_general_Banks = Interface.Oxide.DataFileSystem.GetFile("Banks");
			try
			{
				general_Banks = get_general_Banks.ReadObject<General_Banks>();
			}
			catch
			{
				general_Banks = new General_Banks();
			}
		}
		//List of Commands
		[ChatCommand("money")]
		void my_money(PlayerSession session, string command, string[] args)
		{
			money(session, command, args);
		}
		[ChatCommand("give")]
		void my_give(PlayerSession session, string command, string[] args)
		{
			give(session, command, args);
		}
		[ChatCommand("money_create")]
		void my_money_create(PlayerSession session, string command, string[] args)
		{
			money_create(session, command, args);
		}
		[ChatCommand("money_remove")]
		void my_money_remove(PlayerSession session, string command, string[] args)
		{
			money_remove(session, command, args);
		}
		[ChatCommand("money_config")]
		void my_money_config(PlayerSession session, string command, string[] args)
		{
			money_config(session, command, args);
		}
		[ChatCommand("withdrawal")]
		void my_withdrawal(PlayerSession session, string command, string[] args)
		{
			withdrawal(session, command, args);
		}
		[ChatCommand("deposit")]
		void my_deposit(PlayerSession session, string command, string[] args)
		{
			deposit(session, command, args);
		}
		[ChatCommand("transfert")]
		void my_transfert(PlayerSession session, string command, string[] args)
		{
			transfert(session, command, args);
		}
	}
}