using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;

using CodeHatch.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using CodeHatch.Permissions; 

namespace Oxide.Plugins
{
    [Info("Jail for RoK", "SweetLouHD", "0.1.1", ResourceId = 1177)]
    public class Jail : ReignOfKingsPlugin
    {
		/////////////////////////////////////////////
		/// VARIABLES
		/// Do not change these, instead change the
		/// values in the Jail.cfg file.
		/////////////////////////////////////////////
		private const int DefaultJailX = 0;
		private const int DefaultJailY = 214;
		private const int DefaultJailZ = 0;
		private const int DefaultJailRadius = 1;
		public int JailX { get; private set; }
		public int JailY { get; private set; }
		public int JailZ { get; private set; }
		public int JailRadius { get; private set; }
		
		private const int DefaultReleaseX = 10;
		private const int DefaultReleaseY = 214;
		private const int DefaultReleaseZ = 10;
		public int ReleaseX { get; private set; }
		public int ReleaseY { get; private set; }
		public int ReleaseZ { get; private set; }
		
		private const int DefaultRollCallInterval = 120;
		private const bool DefaultConsoleSpam = false;
		public int RollCallInterval { get; private set; }
		public bool ConsoleSpam { get; private set; }
		
		private const string DefaultMessagePrefix = "Server";
		private const string DefaultMessagePrefixColor = "C0D737";
		private const string DefaultMessageTextColor = "FFFFFF";
		public string MessagePrefix { get; private set; }
		public string MessagePrefixColor { get; private set; }
		public string MessageTextColor { get; private set; }

		private const bool DefaultDisplayInmateNickname = false;
		private const string DefaultInmateNickname = "Inmate";
		public bool DisplayInmateNickname { get; private set; }
		public string InmateNickname { get; private set; }
		
		private const string DefaultConfigPermission = "oxide.jail.config";
		private const string DefaultUsePermission = "oxide.jail.use";
		public string ConfigPermission { get; private set; }
		public string UsePermission { get; private set; }
		
		bool configChanged;
		
		Permission permissions;
		
		private const string SentToJail = "You are in jail.";
		private const string ReleasedFromJail = "You have been released from jail.";
		private const string CaughtEscaping = "Your escape was not very well planned, you have been caught. Back to the jail cell you go.";
		private const string ConfigPermissionRequired = "You are not allowed to configure this plugin.";
		private const string UsePermissionRequired = "You are not allowed to use this command.";
        private const string SyntaxErrorMsg = "I'm sorry Dave, I'm afraid I can't do that. (Check your syntax)";

        ////////////////////////////////////////////
        /// FIELDS
        ////////////////////////////////////////////
        StoredData storedData;
        static Hash<string, JailInmate> jailinmates = new Hash<string, JailInmate>();
        public DateTime epoch = new System.DateTime(1970, 1, 1);
        bool hasSpawns = false;
        private Hash<Player, Plugins.Timer> TimersList = new Hash<Player, Plugins.Timer>();

        /////////////////////////////////////////
        /// Cached Fields, used to make the plugin faster
        /////////////////////////////////////////
        public Player cachedPlayer;
        public int cachedTime;
        public int cachedCount;
        public JailInmate cachedJail;
        public int cachedInterval;

        /////////////////////////////////////////
        // Data Management
        /////////////////////////////////////////
        class StoredData
        {
            public HashSet<JailInmate> JailInmates = new HashSet<JailInmate>();
            public StoredData(){}
        }
		
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("Jail", storedData);
        }
		
