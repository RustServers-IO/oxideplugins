using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("RecycleManager", "redBDGR", "1.0.2", ResourceId = 2391)]
    [Description("Easily change features about the recycler")]

    class RecycleManager : RustPlugin
    {
        bool Changed = false;

        public float recycleTime = 5.0f;
        const string permissionNameADMIN = "recyclemanager.admin";
        int maxItemsPerRecycle = 100;

        static Dictionary<string, object> Multipliers()
        {
            var at = new Dictionary<string, object>();
            at.Add("*", 1);
            at.Add("metal.refined", 1);
            return at;
        }
        static List<object> Blacklist()
        {
            var at = new List<object>();
            at.Add("explosive.timed");
            return at;
        }

        List<object> BlacklistedItems;
        Dictionary<string, object> MultiplyList;

        void LoadVariables()
        {
            BlacklistedItems = (List<object>)GetConfig("Lists", "Blacklisted Items", Blacklist());
            recycleTime = Convert.ToSingle(GetConfig("Settings", "Recycle Time", 5.0f));
            MultiplyList = (Dictionary<string, object>)GetConfig("Lists", "Recycle Output Multipliers", Multipliers());
            maxItemsPerRecycle = Convert.ToInt32(GetConfig("Settings", "Max Items Per Recycle", 100));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionNameADMIN, this);
        }

        void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permissions"] = "You cannot use this command!",

            }, this);
        }

        [ChatCommand("addrecycler")]
        void AddRecyclerCMD(BasePlayer player, string command, String[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("No Permissions", player.UserIDString));
                return;
            }

            BaseEntity ent = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", player.transform.position, player.GetEstimatedWorldRotation(), true);
            ent.Spawn();
            return;
        }

        void OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (recycler.IsOn()) return;
            recycler.CancelInvoke("RecycleThink");
            timer.Once(0.1f, () => { recycler.InvokeRepeating("RecycleThink", recycleTime, recycleTime); });
        }

        bool CanRecycle(Recycler recycler, Item item)
        {
            if (BlacklistedItems.Contains(item.info.shortname)) return false;
            return true;
        }

        object OnRecycleItem(Recycler recycler, Item item)
        {
            Item slot = item;
            double itemamount = Convert.ToDouble(slot.amount);
            bool flag = false;
            int usedItems = 1;

            if (slot.amount > 1)
                usedItems = slot.amount;
            if (usedItems > maxItemsPerRecycle)
                usedItems = maxItemsPerRecycle;

            slot.UseItem(usedItems);

            foreach (ItemAmount ingredient in slot.info.Blueprint.ingredients)
            {
                double multi = 1;
                if (MultiplyList.ContainsKey("*"))
                    multi = Convert.ToDouble(MultiplyList["*"]);
                if (MultiplyList.ContainsKey(ingredient.itemDef.shortname))
                    multi = Convert.ToDouble(MultiplyList[ingredient.itemDef.shortname]);

                int outputamount = Convert.ToInt32(usedItems * ((Convert.ToDouble(ingredient.amount) * multi) / 2));

                if (!recycler.MoveItemToOutput(ItemManager.CreateByItemID(ingredient.itemid, outputamount, (ulong)0)))
                    flag = true;
            }
            if (flag || !recycler.HasRecyclable())
                recycler.StopRecycling();
            return true;
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

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}