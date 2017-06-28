using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Facepunch;
using System;

// 1.0.4 - added permission to use or see helptext per feedback, now repairs all construction types per author intentions, fix keynotfound in handyman chat command, removed unused using, reworked config
// allow all to use: oxide.grant group default handyman.use

// 1.0.5 - fix for InvalidCastException

namespace Oxide.Plugins
{
    [Info("HandyMan", "MrMan", "1.0.5")]
    [Description("Provides AOE repair functionality to the player. Repair is only possible where you can build.")]
    public class HandyMan : RustPlugin
    {
        [PluginReference]
        Plugin NoEscape;

        #region Constants
        private const string permName = "handyman.use";
        #endregion

        #region Members
        readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("HandyMan");
        Dictionary<ulong, bool> playerPrefs_IsActive = new Dictionary<ulong, bool>(); //Stores player preference values - on or off.
        private bool _allowHandyManFixMessage = true; //indicator allowing for handyman fix messages
        private bool _allowAOERepair = true; //indicator for allowing AOE repair

        private PluginTimers RepairMessageTimer; //Timer to control HandyMan chats
        #endregion

        #region Oxide Hooks
        //Called when this plugin has been fully loaded
        private void Loaded()
        {
            permission.RegisterPermission(permName, this);

            LoadVariables();
            LoadMessages();

            try
            {
                playerPrefs_IsActive = dataFile.ReadObject<Dictionary<ulong, bool>>();
            }
            catch { }

            if (playerPrefs_IsActive == null)
                playerPrefs_IsActive = new Dictionary<ulong, bool>();
        }

