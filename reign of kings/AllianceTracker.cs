using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.ObjectModel;
using System.Timers;
using CodeHatch.Engine.Networking;
using CodeHatch.Engine.Core.Networking;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Common;
using CodeHatch.Permissions;
using Oxide.Core;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Entities.Players;
using CodeHatch.Networking.Events.Players;


namespace Oxide.Plugins
{
    [Info("AllianceTracker", "Scorpyon", "1.0.3")]
    public class AllianceTracker : ReignOfKingsPlugin
    {
        private Dictionary<string, Collection<string>> allianceList = new Dictionary<string, Collection<string>>();
        private Collection<Collection<string>> requestList = new Collection<Collection<string>>();


        // SAVE DATA ===============================================================================================

        private void LoadAllianceData()
        {
            allianceList = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, Collection<string>>>("SavedAllianceList");
            requestList = Interface.GetMod().DataFileSystem.ReadObject<Collection<Collection<string>>>("SavedAllianceRequestList");
        }

        private void SaveAllianceListData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("SavedAllianceList", allianceList);
            Interface.GetMod().DataFileSystem.WriteObject("SavedAllianceRequestList", requestList);
        }

        void Loaded()
        {
            LoadAllianceData();
        }


        // ===========================================================================================================
        //[ChatCommand("testally")]
        //private void CreateTestAllyList(Player player, string cmd)
        //{
        //    PrintToChat("Adding Test Data");

        //    var requestDetails = new Collection<string>();
        //    requestDetails.Add("Guild A");
        //    requestDetails.Add("Scorpyon's Guild");
        //    requestList.Add(requestDetails);

        //    requestDetails = new Collection<string>();
        //    requestDetails.Add("Guild B");
        //    requestDetails.Add("Scorpyon's Guild");
        //    requestList.Add(requestDetails);

        //    requestDetails = new Collection<string>();
        //    requestDetails.Add("Scorpyon's Guild");
        //    requestDetails.Add("Guild A");
        //    requestList.Add(requestDetails);

        //    requestDetails = new Collection<string>();
        //    requestDetails.Add("Scorpyon's Guild");
        //    requestDetails.Add("Guild B");
        //    requestList.Add(requestDetails);

        //    requestDetails = new Collection<string>();
        //    requestDetails.Add("Scorpyon's Guild");
        //    requestDetails.Add("Guild C");
        //    requestList.Add(requestDetails);
        //}

        // LIST ALLIANCE COMMANDS
        [ChatCommand("alliancecommands")]
        private void ListAllAllianceCommands(Player player, string cmd)
        {
            PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : Use the following commands for Alliances :");
            PrintToChat(player, "[00FF00]/requestally [FF00FF]<player_name> [FFFFFF] - Request to start an alliance");
            PrintToChat(player, "[00FF00]/viewallies [FF00FF]<guild_name> [FFFFFF] - View all allies for this guild");
            PrintToChat(player, "[00FF00]/viewallyrequests [FFFFFF] - View all ally requests for your guild");
            PrintToChat(player, "[00FF00]/acceptally [FF00FF]<guild_name> [FFFFFF] - Accept an ally request from this guild");
            PrintToChat(player, "[00FF00]/denyally [FF00FF]<guild_name> [FFFFFF] - Deny an ally request from this guild");
            PrintToChat(player, "[00FF00]/endalliance [FF00FF]<guild_name> [FFFFFF] - End a current Alliance with this guild");
        }

        // End an Alliance
        [ChatCommand("endalliance")]
        private void EndThisAlliance(Player player, string cmd, string[] guildArray)
        {
            // Check the player has specified an alliance to end
            if (PlayerHasNotenteredAGuildName(guildArray))
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : To end an alliance, type [00FF00]/endalliance [FF00FF]<guildname>.");
                return;
            }

            // Get the guild name
            var guildName = ConvertGuildNameToString(guildArray);

            // Get my own guild name
            var myGuild = PlayerExtensions.GetGuild(player).Name;

            // Remove unneccessary [0] at start of string
            myGuild = myGuild.Replace("[0]", "");
            guildName = guildName.Replace("[0]", "");

