// Reference: Rust.Workshop
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("InstantCraft", "Vlad-00003", "1.1.2", ResourceId = 2409)]
    [Description("Instant craft items(includes normalspeed list and blacklist)")]

    class InstantCraft : RustPlugin
    {
        #region Config setup
                
        private string Prefix = "[InstantCraft]";
        private string PrefixColor = "#42d4f4";
        private List<string> BlockedItems = new List<string>();
        private List<string> NormalSpeed = new List<string>();
        private bool SplitStacks = true;

        #endregion

        #region Initializing

        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created.");
        }
        void LoadConfigValues()
        {
            List<object> blockedItems = new List<object>();
            List<object> normalSpeed = new List<object>() { "Hammer", "Rock" };
            GetConfig("Prefix", ref Prefix);
            GetConfig("Prefix Color", ref PrefixColor);
            GetConfig("Split Stacks", ref SplitStacks);
            GetConfig("Blocked item list", ref blockedItems);
            GetConfig("Normal Speed", ref normalSpeed);
            SaveConfig();
            BlockedItems = blockedItems.Select(i => (string)i).ToList();
            NormalSpeed = normalSpeed.Select(i => (string)i).ToList();
        }

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"InvFull","Your <color=yellow>inventory</color> is <color=red>full!</color>" },
                {"NormalSpeed","This item <color=red>was removed</color> frome InstaCraft and will be crafted with <color=yellow>normal</color> speed." },
                {"Blocked","This items is <color=red>blocked</color> from crafting" },
                {"NotEnoughtSlots","<color=red>Not enought slots</color> in the inventory! Created <color=green>{0}</color>/<color=green>{1}</color>" }
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"InvFull","В вашем <color=yellow>инвентаре</color> <color=red>нет свободного места!</color>" },
                {"NormalSpeed","Данный предмет <color=red>убран</color> из мнгновенного крафта и будет создаваться с <color=yellow>обычной</color> скоростью." },
                {"Blocked","Крафт данного предмета <color=red>запрещён</color>" },
                {"NotEnoughtSlots","<color=red>Недостаточно слотов</color> для крафта! Создано <color=green>{0}</color>/<color=green>{1}</color>" }
            }, this, "ru");
        }

        void Init()
        {
            LoadMessages();
            LoadConfigValues();
            webrequest.EnqueueGet("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", GetWorkshopIDs, this);
        }

        #endregion

        #region Main function
        private object OnItemCraft(ItemCraftTask task)
        {
            var player = task.owner;
            int finalamount = 0;
            bool lastslot = false;
            int refund;
            int amount = task.amount;
            int invAmount = player.inventory.GetAmount(task.blueprint.targetItem.itemid);

            if (task.blueprint.targetItem.shortname == "door.key")
            {
                return null;
            }
            if (FreeSlots(player) <= 0)
            {
                if (invAmount == 0 || invAmount >= task.blueprint.targetItem.stackable)
                {
                    task.cancelled = true;
                    RefundIngredients(task.blueprint, player, task.amount);

                    SendToChat(player, GetMsg("InvFull", player.UserIDString));
                    return null;
                }
                lastslot = true;
            }

            if (BlockedItems.Contains(task.blueprint.targetItem.displayName.english) || BlockedItems.Contains(task.blueprint.targetItem.shortname))
            {
                task.cancelled = true;
                RefundIngredients(task.blueprint, player, task.amount);

                SendToChat(player, GetMsg("Blocked", player.UserIDString));
                return null;
            }

            if (NormalSpeed.Contains(task.blueprint.targetItem.displayName.english) || NormalSpeed.Contains(task.blueprint.targetItem.shortname))
            {
                SendToChat(player, GetMsg("NormalSpeed", player.UserIDString));
                return null;
            }

            task.endTime = 1f;
            if (lastslot)
            {
                var spaceleft = task.blueprint.targetItem.stackable - invAmount;
                int cancraft = spaceleft / task.blueprint.amountToCreate;
                refund = amount - cancraft;
                if (refund > 0)
                {
                    string reply = string.Format(GetMsg("NotEnoughtSlots", player.userID), cancraft, amount);
                    SendToChat(player, reply);
                    GiveItem(player, task.blueprint.targetItem.itemid, cancraft * task.blueprint.amountToCreate, (ulong)task.skinID);
                    RefundIngredients(task.blueprint, player, refund);
                    task.cancelled = true;
                    return null;
                }
                GiveItem(player, task.blueprint.targetItem.itemid, amount * task.blueprint.amountToCreate, (ulong)task.skinID);
                task.cancelled = true;
                return null;
            }
            finalamount = amount * task.blueprint.amountToCreate;
            var stacks = CalculateStacks(finalamount, task.blueprint.targetItem);
            if(SplitStacks || task.blueprint.targetItem.stackable == 1)
            {
                if(stacks.Count() > FreeSlots(player))
                {
                    int refund_stacks = stacks.Count() - FreeSlots(player) - 1;
                    int refund_amount = refund_stacks * stacks.ElementAt(0) + stacks.Last();
                    refund = refund_amount / task.blueprint.amountToCreate;
                    int iter = FreeSlots(player);
                    int created=0;
                    for(int i = 0; i < iter; i++)
                    {
                        //player.GiveItem(ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, stacks.ElementAt(i), (ulong)task.skinID));
                        GiveItem(player, task.blueprint.targetItem.itemid, stacks.ElementAt(i), (ulong)task.skinID);
                        created += stacks.ElementAt(i);
                    }
                    RefundIngredients(task.blueprint, player, refund);
                    string reply = string.Format(GetMsg("NotEnoughtSlots", player.userID), created, amount * task.blueprint.amountToCreate);
                    SendToChat(player, reply);
                    task.cancelled = true;
                    return null;
                }
                if(stacks.Count() > 1)
                {
                    foreach(var stack_amount in stacks)
                    {
                        //player.GiveItem(ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, stack_amount, (ulong)task.skinID));
                        GiveItem(player, task.blueprint.targetItem.itemid, stack_amount, (ulong)task.skinID);
                    }
                    task.cancelled = true;
                    return null;
                }
            }
            //player.GiveItem(ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, finalamount, (ulong)task.skinID));
            GiveItem(player, task.blueprint.targetItem.itemid, finalamount, (ulong)task.skinID);
            task.cancelled = true;
            return null;
        }
        #endregion

        #region Skin Fix Attempt
        Dictionary<ulong, ulong> SchemaSkins = new Dictionary<ulong, ulong>();
        private void GetWorkshopIDs(int code, string response)
        {
            if (response != null && code == 200)
            {
                SchemaSkins.Clear();
                ulong WsSID;
                var schema = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
                foreach (var item in schema.items)
                {
                    if (string.IsNullOrEmpty(item.itemshortname)) continue;
                    if (item.workshopdownload == null) { WsSID = 0; } else { WsSID = Convert.ToUInt64(item.workshopdownload); }
                    SchemaSkins.Add(item.itemdefid, WsSID);
                }
                Puts($"Pulled {SchemaSkins.Count} skins.");
            }
            else
            {
                PrintWarning($"Failed to pull skins... Error {code}");
            }
        }
        private void GiveItem(BasePlayer player, int itemid, int amount, ulong skinid)
        {
            Item i;
            if (!player.IsConnected) return;
            if (skinid != 0 && SchemaSkins.ContainsKey(skinid) && SchemaSkins[skinid] != 0) { i = ItemManager.CreateByItemID(itemid, amount, SchemaSkins[skinid]); }
            else { i = ItemManager.CreateByItemID(itemid, amount, skinid); }
            if (i != null)
                player.GiveItem(i, BaseEntity.GiveItemReason.Crafted);
        }
        #endregion

        #region Helpers
        //Thanks Norn for this functions and his MagicCraft plugin!
        private IEnumerable<int> CalculateStacks(int amount, ItemDefinition item)
        {
            var results = Enumerable.Repeat(item.stackable, amount / item.stackable); if (amount % item.stackable > 0) { results = results.Concat(Enumerable.Repeat(amount % item.stackable, 1)); }
            return results;
        }
        private void RefundIngredients(ItemBlueprint bp, BasePlayer player, int amount = 1)
        {
            using (List<ItemAmount>.Enumerator enumerator = bp.ingredients.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    ItemAmount current = enumerator.Current;
                    Item i = ItemManager.CreateByItemID(current.itemid, Convert.ToInt32(current.amount) * amount);
                    if (!i.MoveToContainer(player.inventory.containerMain)) { i.Drop(player.eyes.position, player.eyes.BodyForward() * 2f); }
                }
            }
        }

        //Sends the message to the player chat with prefix
        private void SendToChat(BasePlayer Player, string Message)
        {
            PrintToChat(Player, "<color=" + PrefixColor + ">" + Prefix + "</color> " + Message);
        }

        //Sends the message to the whole chat with prefix
        private void SendToChat(string Message)
        {
            PrintToChat("<color=" + PrefixColor + ">" + Prefix + "</color> " + Message);
        }

        //Get the msg form lang API
        string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());
        private void GetConfig<T>(string Key, ref T var)
        {
            if (Config[Key] != null)
            {
                var = (T)Convert.ChangeType(Config[Key], typeof(T));
            }
            Config[Key] = var;
        }
        int FreeSlots(BasePlayer player) => 30 - player.inventory.containerMain.itemList.Count - player.inventory.containerBelt.itemList.Count;
        #endregion

    }
}