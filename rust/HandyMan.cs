using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Facepunch;
using System;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HandyMan", "nivex", "1.1.0")]
    [Description("Provides AOE repair functionality to the player. Repair is only possible where you can build.")]
    public class HandyMan : RustPlugin
    {
        [PluginReference]
        Plugin NoEscape;

        const string permName = "handyman.use";
        readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("HandyMan");
        Dictionary<ulong, bool> playerPrefs_IsActive = new Dictionary<ulong, bool>(); //Stores player preference values - on or off.
        bool _allowHandyManFixMessage = true; //indicator allowing for handyman fix messages
        bool _allowAOERepair = true; //indicator for allowing AOE repair
        PluginTimers RepairMessageTimer; //Timer to control HandyMan chats
        static int constructionMask = LayerMask.GetMask("Construction");
        static int allMask = LayerMask.GetMask("Construction", "Deployed");

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
                {"IFixedEx", "I fixed {0} constructions over here..."},
                {"FixDone", "Guess I fixed them all..."},
                {"MissingFix", "I'm telling you... it disappeared... I can't find anything to fix."},
                {"NoPermission", "You don't have permission to use this command." },
                {"Help", helpText}
            }, this);
        }

        /// <summary>
        /// Repairs all entities
        /// </summary>
        /// <param name="player"></param>
        /// <param name="info"></param>
        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!HasPerm(player) || IsRaidBlocked(player.UserIDString) || info.HitEntity == null || info.HitEntity.IsDestroyed)
                return;

            //gets the correct entity type from the hammer target
            var entity = info.HitEntity.GetComponent<BaseCombatEntity>();

            if (!entity)
                return;

            if (!playerPrefs_IsActive.ContainsKey(player.userID)) // update user if they have no profile
            {
                playerPrefs_IsActive[player.userID] = DefaultHandyManOn;
                dataFile.WriteObject(playerPrefs_IsActive);
            }

            //Check if repair should fire - This is to prevent a recursive / infinate loop when all structures in range fire this method.
            //This also checks if the player has turned HandyMan on
            if (_allowAOERepair && playerPrefs_IsActive[player.userID])
            {
                //calls our custom method for this
                Repair(entity, player);
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
            if (HasPerm(player))
                player.ChatMessage(GetMsg("Help", player.userID).Replace("{ver}", Version.ToString()));
        }
        #endregion
        
        #region Repair Methods

        /// <summary>
        /// Executes the actual repair logic.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="player"></param>
        void Repair(BaseCombatEntity entity, BasePlayer player)
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
                RepairAOE(entity, player);
            }
            else
                SendChatMessage(player, Title, GetMsg("NotAllowed", player.userID));
        }

        bool CanRepair(BaseCombatEntity entity, BasePlayer player)
        {
            float num = entity.MaxHealth() - entity.health;
            float num2 = num / entity.MaxHealth();
            var list = entity.RepairCost(num2);

            if (list != null && list.Count > 0)
            {
                foreach(var ia in list)
                {
                    var items = player.inventory.FindItemIDs(ia.itemid);
                    int sum = items.Sum(item => item.amount);

                    if (sum < ia.amount)
                        return false;
                }
            }

            return player.CanBuild(new OBB(entity.transform, entity.bounds));
        }

        /// <summary>
        /// Contains the actual AOE repair logic
        /// </summary>
        /// <param name="block"></param>
        /// <param name="player"></param>
        private void RepairAOE(BaseCombatEntity entity, BasePlayer player)
        {
            //This needs to be set to false in order to prevent the subsequent repairs from triggering the AOE repair.
            //If you don't do this - you create an infinite repair loop.
            _allowAOERepair = false;
            
            //gets the position of the block we just hit
            var position = new OBB(entity.transform, entity.bounds).ToBounds().center;
            //sets up the collectionf or the blocks that will be affected
            var entities = Pool.GetList<BaseCombatEntity>();

            //gets a list of entities within a specified range of the current target
            Vis.Entities(position, RepairRange, entities, repairDeployables ? allMask : constructionMask);
            int repaired = 0;
            
            //check if we have blocks - we should always have at least 1
            if (entities.Count > 0)
            {
                bool hasRepaired = false;

                //cycle through our block list - figure out which ones need repairing
                foreach (var ent in entities)
                {   
                    //check to see if the block has been damaged before repairing.
                    if (ent.health < ent.MaxHealth())
                    {
                        //yes - repair
                        if (!CanRepair(ent, player))
                            continue;

                        DoRepair(ent, player);
                        hasRepaired = true;

                        if (++repaired > maxRepairEnts)
                            break;
                    }
                }
                Pool.FreeList(ref entities);

                //checks to see if any entities were repaired
                if (hasRepaired)
                {
                    //yes - indicate
                    //SendChatMessage(player, Title, GetMsg("IFixed", player.userID));
                    SendChatMessage(player, Title, string.Format(GetMsg("IFixedEx", player.userID), repaired));
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

        // BaseCombatEntity
        public virtual void DoRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (!entity.repair.enabled)
            {
                return;
            }
            if (Interface.CallHook("OnStructureRepair", new object[]
            {
                entity,
                player
            }) != null)
            {
                return;
            }
            if (entity.SecondsSinceAttacked <= 8f)
            {
                entity.OnRepairFailed();
                return;
            }
            float num = entity.MaxHealth() - entity.health;
            float num2 = num / entity.MaxHealth();
            if (num <= 0f || num2 <= 0f)
            {
                entity.OnRepairFailed();
                return;
            }
            var list = entity.RepairCost(num2);
            if (list == null)
            {
                return;
            }
            foreach(var ia in list.ToList())
            {
                ia.amount *= repairMulti;
            }
            float num3 = list.Sum(x => x.amount);
            if (num3 > 0f)
            {
                float num4 = list.Min(x => Mathf.Clamp01((float)player.inventory.GetAmount(x.itemid) / x.amount));
                num4 = Mathf.Min(num4, 50f / num);
                if (num4 <= 0f)
                {
                    entity.OnRepairFailed();
                    return;
                }
                int num5 = 0;
                foreach (var current in list)
                {
                    int amount = Mathf.CeilToInt(num4 * current.amount);
                    int num6 = player.inventory.Take(null, current.itemid, amount);
                    if (num6 > 0)
                    {
                        num5 += num6;
                        /*player.Command("note.inv", new object[]
                        {
                            current.itemid,
                            num6 * -1
                        });*/
                    }
                }
                float num7 = (float)num5 / num3;
                entity.health += num * num7;
                entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
            else
            {
                entity.health += num;
                entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
            if (entity.health >= entity.MaxHealth())
            {
                entity.OnRepairFinished();
            }
            else
            {
                entity.OnRepair();
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
        [ChatCommand("handyman")]
        private void ChatCommand_HandyMan(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
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
                playerPrefs_IsActive[player.userID] = args[0].ToLower() == "on";
                dataFile.WriteObject(playerPrefs_IsActive);
            }

            SendChatMessage(player, Title, GetMsg(playerPrefs_IsActive[player.userID] ? "Hired" : "Fired", player.userID));
        }

        [ConsoleCommand("healthcheck")]
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
        bool IsRaidBlocked(string targetId) => UseRaidBlocker && (bool)(NoEscape?.Call("IsRaidBlocked", targetId) ?? false);
        bool HasPerm(BasePlayer player) => permission.UserHasPermission(player.UserIDString, permName) || player.IsAdmin;

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
        private float repairMulti;
        private bool repairDeployables;
        private int maxRepairEnts;

        void LoadVariables() //Assigns configuration data once read
        {
            HandyManChatInterval = Convert.ToInt32(GetConfig("Settings", "Chat Interval", 30));
            DefaultHandyManOn = Convert.ToBoolean(GetConfig("Settings", "Default On", true));
            RepairRange = Convert.ToInt32(GetConfig("Settings", "Repair Range", 50));
            UseRaidBlocker = Convert.ToBoolean(GetConfig("Settings", "Use Raid Blocker", false));
            repairMulti = Convert.ToSingle(GetConfig("Settings", "Repair Cost Multiplier", 1.0f));
            repairDeployables = Convert.ToBoolean(GetConfig("Settings", "Repair Deployables", false));
            maxRepairEnts = Convert.ToInt32(GetConfig("Settings", "Maximum Entities To Repair", 50));

            if (repairMulti < 1.0f)
                repairMulti = 1.0f;

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
