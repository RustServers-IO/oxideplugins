using System;
using System.Collections.Generic;

using Oxide;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
	

	[Info("SecurityLights", "S0N_0F_BISCUIT", "1.0.1", ResourceId = 2577)]
	class SecurityLights : RustPlugin
	{
		#region Variables
		[PluginReference]
		Plugin Clans, Vanish;

		public enum TargetMode { all, players, heli, lightshow };

		public class SecurityLight
		{
			public uint id;
			public SearchLight light { get; set; } = null;
			public BasePlayer owner { get; set; } = null;
			public TargetMode mode { get; set; } = TargetMode.all;
			public BaseCombatEntity target;
		}

		class ConfigData
		{
			public int allRadius;
			public int playerRadius;
			public int heliRadius;
			public bool autoConvert;
			public bool requireFuel;
		}

		class StoredData
		{
			public Dictionary<uint, SecurityLight> LightList { get; set; } = new Dictionary<uint, SecurityLight>();
		}

		private ConfigData config = new ConfigData();
		private StoredData data;
		private List<BaseCombatEntity> heliList = new List<BaseCombatEntity>();
		#endregion

		#region Localization
		private new void LoadDefaultMessages()
		{
			// English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["InvalidTarget"] = "Invalid Target!",
				["NoPermission"] = "You do not have permission to use this {0} light!",
				["Convert"] = "Converted to a security light.",
				["AlreadySL"] = "This is already a security light.",
				["Revert"] = "Converted to search light.",
				["NotSL"] = "This is not a security light.",
				["AllMode"] = "Targeting players and helicopters.",
				["PlayersMode"] = "Targeting only players.",
				["HeliMode"] = "Targeting only helicopters.",
				["LightshowMode"] = "Targeting owner for a lightshow!",
				["ModeUsage"] = "Usage: /sl mode <all|players|heli|lightshow>",
				["GlobalModeUsage"] = "Usage: /sl globalmode <all|players|heli|lightshow>",
				["GlobalChange"] = "Changed {0} light(s) to {1} mode.",
				["Unknown"] = "Unknown",
				["SecurityLight"] = "Security Light",
				["SearchLight"] = "Search Light",
				["NoCommandPermission"] = "You do not have permission to use this command!",
				["False"] = "False",
				["True"] = "True",
				["SecurityInfo"] = "Owner: {0}\nState: {1}\nMode: {2}\nTargeting: {3}",
				["SearchInfo"] = "Owner: {0}\nState: {1}",
				["DataReload"] = "Reloaded plugin data.",
				["ConfigReload"] = "Reloaded plugin config.",
				["ConfigInfo"] = "Configuration Info:\nRadius - All: {0}\nRadius - Players: {1}\nRadius - Helicopters: {2}\nAuto-Convert: {3}\nRequire Fuel: {4}",
				["AdminUsage"] = "Usage: /sl <add|remove|mode|globalmode|info|reloaddata|reloadconfig>",
				["Usage"] = "Usage: /sl <add|remove|mode|globalmode|info>"
			}, this);
		}
		#endregion

		#region Initialization
		//
		// Load config file
		//
		protected override void LoadDefaultConfig()
		{
			Config["Mode Radius - All"] = (int)ConfigValue("Mode Radius - All");
			Config["Mode Radius - Players"] = (int)ConfigValue("Mode Radius - Players");
			Config["Mode Radius - Helicopter"] = (int)ConfigValue("Mode Radius - Helicopter");
			Config["Auto Convert"] = ConfigValue("Auto Convert");
			Config["Require Fuel"] = ConfigValue("Require Fuel");
			
			SaveConfig();
		}
		//
		// Mod initialization
		//
		private void Init()
		{
			LoadConfig();
			LoadData();
		}
		//
		// Restore plugin data when server finishes startup
		//
		void OnServerInitialized()
		{
			RestoreData();
		}
		//
		// Unloading Plugin
		//
		void Unload()
		{
			SaveData();
		}
		#endregion

		#region Data Handling
		//
		// Load plugin data
		//
		private void LoadData()
		{
			try
			{
				data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("SecurityLights");
			}
			catch
			{
				data = new StoredData();
				SaveData();
			}
		}
		//
		// Save PlayerData
		//
		private void SaveData()
		{
			foreach (SecurityLight light in data.LightList.Values)
			{
				light.target = null;
				light.light = null;
				light.owner = null;
			}
			Interface.Oxide.DataFileSystem.WriteObject("SecurityLights", data);
		}
		//
		// Restore Data
		//
		private void RestoreData()
		{
			List<uint> removeIDs = new List<uint>();
			foreach (uint key in data.LightList.Keys)
			{
				try
				{
					data.LightList[key].light = (BaseNetworkable.serverEntities.Find(key) as SearchLight);
					data.LightList[key].owner = getPlayerFromID(data.LightList[key].light.OwnerID);
				}
				catch
				{
					removeIDs.Add(key);
				}
			}
			foreach (uint key in removeIDs)
			{
				data.LightList.Remove(key);
			}
		}
		//
		// Clear PlayerData
		//
		private void ClearData()
		{
			data = new StoredData();
			SaveData();
		}
		#endregion

		#region Chat Commands
		[ChatCommand("sl")]
		void manageSecurityLight(BasePlayer player, string command, string[] args)
		{
			var target = RaycastAll<BaseEntity>(player.eyes.HeadRay());
			SearchLight sl = null;
			BasePlayer owner = null;
			if (target is SearchLight)
			{
				sl = target as SearchLight;
				owner = getPlayerFromID(sl.OwnerID);
			}

			if (args.Length == 0)
				args = new string[] { String.Empty };
			switch (args[0].ToLower())
			{
				case "add":
					if (!(target is SearchLight))
					{
						PrintToChat(player, Lang("InvalidTarget", player.UserIDString));
						return;
					}
					if (!data.LightList.ContainsKey(sl.net.ID))
					{
						if (!isAuthed(player, sl))
						{
							PrintToChat(player, Lang("NoPermission", player.UserIDString, "search"));
							return;
						}
						SecurityLight newLight = new SecurityLight();
						newLight.id = sl.net.ID;
						newLight.light = sl;
						newLight.owner = owner;
						data.LightList.Add(sl.net.ID, newLight);
						SaveData();
						RestoreData();
						PrintToChat(player, Lang("Convert", player.UserIDString));
					}
					else
						PrintToChat(player, Lang("AlreadySL", player.UserIDString));
					return;
					break;
				case "remove":
					if (!(target is SearchLight))
					{
						PrintToChat(player, Lang("InvalidTarget", player.UserIDString));
						return;
					}
					if (data.LightList.ContainsKey(sl.net.ID))
					{
						if (!isAuthed(player, sl))
						{
							PrintToChat(player, Lang("NoPermission", player.UserIDString, "security"));
							return;
						}
						data.LightList.Remove(sl.net.ID);
						SaveData();
						RestoreData();
						PrintToChat(player, Lang("Revert", player.UserIDString));
					}
					else
						PrintToChat(player, Lang("NotSL", player.UserIDString));
					return;
					break;
				case "mode":
					if (!(target is SearchLight))
					{
						PrintToChat(player, Lang("InvalidTarget", player.UserIDString));
						return;
					}
					if (data.LightList.ContainsKey(sl.net.ID))
					{
						if (!isAuthed(player, sl))
						{
							PrintToChat(player, Lang("NoPermission", player.UserIDString, "security"));
							return;
						}
						string option = String.Empty;
						if (args.Length == 2)
							option = args[1].ToLower();
						switch (option)
						{
							case "all":
								data.LightList[sl.net.ID].mode = TargetMode.all;
								PrintToChat(player, Lang("AllMode", player.UserIDString));
								break;
							case "players":
								data.LightList[sl.net.ID].mode = TargetMode.players;
								PrintToChat(player, Lang("PlayersMode", player.UserIDString));
								break;
							case "heli":
								data.LightList[sl.net.ID].mode = TargetMode.heli;
								PrintToChat(player, Lang("HeliMode", player.UserIDString));
								break;
							case "lightshow":
								data.LightList[sl.net.ID].mode = TargetMode.lightshow;
								PrintToChat(player, Lang("LightshowMode", player.UserIDString));
								break;
							default:
								PrintToChat(player, Lang("ModeUsage", player.UserIDString));
								return;
						}
						SaveData();
						RestoreData();
					}
					else
						PrintToChat(player, Lang("NotSL", player.UserIDString));
					return;
					break;
				case "globalmode":
					TargetMode globalmode;
					int lightsChanged = 0;
					string option2 = String.Empty;
					if (args.Length == 2)
						option2 = args[1].ToLower();
					switch (option2)
					{
						case "all":
							globalmode = TargetMode.all;
							break;
						case "players":
							globalmode = TargetMode.players;
							break;
						case "heli":
							globalmode = TargetMode.heli;
							break;
						case "lightshow":
							globalmode = TargetMode.lightshow;
							break;
						default:
							PrintToChat(player, Lang("GlobalModeUsage", player.UserIDString));
							return;
					}
					foreach (SecurityLight currentLight in data.LightList.Values)
					{
						if (currentLight.owner == player)
						{
							currentLight.mode = globalmode;
							lightsChanged++;
						}
					}
					PrintToChat(player, Lang("GlobalChange", player.UserIDString, lightsChanged, globalmode));
					return;
					break;
				case "info":
					if (!(target is SearchLight))
					{
						PrintToChat(player, Lang("InvalidTarget", player.UserIDString));
						return;
					}
					if (!isAuthed(player, sl) && !player.IsAdmin)
					{
						PrintToChat(player, Lang("NoCommandPermission", player.UserIDString));
						return;
					}
					string ownerString = Lang("Unknown");
					if (owner != null)
						ownerString = owner.displayName;
					string stateString = data.LightList.ContainsKey(sl.net.ID) ? Lang("SecurityLight", player.UserIDString) : Lang("SearchLight", player.UserIDString);
					if (stateString == "Security Light")
					{
						string modeString = data.LightList[sl.net.ID].mode.ToString();
						string targeting = data.LightList[sl.net.ID].target == null ? Lang("False", player.UserIDString) : Lang("True", player.UserIDString);
						PrintToChat(player, Lang("SecurityInfo", player.UserIDString, ownerString, stateString, modeString, targeting));
					}
					else
						PrintToChat(player, Lang("SearchInfo", player.UserIDString, ownerString, stateString));
					return;
					break;
				case "reloaddata":
					if (!player.IsAdmin)
					{
						PrintToChat(player, Lang("NoCommandPermission", player.UserIDString));
						return;
					}
					RestoreData();
					PrintToChat(player, Lang("DataReload", player.UserIDString));
					return;
					break;
				case "reloadconfig":
					if (!player.IsAdmin)
					{
						PrintToChat(player, Lang("NoCommandPermission", player.UserIDString));
						return;
					}
					Config.Load();
					LoadConfig();
					PrintToChat(player, Lang("ConfigReload", player.UserIDString));
					PrintToChat(player, Lang("ConfigInfo", player.UserIDString, config.allRadius, config.playerRadius, config.heliRadius, config.autoConvert, config.requireFuel));
					return;
					break;
				default:
					if (player.IsAdmin)
						PrintToChat(player, Lang("AdminUsage", player.UserIDString));
					else
						PrintToChat(player, Lang("Usage", player.UserIDString));
					return;
					break;
			}
			return;
		}
		#endregion

		#region Functionality
		//
		// Update the target info on all security lights
		//
		void OnTick()
		{
			List<uint> removeIDs = new List<uint>();
			foreach (SecurityLight sl in data.LightList.Values)
			{
				try
				{
					Item slot = sl.light.inventory.GetSlot(0);
					if ((slot == null || (UnityEngine.Object)slot.info != (UnityEngine.Object)sl.light.inventory.onlyAllowedItem) && config.requireFuel)
					{
						sl.light.SetFlag(BaseEntity.Flags.On, false);
						continue;
					}
					if (sl.light.IsMounted())
						continue;
					List<BaseCombatEntity> list = Facepunch.Pool.GetList<BaseCombatEntity>();
					list.AddRange(heliList);
					if (list == null) continue;
					Vis.Entities<BaseCombatEntity>(sl.light.eyePoint.transform.position, sl.mode == TargetMode.heli ? config.heliRadius : sl.mode == TargetMode.players ? config.playerRadius : config.allRadius, list, 133120, QueryTriggerInteraction.Collide);
					if (list == null) continue;
					if (sl.mode == TargetMode.players)
						list.RemoveAll(x => !(x is BasePlayer));
					else if (sl.mode == TargetMode.heli)
						list.RemoveAll(x => !(x is BaseHelicopter));
					else if (sl.mode == TargetMode.all)
						list.RemoveAll(x => !((x is BasePlayer) || (x is BaseHelicopter)));

					if (sl.mode == TargetMode.lightshow)
					{
						sl.target = sl.owner;
						sl.light.SetTargetAimpoint(sl.target.transform.position + Vector3.up);
					}
					else if (list.Count == 0)
					{
						sl.target = null;
					}
					else if (list.Contains(sl.target))
					{
						if (sl.target is BasePlayer)
						{
							if (shouldTarget(sl.target as BasePlayer, sl) && isTargetVisible(sl, sl.target))
								sl.light.SetTargetAimpoint(sl.target.transform.position + Vector3.up);
							else
								sl.target = null;
						}
						else
							sl.light.SetTargetAimpoint(sl.target.transform.position);
					}
					if (sl.target == null)
					{
						foreach (BaseCombatEntity entity in list)
						{
							if ((sl.mode != TargetMode.heli && isOwnerTargeting(sl.owner, entity)) || !isTargetVisible(sl, entity) || sl.target != null)
								continue;
							if (entity is BasePlayer)
							{
								if (shouldTarget(entity as BasePlayer, sl))
								{
									sl.target = entity;
									sl.light.SetTargetAimpoint(entity.transform.position + Vector3.up);
								}
							}
							else
							{
								sl.target = entity;
								sl.light.SetTargetAimpoint(entity.transform.position);
							}
						}
					}
					if (sl.target == null)
					{
						sl.light.SetTargetAimpoint(sl.light.eyePoint.transform.position + Vector3.down * 3);
						sl.light.SetFlag(BaseEntity.Flags.On, false);
					}
					else
					{
						if (!config.requireFuel && sl.light.inventory.GetSlot(0) == null)
							sl.light.inventory.AddItem(sl.light.inventory.onlyAllowedItem, 1);
						sl.light.SetFlag(BaseEntity.Flags.On, true);
					}
					sl.light.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
				}
				catch
				{
					removeIDs.Add(sl.id);
				}
			}
			foreach (uint ID in removeIDs)
			{
				data.LightList.Remove(ID);
			}
			if (removeIDs.Count > 0)
			{
				SaveData();
				RestoreData();
			}
		}
		//
		// Check for heli spawn or searchlight placed
		//
		void OnEntitySpawned(BaseNetworkable entity)
		{
			if (entity is BaseHelicopter)
			{
				heliList.Add(entity as BaseCombatEntity);
			}
			else if (entity is SearchLight && config.autoConvert)
			{
				SecurityLight newLight = new SecurityLight() { id = entity.net.ID, light = entity as SearchLight, owner = BasePlayer.FindByID((entity as SearchLight).OwnerID) };
				data.LightList.Add(entity.net.ID, newLight);
				SaveData();
				RestoreData();
			}
		}
		//
		// Check for heli death
		//
		void OnEntityKill(BaseNetworkable entity)
		{
			if (entity is BaseHelicopter)
				if (heliList.Contains(entity as BaseCombatEntity))
					heliList.Remove(entity as BaseCombatEntity);
		}
		//
		// Don't consume fuel for search lights
		//
		void OnItemUse(Item item, int amountToUse)
		{
			try
			{
				if (item.parent.entityOwner is SearchLight && !config.requireFuel)
				{
					item.parent.AddItem(item.info, 1);
				}
			}
			catch { }
		}
		#endregion

		#region Helpers
		//
		// Get config value
		//
		private object ConfigValue(string value)
		{
			switch (value)
			{
				case "Mode Radius - All":
					if (Config[value] == null)
						return 30;
					else
						return Config[value];
					break;
				case "Mode Radius - Players":
					if (Config[value] == null)
						return 30;
					else
						return Config[value];
					break;
				case "Mode Radius - Helicopter":
					if (Config[value] == null)
						return 100;
					else
						return Config[value];
					break;
				case "Auto Convert":
					if (Config[value] == null)
						return false;
					else
						return Config[value];
					break;
				case "Require Fuel":
					if (Config[value] == null)
						return true;
					else
						return Config[value]; ;
					break;
				default:
					return null;
					break;
			}
		}
		//
		// Load the config values to the config class
		//
		private void LoadConfig()
		{
			config.allRadius = (int)Config["Mode Radius - All"];
			config.playerRadius = (int)Config["Mode Radius - Players"];
			config.heliRadius = (int)Config["Mode Radius - Helicopter"];
			config.autoConvert = (bool)Config["Auto Convert"];
			config.requireFuel = (bool)Config["Require Fuel"];
		}
		//
		// Get string and format from lang file
		//
		private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
		//
		// Get player name from ID
		//
		protected BasePlayer getPlayerFromID(ulong id)
		{
			if (string.IsNullOrEmpty(id.ToString()))
				return null;

			IPlayer player = covalence.Players.FindPlayer(id.ToString());

			if (player.Object != null)
			{
				return (BasePlayer)player.Object;
			}
			else
			{
				foreach (BasePlayer current in BasePlayer.activePlayerList)
				{
					if (current.userID == id)
						return current;
				}
				
				foreach (BasePlayer current in BasePlayer.sleepingPlayerList)
				{
					if (current.userID == id)
						return current;
				}
			}
			return null;
		}
		//
		// Check if searchlight from owner/clan is already targeting entity
		//
		private bool isOwnerTargeting(BasePlayer owner, BaseCombatEntity target)
		{
			foreach (SecurityLight sl in data.LightList.Values)
			{
				if (sl.owner == owner && sl.target == target)
					return true;
			}
			return false;
		}
		//
		// Check if player is authorized
		//
		public bool isAuthed(BasePlayer player, SearchLight light)
		{
			BasePlayer owner = getPlayerFromID(light.OwnerID);
			if (owner == null)
				return false;
			if (owner == player)
				return true;
			else if (Clans)
			{
				string ownerClan = (string)(Clans.CallHook("GetClanOf", owner));
				string playerClan = (string)(Clans.CallHook("GetClanOf", player));
				if (ownerClan == playerClan && !String.IsNullOrEmpty(ownerClan))
					return true;
			}
			return false;
		}
		public bool isAuthed(BasePlayer player, SecurityLight sl)
		{
			BasePlayer owner = sl.owner;
			if (owner == null)
				return false;
			if (owner == player)
				return true;
			else if (Clans)
			{
				string ownerClan = (string)(Clans.CallHook("GetClanOf", owner));
				string playerClan = (string)(Clans.CallHook("GetClanOf", player));
				if (ownerClan == playerClan && !String.IsNullOrEmpty(ownerClan))
					return true;
			}
			return false;
		}
		//
		// Check if player should be targeted
		//
		private bool shouldTarget(BasePlayer player, SecurityLight sl)
		{
			if (isAuthed(player, sl))
				return false;
			if (getPlayerFromID(player.userID) == null)
				return false;
			else if (player.HasPlayerFlag(BasePlayer.PlayerFlags.HasBuildingPrivilege))
				return false;
			else if (player.IsDucked() && player != sl.light.lastAttacker)
				return false;
			object invisible = Vanish?.Call("IsInvisible", player);
			if (invisible is bool)
			{
				if ((bool)invisible)
					return false;
			}
			if (!closestLight(player, sl.id) && sl.target == null)
				return false;
			return true;
		}
		//
		// Find entity the player is looking at
		//
		private object RaycastAll<T>(Ray ray) where T : BaseEntity
		{
			var hits = Physics.RaycastAll(ray);
			GamePhysics.Sort(hits);
			var distance = 100f;
			object target = false;
			foreach (var hit in hits)
			{
				var ent = hit.GetEntity();
				if (ent is T && hit.distance < distance)
				{
					target = ent;
					break;
				}
			}
			return target;
		}
		//
		// Find potential target for security light
		//
		private object RaycastAll<T>(Ray ray, float distance) where T : BaseEntity
		{
			var hits = Physics.RaycastAll(ray);
			GamePhysics.Sort(hits);
			object target = false;
			foreach (var hit in hits)
			{
				var ent = hit.GetEntity();
				if (ent is T && hit.distance < distance)
				{
					target = ent;
					break;
				}
			}
			return target;
		}
		//
		// Check if security light can see the target
		//
		private bool isTargetVisible(SecurityLight sl, BaseCombatEntity target)
		{
			Ray ray = new Ray(sl.light.eyePoint.transform.position, (target is BasePlayer ? (target.transform.position + Vector3.up) : target.transform.position) - sl.light.eyePoint.transform.position);
			float distance = (sl.mode == TargetMode.all ? config.allRadius : (sl.mode == TargetMode.players ? config.playerRadius : config.heliRadius));
			var foundEntity = RaycastAll<BaseEntity>(ray, distance);

			if (foundEntity is BaseCombatEntity)
			{
				if (target == foundEntity as BaseCombatEntity)
				{
					return true;
				}
			}
			return false;
		}
		//
		// Make sure player is being targeted by the closest light
		//
		private bool closestLight(BasePlayer target, uint id)
		{
			float distance = float.MaxValue;
			uint closestID = 0;

			foreach (SecurityLight sl in data.LightList.Values)
			{
				Vector3 line = target.transform.position + Vector3.up - sl.light.eyePoint.transform.position;
				if (Vector3.Magnitude(line) < distance)
				{
					distance = Vector3.Magnitude(line);
					closestID = sl.id;
				}
			}
			if (closestID == id)
				return true;
			return false;
		}
		#endregion
	}
}