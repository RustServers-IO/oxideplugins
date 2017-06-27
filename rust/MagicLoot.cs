using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Rust;

namespace Oxide.Plugins
{
    [Info("MagicLoot", "Norn", "0.1.13", ResourceId = 2212)]
    [Description("Basic loot multiplier.")]
    class MagicLoot : RustPlugin
    {
        int VANILLA_MULTIPLIER = 1;
        int MAX_LOOT_CONTAINER_SLOTS = 18;
        bool INIT = false;
        Configuration Exclude = new Configuration();
        Configuration ExcludeFromMultiplication = new Configuration();
        Configuration Components = new Configuration();
        MLData LootData = new MLData();
        List<ContainerToRefresh> refreshList = new List<ContainerToRefresh>();
        DateTime lastRefresh = DateTime.MinValue;
        int lastMinute;

        class MLData
        {
            public Dictionary<string, int> ItemList = new Dictionary<string, int>();
            public MLData()
            {
            }
        }
        class ContainerToRefresh
        {
            public LootContainer container;
            public DateTime time;
        }
        public class Configuration
        {
            public List<String> list;
            public Configuration() { list = new List<String>(); }
        }

        void SaveMagicLootData() { Interface.Oxide.DataFileSystem.WriteObject(this.Title, LootData); }
        void LoadMagicLootData()
        {
            int newitems = 0;
            LootData = Interface.Oxide.DataFileSystem.ReadObject<MLData>(this.Title);
            if (LootData.ItemList.Count == 0) { Puts("Generating item list with limits..."); { foreach (var item in ItemManager.itemList) { LootData.ItemList.Add(item.shortname, item.stackable); } SaveMagicLootData(); } }
            foreach (var item in ItemManager.itemList) { if (!LootData.ItemList.ContainsKey(item.shortname)) { LootData.ItemList.Add(item.shortname, item.stackable); newitems++; } }
            if (newitems != 0) { Puts("Added " + newitems.ToString() + " new items to /data/" + this.Title + ".json"); SaveMagicLootData(); }
            Puts("Loaded " + LootData.ItemList.Count + " item limits from /data/" + this.Title + ".json");
            Puts("Loaded " + Components.list.Count + " components from /config/" + this.Title + ".json");
        }
        void OnServerInitialized()
        {
            INIT = true; // Server has fully loaded.
            lastMinute = DateTime.UtcNow.Minute;
            if (Config["Loot", "RefreshMinutes"] == null) { Config["Loot", "RefreshMinutes"] = 25; SaveConfig(); }
            if(Config["Settings", "RefreshMessage"] == null) { Config["Settings", "RefreshMessage"] = true;  SaveConfig(); }
            try { Exclude = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(Config["Exclude"]).ToString()); } catch { }
            try { Components = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(Config["Components"]).ToString()); } catch { }
            LoadMagicLootData();
            Puts("Loaded at x" + Config["Settings", "Multiplier"].ToString() + " vanilla rate | components rate x" + Config["Settings", "MultiplierComponents"].ToString() + "  [Extra Loot: " + Config["Loot", "Enabled"].ToString() + " | X Only Components: " + Config["Settings", "MultiplyOnlyComponents"].ToString() + "]");
            RefreshLootContainers();
        }
        private readonly Dictionary<string, List<ulong>> skinsCache = new Dictionary<string, List<ulong>>();
        private List<ulong> GetSkins(ItemDefinition def)
        {
            List<ulong> skins;
            if (skinsCache.TryGetValue(def.shortname, out skins)) return skins;
            skins = new List<ulong> { 0 };
            skins.AddRange(ItemSkinDirectory.ForItem(def).Select(skin => (ulong)skin.id));
            skins.AddRange(Rust.Workshop.Approved.All.Where(skin => skin.Skinnable.ItemName == def.shortname).Select(skin => skin.WorkshopdId));
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

            int itemsremoved = 0;
            foreach(var ra in RarityList[Rarity.Common].ToList()) { int limit = 0; if (LootData.ItemList.TryGetValue(ra.shortname, out limit)) { if (limit == 0) { RarityList[Rarity.Common].Remove(ra); itemsremoved++; } } }
            foreach(var ra in RarityList[Rarity.Rare].ToList()) { int limit = 0; if (LootData.ItemList.TryGetValue(ra.shortname, out limit)) { if (limit == 0) { RarityList[Rarity.Rare].Remove(ra); itemsremoved++; } } }
            foreach (var ra in RarityList[Rarity.Uncommon].ToList()) { int limit = 0; if (LootData.ItemList.TryGetValue(ra.shortname, out limit)) { if (limit == 0) { RarityList[Rarity.Uncommon].Remove(ra); itemsremoved++; } } }
            foreach (var ra in RarityList[Rarity.VeryRare].ToList()) { int limit = 0; if (LootData.ItemList.TryGetValue(ra.shortname, out limit)) { if (limit == 0) { RarityList[Rarity.VeryRare].Remove(ra); itemsremoved++; } } }
            if(itemsremoved != 0) { Puts("Removed " + itemsremoved.ToString() + " items from loot table. [ LIMIT = 0 ]"); }

        }
        protected override void LoadDefaultConfig()
        {
            // -- [ RESET ] ---
            Config.Clear();
            Puts("No configuration file found, generating...");
            // -- [ SETTINGS ] ---
            Config["Settings", "Multiplier"] = VANILLA_MULTIPLIER;
            Config["Settings", "MultiplierComponents"] = VANILLA_MULTIPLIER;

            Config["Settings", "MultiplyOnlyComponents"] = false;
            Config["Settings", "RefreshMessage"] = true;

            Config["Loot", "Enabled"] = true;
            Config["Loot", "ItemsMin"] = 1;
            Config["Loot", "ItemsMax"] = 3;
            Config["Loot", "AmountMin"] = 1;
            Config["Loot", "PreventDuplicates"] = false;
            Config["Loot", "WorkshopSkins"] = true;
            Config["Loot", "RefreshMinutes"] = 25;

            Config["Developer", "Debug"] = false;
            Config["Developer", "Skins"] = false;
            Config["Developer", "AmountChange"] = false;
            Config["Developer", "ExtraItem"] = false;


            Exclude.list.Add("supply.signal");
            Exclude.list.Add("ammo.rocket.smoke");
            Config["Exclude"] = Exclude;

            ExcludeFromMultiplication.list.Add("crude.oil");
            Config["ExcludeFromMultiplication"] = ExcludeFromMultiplication;

            foreach (ItemDefinition q in ItemManager.itemList.Where(p => p.category == ItemCategory.Component)) { Components.list.Add(q.shortname); }
            Puts("Added " + Components.list.Count.ToString() + " components to configuration file.");
            Config["Components"] = Components;
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
                refreshList.Add(new ContainerToRefresh() { container = container, time = DateTime.UtcNow.AddMinutes(Convert.ToInt32(Config["Loot", "RefreshMinutes"])) });
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
        void SuppressRefresh(LootContainer container)
        {
            container.minSecondsBetweenRefresh = -1;
            container.maxSecondsBetweenRefresh = 0;
            container.CancelInvoke("SpawnLoot");
        }
        void OnTick()
        {
            if (lastMinute == DateTime.UtcNow.Minute) return;
            lastMinute = DateTime.UtcNow.Minute;

            var now = DateTime.UtcNow;
            int n = 0;
            int m = 0;
            var all = refreshList.ToArray();
            refreshList.Clear();
            foreach (var ctr in all)
            {
                if (ctr.time < now)
                {
                    if (ctr.container.IsDestroyed)
                    {
                        ++m;
                        continue;
                    }
                    if (ctr.container.IsOpen())
                    {
                        refreshList.Add(ctr);
                        continue;
                    }
                    try
                    {
                        RepopulateContainer(ctr.container); // Will re-add
                        ++n;
                    }
                    catch (Exception ex) { PrintError("Failed to refresh container: " + ContainerName(ctr.container) + ": " + ex.Message + "\n" + ex.StackTrace); }
                }
                else
                    refreshList.Add(ctr); // Re-add for later
            }
            if (n > 0 || m > 0)
                if (Convert.ToBoolean(Config["Settings", "RefreshMessage"])) { Puts("Refreshed " + n + " containers (" + m + " destroyed)."); }
        }

        static string ContainerName(LootContainer container)
        {
            var name = container.gameObject.name;
            name = name.Substring(name.LastIndexOf("/") + 1);
            name += "#" + container.gameObject.GetInstanceID();
            return name;
        }

        void ModifyContainerContents(BaseNetworkable entity)
        {
            var e = entity as LootContainer; if (e?.inventory?.itemList == null) return;
            List<Rarity> RaritiesUsed = new List<Rarity>();
            SuppressRefresh(e);
            foreach (Item lootitem in e.inventory.itemList)
            {
                if (Exclude.list.Contains(lootitem.info.shortname)) { lootitem.RemoveFromContainer(); e.inventory.itemList.Remove(lootitem); break; }
                if (ExcludeFromMultiplication.list.Contains(lootitem.info.shortname)) { break; }
                var skins = GetSkins(ItemManager.FindItemDefinition(lootitem.info.itemid));
                if (skins.Count > 1 && Convert.ToBoolean(Config["Loot", "WorkshopSkins"])) // If workshop skins enabled, randomise skin
                {
                    lootitem.skin = skins.GetRandom(); if (lootitem.GetHeldEntity() != null) { lootitem.GetHeldEntity().skinID = lootitem.skin; }
                    if (Convert.ToBoolean(Config["Developer", "Debug"]) && Convert.ToBoolean(Config["Developer", "Skins"]))
                    { string debugs = "[" + lootitem.info.displayName.english + "] Skin has been modified to: " + lootitem.skin; Puts(debugs); PrintToChat(debugs); }
                }
                if (Components.list.Contains(lootitem.info.shortname))
                {
                    if (Convert.ToInt16(Config["Settings", "MultiplierComponents"]) != VANILLA_MULTIPLIER)
                    {
                        if (lootitem.info.stackable > 1) // Detect whether to change Amounts
                        {
                            if (Convert.ToBoolean(Config["Developer", "Debug"]) && Convert.ToBoolean(Config["Developer", "AmountChange"]))
                            { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=yellow>" + lootitem.info.displayName.english + " : original amount: " + lootitem.amount.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }

                            int limit = 0;
                            int ac = lootitem.amount * Convert.ToUInt16(Config["Settings", "MultiplierComponents"]);
                            if (LootData.ItemList.TryGetValue(lootitem.info.shortname, out limit)) { lootitem.amount = Math.Min(ac, Math.Min(limit, lootitem.info.stackable)); } else { break; }

                            if (Convert.ToBoolean(Config["Developer", "Debug"]) && Convert.ToBoolean(Config["Developer", "AmountChange"]))
                            { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=white>" + lootitem.info.displayName.english + " : new amount: " + lootitem.amount.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }
                        }
                    }
                }
                else
                {
                    if (Convert.ToInt16(Config["Settings", "Multiplier"]) != VANILLA_MULTIPLIER)
                    {
                        if (lootitem.info.stackable > 1 && !Convert.ToBoolean(Config["Settings", "MultiplyOnlyComponents"])) // Detect whether to change Amounts
                        {
                            if (Convert.ToBoolean(Config["Developer", "Debug"]) && Convert.ToBoolean(Config["Developer", "AmountChange"]))
                            { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=yellow>" + lootitem.info.displayName.english + " : original amount: " + lootitem.amount.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }

                            int limit = 0;
                            int ac = lootitem.amount * Convert.ToUInt16(Config["Settings", "Multiplier"]);
                            if (LootData.ItemList.TryGetValue(lootitem.info.shortname, out limit)) { lootitem.amount = Math.Min(ac, Math.Min(limit, lootitem.info.stackable)); } else { break; }

                            if (Convert.ToBoolean(Config["Developer", "Debug"]) && Convert.ToBoolean(Config["Developer", "AmountChange"]))
                            { string debugs = "[<color=green>" + e.GetInstanceID().ToString() + "</color> | " + e.ShortPrefabName + "] <color=white>" + lootitem.info.displayName.english + " : new amount: " + lootitem.amount.ToString() + "</color>"; Puts(debugs); PrintToChat(debugs); }
                        }
                    }
                    if (lootitem.info.rarity != Rarity.None && !RaritiesUsed.Contains(lootitem.info.rarity)) { RaritiesUsed.Add(lootitem.info.rarity); }
                }
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
                            if (Exclude.list.Contains(item.shortname)) { break; }
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
        void OnEntitySpawned(BaseNetworkable entity) { if (INIT) { var e = entity as LootContainer; if (e?.inventory?.itemList == null) return; RepopulateContainer(e); } }
    }
}
