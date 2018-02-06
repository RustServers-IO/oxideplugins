using System;
using System.Collections.ObjectModel;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using Oxide.Core;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Blocks.Networking.Events;

namespace Oxide.Plugins
{
    [Info("WarTracker", "Scorpyon", "1.1.5")]
    public class WarTracker : ReignOfKingsPlugin
    {
#region MODIFIABLE VARIABLES

        // MODIFY THIS VALUE TO THE NUMBER OF 'SECONDS' THAT WARS WILL LAST FOR
        private const int WarTimeLength = 5400; // Currently 1.5 hrs
        // MODIFY THIS VALUE TO THE NUMBER OF 'SECONDS' THAT YOU WANT BETWEEN WAR UPDATE REPORTS TO PLAYERS 
        private const int WarReportInterval = 300; // Currently 5 minutes
        // MODIFY THIS VALUE FOR PREPARATION TIME BEFORE WAR STARTS
        private const int WarPrepTime = 3600; // Total prep time in seconds = Currently 10 minutes
        private const int WarPrepTimeHours = 0;         //
        private const int WarPrepTimeMinutes = 10;      // These are for text purposes to save time on calculations later
        private const int WarPrepTimeSeconds = 0;       //
        // MODIFY THIS VALUE TO TRUE IF YOU ONLY WANT PLAYERS TO BE KILLED WHEN AT WAR (Prevents KoS)
        private bool _noPeaceKilling = true;
        // MODIFY THIS VALUE TO TRUE IF YOU ONLY WANT CRESTS TO BE DAMAGED WHEN AT WAR (Prevents Base Stealing)
        private bool _noCrestKilling = true;
        // MODIFY THIS VALUE TO TRUE IF YOU ONLY WANT BUILDINGS TO BE DAMAGED WHEN AT WAR (Prevents Base Destruction)
        private bool _noBaseKilling = true; //(Currently Not Working - disregard for now...)

#endregion


        // DO NOT EDIT ANYTHING BELOW THIS LINE UNLESS YOU WANT TO EXPERIMENT / KNOW WHAT YOU'RE DOING.
        // ==================================================================================================

#region SERVER VARIABLES (DO NOT MODIFY)

        private Collection<Collection<string>> WarList = new Collection<Collection<string>>();
        // WarList[0] = the war ID
        // WarList[1] = the instigating Guild name
        // WarList[2] = the enemy guild name
        private const int WarTimerInterval = 1;
        void Log(string msg) => Puts($"{Title} : {msg}");

        // SAVE DATA ===============================================================================================

		private void LoadWarData()
		{
            WarList = Interface.GetMod().DataFileSystem.ReadObject<Collection<Collection<string>>>("SavedWarList");
            _noPeaceKilling = Interface.GetMod().DataFileSystem.ReadObject<bool>("SavedWarListNoPeace");
            _noCrestKilling = Interface.GetMod().DataFileSystem.ReadObject<bool>("SavedWarListNoCrest");
        }

        private void SaveWarListData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("SavedWarList", WarList);
            Interface.GetMod().DataFileSystem.WriteObject("SavedWarListNoPeace", _noPeaceKilling);
            Interface.GetMod().DataFileSystem.WriteObject("SavedWarListNoCrest", _noCrestKilling);
        }
        
        void Loaded()
        {            
            // Load the WarTimer Updater
            timer.Repeat(WarReportInterval, 0, WarReport);
            timer.Repeat(WarTimerInterval, 0, WarUpdate);

            LoadWarData();
		}

#endregion

#region PLAYER COMMANDS 
        
        
        // Get current War Report
        [ChatCommand("warreport")]
        private void GetWarReport(Player player, string cmd)
        {
            if(WarList.Count <= 0) PrintToChat("[FF0000] War Report [FFFFFF] : There are currently no active wars. The land is finally at peace once more.");
            WarReport();
        }