            // Check the alliance exists
            if(!AllianceAlreadyExists(myGuild, guildName))
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : You are not currently allied with this guild.");
                return;
            }

            // Report that the alliance is ending
            PrintToChat("[FF0000]Alliance Organiser [FF00FF](ALLIANCE ENDED!) [FFFFFF]: [00FF00]" + player.DisplayName + " [FFFFFF]from [00FF00]" + Capitalise(myGuild) + " [FFFFFF]has ended the Alliance with [00FF00]" + Capitalise(guildName) + "[FFFFFF]! They are no longer friends!");

            // End the alliance
            RemoveAlliance(myGuild.ToLower(), guildName.ToLower());

            // Save Data
            SaveAllianceListData();
        }

        // View all the allies of a guild
        [ChatCommand("viewallies")]
        private void ViewAllAllies(Player player, string cmd, string[] guildArray)
        {
            // Get the Guild name to check alliances for
            var guildName = "";
            if (PlayerHasNotenteredAGuildName(guildArray)) guildName = PlayerExtensions.GetGuild(player).Name.ToLower();
            else
            {
                guildName = ConvertGuildNameToString(guildArray);
            }

            // Remove unneccessary [0] at start of string
            guildName = guildName.Replace("[0]", "");

            var myAlliances = GetGuildList(guildName);
            if (myAlliances == null || myAlliances.Count < 1)
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : [00FF00]" + Capitalise(guildName) + "[FFFFFF] is not currently in an alliance with another guild.");
                return;
            }

            PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : [FF00FF]" + Capitalise(guildName) + "[FFFFFF] is currently allied with the following guilds :");
            foreach (var alliance in myAlliances)
            {
                PrintToChat(player, "[00FF00]" + alliance);
            }
        }

        // Accept an Ally Request
        [ChatCommand("acceptally")]
        private void AcceptAllyRequest(Player player, string cmd, string[] guildArray)
        {
            // Check the player has specified a guild to accept
            if (PlayerHasNotenteredAGuildName(guildArray))
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : To accept an alliance request, type [00FF00]/acceptally [FF00FF]<guildname>.");
                return;
            }

            // Convert guild array to string
            var guildName = ConvertGuildNameToString(guildArray);

            // Get my own guild name
            var myGuild = PlayerExtensions.GetGuild(player).Name;

            // Remove unneccessary [0] at start of string
            myGuild = myGuild.Replace("[0]", "");
            guildName = guildName.Replace("[0]", "");

            // Check if this invitation is in the request list
            var allianceRequestNumber = AllianceRequestAlreadyExists(myGuild, guildName);
            if (allianceRequestNumber == -1)
            {
                PrintToChat(player,"[FF0000]Alliance Organiser[FFFFFF] : There is no Alliance request for that guild to accept, my Lord.");
                return;
            }

            // Check if already in an alliance with this guild
            var allianceExists = AllianceAlreadyExists(myGuild, guildName);
            if (allianceExists)
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : You are already allied with that guild, my Lord.");
                return;
            }

            // Broadcast an accept message
            PrintToChat("[FF0000]Alliance Organiser [FF00FF](NEW ALLIANCE) [FFFFFF]: [00FF00]" + Capitalise(myGuild) + " [FFFFFF]has formed a new Alliance with [00FF00]" + Capitalise(guildName) + "[FFFFFF]! May this friendship last through the ages!");

            // Remove request from Request List
            requestList.RemoveAt(allianceRequestNumber);

            // Add new Alliance to the Alliance List
            AddGuildsToTheAllianceList(myGuild, guildName);

            // Save Data
            SaveAllianceListData();
        }

        // Deny an Ally request
        [ChatCommand("denyally")]
        private void DenyAllyRequest(Player player, string cmd, string[] guildArray)
        {
            // Check the player has specified a guild to accept
            if (PlayerHasNotenteredAGuildName(guildArray))
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : To deny an alliance request, type [00FF00]/denyally [FF00FF]<guildname>.");
                return;
            }

            // Get the full guild name
            var guildName = ConvertGuildNameToString(guildArray);

            // Get my guild name
            var myGuild = PlayerExtensions.GetGuild(player).Name;

            // Remove unneccessary [0] at start of string
            myGuild = myGuild.Replace("[0]", "");
            guildName = guildName.Replace("[0]", "");

            // Check if the request is in there
            var allianceRequestNumber = AllianceRequestAlreadyExists(myGuild, guildName);
            if(allianceRequestNumber >= 0)
            { 
                PrintToChat("[FF0000]Alliance Organiser[FFFFFF] : The alliance request with [00FF00]" + Capitalise(guildName) + "[FFFFFF] has been denied.");
                requestList.RemoveAt(allianceRequestNumber);
                return;
            }
            PrintToChat(player,"[FF0000]Alliance Organiser[FFFFFF] : There is no outstanding request with this guild to deny.");

            // Save Data
            SaveAllianceListData();
        }

        // View requests for your guild
        [ChatCommand("viewallyrequests")]
        private void ViewAllyRequestsForMyGuild(Player player, string cmd)
        {
            // Get players guild
            var myGuild = PlayerExtensions.GetGuild(player).Name;
            if (myGuild == null)
            {
                PrintToChat(player, "You don't appear to be in a guild!");
                return;
            }
            
            // Check for Requests this player has made
            var myList = new Collection<string>();
            foreach(var request in requestList)
            {
                if(request[0] == myGuild)
                {
                    myList.Add(request[1]);
                }
            }
            if (myList.Count > 0)
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : You have requested an alliance with the following guilds :");
                foreach(var guild in myList)
                {
                    PrintToChat(player, "[00FF00]" + guild);
                }
            }
            else PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : You have not requested any alliances currently.");

            // Check for requests that have been sent to this player
            myList = new Collection<string>();
            foreach (var request in requestList)
            {
                if (request[1] == myGuild)
                {
                    myList.Add(request[0]);
                }
            }
            if (myList.Count > 0)
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : The following guilds have sent you an ally request :");
                foreach (var guild in myList)
                {
                    PrintToChat(player, "[00FF00]" + guild);
                }
            }
            else PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : You have not had any alliance requests currently.");
        }

        // Request Alliance with player
        [ChatCommand("requestally")]
        private void RequestAllianceWithPlayer(Player player, string cmd, string[] playerArray)
        {
            if (playerArray.Length < 1)
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : To request an alliance, type [00FF00]/requestally [FF00FF]<player_name>.");
                return;
            }

            // Get players involved in the alliance
            var playerName = player.DisplayName;
            var targetPlayerName = playerArray[0];
            if(playerArray.Length > 1)
            {
                for (var i = 1; i < playerArray.Length; i++)
                {
                    targetPlayerName = string.Format(targetPlayerName + " {0}", playerArray[i]);
                }
            }

            // Find the chosen target player
            Player targetPlayer = Server.GetPlayerByName(targetPlayerName);

            //Check that this player can be found
            if (targetPlayer == null)
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : That person is not currently awake.");
                return;
            }

            // Check they are not trying to ally with themselves
            if (string.Compare(targetPlayerName.ToLower(), player.DisplayName.ToLower()) == 0)
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : Alliances with yourself? Are you in need of friends that much, my Lord?");
                return;
            }

            // Get the player's guild
            if (PlayerExtensions.GetGuild(targetPlayer).DisplayName == null)
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : It would appear you have no guild, my Lord! I don't know how that happened...");
                return;
            }
            string playerGuild = PlayerExtensions.GetGuild(targetPlayer).Name;

            // Check they are not in the same guild
            string myGuild = PlayerExtensions.GetGuild(player).Name;

            // Remove unneccessary [0] at start of string
            playerGuild = playerGuild.Replace("[0]", "");
            myGuild = myGuild.Replace("[0]", "");

            if (string.Compare(playerGuild, myGuild) == 0)
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : That person is already a part of your guild, my Lord. An alliance is somewhat unnecessary at this juncture...");
                return;
            }

            //Check that the request hasn't already been sent.
            var allianceRequestNumber = AllianceRequestAlreadyExists(myGuild, playerGuild);
            if (allianceRequestNumber >= 0)
            {
                PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : There is already an outstanding ally request between these two guilds.");
                return;
            }

            // Check that you are not already allied
            if (allianceList.ContainsKey(myGuild.ToLower()))
            {
                var myAlliances = allianceList[myGuild.ToLower()];
                foreach(var alliance in myAlliances)
                {
                    if (alliance == playerGuild.ToLower())
                    {
                        PrintToChat(player, "[FF0000]Alliance Organiser[FFFFFF] : You are already allied with this guild, my Lord.");
                        return;
                    }
                }
            }

            // Send Alliance Request
            PrintToChat("[FF0000]Alliance Organiser : [00FF00]" + Capitalise(playerName) + "[FFFFFF] from [00FF00]" + Capitalise(myGuild) + "[FFFFFF] is requesting a formal Alliance with [00FF00]" + Capitalise(playerGuild) + "[FFFFFF].");
            PrintToChat("[FFFFFF]Type [00FF00]/acceptally [FF00FF]<guild_name> [FFFFFF]to accept the request or [00FF00]/denyally [FF00FF]<guild_name> [FFFFFF]to deny the request.");

            // Add request to the RequestAlliance list
            var requestDetails = new Collection<string>();
            requestDetails.Add(myGuild);
            requestDetails.Add(playerGuild);
            requestList.Add(requestDetails);

            // Save Data
            SaveAllianceListData();
        }

        // PRIVATE METHODS ==================================================================================

        private Collection<string> GetGuildList(string myGuild)
        {
            if (allianceList.ContainsKey(myGuild))
            {
                return allianceList[myGuild];
            }
            return null;
        }

        private void RemoveAlliance(string myGuild, string guildName)
        {
            // Remove Alliance for MyGuild
            if (allianceList.ContainsKey(myGuild.ToLower()))
            {
                var myAlliances = allianceList[myGuild.ToLower()];
                for (var i = 0; i < myAlliances.Count; i++)
                {
                    if (myAlliances[i] == guildName.ToLower()) myAlliances.RemoveAt(i);
                }
            }

            // Remove Alliance for Their guild
            if (allianceList.ContainsKey(guildName))
            {
                var theirAlliances = allianceList[myGuild];
                for (var i = 0; i < theirAlliances.Count; i++)
                {
                    if (theirAlliances[i] == myGuild) theirAlliances.RemoveAt(i);
                }
            }
        }

        private void AddGuildsToTheAllianceList(string myGuild, string guildName)
        {
            // Implement Alliance List for MyGuild
            if (allianceList.ContainsKey(myGuild.ToLower()))
            {
                var myAlliances = allianceList[myGuild];
                myAlliances.Add(guildName.ToLower());
            }
            else
            {
                var newAllianceList = new Collection<string>();
                newAllianceList.Add(guildName.ToLower());
                allianceList.Add(myGuild.ToLower(), newAllianceList);
            }

            // Implement AllianceList for their guild
            if (allianceList.ContainsKey(guildName.ToLower()))
            {
                var myAlliances = allianceList[guildName.ToLower()];
                myAlliances.Add(myGuild.ToLower());
            }
            else
            {
                var newAllianceListOther = new Collection<string>();
                newAllianceListOther.Add(myGuild.ToLower());
                allianceList.Add(guildName.ToLower(), newAllianceListOther);
            }
        }

        private bool PlayerHasNotenteredAGuildName(string[] guildArray)
        {
            if (guildArray.Length < 1)
            {
                return true;
            }
            return false;
        }

        private bool AllianceAlreadyExists(string myGuild, string guildName)
        {
            var listOfAlliances = new Collection<string>();
            if (allianceList.ContainsKey(myGuild.ToLower()))
            {
                listOfAlliances = allianceList[myGuild.ToLower()];
                foreach (var alliance in listOfAlliances)
                {
                    if (alliance == guildName.ToLower())
                    {
                        return true;
                    }
                }
            }
            if (allianceList.ContainsKey(guildName.ToLower()))
            {
                listOfAlliances = allianceList[guildName.ToLower()];
                foreach (var alliance in listOfAlliances)
                {
                    if (alliance == myGuild.ToLower())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private int AllianceRequestAlreadyExists(string myGuild, string guildName)
        {
            for (var i = 0; i < requestList.Count; i++)
            {
                if (requestList[i][0].ToLower() == guildName.ToLower() && requestList[i][1].ToLower() == myGuild.ToLower())
                {
                    return i;
                }
                if (requestList[i][1].ToLower() == guildName.ToLower() && requestList[i][0].ToLower() == myGuild.ToLower())
                {
                    return i;
                }
            }
            return -1;
        }

        private string ConvertGuildNameToString(string[] guildArray)
        {
            var guildName = guildArray[0];
            if (guildArray.Length > 1)
            {
                for (var i = 1; i < guildArray.Length; i++)
                {
                    guildName = guildName + " " + guildArray[i];
                }
            }
            return guildName;
        }
		
		
		// Capitalise the Starting letters
		private string Capitalise(string word)
		{
			var finalText = "";
			finalText = Char.ToUpper(word[0]).ToString();
			var spaceFound = 0;
			for(var i=1; i<word.Length;i++)
			{
				if(word[i] == ' ')
				{
					spaceFound = i + 1;
				}
				if(i == spaceFound)
				{
					finalText = finalText + Char.ToUpper(word[i]).ToString();
				}
				else finalText = finalText + word[i].ToString();
			}
			return finalText;
		}
	}
}
