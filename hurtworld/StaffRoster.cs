//Reference: UnityEngine.UI
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("StaffRoster", "Noviets", "1.0.5", ResourceId = 2048)]
    [Description("Shows staff roster and availability")]

    class StaffRoster : HurtworldPlugin
    {		

		void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
				{"stafflist","Current Staff online are:"},
				{"nopermission","StaffRoster: Only staff members are able to use this command."},
				{"mod","<color=blue>{Mod}</color>"},
				{"admin","<color=red>{Admin}</color>"},
				{"statuschange","You have changed your Status to: {Status}"},
				{"playerchangedstatus","{Player} has changed their Status to: {Status}"},
				{"invalidstatusmsg","StaffRoster: Invalid Status. ({available}, {afk}, {busy}, {offduty})"},
				{"afk","<color=yellow>AFK</color>"},
				{"available","<color=green>Available</color>"},
				{"busy","<color=orange>Busy</color>"},
				{"offduty","<color=red>Off Duty</color>"},
				{"afkcmd","afk"},
				{"availablecmd","available"},
				{"busycmd","busy"},
				{"offdutycmd","offduty"}
            };
			
			lang.RegisterMessages(messages, this);
        }
		Dictionary<string, string> StaffList = new Dictionary<string, string>();
		string Msg(string msg, string SteamId = null) => lang.GetMessage(msg, this, SteamId);

		void Loaded() => UpdateList();
		void OnPlayerDisconnected(PlayerSession session)
		{
			if(StaffList.ContainsKey(Msg("admin",session.SteamId.ToString()).Replace("{Admin}",session.Name)))
				StaffList.Remove(Msg("admin",session.SteamId.ToString()).Replace("{Admin}",session.Name));

			else if(StaffList.ContainsKey(Msg("mod",session.SteamId.ToString()).Replace("{Mod}",session.Name)))
				StaffList.Remove(Msg("mod",session.SteamId.ToString()).Replace("{Mod}",session.Name));
		}
		
		void OnPlayerConnected(PlayerSession session)
		{
			if(session.IsAdmin || permission.UserHasGroup(session.SteamId.ToString(),"Admin"))
			{
				if(!StaffList.ContainsKey(Msg("admin",session.SteamId.ToString()).Replace("{Admin}",session.Name)))
					StaffList.Add(Msg("admin",session.SteamId.ToString()).Replace("{Admin}",session.Name), Msg("available",session.SteamId.ToString()));
			}
			else if(permission.UserHasGroup(session.SteamId.ToString(),"Moderator"))
			{
				if(!StaffList.ContainsKey(Msg("mod",session.SteamId.ToString()).Replace("{Mod}",session.Name)))
					StaffList.Add(Msg("mod",session.SteamId.ToString()).Replace("{Mod}",session.Name), Msg("available",session.SteamId.ToString()));
			}
		}
		
		void UpdateList()
		{
			foreach (PlayerSession player in GameManager.Instance.GetSessions().Values)
			{
				if(player.IsAdmin || permission.UserHasGroup(player.SteamId.ToString(),"Admin"))
				{
					if(!StaffList.ContainsKey(Msg("admin",player.SteamId.ToString()).Replace("{Admin}",player.Name)))
						StaffList.Add(Msg("admin",player.SteamId.ToString()).Replace("{Admin}",player.Name), Msg("available",player.SteamId.ToString()));
				}
				else if(permission.UserHasGroup(player.SteamId.ToString(),"Moderator"))
				{
					if(!StaffList.ContainsKey(Msg("mod",player.SteamId.ToString()).Replace("{Mod}",player.Name)))
						StaffList.Add(Msg("mod",player.SteamId.ToString()).Replace("{Mod}",player.Name), Msg("available",player.SteamId.ToString()));
				}
			}
		}
		
		[ChatCommand("staff")]
        void staffCommand(PlayerSession session, string command, string[] args)
        {
			if(args.Length == 0)
			{
				UpdateList();
				hurt.SendChatMessage(session, Msg("stafflist",session.SteamId.ToString()));
				foreach(var staffmember in StaffList)
				{
					hurt.SendChatMessage(session, staffmember.Key.ToString()+" Status: "+staffmember.Value);
				}
			}
			if(args.Length == 1)
			{
				if(session.IsAdmin || permission.UserHasGroup(session.SteamId.ToString(),"Admin") || permission.UserHasGroup(session.SteamId.ToString(),"Moderator"))
				{
					if(args[0].ToLower() == Msg("afkcmd") || args[0].ToLower() == Msg("busycmd") ||args[0].ToLower() == Msg("availablecmd") ||args[0].ToLower() == Msg("offdutycmd"))
					{

						if(session.IsAdmin || permission.UserHasGroup(session.SteamId.ToString(),"Admin"))
						{
							if(StaffList.ContainsKey(Msg("admin",session.SteamId.ToString()).Replace("{Admin}",session.Name)))
							{
								StaffList[Msg("admin",session.SteamId.ToString()).Replace("{Admin}",session.Name)] = GetMsgForStatus(args[0].ToLower());
								hurt.BroadcastChat(Msg("playerchangedstatus", session.SteamId.ToString()).Replace("{Player}", Msg("admin",session.SteamId.ToString()).Replace("{Admin}",session.Name)).Replace("{Status}", GetMsgForStatus(args[0].ToLower())));
							}
						}
						if(permission.UserHasGroup(session.SteamId.ToString(),"Moderator"))
						{
							if(StaffList.ContainsKey(Msg("mod",session.SteamId.ToString()).Replace("{Mod}",session.Name)))
							{
								StaffList[Msg("mod",session.SteamId.ToString()).Replace("{Mod}",session.Name)] = GetMsgForStatus(args[0].ToLower());
								hurt.BroadcastChat(Msg("playerchangedstatus", session.SteamId.ToString()).Replace("{Player}", Msg("mod",session.SteamId.ToString()).Replace("{Mod}",session.Name)).Replace("{Status}", GetMsgForStatus(args[0].ToLower())));
							}
						}
					}
					else
				hurt.SendChatMessage(session, Msg("invalidstatusmsg",session.SteamId.ToString()).Replace("{available}", Msg("availablecmd")).Replace("{afk}", Msg("afkcmd")).Replace("{busy}", Msg("busycmd")).Replace("{offduty}", Msg("offdutycmd")));
				}
				else
					hurt.SendChatMessage(session, Msg("nopermission",session.SteamId.ToString()));
			}
		}
		string GetMsgForStatus(string status)
		{
			if(status == "afk")
				return Msg("afk");
			else if(status == "available")
				return Msg("available");
			else if(status == "busy")
				return Msg("busy");
			else if(status == "offduty")
				return Msg("offduty");
			else return "Unknown";
		}
	}
}