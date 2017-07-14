using System.Collections.Generic;
using System;

namespace Oxide.Plugins
{
    [Info("AntiItems", "redBDGR", "1.0.9", ResourceId = 2405)]
    [Description("Remove the need for certain items in crafting and repairing")]

    class AntiItems : RustPlugin
    {
        private bool Changed = false;
        private string permissionName = "antiitems.use";
        private bool useActiveRefreshing = true;
        private float refreshTime = 120f;

        Dictionary<string, object> doComponentList()
        {
            var x = new Dictionary<string, object>();
            x.Add("propanetank", 1000);
            x.Add("gears", 1000);
            x.Add("metalpipe", 1000);
            x.Add("metalspring", 1000);
            x.Add("riflebody", 1000);
            x.Add("roadsigns", 1000);
            x.Add("rope", 1000);
            x.Add("semibody", 1000);
            x.Add("sewingkit", 1000);
            x.Add("smgbody", 1000);
            x.Add("tarp", 1000);
            x.Add("techparts", 1000);
            x.Add("sheetmetal", 1000);
            return x;
        }
        Dictionary<string, object> componentList;

        void OnPlayerInit(BasePlayer player) => timer.Once(5f, () => DoItems(player));
        void OnPlayerDisconnected(BasePlayer player, string reason) => RemoveItems(player, player.inventory.containerMain);
        void OnPlayerRespawned(BasePlayer player) => DoItems(player);

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
            timer.Repeat(refreshTime, 0, () =>
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    if (permission.UserHasPermission(player.UserIDString, permissionName))
                        RefreshItems(player);
            });
        }   

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity is BasePlayer)) return;
                RemoveItems(entity as BasePlayer, (entity as BasePlayer).inventory.containerMain);
        }

        void OnItemCraftCancelled(ItemCraftTask task)
        {
            foreach (var entry in task.takenItems)
                if (componentList.ContainsKey(entry.info.shortname))
                    timer.Once(0.01f, () => entry.RemoveFromContainer());
        }

        void RefreshItems(BasePlayer player)
        {
            for (int i = 0; i < componentList.Count; i++)
                if (player.inventory.containerMain.GetSlot(24 + i) != null)
                    player.inventory.containerMain.GetSlot(24 + i).RemoveFromContainer();
            DoItems(player);
        }

        void DoItems(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName)) return;
            player.inventory.containerMain.capacity = 24 + componentList.Count;
            List<string> y = new List<string>();
            foreach (var key in componentList)
                y.Add(key.Key);
            for (int i = 0; i < componentList.Count; i++)
            {
                Item item = ItemManager.CreateByName(y[i], Convert.ToInt32(componentList[y[i]]));
                item.MoveToContainer(player.inventory.containerMain, 24 + i, true);
            }
        }

        void RemoveItems(BasePlayer player, ItemContainer container)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName)) return;
            List<Item> x = new List<Item>();

            for (int i = 0; i < container.itemList.Count; i++)
            {
                Item item = container.itemList[i];
                if (item == null) return;
                if (componentList.ContainsKey(item.info.shortname))
                    x.Add(item);
            }
            foreach (var key in x)
            {
                key.RemoveFromContainer();
                key.Remove(0.1f);
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            componentList = (Dictionary<string, object>)GetConfig("Settings", "Components", doComponentList());
            useActiveRefreshing = Convert.ToBoolean(GetConfig("Settings", "Use Active Item Refreshing", true));
            refreshTime = Convert.ToSingle(GetConfig("Settings", "Refresh Time", 120f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
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
    }
}