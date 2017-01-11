//Reference: Oxide.Ext.MySql

using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Ext.MySql;
using CodeHatch.Build;
using CodeHatch.Engine.Networking;
using CodeHatch.Engine.Core.Commands;
using CodeHatch.Engine.Registration;
using CodeHatch.Common;
using CodeHatch.Networking.Events;
using CodeHatch.Permissions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AutoWhitelist", "SweetLouHD", "2.2.0", ResourceId = 1128)]
    public class AutoWhitelist : ReignOfKingsPlugin
    {
/*	-----------------------------------------------------------------------------------------------------	
	The following are default values that make up the config file. Do not change these values, instead
	modify the config file in the [Server Location]/Saves/oxide/config/AutoWhitelist.cfg file. If this
	file does not exist yet, run the Plugin once and it will be generated for you.
	
	If you load this Plugin onto your server and make changes in the config file, you can type
	"Oxide.Reload AutoWhitelist" in the console and press enter to Reload the Plugin with the new
	config settings.
	-----------------------------------------------------------------------------------------------------	*/
		private const bool DefaultConsoleDebug = false;
		private const bool DefaultEnableMySqlConnection = false;
		private const bool DefaultEnableAnnouncements = false;
		private const int DefaultUpdateInterval = 300;
		private const string DefaultAnnouncementPrefix = "Server";
		private const string DefaultAnnouncementPrefixColor = "C0D737";
		private const string DefaultAnnouncementText = "The Whitelist has been updated.";
		private const string DefaultAnnouncementTextColor = "FFFFFF";
		private const string DefaultMySqlAddress = "localhost";
		private const int DefaultMySqlPort = 3306;
		private const string DefaultMySqlDatabaseName = "";
		private const string DefaultMySqlUsername = "";
		private const string DefaultMySqlPassword = "";
		private const string DefaultMySqlTableName = "";
		public bool ConsoleDebug { get; private set; }
		public bool EnableMySqlConnection { get; private set; }
		public bool EnableAnnouncements { get; private set; }
		public int UpdateInterval { get; private set; }
		public string AnnouncementPrefix { get; private set; }
		public string AnnouncementPrefixColor { get; private set; }
		public string AnnouncementText { get; private set; }
		public string AnnouncementTextColor { get; private set; }
		public string MySqlAddress { get; private set; }
		public int MySqlPort { get; private set; }
		public string MySqlDatabaseName { get; private set; }
		public string MySqlUsername { get; private set; }
		public string MySqlPassword { get; private set; }
		public string MySqlTableName { get; private set; }
		public const string UsePermission = "oxide.whitelist.update";
		bool configChanged;
		Permission permissions;
		private readonly Ext.MySql.Libraries.MySql mySql = Interface.GetMod().GetLibrary<Ext.MySql.Libraries.MySql>();
        private Ext.MySql.Connection connection;

		void OnServerInitialized()
        {
            permissions = Server.Permissions;
		}

		void Loaded()
		{
			// Load in the configuration data
			LoadConfigData();

			// Starts timer that keeps reloading the Whitelist.
			timer.Repeat(UpdateInterval, 0, UpdateWhitelist);

			// Prints to console whether announcements will be broadcast over chat
			if(EnableAnnouncements)
				PrintWarning($"Activated WITH announcements every {UpdateInterval} seconds!");
			else
				PrintWarning($"Activated WITHOUT announcements every {UpdateInterval} seconds!");

			// Prints to console whether MySql has been enabled.
			if(EnableMySqlConnection)
				PrintWarning("MySqlConnection IS enabled");
			else
				PrintWarning("MySqlConnection IS NOT enabled");
		}

		bool HasPermission(Player player, string perm = null){return PlayerExtensions.HasPermission(player, perm);}

		private void SendHelpText(Player player)
		{
			if(HasPermission(player, UsePermission)) PrintToChat(player, "/whitelist.update - Updates the whitelist.");
		}

		protected override void LoadDefaultConfig() => PrintWarning("New configuration file created.");

/*	-----------------------------------------------------------------------------------------------------	
	Command "Whitelist.Update" can be typed into the game console to force update the Whitelist without
	interrupting the next scheduled Whitelist update.
	-----------------------------------------------------------------------------------------------------	*/
		[ChatCommand("Whitelist.Update")]
		private void cmdUpdateWhitelist(Player player)
		{
			if(!HasPermission(player, UsePermission)){ PrintToChat(player, "You are not allowed to use this command."); return; }
			UpdateWhitelist();
			PrintToChat(player, "You have requested a manual whitelist update, this will not affect the next scheduled update.");
		}

		private void UpdateWhitelist()
		{
			if(EnableMySqlConnection)
			{
				/*	Building the query to execute
					"*" tells it to grab all fields in the row
					MySqlTableName is the table name defined in the config file. */
				MySqlSelect("*", MySqlTableName);
			}

			// Tells the server to use the updated Whitelist.cfg
			CodeHatch.Engine.Networking.Server.UserWhitelist.Load();

			// Print to the console that the Whitelist Update has occurred
			Puts("Whitelist has been updated");

			/*	If EnableAnnouncements is set to true in the config file, Broadcasts on the server that
				the Whitelist update has occurred. */
            if(EnableAnnouncements)
                PrintToChat($"[{AnnouncementPrefixColor}]{AnnouncementPrefix} [{AnnouncementTextColor}]: {AnnouncementText}");
		}

		void MySqlSelect(string SelectFields, string TableName)
		{
			// Connects to the database
			connection = mySql.OpenDb(MySqlAddress, MySqlPort, MySqlDatabaseName, MySqlUsername, MySqlPassword, this);

			var SelectData = $"SELECT {SelectFields} FROM `{TableName}`";
			// Build MySql Select Query
			var sql = Ext.MySql.Sql.Builder.Append(@SelectData);

			// Execute MySql Query
			mySql.Query(sql, connection, result =>
			{
				if(result == null) return;
				// Process each record
				foreach (var entry in result)
				{
					// Gets steamID from the Database and converts it to ulong so Whitelist will accept it
					var steamID = $"{entry["steamID"]}";
					ulong ulongID = Convert.ToUInt64(steamID);

					// Gets steamName used to register on the Whitelist
					var steamName = $"{entry["steamName"]}";

					// Gets Players IP they used when registering
					var accessIP = $"{entry["ip"]}";

					// Gets the Timestamp of when the registration happened
					var accessTimestamp = $"{entry["timestamp"]}";

					// Set ConsoleDebug to true in the config file to enable Debug messages in the console
					if(ConsoleDebug)
					Puts($"Adding {steamName} to the Whitelist...");

					// Adds the Record to the Whitelist
					CodeHatch.Engine.Networking.Server.UserWhitelist.Add(ulongID, steamName, $"IP that requested access: {accessIP}; Registered Steam Name: {steamName}; Timestamp: {accessTimestamp};");
				}
			});
		}

/*	-----------------------------------------------------------------------------------------------------	
	The rest of this Plugin deals with Loading in the config file data.
	-----------------------------------------------------------------------------------------------------	*/
		private void LoadConfigData()
        {
			Puts("Loading Configuration...");
			ConsoleDebug = GetConfigValue("Debug", "ConsoleDebug", DefaultConsoleDebug);
			EnableMySqlConnection = GetConfigValue("Settings", "EnableMySqlConnection", DefaultEnableMySqlConnection);
			EnableAnnouncements = GetConfigValue("Settings", "EnableAnnouncements", DefaultEnableAnnouncements);
			UpdateInterval = GetConfigValue("Settings", "UpdateInterval", DefaultUpdateInterval);
			AnnouncementPrefix = GetConfigValue("Messages", "AnnouncementPrefix", DefaultAnnouncementPrefix);
            AnnouncementPrefixColor = GetConfigValue("Messages", "AnnouncementPrefixColor", DefaultAnnouncementPrefixColor);	
			AnnouncementText = GetConfigValue("Messages", "AnnouncementText", DefaultAnnouncementText);
            AnnouncementTextColor = GetConfigValue("Messages", "AnnouncementTextColor", DefaultAnnouncementTextColor);	
			MySqlAddress = GetConfigValue("MySql Database", "MySqlAddress", DefaultMySqlAddress);
			MySqlPort = GetConfigValue("MySql Database", "MySqlPort", DefaultMySqlPort);
			MySqlDatabaseName = GetConfigValue("MySql Database", "MySqlDatabaseName", DefaultMySqlDatabaseName);
			MySqlUsername = GetConfigValue("MySql Database", "MySqlUsername", DefaultMySqlUsername);
			MySqlPassword = GetConfigValue("MySql Database", "MySqlPassword", DefaultMySqlPassword);
			MySqlTableName = GetConfigValue("MySql Database", "MySqlTableName", DefaultMySqlTableName);
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
