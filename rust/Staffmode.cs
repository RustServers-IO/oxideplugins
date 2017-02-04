using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;
using Oxide.Core;

using System;

using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("Staffmode", "Canopy Sheep", "1.0.2")]
    [Description("Toggle on/off staff mode")]
    class Staffmode : RustPlugin
    {	
		readonly FieldInfo displayname = typeof(BasePlayer).GetField("_displayName", (BindingFlags.Instance | BindingFlags.NonPublic));

        #region Data
        class Data
		{
			public Dictionary<string, StaffData> StaffData = new Dictionary<string, StaffData>();
		}
		
		Data data;

		class GroupData
		{
			public Dictionary<string, Group> Groups = new Dictionary<string, Group>();
		}
		
		GroupData groupData; 
		
		class StaffData
		{
			public bool EnabledOffDutyMode;
			
			public StaffData(BasePlayer player)
			{
				EnabledOffDutyMode = false;
			}
		}
		
		class Group
		{
			public string GroupName;
			public int AuthLevel;
			public string OffDutyGroup;
			public string OnDutyGroup;
			public string PermissionNode;
		}

        #endregion
        #region Config

        public int groupcount; 
		private ConfigData configData;
		public string groupname;
		private bool AlreadyPowered = false;
		private bool AlreadyAnnounced = false;
		private bool PermissionDenied = false;
		private bool AlreadyToggled = false;

		class ConfigData
		{
			public SettingsData Settings { get; set; }
            public DebugData Debug { get; set; }
		}
		class SettingsData
		{
			public string PluginPrefix { get; set; }
			public string EditPermission { get; set; }
			public bool AnnounceOnToggle { get; set; }
			public bool LogOnToggle { get; set; }
			public bool DisconnectOnToggle { get; set; }
			public bool EnableGroupToggle { get; set; }

		}
        class DebugData
        {
            public bool CheckGroupDataOnLoad { get; set; }
            public bool CheckGroupDataOnError { get; set; }
            public bool Dev { get; set; }
        }

        void TryConfig()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch (Exception)
            {
                Puts("Corrupt config");
                LoadDefaultConfig();
                timer.Once(3, () => ConsoleSystem.Run.Server.Normal("reload Staffmode"));
                Puts("Reloading Plugin in 3 seconds");
            }
        }

        void LoadConfig()
        {
            Config.WriteObject(new ConfigData
            {
                Settings = new SettingsData
                {
                    PluginPrefix = "<color=orange>[StaffMode]</color>",
                    EditPermission = "staffmode.canedit",
                    AnnounceOnToggle = true,
                    LogOnToggle = true,
                    DisconnectOnToggle = true,
                    EnableGroupToggle = true
                },
                Debug = new DebugData
                {
                    CheckGroupDataOnLoad = false,
                    CheckGroupDataOnError = false,
                    Dev = false
                }
            }, true);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Generating a new config file...");
            LoadConfig();
        }

        #endregion
        #region Language

        internal string Replace(string source, string name) => source.Replace(source, name);

        string Lang(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());

        private void Language()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "ToggleOnAnnounce", "{player.name} has switched to staff mode. They can now use staff commands."},
                { "ToggleOffAnnounce", "{player.name} has switched to player mode. They can no longer use staff commands."},
                { "ToggleOn", "You have switched to staff mode!"},
                { "ToggleOff", "You have switched to player mode!"},
                { "Disconnect", "You will be disconnected in {seconds}, please reconnect to update your auth level."},
                { "ToggleOnLog", "{player.name} is now in staff mode."},
                { "ToggleOffLog", "{player.name} is now out of staff mode."},
                { "NoPermission", "You do not have permission to use this command."},
                { "Reconnect", "You will be kicked in 5 seconds to update your status. Please reconnect!"},
                { "Corrupt", "A group you tried to toggle into is corrupt, please check console for more information."},
                { "Usage", "Usage: /staffmode group create/remove [groupname]"},
                { "AlreadyExists", "This group already exists."},
                { "DoesNotExist", "This group doesn't exists."},
                { "RemovedGroup", "Removed group '{group}' successfully"},
                { "CreatedGroup", "Created group '{group}' successfully."},
                { "NoGroups", "No groups have been configured properly. Check console for more information."}
            }, this);
        }

        #endregion
        #region Hooks

        void Loaded()
		{		
			data = Interface.Oxide.DataFileSystem.ReadObject<Data>("Staffmode_PlayerData");
			
			LoadData();
			Language();
			TryConfig();
		}

		void OnServerInitialized()
		{
			RegisterPermissions();
			CheckData(1);
		}

		void RegisterPermissions()
		{
			foreach (var group in groupData.Groups.Values)
            {
                if (!string.IsNullOrEmpty(group.PermissionNode))
                    permission.RegisterPermission(group.PermissionNode, this);
            }
			permission.RegisterPermission(configData.Settings.EditPermission, this);
		}

        void LoadData()
        {
            var groupdata = Interface.Oxide.DataFileSystem.GetFile("Staffmode_Groups");
            try
            {
                groupData = groupdata.ReadObject<GroupData>();
                var update = new List<string>();
            }
            catch
            {
                groupData = new GroupData();
            }
        }

        void SaveData() 
		{
			Interface.Oxide.DataFileSystem.WriteObject("Staffmode_PlayerData", data);
		}
		
		void SaveGroups()
		{
			Interface.Oxide.DataFileSystem.WriteObject("Staffmode_Groups", groupData);
		}

		void CheckData(int check)
		{
            groupcount = 0;
            if (check == 1 && !(configData.Debug.CheckGroupDataOnLoad))
            {
                foreach (var group in groupData.Groups.Values)
                {
                    groupcount = groupcount + 1;
                }
                return;
            }
            if (check == 2 && !(configData.Debug.CheckGroupDataOnError)) { return; }

			
			int possibletotalerrors = 0;
            int possiblemajorerrors = 0;
			Puts("Checking groups...");
			foreach (var group in groupData.Groups.Values)
			{
				groupcount = groupcount + 1;
				if (configData.Settings.EnableGroupToggle)
				{
                    if (!(group.OnDutyGroup == null))
                    {
                        try
                        {
                            if (!(permission.GroupExists(group.OnDutyGroup))) { Puts("Permission Group '" + group.OnDutyGroup.ToString() + "' does not exist. Check to make sure this permission group exists."); possibletotalerrors = possibletotalerrors + 1; }
                        }
                        catch (NullReferenceException)
                        {
                            Puts("Check could not continue for group '" + group.GroupName.ToString() + ".' Check for any 'null' settings.");
                            possibletotalerrors = possibletotalerrors + 1;
                            possiblemajorerrors = possiblemajorerrors + 1;
                            continue;
                        }
                    }
                    else { Puts("Group '" + group.GroupName.ToString() + "' OnDutyGroup is null with GroupToggling enabled."); possibletotalerrors = possibletotalerrors + 1; }

                    if (!(group.OffDutyGroup == null))
                    {
                        try
                        {
                            if (!(permission.GroupExists(group.OffDutyGroup))) { Puts("Permission Group '" + group.OffDutyGroup.ToString() + "' does not exist. Check to make sure this permission group exists."); possibletotalerrors = possibletotalerrors + 1; }
                        }
                        catch (NullReferenceException)
                        {
                            Puts("Check could not continue for group '" + group.GroupName.ToString() + ".' Check for any 'null' settings.");
                            possibletotalerrors = possibletotalerrors + 1;
                            possiblemajorerrors = possiblemajorerrors + 1;
                            continue;
                        }
                    }
                    else { Puts("Group '" + group.GroupName.ToString() + "' OffDutyGroup is null with GroupToggling enabled."); possibletotalerrors = possibletotalerrors + 1; }
                }
                if (group.AuthLevel != null)
				{
                    if (group.AuthLevel != 0)
					{
                        if (group.AuthLevel != 1 && group.AuthLevel != 2) { Puts("Group '" + group.GroupName.ToString() + "' does not have a correct auth level setting. Must be '0' '1' or '2'" ); possibletotalerrors = possibletotalerrors + 1; possiblemajorerrors = possiblemajorerrors + 1; }
                    }
				}
				if (group.PermissionNode == null)
				{
					Puts("Group '" + group.GroupName + "' permission node is null. Anyone will be able to toggle into this group.");
					possibletotalerrors = possibletotalerrors + 1;
				}
			}
			Puts("Group check complete. Checked '" + groupcount + "' groups. Detected '" + possibletotalerrors + "' possible error(s), '" + possiblemajorerrors + "' which are critical based on your settings.");
		}
		
		bool CheckPermission(BasePlayer player, string perm)
		{
			if(permission.UserHasPermission(player.userID.ToString(), perm)) return true;
			return false;
		}

        #endregion
        #region Commands

        [ChatCommand("staffmode")]
		void StaffToggleCommand(BasePlayer player, string cmd, string[] args)
		{	
			if (args.Length == 0)
			{
				if (groupcount == 0)
				{
					SendReply(player, configData.Settings.PluginPrefix + " " + Lang("NoGroups", player.UserIDString));
                    if (configData.Debug.CheckGroupDataOnError) { Puts("No groups detected in oxide/data/Staffmode_Groups.json, running data check."); }
                    else { Puts("Error: No groups detected. Data check is disabled, please check your data file."); }
					CheckData(2);
					return;
				}
				foreach (var group in groupData.Groups.Values)
				{					
					if(!(CheckPermission(player, group.PermissionNode)) && group.PermissionNode != null)
					{
						continue;
					}
					
					PermissionDenied = false;
					if (configData.Settings.EnableGroupToggle)
					{
						if (group.OffDutyGroup == null) {  Puts("Off Duty Group not configured properly. Skipping group '" + group.GroupName + "'"); SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Corrupt", player.UserIDString)); continue; }
						if (group.OnDutyGroup == null) {  Puts("On Duty Group not configured properly. Skipping group '" + group.GroupName + "'"); SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Corrupt", player.UserIDString)); continue; }
					}
					
					if(!data.StaffData.ContainsKey(player.userID.ToString())) { data.StaffData.Add(player.userID.ToString(), new StaffData(player)); }

					//Toggle on
					if(data.StaffData[player.userID.ToString()].EnabledOffDutyMode)
					{
						if (group.AuthLevel != 0 && !(AlreadyPowered))
						{
							if (configData.Settings.DisconnectOnToggle)
							{
								if (group.AuthLevel == 1) { ConsoleSystem.Run.Server.Normal("moderatorid", player.userID.ToString()); ConsoleSystem.Run.Server.Normal("server.writecfg"); AlreadyPowered = true; }
								else if (group.AuthLevel == 2) { ConsoleSystem.Run.Server.Normal("ownerid", player.userID.ToString()); ConsoleSystem.Run.Server.Normal("server.writecfg"); AlreadyPowered = true; } 
							}
							else if (group.AuthLevel == 1 || group.AuthLevel == 2) { player.SetPlayerFlag( BasePlayer.PlayerFlags.IsAdmin, true); AlreadyPowered = true; }
							else { Puts("Error: AuthLevel invalid for group '" + group.GroupName + ".' No AuthLevel given."); }
						}
						if (configData.Settings.EnableGroupToggle)
						{
							permission.AddUserGroup(player.userID.ToString(), group.OnDutyGroup.ToString());
							permission.RemoveUserGroup(player.userID.ToString(), group.OffDutyGroup.ToString());	
						}

						if (!(AlreadyAnnounced))
						{
							SendReply(player, configData.Settings.PluginPrefix + " " + Lang("ToggleOn", player.UserIDString));
							if (configData.Settings.LogOnToggle) { Puts(Lang("ToggleOnLog", player.UserIDString).Replace("{player.name}", player.displayName)); }
							if (configData.Settings.AnnounceOnToggle) { PrintToChat(configData.Settings.PluginPrefix + " " + Lang("ToggleOnAnnounce", player.UserIDString).Replace("{player.name}", player.displayName)); }
							if (configData.Settings.DisconnectOnToggle)
							{
								SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Reconnect", player.UserIDString));
								if (!(configData.Debug.Dev)) { timer.Once(5, () => player.SendConsoleCommand("client.disconnect")); }
							}
							AlreadyAnnounced = true;
						}		
						AlreadyToggled = true;
					}
					//Toggle off
					else if(!(data.StaffData[player.userID.ToString()].EnabledOffDutyMode))
					{
						if (configData.Settings.EnableGroupToggle)
						{
							permission.AddUserGroup(player.userID.ToString(), group.OffDutyGroup.ToString());
							permission.RemoveUserGroup(player.userID.ToString(), group.OnDutyGroup.ToString());	
						}
						if (group.AuthLevel != 0 && !AlreadyPowered)
						{
							if (configData.Settings.DisconnectOnToggle)
							{
								if (group.AuthLevel == 1) { ConsoleSystem.Run.Server.Normal("removemoderator", player.userID.ToString()); ConsoleSystem.Run.Server.Normal("server.writecfg"); }
								else if (group.AuthLevel == 2) { ConsoleSystem.Run.Server.Normal("removeowner", player.userID.ToString()); ConsoleSystem.Run.Server.Normal("server.writecfg"); }
							}
							else if (group.AuthLevel == 1 || group.AuthLevel == 2) { player.SetPlayerFlag( BasePlayer.PlayerFlags.IsAdmin, false); }
							else { Puts("Error: AuthLevel invalid for group '" + group.GroupName + ".' No AuthLevel revoked."); }
							AlreadyPowered = true;
						}
						if (!(AlreadyAnnounced))
						{
							SendReply(player, configData.Settings.PluginPrefix + " " + Lang("ToggleOff", player.UserIDString));
							if (configData.Settings.LogOnToggle) { Puts(Lang("ToggleOffLog", player.UserIDString).Replace("{player.name}", player.displayName)); }
							if (configData.Settings.AnnounceOnToggle) { PrintToChat(configData.Settings.PluginPrefix + " " + Lang("ToggleOffAnnounce", player.UserIDString).Replace("{player.name}", player.displayName)); }
							if (configData.Settings.DisconnectOnToggle)
							{
								SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Reconnect", player.UserIDString)); 
								if (!configData.Debug.Dev) { timer.Once(5, () => player.SendConsoleCommand("client.disconnect")); }
							}
							AlreadyAnnounced = true;
						}
						AlreadyToggled = true;
					}
				}
				if(data.StaffData.ContainsKey(player.userID.ToString()))
				{
					data.StaffData[player.userID.ToString()].EnabledOffDutyMode = !data.StaffData[player.userID.ToString()].EnabledOffDutyMode;
					SaveData();
				}
				AlreadyAnnounced = false;
				AlreadyPowered = false;
				if (PermissionDenied && !(AlreadyToggled)) { SendReply(player, configData.Settings.PluginPrefix + " " + Lang("NoPermission", player.UserIDString)); }
				PermissionDenied = true;
				AlreadyToggled = false;
				return;
			}
			else
			{
				switch (args[0].ToLower())
				{
					case "group":
					{
						if(!(CheckPermission(player, configData.Settings.EditPermission))) { SendReply(player, configData.Settings.PluginPrefix + " " + Lang("NoPermission", player.UserIDString)); return; }
						if (args.Length < 2)
						{
							SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Usage", player.UserIDString));
							return;
						}
						switch (args[1].ToLower())
						{
							case "create":
							{	
								if(!(CheckPermission(player, configData.Settings.EditPermission))) { SendReply(player, configData.Settings.PluginPrefix + " " + Lang("NoPermission", player.UserIDString)); return; }
								
								if (args.Length != 3)
								{
									SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Usage", player.UserIDString));
									return;
								}
								
								groupname = args[2].ToLower();
								
								if(groupData.Groups.ContainsKey(groupname.ToString())) 
								{ 
									SendReply(player, configData.Settings.PluginPrefix + " " + Lang("AlreadyExists", player.UserIDString));
									return; 
								}
								
								groupData.Groups[groupname] = new Group { GroupName = args[2] };
								SaveGroups();
								SendReply(player, configData.Settings.PluginPrefix + " " + Lang("CreatedGroup", player.UserIDString).Replace("{group}", groupname.ToString()));
								break;
							}
							case "remove":
							{
								if(!(CheckPermission(player, configData.Settings.EditPermission))) { SendReply(player, configData.Settings.PluginPrefix + " " + Lang("NoPermission", player.UserIDString)); return; }
								
								if (args.Length != 3)
								{
									SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Usage", player.UserIDString));
									return;
								}
								
								groupname = args[2].ToLower();
								
								if(!(groupData.Groups.ContainsKey(groupname.ToString()))) 
								{ 
									SendReply(player, configData.Settings.PluginPrefix + " " + Lang("DoesNotExist", player.UserIDString));
									return; 
								}
								
								groupData.Groups.Remove(groupname.ToString());
								SaveGroups();
								SendReply(player, configData.Settings.PluginPrefix + " " + Lang("RemovedGroup", player.UserIDString).Replace("{group}", groupname.ToString()));
								break;
							}
							default:
							{
								if(!(CheckPermission(player, configData.Settings.EditPermission))) { SendReply(player, configData.Settings.PluginPrefix + " " + Lang("NoPermission", player.UserIDString)); }
								
								SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Usage", player.UserIDString));
								break;
							}
						}
						break;
					}
					default:
					{
						SendReply(player, configData.Settings.PluginPrefix + " " + Lang("Usage", player.UserIDString));
						break;
					}
				}
			}
		}
        #endregion
    }
}