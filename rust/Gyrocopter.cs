using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("Gyrocopter", "ColonBlow", "1.0.10", ResourceId = 2521)]
    class Gyrocopter : RustPlugin
    {

        #region Fields and Hooks

        static LayerMask layerMask;
        BaseEntity newCopter;

        void Init()
        {
            ConVar.AntiHack.flyhack_protection = 0;
            lang.RegisterMessages(messages, this);
            LoadVariables();
            layerMask = (1 << 29);
            layerMask |= (1 << 18);
            layerMask = ~layerMask;
            permission.RegisterPermission("gyrocopter.fly", this);
            permission.RegisterPermission("gyrocopter.build", this);
        }

        bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        #endregion

        #region Configuration

        static float MinAltitude = 10f;
        static float RechargeRange = 12f; //Needs to be more than the MinAltitude
        static int SprintCost = 20;
        static int RechargeRate = 3;

	static bool UseCooldown = true;
        static bool NoGodMode = true;

        static float DaBombReloadRange = 12f; //Needs to be more than the MinAltitude
        static float DaBombDamageRadius = 2f;
        static float DaBombDamageAmount = 1000f;

        bool Changed;

        void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        private void LoadConfigVariables()
        {
            CheckCfgFloat("Minimum Flight Altitude : ", ref MinAltitude);
            CheckCfgFloat("Substation Recharge Range (must be higher than Min Altitude) : ", ref RechargeRange);
            CheckCfg("Substation recharge rate : ", ref RechargeRate);
            CheckCfg("Sprint Cost (fast speed) : ", ref SprintCost);

            CheckCfg("Reuse cooldown is enabled : ", ref UseCooldown);
            CheckCfg("Players in God Mode Cannot fly copter : ", ref NoGodMode);

            CheckCfgFloat("Bomb Reload Range : ", ref DaBombReloadRange);
            CheckCfgFloat("Bomb Damage Radius : ", ref DaBombDamageRadius);
            CheckCfgFloat("Bomb Damage Amount : ", ref DaBombDamageAmount);
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
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
                var = Convert.ToSingle(Config[Key]);
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

        #endregion

        #region Language Area

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"helptext1", "type /copterbuild to spawn static copter(with perms)." },
            {"helptext2", "type /copter while looking at copter sign to activate flight mode." },
            {"helptext3", "type /copterland to start landing sequence." },
            {"helptext4", "type /copterdropnet to drop/raise cargo netting." },
            {"helptext5", "type /copterdropbomb to drop Da Bomb." },
            {"helptext6", "type /copterreload to reload bomb (must be next to red rad oil barrel)." },
            {"notauthorized", "You don't have permission to do that !!" },
            {"nocopterfound", "To activate copter, make sure you are looking at the copters sign" },
            {"nogodmode", "You cannot be in god mode to fly the Gyrocopter !!" },
            {"cooldown", "Gyrocopter is still under cooldown, please try again later !!" },
            {"tellabouthelp", "type /copterhelp to see a list of commands !!" },
            {"notflyingcopter", "You are not piloting a gyrocopter !!" },
            {"landingcopter", "Gryocopter Landing Sequence started !!" },
            {"dropnet", "Dropping cargo netting !!" },
            {"raisenet", "Raising cargo netting !!" },
            {"dropbomb", "You just dropped Da Bomb !!" },
            {"outofbombs", "You are out of Da Bombs !!" },
            {"reloadbomb", "You reloaded Da Bomb !!" }
        };

        #endregion

        #region Chat Commands

        [ChatCommand("copterhelp")]
        void chatCopterHelp(BasePlayer player, string command, string[] args)
        {
            SendReply(player, lang.GetMessage("helptext1", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext2", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext3", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext4", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext5", this, player.UserIDString));
            SendReply(player, lang.GetMessage("helptext6", this, player.UserIDString));
        }

        [ChatCommand("copterbuild")]
        void chatBuildCopter(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "gyrocopter.build")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
            AddStaticCopter(player, player.transform.position, true);
        }

        [ChatCommand("copterreload")]
        void chatReloadCopter(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "gyrocopter.fly")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
            var copter = player.GetComponentInParent<PlayerCopter>();
            if (copter == null) { SendReply(player, lang.GetMessage("notflyingcopter", this, player.UserIDString)); return; }
            if (copter != null)
            {
                copter.FindMoreDaBombs(player.transform.position);
            }
        }

        [ChatCommand("copterdropnet")]
        void chatDropNetCopter(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "gyrocopter.fly")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
            var copter = player.GetComponent<PlayerCopter>();
            if (copter == null) { SendReply(player, lang.GetMessage("notflyingcopter", this, player.UserIDString)); return; }
            if (copter != null)
            {
                copter.DropNet();
            }
        }

        [ChatCommand("copterland")]
        void chatLandCopter(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "gyrocopter.fly")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
            var copter = player.GetComponent<PlayerCopter>();
            if (copter == null) { SendReply(player, lang.GetMessage("notflyingcopter", this, player.UserIDString)); return; }
            if (copter != null)
            {
		if (copter.islanding) return;
                copter.islanding = true;
                SendReply(player, lang.GetMessage("landingcopter", this, player.UserIDString));
            }
        }

        [ChatCommand("copterdropbomb")]
        void chatDropBombCopter(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "gyrocopter.fly")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
            var copter = player.GetComponent<PlayerCopter>();
            if (copter == null) { SendReply(player, lang.GetMessage("notflyingcopter", this, player.UserIDString)); return; }
            if (copter != null)
            {
                if ((!copter.hasdabomb1) && (!copter.hasdabomb2))
                {
                    SendReply(player, lang.GetMessage("outofbombs", this, player.UserIDString));
                    return;
                }
                if (copter.hasdabomb1 || copter.hasdabomb2)
                {
                    SendReply(player, lang.GetMessage("dropbomb", this, player.UserIDString));
                    player.gameObject.AddComponent<DaBomb>();
                    copter.usedabomb = true;
                    return;
                }
            }
        }

        #endregion

        #region Gyrocopter helpers

        public void AddStaticCopter(BasePlayer player, Vector3 hit, bool isnew, float cooldown = 0f)
        {
            var hasplayercopter = player.GetComponent<PlayerCopter>();
            if (hasplayercopter) GameObject.Destroy(hasplayercopter);

            var playerpos = hit;
            var groundy = TerrainMeta.HeightMap.GetHeight(playerpos);
            var groundpos = new Vector3(playerpos.x, playerpos.y + 2f, playerpos.z);
            if (isnew) groundpos = new Vector3(playerpos.x, groundy + 2f, playerpos.z);

            string staticprefab = "assets/prefabs/deployable/signs/sign.small.wood.prefab";
            newCopter = GameManager.server.CreateEntity(staticprefab, groundpos, new Quaternion(), true);
	    newCopter?.Spawn();
	    var addstatic = newCopter.gameObject.AddComponent<StaticCopter>();
	    addstatic.incooldown = true;
	    timer.Once(cooldown, () => addstatic.incooldown = false);
        }

	public void AddPlayerCopter(BasePlayer player)
	{
		if (!isAllowed(player, "gyrocopter.fly")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
            	if (NoGodMode && player.IsImmortal()) { SendReply(player, lang.GetMessage("nogodmode", this, player.UserIDString)); return; }

		var addcopter = player.gameObject.AddComponent<PlayerCopter>();	
		SendReply(player, lang.GetMessage("tellabouthelp", this, player.UserIDString));
		var playerpos = player.transform.position;
		player.ClientRPCPlayer(null, player, "ForcePositionTo", playerpos + new Vector3(0f, 10f, 0f));
               	player.SendNetworkUpdate();
	}

        // Prevents Damage to Static Copter Sign and chair itself
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity.name.Contains("sign.small.wood"))
            {
                var iscopter = entity.GetComponentInParent<StaticCopter>();
                if (iscopter) return false;
            }
            if (entity.name.Contains("chair/chair.deployed"))
            {
                var isscopter = entity.GetComponentInParent<StaticCopter>();
                var ispcopter = entity.GetComponentInParent<PlayerCopter>();
                if (isscopter || ispcopter) return false;
            }
            return null;
        }

        object OnPlayerDie(BasePlayer player, HitInfo info)
        {
            var copter = player.GetComponent<PlayerCopter>();
            if (copter)
            {
                copter.islanding = true;
                timer.Once(3f, () => player.Die());
                return false;
            }
            return null;
        }

	void OnSpinWheel(BasePlayer player, SpinnerWheel wheel)
	{
		BaseEntity parentEntity = wheel.GetParentEntity();
		if (parentEntity.name.Contains("sign.small.wood"))
            	{
			var iscopter = parentEntity.GetComponent<StaticCopter>();
			if (iscopter.incooldown && UseCooldown) { SendReply(player, lang.GetMessage("cooldown", this, player.UserIDString)); return; }
                	BaseEntity.saveList.Remove(parentEntity);
                	if (parentEntity != null) { parentEntity.Invoke("KillMessage", 0.1f); }
			AddPlayerCopter(player);
            	}
	}

        #endregion

        #region PlayerCopter

        class PlayerCopter : MonoBehaviour
        {
            BasePlayer player;
            GyroCopter copter;
            Gyrocopter instance;
            FuelControl fuelcontrol;
            Vector3 entitypos;
            Quaternion entityrot;
            int count;
            CopterNet copternet;
            public bool usedabomb;
            public bool hasdabomb1;
            public bool hasdabomb2;
            public bool throttleup;
            public bool islanding;
            public bool isrunning;
            bool didtell;
            float minaltitude;
            float reloadrange;
            int sprintcost;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                copter = player.gameObject.AddComponent<GyroCopter>();
                instance = new Gyrocopter();
                entitypos = player.transform.position;
                entityrot = Quaternion.identity;
                count = 0;
                usedabomb = false;
                hasdabomb1 = true;
                hasdabomb2 = true;
                throttleup = false;
                islanding = false;
                isrunning = false;
                didtell = false;
                minaltitude = MinAltitude;
                reloadrange = DaBombReloadRange;
                sprintcost = SprintCost;
                fuelcontrol = player.gameObject.AddComponent<FuelControl>();
            }

            public void ImpactFX(Vector3 pos)
            {
                Effect.server.Run("assets/bundled/prefabs/fx/weapons/landmine/landmine_explosion.prefab", pos);
                Effect.server.Run("assets/bundled/prefabs/napalm.prefab", pos);
                BaseEntity firebomb = GameManager.server.CreateEntity("assets/bundled/prefabs/oilfireballsmall.prefab", pos);
                firebomb?.Spawn();
            }

            public void DropNet()
            {
                if (copternet == null) { copternet = player.gameObject.AddComponent<CopterNet>(); instance.SendReply(player, instance.lang.GetMessage("dropnet", instance, player.UserIDString)); return; }
                GameObject.Destroy(copternet);
                instance.SendReply(player, instance.lang.GetMessage("raisenet", instance, player.UserIDString));
            }

            public void ReloadDaBombs()
            {
                string prefabbarrel = "assets/bundled/prefabs/radtown/oil_barrel.prefab";
                entitypos = player.transform.position;
                entityrot = Quaternion.identity;
                if (copter.bomb1 == null)
                {
                    copter.bomb1 = GameManager.server.CreateEntity(prefabbarrel, entitypos, entityrot, false);
                    copter.bomb1?.Spawn();
                    copter.bomb1.SetParent(copter.deck2);
                    copter.bomb1.transform.localEulerAngles = new Vector3(0, 0, 0);
                    copter.bomb1.transform.localPosition = new Vector3(0.5f, 1f, 0.3f);
                    copter.bomb1.transform.hasChanged = true;
                    copter.bomb1.SendNetworkUpdateImmediate();
                    instance.SendReply(player, instance.lang.GetMessage("reloadbomb", instance, player.UserIDString));
                    hasdabomb1 = true;
                    return;
                }
                if (copter.bomb2 == null)
                {
                    copter.bomb2 = GameManager.server.CreateEntity(prefabbarrel, entitypos, entityrot, false);
                    copter.bomb2?.Spawn();
                    copter.bomb2.SetParent(copter.deck2);
                    copter.bomb2.transform.localEulerAngles = new Vector3(0, 0, 0);
                    copter.bomb2.transform.localPosition = new Vector3(-0.5f, 1f, 0.3f);
                    copter.bomb2.transform.hasChanged = true;
                    copter.bomb2.SendNetworkUpdateImmediate();
                    instance.SendReply(player, instance.lang.GetMessage("reloadbomb", instance, player.UserIDString));
                    hasdabomb2 = true;
                }
                return;
            }

            public void FindMoreDaBombs(Vector3 localpos)
            {
                List<BaseEntity> barrellist = new List<BaseEntity>();
                Vis.Entities<BaseEntity>(localpos, reloadrange, barrellist);
                foreach (BaseEntity barrel in barrellist)
                {
                    if (barrel.name.Contains("oil_barrel"))
                    {
                        BaseEntity.saveList.Remove(barrel);
                        barrel.Invoke("KillMessage", 0.1f);
                        ReloadDaBombs();
                        return;
                    }
                }
            }

            void FixedUpdate()
            {
                var currentfuel = fuelcontrol.copterfuel;
                Vector3 oldpos = player.transform.position;
                if (currentfuel >= 1) fuelcontrol.copterfuel = currentfuel - 1;
                if (currentfuel <= 0) currentfuel = 0;
                if (NoGodMode && player.IsImmortal() && (!didtell))
                {
                    instance.SendReply(player, instance.lang.GetMessage("nogodmode", instance, player.UserIDString));
                    islanding = true;
                    didtell = true;
                }
                if (player.serverInput.IsDown(BUTTON.SPRINT))
                {
                    throttleup = true;
                    fuelcontrol.copterfuel = fuelcontrol.copterfuel - sprintcost;
                }
                if (copter.barcenter != null)
                {
                    Vector3 startpos = copter.barcenter.transform.eulerAngles;
                    var throttlespeed = 30;
                    if (throttleup) throttlespeed = 60;
                    copter.barcenter.transform.eulerAngles = new Vector3(startpos.x, startpos.y + throttlespeed, startpos.z);
                    copter.barcenter.transform.hasChanged = true;
                    copter.barcenter.SendNetworkUpdateImmediate();
                    if (copter.rotor1 != null) copter.rotor1.transform.hasChanged = true;
                    if (copter.rotor1 != null) copter.rotor1.SendNetworkUpdateImmediate();
                    if (copter.rotor2 != null) copter.rotor2.transform.hasChanged = true;
                    if (copter.rotor2 != null) copter.rotor2.SendNetworkUpdateImmediate();
                    if (copter.rotor3 != null) copter.rotor3.transform.hasChanged = true;
                    if (copter.rotor3 != null) copter.rotor3.SendNetworkUpdateImmediate();
                    if (copter.rotor4 != null) copter.rotor4.transform.hasChanged = true;
                    if (copter.rotor4 != null) copter.rotor4.SendNetworkUpdateImmediate();

                    count = count + 1;
                    if (count == 3)
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/player/swing_weapon.prefab", this.transform.position);
                    }
                    if (count == 6 && throttleup) Effect.server.Run("assets/bundled/prefabs/fx/player/swing_weapon.prefab", this.transform.position);
                    throttleup = false;
                    if (count >= 6) count = 0;
                }
                if (islanding || currentfuel <= 0)
                {
		    islanding = true;
                    var newpos = oldpos + Vector3.down;
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", newpos);
                    player.SendNetworkUpdate();
                    RaycastHit hit;
                    if (Physics.Raycast(new Ray(player.transform.position, Vector3.down), out hit, 3f, layerMask))
                    {
                        Vector3 hitpoint = hit.point;
			GameObject.Destroy(this);
			
			float rechargetime = (float)((10000 - currentfuel)/10);
			if (UseCooldown) instance.SendReply(player, "Current Recharge Time is : " + rechargetime.ToString());
                        instance.timer.Once(0.5f, () => instance.AddStaticCopter(player, hitpoint, false, rechargetime));
                        return;
                    }
                    return;
                }
                if (copter.barcenter == null || copter.rotor1 == null || copter.rotor2 == null)
                {
                    Vector3 position = oldpos + Vector3.down;
                    if (Physics.Raycast(new Ray(player.transform.position, Vector3.down), 1f, layerMask))
                    {
                        ImpactFX(oldpos);
                        GameObject.Destroy(this);
                        return;
                    }
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
                    player.SendNetworkUpdate();
                    return;
                }
                if (Physics.Raycast(new Ray(player.transform.position, Vector3.down), minaltitude, layerMask))
                {
                    var newpos = oldpos + Vector3.up;
                    player.transform.position = newpos;
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", newpos);
                    player.SendNetworkUpdate();
                }
                if (usedabomb)
                {
                    if (copter.bomb1)
                    {
                        BaseEntity.saveList.Remove(copter.bomb1);
                        copter.bomb1.Invoke("KillMessage", 0.1f);
                        copter.bomb1.transform.hasChanged = true;
                        copter.bomb1.SendNetworkUpdateImmediate();
                        usedabomb = false;
                        hasdabomb1 = false;
                        return;
                    }
                    if (copter.bomb2)
                    {
                        BaseEntity.saveList.Remove(copter.bomb2);
                        copter.bomb2.Invoke("KillMessage", 0.1f);
                        copter.bomb2.transform.hasChanged = true;
                        copter.bomb2.SendNetworkUpdateImmediate();
                        usedabomb = false;
                        hasdabomb2 = false;
                        return;
                    }
                    usedabomb = false;
                    hasdabomb1 = false;
                    hasdabomb2 = false;
                }
            }

            void OnDestroy()
            {
                GameObject.Destroy(copter);
                GameObject.Destroy(fuelcontrol);
                GameObject.Destroy(copternet);
                GameObject.Destroy(this);
            }
        }

        #endregion


        #region StaticCopter

        class StaticCopter : MonoBehaviour
        {
            BaseEntity entity;
            GyroCopter copter;
	    public bool incooldown;

            void Awake()
            {
                entity = GetComponent<BaseEntity>();
                copter = entity.gameObject.AddComponent<GyroCopter>();
		incooldown = false;
            }

            void OnDestroy()
            {
                GameObject.Destroy(this);
            }
        }

        #endregion

        #region GyroCopter Entity

        class GyroCopter : BaseEntity
        {
            public BaseEntity entity;
            public BaseEntity wheel;
            public BaseEntity deck1;
            public BaseEntity deck2;
            public BaseEntity barrel;
            public BaseEntity barcenter;
            public BaseEntity chair;
            public BaseEntity rotor1;
            public BaseEntity rotor2;
            public BaseEntity rotor3;
            public BaseEntity rotor4;
            public BaseEntity skid1;
            public BaseEntity skid2;
            public BaseEntity skid3;
            public BaseEntity skid4;
            public BaseEntity tailrotor1;
            public BaseEntity tailrotor2;
            public BaseEntity floor;
            public BaseEntity lootbox;
            public BaseEntity tail1;
            public BaseEntity tail2;
            public BaseEntity bomb1;
            public BaseEntity bomb2;
            Quaternion entityrot;
            Vector3 entitypos;

            string prefabchair = "assets/prefabs/deployable/chair/chair.deployed.prefab";
            string prefabdeck = "assets/prefabs/deployable/signs/sign.post.town.prefab";
            string prefabbar = "assets/prefabs/deployable/signs/sign.post.single.prefab";
            string prefabrotor = "assets/prefabs/deployable/signs/sign.pictureframe.tall.prefab";
            string prefabbarrel = "assets/prefabs/deployable/liquidbarrel/waterbarrel.prefab";
            string prefabbomb = "assets/bundled/prefabs/radtown/oil_barrel.prefab";
            string prefabfloor = "assets/prefabs/building/floor.grill/floor.grill.prefab";
            string prefablootbox = "assets/bundled/prefabs/radtown/dmloot/dm tier3 lootbox.prefab";
            string prefabskid = "assets/prefabs/deployable/mailbox/mailbox.deployed.prefab";
	    string wheelprefab = "assets/prefabs/deployable/spinner_wheel/spinner.wheel.deployed.prefab";

            void Awake()
            {
                entity = GetComponent<BaseEntity>();
                entityrot = Quaternion.identity;
                entitypos = entity.transform.position;

                chair = GameManager.server.CreateEntity(prefabchair, entitypos, entityrot, true);
                deck1 = GameManager.server.CreateEntity(prefabdeck, entitypos, entityrot, true);
                deck2 = GameManager.server.CreateEntity(prefabdeck, entitypos, entityrot, false);
                barrel = GameManager.server.CreateEntity(prefabbarrel, entitypos, entityrot, false);
                barcenter = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, false);
                rotor1 = GameManager.server.CreateEntity(prefabrotor, entitypos, entityrot, false);
                rotor2 = GameManager.server.CreateEntity(prefabrotor, entitypos, entityrot, false);
                rotor3 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, false);
                rotor4 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, false);
                floor = GameManager.server.CreateEntity(prefabdeck, entitypos, entityrot, false);
                lootbox = GameManager.server.CreateEntity(prefablootbox, entitypos, entityrot, true);
                tail1 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, false);
                tail2 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, false);
                tailrotor1 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, false);
                tailrotor2 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, false);
                skid1 = GameManager.server.CreateEntity(prefabskid, entitypos, entityrot, false);
                skid2 = GameManager.server.CreateEntity(prefabskid, entitypos, entityrot, false);
                skid3 = GameManager.server.CreateEntity(prefabskid, entitypos, entityrot, false);
                skid4 = GameManager.server.CreateEntity(prefabskid, entitypos, entityrot, false);
		wheel = GameManager.server.CreateEntity(wheelprefab, entitypos, entityrot, true);

                chair?.Spawn();
                deck1?.Spawn();
                deck2?.Spawn();
                barrel?.Spawn();
                barcenter?.Spawn();
                rotor1?.Spawn();
                rotor2?.Spawn();
                rotor3?.Spawn();
                rotor4?.Spawn();
                floor?.Spawn();
                lootbox?.Spawn();
                tail1?.Spawn();
                tail2?.Spawn();
                tailrotor1?.Spawn();
                tailrotor2?.Spawn();
                skid1?.Spawn();
                skid2?.Spawn();
                skid3?.Spawn();
                skid4?.Spawn();
		wheel?.Spawn();

                chair.SetParent(entity);
                floor.SetParent(entity);
                deck1.SetParent(entity);
                deck2.SetParent(entity);
                barrel.SetParent(entity);
                barcenter.SetParent(barrel);
                rotor1.SetParent(barcenter);
                rotor2.SetParent(barcenter);
                rotor3.SetParent(barcenter);
                rotor4.SetParent(barcenter);
                lootbox.SetParent(deck1);
                tail1.SetParent(deck2);
                tail2.SetParent(deck2);
                tailrotor1.SetParent(tail1);
                tailrotor2.SetParent(tail2);
                skid1.SetParent(deck2);
                skid2.SetParent(deck2);
                skid3.SetParent(deck2);
                skid4.SetParent(deck2);
		wheel.SetParent(entity);

                chair.transform.localEulerAngles = new Vector3(0, 0, 0);
                floor.transform.localEulerAngles = new Vector3(-90, 0, 0);
                deck1.transform.localEulerAngles = new Vector3(90, 0, 0);
                deck2.transform.localEulerAngles = new Vector3(-90, 0, 0);
                barrel.transform.localEulerAngles = new Vector3(0, 180, 0);
                barcenter.transform.localEulerAngles = new Vector3(0, 0, 0);
                rotor1.transform.localEulerAngles = new Vector3(90, 0, 0);
                rotor2.transform.localEulerAngles = new Vector3(90, 0, 0);
                rotor3.transform.localEulerAngles = new Vector3(90, 90, 0);
                rotor4.transform.localEulerAngles = new Vector3(-90, 90, 0);
                lootbox.transform.localEulerAngles = new Vector3(-90, 0, 0);
                tail1.transform.localEulerAngles = new Vector3(0, 90, 0);
                tail2.transform.localEulerAngles = new Vector3(0, 90, 0);
                tailrotor1.transform.localEulerAngles = new Vector3(0, 90, 0);
                tailrotor2.transform.localEulerAngles = new Vector3(0, 90, 0);
                skid1.transform.localEulerAngles = new Vector3(0, 0, 0);
                skid2.transform.localEulerAngles = new Vector3(0, 0, 0);
                skid3.transform.localEulerAngles = new Vector3(0, 0, 180);
                skid4.transform.localEulerAngles = new Vector3(0, 0, 180);
		wheel.transform.localEulerAngles = new Vector3(90, 0, 90);

                chair.transform.localPosition = new Vector3(0f, -1f, 0f);
                floor.transform.localPosition = new Vector3(0f, -1f, 1f);
                deck1.transform.localPosition = new Vector3(0f, -1f, -0.8f);
                deck2.transform.localPosition = new Vector3(0f, -1f, -0.8f);
                barrel.transform.localPosition = new Vector3(0f, -1.5f, -1f);
                barcenter.transform.localPosition = new Vector3(0f, 1f, 0f);
                rotor1.transform.localPosition = new Vector3(0f, 2f, 0.5f);
                rotor2.transform.localPosition = new Vector3(0f, 2f, -3f);
                rotor3.transform.localPosition = new Vector3(-2f, 2f, 0f);
                rotor4.transform.localPosition = new Vector3(2f, 2f, 0f);
                lootbox.transform.localPosition = new Vector3(0f, 1.8f, 0f);
                tail1.transform.localPosition = new Vector3(0.5f, 2f, 0f);
                tail2.transform.localPosition = new Vector3(-0.5f, 2f, 0f);
                tailrotor1.transform.localPosition = new Vector3(0f, 0f, -1f);
                tailrotor2.transform.localPosition = new Vector3(0f, 0f, 1f);
                skid1.transform.localPosition = new Vector3(-0.9f, -1.4f, -0.5f);
                skid2.transform.localPosition = new Vector3(0.9f, -1.4f, -0.5f);
                skid3.transform.localPosition = new Vector3(-0.9f, 1.6f, -0.5f);
                skid4.transform.localPosition = new Vector3(0.9f, 1.6f, -0.5f);
		wheel.transform.localPosition = new Vector3(0.4f, -0.8f, 0f);

                // turns off any interaction to some signs and mailbox
                floor.SetFlag(BaseEntity.Flags.Busy, true, true);
                chair.SetFlag(BaseEntity.Flags.Busy, true, true);
                deck2.SetFlag(BaseEntity.Flags.Busy, true, true);
                rotor3.SetFlag(BaseEntity.Flags.Busy, true, true);
                rotor4.SetFlag(BaseEntity.Flags.Busy, true, true);
                lootbox.SetFlag(BaseEntity.Flags.Busy, true, true);

                AddDaBomb1();
                AddDaBomb2();
            }

            void AddDaBomb1()
            {
                bomb1 = GameManager.server.CreateEntity(prefabbomb, entitypos, entityrot, false);
                bomb1?.Spawn();
                bomb1.SetParent(deck2);
                bomb1.transform.localEulerAngles = new Vector3(0, 0, 0);
                bomb1.transform.localPosition = new Vector3(0.5f, 1f, 0.3f);
            }

            void AddDaBomb2()
            {
                bomb2 = GameManager.server.CreateEntity(prefabbomb, entitypos, entityrot, false);
                bomb2?.Spawn();
                bomb2.SetParent(deck2);
                bomb2.transform.localEulerAngles = new Vector3(0, 0, 0);
                bomb2.transform.localPosition = new Vector3(-0.5f, 1f, 0.3f);
            }

            void OnDestroy()
            {
                BaseEntity.saveList.Remove(bomb2);
                if (bomb2 != null) { bomb2.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(bomb1);
                if (bomb1 != null) { bomb1.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(skid4);
                if (skid4 != null) { skid4.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(skid3);
                if (skid3 != null) { skid3.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(skid2);
                if (skid2 != null) { skid2.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(skid1);
                if (skid1 != null) { skid1.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(tailrotor2);
                if (tailrotor2 != null) { tailrotor2.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(tailrotor1);
                if (tailrotor1 != null) { tailrotor1.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(tail2);
                if (tail2 != null) { tail2.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(tail1);
                if (tail1 != null) { tail1.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(lootbox);
                if (lootbox != null) { lootbox.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(rotor4);
                if (rotor4 != null) { rotor4.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(rotor3);
                if (rotor3 != null) { rotor3.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(rotor2);
                if (rotor2 != null) { rotor2.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(rotor1);
                if (rotor1 != null) { rotor1.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(barcenter);
                if (barcenter != null) { barcenter.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(wheel);
                if (wheel != null) { wheel.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(barrel);
                if (barrel != null) { barrel.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(deck2);
                if (deck2 != null) { deck2.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(deck1);
                if (deck1 != null) { deck1.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(floor);
                if (floor != null) { floor.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(chair);
                if (chair != null) { chair.Invoke("KillMessage", 0.1f); }
            }
        }

        #endregion

        #region DaBomb Spawner

        class DaBomb : MonoBehaviour
        {
            BaseEntity dabomb;
            BasePlayer player;
            GyroCopter copter;
            Vector3 PlayerPOS;
            bool onGround;
            float damageradius;
            float damageamount;

            void Awake()
            {
                onGround = false;
                damageradius = DaBombDamageRadius;
                damageamount = DaBombDamageAmount;
                player = GetComponent<BasePlayer>();
                copter = player.GetComponent<GyroCopter>();
                PlayerPOS = player.transform.position + new Vector3(1, 0, 0);
                dabomb = GameManager.server.CreateEntity("assets/bundled/prefabs/radtown/oil_barrel.prefab", PlayerPOS, new Quaternion(), true);
                dabomb.Spawn();
                SpawnFireEffects();
            }

            void ImpactDamage(Vector3 hitpos)
            {
                List<BaseCombatEntity> playerlist = new List<BaseCombatEntity>();
                Vis.Entities<BaseCombatEntity>(hitpos, damageradius, playerlist);
                foreach (BaseCombatEntity p in playerlist)
                {
                    if (!(p is BuildingPrivlidge))
                    {
                        p.Hurt(damageamount, Rust.DamageType.Blunt, null, false);
                    }
                }
            }

            public void ImpactFX(Vector3 pos)
            {
                Effect.server.Run("assets/bundled/prefabs/fx/weapons/landmine/landmine_explosion.prefab", pos);
                Effect.server.Run("assets/bundled/prefabs/napalm.prefab", pos);
                BaseEntity firebomb = GameManager.server.CreateEntity("assets/bundled/prefabs/oilfireballsmall.prefab", pos);
                firebomb?.Spawn();
            }

            void SpawnFireEffects()
            {
                BaseEntity flame = GameManager.server.CreateEntity("assets/bundled/prefabs/fireball.prefab", new Vector3(), new Quaternion(), true);
                FireBall fireball = flame.GetComponent<FireBall>();
                fireball.SetParent(dabomb);
                fireball.Spawn();
            }

            void FixedUpdate()
            {
                if (onGround) return;
                if (Physics.Raycast(new Ray(dabomb.transform.position, Vector3.down), 1f, layerMask))
                {
                    ImpactDamage(dabomb.transform.position);
                    ImpactFX(dabomb.transform.position);
                    BaseEntity.saveList.Remove(dabomb);
                    if (dabomb != null) { dabomb.Invoke("KillMessage", 0.1f); }
                    GameObject.Destroy(this);
                    onGround = true;
                }
                Quaternion qTo = Quaternion.Euler(new Vector3(UnityEngine.Random.Range(-180.0f, 180.0f), UnityEngine.Random.Range(-180.0f, 180.0f), UnityEngine.Random.Range(-180.0f, 180.0f)));
                dabomb.transform.rotation = Quaternion.Slerp(dabomb.transform.rotation, qTo, Time.deltaTime * 3.0f);
                dabomb.transform.position = dabomb.transform.position + Vector3.down * (10f * Time.deltaTime);
                dabomb.transform.hasChanged = true;
                dabomb.SendNetworkUpdateImmediate();
            }

            void OnDestroy()
            {
                BaseEntity.saveList.Remove(dabomb);
                if (dabomb != null) { dabomb.Invoke("KillMessage", 0.1f); }
                GameObject.Destroy(this);
            }
        }

        #endregion

        #region CopterNet Spawner

        class CopterNet : MonoBehaviour
        {
            BaseEntity netting1;
            BaseEntity netting2;
            BaseEntity netting3;
            BasePlayer player;
            GyroCopter copter;
            Vector3 entitypos;
            Quaternion entityrot;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                copter = player.GetComponentInParent<GyroCopter>();
                entitypos = player.transform.position;
                entityrot = Quaternion.identity;
                string prefabnetting = "assets/prefabs/building/wall.frame.netting/wall.frame.netting.prefab";

                netting1 = GameManager.server.CreateEntity(prefabnetting, entitypos, entityrot, false);
                netting1?.Spawn();
                netting1.transform.localEulerAngles = new Vector3(0, 0, 0);
                netting1.transform.localPosition = new Vector3(0.9f, -3.7f, -1.5f);
                var netstab1 = netting1.GetComponent<StabilityEntity>();
                netstab1.grounded = true;
                netting1.SetParent(player);

                netting2 = GameManager.server.CreateEntity(prefabnetting, entitypos, entityrot, false);
                netting2?.Spawn();
                netting2.transform.localEulerAngles = new Vector3(0, 0, 0);
                netting2.transform.localPosition = new Vector3(0.9f, -6.7f, -1.5f);
                var netstab2 = netting2.GetComponent<StabilityEntity>();
                netstab2.grounded = true;
                netting2.SetParent(player);

                netting3 = GameManager.server.CreateEntity(prefabnetting, entitypos, entityrot, false);
                netting3?.Spawn();
                netting3.transform.localEulerAngles = new Vector3(0, 0, 0);
                netting3.transform.localPosition = new Vector3(0.9f, -9.7f, -1.5f);
                var netstab3 = netting3.GetComponent<StabilityEntity>();
                netstab3.grounded = true;
                netting3.SetParent(player);
            }

            void OnDestroy()
            {
                BaseEntity.saveList.Remove(netting3);
                if (netting3 != null) { netting3.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(netting2);
                if (netting2 != null) { netting2.Invoke("KillMessage", 0.1f); }
                BaseEntity.saveList.Remove(netting1);
                if (netting1 != null) { netting1.Invoke("KillMessage", 0.1f); }
                GameObject.Destroy(this);
            }
        }

        #endregion

        #region FuelControl and Fuel Cui

        class FuelControl : MonoBehaviour
        {
            BasePlayer player;
            PlayerCopter copter;
            BaseEntity entity;
            public int copterfuel;
            public string anchormaxstr;
            public string colorstr;
            Vector3 playerpos;
            Gyrocopter instance;
            bool ischarging;
            int count;
            float rechargerange;
            int rechargerate;

            void Awake()
            {
                instance = new Gyrocopter();
                rechargerange = RechargeRange;
                rechargerate = RechargeRate;
                player = GetComponentInParent<BasePlayer>();
                copter = GetComponentInParent<PlayerCopter>();
                playerpos = player.transform.position;
                copterfuel = 10000;
                ischarging = false;
                count = 0;
            }

            void Recharge()
            {
                var hits = Physics.OverlapSphere(copter.transform.position, rechargerange);
                foreach (var hit in hits)
                {
                    if (hit.name.Contains("substation"))
                    {
                        ischarging = true;
                        ChargingFX();
                        RechargeIndicator(player);
                        copterfuel = copterfuel + rechargerate;
                        return;
                    }
                }
                DestroyChargeCui(player);
                ischarging = false;
            }

            void ChargingFX()
            {
                if (count == 15)
                {
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", playerpos + Vector3.down);
                    count = 0;
                    return;
                }
                count = count + 1;
            }

            void FixedUpdate()
            {
                playerpos = player.transform.position;
                if (copterfuel >= 10000) copterfuel = 10000;
                if (copterfuel <= 0) copterfuel = 0;
                fuelIndicator(player, copterfuel);
                Recharge();
            }

            public void RechargeIndicator(BasePlayer player)
            {
                DestroyChargeCui(player);
                if (ischarging == false) return;
                var chargeindicator = new CuiElementContainer();
                chargeindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = "1.0 1.0 0.0 0.8" },
                    RectTransform = { AnchorMin = "0.47 0.155", AnchorMax = "0.53 0.175" },
                    Text = { Text = ("CHARGING"), FontSize = 14, Color = "0.0 0.0 0.0 1.0", Align = TextAnchor.MiddleCenter }
                }, "Overall", "recharge");
                CuiHelper.AddUi(player, chargeindicator);
            }

            public void fuelIndicator(BasePlayer player, int fuel)
            {
                DestroyCui(player);
                var displayfuel = fuel;
                var fuelstr = displayfuel.ToString();
                var colorstrred = "0.6 0.1 0.1 0.8";
                var colorstryellow = "0.8 0.8 0.0 0.8";
                var colorstrgreen = "0.0 0.6 0.1 0.8";
                colorstr = colorstrgreen;
                if (fuel >= 9001) anchormaxstr = "0.60 0.145";
                if (fuel >= 8001 && fuel <= 9000) anchormaxstr = "0.58 0.145";
                if (fuel >= 7001 && fuel <= 8000) anchormaxstr = "0.56 0.145";
                if (fuel >= 6001 && fuel <= 7000) anchormaxstr = "0.54 0.145";
                if (fuel >= 5001 && fuel <= 6000) anchormaxstr = "0.52 0.145";
                if (fuel >= 4001 && fuel <= 5000) anchormaxstr = "0.50 0.145";
                if (fuel >= 3001 && fuel <= 4000) { anchormaxstr = "0.48 0.145"; colorstr = colorstryellow; }
                if (fuel >= 2001 && fuel <= 3000) { anchormaxstr = "0.46 0.145"; colorstr = colorstryellow; }
                if (fuel >= 1001 && fuel <= 2000) { anchormaxstr = "0.44 0.145"; colorstr = colorstrred; }
                if (fuel <= 1000) { anchormaxstr = "0.42 0.145"; colorstr = colorstrred; }
                var fuelindicator = new CuiElementContainer();
                fuelindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = "0.0 0.0 0.0 0.3" },
                    RectTransform = { AnchorMin = "0.40 0.12", AnchorMax = "0.60 0.15" },
                    Text = { Text = (""), FontSize = 18, Color = "1.0 1.0 1.0 1.0", Align = TextAnchor.MiddleLeft }
                }, "Overall", "fuelGuia");

                fuelindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = colorstr },
                    RectTransform = { AnchorMin = "0.40 0.125", AnchorMax = anchormaxstr },
                    Text = { Text = (fuelstr), FontSize = 14, Color = "1.0 1.0 1.0 0.6", Align = TextAnchor.MiddleRight }
                }, "Overall", "fuelGui");

                CuiHelper.AddUi(player, fuelindicator);
            }

            void DestroyChargeCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "recharge");
            }

            void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "fuelGui");
                CuiHelper.DestroyUi(player, "fuelGuia");
            }

            void OnDestroy()
            {
                DestroyChargeCui(player);
                DestroyCui(player);
                Destroy(this);
            }
        }

        #endregion

        #region Helpers

        void RemoveCopter(BasePlayer player)
        {
            var hasgyro = player.GetComponent<PlayerCopter>();
            if (hasgyro == null) return;
            GameObject.Destroy(hasgyro);
            return;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            RemoveCopter(player);
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            RemoveCopter(player);
        }

        static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        void Unload()
        {
            DestroyAll<StaticCopter>();
            DestroyAll<PlayerCopter>();
            DestroyAll<GyroCopter>();
            DestroyAll<DaBomb>();
            DestroyAll<FuelControl>();
            DestroyAll<CopterNet>();
        }

        #endregion

    }
}