        void LoadMessages()
        {
            string helpText = "HandyMan - Help - v {ver} \n"
                            + "-----------------------------\n"
                            + "/HandyMan - Shows your current preference for HandyMan.\n"
                            + "/HandyMan on/off - Turns HandyMan on/off.";

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Hired", "HandyMan has been Hired."},
                {"Fired", "HandyMan has been Fired."},
                {"Fix", "You fix this one, I'll get the rest."},
                {"NotAllowed", "You are not allowed to build here - I can't repair for you."},
                {"IFixed", "I fixed some damage over here..."},
                {"FixDone", "Guess I fixed them all..."},
                {"MissingFix", "I'm telling you... it disappeared... I can't find anything to fix."},
                {"NoPermission", "You don't have permission to use this command." },
                {"Help", helpText}
            }, this);
        }

        /// <summary>
        /// TODO: Investigate entity driven repair.
        /// Currently only building structures are driving repair. I want to allow things like high external walls to also
        /// drive repair, but they don't seem to fire under OnStructureRepair. I suspect this would be a better trigger as it would
        /// allow me to check my entity configuration rather than fire on simple repair.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="info"></param>
        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!permission.UserHasPermission(player.UserIDString, permName) || IsRaidBlocked(player.UserIDString))
                return;

            //gets the correct entity type from the hammer target
            var e = info.HitEntity.GetComponent<BaseCombatEntity>();

            //checks to see that we have an entity - we should always have one
            if (e != null)
            {
                //yes - continue repair
                //checks if player preference for handyman exists on this player
                if (!playerPrefs_IsActive.ContainsKey(player.userID))
                {
                    //no - create a default entry for this player based on the default HandyMan configuration state
                    playerPrefs_IsActive[player.userID] = DefaultHandyManOn;
                    dataFile.WriteObject(playerPrefs_IsActive);
                }

                //Check if repair should fire - This is to prevent a recursive / infinate loop when all structures in range fire this method.
                //This also checks if the player has turned HandyMan on
                if (_allowAOERepair && playerPrefs_IsActive[player.userID])
                {
                    //calls our custom method for this
                    Repair(e, player);
                }
            }
        }

        #endregion

        #region HelpText Hooks

        /// <summary>
        /// Responsible for publishing help for handyman on request
        /// </summary>
        /// <param name="player"></param>
        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permName))
                player.ChatMessage(GetMsg("Help", player.userID).Replace("{ver}", Version.ToString()));
        }
        #endregion
        
        #region Repair Methods

        /// <summary>
        /// Executes the actual repair logic.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="player"></param>
        void Repair(BaseCombatEntity block, BasePlayer player)
        {
            //Set message timer to prevent user spam
            ConfigureMessageTimer();

            //Checks to see if the player can build
            if (player.CanBuild())
            {
                //yes - Player can build - check if we can display our fix message
                if (_allowHandyManFixMessage)
                {
                    //yes - display our fix message
                    SendChatMessage(player, Title, GetMsg("Fix", player.userID));
                    _allowHandyManFixMessage = false;
                }

                //Envoke the AOE repair set
                RepairAOE(block, player);
            }
            else
                SendChatMessage(player, Title, GetMsg("NotAllowed", player.userID));
        }

        /// <summary>
        /// Contains the actual AOE repair logic
        /// </summary>
        /// <param name="block"></param>
        /// <param name="player"></param>
        private void RepairAOE(BaseCombatEntity block, BasePlayer player)
        {
            //This needs to be set to false in order to prevent the subsequent repairs from triggering the AOE repair.
            //If you don't do this - you create an infinate repair loop.
            _allowAOERepair = false;

            //gets the position of the block we just hit
            var position = new OBB(block.transform, block.bounds).ToBounds().center;
            //sets up the collectionf or the blocks that will be affected
            var entities = Pool.GetList<BaseCombatEntity>();

            //gets a list of entities within a specified range of the current target
            Vis.Entities(position, RepairRange, entities);

            //check if we have blocks - we should always have at least 1
            if (entities.Count > 0)
            {
                bool hasRepaired = false;

                //cycle through our block list - figure out which ones need repairing
                foreach (var entity in entities)
                {
                    if (entity.gameObject.layer != (int)Rust.Layer.Construction)
                        continue;

                    //if (entity.lastAttacker != null)
                        //continue;

                    //check to see if the block has been damaged before repairing.
                    if (entity.health < entity.MaxHealth())
                    {
                        //yes - repair
                        entity.DoRepair(player);
                        entity.SendNetworkUpdate();
                        hasRepaired = true;
                    }
                }
                Pool.FreeList(ref entities);

                //checks to see if any entities were repaired
                if (hasRepaired)
                {
                    //yes - indicate
                    SendChatMessage(player, Title, GetMsg("IFixed", player.userID));
                }
                else
                {
                    //No - indicate
                    SendChatMessage(player, Title, GetMsg("FixDone", player.userID));
                }
            }
            else
            {
                SendChatMessage(player, Title, GetMsg("MissingFix", player.userID));
            }
            
            _allowAOERepair = true;
        }

        /// <summary>
        /// Responsible for preventing spam to the user by setting a timer to prevent messages from Handyman for a set duration.
        /// </summary>
        private void ConfigureMessageTimer()
        {
            //checks if our timer exists
            if (RepairMessageTimer == null)
            {
                //no - create it
                RepairMessageTimer = new PluginTimers(this);
                //set it to fire every xx seconds based on configuration
                RepairMessageTimer.Every(HandyManChatInterval, RepairMessageTimer_Elapsed);
            }
        }

        /// <summary>
        /// Timer for our repair message elapsed - set allow to true
        /// </summary>
        private void RepairMessageTimer_Elapsed()
        {
            //set the allow message to true so the next message will show
            _allowHandyManFixMessage = true;
        }

        #endregion


        #region Chat and Console Command Examples
        [ChatCommand("HandyMan")]
        private void ChatCommand_HandyMan(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permName))
            {
                SendChatMessage(player, Title, GetMsg("NoPermission", player.userID));
                return;
            }

            //checks if player preference for handyman exists on this player
            if (!playerPrefs_IsActive.ContainsKey(player.userID))
            {
                //no - create a default entry for this player based on the default HandyMan configuration state
                playerPrefs_IsActive[player.userID] = DefaultHandyManOn;
                dataFile.WriteObject(playerPrefs_IsActive);
            }

            if (args.Length > 0)
            {
                if (args[0].ToLower() == "on")
                    playerPrefs_IsActive[player.userID] = true;
                else
                    playerPrefs_IsActive[player.userID] = false;

                dataFile.WriteObject(playerPrefs_IsActive);
            }

            SendChatMessage(player, Title, GetMsg(playerPrefs_IsActive[player.userID] ? "Hired" : "Fired", player.userID));
        }

        [ConsoleCommand("HealthCheck")]
        private void ConsoleCommand_HealthCheck() => Puts("HandyMan is running.");
        #endregion

        #region Helpers

        /// <summary>
        /// Retreives the configured message from the lang API storage.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());
        bool IsRaidBlocked(string targetId) => UseRaidBlocker && (bool)(NoEscape?.Call("IsRaidBlockedS", targetId) ?? false);

        /// <summary>
        /// Writes message to player chat
        /// </summary>
        /// <param name="player"></param>
        /// <param name="prefix"></param>
        /// <param name="msg"></param>
        private void SendChatMessage(BasePlayer player, string prefix, string msg = null) => SendReply(player, msg == null ? prefix : "<color=#00FF8D>" + prefix + "</color>: " + msg);

        #endregion

        #region Config
        private bool Changed;
        private bool UseRaidBlocker;
        private bool DefaultHandyManOn;
        private int RepairRange;
        private int HandyManChatInterval;

        void LoadVariables() //Assigns configuration data once read
        {
            HandyManChatInterval = Convert.ToInt32(GetConfig("Settings", "Chat Interval", 30));
            DefaultHandyManOn = Convert.ToBoolean(GetConfig("Settings", "Default On", true));
            RepairRange = Convert.ToInt32(GetConfig("Settings", "Repair Range", 50));
            UseRaidBlocker = Convert.ToBoolean(GetConfig("Settings", "Use Raid Blocker", false));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        /// <summary>
        /// Responsible for loading default configuration.
        /// Also creates the initial configuration file
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        object GetConfig(string menu, string dataValue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(dataValue, out value))
            {
                value = defaultValue;
                data[dataValue] = value;
                Changed = true;
            }
            return value;
        }
        #endregion
    }
}