        // CHEAT COMMAND for Admins to end all wars
        [ChatCommand("endallwars")]
        private void EndAllWarsOnServer(Player player, string cmd)
        {
            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, "Only an admin may end all guild wars!!");
                return;
            }
            PrintToChat(player, "Ending all guild wars...");

            // End all Wars in the List
            WarList = new Collection<Collection<string>>();

            SaveWarListData();
        }

        // CHEAT COMMAND for end specific wars
        [ChatCommand("endwar")]
        private void EndSpecificWarOnServer(Player player, string cmd, string[] playerToEndWar)
        {
            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, "Only an admin may end a guild war!!");
                return;
            }

            var playerName = playerToEndWar[0];
            if(playerToEndWar.Length > 1){
                for(var i=1; i<playerToEndWar.Length; i++)
                {
                    playerName = string.Format(playerName + " {0}",playerToEndWar[i]);
                }
            }

            Player targetPlayer = Server.GetPlayerByName(playerName);
            if(targetPlayer == null){
                PrintToChat(player,"That player is currently not on the server.");
            }
            var guildName = PlayerExtensions.GetGuild(targetPlayer).DisplayName;

            //PrintToChat(player, "Ending all wars for guild : [00FF00]" + guildName);

            // End all Wars for this guild
            for(var i=0; i<WarList.Count; i++)
            {
                if(string.Compare(guildName.ToLower(),WarList[i][1].ToLower()) == 0 || string.Compare(guildName.ToLower(),WarList[i][2].ToLower()) == 0)
                {
                    PrintToChat("[FF0000]Ending all Wars for :[00FF00]" + guildName);
                    WarList.RemoveAt(i);
                    i--;
                }
            }
        }

        [ChatCommand("declarewar")]
        private void DeclareWarOnGuild(Player player, string cmd, string[] targetPlayerNameArray)
        {
            //if the player only types /delcarewar, show the instructions on what to do
            if (targetPlayerNameArray.Length < 1)
            {
                PrintToChat(player, "[FF0000]Declare War Instructions[FFFFFF] : Type /declarewar followed by the Player's name to declare war on that player's guild. THIS CANNOT BE UNDONE!");
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
                PrintToChat(player, "[FF0000]War Squire[FFFFFF] : That person is not available. Perhaps they exist only in your imagination my Lord?");
                return;
            }

            // Check they are not trying to declare war on themselves
            if (string.Compare(targetPlayerName.ToLower(), player.DisplayName.ToLower()) == 0) 
            {
                PrintToChat(player, "[FF0000]War Squire[FFFFFF] : You can't declare war upon thyself, my Lord! This is crazy talk!");
                return;
            }

            // Get the player's guild
            if (PlayerExtensions.GetGuild(targetPlayer).DisplayName == null)
            {
                PrintToChat(player, "[FF0000]War Squire[FFFFFF] : My Lord, you have not yet formed a guild. You must do so before you can declare a war!");
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
                PrintToChat(player, "[FF0000]War Squire[FFFFFF] : My Lord! That is your friend! A trusted Ally! You can't declare war on them! It would get... awkward...!");
                return;
            }

            // Check that they aren't already in a war with this guild
            foreach(var war in WarList)
            {
                if((string.Compare(myGuild,war[1]) == 0 && string.Compare(playerGuild,war[2]) == 0) || (string.Compare(myGuild,war[2]) == 0 && string.Compare(playerGuild,war[1]) == 0))
                {
                    PrintToChat(player, "[FF0000]War Squire[FFFFFF] : We are already at war with that guild, my Lord.");
                     return;
                }                
            }

            // Tell the World that war has been declared!
            string warText = player.DisplayName + "[FFFFFF] ([FF0000]Declaring War[FFFFFF]) : [00FF00]" + Capitalise(myGuild) + " [FFFFFF]has declared war on [00FF00]" + Capitalise(playerGuild) + "[FFFFFF]! They have [00FF00]" + WarPrepTimeHours + "[FFFFFF]hrs, [00FF00]" + WarPrepTimeMinutes + "[FFFFFF]mins and [00FF00]" + WarPrepTimeSeconds + "[FFFFFF]secs to prepare for war!";
            PrintToChat(warText);
            
            // Begin the War!
            CommenceWar(myGuild + playerGuild, myGuild, playerGuild);

            // Save the data
            SaveWarListData();
        }

        // LIST WAR COMMANDS
        [ChatCommand("warcommands")]
        private void ListAllAllianceCommands(Player player, string cmd)
        {
            PrintToChat(player, "[FF0000]War Organiser[FFFFFF] : Use the following commands for Wars :");
            PrintToChat(player, "[00FF00]/declarewar [FF00FF]<player_name> [FFFFFF] - Declare war on players guild");
            PrintToChat(player, "[00FF00]/warreport [FFFFFF] - View all active wars");
            if (player.HasPermission("admin"))
            {
                PrintToChat(player, "[00FF00]/endwar [FF00FF]<player_name> [FFFFFF] - End current war on players guild");
                PrintToChat(player, "[00FF00]/endallwars [FFFFFF] - End current war on players guild");
                PrintToChat(player, "[00FF00]/warnokos [FFFFFF] - Toggle player protection for when not in a war");
                PrintToChat(player, "[00FF00]/warnocrest [FFFFFF] - Toggle crest protection for when not in a war");
                PrintToChat(player, "[00FF00]/warnobase [FFFFFF] - Toggle base protection for when not in a war");
            }
        }

        // Toggle KoS Rules
        [ChatCommand("warnokos")]
        private void ToggleNoKoS(Player player, string cmd)
        {
            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, "Only an admin may use this command!");
                return;
            }
            if (_noPeaceKilling)
            {
                _noPeaceKilling = false;
                SaveWarListData();
                PrintToChat(player, "Players can now kill on sight!");
                return;
            }
            _noPeaceKilling = true;
            SaveWarListData();
            PrintToChat(player, "Players can no longer kill unless at war!");
        }

        // Toggle Crest breaking rules
        [ChatCommand("warnocrest")]
        private void ToggleNoCrestKill(Player player, string cmd)
        {
            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, "Only an admin may use this command!");
                return;
            }
            if (_noCrestKilling)
            {
                _noCrestKilling = false;
                SaveWarListData();
                PrintToChat(player, "Players can now break crests at any time!");
                return;
            }
            _noCrestKilling = true;
            SaveWarListData();
            PrintToChat(player, "Players can no longer break crests unless at war!");
        }

        // Toggle Base Rules
        [ChatCommand("warnobase")]
        private void ToggleNoBase(Player player, string cmd)
        {
            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, "Only an admin may use this command!");
                return;
            }
            if (_noBaseKilling)
            {
                _noBaseKilling = false;
                SaveWarListData();
                PrintToChat(player, "Players can now attack other players' bases at will!");
                return;
            }
            _noBaseKilling = true;
            SaveWarListData();
            PrintToChat(player, "Players can no longer attack bases unless at war!");
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
        }

        private void EndWar(string warID)
        {
            // Find the War by it's ID string
            for (var i = 0; i < WarList.Count; i++)
            {
                if (string.Compare(warID, WarList[i][0]) == 0)
                {
                    WarList.RemoveAt(i);
                }
            }
        }
		
        private bool GuildsAreAtWar(EntityDamageEvent damageEvent)
        {
            var player = damageEvent.Damage.DamageSource.Owner;
            var target = damageEvent.Entity.Owner;
            var playerGuild = PlayerExtensions.GetGuild(player).Name.ToLower();
            var targetGuild = PlayerExtensions.GetGuild(target).Name.ToLower();

            //PrintToChat(player, playerGuild.ToLower() + " " + targetGuild.ToLower());
            //PrintToChat(player, "Number of wars = " + WarList.Count);
            ////PrintToChat(player, "First war in list = " + WarList[0][0] + " " + WarList[0][1] + " " + WarList[0][2]);

            foreach (var war in WarList)
            {
                PrintToChat(player, playerGuild.ToLower() + " " + targetGuild.ToLower() + " " + war[1].ToLower() + " " + war[2].ToLower() );
                if (war[2].ToLower() == playerGuild.ToLower() && war[1].ToLower() == targetGuild.ToLower()) return true;
                if (war[1].ToLower() == playerGuild.ToLower() && war[2].ToLower() == targetGuild.ToLower()) return true;
            }

            return false;
        }

        
        private void WarReport()
        {
            var hours = "";
            var minutes = "";
            var seconds = "";

            if(WarList.Count >= 1)
            {
                // Check each War in the List
                PrintToChat("[0000FF]WAR REPORT");
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

                        PrintToChat("[FF0000]War Report : [00FF00]" + Capitalise(WarList[i][1]) + "[FFFFFF] is preparing for war with [00FF00]" + Capitalise(WarList[i][2]));
                        PrintToChat("[FFFFFF]There are [00FF00]" + prepHours + "[FFFFFF]hrs, [00FF00]" + prepMinutes + "[FFFFFF]mins, [00FF00]" + prepSeconds + "[FFFFFF]secs until this war begins!");
                    }
                    else
                    {
                        PrintToChat("[FF0000]War Report : [00FF00]" + Capitalise(WarList[i][1]) + "[FFFFFF] is at war with [00FF00]" + Capitalise(WarList[i][2]));
                        PrintToChat("[00FF00]" + hours + "[FFFFFF]hrs, [00FF00]" + minutes + "[FFFFFF]mins, [00FF00]" + seconds + "[FFFFFF]secs remaining.");
                    }
                }
            }

            // Save the data
            SaveWarListData();
        }

        private void WarUpdate()
        {
            // Check each War in the List
            foreach(var war in WarList)
            {
                // Countdown the time for this war
                var timeLeft = Int32.Parse(war[3]);

                if(timeLeft == WarTimeLength)
                {
                    PrintToChat("[FF0000]WAR BETWEEN [00FF00]" + Capitalise(war[1]) + "[FF0000] AND " + Capitalise(war[2]) + "[FF0000] HAS BEGUN!");
                }

                timeLeft = timeLeft - 1;

                //Store this value in the War Record
                war[3] = timeLeft.ToString();

                // If war has ended, let everyone know and end the war
                if(timeLeft <= 0)
                {
                    PrintToChat("[FF0000] War Report [FFFFFF]([00FF00]WAR OVER![FFFFFF]) : The war between [00FF00]" + Capitalise(war[1]) + " [FFFFFF] and [00FF00]" + Capitalise(war[2]) + "[FFFFFF] has now ended!");
                    EndWar(war[0]);
                }
            }
        }
        

