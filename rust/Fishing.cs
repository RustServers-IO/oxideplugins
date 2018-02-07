using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Fishing", "Colon Blow", "1.3.3", ResourceId = 1537)]
    class Fishing : RustPlugin
    {

        private static int waterlayer;
        private static int groundlayer;
        private bool Changed;

        Dictionary<ulong, string> GuiInfo = new Dictionary<ulong, string>();

        void Loaded()
        {
            lang.RegisterMessages(messages, this);
            LoadVariables();
            permission.RegisterPermission("fishing.allowed", this);
        }
        void LoadDefaultConfig()
        {
            Puts("No configuration file found, generating...");
            Config.Clear();
            LoadVariables();
        }

        void OnServerInitialized()
        {
            waterlayer = UnityEngine.LayerMask.GetMask("Water");
            groundlayer = UnityEngine.LayerMask.GetMask("Terrain", "World", "Construction");
        }

        bool IsAllowed(BasePlayer player, string perm)
        {
            if (permission.UserHasPermission(player.userID.ToString(), perm)) return true;
            return false;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////

        bool ShowFishCatchIcon = true;
        bool allowrandomitemchance = true;
        bool useweaponmod = true;
        bool useattiremod = true;
        bool useitemmod = true;
        bool usetimemod = true;

        int fishchancedefault = 10;
        int randomitemchance = 1;
        int fishchancemodweaponbonus = 10;
        int fishchancemodattirebonus = 10;
        int fishchancemoditembonus = 10;
        int fishchancemodtimebonus = 10;
        float treasureDespawn = 200f;

        string iconcommonfish2 = "http://i.imgur.com/HftxU00.png";
        string iconuncommonfish1 = "http://i.imgur.com/xReDQM1.png";
        string iconcommonfish1 = "http://i.imgur.com/rBEmhpg.png";
        string iconrandomitem = "http://i.imgur.com/y2scGmZ.png";
        string iconrarefish1 = "http://i.imgur.com/jMZxGf1.png";

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private void LoadConfigVariables()
        {
            CheckCfg("Show Fish Catch Indicator", ref ShowFishCatchIcon);
            CheckCfg("Allow Random Item Chance", ref allowrandomitemchance);
            CheckCfg("Allow Bonus from Weapon", ref useweaponmod);
            CheckCfg("Allow Bonus from Attire", ref useattiremod);
            CheckCfg("Allow Bonus from Item", ref useitemmod);
            CheckCfg("Allow Bonus from Time of Day", ref usetimemod);

            CheckCfg("Chance - Default to Catch Fish (Percentage)", ref fishchancedefault);
            CheckCfg("Chance - Get Random World Item (Percentage)", ref randomitemchance);
            CheckCfg("Bonus - From Weapon (Percentage)", ref fishchancemodweaponbonus);
            CheckCfg("Bonus - From Attire (Percentage)", ref fishchancemodattirebonus);
            CheckCfg("Bonus - From Items (Percentage)", ref fishchancemoditembonus);
            CheckCfg("Bonus - From Time of Day (Percentage)", ref fishchancemodtimebonus);
            CheckCfg("Treasure - Time to despawn chests : ", ref treasureDespawn);

            CheckCfg("Icon - Url for Common Fish 2", ref iconcommonfish2);
            CheckCfg("Icon - Url for Common Fish 1", ref iconcommonfish1);
            CheckCfg("Icon - Url for UnCommon Fish 1", ref iconuncommonfish1);
            CheckCfg("Icon - Url for Random Item", ref iconrandomitem);
            CheckCfg("Icon - Url for Rare Fish 1", ref iconrarefish1);
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        private void CheckCfgFloat(string Key, ref float var)
        {

            if (Config[Key] != null)
                var = System.Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
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

        ///////////////////////////////////////////////////////////////////////////////////////////

        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            ["missedfish"] = "You Missed the fish....",
            ["notlookingatwater"] = "You must be aiming at water !!!!",
            ["notstandinginwater"] = "You must be standing in water !!!!",
            ["alreadyfishing"] = "You are already fishing !!",
            ["toosoon"] = "Please wait to try that again !!",
            ["cantmove"] = "You must stay still while fishing !!!",
            ["wrongweapon"] = "You are not holding a fishing pole !!!",
            ["commonfish1"] = "You Got a Savis Island Swordfish",
            ["commonfish2"] = "You Got a Hapis Island RazorJaw",
            ["uncommonfish1"] = "You Got a Colon BlowFish",
            ["rarefish1"] = "You Got a Craggy Island Dorkfish",
            ["randomitem"] = "You found something in the water !!!",
            ["chancetext1"] = "Your chance to catch a fish is : ",
            ["chancetext2"] = "at Current time of : "
        };

        //////////////////////////////////////////////////////////////////////////////////////////

        [ChatCommand("castfishingpole")]
        void cmdChatcastfishingpole(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, "fishing.allowed")) return;
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing) { SendReply(player, msg("alreadyfishing", player.UserIDString)); return; }
	    var incooldown = player.GetComponent<SpearFishingControl>();
	    if (incooldown)  { SendReply(player, msg("toosoon", player.UserIDString)); return; }
            if (!UsingFishingWeapon(player)) { SendReply(player, msg("wrongweapon", player.UserIDString)); return; }
            if (!LookingAtWater(player)) { SendReply(player, msg("notlookingatwater", player.UserIDString)); return; }
            Vector3 whitpos = new Vector3();
            RaycastHit whit;
            if (Physics.Raycast(player.eyes.HeadRay(), out whit, 50f, waterlayer)) whitpos = whit.point;
            var addfishing = player.gameObject.AddComponent<FishingControl>();
            addfishing.SpawnBobber(whitpos);
        }

	[ChatCommand("fishchance")]
        void cmdChatfishchance(BasePlayer player, string command, string[] args)
        {
		catchChanceMod(player, player.transform.position, false);
        }

        ///////////////////////////////////////////////////////////////////////////////////////////

        void catchChanceMod(BasePlayer player, Vector3 hitloc, bool catchfish)
        {
	    int chances = fishchancedefault;
            var currenttime = TOD_Sky.Instance.Cycle.Hour;
            Item activeItem = player.GetActiveItem();
            if (activeItem != null && (activeItem.info.shortname == "spear.stone" || activeItem.info.shortname == "crossbow") && useweaponmod) chances += fishchancemodweaponbonus;
            int hasBoonieOn = player.inventory.containerWear.GetAmount(-1397343301, true);
            if (hasBoonieOn >= 1 && useattiremod) chances += fishchancemodattirebonus;
            int hasPookie = player.inventory.containerMain.GetAmount(640562379, true);
            if (hasPookie >= 1 && useitemmod) chances += fishchancemoditembonus;
            if (currenttime < 8 && currenttime > 6 && usetimemod) chances += fishchancemodtimebonus;
            if (currenttime < 19 && currenttime > 16 && usetimemod) chances += fishchancemodtimebonus;
            if (catchfish) { FishChanceRoll(player, hitloc, chances); return; }
	    if (!catchfish) SendReply(player, lang.GetMessage("chancetext1", this) + chances + "%\n"+lang.GetMessage("chancetext2", this) + currenttime);
        }

        void FishChanceRoll(BasePlayer player, Vector3 hitloc, int fishchance)
        {
            int roll = UnityEngine.Random.Range(0, 100);
            if (roll < fishchance)
            {
                FishTypeRoll(player, hitloc);
                return;
            }
            else
                SendReply(player, msg("missedfish", player.UserIDString));
            return;
        }

        void FishTypeRoll(BasePlayer player, Vector3 hitloc)
        {
            int fishtyperoll = UnityEngine.Random.Range(1, 100);
            if (fishtyperoll < randomitemchance && allowrandomitemchance)
            {
                catchFishCui(player, iconrandomitem);
                SendReply(player, msg("randomitem", player.UserIDString));
                SpawnLootBox(player, hitloc);
                return;
            }
            if (fishtyperoll > 99)
            {
                catchFishCui(player, iconrarefish1);
                SendReply(player, msg("rarefish1", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(865679437, 5));
                player.Command("note.inv", 865679437, 5);
                return;
            }
            if (fishtyperoll >= 90 && fishtyperoll < 100)
            {
                catchFishCui(player, iconuncommonfish1);
                SendReply(player, msg("uncommonfish1", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(865679437, 2));
                player.Command("note.inv", 865679437, 2);
                return;
            }
            if (fishtyperoll > 45 && fishtyperoll < 90)
            {
                catchFishCui(player, iconcommonfish2);
                SendReply(player, msg("commonfish2", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(88869913, 1));
                player.Command("note.inv", 88869913, 1);
                return;
            }
            if (fishtyperoll >= 1 && fishtyperoll <= 45)
            {
                catchFishCui(player, iconcommonfish1);
                SendReply(player, msg("commonfish1", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(865679437, 1));
                player.Command("note.inv", 865679437, 1);
                return;
            }
        }

        ///////////////////////////////////////////////////////////////////////////////////////////

        void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            var player = attacker as BasePlayer;

            if (!IsAllowed(player, "fishing.allowed")) return;
            if (IsAllowed(player, "fishing.allowed"))
            {
                if (hitInfo?.HitEntity as BaseCombatEntity) return;
                if (hitInfo == null) return;

                Vector3 hitloc = hitInfo.HitPositionWorld;
                if (hitInfo.WeaponPrefab.ToString().Contains("spear") || hitInfo.WeaponPrefab.ToString().Contains("bow"))
                {
            		var isfishing = player.GetComponent<FishingControl>();
            		if (isfishing) { SendReply(player, msg("alreadyfishing", player.UserIDString)); return; }
		    	var incooldown = player.GetComponent<SpearFishingControl>();
		    	if (incooldown)  { SendReply(player, msg("toosoon", player.UserIDString)); return; }
                    if (IsStandingInWater(player))
                    {
                        catchChanceMod(player, hitloc, true);
			player.gameObject.AddComponent<SpearFishingControl>();
                        hitInfo.CanGather = true;
                        return;
                    }
                }
                if (player.IsHeadUnderwater())
                {
                    {
                        catchChanceMod(player, hitloc, true);
			player.gameObject.AddComponent<SpearFishingControl>();
                        hitInfo.CanGather = true;
                        return;
                    }
                }
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (!isfishing) return;
            if (input != null)
            {
                if (input.WasJustPressed(BUTTON.FORWARD)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.BACKWARD)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.RIGHT)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.LEFT)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.JUMP)) isfishing.playermoved = true;
            }
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing) isfishing.OnDestroy();
	    var hascooldown = player.GetComponent<SpearFishingControl>();
            if (hascooldown) hascooldown.OnDestroy();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing) isfishing.OnDestroy();
	    var hascooldown = player.GetComponent<SpearFishingControl>();
            if (hascooldown) hascooldown.OnDestroy();
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public bool IsStandingInWater(BasePlayer player)
        {
            if (player.modelState.waterLevel > 0.05f) return true;
            return false;
        }

        bool UsingFishingWeapon(BasePlayer player)
        {
            Item activeItem = player.GetActiveItem();
            if (activeItem != null && activeItem.info.shortname.Contains("spear")) return true;
            return false;
        }

        bool LookingAtWater(BasePlayer player)
        {
            RaycastHit whit;
            RaycastHit hit;
            float Gdistance = 0f;
            float Wdistance = 0f;
            if (Physics.Raycast(player.eyes.HeadRay(), out whit, 50f, waterlayer)) Wdistance = whit.distance;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 50f, groundlayer)) Gdistance = hit.distance;
            if (Gdistance > Wdistance) return true;
	    if (Gdistance == 0 && Wdistance != null) return true;
            return false;
        }

        void SpawnLootBox(BasePlayer player, Vector3 hitloc)
        {
	    var randomlootprefab = "assets/bundled/prefabs/radtown/dmloot/dm tier1 lootbox.prefab";
            int rlroll = UnityEngine.Random.Range(1, 6);
            if (rlroll == 1) randomlootprefab = "assets/bundled/prefabs/radtown/crate_basic.prefab";
            if (rlroll == 2) randomlootprefab = "assets/bundled/prefabs/radtown/crate_elite.prefab";
            if (rlroll == 3) randomlootprefab = "assets/bundled/prefabs/radtown/crate_mine.prefab";
            if (rlroll == 4) randomlootprefab = "assets/bundled/prefabs/radtown/crate_normal.prefab";
            if (rlroll == 5) randomlootprefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab";
            var createdPrefab = GameManager.server.CreateEntity(randomlootprefab, hitloc);
            BaseEntity treasurebox = createdPrefab?.GetComponent<BaseEntity>();
            treasurebox.enableSaving = false;
            treasurebox?.Spawn();
            timer.Once(treasureDespawn, () => CheckTreasureDespawn(treasurebox));
            }

   	void CheckTreasureDespawn(BaseEntity treasurebox)
   	{
         	if (treasurebox != null) treasurebox.Kill(BaseNetworkable.DestroyMode.None);
	}

        void Unload()
        {
            DestroyAll<FishingControl>();
            DestroyAll<SpearFishingControl>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                string guiInfo;
                if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
            }
        }

        static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

	////////////////////////////////////////////////////////////////////////////////////////////////////////////

        void catchFishCui(BasePlayer player, string fishicon)
        {
            if (ShowFishCatchIcon) FishingGui(player, fishicon);
        }

        void FishingGui(BasePlayer player, string fishicon)
        {
            DestroyCui(player);

            var elements = new CuiElementContainer();
            GuiInfo[player.userID] = CuiHelper.GetGuid();

            if (ShowFishCatchIcon)
            {
                elements.Add(new CuiElement
                {
                    Name = GuiInfo[player.userID],
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 1", Url = fishicon, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent { AnchorMin = "0.220 0.03",  AnchorMax = "0.260 0.10" }
                    }
                });
            }

            CuiHelper.AddUi(player, elements);
            timer.Once(1f, () => DestroyCui(player));
        }


        void DestroyCui(BasePlayer player)
        {
            string guiInfo;
            if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
        }

	////////////////////////////////////////////////////////////////////////////////////////////////////////////

        class SpearFishingControl : MonoBehaviour
        {
            BasePlayer player;
            public string anchormaxstr;
            Fishing fishing;
            public int counter;

            void Awake()
            {
                fishing = new Fishing();
                player = base.GetComponentInParent<BasePlayer>();
                counter = 1000;
            }

            void FixedUpdate()
            {
                counter = counter - 10;
                if (counter <= 0) OnDestroy();
                fishingindicator(player, counter);
            }

            public void fishingindicator(BasePlayer player, int counter)
            {
                DestroyCui(player);
                if (counter >= 901 && counter <= 1000) anchormaxstr = "0.60 0.145";
                if (counter >= 801 && counter <= 900) anchormaxstr = "0.58 0.145";
                if (counter >= 701 && counter <= 800) anchormaxstr = "0.56 0.145";
                if (counter >= 601 && counter <= 700) anchormaxstr = "0.54 0.145";
                if (counter >= 501 && counter <= 600) anchormaxstr = "0.52 0.145";
                if (counter >= 401 && counter <= 500) anchormaxstr = "0.50 0.145";
                if (counter >= 301 && counter <= 400) anchormaxstr = "0.48 0.145";
                if (counter >= 201 && counter <= 300) anchormaxstr = "0.46 0.145";
                if (counter >= 101 && counter <= 200) anchormaxstr = "0.44 0.145";
                if (counter >= 0 && counter <= 100) anchormaxstr = "0.42 0.145";
                var fishingindicator = new CuiElementContainer();

                fishingindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = "1.0 0.0 0.0 0.6" },
                    RectTransform = { AnchorMin = "0.40 0.125", AnchorMax = anchormaxstr },
                    Text = { Text = (""), FontSize = 14, Color = "1.0 1.0 1.0 0.6", Align = TextAnchor.MiddleRight }
                }, "Overall", "FishingGui");
                CuiHelper.AddUi(player, fishingindicator);
            }

            void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "FishingGui");
            }

            public void OnDestroy()
            {
                DestroyCui(player);
                Destroy(this);
            }
        }

	////////////////////////////////////////////////////////////////////////////////////////////////////////////

        class FishingControl : MonoBehaviour
        {
            BasePlayer player;
            public string anchormaxstr;
            Fishing fishing;
            public int counter;
            BaseEntity bobber;
            public bool playermoved;
            Vector3 bobberpos;

            void Awake()
            {
                fishing = new Fishing();
                player = base.GetComponentInParent<BasePlayer>();
                counter = 1000;
                playermoved = false;
            }

            public void SpawnBobber(Vector3 pos)
            {
                float waterheight = TerrainMeta.WaterMap.GetHeight(pos);

                pos = new Vector3(pos.x, waterheight, pos.z);
                var createdPrefab = GameManager.server.CreateEntity("assets/prefabs/food/small water bottle/smallwaterbottle.entity.prefab", pos, Quaternion.identity);
                bobber = createdPrefab?.GetComponent<BaseEntity>();
                bobber.enableSaving = false;
                bobber.transform.eulerAngles = new Vector3(270, 0, 0);
                bobber?.Spawn();
                bobberpos = bobber.transform.position;
            }

            void FixedUpdate()
            {
                bobberpos = bobber.transform.position;
                if (playermoved) PlayerMoved();
                counter = counter - 10;
                if (counter <= 0) RollForFish();
                fishingindicator(player, counter);
            }

	    void PlayerMoved()
	    {
		if (bobber != null && !bobber.IsDestroyed) { bobber.Invoke("KillMessage", 0.1f); }
		fishing.SendReply(player, fishing.msg("cantmove", player.UserIDString)); 
		OnDestroy();
	    }

	    void RollForFish()
	    {
		fishing.catchChanceMod(player, bobberpos, true); 
		if (bobber != null && !bobber.IsDestroyed) { bobber.Invoke("KillMessage", 0.1f); } 
		OnDestroy();
	    }

            public void fishingindicator(BasePlayer player, int counter)
            {
                DestroyCui(player);
                if (counter >= 901 && counter <= 1000) anchormaxstr = "0.60 0.145";
                if (counter >= 801 && counter <= 900) anchormaxstr = "0.58 0.145";
                if (counter >= 701 && counter <= 800) anchormaxstr = "0.56 0.145";
                if (counter >= 601 && counter <= 700) anchormaxstr = "0.54 0.145";
                if (counter >= 501 && counter <= 600) anchormaxstr = "0.52 0.145";
                if (counter >= 401 && counter <= 500) anchormaxstr = "0.50 0.145";
                if (counter >= 301 && counter <= 400) anchormaxstr = "0.48 0.145";
                if (counter >= 201 && counter <= 300) anchormaxstr = "0.46 0.145";
                if (counter >= 101 && counter <= 200) anchormaxstr = "0.44 0.145";
                if (counter >= 0 && counter <= 100) anchormaxstr = "0.42 0.145";
                var fishingindicator = new CuiElementContainer();

                fishingindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = "0.0 0.0 1.0 0.6" },
                    RectTransform = { AnchorMin = "0.40 0.125", AnchorMax = anchormaxstr },
                    Text = { Text = (""), FontSize = 14, Color = "1.0 1.0 1.0 0.6", Align = TextAnchor.MiddleRight }
                }, "Overall", "FishingGui");
                CuiHelper.AddUi(player, fishingindicator);
            }

            void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "FishingGui");
            }

            public void OnDestroy()
            {
                DestroyCui(player);
                Destroy(this);
            }
        }
    }
}