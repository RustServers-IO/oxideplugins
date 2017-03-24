
using System.Collections.Generic;
using Facepunch;
using ProtoBuf;
using Facepunch.Math;
using UnityEngine;
using System;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
	[Info("BedShare", "ignignokt84", "0.0.4", ResourceId = 2343)]
	[Description("Bed sharing plugin")]
	class BedShare : RustPlugin
	{
		#region Variables

		BagData data = new BagData();
		Dictionary<ulong, string> playerNameCache = new Dictionary<ulong, string>();
		Dictionary<uint, ulong> dummyBags = new Dictionary<uint, ulong>();
		Dictionary<uint, ulong> guiCache = new Dictionary<uint, ulong>();

		const string PermCanUse = "bedshare.use";
		const string PermNoShare = "bedshare.noshare";
		const string PermCanSharePublic = "bedshare.public";
		const string PermCanSharePrivate = "bedshare.private";
		const string PermCanClear = "bedshare.canclear";

		const string BedPrefabName = "bed_deployed";

		CuiElementContainer SharedBagGUI;
		const string SharedBagGUIName = "SharedBagOverlay";

		enum Command { clear, killgui, share, sharewith, status, unshare, unsharewith };

		#endregion

		#region Lang

		// load default messages to Lang
		void LoadDefaultMessages()
		{
			var messages = new Dictionary<string, string>
			{
				{"Prefix", "<color=orange>[ BedShare ]</color> "},
				{"CannotShareOther", "You cannot {1} another player's {0}"},
				{"ShareSuccess", "This {0} is now {1}" },
				{"NotShared", "This {0} is not shared" },
				{"NoBag", "No bag or bed found" },
				{"ClearSuccess", "Successfully cleared {0} bags/beds" },
				{"NoClearPerm", "You do not have permission to clear shared beds/bags" },
				{"CommandList", "<color=cyan>Valid Commands:</color>" + System.Environment.NewLine + "{0}"},
				{"Status", "This {0} is currently {1}" },
				{"ValidateStats", "Shared bag/bed mappings validated - {0} removed" },
				{"SharedHeaderText", "Spawn in Shared Sleeping Bag" },
				{"SharedBagNameText", "{0} (public) [Shared by {1}]" },
				{"SharedBagNameTextPrivate", "{0} (private) [Shared by {1}]" },
				{"InvalidArguments", "Invalid arguments for command: {0}" },
				{"PlayersNotFound", "Unable to find player(s): {0}" }
			};
			lang.RegisterMessages(messages, this);
		}
		
		// get message from Lang
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

		#endregion

		#region Loading/Unloading

		// on load
		void Loaded()
		{
			LoadDefaultMessages();
			// register both /bag and /bed since they're really ambiguous
			cmd.AddChatCommand("bag", this, "CommandDelegator");
			cmd.AddChatCommand("bed", this, "CommandDelegator");
			permission.RegisterPermission(PermCanUse, this);
			permission.RegisterPermission(PermNoShare, this);
			permission.RegisterPermission(PermCanSharePublic, this);
			permission.RegisterPermission(PermCanSharePrivate, this);
			permission.RegisterPermission(PermCanClear, this);
			LoadData();
		}

		// unload
		void Unload()
		{
			DestroyAllDummyBags();
			DestroyAllGUI();
		}

		// server initialized
		void OnServerInitialized()
		{
			ValidateSharedBags();
			InitGUI();
		}

		// initialize GUI elements
		void InitGUI()
		{
			SharedBagGUI = new CuiElementContainer();
		}

		#endregion

		#region Configuration

		// load default config
		bool LoadDefaultConfig()
		{
			data = new BagData();
			CheckConfig();
			return true;
		}

		// load data
		void LoadData()
		{
			bool dirty = false;
			Config.Settings.NullValueHandling = NullValueHandling.Include;
			try
			{
				data = Config.ReadObject<BagData>();
			}
			catch (Exception) { }
			dirty = CheckConfig();
			if (data.sharedBags == null)
				dirty |= LoadDefaultConfig();
			if (dirty)
				SaveData();
		}

		// write data container to config
		void SaveData()
		{
			Config.WriteObject(data);
		}

		// get value from config (handles type conversion)
		T GetConfig<T>(string group, string name, T value)
		{
			if (Config[group, name] == null)
			{
				Config[group, name] = value;
				SaveConfig();
			}
			return (T)Convert.ChangeType(Config[group, name], typeof(T));
		}

		// validate config
		bool CheckConfig()
		{
			bool dirty = false;
			if (data.ui == null)
			{
				data.ui = new UIConfig();
				dirty = true;
			}
			return dirty;
		}

		#endregion

		#region Command Handling

		// command delegator
		void CommandDelegator(BasePlayer player, string command, string[] args)
		{
			if (!hasPermission(player, PermCanUse))
			{
				if (Enum.IsDefined(typeof(Command), command) && (Command)Enum.Parse(typeof(Command), command) == Command.killgui)
					DestroyGUI(player);
				return;
			}
			string message = "InvalidCommand";
			// assume args[0] is the command (beyond /bed)
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
			object[] opts = new object[] { command };
			if (Enum.IsDefined(typeof(Command), command))
			{
				Command cmd = (Command)Enum.Parse(typeof(Command), command);
				if ((!hasPermission(player, PermCanSharePublic) && (cmd == Command.share || cmd == Command.unshare)) ||
					(!hasPermission(player, PermCanSharePrivate) && (cmd == Command.sharewith || cmd == Command.unsharewith)))
				{
					return;
				}
				switch (cmd)
				{
					case Command.clear:
						if (hasPermission(player, PermCanClear))
							HandleClear(out message, out opts);
						else
							message = "NoClearPerm";
						break;
					case Command.share:
						HandleShare(player, true, null, out message, out opts);
						break;
					case Command.sharewith:
						if(args == null && args.Length == 0)
						{
							message = "InvalidArguments";
							break;
						}
						HandleShare(player, true, args, out message, out opts);
						break;
					case Command.status:
						HandleStatus(player, out message, out opts);
						break;
					case Command.unshare:
						HandleShare(player, false, null, out message, out opts);
						break;
					case Command.unsharewith:
						if (args == null && args.Length == 0)
						{
							message = "InvalidArguments";
							break;
						}
						HandleShare(player, false, args, out message, out opts);
						break;
					default:
						break;
				}
			}
			else
				ShowCommands(out message, out opts);
			if (message != null && message != "")
				SendMessage(player, message, opts);
		}

		// handle sharing/unsharing
		void HandleShare(BasePlayer player, bool share, string[] args, out string message, out object[] opts)
		{
			message = "ShareSuccess";
			opts = new object[] { "bag", share ? "shared" : "unshared" };

			bool with = args != null;
			bool all = false;
			List<ulong> players = new List<ulong>();
			List<string> names = new List<string>();
			if(with)
			{
				foreach (string s in args)
				{
					if (!share && s == "all")
					{
						all = true;
						break;
					}
					BasePlayer p = rust.FindPlayer(s);
					if (p == null)
					{
						names.Add(s);
						continue;
					}
					players.Add(p.userID);
				}
			}

			if(with && !all && (players.Count == 0 || players.Count != args.Length))
			{
				message = "PlayersNotFound";
				opts = new object[] { string.Join(", ", names.ToArray()) };
				return;
			}

			object entity;
			if (GetRaycastTarget(player, out entity) && entity is SleepingBag)
			{
				SleepingBag bag = entity as SleepingBag;
				if (bag.ShortPrefabName == BedPrefabName)
					opts[0] = "bed";
				if (bag.deployerUserID != player.userID && !isAdmin(player))
				{
					message = "CannotShareOther";
					opts[1] = "share";
					return;
				}
				else
				{
					if (share)
					{
						bag.secondsBetweenReuses = 0f;
						data.AddOrUpdateBag(bag.net.ID, bag.deployerUserID, players);
						playerNameCache[player.userID] = player.displayName;
					}
					else
					{
						if (!data.RemoveOrUpdateBag(bag.net.ID, players, all))
							message = "NotShared";
					}
					SaveData();
				}
			}
			else
			{
				message = "NoBag";
				opts = new object[] { };
			}
		}

		// handle checking status of a bed/bag
		void HandleStatus(BasePlayer player, out string message, out object[] opts)
		{
			message = "Status";
			opts = new object[] { "bag", "unshared" };
			object entity;
			if (GetRaycastTarget(player, out entity) && entity is SleepingBag)
			{
				SleepingBag bag = entity as SleepingBag;
				if (bag.ShortPrefabName == BedPrefabName)
					opts[0] = "bed";
				SharedBagInfo i = data.sharedBags.FirstOrDefault(s => s.bagId == bag.net.ID);
				if (i != null)
					opts[1] = "shared " + (i.isPublic ? " (public)" : " (private)");
			}
			else
			{
				message = "NoBag";
				opts = new object[] { };
			}
		}

		// handle clearing shared bag/beds
		void HandleClear(out string message, out object[] opts)
		{
			message = "ClearSuccess";
			opts = new object[] { data.sharedBags.Count };
			data.sharedBags.Clear();
			SaveData();
		}

		#endregion

		#region Hooks

		// on player death, wait for 1s then rebuild respawnInformation including shared beds/bags
		object OnPlayerDie(BasePlayer player, HitInfo hitinfo)
		{
			if (!data.HasSharedBags() || hasPermission(player, PermNoShare, false))
				return null;
			BuildGUIForPlayerRespawn(player);
			timer.Once(1f, () => {
				using (RespawnInformation respawnInformation = Pool.Get<RespawnInformation>())
				{
					respawnInformation.spawnOptions = Pool.Get<List<RespawnInformation.SpawnOptions>>();
					SleepingBag[] sleepingBagArray = SleepingBag.FindForPlayer(player.userID, true);
					for (int i = 0; i < (int)sleepingBagArray.Length; i++)
					{
						SleepingBag sleepingBag = sleepingBagArray[i];
						if (data.sharedBags.Count(s => s.bagId == sleepingBag.net.ID) > 0 || dummyBags.ContainsKey(sleepingBag.net.ID))
							continue;
						RespawnInformation.SpawnOptions d = Pool.Get<RespawnInformation.SpawnOptions>();
						d.id = sleepingBag.net.ID;
						d.name = sleepingBag.niceName;
						d.type = RespawnInformation.SpawnOptions.RespawnType.SleepingBag;
						d.unlockSeconds = sleepingBag.unlockSeconds;
						respawnInformation.spawnOptions.Add(d);
					}
					respawnInformation.previousLife = SingletonComponent<ServerMgr>.Instance.persistance.GetLastLifeStory(player.userID);
					respawnInformation.fadeIn = (respawnInformation.previousLife == null ? false : respawnInformation.previousLife.timeDied > (Epoch.Current - 5));
					player.ClientRPCPlayer(null, player, "OnRespawnInformation", respawnInformation, null, null, null, null);
				}
			});
			if (data.HasSharedBags())
				timer.Once(6f, () => ShowGUI(player));
			return null;
		}

		// on respawn, destroy dummy bags and gui
		object OnPlayerRespawn(BasePlayer player)
		{
			DestroyDummyBags(player);
			DestroyGUI(player);
			return null;
		}

		#endregion

		#region GUI

		// build respawn GUI for a player
		void BuildGUIForPlayerRespawn(BasePlayer player)
		{
			bool dirty = false;
			int counter = 0;
			foreach (SharedBagInfo entry in data.sharedBags.Where(i => i.isPublic || i.sharedWith.Contains(player.userID)))
			{
				SleepingBag sleepingBag = SleepingBag.FindForPlayer(entry.owner, entry.bagId, true);
				if (sleepingBag == null)
				{
					dirty = true; // no longer a valid shared bag
					continue;
				}
				uint bagId;
				if (SpawnDummyBag(sleepingBag, player, out bagId))
				{
					string messageName = "SharedBagNameText";
					if (!entry.isPublic)
						messageName = "SharedBagNameTextPrivate";
					string bagName = string.Format(GetMessage(messageName, player.UserIDString), new object[] { sleepingBag.niceName, GetPlayerName(sleepingBag.deployerUserID) });
					if(RegisterGUIElement(bagId, player.userID))
						CreateRespawnButton(bagId, bagName, player.UserIDString, counter++);
				}
			}
			// save changes to shared mappings
			if (dirty)
				ValidateSharedBags();
		}

		// build respawn button
		void CreateRespawnButton(uint bagId, string bagName, string userID, int counter)
		{
			// set up button position
			float xPosMin = data.ui.screenMarginX;
			float yPosMin = data.ui.screenMarginY + ((data.ui.verticalSpacer + data.ui.buttonHeight) * counter);
			float xPosMax = xPosMin + data.ui.buttonWidth;
			float yPosMax = yPosMin + data.ui.buttonHeight;

			string buttonAnchorMin = String.Format("{0} {1}", new object[] { xPosMin, yPosMin });
			string buttonAnchorMax = String.Format("{0} {1}", new object[] { xPosMax, yPosMax });

			// set up icon layout
			float iconXMin = data.ui.iconPaddingX - data.ui.iconWidth;
			float iconYMin = data.ui.iconPaddingY;
			float iconXMax = data.ui.iconPanelWidth - data.ui.iconWidth;
			float iconYMax = 1f - iconYMin;
			
			string iconPosMin = String.Format("{0} {1}", new object[] { iconXMin, iconYMin });
			string iconPosMax = String.Format("{0} {1}", new object[] { iconXMax, iconYMax });

			// set up text layout
			float spawnTextYMin = data.ui.spawnTextPaddingY;

			string spawnTextPosMin = String.Format("{0} {1}", new object[] { iconXMax, spawnTextYMin });
			string spawnTextPosMax = String.Format("{0} {1}", new object[] { 1f, 1f });

			float bagNameTextYMin = data.ui.bagNameTextPaddingY;
		
			string bagNameTextPosMin = String.Format("{0} {1}", new object[] { iconXMax, bagNameTextYMin });
			string bagNameTextPosMax = String.Format("{0} {1}", new object[] { 1f, spawnTextYMin });

			string headerText = GetMessage("SharedHeaderText", userID);

			// build GUI elements

			CuiElement icon = new CuiElement() {
				Name = "Icon" + bagId,
				Parent = SharedBagGUIName + bagId,
				Components = {
					new CuiImageComponent() {
						Sprite = "assets/icons/sleepingbag.png",
						Color = "1 1 1 0.6"
					},
					new CuiRectTransformComponent() {
						AnchorMin = iconPosMin,
						AnchorMax = iconPosMax
					}
				}
			};

			CuiElement spawnText = new CuiElement() {
				Name = "SpawnText" + bagId,
				Parent = SharedBagGUIName + bagId,
				Components = {
					new CuiTextComponent() {
						Text = headerText,
						Align = TextAnchor.MiddleLeft,
						FontSize = 16,
						Font = "robotocondensed-bold.ttf"
					},
					new CuiRectTransformComponent() {
						AnchorMin = spawnTextPosMin,
						AnchorMax = spawnTextPosMax
					}
				}
			};

			CuiElement bagNameText = new CuiElement() {
				Name = "BagNameText" + bagId,
				Parent = SharedBagGUIName + bagId,
				Components = {
					new CuiTextComponent() {
						Text = bagName,
						Align = TextAnchor.UpperLeft,
						FontSize = 12,
						Font = "robotocondensed-regular.ttf",
						Color = "1 1 1 0.6"
					},
					new CuiRectTransformComponent() {
						AnchorMin = bagNameTextPosMin,
						AnchorMax = bagNameTextPosMax
					}
				}
			};

			CuiElement buttonOverlay = new CuiElement() {
				Name = "ButtonOverlay" + bagId,
				Parent = SharedBagGUIName + bagId,
				Components = {
					new CuiButtonComponent() {
						Command = "respawn_sleepingbag " + bagId,
						Close = SharedBagGUIName,
						Color = "1 1 1 0.05"
					},
					new CuiRectTransformComponent() {
						AnchorMin = "0 0",
						AnchorMax = "1 1"
					}
				}
			};
			
			SharedBagGUI.Add(new CuiElement() {
				Name = SharedBagGUIName+bagId,
				Parent = "Hud",
				Components = {
					new CuiImageComponent() {
						Color = "0.5 0.4 0.2 0.9",
						FadeIn = 0.5f
					},
					new CuiRectTransformComponent() {
						AnchorMin = buttonAnchorMin,
						AnchorMax = buttonAnchorMax
					}
				}
			});

			// add elements to GUI container

			SharedBagGUI.Add(spawnText);
			SharedBagGUI.Add(bagNameText);
			SharedBagGUI.Add(icon);
			SharedBagGUI.Add(buttonOverlay);
		}

		// register a GUI element
		bool RegisterGUIElement(uint id, ulong playerId)
		{
			if (guiCache.ContainsKey(id))
				return false;
			guiCache[id] = playerId;
			return true;
		}

		// show GUI for a player
		void ShowGUI(BasePlayer player)
		{
			DestroyGUI(player);
			CuiHelper.AddUi(player, SharedBagGUI);
		}

		// destroy a player's GUI elements
		void DestroyGUI(BasePlayer player)
		{
			foreach(uint id in guiCache.Where(x => x.Value == player.userID).Select(pair => pair.Key).ToArray())
				CuiHelper.DestroyUi(player, SharedBagGUIName+id);
		}

		// destroy all player GUI elements
		void DestroyAllGUI()
		{
			foreach (BasePlayer player in BasePlayer.activePlayerList)
				DestroyGUI(player);
		}

		#endregion

		#region Helper Procedures

		// spawn a dummy bag at location of shared bag/bed to be used as a respawn point
		bool SpawnDummyBag(SleepingBag bag, BasePlayer player, out uint bagId)
		{
			bagId = 0;
			BaseEntity entity = GameManager.server.CreateEntity(bag.PrefabName, bag.transform.position, bag.transform.rotation, false);
			entity.limitNetworking = true;
			entity.Spawn();
			if (entity != null && entity is SleepingBag)
			{
				SleepingBag newBag = entity as SleepingBag;
				newBag.model.enabled = false;
				newBag.deployerUserID = player.userID;
				newBag.secondsBetweenReuses = 0f;
				bagId = newBag.net.ID;
				dummyBags[bagId] = player.userID;
				return true;
			}
			return false;
		}
		
		// Destroy all dummy bags for a player
		void DestroyDummyBags(BasePlayer player)
		{
			uint[] bags = dummyBags.Where(x => x.Value == player.userID).Select(pair => pair.Key).ToArray();
			if (bags == null || bags.Length == 0)
				return;
			foreach (uint bagId in bags)
				SleepingBag.DestroyBag(player, bagId);
		}

		// Destroy all dummy bags
		void DestroyAllDummyBags()
		{
			foreach(KeyValuePair<uint, ulong> entry in dummyBags)
			{
				SleepingBag bag = SleepingBag.FindForPlayer(entry.Value, entry.Key, true);
				if (bag != null)
					bag.Kill(BaseNetworkable.DestroyMode.None);
			}
			dummyBags.Clear();
		}

		// validate shared bag list
		void ValidateSharedBags()
		{
			if (!data.HasSharedBags()) return;
			List<uint> toRemove = new List<uint>();
			// check each bag in the shared bags list and remove any invalid bags
			foreach (SharedBagInfo entry in data.sharedBags)
			{
				SleepingBag sleepingBag = SleepingBag.FindForPlayer(entry.owner, entry.bagId, true);
				if (sleepingBag == null)
					toRemove.Add(entry.bagId); // no longer a valid shared bag
			}

			if (data.sharedBags.RemoveWhere(i => toRemove.Contains(i.bagId)) > 0)
			{
				Puts(GetMessage("Prefix") + string.Format(GetMessage("ValidateStats"), new object[] { toRemove.Count }));
				SaveData();
			}
		}

		// handle raycast from player
		bool GetRaycastTarget(BasePlayer player, out object closestEntity)
		{
			closestEntity = false;

			RaycastHit hit;
			if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
				return false;

			closestEntity = hit.GetEntity();
			return true;
		}

		// get a player name (using cache if possible)
		string GetPlayerName(ulong userID)
		{
			if (playerNameCache.ContainsKey(userID))
				return playerNameCache[userID];
			else
			{
				BasePlayer player = BasePlayer.FindByID(userID);
				if(player == null)
					player = BasePlayer.FindSleeping(userID);

				if (player != null)
				{
					playerNameCache[userID] = player.displayName;
					return player.displayName;
				}
			}
			return "unknown";
		}

		// check if player is an admin
		private static bool isAdmin(BasePlayer player)
		{
			if (player?.net?.connection == null) return true;
			return player.net.connection.authLevel > 0;
		}

		// check if player has permission or is an admin
		private bool hasPermission(BasePlayer player, string permname, bool allowAdmin = true)
		{
			return (allowAdmin && isAdmin(player)) || permission.UserHasPermission(player.UserIDString, permname);
		}

		#endregion

		#region Messaging

		// send reply to a player
		void SendMessage(BasePlayer player, string message, object[] options = null)
		{
			string msg = GetMessage(message, player.UserIDString);
			if (options != null && options.Length > 0)
				msg = String.Format(msg, options);
			SendReply(player, GetMessage("Prefix", player.UserIDString) + msg);
		}

		// show list of valid commands
		void ShowCommands(out string message, out object[] opts)
		{
			message = "CommandList";
			opts = new object[] { string.Join(", ", Enum.GetValues(typeof(Command)).Cast<Command>().Select(x => x.ToString()).ToArray()) };
		}

		#endregion

		#region Subclasses

		class BagData
		{
			public HashSet<SharedBagInfo> sharedBags = new HashSet<SharedBagInfo>();
			public UIConfig ui;

			public void AddOrUpdateBag(uint bagID, ulong playerID, List<ulong> players)
			{
				bool isPublic = players == null || players.Count == 0;
				SharedBagInfo i = sharedBags.FirstOrDefault(s => s.bagId == bagID);
				if (i == null)
					sharedBags.Add(i = new SharedBagInfo(bagID, playerID, isPublic));
				if (!i.isPublic && !isPublic)
					i.sharedWith.UnionWith(players);
			}
			
			public bool RemoveOrUpdateBag(uint bagID, List<ulong> players, bool all = false)
			{
				if (sharedBags == null || sharedBags.Count == 0) return false;
				SharedBagInfo i = sharedBags.FirstOrDefault(s => s.bagId == bagID);
				if (i == null)
					return false;
				if(i.isPublic || all)
					return sharedBags.Remove(i);
				i.sharedWith.ExceptWith(players);
				if (i.sharedWith.Count == 0)
					sharedBags.Remove(i);
				return true;
			}

			public bool HasSharedBags()
			{
				return (sharedBags != null && sharedBags.Count > 0);
			}
		}

		class SharedBagInfo
		{
			public uint bagId;
			public ulong owner;
			public bool isPublic { private set; get; } = false;
			public HashSet<ulong> sharedWith = new HashSet<ulong>();
			public SharedBagInfo(uint bagId, ulong owner, bool isPublic)
			{
				this.bagId = bagId;
				this.owner = owner;
				this.isPublic = isPublic;
			}
		}

		class UIConfig
		{
			public float buttonWidth = 0.25f;
			public float buttonHeight = 0.09722222222222222222222222222222f;
			public float screenMarginX = 0.025f;
			public float screenMarginY = 0.04444444444444444444444444444444f;
			public float verticalSpacer = 0.02222222222222222222222222222222f;
			public float iconWidth = 0.03854166666666666666666666666667f;
			public float iconPanelWidth = 0.21875f;
			public float iconPaddingX = 0.07083333333333333333333333333333f;
			public float iconPaddingY = 0.16190476190476190476190476190476f;
			public float spawnTextPaddingY = 0.57142857142857142857142857142857f;
			public float bagNameTextPaddingY = 0.3047619047619047619047619047619f;
		}

		#endregion
	}
}