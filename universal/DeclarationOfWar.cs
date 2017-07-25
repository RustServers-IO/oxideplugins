using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using Oxide.Core;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Thrones.SocialSystem;
namespace Oxide.Plugins
{
    [Info("DeclarationOfWar", "juk3b0x", "1.2.0")]
    public class DeclarationOfWar : ReignOfKingsPlugin
    {
        #region MODIFIABLE VARIABLES


        int WarTimeLength;
        int WarReportInterval;
        int WarPrepTime;
        int WarPrepTimeHours;
        int WarPrepTimeMinutes;
        int WarPrepTimeSeconds;

        protected override void LoadDefaultConfig()
        {
            Config["WarTimeLength"] = WarTimeLength = GetConfig("WarTimeLength", 5400);
            Config["WarReportInterval"] = WarReportInterval = GetConfig("WarReportinterval", 300);
            Config["WarPrepTime"] = WarPrepTime = GetConfig("WarPrepTime", 600);
            Config["WarPrepTimeHours"] = WarPrepTimeHours = GetConfig("WarPrepTimeHours", 0);
            Config["WarPrepTimeMinutes"] = WarPrepTimeMinutes = GetConfig("WarPrepTimeMinutes", 10);
            Config["WarPrepTimeSeconds"] = WarPrepTimeSeconds = GetConfig("WarPrepTimeSeconds", 0);

            SaveConfig();
        }
        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));
        #endregion
        #region language Dictionary
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoActiveWars", "[FF0000] War Report [FFFFFF]  There are currently no active wars. The land is finally at peace once more." },
                { "NoAdmin", "Only a God(admin) can end wars." },
                { "EndAllWarsMessage", "[FF0000]Ended all Wars for :[00FF00] {0} [FFFFFF]" },
                { "WarReportPrefix", "[0000FF]WAR REPORT[FFFFFF]" },
                { "PlayerOffline", "The Player {0} seems to be offline." },
                { "IsOwnGuildPlayer", "[FF0000]War Squire[FFFFFF] : My Lord! {0} is your friend! A trusted Ally! You can't declare war on them! It would get... awkward...!" },
                { "AlreadyAtWar", "[FF0000]War Squire[FFFFFF] : We are already at war with {0}, my Lord." },
                { "DeclarationOfWar", " [FFFFFF] ([FF0000]Declaring War[FFFFFF]) : [00FF00] {0} [FFFFFF]has declared war on [00FF00] {1}[FFFFFF]! They have [00FF00] {2} [FFFFFF]hrs, [00FF00] {3}[FFFFFF]mins and [00FF00]{4}[FFFFFF]secs to prepare for war!" },
                { "HelpText", "[FF0000]War Organiser[FFFFFF] : Use the following commands for Wars :" },
                { "1HelpText", "[00FF00]/declarewar [FF00FF]<player_name> [FFFFFF] - Declare war on players guild" },
                { "2HelpText", "[00FF00]/warreport [FFFFFF] - View all active wars" },
                { "HTA", "[00FF00]/endwar [FF00FF]<player_name> [FFFFFF] - End current war on players guild" },
                { "AHT1", "[00FF00]/endallwars [FFFFFF] - End current war on players guild" },
                { "PreparingForWar", "[FF0000]War Report : [00FF00]{0}[FFFFFF] is preparing for war with [00FF00] {1} [FFFFFF]." },
                { "IsAtWar", "[FF0000]War Report : [00FF00]{0}[FFFFFF] is at war with [00FF00]{1}." },
                { "WarHasEnded", "You need [00FF00]{0}[FFFF00]xp[FFFFFF] more to reach the next level." },
                { "TimePrepare", "[FFFFFF]There are [00FF00] {0}[FFFFFF]hrs, [00FF00] {1}[FFFFFF]mins, [00FF00] {2}[FFFFFF]secs until this war begins!" },
                { "AtWarTime", "[00FF00]{0}[FFFFFF]hrs, [00FF00]{1}[FFFFFF]mins, [00FF00]{2}[FFFFFF]secs remaining." },
                { "CommencedWar", "[FF0000]WAR BETWEEN [00FF00]{0}[FF0000] AND {1}[FF0000] HAS BEGUN!" },
                { "WarIsOver", "[FF0000] War Report [FFFFFF]([00FF00]WAR OVER![FFFFFF]) : The war between [00FF00]{0} [FFFFFF] and [00FF00]{1}[FFFFFF] has now ended!" },
                { "NotAtWar", "[FF0000]War General : [00FF00]{0}[FFFFFF]! You cannot attack this base when you are not at war with [00FF00] {1}[FFFFFF]!!" },
                { "SelfDeclare", "[FF0000]War Squire[FFFFFF] : You can't declare war upon thyself, my Lord! This is crazy talk!" },
                { "Instructions", "[FF0000]Declare War Instructions[FFFFFF] : Type /declarewar followed by the Player's name to declare war on that player's guild. THIS CANNOT BE UNDONE!" },
                { "GuildLess", "[FF0000]War Squire[FFFFFF] : My Lord, you have not yet formed a guild. You must do so before you can declare a war!"},
                { "EndAllWars", "Ending all guild wars..."},
                { "SpecificEndWar", "Ending all wars for guild : [00FF00] {0}"}
            }, this);
        }
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
        #region SERVER VARIABLES (DO NOT MODIFY)

        private Collection<Collection<string>> WarList = new Collection<Collection<string>>();
        // WarList[0] = the war ID
        // WarList[1] = the instigating Guild name
        // WarList[2] = the enemy guild name
        private const int WarTimerInterval = 1;


        // SAVE DATA ===============================================================================================

        private void LoadWarData()
        {
            WarList = Interface.GetMod().DataFileSystem.ReadObject<Collection<Collection<string>>>("DeclarationOfWar");
        }

        private void SaveWarListData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("DeclarationOfWar", WarList);
        }


        void Loaded()
        {
            LoadDefaultConfig();
            LoadWarData();
            LoadDefaultMessages();
            // Load the WarTimer Updater
            timer.Repeat(WarReportInterval, 0, WarReport);
            timer.Repeat(WarTimerInterval, 0, WarUpdate);


        }

        #endregion

        #region PLAYER COMMANDS 


        // Get current War Report
        [ChatCommand("warreport")]
        private void GetWarReport(Player player, string cmd)
        {
            if (WarList.Count <= 0) PrintToChat(player, string.Format(GetMessage("NoActiveWars", player.Id.ToString())));
            WarReport();
        }

        // CHEAT COMMAND for Admins to end all wars
        [ChatCommand("endallwars")]
        private void EndAllWarsOnServer(Player player, string cmd)
        {

            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, string.Format(GetMessage("NoAdmin", player.Id.ToString())));
                return;
            }
            PrintToChat(string.Format(GetMessage("EndAllWars", null)));

            // End all Wars in the List
            WarList = new Collection<Collection<string>>();

            SaveWarListData();
        }

        // CHEAT COMMAND for end specific wars
        [ChatCommand("endwar")]
        private void EndSpecificWarOnServer(Player player, string cmd, string[] playerToEndWar)
        {
            Collection<string> WarToRemove = null;
            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, string.Format(GetMessage("NoAdmin", player.Id.ToString())));
                return;
            }
            var PlayerToEndWar = string.Concat(playerToEndWar);
            var playerName = playerToEndWar[0];
            if (PlayerToEndWar == "")
            {
                PrintToChat(player, GetMessage("EnterAName"));
            }

            Player targetPlayer = Server.GetPlayerByName(PlayerToEndWar);
            if (targetPlayer == null)
            {
                PrintToChat(player, string.Format(GetMessage("PlayerOffline", player.Id.ToString()), playerToEndWar));
            }
            var guildName = PlayerExtensions.GetGuild(targetPlayer).Name;

            PrintToChat(player, string.Format(GetMessage("SpecificEndWar", player.Id.ToString()), guildName));

            // End all Wars for this guild
            foreach (Collection<string> war in WarList)
            {
                if (!war.Contains(guildName.ToLower())) continue;
                PrintToChat(string.Format(GetMessage("EndAllWarsMessage", null), guildName));
                WarToRemove = war;
            }
            WarList.Remove(WarToRemove);
            SaveWarListData();
        }

        [ChatCommand("declarewar")]
        private void DeclareWarOnGuild(Player player, string cmd, string[] targetPlayerNameArray)
        {
            //if the player only types /delcarewar, show the instructions on what to do
            if (targetPlayerNameArray.Length < 1)
            {
                PrintToChat(player, string.Format(GetMessage("Instructions", player.Id.ToString())));
                return;
            }

            // Convert the player name string array to a string
            var targetPlayerName = targetPlayerNameArray[0];
            if(targetPlayerNameArray.Length > 1)
            {
                for(var i=1; i<targetPlayerNameArray.Length; i++)
                {
                    targetPlayerName = string.Format(targetPlayerName + " {0}", targetPlayerNameArray[i]);
                }
            }

            // Find the chosen target player
            Player targetPlayer = Server.GetPlayerByName(targetPlayerName);

            //Check that this player can be found
            if (targetPlayer == null)
            {
                PrintToChat(player, string.Format(GetMessage("PlayerOffline",player.Id.ToString()), targetPlayerNameArray));
                return;
            }

            // Check they are not trying to declare war on themselves
            if (string.Compare(targetPlayerName.ToLower(), player.DisplayName.ToLower()) == 0) 
            {
                PrintToChat(player, string.Format(GetMessage("SelfDeclare", player.Id.ToString())));
                return;
            }

            // Get the player's guild
            if (PlayerExtensions.GetGuild(targetPlayer).DisplayName == null)
            {
                PrintToChat(player, string.Format(GetMessage("GuildLess", player.Id.ToString())));
                return;
            }
            string playerGuild = PlayerExtensions.GetGuild(targetPlayer).Name.ToLower();

            // Check they are not in the same guild
            string myGuild = PlayerExtensions.GetGuild(player).Name.ToLower();
            
            // Remove unneccessary [0] at start of string
            playerGuild = playerGuild.Replace("[0]","");
            myGuild = myGuild .Replace("[0]","");

            if (string.Compare(playerGuild, myGuild) == 0)
            {
                PrintToChat(player, string.Format(GetMessage("IsOwnGuildPlayer",player.Id.ToString()), targetPlayerNameArray));
                return;
            }

            // Check that they aren't already in a war with this guild
            foreach(var war in WarList)
            {
                if((string.Compare(myGuild,war[1]) == 0 && string.Compare(playerGuild,war[2]) == 0) || (string.Compare(myGuild,war[2]) == 0 && string.Compare(playerGuild,war[1]) == 0))
                {
                    PrintToChat(player, string.Format(GetMessage("AlreadyAtWar",player.Id.ToString()), playerGuild));
                     return;
                }                
            }

            // Tell the World that war has been declared!
          
            PrintToChat(string.Format(GetMessage("DeclarationOfWar",player.Id.ToString()),Capitalise(myGuild), Capitalise(playerGuild), WarPrepTimeHours, WarPrepTimeMinutes, WarPrepTimeSeconds));
        //    PrintToChat(targetPlayer,string.Format(GetMessage("DeclarationOfWar",player.Id.ToString()),Capitalise(myGuild), Capitalise(playerGuild), WarPrepTimeHours, WarPrepTimeMinutes, WarPrepTimeSeconds));
            // Begin the War!
            CommenceWar(myGuild + playerGuild, myGuild, playerGuild);
            // Save the data
            SaveWarListData();
        }

        // LIST WAR COMMANDS
        [ChatCommand("warcommands")]
        private void ShowHelpPopup(Player player, string cmd)
        {
            //	player.ShowPopup("WarCommands", helpText, "Exit", (selection, dialogue, data) => DoNothing(player, selection, dialogue, data));
            //	return;
            PrintToChat(player, string.Format(GetMessage("HelpText", player.Id.ToString())));
			    PrintToChat(player, string.Format(GetMessage("1HelpText", player.Id.ToString())));
			    PrintToChat(player, string.Format(GetMessage("2HelpText", player.Id.ToString())));
				if (player.HasPermission("admin"))
				{
			          PrintToChat(player, string.Format(GetMessage("HTA", player.Id.ToString())));
			          PrintToChat(player, string.Format(GetMessage("AHT1", player.Id.ToString())));
				}
        }
	