        void LoadData()
        {
            jailinmates.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("Jail");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var jaildef in storedData.JailInmates)
                jailinmates[jaildef.playerid] = jaildef;
        }
		
		/////////////////////////////////////////
        // class JailInmate
        // Where all informations about a jail inmate is stored in the database
        /////////////////////////////////////////

        public class JailInmate
        {
            public string playerid;
            public string x;
            public string y;
            public string z;
            public string jx;
            public string jy;
            public string jz;
            public string expireTime;
            Vector3 jail_position;
            Vector3 free_position;
            int expire_time;

            public JailInmate(){}

            public JailInmate(Player player, Vector3 position, int expiretime = -1)
            {
                playerid = player.Id.ToString();
                x = player.Entity.Position.x.ToString();
                y = player.Entity.Position.y.ToString();
                z = player.Entity.Position.z.ToString();
                jx = position.x.ToString();
                jy = position.y.ToString();
                jz = position.z.ToString();
                expireTime = expiretime.ToString();
            }
            public void UpdateJail(Vector3 position, int expiretime = -1)
            {
                jx = position.x.ToString();
                jy = position.y.ToString();
                jz = position.z.ToString();
                expireTime = expiretime.ToString();
            }
            public Vector3 GetJailPosition()
            {
                if (jail_position == default(Vector3)) jail_position = new Vector3(float.Parse(jx),float.Parse(jy),float.Parse(jz));
                return jail_position;
            }
            public Vector3 GetFreePosition()
            {
                if (free_position == default(Vector3)) free_position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                return free_position;
            }
            public int GetExpireTime()
            {
                if (expire_time == 0) expire_time = int.Parse(expireTime);
                return expire_time;
            }
        }
		
		void OnServerInitialized()
        {
            permissions = Server.Permissions;
		}
		
		/////////////////////////////////////////
        // Loaded()
        // Called when the plugin is loaded
        /////////////////////////////////////////
        void Loaded()
        {
			LoadData();
			LoadConfigData();
			timer.Repeat(RollCallInterval, 0, CheckForJailBreaks);
        }
		
		/////////////////////////////////////////
        // Unload()
        // Called when the plugin is unloaded (via oxide.unload or oxide.reload or when the server shuts down)
        /////////////////////////////////////////
        void Unload()
        {
            foreach (KeyValuePair<Player, Plugins.Timer> pair in TimersList)
                pair.Value.Destroy();
            TimersList.Clear();
        }
		
		/////////////////////////////////////////
        // OnPlayerConnected(Player player)
        // Called when a player logs in
        /////////////////////////////////////////
        void OnPlayerConnected(Player player)
        {
            if (jailinmates[player.Id.ToString()] != null)
				CheckForJailBreaks();
        }
		
		/////////////////////////////////////////
        // Called to see if a player is inside a zone or not
        /////////////////////////////////////////
        bool isInZone(Player player)
        {
			if(ConsoleSpam)
				Puts("Checking if " + player.DisplayName.ToString() + " is still in their jail cell.");
            
			if(player.Entity.Position.x > JailX + JailRadius || player.Entity.Position.x < JailX - JailRadius) return false;
			if(player.Entity.Position.z > JailZ + JailRadius || player.Entity.Position.z < JailZ - JailRadius) return false;
			return true;
		}
		/////////////////////////////////////////
        // Jail functions
        /////////////////////////////////////////

        /////////////////////////////////////////
        // AddPlayerToJail(Player player, int expiretime)
        // Adds a player to the jail, and saves him in the database
        /////////////////////////////////////////
        void AddPlayerToJail(Player player, int expiretime)
        {
            var tempPoint = player.Entity.Position;//FindCell(player.Id.ToString());
            if (tempPoint == null) { return; }
            JailInmate newjailmate;
            if (jailinmates[player.Id.ToString()] != null) { newjailmate = jailinmates[player.Id.ToString()]; newjailmate.UpdateJail((Vector3)tempPoint, expiretime); }
            else newjailmate = new JailInmate(player, (Vector3)tempPoint, expiretime);
            if (jailinmates[player.Id.ToString()] != null) storedData.JailInmates.Remove(jailinmates[player.Id.ToString()]);
            jailinmates[player.Id.ToString()] = newjailmate;
            storedData.JailInmates.Add(jailinmates[player.Id.ToString()]);
            SaveData();
        }
		
		/////////////////////////////////////////
        // SendPlayerToJail(Player player)
        // Sends a player to the jail
        /////////////////////////////////////////
        void SendPlayerToJail(Player player)
        {
            if (jailinmates[player.Id.ToString()] == null) return;
            EventManager.CallEvent((BaseEvent)new TeleportEvent(player.Entity, new Vector3(JailX, JailY, JailZ)));
			PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + SentToJail);
			if(DisplayInmateNickname)
				player.DisplayNameFormat = "(" + InmateNickname + ") %name%";
        }
		
		/////////////////////////////////////////
        // RemovePlayerFromJail(Player player)
        // Removes a player from the jail (need to be called after SendPlayerOutOfJail, because we need the return point)
        /////////////////////////////////////////
        void RemovePlayerFromJail(Player player)
        {
            if (jailinmates[player.Id.ToString()] != null) storedData.JailInmates.Remove(jailinmates[player.Id.ToString()]);
            jailinmates[player.Id.ToString()] = null;
            SaveData();
        }
		
		/////////////////////////////////////////
        // SendPlayerOutOfJail(Player player)
        // Send player out of the jail
        /////////////////////////////////////////
        void SendPlayerOutOfJail(Player player)
        {
            if (jailinmates[player.Id.ToString()] == null) return;
            cachedJail = jailinmates[player.Id.ToString()];
			EventManager.CallEvent((BaseEvent)new TeleportEvent(player.Entity, new Vector3(ReleaseX, ReleaseY, ReleaseZ)));
            PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + ReleasedFromJail);
			if(DisplayInmateNickname)
				player.DisplayNameFormat = "%name%";
		}
		
		/////////////////////////////////////////
        // CheckPlayerExpireTime(Player player)
        // One function to take care of the timer, calls himself.
        /////////////////////////////////////////
        void CheckPlayerExpireTime(Player player)
        {
            if (TimersList[player] != null) { TimersList[player].Destroy(); TimersList[player] = null; }
            if (jailinmates[player.Id.ToString()] == null) return;
            cachedJail = jailinmates[player.Id.ToString()];
            if (cachedJail.GetExpireTime() < 0) return;
            cachedInterval = cachedJail.GetExpireTime() - CurrentTime();
            if (cachedInterval < 1)
            {
                SendPlayerOutOfJail(player);
                RemovePlayerFromJail(player);
            }
            else
			{
                TimersList[player] = timer.Once( (float)(cachedInterval + 1), () => CheckPlayerExpireTime(player));
			}
        }
		
		/////////////////////////////////////////
		/// Random Functions
		/////////////////////////////////////////
		int CurrentTime() { return System.Convert.ToInt32(System.DateTime.UtcNow.Subtract(epoch).TotalSeconds); }
		
		private void SendHelpText(Player player)
		{
			if(HasPermission(player, ConfigPermission))
			{
				PrintToChat(player, "/jail_config - Shows commands to set up Jail for Reign of Kings.");
				PrintToChat(player, "/jail.set - Set up Jail for Reign of Kings.");
			}
			
			if(HasPermission(player, UsePermission))
			{
				PrintToChat(player, "/jail - Send a player to Jail.");
				PrintToChat(player, "/free - Release a player from Jail early.");
				PrintToChat(player, "/loc - Get your current coordinates.");
			}
		}
		
		bool HasPermission(Player player, string perm = null)
		{
			return PlayerExtensions.HasPermission(player, perm);
		}
		
		void CheckForJailBreaks(){
			if(ConsoleSpam)
				Puts("Preforming Scheduled Roll Call.");
			List<Player> onlineplayers = Server.ClientPlayers as List<Player>;
			foreach (Player player in onlineplayers.ToArray())
            {
				if (jailinmates[player.Id.ToString()] != null)
				{
					CheckPlayerExpireTime(player);
					if (!isInZone(player))
					{
						if(ConsoleSpam)
							Puts(player.DisplayName.ToString() + " has escaped!. No worries we got him :)");
						PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + CaughtEscaping);
						EventManager.CallEvent((BaseEvent)new TeleportEvent(player.Entity, new Vector3(JailX, JailY, JailZ)));
					} else {
						if(ConsoleSpam)
							Puts(player.DisplayName.ToString() + " had been accounted for.");
					}
				}
			}
			if(ConsoleSpam)
				Puts("All Inmates have been accounted for. Next Roll Call will be in " + RollCallInterval + " seconds.");
		}
		
		private object FindPlayer(string tofind)
        {
            if (tofind.Length == 17)
            {
                ulong steamid;
                if (ulong.TryParse(tofind.ToString(), out steamid))
                    return FindPlayerByID(steamid);
            }
            List<Player> onlineplayers = Server.ClientPlayers as List<Player>;
            object targetplayer = null;
            foreach (Player player in onlineplayers.ToArray())
            {

                if (player.DisplayName.ToString() == tofind)
                    return player;
                else if (player.DisplayName.ToString().Contains(tofind))
                {
                    if (targetplayer == null)
                        targetplayer = player;
                    else
                        return "Multiple players found";
                }
            }
            if (targetplayer == null)
                return "No Online player with this name was found";
            return targetplayer;
        }
		
		private object FindPlayerByID(ulong steamid)
        {
			Player targetplayer = Server.GetPlayerById(steamid);
			if (targetplayer != null)
                return targetplayer;
            return null;
        }

        public void ShowJailHelp(Player player)
        {
            PrintToChat(player, "Jail.set Help:");
            PrintToChat(player, "/Jail.set spam on/off => Turns on/off Console Messages. Good for debugging your configuration.");
            PrintToChat(player, "/Jail.set cell => Sets your current location as the center point of the cell where jailed players will be teleported to.");
            PrintToChat(player, "/Jail.set radius # => Tell us in a single whole number how big your cell is. ie. 3x3 cell radius = 1, 4x4 cell radius = 2. # = radius.");
            PrintToChat(player, "/Jail.set rollcall # => How many seconds between a scheduled roll call? Roll Calls are used to make sure inmates did not escape. # = seconds.");
            PrintToChat(player, "/Jail.set release => Sets your current location to where inmates will be teleported to when they have finished serving their sentence.");
            PrintToChat(player, "/Jail.set nicknames on/off => Turns Inmate Nicknames on or off.");
            PrintToChat(player, "/Jail.set nickname inmate x => Sets the nickname an inmate will receive while in jail. x = nickname.");
            PrintToChat(player, "/Jail.set restore => Restored Default Config file.");
        }

        /////////////////////////////////////////
        // Chat commands
        /////////////////////////////////////////
        [ChatCommand("Jail_Config")]
        void cmdChatJailConfig(Player player, string command, string[] args)
        {
            if (!HasPermission(player, ConfigPermission)) { PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + ConfigPermissionRequired); return; }
            ShowJailHelp(player);
        }

        [ChatCommand("Jail.set")]
        void cmdChatJailSet(Player player, string command, string[] args)
        {
            if (!HasPermission(player, ConfigPermission)) { PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + ConfigPermissionRequired); return; }
            if (args.Length < 1)
                ShowJailHelp(player);
            switch (args[0].ToLower())
            {
                case "cell":
                    JailX = (int)player.Entity.Position.x;
                    JailY = (int)player.Entity.Position.y + 1;
                    JailZ = (int)player.Entity.Position.z;
                    PrintToChat(player, "Center of Jail Cell has been set.");
                    if(ConsoleSpam)
                        Puts("Center of Jail Cell has been set.");
                    break;
                case "radius":
                    JailRadius = Int32.Parse(args[1]);
                    PrintToChat(player, "Jail Cell Radius has been set to " + JailRadius + ".");
                    if(ConsoleSpam)
                        Puts("Jail Cell Radius has been set to " + JailRadius + ".");            
                    break;
                case "rollcall":
                    RollCallInterval = Int32.Parse(args[1]);
                    PrintToChat(player, "Roll Calls will occur every " + RollCallInterval + " seconds.");
                    if(ConsoleSpam)
                        Puts("Roll Calls will occur every " + RollCallInterval + " seconds."); 
                    break;
                case "release":
                    if (!player.HasPermission("admin")) { PrintToChat(player, ConfigPermissionRequired); return; }
                    ReleaseX = (int)player.Entity.Position.x;
                    ReleaseY = (int)player.Entity.Position.y + 1;
                    ReleaseZ = (int)player.Entity.Position.z;
                    PrintToChat(player, "Inmate release point has been set.");
                    if(ConsoleSpam)
                        Puts("Inmate release point has been set."); 
                    break;
                case "spam":
                    switch (args[1].ToLower())
                    {
                        case "on":
                            ConsoleSpam = true;
                            PrintToChat(player, "Console Spam has been turned on.");
                            if (ConsoleSpam)
                                Puts("Console Spam has been turned on.");
                            break;
                        case "off":
                            ConsoleSpam = false;
                            PrintToChat(player, "Console Spam has been turned off.");
                            if (ConsoleSpam)
                                Puts("Console Spam has been turned off.");
                            break;
                        default:
                            PrintToChat(player, SyntaxErrorMsg);
                            if (ConsoleSpam)
                                Puts(SyntaxErrorMsg);
                            break;
                    }
                    break;
                case "nicknames":
                    switch (args[1].ToLower())
                    {
                        case "on":
                            DisplayInmateNickname = true;
                            PrintToChat(player, "Inmate Nicknames have been turned on.");
                            if (ConsoleSpam)
                                Puts("Inmate Nicknames have been turned on.");
                            break;
                        case "off":
                            DisplayInmateNickname = false;
                            PrintToChat(player, "Inmate Nicknames have been turned off.");
                            if (ConsoleSpam)
                                Puts("Inmate Nicknames have been turned off.");
                            break;
                        default:
                            PrintToChat(player, SyntaxErrorMsg);
                            if (ConsoleSpam)
                                Puts(SyntaxErrorMsg);
                            break;
                    }
                    break;
                case "nickname":
                    switch (args[1].ToLower())
                    {
                        case "inmate":
                            if(args[2] != "")
                            {
                                InmateNickname = args[2];
                                PrintToChat(player, "Inmate Nickname has been changed to " + InmateNickname + ".");
                                if (ConsoleSpam)
                                    Puts("Inmate Nickname has been changed to " + InmateNickname + ".");
                            } else {
                                PrintToChat(player, SyntaxErrorMsg);
                                if (ConsoleSpam)
                                    Puts(SyntaxErrorMsg);
                            }
                            break;
                        default:
                            PrintToChat(player, SyntaxErrorMsg);
                            if (ConsoleSpam)
                                Puts(SyntaxErrorMsg);
                            break;
                    }
                    break;
                case "restore":
                    Config.Clear();
                    LoadConfigData();
                    PrintToChat(player, "Jail Defaults have been restored.");
                    if (ConsoleSpam)
                        Puts("Jail Defaults have been restored.");
                    break;
                default:
                    PrintToChat(player, SyntaxErrorMsg);
                    if(ConsoleSpam)
                        Puts(SyntaxErrorMsg);
                    return;
                    break;
            }
            UpdateConfig();
        }
        
        [ChatCommand("Jail")]
        void cmdChatJail(Player player, string command, string[] args)
        {
            if (!HasPermission(player, UsePermission)){ PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + UsePermissionRequired); return; }
            if (args.Length  == 0) { PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + "/jail PLAYER option:Time(seconds)"); return; }

            var target = FindPlayer(args[0].ToString());
            if (target is string) { PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + target.ToString()); return; }
            cachedPlayer = (Player)target;
            cachedTime = -1;
            if (args.Length > 1) int.TryParse(args[1], out cachedTime);
            if (cachedTime != -1) cachedTime += CurrentTime();

            AddPlayerToJail(cachedPlayer, cachedTime);
            SendPlayerToJail(cachedPlayer);

            CheckPlayerExpireTime(cachedPlayer);

            PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + string.Format("{0} was sent to jail",cachedPlayer.DisplayName.ToString()));
        }

        [ChatCommand("Free")]
        void cmdChatFree(Player player, string command, string[] args)
        {
            if (!HasPermission(player, UsePermission)) { PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + UsePermissionRequired); return; }
            if (args.Length == 0) { PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + "/free PLAYER"); return; }

            var target = FindPlayer(args[0].ToString());
            if (target is string) { PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + target.ToString()); return; }
            cachedPlayer = (Player)target;

            SendPlayerOutOfJail(cachedPlayer);
            RemovePlayerFromJail(cachedPlayer);

            CheckPlayerExpireTime(cachedPlayer);

            PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + string.Format("{0} was freed from jail", cachedPlayer.DisplayName.ToString()));
        }
		
		/////////////////////////////////////////
		/// /loc command courtesy of DumbleDora
		/////////////////////////////////////////
        [ChatCommand("Loc")]
        private void locate(Player player, string cmd, string[] args)
        {   
			if (HasPermission(player, UsePermission)) {
                if( args.Length < 1 ){
                    string playerPosition = "Your position: X: " + player.Entity.Position.x + ", Y: " + player.Entity.Position.y + ", Z: " + player.Entity.Position.z;   
                    PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + playerPosition);
				    return;						
                }
                if( args.Length == 1 ){
                    Player toLoc = Server.GetPlayerByName(args[0]);
                    string playerPosition = toLoc.DisplayName + "'s position: X: " + toLoc.Entity.Position.x + ", Y: " + toLoc.Entity.Position.y + ", Z: " + toLoc.Entity.Position.z;   
                    PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + playerPosition);   
				    return;
                }
			    PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + "Incorrect usage. Try '/loc' or '/loc player'.");  
			} else {
				 PrintToChat(player, "[" + MessagePrefixColor + "]" + MessagePrefix + " [" + MessageTextColor + "]: " + UsePermissionRequired);
			}					
        }
		
		private void UpdateConfig()
		{
            PrintWarning("Updating Config File...");

            Config.Clear();
            Config["Chat Manipulation", "Display Nicknames"] = DisplayInmateNickname;
            Config["Chat Manipulation", "Inmate Nickname"] = DisplayInmateNickname;

            Config["Jail", "Radius"] = JailRadius;
            Config["Jail", "X"] = JailX;
            Config["Jail", "Y"] = JailY;
            Config["Jail", "Z"] = JailZ;

            Config["Messages", "Prefix"] = MessagePrefix;
            Config["Messages", "Prefix Color"] = MessagePrefixColor;
            Config["Messages", "Text Color"] = MessageTextColor;

            Config["Release", "X"] = ReleaseX;
            Config["Release", "Y"] = ReleaseY;
            Config["Release", "Z"] = ReleaseZ;

            Config["Settings", "Console Spam"] = ConsoleSpam;
            Config["Settings", "Roll Call Interval"] = RollCallInterval;

            SaveConfig();
		}
		
		protected override void LoadDefaultConfig() => PrintWarning("New configuration file created.");
		
		private void LoadConfigData(){
		    JailX = GetConfigValue("Jail", "X", DefaultJailX);
		    JailY = GetConfigValue("Jail", "Y", DefaultJailY);
		    JailZ = GetConfigValue("Jail", "Z", DefaultJailZ);
		    JailRadius = GetConfigValue("Jail", "Radius", DefaultJailRadius);
		
		    ReleaseX = GetConfigValue("Release", "X", DefaultReleaseX);
		    ReleaseY = GetConfigValue("Release", "Y", DefaultReleaseY);
		    ReleaseZ = GetConfigValue("Release", "Z", DefaultReleaseZ);
		
		    RollCallInterval = GetConfigValue("Settings", "Roll Call Interval", DefaultRollCallInterval);
		    ConsoleSpam = GetConfigValue("Settings", "Console Spam", DefaultConsoleSpam);
		
		    MessagePrefix = GetConfigValue("Messages", "Prefix", DefaultMessagePrefix);
            MessagePrefixColor = GetConfigValue("Messages", "Prefix Color", DefaultMessagePrefixColor);
            MessageTextColor = GetConfigValue("Messages", "Text Color", DefaultMessageTextColor);
		
		    DisplayInmateNickname = GetConfigValue("Chat Manipulation", "Display Nicknames", DefaultDisplayInmateNickname);
		    InmateNickname = GetConfigValue("Chat Manipulation", "Inmate Nickname", DefaultInmateNickname);
			
			ConfigPermission = GetConfigValue("Permissions", "Configure Plugin", DefaultConfigPermission);
			UsePermission = GetConfigValue("Permissions", "Use Plugin", DefaultUsePermission);
	
            if (!configChanged) return;
            PrintWarning("The configuration file was updated!");
            SaveConfig();
		}
		
		private T GetConfigValue<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                configChanged = true;
            }
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            configChanged = true;
            return (T)Convert.ChangeType(value, typeof(T));
        }
    }
}