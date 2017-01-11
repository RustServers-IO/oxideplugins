// Reference: UnityEngine.UI

using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Steamworks;
using uLink;

namespace Oxide.Plugins
{

    [Info("Custom Kick/Ban", "Мизантроп", 0.4)]
    public class CustomKickBan : HurtworldPlugin
    {
		void Init()
		{
			LoadDefaultMessages();
			permission.RegisterPermission("customkickban.ban", this);
			permission.RegisterPermission("customkickban.kick", this);
		}
		
		protected override void LoadDefaultConfig()
        {
            this.Config["prefix"] = (object) "Custom Kick/Ban";
        }
		
		void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"NoAccess", "not access"},
				{"NoName", "write target nickname"},
				{"NoReason", "write reason"},
				{"NoFound", "player not found"},
				{"Multiple", "Multiple matching players found: \n"}
            };
            lang.RegisterMessages(messages, this);
        }
		
		string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
		
		[ChatCommand("kick")]
        void cmdKick(PlayerSession session, string command, string[] args)
        {
			if (!HasAccess(session, "customkickban.kick")){
				hurt.SendChatMessage(session, "<color=#ff8000>"+ this.Config["prefix"].ToString() +": </color> "+GetMessage("NoAccess", session.SteamId.ToString()));
				return;
			}
			if (args.Length == 0)
            {
				hurt.SendChatMessage(session, "<color=#ff8000>"+ this.Config["prefix"].ToString() +": </color> "+GetMessage("NoName", session.SteamId.ToString()));
				return;
			}
			if (args.Length < 2)
			{
				hurt.SendChatMessage(session, "<color=#ff8000>"+ this.Config["prefix"].ToString() +": </color> "+GetMessage("NoReason", session.SteamId.ToString()));
			}
			else 
			{
				PlayerSession player = getPlayerFromName(args[0], session);
				if (player == null)
				{
					
				}
				else 
				{
					GameManager.Instance.KickPlayer(player.SteamId.ToString(), args[1]);
				}
			}
		}
		
		[ChatCommand("ban")]
        void cmdBan(PlayerSession session, string command, string[] args)
        {
			if (!HasAccess(session, "customkickban.ban"))
			{
				hurt.SendChatMessage(session, "<color=#ff8000>"+ this.Config["prefix"].ToString() +": </color> "+GetMessage("NoAccess", session.SteamId.ToString()));
				return;
			}
			if (args.Length == 0)
            {
				hurt.SendChatMessage(session, "<color=#ff8000>"+ this.Config["prefix"].ToString() +": </color> "+GetMessage("NoName", session.SteamId.ToString()));
				return;
			}
			else 
			{
				PlayerSession player = getPlayerFromName(args[0], session);
				if (player == null)
				{
					
				}
				{
					ConsoleManager.Instance?.ExecuteCommand("ban "+player.SteamId.ToString());
				}
			}
		}
		
		PlayerSession getPlayerFromIP(string ip)
        {
            var identityMap = GameManager.Instance.GetSessions();
            var identity = identityMap.FirstOrDefault(x => string.Equals(x.Value.Player.ipAddress, ip)).Value;
            return identity;
        }
		
		PlayerSession getPlayerFromName(string name, PlayerSession player)
        {
            foreach (PlayerSession current in GameManager.Instance.GetSessions().Values)
                if (current != null && current.Name != null && current.IsLoaded && current.Name.ToLower() == name)
                    return current;

            List<PlayerSession> foundPlayers =
                (from current in GameManager.Instance.GetSessions().Values
                 where current != null && current.Name != null && current.IsLoaded && current.Name.ToLower().Contains(name.ToLower())
                 select current).ToList();

            switch (foundPlayers.Count)
            {
                case 0:
                    hurt.SendChatMessage(player, "<color=#ff8000>"+ this.Config["prefix"].ToString() +": </color> "+GetMessage("NoFound", player.SteamId.ToString()));
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    List<string> playerNames = (from current in foundPlayers select current.Name).ToList();
                    string players = ListToString(playerNames, 0, ", ");
					hurt.SendChatMessage(player, "<color=#ff8000>"+ this.Config["prefix"].ToString() +": </color> "+GetMessage("Multiple", player.SteamId.ToString()) + players);
                    break;
            }

            return null;
        }
		
		string ListToString(List<string> list, int first, string seperator)
        {
            return String.Join(seperator, list.Skip(first).ToArray());
        }
		
		bool HasAccess(PlayerSession session, string perm)
        {
            if (session.IsAdmin) return true;
            return permission.UserHasPermission(session.SteamId.ToString(), perm);
        }
	}
}