using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ZLevelsRemastered", "Fujikura/Visagalis", "2.5.3", ResourceId = 1453)]
    [Description("Lets players level up as they harvest different resources and when crafting")]

    class ZLevelsRemastered : RustPlugin
    {
        #region Variables

		[PluginReference]
        Plugin EventManager;

		bool Changed = false;
		bool initialized;
		bool bonusOn = false;
		
		Dictionary<string, ItemDefinition> CraftItems;
		CraftData _craftData;
		PlayerData playerPrefs = new PlayerData();
		bool newSaveDetected = false;

		int MaxB = 999;
        int MinB = 10;

		#endregion Variables

		#region Config

		int gameProtocol;
		bool wipeDataOnNewSave;
		bool enablePermission;
		bool enableNightBonus;
		bool logEnabledBonusConsole;
		bool broadcastEnabledBonus;
		string permissionName;
		string pluginPrefix;
		Dictionary<string, object> defaultMultipliers;
        Dictionary<string, object> resourceMultipliers;
        Dictionary<string, object> levelCaps;
        Dictionary<string, object> pointsPerHit;
		Dictionary<string, object> pointsPerHitAtNight;
		Dictionary<string, object> pointsPerHitCurrent;
        Dictionary<string, object> craftingDetails;
        Dictionary<string, object> percentLostOnDeath;
		Dictionary<string, object> colors;

		Dictionary<string, object> cuiColors;
		int cuiFontSizeLvl;
		int cuiFontSizeBar;
		string cuiFontColor;
		bool cuiTextShadow;
		string cuiXpBarBackground;
		string cuiBoundsBackground;
		Dictionary<string, object> cuiPositioning;

		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadVariables();
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

		void LoadVariables()
		{
			gameProtocol = Convert.ToInt32(GetConfig("Generic", "gameProtocol", Rust.Protocol.network));
			wipeDataOnNewSave = Convert.ToBoolean(GetConfig("Generic", "wipeDataOnNewSave", false));
			enablePermission = Convert.ToBoolean(GetConfig("Generic", "enablePermission", false));
			permissionName = Convert.ToString(GetConfig("Generic", "permissionName", "zlevelsremastered.use"));
			pluginPrefix = Convert.ToString(GetConfig("Generic", "pluginPrefix", "<color=orange>ZLevels</color>:"));

			defaultMultipliers = (Dictionary<string, object>)GetConfig("Settings", "DefaultResourceMultiplier", new Dictionary<string, object>{
                {Skills.WOODCUTTING, 1},
                {Skills.MINING, 1},
                {Skills.SKINNING, 1},
				{Skills.ACQUIRE, 1}
            });
			
			resourceMultipliers = (Dictionary<string, object>)GetConfig("Settings", "ResourcePerLevelMultiplier", new Dictionary<string, object>{
                {Skills.WOODCUTTING, 2.0d},
                {Skills.MINING, 2.0d},
                {Skills.SKINNING, 2.0d},
				{Skills.ACQUIRE, 2.0d}
            });
			levelCaps = (Dictionary<string, object>)GetConfig("Settings", "LevelCaps", new Dictionary<string, object>{
                {Skills.WOODCUTTING, 200},
                {Skills.MINING, 200},
                {Skills.SKINNING, 200},
                {Skills.ACQUIRE, 200},
				{Skills.CRAFTING, -1}

            });
			pointsPerHit = (Dictionary<string, object>)GetConfig("Settings", "PointsPerHit", new Dictionary<string, object>{
                {Skills.WOODCUTTING, 30},
                {Skills.MINING, 30},
                {Skills.SKINNING, 30},
				{Skills.ACQUIRE, 30}
            });
			craftingDetails = (Dictionary<string, object>)GetConfig("Settings", "CraftingDetails", new Dictionary<string, object>{
                { "TimeSpent", 1},
                { "XPPerTimeSpent", 3},
                { "PercentFasterPerLevel", 5 }
            });
			percentLostOnDeath = (Dictionary<string, object>)GetConfig("Settings", "PercentLostOnDeath", new Dictionary<string, object>{
                {Skills.WOODCUTTING, 50},
                {Skills.MINING, 50},
                {Skills.SKINNING, 50},
				{Skills.ACQUIRE, 50},
                {Skills.CRAFTING, 50}
            });
			colors = (Dictionary<string, object>)GetConfig("Settings", "SkillColors", new Dictionary<string, object>()
			{
				{Skills.WOODCUTTING, "#FFDDAA"},
				{Skills.MINING, "#DDDDDD"},
				{Skills.SKINNING, "#FFDDDD"},
				{Skills.ACQUIRE, "#ADD8E6"},
				{Skills.CRAFTING, "#CCFF99"}
			});
			cuiColors = (Dictionary<string, object>)GetConfig("CUI", "XpBarColors", new Dictionary<string, object>()
			{
				{Skills.WOODCUTTING, "0.8 0.4 0 0.5"},
				{Skills.MINING, "0.1 0.5 0.8 0.5"},
				{Skills.SKINNING, "0.8 0.1 0 0.5"},
				{Skills.ACQUIRE, "0 0.8 0 0.5"},
				{Skills.CRAFTING, "0.2 0.72 0.5 0.5"}
			});
			cuiFontSizeLvl = Convert.ToInt32(GetConfig("CUI", "FontSizeLevel", 12));
			cuiFontSizeBar = Convert.ToInt32(GetConfig("CUI", "FontSizeBar", 13));
			cuiTextShadow = Convert.ToBoolean(GetConfig("CUI", "TextShadowEnabled", true));
			cuiFontColor = Convert.ToString(GetConfig("CUI", "FontColor", "0.74 0.76 0.78 1"));
			cuiXpBarBackground = Convert.ToString(GetConfig("CUI", "XpBarBackground", "0.2 0.2 0.2 0.2"));
			cuiBoundsBackground = Convert.ToString(GetConfig("CUI", "BoundsBackground", "0.1 0.1 0.1 0.1"));
			cuiPositioning = (Dictionary<string, object>)GetConfig("CUI", "Bounds", new Dictionary<string, object>()
			{
				{"WidthLeft", "0.72"},
				{"WidthRight", "0.847"},
				{"HeightLower", "0.023"},
				{"HeightUpper", "0.19"}
			});
			
			pointsPerHitAtNight = (Dictionary<string, object>)GetConfig("NightBonus", "PointsPerHitAtNight", new Dictionary<string, object>{
                {Skills.WOODCUTTING, 60},
                {Skills.MINING, 60},
                {Skills.SKINNING, 60},
				{Skills.ACQUIRE, 60}
            });
			enableNightBonus = Convert.ToBoolean(GetConfig("NightBonus", "enableNightBonus", false));
			logEnabledBonusConsole = Convert.ToBoolean(GetConfig("NightBonus", "logEnabledBonusConsole", false));
			broadcastEnabledBonus = Convert.ToBoolean(GetConfig("NightBonus", "broadcastEnabledBonus", true));
			
			if (!Changed) return;
			SaveConfig();
			Changed = false;
		}

		void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"StatsHeadline", "Level stats (/statinfo - To get more information about skills)"},
				{"StatsText",   "-{0}\nLevel: {1} (+{4}% bonus) \nXP: {2}/{3} [{5}].\n<color=red>-{6} XP loose on death.</color>"},
				{"LevelUpText", "{0} Level up\nLevel: {1} (+{4}% bonus) \nXP: {2}/{3}"},
				{"PenaltyText", "<color=orange>You have lost XP for dying:{0}</color>"},
				{"NoPermission", "You don't have permission to use this command"},
				{"WCSkill", "Woodcutting"},
				{"MSkill", "Mining"},
				{"SSkill", "Skinning"},
				{"CSkill", "Crafting" },
				{"ASkill", "Acquire" },
				{"NightBonusOn", "Nightbonus for points per hit enabled"},
				{"NightBonusOff", "Nightbonus for points per hit disabled"},
			},this);
		}

        #endregion Config

		#region Main

		void Init()
        {
            LoadVariables();
			LoadDefaultMessages();
			initialized = false;
			try {
				if ((int)levelCaps[Skills.CRAFTING] > 20)
				levelCaps[Skills.CRAFTING] = 20;
			} catch {}
			if (!permission.PermissionExists(permissionName)) permission.RegisterPermission(permissionName, this);
            if ((_craftData = Interface.GetMod().DataFileSystem.ReadObject<CraftData>("ZLevelsCraftDetails")) == null)
                _craftData = new CraftData();
            playerPrefs = Interface.GetMod().DataFileSystem.ReadObject<PlayerData>(this.Title);
         }

		void OnServerSave()
		{
			if (initialized) Interface.Oxide.DataFileSystem.WriteObject(this.Title, playerPrefs);
		}

        void Unload()
        {
			if (!initialized) return;
			Interface.Oxide.DataFileSystem.WriteObject(this.Title, playerPrefs);
			foreach (var player in BasePlayer.activePlayerList)
				BlendOutUI(player);
        }

		void OnNewSave(string strFilename)
		{
			if (wipeDataOnNewSave)
				newSaveDetected = true;
		}

		void OnServerInitialized()
		{
			if (newSaveDetected)
			{
				playerPrefs = new PlayerData();
				Interface.Oxide.DataFileSystem.WriteObject(this.Title, playerPrefs);
			}
			pointsPerHitCurrent = pointsPerHit;
			if (enableNightBonus && TOD_Sky.Instance.IsNight)
			{
				pointsPerHitCurrent = pointsPerHitAtNight;
				bonusOn = true;
			}
			initialized = true;
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (player != null)
				{
					UpdatePlayer(player);
					RenderUI(player);
				}
			}
		}

		#endregion Main

		#region Classes

		class Skills
        {
            public static string CRAFTING = "C";
            public static string WOODCUTTING = "WC";
            public static string SKINNING = "S";
            public static string MINING = "M";
			public static string ACQUIRE = "A";
            public static string[] ALL = { WOODCUTTING, MINING, SKINNING, ACQUIRE, CRAFTING };
        }

        class CraftData
        {
            public Dictionary<string, CraftInfo> CraftList = new Dictionary<string, CraftInfo>();
        }

		class CraftInfo
        {
            public int MaxBulkCraft;
            public int MinBulkCraft;
            public string shortName;
            public bool Enabled;
        }

		class PlayerData
		{
			public Dictionary<ulong, PlayerInfo> PlayerInfo = new Dictionary<ulong, PlayerInfo>();
			public PlayerData(){}
		}

		class PlayerInfo
		{
			public long WCL = 1;
			public long WCP = 10;
			public long ML = 1;
			public long MP = 10;
			public long SL = 1;
			public long SP = 10;
			public long AL = 1;
			public long AP = 10;
			public long CL = 1;
			public long CP = 10;
			public long LD;
			public long LLD;
			public long XPM = 100;
			public bool CUI = true;
		}

		#endregion Classes

		#region Serverhooks

		void OnPlayerInit(BasePlayer player)
        {
            if (!initialized || player == null) return;
			UpdatePlayer(player);
			RenderUI(player);			
			/*
			long multiplier = 100;
            var playerPermissions = permission.GetUserPermissions(player.UserIDString);
            if (playerPermissions.Any(x => x.ToLower().StartsWith("zlvlboost")))
            {
                var perm = playerPermissions.First(x => x.ToLower().StartsWith("zlvlboost"));
                if (!long.TryParse(perm.ToLower().Replace("zlvlboost", ""), out multiplier))
                    multiplier = 100;
            }
            editMultiplierForPlayer(multiplier, player.userID);
			*/
        }

		void OnPlayerDisconnected(BasePlayer player)
        {
            if (initialized && player != null)
				UpdatePlayer(player);
        }

		void UpdatePlayer(BasePlayer player)
        {
			PlayerInfo p = null;
			if (!playerPrefs.PlayerInfo.TryGetValue(player.userID, out p))
			{
				var info = new PlayerInfo();
				info.LD = ToEpochTime(DateTime.UtcNow);
				info.LLD = ToEpochTime(DateTime.UtcNow);
				playerPrefs.PlayerInfo.Add(player.userID, info);
				return;
			}
            else
				p.LLD = ToEpochTime(DateTime.UtcNow);
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!initialized || player == null) return;
			PlayerInfo p = null;
			if (playerPrefs.PlayerInfo.TryGetValue(player.userID, out p))
				RenderUI(player);
			else
			{
				UpdatePlayer(player);
				RenderUI(player);	
			}
        }

		void OnPlayerSleep(BasePlayer player)
        {
            if (!initialized || player == null) return;
			PlayerInfo p = null;
			if (playerPrefs.PlayerInfo.TryGetValue(player.userID, out p))
				BlendOutUI(player);
        }

        void OnLootEntity(BasePlayer looter, BaseEntity target)
        {
            if (looter != null)
				BlendOutUI(looter);
        }

        void OnLootPlayer(BasePlayer looter, BasePlayer beingLooter)
        {
			if (looter != null)
				BlendOutUI(looter);
        }

        void OnLootItem(BasePlayer looter, Item lootedItem)
        {
            if (looter != null)
				BlendOutUI(looter);
        }

		void OnPlayerLootEnd(PlayerLoot inventory)
        {
            if (inventory != null)
			{
				var player = inventory.GetComponent<BasePlayer>();
				if (player != null)
					RenderUI(player);
			}
        }

		void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			if (!initialized || entity == null || !(entity is BasePlayer)) return;
			NextTick(()=>{
				if (entity != null && entity.health <= 0f) BlendOutUI(entity as BasePlayer);
			});
		}

		void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!initialized || entity == null || !(entity is BasePlayer)) return;
			var player = entity as BasePlayer;
			PlayerInfo p = null;
			if (!hasRights(player.UserIDString) || !playerPrefs.PlayerInfo.TryGetValue(player.userID, out p)) return;
			if (EventManager?.Call("isPlaying", player) != null && (bool)EventManager?.Call("isPlaying", player)) return;
			if (hitInfo != null && hitInfo.damageTypes != null && hitInfo.damageTypes.Has(Rust.DamageType.Suicide)) return;
			var penaltyText = string.Empty;
			var penaltyExist = false;
			foreach (var skill in Skills.ALL)
				if (IsSkillEnabled(skill))
				{
					var penalty = GetPenalty(player, skill);
					if (penalty > 0)
					{
						penaltyText += "\n* -" + penalty + " " + msg(skill + "Skill") + " XP.";
						removePoints(player.userID, skill, penalty);
						penaltyExist = true;
					}
				}
			if (penaltyExist)
				PrintToChat(player, string.Format(msg("PenaltyText", player.UserIDString), penaltyText));
			playerPrefs.PlayerInfo[player.userID].LD = ToEpochTime(DateTime.UtcNow);
        }

		void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!initialized || entity == null || !(entity is BasePlayer) || item == null || dispenser == null) return;
			var player = entity as BasePlayer;
			if (!hasRights(player.UserIDString)) return;
            if (IsSkillEnabled(Skills.WOODCUTTING) &&(int)dispenser.gatherType == 0) levelHandler(player, item, Skills.WOODCUTTING);
            if (IsSkillEnabled(Skills.MINING) && (int)dispenser.gatherType == 1) levelHandler(player, item, Skills.MINING);
            if (IsSkillEnabled(Skills.SKINNING) && (int)dispenser.gatherType == 2) levelHandler(player, item, Skills.SKINNING);
        }

        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (!initialized || item == null || player == null || !hasRights(player.UserIDString)) return;
			var skillName = string.Empty;

			if (IsSkillDisabled(Skills.ACQUIRE))
			{
				switch (item.info.shortname)
				{
					case "wood":
						skillName = Skills.WOODCUTTING;
						break;
					case "cloth":
					case "mushroom":
					case "corn":
					case "pumpkin":
					case "seed.hemp":
					case "seed.pumpkin":
					case "seed.corn":
						skillName = Skills.SKINNING;
						break;
					case "metal.ore":
					case "sulfur.ore":
					case "stones":
						skillName = Skills.MINING;
						break;
				}
			}
			else
				skillName = Skills.ACQUIRE;

			if (!string.IsNullOrEmpty(skillName))
				levelHandler(player, item, skillName);
        }
		
		void OnTimeSunset()
		{
			if (!enableNightBonus || bonusOn) return;
			bonusOn = true;
			pointsPerHitCurrent = pointsPerHitAtNight;
			if (broadcastEnabledBonus)
				rust.BroadcastChat(pluginPrefix + " "+ msg("NightBonusOn"));			
			if (logEnabledBonusConsole)
				Puts("Nightbonus points enabled");
		}

		void OnTimeSunrise()
		{
			if (!enableNightBonus || !bonusOn) return;
			bonusOn = false;
			pointsPerHitCurrent = pointsPerHit;
			if (broadcastEnabledBonus)
				rust.BroadcastChat(pluginPrefix + " "+ msg("NightBonusOff"));
			if (logEnabledBonusConsole)
				Puts("Nightbonus points disabled");
		}
		
		void OnCropGather(PlantEntity plant, Item item, BasePlayer player)
		{
			if (!initialized || item == null || player == null || !hasRights(player.UserIDString)) return;
			var skillName = string.Empty;
			if (IsSkillDisabled(Skills.ACQUIRE))
				skillName = Skills.SKINNING;
			else
				skillName = Skills.ACQUIRE;
			levelHandler(player, item, skillName);
		}

		object OnItemCraft(ItemCraftTask task, BasePlayer crafter)
        {
            if (!initialized || IsSkillDisabled(Skills.CRAFTING) || !hasRights(crafter.UserIDString)) return null;
            var Level = getLevel(crafter.userID, Skills.CRAFTING);
            var craftingTime = task.blueprint.time;
            var amountToReduce = task.blueprint.time * ((float)(Level * (int)craftingDetails["PercentFasterPerLevel"]) / 100);
            craftingTime -= amountToReduce;
            if (craftingTime < 0)
                craftingTime = 0;
            if (craftingTime == 0)
            {
                try
                {
                    foreach (var entry in _craftData.CraftList)
                    {
                        var itemname = task.blueprint.targetItem.shortname;
                        if (entry.Value.shortName == itemname && entry.Value.Enabled)
                        {
                            var amount = task.amount;
                            if (amount >= entry.Value.MinBulkCraft && amount <= entry.Value.MaxBulkCraft)
                            {
                                var item = GetItem(itemname);
                                var final_amount = task.blueprint.amountToCreate * amount;
                                var newItem = ItemManager.CreateByItemID(item.itemid, (int)final_amount);
                                crafter.inventory.GiveItem(newItem);

                                var returnstring = "You have crafted <color=#66FF66>" + amount + "</color> <color=#66FFFF>" + item.displayName.english + "</color>\n[Batch Amount: <color=#66FF66>" + final_amount + "</color>]";
                                PrintToChat(crafter, returnstring);
                                return false;
                            }
                        }
                    }
                }
                catch
                {
                    GenerateItems();
                }
            }

            if (!task.blueprint.name.Contains("(Clone)"))
                task.blueprint = UnityEngine.Object.Instantiate(task.blueprint);
            task.blueprint.time = craftingTime;
            return null;
        }

		object OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (!initialized || IsSkillDisabled(Skills.CRAFTING)) return null;
            var crafter = task.owner as BasePlayer;
			if (crafter == null || !hasRights(crafter.UserIDString)) return null;
            var xpPercentBefore = getExperiencePercent(crafter, Skills.CRAFTING);
            if (task.blueprint == null)
            {
                Puts("There is problem obtaining task.blueprint on 'OnItemCraftFinished' hook! This is usually caused by some incompatable plugins.");
                return null;
            }
            var experienceGain = Convert.ToInt32(Math.Floor((task.blueprint.time + 0.99f) / (int)craftingDetails["TimeSpent"]));//(int)task.blueprint.time / 10;
            if (experienceGain == 0)
                return null;

            long Level = 0;
            long Points = 0;
            try
            {
                Level = getLevel(crafter.userID, Skills.CRAFTING);
                Points = getPoints(crafter.userID, Skills.CRAFTING);
            } catch {}
            Points += experienceGain * (int)craftingDetails["XPPerTimeSpent"];
            if (Points >= getLevelPoints(Level + 1))
            {
                var maxLevel = (int)levelCaps[Skills.CRAFTING] > 0 && Level + 1 > (int)levelCaps[Skills.CRAFTING];
                if (!maxLevel)
                {
                    Level = getPointsLevel(Points, Skills.CRAFTING);
                    PrintToChat(crafter, string.Format("<color=" + colors[Skills.CRAFTING] + '>' + msg("LevelUpText", crafter.UserIDString) + "</color>",
                        msg("CSkill", crafter.UserIDString),
                        Level,
                        Points,
                        getLevelPoints(Level + 1),
                        (getLevel(crafter.userID, Skills.CRAFTING) * Convert.ToDouble(craftingDetails["PercentFasterPerLevel"]))
                        )
                    );
                }
            }
            try
            {
                if (item.info.shortname != "lantern_a" && item.info.shortname != "lantern_b")
					setPointsAndLevel(crafter.userID, Skills.CRAFTING, Points, Level);
            } catch {}

            try
            {
                var xpPercentAfter = getExperiencePercent(crafter, Skills.CRAFTING);
                if (!xpPercentAfter.Equals(xpPercentBefore))
                    RenderUI(crafter);
            } catch {}

            if (task.amount > 0) return null;
            if (task.blueprint != null && task.blueprint.name.Contains("(Clone)"))
            {
                var behaviours = task.blueprint.GetComponents<MonoBehaviour>();
                foreach (var behaviour in behaviours)
                {
                    if (behaviour.name.Contains("(Clone)")) UnityEngine.Object.Destroy(behaviour);
                }
                task.blueprint = null;
            }
            return null;
        }

		#endregion Serverhooks

	   #region Commands

        [HookMethod("SendHelpText"), ChatCommand("stathelp")]
        void SendHelpText(BasePlayer player)
        {
			var sb = new StringBuilder();
			sb.AppendLine("<size=18><color=orange>ZLevels</color></size><size=14><color=#ce422b>REMASTERED</color></size>");
			sb.AppendLine("/stats - Displays your stats.");
			sb.AppendLine("/statsui - Enable/Disable stats UI.");
			sb.AppendLine("/statinfo - Displays information about skills.");
			sb.AppendLine("/stathelp - Displays the help.");
			sb.AppendLine("/topskills - Display max levels reached so far.");
			player.ChatMessage(sb.ToString());
        }

		[ChatCommand("topskills")]
        void StatsTopCommand(BasePlayer player, string command, string[] args)
        {
			if (!hasRights(player.UserIDString))
			{				 
				player.ChatMessage(pluginPrefix + " "+ msg("NoPermission", player.UserIDString));
				return;
			}
			var sb = new StringBuilder();
			sb.AppendLine("<size=18><color=orange>ZLevels</color></size><size=14><color=#ce422b>REMASTERED</color></size>");
			sb.AppendLine("Data temporary not available");
			player.ChatMessage(sb.ToString());
			/*
			PrintToChat(player, "Max stats on server so far:");
            foreach (var skill in Skills.ALL)
                if (!IsSkillDisabled(skill))
                    printMaxSkillDetails(player, skill);
			*/
        }
		
		[ConsoleCommand("zl.pointsperhit")]
        void PointsPerHitCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) return;
            if (arg.Args == null || arg.Args.Length < 2)
            {
				SendReply(arg, "Syntax: zl.pointsperhit skill number");
                SendReply(arg, "Possible skills are: WC, M, S, A, C, *(All skills)");
				var sb = new StringBuilder();
				sb.Append("Current points per hit:");
				foreach (var currSkill in Skills.ALL)
				{
					if (IsSkillDisabled(currSkill)) continue;
					sb.Append($" {currSkill} > {pointsPerHitCurrent[currSkill]} |");
				}
				SendReply(arg, sb.ToString().TrimEnd('|'));
				return;
            }
			int points = -1;
			if (int.TryParse(arg.Args[1], out points))
			{
				if (points < 1)
				{
					SendReply(arg, "Incorrect number. Must be greater than 1");
					return;
				}
			}
			else
			{
				SendReply(arg, "Incorrect number. Must be greater than 1");
				return;
			}
			
			var skill = arg.Args[0].ToUpper();
			if (skill == Skills.WOODCUTTING || skill == Skills.MINING || skill == Skills.SKINNING || skill == Skills.ACQUIRE || skill == Skills.CRAFTING || skill == "*")
			{
				if (skill == "*")
				{
					foreach (var currSkill in Skills.ALL)
					{
						if (IsSkillDisabled(currSkill)) continue;
						pointsPerHitCurrent[currSkill] = points;
					}
					var sb = new StringBuilder();
					sb.Append("New points per hit:");
					foreach (var currSkill in Skills.ALL)
					{
						if (IsSkillDisabled(currSkill)) continue;
						sb.Append($" {currSkill} > {pointsPerHitCurrent[currSkill]} |");
					}
					SendReply(arg, sb.ToString().TrimEnd('|'));
					return;
				}
				else
				{
					pointsPerHitCurrent[skill] = points;
					var sb = new StringBuilder();
					sb.Append("New points per hit:");
					foreach (var currSkill in Skills.ALL)
					{
						if (IsSkillDisabled(currSkill)) continue;
						sb.Append($" {currSkill} > {pointsPerHitCurrent[currSkill]} |");
					}
					SendReply(arg, sb.ToString().TrimEnd('|'));
					return;
				}
			}
			else
				SendReply(arg, "Incorrect skill. Possible skills are: WC, M, S, A, C, *(All skills).");
        }
        
		[ConsoleCommand("zl.playerxpm")]
        void PlayerXpmCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "Syntax: zl.playerxpm name|steamid (to show current XP multiplier)");
                SendReply(arg, "Syntax: zl.playerxpm name|steamid number (to set current XP multiplier >= 100)");
				return;
            }
			IPlayer player = this.covalence.Players.FindPlayer(arg.Args[0]);
			if (player == null)
			{
				SendReply(arg, "Player not found!");
				return;
			}
			PlayerInfo playerData = null;
			if (!playerPrefs.PlayerInfo.TryGetValue(Convert.ToUInt64(player.Id), out playerData))
			{
				SendReply(arg, "PlayerData is NULL!");
				return;
			}
			if (arg.Args.Length < 2)
            {
				SendReply(arg, $"Current XP multiplier for player '{player.Name}' is {playerData.XPM.ToString()}%");
				return;
            }
			int multiplier = -1;
			if (int.TryParse(arg.Args[1], out multiplier))
			{
				if (multiplier < 100)
				{
					SendReply(arg, "Incorrect number. Must be greater greater or equal 100");
					return;
				}
			}
			playerData.XPM = multiplier;
			SendReply(arg, $"New XP multiplier for player '{player.Name}' is {playerData.XPM.ToString()}%");
        }		
        
		[ConsoleCommand("zl.info")]
        void InfoCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "Syntax: zl.info name|steamid");
                return;
            }
			IPlayer player = this.covalence.Players.FindPlayer(arg.Args[0]);
			if (player == null)
			{
				SendReply(arg, "Player not found!");
				return;
			}
			PlayerInfo playerData = null;
			if (!playerPrefs.PlayerInfo.TryGetValue(Convert.ToUInt64(player.Id), out playerData))
			{
				SendReply(arg, "PlayerData is NULL!");
				return;
			}
			TextTable textTable = new TextTable();
			textTable.AddColumn("FieldInfo");
			textTable.AddColumn("Level");
			textTable.AddColumn("Points");
			textTable.AddRow(new string[]	{ "Woodcutting", playerData.WCL.ToString(), playerData.WCP.ToString() });
			textTable.AddRow(new string[]	{ "Mining", playerData.ML.ToString(), playerData.MP.ToString() });
			textTable.AddRow(new string[]	{ "Skinning", playerData.SL.ToString(), playerData.SP.ToString() });
			textTable.AddRow(new string[]	{ "Acquire", playerData.AL.ToString(), playerData.AP.ToString() });
			textTable.AddRow(new string[]	{ "Crafting", playerData.CL.ToString(), playerData.CP.ToString() });
			textTable.AddRow(new string[]	{ "XP Multiplier", playerData.XPM.ToString()+"%", string.Empty });
			SendReply(arg, "\nStats for player: " + player.Name + "\n" +textTable.ToString());
        }

        [ConsoleCommand("zl.lvl")]
        void ZlvlCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) return;

            if (arg.Args == null || arg.Args.Length < 3)
            {
                var sb = new StringBuilder();
				sb.AppendLine("Syntax: zl.lvl name|steamid skill [OPERATOR]NUMBER");
                sb.AppendLine("Example: zl.lvl Player WC /2 -- Player gets his WC level divided by 2.");
                sb.AppendLine("Example: zl.lvl * * +3 -- Everyone currently playing in the server gets +3 for all skills.");
                sb.AppendLine("Example: zl.lvl ** * /2 -- Everyone (including offline players) gets their level divided by 2.");
                sb.AppendLine("Instead of names you can use wildcard(*): * - affects online players, ** - affects all players");
                sb.AppendLine("Possible operators: *(XP Modified %), +(Adds level), -(Removes level), /(Divides level)");
                SendReply(arg, "\n"+sb.ToString());
				return;
            }
			var playerName = arg.Args[0];
			IPlayer p = this.covalence.Players.FindPlayer(arg.Args[0]);

			if (playerName != "*" && playerName != "**" && p == null)
			{
				SendReply(arg, "Player not found!");
				return;
			}
			PlayerInfo playerData = null;
			if (playerName != "*" && playerName != "**" && !playerPrefs.PlayerInfo.TryGetValue(Convert.ToUInt64(p.Id), out playerData))
			{
				SendReply(arg, "PlayerData is NULL!");
				return;
			}

            if (p != null || playerName == "*" || playerName == "**")
            {
                var playerMode = 0; // Exact player
                if (playerName == "*")
                    playerMode = 1; // Online players
                else if (playerName == "**")
                    playerMode = 2; // All players
                var skill = arg.Args[1].ToUpper();
                if (skill == Skills.WOODCUTTING || skill == Skills.MINING || skill == Skills.SKINNING || skill == Skills.ACQUIRE ||
                    skill == Skills.CRAFTING || skill == "*")
                {
                    var allSkills = skill == "*";
                    var mode = 0; // 0 = SET, 1 = ADD, 2 = SUBTRACT, 3 = multiplier, 4 = divide
                    int value;
                    var correct = false;
                    if (arg.Args[2][0] == '+')
                    {
                        mode = 1;
                        correct = int.TryParse(arg.Args[2].Replace("+", ""), out value);
                    }
                    else if (arg.Args[2][0] == '-')
                    {
                        mode = 2;
                        correct = int.TryParse(arg.Args[2].Replace("-", ""), out value);
                    }
                    else if (arg.Args[2][0] == '*')
                    {
                        mode = 3;
                        correct = int.TryParse(arg.Args[2].Replace("*", ""), out value);
                    }
                    else if (arg.Args[2][0] == '/')
                    {
                        mode = 4;
                        correct = int.TryParse(arg.Args[2].Replace("/", ""), out value);
                    }
                    else
                    {
                        correct = int.TryParse(arg.Args[2], out value);
                    }
                    if (correct)
                    {
                        if (mode == 3) // Change XP Multiplier.
                        {
                            if (!allSkills)
                            {
                                SendReply(arg, "XPMultiplier is changeable for all skills! Use * instead of " + skill + ".");
                                return;
                            }
                            if (playerMode == 1)
                            {
                                foreach (var currPlayer in BasePlayer.activePlayerList)
                                    editMultiplierForPlayer(value, currPlayer.userID);
                            }
                            else if (playerMode == 2)
                                editMultiplierForPlayer(value);
                            else if (p != null)
                                editMultiplierForPlayer(value, Convert.ToUInt64(p.Id));

                            SendReply(arg, "XP rates has changed to " + value + "% of normal XP for " + (playerMode == 1 ? "ALL ONLINE PLAYERS" : (playerMode == 2 ? "ALL PLAYERS" : p.Name)));
                            return;
                        }

                        if (playerMode == 1)
						{
							foreach (var connPlayer in this.covalence.Players.Connected)
							{
								adminModifyPlayerStats(arg, skill, value, mode, connPlayer);
							}
						}
                        else if (playerMode == 2)
						{
							foreach (var allPlayer in this.covalence.Players.All)
							{
								PlayerInfo checkData = null;
								if (playerPrefs.PlayerInfo.TryGetValue(Convert.ToUInt64(allPlayer.Id), out checkData))
								{
									adminModifyPlayerStats(arg, skill, value, mode, allPlayer);
								}
							}
						}
                        else
						{
							adminModifyPlayerStats(arg, skill, value, mode, p);
						}
                    }
                }
                else
                    SendReply(arg, "Incorrect skill. Possible skills are: WC, M, S, A, C, *(All skills).");
            }
        }

        [ChatCommand("stats")]
        void StatsCommand(BasePlayer player, string command, string[] args)
        {
			if (!hasRights(player.UserIDString))
			{				 
				player.ChatMessage(pluginPrefix + " " + msg("NoPermission", player.UserIDString));
				return;
			}
			var text = "<size=18><color=orange>ZLevels</color></size><size=14><color=#ce422b>REMASTERED</color></size>\n";
            foreach (var skill in Skills.ALL)
                text += getStatPrint(player, skill);
            var details = playerPrefs.PlayerInfo[player.userID].LD;
			var currentTime = DateTime.UtcNow;
			var lastDeath = ToDateTimeFromEpoch(details);
			var timeAlive = currentTime - lastDeath;
			text += "\nTime alive: " + ReadableTimeSpan(timeAlive);
			if (playerPrefs.PlayerInfo[player.userID].XPM.ToString() != "100")
				text += "XP rates for you are " + playerPrefs.PlayerInfo[player.userID].XPM + "%";
			rust.SendChatMessage(player, text);
        }

		[ChatCommand("statinfo")]
        void StatInfoCommand(BasePlayer player, string command, string[] args)
        {
			if (!hasRights(player.UserIDString))
			{				 
				player.ChatMessage(pluginPrefix + " " + msg("NoPermission", player.UserIDString));
				return;
			}
			var messagesText = string.Empty;
            long xpMultiplier = playerPrefs.PlayerInfo[player.userID].XPM;

			messagesText += "<size=18><color=orange>ZLevels</color></size><size=14><color=#ce422b>REMASTERED</color></size>\n";

			messagesText += "<color=" + colors[Skills.MINING] + ">Mining</color>" + (IsSkillDisabled(Skills.MINING) ? "(DISABLED)" : "") + "\n";
			messagesText += "XP per hit: <color=" + colors[Skills.MINING] + ">" + ((int)pointsPerHitCurrent[Skills.MINING] * (xpMultiplier / 100f)) + "</color>\n";
			messagesText += "Bonus materials per level: <color=" + colors[Skills.MINING] + ">" + ((getGathMult(2, Skills.MINING) - 1) * 100).ToString("0.##") + "%</color>\n";

			messagesText += "<color=" + colors[Skills.WOODCUTTING] + ">Woodcutting</color>" + (IsSkillDisabled(Skills.WOODCUTTING) ? "(DISABLED)" : "") + "\n";
			messagesText += "XP per hit: <color=" + colors[Skills.WOODCUTTING] + ">" + ((int)pointsPerHitCurrent[Skills.WOODCUTTING] * (xpMultiplier / 100f)) + "</color>\n";
			messagesText += "Bonus materials per level: <color=" + colors[Skills.WOODCUTTING] + ">" + ((getGathMult(2, Skills.WOODCUTTING) - 1) * 100).ToString("0.##") + "%</color>\n";

			messagesText += "<color=" + colors[Skills.SKINNING] + '>' + "Skinning" + "</color>" + (IsSkillDisabled(Skills.SKINNING) ? "(DISABLED)" : "") + "\n";
			messagesText += "XP per hit: <color=" + colors[Skills.SKINNING] + ">" + ((int)pointsPerHitCurrent[Skills.SKINNING] * (xpMultiplier / 100f)) + "</color>\n";
			messagesText += "Bonus materials per level: <color=" + colors[Skills.SKINNING] + ">" + ((getGathMult(2, Skills.SKINNING) - 1) * 100).ToString("0.##") + "%</color>\n";

			if (IsSkillEnabled(Skills.ACQUIRE))
			{
				messagesText += "<color=" + colors[Skills.ACQUIRE] + '>' + "Acquire" + "</color>" + (IsSkillDisabled(Skills.ACQUIRE) ? "(DISABLED)" : "") + "\n";
				messagesText += "XP per hit: <color=" + colors[Skills.ACQUIRE] + ">" + ((int)pointsPerHitCurrent[Skills.ACQUIRE] * (xpMultiplier / 100f)) + "</color>\n";
				messagesText += "Bonus materials per level: <color=" + colors[Skills.ACQUIRE] + ">" + ((getGathMult(2, Skills.ACQUIRE) - 1) * 100).ToString("0.##") + "%</color>\n";
			}

			messagesText += "<color=" + colors[Skills.CRAFTING] + '>' + "Crafting" + "</color>" + (IsSkillDisabled(Skills.CRAFTING) ? "(DISABLED)" : "") + "\n";
			messagesText += "XP gain: <color=" + colors[Skills.SKINNING] + ">You get " + craftingDetails["XPPerTimeSpent"] + " XP per " + craftingDetails["TimeSpent"] + "s spent crafting.</color>\n";
			messagesText += "Bonus: <color=" + colors[Skills.SKINNING] + ">Crafting time is decreased by " + craftingDetails["PercentFasterPerLevel"] + "% per every level.</color>\n";

			PrintToChat(player, messagesText);
        }

        [ChatCommand("statsui")]
        void StatsUICommand(BasePlayer player, string command, string[] args)
        {
            if (!hasRights(player.UserIDString)) return;
			if (playerPrefs.PlayerInfo[player.userID].CUI)
            {
				CuiHelper.DestroyUi(player, "StatsUI");
				playerPrefs.PlayerInfo[player.userID].CUI = false;
            }
            else
            {
                playerPrefs.PlayerInfo[player.userID].CUI = true;
				RenderUI(player);
            }
        }

		#endregion Commands

		#region Functions
		
		bool hasRights(string UserIDString)
		{
			if (!enablePermission)
				return true;
			if (!permission.UserHasPermission(UserIDString, permissionName))
				return false;
			return true;
		}

		void editMultiplierForPlayer(long multiplier, ulong userID = ulong.MinValue)
        {
			if (userID == ulong.MinValue)
			{
				foreach (var p in playerPrefs.PlayerInfo.ToList())
					playerPrefs.PlayerInfo[p.Key].XPM = multiplier;
				return;
			}
			PlayerInfo playerData = null;
			if (playerPrefs.PlayerInfo.TryGetValue(userID, out playerData))
					playerData.XPM = multiplier;
		}

		void adminModifyPlayerStats(ConsoleSystem.Arg arg, string skill, long level, int mode, IPlayer p)
        {
            if (skill == "*")
            {
                var sb = new StringBuilder();
				foreach (var currSkill in Skills.ALL)
                {
					if (IsSkillDisabled(currSkill)) continue;
					var modifiedLevel = getLevel(Convert.ToUInt64(p.Id), currSkill);
					if (mode == 0) // SET
						modifiedLevel = level;
					else if (mode == 1) // ADD
						modifiedLevel += level;
					else if (mode == 2) // SUBTRACT
						modifiedLevel -= level;
					else if (mode == 4) // DIVIDE
						modifiedLevel /= level;
					if (modifiedLevel < 1)
						modifiedLevel = 1;
					if (modifiedLevel > Convert.ToInt32(levelCaps[currSkill]) && Convert.ToInt32(levelCaps[currSkill]) != 0)
					{
						modifiedLevel = Convert.ToInt32(levelCaps[currSkill]);
					}
					setPointsAndLevel(Convert.ToUInt64(p.Id), currSkill, getLevelPoints(modifiedLevel), modifiedLevel);
					var baseP = BasePlayer.FindByID(Convert.ToUInt64(p.Id));
					if (baseP != null)
						RenderUI(baseP);
					sb.Append($"({msg(currSkill + "Skill")} > {modifiedLevel}) ");
                }
				SendReply(arg, $"\nChanges for '{p.Name}': "+ sb.ToString().TrimEnd());
            }
            else
            {
                var modifiedLevel = getLevel(Convert.ToUInt64(p.Id), skill);
                if (mode == 0) // SET
                    modifiedLevel = level;
                else if (mode == 1) // ADD
                    modifiedLevel += level;
                else if (mode == 2) // SUBTRACT
                    modifiedLevel -= level;
                else if (mode == 4) // DIVIDE
                    modifiedLevel /= level;
                if (modifiedLevel < 1)
                    modifiedLevel = 1;
                if (modifiedLevel > Convert.ToInt32(levelCaps[skill]) && Convert.ToInt32(levelCaps[skill]) != 0)
                {
                    modifiedLevel = Convert.ToInt32(levelCaps[skill]);
                }
				setPointsAndLevel(Convert.ToUInt64(p.Id), skill, getLevelPoints(modifiedLevel), modifiedLevel);
				var baseP = BasePlayer.FindByID(Convert.ToUInt64(p.Id));
				if (baseP != null)
					RenderUI(baseP);
                SendReply(arg, msg(skill + "Skill") + " Lvl for [" + p.Name + "] set to: [" + modifiedLevel + "]");
            }
        }

		string getStatPrint(BasePlayer player, string skill)
        {
            if (IsSkillDisabled(skill))
				return string.Empty;

            var skillMaxed = (int)levelCaps[skill] != 0 && getLevel(player.userID, skill) == (int)levelCaps[skill];
            var bonusText = string.Empty;
            if (skill == Skills.CRAFTING)
                bonusText =
                    (getLevel(player.userID, skill) * (int)craftingDetails["PercentFasterPerLevel"]).ToString("0.##");
            else
                bonusText = ((getGathMult(getLevel(player.userID, skill), skill) - 1) * 100).ToString("0.##");

            return string.Format("<color=" + colors[skill] + '>' + msg("StatsText", player.UserIDString) + "</color>\n",
                msg(skill + "Skill", player.UserIDString),
                getLevel(player.userID, skill) + (Convert.ToInt32(levelCaps[skill]) > 0 ? ("/" + levelCaps[skill]) : ""),
                getPoints(player.userID, skill),
                skillMaxed ? "∞" : getLevelPoints(getLevel(player.userID, skill) + 1).ToString(),
                bonusText,
                getExperiencePercent(player, skill),
                getPenaltyPercent(player, skill) + "%");
        }

		void removePoints(ulong userID, string skill, long points)
        {
			var field = typeof(PlayerInfo).GetField(skill + "P");
			var skillpoints = (long)field.GetValue(playerPrefs.PlayerInfo[userID]);
			if (skillpoints - 10 > points)
				skillpoints -= points;
			else
				skillpoints = 10;
			field.SetValue(playerPrefs.PlayerInfo[userID], skillpoints);

			setLevel(userID, skill, getPointsLevel(skillpoints, skill));
        }

		void setLevel(ulong userID, string skill, long level)
        {
            var field = typeof(PlayerInfo).GetField(skill + "L");
			field.SetValue(playerPrefs.PlayerInfo[userID], level);
        }

		int GetPenalty(BasePlayer player, string skill)
        {
            var penalty = 0;
            var penaltyPercent = getPenaltyPercent(player, skill);
			var field = typeof(PlayerInfo).GetField(skill + "L");
			penalty = Convert.ToInt32(getPercentAmount((long)field.GetValue(playerPrefs.PlayerInfo[player.userID]), penaltyPercent));
			return penalty;
        }

		int getPenaltyPercent(BasePlayer player, string skill)
        {
            var penaltyPercent = 0;
            var details = playerPrefs.PlayerInfo[player.userID].LD;
			var currentTime = DateTime.UtcNow;
			var lastDeath = ToDateTimeFromEpoch(details);
			var timeAlive = currentTime - lastDeath;
			if (timeAlive.TotalMinutes > 10)
			{
				penaltyPercent = ((int)percentLostOnDeath[skill] - ((int)timeAlive.TotalHours * (int)percentLostOnDeath[skill] / 10));
				if (penaltyPercent < 0)
					penaltyPercent = 0;
			}
            return penaltyPercent;
        }

		void levelHandler(BasePlayer player, Item item, string skill)
        {
            var xpPercentBefore = getExperiencePercent(player, skill);
            var Level = getLevel(player.userID, skill);
            var Points = getPoints(player.userID, skill);
            item.amount = (int)(item.amount * getGathMult(Level, skill));
            var pointsToGet = (int)pointsPerHitCurrent[skill];
            var xpMultiplier = Convert.ToInt64(playerPrefs.PlayerInfo[player.userID].XPM);
            Points += Convert.ToInt64(pointsToGet * (xpMultiplier / 100f));
            getPointsLevel(Points, skill);
            try
            {
                if (Points >= getLevelPoints(Level + 1))
                {
                    var maxLevel = (int)levelCaps[skill] > 0 && Level + 1 > (int)levelCaps[skill];
                    if (!maxLevel)
                    {
                        Level = getPointsLevel(Points, skill);
                        PrintToChat(player, string.Format("<color=" + colors[skill] + '>' + msg("LevelUpText", player.UserIDString) + "</color>",
                            msg(skill + "Skill", player.UserIDString),
                            Level,
                            Points,
                            getLevelPoints(Level + 1),
                            ((getGathMult(Level, skill) - 1) * 100).ToString("0.##")
                            )
                        );
                    }
                }
            } catch {}
            setPointsAndLevel(player.userID, skill, Points, Level);
            var xpPercentAfter = getExperiencePercent(player, skill);
            if (!xpPercentAfter.Equals(xpPercentBefore))
                RenderUI(player);
        }

		string getExperiencePercent(BasePlayer player, string skill)
        {
            return getExperiencePercentInt(player, skill) + "%";
        }

		int getExperiencePercentInt(BasePlayer player, string skill)
        {
            var Level = getLevel(player.userID, skill);
            var startingPoints = getLevelPoints(Level);
            var nextLevelPoints = getLevelPoints(Level + 1) - startingPoints;
            var Points = getPoints(player.userID, skill) - startingPoints;
            var experienceProc = Convert.ToInt32((Points / (double)nextLevelPoints) * 100);
            if (experienceProc >= 100)
                experienceProc = 99;
            else if (experienceProc == 0)
                experienceProc = 1;
            return experienceProc;
        }

		void setPointsAndLevel(ulong userID, string skill, long points, long level)
        {
            var fieldL = typeof(PlayerInfo).GetField(skill + "L");
			fieldL.SetValue(playerPrefs.PlayerInfo[userID], level);
			var fieldP = typeof(PlayerInfo).GetField(skill + "P");
			fieldP.SetValue(playerPrefs.PlayerInfo[userID], points == 0 ? getLevelPoints(level) : points);
        }

		long getLevel(ulong userID, string skill)
        {
            var field = typeof(PlayerInfo).GetField(skill + "L");
			return (long)field.GetValue(playerPrefs.PlayerInfo[userID]);
        }

		long getPoints(ulong userID, string skill)
        {
            var field = typeof(PlayerInfo).GetField(skill + "P");
			return (long)field.GetValue(playerPrefs.PlayerInfo[userID]);
        }

		/*
		void printMaxSkillDetails(BasePlayer player, string skill)
        {
            var sql = Sql.Builder.Append("SELECT * FROM RPG_User ORDER BY " + skill + "Level DESC," + skill + "Points DESC LIMIT 1;");
            if (usingMySQL())
            {
                _mySql.Query(sql, mySqlConnection, list =>
                {
                    if (list.Count > 0)
                        printMaxSkillDetails(player, skill, list);
                });
            }
            else
            {
                _sqLite.Query(sql, sqLiteConnection, list =>
                {
                    if (list.Count > 0)
                        printMaxSkillDetails(player, skill, list);
                });
            }
        }
		*/

		/*
        void printMaxSkillDetails(BasePlayer player, string skill, List<Dictionary<string, object>> sqlData)
        {
            PrintToChat(player,
                            "<color=" + colors[skill] + ">" + messages[skill + "Skill"] + ": " +
                            sqlData[0][skill + "Level"] + " (XP: " + sqlData[0][skill + "Points"] + ")</color> <- " +
                            sqlData[0]["Name"]);
        }
		*/
		
        long getLevelPoints(long level) => 110 * level * level - 100 * level;

        long getPointsLevel(long points, string skill)
        {
            var a = 110;
            var b = 100;
            var c = -points;
            var x1 = (-b - Math.Sqrt(b * b - 4 * a * c)) / (2 * a);
            if ((int)levelCaps[skill] == 0 || (int)-x1 <= (int)levelCaps[skill])
                return (int)-x1;
            return (int)levelCaps[skill];
        }

        double getGathMult(long skillLevel, string skill)
        {
			return Convert.ToDouble(defaultMultipliers[skill]) + Convert.ToDouble(resourceMultipliers[skill]) * 0.1 * (skillLevel - 1);
        }

        bool IsSkillDisabled(string skill)
        {
			if (skill == Skills.CRAFTING && ConVar.Craft.instant)
				return true;
			return levelCaps[skill].ToString() == "-1";
        }

        bool IsSkillEnabled(string skill)
        {
            if (skill == Skills.CRAFTING && ConVar.Craft.instant)
				return false;
			return levelCaps[skill].ToString() != "-1";
        }

        long getPointsNeededForNextLevel(long level)
        {
            //var startingPoints = getLevelPoints(level);
            //var nextLevelPoints = getLevelPoints(level + 1);
            //var pointsNeeded = nextLevelPoints - startingPoints;
            //return pointsNeeded;
			return getLevelPoints(level + 1) - getLevelPoints(level);
        }

        long getPercentAmount(long level, int percent)
        {
            //var points = getPointsNeededForNextLevel(level);
            //var percentPoints = (points * percent) / 100;
            //return percentPoints;
			return (getPointsNeededForNextLevel(level) * percent) / 100;
        }

        #endregion Functions

		#region CUI

        void FillElements(ref CuiElementContainer elements, string mainPanel, int rowNumber, int maxRows, long level, int percent, string skillName, string progressColor, int fontSize, string xpBarAnchorMin, string xpBarAnchorMax)
        {
            var value = 1 / (float)maxRows;
            var positionMin = 1 - (value * rowNumber);
            var positionMax = 2 - (1 - (value * (1 - rowNumber)));
            var xpBarPlaceholder1 = new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = mainPanel,
                Components =
                        {
                            new CuiImageComponent { Color = cuiBoundsBackground },
                            new CuiRectTransformComponent{ AnchorMin = "0 " + positionMin.ToString("0.####"), AnchorMax = $"1 "+ positionMax.ToString("0.####") }
                        }
            };
            elements.Add(xpBarPlaceholder1);

            var innerXPBar1 = new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = xpBarPlaceholder1.Name,
                Components =
                        {
                            new CuiImageComponent { Color = cuiXpBarBackground },
                            new CuiRectTransformComponent{ AnchorMin = xpBarAnchorMin, AnchorMax = xpBarAnchorMax }
                        }
            };
            elements.Add(innerXPBar1);

            var innerXPBarProgress1 = new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = innerXPBar1.Name,
                Components =
                        {
                            new CuiImageComponent() { Color = progressColor },
                            new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = (percent / 100.0) + " 1" }
                        }
            };
            elements.Add(innerXPBarProgress1);
			
            if (cuiTextShadow)
			{
				var innerXPBarTextShadow1 = new CuiElement
				{
					Name = CuiHelper.GetGuid(),
					Parent = innerXPBar1.Name,
					Components =
							{
								new CuiTextComponent { Color = "0.1 0.1 0.1 0.75", Text = $"{skillName} ({percent}%)", FontSize = cuiFontSizeBar, Align = TextAnchor.MiddleLeft},
								new CuiRectTransformComponent{ AnchorMin = "0.06 -0.1", AnchorMax = "1 1" }
							}
				};
				elements.Add(innerXPBarTextShadow1);
			}

            var innerXPBarText1 = new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = innerXPBar1.Name,
                Components =
                        {
                            new CuiTextComponent { Color = cuiFontColor, Text = $"{skillName} ({percent}%)", FontSize = cuiFontSizeBar, Align = TextAnchor.MiddleLeft},
                            new CuiRectTransformComponent{ AnchorMin = "0.05 0", AnchorMax = "1 1" }
                        }
            };
            elements.Add(innerXPBarText1);

			if (cuiTextShadow)
			{
				var lvShader1 = new CuiElement
				{
					Name = CuiHelper.GetGuid(),
					Parent = xpBarPlaceholder1.Name,
					Components =
							{
								new CuiTextComponent { Text = "Lv." + level, FontSize = cuiFontSizeLvl , Align = TextAnchor.MiddleLeft, Color = "0.1 0.1 0.1 0.75" },
								new CuiRectTransformComponent{ AnchorMin = "0.035 -0.1", AnchorMax = $"0.5 1" }
							}
				};
				elements.Add(lvShader1); 
			}			
			
			var lvText1 = new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = xpBarPlaceholder1.Name,
                Components =
                        {
                            new CuiTextComponent { Text = "Lv." + level, FontSize = cuiFontSizeLvl , Align = TextAnchor.MiddleLeft, Color = cuiFontColor },
                            new CuiRectTransformComponent{ AnchorMin = "0.025 0", AnchorMax = $"0.5 1" }
                        }
            };
            elements.Add(lvText1);
        }

        void BlendOutUI(BasePlayer player)
        {
            if (player.userID < 76560000000000000L || !playerPrefs.PlayerInfo[player.userID].CUI || !hasRights(player.UserIDString)) return;
			CuiHelper.DestroyUi(player, "StatsUI");
		}

        void RenderUI(BasePlayer player)
        {
            if (!playerPrefs.PlayerInfo[player.userID].CUI || !hasRights(player.UserIDString)) return;
            var enabledSkillCount = 0;
            foreach (var skill in Skills.ALL)
            {
                if (IsSkillEnabled(skill))
				{
					enabledSkillCount++;
				}
            }
            CuiHelper.DestroyUi(player, "StatsUI");

            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.15 0.15 0.15 0.05"
                },
                RectTransform =
                {
					AnchorMin = $"{(string)cuiPositioning["WidthLeft"]} {(string)cuiPositioning["HeightLower"]}",
                    AnchorMax =$"{(string)cuiPositioning["WidthRight"]} {(string)cuiPositioning["HeightUpper"]}"

				}
            }, "Hud", "StatsUI");

            var fontSize = 12;
            var xpBarAnchorMin = "0.18 0.1";
            var xpBarAnchorMax = "0.98 0.9";
            var currentSKillIndex = 1;

            foreach (var skill in Skills.ALL)
            {
                if (IsSkillEnabled(skill))
                {
                    FillElements(ref elements, mainName, currentSKillIndex, enabledSkillCount, getLevel(player.userID, skill), getExperiencePercentInt(player,
                        skill), msg(skill + "Skill", player.UserIDString), (string)cuiColors[skill], fontSize, xpBarAnchorMin, xpBarAnchorMax);
                    currentSKillIndex++;
                }
            }
            CuiHelper.AddUi(player, elements);
        }

		#endregion CUI

		#region Database

		#endregion Database

		#region Helpers

		 string ReadableTimeSpan(TimeSpan span)
        {
            var formatted = string.Format("{0}{1}{2}{3}{4}",
                (span.Days / 7) > 0 ? string.Format("{0:0} weeks, ", span.Days / 7) : string.Empty,
                span.Days % 7 > 0 ? string.Format("{0:0} days, ", span.Days % 7) : string.Empty,
                span.Hours > 0 ? string.Format("{0:0} hours, ", span.Hours) : string.Empty,
                span.Minutes > 0 ? string.Format("{0:0} minutes, ", span.Minutes) : string.Empty,
                span.Seconds > 0 ? string.Format("{0:0} seconds, ", span.Seconds) : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);
            return formatted;
        }

		long ToEpochTime(DateTime dateTime)
        {
            var date = dateTime.ToUniversalTime();
            var ticks = date.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;
            var ts = ticks / TimeSpan.TicksPerSecond;
            return ts;
        }

        DateTime ToDateTimeFromEpoch(long intDate)
        {
            var timeInTicks = intDate * TimeSpan.TicksPerSecond;
            return new DateTime(1970, 1, 1, 0, 0, 0, 0).AddTicks(timeInTicks);
        }

		string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        ItemDefinition GetItem(string shortname)
        {
            if (string.IsNullOrEmpty(shortname) || CraftItems == null) return null;
            ItemDefinition item;
            if (CraftItems.TryGetValue(shortname, out item)) return item;
            return null;
        }

		void GenerateItems(bool reset = false)
        {
            if (!reset)
            {
				var config_protocol = gameProtocol;
                if (config_protocol != Rust.Protocol.network)
                {
                    gameProtocol = Rust.Protocol.network;
                    Config["Generic","gameProtocol"] = gameProtocol;
					Puts("Updating item list from protocol " + config_protocol + " to protocol " + gameProtocol + ".");
                    GenerateItems(true);
                    SaveConfig();
                    return;
                }
            }

            if (reset)
            {
                Interface.GetMod().DataFileSystem.WriteObject("ZLevelsCraftDetails.old", _craftData);
                _craftData.CraftList.Clear();
                Puts("Generating new item list...");
            }

            CraftItems = ItemManager.itemList.ToDictionary(i => i.shortname);
            int loaded = 0, enabled = 0;
            foreach (var definition in CraftItems)
            {
                if (definition.Value.shortname.Length >= 1)
                {
                    CraftInfo p;
                    if (_craftData.CraftList.TryGetValue(definition.Value.shortname, out p))
                    {
                        if (p.Enabled) { enabled++; }
                        loaded++;
                    }
                    else
                    {
                        var z = new CraftInfo
                        {
                            shortName = definition.Value.shortname,
                            MaxBulkCraft = MaxB,
                            MinBulkCraft = MinB,
                            Enabled = true
                        };
                        _craftData.CraftList.Add(definition.Value.shortname, z);
                        loaded++;
                    }
                }
            }
            var inactive = loaded - enabled;
            Puts("Loaded " + loaded + " items. (Enabled: " + enabled + " | Inactive: " + inactive + ").");
            Interface.GetMod().DataFileSystem.WriteObject("ZLevelsCraftDetails", _craftData);
        }

		#endregion Helpers
    }
}