#endregion
#region WAR SCRIPTS

        
        private void CommenceWar(string warID, string myGuild, string targetGuild)
        {
            // Add the War Details 
            var newWarInfo = new Collection<string>();
            newWarInfo.Add(warID);
            newWarInfo.Add(myGuild);
            newWarInfo.Add(targetGuild);
            var timeLengthAsString = (WarTimeLength + WarPrepTime).ToString();
            newWarInfo.Add(timeLengthAsString);

            // Add the War to the War List
            WarList.Add(newWarInfo);
            SaveWarListData();
        }

        private void EndWar(string warID)
        {
            // Find the War by it's ID string
            for (var i = 0; i < WarList.Count; i++)
            {
                if (string.Compare(warID, WarList[i][0]) == 0)
                {
                    WarList.RemoveAt(i);
                    SaveWarListData();
                }
            }
        }
		
       
        private void WarReport()
        {
            LoadWarData();
            var player = "default";
            var hours = "";
            var minutes = "";
            var seconds = "";

            if(WarList.Count >= 1)
            {
                // Check each War in the List
                PrintToChat(GetMessage("WarReportPrefix",null));
                for (var i = 0; i < WarList.Count; i++)
                {
                    var timeLeft = Int32.Parse(WarList[i][3]);
                    hours = (timeLeft / 60 / 60).ToString();
                    minutes = ((timeLeft - (Int32.Parse(hours) * 60 * 60))/60).ToString();
                    var intSeconds = timeLeft - (Int32.Parse(hours) * 60 * 60) - (Int32.Parse(minutes) * 60);
                    seconds = intSeconds.ToString();

                    if(timeLeft > WarTimeLength)
                    {
						
                        var prepTimeLeft = timeLeft  - WarTimeLength;
                        var prepHours = (prepTimeLeft / 60 / 60).ToString();
                        var prepMinutes = ((prepTimeLeft - (Int32.Parse(prepHours) * 60 * 60))/60).ToString();
                        var intPrepSeconds = prepTimeLeft - (Int32.Parse(prepHours) * 60 * 60) - (Int32.Parse(prepMinutes) * 60);
                        var prepSeconds = intPrepSeconds.ToString();

                        PrintToChat( string.Format(GetMessage("PreparingForWar",player),Capitalise(WarList[i][1]),Capitalise(WarList[i][2]) ));
                        PrintToChat( string.Format(GetMessage("TimePrepare", player),prepHours , prepMinutes , prepSeconds));
                    }
                    else
                    {
                        PrintToChat(string.Format(GetMessage("IsAtWar",player),Capitalise(WarList[i][1]),Capitalise(WarList[i][2]) ));
                        PrintToChat(string.Format(GetMessage("AtWarTime",player),hours , minutes , seconds));
                    }
                }
            }

            // Save the data
            SaveWarListData();
        }

        private void WarUpdate()
        {
            var player="Default";
            // Check each War in the List
            foreach(var war in WarList)
            {
                // Countdown the time for this war
                var timeLeft = Int32.Parse(war[3]);

                if(timeLeft == WarTimeLength)
                {
                    PrintToChat(string.Format(GetMessage("CommencedWar",player), Capitalise(war[1]) , Capitalise(war[2])));
                }

                timeLeft = timeLeft - 1;

                //Store this value in the War Record
                war[3] = timeLeft.ToString();

                // If war has ended, let everyone know and end the war
                if(timeLeft <= 0)
                {
                    PrintToChat(string.Format(GetMessage("WarIsOver" , player) , Capitalise(war[1]) , Capitalise(war[2])));
                    EndWar(war[0]);
                }
            }
        }
        

