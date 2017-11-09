using Oxide.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Night Door", "Slydelix", 1.4, ResourceId = 2684)]
    class NightDoor : RustPlugin
    {
        int layers = LayerMask.GetMask("Construction");
        bool BypassAdmin, BypassPerm, UseRealTime, AutoDoor;
        string startTime, endTime;

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
            Config["End time (HH:mm)"] = endTime = GetConfig("End time (HH:mm)", "00:00");
            Config["Use system (real) time"] = UseRealTime = GetConfig("Use system (real) time", false);
            Config["Automatic door closing/opening"] = AutoDoor = GetConfig("Automatic door closing/opening", false);
            SaveConfig();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"serverWipe1", "Server wipe detected, wiping data file"},
                {"NotOpenable1", "This door/hatch/gate cannot be opened at this time"},
                {"CantPlace1", "The code lock cannot be placed on this door/hatch/gate"},
                {"ManualWipe1", "Manually wiped Night Door data file"},
                {"NoPerm1", "You don't have permission to use this command"},
                {"WrongSyntax1", "Wrong syntax! /nd help for more info"},
                {"NotDoor1", "The object you are looking at is not a door/hatch/gate" },
                {"AlreadyOnList1", "That door/hatch/gate is already time locked"},
                {"Start>End1", "Config seems to be set up incorrectly! (Start time value is bigger than end time value)"},
                {"Start&EndValIs01", "Detected 00:00 as both end and start time! Change this to values you want"},
                {"NoDoorsCurrently1", "No time locked doors/hatches/gates set up yet"},
                {"ShowingDoor1", "Showing all time locked doors/hatches/gates"},
                {"AddedDoor1", "This door/hatch/gate is now time locked"},
                {"NotOnList1", "That door/hatch/gate isn't time locked"},
                {"RemovedDoor1", "This door/hatch/gate is not time locked anymore"},
                {"HelpMsg1", "To make a door/hatch/gate openable during certain time period type\n<color=silver>/nd add</color> while looking at it\nIf you want to make the door normal again type\n<color=silver>/nd remove</color> while looking at it\nTo see all doors/hatches/gates that are time locked type\n<color=silver>/nd show</color>\nCurrent time period: {0} - {1}"},
            }, this);
        }

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        class StoredData
        {
            public List<uint> IDlist = new List<uint>();

            public StoredData()
            {
            }
        }

        StoredData storedData;

        void Init()
        {
            LoadDefaultConfig();
            CheckDoors();
            permission.RegisterPermission("nightdoor.use", this);
            permission.RegisterPermission("nightdoor.bypass", this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("NightDoor");
            if (GetDateTime(startTime) > GetDateTime(endTime)) PrintWarning(lang.GetMessage("Start>End1", this));
            if(startTime == "00:00" && endTime == "00:00") PrintWarning(lang.GetMessage("Start&EndValIs01", this));
        }

        void Unload() => SaveFile();

        void OnServerSave() => SaveFile();

        void OnNewSave(string filename)
        {
            Puts(lang.GetMessage("serverWipe1", this));
            storedData.IDlist.Clear();
            SaveFile();
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (storedData.IDlist.Contains(entity.net.ID))
            {
                storedData.IDlist.Remove(entity.net.ID);
                SaveFile();
                return;
            }
        }

        float GetFloat(string input)
        {
            float final = 0f;
            string[] parts = input.Split(':');
            string h, m;
            h = parts[0];
            m = parts[1];
            int minInt = Convert.ToInt32(m);
            int hourInt = Convert.ToInt32(h);
            float min = minInt / 60;
            final = hourInt + min;
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

        void CheckDoors()
        {

            if (!AutoDoor) return;

            DateTime start = GetDateTime(startTime);
            DateTime end = GetDateTime(endTime);
            float time;

            timer.Every(5f, () =>
            {
                time = ConVar.Env.time;
                foreach (var entry in storedData.IDlist)
                {
                    BaseEntity ent = BaseNetworkable.serverEntities.Find(entry) as BaseEntity;

                    if (ent == null || ent.IsDestroyed)
                    {
                        storedData.IDlist.Remove(entry);
                        continue;
                    }

                    if (UseRealTime)
                    {
                        if ((DateTime.Now >= start) && (DateTime.Now <= end))
                        {
                            ent.SetFlag(BaseEntity.Flags.Open, true);
                            ent.SendNetworkUpdateImmediate();
                        }

                        else
                        {
                            ent.SetFlag(BaseEntity.Flags.Open, false);
                            ent.SendNetworkUpdateImmediate();
                        }

                        continue;
                    }

                    if (time >= GetFloat(startTime) && time <= GetFloat(endTime))
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
            });
        }

        void OnDoorOpened(Door door, BasePlayer player)
        {
            if (storedData.IDlist.Contains(door.net.ID))
            {
                float time = ConVar.Env.time;
                if (player.IsAdmin && BypassAdmin) return;
                if (permission.UserHasPermission(player.UserIDString, "nightdoor.bypass") && BypassPerm) return;

                if (UseRealTime)
                {
                    DateTime start = GetDateTime(startTime);
                    DateTime end = GetDateTime(endTime);

                    if ((DateTime.Now >= start) && (DateTime.Now <= end)) return;
                }

                if (time >= GetFloat(startTime) && time <= GetFloat(endTime)) return;
                door.CloseRequest();
                SendReply(player, lang.GetMessage("NotOpenable1", this, player.UserIDString));
                return;
            }
        }

        void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            BasePlayer player = deployer.GetOwnerPlayer();

            if (entity is Door)
            {
                BaseEntity codelockent = entity.GetSlot(BaseEntity.Slot.Lock);

                if (storedData.IDlist.Contains(entity.net.ID))
                {
                    Item codelock;
                    if (codelockent.PrefabName == "assets/prefabs/locks/keypad/lock.code.prefab") codelock = ItemManager.CreateByName("lock.code", 1);
                    else codelock = ItemManager.CreateByName("lock.key", 1);
                    player.GiveItem(codelock);
                    codelockent.Kill();
                    SendReply(player, lang.GetMessage("CantPlace1", this, player.UserIDString));
                    return;
                }
            }
        }

        bool CanPickupEntity(BaseCombatEntity entity, BasePlayer player)
        {
            if (storedData.IDlist.Contains(entity.net.ID)) return false;
            else return true;
        }

        void SaveFile() => Interface.Oxide.DataFileSystem.WriteObject("NightDoor", storedData);

        [ConsoleCommand("wipedoordata")]
        void nightdoorwipeccmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            storedData.IDlist.Clear();
            SaveFile();
            Puts(lang.GetMessage("ManualWipe1", this));
        }

        [ChatCommand("nd")]
        void nightdoorcmd(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "nightdoor.use"))
            {
                SendReply(player, lang.GetMessage("NoPerm1", this, player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, lang.GetMessage("WrongSyntax1", this, player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                    {
                        RaycastHit hit;
                        if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hit, UnityEngine.Mathf.Infinity, layers)) return;
                        BaseEntity ent = hit.GetEntity();

                        if (!(ent is Door))
                        {
                            SendReply(player, lang.GetMessage("NotDoor1", this, player.UserIDString));
                            return;
                        }

                        if (storedData.IDlist.Contains(ent.net.ID))
                        {
                            SendReply(player, lang.GetMessage("AlreadyOnList1", this, player.UserIDString));
                            return;
                        }

                        storedData.IDlist.Add(ent.net.ID);
                        SendReply(player, lang.GetMessage("AddedDoor1", this, player.UserIDString));
                        SaveFile();
                        return;
                    }

                case "remove":
                    {
                        RaycastHit hit;
                        if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hit, UnityEngine.Mathf.Infinity, layers)) return;
                        BaseEntity ent = hit.GetEntity();

                        if (!(ent is Door))
                        {
                            SendReply(player, lang.GetMessage("NotDoor1", this, player.UserIDString));
                            return;
                        }

                        if (!storedData.IDlist.Contains(ent.net.ID))
                        {
                            SendReply(player, lang.GetMessage("NotOnList1", this, player.UserIDString));
                            return;
                        }

                        storedData.IDlist.Remove(ent.net.ID);
                        SendReply(player, lang.GetMessage("RemovedDoor1", this, player.UserIDString));
                        SaveFile();
                        return;
                    }

                case "show":
                    {
                        if(storedData.IDlist.Count == 0)
                        {
                            SendReply(player, lang.GetMessage("NoDoorsCurrently1", this, player.UserIDString));
                            return;
                        }

                        SendReply(player, lang.GetMessage("ShowingDoor1", this, player.UserIDString));
                        foreach(var entry in storedData.IDlist)
                        {
                            BaseNetworkable ent = BaseNetworkable.serverEntities.Find(entry);
                            Vector3 pos = ent.transform.position;
                            pos.y += 1f;
                            player.SendConsoleCommand("ddraw.sphere", 10f, Color.green, pos, 1f);
                        }
                        return;
                    }

                case "help":
                    {
                        SendReply(player, lang.GetMessage("HelpMsg1", this, player.UserIDString), startTime, endTime);
                        return;
                    }

                default:
                    {
                        SendReply(player, lang.GetMessage("WrongSyntax1", this, player.UserIDString));
                        return;
                    }
            }
        }
    }
}