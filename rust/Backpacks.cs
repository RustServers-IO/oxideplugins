using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("Backpacks", "LaserHydra", "2.0.4", ResourceId = 1408)]
    [Description("Allows players to have a Backpack which provides them extra inventory space.")]
    internal class Backpacks : RustPlugin
    {
        public static Backpacks Instance;
        private static Dictionary<ulong, Backpack> backpacks = new Dictionary<ulong, Backpack>();

        #region Classes

        private static class Configuration
        {
            public static StorageSize BackpackSize = StorageSize.Middle;

            public static int BackpackSizeInt
            {
                get { return (int) BackpackSize; }
                set { BackpackSize = (StorageSize) value; }
            }
            
            public static string BackpackHotkey = "B";
            public static bool ShowOnBack = true;
            public static bool HideOnBackIfEmpty = true;
            public static bool DropOnDeath = true;
            public static bool EraseOnDeath = false;
        }

        private class StorageCloser : MonoBehaviour
        {
            private Action<BasePlayer> callback;

            public static void Attach(BaseEntity entity, Action<BasePlayer> callback)
                => entity.gameObject.AddComponent<StorageCloser>().callback = callback;

            private void PlayerStoppedLooting(BasePlayer player) => callback(player);
        }

        private class Backpack
        {
            public BackpackInventory Inventory = new BackpackInventory();
            public ulong ownerID;

            private BaseEntity entity;
            private BaseEntity visualEntity;
            private StorageContainer container => entity.GetComponent<StorageContainer>();
            public bool IsOpen => entity != null;

            public Backpack(ulong id)
            {
                ownerID = id;
            }

            public void Drop(Vector3 position)
            {
                if (Inventory.Items.Count > 0)
                {
                    var entity = GameManager.server.CreateEntity(GetContainerPrefab(StorageSize.Small), position);

                    entity.UpdateNetworkGroup();
                    entity.SendNetworkUpdateImmediate();

                    entity.globalBroadcast = true;

                    entity.Spawn();

                    entity.name = "droppedbackpack";

                    StorageContainer container = entity.GetComponent<StorageContainer>();

                    switch (Configuration.BackpackSize)
                    {
                        case StorageSize.Big:
                            container.panelName = "largewoodbox";
                            container.inventorySlots = 30;
                            container.inventory.capacity = 30;
                            break;
                        case StorageSize.Middle:
                            container.panelName = "smallwoodbox";
                            container.inventorySlots = 12;
                            container.inventory.capacity = 12;
                            break;
                        case StorageSize.Small:
                            container.panelName = "smallstash";
                            break;
                    }

                    foreach (var backpackItem in Inventory.Items)
                        backpackItem.ToItem().MoveToContainer(entity.GetComponent<StorageContainer>().inventory);
                }

                Erase();
            }

            public void Erase()
            {
                RemoveVisual();
                Inventory.Items.Clear();
                SaveData(this, $"{Instance.DataFileName}/{ownerID}");
            }

            public void Open(BasePlayer player)
            {
                if (IsOpen)
                {
                    Instance.PrintToChat(player, Instance.lang.GetMessage("Backpack Already Open", Instance, player.UserIDString));
                    return;
                }

                entity = SpawnContainer(Configuration.BackpackSize, player.transform.position - new Vector3(0, 100, 0));

                foreach (var backpackItem in Inventory.Items)
                    backpackItem.ToItem().MoveToContainer(container.inventory);

                PlayerLootContainer(player, entity.GetComponent<StorageContainer>());
                StorageCloser.Attach(entity, Close);
            }

            private void Close(BasePlayer player)
            {
                if (entity != null)
                {
                    Inventory.Items = container.inventory.itemList.Select(BackpackInventory.BackpackItem.FromItem).ToList();

                    entity.Kill();
                    entity = null;
                }

                if (player.userID != ownerID)
                {
                    BasePlayer target = BasePlayer.FindByID(ownerID);

                    if (target != null)
                    {
                        if (Inventory.Items.Count == 0 && Configuration.HideOnBackIfEmpty)
                            RemoveVisual();
                        else if (visualEntity == null)
                            SpawnVisual(target);
                    }
                }
                else
                {
                    if (Inventory.Items.Count == 0 && Configuration.HideOnBackIfEmpty)
                        RemoveVisual();
                    else if (visualEntity == null)
                        SpawnVisual(player);
                }

                SaveData(this, $"{Instance.DataFileName}/{ownerID}");
            }

            public void ForceClose(BasePlayer player) => Close(player);

            public void SpawnVisual(BasePlayer player)
            {
                if (visualEntity != null || !Configuration.ShowOnBack)
                    return;

                var ent = GameManager.server.CreateEntity("assets/prefabs/weapons/satchelcharge/explosive.satchel.deployed.prefab", new Vector3(0, 0.35F, -0.075F), Quaternion.Euler(0, 90, -90));

                ent.SetParent(player);

                ent.UpdateNetworkGroup();
                ent.SendNetworkUpdateImmediate();

                ent.Spawn();

                ent.globalBroadcast = true;

                ent.name = "backpack";
                ent.creatorEntity = player;
                ent.OwnerID = player.userID;

                DudTimedExplosive explosive = ent.GetComponent<DudTimedExplosive>();

                explosive.itemToGive = ItemManager.FindItemDefinition(1916127949);
                explosive.dudChance = 2;
                explosive.CancelInvoke("Explode");
                explosive.CancelInvoke("Kill");
                explosive.SetFlag(BaseEntity.Flags.On, false);
                explosive.SendNetworkUpdate();

                visualEntity = ent;
            }

            public void RemoveVisual()
            {
                if (visualEntity != null)
                {
                    visualEntity.Kill();
                    visualEntity = null;
                }
            }

            public static Backpack LoadOrCreate(ulong id)
            {
                if (backpacks.ContainsKey(id))
                    return backpacks[id];

                Backpack backpack = null;

                if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Instance.DataFileName}/{id}"))
                    LoadData(ref backpack, $"{Instance.DataFileName}/{id}");
                else
                {
                    backpack = new Backpack(id);
                    SaveData(backpack, $"{Instance.DataFileName}/{id}");
                }

                backpacks.Add(id, backpack);

                return backpack;
            }
        }

        private class BackpackInventory
        {
            public List<BackpackItem> Items = new List<BackpackItem>();

            public class BackpackItem
            {
                public int ID;
                public int Amount;
                public ulong Skin;
                public float Fuel;
                public int FlameFuel;
                public float Condition;
                public int Ammo;
                public int AmmoType;

                public List<BackpackItem> Contents = new List<BackpackItem>();

                public Item ToItem()
                {
                    Item item = ItemManager.CreateByItemID(ID, Amount, Skin);
                    BaseProjectile.Magazine magazine = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
                    FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent<FlameThrower>();

                    item.fuel = Fuel;
                    item.condition = Condition;

                    if (Contents != null)
                        foreach (var contentItem in Contents)
                            contentItem.ToItem().MoveToContainer(item.contents);
                    else
                        item.contents = null;

                    if (magazine != null)
                    {
                        magazine.contents = Ammo;
                        magazine.ammoType = ItemManager.FindItemDefinition(AmmoType);
                    }

                    if (flameThrower != null)
                        flameThrower.ammo = FlameFuel;

                    return item;
                }

                public static BackpackItem FromItem(Item item) => new BackpackItem
                {
                    ID = item.info.itemid,
                    Ammo = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.contents ?? 0,
                    AmmoType = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.ammoType?.itemid ?? 0,
                    Amount = item.amount,
                    Condition = item.condition,
                    Fuel = item.fuel,
                    Skin = item.skin,
                    Contents = item.contents?.itemList?.Select(FromItem).ToList(),
                    FlameFuel = item.GetHeldEntity()?.GetComponent<FlameThrower>()?.ammo ?? 0
                };
            }
        }

        public enum StorageSize
        {
            Big = 3,
            Middle = 2,
            Small = 1
        }

        #endregion

        #region Container Related

        public static string GetContainerPrefab(StorageSize size)
        {
            switch (size)
            {
                case StorageSize.Big:
                    return "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
                case StorageSize.Middle:
                    return "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
                case StorageSize.Small:
                    return "assets/prefabs/deployable/small stash/small_stash_deployed.prefab";
            }

            return null;
        }

        private static BaseEntity SpawnContainer(StorageSize size = StorageSize.Middle, Vector3 position = default(Vector3))
        {
            var ent = GameManager.server.CreateEntity(GetContainerPrefab(size), position);

            ent.UpdateNetworkGroup();
            ent.SendNetworkUpdateImmediate();

            ent.globalBroadcast = true;

            ent.Spawn();

            return ent;
        }

        private static void PlayerLootContainer(BasePlayer player, StorageContainer container)
        {
            container.SetFlag(BaseEntity.Flags.Open, true, false);
            player.inventory.loot.StartLootingEntity(container, false);
            player.inventory.loot.AddContainer(container.inventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", container.panelName);
            container.DecayTouch();
            container.SendNetworkUpdate();
        }

        #endregion

        #region Loading

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You don't have permission to use this command.",
                ["Backpack Already Open"] = "Somebody already has this backpack open!"
            }, this);
        }

        private new void LoadConfig()
        {
            Configuration.BackpackSizeInt = GetConfig(Configuration.BackpackSizeInt, "Backpack Size (1-3)");
            GetConfig(ref Configuration.BackpackHotkey, "Backpack Hotkey");

            GetConfig(ref Configuration.DropOnDeath, "Drop On Death");
            GetConfig(ref Configuration.EraseOnDeath, "Erase On Death");

            GetConfig(ref Configuration.ShowOnBack, "Show On Back");
            GetConfig(ref Configuration.HideOnBackIfEmpty, "Hide On Back If Empty");

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");

        #endregion
        
        #region Hooks

        private void Loaded()
        {
            Instance = this;

            LoadConfig();
            LoadMessages();

            permission.RegisterPermission("backpacks.use", this);
            permission.RegisterPermission("backpacks.admin", this);

            foreach (var basePlayer in BasePlayer.activePlayerList)
                OnPlayerInit(basePlayer);
        }

        private void Unloaded()
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
                OnPlayerDisconnected(basePlayer);

            foreach (var ent in Resources.FindObjectsOfTypeAll<TimedExplosive>().Where(ent => ent.name == "backpack"))
                ent.KillMessage();

            foreach (var ent in Resources.FindObjectsOfTypeAll<StorageContainer>().Where(cont => cont.name == "droppedbackpack" && cont.inventory.itemList.Count == 0))
                ent.KillMessage();
        }
        
        private void OnPlayerInit(BasePlayer player)
        {
            Backpack backpack = Backpack.LoadOrCreate(player.userID);

            //=====> Bind hotkey
            player.SendConsoleCommand($"bind {Configuration.BackpackHotkey} backpack.open");

            //=====> Spawn visual backpack
            if (permission.UserHasPermission(player.UserIDString, "backpacks.use") && Configuration.ShowOnBack)
            {
                if (backpack.Inventory.Items.Count == 0 && Configuration.HideOnBackIfEmpty)
                    return;

                backpack.SpawnVisual(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            player.SendConsoleCommand($"bind {Configuration.BackpackHotkey} \"\"");

            if (backpacks.ContainsKey(player.userID))
                backpacks.Remove(player.userID);
        }

        private void OnUserPermissionGranted(string id, string perm)
        {
            if (perm == "backpacks.use" && Configuration.ShowOnBack)
            {
                BasePlayer player = BasePlayer.Find(id);
                Backpack backpack = Backpack.LoadOrCreate(player.userID);

                if (backpack.Inventory.Items.Count == 0 && Configuration.HideOnBackIfEmpty)
                    return;

                backpack.SpawnVisual(player);
            }
        }

        private void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            if (victim is BasePlayer)
            {
                BasePlayer player = (BasePlayer) victim;
                Backpack backpack = Backpack.LoadOrCreate(player.userID);

                backpack.ForceClose(player);

                if (Configuration.EraseOnDeath)
                    backpack.Erase();
                else if (Configuration.DropOnDeath)
                    backpack.Drop(player.transform.position);
            }
        }

        #endregion

        #region Commands

        [ConsoleCommand("backpack.open")]
        private void BackpackOpenCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;

            BasePlayer player = arg.Player();

            if (permission.UserHasPermission(player.UserIDString, "backpacks.use"))
                Backpack.LoadOrCreate(player.userID).Open(player);
        }

        [ChatCommand("viewbackpack")]
        private void ViewBackpack(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "backpacks.admin"))
            {
                PrintToChat(player, lang.GetMessage("No Permission", this, player.UserIDString));
                return;
            }

            if (args.Length != 1)
            {
                PrintToChat(player, "Syntax: /viewbackpack <steamid>");
                return;
            }

            ulong id;

            if (!ulong.TryParse(args[0], out id) || !args[0].StartsWith("7656119") || args[0].Length != 17)
            {
                PrintToChat(player, $"{args[0]} is not a valid SteamID (64)!");
                return;
            }

            Backpack backpack = Backpack.LoadOrCreate(id);

            if (backpack.IsOpen)
            {
                PrintToChat(player, lang.GetMessage("Backpack Already Open", this, player.UserIDString));
                return;
            }

            timer.Once(0.5f, () => backpack.Open(player));
        }

        #endregion
        
        #region Data & Config Helper

        private static void GetConfig<T>(ref T variable, params string[] path) => variable = GetConfig(variable, path);

        private static T GetConfig<T>(T defaultValue, params string[] path)
        {
            if (path.Length == 0)
                return defaultValue;

            if (Instance.Config.Get(path) == null)
            {
                Instance.Config.Set(path.Concat(new object[] { defaultValue }).ToArray());
                Instance.PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            return (T)Convert.ChangeType(Instance.Config.Get(path), typeof(T));
        }

        private string DataFileName => Title.Replace(" ", "");

        private static void LoadData<T>(ref T data, string filename = null) => data = Core.Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? Instance.DataFileName);

        private static void SaveData<T>(T data, string filename = null) => Core.Interface.Oxide.DataFileSystem.WriteObject(filename ?? Instance.DataFileName, data);

        #endregion
    }
}