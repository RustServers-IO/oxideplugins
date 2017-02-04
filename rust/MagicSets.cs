// Reference: Rust.Workshop
using System;
using System.Collections.Generic;
using Oxide.Core;
namespace Oxide.Plugins
{
    [Info("MagicSets", "Norn", "0.1.2", ResourceId = 2290)]
    [Description("Magical gearsets.")]

    class MagicSets : RustPlugin
    {
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        class StoredData
        {
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
            public StoredData(){}
        }
        class SetInfo
        {
            public string Name;
            public List<InternalItemInfo> Items = new List<InternalItemInfo>();
            public SetInfo(){}
        }
        class InternalItemInfo
        {
            public string Shortname;
            public ulong SkinID;
            public InternalItemInfo() { }
        }
        class PlayerData
        {
            public ulong ID;
            public Dictionary<string, SetInfo> Sets = new Dictionary<string, SetInfo>();
            public PlayerData(){}
        }
        StoredData playerSetData;

        void Loaded()
        {
            permission.RegisterPermission("MagicSets.able", this);
            permission.RegisterPermission("MagicSets.vip", this);
            RegisterLang();
            LoadData();
        }
        void Unload() { SaveData(); }
        void OnServerInitialized() { }
        void RegisterLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoValue"] = "<color=yellow>USAGE:</color> /sets [add | remove | create | list | clear]",
                ["ClearSuccess"] = "<color=yellow>INFO:</color> You have <color=green>successfully</color> removed {0} sets.",
                ["NoSets"] = "<color=yellow>INFO:</color> You have <color=red>no</color> sets.",
                ["SetAlreadyExists"] = "<color=yellow>INFO:</color> A set with the name <color=red>{0}</color> already exists!",
                ["SetAdded"] = "<color=yellow>INFO:</color> You have <color=green>added</color> a new set! (<color=yellow>{0}</color>)\n<color=yellow>/sets create <color=green>{1}</color> to create it</color>",
                ["SetAddUsage"] = "<color=yellow>USAGE:</color> /sets add <color=green>name</color>",
                ["SetNotExist"] = "<color=yellow>INFO:</color> A set with the name <color=red>{0}</color> does not exist.",
                ["SetRemoved"] = "<color=yellow>INFO:</color> You have successfully removed the set <color=red>{0}</color>.",
                ["RemoveUsage"] = "<color=yellow>USAGE:</color> /sets remove <color=green>name</color>",
                ["MaxSets"] = "You have reached your <color=yellow>maximum</color> allowed sets. (<color=red>{0}</color>)",
                ["ListFormat"] = "<color=yellow>({0}</color>) <color=red>{1}</color>\n<color=green>Contents:</color>{2}",
                ["SetCreated"] = "<color=yellow>INFO:</color> You have successfully created a set (<color=green>{0}</color>).",
                ["NotEnoughResources"] = "<color=red>ERROR:</color> You do not have enough resources to create set (<color=green>{0}</color>).",
                ["SetsCreateUsage"] = "<color=yellow>USAGE:</color> /sets create <color=green>name</color>",
                ["NoItems"] = "<color=yellow>INFO:</color> You are not wearing any gear. Please equip the items you add to add to a set."
            }, this);
        }
        protected override void LoadDefaultConfig()
        {
            // -- [ RESET ] ---
            Config.Clear();
            Puts("No configuration file found, generating...");
            // -- [ SETTINGS ] ---
            Config["Settings", "SetLimit"] = 2;
            Config["Settings", "SetLimitVIP"] = 5;
        }
        private List<ItemAmount> ReturnIngredients(ItemDefinition item)
        {
            var bp = ItemManager.FindBlueprint(item);
            if (bp == null) return null;
            return bp.ingredients;
        }
        public static int GetRandomNumber(int min, int max)
        {
            System.Random r = new System.Random();
            int n = r.Next(min, max);
            return n;
        }
        // -------------------------- [ DATA FUNCTIONS ] -----------------------------
        void LoadData()
        {
            playerSetData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Title);
            Puts("Loaded " + playerSetData.Players.Count + " players from /data/" + this.Title + ".json");
        }
        void SaveData() { Interface.Oxide.DataFileSystem.WriteObject(this.Title, playerSetData); }
        private bool PlayerExists(BasePlayer player)
        {
            PlayerData item = null;
            if (playerSetData.Players.TryGetValue(player.userID, out item)) { return true; }
            return false;
        }
        private bool InitPlayerData(BasePlayer player)
        {
            if (!PlayerExists(player))
            {
                PlayerData z = new PlayerData();
                z.ID = player.userID;
                playerSetData.Players.Add(z.ID, z);
                return true;
            }
            return false;
        }
        // ---------------------------------------------------------------------------
        private bool HasItem(BasePlayer player, string item, int amount)
        {
            if (amount <= 0) return false;
            var definition = ItemManager.FindItemDefinition(item);
            if (definition == null) return false;
            var pamount = player.inventory.GetAmount(definition.itemid);
            if (pamount < amount) return false;
            return true;
        }
        private bool TakeItem(BasePlayer player, string item, int amount)
        {
            if (amount <= 0) return false;
            var definition = ItemManager.FindItemDefinition(item);
            if (definition == null) return false;

            var pamount = player.inventory.GetAmount(definition.itemid);
            if (pamount < amount) return false;
            player.inventory.Take(null, definition.itemid, amount);
            return true;
        }
        private bool GiveSetItem(BasePlayer player, int itemid, ulong skinid, int amount)
        {
            Item i;
            if (!player.isConnected) return false;
            i = ItemManager.CreateByItemID(itemid, amount, skinid);
            if (i != null) if (!i.MoveToContainer(player.inventory.containerWear) && !i.MoveToContainer(player.inventory.containerMain) && !i.MoveToContainer(player.inventory.containerBelt)) { i.Drop(player.eyes.position, player.eyes.BodyForward() * 2f); }
            return true;
        }
        [ChatCommand("sets")]
        private void setsCommand(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.net.connection.userid.ToString(), "MagicSets.able"))
            {
                if(!PlayerExists(player)) { InitPlayerData(player); }
                if (args.Length == 0 || args.Length > 3) { PrintToChat(player, Lang("NoValue", player.UserIDString)); }
                else if (args[0] == "clear")
                {
                    int count = playerSetData.Players[player.userID].Sets.Count;
                    if(count == 0) { PrintToChat(player, Lang("NoSets", player.UserIDString)); return; }
                    playerSetData.Players[player.userID].Sets.Clear();
                    PrintToChat(player, Lang("ClearSuccess", player.UserIDString, count.ToString()));
                }
                else if(args[0] == "add")
                {
                    if (args.Length == 2)
                    {
                        if (!permission.UserHasPermission(player.net.connection.userid.ToString(), "MagicSets.vip") && playerSetData.Players[player.userID].Sets.Count >= Convert.ToInt16(Config["Settings", "SetLimit"])) { PrintToChat(player, Lang("MaxSets", player.UserIDString, Config["Settings", "SetLimit"].ToString())); return; }
                        else if (permission.UserHasPermission(player.net.connection.userid.ToString(), "MagicSets.vip") && playerSetData.Players[player.userID].Sets.Count >= Convert.ToInt16(Config["Settings", "SetLimitVIP"])) { PrintToChat(player, Lang("MaxSets", player.UserIDString, Config["Settings", "SetLimitVIP"].ToString())); return; }
                        if (player.inventory.containerWear.itemList.Count >= 1)
                        {
                            SetInfo z = new SetInfo();
                            string setname = args[1];
                            z.Name = setname;
                            if (playerSetData.Players[player.userID].Sets.ContainsKey(setname)) { PrintToChat(player, Lang("SetAlreadyExists", player.UserIDString, setname)); return; }
                            foreach (var wearItem in player.inventory.containerWear.itemList)
                            {
                                if (ReturnIngredients(wearItem.info) == null) break;
                                InternalItemInfo e = new InternalItemInfo();
                                e.Shortname = wearItem.info.shortname;
                                e.SkinID = wearItem.skin;
                                z.Items.Add(e);
                            }
                            PrintToChat(player, Lang("SetAdded", player.UserIDString, z.Name, z.Name));
                            playerSetData.Players[player.userID].Sets.Add(setname, z);
                        }
                        else { PrintToChat(player, Lang("NoItems", player.UserIDString)); }
                    }
                    else { PrintToChat(player, Lang("SetAddUsage", player.UserIDString)); }
                }
                else if (args[0] == "remove")
                {
                    if (args.Length == 2)
                    {
                        string setname = args[1];
                        if (!playerSetData.Players[player.userID].Sets.ContainsKey(setname)) { PrintToChat(player, Lang("SetNotExist", player.UserIDString, setname)); return; }
                        playerSetData.Players[player.userID].Sets.Remove(setname);
                        PrintToChat(player, Lang("SetRemoved", player.UserIDString, setname));
                    }
                    else { PrintToChat(player, Lang("RemoveUsage", player.UserIDString)); }
                }
                else if (args[0] == "list")
                {
                    if(playerSetData.Players[player.userID].Sets.Count == 0) { PrintToChat(player, Lang("NoSets", player.UserIDString)); return; }
                    int count = 1;
                    foreach(var set in playerSetData.Players[player.userID].Sets.Values)
                    {
                        string items = "";
                        foreach(var item in set.Items) { items = items + " | " + item.Shortname + "<color=yellow> [WID: " + item.SkinID + "]</color>"; }
                        PrintToChat(player, Lang("ListFormat", player.UserIDString, count.ToString(), set.Name, items));
                        count++;
                    }
                }
                else if (args[0] == "create")
                {
                    if (args.Length == 2)
                    {
                        string setname = args[1];
                        Dictionary<ItemDefinition, float> Costs = new Dictionary<ItemDefinition, float>();
                        if (!playerSetData.Players[player.userID].Sets.ContainsKey(setname)) { PrintToChat(player, Lang("SetNotExist", player.UserIDString, setname)); return; }
                        foreach (InternalItemInfo item in playerSetData.Players[player.userID].Sets[setname].Items)
                        {
                            List<ItemAmount> ingred = ReturnIngredients(ItemManager.FindItemDefinition(item.Shortname));
                            foreach (ItemAmount ingredients in ingred)
                            {
                                if(!Costs.ContainsKey(ingredients.itemDef)){ Costs.Add(ingredients.itemDef, ingredients.amount); } 
                                else { Costs[ingredients.itemDef] += ingredients.amount; }
                            }
                        }
                        int item_amount_to_total = Costs.Count;
                        int has_items = 0;
                        foreach(var c in Costs) { if(HasItem(player, c.Key.shortname, Convert.ToInt32(c.Value))) { has_items++; } }
                        if(has_items == item_amount_to_total)
                        {
                            foreach (var c in Costs) { TakeItem(player, c.Key.shortname, Convert.ToInt32(c.Value)); }
                            foreach (InternalItemInfo item in playerSetData.Players[player.userID].Sets[setname].Items) { GiveSetItem(player, ItemManager.FindItemDefinition(item.Shortname).itemid, item.SkinID, 1); }
                            PrintToChat(player, Lang("SetCreated", player.UserIDString, setname));
                        }
                        else
                        {
                            PrintToChat(player, Lang("NotEnoughResources", player.UserIDString, setname));
                        }
                    }
                    else { PrintToChat(player, Lang("SetsCreateUsage", player.UserIDString)); }
                }
                else { PrintToChat(player, Lang("NoValue", player.UserIDString)); }
            }
        }
    }
}