using ConVar;
using Oxide.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Night Door", "Slydelix", 1.1)]
    class NightDoor : RustPlugin
    {
        int layers = LayerMask.GetMask("Construction");
        bool BypassAdmin, BypassPerm;
        float startTime, endTime;

        protected override void LoadDefaultConfig()
        {
            Config["Allow admins to open time-limited door"] = BypassAdmin = GetConfig("Allow admins to open time-limited door", false);
            Config["Allow players with bypass permission to open time-limited door"] = BypassPerm = GetConfig("Allow players with bypass permission to open time-limited door", false);
            Config["Start time (if you want to be able to open doors from 16:30 you would put 16.5)"] = startTime = GetConfig("Start time (if you want to be able to open doors from 16:30 you would put 16.5)", 0f);
            Config["End time (if you want to be able to open doors until 20:30 you would put 20.5)"] = endTime = GetConfig("End time (if you want to be able to open doors until 20:30 you would put 20.5)", 0f);
            SaveConfig();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary <string, string>()
            {
                {"serverWipe", "Server wipe detected, wiping data file"},
                {"NotOpenable", "This door cannot be opened at this time"},
                {"CantPlace", "That codelock cannot be placed on this door"},
                {"ManualWipe", "Manually wiped Night Door data file"},
                {"NoPerm", "You don't have permission to use this command"},
                {"WrongSyntax", "Wrong syntax! /nd help for more info"},
                {"NotDoor", "The object you are looking at is not a door" },
                {"AlreadyOnList", "That door is already on the locked door list"},
                {"AddedDoor", "Added door to the night door list"},
                {"NotOnList", "That door isn't on the locked door list"},
                {"RemovedDoor", "Removed door from night door list"},
                {"HelpMsg", "To make a door openable during certain time period type\n<color=silver>/nd add</color> while looking at it\nIf you want to make the door normal again type\n<color=silver>/nd remove</color> while looking at it\nCurrent time period: {0} - {1}"},
            }, this, "en");
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
            permission.RegisterPermission("nightdoor.use", this);
            permission.RegisterPermission("nightdoor.bypass", this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("NightDoor");
        }

        void Unload() => SaveFile();

        void OnServerSave() => SaveFile();

        void OnNewSave(string filename)
        {
            Puts(lang.GetMessage("serverWipe", this));
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

        void OnDoorOpened(Door door, BasePlayer player)
        {
            if (storedData.IDlist.Contains(door.net.ID))
            {
                float ctime = Env.time;
                if (player.IsAdmin && BypassAdmin) return;
                if (permission.UserHasPermission(player.UserIDString, "nightdoor.bypass") && BypassPerm) return;
                if (Env.time >= startTime && Env.time <= endTime) return;
                door.CloseRequest();
                SendReply(player, lang.GetMessage("NotOpenable", this, player.UserIDString));
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
                    Item codelck = ItemManager.CreateByItemID(-975723312, 1);
                    player.GiveItem(codelck);
                    codelockent.Kill();
                    SendReply(player, lang.GetMessage("CantPlace", this, player.UserIDString));
                    return;
                }
            }
        }

        bool CanPickupEntity(BaseCombatEntity entity, BasePlayer player)
        {
            if (storedData.IDlist.Contains(entity.net.ID)) return false;
            else return true;
        }

        void SaveFile()
        {
            //storedData.IDlist.RemoveAll(p => !BaseEntity.saveList.Any(x =>
            Interface.Oxide.DataFileSystem.WriteObject("NightDoor", storedData);
        }

        [ConsoleCommand("wipedoordata")]
        void nightdoorwipeccmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            storedData.IDlist.Clear();
            SaveFile();
            Puts(lang.GetMessage("ManualWipe", this));
        }

        [ChatCommand("nd")]
        void nightdoorcmd(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "nightdoor.use"))
            {
                SendReply(player, lang.GetMessage("NoPerm", this, player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, lang.GetMessage("WrongSyntax", this, player.UserIDString));
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
                            SendReply(player, lang.GetMessage("NotDoor", this, player.UserIDString));
                            return;
                        }

                        if (storedData.IDlist.Contains(ent.net.ID))
                        {
                            SendReply(player, lang.GetMessage("AlreadyOnList", this, player.UserIDString));
                            return;
                        }

                        storedData.IDlist.Add(ent.net.ID);
                        SendReply(player, lang.GetMessage("AddedDoor", this, player.UserIDString));
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
                            SendReply(player, lang.GetMessage("NotDoor", this, player.UserIDString));
                            return;
                        }

                        if (!storedData.IDlist.Contains(ent.net.ID))
                        {
                            SendReply(player, lang.GetMessage("NotOnList", this, player.UserIDString));
                            return;
                        }

                        storedData.IDlist.Remove(ent.net.ID);
                        SendReply(player, lang.GetMessage("RemovedDoor", this, player.UserIDString));
                        SaveFile();
                        return;
                    }

                case "help":
                    {
                        int iTimeStart = (int)startTime;
                        float dTimeStart = (startTime - iTimeStart) * 60;
                        string sTimeStart = iTimeStart + ":" + dTimeStart;

                        int iTimeEnd = (int)endTime;
                        float dTimeEnt = (endTime - iTimeEnd) * 60;
                        string sTimeEnd = iTimeEnd + ":" + dTimeEnt;
                        SendReply(player, lang.GetMessage("HelpMsg", this, player.UserIDString), sTimeStart, sTimeEnd);
                        return;
                    }

                default:
                    {
                        SendReply(player, lang.GetMessage("WrongSyntax", this, player.UserIDString));
                        return;
                    }
            }


        }
    }
}