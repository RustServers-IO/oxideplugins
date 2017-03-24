// Reference: Rust.Workshop
using System;
using System.Collections.Generic;
using System.Linq;
using Rust;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Reflection;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("MagicCraft", "Norn", "0.3.0", ResourceId = 1347)]
    [Description("An alternative crafting system.")]
    public class MagicCraft : RustPlugin
    {
        int MaxB = 999;
        int MinB = 1;
        int MAX_INV_SLOTS = 30;
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        [PluginReference]
        Plugin PopupNotifications;
        class StoredData
        {
            public Dictionary<string, CraftInfo> CraftList = new Dictionary<string, CraftInfo>();
            public StoredData()
            {
            }
        }
        class CraftInfo
        {
            public int MaxBulkCraft;
            public int MinBulkCraft;
            public string displayName;
            public string shortName;
            public string description;
            public bool Enabled;
            public CraftInfo()
            {
            }
        }
        StoredData storedData;
        private void ConfigurationCheck()
        {
            try { if (Config.Count() == 0) LoadDefaultConfig(); } catch { Puts("Configuration file seems to be unreadable... Re-generating."); LoadDefaultConfig(); }
        }
        void Loaded()
        {
            permission.RegisterPermission("MagicCraft.able", this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CraftSuccess"] = "You have crafted <color=#66FF66>{0}</color> <color=#66FFFF>{1}</color>\n[Batch Amount: <color=#66FF66>{2}</color>]",
                ["DifferentSlots"] = "You <color=yellow>only</color> have <color=green>{0}</color> slots left, crafting <color=green>{1}</color> / <color=red>{2}</color> requested.",
                ["InventoryFull"] = "Your <color=yellow>inventory</color> is <color=red>full</color>!",
                ["InventoryFullBypass"] = "Magic Craft has been <color=red>bypassed</color> because your <color=yellow>inventory</color> is <color=red>full</color>!",
                ["InventoryFullBypassStack"] = "Magic Craft has been <color=red>bypassed</color>!\nYou need <color=red>{0}</color> inventory slots free to craft <color=yellow>{1} {2}</color>.",
                ["CooldownEnabled"] = "Magic Craft has been <color=red>bypassed</color> because you're crafting <color=red>too fast</color>!"
            }, this);
        }
        private void OnServerInitialized()
        {
            storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Title);
            ConfigurationCheck();
            webrequest.EnqueueGet("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", PullWorkshopIDS, this);
            if (Config["Messages", "ItemFailed"] == null) { Puts("Updating configuration..."); LoadDefaultConfig(); }
            GenerateItems();
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Generating Magic Craft configuration...");
            Config.Clear();
            // --- [ SETTINGS ] ---
            Config["Settings", "BypassInvFull"] = true;
            // --- [ COOLDOWN ] ---
            Config["Cooldown", "Enabled"] = true;
            Config["Cooldown", "Timer"] = 4;
            Config["Cooldown", "Trigger"] = 499;
            // --- [ Dependencies ] ---
            Config["Dependencies", "PopupNotifications"] = false;
            // --- [ Messages ] ---
            Config["Messages", "Enabled"] = true;
            Config["Messages", "ItemCrafted"] = false;
            Config["Messages", "ItemFailed"] = true;
            timer.Once(10, () => GenerateItems());
            SaveConfig();
        }

        void GenerateItems()
        {
            mcITEMS = ItemManager.itemList.ToDictionary(i => i.shortname);
            int loaded = 0, enabled = 0;
            foreach (var definition in mcITEMS)
            {
                if (definition.Value.shortname.Length >= 1)
                {
                    CraftInfo p = null;
                    if (storedData.CraftList.TryGetValue(definition.Value.shortname, out p))
                    {
                        if (p.Enabled) { enabled++; }
                        loaded++;
                    }
                    else
                    {
                        CraftInfo z = new CraftInfo();
                        z.description = definition.Value.displayDescription.english.ToString();
                        z.displayName = definition.Value.displayName.english.ToString();
                        z.shortName = definition.Value.shortname.ToString();
                        z.MaxBulkCraft = MaxB;
                        z.MinBulkCraft = MinB;
                        z.Enabled = false;
                        storedData.CraftList.Add(definition.Value.shortname.ToString(), z);
                        loaded++;
                    }
                }
            }
            int inactive = loaded - enabled;
            Puts("Loaded " + loaded.ToString() + " items. (Enabled: " + enabled.ToString() + " | Inactive: " + inactive.ToString() + ").");
            Interface.GetMod().DataFileSystem.WriteObject(this.Title, storedData);
        }
        public static Int32 UnixTimeStampUTC()
        {
            Int32 unixTimeStamp;
            DateTime currentTime = DateTime.Now;
            DateTime zuluTime = currentTime.ToUniversalTime();
            DateTime unixEpoch = new DateTime(1970, 1, 1);
            unixTimeStamp = (Int32)(zuluTime.Subtract(unixEpoch)).TotalSeconds;
            return unixTimeStamp;
        }
        public int InventorySlots(BasePlayer player, bool incwear = true, bool incbelt = true)
        {
            List<Item> list = new List<Item>();
            list.AddRange(player.inventory.containerMain.itemList);                     // 24
            if (incbelt) { list.AddRange(player.inventory.containerBelt.itemList); }    // 6
            if (incwear) { list.AddRange(player.inventory.containerWear.itemList); }    // 6
            return list.Count;
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (BulkCraftCooldown.ContainsKey(player)) { BulkCraftCooldown.Remove(player); }
        }
        List<string> ExcludeList = new List<string>()
        {
            {
                "door.key"
            },
        };
        public int FreeInventorySlots(BasePlayer player, bool incwear = true, bool incbelt = true) { return MAX_INV_SLOTS - InventorySlots(player, false, true); }
        private Dictionary<string, ItemDefinition> mcITEMS;
        private Dictionary<BasePlayer, Int32> BulkCraftCooldown = new Dictionary<BasePlayer, Int32>();

        private object OnItemCraft(ItemCraftTask task, BasePlayer crafter)
        {
            if (permission.UserHasPermission(crafter.net.connection.userid.ToString(), "MagicCraft.able"))
            {
                var itemname = task.blueprint.targetItem.shortname.ToString();
                foreach (var entry in storedData.CraftList)
                {
                    if (entry.Value.shortName == itemname && entry.Value.Enabled && !ExcludeList.Contains(itemname))
                    {
                        if (Convert.ToBoolean(Config["Settings", "BypassInvFull"]) && InventorySlots(crafter, false, true) >= MAX_INV_SLOTS)
                        {
                            if (Convert.ToBoolean(Config["Messages", "Enabled"]) && Convert.ToBoolean(Config["Messages", "ItemFailed"])) { PrintToChatEx(crafter, Lang("InventoryFullBypass", crafter.UserIDString)); }
                            return null;
                        }
                        if (Convert.ToBoolean(Config["Cooldown", "Enabled"]))
                        {
                            if (task.amount >= Convert.ToInt32(Config["Cooldown", "Trigger"]))
                            {
                                if (!BulkCraftCooldown.ContainsKey(crafter))
                                {
                                    BulkCraftCooldown.Add(crafter, UnixTimeStampUTC());
                                }
                                else
                                {
                                    if (UnixTimeStampUTC() - BulkCraftCooldown[crafter] >= Convert.ToInt32(Config["Cooldown", "Timer"]))
                                    {
                                        BulkCraftCooldown[crafter] = UnixTimeStampUTC();
                                    }
                                    else
                                    {
                                        if (Convert.ToBoolean(Config["Messages", "Enabled"]) && Convert.ToBoolean(Config["Messages", "ItemFailed"])) { PrintToChatEx(crafter, Lang("CooldownEnabled", crafter.UserIDString)); }
                                        return null;
                                    }
                                }
                            }
                        }
                        int amount = task.amount;
                        if (amount < entry.Value.MinBulkCraft || amount > entry.Value.MaxBulkCraft) { return null; }
                        ItemDefinition item = GetItem(itemname);
                        int final_amount = task.blueprint.amountToCreate * amount;
                        var results = CalculateStacks(final_amount, item);
                        if (results.Count() > 1)
                        {
                            if (Convert.ToBoolean(Config["Settings", "BypassInvFull"]) && InventorySlots(crafter, false, true) + results.Count() >= MAX_INV_SLOTS) { if (Convert.ToBoolean(Config["Messages", "Enabled"]) && Convert.ToBoolean(Config["Messages", "ItemFailed"])) { PrintToChatEx(crafter, Lang("InventoryFullBypassStack", crafter.UserIDString, results.Count(), final_amount.ToString(), item.displayName.english)); } return null; }
                            foreach (var stack_amount in results) { SAFEGiveItem(crafter, item.itemid, (ulong)task.skinID, (int)stack_amount); }
                        }
                        else { SAFEGiveItem(crafter, item.itemid, (ulong)task.skinID, final_amount); }
                        if (Convert.ToBoolean(Config["Messages", "Enabled"]) && Convert.ToBoolean(Config["Messages", "ItemCrafted"]))
                        {
                            string returnstring = null;
                            returnstring = Lang("CraftSuccess", crafter.UserIDString, amount.ToString(), item.displayName.english.ToString(), final_amount.ToString());
                            PrintToChatEx(crafter, returnstring);
                        }
                        crafter.Command("note.inv", new object[] { item.itemid, final_amount });
                        return false;
                    }
                }
            }
            return null;
        }
        private IEnumerable<int> CalculateStacks(int amount, ItemDefinition item)
        {
            var results = Enumerable.Repeat(item.stackable, amount / item.stackable); if (amount % item.stackable > 0) { results = results.Concat(Enumerable.Repeat(amount % item.stackable, 1)); }
            return results;
        }
        private bool SAFEGiveItem(BasePlayer player, int itemid, ulong skinid, int amount)
        {
            Item i;
            if (!player.IsConnected) return false;
            if (skinid != 0 && SkinFromInventoryID.ContainsKey(skinid) && SkinFromInventoryID[skinid] != 0) { i = ItemManager.CreateByItemID(itemid, amount, SkinFromInventoryID[skinid]); }
            else { i = ItemManager.CreateByItemID(itemid, amount, skinid); }
            if (i != null) if (!i.MoveToContainer(player.inventory.containerMain) && !i.MoveToContainer(player.inventory.containerBelt)) { i.Drop(player.eyes.position, player.eyes.BodyForward() * 2f); }
            return true;
        }
        Dictionary<ulong, ulong> SkinFromInventoryID = new Dictionary<ulong, ulong>();
        private void PullWorkshopIDS(int code, string response)
        {
            if (response != null && code == 200)
            {
                SkinFromInventoryID.Clear();
                ulong wsdl;
                var schema = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
                foreach (var item in schema.items)
                {
                    if (string.IsNullOrEmpty(item.itemshortname)) continue;
                    if (item.workshopdownload == null) { wsdl = 0; } else { wsdl = Convert.ToUInt64(item.workshopdownload); }
                    SkinFromInventoryID.Add(item.itemdefid, wsdl);
                }
                Puts($"Pulled {SkinFromInventoryID.Count} skins.");
            }
            else
            {
                PrintWarning($"Failed to pull skins... Error {code}");
            }
        }
        private void PrintToChatEx(BasePlayer player, string result, string tcolour = "#66FF66")
        {
            if (Convert.ToBoolean(Config["Dependencies", "PopupNotifications"]))
            {
                if (PopupNotifications)
                {
                    PopupNotifications?.Call("CreatePopupNotification", "<color=" + tcolour + ">" + this.Title.ToString() + "</color>\n" + result, player);
                }
                else
                {
                    Puts("Setting Dependencies : PopupNotifications to false because it's missing.");
                    Config["Dependencies", "PopupNotifications"] = false;
                    SaveConfig();
                    PrintToChatEx(player, result, tcolour);
                }
            }
            else { PrintToChat(player, "<color=\"" + tcolour + "\">[" + this.Title.ToString() + "]</color> " + result); }
        }
        private ItemDefinition GetItem(string shortname)
        {
            if (string.IsNullOrEmpty(shortname) || mcITEMS == null) return null;
            ItemDefinition item;
            if (mcITEMS.TryGetValue(shortname, out item)) return item;
            return null;
        }
    }
}