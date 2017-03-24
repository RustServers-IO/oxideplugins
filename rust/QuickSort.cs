/*
TODO:
- Add component sorting to GUI
- Add command for player preferences
- Add options to enable/disable each container type (wear, bar, main)
- Fix players being able to teleport/move and exploit loot containers
*/

using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("QuickSort", "Wulf/lukespragg", "1.1.0", ResourceId = 1263)]
    [Description("Adds a GUI that allows players to quickly sort items into containers")]

    class QuickSort : CovalencePlugin
    {
        #region Initialization

        static readonly Dictionary<ulong, string> guiInfo = new Dictionary<ulong, string>();

        const string permLootAll = "quicksort.lootall";
        const string permUse = "quicksort.use";

        int lootAllDelay;
        string uiStyle;

        protected override void LoadDefaultConfig()
        {
            // Settings
            Config["Loot All Delay (Seconds, 0 to Disable)"] = lootAllDelay = GetConfig("Loot All Delay (Seconds, 0 to Disable)", 0);
            Config["UI Style (center, lite, right)"] = uiStyle = GetConfig("UI Style (center, lite, right)", "right");

            // Cleanup
            Config.Remove("LootAllowed");
            Config.Remove("LootDelay");

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();

            permission.RegisterPermission(permLootAll, this);
            permission.RegisterPermission(permUse, this);
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Deposit"] = "Deposit",
                ["DepositAll"] = "All",
                ["DepositAmmo"] = "Ammo",
                ["DepositAttire"] = "Attire",
                ["DepositConstruction"] = "Construction",
                ["DepositExisting"] = "Existing",
                ["DepositFood"] = "Food",
                ["DepositItems"] = "Deployables",
                ["DepositMedical"] = "Medical",
                ["DepositResources"] = "Resources",
                ["DepositTools"] = "Tools",
                ["DepositTraps"] = "Traps",
                ["DepositWeapons"] = "Weapons",
                ["LootAll"] = "Loot All"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Deposit"] = "Facilités",
                ["DepositAll"] = "Tout",
                ["DepositAmmo"] = "Munitions",
                ["DepositAttire"] = "Vêtements",
                ["DepositConstruction"] = "Construction",
                ["DepositExisting"] = "Existants",
                ["DepositFood"] = "Nourritures",
                ["DepositItems"] = "Deployables",
                ["DepositMedical"] = "Medical",
                ["DepositResources"] = "Resources",
                ["DepositTools"] = "Outils",
                ["DepositTraps"] = "Pièges",
                ["DepositWeapons"] = "Armes",
                ["LootAll"] = "Prendre Tout"
            }, this, "fr");

            // German
            /*lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Deposit"] = "",
                ["DepositAll"] = "",
                ["DepositAmmo"] = "",
                ["DepositAttire"] = "",
                ["DepositConstruction"] = "",
                ["DepositExisting"] = "",
                ["DepositFood"] = "",
                ["DepositItems"] = "",
                ["DepositMedical"] = "",
                ["DepositResources"] = "",
                ["DepositTools"] = "",
                ["DepositTraps"] = "",
                ["DepositWeapons"] = "",
                ["LootAll"] = ""
            }, this, "de");*/

            // Russian
            /*lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Deposit"] = "",
                ["DepositAll"] = "",
                ["DepositAmmo"] = "",
                ["DepositAttire"] = "",
                ["DepositConstruction"] = "",
                ["DepositExisting"] = "",
                ["DepositFood"] = "",
                ["DepositItems"] = "",
                ["DepositMedical"] = "",
                ["DepositResources"] = "",
                ["DepositTools"] = "",
                ["DepositTraps"] = "",
                ["DepositWeapons"] = "",
                ["LootAll"] = ""
            }, this, "ru");*/

            // Spanish
            /*lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Deposit"] = "",
                ["DepositAll"] = "",
                ["DepositAmmo"] = "",
                ["DepositAttire"] = "",
                ["DepositConstruction"] = "",
                ["DepositExisting"] = "",
                ["DepositFood"] = "",
                ["DepositItems"] = "",
                ["DepositMedical"] = "",
                ["DepositResources"] = "",
                ["DepositTools"] = "",
                ["DepositTraps"] = "",
                ["DepositWeapons"] = "",
                ["LootAll"] = ""
            }, this, "es");*/
        }

        #endregion

        #region Game Hooks

        void OnLootPlayer(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse)) UserInterface(player);
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse) && !(entity is VendingMachine) && !(entity is ShopFront)) UserInterface(player);
        }

        #endregion

        #region Console Commands

        [Command("quicksort")]
        void SortCommand(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(permUse)) SortItems(player.Object as BasePlayer, args);
        }

        [Command("quicksort.lootall")]
        void LootAllCommand(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(permLootAll)) timer.Once(lootAllDelay, () => AutoLoot(player.Object as BasePlayer));
        }

        [Command("quicksort.lootdelay")]
        void LootDelayCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            int x;
            if (!int.TryParse(args[0], out x)) return;

            lootAllDelay = x;
            Config["Loot All Delay (Seconds)"] = lootAllDelay;
            SaveConfig();
        }

        #endregion

        #region Loot Handling

        void AutoLoot(BasePlayer player)
        {
            var container = GetLootedInventory(player);
            var playerMain = player.inventory.containerMain;
            if (container == null || playerMain == null || container.playerOwner != null) return;

            var itemsSelected = CloneItemList(container.itemList);
            itemsSelected.Sort((item1, item2) => item2.info.itemid.CompareTo(item1.info.itemid));
            MoveItems(itemsSelected, playerMain);
        }

        void SortItems(BasePlayer player, string[] args)
        {
            var container = GetLootedInventory(player);
            var playerMain = player.inventory.containerMain;
            //var playerWear = player.inventory.containerWear;
            //var playerBelt = player.inventory.containerBelt;
            if (container == null || playerMain == null) return;

            List<Item> itemsSelected;
            if (args.Length == 1)
            {
                if (args[0].Equals("existing"))
                {
                    itemsSelected = GetExistingItems(playerMain, container);
                }
                else
                {
                    var category = StringToItemCategory(args[0]);
                    itemsSelected = GetItemsOfType(playerMain, category);
                    //itemsSelected.AddRange(GetItemsOfType(playerWear, category));
                    //itemsSelected.AddRange(GetItemsOfType(playerBelt, category));
                }
            }
            else
            {
                itemsSelected = CloneItemList(playerMain.itemList);
                //itemsSelected.AddRange(CloneItemList(playerWear.itemList));
                //itemsSelected.AddRange(CloneItemList(playerBelt.itemList));
            }

            var uselessItems = GetUselessItems(itemsSelected, container);
            foreach (var item in uselessItems) itemsSelected.Remove(item);

            itemsSelected.Sort((item1, item2) => item2.info.itemid.CompareTo(item1.info.itemid));
            MoveItems(itemsSelected, container);
        }

        #endregion

        #region Item Helpers

        static float CookingTemperature(BaseOven oven)
        {
            switch (oven.temperature)
            {
                case BaseOven.TemperatureType.Warming:
                    return 50f;
                case BaseOven.TemperatureType.Cooking:
                    return 200f;
                case BaseOven.TemperatureType.Smelting:
                    return 1000f;
                case BaseOven.TemperatureType.Fractioning:
                    return 1500f;
                case BaseOven.TemperatureType.Normal:
                    return 15f;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        IEnumerable<Item> GetUselessItems(IEnumerable<Item> items, ItemContainer container)
        {
            var uselessItems = new List<Item>();
            var furnace = container.entityOwner.GetComponent<BaseOven>();
            if (furnace == null) return uselessItems;

            foreach (var item in items)
            {
                var cookable = item.info.GetComponent<ItemModCookable>();
                if (item.info.GetComponent<ItemModBurnable>() == null && (cookable == null
                    || cookable.lowTemp > CookingTemperature(furnace) || cookable.highTemp < CookingTemperature(furnace))) uselessItems.Add(item);
            }

            return uselessItems;
        }

        List<Item> CloneItemList(IEnumerable<Item> list)
        {
            var clone = new List<Item>();
            foreach (var item in list) clone.Add(item);

            return clone;
        }

        List<Item> GetExistingItems(ItemContainer primary, ItemContainer secondary)
        {
            var existingItems = new List<Item>();
            if (primary == null || secondary == null) return existingItems;

            foreach (var t in primary.itemList)
            {
                foreach (var t1 in secondary.itemList)
                {
                    if (t.info.itemid != t1.info.itemid) continue;
                    existingItems.Add(t);
                    break;
                }
            }

            return existingItems;
        }

        List<Item> GetItemsOfType(ItemContainer container, ItemCategory category)
        {
            var items = new List<Item>();
            foreach (var item in container.itemList)
                if (item.info.category == category) items.Add(item);

            return items;
        }

        ItemContainer GetLootedInventory(BasePlayer player)
        {
            var playerLoot = player.inventory.loot;
            return playerLoot != null && playerLoot.IsLooting() ? playerLoot.containers[0] : null;
        }

        void MoveItems(IEnumerable<Item> items, ItemContainer to)
        {
            foreach (var item in items) item.MoveToContainer(to);
        }

        ItemCategory StringToItemCategory(string categoryName)
        {
            var categoryNames = Enum.GetNames(typeof(ItemCategory));
            for (var i = 0; i < categoryNames.Length; i++)
                if (categoryName.ToLower().Equals(categoryNames[i].ToLower())) return (ItemCategory)i;

            return (ItemCategory)categoryNames.Length;
        }

        #endregion

        #region User Interface

        void UserInterface(BasePlayer player)
        {
            DestroyUi(player);
            player.inventory.loot.entitySource.gameObject.AddComponent<UIDestroyer>();
            guiInfo[player.userID] = CuiHelper.GetGuid();

            if (uiStyle.ToLower() == "center") UiCenter(player);
            if (uiStyle.ToLower() == "lite") UiLite(player);
            if (uiStyle.ToLower() == "right") UiRight(player);
        }

        #region UI Center

        void UiCenter(BasePlayer player)
        {
            var elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.5 0.5 0.33" },
                RectTransform = { AnchorMin = "0.354 0.625", AnchorMax = "0.633 0.816" }
            }, "Hud.Menu", guiInfo[player.userID]);
            elements.Add(new CuiLabel
            {
                Text = { Text = Lang("Deposit"), FontSize = 16, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.02 0.8", AnchorMax = "0.3 1" }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort existing", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.02 0.6", AnchorMax = "0.3 0.8" },
                Text = { Text = Lang("DepositExisting"), FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.3 0.55" },
                Text = { Text = Lang("DepositAll"), FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, panel);
            if (permission.UserHasPermission(player.UserIDString, permLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksort.lootall", Color = "0 0.7 0 0.5" },
                    RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.3 0.3" },
                    Text = { Text = Lang("LootAll"), FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, panel);
            }
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort weapon", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.8", AnchorMax = "0.63 0.94" },
                Text = { Text = Lang("DepositWeapons"), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort ammunition", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.6", AnchorMax = "0.63 0.75" },
                Text = { Text = Lang("DepositAmmo"), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort medical", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.41", AnchorMax = "0.63 0.555" },
                Text = { Text = Lang("DepositMedical"), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort attire", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.235", AnchorMax = "0.63 0.368" },
                Text = { Text = Lang("DepositAttire"), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort resources", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.05", AnchorMax = "0.63 0.19" },
                Text = { Text = Lang("DepositResources"), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort construction", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.8", AnchorMax = "0.95 0.94" },
                Text = { Text = Lang("DepositConstruction"), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort items", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.6", AnchorMax = "0.95 0.75" },
                Text = { Text = Lang("DepositItems"), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort tool", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.41", AnchorMax = "0.95 0.555" },
                Text = { Text = Lang("DepositTools"), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort food", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.235", AnchorMax = "0.95 0.368" },
                Text = { Text = Lang("DepositFood"), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort traps", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.05", AnchorMax = "0.95 0.19" },
                Text = { Text = Lang("DepositTraps"), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);

            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region UI Lite

        void UiLite(BasePlayer player)
        {
            var elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.0 0.0 0.0 0.0" },
                RectTransform = { AnchorMin = "0.677 0.769", AnchorMax = "0.963 0.96" }
            }, "Hud.Menu", guiInfo[player.userID]);

            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort existing", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "-0.88 -1.545", AnchorMax = "-0.63 -1.435" },
                Text = { Text = Lang("DepositExisting", player.UserIDString), FontSize = 13, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "-0.61 -1.545", AnchorMax = "-0.36 -1.435" },
                Text = { Text = Lang("DepositAll", player.UserIDString), FontSize = 13, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort.lootall", Color = "0 0.7 0 0.5" },
                RectTransform = { AnchorMin = "-0.34 -1.545", AnchorMax = "-0.13 -1.435" },
                Text = { Text = Lang("LootAll", player.UserIDString), FontSize = 13, Align = TextAnchor.MiddleCenter }
            }, panel);

            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region UI Right

        void UiRight(BasePlayer player)
        {
            var elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.5 0.5 0.33" },
                RectTransform = { AnchorMin = "0.677 0.769", AnchorMax = "0.963 0.96" }
            }, "Hud.Menu", guiInfo[player.userID]);
            elements.Add(new CuiLabel
            {
                Text = { Text = Lang("Deposit", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.02 0.8", AnchorMax = "0.3 1" }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort existing", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.02 0.6", AnchorMax = "0.3 0.8" },
                Text = { Text = Lang("DepositExisting", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.3 0.55" },
                Text = { Text = Lang("DepositAll", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, panel);
            if (permission.UserHasPermission(player.UserIDString, permLootAll))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "quicksort.lootall", Color = "0 0.7 0 0.5" },
                    RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.3 0.3" },
                    Text = { Text = Lang("LootAll", player.UserIDString), FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, panel);
            }
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort weapon", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.8", AnchorMax = "0.63 0.94" },
                Text = { Text = Lang("DepositWeapons", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort ammunition", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.6", AnchorMax = "0.63 0.75" },
                Text = { Text = Lang("DepositAmmo", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort medical", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.41", AnchorMax = "0.63 0.555" },
                Text = { Text = Lang("DepositMedical", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort attire", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.235", AnchorMax = "0.63 0.368" },
                Text = { Text = Lang("DepositAttire", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort resources", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.35 0.05", AnchorMax = "0.63 0.19" },
                Text = { Text = Lang("DepositResources", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort construction", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.8", AnchorMax = "0.95 0.94" },
                Text = { Text = Lang("DepositConstruction", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort items", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.6", AnchorMax = "0.95 0.75" },
                Text = { Text = Lang("DepositItems", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort tool", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.41", AnchorMax = "0.95 0.555" },
                Text = { Text = Lang("DepositTools", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort food", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.235", AnchorMax = "0.95 0.368" },
                Text = { Text = Lang("DepositFood", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "quicksort traps", Color = "1 0.5 0 0.5" },
                RectTransform = { AnchorMin = "0.67 0.05", AnchorMax = "0.95 0.19" },
                Text = { Text = Lang("DepositTraps", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, panel);

            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region Cleanup

        static void DestroyUi(BasePlayer player)
        {
            string gui;
            if (guiInfo.TryGetValue(player.userID, out gui)) CuiHelper.DestroyUi(player, gui);
        }

        class UIDestroyer : MonoBehaviour
        {
            void PlayerStoppedLooting(BasePlayer player)
            {
                DestroyUi(player);
                Destroy(this);
            }
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) DestroyUi(player);
        }

        #endregion

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
