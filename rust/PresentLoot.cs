using System;
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("PresentLoot", "redBDGR", "1.0.3", ResourceId = 2599)]
    [Description("Modify the loot tables for the presents")]

    class PresentLoot : RustPlugin
    {
        private bool Changed;

        #region Data

        private DynamicConfigFile PresentData;
        private DynamicConfigFile PresentData2;
        private DynamicConfigFile PresentData3;
        private StoredData storedData;
        private StoredData storedData2;
        private StoredData storedData3;

        private class StoredData
        {
            public List<ItemInfo> lootTable = new List<ItemInfo>();
        }

        private void SaveData()
        {
            storedData.lootTable = smallList;
            storedData2.lootTable = mediumList;
            storedData3.lootTable = largeList;
            PresentData.WriteObject(storedData);
            PresentData2.WriteObject(storedData2);
            PresentData3.WriteObject(storedData3);
        }

        private void LoadData()
        {
            try
            {
                storedData = PresentData.ReadObject<StoredData>();
                storedData2 = PresentData2.ReadObject<StoredData>();
                storedData3 = PresentData3.ReadObject<StoredData>();
                smallList = storedData.lootTable;
                mediumList = storedData2.lootTable;
                largeList = storedData3.lootTable;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        private class ItemInfo
        {
            public string itemName;
            public int minItemAmount;
            public int maxItemAmount;
            public Dictionary<string, float> attachments;
            public float chance;
            public int skinID;
        }

        #endregion

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            smallMinItems = Convert.ToInt16(GetConfig("Small Presents", "Min Items", 1));
            smallMaxItems = Convert.ToInt16(GetConfig("Small Presents", "Max Items", 2));

            mediumMinItems = Convert.ToInt16(GetConfig("Medium Presents", "Min Items", 1));
            mediumMaxItems = Convert.ToInt16(GetConfig("Medium Presents", "Max Items", 2));

            largeMinItems = Convert.ToInt16(GetConfig("Large Presents", "Min Items", 1));
            largeMaxItems = Convert.ToInt16(GetConfig("Large Presents", "Max Items", 2));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            Changed = true;
            return value;
        }

        #endregion

        private List<ItemInfo> smallList = new List<ItemInfo>();
        private List<ItemInfo> mediumList = new List<ItemInfo>();
        private List<ItemInfo> largeList = new List<ItemInfo>();

        private int smallMinItems = 1;
        private int smallMaxItems = 2;
        private int mediumMinItems = 1;
        private int mediumMaxItems = 2;
        private int largeMinItems = 1;
        private int largeMaxItems = 2;

        private void Init()
        {
            PresentData = Interface.Oxide.DataFileSystem.GetFile("PresentLoot/SmallPresents");
            PresentData2 = Interface.Oxide.DataFileSystem.GetFile("PresentLoot/MediumPresents");
            PresentData3 = Interface.Oxide.DataFileSystem.GetFile("PresentLoot/LargePresents");
            LoadData();
            AddDefaultItems();
            LoadVariables();
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You are not allowed to use this command",
                ["Tables Reloaded"] = "The present data tables have been reloaded",
            }, this);
        }

        private void AddDefaultItems()
        {
            bool updated = false;
            if (smallList.Count == 0)
            {
                smallList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 4, chance = 0.20f, itemName = "chocholate", maxItemAmount = 5, skinID = 0 });
                smallList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 10, chance = 0.20f, itemName = "metal.fragments", maxItemAmount = 50, skinID = 0 });
                updated = true;
            }
            if (mediumList.Count == 0)
            {
                mediumList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 3, chance = 0.20f, itemName = "metalspring", maxItemAmount = 5, skinID = 0 });
                mediumList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "pistol.revolver", maxItemAmount = 1, skinID = 0 });
                mediumList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "stocking.small", maxItemAmount = 1, skinID = 0 });
                updated = true;
            }
            if (largeList.Count == 0)
            {
                largeList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "stocking.large", maxItemAmount = 1, skinID = 0 });
                largeList.Add(new ItemInfo { attachments = new Dictionary<string, float> { {"weapon.mod.holosight", 0.5f}, {"weapon.mod.lasersight", 0.5f} }, minItemAmount = 1, chance = 0.20f, itemName = "shotgun.pump", maxItemAmount = 1, skinID = 0 });
                largeList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 5, chance = 0.20f, itemName = "ammo.shotgun", maxItemAmount = 15, skinID = 0 });
                updated = true;
            }
            if (!updated) return;
            SaveData();
            LoadData();
        }

        private object OnItemAction(Item item, string action)
        {
            if (item == null || action == null || action == "")
                return null;
            if (item.info.shortname != "xmas.present.large" && item.info.shortname != "xmas.present.medium" && item.info.shortname != "xmas.present.small")
                return null;
            if (action != "unwrap")
                return null;
            BasePlayer player = item.GetRootContainer().GetOwnerPlayer();
            if (player == null)
                return null;
            GiveThink(player, item.info.shortname);
            ItemRemovalThink(item, player);
            Effect.server.Run("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player.transform.position);
            return false;
        }

        private void GiveThink(BasePlayer player, string presentName)
        {
            foreach (Item newItem in CreateItems(presentName))
            {
                if (newItem == null)
                {
                    Puts("Item was null");
                    continue;
                }
                if (player.inventory.containerMain.IsFull())
                    newItem.Drop(player.transform.position, Vector3.down);
                else
                    newItem.MoveToContainer(player.inventory.containerMain);
            }
        }

        private List<Item> CreateItems(string presentname)
        {
            List<Item> x = new List<Item>();
            int itemCount = 0;
            switch (presentname)
            {
                case "xmas.present.small":
                {
                    int maxItemAmount = Mathf.RoundToInt(Random.Range(Convert.ToSingle(smallMinItems), Convert.ToSingle(smallMaxItems)));
                        while (itemCount < maxItemAmount)
                    {
                        x.Add(TryMakeItem(smallList));
                        itemCount++;
                    }
                    return x;
                }
                case "xmas.present.medium":
                {
                    int maxItemAmount = Mathf.RoundToInt(Random.Range(Convert.ToSingle(mediumMinItems), Convert.ToSingle(mediumMaxItems)));
                        while (itemCount < maxItemAmount)
                    {
                        x.Add(TryMakeItem(mediumList));
                        itemCount++;
                    }
                    return x;
                }
                case "xmas.present.large":
                {
                    int maxItemAmount = Mathf.RoundToInt(Random.Range(Convert.ToSingle(largeMinItems), Convert.ToSingle(largeMaxItems)));
                    while (itemCount < maxItemAmount)
                    {
                        x.Add(TryMakeItem(largeList));
                        itemCount++;
                    }
                    return x;
                }
            }
            return x;
        }

        private static Item TryMakeItem(List<ItemInfo> list)
        {
            while (true)
            {
                ItemInfo entry = list[Mathf.RoundToInt(Random.Range(0f, Convert.ToSingle(list.Count - 1)))];
                if (Random.Range(0f, 1f) > entry.chance)
                    continue;
                Item newItem = ItemManager.CreateByName(entry.itemName, Random.Range(entry.minItemAmount, entry.maxItemAmount), (ulong) entry.skinID);
                foreach (var attachmentName in entry.attachments)
                    if (Random.Range(0f, 1f) < attachmentName.Value)
                        ItemManager.CreateByName(attachmentName.Key).MoveToContainer(newItem.contents);
                return newItem;
            }
        }

        private static void ItemRemovalThink(Item item, BasePlayer player)
        {
            if (item.amount == 1)
            {
                item.RemoveFromContainer();
                item.Remove();
            }
            else
            {
                item.amount = item.amount - 1;
                player.inventory.SendSnapshot();
            }
        }

        private string msg(string key, string id = null)
        {
            return lang.GetMessage(key, this, id);
        }
    }
}