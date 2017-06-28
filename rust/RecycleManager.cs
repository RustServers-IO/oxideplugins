using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RecycleManager", "redBDGR", "1.0.6", ResourceId = 2391)]
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
            at.Add("hemp.seed");
            return at;
        }
        static List<object> OutputBlacklist()
        {
            var at = new List<object>();
            at.Add("hemp.seed");
            return at;
        }

        List<object> BlacklistedItems;
        List<object> OutputBlacklistedItems;
        Dictionary<string, object> MultiplyList;

        void LoadVariables()
        {
            BlacklistedItems = (List<object>)GetConfig("Lists", "Input Blacklist", Blacklist());
            recycleTime = Convert.ToSingle(GetConfig("Settings", "Recycle Time", 5.0f));
            MultiplyList = (Dictionary<string, object>)GetConfig("Lists", "Recycle Output Multipliers", Multipliers());
            maxItemsPerRecycle = Convert.ToInt32(GetConfig("Settings", "Max Items Per Recycle", 100));
            OutputBlacklistedItems = (List<object>)GetConfig("Lists", "Output Blacklist", OutputBlacklist());

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
                ["addrecycler CONSOLE invalid syntax"] = "Invalid syntax! addrecycler <playername/id>",
                ["No Player Found"] = "No player was found or they are offline",
                ["AddRecycler CONSOLE success"] = "A recycler was successfully placed at the players location!",

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

        [ConsoleCommand("addrecycler")]
        void AddRecyclerCMDConsole(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Args == null)
            {
                arg.ReplyWith(msg("addrecycler CONSOLE invalid syntax"));
                return;
            }

            if (arg.Connection != null) return;

            if (arg.Args.Length != 1)
            {
                arg.ReplyWith(msg("addrecycler CONSOLE invalid syntax"));
                return;
            }

            BasePlayer target = FindPlayer(arg.Args[0]);

            if (target == null || !target.IsValid())
            {
                arg.ReplyWith(msg("No Player Found"));
                return;
            }

            BaseEntity ent = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", target.transform.position, target.GetEstimatedWorldRotation(), true);
            ent.Spawn();
            arg.ReplyWith(msg("AddRecycler CONSOLE success"));
        }

        void OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (recycler.IsOn()) return;
            recycler.CancelInvoke("RecycleThink");
            timer.Once(0.1f, () => { recycler.Invoke("RecycleThink", recycleTime); });
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
            if (slot.info.Blueprint.ingredients == null || slot.info.Blueprint.ingredients.Count == 0)
            {
                recycler.StopRecycling();
                return true;
            }

            foreach (ItemAmount ingredient in slot.info.Blueprint.ingredients)
            {
                double multi = 1;
                if (MultiplyList.ContainsKey("*"))
                    multi = Convert.ToDouble(MultiplyList["*"]);
                if (MultiplyList.ContainsKey(ingredient.itemDef.shortname))
                    multi = Convert.ToDouble(MultiplyList[ingredient.itemDef.shortname]);

                int outputamount = Convert.ToInt32(usedItems * ((Convert.ToDouble(ingredient.amount) * multi) / 2));
                if (outputamount < 1)
                    continue;
                if (!recycler.MoveItemToOutput(ItemManager.CreateByItemID(ingredient.itemid, outputamount, (ulong)0)))
                    flag = true;
            }
            if (flag || !recycler.HasRecyclable())
            {
                recycler.StopRecycling();
                for (int i = 5; i <= 11; i++)
                {
                    Item _item = recycler.inventory.GetSlot(i);
                    if (_item != null)
                        if (_item.IsValid())
                            if (OutputBlacklistedItems.Contains(_item.info.shortname))
                            {
                                _item.Remove(0f);
                                _item.RemoveFromContainer();
                            }
                }
            }
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

        private static BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrId)
                    return activePlayer;
            }
            return null;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}