#endregion
#region Base Attack

        private void OnCubeTakeDamage(CubeDamageEvent e)
        {
            //define attacker and defender			

            bool trebuchet = e.Damage.Damager.name.Contains("Trebuchet");
            bool ballista = e.Damage.Damager.name.Contains("Ballista");
			
          	//attacker is easy, because he is "usually" an online player
			var player = e.Damage.DamageSource.Owner;
			//defender is less easy, because there is no actual "cubeOwner"
			var CubePosition = e.Position; 
			var worldCoordinate = e.Grid.LocalToWorldCoordinate(CubePosition);
	        var crestScheme = SocialAPI.Get<CrestScheme>();
            if (crestScheme.GetCrestAt(worldCoordinate) == null)
            {
                return;
            }
			var crest = crestScheme.GetCrestAt(worldCoordinate);
			var playerGuild = PlayerExtensions.GetGuild(player).Name.ToLower();
			var targetGuild = crest.GuildName.ToLower();
			
                // Check if the guilds are at war
                foreach (var war in WarList)
				{
					if (war[2].ToLower() == playerGuild.ToLower() && war[1].ToLower() == targetGuild.ToLower() || (war[1].ToLower() == playerGuild.ToLower() && war[2].ToLower() == targetGuild.ToLower()))
					{
						return;
					}
				}
				
				if ((e.Damage.Amount > 0 && e.Damage.DamageSource.Owner is Player) && trebuchet || ballista)
                        {
                            e.Damage.Amount = 0f;
                           
                            PrintToChat(string.Format(GetMessage("NotAtWar", player.Id.ToString()) , player.Name , targetGuild));
                        }
                    } 
		
            
        

#endregion
#region UTILITY METHODS
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
        //The following BOOLS are only for Plugin-Referrence purposes. You can use them in NoKoS-Plugins, to activate Kill on Sight for Guilds, which are at war with each other.
        private bool DiedInWar(string attacker)
        {
            foreach (var players in WarList)
                if (players.Contains(attacker)) return true;

            return false;
        }
        private bool AtWar(string attackerGuild, string defenderGuild)
        {
            LoadWarData();
            foreach (var war in WarList)
                if (war.Contains(attackerGuild) && war.Contains(defenderGuild))return true;

            return false;
        }
        #endregion

    }
}