#endregion

#region ENTITY HEALTH CHANGE AND DEATH

        //// PREVENTS ALL PLAYER DAMAGE WHEN GUILDS ARE NOT AT WAR
        //private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        //{
        //    //PrintToChat("Damage detected");
        //    if (damageEvent.Damage.Amount < 0) return;
        //    if (_noPeaceKilling)
        //    {
        //        //PrintToChat("Checking if allowed...");
        //        if (
        //            damageEvent.Damage.Amount > 0 // taking damage
        //            && damageEvent.Entity.IsPlayer // entity taking damage is player
        //            && damageEvent.Damage.DamageSource.IsPlayer // entity delivering damage is a player
        //            && damageEvent.Entity != damageEvent.Damage.DamageSource // entity taking damage is not taking damage from self
        //            && !GuildsAreAtWar(damageEvent) // The guilds are not currently at war
        //            )
        //        {
        //            //PrintToChat("Cancelling Damage");
        //            damageEvent.Damage.Amount = 0f;
        //            //PrintToChat("Damage amount - " + damageEvent.Damage.Amount);
        //            damageEvent.Cancel("Can Only Kill When At War");
        //            PrintToChat(damageEvent.Damage.DamageSource.Owner,
        //                "[FF0000]War General : [FFFFFF]You cannot attack another person when you are not at war with them!");
        //        }
        //    }
        //    if (_noCrestKilling)
        //    {
        //        // Make sure it's not a player with a clever name! 
        //        if (!damageEvent.Entity.IsPlayer)
        //        {
        //            if (damageEvent.Entity.name.Contains("Crest"))
        //            {
        //                damageEvent.Cancel("Can Only Break Crests When At War");
        //                damageEvent.Damage.Amount = 0f;
        //                PrintToChat(damageEvent.Damage.DamageSource.Owner,
        //                    "[FF0000]War General : [FFFFFF]You cannot break another guild's crest when you are not at war with them!");
        //            }
        //        }
        //    }
        //}

        //private void OnCubeTakeDamage(CubeDamageEvent cubeDamageEvent)
        //{
        //    if (_noBaseKilling)
        //    {
        //        var player = cubeDamageEvent.Damage.DamageSource.Owner;
        //        var isAtWar = false;
				
        //        // CHeck if the guilds are at war
        //        foreach(var war in WarList)
        //        {
        //            if(war[1].ToLower() == PlayerExtensions.GetGuild(player).DisplayName.ToLower() || war[2].ToLower() == PlayerExtensions.GetGuild(player).DisplayName.ToLower())
        //            {
        //                isAtWar = true;
        //            }
        //        }
				
        //        if (!isAtWar)
        //        {
        //            // IF its a player attacking the base
        //            if (cubeDamageEvent.Damage.Amount > 50 && cubeDamageEvent.Damage.DamageSource.Owner is Player)
        //            {
                        
        //            }
        //            // Or if it's a siege weapon
        //            else if (cubeDamageEvent.Damage.Amount > 50)
        //            {
        //                bool trebuchet = cubeDamageEvent.Damage.Damager.name.Contains("Trebuchet");
        //                bool ballista = cubeDamageEvent.Damage.Damager.name.Contains("Ballista");
        //                if (trebuchet || ballista)
        //                {
        //                    cubeDamageEvent.Damage.Amount = 0f;
        //                    var message = "[FF0000]War General : [00FF00]" + player.DisplayName + "[FFFFFF]! You cannot attack this base when you are not at war with this guild!";
        //                    PrintToChat(message);
        //                    Log(message);
        //                }
        //            }
        //        }
        //    }
        //}

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
#endregion

	}
}
