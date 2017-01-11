using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;
using Oxide.Core.Plugins;
using Random = System.Random;
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("BetterLoot", "Fujikura/dcode", "2.11.4", ResourceId = 828)]
	[Description("A complete re-implementation of the drop system")]
	public class BetterLoot : RustPlugin
	{
		bool Changed = false;
		int populatedContainers;

		StoredSupplyDrop storedSupplyDrop = new StoredSupplyDrop();
		StoredHeliCrate storedHeliCrate = new StoredHeliCrate();
		StoredExportNames storedExportNames = new StoredExportNames();
		StoredLootTable storedLootTable = new StoredLootTable();
		SeparateLootTable separateLootTable = new SeparateLootTable();
		StoredBlacklist storedBlacklist = new StoredBlacklist();

		Dictionary<string, string> messages = new Dictionary<string, string>();

		Regex barrelEx;
		Regex crateEx;
		Regex heliEx = new Regex(@"heli_crate");

		List<string>[] items = new List<string>[5];
		List<string>[] itemsB = new List<string>[5];
		List<string>[] itemsC = new List<string>[5];
		List<string>[] itemsHeli = new List<string>[5];
		List<string>[] itemsSupply = new List<string>[5];
		int totalItems;
		int totalItemsB;
		int totalItemsC;
		int totalItemsHeli;
		int totalItemsSupply;
		int[] itemWeights = new int[5];
		int[] itemWeightsB = new int[5];
		int[] itemWeightsC = new int[5];
		int[] itemWeightsHeli = new int[5];
		int[] itemWeightsSupply = new int[5];
		int totalItemWeight;
		int totalItemWeightB;
		int totalItemWeightC;
		int totalItemWeightHeli;
		int totalItemWeightSupply;

		List<ItemDefinition> originalItems;
		List<ItemDefinition> originalItemsB;
		List<ItemDefinition> originalItemsC;
		List<ItemDefinition> originalItemsHeli;
		List<ItemDefinition> originalItemsSupply;

		Random rng = new Random();

		bool initialized = false;
		int lastMinute;

		List<ContainerToRefresh> refreshList = new List<ContainerToRefresh>();

		Regex _findTrash = new Regex(@"explosives|leather|gunpowder|cloth|map|wall.window.bars.wood|torch|paper|note|jackolantern.happy|jackolantern.angry|hat.cap|hat.boonie|hammer|lowgradefuel|door.hinged.wood|door.double.hinged.wood|campfire|building.planner|bone.club|bandage|ammo.rocket.smoke|tool.camera|hide|bone.fragments|boar|burlap|cactus|corn|seed|meat|flare|generator|battery|blood|pumpjack|signal|key|human|chicken|spoiled|cooked|burned|raw", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		#region Config

		bool pluginEnabled;
		bool seperateLootTables;
		string barrelTypes;
		string crateTypes;
		bool enableBarrels;
		int minItemsPerBarrel;
		int maxItemsPerBarrel;
		bool enableCrates;
		int minItemsPerCrate;
		int maxItemsPerCrate;
		int minItemsPerSupplyDrop;
		int maxItemsPerSupplyDrop;
		int minItemsPerHeliCrate;
		int maxItemsPerHeliCrate;
		double baseItemRarity = 2;
		int refreshMinutes;
		bool removeStackedContainers;
		bool enforceBlacklist;
		bool dropWeaponsWithAmmo;
		bool includeSupplyDrop;
		bool includeHeliCrate;
		bool listUpdatesOnLoaded;
		bool listUpdatesOnRefresh;
		bool useCustomTableHeli;
		bool useCustomTableSupply;
		bool refreshBarrels;
		bool refreshCrates;

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
			//baseItemRarity = Convert.ToDouble(GetConfig("Chances", "baseItemRarity", 2));
			minItemsPerBarrel =  Convert.ToInt32(GetConfig("Barrel", "minItemsPerBarrel", 1));
			maxItemsPerBarrel = Convert.ToInt32(GetConfig("Barrel", "maxItemsPerBarrel", 3));
			refreshBarrels = Convert.ToBoolean(GetConfig("Barrel", "refreshBarrels", false));
			barrelTypes = Convert.ToString(GetConfig("Barrel","barrelTypes","loot-barrel|loot_barrel|loot_trash"));
			enableBarrels = Convert.ToBoolean(GetConfig("Barrel", "enableBarrels", true));

			minItemsPerCrate = Convert.ToInt32(GetConfig("Crate", "minItemsPerCrate", 3));
			maxItemsPerCrate = Convert.ToInt32(GetConfig("Crate", "maxItemsPerCrate", 6));
			refreshCrates = Convert.ToBoolean(GetConfig("Crate", "refreshCrates", true));
			crateTypes = Convert.ToString(GetConfig("Crate","crateTypes","crate_normal"));
			enableCrates = Convert.ToBoolean(GetConfig("Crate", "enableCrates", true));

			minItemsPerSupplyDrop = Convert.ToInt32(GetConfig("SupplyDrop", "minItemsPerSupplyDrop", 3));
			maxItemsPerSupplyDrop = Convert.ToInt32(GetConfig("SupplyDrop", "maxItemsPerSupplyDrop", 6));
			includeSupplyDrop = Convert.ToBoolean(GetConfig("SupplyDrop", "includeSupplyDrop", false));
			useCustomTableSupply = Convert.ToBoolean(GetConfig("SupplyDrop", "useCustomTableSupply", true));

			minItemsPerHeliCrate = Convert.ToInt32(GetConfig("HeliCrate", "minItemsPerHeliCrate", 2));
			maxItemsPerHeliCrate = Convert.ToInt32(GetConfig("HeliCrate", "maxItemsPerHeliCrate", 4));
			includeHeliCrate = Convert.ToBoolean(GetConfig("HeliCrate", "includeHeliCrate", false));
			useCustomTableHeli = Convert.ToBoolean(GetConfig("HeliCrate", "useCustomTableHeli", true));

			refreshMinutes = Convert.ToInt32(GetConfig("Generic", "refreshMinutes", 30));
			enforceBlacklist = Convert.ToBoolean(GetConfig("Generic", "enforceBlacklist", false));
			dropWeaponsWithAmmo = Convert.ToBoolean(GetConfig("Generic", "dropWeaponsWithAmmo", true));
			listUpdatesOnLoaded = Convert.ToBoolean(GetConfig("Generic", "listUpdatesOnLoaded", true));
			listUpdatesOnRefresh = Convert.ToBoolean(GetConfig("Generic", "listUpdatesOnRefresh", false));
			pluginEnabled = Convert.ToBoolean(GetConfig("Generic", "pluginEnabled", true));
			removeStackedContainers = Convert.ToBoolean(GetConfig("Generic", "removeStackedContainers", true));
			seperateLootTables = Convert.ToBoolean(GetConfig("Generic", "seperateLootTables", true));

			if (!Changed) return;
			SaveConfig();
			Changed = false;
		}

		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadVariables();
		}

		#endregion Config

		Dictionary<string, string> weaponAmmunition = new Dictionary<string, string>()
		{
			{ "bow.hunting", "arrow.wooden" },
			{ "pistol.eoka", "ammo.handmade.shell" },
			{ "pistol.revolver", "ammo.pistol" },
			{ "pistol.semiauto", "ammo.pistol" },
			{ "shotgun.waterpipe", "ammo.shotgun" },
			{ "shotgun.pump", "ammo.shotgun" },
			{ "smg.thompson", "ammo.pistol" },
			{ "smg.2", "ammo.rifle" },
			{ "rifle.bolt", "ammo.rifle" },
			{ "rifle.semiauto", "ammo.rifle" },
			{ "lmg.m249", "ammo.rifle" },
			{ "rocket.launcher", "ammo.rocket.basic" },
			{ "rifle.ak", "ammo.rifle" },
			{ "crossbow", "arrow.wooden" },
			{ "rifle.lr300", "ammo.rifle" },
			{ "smg.mp5", "ammo.pistol" }
		};

		void Init()
		{
			LoadVariables();
			lastMinute = DateTime.UtcNow.Minute;
		}

		void OnServerInitialized()
		{
			if (initialized)
				return;
			var itemList = ItemManager.itemList;
			if (itemList == null || itemList.Count == 0)
			{
				NextTick(OnServerInitialized);
				return;
			}
			UpdateInternals(listUpdatesOnLoaded);
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
			foreach (var ctr in all) {
				if (ctr.time < now) {
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
					try {
						PopulateContainer(ctr.container); // Will re-add
						++n;
					} catch (Exception ex) {
						PrintError("Failed to refresh container: " + ContainerName(ctr.container) + ": " + ex.Message + "\n" + ex.StackTrace);
					}
				} else
					refreshList.Add(ctr); // Re-add for later
			}
			if (n > 0 || m > 0)
					if (listUpdatesOnRefresh) Puts("Refreshed " + n + " containers");
		}

		void UpdateInternals(bool doLog)
		{
			LoadBlacklist();
			if (seperateLootTables)
				LoadSeparateLootTable();
			else
				LoadLootTable();
			LoadHeliCrate();
			LoadSupplyDrop();
			timer.Once(0.1f, SaveExportNames);
			if (Changed)
			{
				SaveConfig();
				Changed = false;
			}
			if(!pluginEnabled)
			{
				PrintWarning("Plugin not active after first Setup. Change 'pluginEnabled' by config");
				return;
			}
			Puts("Updating internals ...");
			barrelEx = new Regex(@barrelTypes.ToLower());
			crateEx = new Regex(@crateTypes.ToLower());

			if (seperateLootTables)
			{
				originalItemsB = new List<ItemDefinition>();
				originalItemsC = new List<ItemDefinition>();
			}
			else
				originalItems = new List<ItemDefinition>();

			originalItemsHeli = new List<ItemDefinition>();
			originalItemsSupply = new List<ItemDefinition>();

			if (seperateLootTables)
			{
				foreach (KeyValuePair<string, int> pair in separateLootTable.ItemListBarrels)
					originalItemsB.Add(ItemManager.FindItemDefinition(pair.Key));
				foreach (KeyValuePair<string, int> pair in separateLootTable.ItemListCrates)
					originalItemsC.Add(ItemManager.FindItemDefinition(pair.Key));
			}
			else
				foreach (KeyValuePair<string, int> pair in storedLootTable.ItemList)
					originalItems.Add(ItemManager.FindItemDefinition(pair.Key));

			if (useCustomTableHeli && includeHeliCrate)
				foreach (KeyValuePair<string, int> pair in storedHeliCrate.ItemList)
					originalItemsHeli.Add(ItemManager.FindItemDefinition(pair.Key));
			if (useCustomTableSupply && includeSupplyDrop)
				foreach (KeyValuePair<string, int> pair in storedSupplyDrop.ItemList)
					originalItemsSupply.Add(ItemManager.FindItemDefinition(pair.Key));

			if (doLog)
			{
				if (seperateLootTables)
				{
					Puts("There are " + originalItemsB.Count+ " items in the global Barrels LootTable.");
					Puts("There are " + originalItemsC.Count+ " items in the global Crates LootTable.");
				}
				else
					Puts("There are " + originalItems.Count+ " items in the global LootTable.");

				if (useCustomTableHeli && includeHeliCrate)
					Puts("There are " + originalItemsHeli.Count + " items in the HeliTable.");
				if (useCustomTableSupply && includeSupplyDrop)
					Puts("There are " + originalItemsSupply.Count + " items in the SupplyTable.");
			}
			for (var i = 0; i < 5; ++i)
			{
				if (seperateLootTables)
				{
					itemsB[i] = new List<string>();
					itemsC[i] = new List<string>();
				}
				else
					items[i] = new List<string>();

				if (useCustomTableHeli && includeHeliCrate) itemsHeli[i] = new List<string>();
				if (useCustomTableSupply && includeSupplyDrop) itemsSupply[i] = new List<string>();
			}
			if (seperateLootTables)
			{
				totalItemsB = 0;
				totalItemsC = 0;
			}
			else
				totalItems = 0;

			if (useCustomTableHeli && includeHeliCrate) totalItemsHeli = 0;
			if (useCustomTableSupply && includeSupplyDrop) totalItemsSupply = 0;

			var notExistingItems = 0;
			var notExistingItemsB = 0;
			var notExistingItemsC = 0;

			var notExistingItemsHeli = 0;
			var notExistingItemsSupply = 0;

			if (seperateLootTables)
			{
				foreach (var item in originalItemsB)
				{
					if (item == null)
						continue;
					if (ItemExists(item.shortname)) {
						if (!storedBlacklist.ItemList.Contains(item.shortname)) {
							itemsB[RarityIndex(item.rarity)].Add(item.shortname);
							++totalItemsB;
						}
					}
					else
					{
						++notExistingItemsB;
					}
				}
				foreach (var item in originalItemsC)
				{
					if (item == null)
						continue;
					if (ItemExists(item.shortname)) {
						if (!storedBlacklist.ItemList.Contains(item.shortname)) {
							itemsC[RarityIndex(item.rarity)].Add(item.shortname);
							++totalItemsC;
						}
					}
					else
					{
						++notExistingItemsC;
					}
				}
			}
			else
			{
				foreach (var item in originalItems)
				{
					if (item == null) continue;
					if (ItemExists(item.shortname)) {
						if (!storedBlacklist.ItemList.Contains(item.shortname)) {
							items[RarityIndex(item.rarity)].Add(item.shortname);
							++totalItems;
						}
					}
					else
					{
						++notExistingItems;
					}
				}
			}
			if (useCustomTableHeli && includeHeliCrate)
			{
				foreach (var item in originalItemsHeli)
				{
					if (ItemExists(item.shortname))
					{
						if (!storedBlacklist.ItemList.Contains(item.shortname))
						{
							itemsHeli[RarityIndex(item.rarity)].Add(item.shortname);
							++totalItemsHeli;
						}
					}
					else
					{
						++notExistingItemsHeli;
					}
				}
			}
			if (useCustomTableSupply && includeSupplyDrop)
			{
				foreach (var item in originalItemsSupply)
				{
					if (ItemExists(item.shortname)) {
						if (!storedBlacklist.ItemList.Contains(item.shortname)) {
							itemsSupply[RarityIndex(item.rarity)].Add(item.shortname);
							++totalItemsSupply;
						}
					}
					else
					{
						++notExistingItemsSupply;
					}
				}
			}
			totalItemWeight = 0;
			totalItemWeightB = 0;
			totalItemWeightC = 0;
			totalItemWeightHeli = 0;
			totalItemWeightSupply = 0;
			for (var i = 0; i < 5; ++i) {
				if (seperateLootTables)
				{
					totalItemWeightB += (itemWeightsB[i] = ItemWeight(baseItemRarity, i) * itemsB[i].Count);
					totalItemWeightC += (itemWeightsC[i] = ItemWeight(baseItemRarity, i) * itemsC[i].Count);
				}
				else
				{
					totalItemWeight += (itemWeights[i] = ItemWeight(baseItemRarity, i) * items[i].Count);
				}
				if (useCustomTableHeli && includeHeliCrate) { totalItemWeightHeli += (itemWeightsHeli[i] = ItemWeight(baseItemRarity, i) * itemsHeli[i].Count); }
				if (useCustomTableSupply && includeSupplyDrop) { totalItemWeightSupply += (itemWeightsSupply[i] = ItemWeight(baseItemRarity, i) * itemsSupply[i].Count); }
				}
			populatedContainers = 0;
			timer.Once( 0.1f, () => {
				if (removeStackedContainers) FixLoot();
				foreach (var container in UnityEngine.Object.FindObjectsOfType<LootContainer>()) {
					try {
						PopulateContainer(container);
					} catch (Exception ex) {
						PrintWarning("Failed to populate container " + ContainerName(container) + ": " + ex.Message + "\n" + ex.StackTrace);
					}
				}
				Puts($"Internals have been updated. Populated '{populatedContainers}' supported containers.");
				initialized = true;
				populatedContainers = 0;
			});
		}
		

		void FixLoot()
		{
			var spawns = Resources.FindObjectsOfTypeAll<LootContainer>()
				.Where(c => c.isActiveAndEnabled).
				OrderBy(c => c.transform.position.x).ThenBy(c => c.transform.position.z).ThenBy(c => c.transform.position.z)
				.ToList();

			var count = spawns.Count();
			var racelimit = count * count;

			var antirace = 0;
			var deleted = 0;

			for (var i = 0; i < count; i++)
			{
				var box = spawns[i];
				var pos = new Vector2(box.transform.position.x, box.transform.position.z);

				if (++antirace > racelimit)
				{
					return;
				}

				var next = i + 1;
				while (next < count)
				{
					var box2 = spawns[next];
					var pos2 = new Vector2(box2.transform.position.x, box2.transform.position.z);
					var distance = Vector2.Distance(pos, pos2);

					if (++antirace > racelimit)
					{
						return;
					}

					if (distance < 0.25f)
					{
						spawns.RemoveAt(next);
						count--;
						(box2 as BaseEntity).KillMessage();
						deleted++;
					}
					else break;
				}
			}

			if (deleted > 0)
				Puts($"Removed {deleted} stacked LootContainer (out of {count})");
			else
				Puts($"No stacked LootContainer found.");
		}

		Item MightyRNG(string type) {
			List<string> selectFrom;
			int limit = 0;
			string itemName;
			Item item;
			int maxRetry = 20;

			switch (type)
			{
			case "default":
								do {
									selectFrom = null;
									item = null;
									var r = rng.Next(totalItemWeight);
									for (var i=0; i<5; ++i) {
										limit += itemWeights[i];
										if (r < limit) {
											selectFrom = items[i];
											break;
										}
									}
									if (selectFrom == null) {
										if (--maxRetry <= 0) {
											PrintError("Endless loop detected: ABORTING");
											break;
										}
										continue;
									}
									itemName = selectFrom[rng.Next(0, selectFrom.Count)];
									item = ItemManager.CreateByName(itemName, 1);
									if (item == null) {
										continue;
									}
									if (item.info == null) {
										continue;
									}
									break;
								} while (true);
								if (item == null)
									return null;
								if (item.info.stackable > 1 && storedLootTable.ItemList.TryGetValue(item.info.shortname, out limit))
								{
									item.amount = rng.Next(1, Math.Min(limit, item.info.stackable) + 1);
								}
								return item;
			case "crate":
								do {
									selectFrom = null;
									item = null;
									var r = rng.Next(totalItemWeightC);
									for (var i=0; i<5; ++i) {
										limit += itemWeightsC[i];
										if (r < limit) {
											selectFrom = itemsC[i];
											break;
										}
									}
									if (selectFrom == null) {
										if (--maxRetry <= 0) {
											PrintError("Endless loop detected: ABORTING");
											break;
										}
										continue;
									}
									itemName = selectFrom[rng.Next(0, selectFrom.Count)];
									item = ItemManager.CreateByName(itemName, 1);
									if (item == null) {
										continue;
									}
									if (item.info == null) {
										continue;
									}
									break;
								} while (true);
								if (item == null)
									return null;
								if (item.info.stackable > 1 && separateLootTable.ItemListCrates.TryGetValue(item.info.shortname, out limit))
								{
									item.amount = rng.Next(1, Math.Min(limit, item.info.stackable) + 1);
								}
								return item;
			case "barrel":
								do {
									selectFrom = null;
									item = null;
									var r = rng.Next(totalItemWeightB);
									for (var i=0; i<5; ++i) {
										limit += itemWeightsB[i];
										if (r < limit) {
											selectFrom = itemsB[i];
											break;
										}
									}
									if (selectFrom == null) {
										if (--maxRetry <= 0) {
											PrintError("Endless loop detected: ABORTING");
											break;
										}
										continue;
									}
									itemName = selectFrom[rng.Next(0, selectFrom.Count)];
									item = ItemManager.CreateByName(itemName, 1);
									if (item == null) {
										continue;
									}
									if (item.info == null) {
										continue;
									}
									break;
								} while (true);
								if (item == null)
									return null;
								if (item.info.stackable > 1 && separateLootTable.ItemListBarrels.TryGetValue(item.info.shortname, out limit))
								{
									item.amount = rng.Next(1, Math.Min(limit, item.info.stackable) + 1);
								}
								return item;
			case "heli":
								do {
									selectFrom = null;
									item = null;
									var r = rng.Next(totalItemWeightHeli);
									for (var i=0; i<5; ++i) {
										limit += itemWeightsHeli[i];
										if (r < limit) {
											selectFrom = itemsHeli[i];
											break;
										}
									}
									if (selectFrom == null) {
										if (--maxRetry <= 0) {
											PrintError("Endless loop detected: ABORTING");
											break;
										}
										continue;
									}
									itemName = selectFrom[rng.Next(0, selectFrom.Count)];
									item = ItemManager.CreateByName(itemName, 1);
									if (item == null) {
										continue;
									}
									if (item.info == null) {
										continue;
									}
									break;
								} while (true);
								if (item == null)
									return null;
								if (item.info.stackable > 1 && storedHeliCrate.ItemList.TryGetValue(item.info.shortname, out limit))
								{
									item.amount = rng.Next(1, Math.Min(limit, item.info.stackable) + 1);
								}
								return item;
			case "supply":
								do {
									selectFrom = null;
									item = null;
									var r = rng.Next(totalItemWeightSupply);
									for (var i=0; i<5; ++i) {
										limit += itemWeightsSupply[i];
										if (r < limit) {
											selectFrom = itemsSupply[i];
											break;
										}
									}
									if (selectFrom == null) {
										if (--maxRetry <= 0) {
											PrintError("Endless loop detected: ABORTING");
											break;
										}
										continue;
									}
									itemName = selectFrom[rng.Next(0, selectFrom.Count)];
									item = ItemManager.CreateByName(itemName, 1);
									if (item == null) {
										continue;
									}
									if (item.info == null) {
										continue;
									}
									break;
								} while (true);
								if (item == null)
									return null;
								if (item.info.stackable > 1 && storedSupplyDrop.ItemList.TryGetValue(item.info.shortname, out limit))
								{
									item.amount = rng.Next(1, Math.Min(limit, item.info.stackable) + 1);
								}
								return item;
					default:
								return null;
			}
		}

		void ClearContainer(LootContainer container)
		{
			while (container.inventory.itemList.Count > 0) {
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

		void PopulateContainer(LootContainer container)
		{
			if (container.inventory == null) {
				return;
			}
			int min = 1;
			int max = 0;
			bool refresh = false;
			string type = "empty";

			if (barrelEx.IsMatch(container.gameObject.name.ToLower()) && enableBarrels) {
				SuppressRefresh(container);
				ClearContainer(container);
				min = minItemsPerBarrel;
				max = maxItemsPerBarrel;
				if (seperateLootTables)
					type = "barrel";
				else
					type = "default";
				refresh = refreshBarrels;
			}
			else if (crateEx.IsMatch(container.gameObject.name.ToLower()) && enableCrates) {
				SuppressRefresh(container);
				ClearContainer(container);
				min = minItemsPerCrate;
				max = maxItemsPerCrate;
				if (seperateLootTables)
					type = "crate";
				else
					type = "default";
				refresh = refreshCrates;
			}
			else if (heliEx.IsMatch(container.gameObject.name) && includeHeliCrate) {
				SuppressRefresh(container);
				ClearContainer(container);
				min = minItemsPerHeliCrate;
				max = maxItemsPerHeliCrate;
				if(useCustomTableHeli)
				{
					type = "heli";
				}
				else
				{
					if (seperateLootTables)
						type = "crate";
					else
						type = "default";
				}
			}
			else if (container is SupplyDrop && includeSupplyDrop) {
				SuppressRefresh(container);
				ClearContainer(container);
				min = minItemsPerSupplyDrop;
				max = maxItemsPerSupplyDrop;
				if(useCustomTableSupply)
				{
					type = "supply";
				}
				else
				{
					if (seperateLootTables)
						type = "crate";
					else
						type = "default";
				}
			}
			else return; // not in List

			if (min < 1 ) min = 1;
			if (max > 30) max = 30;
			var n = UnityEngine.Random.Range(min,max);
			container.inventory.capacity = n;
			container.inventorySlots = n;
			if (n > 18) container.panelName= "largewoodbox";
			else container.panelName= "generic";

			var sb = new StringBuilder();
			var items = new List<Item>();
			var itemNames = new List<string>();
			var maxRetry = 20;
			bool hasAmmo = false;
			for (int i = 0; i < n; ++i)
			{
				if (maxRetry == 0) break;
				var item = MightyRNG(type);
				if (item == null)
				{
					--maxRetry;
                    --i;
					continue;
				}
				if (itemNames.Contains(item.info.shortname))
                {
					item.Remove(0f);
                    --maxRetry;
                    --i;
                    continue;
                }
                else
                    itemNames.Add(item.info.shortname);
				items.Add(item);
				if (sb.Length > 0)
					sb.Append(", ");
				if (item.amount > 1)
					sb.Append(item.amount).Append("x ");
				sb.Append(item.info.shortname);

				if (dropWeaponsWithAmmo && !hasAmmo && items.Count < container.inventorySlots) { // Drop some ammunition with first weapon
					string ammo;
					int limit;
					if (weaponAmmunition.TryGetValue(item.info.shortname, out ammo) && storedLootTable.ItemList.TryGetValue(ammo, out limit)) {
						try {
							item = ItemManager.CreateByName(ammo, rng.Next(2, limit + 1));
							items.Add(item);
							sb.Append(" + ");
							if (item.amount > 1)
								sb.Append(item.amount).Append("x ");
							sb.Append(item.info.shortname);
							hasAmmo = true;
						} catch (Exception) {
							PrintWarning("Failed to obtain ammo item: "+ammo);
						}
					}
				}
			}
			foreach (var item in items)
				item.MoveToContainer(container.inventory, -1, false);
			container.inventory.MarkDirty();
			populatedContainers++;
			if (refresh)
				refreshList.Add(new ContainerToRefresh() { container = container, time = DateTime.UtcNow.AddMinutes(refreshMinutes) });
		}

		void OnEntitySpawned(BaseNetworkable entity) {
			if (!initialized)
				return;
			try {
				var container = entity as LootContainer;
				if (container == null)
					return;
				if (container.inventory == null || container.inventory.itemList == null) {
					return;
				}
				PopulateContainer(container);
			} catch (Exception ex) {
				PrintError("OnEntitySpawned failed: " + ex.Message);
			}
		}

		[ChatCommand("blacklist")]
		void cmdChatBlacklist(BasePlayer player, string command, string[] args) {
			string usage = "Usage: /blacklist [additem|deleteitem] \"ITEMNAME\"";
			if (!initialized)
			{
				SendReply(player, string.Format("Plugin not enabled."));
				return;
			}
			if (args.Length == 0) {
				if (storedBlacklist.ItemList.Count == 0) {
					SendReply(player, string.Format("There are no blacklisted items"));
				} else {
					var sb = new StringBuilder();
					foreach (var item in storedBlacklist.ItemList) {
						if (sb.Length > 0)
							sb.Append(", ");
						sb.Append(item);
					}
					SendReply(player, string.Format("Blacklisted items: {0}", sb.ToString()));
				}
				return;
			}
			if (!ServerUsers.Is(player.userID, ServerUsers.UserGroup.Owner)) {
				SendReply(player, "You are not authorized to use this command");
				return;
			}
			if (args.Length != 2) {
				SendReply(player, usage);
				return;
			}
			if (args[0] == "additem") {
				if (!ItemExists(args[1])) {
					SendReply(player, string.Format("Not a valid item: {0}", args[1]));
					return;
				}
				if (!storedBlacklist.ItemList.Contains(args[1])) {
					storedBlacklist.ItemList.Add(args[1]);
					UpdateInternals(false);
					SendReply(player, string.Format("The item '{0}' is now blacklisted", args[1]));
					SaveBlacklist();
					return;
				} else {
					SendReply(player, string.Format("The item '{0}' is already blacklisted", args[1]));
					return;
				}
			}
			else if (args[0] == "deleteitem") {
				if (!ItemExists(args[1])) {
					SendReply(player, string.Format("Not a valid item: {0}", args[1]));
					return;
				}
				if (storedBlacklist.ItemList.Contains(args[1])) {
					storedBlacklist.ItemList.Remove(args[1]);
					UpdateInternals(false);
					SendReply(player, string.Format("The item '{0}' is now no longer blacklisted", args[1]));
					SaveBlacklist();
					return;
				} else {
					SendReply(player, string.Format("The item '{0}' is not blacklisted", args[1]));
					return;
				}
			}
			else {
				SendReply(player, usage);
				return;
			}
		}

		void OnItemAddedToContainer(ItemContainer container, Item item)
		{
			if (!initialized || !enforceBlacklist)
				return;
			try {
				var owner = item.GetOwnerPlayer();
				if (owner != null && (ServerUsers.Is(owner.userID, ServerUsers.UserGroup.Owner) || ServerUsers.Is(owner.userID, ServerUsers.UserGroup.Moderator)))
					return;
				if (storedBlacklist.ItemList.Contains(item.info.shortname)) {
					Puts(string.Format("Destroying item instance of '{0}'", item.info.shortname));
					item.RemoveFromContainer();
					item.Remove(0f);
				}
			} catch (Exception ex) {
				PrintError("OnItemAddedToContainer failed: " + ex.Message);
			}
		}

		#region Utility Methods

		static string ContainerName(LootContainer container)
		{
			var name = container.gameObject.name;
			name = name.Substring(name.LastIndexOf("/") + 1);
			name += "#" + container.gameObject.GetInstanceID();
			return name;
		}

		static int RarityIndex(Rarity rarity)
		{
			switch (rarity) {
				case Rarity.None: return 0;
				case Rarity.Common: return 1;
				case Rarity.Uncommon: return 2;
				case Rarity.Rare: return 3;
				case Rarity.VeryRare: return 4;
			}
			return -1;
		}

		bool ItemExists(string name)
		{
			foreach (var def in ItemManager.itemList) {
				if (def.shortname != name)
					continue;
				var testItem = ItemManager.CreateByName(name, 1);
				if (testItem != null) {
					testItem.Remove(0f);
					return true;
				}
			}
			return false;
		}

		bool IsWeapon(string name)
		{
			return weaponAmmunition.ContainsKey(name);
		}

		int ItemWeight(double baseRarity, int index)
		{
			return (int)(Math.Pow(baseRarity, 4 - index) * 1000); // Round to 3 decimals
		}

		string _(string text, Dictionary<string, string> replacements = null)
		{
			if (messages.ContainsKey(text) && messages[text] != null)
				text = messages[text];
			if (replacements != null)
				foreach (var replacement in replacements)
					text = text.Replace("%" + replacement.Key + "%", replacement.Value);
			return text;
		}

		bool isSupplyDropActive()
		{
			if (includeSupplyDrop) return true;
			return false;
		}

		#endregion

		class ContainerToRefresh
		{
			public LootContainer container;
			public DateTime time;
		}

		#region LootTable

		class StoredLootTable
		{
			public Dictionary<string, int> ItemList = new Dictionary<string, int>();

			public StoredLootTable()
			{
			}
		}

		void LoadLootTable()
		{
			storedLootTable = Interface.GetMod().DataFileSystem.ReadObject<StoredLootTable>("BetterLoot\\LootTable");
			if (storedLootTable.ItemList.Count == 0)
			{
				storedLootTable = new StoredLootTable();
				foreach(var it in ItemManager.itemList)
				{
					int stack = 0;
					if(!ItemExists(it.shortname)) continue;
					if(_findTrash.IsMatch(it.shortname)) continue;
					if (it.category == ItemCategory.Weapon) continue;
					if (it.category == ItemCategory.Ammunition) continue;
					if (it.category == ItemCategory.Tool) continue;
					if (it.category == ItemCategory.Traps) continue;
					if (it.category == ItemCategory.Construction) continue;
					if (it.category == ItemCategory.Attire) continue;
					if (it.category == ItemCategory.Resources) continue;
					if (it.category == ItemCategory.Misc) continue;
					if (it.category == ItemCategory.Items) continue;					
					if (it.category == ItemCategory.Food) stack = 10;
					if (it.category == ItemCategory.Medical) stack = 5;
					if (it.category == ItemCategory.Component) stack = 5;
					if (stack == 0) stack = 1;
					storedLootTable.ItemList.Add(it.shortname,stack);
				}
				Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\LootTable", storedLootTable);
			}
		}
		

		void SaveLootTable() => Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\LootTable", storedLootTable);

		#endregion LootTable

		#region SeparateLootTable

		class SeparateLootTable
		{
			public Dictionary<string, int> ItemListBarrels = new Dictionary<string, int>();
			public Dictionary<string, int> ItemListCrates = new Dictionary<string, int>();
			public SeparateLootTable()
			{
			}
		}

		void LoadSeparateLootTable()
		{
			separateLootTable = Interface.GetMod().DataFileSystem.ReadObject<SeparateLootTable>("BetterLoot\\LootTable");
			if (separateLootTable.ItemListBarrels.Count == 0 || separateLootTable.ItemListCrates.Count == 0)
			{
				var checkLootTable = Interface.GetMod().DataFileSystem.ReadObject<StoredLootTable>("BetterLoot\\LootTable");
				if (checkLootTable.ItemList.Count > 0)
				{
					separateLootTable.ItemListBarrels = checkLootTable.ItemList;
					separateLootTable.ItemListCrates = checkLootTable.ItemList;
					Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\LootTable", separateLootTable);
					return;
				}
				separateLootTable = new SeparateLootTable();
				foreach(var it in ItemManager.itemList)
				{
					int stack = 0;
					if(!ItemExists(it.shortname)) continue;
					if(_findTrash.IsMatch(it.shortname)) continue;
					if (it.category == ItemCategory.Weapon) continue;
					if (it.category == ItemCategory.Ammunition) continue;
					if (it.category == ItemCategory.Tool) continue;
					if (it.category == ItemCategory.Traps) continue;
					if (it.category == ItemCategory.Construction) continue;
					if (it.category == ItemCategory.Attire) continue;
					if (it.category == ItemCategory.Resources) continue;
					if (it.category == ItemCategory.Misc) continue;
					if (it.category == ItemCategory.Items) continue;		
					if (it.category == ItemCategory.Food) stack = 10;
					if (it.category == ItemCategory.Medical) stack = 5;
					if (it.category == ItemCategory.Component) stack = 5;
					if (stack == 0) stack = 1;
					separateLootTable.ItemListBarrels.Add(it.shortname,stack);
					separateLootTable.ItemListCrates.Add(it.shortname,stack);
				}
				Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\LootTable", separateLootTable);
			}
		}

		void SaveSeparateLootTable() => Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\LootTable", separateLootTable);

		#endregion SeparateLootTable

		#region ExportNames

		class StoredExportNames
		{
			public int version;
			public Dictionary<string, string> AllItemsAvailable = new Dictionary<string, string>();
			public StoredExportNames()
			{
			}
		}

		void SaveExportNames()
		{
			storedExportNames = Interface.GetMod().DataFileSystem.ReadObject<StoredExportNames>("BetterLoot\\NamesList");
			if (storedExportNames.AllItemsAvailable.Count == 0 || (int)storedExportNames.version != Rust.Protocol.network)
			{
				storedExportNames = new StoredExportNames();
				var exportItems = new List<ItemDefinition>(ItemManager.itemList);
				storedExportNames.version = Rust.Protocol.network;
				foreach(var it in exportItems)
					if(ItemExists(it.shortname))
						storedExportNames.AllItemsAvailable.Add(it.shortname, it.displayName.english);
				Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\NamesList", storedExportNames);
				Puts($"Exported {storedExportNames.AllItemsAvailable.Count} items to 'NamesList'");
			}
		}

		#endregion ExportNames

		#region SupplyDrop

		class StoredSupplyDrop
		{
			public Dictionary<string, int> ItemList = new Dictionary<string, int>();

			public StoredSupplyDrop()
			{
			}
		}

		void LoadSupplyDrop()
		{
			storedSupplyDrop = Interface.GetMod().DataFileSystem.ReadObject<StoredSupplyDrop>("BetterLoot\\SupplyDrop");
			if (pluginEnabled && storedSupplyDrop.ItemList.Count > 0 && !includeSupplyDrop && !useCustomTableSupply)
				Puts("SupplyDrop > loot population is disabled by 'includeSupplyDrop'");
			if (pluginEnabled && storedSupplyDrop.ItemList.Count > 0 && !includeSupplyDrop && useCustomTableSupply)
				Puts("SupplyDrop > 'useCustomTableSupply' enabled, but loot population inactive by 'includeSupplyDrop'");
			if (storedSupplyDrop.ItemList.Count == 0)
			{
				includeSupplyDrop = false;
				Config["SupplyDrop","includeSupplyDrop"]= includeSupplyDrop;
				Changed = true;
				Puts("SupplyDrop > table not found, option disabled by 'includeSupplyDrop' > Creating a new file.");
				storedSupplyDrop = new StoredSupplyDrop();
				foreach(var it in ItemManager.itemList)
				{
					int stack = 0;
					if(!ItemExists(it.shortname)) continue;
					if(_findTrash.IsMatch(it.shortname)) continue;
					if(it.shortname == "rock" || it.shortname == "lmg.m249" || it.shortname.Contains("arrow") || it.shortname.Contains("rocket.") || it.shortname.Contains("hazmat.")) continue;
					if (it.category == ItemCategory.Weapon) stack = 1;
					if (it.category == ItemCategory.Ammunition) stack = 32;
					if (it.category == ItemCategory.Tool) stack = 1;
					if (it.category == ItemCategory.Traps && it.shortname != "autoturret") stack = 5;
					if (it.category == ItemCategory.Construction) continue;
					if (it.category == ItemCategory.Attire) stack = 1;
					if (it.category == ItemCategory.Resources && it.shortname != "targeting.computer" && it.shortname != "cctv.camera") continue;
					if (it.category == ItemCategory.Misc) continue;
					if (it.category == ItemCategory.Items) continue;		
					if (it.category == ItemCategory.Food) continue;
					if (it.category == ItemCategory.Medical) continue;
					if (it.category == ItemCategory.Component) continue;
					if (stack == 0) stack = 1;
					storedSupplyDrop.ItemList.Add(it.shortname,stack);
				}
				Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\SupplyDrop", storedSupplyDrop);
			}
		}

		void SaveSupplyDrop() => Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\SupplyDrop", storedSupplyDrop);

		#endregion SupplyDrop

		#region HeliCrate

		class StoredHeliCrate
		{
			public Dictionary<string, int> ItemList = new Dictionary<string, int>();

			public StoredHeliCrate()
			{
			}
		}

		void LoadHeliCrate()
		{
			storedHeliCrate = Interface.GetMod().DataFileSystem.ReadObject<StoredHeliCrate>("BetterLoot\\HeliCrate");
			if (pluginEnabled && storedHeliCrate.ItemList.Count > 0 && !includeHeliCrate && !useCustomTableHeli)
				Puts("HeliCrate > loot population is disabled by 'includeHeliCrate'");
			if (pluginEnabled && storedHeliCrate.ItemList.Count > 0 && !includeHeliCrate && useCustomTableHeli)
				Puts("HeliCrate > 'useCustomTableHeli' enabled, but loot population inactive by 'includeHeliCrate'");
			if (storedHeliCrate.ItemList.Count == 0)
			{
				includeHeliCrate = false;
				Config["HeliCrate","includeHeliCrate"]= includeHeliCrate;
				Changed = true;
				Puts("HeliCrate > table not found, option disabled by 'includeHeliCrate' > Creating a new file.");
				storedHeliCrate = new StoredHeliCrate();
				foreach(var it in ItemManager.itemList)
				{
					int stack = 0;
					if(!ItemExists(it.shortname)) continue;
					if(_findTrash.IsMatch(it.shortname)) continue;
					if(it.shortname == "rock" || it.shortname.Contains("arrow") || it.shortname.Contains("grenade") || it.shortname.Contains("handmade") ||
					 it.shortname.Contains("bow") || it.shortname.Contains("salvaged") || it.shortname.Contains("knife") ||
					 it.shortname.Contains("mac") || it.shortname.Contains("waterpipe") || it.shortname.Contains("spear") || it.shortname == "longsword") continue;

					if (it.category == ItemCategory.Weapon) stack = 1;
					if (it.category == ItemCategory.Ammunition && !it.shortname.Contains("rocket.")) stack = 128;
					if (it.category == ItemCategory.Ammunition && it.shortname.Contains("rocket.")) stack = 8;
					if (it.category == ItemCategory.Tool) continue;
					if (it.category == ItemCategory.Traps) continue;
					if (it.category == ItemCategory.Construction) continue;
					if (it.category == ItemCategory.Attire) continue;
					if (it.category == ItemCategory.Resources && it.shortname != "targeting.computer" && it.shortname != "cctv.camera") continue;
					if (it.category == ItemCategory.Misc) continue;
					if (it.category == ItemCategory.Items) continue;		
					if (it.category == ItemCategory.Food) continue;
					if (it.category == ItemCategory.Medical) continue;
					if (it.category == ItemCategory.Component) continue;
					if (stack == 0) stack = 1;
					storedHeliCrate.ItemList.Add(it.shortname,stack);
				}
				Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\HeliCrate", storedHeliCrate);
			}
		}

		void SaveHeliCrate() => Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\HeliCrate", storedHeliCrate);

		#endregion HeliCrate

		#region Blacklist

		class StoredBlacklist
		{
			public List<string> ItemList = new List<string>();

			public StoredBlacklist()
			{
			}
		}

		void LoadBlacklist()
		{
			storedBlacklist = Interface.GetMod().DataFileSystem.ReadObject<StoredBlacklist>("BetterLoot\\Blacklist");
			if (storedBlacklist.ItemList.Count == 0)
			{
				Puts("No Blacklist found, creating new file...");
				storedBlacklist = new StoredBlacklist();
				storedBlacklist.ItemList.Add("flare");
				Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\Blacklist", storedBlacklist);
				return;
			}
		}

		void SaveBlacklist() => Interface.GetMod().DataFileSystem.WriteObject("BetterLoot\\Blacklist", storedBlacklist);

		#endregion Blacklist

		#region Reload

		[ConsoleCommand("betterloot.reload")]
		void consoleReload(ConsoleSystem.Arg arg)
		{
			if(arg.connection != null && arg.connection.authLevel < 2) return;
			try { Config.Load(); }
			catch { LoadDefaultConfig(); }
			LoadVariables();
			UpdateInternals(true);
		}

		#endregion Reload
	}
}
