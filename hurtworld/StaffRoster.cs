//Reference: UnityEngine.UI
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("StaffRoster", "Noviets", "1.0.4", ResourceId = 2048)]
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
				{"invalidstatus","StaffRoster: Invalid Status. (<color=green>available</color>, <color=yellow>afk</color>, <color=orange>busy</color>, <color=red>offduty</color>)"},
				{"afk","<color=yellow>AFK</color>"},
				{"available","<color=green>Available</color>"},
				{"busy","<color=orange>Busy</color>"},
				{"offduty","<color=red>Off Duty</color>"}
            };
			
			lang.RegisterMessages(messages, this);
        }
		Dictionary<string, int> StaffList = new Dictionary<string, int>();
		void Loaded() => LoadDefaultMessages();
		string Msg(string msg, string SteamId = null) => lang.GetMessage(msg, this, SteamId);

		void OnPlayerDisconnected(PlayerSession session)
		{
			if(StaffList.ContainsKey(Msg("admin",session.SteamId.ToString()).Replace("{Admin}",session.Name)))
				StaffList.Remove(Msg("admin",session.SteamId.ToString()).Replace("{Admin}",session.Name));

			if(StaffList.ContainsKey(Msg("mod",session.SteamId.ToString()).Replace("{Mod}",session.Name)))
				StaffList.Remove(Msg("mod",session.SteamId.ToString()).Replace("{Mod}",session.Name));
		}
		
		[ChatCommand("staff")]
        void staffCommand(PlayerSession session, string command, string[] args)
        {
			if(args.Length == 0)
			{
				foreach (PlayerSession player in GameManager.Instance.GetSessions().Values)
				{
					if(player.IsAdmin || permission.UserHasGroup(player.SteamId.ToString(),"Admin"))
					{
						if(!StaffList.ContainsKey(Msg("admin",player.SteamId.ToString()).Replace("{Admin}",player.Name)))
							StaffList.Add(Msg("admin",player.SteamId.ToString()).Replace("{Admin}",player.Name), 1);
					}
					if(permission.UserHasGroup(player.SteamId.ToString(),"Moderator"))
					{
						if(!StaffList.ContainsKey(Msg("mod",player.SteamId.ToString()).Replace("{Mod}",player.Name)))
							StaffList.Add(Msg("mod",player.SteamId.ToString()).Replace("{Mod}",player.Name), 1);
					}
				}
				hurt.SendChatMessage(session, Msg("stafflist",session.SteamId.ToString()));
				foreach(var staffmember in StaffList)
				{
					string status = "";
					switch(staffmember.Value)
					{
						case 1:
							status = Msg("available",session.SteamId.ToString());
							break;
						case 2:
							status = Msg("afk",session.SteamId.ToString());
							break;
						case 3:
							status = Msg("busy",session.SteamId.ToString());
							break;
						case 4:
							status = Msg("offduty",session.SteamId.ToString());
							break;
					}
					hurt.SendChatMessage(session, staffmember.Key.ToString()+" Status: "+status);
				}
			}
			if(args.Length == 1)
			{
				if(session.IsAdmin || permission.UserHasGroup(session.SteamId.ToString(),"Admin") || permission.UserHasGroup(session.SteamId.ToString(),"Moderator"))
				{
					int intstatus = 0;
					switch(args[0].ToLower())
					{
						case "available":
							intstatus = 1;
							break;
						case "afk":
							intstatus = 2;
							break;
						case "busy":
							intstatus = 3;
							break;
						case "offduty":
							intstatus = 4;
							break;
						default:
							hurt.SendChatMessage(session, Msg("invalidstatus",session.SteamId.ToString()));
							return;
					}

					if(session.IsAdmin || permission.UserHasGroup(session.SteamId.ToString(),"Admin"))
					{
						if(StaffList.ContainsKey(Msg("admin",session.SteamId.ToString()).Replace("{Admin}",session.Name)))
							StaffList[Msg("admin",session.SteamId.ToString()).Replace("{Admin}",session.Name)] = intstatus;
					}
					if(permission.UserHasGroup(session.SteamId.ToString(),"Moderator"))
					{
						if(StaffList.ContainsKey(Msg("mod",session.SteamId.ToString()).Replace("{Mod}",session.Name)))
							StaffList[Msg("mod",session.SteamId.ToString()).Replace("{Mod}",session.Name)] = intstatus;
					}
					hurt.SendChatMessage(session, Msg("statuschange",session.SteamId.ToString()).Replace("{Status}", args[0]));
				}
				else
					hurt.SendChatMessage(session, Msg("nopermission",session.SteamId.ToString()));
			}
		}
	}
}