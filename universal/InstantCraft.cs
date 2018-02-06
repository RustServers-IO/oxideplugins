// Reference: Rust.Workshop
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("InstantCraft", "Vlad-00003", "1.2.3", ResourceId = 2409)]
    [Description("Instant craft items(includes normalspeed list and blacklist)")]

    class InstantCraft : RustPlugin
    {
        #region Config setup
                
        private string Prefix = "[InstantCraft]";
        private string PrefixColor = "#42d4f4";
        private List<string> BlockedItems = new List<string>();
        private List<string> NormalSpeed = new List<string>();
        private bool SplitStacks = true;
        private bool RandomizeSkins = false;

        #endregion

        #region Initializing

        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created.");
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            bool changed = false;
            List<object> blockedItems = new List<object>();
            List<object> normalSpeed = new List<object>() { "Hammer", "Rock" };
            if (GetConfig("Prefix", ref Prefix))
            {
                PrintWarning("Oprion \"Prefix\" was added to the config");
                changed = true;
            }
            if (GetConfig("Prefix Color", ref PrefixColor))
            {
                PrintWarning("Oprion \"Prefix Color\" was added to the config");
                changed = true;
            }
            if (GetConfig("Split Stacks", ref SplitStacks))
            {
                PrintWarning("Oprion \"Split Stacks\" was added to the config");
                changed = true;
            }
            if (GetConfig("Blocked item list", ref blockedItems))
            {
                PrintWarning("Oprion \"Blocked item list\" was added to the config");
                changed = true;
            }
            if (GetConfig("Normal Speed", ref normalSpeed))
            {
                PrintWarning("Oprion \"Normal Speed\" was added to the config");
                changed = true;
            }
            if (GetConfig("Randomize item skins if skin is zero", ref RandomizeSkins))
            {
                PrintWarning("Oprion \"Randomize item skins if skin is zero\" was added to the config");
                changed = true;
            }
            if (changed)
                SaveConfig();
            BlockedItems = blockedItems.Select(i => (string)i).ToList();
            NormalSpeed = normalSpeed.Select(i => (string)i).ToList();
        }
        protected override void LoadDefaultMessages()
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
            webrequest.Enqueue("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", null, GetWorkshopIDs, this, Core.Libraries.RequestMethod.GET);
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
                if(task.skinID == 0 && RandomizeSkins)
                {
                    task.skinID = GetSkinsInt(task.blueprint.targetItem).GetRandom();
                }
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
                    GiveItem(player, task.blueprint.targetItem, cancraft * task.blueprint.amountToCreate, (ulong)task.skinID);
                    RefundIngredients(task.blueprint, player, refund);
                    task.cancelled = true;
                    return null;
                }
                GiveItem(player, task.blueprint.targetItem, amount * task.blueprint.amountToCreate, (ulong)task.skinID);
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
                        GiveItem(player, task.blueprint.targetItem, stacks.ElementAt(i), (ulong)task.skinID);
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
                        GiveItem(player, task.blueprint.targetItem, stack_amount, (ulong)task.skinID);
                    }
                    task.cancelled = true;
                    return null;
                }
            }
            //player.GiveItem(ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, finalamount, (ulong)task.skinID));
            GiveItem(player, task.blueprint.targetItem, finalamount, (ulong)task.skinID);
            task.cancelled = true;
            return null;
        }
        #endregion

        #region Skins
        private class Skin
        {
            public string itemshortname;
            public uint itemdefid;
            public ulong workshopdownload;
        }
        //Dictionary<ulong, ulong> SchemaSkins = new Dictionary<ulong, ulong>();
        private List<Skin> SchemaSkins = new List<Skin>();
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
                    //SchemaSkins.Add(item.itemdefid, WsSID);
                    SchemaSkins.Add(new Skin()
                    {
                        itemshortname = item.itemshortname,
                        itemdefid = item.itemdefid,
                        workshopdownload = WsSID
                    });
                }
                Puts($"Pulled {SchemaSkins.Count} skins.");
            }
            else
            {
                PrintWarning($"Failed to pull skins... Error {code}");
            }
        }
        private void GiveItem(BasePlayer player, ItemDefinition def, int amount, ulong skinid)
        {
            Item i;
            if (!player.IsConnected) return;
            var skin = SchemaSkins.FirstOrDefault(x => x.itemdefid == skinid);
            if(skinid != 0 && skin != null && skin.workshopdownload != 0)
            {
                i = ItemManager.Create(def, amount, skin.workshopdownload);
            }
            else
            {
                if (skinid == 0 && RandomizeSkins)
                {
                    skinid = GetSkins(def).GetRandom();
                }
                i = ItemManager.Create(def, amount, skinid);
            }
            if (i != null)
                player.GiveItem(i, BaseEntity.GiveItemReason.Crafted);
        }
        List<ulong> GetSkins(ItemDefinition def)
        {
            List<ulong> skins = new List<ulong>();
            var SchemaSkin = SchemaSkins.Where(s => s.itemshortname == def.shortname).Select(x => x.workshopdownload);
            if(SchemaSkin != null)
                skins.AddRange(SchemaSkin);
            if (def.skins != null)
                skins.AddRange(def.skins.Select(skin => Convert.ToUInt64(skin.id)));
            if (def.skins2 != null)
                skins.AddRange(def.skins2.Select(skin => Convert.ToUInt64(skin.Id)));
            return skins;

        }
        List<int> GetSkinsInt(ItemDefinition def)
        {
            List<int> skins = new List<int>();
            var SchemaSkin = SchemaSkins.Where(s => s.itemshortname == def.shortname).Select(x => Convert.ToInt32(x.itemdefid));
            if (SchemaSkin != null)
                skins.AddRange(SchemaSkin);
            if (def.skins != null)
                skins.AddRange(def.skins.Select(skin => skin.id));
            if (def.skins2 != null)
                skins.AddRange(def.skins2.Select(skin => skin.Id));
            return skins;

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
        //Get the msg form lang API
        string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());
        private bool GetConfig<T>(string Key, ref T var)
        {
            if (Config[Key] != null)
            {
                var = (T)Convert.ChangeType(Config[Key], typeof(T));
                return false;
            }
            Config[Key] = var;
            return true;
        }
        int FreeSlots(BasePlayer player) => 30 - player.inventory.containerMain.itemList.Count - player.inventory.containerBelt.itemList.Count;
        #endregion

    }
}
