using Assets.Scripts.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using uLink;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("VehicleManager", "SouZa", "1.2.4", ResourceId = 1725)]
    [Description("Vehicle customization! You can now install/remove/switch vehicle attachments.")]

    class VehicleManager : HurtworldPlugin
    {
        #region [CLASSES]
        public class PlayerVehicles
        {
            public PlayerVehicles(ulong steamid)
            {
                this.owner = steamid;
                positions = new List<string>();
            }

            public ulong owner { get; set; }
            public List<string> positions { get; set; }

            public void addVehicle(Vector3 pos)
            {
                string s = pos.ToString();
                s = s.Substring(1, s.Length - 2);
                positions.Add(s);
            }
        }
        #endregion

        #region [Variables]
        Dictionary<ulong, PlayerVehicles> _playerVehicles = new Dictionary<ulong, PlayerVehicles>();
        #endregion

        #region [LOADS] AND [SAVES]
        new void LoadConfig()
        {
            SetConfig("VehiclePermissionByDefault", ".use", false);
            SetConfig("VehiclePermissionByDefault", ".install", false);
            SetConfig("VehiclePermissionByDefault", ".remove", false);
            SetConfig("VehiclePermissionByDefault", ".remove.extra", false);
            SetConfig("ShowDistanceCommand", true);
            SetConfig("MaxVehicleClaimsPerPlayer", 1);

            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new configuration file...");
        }
        void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"carCmds_AVAILABLE", "Available vehicle commands:"},
                {"carCmds_carInfo", "Show distance between you and your claimed vehicles."},
                {"carCmds_install", "Install new attachment from quick slot 1-8."},
                {"carCmds_installLR", "Install panel on [L]eft or [R]ight side."},
                {"carCmds_remove", "Remove bumper|front|left|right|roof|rear."},
                {"carCmds_removeExtra", "Remove gearbox|engine|tire."},
                {"carCmds_claim", "Claim a vehicle."},
                {"carCmds_unclaim", "Remove the claim from a vehicle."},
                {"msg_SERVER", "SERVER "},
                {"msg_INFO", "INFO "},
                {"msg_permission", "You don't have permission to use this command. Required: {perm}" },
                {"msg_showDistanceCMD", "This command is disabled." },
                {"msg_carInfo", "Type /car help for proper commands usage."},
                {"msg_vehicleDistance", "You have a {vehicle} {distance} away from you." },
                {"msg_noCarAttachment", "There is no vehicle attachment in slot {slot}." },
                {"msg_removeError", "The vehicle doesn't have a {attach} to remove." },
                {"msg_removeSeatError", "The {seat} is occupied. Remove failed." },
                {"msg_noClaim", "You don't have a claimed vehicle." },
                {"msg_notInsideVehicle", "You are not inside a vehicle." },
                {"msg_notOwner", "You are not the owner of this vehicle." },
                {"msg_notNearVehicle", "You are not near your claimed vehicle." },
                {"msg_notVehicleAttachment", "You are not installing a vehicle attachment." },
                {"msg_notCorrectVehicleAttachment", "You are not installing a correct attachment for {vehicleType}." },
                {"msg_vehicleInstall", "You have installed {attachInstalled}."},
                {"msg_vehicleSwitch", "You have switched {attachSwitched} to {attachInstalled}." },
                {"msg_roachSideIncorrect", "Want to install side panel in left or right?"},
                {"msg_vehicleRemove", "You have removed {attachRemoved}." },
                {"msg_vehicleRemoveAll", "You have removed everything from {vehicleType}." },
                {"msg_maxClaim", "You already have {number} claimed vehicles." },
                {"msg_claimWithButtom", "Use the claim button on this vehicle window." }
            };

            lang.RegisterMessages(messages, this);
        }
        #endregion

        #region [PERMISSIONS]
        void SetPermissions()
        {
            permission.RegisterPermission("vehiclemanager.use", this);
            permission.RegisterPermission("vehiclemanager.install", this);
            permission.RegisterPermission("vehiclemanager.remove", this);
            permission.RegisterPermission("vehiclemanager.remove.extra", this);

            if (GetConfig(false, "VehiclePermissionByDefault", ".use"))
                permission.GrantGroupPermission("default", "vehiclemanager.use", this);
            if (GetConfig(false, "VehiclePermissionByDefault", ".install"))
                permission.GrantGroupPermission("default", "vehiclemanager.install", this);
            if (GetConfig(false, "VehiclePermissionByDefault", ".remove"))
                permission.GrantGroupPermission("default", "vehiclemanager.remove", this);
            if (GetConfig(false, "VehiclePermissionByDefault", ".remove.extra"))
                permission.GrantGroupPermission("default", "vehiclemanager.remove.extra", this);
        }
        #endregion

        //Plugin Loaded
        void Loaded()
        {
            LoadConfig();
            LoadDefaultMessages();
            SetPermissions();
            LoadData();
        }

        void LoadData()
        {
            _playerVehicles = Core.Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PlayerVehicles>>("VehicleManager/PlayerVehicles");
        }


        public string Vector3ToString(Vector3 v3, int decimals = 2, string separator = " ")
        {
            return
                $"{Math.Round(v3.x, decimals)}{separator}{Math.Round(v3.y, decimals)}{separator}{Math.Round(v3.z, decimals)}";
        }

        public Vector3 StringToVector3(string v3)
        {
            var split = v3.Split(',').Select(Convert.ToSingle).ToArray();
            return split.Length == 3 ? new Vector3(split[0], split[1], split[2]) : Vector3.zero;
        }

        void SaveData()
        {
            PrintWarning("Saving Data on VehicleManager/PlayerVehicles");
            Core.Interface.GetMod().DataFileSystem.WriteObject("VehicleManager/PlayerVehicles", _playerVehicles);
        }

        #region [CHAT COMMANDS]
        [ChatCommand("offset")]
        void cmdOffset(PlayerSession session, string command, string[] args)
        {
            if (session.IsAdmin)
            {
                var allVsm = Resources.FindObjectsOfTypeAll<VehicleStatManager>().ToList();
                VehicleOwnershipManager vom = Singleton<VehicleOwnershipManager>.Instance;
                VehicleStatManager vsm = getVSMs(session, vom, allVsm, true)?[0];

                //hurt.SendChatMessage(session, "DEBUG: ", $"{session.WorldPlayerEntity.transform.localPosition}");
            }
        }

        [ChatCommand("car")]
        void cmdCar(PlayerSession session, string command, string[] args)
        {
            //Check for permission to use the plugin
            if (!userHasPerm(session, "vehiclemanager.use"))
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_SERVER"), "red") + GetMsg("msg_permission").Replace("{perm}", "vehiclemanager.use"));
                return;
            }

            var allVsm = Resources.FindObjectsOfTypeAll<VehicleStatManager>().ToList();
            VehicleOwnershipManager vom = Singleton<VehicleOwnershipManager>.Instance;
            VehicleControllerBase controller = null;
            RestrictedInventory restrictedInv = null;
            VehicleStatManager vsm = getVSMs(session, vom, allVsm, true)?[0];

            if (args.Length == 0)
            {
                if (!GetConfig(true, "ShowDistanceCommand"))
                {
                    hurt.SendChatMessage(session, Color(GetMsg("msg_SERVER"), "red") + GetMsg("msg_showDistanceCMD"));
                    return;
                }
                var playerVSMs = getVSMs(session, vom, allVsm, false);
                if (playerVSMs != null && playerVSMs.Count > 0)
                {
                    foreach (var playerVSM in playerVSMs)
                    {
                        double distance = getPlayerToCarDistance(session, playerVSM);
                        string distanceMsg = string.Format("{0:0}m", (int)Math.Ceiling(distance));
                        int index = playerVSM.gameObject.name.IndexOf('(');
                        string vehicleName = playerVSM.gameObject.name.Substring(0, index);
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_vehicleDistance").Replace("{vehicle}", vehicleName).Replace("{distance}", distanceMsg));
                    }
                }
                else
                {
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_noClaim"));
                }
            }
            else if (args.Length == 1)
            {
                if (args[0] == "claim")
                {
                    if (vsm != null)
                    {
                        if (vsm.Owner == null)
                        {
                            controller = vsm.GetComponent<VehicleControllerBase>();
                            if (!controller.IsKittedOut())
                            {
                                Singleton<AlertManager>.Instance.GenericTextNotificationServer("Alerts/Not Drivable", session.Player);
                                return;
                            }
                            if (!vom.HasClaim(session.Identity))
                            {
                                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_claimWithButtom"));
                                return;
                            }

                            List<VehicleStatManager> playerVSM = (from v in allVsm where v.Owner == session.Identity select v).ToList();
                            if (playerVSM.Count >= GetConfig(1, "MaxVehicleClaimsPerPlayer"))
                            {
                                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_maxClaim").Replace("{number}", playerVSM.Count + ""));
                                return;
                            }

                            vsm.Owner = session.Identity;
                            Singleton<AlertManager>.Instance.GenericTextNotificationServer("Sucessfully claimed", session.Player);
                        }
                        else
                            Singleton<AlertManager>.Instance.GenericTextNotificationServer("Alerts/Already Owned", session.Player);
                    }
                    else
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_notInsideVehicle"));
                }
                else if (args[0] == "unclaim")
                {
                    if (vsm != null)
                    {
                        if (vsm.Owner != null && vsm.Owner == session.Identity)
                        {
                            if (vom.GetClaimant(vsm) != string.Empty)       //unclaim official vehicle
                            {
                                vom.Unclaim(session.Identity);
                            }
                            else                                            //unclaim one of additional vehicles
                            {
                                vsm.Owner = null;
                            }
                            Singleton<AlertManager>.Instance.GenericTextNotificationServer("Sucessfully unclaimed", session.Player);
                        }
                        else
                            Singleton<AlertManager>.Instance.GenericTextNotificationServer("Alerts/Not Owner", session.Player);
                    }
                    else
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_notInsideVehicle"));
                }
                else if (args[0] == "help" || args[0] == "h")
                {
                    // /car help | h

                    string[] commands = {
                        Color(GetMsg("carCmds_AVAILABLE"), "orange"),
                        Color("/car ", "orange") + GetMsg("carCmds_carInfo"),
                        Color("/car install <1-8> ", "orange") + GetMsg("carCmds_install"),
                        Color("/car install <1-8> <L|R> ", "orange") + GetMsg("carCmds_installLR"),
                        Color("/car remove <attach> ", "orange") + GetMsg("carCmds_remove"),
                        Color("/car remove <attach> ", "orange") + GetMsg("carCmds_removeExtra"),
                        Color("/car claim ", "orange") + GetMsg("carCmds_claim"),
                        Color("/car unclaim ", "orange") + GetMsg("carCmds_unclaim")
                    };

                    foreach (string cmd in commands)
                        hurt.SendChatMessage(session, cmd);
                }
                else
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_carInfo"));
            }
            else if (args.Length == 2 || (args.Length == 3 && (args[2].ToLower() == "l" || args[2].ToLower() == "r")))
            {
                //Get vehicle type (roach or goat) and RestrictedInventory
                string vehicleType = "";
                if (vsm != null)
                {
                    if (vsm.Owner == null || vsm.Owner == session.Identity)
                    {
                        controller = vsm.GetComponent<VehicleControllerBase>();
                        restrictedInv = controller.GetComponent<RestrictedInventory>();

                        //Get vehicle type
                        string tmp = vsm.name.ToLower();
                        if (tmp.Contains("roach"))
                            vehicleType = "roach";
                        else if (tmp.Contains("kanga"))
                            vehicleType = "kanga";
                        else if (tmp.Contains("goat"))
                            vehicleType = "goat";
                    }
                    else
                    {
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_notOwner"));
                        return;
                    }
                }
                else
                {
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_notInsideVehicle"));
                    return;
                }
                if (args[0] == "install")
                {
                    //Check for permission to install
                    if (!userHasPerm(session, "vehiclemanager.install"))
                    {
                        hurt.SendChatMessage(session, Color(GetMsg("msg_SERVER"), "red") + GetMsg("msg_permission").Replace("{perm}", "vehiclemanager.install"));
                        return;
                    }

                    // /car install 1-8 [mandatory for side panels: L|R]
                    //Check if player is installing a correct vehicle attachment
                    int slot = -1;
                    if (!int.TryParse(args[1], out slot) || slot < 1 || slot > 8)
                    {
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_carInfo"));
                        return;
                    }
                    PlayerInventory playerInventory = session.WorldPlayerEntity.GetComponent<PlayerInventory>();
                    var playerInventory_items = playerInventory.Items;
                    var playerItemInstance = playerInventory_items[slot - 1];
                    if (playerItemInstance != null)
                    {
                        IItem item = playerItemInstance.Item;
                        ESlotType itemSlotType = getSlotType(item);
                        string slotTypeString = itemSlotType.ToString().ToLower();
                        if (!slotTypeString.Contains(vehicleType))
                        {
                            if ((vehicleType == "roach" && slotTypeString.Contains("goat"))
                                || (vehicleType == "goat" && slotTypeString.Contains("roach"))
                                || (vehicleType == "kanga" && slotTypeString.Contains("kanga")))
                                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_notCorrectVehicleAttachment").Replace("{vehicleType}", vehicleType));
                            else
                                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_notVehicleAttachment"));
                            return;
                        }
                        if (itemSlotType == ESlotType.RoachSidePanel)
                        {
                            if (args.Length == 2 || (args.Length == 3 && (args[2].ToLower() != "l" && args[2].ToLower() != "r")))
                            {
                                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_roachSideIncorrect"));
                                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_carInfo"));
                                return;
                            }
                        }
                        //Correct item. Can install/switch.
                        //Check if vehicle has the same attachment type installed. If not, install player's item. If yes, switch with player's item.
                        if (controller != null)
                        {
                            if (restrictedInv != null)
                            {
                                SlotRestriction[] restrictions = restrictedInv.Restrictions;
                                for (int k = 0; k < (int)restrictions.Length; k++)
                                {
                                    ESlotType slotRestrictionType = restrictions[k].SlotType;
                                    if (slotRestrictionType == itemSlotType)
                                    {
                                        if (itemSlotType == ESlotType.RoachSidePanel && !sideMatchWithSlot(k, args[2].ToLower()))
                                        {
                                            continue;
                                        }
                                        //Found correct slot type
                                        if (restrictedInv.Items[restrictions[k].SlotNumber] == null)
                                        {
                                            //Vehicle doesn't have attachment on that slot. Can install.
                                            vehicleInstall(session, playerItemInstance, restrictedInv, restrictions[k].SlotNumber);
                                            hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_vehicleInstall").Replace("{attachInstalled}", Color(playerItemInstance.Item.GetNameKey(), "orange")).Replace("{vehicleType}", vehicleType));
                                        }
                                        else
                                        {
                                            //Vehicle have attachment on that slot. Can switch.
                                            string attachSwitched = vehicleSwitch(session, playerItemInstance, restrictedInv, restrictions[k].SlotNumber);
                                            hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_vehicleSwitch").Replace("{attachSwitched}", Color(attachSwitched, "orange")).Replace("{vehicleType}", vehicleType).Replace("{attachInstalled}", Color(playerItemInstance.Item.GetNameKey(), "orange")));
                                        }
                                        restrictedInv.Invalidate(false);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_noCarAttachment").Replace("{slot}", slot + ""));
                    }
                }
                else if (args[0] == "remove")
                {
                    // /car remove bumper|front|left|right|roof|rear|tire|engine|gearbox

                    string tmp = args[1];

                    //Check for permission to remove.extra
                    if (tmp == "gear" || tmp == "gearbox" || tmp == "engine" || tmp == "tire" || tmp == "wheel")
                        if (!userHasPerm(session, "vehiclemanager.remove.extra"))
                        {
                            hurt.SendChatMessage(session, Color(GetMsg("msg_SERVER"), "red") + GetMsg("msg_permission").Replace("{perm}", "vehiclemanager.remove.extra"));
                            return;
                        }
                    //Check for permission to remove
                    if (tmp == "bumper" || tmp == "front" || tmp == "left" || tmp == "right" || tmp == "roof" || tmp == "rear" || tmp == "all")
                        if (!userHasPerm(session, "vehiclemanager.remove"))
                        {
                            hurt.SendChatMessage(session, Color(GetMsg("msg_SERVER"), "red") + GetMsg("msg_permission").Replace("{perm}", "vehiclemanager.remove"));
                            return;
                        }

                    List<string> parts;
                    if (tmp == "all")
                    {
                        parts = new List<string> { "bumper", "front", "left", "right", "roof", "rear" };
                    }
                    else
                        parts = new List<string> { tmp };

                    bool ignorePart = false;
                    foreach (string attach in parts)
                    {
                        ignorePart = false;
                        //Cannot remove rear if a player is on the vehicle rear
                        if (attach == "rear" || attach == "front")
                        {
                            List<VehiclePassenger> passengers = GameObjectExtensions.GetComponentsByInterfaceInChildren<VehiclePassenger>(vsm.gameObject).ToList();
                            foreach (VehiclePassenger p in passengers)
                            {
                                if (p.Passenger != null)
                                {
                                    string seat = getSeatName(p.SeatOffset);
                                    if (attach == seat)
                                    {
                                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_removeSeatError").Replace("{seat}", seat));
                                        ignorePart = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (ignorePart)
                            continue;
                        //Get the restrictedSlots relative to vehicle attachment player wants to remove
                        List<int> restrictedSlots = getRestrictedSlots(attach, vehicleType, restrictedInv);
                        var im = GlobalItemManager.Instance;
                        foreach (int slot in restrictedSlots)
                        {
                            //Give vehicle attach to player inventory
                            ItemInstance vehicleAttach = restrictedInv.Items[slot];
                            im.GiveItem(session.Player, vehicleAttach.Item, 1);
                            //Remove attachment from vehicle.
                            restrictedInv.Items[slot] = null;
                            restrictedInv.Invalidate(false);
                            if (tmp != "all")
                                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_vehicleRemove").Replace("{attachRemoved}", Color(vehicleAttach.Item.GetNameKey(), "orange")).Replace("{vehicleType}", vehicleType));
                        }
                        if (restrictedSlots.Count == 0)
                        {
                            if (tmp != "all")
                                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_removeError").Replace("{attach}", attach));
                        }
                    }
                    if (tmp == "all")
                    {
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_vehicleRemoveAll").Replace("{vehicleType}", vehicleType));
                    }
                }
                else
                {
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_carInfo"));
                }
            }
            else
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO"), "orange") + GetMsg("msg_carInfo"));
            }
        }
        #endregion

        #region [HELP METHODS]
        //From @LaserHydra BetterChat.cs
        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList<string>();
            stringArgs.RemoveAt(args.Length - 1);

            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }
        //From @LaserHydra BetterChat.cs
        string ListToString(List<string> list, int first, string seperator)
        {
            return String.Join(seperator, list.Skip(first).ToArray());
        }
        //From @LaserHydra BetterChat.cs
        T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList<string>();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }
        bool userHasPerm(PlayerSession user, string perm)
        {
            return permission.UserHasPermission(user.SteamId.ToString(), perm);
        }
        List<VehicleStatManager> getVSMs(PlayerSession session, VehicleOwnershipManager vom, List<VehicleStatManager> allVSM, bool insideVehicle)
        {
            if (insideVehicle)
            {
                List<VehicleStatManager> nearVSM = (from vsm in allVSM where getPlayerToCarDistance(session, vsm) < 10 select vsm).ToList();
                foreach (var vsm in nearVSM)
                {
                    List<VehiclePassenger> passengers = GameObjectExtensions.GetComponentsByInterfaceInChildren<VehiclePassenger>(vsm.gameObject).ToList();

                    if ((from p in passengers where p.Passenger == session.WorldPlayerEntity select p).ToList().Count >= 1)
                    {
                        return new List<VehicleStatManager> { vsm };
                    }

                    /*
                    foreach (VehiclePassenger p in passengers)
                    {
                        if (p.Passenger == session.WorldPlayerEntity)
                        {
                            //Player is inside Vehicle
                            return new List<VehicleStatManager> { vsm };
                        }
                    }
                    */
                }
                return null;
            }

            List<VehicleStatManager> playerVSMs = (from v in allVSM where v.Owner != null && v.Owner == session.Identity select v).ToList();
            playerVSMs = playerVSMs.OrderBy(vsm => getPlayerToCarDistance(session, vsm)).ToList();

            return playerVSMs;
        }
        List<int> getRestrictedSlots(string attach, string vehicleType, RestrictedInventory restrictedInv)
        {
            SlotRestriction[] restrictions = restrictedInv.Restrictions;
            List<int> slots = new List<int>();
            ESlotType result = ESlotType.None;
            switch (attach)
            {
                case "bumper": result = ESlotType.RoachBullBar; break;
                case "front": result = (vehicleType == "roach") ? ESlotType.RoachFrontBay : (vehicleType == "goat" ? ESlotType.GoatFrontpanel : ESlotType.KangaFrontpanel); break;
                case "rear": result = (vehicleType == "roach") ? ESlotType.RoachRearBay : (vehicleType == "goat" ? ESlotType.GoatBackpanel : ESlotType.KangaBackpanel); break;
                case "left": result = ESlotType.RoachSidePanel; break;
                case "right": result = ESlotType.RoachSidePanel; break;
                case "roof": result = ESlotType.RoachRoofBay; break;
                case "wheel":
                case "tire": result = (vehicleType == "roach") ? ESlotType.RoachWheel : (vehicleType == "goat" ? ESlotType.QuadWheel : ESlotType.KangaWheel); break;
                case "gearbox":
                case "gear": result = (vehicleType == "roach") ? ESlotType.RoachGearbox : (vehicleType == "goat" ? ESlotType.QuadGearbox : ESlotType.KangaGearbox); break;
                case "engine": result = (vehicleType == "roach") ? ESlotType.RoachEngine : (vehicleType == "goat" ? ESlotType.QuadEngine : ESlotType.KangaEngine); break;
                default: break;
            }

            for (int k = 0; k < (int)restrictions.Length; k++)
            {
                ESlotType slotRestrictionType = restrictions[k].SlotType;
                if (slotRestrictionType == result && restrictedInv.Items[restrictions[k].SlotNumber] != null)
                {
                    if ((attach == "left" && restrictions[k].SlotNumber != 0) || (attach == "right" && restrictions[k].SlotNumber != 1))
                        continue;
                    slots.Add(restrictions[k].SlotNumber);
                }
            }

            return slots;
        }
        bool sideMatchWithSlot(int k, string side)
        {
            return ((side == "l" && k == 0) || (side == "r" && k == 1));
        }
        void vehicleInstall(PlayerSession session, ItemInstance playerItemInstance, RestrictedInventory vehicleInventory, int slotNumber)
        {
            var im = GlobalItemManager.Instance;
            //Remove attachment from player inventory
            playerItemInstance.ReduceStackSize(1);
            //give 0 "wood plank" (22) to submitter, just to refresh the inventory so the removed item icon disappear.
            im.GiveItem(session.Player, im.GetItem(22), 0);
            //Add attachment to vehicle.
            vehicleInventory.Items[slotNumber] = new ItemInstance(playerItemInstance.Item, playerItemInstance.Item.MaxStackSize);
        }
        string vehicleSwitch(PlayerSession session, ItemInstance playerItemInstance, RestrictedInventory vehicleInventory, int slotNumber)
        {
            var im = GlobalItemManager.Instance;
            //Remove attachment from player inventory
            playerItemInstance.ReduceStackSize(1);
            //Give vehicle attach to player inventory
            ItemInstance vehicleAttach = vehicleInventory.Items[slotNumber];
            im.GiveItem(session.Player, vehicleAttach.Item, 1);
            //Add attachment to vehicle.
            vehicleInventory.Items[slotNumber] = new ItemInstance(playerItemInstance.Item, playerItemInstance.Item.MaxStackSize);

            return vehicleAttach.Item.GetNameKey();
        }
        ESlotType getSlotType(IItem item)
        {
            foreach (ESlotType t in Enum.GetValues(typeof(ESlotType)))
            {
                if (item.IsSlotType(t))
                {
                    return t;
                }
            }
            return ESlotType.None;
        }
        //Returns the vehicle seat name of the given offset
        string getSeatName(Vector3 offset)
        {
            string seat;
            switch (offset.ToString())
            {
                case "(0.0, -0.2, 1.1)":
                    seat = "front";
                    break;
                case "(-0.4, 0.1, 0.0)":
                    seat = "left";
                    break;
                case "(0.4, 0.1, 0.0)":
                    seat = "right";
                    break;
                case "(0.0, 0.9, -1.1)":
                    seat = "rear";
                    break;
                case "(0.0, 0.0, 0.0)":
                    seat = "goat/kanga";
                    break;
                default:
                    seat = "unknown";
                    break;
            }
            return seat;
        }
        string GetMsg(string key) => lang.GetMessage(key, this);
        string Color(string text, string color)
        {
            switch (color)
            {
                case "chatBlue":
                    return "<color=#6495be>" + text + "</color>";

                default:
                    return "<color=" + color + ">" + text + "</color>";
            }
        }
        //PlayerSession owner needs to have a claim to use this method
        private float getPlayerToCarDistance(PlayerSession owner, VehicleStatManager vsm)
        {
            Vector3 playerPos = owner.WorldPlayerEntity.transform.position;
            Vector3 vehiclePos = vsm.transform.position;
            float distance = Vector3.Distance(playerPos, vehiclePos);

            return distance;
        }
        //Returns an online session from the player with name <identifier>
        PlayerSession getSession(string identifier)
        {
            var sessions = GameManager.Instance.GetSessions();
            PlayerSession session = null;
            foreach (var i in sessions)
            {
                if (identifier.ToLower().Equals(i.Value.Name.ToLower()) || identifier.Equals(i.Value.SteamId.ToString()))
                {
                    session = i.Value;
                    break;
                }
            }
            return session;
        }

        void OnServerInitialized()
        {
            List<VehicleStatManager> allVSM = Resources.FindObjectsOfTypeAll<VehicleStatManager>().ToList();
            foreach (var kvp in _playerVehicles)
            {
                List<Vector3> vehicles = new List<Vector3>();
                foreach (string p in kvp.Value.positions)
                {
                    vehicles.Add(StringToVector3(p));
                }

                foreach (Vector3 pos in vehicles)
                {
                    List<VehicleStatManager> vsms = (from v in allVSM where Vector3.Distance(v.transform.position, pos) <= 0.1f select v).ToList();
                    VehicleStatManager vsm = vsms.Count > 0 ? vsms[0] : null;

                    if (vsm != null)
                    {
                        vsm.Owner = GameManager.Instance.GetIdentifierMap()[new Steamworks.CSteamID(kvp.Key)];
                        vsms.Remove(vsm);
                    }
                }
            }
        }

        void OnServerShutdown()
        {
            _playerVehicles = new Dictionary<ulong, PlayerVehicles>();

            VehicleStatManager[] allVSM = Resources.FindObjectsOfTypeAll<VehicleStatManager>();

            foreach (var vsm in allVSM)
            {
                if (vsm.Owner != null)
                {
                    var steamid = vsm.Owner.SteamId.m_SteamID;
                    if (!_playerVehicles.ContainsKey(steamid))
                        _playerVehicles.Add(steamid, new PlayerVehicles(vsm.Owner.SteamId.m_SteamID));
                    _playerVehicles[steamid].addVehicle(vsm.transform.position);
                }
            }
            SaveData();
        }

        /*
        void OnEnterVehicle(PlayerSession session, CharacterMotorSimple passenger)
        {
            if (!session.IsAdmin)
            {
                return;
            }

            VehicleStatManager vsm = getVSMs(session, VehicleOwnershipManager.Instance, Resources.FindObjectsOfTypeAll<VehicleStatManager>().ToList(), true)?[0];

            if(vsm != null)
            {
                vsm.ThrottleTorqueMultiplier = 2;
                vsm.TurnSpeedMultiplier = 2;
                vsm.TopSpeedMultiplier = 2;
                vsm.TractionMultiplier = 2;
            }
        }

        void OnExitVehicle(PlayerSession session, CharacterMotorSimple passenger)
        {
            if (!session.IsAdmin)
            {
                return;
            }

            VehicleStatManager vsm = getVSMs(session, VehicleOwnershipManager.Instance, Resources.FindObjectsOfTypeAll<VehicleStatManager>().ToList(), true)?[0];

            if (vsm != null)
            {
                vsm.ThrottleTorqueMultiplier = 1;
                vsm.TurnSpeedMultiplier = 1;
                vsm.TopSpeedMultiplier = 1;
                vsm.TractionMultiplier = 1;
            }
        }
        */
        #endregion
    }
}
