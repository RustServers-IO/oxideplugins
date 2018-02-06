using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

using Oxide.Core;
using Oxide.Game.Rust.Cui;

// TODO: Recycling.

namespace Oxide.Plugins
{
    [Info("XpRevived", "Mattparks", "0.2.5", ResourceId = 2753)]
    [Description("A plugin that brings back a XP system.")]
    class XpRevived : RustPlugin
    {
        #region Managers

        [PluginReference] RustPlugin ImageLibrary;
        static XpRevived _plugin;

        public class Images
        {
            public static string TryForImage(string shortname, ulong skin = 99, bool localimage = true)
            {
                if (localimage)
                {
                    if (skin == 99)
                    {
                        return GetImage(shortname, (ulong)_plugin.ResourceId);
                    }
                    else
                    {
                        return GetImage(shortname, skin);
                    }
                }
                else if (skin == 99)
                {
                    return GetImageURL(shortname, (ulong)_plugin.ResourceId);
                }
                else
                {
                    return GetImageURL(shortname, skin);
                }
            }

            public static string GetImageURL(string shortname, ulong skin = 0) => (string)_plugin.ImageLibrary?.Call("GetImageURL", shortname, skin);
            public static uint GetTextureID(string shortname, ulong skin = 0) => (uint)_plugin.ImageLibrary?.Call("GetTextureID", shortname, skin);
            public static string GetImage(string shortname, ulong skin = 0) => (string)_plugin.ImageLibrary?.Call("GetImage", shortname, skin);
            public static bool AddImage(string url, string shortname, ulong skin = 0) => (bool)_plugin.ImageLibrary?.Call("AddImage", url, shortname, skin);
            public static bool HasImage(string shortname, ulong skin = 0) => (bool)_plugin.ImageLibrary?.Call("HasImage", shortname, skin);
            public static void TryAddImage(string url, string shortname, ulong skin = 0)
            {
                if (!HasImage(shortname, skin))
                {
                    AddImage(url, shortname, skin);
                }
            }

            public static List<ulong> GetImageList(string shortname) => (List<ulong>)_plugin.ImageLibrary?.Call("GetImageList", shortname);
        }

        public class UI
        {
            public static CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool useCursor = false, string parent = "Overlay")
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }

            public static void LoadImage(ref CuiElementContainer container, string panel, string url, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    FadeOut = 0.15f,
                    Components =
                    {
                        new CuiRawImageComponent { Url = url, FadeIn = 0.3f },
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }

            public static void CreateInput(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, bool password, int charLimit, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiInputFieldComponent { Text = text, FontSize = size, Align = align, Color = color, Command = command, IsPassword = password, CharsLimit = charLimit},
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }

            public static void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }

