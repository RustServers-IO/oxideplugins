using System.Collections.Generic;
using System;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using UnityEngine;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using static UnityEngine.Vector3;

namespace Oxide.Plugins
{
    [Info("Npctp", "Ts3hosting", "2.2.5", ResourceId = 2229)]
    [Description("Some NPC Controle")]

    class Npctp : RustPlugin
    {
        #region Initialization


        [PluginReference]
        Plugin Spawns;
        [PluginReference]
        Plugin Economics;


        PlayerCooldown pcdData;
        private DynamicConfigFile PCDDATA;

        private static int cooldownTime = 3600;
        private static int cooldownTime2 = 3600;
        private static int cooldownTime3 = 3600;
        private static int cooldownTime4 = 60;
        private static bool useEconomics = true;
        private static int buyMoney = 500;
        private static int buyMoney1 = 500;
        private static int buyMoney2 = 500;
        private static int buyMoney3 = 500;


        private string setx;
        private string sety;
        private string setz;



        private static int auth = 2;
        private static bool noAdminCooldown = false;


        private bool Changed;
        private string text;
        private bool displayoneveryconnect;

        private string NpcID;
        private string NpcID1;
        private string NpcID2;
        private string NpcID3;


        private string SpawnsFile;
        private string SpawnsFile1;


        private string command;
        private string arrangements;



        private string CanUse = "false";
        private string CanUse1 = "false";
        private string CanUse2 = "false";
        private string CanUse3 = "false";



