using System;
using System.Collections;
using System.Linq;

using Oxide.Core;

using CodeHatch.Engine.Networking;
using CodeHatch.Common;

using CodeHatch.Engine.Core.Networking;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Entities.Players;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Permissions;

namespace Oxide.Plugins
{
    [Info("Mute", "DumbleDora", "0.1")]
    public class Mute : ReignOfKingsPlugin {
        // MuteAndOoc Reign of Kings mod by DumbleDora
        // this plugin allows players with admin permissions to /mute and /unmute people
		// check who is muted with /muted

        public ArrayList mutedPlayers = new ArrayList();

        #region Hooks

        void Loaded()
        {
            LoadMutedPlayers();
        }

        void Unload() => SaveData();

        void OnServerSave() => SaveData();

        void OnServerShutdown() => SaveData();

		private void OnPlayerChat(PlayerEvent e)
        {
			if (e is PlayerChatEvent)
            {
				var chat = (PlayerChatEvent)e;
				if (mutedPlayers.Contains(chat.Player.Id.ToString())){
					PrintToChat(chat.Player, "You are muted.");
					e.Cancel("Player is Muted");
				}
			}
		}

        #endregion

        #region Data

        void SaveData() 
        {
            Puts("SAVED DATA");
            Interface.GetMod().DataFileSystem.WriteObject("MutedPlayers", mutedPlayers);
        }
		
        void LoadMutedPlayers()
        {
            mutedPlayers = Interface.GetMod().DataFileSystem.ReadObject<ArrayList>("MutedPlayers");
        }

        #endregion

        #region chat commands

		[ChatCommand("mute")]
		private void MutePlayer(Player player, string cmd, string[]args){
			
			if (!player.HasPermission("admin")) {
				PrintToChat(player, "Only admins may mute");
				return;
			}
			 
			if( args.Length < 1 ){
				PrintToChat(player, "Usage: /mute username");				
				return ;
			}
			
			Player toMute = Server.GetPlayerByName( args[0] );
			
			if (toMute == null){
				PrintToChat(player, "Player not found - are they online?");
				return;
			}
			
			if (mutedPlayers.Contains(toMute.Id.ToString())) {
				string already = toMute.DisplayName + " is already muted.";						
				PrintToChat(player, already);
				return;
			}
			
			mutedPlayers.Add(toMute.Id.ToString());
			string muted = toMute.DisplayName + "[FFFFFF] has been muted.";
			PrintToChat(muted);			
		}
		
		[ChatCommand("unmute")]
        private void UnmutePlayer(Player player, string cmd, string[] args)
        {
			if (!player.HasPermission("admin")) {
				PrintToChat(player, "Only admins may unmute");
				return;
			}
			
			if( args.Length < 1 ){
				PrintToChat(player, "Usage: /unmute username");				
				return ;
			}
			
			Player toUnMute = Server.GetPlayerByName( args[0] );
			if (toUnMute == null){
				PrintToChat(player, "Player not found - are they online?");
				return;
			}
			
			if (!mutedPlayers.Contains(toUnMute.Id.ToString())) {
				string not = toUnMute.DisplayName + " was not muted.";						
				PrintToChat(player, not);
				return;
			}
			
			string toRemove = toUnMute.Id.ToString();
			mutedPlayers.Remove(toRemove);
			string unmuted = toUnMute.DisplayName + "[FFFFFF] has been unmuted.";
			PrintToChat(unmuted);		
		}
		
		[ChatCommand("muted")]
        private void ListMuted(Player player, string cmd, string[] args)
        {
			var strings = mutedPlayers.Cast<string>().ToArray();
			var result = string.Join(", ", strings);			
			PrintToChat(player, "Muted players: " + result);
		}

        #endregion
    }
}
