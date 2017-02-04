using Oxide.Game.Rust.Cui;
using Oxide.Plugins;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Enhanced Hammer", "Fuji/Visa", "1.2.1", ResourceId = 1439)]
    public class EnhancedHammer : RustPlugin
    {
        bool Changed = false;
		
		string pluginPrefix;
		string permissionName;
		bool enablePermission;
		static int defaultDisableTimer;
		float hammerHitRange;
		bool enableDistanceUpgrade;
		
		string UIAnchorMin = "0.32 0.09";
        string UIAnchorMax = "0.34 0.13";
		
		string iconRepair;
		string iconWood;
		string iconStone;
		string iconMetal;
		string iconTopTier;
		
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
			pluginPrefix = Convert.ToString(GetConfig("Settings", "pluginPrefix", "<color=orange>Enhanced Hammer</color>: "));
			defaultDisableTimer = Convert.ToInt32(GetConfig("Settings", "defaultDisableTimer", 30));
			UIAnchorMin = Convert.ToString(GetConfig("Settings", "UIAnchorMin", "0.32 0.09"));
			UIAnchorMax = Convert.ToString(GetConfig("Settings", "UIAnchorMax", "0.34 0.13"));
			hammerHitRange = Convert.ToInt32(GetConfig("Settings", "hammerHitRange", 3.0f));
			enableDistanceUpgrade =  Convert.ToBoolean(GetConfig("Settings", "enableDistanceUpgrade", true));
			
			permissionName  = Convert.ToString(GetConfig("Permission", "permissionName", "enhancedhammer.use"));
			enablePermission  = Convert.ToBoolean(GetConfig("Permission", "enablePermission", false));
			
			iconRepair  = Convert.ToString(GetConfig("Icons", "iconRepair", "http://i.imgur.com/Nq6DNSX.png"));
			iconWood  = Convert.ToString(GetConfig("Icons", "iconWood", "http://i.imgur.com/F4XBBhY.png"));
			iconStone  = Convert.ToString(GetConfig("Icons", "iconStone", "http://i.imgur.com/S7Sl9oh.png"));
			iconMetal  = Convert.ToString(GetConfig("Icons", "iconMetal", "http://i.imgur.com/fVjzbag.png"));
			iconTopTier  = Convert.ToString(GetConfig("Icons", "iconTopTier", "http://i.imgur.com/f0WklR3.png"));
			
			if (!Changed) return;
			SaveConfig();
			Changed = false;
		}
		
		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadVariables();
		}
		
		void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			                      {
									{"NoPermission", "You don't have permission to use this command"},
									{"RepairMode", "You are now in REPAIR mode"},
									{"UpgradeBlocked", "You can't upgrade it, something is blocking it's way"},
									{"BuildingBlocked", "Building is blocked"},
									{"CantUpgradeWalls", "Can't upgrade walls! Switching to REPAIR mode"},
									{"CantAffordUpgrade", "Can't afford to upgrade"},
									{"UpgradeModeGrade", "You are now in UPGRADE mode [ {0} ]"},
									{"TimerSetTo", "Timer set to {0} seconds"},
									{"TimerUnlimited", "Timer will never end"},
									{"Help1", "Command usage"},
									{"Help2", "<color=yellow>/eh <enable | disable></color> - Enables or disabled plugin functionality."},
									{"Help3", "<color=yellow>/eh <show | hide></color> - Shows or hides plugin icons."},
									{"Help4", "<color=yellow>/eh timer <0 (unltd) | seconds></color> - Time in which hammer goes back to default mode."},
									{"Help5", "<color=yellow>/eh msgs <show | hide ></color> - Show messages in chat about hammer state."},
									{"Action1", "Icons"},
									{"Action2", "Plugin"},
									{"Action3", "Messages"},
									{"ActionEnabled", "Enabled"},
									{"ActionDisabled", "Disabled"},
									},this);
		}
		
		void Loaded()
		{
			LoadVariables();
			LoadDefaultMessages();
			if (!permission.PermissionExists(permissionName)) permission.RegisterPermission(permissionName, this);
		}

		public class PlayerDetails
        {
            public PlayerFlags flags = PlayerFlags.MESSAGES_DISABLED;
            public BuildingGrade.Enum upgradeInfo = BuildingGrade.Enum.Count; // HAMMER
            public int backToDefaultTimer = defaultDisableTimer;
        }

        public enum PlayerFlags
        {
            NONE = 0,
            ICONS_DISABLED = 2,
            PLUGIN_DISABLED = 4,
            MESSAGES_DISABLED = 8
        }

        public static Dictionary<ulong, PlayerDetails> playersInfo = new Dictionary<ulong, PlayerDetails>();
        public static Dictionary<ulong, Timer> playersTimers = new Dictionary<ulong, Timer>();
		
		void OnPlayerInput(BasePlayer player, InputState inputState)
		{
			if (!enableDistanceUpgrade || player == null || player.svActiveItemID == 0 || player.GetActiveItem() == null || player.GetActiveItem().info.shortname != "hammer" || inputState == null || !inputState.WasJustPressed(BUTTON.FIRE_PRIMARY) ||
				(enablePermission && !permission.UserHasPermission(player.UserIDString, permissionName)) || PlayerHasFlag(player.userID, PlayerFlags.PLUGIN_DISABLED)) return;
			BaseEntity targetEntity;
            RaycastHit rayHit;
			bool flag1 = UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out rayHit, hammerHitRange, 2097152);
			targetEntity = flag1 ? rayHit.GetEntity() : null;
			if (targetEntity != null && targetEntity is BuildingBlock)
			{
				BuildingBlock block = targetEntity as BuildingBlock;
				BaseCombatEntity entity = targetEntity as BaseCombatEntity;
				if (playersInfo[player.userID].upgradeInfo == BuildingGrade.Enum.Count || playersInfo[player.userID].upgradeInfo <= block.currentGrade.gradeBase.type || !player.CanBuild())
				{
					if (playersInfo[player.userID].upgradeInfo != BuildingGrade.Enum.Count && playersInfo[player.userID].upgradeInfo <= block.currentGrade.gradeBase.type)
					{
						if (entity.healthFraction < 1f)
						{
							if(!PlayerHasFlag(player.userID, PlayerFlags.MESSAGES_DISABLED))
								player.ChatMessage(pluginPrefix + lang.GetMessage("RepairMode", this, player.UserIDString));
							playersInfo[player.userID].upgradeInfo = BuildingGrade.Enum.Count;
							RenderMode(player, true);
						}
					}
					else if (!player.CanBuild())
						player.ChatMessage(pluginPrefix + lang.GetMessage("BuildingBlocked", this, player.UserIDString));
				}
				else
					UpgradeBlock(player, entity, block);
			}
		}

        void OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
			if (entity == null || player == null || (enablePermission && !permission.UserHasPermission(player.UserIDString, permissionName)) || PlayerHasFlag(player.userID, PlayerFlags.PLUGIN_DISABLED) || !(entity is BuildingBlock))
				return;
			BuildingBlock block = entity as BuildingBlock;
            if (playersInfo[player.userID].upgradeInfo == BuildingGrade.Enum.Count || playersInfo[player.userID].upgradeInfo <= block.currentGrade.gradeBase.type || !player.CanBuild())
            {
                if (playersInfo[player.userID].upgradeInfo != BuildingGrade.Enum.Count && playersInfo[player.userID].upgradeInfo <= block.currentGrade.gradeBase.type)
                {
                    if (entity.healthFraction < 1f)
					{
						if(!PlayerHasFlag(player.userID, PlayerFlags.MESSAGES_DISABLED))
							player.ChatMessage(pluginPrefix + lang.GetMessage("RepairMode", this, player.UserIDString));
						playersInfo[player.userID].upgradeInfo = BuildingGrade.Enum.Count;
						RenderMode(player, true);
					}
                }
                else if (!player.CanBuild())
					player.ChatMessage(pluginPrefix + lang.GetMessage("BuildingBlocked", this, player.UserIDString));
            }
            else
				UpgradeBlock(player, entity, block);
            RefreshTimer(player);
        }
		
		void UpgradeBlock(BasePlayer player, BaseCombatEntity entity, BuildingBlock block)
		{
			BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
			MethodInfo dynMethod = block.GetType().GetMethod("CanChangeToGrade", flags);
			bool canChangeGrade = (bool)dynMethod.Invoke(block, new object[] { playersInfo[player.userID].upgradeInfo, player });

			if (!canChangeGrade)
			{
				player.ChatMessage(pluginPrefix + lang.GetMessage("UpgradeBlocked", this, player.UserIDString));
				return;
			}

			if (block.name.ToLower().Contains("wall.external"))
			{
				player.ChatMessage(pluginPrefix + lang.GetMessage("CantUpgradeWalls", this, player.UserIDString));
				playersInfo[player.userID].upgradeInfo = BuildingGrade.Enum.Count;
				return;
			}
			float currentHealth = block.health;
			var currentGradeType = block.currentGrade.gradeBase.type;
			block.SetGrade(playersInfo[player.userID].upgradeInfo);
			var TwigsDecay = plugins.Find("TwigsDecay");
			TwigsDecay?.Call("OnStructureUpgrade", block, player, playersInfo[player.userID].upgradeInfo);
			block.UpdateSkin(false);
			var cost = block.currentGrade.gradeBase.baseCost;
			int hasEnough = 0;
			foreach (var itemCost in cost)
			{
				int itemCostAmount = Convert.ToInt32((float)itemCost.amount*block.blockDefinition.costMultiplier);
				var foundItems = player.inventory.FindItemIDs(itemCost.itemid);
				var amountFound = foundItems?.Sum(item => item.amount) ?? 0;
				if (amountFound >= itemCostAmount)
					hasEnough++;
			}
			if (hasEnough >= cost.Count)
			{
				foreach (var itemCost in cost)
				{
					int itemCostAmount = Convert.ToInt32((float)itemCost.amount * block.blockDefinition.costMultiplier);
					var foundItems = player.inventory.FindItemIDs(itemCost.itemid);
					int taken = player.inventory.Take(foundItems, itemCost.itemid, itemCostAmount);
					player.Command(string.Concat(new object[]{"note.inv ", itemCost.itemid, " ",	taken * -1f}), new object[0]);
				}
				block.SetHealthToMax();
				block.SetFlag(BaseEntity.Flags.Reserved1, true); // refresh rotation
				block.Invoke("StopBeingRotatable", 600f);
				Effect.server.Run("assets/bundled/prefabs/fx/build/promote_" + playersInfo[player.userID].upgradeInfo.ToString().ToLower() + ".prefab", block, 0u, Vector3.zero, Vector3.zero, null, false);
			}
			else
			{
				block.SetGrade(currentGradeType);
				TwigsDecay?.Call("OnStructureUpgrade", block, player, currentGradeType);
				block.UpdateSkin(false);
				block.health = currentHealth;
				player.ChatMessage(pluginPrefix + lang.GetMessage("CantAffordUpgrade", this, player.UserIDString));
			}
		}		

        void RefreshTimer(BasePlayer player)
        {
            if (playersInfo[player.userID].backToDefaultTimer == 0)
                return;

            if (playersTimers.ContainsKey(player.userID))
            {
                playersTimers[player.userID].Destroy();
                playersTimers.Remove(player.userID);
            }

            var timerIn = timer.Once(playersInfo[player.userID].backToDefaultTimer, () => SetBackToDefault(player));
            playersTimers.Add(player.userID, timerIn);
        }

        void OnStructureUpgrade(BaseCombatEntity block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if ((enablePermission && !permission.UserHasPermission(player.UserIDString, permissionName)) || PlayerHasFlag(player.userID, PlayerFlags.PLUGIN_DISABLED))
                return;

            if (playersInfo[player.userID].upgradeInfo != grade)
            {
                playersInfo[player.userID].upgradeInfo = grade;
                RenderMode(player, false);
                if (!PlayerHasFlag(player.userID, PlayerFlags.MESSAGES_DISABLED))
					player.ChatMessage(pluginPrefix + string.Format(lang.GetMessage("UpgradeModeGrade", this, player.UserIDString), grade.ToString()));
            }
            RefreshTimer(player);
        }

        void RenderMode(BasePlayer player, bool repair = false)
        {
            CuiHelper.DestroyUi(player, "EnhancedHammerUI");
            if (PlayerHasFlag(player.userID, PlayerFlags.PLUGIN_DISABLED) || 
                PlayerHasFlag(player.userID, PlayerFlags.ICONS_DISABLED) || 
                (!repair && playersInfo[player.userID].upgradeInfo == BuildingGrade.Enum.Count))
                return;

            CuiElementContainer panel = new CuiElementContainer();
            string icon = iconRepair;
            if (!repair)
            {
                switch (playersInfo[player.userID].upgradeInfo)
                {
                    case BuildingGrade.Enum.Wood:
                        icon = iconWood;
                        break;
                    case BuildingGrade.Enum.Stone:
                        icon = iconStone;
                        break;
                    case BuildingGrade.Enum.Metal:
                        icon = iconMetal;
                        break;
                    case BuildingGrade.Enum.TopTier:
                        icon = iconTopTier;
                        break;
                }
            }
            CuiElement ehUI = new CuiElement { Name = "EnhancedHammerUI", Parent = "Hud", FadeOut = 0.5f };
            CuiRawImageComponent ehUI_IMG = new CuiRawImageComponent { FadeIn = 0.5f, Url = icon };
            CuiRectTransformComponent ehUI_RECT = new CuiRectTransformComponent
            {
                AnchorMin = UIAnchorMin,
                AnchorMax = UIAnchorMax
            };
            ehUI.Components.Add(ehUI_IMG);
            ehUI.Components.Add(ehUI_RECT);
            panel.Add(ehUI);
            CuiHelper.AddUi(player, panel);
        }

        void SetBackToDefault(BasePlayer player)
        {
			playersTimers.Remove(player.userID);
			if(playersInfo.ContainsKey(player.userID))
				playersInfo[player.userID].upgradeInfo = BuildingGrade.Enum.Count;
            RemoveUI(player);
            if (!PlayerHasFlag(player.userID, PlayerFlags.MESSAGES_DISABLED))
                player.ChatMessage(pluginPrefix + lang.GetMessage("RepairMode", this, player.UserIDString));
        }

        void RemoveUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "EnhancedHammerUI");
        }

        void OnPlayerInit(BasePlayer player)
        {
            playersInfo[player.userID] = new PlayerDetails();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
			playersInfo.Remove(player.userID);
        }

        public PlayerFlags GetPlayerFlags(ulong userID)
        {
            if (playersInfo.ContainsKey(userID))
                    return playersInfo[userID].flags;
            return PlayerFlags.NONE;
        }

        void Init()
        {
            foreach (var player in BasePlayer.activePlayerList)
				playersInfo[player.userID] = new PlayerDetails();
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                RemoveUI(player);
				playersInfo.Remove(player.userID);
            }
        }

        [ChatCommand("eh")]
        private void OnEhCommand(BasePlayer player, string command, string[] arg)
        {
			if(enablePermission && !permission.UserHasPermission(player.UserIDString, permissionName))
			{
				player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString));
				return;
			}			
			bool incorrectUsage = arg.Length == 0;
            bool ADD = true;
            bool REMOVE = false;
            if (arg.Length == 1)
            {
                switch (arg[0].ToLower())
                {
                    case "enable":
                        ModifyPlayerFlags(player, REMOVE, PlayerFlags.PLUGIN_DISABLED);
                        break;
                    case "disable":
                        ModifyPlayerFlags(player, ADD, PlayerFlags.PLUGIN_DISABLED);
                        break;
                    case "show":
                        ModifyPlayerFlags(player, REMOVE, PlayerFlags.ICONS_DISABLED);
                        break;
                    case "hide":
                        ModifyPlayerFlags(player, ADD, PlayerFlags.ICONS_DISABLED);
                        break;
                    default:
                        incorrectUsage = true;
                        break;
                }
                if (!incorrectUsage)
                    RenderMode(player);
            }
            else if (arg.Length == 2)
            {
                if (arg[0].ToLower() == "timer")
                {
                    int seconds;
                    if (int.TryParse(arg[1], out seconds) && seconds >= 0)
                    {
                        playersInfo[player.userID].backToDefaultTimer = seconds;
                        string msg = "";
                        if (seconds > 0)
                            msg += string.Format(lang.GetMessage("TimerSetTo", this, player.UserIDString), seconds);
						
                        else
                            msg += lang.GetMessage("TimerUnlimited", this, player.UserIDString);
                        SendReply(player, pluginPrefix + msg);
                        incorrectUsage = false;
                    }
                }
                else if (arg[0].ToLower() == "msgs")
                {
                    if (arg[1].ToLower() == "show")
                        ModifyPlayerFlags(player, false, PlayerFlags.MESSAGES_DISABLED);
                    else if (arg[1].ToLower() == "hide")
                        ModifyPlayerFlags(player, true, PlayerFlags.MESSAGES_DISABLED);
                    else
                        incorrectUsage = true;
                }
            }

            if (incorrectUsage)
            {
                var sb = new StringBuilder();
				sb.AppendLine(pluginPrefix + lang.GetMessage("Help1", this, player.UserIDString));
				sb.AppendLine(lang.GetMessage("Help2", this, player.UserIDString));
				sb.AppendLine(lang.GetMessage("Help3", this, player.UserIDString));
				sb.AppendLine(lang.GetMessage("Help4", this, player.UserIDString));
				sb.AppendLine(lang.GetMessage("Help5", this, player.UserIDString));
				player.ChatMessage(sb.ToString().TrimEnd());
            }
        }

        private bool PlayerHasFlag(ulong userID, PlayerFlags flag)
        {
            return (GetPlayerFlags(userID) & flag) == flag;
        }

        private void ModifyPlayerFlags(BasePlayer player, bool addFlag, PlayerFlags flag)
        {
            bool actionCompleted = false;
            if (addFlag)
            {
                if ((playersInfo[player.userID].flags & flag) != flag)
                {
                    playersInfo[player.userID].flags |= flag;
                    actionCompleted = true;
                }
            }
            else
            {
                if ((playersInfo[player.userID].flags & flag) == flag)
                {
                    playersInfo[player.userID].flags &= ~flag;
                    actionCompleted = true;
                }
            }

            if (actionCompleted)
            {
                string msg = "";
                switch (flag)
                {
                    case PlayerFlags.ICONS_DISABLED:
                        msg += "ICONS"; lang.GetMessage("Action1", this, player.UserIDString);
                        break;
                    case PlayerFlags.PLUGIN_DISABLED:
                        msg += "PLUGIN"; lang.GetMessage("Action2", this, player.UserIDString);
                        break;
                    case PlayerFlags.MESSAGES_DISABLED:
                        msg += "MESSAGES"; lang.GetMessage("Action3", this, player.UserIDString);
                        break;
                }
				player.ChatMessage(pluginPrefix + msg + " > " + (!addFlag? lang.GetMessage("ActionEnabled", this, player.UserIDString) : lang.GetMessage("ActionDisabled", this, player.UserIDString)));
				
            }
        }
    }
}