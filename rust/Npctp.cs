using System.Collections.Generic;
using System;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using static UnityEngine.Vector3;
using Oxide.Game.Rust.Cui;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("Npctp", "Ts3hosting", "2.3.7", ResourceId = 2229)]
    [Description("Some NPC Controle Thanks Wulf and k1lly0u")]

    class Npctp : RustPlugin
    {
        #region Initialization


        [PluginReference]
        Plugin Spawns;
        [PluginReference]
        Plugin Economics;
        [PluginReference]
        Plugin ServerRewards;

        PlayerCooldown pcdData;
        NPCTPDATA npcData;
        private DynamicConfigFile PCDDATA;
        private DynamicConfigFile NPCDATA;

        private bool backroundimage;
        private string kickmsg;
        private string backroundimageurl;


        private static int cooldownTime = 3600;


        private static int auth = 2;
        private static bool noAdminCooldown = false;


        private bool Changed;
        private string text;
        private bool displayoneveryconnect;



        private float Cost = 0;
        private static bool useEconomics = false;
        private static bool useRewards = false;



        #region Localization       
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "<color=orange>Npc</color> : "},
            {"cdTime", "You must wait another {0} minutes and some seconds before using me again" },
            {"noperm", "You do not have permissions to talk to me!" },
            {"notenabled", "Sorry i am not enabled!" },
            {"nomoney", "Sorry you need {0} to talk to me!" },
            {"charged", "Thanks i only took {0} from you!" },
            {"npcCommand", "I just ran a Command!" },
            {"npcadd", "Added npcID {0} to datafile and not enabled edit NpcTP_Data ." },
            {"npcadds", "Added npcID {0} with spawnfile {1} to datafile and enabled edit NpcTP_Data for more options." },
            {"npcerror", "error example /npctp_add <npcID> <spawnfile> or /npctcp_add <npcID>" },
            {"nopermcmd", "You do not have permissions to use this command!" },
            {"commandyesno", "This will cost you " },
            {"commandyesno1", " do you want to pay?" },
            {"notfound", "The npc was not found check npcID" },
            {"npchelp", "Error: use /npctp <npcID>" },
			{"DeadCmd", "Sorry you can not kill me again that fast. Wait {0} seconds." }






       };
        #endregion


        void Loaded()
        {
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile("NpcTp/NpcTP_Player");
            NPCDATA = Interface.Oxide.DataFileSystem.GetFile("NpcTp/NpcTP_Data");
            LoadData();
            LoadVariables();
            RegisterPermissions();
            CheckDependencies();
            lang.RegisterMessages(messages, this);
            Puts("Thanks for using NPCTP drop me a line if you need anything added.");


        }

        void Unloaded()
        {
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(current, "Npctp");
            }
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
            permission.RegisterPermission("npctp.default", this);
            foreach (var perm in npcData.NpcTP.Values)
            {
                if (!string.IsNullOrEmpty(perm.permission) && !permission.PermissionExists(perm.permission))
                    permission.RegisterPermission(perm.permission, this);
            }
        }

        private void CheckDependencies()
        {
            if (Economics == null)
                if (useEconomics)
                {
                    PrintWarning($"Economics could not be found! Disabling money feature");
                    useEconomics = false;
                }
            if (ServerRewards == null)
                if (useRewards)
                {
                    PrintWarning($"ServerRewards could not be found! Disabling RP feature");
                    useRewards = false;
                }
            if (Spawns == null)
            {
                PrintWarning($"Spawns Database could not be found you only can use command NPC all other will fail!");

            }

        }


        void LoadVariables()
        {

            useEconomics = Convert.ToBoolean(GetConfig("SETTINGS", "useEconomics", false));
            useRewards = Convert.ToBoolean(GetConfig("SETTINGS", "useRewards", false));



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
        void SaveNpcTpData()
        {
            NPCDATA.WriteObject(npcData);
        }

        class NPCTPDATA
        {
            public Dictionary<string, NPCInfo> NpcTP = new Dictionary<string, NPCInfo>();

            public NPCTPDATA() { }
        }
        class NPCInfo
        {
            public string SpawnFile;
            public int Cooldown;
            public bool CanUse;
            public bool useUI;
            public float Cost;
            public string permission;
            public bool UseCommand;
            public bool CommandOnPlayer;
            public string Command;
            public string Arrangements;
            public bool useMessage;
            public string MessageNpc;
			public bool EnableDead;
            public bool DeadOnPlayer;
            public string DeadCmd;
            public string DeadArgs;
        }

        class PlayerCooldown
        {
            public Dictionary<ulong, PCDInfo> pCooldown = new Dictionary<ulong, PCDInfo>();


            public PlayerCooldown() { }
        }
        class PCDInfo
        {

            public Dictionary<string, long> npcCooldowns = new Dictionary<string, long>();

            public PCDInfo() { }
            public PCDInfo(long cd)
            {






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
                pcdData = Interface.GetMod().DataFileSystem.ReadObject<PlayerCooldown>("NpcTp/NpcTP_Player");
            }
            catch
            {
                Puts("Couldn't load NPCTP data, creating new Playerfile");
                pcdData = new PlayerCooldown();
            }
            try
            {
                npcData = Interface.GetMod().DataFileSystem.ReadObject<NPCTPDATA>("NpcTp/NpcTP_Data");
            }
            catch
            {
                Puts("Couldn't load NPCTP data, creating new datafile");
                npcData = new NPCTPDATA();
            }
        }

        #endregion


        #region Cooldown Management       

        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;


        #endregion


        private void TeleportPlayerPosition1(BasePlayer player, Vector3 destination)
        {
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading", null, null, null, null, null);
            StartSleeping(player);
            player.MovePosition(destination);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }
        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }

        private bool CheckPlayerMoney(BasePlayer player, float amount)
        {
            if (useEconomics)
            {
                double money = (double)Economics?.CallHook("GetPlayerMoney", player.userID);
                if (money >= amount)
                {
                    money = money - amount;
                    Economics?.CallHook("Set", player.userID, money);
                    if (amount >= 1)
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("charged", this, player.UserIDString), (int)(amount)));
                        return true;
                    }
                    if (amount == 0)
                        return true;
                }
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("nomoney", this, player.UserIDString), (int)(amount)));

            }
            return false;
        }

        bool CheckPlayerRP(BasePlayer player, float amount)
        {
            if (useRewards)
            {
                var money = (int)ServerRewards.Call("CheckPoints", player.userID);
                if (money >= amount)
                {
                    ServerRewards.Call("TakePoints", player.userID, amount);
                    if (amount >= 1)
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("charged", this, player.UserIDString), (int)(amount)));
                    }
                    return true;
                }
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("nomoney", this, player.UserIDString), (int)(amount)));
            }
            return false;
        }

        #region USENPC

        [ChatCommand("npctp_add")]
        void cmdNpcAdD(BasePlayer player, string command, string[] args)
        {

            var n = npcData.NpcTP;
            string IDNPC = "";
            string SpawnFiles = "";
            if (!permission.UserHasPermission(player.userID.ToString(), "npctp.admin"))
            {
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("nopermcmd", this)));
                return;
            }
            if (args.Length == 2)
            {
                SpawnFiles = (args[1]);
            }

            var setup = new NPCInfo { Cost = Cost, CanUse = false, useUI = false, permission = "npctp.default", UseCommand = false, CommandOnPlayer = false, Command = "say", Arrangements = "this is a test", useMessage = false, MessageNpc = "none", EnableDead = false, DeadOnPlayer = false, DeadCmd = "jail.send", DeadArgs = "5" };
            var setups = new NPCInfo { Cost = Cost, CanUse = true, useUI = false, SpawnFile = SpawnFiles, permission = "npctp.default", UseCommand = false, CommandOnPlayer = false, Command = "say", Arrangements = "this is a test", useMessage = false, MessageNpc = "none", EnableDead = false, DeadOnPlayer = false, DeadCmd = "jail.send", DeadArgs = "5" };

            if (args.Length <= 0)
            {
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("npcerror", this, player.UserIDString)));
                return;
            }

            IDNPC = (args[0]);

            if (args.Length == 1)
            {
                if (!n.ContainsKey(IDNPC))
                    n.Add(IDNPC, setup);
                SaveNpcTpData();
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("npcadd", this, player.UserIDString), (string)(IDNPC)));
                return;
            }
            if (args.Length == 2)
            {
                if (!n.ContainsKey(IDNPC))
                    n.Add(IDNPC, setups);
                SaveNpcTpData();
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("npcadds", this, player.UserIDString), (string)(IDNPC), (string)(SpawnFiles)));
                return;
            }
        }


        [ChatCommand("npctp")]
        void cmdChatNPCEdit(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "npctp.admin"))
            {
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("nopermcmd", this)));
                return;
            }
            if (args.Length <= 0)
            {
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("npchelp", this, player.UserIDString)));
                return;
            }
            var n = npcData.NpcTP;
            string npcId = (args[0]);
            int newvalueint = 0;
            bool newbool = false;
            float newfloat = 0;
            string newvalue = "";

            if (args.Length == 1 && n.ContainsKey(npcId))
            {
                SendReply(player, "====== Settings ======");
                SendReply(player, "SpawnFile" + ": " + npcData.NpcTP[npcId].SpawnFile);
                SendReply(player, "Cost" + ": " + npcData.NpcTP[npcId].Cost);
                SendReply(player, "CanUse" + ": " + npcData.NpcTP[npcId].CanUse);
                SendReply(player, "useUI" + ": " + npcData.NpcTP[npcId].useUI);
                SendReply(player, "permission" + ": " + npcData.NpcTP[npcId].permission);
                SendReply(player, "UseCommand" + ": " + npcData.NpcTP[npcId].UseCommand);
                SendReply(player, "CommandOnPlayer" + ": " + npcData.NpcTP[npcId].CommandOnPlayer);
                SendReply(player, "Command" + ": " + npcData.NpcTP[npcId].Command);
                SendReply(player, "Arrangements" + ": " + npcData.NpcTP[npcId].Arrangements);
                SendReply(player, "useMessage" + ": " + npcData.NpcTP[npcId].useMessage);
                SendReply(player, "MessageNpc" + ": " + npcData.NpcTP[npcId].MessageNpc);
				SendReply(player, "EnableDead" + ": " + npcData.NpcTP[npcId].EnableDead);
				SendReply(player, "DeadOnPlayer" + ": " + npcData.NpcTP[npcId].DeadOnPlayer);
				SendReply(player, "DeadCmd" + ": " + npcData.NpcTP[npcId].DeadCmd);
				SendReply(player, "DeadArgs" + ": " + npcData.NpcTP[npcId].DeadArgs);
                SendReply(player, "====== End Settings ======");
                SendReply(player, "To change /npctp_edit" + " " + npcId + " " + "<setting>" + " " + "<NewValue>");
                return;
            }

            if (!n.ContainsKey(npcId))
            {
                SendReply(player, string.Format(lang.GetMessage("notfound", this)));
                return;
            }
            string change = (args[1]).ToLower();
            if (args.Length >= 3 && change == "cooldown")
            {
                newvalue = (args[2]);
                newvalueint = Convert.ToInt32(newvalue);
                npcData.NpcTP[npcId].Cooldown = newvalueint;
                SaveNpcTpData();
                SendReply(player, "Cooldown value changed to {0}", newvalueint);
                return;
            }
            if (args.Length >= 3 && change == "spawnfile")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].SpawnFile = newvalue;
                SaveNpcTpData();
                SendReply(player, "SpawnFile value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "permission")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].permission = newvalue;
                SaveNpcTpData();
                SendReply(player, "permission value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "command")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].Command = newvalue;
                SaveNpcTpData();
                SendReply(player, "Coommand value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "arrangements")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].Arrangements = newvalue;
                SaveNpcTpData();
                SendReply(player, "Arrangements value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "messagenpc")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].MessageNpc = newvalue;
                SaveNpcTpData();
                SendReply(player, "MessageNpc value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "deadcmd")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].DeadCmd = newvalue;
                SaveNpcTpData();
                SendReply(player, "DeadCmd value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "deadargs")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].DeadArgs = newvalue;
                SaveNpcTpData();
                SendReply(player, "DeadArgs value changed to {0}", newvalue);
                return;
            }			
            if (args.Length >= 3 && change == "canuse")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].CanUse = newbool;
                SaveNpcTpData();
                SendReply(player, "canUse value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "useui")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].useUI = newbool;
                SaveNpcTpData();
                SendReply(player, "useUI value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "usecommand")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].UseCommand = newbool;
                SaveNpcTpData();
                SendReply(player, "UseCommand value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "commandonplayer")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].CommandOnPlayer = newbool;
                SaveNpcTpData();
                SendReply(player, "CommandOnPlayer value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "usemessage")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].useMessage = newbool;
                SaveNpcTpData();
                SendReply(player, "useMessage value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "enabledead")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].EnableDead = newbool;
                SaveNpcTpData();
                SendReply(player, "EnableDead value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "deadonplayer")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].DeadOnPlayer = newbool;
                SaveNpcTpData();
                SendReply(player, "DeadOnPlayer value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }						
            if (args.Length >= 3 && change == "cost")
            {
                newvalue = (args[2]);
                newfloat = float.Parse(newvalue);
                npcData.NpcTP[npcId].Cost = newfloat;
                SaveNpcTpData();
                SendReply(player, "Cost value changed to {0}", newfloat);
                return;
            }
        }



        void OnKillNPC(BasePlayer npc, HitInfo hinfo)
		{
			
           if (!npcData.NpcTP.ContainsKey(npc.UserIDString)) return; // Check if this NPC is registered
           var attacker = hinfo.Initiator as BasePlayer;
		   if (attacker == null) return;
		   
            var player = hinfo.Initiator.ToPlayer();
			ulong playerId = player.userID;
            string npcId = npc.UserIDString;
			var EnableDead = npcData.NpcTP[npcId].EnableDead;
			var DeadOnPlayer = npcData.NpcTP[npcId].DeadOnPlayer;
            string DeadCmd = npcData.NpcTP[npcId].DeadCmd;
			string DeadArgs = npcData.NpcTP[npcId].DeadArgs;
			double timeStamp = GrabCurrentTime();
			var cooldownTime = npcData.NpcTP[npcId].Cooldown;
			if (!EnableDead) return;
            if (!pcdData.pCooldown.ContainsKey(playerId))
            {
                pcdData.pCooldown.Add(playerId, new PCDInfo());
                //SaveData();
            }
                if (pcdData.pCooldown[playerId].npcCooldowns.ContainsKey(npcId)) // Check if the player already has a cooldown for this NPC
                {
                    var cdTime = pcdData.pCooldown[playerId].npcCooldowns[npcId]; // Get the cooldown time of the NPC
                    if (cdTime > timeStamp)
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("DeadCmd", this, player.UserIDString), (int)(cdTime - timeStamp)));
                        return;
                    }
                }

				  
			  if (EnableDead)
                if (!DeadOnPlayer) // Check if this is command on player
                {
					pcdData.pCooldown[playerId].npcCooldowns[npcId] = (long)timeStamp + cooldownTime; // Store the new cooldown in the players data under the specified NPC
                    SaveData();
                    rust.RunServerCommand($"{DeadCmd} {DeadArgs}");					

                }
			
			    if (DeadOnPlayer) // Check if this is command on player
                {
                    pcdData.pCooldown[playerId].npcCooldowns[npcId] = (long)timeStamp + cooldownTime; // Store the new cooldown in the players data under the specified NPC
                    SaveData();					
                    rust.RunServerCommand($"{DeadCmd} {playerId} {DeadArgs}");  

                }
		      }



        [ConsoleCommand("hardestcommandtoeverguessnpctp")]
        void cmdRun(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            var npcId = arg.Args[1];
            string spawn = npcData.NpcTP[npcId].SpawnFile;

            var cooldownTime = npcData.NpcTP[npcId].Cooldown;
            var UseCommand = npcData.NpcTP[npcId].UseCommand;
            ulong playerId = player.userID;
            string Command = npcData.NpcTP[npcId].Command;
            string Arrangements = npcData.NpcTP[npcId].Arrangements;
            var CommandOnPlayer = npcData.NpcTP[npcId].CommandOnPlayer;
            var buyMoney1 = npcData.NpcTP[npcId].Cost;
            var bad = "somthing is not right somewhere";
            double timeStamp = GrabCurrentTime();

            if (useEconomics)
            {
                double money = (double)Economics?.CallHook("GetPlayerMoney", player.userID);
                if (money < buyMoney1)
                {
                    SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("nomoney", this, player.UserIDString), (int)(buyMoney1)));
                    return;
                }

                if (money >= buyMoney1)
                {
                    money = money - buyMoney1;
                    Economics?.CallHook("Set", player.userID, money);
                    if (buyMoney1 >= 1)
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("charged", this, player.UserIDString), (int)(buyMoney1)));
                    }
                }
            }

            if (useRewards)
            {
                var money = (int)ServerRewards.Call("CheckPoints", player.userID);
                if (money < buyMoney1)
                {
                    SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("nomoney", this, player.UserIDString), (int)(buyMoney1)));
                    return;
                }

                if (money >= buyMoney1)
                {
                    ServerRewards.Call("TakePoints", player.userID, buyMoney1);
                    if (buyMoney1 >= 1)
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("charged", this, player.UserIDString), (int)(buyMoney1)));
                    }
                }
            }



            if (UseCommand == false)
            {

                pcdData.pCooldown[playerId].npcCooldowns[npcId] = (long)timeStamp + cooldownTime; // Store the new cooldown in the players data under the specified NPC
                SaveData();
                object success = Spawns.Call("GetRandomSpawn", spawn);
                if (success is Vector3) // Check if the returned type is Vector3
                {
                    Vector3 location = (Vector3)success;
                    TeleportPlayerPosition1(player, (Vector3)success);
                }
                else PrintError((string)bad); // Otherwise print the error message to console so server owners know there is a problem

            }
            if (UseCommand == true && CommandOnPlayer == false)
            {
                pcdData.pCooldown[playerId].npcCooldowns[npcId] = (long)timeStamp + cooldownTime;
                SaveData();

                if (!CommandOnPlayer) // Check if this is command on player
                {
                    rust.RunServerCommand($"{Command} {Arrangements}");
                    SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("npcCommand", this)));
                }
                else PrintError((string)bad); // Otherwise print the error message to console so server owners know there is a problem

            }
            if (UseCommand == true && CommandOnPlayer == true)
            {
                pcdData.pCooldown[playerId].npcCooldowns[npcId] = (long)timeStamp + cooldownTime;
                SaveData();

                if (CommandOnPlayer) // Check if this is command on player
                {
                    rust.RunServerCommand($"{Command} {player.userID} {Arrangements}");
                    SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("npcCommand", this)));
                }
                else PrintError((string)bad); // Otherwise print the error message to console so server owners know there is a problem

            }
        }

        void OnUseNPC(BasePlayer npc, BasePlayer player, Vector3 destination)
        {
            if (!npcData.NpcTP.ContainsKey(npc.UserIDString)) return; // Check if this NPC is registered

            ulong playerId = player.userID;
            string npcId = npc.UserIDString;
            double timeStamp = GrabCurrentTime();
            var CanUse = npcData.NpcTP[npcId].CanUse;
            var useUI = npcData.NpcTP[npcId].useUI;
            var cooldownTime = npcData.NpcTP[npcId].Cooldown;
            var Perms = npcData.NpcTP[npcId].permission;
            var amount = npcData.NpcTP[npcId].Cost;
            var useMessage = npcData.NpcTP[npcId].useMessage;
            var MessageNpc = npcData.NpcTP[npcId].MessageNpc;
            string msg = "";


            if (!pcdData.pCooldown.ContainsKey(playerId))
            {
                pcdData.pCooldown.Add(playerId, new PCDInfo());
                //SaveData();
            }

            if (!CanUse)
            {
                SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("notenabled", this)));
                return;
            }
            else
            {
                if (!permission.UserHasPermission(player.userID.ToString(), Perms))
                {
                    SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("noperm", this)));
                    return;
                }
                if (pcdData.pCooldown[playerId].npcCooldowns.ContainsKey(npcId)) // Check if the player already has a cooldown for this NPC
                {
                    var cdTime = pcdData.pCooldown[playerId].npcCooldowns[npcId]; // Get the cooldown time of the NPC
                    if (cdTime > timeStamp)
                    {
                        SendReply(player, string.Format(lang.GetMessage("title", this) + lang.GetMessage("cdTime", this, player.UserIDString), (int)(cdTime - timeStamp) / 60));
                        return;
                    }
                }

                if (!useUI && !useMessage)
                {
                    player.SendConsoleCommand($"hardestcommandtoeverguessnpctp {playerId} {npcId}");
                    return;
                }
                else
                if (useUI && amount >= 1 || useMessage)
                {

                    var elements = new CuiElementContainer();
                    msg = (useMessage && amount >= 1) ? MessageNpc + "\n \n" + lang.GetMessage("commandyesno", this, player.UserIDString) + amount + lang.GetMessage("commandyesno1", this) : "";
                    if (msg == "")
                        msg = (!useMessage && amount >= 1) ? "\n \n" + lang.GetMessage("commandyesno", this, player.UserIDString) + amount + lang.GetMessage("commandyesno1", this) : "";
                    if (msg == "")
                        msg = (useMessage && amount == 0) ? MessageNpc : "";
                    if (msg == "")
                        //Sets the msg to unknown as we could not find a correct variable to match it with.
                        msg = "Unknown";
                    {


                        var mainName = elements.Add(new CuiPanel
                        {
                            Image =
                {
                    Color = "0.1 0.1 0.1 1"
                },
                            RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                            CursorEnabled = true
                        }, "Overlay", "Npctp");
                        if (backroundimage == true)
                        {
                            elements.Add(new CuiElement
                            {
                                Parent = "Npctp",
                                Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = backroundimageurl,
                            Sprite = "assets/content/textures/generic/fulltransparent.tga"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                            });
                        }
                        var Agree = new CuiButton
                        {
                            Button =
                {
                    Command = $"hardestcommandtoeverguessnpctp {playerId} {npcId}",
                    Close = mainName,
                    Color = "0 255 0 1"
                },
                            RectTransform =
                {
                    AnchorMin = "0.2 0.16",
                    AnchorMax = "0.45 0.2"
                },
                            Text =
                {
                    Text = "Go",
                    FontSize = 22,
                    Align = TextAnchor.MiddleCenter
                }
                        };
                        var Disagree = new CuiButton
                        {


                            Button =
                {

                    Close = mainName,
                    Color = "255 0 0 1"

                },
                            RectTransform =
                {
                    AnchorMin = "0.5 0.16",
                    AnchorMax = "0.75 0.2"
                },
                            Text =
                {
                    Text = "Cancel",
                    FontSize = 22,
                    Align = TextAnchor.MiddleCenter
                }
                        };
                        elements.Add(new CuiLabel
                        {
                            Text =
                {
                    Text = msg,
                    FontSize = 22,
                    Align = TextAnchor.MiddleCenter
                },
                            RectTransform =
                {
                    AnchorMin = "0 0.20",
                    AnchorMax = "1 0.9"
                }
                        }, mainName);
                        elements.Add(Agree, mainName);
                        elements.Add(Disagree, mainName);
                        CuiHelper.AddUi(player, elements);
                    }
                }
            }

            #endregion



        }

    }

}