        #region Localization       
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "<color=orange>Npc</color> : "},
            {"cdTime", "You must wait another {0} minutes and some seconds before using me again" },
            {"noperm", "You do not have permissions to talk to me!" },
            {"notenabled", "Sorry i am not enabled!" },
            {"nomoney", "Sorry you need {0} to talk to me!" },
            {"charged", "Thanks i only took {0} from you!" },
            {"npcCommand", "I gave you a little gift!" }





       };
        #endregion


        void Loaded()
        {
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile("NpcTP_data");
            LoadData();
            LoadVariables();
            RegisterPermissions();
            lang.RegisterMessages(messages, this);
            Puts("Thanks for using NPCTP drop me a line if you need anything added.");


        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission("npctp.admin", this);
            permission.RegisterPermission("npctp.one", this);
            permission.RegisterPermission("npctp.two", this);
            permission.RegisterPermission("npctp.command", this);
            permission.RegisterPermission("npctp.name", this);

        }

        private void CheckDependencies()
        {
            if (Economics == null)
                if (useEconomics)
                {
                    PrintWarning($"Economics could not be found! Disabling money feature");
                    useEconomics = false;
                }
        }


        void LoadVariables()
        {

            useEconomics = Convert.ToBoolean(GetConfig("SETTINGS", "useEconomics", false));

            NpcID = Convert.ToString(GetConfig("NpcOne", "NpcID", "123456789"));
            buyMoney1 = Convert.ToInt32(GetConfig("NpcOne", "Money", "500"));
            CanUse = Convert.ToString(GetConfig("NpcOne", "EnableNpcOne", "false"));
            SpawnsFile = Convert.ToString(GetConfig("NpcOne", "SpawnsFile", "spawnfile"));
            cooldownTime = Convert.ToInt32(GetConfig("NpcOne", "cooldownTime", "3600"));


            NpcID1 = Convert.ToString(GetConfig("NpcTwo", "NpcID", "123456789"));
            buyMoney = Convert.ToInt32(GetConfig("NpcTwo", "Money", "500"));
            CanUse1 = Convert.ToString(GetConfig("NpcTwo", "EnableNpcTwo", "false"));
            SpawnsFile1 = Convert.ToString(GetConfig("NpcTwo", "SpawnsFile", "spawnfile"));
            cooldownTime2 = Convert.ToInt32(GetConfig("NpcTwo", "cooldownTime", "3600"));

            NpcID2 = Convert.ToString(GetConfig("NpcCommand", "NpcID", "123456789"));
            buyMoney2 = Convert.ToInt32(GetConfig("NpcCommand", "Money", "500"));
            CanUse2 = Convert.ToString(GetConfig("NpcCommand", "EnableNpcCommand", "false"));
            command = Convert.ToString(GetConfig("NpcCommand", "Command", "inv.giveplayer"));
            arrangements = Convert.ToString(GetConfig("NpcCommand", "Arrangements", "kit MVP"));
            cooldownTime3 = Convert.ToInt32(GetConfig("NpcCommand", "cooldownTime", "86400"));

            NpcID3 = Convert.ToString(GetConfig("NpcNAME", "NpcNAME", "GoToTown"));
            buyMoney3 = Convert.ToInt32(GetConfig("NpcNAME", "Money", "500"));
            CanUse3 = Convert.ToString(GetConfig("NpcNAME", "EnableNpcName", "false"));
            setx = Convert.ToString(GetConfig("NpcNAME", "set x", "100"));
            sety = Convert.ToString(GetConfig("NpcNAME", "set y", "50"));
            setz = Convert.ToString(GetConfig("NpcNAME", "set z", "100"));
            cooldownTime4 = Convert.ToInt32(GetConfig("NpcNAME", "cooldownTime", "360"));



            if (Changed)
            {
                SaveConfig();
                Changed = false;

            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file!");
            Config.Clear();
            LoadVariables();
        }



        #endregion




        #region Classes and Data Management       
        class PlayerCooldown
        {
            public Dictionary<ulong, PCDInfo> pCooldown = new Dictionary<ulong, PCDInfo>();


            public PlayerCooldown() { }
        }
        class PCDInfo
        {
            public long Cooldown;
            public long Cooldown2;
            public long Cooldown3;
            public long Cooldown4;
            public PCDInfo() { }
            public PCDInfo(long cd)
            {
                Cooldown = cd;
                Cooldown2 = cd;
                Cooldown3 = cd;
                Cooldown4 = cd;




            }
        }

        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }
        void LoadData()
        {
            try
            {
                pcdData = Interface.GetMod().DataFileSystem.ReadObject<PlayerCooldown>("NpcTP_data");
            }
            catch
            {
                Puts("Couldn't load NPCTP data, creating new datafile");
                pcdData = new PlayerCooldown();
            }
        }

        #endregion


        #region Cooldown Management       

        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;


        #endregion


        private bool CheckPlayerMoney(BasePlayer player, int amount)
        {
            if (useEconomics)
            {
                double money = (double)Economics?.CallHook("GetPlayerMoney", player.userID);
                if (money >= amount)
                {
                    money = money - amount;
                    Economics?.CallHook("Set", player.userID, money);
                    SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("charged", this, player.UserIDString), (int)(amount)));
                    return true;
                }
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("nomoney", this, player.UserIDString), (int)(amount)));

            }
            return false;
        }










        #region USENPC



        void OnUseNPC(BasePlayer npc, BasePlayer player, Vector3 destination)
        {
            ulong playerID = player.userID;
            string lowername = npc.userID.ToString();
            string lowername2 = npc.displayName;

            var d = pcdData.pCooldown;
            ulong ID = player.userID;
            double timeStamp = GrabCurrentTime();


            string lowernpc = NpcID.ToString();
            string lowernpc1 = NpcID1.ToString();
            string lowernpc2 = NpcID2.ToString();
            string lowernpc3 = NpcID3;

            string x = setx;
            string y = sety;
            string z = setz;



            object newpos = null;
            string spawn = SpawnsFile;
            string spawn1 = SpawnsFile1;


            if (!d.ContainsKey(ID))
            {
                d.Add(ID, new PCDInfo((long)timeStamp));
                SaveData();
            }





            if (lowernpc == lowername)
            {

                if (CanUse == "false")
                {
                    SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("notenabled", this)));
                    return;
                }
                else
                {

                    long time = d[ID].Cooldown;
                    if (!permission.UserHasPermission(player.userID.ToString(), "npctp.one"))
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("noperm", this)));
                        return;
                    }
                    if (time > timeStamp && time != 0.0)
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("cdTime", this, player.UserIDString), (int)(time - timeStamp) / 60));
                        return;
                    }
                    else if (!useEconomics)
                    {
                        d[ID].Cooldown = (long)timeStamp + cooldownTime;
                        SaveData();
                        object success = Spawns.Call("GetRandomSpawn", spawn);
                        if (success is Vector3) // Check if the returned type is Vector3
                        {
                            Vector3 location = (Vector3)success;
                            rust.RunServerCommand($"teleport.topos {player.userID} {location.x} {location.y} {location.z}");
                        }
                        else PrintError((string)newpos); // Otherwise print the error message to console so server owners know there is a problem

                    }

                    if (useEconomics)
                        if (CheckPlayerMoney(player, buyMoney1))
                        {
                            d[ID].Cooldown = (long)timeStamp + cooldownTime;
                            SaveData();
                            object success = Spawns.Call("GetRandomSpawn", spawn);
                            if (success is Vector3) // Check if the returned type is Vector3
                            {
                                Vector3 location = (Vector3)success;
                                rust.RunServerCommand($"teleport.topos {player.userID} {location.x} {location.y} {location.z}");
                            }
                            else PrintError((string)newpos); // Otherwise print the error message to console so server owners know there is a problem

                        }


                }
            }


            if (lowernpc1 == lowername)
            {
                if (CanUse1 == "false")
                {
                    SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("notenabled", this)));
                    return;
                }
                else
                {

                    long time = d[ID].Cooldown2;
                    if (!permission.UserHasPermission(player.userID.ToString(), "npctp.two"))
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("noperm", this)));
                        return;
                    }
                    if (time > timeStamp && time != 0.0)
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("cdTime", this, player.UserIDString), (int)(time - timeStamp) / 60));
                        return;
                    }
                    else if (!useEconomics)
                    {
                        object success = Spawns.Call("GetRandomSpawn", spawn1);
                        if (success is Vector3) // Check if the returned type is Vector3
                        {
                            Vector3 location = (Vector3)success;
                            rust.RunServerCommand($"teleport.topos {player.userID} {location.x} {location.y} {location.z}");
                        }
                        else PrintError((string)newpos); // Otherwise print the error message to console so server owners know there is a problem
                    }

                    if (useEconomics)
                        if (CheckPlayerMoney(player, buyMoney))
                        {
                            object success = Spawns.Call("GetRandomSpawn", spawn1);
                            if (success is Vector3) // Check if the returned type is Vector3
                            {
                                Vector3 location = (Vector3)success;
                                rust.RunServerCommand($"teleport.topos {player.userID} {location.x} {location.y} {location.z}");
                            }
                            else PrintError((string)newpos); // Otherwise print the error message to console so server owners know there is a problem
                        }

                }
            }


            if (lowernpc2 == lowername)
            {
                if (CanUse2 == "false")
                {
                    SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("notenabled", this)));
                    return;
                }
                else
                {
                    long time = d[ID].Cooldown3;
                    if (!permission.UserHasPermission(player.userID.ToString(), "npctp.command"))
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("noperm", this)));
                        return;
                    }
                    if (time > timeStamp && time != 0.0)
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("cdTime", this, player.UserIDString), (int)(time - timeStamp) / 60));
                        return;
                    }
                    else if (!useEconomics)
                    {
                        d[ID].Cooldown3 = (long)timeStamp + cooldownTime3;
                        SaveData();

                        rust.RunServerCommand($"{command} {player.userID} {arrangements}");

                    }

                    if (useEconomics)
                        if (CheckPlayerMoney(player, buyMoney2))
                        {
                            d[ID].Cooldown3 = (long)timeStamp + cooldownTime3;
                            SaveData();

                            rust.RunServerCommand($"{command} {player.userID} {arrangements}");
                            SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("npcCommand", this)));

                        }
                    }
                }

            if (lowernpc3 == lowername2)
            {
                if (CanUse3 == "false")
                {
                    SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("notenabled", this)));
                    return;
                }
                else
                {
                    long time = d[ID].Cooldown4;
                    if (!permission.UserHasPermission(player.userID.ToString(), "npctp.name"))
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("noperm", this)));
                        return;
                    }
                    if (time > timeStamp && time != 0.0)
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("cdTime", this, player.UserIDString), (int)(time - timeStamp) / 60));
                        return;
                    }
                    else if (!useEconomics)
                    {

                        d[ID].Cooldown4 = (long)timeStamp + cooldownTime4;
                        SaveData();

                        rust.RunServerCommand($"teleport.topos {player.userID} {x} {y} {z}");

                    }
                    if (useEconomics)
                        if (CheckPlayerMoney(player, buyMoney3))
                        {
                            d[ID].Cooldown4 = (long)timeStamp + cooldownTime4;
                            SaveData();

                            rust.RunServerCommand($"teleport.topos {player.userID} {x} {y} {z}");

                        }


                }


                #endregion


            }

        }

    }

}



