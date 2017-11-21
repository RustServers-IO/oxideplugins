using Oxide.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Night Door", "Slydelix", "1.6.2", ResourceId = 2684)]
    class NightDoor : RustPlugin
    {
        int layers = LayerMask.GetMask("Construction");
        bool debug = false;
        bool BypassAdmin, BypassPerm, UseRealTime, AutoDoor, useMulti;
        string startTime, endTime;

        #region config

        protected override void LoadDefaultConfig()
        {
            if (Config["End time (if you want to be able to open doors until 20:30 you would put 20.5)"] != null)
            {
                Config.Save(Config.Filename + ".old");
                PrintWarning("New config was created because old config version was detected (sorry :/)");
                Config.Clear();
            }
            Config["Allow admins to open time-limited door"] = BypassAdmin = GetConfig("Allow admins to open time-limited door", false);
            Config["Allow players with bypass permission to open time-limited door"] = BypassPerm = GetConfig("Allow players with bypass permission to open time-limited door", false);
            Config["Beginning time (HH:mm)"] = startTime = GetConfig("Beginning time (HH:mm)", "00:00");
            Config["Use multiple time intervals"] = useMulti = GetConfig("Use multiple time intervals", false);
            Config["End time (HH:mm)"] = endTime = GetConfig("End time (HH:mm)", "00:00");
            Config["Use system (real) time"] = UseRealTime = GetConfig("Use system (real) time", false);
            Config["Automatic door closing/opening"] = AutoDoor = GetConfig("Automatic door closing/opening", false);
            SaveConfig();
        }

        #endregion
        #region lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"serverWipe_NEW", "Server wipe detected, wiping data file"},
                {"NotOpenable", "This door/hatch/gate cannot be opened until {0}"},
                {"CantPlace_NEW", "The code lock cannot be placed on this door/hatch/gate"},
                {"ManualWipe_NEW", "Manually wiped Night Door data file"},
                {"NoPerm_NEW", "You don't have permission to use this command"},
                {"WrongSyntax_NEW", "Wrong syntax! /nd help for more info"},
                {"NotDoor_NEW", "The object you are looking at is not a door/hatch/gate" },
                {"AlreadyOnList_NEW", "This door/hatch/gate is already time locked"},
                {"Start>End_NEW", "Config seems to be set up incorrectly! (Start time value is bigger than end time value)"},
                {"Start&EndValIs0_NEW", "Detected 00:00 as both end and start time! Change these to values you want"},
                {"NoDoorsCurrently_NEW", "No time locked doors/hatches/gates set up yet"},
                {"DoorInfo_NEW", "This door is time locked\nTime period is {0} ({1} - {2})"},
                {"ShowingDoor_NEW", "Showing all time locked doors/hatches/gates"},
                {"Timeperiod_removed", "Door at {0} can now be opened at any time because the time period got removed!"},
                {"AddedDoor_NEW", "This door/hatch/gate is now time locked (default time)"},
                {"AddedDoorCustom_NEW", "This door/hatch/gate is now time locked (Time period '{0}' ({1} - {2})"},
                {"NotOnList_NEW", "This door/hatch/gate isn't time locked"},
                {"timeintervalAddWrong_NEW", "Wrong syntax! <color=silver>/timeperiod create <name> <HH:mm>(starting time) <HH:mm>(ending time)</color>"},
                {"timeintervalRemoveWrong_NEW", "Wrong syntax! <color=silver>/timeperiod remove <name></color>"},
                {"RemovedDoor_NEW", "This door/hatch/gate is not time locked anymore"},
                {"ndMultiMsg_NEW", "Wrong syntax! /nd add <name of time period the door will use>"},
                {"CreatedEntry_NEW", "Created a new time period with name '{0}' ({1} - {2})"},
                {"RemovedEntry_NEW", "Removed a time period with name '{0}'"},
                {"NoTimeIntervals_NEW", "There are no time periods set up"},
                {"HowTo26Hour", "To create time periods that includes 2 days, like 22:00 - 02:00 add 24 to the last value (22:00 - 26:00)" },
                {"ListOfTimeIntervals_NEW", "List of all time periods: \n{0}"},
                {"NotFoundEntry_NEW", "Couldn't find a time period with name '{0}'"},
                {"TimeIntervalExists_NEW", "A time period with that name already exists"},
                {"debugging_disabled", "Debugging is disabled"},
                {"debugging_null", "Entity is null"},
                {"debugging_period", "Entity time period: {0}"},
                {"debugging_hit", "HIT entity: {0}"},
                {"debugging_entID", "Entity ID: {0}"},
                {"debugging_list", "Entity is in save list: {0}"},
                {"TimeIntervalUsage_NEW", "To create a time period type <color=silver>/timeperiod create <name of time period> <HH:mm> (starting time) <HH:mm> (ending time)</color>\nTo delete a time period type <color=silver>/timeperiod remove <name></color>\nFor list of all time periods type <color=silver>/timeperiod list</color>"},
                {"HelpMsg_NEW", "List of commands:\n<color=silver>*You have to look at the door/hatch/gate for most of the commands to work*</color>\n<color=silver>/nd add</color> - Makes the entity openable only during default time period(config time)\n<color=silver>/nd add <time period></color> Makes the entity openable only during specified time period (/timeperiod)\n<color=silver>/nd remove</color> Makes the entity 'normal' again (openable at any time)\n<color=silver>/nd show</color> shows all time locked entites\n<color=silver>/nd info</color> shows if the door/hatch/gate is time locked and the time period if it is\nCurrent time period: {0} - {1}"}
            }, this);
        }

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion
        #region DataFiles

        class StoredData2
        {
            public List<uint> IDlist = new List<uint>();

            public StoredData2()
            {
            }
        }

        StoredData2 storedData2;

        class StoredData
        {
            public Dictionary<uint, string> IDlist = new Dictionary<uint, string>();
            public HashSet<TimeInfo> TimeEntries = new HashSet<TimeInfo>();


            public StoredData()
            {
            }
        }

        class TimeInfo
        {
            public string name;
            public string start;
            public string end;

            public TimeInfo()
            {
            }

            public TimeInfo(string nameIn, string startInput, string endInput)
            {
                name = nameIn;
                start = startInput;
                end = endInput;
            }
        }

        StoredData storedData;

        #endregion
        #region Hooks

        void OnNewSave(string filename)
        {
            PrintWarning(lang.GetMessage("serverWipe_NEW", this));
            storedData.IDlist.Clear();
            SaveFile();
        }

        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission("nightdoor.use", this);
            permission.RegisterPermission("nightdoor.createinterval", this);
            permission.RegisterPermission("nightdoor.bypass", this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("NightDoor_NEW");
            storedData2 = Interface.Oxide.DataFileSystem.ReadObject<StoredData2>("NightDoor");
            if (GetDateTime(startTime) > GetDateTime(endTime)) PrintWarning(lang.GetMessage("Start>End_NEW", this));
            if (startTime == "00:00" && endTime == "00:00") PrintWarning(lang.GetMessage("Start&EndValIs0_NEW", this));
        }

        void Loaded()
        {
            Repeat();
            UpgradeSaveFile();
            DoDefaultT();
        }

        void SaveFile() => Interface.Oxide.DataFileSystem.WriteObject("NightDoor_NEW", storedData);

        void Unload() => SaveFile();

        void OnServerSave() => CheckAllEntites();

        void OnEntityKill(BaseNetworkable entity) => CheckAllEntites();

        void OnDoorOpened(Door door, BasePlayer player)
        {
            uint entID = door.GetEntity().net.ID;
            if (storedData.IDlist.ContainsKey(door.net.ID))
            {
                float time = ConVar.Env.time;
                if (CanOpen(player, entID, time)) return;
                door.CloseRequest();

                string entname = storedData.IDlist[entID];

                foreach(var entry in storedData.TimeEntries)
                {
                    if(entry.name == entname)
                    {
                        SendReply(player, lang.GetMessage("NotOpenable", this, player.UserIDString), entry.start);
                        return;
                    }
                }
                return;
            }
        }

        void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            BasePlayer player = deployer.GetOwnerPlayer();

            if (entity is Door)
            {
                BaseEntity codelockent = entity.GetSlot(BaseEntity.Slot.Lock);

                if (storedData.IDlist.ContainsKey(entity.net.ID))
                {
                    Item codelock;
                    if (codelockent.PrefabName == "assets/prefabs/locks/keypad/lock.code.prefab") codelock = ItemManager.CreateByName("lock.code", 1);
                    else codelock = ItemManager.CreateByName("lock.key", 1);
                    player.GiveItem(codelock);
                    codelockent.Kill();
                    SendReply(player, lang.GetMessage("CantPlace_NEW", this, player.UserIDString));
                    return;
                }
            }
        }

        bool CanPickupEntity(BaseCombatEntity entity, BasePlayer player)
        {
            if (storedData.IDlist.ContainsKey(entity.net.ID)) return false;
            else return true;
        }

        #endregion
        #region functions

        void Repeat()
        {
            timer.Every(5f, () => {
                CheckDoors();
                CheckAllEntites();
            }); 
        }

        void UpgradeSaveFile()
        {
            if (storedData2.IDlist == null) return;
            foreach (var entry in storedData2.IDlist)
            {
                if (!storedData.IDlist.ContainsKey(entry))
                {
                    storedData.IDlist.Add(entry, "default");
                    SaveFile();
                }

            }
            storedData2.IDlist = null;
            Interface.Oxide.DataFileSystem.WriteObject("NightDoor", storedData2);
            //END OF OLD DATA FILE
            return;
        }

        void DoDefaultT()
        {
            timer.Once(1f, () => {
                foreach (var entry in storedData.TimeEntries) if (entry.name == "default")
                    {
                        entry.start = startTime;
                        entry.end = endTime;
                        SaveFile();
                        return;
                    }

                var cfgTime = new TimeInfo("default", startTime, endTime);
                storedData.TimeEntries.Add(cfgTime);
                SaveFile();
                return;
            });
        }

        void CheckDoors()
        {
            if (!AutoDoor) return;

            Dictionary<uint, string> tempList = new Dictionary<uint, string>();
            float time;

            tempList = storedData.IDlist ?? tempList;
            time = ConVar.Env.time;

            if (tempList.Count > 1)
            {
                foreach (var entry in tempList)
                {
                    BaseEntity ent = BaseNetworkable.serverEntities.Find(entry.Key) as BaseEntity;

                    if (ent == null || ent.IsDestroyed) continue;

                    if (CanOpen(null, ent.net.ID, time))
                    {
                        ent.SetFlag(BaseEntity.Flags.Open, true);
                        ent.SendNetworkUpdateImmediate();
                    }

                    else
                    {
                        ent.SetFlag(BaseEntity.Flags.Open, false);
                        ent.SendNetworkUpdateImmediate();
                    }  
                }
            }
        }

        bool CanOpen(BasePlayer player, uint ID, float now)
        {
            if (player != null)
            {
                if (player.IsAdmin && BypassAdmin) return true;
                if (permission.UserHasPermission(player.UserIDString, "nightdoor.bypass") && BypassPerm) return true;
            }

            if (UseRealTime)
            {
                foreach(var entry in storedData.IDlist)
                {
                    if(entry.Key == ID)
                    {
                        string name = entry.Value;
                        foreach(var ent in storedData.TimeEntries)
                        {
                            if(ent.name == name)
                            {
                                string ending = ent.end;
                                string val = ending.Split(':')[0];
                                int valInt = Convert.ToInt32(val);
                                if (valInt > 24)
                                {
                                    DateTime start_changed = GetDateTime(ID, true);
                                    DateTime end_changed = GetDateTime(ID, false);
                                    start_changed = start_changed.AddDays(-1);
                                    end_changed = end_changed.AddDays(-1);
                                    if ((DateTime.Now >= start_changed) && (DateTime.Now <= end_changed)) return true;
                                    return false;
                                }

                                else
                                {
                                    DateTime start = GetDateTime(ID, true);
                                    DateTime end = GetDateTime(ID, false);
                                    if ((DateTime.Now >= start) && (DateTime.Now <= end)) return true;
                                    return false;
                                } 
                            }
                        }
                    }
                }
            }

            float a = GetFloat(ID, true);
            float b = GetFloat(ID, false);
            float c = b - 24f;
            if (c < 0) c = 99999f;

            if (now >= a && now <= b) return true;
            if (c != 99999f)
            {
                if (now >= 0f && now <= c) return true;
            }

            return false;
        }

        void CheckAllEntites()
        {
            List<uint> temp = new List<uint>();
            foreach (var entry in storedData.IDlist)
            {
                BaseEntity ent = BaseNetworkable.serverEntities.Find(entry.Key) as BaseEntity;
                if (ent == null || ent.IsDestroyed) temp.Add(entry.Key);
                else continue;
            }

            if (temp.Count < 1) return;

            foreach(uint id in temp) storedData.IDlist.Remove(id);
            SaveFile();
        }

        float GetFloat(uint ID, bool start)
        {
            string temp = storedData.IDlist[ID];
            string input = "";
            foreach (var entry in storedData.TimeEntries)
            {
                if (entry.name == temp)
                {
                    if (start)
                    {
                        input = entry.start;
                    }

                    else
                    {
                        input = entry.end;
                    }
                }
            }
            float final = 0f;
            string[] parts = input.Split(':');
            string h = "", m = "";
            int hourInt = Convert.ToInt32(parts[0]);
            int minInt = Convert.ToInt32(parts[1]);

            float min = (float)minInt / 60;
            final = hourInt + min;
            return final;
        }

        float GetFloat(string in2)
        {
            float final = 0f;
            string[] parts = in2.Split(':');
            string h = "", m = "";
            int hourInt = Convert.ToInt32(parts[0]);
            int minInt = Convert.ToInt32(parts[1]);

            float min = (float)minInt / 60;
            final = hourInt + min;
            return final;
        }

        DateTime GetDateTime(uint ID, bool start)
        {
            DateTime final = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
            string temp = storedData.IDlist[ID];
            string input = "";
            foreach (var entry in storedData.TimeEntries)
            {
                if (entry.name == temp)
                {
                    if (start)
                    {
                        input = entry.start;
                    }

                    else
                    {
                        input = entry.end;
                    }
                }
            }
            string[] parts = input.Split(':');
            string h, m;
            h = parts[0].ToString();
            m = parts[1].ToString();
            int mInt = Convert.ToInt32(m);
            int hInt = Convert.ToInt32(h);
            
            if(hInt > 24 && start)
            {
                final = final.AddHours(hInt);
                final = final.AddMinutes(mInt);
                final = final.AddDays(-1);
                return final;
            }

            final = final.AddHours(hInt);
            final = final.AddMinutes(mInt);
            return final;
        }

        DateTime GetDateTime(string input)
        {
            DateTime final = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
            string[] parts = input.Split(':');
            string h, m;
            h = parts[0].ToString();
            m = parts[1].ToString();
            int mInt = Convert.ToInt32(m);
            int hInt = Convert.ToInt32(h);
            final = final.AddHours(hInt);
            final = final.AddMinutes(mInt);
            return final;
        }

        BaseEntity GetLookAtEntity(BasePlayer player, float maxDist = 250, int coll = -1)
        {
            if (player == null || player.IsDead()) return null;
            RaycastHit hit;
            var currentRot = Quaternion.Euler(player?.serverInput?.current?.aimAngles ?? Vector3.zero) * Vector3.forward;
            var ray = new Ray((player?.eyes?.position ?? Vector3.zero), currentRot);
            if (UnityEngine.Physics.Raycast(ray, out hit, maxDist, ((coll != -1) ? coll : layers)))
            {
                var ent = hit.GetEntity() ?? null;
                if (debug)
                {
                    Vector3 ent_pos = ent.transform.position;
                    Vector3 RayPos = ray.GetPoint(1f);
                    SendReply(player, "Ray Pos: " + RayPos + " Ent pos: " + ent_pos);
                    player.SendConsoleCommand("ddraw.arrow", 10f, Color.blue, player.eyes.position, hit.point, 0.1f);
                    player.SendConsoleCommand("ddraw.sphere", 10f, Color.green, ent.transform.position, 1f);
                    player.SendConsoleCommand("ddraw.sphere", 10f, Color.blue, hit.point, 0.1f);
                }
                if (ent != null && !(ent?.IsDestroyed ?? true)) return ent;
            }
            return null;
        }

        #endregion
        #region commands

        [ConsoleCommand("wipedoordata")]
        void nightdoorwipeccmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            storedData.IDlist.Clear();
            storedData.TimeEntries.Clear();
            SaveFile();
            Puts(lang.GetMessage("ManualWipe_NEW", this));
        }

        [ChatCommand("timeperiod")]
        void timeCmd(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "nightdoor.createinterval"))
            {
                SendReply(player, lang.GetMessage("NoPerm_NEW", this, player.UserIDString));
                return;
            }

            if (!useMulti)
            {
                SendReply(player, lang.GetMessage("TimeIntervalDisabled_NEW", this, player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, lang.GetMessage("TimeIntervalUsage_NEW", this, player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "create":
                    {
                        if (args.Length < 4)
                        {
                            SendReply(player, lang.GetMessage("timeintervalAddWrong_NEW", this, player.UserIDString));
                            return;
                        }

                        string Inputname = args[1];
                        string time1 = args[2];
                        string time2 = args[3];

                        foreach (var timeEntry in storedData.TimeEntries)
                        {
                            if (timeEntry.name.ToLower() == Inputname.ToLower())
                            {
                                SendReply(player, lang.GetMessage("TimeIntervalExists_NEW", this, player.UserIDString));
                                return;
                            }
                        }

                        //Most complicated methods VVVVV

                        if (!time1.Contains(":") && !time2.Contains(":"))
                        {
                            SendReply(player, lang.GetMessage("timeintervalAddWrong_NEW", this, player.UserIDString));
                            return;
                        }

                        string[] split1, split2;
                        split2 = time2.Split(':');
                        split1 = time1.Split(':');
                        string[] allNumbers = { split1[0], split1[1], split2[0], split2[1], };
                        foreach (string p in allNumbers)
                        {
                            foreach (var ew in p)
                            {
                                if (ew == '0' || ew == '1' || ew == '2' || ew == '3' || ew == '4' || ew == '5' || ew == '6' || ew == '7' || ew == '8' || ew == '9') continue;

                                else
                                {
                                    SendReply(player, lang.GetMessage("timeintervalAddWrong_NEW", this, player.UserIDString));
                                    return;
                                }
                            }

                            if (p.Length != 2)
                            {
                                SendReply(player, lang.GetMessage("timeintervalAddWrong_NEW", this, player.UserIDString));
                                return;
                            }
                        }

                        float a = GetFloat(time1);
                        float b = GetFloat(time2);

                        if (a > b)
                        {
                            SendReply(player, lang.GetMessage("HowTo26Hour", this, player.UserIDString));
                            return;
                        }

                        TimeInfo entry = new TimeInfo(Inputname, time1, time2);
                        storedData.TimeEntries.Add(entry);
                        SendReply(player, lang.GetMessage("CreatedEntry_NEW", this, player.UserIDString), Inputname, time1, time2);
                        SaveFile();
                        return;
                    }

                case "remove":
                    {
                        if (args.Length < 2)
                        {
                            SendReply(player, lang.GetMessage("timeintervalRemoveWrong_NEW", this, player.UserIDString));
                            return;
                        }

                        string Inputname = args[1];
                        List<TimeInfo> toRemove = new List<TimeInfo>();
                        List<uint> idList = new List<uint>();

                        foreach (var entry in storedData.TimeEntries)
                        {
                            if (entry.name == Inputname && entry.name != "default")
                            {
                                toRemove.Add(entry);
                                foreach (var ent in storedData.IDlist)
                                {
                                    if(ent.Value == Inputname)
                                    {
                                        idList.Add(ent.Key);
                                        BaseNetworkable entity = BaseNetworkable.serverEntities.Find(ent.Key);
                                        SendReply(player, lang.GetMessage("Timeperiod_removed", this, player.UserIDString), entity.transform.position.ToString());
                                        player.SendConsoleCommand("ddraw.sphere", 10f, Color.green, entity.transform.position, 1f);
                                    }
                                }

                                SendReply(player, lang.GetMessage("RemovedEntry_NEW", this, player.UserIDString), entry.name);
                                return;
                            }
                        }

                        foreach(var ent2 in toRemove) storedData.TimeEntries.Remove(ent2);
                        foreach (var ent3 in idList) storedData.IDlist.Remove(ent3);

                        SendReply(player, lang.GetMessage("NotFoundEntry_NEW", this, player.UserIDString), Inputname);
                        return;
                    }

                case "list":
                    {
                        string text = "";
                        List<string> L = new List<string>();
                        if (storedData.TimeEntries.Count < 1)
                        {
                            SendReply(player, lang.GetMessage("NoTimeIntervals_NEW", this, player.UserIDString));
                            return;
                        }

                        foreach (var entry in storedData.TimeEntries)
                        {
                            if (entry.name == "default") text = entry.name + " (" + entry.start + " - " + entry.end + ") (updates on plugin startup if changed)";
                            else text = entry.name + " (" + entry.start + " - " + entry.end + ")";
                            L.Add(text);
                        }

                        string finished = string.Join("\n", L.ToArray());
                        SendReply(player, lang.GetMessage("ListOfTimeIntervals_NEW", this, player.UserIDString), finished);
                        return;
                    }

                default:
                    {
                        SendReply(player, lang.GetMessage("timeintervalAddWrong_NEW", this, player.UserIDString));
                        return;
                    }
            }

        }

        [ChatCommand("nd")]
        void nightdoorcmd(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "nightdoor.use"))
            {
                SendReply(player, lang.GetMessage("NoPerm_NEW", this, player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {

                SendReply(player, lang.GetMessage("WrongSyntax_NEW", this, player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                    {
                        BaseEntity ent = GetLookAtEntity(player, 10f, layers);

                        if (!(ent is Door))
                        {
                            SendReply(player, lang.GetMessage("NotDoor_NEW", this, player.UserIDString));
                            return;
                        }

                        if (storedData.IDlist.ContainsKey(ent.net.ID))
                        {
                            SendReply(player, lang.GetMessage("AlreadyOnList_NEW", this, player.UserIDString));
                            return;
                        }

                        if (args.Length < 2)
                        {
                            storedData.IDlist.Add(ent.net.ID, "default");
                            SendReply(player, lang.GetMessage("AddedDoor_NEW", this, player.UserIDString));
                            SaveFile();
                            return;
                        }

                        string tiName = args[1];
                        if (string.IsNullOrEmpty(tiName))
                        {
                            SendReply(player, lang.GetMessage("ndMultiMsg_NEW", this, player.UserIDString));
                            return;
                        }

                        foreach (var entry in storedData.TimeEntries)
                        {
                            if (entry.name.ToLower() == tiName.ToLower())
                            {
                                storedData.IDlist.Add(ent.net.ID, entry.name);
                                SendReply(player, lang.GetMessage("AddedDoorCustom_NEW", this, player.UserIDString), entry.name, entry.start, entry.end);
                                SaveFile();
                                return;
                            }
                        }
                        SendReply(player, lang.GetMessage("NotFoundEntry_NEW", this, player.UserIDString), tiName);
                        return;
                    }

                case "remove":
                    {
                        BaseEntity ent = GetLookAtEntity(player, 10f, layers);

                        if (!(ent is Door))
                        {
                            SendReply(player, lang.GetMessage("NotDoor_NEW", this, player.UserIDString));
                            return;
                        }

                        if (!storedData.IDlist.ContainsKey(ent.net.ID))
                        {
                            SendReply(player, lang.GetMessage("NotOnList_NEW", this, player.UserIDString));
                            return;
                        }

                        storedData.IDlist.Remove(ent.net.ID);
                        SendReply(player, lang.GetMessage("RemovedDoor_NEW", this, player.UserIDString));
                        SaveFile();
                        return;
                    }

                case "info":
                    {
                        BaseEntity ent = GetLookAtEntity(player, 10f, layers);

                        if (!(ent is Door))
                        {
                            SendReply(player, lang.GetMessage("NotDoor_NEW", this, player.UserIDString));
                            return;
                        }

                        if (!storedData.IDlist.ContainsKey(ent.net.ID))
                        {
                            SendReply(player, lang.GetMessage("NotOnList_NEW", this, player.UserIDString));
                            return;
                        }

                        string type = storedData.IDlist[ent.net.ID];
                        string start = "", end = "";

                        if (type == "default")
                        {
                            string txt = "default (config time)";
                            string p1, p2, p3, p4;
                            p1 = GetDateTime(startTime).Hour.ToString();
                            p2 = GetDateTime(startTime).Minute.ToString();
                            p3 = GetDateTime(endTime).Hour.ToString();
                            p4 = GetDateTime(endTime).Minute.ToString();
                            if (p1 == "0") p1 = "00";
                            if (p2 == "0") p2 = "00";
                            if (p3 == "0") p3 = "00";
                            if (p4 == "0") p4 = "00";
                            start = p1 + ":" + p2;
                            end = p3 + ":" + p4;
                            SendReply(player, lang.GetMessage("DoorInfo_NEW", this, player.UserIDString), txt, start, end);
                            return;
                        }

                        foreach (var entry in storedData.TimeEntries)
                        {
                            if (entry.name == type)
                            {
                                start = entry.start;
                                end = entry.end;
                            }
                        }

                        SendReply(player, lang.GetMessage("DoorInfo_NEW", this, player.UserIDString), type, start, end);
                        return;
                    }

                case "show":
                    {
                        if (storedData.IDlist.Count == 0)
                        {
                            SendReply(player, lang.GetMessage("NoDoorsCurrently_NEW", this, player.UserIDString));
                            return;
                        }

                        SendReply(player, lang.GetMessage("ShowingDoor_NEW", this, player.UserIDString));
                        foreach (var entry in storedData.IDlist)
                        {
                            BaseNetworkable ent = BaseNetworkable.serverEntities.Find(entry.Key);
                            if(ent == null)
                            {
                                CheckAllEntites();
                                continue;
                            }
                            Vector3 pos = ent.transform.position;
                            pos.y += 1f;
                            player.SendConsoleCommand("ddraw.sphere", 10f, Color.green, pos, 1f);
                        }
                        return;
                    }

                case "help":
                    {
                        string start, end;
                        string p1, p2, p3, p4;
                        p1 = GetDateTime(startTime).Hour.ToString();
                        p2 = GetDateTime(startTime).Minute.ToString();
                        p3 = GetDateTime(endTime).Hour.ToString();
                        p4 = GetDateTime(endTime).Minute.ToString();
                        if (p1 == "0") p1 = "00";
                        if (p2 == "0") p2 = "00";
                        if (p3 == "0") p3 = "00";
                        if (p4 == "0") p4 = "00";
                        start = p1 + ":" + p2;
                        end = p3 + ":" + p4;

                        SendReply(player, lang.GetMessage("HelpMsg_NEW", this, player.UserIDString), start, end);
                        return;
                    }

                case "debug":
                    {
                        if (!debug)
                        {
                            SendReply(player, lang.GetMessage("debugging_disabled", this, player.UserIDString));
                            return;
                        }

                        BaseEntity ent = GetLookAtEntity(player, 10f, layers);

                        if (ent == null || ent.IsDestroyed)
                        {
                            SendReply(player, lang.GetMessage("debugging_null", this, player.UserIDString));
                            return;
                        }

                        SendReply(player, lang.GetMessage("debugging_hit", this, player.UserIDString), ent.ShortPrefabName);

                        bool isInList = storedData.IDlist.ContainsKey(ent.net.ID);
                        string type = "";

                        if (storedData.IDlist.ContainsKey(ent.net.ID)) type = storedData.IDlist[ent.net.ID];
                        if (string.IsNullOrEmpty(type)) type = "none";

                        SendReply(player, lang.GetMessage("debugging_list", this, player.UserIDString), isInList);

                        SendReply(player, lang.GetMessage("debugging_entID", this, player.UserIDString), ent.net.ID);

                        SendReply(player, lang.GetMessage("debugging_period", this, player.UserIDString), type);

                        if (!(ent is Door)) SendReply(player, lang.GetMessage("NotDoor_NEW", this, player.UserIDString));

                        player.SendConsoleCommand("ddraw.sphere", 10f, Color.green, ent.transform.position, 1f);
                        return;
                    }

                default:
                    {
                        SendReply(player, lang.GetMessage("WrongSyntax_NEW", this, player.UserIDString));
                        return;
                    }
            }
        }
        #endregion
    }
}