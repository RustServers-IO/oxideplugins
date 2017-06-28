using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("DaySeven", "Mordeus", 1.1)]
	[Description("Tells a player when the 7th day is")]
    class DaySeven : SevenDaysPlugin
    {
		#region Localizations
		void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"DaySeven", "It is day 7!"},
				{"Message", "Next Horde is in {0} days, {1} hours, {2} minutes, prepare!"},
				{"ErrorMessage", "Error: the day is unknown"},
				
            }, this);
        }
		#endregion
		#region Configuration
		protected override void LoadDefaultConfig()
        {
			if ((Config["Version"] == null) || (Config.Get<string>("Version") != "1.1"))
            {
			PrintWarning("Creating a new configuration file.");
			Config["Version"] = "1.1";
            Config["ChatName"] = "[Server]";
			Config["ChatColor"] = "[00ba67] {0} [FFFFFF]";
			Config["ChatColor5days"] = "[fafc57] {0} [FFFFFF] ";
			Config["ChatColor3days"] = "[ff0000] {0} [FFFFFF]";
			SaveConfig();
            }
        }
		#endregion
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
		#region Oxide Hooks
		void Loaded()
		{
		LoadDefaultConfig();
        LoadDefaultMessages();
		}
		#endregion
		#region Command
		void OnPlayerChat(ClientInfo _cInfo, string message)
		{
			int currentDay = GameUtils.WorldTimeToDays(GameManager.Instance.World.worldTime);
            int currentHour = GameUtils.WorldTimeToHours(GameManager.Instance.World.worldTime);
            int currentMinute = GameUtils.WorldTimeToMinutes(GameManager.Instance.World.worldTime);
			int dayLength = GameStats.GetInt(EnumGameStats.DayLightLength);
			string ServerName = Convert.ToString(Config.Get("ChatName"));
			string color = Convert.ToString(Config.Get("ChatColor"));
			
			if ( message.StartsWith("/") )
			{
				message = message.Replace("/", "");
				string msg = message.ToLower();
				if ( message == "day7" )
				{
				     // determine if we are within the horde period for day 7
                    Boolean IsInDay7 = false;
                    if (currentDay >= 7)
                    {
                        if (currentDay % 7 == 0 && currentHour >= 22)
                        {
                            IsInDay7 = true;
                        }
                        // day 8 before 4 AM assuming default day length of 18, time will change otherwise
                        else if (currentDay % 8 == 0 && currentHour < 24 - dayLength - 2)
                        {
                            IsInDay7 = true;
                        }
                    }

                    // not in day 7 horde period
                    if (!IsInDay7)
                    {
                        // find the next day 7
                        int daysUntilHorde = 0;

                        if (currentDay % 7 != 0)
                        {							
							daysUntilHorde = 7 - (currentDay % 7);									
                        }

                        // when is the next horde?
                        ulong nextHordeTime = GameUtils.DayTimeToWorldTime(currentDay + daysUntilHorde, 22, 0);
                        ulong timeUntilHorde = nextHordeTime - GameManager.Instance.World.worldTime;
                        int hoursUntilHorde = GameUtils.WorldTimeToHours(timeUntilHorde);
                        int minutesUntilHorde = GameUtils.WorldTimeToMinutes(timeUntilHorde);
						// Chat color green more than 5
                        if ( daysUntilHorde < 3)
                        {
                            // Chat color red if less than 3 days
                            color = Convert.ToString(Config.Get("ChatColor3days"));
                        }
                        else if ( daysUntilHorde < 5 )
                        {
                            // Chat color yellow if less that 5
                             color = Convert.ToString(Config.Get("ChatColor5days"));
                        }
						if (currentDay % 7 != 0)
                        {
							if (currentHour >= 22 && currentHour <=23 && currentDay!= 0)
							{	
                            daysUntilHorde = (daysUntilHorde -1);
							}							
                        }
							
						string response = string.Format(GetMessage("Message", _cInfo.playerId), daysUntilHorde, hoursUntilHorde, minutesUntilHorde);
						string error = string.Format(GetMessage("Error", _cInfo.playerId));
						if (_cInfo != null)
                        { 
					        NextFrame(() =>
							{
							 _cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(color, response), ServerName, false, "", false));
							});	                         							
                        }
                        else
                        {
                         _cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(color, error),ServerName, false, "", false));
                        }
                    }
                    else
                    {
                        color = Convert.ToString(Config.Get("ChatColor3days"));
						string response = string.Format(GetMessage("DaySeven", _cInfo.playerId));
						string error = string.Format(GetMessage("Error", _cInfo.playerId));
						if (_cInfo != null)
                        {
							NextFrame(() =>
							{
							 _cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(color, response),ServerName, false, "", false));	
                            });	                         							
                        }
                        else
                        {
                         _cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format(color, error),ServerName, false, "", false));
                        }
                    }
                     
                  
				
				}
			}
		}
		#endregion
    }
}