            public static void CreateText(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            public static void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.TrimStart('#');
                }

                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }

        public class UIManager
        {
            public List<string> activeUis = new List<string>();

            public void AddContainer(string container)
            {
                activeUis.Add(container);
            }

            public void RemoveContainer(string container)
            {
                activeUis.Remove(container);
            }

            public void DestroyUI(BasePlayer player, bool destroyNav = false)
            {
                foreach (var active in activeUis)
                {
                    CuiHelper.DestroyUi(player, active);
                }

                activeUis.Clear();
            }
        }

        #endregion

        #region Configuration

        private List<string> blockedBps = new List<string>{
            "scrap",
            "blueprintbase",
            "bleach",
            "ducttape",
            "gears",
            "glue",
            "techparts",
            "tarp",
            "sticks",
            "metalspring",
            "sewingkit",
            "rope",
            "metalpipe",
            "riflebody",
            "smgbody",
            "semibody",
            "propanetank",
            "metalblade",
            "roadsigns",
            "sheetmetal",
            "targeting.computer",
            "cctv.camera"
        };

        public class ItemData
        {
            public string shortName;
            public string englishName;
            public int unlockLevel;
            public int costXP;
        }

        public class Options
        {
            public bool unlimitedComponents = false; // Allows unlimited components while crafting (items defined in blockedItems list).
            public List<string> blockedItems = null; // A list of blocked items, they will be removed from crafting requirements and loot.
            public List<ItemData> itemDatas = new List<ItemData>(); // A list of all items that have level and xp requirements.
            public string levelUpPrefab = "assets/prefabs/misc/xmas/presents/effects/unwrap.prefab"; // The sound that will play when leveling up.
            public string learnPrefab = "assets/prefabs/misc/xmas/presents/effects/unwrap.prefab"; // The sound that will play the player learns.
            public float levelPivot = 7.0f; // The point where level growth delines.
            public float levelXpRatio = 2.666f; // The amount of leveling defined in LevelRates times this value is the XP gained from a level task.
            public float xpFromGivenTool = 0.05f; // The amount of XP shared to a tools original owner. (TODO)
            public bool removeIngredients = false; // Removes ingredients from blockedItems from item crafting blueprints.
        }

        public class Display
        {
			public string levelIcon = "https://i.imgur.com/lXpowuB.png"; // The icon displayed by the level.
			public string xpIcon = "https://i.imgur.com/RoKRyG7.png"; // The icon displayed by the xp.
			public float offsetX = 0.0f; // HUD offset X.
			public float offsetY = 0.0f; // HUD offset Y.
            public string colourLearned = "#27ae60";
            public string colourNotLearned = "#e74c3c";
        }
		
        public class LevelRates
        {
            public float repeatDelay = 120.0f; // The timeout between repeated XP (AFK points).
            public float repeat = 0.054f; // The amount of repeat XP.

            public float playerKilled = 0.072f; // Amount from killing a player.
            public float playerRecovered = 0.087f; // Amount recovering from wounded.
            public float playerHelped = 0.1f; // Amount from helping a player up. (TODO)

            public float itemCrafted = 0.081f; // Amount from crafting.

            public float recycling = 0.082f; // Amount from recycling. (TODO)
            public float looting = 0.072f; // Amount from looting. (TODO)

            public float killedHeli = 0.92f; // Amount from killing a heli.
            public float killedAnimal = 0.082f; // Amount from killing a animal (scales with animals health).
            public float brokeBarrel = 0.063f; // Amount from breaking a barrel.
            public float itemPickup = 0.045f; // Amount from picking a item up (like hemp or stones, not items).

            public float hitTree = 0.017f; // Amount from hitting a tree.
            public float hitOre = 0.020f; // Amount from hitting a node.
            public float hitFlesh = 0.009f; // Amount from corpse.

            public float supplyThrown = 0.09f; // Amount from throwing a supply signal.
			
            public float structureUpgraded = 0.02f; // Amount from upgrading a structure.
        }

        public class NoobKill
        {
            public int maxNoobLevel = 4; // The highest level to be considered as a noob.
            public int xpPunishment = -2; // The XP punishment given to a noob killer.
        }

        public class ConfigData
        {
            public Options options = new Options();
			public Display display = new Display();
            public LevelRates levelRates = new LevelRates();
            public NoobKill noobKill = new NoobKill();
        }

        public class PlayerData
        {
            public float level;
            public float xp;
        }

        public class StoredData
        {
            public Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();
        }

        private UIManager uiManager;
        private ConfigData configs;
        private StoredData storedData;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new config file!");
            configs = new ConfigData();
			configs.options.blockedItems = null;
			configs.options.itemDatas = null;
            Config.WriteObject(configs, true);
        }

        protected override void LoadDefaultMessages()
        {
            // English messages.
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["XP_ABOUT"] = "<color=#e74c3c>Xp Revived {Version}:</color> by <color=green>mattparks</color>. Xp Revived is a plugin that brings back a XP system. Use the commands as follows: \n <color=#3498db>-</color> /level # (Describes what you can learn from a level) \n <color=#3498db>-</color> /learn 'item' (Lets you learn a item)",
                ["XP_LEVEL_ITEMS"] = "These items are unlocked over level {Level}: ",
                ["XP_LEVEL_NONE"] = "No items found for level{Level}: ",
                ["XP_LEARN_USAGE"] = "Usage: /learn itemname (You try to learn a item, if you are a high enough level with XP)",
                ["XP_LEARN_MIN_LEVEL"] = "You must be level {Level} to learn {ItemName}",
                ["XP_LEARN_UNKNOWN"] = "Could not find item by name of: {ItemName}",
                ["XP_LEARN_KNOWN"] = "You already know: {ItemName}",
                ["XP_LEARN_NEEDS_XP"] = "You must have {Cost} XP to learn {ItemName}",
                ["XP_LEARN_SUCESS"] = "You learned {ItemName}",
                ["XP_NOOB_KILL"] = "You killed a new player, you will be punished!"
            }, this, "en");
        }

        private void LoadStoredData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("XpRevived");

            if (storedData == null)
            {
                PrintWarning("Creating a new data file!");
                storedData = new StoredData();
                SaveStoredData();
            }
        }

        private void SaveStoredData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("XpRevived", storedData);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _plugin = this;
            uiManager = new UIManager();
            configs = Config.ReadObject<ConfigData>();
            LoadStoredData();
        }

        private void OnServerInitialized()
        {
			if (configs.options.unlimitedComponents && configs.options.blockedItems == null)
			{
				PrintWarning("Generating block list!");
				configs.options.blockedItems = new List<string>()
				{
					"scrap", "blueprintbase", "bleach", "ducttape", "gears", "glue", "techparts", "tarp", "sticks", "metalspring", "sewingkit", "rope", "metalpipe", "riflebody", "smgbody", "semibody", "propanetank", "metalblade", "roadsigns", "sheetmetal", // "targeting.computer", "cctv.camera"
				};
			}
			
			if (configs.options.itemDatas == null)
			{
				configs.options.itemDatas = new List<ItemData>();
				ResetConfigXpLevels();
			}
			
            if (configs.options.removeIngredients)
            {
                foreach (var bp in ItemManager.itemList)
                {
                    bp?.Blueprint?.ingredients?.RemoveAll(x =>
                        configs.options.blockedItems.Contains(x.itemDef.shortname));
                }
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                InfiniteComponents(player, true, true);
                UpdatePlayerUi(player);
            }
            
            timer.Repeat(configs.levelRates.repeatDelay, 0, () =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    IncreaseXp(player, configs.levelRates.repeat, false);
                }
            });
            Config.WriteObject(configs, true);
        }
        
        private void OnPlayerInit(BasePlayer player)
        {
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(2.0f, () => OnPlayerInit(player));
                return;
            }

            timer.In(1.0f, () =>
            {
                InfiniteComponents(player, true, true);
                UpdatePlayerUi(player);
            });
        }

        private void OnPlayerSpawn(BasePlayer player)
        {
            InfiniteComponents(player, true, true);
            UpdatePlayerUi(player);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            InfiniteComponents(player, true, true);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            InfiniteComponents(player, true, false);
            uiManager.DestroyUI(player, true);
        }

        private object OnPlayerDie(BasePlayer player, HitInfo info)
        {
            var playerData = GetPlayerData(player.userID);

            if (info?.InitiatorPlayer != null && player != info.InitiatorPlayer)
            {
                var killerData = GetPlayerData(info.InitiatorPlayer.userID);

                if (Math.Floor(playerData.level) <= configs.noobKill.maxNoobLevel &&
                    Math.Floor(killerData.level) > configs.noobKill.maxNoobLevel)
                {
                    MessagePlayer(Lang("XP_NOOB_KILL", info.InitiatorPlayer), info.InitiatorPlayer);
                    IncreaseXp(info.InitiatorPlayer, configs.noobKill.xpPunishment);
                }
                else
                {
                    IncreaseXp(info.InitiatorPlayer, configs.levelRates.playerKilled);
                }
            }

            return null;
        }

        private void OnPlayerRecover(BasePlayer player)
        {
            IncreaseXp(player, configs.levelRates.playerRecovered);
        }

        private void OnItemCraftCancelled(ItemCraftTask task)
        {
            foreach (Item item in task.takenItems.ToList())
            {
                if (configs.options.blockedItems.Contains(item.info.shortname))
                {
                    task.takenItems.Remove(item);
                    item.Remove();
                }
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is LootContainer)
            {
                LootContainer container = entity as LootContainer;
                AssignLoot(container);
            }
        }

        private object OnRecycleItem(Recycler recycler, Item item)
        {
            if (configs.options.blockedItems == null)
            {
                return null;
            }

            if (item.info.Blueprint != null)
            {
                if (item.info.Blueprint.ingredients.Any(x => configs.options.blockedItems.Contains(x?.itemDef?.shortname)))
                {
                    foreach (var itemAmount in item.info.Blueprint.ingredients)
                    {
                        if (!configs.options.blockedItems.Contains(itemAmount.itemDef.shortname))
                        {
                            recycler.MoveItemToOutput(ItemManager.Create(itemAmount.itemDef, Mathf.CeilToInt(itemAmount.amount * recycler.recycleEfficiency))); // Give normal items.
                            continue;
                        }

                        foreach (var componentIngredient in itemAmount.itemDef.Blueprint.ingredients) // Directly convert components into sub materials.
                        {
                            Item newItem = ItemManager.Create(componentIngredient.itemDef, Mathf.CeilToInt((componentIngredient.amount * recycler.recycleEfficiency)) * Mathf.CeilToInt(itemAmount.amount * recycler.recycleEfficiency), 0uL);
                            recycler.MoveItemToOutput(newItem);
                        }
                    }

                    item.UseItem();
                    return true;
                }
            }

            return null;
        }

        private object OnItemResearch(Item item, BasePlayer player)
        {
            return false;
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            IncreaseXp(task.owner, configs.levelRates.itemCrafted);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer)
            {
                BasePlayer player = entity as BasePlayer;
                InfiniteComponents(player, true, false);
            }
			
            if (info.InitiatorPlayer != null)
            {
                if (entity.PrefabName.Contains("npc"))
                {
                    if (entity.PrefabName.Contains("patrolhelicopter"))
                    {
                        IncreaseXp(info.InitiatorPlayer, configs.levelRates.killedHeli);
                    }
                }
                else if (entity.PrefabName.Contains("rust.ai") && !entity.PrefabName.Contains("corpse"))
                {
                    IncreaseXp(info.InitiatorPlayer, configs.levelRates.killedAnimal * (entity._maxHealth / 90.0f));
                }
                else if (entity.PrefabName.Contains("radtown") || entity.PrefabName.Contains("loot-barrel"))
                {
                    IncreaseXp(info.InitiatorPlayer, configs.levelRates.brokeBarrel);
                }
            }
        }

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            IncreaseXp(player, configs.levelRates.itemPickup);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();

            if (player == null || player is NPCPlayer || dispenser == null)
            {
                return;
            }

            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
            {
                IncreaseXp(player, configs.levelRates.hitTree);
            }

            if (dispenser.gatherType == ResourceDispenser.GatherType.Ore)
            {
                IncreaseXp(player, configs.levelRates.hitOre);
            }

            if (dispenser.gatherType == ResourceDispenser.GatherType.Flesh)
            {
                IncreaseXp(player, configs.levelRates.hitFlesh);
            }
        }
		
		private object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
		{
            IncreaseXp(player, configs.levelRates.structureUpgraded);
			return null;
		}
		
        //    private void SupplyThrown(BasePlayer player, BaseEntity entity)
        //    {
        //        IncreaseXp(player, configs.levelRates.supplyThrown);
        //    }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                InfiniteComponents(player, true, false);
                uiManager.DestroyUI(player, true);
            }

            SaveStoredData();
        }

        #endregion

        #region Chat/Console Commands

        [ChatCommand("xp")]
        private void CommandXp(BasePlayer player, string command, string[] args)
        {
            MessagePlayer(Lang("XP_ABOUT", player).Replace("{Version}", Version.ToString()), player);
        }

        [ChatCommand("level")]
        private void CommandLevel(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 1)
            {
                int level = -1;
				bool isNumeric = int.TryParse(args[0], out level);
                var message = new StringBuilder();
				
				if (!isNumeric)
				{
					return;
				}

                foreach (ItemData item in configs.options.itemDatas)
                {
                    if (item.unlockLevel == level)
                    {
                        if (message.Length == 0)
                        {
                            message.Append(Lang("XP_LEVEL_ITEMS", player).Replace("{Level}", level.ToString()));
                        }

                        var learned = player.blueprints.IsUnlocked(ItemManager.CreateByPartialName(item.shortName).info);
                        message.Append($"\n<color={(learned ? configs.display.colourLearned : configs.display.colourNotLearned)}> - {GetItemDefinition(item.shortName).displayName.english} ({item.costXP} XP)</color>");
                    }
                }


                if (message.Length != 0)
                {
                    MessagePlayer(message.ToString(), player);
                    return;
                }

                MessagePlayer(Lang("XP_LEVEL_NONE", player).Replace("{Level}", level.ToString()), player);
            }
        }

        [ChatCommand("learn")]
        private void CommandLearn(BasePlayer player, string command, string[] args)
        {
            PlayerData playerData = GetPlayerData(player.userID);
            string givenName = String.Join(" ", args);

            if (givenName.Trim().Length == 0 || givenName.Trim().ToLower() == "help")
            {
                MessagePlayer(Lang("XP_LEARN_USAGE", player), player);
                return;
            }
            
            ItemData itemData = null;
            ItemDefinition itemDefinition = null;

            foreach (ItemData item in configs.options.itemDatas)
            {
                if (!string.Equals(item.shortName, givenName, StringComparison.CurrentCultureIgnoreCase) &&
                    !string.Equals(item.englishName, givenName, StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

                itemData = item;
                itemDefinition = GetItemDefinition(item.shortName);

                if (item.unlockLevel > playerData.level)
                {
                    MessagePlayer(Lang("XP_LEARN_MIN_LEVEL", player).Replace("{Level}", item.unlockLevel.ToString()).Replace("{ItemName}", itemDefinition.displayName.english), player);
                    return;
                }

                continue;
            }

            if (itemData == null || itemDefinition == null)
            {
                MessagePlayer(Lang("XP_LEARN_UNKNOWN", player).Replace("{ItemName}", givenName), player);
                return;
            }

            if (player.blueprints.IsUnlocked(itemDefinition))
            {
                MessagePlayer(Lang("XP_LEARN_KNOWN", player).Replace("{ItemName}", givenName), player);
                return;
            }

            if (itemData.costXP > playerData.xp)
            {
                MessagePlayer(Lang("XP_LEARN_NEEDS_XP", player).Replace("{Cost}", itemData.costXP.ToString()).Replace("{ItemName}", givenName), player);
                return;
            }

            playerData.xp -= itemData.costXP;
            Effect.server.Run(configs.options.learnPrefab, player.transform.position);
            player.blueprints.Unlock(itemDefinition);

            MessagePlayer(Lang("XP_LEARN_SUCESS", player).Replace("{ItemName}", itemDefinition.displayName.english), player);
            player.SendConsoleCommand("gametip.hidegametip");
            player.SendConsoleCommand("gametip.showgametip", $"You learned {itemDefinition.displayName.english}");

            timer.Once(2.2f, () =>
            {
                player.SendConsoleCommand("gametip.hidegametip");
            });

            UpdatePlayerUi(player, false);
        }

        #endregion

        #region XpRevived

        private PlayerData GetPlayerData(ulong playerID)
        {
            PlayerData playerdata;

            if (!storedData.playerData.TryGetValue(playerID, out playerdata))
            {
                playerdata = new PlayerData();
                playerdata.level = 1.0f;
                playerdata.xp = 0.0f;
                storedData.playerData[playerID] = playerdata;
            }

            return playerdata;
        }

        public void IncreaseXp(ulong playerID, float amount, bool updateAFK = true)
        {
            IncreaseXp(GetPlayerFromId(playerID, false), amount, updateAFK);
        }

        public void IncreaseXp(BasePlayer player, float amount, bool updateAFK = true)
        {
            PlayerData playerData = GetPlayerData(player.userID);

            float oldLevel = playerData.level;
            float oldXP = playerData.xp;

            float levelAmount = amount;

            if (playerData.level >= configs.options.levelPivot)
            {
                levelAmount *= configs.options.levelPivot / (playerData.level);
            }

            playerData.level += levelAmount;
            playerData.xp += configs.options.levelXpRatio * amount;

            if (playerData.level < 0.0f)
            {
                playerData.level = 0.0f;
            }

            if (playerData.xp < 0.0f)
            {
                playerData.xp = 0.0f;
            }

            if (Math.Floor(oldLevel) != Math.Floor(playerData.level))
            {
                playerData.xp += 2.0f;

                timer.In(2.2f, () =>
                {
                    UnlockLevel(player, (int)Math.Floor(playerData.level));
                });
            }

            if (Math.Floor(oldXP) != Math.Floor(playerData.xp))
            {
                player.SendConsoleCommand("gametip.hidegametip");
                player.SendConsoleCommand("gametip.showgametip", "You Gained 1 XP");

                timer.Once(2.2f, () =>
                {
                    player.SendConsoleCommand("gametip.hidegametip");

                    if (playerData.level < 5.0f)
                    {
                        player.SendConsoleCommand("gametip.showgametip", "Remember to spend XP using /learn");
                    }
                });

                if (playerData.level < 5.0f)
                {
                    timer.Once(4.8f, () =>
                    {
                        player.SendConsoleCommand("gametip.hidegametip");
                    });
                }
            }

            if (BasePlayer.activePlayerList.Contains(player))
            {
                UpdatePlayerUi(player, false);
            }
        }

        public void UnlockLevel(BasePlayer player, int level)
        {
            Effect.server.Run(configs.options.levelUpPrefab, player.transform.position);
            float timerOffset = 2.2f;

            player.SendConsoleCommand("gametip.hidegametip");
            player.SendConsoleCommand("gametip.showgametip", "Welcome to Level " + level);

            foreach (ItemData item in configs.options.itemDatas)
            {
                if (item.unlockLevel == level)
                {
                    ItemDefinition itemDefinition = GetItemDefinition(item.shortName);

                    timer.Once(timerOffset, () =>
                    {
                        player.SendConsoleCommand("gametip.hidegametip");
                        player.SendConsoleCommand("gametip.showgametip", "Unlocked " + itemDefinition.displayName.english);
                    });

                    timerOffset += 1.8f;
                }
            }

            timer.Once(timerOffset, () =>
            {
                player.SendConsoleCommand("gametip.hidegametip");
            });
        }

        private bool IsUnlockable(string shortName, int level)
        {
            foreach (ItemData item in configs.options.itemDatas)
            {
                if (item.shortName == shortName && item.unlockLevel <= level)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResetConfigXpLevels()
        {
            PrintWarning("Resetting xp levels!");
            configs.options.itemDatas.Clear();

            foreach (ItemDefinition itemDefinition in ItemManager.itemList)
            {
                if (itemDefinition.Blueprint != null && itemDefinition.Blueprint.isResearchable && !itemDefinition.Blueprint.defaultBlueprint && !blockedBps.Contains(itemDefinition.shortname))
                {
                    float score = 0.0f;

                    Rust.Rarity rarity = itemDefinition.Blueprint.rarity;
                    score += 1.19f * (float)rarity;
                    int workbench = itemDefinition.Blueprint.workbenchLevelRequired;
                    score += 6.34f * (float)workbench;
                    int ingredients = itemDefinition.Blueprint.ingredients.Count;
                    score += 0.13f * (float)ingredients;

                    if (score > 14.0f)
                    {
                        score += (float)Math.Pow(1.25f, score - 14.0f) - 1.0f;
                    }

                    ItemData itemData = new ItemData();
                    itemData.shortName = itemDefinition.shortname;
                    itemData.englishName = itemDefinition.displayName.english;
                    itemData.unlockLevel = (int)Math.Floor(score);
                    itemData.costXP = (int)(0.7f * itemData.unlockLevel + 1.0f);
                    configs.options.itemDatas.Add(itemData);
                }
            }

            Config.WriteObject(configs, true);
        }

        private void InfiniteComponents(BasePlayer player, bool removeComponents, bool giveComponents)
        {
            if (!configs.options.unlimitedComponents)
            {
                return;
            }

            if (configs.options.blockedItems == null)
            {
                return;
            }

            if (removeComponents && player.inventory.containerMain.capacity > 24)
            {
                if (player?.inventory?.containerMain == null)
                {
                    return;
                }

                var retainedMainContainer = player.inventory.containerMain.uid;

                foreach (Item item in player.inventory.containerMain.itemList.ToList())
                {
                    if (configs.options.blockedItems.Contains(item?.info.shortname))
                    {
                        item.RemoveFromContainer();
                    }
                }

                ItemManager.DoRemoves();
                player.inventory.containerMain.capacity = 24;
            }

            if (giveComponents)
            {
                player.inventory.containerMain.capacity = 24 + configs.options.blockedItems.Count;

                NextFrame(() =>
                {
                    int hiddenSlotNumber = 0;

                    foreach (string itemName in configs.options.blockedItems)
                    {
                        Item item = ItemManager.CreateByName(itemName, 99999);
                        item.MoveToContainer(player.inventory.containerMain, 24 + hiddenSlotNumber, false);
                        item.LockUnlock(true, player);
                        hiddenSlotNumber++;
                    }
                });
            }
        }

        private void AssignLoot(LootContainer container)
        {
            if (configs.options.blockedItems == null)
            {
                return;
            }

            if (container == null || container.inventory == null)
            {
                return;
            }

            foreach (var item in new List<Item>(container.inventory.itemList))
            {
                if (configs.options.blockedItems.Contains(item.info.shortname))
                {
                    item.RemoveFromContainer();
                }
            }

            container.inventory.dirty = true;
        }

        #endregion

        #region UIs

        private string colourText = UI.Color("#ffffff", 0.8f);
        private string colourBackground = UI.Color("#b3b3b3", 0.05f);
        private string colourProgressLevel = UI.Color("#CD7C41", 1.0f);
        private string colourProgressXp = UI.Color("#95BB42", 1.0f);

        private const string uiPrefix = "XP_";
        private const string uiPanel = uiPrefix + "Panel";
        private const string uiLevel = uiPrefix + "Level";
        private const string uiXp = uiPrefix + "Xp";

        private void UpdatePlayerUi(BasePlayer player, bool refreshAll = true)
        {
            PlayerData playerData = GetPlayerData(player.userID);
            float playerLevel = playerData.level;
            float playerXp = playerData.xp;

            if (refreshAll)
            {
                // Element Panel.
                CuiHelper.DestroyUi(player, uiPanel);
                uiManager.RemoveContainer(uiPanel);

                var elementPanel = UI.CreateElementContainer(uiPanel, colourBackground,
                    (0.01f + configs.display.offsetX) + " " + (0.025f + configs.display.offsetY),
                    (0.13f + configs.display.offsetX) + " " + (0.1 + configs.display.offsetY));

                UI.LoadImage(ref elementPanel, uiPanel, configs.display.levelIcon, "0.026 0.65", "0.091 0.89");
                UI.LoadImage(ref elementPanel, uiPanel, configs.display.xpIcon, "0.026 0.10", "0.091 0.36");

                uiManager.AddContainer(uiPanel);
                CuiHelper.AddUi(player, elementPanel);
            }

            // Element Level.
            CuiHelper.DestroyUi(player, uiLevel);
            uiManager.RemoveContainer(uiLevel);

            var elementLevel = UI.CreateElementContainer(uiLevel, colourBackground,
                (0.01f + configs.display.offsetX) + " " + (0.065f + configs.display.offsetY),
                (0.13f + configs.display.offsetX) + " " + (0.1 + configs.display.offsetY));

            UI.CreatePanel(ref elementLevel, uiLevel, colourProgressLevel, "0.12 0.13",
                (playerLevel - (float)Math.Floor(playerLevel)) + " 0.87");
            UI.CreateText(ref elementLevel, uiLevel, colourText, "" + (int)Math.Floor(playerLevel), 14, "0.16 0.0", "1.0 1.0", TextAnchor.MiddleLeft);

            uiManager.AddContainer(uiLevel);
            CuiHelper.AddUi(player, elementLevel);

            // Element Xp.
            CuiHelper.DestroyUi(player, uiXp);
            uiManager.RemoveContainer(uiXp);

            var elementXp = UI.CreateElementContainer(uiXp, colourBackground,
                (0.01f + configs.display.offsetX) + " " + (0.025f + configs.display.offsetY),
                (0.13f + configs.display.offsetX) + " " + (0.06 + configs.display.offsetY));

            UI.CreatePanel(ref elementXp, uiXp, colourProgressXp, "0.12 0.13",
                (playerXp - (float)Math.Floor(playerXp)) + " 0.87");
            UI.CreateText(ref elementXp, uiXp, colourText, "" + (int)Math.Floor(playerXp), 14, "0.16 0.0", "1.0 1.0", TextAnchor.MiddleLeft);

            uiManager.AddContainer(uiXp);
            CuiHelper.AddUi(player, elementXp);
        }

        #endregion

        #region Helpers

        private string Lang(string key, BasePlayer player)
        {
            var userString = player == null ? "null" : player.UserIDString;
            return lang.GetMessage(key, this, userString);
        }

        private void MessagePlayer(string message, BasePlayer player)
        {
            player.ChatMessage(message);
        }

        private BasePlayer GetPlayerFromId(ulong id, bool canBeOffline)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.userID == id)
                {
                    return player;
                }
            }

            if (canBeOffline)
            {
                foreach (var player in BasePlayer.sleepingPlayerList)
                {
                    if (player.userID == id)
                    {
                        return player;
                    }
                }
            }

            return null;
        }

        private BasePlayer FindPlayer(string partialName, bool canBeOffline)
        {
            if (string.IsNullOrEmpty(partialName))
            {
                return null;
            }

            var players = new HashSet<BasePlayer>();

            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString.Equals(partialName))
                {
                    players.Add(activePlayer);
                }
                else if (!string.IsNullOrEmpty(activePlayer.displayName) && activePlayer.displayName.Contains(partialName, CompareOptions.IgnoreCase))
                {
                    players.Add(activePlayer);
                }
                else if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress.Equals(partialName))
                {
                    players.Add(activePlayer);
                }
            }

            if (canBeOffline)
            {
                foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
                {
                    if (sleepingPlayer.UserIDString.Equals(partialName))
                    {
                        players.Add(sleepingPlayer);
                    }
                    else if (!string.IsNullOrEmpty(sleepingPlayer.displayName) && sleepingPlayer.displayName.Contains(partialName, CompareOptions.IgnoreCase))
                    {
                        players.Add(sleepingPlayer);
                    }
                }
            }

            if (players.Count <= 0)
            {
                return null;
            }

            return players.First();
        }

        public ItemDefinition GetItemDefinition(string shortName)
        {
            if (string.IsNullOrEmpty(shortName) || shortName == "")
            {
                return null;
            }

            return ItemManager.FindItemDefinition(shortName.ToLower());
        }

        #endregion
    }
}
