using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.IO.Compression;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("ChestStacks", "Jake_Rich", "1.0.5", ResourceId = 2739)]
    [Description("Higher stack sizes in storage containers.")]

    public class ChestStacks : RustPlugin
    {
        public static ChestStacks _plugin;
        public ConfigData Settings { get { return _settingsFile.Instance; } }
        public JSONFile<ConfigData> _settingsFile; 

        void Init()
        {
            _plugin = this;
        }

        void Loaded()
        {
            _settingsFile = new JSONFile<ConfigData>("ChestStacks", ConfigLocation.Config);
        }

        void Unload()
        {

        }

        #region Hooks

        object OnMaxStackable(Item item)
        {
            if (item.info.stackable == 1)
            {
                return null;
            }
            if (item.parent == null)
            {
                return null;
            }
            if (item.parent.entityOwner == null)
            {
                return null;
            }
            int stacksize = Mathf.FloorToInt(Settings.GetStackSize(item.parent.entityOwner) * item.info.stackable);
            return stacksize;
        }

        object CanMoveItem(Item movedItem, PlayerInventory playerInventory, uint targetContainerID, int targetSlot, int amount)
        {
            var container = playerInventory.FindContainer(targetContainerID);
            var player = playerInventory.GetComponent<BasePlayer>();
            var lootContainer = playerInventory.loot?.FindContainer(targetContainerID);

            //Puts($"TargetSlot {targetSlot} Amount {amount} TargetContainer {targetContainerID}");

            #region Right-Click Overstack into Player Inventory

            if (targetSlot == -1)  
            {
                //Right click overstacks into player inventory
                if (lootContainer == null) 
                {
                    if (movedItem.amount > movedItem.info.stackable)
                    {
                        int loops = 1;
                        if (player.serverInput.IsDown(BUTTON.SPRINT))
                        {
                            loops = Mathf.CeilToInt((float)movedItem.amount / movedItem.info.stackable);
                        }
                        for (int i = 0; i < loops; i++)
                        {
                            if (movedItem.amount <= movedItem.info.stackable)
                            {
                                if (container != null)
                                {
                                    movedItem.MoveToContainer(container, targetSlot);
                                }
                                else
                                {
                                    playerInventory.GiveItem(movedItem);
                                }
                                break;
                            }
                            var itemToMove = movedItem.SplitItem(movedItem.info.stackable);
                            bool moved = false;
                            if (container != null)
                            {
                                moved = itemToMove.MoveToContainer(container, targetSlot);
                            }
                            else
                            {
                                moved = playerInventory.GiveItem(itemToMove);
                            }
                            if (moved == false)
                            {
                                movedItem.amount += itemToMove.amount;
                                itemToMove.Remove();
                                break;
                            }
                            if (movedItem != null)
                            {
                                movedItem.MarkDirty();
                            }
                        }
                        playerInventory.ServerUpdate(0f);
                        return false;
                    }
                }
                //Shift Right click into storage container
                else
                {
                    if (player.serverInput.IsDown(BUTTON.SPRINT))
                    {
                        var items = playerInventory.AllItems().Where(x => x.info == movedItem.info);
                        foreach (var item in items)
                        {
                            if (!item.MoveToContainer(lootContainer))
                            {
                                continue;
                            }
                        }
                        playerInventory.ServerUpdate(0f);
                        return false;
                    }
                }
            }

            #endregion

            #region Moving Overstacks Around In Chest

            if (amount > movedItem.info.stackable && lootContainer != null)
            {
                var targetItem = container.GetSlot(targetSlot);
                if (targetItem == null)
                {
                    //Split item into chest
                    if (amount < movedItem.amount)
                    {
                        ItemHelper.SplitMoveItem(movedItem, amount, container, targetSlot);
                    }
                    else
                    {
                        //Moving items when amount > info.stacksize
                        movedItem.MoveToContainer(container, targetSlot);
                    }
                }
                else
                {
                    if (!targetItem.CanStack(movedItem) && amount == movedItem.amount)
                    {
                        //Swapping positions of items
                        ItemHelper.SwapItems(movedItem, targetItem);
                    }
                    else
                    {
                        if (amount < movedItem.amount)
                        {
                            ItemHelper.SplitMoveItem(movedItem, amount, playerInventory);
                        }
                        else
                        {
                            movedItem.MoveToContainer(container, targetSlot);
                        }
                        //Stacking items when amount > info.stacksize

                    }
                }
                playerInventory.ServerUpdate(0f);
                return false;
            }

            #endregion

            #region Prevent Moving Overstacks To Inventory  

            if (lootContainer != null)
            {
                var targetItem = container.GetSlot(targetSlot);
                if (targetItem != null)
                {
                    if (movedItem.parent.playerOwner == player)
                    {
                        if (!movedItem.CanStack(targetItem))
                        {
                            if (targetItem.amount > targetItem.info.stackable)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            #endregion

            return null;
        }
        
        //Hook not implmented, using OnItemDropped for now
        object OnDropItem(PlayerInventory inventory, Item item, int amount)
        {
            return null;
            var player = inventory.GetComponent<BasePlayer>();
            if (inventory.loot.entitySource == null)
            {
                return null;
            }
            if (item.amount > item.info.stackable)
            {
                int loops = Mathf.CeilToInt((float)item.amount / item.info.stackable);
                for (int i = 0; i < loops; i++)
                {
                    if (item.amount <= item.info.stackable)
                    {
                        item.Drop(player.eyes.position, player.eyes.BodyForward() * 4f + Vector3Ex.Range(-1f, 1f));
                        break;
                    }
                    var splitItem = item.SplitItem(item.info.stackable);
                    if (splitItem != null)
                    {
                        splitItem.Drop(player.eyes.position, player.eyes.BodyForward() * 4f + Vector3Ex.Range(-1f, 1f));
                    }
                }
                player.SignalBroadcast(BaseEntity.Signal.Gesture, "drop_item", null);
                return false;
            }
            return null;
        }

        void OnItemDropped(Item item, BaseEntity entity)
        {
            item.RemoveFromContainer();
            int stackSize = item.MaxStackable();
            if (item.amount > stackSize)
            {
                int loops = Mathf.FloorToInt((float)item.amount / stackSize);
                if (loops > 20)
                {
                    return;
                }
                for (int i = 0; i < loops; i++)
                {
                    if (item.amount <= stackSize)
                    {
                        break;
                    }
                    var splitItem = item.SplitItem(stackSize);
                    if (splitItem != null)
                    {
                        splitItem.Drop(entity.transform.position, entity.GetComponent<Rigidbody>().velocity + Vector3Ex.Range(-1f, 1f));
                    }
                }
            }
        }

        #endregion

        public class ItemHelper
        {
            public static bool SplitMoveItem(Item item, int amount, ItemContainer targetContainer, int targetSlot)
            {
                var splitItem = item.SplitItem(amount);
                if (splitItem == null)
                {
                    return false;
                }
                if (!splitItem.MoveToContainer(targetContainer, targetSlot))
                {
                    item.amount += splitItem.amount;
                    splitItem.Remove();
                }
                return true;
            }

            public static bool SplitMoveItem(Item item, int amount, BasePlayer player)
            {
                return SplitMoveItem(item, amount, player.inventory);
            }

            public static bool SplitMoveItem(Item item, int amount, PlayerInventory inventory)
            {
                var splitItem = item.SplitItem(amount);
                if (splitItem == null)
                {
                    return false;
                }
                if (!inventory.GiveItem(splitItem))
                {
                    item.amount += splitItem.amount;
                    splitItem.Remove();
                }
                return true;
            }

            public static void SwapItems(Item item1, Item item2)
            {
                var container1 = item1.parent;
                var container2 = item2.parent;
                var slot1 = item1.position;
                var slot2 = item2.position;
                item1.RemoveFromContainer();
                item2.RemoveFromContainer();
                item1.MoveToContainer(container2, slot2);
                item2.MoveToContainer(container1, slot1);
            }
        }

        public class ConfigData
        {
            public Dictionary<string, float> StackConfig = new Dictionary<string, float>()
            {
                {"cupboard.tool.deployed", 5f }, //Tool Cupboard
                { "campfire",1f }, //Fireplace
                { "lantern.deployed",1f }, //Lantern
                { "box.wooden.large",5f }, //Large wooden box
                { "small_stash_deployed",1f }, //Stash
                { "dropbox.deployed",1f }, //Drop box
                { "woodbox_deployed",2f }, //Small wooden box
                { "vendingmachine.deployed",5f }, //Vending Machine
                { "bbq.deployed",2f }, //BBQ
                { "furnace.large",5f }, //Large furnace
                { "skull_fire_pit",1f }, //Skull fireplace
                { "mailbox.deployed",1f }, //Mailbox
                { "furnace", 5f }, //Small furnace
                { "hopperoutput", 5f }, //Quarry output
                { "fuelstorage", 2f }, //Pumpjack and quarry fuel storage
                { "crudeoutput", 1f }, //Pumpjack output
                { "refinery_small_deployed", 5f }, //Oil Refinery
                { "fireplace.deployed", 2f }, //New Large Fireplace
                { "foodbox", 1f }, //Small food pile
                { "trash-pile-1", 1f}, //Other food pile
                { "supply_drop", 5f }, //Supply Drop 
                { "recycler_static", 1f }, //Recycler
                { "water_catcher_small", 1f }, // Small Water Catcher
                { "water_catcher_large", 1f }, // Large Water Catcher
            };

            public float GetStackSize(BaseEntity entity)
            {
                float amount = 1f;
                if (entity.skinID != 0)
                {
                    if (StackConfig.TryGetValue($"{entity.ShortPrefabName}-{entity.skinID}", out amount))
                    {
                        return amount;
                    }
                }
                if (StackConfig.TryGetValue($"{entity.ShortPrefabName}", out amount))
                {
                    return amount;
                }
                else
                {
                    if (entity is LootContainer)
                    {
                        StackConfig.Add(entity.ShortPrefabName, 1f);
                    }
                    else
                    {
                        _plugin.Puts($"Couldn't find stacksize for {entity.ShortPrefabName}! (Please post in the plugin thread on oxidemod.org and let me know)");
                    }
                    amount = 1f;
                }
                return amount;
            }
        }

        #region PlayerData

        public class BasePlayerData
        {
            [JsonIgnore]
            public BasePlayer Player { get; set; }

            public string userID { get; set; } = "";

            public BasePlayerData()
            {

            }
            public BasePlayerData(BasePlayer player) : base()
            {
                userID = player.UserIDString;
                Player = player;
            }
        }

        public class PlayerDataController<T> where T : BasePlayerData
        {
            [JsonPropertyAttribute(Required = Required.Always)]
            private Dictionary<string, T> playerData { get; set; } = new Dictionary<string, T>();
            private JSONFile<Dictionary<string, T>> _file;
            private Timer _timer;
            public IEnumerable<T> All { get { return playerData.Values; } }

            public PlayerDataController()
            {

            }

            public PlayerDataController(string filename = null)
            {
                if (filename == null)
                {
                    return;
                }
                _file = new JSONFile<Dictionary<string, T>>(filename);
                _timer = _plugin.timer.Every(120f, () =>
                {
                    _file.Save();
                });
            }

            public void Unload()
            {
                if (_file == null)
                {
                    return;
                }
                _file.Save();
            }

            public T Get(string identifer)
            {
                T data;
                if (!playerData.TryGetValue(identifer, out data))
                {
                    data = Activator.CreateInstance<T>();
                    playerData[identifer] = data;
                }
                return data;
            }

            public T Get(ulong userID)
            {
                return Get(userID.ToString());
            }

            public T Get(BasePlayer player)
            {
                var data = Get(player.UserIDString);
                data.Player = player;
                return data;
            }

            public bool Has(ulong userID)
            {
                return playerData.ContainsKey(userID.ToString());
            }

            public void Set(string userID, T data)
            {
                playerData[userID] = data;
            }

            public bool Remove(string userID)
            {
                return playerData.Remove(userID);
            }

            public void Update(T data)
            {
                playerData[data.userID] = data;
            }
        }

        #endregion

        #region Configuration Files

        public enum ConfigLocation
        {
            Data = 0,
            Config = 1,
            Logs = 2,
            Plugins = 3,
            Lang = 4,
            Custom = 5,
        }

        public class JSONFile<Type> where Type : class
        {
            private DynamicConfigFile _file;
            public string _name { get; set; }
            public Type Instance { get; set; }
            private ConfigLocation _location { get; set; }
            private string _path { get; set; }
            public bool SaveOnUnload = false;
            public bool Compressed = false;

            public JSONFile(string name, ConfigLocation location = ConfigLocation.Data, string path = null, string extension = ".json", bool saveOnUnload = false)
            {
                SaveOnUnload = saveOnUnload;
                _name = name.Replace(".json", "");
                _location = location;
                switch (location)
                {
                    case ConfigLocation.Data:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.DataDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Config:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.ConfigDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Logs:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LogDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Lang:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LangDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Custom:
                        {
                            _path = $"{path}/{name}{extension}";
                            break;
                        }
                }
                _file = new DynamicConfigFile(_path);
                _file.Settings = new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                Init();
            }

            public virtual void Init()
            {
                _plugin.OnRemovedFromManager.Add(new Action<Plugin, PluginManager>(Unload));
                Load();
                Save();
                Load();
            }

            public virtual void Load()
            {
                if (Compressed)
                {
                    LoadCompressed();
                    return;
                }

                if (!_file.Exists())
                {
                    Save();
                }
                Instance = _file.ReadObject<Type>();
                if (Instance == null)
                {
                    Instance = Activator.CreateInstance<Type>();
                    Save();
                }
                return;
            }

            private void LoadCompressed()
            {
                string str = _file.ReadObject<string>();
                if (str == null || str == "")
                {
                    Instance = Activator.CreateInstance<Type>();
                    return;
                }
                using (var compressedStream = new MemoryStream(Convert.FromBase64String(str)))
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                using (var resultStream = new MemoryStream())
                {
                    var buffer = new byte[4096];
                    int read;

                    while ((read = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        resultStream.Write(buffer, 0, read);
                    }

                    Instance = JsonConvert.DeserializeObject<Type>(Encoding.UTF8.GetString(resultStream.ToArray()));
                }
            }

            public virtual void Save()
            {
                if (Compressed)
                {
                    SaveCompressed();
                    return;
                }

                _file.WriteObject(Instance);
                return;
            }

            private void SaveCompressed()
            {
                using (var stream = new MemoryStream())
                {
                    using (GZipStream zipStream = new GZipStream(stream, CompressionMode.Compress))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Instance));
                        zipStream.Write(bytes, 0, bytes.Length);
                        zipStream.Close();
                        _file.WriteObject(Convert.ToBase64String(stream.ToArray()));
                    }
                }
            }

            public virtual void Reload()
            {
                Load();
            }

            private void Unload(Plugin sender, PluginManager manager)
            {
                if (SaveOnUnload)
                {
                    Save();
                }
            }
        }

        #endregion
    }
}