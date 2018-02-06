using Oxide.Core;
using Oxide.Core.Plugins;
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
    [Info("MoneyTime", "RD156", "1.0.0")]
    [Description("Give money to players for time.")]

    class MoneyTime : HurtworldPlugin
	{
		Plugin  Banks;
		int		fail = 0;
		MoneyTimeConfig moneyTimeConfig;
		
		// function of base	
		void Init() {}
		void Loaded()
		{
			Banks = (Plugin)plugins.Find("Banks");
			if (Banks != null)
                fail = 1;
			else
				fail = 0;
			LoadMessages();
			LoadTimeConfig();
			if (moneyTimeConfig.time > 0)
			{
				timer.Repeat(moneyTimeConfig.time, 0, () =>
				{
					add_money();
				});
			}
		}
		void OnServerSave() => SaveTimeConfig();
		
		class MoneyTimeConfig
		{
			public int time = 300;
			public int money = 10;
		}
		void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				{"No Permission", "<color=#FF1000>You don't Have permission </color> :("},
				{"Recieved Money", "You have recieved {money} {money_name} for playing {time} seconde on the server."},
                {"Fail Load Plugin Banks", "<color=#ff0000> Failled to load Plugin : Banks </color>"},
				{"MoneyTime Help Time", "<color=#ff0000> 1) </color> /MoneyTime money [money]"},
				{"MoneyTime Help Money", "<color=#ff0000> 2) </color> /MoneyTime time [time in seconde]"},
				{"MoneyTime Help Time/Money", "<color=#ff0000> 3) </color> /MoneyTime money [money] [time in seconde]"}
            }, this);
        }		
		
		//Display_message
		string Display_message(string key, string id)
		{
			return lang.GetMessage(key, this, id);
		}

		//Command
		private void money_time(PlayerSession session_my, string command, string[] args)
		{
			if (session_my.IsAdmin != true)
			{
				hurt.SendChatMessage(session_my, $"{Display_message("No Permission", session_my.SteamId.ToString())}");
			}
			else
			{
				if (args != null && args.Length >= 2)
				{
					if (args[0] == "money")
						money_time_money(args[1]);
					else if (args[0] == "time")
						money_time_time(args[1]);
					else
						money_time_money_time(args[0], args[1]);
				}
				else
					money_time_help(session_my);
			}
		}
		
		//function
		private void add_money()
		{
			string name_money;
			
			name_money = Banks.Call<string>("get_name_money");
			if (moneyTimeConfig.money > 0)
			{
				var sessions = GameManager.Instance.GetSessions();
				PlayerSession session = null;
				foreach (var i in sessions)
				{
					session = i.Value;
					Banks.Call("add_money_poket", moneyTimeConfig.money, session);
					hurt.SendChatMessage(session, $"{Display_message("Recieved Money", session.SteamId.ToString()).Replace("{money}",moneyTimeConfig.money.ToString()).Replace("{money_name}",name_money).Replace("{time}",moneyTimeConfig.time.ToString())}");
				}
			}
		}
		private void money_time_money(string m_money)
		{
			int money;
			try
			{
				money = int.Parse(m_money);
			}
			catch(Exception ex)
			{
				money = 0;
			}
			moneyTimeConfig.money = money;
			SaveTimeConfig();
			LoadTimeConfig();
		}
		private void money_time_time(string m_time)
		{
			int time;
			try
			{
				time = int.Parse(m_time);
			}
			catch(Exception ex)
			{
				time = 0;
			}
			moneyTimeConfig.time = time;
			SaveTimeConfig();
			LoadTimeConfig();
		}
		private void money_time_money_time(string m_money, string m_time)
		{
			int money;
			int time;
			try
			{
				time = int.Parse(m_time);
			}
			catch(Exception ex)
			{
				time = 0;
			}
			try
			{
				money = int.Parse(m_money);
			}
			catch(Exception ex)
			{
				money = 0;
			}
			moneyTimeConfig.money = money;
			moneyTimeConfig.time = time;
			SaveTimeConfig();
			LoadTimeConfig();
		}
		private void money_time_help(PlayerSession session)
		{
			hurt.SendChatMessage(session, $"{Display_message("MoneyTime Help Time", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("MoneyTime Help Money", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("MoneyTime Help Time/Money", session.SteamId.ToString())}");
		}
		public void SaveTimeConfig()
		{
			Interface.Oxide.DataFileSystem.WriteObject("MoneyTime", moneyTimeConfig);
		}
		public void LoadTimeConfig()
		{
			var get_moneyTimeConfig = Interface.Oxide.DataFileSystem.GetFile("MoneyTime");
			try
			{
				moneyTimeConfig = get_moneyTimeConfig.ReadObject<MoneyTimeConfig>();
			}
			catch
			{
				moneyTimeConfig = new MoneyTimeConfig();
			}
		}
		[ChatCommand("MoneyTime")]
		void my_money_time(PlayerSession session, string command, string[] args)
		{
			if (fail == 0)
				hurt.SendChatMessage(session, $"{Display_message("Fail Load Plugin Banks", session.SteamId.ToString())}");
			else
				money_time(session, command, args);
		}
		[ChatCommand("MoneyTimeNow")]
		void my_money_time_now(PlayerSession session, string command, string[] args)
		{
			if (fail == 0)
				hurt.SendChatMessage(session, $"{Display_message("Fail Load Plugin Banks", session.SteamId.ToString())}");
			else
			{
				if (session.IsAdmin != true)
				{
					hurt.SendChatMessage(session, $"{Display_message("No Permission", session.SteamId.ToString())}");
				}
				else
					add_money();
			}
		}
	}
}