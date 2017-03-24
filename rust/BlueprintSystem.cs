using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BlueprintSystem", "redBDGR", "1.0.4", ResourceId = 2381)]
    [Description("Bring back the old blueprint system, with some small changes")]
    class BlueprintSystem : RustPlugin
    {
        #region data / config

        #region data

        private DynamicConfigFile BlueprintData;
        StoredData storedData;

        class StoredData
        {
            public Dictionary<string, List<string>> BlueprintData = new Dictionary<string, List<string>>();
        }

        void SaveData()
        {
            storedData.BlueprintData = bpData;
            BlueprintData.WriteObject(storedData);
        }
        void LoadData()
        {
            try
            {
                storedData = BlueprintData.ReadObject<StoredData>();
                bpData = storedData.BlueprintData;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        #endregion

        #region config

        bool Changed = false;

        static List<object> BlockedBPs()
        {
            var bBP = new List<object>()
            {
                "attire.hide.skirt",
                "attire.hide.boots",
                "attire.hide.helterneck",
                "attire.hide.pants",
                "attire.hide.poncho",
                "attire.hide.vest",
                "burlap.gloves",
                "burlap.headwrap",
                "burlap.shirt",
                "burlap.shoes",
                "burlap.trousers",
                "bandage",
                "apple",
                "apple.spoiled",
                "battery.small",
                "mask.bandana",
                "bearmeat",
                "bearmeat.burned",
                "bearmeat.cooked",
                "black.raspberries",
                "blueberries",
                "bleach",
                "blood",
                "blueprintbase",
                "bone.fragments",
                "cactus.flesh",
                "can.beans",
                "can.beans.empty",
                "can.tuna",
                "can.tuna.empty",
                "candycane",
                "cctv.camera",
                "charcoal",
                "chicken.burned",
                "chicken.cooked",
                "chicken.raw",
                "chicken.spoiled",
                "chocolate",
                "clone.corn",
                "clone.hemp",
                "clone.pumpkin",
                "cloth",
                "coal",
                "corn",
                "crude.oil",
                "door.key",
                "door.hinged.wood",
                "door.hinged.metal",
                "fat.animal",
                "fish.cooked",
                "fish.minnows",
                "fish.raw",
                "fish.troutsmall",
                "flare",
                "furnace",
                "gears",
                "generator.wind.scrap",
                "glue",
                "granolabar",
                "hat.beenie",
                "hat.boonie",
                "hat.candle",
                "hat.cap",
                "hat.miner",
                "hat.wolf",
                "hammer",
                "hq.metal.ore",
                "humanmeat.burned",
                "humanmeat.cooked",
                "humanmeat.raw",
                "humanmeat.spoiled",
                "leather",
                "lmg.m249",
                "map",
                "meat.boar",
                "meat.pork.burned",
                "meat.pork.cooked",
                "metal.fragments",
                "metal.ore",
                "metal.refined",
                "metalblade",
                "metalpipe",
                "metalspring",
                "mushroom",
                "pistol.m92",
                "propanetank",
                "pumpkin",
                "pookie.bear",
                "research.table",
                "rifle.lr300",
                "riflebody",
                "rope",
                "jackolantern.angry",
                "jackolantern.happy",
                "santahat",
                "seed.corn",
                "seed.hemp",
                "seed.pumpkin",
                "semibody",
                "sewingkit",
                "sheetmetal",
                "shirt.tanktop",
                "skull.human",
                "skull.wolf",
                "shirt.collared",
                "smallwaterbottle",
                "smg.mp5",
                "sticks",
                "stocking.large",
                "stocking.small",
                "stones",
                "stone.pickaxe",
                "stonehachet",
                "sulfur",
                "sulfur.ore",
                "supply.signal",
                "tarp",
                "targeting.computer",
                "techparts",
                "tool.camera",
                "water",
                "water.salt",
                "wolfmeat.burned",
                "wolfmeat.cooked",
                "wolfmeat.raw",
                "wolfmeat.spoiled",
                "wood",
                "wood.armor.jacket",
                "wood.armor.pants",
                "vending.machine",
                "xmas.present.large",
                "xmax.present.medium",
                "xmas.present.small"
            };
            return bBP;
        }
        static List<object> DefaultBPs()
        {
            var dBP = new List<object>()
            {
                "rock",
                "torch",
                "paper",
                "hammer",
                "lowgradefuel",
                "gunpowder",
                "arrow.wooden",
                "attire.hide.boots",
                "attire.hide.helterneck",
                "attire.hide.pants",
                "attire.hide.poncho",
                "attire.hide.skirt",
                "attire.hide.vest",
                "bandage",
                "bone.armor.suit",
                "bone.club",
                "botabag",
                "bow.hunting",
                "box.wooden",
                "bucket.water",
                "building.planner",
                "burlap.gloves",
                "burlap.headwrap",
                "burlap.shirt",
                "burlap.shoes",
                "burlap.trousers",
                "campfire",
                "crossbow",
                "cupboard.tool",
                "deer.skull.mask",
                "door.hinged.metal",
                "door.hinged.toptier",
                "door.hinged.wood",
                "door.key",
                "fishtrap.small",
                "furnace",
                "grenade.beancan",
                "hat.beenie",
                "hat.boonie",
                "hat.candle",
                "hat.cap",
                "hat.miner",
                "hat.wolf",
                "hoodie",
                "jacket",
                "jacket.snow",
                "jackolantern.angry",
                "jackolantern.happy",
                "knife.bone",
                "ladder.wooden.wall",
                "lantern",
                "lock.code",
                "lock.key",
                "lowgradefuel",
                "map",
                "note",
                "pants",
                "pants.short",
                "pistol.eoka",
                "rug",
                "shirt.collared",
                "shirt.tanktop",
                "shoes.boots",
                "shotgun.waterpipe",
                "sleepingbag",
                "spear.stone",
                "spear.wooden",
                "spikes.floor",
                "stash.small",
                "stone.pickaxe",
                "stonehatchet",
                "tool.camera",
                "tshirt",
                "tshirt.long",
                "tunalight",
                "water.catcher.small",
                "wood.armor.jacket",
                "wood.armor.pants",
            };
            return dBP;
        }

        List<object> defaultbplist;
        List<object> BlockedItemList;

        Dictionary<string, List<string>> bpData = new Dictionary<string, List<string>>();
        Dictionary<uint, ResearchEntityInfo> ResearchChestInfo = new Dictionary<uint, ResearchEntityInfo>();
        Dictionary<uint, ItemContainer> ItemInfo = new Dictionary<uint, ItemContainer>();
        Dictionary<string, MovementItem> CommandItem = new Dictionary<string, MovementItem>();
        Dictionary<string, string> GUIinfo = new Dictionary<string, string>();
        Dictionary<string, HelpUIInfo> HelpGUIinfo = new Dictionary<string, HelpUIInfo>();
        List<string> fullItemList = new List<string>();
        List<string> currentResearchers = new List<string>();

        #region Classes for above ^

        class ResearchEntityInfo
        {
            public BaseEntity entity;
            public StorageContainer container;
            public string playerID;
        }

        class HelpUIInfo
        {
            public string bottomLeft;
            public string blocker1;
            public string blocker2;
        }

        class MovementItem
        {
            public Item item;
            public ItemContainer container;
        }

        #endregion

        public const string failEffect = "assets/prefabs/deployable/research table/effects/research-fail.prefab";
        public const string successEffect = "assets/prefabs/deployable/research table/effects/research-success.prefab";
        //public const string storagePrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
        public const string storagePrefab = "assets/bundled/prefabs/radtown/dmloot/dm tier3 lootbox.prefab";
        public const string permissionName = "blueprintsystem.use";
        public float researchChance = 0.30f;
        public float notificationShowTime = 4.0f;
        public bool ResearchOnlyInBuilding = true;
        public int itemListLength = 0;
        public float researchStudyTime = 5.0f;

        void LoadVariables()
        {
            defaultbplist = (List<object>)GetConfig("Blueprint Lists", "Default Blueprints", DefaultBPs());
            BlockedItemList = (List<object>)GetConfig("Blueprint Lists", "Blocked Blueprints", BlockedBPs());
            researchChance = Convert.ToSingle(GetConfig("Settings", "Research success %", 0.30));
            researchStudyTime = Convert.ToSingle(GetConfig("Settings", "Research Study Time", 5.0f));
            ResearchOnlyInBuilding = Convert.ToBoolean(GetConfig("Settings", "No Research When Building Blocked", true));
            notificationShowTime = Convert.ToSingle(GetConfig("Settings", "Notification Show Length", 4.0f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        #endregion

        #endregion

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
        }

        void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Blueprint Not Studied"] = "You do not have this blueprint studied!",
                ["Invalid Itemname"] = "You entered an invalid item name!",
                ["Invalid Items"] = "You do not have any of this item in your inventory!",
                ["Help Menu"] = "/research <itemname> - starts a research on the item. (On a failed research, the item will be removed from your inventory, so research carefully!",
                ["Successful Research"] = "You successfully researched a <color=#228B22>{0}</color>",
                ["Already Researched"] = "You have already researched this item!",
                ["Beginning Research"] = "You have started researching a <color=#228B22>{0}</color>",
                ["More Than One"] = "Please only drag in 1 item at a time to research",
                ["Broken Item"] = "You cannot research a broken item!",
                ["Failed Research"] = "Research failed...",
                ["Box Spawned"] = "A research crate has been spawned below you...",
                ["Box GUI Help"] = "Please note: any items left in the research slot when closing the container will be automatically moved to your inventory",
                ["No Item"] = "Please put a item into the slot first!",
                ["Cannot Research"] = "You are not allowed to research this item",
                ["Outside Building Zone"] = "You are not allowed to research outside of a building authorized zone",
                ["Random Study"] = "You randomly studied a <color=#228B22>{0}</color> from this blueprint!",
                ["Random Study already learned"] = "You randomly studied a <color=#228B22>{0}</color> from this blueprint but you have already learnt this",
                ["Already Researching"] = "You are already researching an item",
                ["BP Invalid Syntax"] = "Invalid Syntax! bp <add/remove> <playername/id> <item shortname>",
                ["BP Player Not Found / Offline"] = "Player was not found or is offline",
                ["BP Already Has This Blueprint"] = "{0} already has this blueprint",
                ["BP Can Now Craft"] = "{0} can now craft {1}",
                ["BP Forgot"] = "{0} forgot how to craft {1}",
                ["BP Doesn't know"] = "{0} doesn't know how to craft this item",
                ["GUI Place Your Item Here"] = "<---- Place your item here then click the button",
                ["GUI Chance Of Success"] = "Chance of success: {0}%",
                ["GUI Time To Research"] = "Time to research: {0} (s)",
                ["GUI Research Button"] = "Research",
                ["Wiping Database"] = "Wiping blueprint database...",
                ["Wipe Complete"] = "Blueprint database has been wiped!",
            }, this);

            BlueprintData = Interface.Oxide.DataFileSystem.GetFile("BlueprintSystem");
            LoadData();
        }

        void OnServerInitialized()
        {
            foreach (var entry in ItemManager.itemList)
                fullItemList.Add(entry.shortname);
            itemListLength = fullItemList.Count;

            InitPlayerCheck();
        }

        void OnServerSave()
        {
            SaveData();
        }

        void Unload()
        {
            SaveData();

            foreach (var key in HelpGUIinfo)
            {
                BasePlayer player = FindPlayer(key.Key);
                if (player == null || !player.IsConnected) return;
                CuiHelper.DestroyUi(player, HelpGUIinfo[player.UserIDString].bottomLeft);
                CuiHelper.DestroyUi(player, HelpGUIinfo[player.UserIDString].blocker1);
                CuiHelper.DestroyUi(player, HelpGUIinfo[player.UserIDString].blocker2);
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (!bpData.ContainsKey(player.UserIDString))
                bpData.Add(player.UserIDString, new List<string>());
        }

        object OnItemCraft(ItemCraftTask item)
        {
            BasePlayer player = item.owner;
            if (player == null) return false;
            if (defaultbplist.Contains(item.blueprint.targetItem.shortname))
                return item;
            if (bpData.ContainsKey(player.UserIDString))
            {
                if (bpData[player.UserIDString].Contains(item.blueprint.targetItem.shortname))
                    return item;
                else
                {
                    DoUI(player, msg("Blueprint Not Studied", player.UserIDString));
                    RefundItems(player, item.takenItems);
                    return false;
                }
            }
            else
            {
                bpData.Add(player.UserIDString, new List<string>());
                if (defaultbplist.Contains(item.blueprint.targetItem.shortname))
                    return item;
                DoUI(player, msg("Blueprint Not Studied", player.UserIDString));
                RefundItems(player, item.takenItems);
                return false;
            }
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (!(container.entityOwner is BaseEntity)) return;
            if (container.entityOwner.name != storagePrefab) return;
            if (!ResearchChestInfo.ContainsKey((uint)container.entityOwner.net.ID)) return;
            if (!ItemInfo.ContainsKey(item.uid)) return;
            BasePlayer player = FindPlayer(ItemInfo[item.uid].GetOwnerPlayer()?.UserIDString);
            if (player == null || !player.IsConnected) return;

            if (CommandItem.ContainsKey(player.UserIDString))
                CommandItem.Remove(player.UserIDString);
            CommandItem.Add(player.UserIDString, new MovementItem() { item = item, container = container });

            permission.GrantUserPermission(player.UserIDString, permissionName, this);
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container.playerOwner == null) return;
            BasePlayer player = container.GetOwnerPlayer();
            if (ItemInfo.ContainsKey(item.uid))
                ItemInfo[item.uid] = container;
            else
                ItemInfo.Add(item.uid, container);
            timer.Once(0.1f, () => { ItemInfo.Remove(item.uid); });
        }

        void OnPlayerLootEnd(PlayerLoot inventory)
        {
            foreach (var key in ResearchChestInfo)
            {
                if (key.Value.playerID == inventory.GetComponentInParent<BasePlayer>()?.UserIDString)
                {
                    BasePlayer player = inventory.GetComponentInParent<BasePlayer>();
                    if (player == null) return;

                    // Move item back to players inventory on research chest close
                    Item leftitem = key.Value.container.inventory.GetSlot(0);
                    if (leftitem != null)
                        leftitem.MoveToContainer(player.inventory.containerMain);

                    key.Value.entity.Kill();
                    ResearchChestInfo.Remove(key.Key);

                    // Destroying UI Components
                    if (HelpGUIinfo.ContainsKey(player.UserIDString))
                    {
                        CuiHelper.DestroyUi(player, HelpGUIinfo[player.UserIDString].bottomLeft);
                        CuiHelper.DestroyUi(player, HelpGUIinfo[player.UserIDString].blocker1);
                        CuiHelper.DestroyUi(player, HelpGUIinfo[player.UserIDString].blocker2);
                        HelpGUIinfo.Remove(player.UserIDString);
                    }
                    if (GUIinfo.ContainsKey(player.UserIDString))
                    {
                        CuiHelper.DestroyUi(player, GUIinfo[player.UserIDString]);
                        GUIinfo.Remove(player.UserIDString);
                    }
                    return;
                }
            }

            // I don't think this is needed anymore
            //permission.RevokeUserPermission(inventory.GetComponentInParent<BasePlayer>().UserIDString, permissionName);
        }

        #region Commands

        [ConsoleCommand("bpwipe")]
        void bpwipeConsoleCMD(ConsoleSystem.Arg args)
        {
            if (args.Connection != null) return;
            args.ReplyWith(msg("Wiping Database"));
            bpData.Clear();
            SaveData();
            args.ReplyWith(msg("Wipe Complete"));
        }

        [ConsoleCommand("bp")]
        void bpConsoleCMD(ConsoleSystem.Arg args)
        {
            if (args.Connection != null) return;
            if (args.Args == null)
            {
                args.ReplyWith(msg("BP Invalid Syntax"));
                return;
            }

            switch (args.Args.Length)
            {
                case 0:
                    args.ReplyWith(msg("BP Invalid Syntax"));
                    break;

                case 1:
                    args.ReplyWith(msg("BP Invalid Syntax"));
                    break;

                case 2:
                    args.ReplyWith(msg("BP Invalid Syntax"));
                    break;

                case 3:
                    BasePlayer targetplayer = FindPlayer(args.Args[1]);
                    if (targetplayer == null || !targetplayer.IsConnected)
                    {
                        args.ReplyWith(msg("BP Player Not Found / Offline"));
                        return;
                    }
                    if (!bpData.ContainsKey(targetplayer.UserIDString)) return;

                    if (args.Args[0] == "add")
                    {
                        if (bpData[targetplayer.UserIDString].Contains(args.Args[2]))
                        {
                            args.ReplyWith(string.Format(msg("BP Already Has This Blueprint"), targetplayer.displayName));
                            return;
                        }
                        else
                        {
                            bpData[targetplayer.UserIDString].Add(args.Args[2]);
                            args.ReplyWith(string.Format(msg("BP Can Now Craft"), targetplayer.displayName, args.Args[2]));
                            return;
                        }
                    }
                    else if (args.Args[0] == "remove")
                    {
                        if (bpData[targetplayer.UserIDString].Contains(args.Args[2]))
                        {
                            bpData[targetplayer.UserIDString].Remove(args.Args[2]);
                            args.ReplyWith(string.Format(msg("BP Forgot"), targetplayer.displayName, args.Args[2]));
                            return;
                        }
                        else
                        {
                            args.ReplyWith(string.Format(msg("BP Doesn't Know"), targetplayer.displayName));
                            return;
                        }
                    }
                    break;
            }
            return;
        }

        [ChatCommand("research")]
        void researchCMD(BasePlayer player, string command, string[] args)
        {
            if (ResearchOnlyInBuilding)
                if (!player.CanBuild())
                {
                    player.ChatMessage(msg("Outside Building Zone", player.UserIDString));
                    return;
                }

            ResearchEntityInfo entity = DoResearchChest(player);
            BaseEntity ent = entity.entity;
            StorageContainer container = entity.container;

            timer.Once(0.1f, () =>
            {
                container.SetFlag(BaseEntity.Flags.Open, true, false);
                player.inventory.loot.StartLootingEntity(container, false);
                player.inventory.loot.entitySource = container;
                player.inventory.loot.AddContainer(container.inventory);
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", container.panelName);
                container.SendNetworkUpdate();
            });
        }

        [ConsoleCommand("research")]
        void researchConsoleCMD(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, permissionName)) return;

            if (currentResearchers.Contains(player.UserIDString))
            {
                DoUI(player, msg("Already Researching", player.UserIDString));
                return;
            }

            currentResearchers.Add(player.UserIDString);

            if (!CommandItem.ContainsKey(player.UserIDString))
            {
                currentResearchers.Remove(player.UserIDString);
                DoUI(player, msg("No Item", player.UserIDString));
                return;
            }

            ItemContainer container = CommandItem[player.UserIDString].container;
            Item item = CommandItem[player.UserIDString].item;

            if (CheckItemIsStillThere(container, item) == false)
            {
                currentResearchers.Remove(player.UserIDString);
                DoUI(player, msg("No Item", player.UserIDString));
                return;
            }

            //CommandItem.Remove(player.UserIDString);

            if (container == null || item == null) return;

            if (item.isBroken)
            {
                currentResearchers.Remove(player.UserIDString);
                DoUI(player, msg("Broken Item", player.UserIDString));
                return;
            }

            if (item.info.shortname == "researchpaper")
                goto skip;

            if (defaultbplist.Contains(item.info.shortname))
            {
                currentResearchers.Remove(player.UserIDString);
                DoUI(player, msg("Already Researched", player.UserIDString));
                return;
            }

            if (BlockedItemList.Contains(item.info.shortname))
            {
                currentResearchers.Remove(player.UserIDString);
                DoUI(player, msg("Cannot Research", player.UserIDString));
                return;
            }


            if (!bpData.ContainsKey(player.UserIDString))
                bpData.Add(player.UserIDString, new List<string>());

            if (bpData[player.UserIDString].Contains(item.info.shortname))
            {
                currentResearchers.Remove(player.UserIDString);
                DoUI(player, msg("Already Researched", player.UserIDString));
                return;
            }
            skip:

            DoUI(player, string.Format(msg("Beginning Research", player.UserIDString), item.info.displayName.english));
            Item item2 = item;
            container.SetLocked(true);

            timer.Repeat(researchStudyTime, 1, () =>
            {
                currentResearchers.Remove(player.UserIDString);
                int x = DoResearch(item, player);
                switch (x)
                {
                    case 0:
                        container.SetLocked(false);
                        break;

                    case 1:
                        if (item.condition == 0.0f)
                        {
                            if (item.amount <= 1)
                                item.Remove(0);
                            else
                                item.amount -= 1;
                        }
                        else
                            item.condition = 0.0f;
                        container.SetLocked(false);
                        break;

                    case 2:
                        container.SetLocked(false);
                        break;

                    case 3:
                        if (item.amount <= 1)
                            item.Remove(0);
                        else item.amount -= 1;
                        container.SetLocked(false);
                        break;
                }
            });
        }

        #endregion

        void RefundItems(BasePlayer player, List<Item> itemlist)
        {
            foreach (var item in itemlist)
                item.MoveToContainer(player.inventory.containerMain);
        }

        int DoResearch(Item item, BasePlayer player)
        {
            if (item.info.shortname == "researchpaper")
            {
                string itemname = "";
                bprandomstart:
                float rng = UnityEngine.Random.Range(0.0f, Convert.ToSingle(itemListLength));
                string x = fullItemList[Convert.ToInt32(rng)];
                if (BlockedItemList.Contains(x))
                    goto bprandomstart;
                else
                {
                    foreach (var entry in ItemManager.itemList)
                        if (entry.shortname == x)
                        {
                            itemname = entry.displayName.english;
                            break;
                        }

                    if (bpData[player.UserIDString].Contains(x))
                    {
                        if (CheckChestIsStillThere(player) == false) return 0;
                        DoUI(player, string.Format(msg("Random Study already learned", player.UserIDString), itemname));
                        Effect.server.Run(successEffect, player.transform.position);
                        return 3;
                    }
                    if (CheckChestIsStillThere(player) == false) return 0;
                    DoUI(player, string.Format(msg("Random Study", player.UserIDString), itemname));
                    Effect.server.Run(successEffect, player.transform.position);
                    bpData[player.UserIDString].Add(x);
                }
                return 3;
            }

            if (UnityEngine.Random.Range(0.00f, 1.00f) > researchChance)
            {
                if (CheckChestIsStillThere(player) == false) return 0;
                DoUI(player, msg("Failed Research", player.UserIDString));
                Effect.server.Run(failEffect, player.transform.position);
                return 1;
            }
            if (CheckChestIsStillThere(player) == false) return 0;
            DoUI(player, string.Format(msg("Successful Research", player.UserIDString), item.info.displayName.english));
            Effect.server.Run(successEffect, player.transform.position);
            bpData[player.UserIDString].Add(item.info.shortname);
            return 2;
        }

        ResearchEntityInfo DoResearchChest(BasePlayer player)
        {
            BaseEntity entity = GameManager.server.CreateEntity(storagePrefab, new Vector3(player.transform.position.x, player.transform.position.y - 200.0f, player.transform.position.z));
            StorageContainer container = entity.GetComponent<StorageContainer>();
            container.inventorySlots = 1;
            entity.Spawn();
            foreach (var slot in container.inventory.itemList)
                slot.Remove();

            if (ResearchChestInfo.ContainsKey(entity.net.ID))
            {
                // Incase an instance is already Init
                ResearchChestInfo[entity.net.ID].playerID = player.UserIDString;
                ResearchChestInfo[entity.net.ID].entity = entity;
                ResearchChestInfo[entity.net.ID].container = container;
            }
            else
                ResearchChestInfo.Add(entity.net.ID, new ResearchEntityInfo() { entity = entity, container = container, playerID = player.UserIDString });

            return ResearchChestInfo[entity.net.ID];
        }

        void DoUI(BasePlayer player, string message)
        {
            if (GUIinfo.ContainsKey(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, GUIinfo[player.UserIDString]);
                GUIinfo.Remove(player.UserIDString);
            }

            var elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.4", FadeIn = 1.0f },
                RectTransform = { AnchorMin = "0.654 0.025", AnchorMax = "0.82 0.14" },
            }, "Hud.Menu");
            elements.Add(new CuiLabel
            {
                Text = { Text = message, FontSize = 14, Align = TextAnchor.MiddleCenter, FadeIn = 2.0f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, panel);
 
            CuiHelper.AddUi(player, elements);
            GUIinfo.Add(player.UserIDString, panel);

            timer.Once(notificationShowTime, () => 
            {
                CuiHelper.DestroyUi(player, panel);
                GUIinfo.Remove(player.UserIDString);
            });
        }

        void OnLootEntity(BasePlayer looter, BaseEntity entity)
        {
            if (!ResearchChestInfo.ContainsKey(entity.net.ID)) return;
            DoHelpUI(looter, msg("Box GUI Help", looter.UserIDString));
        }


        void DoHelpUI(BasePlayer player, string message)
        {
            if (HelpGUIinfo.ContainsKey(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, HelpGUIinfo[player.UserIDString].bottomLeft);
                CuiHelper.DestroyUi(player, HelpGUIinfo[player.UserIDString].blocker1);
                CuiHelper.DestroyUi(player, HelpGUIinfo[player.UserIDString].blocker2);
                HelpGUIinfo.Remove(player.UserIDString);
            }

            var elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 1", FadeIn = 1.0f },
                RectTransform = { AnchorMin = "0.027 0.025", AnchorMax = "0.33 0.14" },
            }, "Hud.Menu");

            var blockerpanel1 = elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 1" },
                RectTransform = { AnchorMin = "0.655 0.15", AnchorMax = "0.942 0.32" },
            }, "Hud.Menu");

            var blockerpanel2 = elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 1" },
                RectTransform = { AnchorMin = "0.703 0.32", AnchorMax = "0.942 0.405" },
            }, "Hud.Menu");

            elements.Add(new CuiLabel
            {
                Text = { Text = message, FontSize = 14, Align = TextAnchor.MiddleCenter, FadeIn = 2.0f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, panel);

            elements.Add(new CuiLabel
            {
                Text = { Text = msg("GUI Place Your Item Here", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, FadeIn = 2.0f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, blockerpanel2);

            elements.Add(new CuiLabel
            {
                Text = { Text = string.Format(msg("GUI Chance Of Success", player.UserIDString), (researchChance * 100).ToString()), FontSize = 14, Align = TextAnchor.UpperLeft, FadeIn = 2.0f },
                RectTransform = { AnchorMin = "0.05 0.1", AnchorMax = "0.95 0.9" }
            }, blockerpanel1);

            elements.Add(new CuiLabel
            {
                Text = { Text = string.Format(msg("GUI Time To Research", player.UserIDString), (researchStudyTime * 1).ToString()), FontSize = 14, Align = TextAnchor.UpperLeft, FadeIn = 2.0f },
                RectTransform = { AnchorMin = "0.05 0.1", AnchorMax = "0.75 0.7" }
            }, blockerpanel1);

            elements.Add(new CuiButton
            {
                Button = { Command = "research", Color = "0.1 0.3 0.1 1" },
                RectTransform = { AnchorMin = "0.05 0.15", AnchorMax = "0.4 0.45" },
                Text = { Text = msg("GUI Research Button", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, blockerpanel1);

            CuiHelper.AddUi(player, elements);
            HelpGUIinfo.Add(player.UserIDString, new HelpUIInfo() { bottomLeft = panel, blocker1 = blockerpanel1, blocker2 = blockerpanel2 });
        }

        bool CheckItemIsStillThere(ItemContainer container, Item item)
        {
            foreach (var slot in container.itemList)
            {
                if (slot.uid == item.uid)
                    return true;
            }
            return false;
        }

        bool CheckChestIsStillThere(BasePlayer player)
        {
            foreach(var entry in ResearchChestInfo)
            {
                if (entry.Value.playerID == player.UserIDString)
                    return true;
            }
            return false;
        }

        Item CheckInventoryForItem(PlayerInventory container, ItemDefinition item)
        {
            foreach (var slotitem in container.containerBelt.itemList)
                if (!slotitem.isBroken)
                    if (slotitem.info.displayName == item.displayName || slotitem.info.shortname == item.shortname)
                        return slotitem;

            foreach (var slotitem in container.containerMain.itemList)
                if (!slotitem.isBroken)
                    if (slotitem.info.displayName == item.displayName || slotitem.info.shortname == item.shortname)
                        return slotitem;

            foreach (var slotitem in container.containerWear.itemList)
                if (!slotitem.isBroken)
                    if (slotitem.info.displayName == item.displayName || slotitem.info.shortname == item.shortname)
                        return slotitem;

            return null;
        }

        ItemDefinition FindItem(string arg)
        {
            ItemDefinition newitem = null;
            foreach (var item in ItemManager.GetItemDefinitions())
                if (arg == item.displayName.english || arg == item.itemid.ToString() || arg == item.shortname)
                    newitem = item;
            return newitem;
        }

        void InitPlayerCheck()
        {
            foreach (var player in BasePlayer.activePlayerList)
                if (!bpData.ContainsKey(player.UserIDString))
                    bpData.Add(player.UserIDString, new List<string>());
            return;
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