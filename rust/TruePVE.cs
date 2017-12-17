
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("TruePVE", "ignignokt84", "0.8.8", ResourceId = 1789)]
	[Description("Improvement of the default Rust PVE behavior")]
	class TruePVE : RustPlugin
	{
		#region Variables

		static TruePVE Instance;

		// config/data container
		TruePVEData data = new TruePVEData();

		// ZoneManager plugin
		[PluginReference]
		Plugin ZoneManager;

		// LiteZone plugin (private)
		[PluginReference]
		Plugin LiteZones;
		
		// usage information string with formatting
		public string usageString;
		// valid commands
		enum Command { def, sched, trace, usage };
		// valid configuration options
		public enum Option {
			handleDamage,		// (true)	enable TruePVE damage handling hooks
			useZones			// (true)	use ZoneManager/LiteZones for zone-specific damage behavior (requires modification of ZoneManager.cs)
		};
		// default values array
		bool[] defaults = {
			true,	// handleDamage
			true	// useZones
		};

		// flags for RuleSets
		[Flags]
		enum RuleFlags
		{
			None = 0,
			SuicideBlocked = 1,
			AuthorizedDamage = 1 << 1,
			NoHeliDamage = 1 << 2,
			HeliDamageLocked = 1 << 3,
			NoHeliDamagePlayer = 1 << 4,
			HumanNPCDamage = 1 << 5,
			LockedBoxesImmortal = 1 << 6,
			LockedDoorsImmortal = 1 << 7,
			AdminsHurtSleepers = 1 << 8,
			ProtectedSleepers = 1 << 9,
			TrapsIgnorePlayers = 1 << 10,
			TurretsIgnorePlayers = 1 << 11,
			CupboardOwnership = 1 << 12
		}
		// timer to check for schedule updates
		Timer scheduleUpdateTimer;
		// current ruleset
		RuleSet currentRuleSet;
		// current broadcast message
		string currentBroadcastMessage;
		// internal useZones flag
		bool useZones = false;
		// constant "any" string for rules
		const string Any = "any";
		// constant "allzones" string for mappings
		const string AllZones = "allzones";
		// flag to prevent certain things from happening before server initialized
		bool serverInitialized = false;
		// permission for mapping command
		string PermCanMap = "truepve.canmap";
		
		// trace flag
		bool trace = false;
		// tracefile name
		string traceFile = "ruletrace";
		// auto-disable trace after 300s (5m)
		float traceTimeout = 300f;
		// trace timeout timer
		Timer traceTimer;

		#endregion

		#region Lang

		// load default messages to Lang
		void LoadDefaultMessages()
		{
			var messages = new Dictionary<string, string>
			{
				{"Prefix", "<color=#FFA500>[ TruePVE ]</color>" },

				{"Header_Usage", "---- TruePVE usage ----"},
				{"Cmd_Usage_def", "Loads default configuration and data"},
				{"Cmd_Usage_sched", "Enable or disable the schedule" },
				{"Cmd_Usage_prod", "Show the prefab name and type of the entity being looked at"},
				{"Cmd_Usage_map", "Create/remove a mapping entry" },
				{"Cmd_Usage_trace", "Toggle tracing on/off" },

				{"Warning_PveMode", "Server is set to PVE mode!  TruePVE is designed for PVP mode, and may cause unexpected behavior in PVE mode."},
				{"Warning_OldConfig", "Old config detected - moving to {0}" },
				{"Warning_NoRuleSet", "No RuleSet found for \"{0}\"" },
				{"Warning_DuplicateRuleSet", "Multiple RuleSets found for \"{0}\"" },

				{"Error_InvalidCommand", "Invalid command" },
				{"Error_InvalidParameter", "Invalid parameter: {0}"},
				{"Error_InvalidParamForCmd", "Invalid parameters for command \"{0}\""},
				{"Error_InvalidMapping", "Invalid mapping: {0} => {1}; Target must be a valid RuleSet or \"exclude\"" },
				{"Error_NoMappingToDelete", "Cannot delete mapping: \"{0}\" does not exist" },
				{"Error_NoPermission", "Cannot execute command: No permission"},
				{"Error_NoSuicide", "You are not allowed to commit suicide"},
				{"Error_NoEntityFound", "No entity found"},
				
				{"Notify_AvailOptions", "Available Options: {0}"},
				{"Notify_DefConfigLoad", "Loaded default configuration"},
				{"Notify_DefDataLoad", "Loaded default mapping data"},
				{"Notify_ProdResult", "Prod results: type={0}, prefab={1}"},
				{"Notify_SchedSetEnabled", "Schedule enabled" },
				{"Notify_SchedSetDisabled", "Schedule disabled" },
				{"Notify_InvalidSchedule", "Schedule is not valid" },
				{"Notify_MappingCreated", "Mapping created for \"{0}\" => \"{1}\"" },
				{"Notify_MappingUpdated", "Mapping for \"{0}\" changed from \"{1}\" to \"{2}\"" },
				{"Notify_MappingDeleted", "Mapping for \"{0}\" => \"{1}\" deleted" },
				{"Notify_TraceToggle", "Trace mode toggled {0}" },
				
				{"Format_NotifyColor", "#00FFFF"}, // cyan
				{"Format_NotifySize", "12"},
				{"Format_HeaderColor", "#FFA500"}, // orange
				{"Format_HeaderSize", "14"},
				{"Format_ErrorColor", "#FF0000"}, // red
				{"Format_ErrorSize", "12"},
			};
			lang.RegisterMessages(messages, this);
        }
        
        // get message from Lang
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

		#endregion

		#region Loading/Unloading

		// load things
		void Loaded()
		{
			Instance = this;
			LoadDefaultMessages();
			string baseCommand = "tpve";
			// register console commands automagically
			foreach(Command command in Enum.GetValues(typeof(Command)))
				cmd.AddConsoleCommand((baseCommand + "." + command.ToString()), this, "CommandDelegator");
			// register chat commands
			cmd.AddChatCommand(baseCommand + "_prod", this, "HandleProd");
			cmd.AddChatCommand(baseCommand, this, "ChatCommandDelegator");
			// build usage string for console (without sizing)
			usageString = WrapColor("orange", GetMessage("Header_Usage")) + "\n" +
						  WrapColor("cyan", $"{baseCommand}.{Command.def.ToString()}") + $" - {GetMessage("Cmd_Usage_def")}{Environment.NewLine}" +
						  WrapColor("cyan", $"{baseCommand}.{Command.trace.ToString()}") + $" - {GetMessage("Cmd_Usage_trace")}{Environment.NewLine}" +
						  WrapColor("cyan", $"{baseCommand}.{Command.sched.ToString()} [enable|disable]") + $" - {GetMessage("Cmd_Usage_sched")}{Environment.NewLine}" +
						  WrapColor("cyan", $"/{baseCommand}_prod") + $" - {GetMessage("Cmd_Usage_prod")}{Environment.NewLine}" +
						  WrapColor("cyan", $"/{baseCommand} map") + $" - {GetMessage("Cmd_Usage_map")}";
			permission.RegisterPermission(PermCanMap, this);
		}

		// on unloaded
		void Unload()
		{
			if(scheduleUpdateTimer != null)
				scheduleUpdateTimer.Destroy();
			Instance = null;
		}
		
		// plugin loaded
		void OnPluginLoaded(Plugin plugin)
		{
			if(plugin.Name == "ZoneManager")
				ZoneManager = plugin;
			if (plugin.Name == "LiteZones")
				LiteZones = plugin;
			if (!serverInitialized) return;
			if (ZoneManager != null || LiteZones != null)
				useZones = data.config[Option.useZones];
		}

		// plugin unloaded
		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ZoneManager")
				ZoneManager = null;
			if (plugin.Name == "LiteZones")
				LiteZones = null;
			if (!serverInitialized) return;
			if (ZoneManager == null && LiteZones == null)
				useZones = false;
			traceTimer?.Destroy();
		}

		// server initialized
		void OnServerInitialized()
		{
			// check for server pve setting
			if (ConVar.Server.pve)
				WarnPve();
			// load configuration
			LoadConfiguration();
			data.Init();
			currentRuleSet = data.GetDefaultRuleSet();
			if (currentRuleSet == null)
				PrintWarning(GetMessage("Warning_NoRuleSet"), data.defaultRuleSet);
			useZones = data.config[Option.useZones] && (LiteZones != null || ZoneManager != null);
			if (useZones && data.mappings.Count == 1 && data.mappings.First().Key.Equals(data.defaultRuleSet))
				useZones = false;
			if (data.schedule.enabled)
				TimerLoop(true);
			serverInitialized = true;
		}

		#endregion

		#region Command Handling

		// delegation method for console commands
		void CommandDelegator(ConsoleSystem.Arg arg)
		{
			// return if user doesn't have access to run console command
			if(!HasAccess(arg)) return;
			
			string cmd = arg.cmd.Name;
			if(!Enum.IsDefined(typeof(Command), cmd))
			{
				// shouldn't hit this
				SendMessage(arg, "Error_InvalidParameter");
			}
			else
			{
				switch((Command) Enum.Parse(typeof(Command), cmd))
				{
					case Command.def:
						HandleDef(arg);
						return;
					case Command.sched:
						HandleScheduleSet(arg);
						return;
					case Command.trace:
						trace = !trace;
						SendMessage(arg, "Notify_TraceToggle", new object[] { trace ? "on" : "off" });
						if (trace)
							traceTimer = timer.In(traceTimeout, () => trace = false);
						else
							traceTimer?.Destroy();
						return;
					case Command.usage:
						ShowUsage(arg);
						return;
				}
				SendMessage(arg, "Error_InvalidParamForCmd", new object[] {cmd});
			}
			ShowUsage(arg);
		}

		// handle setting defaults
		void HandleDef(ConsoleSystem.Arg arg)
		{
			LoadDefaultConfiguration();
			SendMessage(arg, "Notify_DefConfigLoad");
			LoadDefaultData();
			SendMessage(arg, "Notify_DefDataLoad");
			
			SaveData();
		}
		
		// handle prod command (raycast to determine what player is looking at)
		void HandleProd(BasePlayer player, string command, string[] args)
		{
			if(!IsAdmin(player))
				SendMessage(player, "Error_NoPermission");
			
			object entity;
			if(!GetRaycastTarget(player, out entity) || entity == null)
			{
				SendReply(player, WrapSize(12, WrapColor("red", GetMessage("Error_NoEntityFound", player.UserIDString))));
				return;
			}
			SendMessage(player, "Notify_ProdResult", new object[] { entity.GetType(), (entity as BaseEntity).ShortPrefabName });
		}

		// delegation method for chat commands
		void ChatCommandDelegator(BasePlayer player, string command, string[] args)
		{
			if (!hasPermission(player, PermCanMap))
			{
				SendMessage(player, "Error_NoPermission");
				return;
			}

			// assume args[0] is the command (beyond /tpve)
			if (args != null && args.Length > 0)
				command = args[0];
			// shift arguments
			if (args != null)
			{
				if (args.Length > 1)
					args = args.Skip(1).ToArray();
				else
					args = new string[] { };
			}

			string message = "";
			object[] opts = new object[] { };

			if (command == null || command != "map")
			{
				message = "Error_InvalidCommand";
			}
			else if (args == null || args.Length == 0)
			{
				message = "Error_InvalidParamForCmd";
				opts = new object[] { command };
			}
			else
			{
				// args[0] should be mapping name
				// args[1] if exists should be target ruleset or "exclude"
				// if args[1] is empty, delete mapping
				string from = args[0];
				string to = null;
				if(args.Length == 2)
					to = args[1];

				if (to != null && !data.ruleSets.Select(r => r.name).Contains(to) && to != "exclude")
				{
					// target ruleset must exist, or be "exclude"
					message = "Error_InvalidMapping";
					opts = new object[] { from, to };
				}
				else
				{
					bool dirty = false;
					if (to != null)
					{
						dirty = true;
						if (data.HasMapping(from))
						{
							// update existing mapping
							string old = data.mappings[from];
							data.mappings[from] = to;
							message = "Notify_MappingUpdated";
							opts = new object[] { from, old, to };
						}
						else
						{
							// add new mapping
							data.mappings.Add(from, to);
							message = "Notify_MappingCreated";
							opts = new object[] { from, to };
						}
					}
					else
					{
						if (data.HasMapping(from))
						{
							dirty = true;
							// remove mapping
							string old = data.mappings[from];
							data.mappings.Remove(from);
							message = "Notify_MappingDeleted";
							opts = new object[] { from, old };
						}
						else
						{
							message = "Error_NoMappingToDelete";
							opts = new object[] { from };
						}
					}

					if(dirty)
						// save changes to config file
						SaveData();
				}
			}
			SendMessage(player, message, opts);
		}

		// handles schedule enable/disable
		void HandleScheduleSet(ConsoleSystem.Arg arg)
		{
			if (arg == null || arg.Args == null || arg.Args.Length == 0)
			{
				SendMessage(arg, "Error_InvalidParamForCmd");
				return;
			}
			string message = "";
			if(!data.schedule.valid)
			{
				message = "Notify_InvalidSchedule";
			}
			else if(arg.Args[0] == "enable")
			{
				if(data.schedule.enabled) return;
				data.schedule.enabled = true;
				TimerLoop();
				message = "Notify_SchedSetEnabled";
			}
			else if(arg.Args[0] == "disable")
			{
				if (!data.schedule.enabled) return;
				data.schedule.enabled = false;
				if (scheduleUpdateTimer != null)
					scheduleUpdateTimer.Destroy();
				message = "Notify_SchedSetDisabled";
			}
			object[] opts = new object[] { };
			if(message == "")
			{
				message = "Error_InvalidParameter";
				opts = new object[] { arg.Args[0] };
			}
			SendMessage(arg, message, opts);
		}

		#endregion

		#region Configuration/Data

		// load config
		void LoadConfiguration()
		{
			CheckVersion();
			Config.Settings.NullValueHandling = NullValueHandling.Include;
			bool dirty = false;
			try {
				data = Config.ReadObject<TruePVEData>() ?? null;
			} catch (Exception) {
				data = new TruePVEData();
			}
			if (data == null)
				LoadDefaultConfig();
			
			dirty |= CheckConfig();
			dirty |= CheckData();
			// check config version, update version to current version
			if (data.configVersion == null || !data.configVersion.Equals(Version.ToString()))
			{
				data.configVersion = Version.ToString();
				dirty |= true;
			}
			if (dirty)
				SaveData();
		}
		
		// save data
		void SaveData() => Config.WriteObject(data);
		
		// verify/update configuration
		bool CheckConfig()
		{
			bool dirty = false;
			foreach(Option option in Enum.GetValues(typeof(Option)))
				if(!data.config.ContainsKey(option))
				{
					data.config[option] = defaults[(int)option];
					dirty = true;
				}
			return dirty;
		}

		// check rulesets and groups
		bool CheckData()
		{
			bool dirty = false;
			if ((data.ruleSets == null || data.ruleSets.Count == 0) ||
				(data.groups == null || data.groups.Count == 0))
				dirty = LoadDefaultData();
			if (data.schedule == null)
			{
				data.schedule = new Schedule();
				dirty = true;
			}
			dirty |= CheckMappings();
			return dirty;
		}

		// rebuild mappings
		bool CheckMappings()
		{
			bool dirty = false;
			foreach (RuleSet rs in data.ruleSets)
				if (!data.mappings.ContainsValue(rs.name))
				{
					data.mappings[rs.name] = rs.name;
					dirty = true;
				}
			return dirty;
		}

		// default config creation
		protected override void LoadDefaultConfig()
		{
			data = new TruePVEData();
			data.configVersion = Version.ToString();
			LoadDefaultConfiguration();
			LoadDefaultData();
			SaveData();
		}

		void CheckVersion()
		{
			if (Config["configVersion"] == null) return;
			Version config = new Version(Config["configVersion"].ToString());
			if (config < new Version("0.7.0"))
			{
				string fname = Config.Filename.Replace(".json", ".old.json");
				Config.Save(fname);
				PrintWarning(string.Format(GetMessage("Warning_OldConfig"), fname));
				Config.Clear();
			}
		}

		// populates default configuration entries
		bool LoadDefaultConfiguration()
		{
			foreach (Option option in Enum.GetValues(typeof(Option)))
				data.config[option] = defaults[(int)option];
			return true;
		}

		// load default data to mappings, rulesets, and groups
		bool LoadDefaultData()
		{
			data.mappings.Clear();
			data.ruleSets.Clear();
			data.groups.Clear();
			data.schedule = new Schedule();
			data.defaultRuleSet = "default";

			// build groups first
			EntityGroup dispenser = new EntityGroup("dispensers");
			dispenser.Add(typeof(BaseCorpse).Name);
			dispenser.Add(typeof(HelicopterDebris).Name);
			data.groups.Add(dispenser);

			EntityGroup players = new EntityGroup("players");
			players.Add(typeof(BasePlayer).Name);
			data.groups.Add(players);

			EntityGroup traps = new EntityGroup("traps");
			traps.Add(ty