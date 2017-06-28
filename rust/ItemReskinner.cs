using UnityEngine;
using Rust;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;
using Facepunch.Steamworks;
namespace Oxide.Plugins
{
    [Info("ItemReskinner", "Critskrieg (Paradox.exe)", "1.08", ResourceId = 2423)]
    [Description("Allow players to reskin items with skins they own.")]

    class ItemReskinner : RustPlugin
    {
        ///--------------\\\
        // [ BASE VOIDS ] \\
        ///--------------\\\
        #region [ BASE VOIDS ]
        private readonly FieldInfo skins2 = typeof(ItemDefinition).GetField("_skins2", BindingFlags.NonPublic | BindingFlags.Instance);

        protected override void LoadConfig() { base.LoadConfig(); }

        private void OnServerInitialized() { webrequest.EnqueueGet("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", ReadScheme, this); }

        void Loaded() { LoadData(); }
        void Unload() { SaveData(); }
        //protected override void LoadDefaultConfig() { Config.Clear(); Puts("No configuration file found, generating..."); }
        #endregion
        ///-----------------------\\\
        // [ SKIN SCHEMA INQUIRY ] \\
        ///-----------------------\\\
        #region [ SKIN SCHEMA INQUIRY ]
        private void ReadScheme(int code, string response)
        {
            if (response != null && code == 200)
            {
                var schema = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
                var defs = new List<Inventory.Definition>();
                foreach (var item in schema.items)
                {
                    if (string.IsNullOrEmpty(item.itemshortname)) continue;
                    var steamItem = Global.SteamServer.Inventory.CreateDefinition((int)item.itemdefid);
                    steamItem.Name = item.name;
                    steamItem.SetProperty("itemshortname", item.itemshortname);
                    steamItem.SetProperty("workshopid", item.workshopid);
                    steamItem.SetProperty("workshopdownload", item.workshopdownload);
                    skinListSHORTNAMES.Add(item.itemshortname);
                    skinListNAMES.Add(item.name);
                    skinListIDS.Add(item.itemdefid);
                    skinListWIDS.Add(Convert.ToUInt32(item.workshopdownload));
                    defs.Add(steamItem);
                }
                Global.SteamServer.Inventory.Definitions = defs.ToArray();

                foreach (var item in ItemManager.itemList)
                    skins2.SetValue(item, Global.SteamServer.Inventory.Definitions.Where(x => (x.GetStringProperty("itemshortname") == item.shortname) && !string.IsNullOrEmpty(x.GetStringProperty("workshopdownload"))).ToArray());

                Puts($"Loaded {Global.SteamServer.Inventory.Definitions.Length} approved workshop skins.");
            }
            else { PrintWarning($"Failed to load approved workshop skins... Error {code}"); }
        }
        #endregion
        ///--------------------\\\
        // [ MESSAGE REGISTRY ] \\
        ///--------------------\\\
        #region [ MESSAGE REGISTRY ]
        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SkinSyntax"] = "<color=yellow>USAGE:</color> /SKIN LIST <color=yellow>|</color> /SKIN LIST ALL <color=yellow>|</color> /SKIN <#> <color=yellow>|</color> /SKIN 0",
                ["SkinNumberDoesNotExist"] = "That skin number <color=red>DOES NOT</color> exist.",
                ["NoActiveItemForList"] = "<color=yellow>INFO: </color> Select an item on your hotbar to <color=red>/LIST</color> appliable skins!",
                ["NoActiveItemForSkin"] = "<color=yellow>INFO: </color> Select an item on your hotbar to <color=red>/SKIN</color> it!",
                ["InvalidSkin"] = "<color=yellow>INFO:</color> That skin <color=red>CANNOT</color> be applied to this item!",
                ["NoSkins"] = "<color=yellow>INFO:</color> You have <color=red>NO</color> appliable skins for this item. Craft a skin to add it to the list!",
                ["AbsolutelyNoSkins"] = "<color=yellow>INFO:</color> You have <color=red>ABSOLUTELY NO</color> appliable skins for any items. Craft a skin to add it to the list!",
                ["SkinAddOnCraft"] = "<color=yellow>INFO:</color> \"{0}\" <color=green>[WID: {1}]</color> is now appliable skin number <color=yellow>[{2}]</color>",
                ["ListSkin"] = "<color=yellow>[{0}]</color> \"{1}\" <color=green> [WID: {2}]</color>",
            }, this);
        }
        #endregion
        ///----------------------------------\\\
        // [ VARIABLE AND CLASS DECLARATION ] \\
        ///----------------------------------\\\
        #region [ VARIABLE & CLASS DECLARATION ]
        List<string> skinListSHORTNAMES = new List<string>();
        List<string> skinListNAMES = new List<string>();
        List<uint> skinListIDS = new List<uint>();
        List<uint> skinListWIDS = new List<uint>();
        int result;
        Item activeItem;
        StoredData playerSkinData;

        class StoredData
        {
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
            public StoredData() { }
        }
        class SkinInfo
        {
            public List<InternalItemInfo> Items = new List<InternalItemInfo>();
            public SkinInfo() { }
        }
        class InternalItemInfo
        {
            public string Shortname;
            public ulong SkinWID;
            public ulong SkinID;
            public InternalItemInfo() { }
        }
        class PlayerData
        {
            public ulong ID;
            public List<string> skinWIDS = new List<string>();
            public List<string> skinIDS = new List<string>();
            public List<string> skinShortnames = new List<string>();
            public List<string> skinNames = new List<string>();
            public PlayerData() { }
        }
        #endregion
        ///----------------------\\\
        // [ METHOD DECLARATION ] \\
        ///----------------------\\\
        #region [ METHOD DECLARATION ]
        public Item GetItemInSlot(BasePlayer player, int slot) { return player.inventory.containerBelt.GetSlot(slot); }

        void LoadData()
        {
            playerSkinData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Title);
            Puts("Loaded " + playerSkinData.Players.Count + " players from /data/" + this.Title + ".json");
        }
        void SaveData() { Interface.Oxide.DataFileSystem.WriteObject(this.Title, playerSkinData); }

        private void PrintMessage(BasePlayer player, string msgId, params object[] args) { PrintToChat(player, lang.GetMessage(msgId, this, player.UserIDString), args); }

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
        private bool GiveSkinItem(BasePlayer player, int itemid, ulong skinid, int amount)
        {
            Item i;
            if (!player.IsConnected) return false;
            i = ItemManager.CreateByItemID(itemid, amount, skinid);
            if (i != null) if (!i.MoveToContainer(player.inventory.containerBelt)) { i.Drop(player.eyes.position, player.eyes.BodyForward() * 2f); }
            return true;
        }

        private bool PlayerExists(BasePlayer player)
        {
            PlayerData item = null;
            if (playerSkinData.Players.TryGetValue(player.userID, out item)) { return true; }
            return false;
        }

        private bool InitPlayerData(BasePlayer player)
        {
            if (!PlayerExists(player))
            {
                PlayerData z = new PlayerData();
                z.ID = player.userID;
                playerSkinData.Players.Add(z.ID, z);
                return true;
            }
            return false;
        }
        #endregion
        ///-----------------\\\
        // [ ON ITEM CRAFT ] \\
        ///-----------------\\\
        #region [ ON ITEM CRAFT ]
        private void OnItemCraft(ItemCraftTask task, BasePlayer player, BasePlayer crafter)
        {
            if (!PlayerExists(player)) { InitPlayerData(player); }
            if (!playerSkinData.Players[player.userID].skinIDS.Contains(task.skinID.ToString()) && task.skinID != 0)
            {
                InternalItemInfo e = new InternalItemInfo();
                int skinIDIndex = skinListIDS.IndexOf(Convert.ToUInt32(task.skinID));
                e.Shortname = skinListSHORTNAMES[skinIDIndex];
                if (skinListWIDS[skinIDIndex] > 0) { e.SkinWID = skinListWIDS[skinIDIndex]; }
                else { e.SkinWID = skinListIDS[skinIDIndex]; }
                e.SkinID = skinListIDS[skinIDIndex];
                playerSkinData.Players[player.userID].skinWIDS.Add(e.SkinWID.ToString());
                playerSkinData.Players[player.userID].skinIDS.Add(e.SkinID.ToString());
                playerSkinData.Players[player.userID].skinShortnames.Add(e.Shortname.ToString());
                playerSkinData.Players[player.userID].skinNames.Add(skinListNAMES[skinIDIndex]);
                PrintMessage(player, "SkinAddOnCraft", skinListNAMES[skinIDIndex], e.SkinID, playerSkinData.Players[player.userID].skinIDS.Count);
            }
        }
        #endregion
        ///----------------\\\
        // [ SKIN COMMAND ] \\
        ///----------------\\\
        #region [ SKIN COMMAND ]
        [ChatCommand("skin")]
        private void skinCommand(BasePlayer player, string command, string[] args)
        {
            if (!PlayerExists(player)) { InitPlayerData(player); }
            if (args.Length == 0 || args.Length > 2) { PrintMessage(player, "SkinSyntax"); }
            ///---------------------\\\
            // [ SKIN LIST COMMAND ] \\
            ///---------------------\\\
            #region [ SKIN LIST COMMAND ]
            else if (args.Length == 1 && args[0] == "list")
            {
                // "NoActiveItem" Error
                if (player.GetActiveItem() == null) { PrintMessage(player, "NoActiveItemForList"); return; }
                activeItem = player.GetActiveItem();
                int skinCount = 0;
                for (int i = 0; i < playerSkinData.Players[player.userID].skinIDS.Count; i++)
                {
                    if (playerSkinData.Players[player.userID].skinShortnames[i].Equals(activeItem.info.shortname))
                    {
                        PrintMessage(player, "ListSkin", i + 1, playerSkinData.Players[player.userID].skinNames[i], playerSkinData.Players[player.userID].skinWIDS[i]);
                        skinCount++;
                    }
                }
                // "NoSkins" Error
                if (skinCount == 0) { PrintMessage(player, "NoSkins"); return; }
            }
            #endregion
            ///-------------------------\\\
            // [ SKIN LIST ALL COMMAND ] \\
            ///-------------------------\\\
            #region [ SKIN LIST ALL COMMAND ]
            else if (args.Length == 2 && args[0] == "list" && args[1] == "all")
            {
                // "AbsolutelyNoSkins" Error
                if (playerSkinData.Players[player.userID].skinIDS.Count == 0) { PrintMessage(player, "AbsolutelyNoSkins"); return; }
                int skinCount = 0;
                for (int i = 0; i < playerSkinData.Players[player.userID].skinIDS.Count; i++)
                {
                    PrintMessage(player, "ListSkin", skinCount + 1, playerSkinData.Players[player.userID].skinNames[i], playerSkinData.Players[player.userID].skinWIDS[i]);
                    skinCount++;
                }
            }
            #endregion
            ///--------------------\\\
            // [ SKIN <#> COMMAND ] \\
            ///--------------------\\\
            #region [ SKIN <#> COMMAND ]
            else if (args.Length == 1 && (Int32.TryParse(args[0], out result)) == true)
            {
                // "NoActiveItem" Error
                if (player.GetActiveItem() == null) { PrintMessage(player, "NoActiveItemForSkin"); return; }
                // "SkinNumberDoesNotExist" Error
                if (Int32.Parse(args[0]) > (playerSkinData.Players[player.userID].skinIDS.Count) || Int32.Parse(args[0]) < 0) { PrintMessage(player, "SkinNumberDoesNotExist"); return; }
                Item activeItem = player.GetActiveItem();
                if (args.Length == 1 && args[0] == "0")
                {
                    for (int i = 0; i < 6; i++)
                    {
                        Item detectItem = player.inventory.containerBelt.GetSlot(i);
                        if (detectItem == null) { continue; }
                        else if (detectItem.info.shortname.Equals(activeItem.info.shortname)) { player.inventory.containerBelt.GetSlot(i).skin = 0; }
                    }
                    return;
                }
                ulong skinCalled = Convert.ToUInt32(playerSkinData.Players[player.userID].skinWIDS[Int32.Parse(args[0]) - 1]);
                string skinShortname = playerSkinData.Players[player.userID].skinShortnames[Int32.Parse(args[0]) - 1];
                // "InvalidSkin" Error
                if (skinShortname != activeItem.info.shortname) { PrintMessage(player, "InvalidSkin"); return; }
                for (int i = 0; i < 6; i++)
                {
                    Item detectItem = player.inventory.containerBelt.GetSlot(i);
                    if (detectItem == null) { continue; }
                    else if (detectItem.info.shortname.Equals(activeItem.info.shortname)) { player.inventory.containerBelt.GetSlot(i).skin = skinCalled; }
                }
            }
            #endregion
            else { PrintMessage(player, "SkinSyntax"); }
        }
        #endregion
    }
}