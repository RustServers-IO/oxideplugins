// Reference: Rust.Workshop
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
namespace Oxide.Plugins
{
    [Info("MagicLoot", "Norn", "0.1.6", ResourceId = 2212)]
    [Description("Basic loot multiplier.")]

    class MagicLoot : RustPlugin
    {
        int VANILLA_MULTIPLIER = 1;
        int MAX_LOOT_CONTAINER_SLOTS = 18;
        bool INIT = false;
        Configuration exclude = new Configuration();
        MLData LootData = new MLData();

        class MLData
        {
            public Dictionary<string, int> ItemList = new Dictionary<string, int>();
            public MLData()
            {
            }
        }

        public class Configuration
        {
            public List<String> list;
            public Configuration() { list = new List<String>(); }
        }
        void Loaded()
        {
            if (Config["Developer", "ExtraItem"] == null) { Config["Developer", "ExtraItem"] = false; Config["Developer", "AmountChange"] = false; Config["Developer", "Skins"] = false; Config["Loot", "PreventDuplicates"] = false;  Config["Loot", "WorkshopSkins"] = true; SaveConfig(); Puts("Updating configuration..."); }
            try { exclude = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(Config["exclude"]).ToString()); } catch { }
            LoadMagicLootData();
        }
        void Unload() { }
        void SaveMagicLootData() { Interface.GetMod().DataFileSystem.WriteObject(this.Title, LootData); }
        void LoadMagicLootData()
        {
            LootData = Interface.GetMod().DataFileSystem.ReadObject<MLData>(this.Title);
            if (LootData.ItemList.Count == 0) { Puts("Generating item list with limits..."); foreach (var item in ItemManager.itemList) { LootData.ItemList.Add(item.shortname, item.stackable); } SaveMagicLootData(); }
            Puts("Loaded " + LootData.ItemList.Count + " item limits from /data/" + this.Title + ".json");
        }
        void OnServerInitialized()
        {
            INIT = true; // Server has fully loaded.
            Puts("Loaded at x" + Config["Settings", "Multiplier"].ToString() + " vanilla rate [Extra Loot: " + Config["Loot", "Enabled"].ToString() + "]");
            RefreshLootContainers();
        }
        private readonly Dictionary<string, List<ulong>> skinsCache = new Dictionary<string, List<ulong>>();
        private List<ulong> GetSkins(ItemDefinition def)
        {
            List<ulong> skins;
            if (skinsCache.TryGetValue(def.shortname, out skins)) return skins;
            skins = new List<ulong> { 0 };
            skins.AddRange(ItemSkinDirectory.ForItem(def).Select(skin => (ulong)skin.id));
            skins.AddRange(Rust.Workshop.Approved.All.Where(skin => skin.ItemType.ItemName == def.shortname).Select(skin => skin.WorkshopdId));
            skinsCache.Add(def.shortname, skins);
            return skins;
        }
        List<ItemDefinition> ItemList = new List<ItemDefinition>();
        Dictionary<Rarity, List<ItemDefinition>> RarityList = new Dictionary<Rarity, List<ItemDefinition>>();
        void GenerateRarityList()
        {
            RarityList.Add(Rarity.Common, new List<ItemDefinition>(ItemManager.itemList.Where(z => z.rarity == Rarity.Common).Select(z => z)));
            Puts("Added " + RarityList[Rarity.Common].Count.ToString() + " items to Common list.");
            RarityList.Add(Rarity.Rare, new List<ItemDefinition>(ItemManager.itemList.Where(z => z.rarity == Rarity.Rare).Select(z => z)));
            Puts("Added " + RarityList[Rarity.Rare].Count.ToString() + " items to Rare list.");
            RarityList.Add(Rarity.Uncommon, new List<ItemDefinition>(ItemManager.itemList.Where(z => z.rarity == Rarity.Uncommon).Select(z => z)));
            Puts("Added " + RarityList[Rarity.Uncommon].Count.ToString() + " items to Uncommon list.");
            RarityList.Add(Rarity.VeryRare, new List<ItemDefinition>(ItemManager.itemList.Where(z => z.rarity == Rarity.VeryRare).Select(z => z)));
            Puts("Added " + RarityList[Rarity.VeryRare].Count.ToString() + " items to Very Rare list.");
        }
        protected override void LoadDefaultConfig()
        {
            // -- [ RESET ] ---
            Config.Clear();
            Puts("No configuration file found, generating...");
            // -- [ SETTINGS ] ---
            Config["Settings", "Multiplier"] = VANILLA_MULTIPLIER;

            Config["Loot", "Enabled"] = true;
            Config["Loot", "ItemsMin"] = 1;
            Config["Loot", "ItemsMax"] = 3;
            Config["Loot", "AmountMin"] = 1;
            Config["Loot", "PreventDuplicates"] = false;
            Config["Loot", "WorkshopSkins"] = true;

            Config["Developer", "Debug"] = false;
            Config["Developer", "Skins"] = false;
            Config["Developer", "AmountChange"] = false;
            Config["Developer", "ExtraItem"] = false;

            exclude.list.Add("supply.signal");
            exclude.list.Add("ammo.rocket.smoke");
            Config["exclude"] = exclude;
        }
        private IEnumerable<int> CalculateStacks(int amount, ItemDefinition item)
        {
            var results = Enumerable.Repeat(item.stackable, amount / item.stackable); if (amount % item.stackable > 0) { results = results.Concat(Enumerable.Repeat(amount % item.stackable, 1)); }
            return results;
        }
        private void RefreshLootContainers()
        {
            int count = 0;
            foreach (var container in UnityEngine.Object.FindObjectsOfType<LootContainer>())
            {
                RepopulateContainer(container);
                count++;
            }
            Puts("Repopulated " + count.ToString() + " loot containers.");
        }
        void RepopulateContainer(LootContainer container)
        {
            if (container != null)
            {
                ClearContainer(container);
                container.PopulateLoot();
                ModifyContainerContents(container);
            }
        }
        void ClearContainer(LootContainer container)
        {
            while (container.inventory.itemList.Count > 0)
            {
                var item = container.inventory.itemList[0];
                item.RemoveFromContainer();
                item.Remove(0f);
            }
        }
        void ModifyContainerContents(BaseNetworkable entity)
        {
            var e = entity as LootContainer; if (e == null) return;
            List<Rarity> RaritiesUsed = new List<Rarity>();
            foreach (Item lootitem in e.inventory.itemList.ToList())
            {
                if (exclude.list.Contains(lootitem.info.shortname)) { lootitem.RemoveFromContainer(); e.inventory.itemList.Remove(lootitem); break; }
                if (Convert.ToInt16(Config["Settings", "Multiplier"]) != VANILLA_MULTIPLIER)
                {
                    var skins = GetSkins(ItemManager.FindItemDefinition(lootitem.info.itemid));
                    if (skins.Count > 1 && Convert.ToBoolean(Config["Loot", "WorkshopSkins"])) // If workshop skins enabled, randomise skin
                    {
                        lootitem.skin = skins.GetRandom(); if (lootitem.info.category == ItemCategory.Weapon) { lootitem.GetHeldEntity().skinID = lootitem.skin; }
                        if (Convert.ToBoolean(Config["Developer", "Debug"]) && Convert.ToBoolean(Config["Developer", "Skins"]))
                        { string debugs = "[" + lootitem.info.displayName.english + "] Skin has been modified to: " + lootitem.skin; Puts(debugs); PrintToChat(debugs); }
                    }
                    if (lootitem.info.stackable > 1) // Detect whether to change Amounts
                    {
                        if (Convert.ToBoolean(Config["Developer", "Debug"]) && Convert.ToBoolean(Config["Developer", "AmountChange"]))
                        { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=yellow>" + lootitem.info.displayName.english + " : original amount: " + lootitem.amount.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }

                        int limit = 0;
                        int ac = lootitem.amount * Convert.ToUInt16(Config["Settings", "Multiplier"]);
                        if (LootData.ItemList.TryGetValue(lootitem.info.shortname, out limit)) { lootitem.amount = Math.Min(ac, Math.Min(limit, lootitem.info.stackable)); }

                        if (Convert.ToBoolean(Config["Developer", "Debug"]) && Convert.ToBoolean(Config["Developer", "AmountChange"]))
                        { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=white>" + lootitem.info.displayName.english + " : new amount: " + lootitem.amount.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }
                    }
                }
                if (lootitem.info.rarity != Rarity.None && !RaritiesUsed.Contains(lootitem.info.rarity)) { RaritiesUsed.Add(lootitem.info.rarity); }
            }
            if (Convert.ToBoolean(Config["Loot", "Enabled"])) // Extra Loot Items
            {
                if (RarityList.Count == 0) { GenerateRarityList(); }
                if (RaritiesUsed.Count >= 1 && RaritiesUsed != null)
                {
                    Rarity rarity = RaritiesUsed.GetRandom();
                    ItemDefinition item;
                    int itemstogive = UnityEngine.Random.Range(Convert.ToInt16(Config["Loot", "ItemsMin"]), Convert.ToInt16(Config["Loot", "ItemsMax"]));
                    e.inventory.capacity = MAX_LOOT_CONTAINER_SLOTS;
                    e.inventorySlots = MAX_LOOT_CONTAINER_SLOTS;
                    for (int i = 1; i <= itemstogive; i++)
                    {
                        item = RarityList[rarity].GetRandom();
                        if (e.inventory.FindItemsByItemID(item.itemid).Count >= 1 && item.stackable == 1 && Convert.ToBoolean(Config["Loot", "PreventDuplicates"]))
                        { break; }
                        if (item != null)
                        {
                            if (exclude.list.Contains(item.shortname)) { break; }
                            int limit = 0; int amounttogive = 0;
                            if (LootData.ItemList.TryGetValue(item.shortname, out limit) && item.stackable > 1) { amounttogive = UnityEngine.Random.Range(Convert.ToInt16(Config["Loot", "AmountMin"]), Math.Min(limit, item.stackable)); } else { amounttogive = item.stackable; }
                            var skins = GetSkins(item);
                            if (skins.Count > 1 && Convert.ToBoolean(Config["Loot", "WorkshopSkins"]))
                            { Item skinned = ItemManager.CreateByItemID(item.itemid, amounttogive, skins.GetRandom()); skinned.MoveToContainer(e.inventory, -1, false); }
                            else
                            { e.inventory.AddItem(item, amounttogive); }
                            if (Convert.ToBoolean(Config["Developer", "Debug"]) && Convert.ToBoolean(Config["Developer", "ExtraItem"]))
                            { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=white>Extra Item: " + item.displayName.english + " : amount: " + amounttogive.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }
                        }
                    }
                }
            }
            int fcapacity = e.inventory.itemList.Count();
            e.inventory.capacity = fcapacity;
            e.inventorySlots = fcapacity;
        }
        void OnEntitySpawned(BaseNetworkable entity) { if (INIT) { ModifyContainerContents(entity); } }
    }
}