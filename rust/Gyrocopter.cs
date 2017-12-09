using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("Gyrocopter", "ColonBlow", "1.1.2", ResourceId = 2521)]
    class Gyrocopter : RustPlugin
    {

        #region Fields and Hooks

        static LayerMask layerMask;
	BaseEntity newCopter;

        void Init()
        {
            lang.RegisterMessages(messages, this);
            LoadVariables();
            layerMask = (1 << 29);
            layerMask |= (1 << 18);
            layerMask = ~layerMask;
            permission.RegisterPermission("gyrocopter.fly", this);
            permission.RegisterPermission("gyrocopter.build", this);
            permission.RegisterPermission("gyrocopter.unlimited", this);
        }

        bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        #endregion

        #region Configuration

        static float MinAltitude = 10f;
        static float RechargeRange = 12f; //Needs to be more than the MinAltitude

	static int NormalCost = 5;
        static int SprintCost = 20;

        static int BonusRechargeRate = 5;
	static int BaseRechargeRate = 1;

	static float NormalSpeed = 12f;
	static float SprintSpeed = 25f;

	bool OwnerLockPaint = true;

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
            CheckCfgFloat("Recharge - Range - From substation (must be higher than Min Altitude) : ", ref RechargeRange);
            CheckCfgFloat("Speed - Normal Flight Speed is : ", ref NormalSpeed);
            CheckCfgFloat("Speed - Sprint Flight Speed is : ", ref SprintSpeed);
            CheckCfg("Recharge - Bonus Substation Rate : ", ref BonusRechargeRate);
            CheckCfg("Recharge - Base Rate : ", ref BaseRechargeRate);
            CheckCfg("Movement - Normal - Cost (normal speeed) : ", ref NormalCost);
            CheckCfg("Movement - Sprint - Cost (fast speed) : ", ref SprintCost);
            CheckCfg("Only the Builder (owner) of copter can lock paint job : ", ref OwnerLockPaint);
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
            {"helptext1", "type /copterbuild to spawn a gyrocopter and automount it." },
            {"helptext2", "type /copterlockpaint to lock copter paintjob and /copterunlockpaint to unlock" },
            {"helptext3", "use Spinner wheel while seated to start and stop flying copter." },
            {"helptext4", "Rehcharge - land copter to recharge, hover over substation to fast charge." },
            {"helptext5", "Locking codelock will prevent anyone from using copter (even owner)." },
            {"helptext6", "Once copter runs out of charge, it will autoland." },
            {"notauthorized", "You don't have permission to do that !!" },
            {"cooldown", "Gyrocopter is still under cooldown, please try again later !!" },
            {"tellabouthelp", "type /copterhelp to see a list of commands !!" },
            {"notflyingcopter", "You are not piloting a gyrocopter !!" },
            {"landingcopter", "Gryocopter Landing Sequence started !!" }
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
            AddCopter(player, player.transform.position);
        }

        [ChatCommand("copterlockpaint")]
        void chatCopterLockPaint(BasePlayer player, string command, string[] args)
        {
            	if (!player.isMounted) return;
            	var activecopter = player.GetMounted().GetComponentInParent<GyroCopter>();
		if (activecopter == null) return;
		if (!isAllowed(player, "gyrocopter.fly")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
                if (activecopter.islanding) return;
		if (OwnerLockPaint && activecopter.ownerid != player.userID) return;
                if (!activecopter.paintingsarelocked) { activecopter.LockPaintings(); return; }
        }

        [ChatCommand("copterunlockpaint")]
        void chatCopterUnLockPaint(BasePlayer player, string command, string[] args)
        {
            	if (!player.isMounted) return;
            	var activecopter = player.GetMounted().GetComponentInParent<GyroCopter>();
		if (activecopter == null) return;
		if (!isAllowed(player, "gyrocopter.fly")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
                if (activecopter.islanding) return;
		if (OwnerLockPaint && activecopter.ownerid != player.userID) return;
		if (activecopter.paintingsarelocked) { activecopter.UnLockPaintings(); return; }
        }

        private void AddCopter(BasePlayer player, Vector3 location)
        {
	    if (player == null && location == null) return;
            if (location == null && player != null) location = player.transform.position;
            var spawnpos = new Vector3(location.x, location.y + 0.5f, location.z);
            string staticprefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";
            newCopter = GameManager.server.CreateEntity(staticprefab, spawnpos, new Quaternion(), true);
            var chairmount = newCopter.GetComponent<BaseMountable>();
            chairmount.isMobile = true;
	    newCopter.enableSaving = false;
            newCopter.OwnerID = player.userID;
            newCopter.Spawn();
            var gyrocopter = newCopter.gameObject.AddComponent<GyroCopter>();
	    if (chairmount != null && player != null && isAllowed(player, "gyrocopter.fly")) { chairmount.MountPlayer(player); return; }
	    var passengermount = newCopter.GetComponent<GyroCopter>().passengerchair1.GetComponent<BaseMountable>();
	    if (passengermount != null && player != null && isAllowed(player, "gyrocopter.build")) { passengermount.MountPlayer(player); return; }
        }

        [ChatCommand("copterswag")]
        void chatGetCopterSwag(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "gyrocopter.fly")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
	    Item num = ItemManager.CreateByItemID(-864578046, 1, 961776748);
	    player.inventory.GiveItem(num, null);
            player.Command("note.inv", -864578046, 1);
        }

       void OnPlayerInput(BasePlayer player, InputState input)
        {
		if (player == null || input == null) return;
            	if (!player.isMounted) return;
            	var activecopter = player.GetMounted().GetComponentInParent<GyroCopter>();
		if (activecopter == null) return;
            	if (player.GetMounted() != activecopter.entity) return;
		if (!isAllowed(player, "gyrocopter.fly")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
		if (input != null)
		{
                	if (input.WasJustPressed(BUTTON.FORWARD)) activecopter.moveforward = true;
                	if (input.WasJustReleased(BUTTON.FORWARD)) activecopter.moveforward = false;
                	if (input.WasJustPressed(BUTTON.BACKWARD)) activecopter.movebackward = true;
                	if (input.WasJustReleased(BUTTON.BACKWARD)) activecopter.movebackward = false;
                	if (input.WasJustPressed(BUTTON.RIGHT)) activecopter.rotright = true;
                	if (input.WasJustReleased(BUTTON.RIGHT)) activecopter.rotright = false;
                	if (input.WasJustPressed(BUTTON.LEFT)) activecopter.rotleft = true;
                	if (input.WasJustReleased(BUTTON.LEFT)) activecopter.rotleft = false;
                	if (input.IsDown(BUTTON.SPRINT)) activecopter.throttleup = true;
                	if (input.WasJustReleased(BUTTON.SPRINT)) activecopter.throttleup = false;
                	if (input.WasJustPressed(BUTTON.JUMP)) activecopter.moveup = true;
                	if (input.WasJustReleased(BUTTON.JUMP)) activecopter.moveup = false;
               	 	if (input.WasJustPressed(BUTTON.DUCK)) activecopter.movedown = true;
                	if (input.WasJustReleased(BUTTON.DUCK)) activecopter.movedown = false;
		return;
		}
	return;
        }

        private object CanDismountEntity(BaseMountable mountable, BasePlayer player)
        {
	    if (mountable == null || player == null) return null;	
	    if (player.GetComponent<BaseCombatEntity>().IsDead()) return null;
            if (mountable.GetComponent<BaseEntity>() == newCopter)
            {
                var activecopter = mountable.GetComponentInParent<GyroCopter>();
            	if (activecopter != null)
            	{
                	if(activecopter.engineon) return false;
            	}
            }
            return null;
        }

        object CanMountEntity(BaseMountable mountable, BasePlayer player)
        {
	    if (mountable == null || player == null) return null;
            if (mountable.GetComponent<BaseEntity>() == newCopter)
            {
		if (!isAllowed(player, "gyrocopter.fly")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return false; }
            	var activecopter = mountable.GetComponentInParent<GyroCopter>();
            	if (activecopter != null)
            	{
			if(activecopter.copterlock != null && activecopter.copterlock.IsLocked()) return false;
                	if(activecopter.engineon) return false;
            	}
            }
            return null;
        }

        void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            var activecopter = mountable.GetComponentInParent<GyroCopter>();
            if (activecopter != null)
            {
		if (mountable.GetComponent<BaseEntity>() != activecopter.entity) return;
		player.gameObject.AddComponent<FuelControl>();
                activecopter.AddPilot(player);
            }
        }

       void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            var activecopter = mountable.GetComponentInParent<GyroCopter>();
            if (activecopter != null)
            {
	        if (mountable.GetComponent<BaseEntity>() != activecopter.entity) return;
		RemoveCopter(player);
                activecopter.RemovePilot();
            }
        }

        object CanPickupEntity(BaseCombatEntity entity, BasePlayer player)
        {
            if (entity == null || player == null) return null;
            if (entity.GetComponentInParent<GyroCopter>()) return false;
            return null;
        }

	object CanPickupLock(BasePlayer player, BaseLock baseLock)
	{
            if (baseLock == null || player == null) return null;
            if (baseLock.GetComponentInParent<GyroCopter>()) return false;
            return null;
	}

        #endregion

        #region Gyrocopter helpers

        // Prevents Damage to GyroCopter
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return null;
	    var activecopter = entity.GetComponentInParent<GyroCopter>();
            if (activecopter)
		{
		 return false;
		}
            return null;
        }

        void OnSpinWheel(BasePlayer player, SpinnerWheel wheel)
        {
            if (!player.isMounted) return;
            var activecopter = player.GetMounted().GetComponentInParent<GyroCopter>();
	    if (activecopter == null) return;
            if (player.GetMounted() != activecopter.entity) return;
	    if (activecopter != null)
	    {
		if (!isAllowed(player, "gyrocopter.fly")) { SendReply(player, lang.GetMessage("notauthorized", this, player.UserIDString)); return; }
            	var ison = activecopter.engineon;
            	if (ison) { activecopter.islanding = true; SendReply(player, lang.GetMessage("landingcopter", this, player.UserIDString)); wheel.velocity = 0f; return; }
            	if (!ison) { activecopter.engineon = true; wheel.velocity = 0f; return; }
	    }
        }

        #endregion

        #region GyroCopter Entity

        class GyroCopter : BaseEntity
        {
            public BaseEntity entity;
	    public BasePlayer player;
            public BaseEntity wheel;
            public BaseEntity deck1;
            public BaseEntity deck2;
            public BaseEntity barrel;
            public BaseEntity barcenter;
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
            public BaseEntity nosesign;
            public BaseEntity tail1;
            public BaseEntity tail2;
	    public BaseEntity passengerchair1;
	    public BaseEntity passengerchair2;
	    public BaseEntity copterlock;
	    public BaseEntity panel;

            Quaternion entityrot;
            Vector3 entitypos;
            public bool moveforward;
            public bool movebackward;
	    public bool moveup;
	    public bool movedown;
            public bool rotright;
            public bool rotleft;
            public bool sprinting;
	    public bool islanding;
	    public bool hasbonuscharge;
     	    public bool paintingsarelocked;

	    public ulong ownerid;
            int count;
            public bool engineon;
	    float minaltitude;
	    Gyrocopter instance;
            public bool throttleup;
            int sprintcost;
	    int normalcost;
	    float sprintspeed;
	    float normalspeed;
	    public int currentfuel;
	    int baserechargerate;
	    int bonusrechargerate;

            string prefabdeck = "assets/prefabs/deployable/signs/sign.post.town.prefab";
            string prefabbar = "assets/prefabs/deployable/signs/sign.post.single.prefab";
            string prefabrotor = "assets/prefabs/deployable/signs/sign.pictureframe.tall.prefab";
            string prefabbarrel = "assets/prefabs/deployable/liquidbarrel/waterbarrel.prefab";
            string prefabbomb = "assets/bundled/prefabs/radtown/oil_barrel.prefab";
            string prefabfloor = "assets/prefabs/building/floor.grill/floor.grill.prefab";
            string prefabnosesign = "assets/prefabs/deployable/signs/sign.medium.wood.prefab";
	    string prefabpanel = "assets/prefabs/deployable/signs/sign.small.wood.prefab";	
            string prefabskid = "assets/prefabs/deployable/mailbox/mailbox.deployed.prefab";
            string wheelprefab = "assets/prefabs/deployable/spinner_wheel/spinner.wheel.deployed.prefab";
	    string copterlockprefab = "assets/prefabs/locks/keypad/lock.code.prefab"; 

            void Awake()
            {
                entity = GetComponentInParent<BaseEntity>();
                entityrot = Quaternion.identity;
                entitypos = entity.transform.position;
                minaltitude = MinAltitude;
		instance = new Gyrocopter();
		ownerid = entity.OwnerID;
		baserechargerate = BaseRechargeRate;
		bonusrechargerate = BonusRechargeRate;

                engineon = false;
                moveforward = false;
                movebackward = false;
		moveup = false;
		movedown = false;
                rotright = false;
                rotleft = false;
                sprinting = false;
		islanding = false;
                throttleup = false;
		hasbonuscharge = false;
		paintingsarelocked = false;
                sprintcost = SprintCost;
		sprintspeed = SprintSpeed;
		normalcost = NormalCost;
		normalspeed = NormalSpeed;
		currentfuel = 10000;

                deck1 = GameManager.server.CreateEntity(prefabdeck, entitypos, entityrot, true);
                deck2 = GameManager.server.CreateEntity(prefabdeck, entitypos, entityrot, true);
                barrel = GameManager.server.CreateEntity(prefabbarrel, entitypos, entityrot, true);
                barcenter = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, true);
                rotor1 = GameManager.server.CreateEntity(prefabrotor, entitypos, entityrot, true);
                rotor2 = GameManager.server.CreateEntity(prefabrotor, entitypos, entityrot, true);
                rotor3 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, true);
                rotor4 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, true);
                floor = GameManager.server.CreateEntity(prefabdeck, entitypos, entityrot, true);
                nosesign = GameManager.server.CreateEntity(prefabnosesign, entitypos, entityrot, true);
                tail1 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, true);
                tail2 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, true);
                tailrotor1 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, true);
                tailrotor2 = GameManager.server.CreateEntity(prefabbar, entitypos, entityrot, true);
                skid1 = GameManager.server.CreateEntity(prefabskid, entitypos, entityrot, true);
                skid2 = GameManager.server.CreateEntity(prefabskid, entitypos, entityrot, true);
                skid3 = GameManager.server.CreateEntity(prefabskid, entitypos, entityrot, true);
                skid4 = GameManager.server.CreateEntity(prefabskid, entitypos, entityrot, true);
                wheel = GameManager.server.CreateEntity(wheelprefab, entitypos, entityrot, true);

                deck1?.Spawn();
                deck2?.Spawn();
                barrel?.Spawn();
                barcenter?.Spawn();
                rotor1?.Spawn();
                rotor2?.Spawn();
                rotor3?.Spawn();
                rotor4?.Spawn();
                floor?.Spawn();
                nosesign?.Spawn();
                tail1?.Spawn();
                tail2?.Spawn();
                tailrotor1?.Spawn();
                tailrotor2?.Spawn();
                skid1?.Spawn();
                skid2?.Spawn();
                skid3?.Spawn();
                skid4?.Spawn();
                wheel?.Spawn();

                floor.SetParent(entity);
                deck1.SetParent(entity);
                deck2.SetParent(entity);
                barrel.SetParent(entity);
                barcenter.SetParent(barrel);
                rotor1.SetParent(barcenter);
                rotor2.SetParent(barcenter);
                rotor3.SetParent(barcenter);
                rotor4.SetParent(barcenter);
                nosesign.SetParent(deck1);
                tail1.SetParent(deck2);
                tail2.SetParent(deck2);
                tailrotor1.SetParent(tail1);
                tailrotor2.SetParent(tail2);
                skid1.SetParent(deck2);
                skid2.SetParent(deck2);
                skid3.SetParent(deck2);
                skid4.SetParent(deck2);
                wheel.SetParent(entity);

                floor.transform.localEulerAngles = new Vector3(-90, 0, 0);
                deck1.transform.localEulerAngles = new Vector3(90, 0, 0);
                deck2.transform.localEulerAngles = new Vector3(-90, 0, 0);
                barrel.transform.localEulerAngles = new Vector3(0, 180, 0);
                barcenter.transform.localEulerAngles = new Vector3(0, 0, 0);
                rotor1.transform.localEulerAngles = new Vector3(90, 0, 0);
                rotor2.transform.localEulerAngles = new Vector3(90, 0, 0);
                rotor3.transform.localEulerAngles = new Vector3(90, 90, 0);
                rotor4.transform.localEulerAngles = new Vector3(-90, 90, 0);
                nosesign.transform.localEulerAngles = new Vector3(-140, 0, 0);
                tail1.transform.localEulerAngles = new Vector3(0, 90, 0);
                tail2.transform.localEulerAngles = new Vector3(0, 90, 0);
                tailrotor1.transform.localEulerAngles = new Vector3(0, 90, 0);
                tailrotor2.transform.localEulerAngles = new Vector3(0, 90, 0);
                skid1.transform.localEulerAngles = new Vector3(0, 0, 0);
                skid2.transform.localEulerAngles = new Vector3(0, 0, 0);
                skid3.transform.localEulerAngles = new Vector3(0, 0, 180);
                skid4.transform.localEulerAngles = new Vector3(0, 0, 180);
                wheel.transform.localEulerAngles = new Vector3(90, 0, 90);

                floor.transform.localPosition = new Vector3(0f, 0f, 1.4f);
                deck1.transform.localPosition = new Vector3(0f, 0f, -0.4f);
                deck2.transform.localPosition = new Vector3(0f, 0f, -0.2f);
                barrel.transform.localPosition = new Vector3(0f, -0.5f, -1.1f);
                barcenter.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                rotor1.transform.localPosition = new Vector3(0f, 2f, 0.6f);
                rotor2.transform.localPosition = new Vector3(0f, 2f, -3f);
                rotor3.transform.localPosition = new Vector3(-2f, 2f, 0f);
                rotor4.transform.localPosition = new Vector3(2f, 2f, 0f);
                nosesign.transform.localPosition = new Vector3(0f, 2.7f, 0.2f);
                tail1.transform.localPosition = new Vector3(0.5f, 2f, 0f);
                tail2.transform.localPosition = new Vector3(-0.5f, 2f, 0f);
                tailrotor1.transform.localPosition = new Vector3(0f, 0f, -1f);
                tailrotor2.transform.localPosition = new Vector3(0f, 0f, 1f);
                skid1.transform.localPosition = new Vector3(-0.9f, -1.4f, -0.5f);
                skid2.transform.localPosition = new Vector3(0.9f, -1.4f, -0.5f);
                skid3.transform.localPosition = new Vector3(-0.9f, 1.6f, -0.5f);
                skid4.transform.localPosition = new Vector3(0.9f, 1.6f, -0.5f);
                wheel.transform.localPosition = new Vector3(0.8f, 0.4f, 0f);

		skid1.SetFlag(BaseEntity.Flags.Busy, true, true);
		skid2.SetFlag(BaseEntity.Flags.Busy, true, true);
		skid3.SetFlag(BaseEntity.Flags.Busy, true, true);
		skid4.SetFlag(BaseEntity.Flags.Busy, true, true);

		SpawnPassengerChair();
		SpawnCopterLock();
            }

	    public void SpawnCopterLock()
	    {
                panel = GameManager.server.CreateEntity(prefabpanel, entitypos, entityrot, true);
                panel.transform.localEulerAngles = new Vector3(210, 0, 0);
                panel.transform.localPosition = new Vector3(0f, 0.4f, 1.7f);
                panel?.Spawn();
                panel.SetParent(entity);
		
                copterlock = GameManager.server.CreateEntity(copterlockprefab, entitypos, entityrot, true);
                copterlock.transform.localEulerAngles = new Vector3(0, 90, 30);
                copterlock.transform.localPosition = new Vector3(0f, 0.3f, 1.55f);
                copterlock?.Spawn();
                copterlock.SetParent(entity);
	    }

            public void SpawnPassengerChair()
            {
                string prefabchair = "assets/prefabs/deployable/chair/chair.deployed.prefab";
                passengerchair1 = GameManager.server.CreateEntity(prefabchair, entitypos, entityrot, true);
                passengerchair1.transform.localEulerAngles = new Vector3(0, 90, 0);
                passengerchair1.transform.localPosition = new Vector3(0.7f, -0.1f, -2.1f);
                var rmount1 = passengerchair1.GetComponent<BaseMountable>();
                rmount1.isMobile = true;
                passengerchair1?.Spawn();
                passengerchair1.SetParent(entity);

                passengerchair2 = GameManager.server.CreateEntity(prefabchair, entitypos, entityrot, true);
                passengerchair2.transform.localEulerAngles = new Vector3(0, 270, 0);
                passengerchair2.transform.localPosition = new Vector3(-0.7f, -0.1f, -2.1f);
                var rmount2 = passengerchair2.GetComponent<BaseMountable>();
                rmount2.isMobile = true;
                passengerchair2?.Spawn();
                passengerchair2.SetParent(entity);
            }

	    public void LockPaintings()
	    {
		floor.SetFlag(BaseEntity.Flags.Busy, true, true);
		deck1.SetFlag(BaseEntity.Flags.Busy, true, true);
		deck2.SetFlag(BaseEntity.Flags.Busy, true, true);
		barrel.SetFlag(BaseEntity.Flags.Busy, true, true);
		panel.SetFlag(BaseEntity.Flags.Busy, true, true);
		RefreshEntities();
		paintingsarelocked = true;
	    }

	    public void UnLockPaintings()
	    {
		floor.SetFlag(BaseEntity.Flags.Busy, false, true);
		deck1.SetFlag(BaseEntity.Flags.Busy, false, true);
		deck2.SetFlag(BaseEntity.Flags.Busy, false, true);
		barrel.SetFlag(BaseEntity.Flags.Busy, false, true);
		panel.SetFlag(BaseEntity.Flags.Busy, false, true);
		RefreshEntities();
		paintingsarelocked = false;
	    }

            bool PlayerIsMounted()
            {
                if (entity.GetComponent<BaseMountable>().IsMounted()) return true;
                return false;
            }

            public void AddPilot(BasePlayer player)
            {
                this.player = player;
            }

            public void RemovePilot()
            {
                this.player = null;
            }

	    void FuelCheck()
	    {
		if (player != null && instance.isAllowed(player, "gyrocopter.unlimited")) { if (currentfuel <= 9999) currentfuel = 10000; return; }
                if (currentfuel >= 1 && !throttleup && engineon && !hasbonuscharge) { currentfuel = currentfuel - 1; return; }
                if (currentfuel >= 1 && throttleup && engineon && !hasbonuscharge) { currentfuel = currentfuel - sprintcost; return; }
		if (currentfuel <= 9999 && !hasbonuscharge) currentfuel = currentfuel + baserechargerate;
		if (currentfuel <= 9999 && hasbonuscharge) currentfuel = currentfuel + bonusrechargerate;
	    }
		
            void FixedUpdate()
            {
		FuelCheck();
		var currentspeed = normalspeed;
                var throttlespeed = 30;
                if (throttleup) { throttlespeed = 60; currentspeed = sprintspeed; }

		if (engineon)
		{
			var rotorpos = barcenter.transform.eulerAngles;
               		barcenter.transform.eulerAngles = new Vector3(rotorpos.x, rotorpos.y + throttlespeed, rotorpos.z);
                	count = count + 1;
                	if (count == 3)
                	{
                    		Effect.server.Run("assets/bundled/prefabs/fx/player/swing_weapon.prefab", this.transform.position);
                	}
                	if (count == 6 && throttleup) Effect.server.Run("assets/bundled/prefabs/fx/player/swing_weapon.prefab", this.transform.position);
                	throttleup = false;
                	if (count >= 6) count = 0;

                    	var startrot = entity.transform.eulerAngles;
                    	var startloc = entity.transform.localPosition;
			var endloc = startloc;
			var rotdirection = entity.transform.eulerAngles;
                	if (islanding || currentfuel <= 0)
                	{
				islanding = true;
		    		entity.transform.localPosition = entity.transform.localPosition + (transform.up * -5f) * Time.deltaTime;
                    		RaycastHit hit;
                    		if (Physics.Raycast(new Ray(entity.transform.position, Vector3.down), out hit, 1f, layerMask)) 
                    		{ 
					islanding = false; 
					engineon = false;
                    		}
				ResetMovement(); 
				RefreshEntities();
				return;
                	}

			if (Physics.Raycast(new Ray(entity.transform.position, Vector3.down), minaltitude, layerMask)) 
			{
				endloc  += transform.up * minaltitude * Time.deltaTime;
			        entity.transform.localPosition = endloc;
                    		entity.transform.eulerAngles = rotdirection;
				RefreshEntities();
				return;
			}

                  	if (rotright) rotdirection = new Vector3(startrot.x, startrot.y + 2, startrot.z);
                    	if (rotleft) rotdirection = new Vector3(startrot.x, startrot.y - 2, startrot.z);
			if (moveforward) endloc += ((transform.forward * currentspeed) * Time.deltaTime);
			if (movebackward) endloc += ((transform.forward * -currentspeed) * Time.deltaTime);
			if (moveup) endloc += ((transform.up * currentspeed) * Time.deltaTime);
			if (movedown) endloc += ((transform.up * -currentspeed) * Time.deltaTime);

			if (endloc == new Vector3(0f, 0f, 0f)) endloc = startloc;
                    	entity.transform.localPosition = endloc;
                    	entity.transform.eulerAngles = rotdirection;
			RefreshEntities();
			return;
		}
            }

            void ResetMovement()
            {
                moveforward = false;
                movebackward = false;
		moveup = false;
		movedown = false;
                rotright = false;
                rotleft = false;
		throttleup = false;
            }

            void RefreshEntities()
            {
                entity.UpdateNetworkGroup();
                entity.transform.hasChanged = true;
                entity.SendNetworkUpdateImmediate();

                if (floor != null) floor.transform.hasChanged = true;
                if (floor != null) floor.SendNetworkUpdateImmediate();
		if (floor != null) floor.UpdateNetworkGroup();
                if (deck1 != null) deck1.transform.hasChanged = true;
                if (deck1 != null) deck1.SendNetworkUpdateImmediate();
		if (deck1 != null) deck1.UpdateNetworkGroup();
                if (deck2 != null) deck2.transform.hasChanged = true;
                if (deck2 != null) deck2.SendNetworkUpdateImmediate();
		if (deck2 != null) deck2.UpdateNetworkGroup();

                if (barrel != null) barrel.transform.hasChanged = true;
                if (barrel != null) barrel.SendNetworkUpdateImmediate();
		if (barrel != null) barrel.UpdateNetworkGroup();

                if (barcenter != null) barcenter.transform.hasChanged = true;
                if (barcenter != null) barcenter.SendNetworkUpdateImmediate();
		if (barcenter != null) barcenter.UpdateNetworkGroup();

                if (rotor1 != null) rotor1.transform.hasChanged = true;
                if (rotor1 != null) rotor1.SendNetworkUpdateImmediate();
		if (rotor1 != null) rotor1.UpdateNetworkGroup();
                if (rotor2 != null) rotor2.transform.hasChanged = true;
                if (rotor2 != null) rotor2.SendNetworkUpdateImmediate();
		if (rotor2 != null) rotor2.UpdateNetworkGroup();
                if (rotor3 != null) rotor3.transform.hasChanged = true;
                if (rotor3 != null) rotor3.SendNetworkUpdateImmediate();
		if (rotor3 != null) rotor3.UpdateNetworkGroup();
                if (rotor4 != null) rotor4.transform.hasChanged = true;
                if (rotor4 != null) rotor4.SendNetworkUpdateImmediate();
		if (rotor4 != null) rotor4.UpdateNetworkGroup();

                if (nosesign != null) nosesign.transform.hasChanged = true;
                if (nosesign != null) nosesign.SendNetworkUpdateImmediate();
		if (nosesign != null) nosesign.UpdateNetworkGroup();

                if (tail1 != null) tail1.transform.hasChanged = true;
                if (tail1 != null) tail1.SendNetworkUpdateImmediate();
		if (tail1 != null) tail1.UpdateNetworkGroup();
                if (tail2 != null) tail2.transform.hasChanged = true;
                if (tail2 != null) tail2.SendNetworkUpdateImmediate();
		if (tail2 != null) tail2.UpdateNetworkGroup();
                if (tailrotor1 != null) tailrotor1.transform.hasChanged = true;
                if (tailrotor1 != null) tailrotor1.SendNetworkUpdateImmediate();
		if (tailrotor1 != null) tailrotor1.UpdateNetworkGroup();
                if (tailrotor2 != null) tailrotor2.transform.hasChanged = true;
                if (tailrotor2 != null) tailrotor2.SendNetworkUpdateImmediate();
		if (tailrotor2 != null) tailrotor2.UpdateNetworkGroup();
                if (skid1 != null) skid1.transform.hasChanged = true;
                if (skid1 != null) skid1.SendNetworkUpdateImmediate();
		if (skid1 != null) skid1.UpdateNetworkGroup();
                if (skid2 != null) skid2.transform.hasChanged = true;
                if (skid2 != null) skid2.SendNetworkUpdateImmediate();
		if (skid2 != null) skid2.UpdateNetworkGroup();
                if (skid3 != null) skid3.transform.hasChanged = true;
                if (skid3 != null) skid3.SendNetworkUpdateImmediate();
		if (skid3 != null) skid3.UpdateNetworkGroup();
                if (skid4 != null) skid4.transform.hasChanged = true;
                if (skid4 != null) skid4.SendNetworkUpdateImmediate();
		if (skid4 != null) skid4.UpdateNetworkGroup();
                if (wheel != null) wheel.transform.hasChanged = true;
                if (wheel != null) wheel.SendNetworkUpdateImmediate();
		if (wheel != null) wheel.UpdateNetworkGroup();

                if (passengerchair1 != null) passengerchair1.transform.hasChanged = true;
                if (passengerchair1 != null) passengerchair1.SendNetworkUpdateImmediate();
		if (passengerchair1 != null) passengerchair1.UpdateNetworkGroup();

                if (passengerchair2 != null) passengerchair2.transform.hasChanged = true;
                if (passengerchair2 != null) passengerchair2.SendNetworkUpdateImmediate();
		if (passengerchair2 != null) passengerchair2.UpdateNetworkGroup();

                if (copterlock != null) copterlock.transform.hasChanged = true;
                if (copterlock != null) copterlock.SendNetworkUpdateImmediate();
		if (copterlock != null) copterlock.UpdateNetworkGroup();

                if (panel != null) panel.transform.hasChanged = true;
                if (panel != null) panel.SendNetworkUpdateImmediate();
		if (panel != null) panel.UpdateNetworkGroup();
 	     }

            void OnDestroy()
            {
                BaseEntity.saveList.Remove(entity);
                if (entity != null) { entity.Invoke("KillMessage", 0.1f); }
            }
        }

        #endregion

        #region FuelControl and Fuel Cui

        class FuelControl : MonoBehaviour
        {
            BasePlayer player;
            GyroCopter copter;
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
		player = GetComponentInParent<BasePlayer>();
            	copter = player.GetMounted().GetComponentInParent<GyroCopter>();
                playerpos = player.transform.position;
                rechargerange = RechargeRange;
                rechargerate = BonusRechargeRate;

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
                        copter.hasbonuscharge = true;
                        return;
                    }
                }
                DestroyChargeCui(player);
                ischarging = false;
		copter.hasbonuscharge = false;
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
		var copterfuel = copter.currentfuel;
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
            	var hasgyro = player.GetComponent<FuelControl>();
            	if (hasgyro != null) GameObject.Destroy(hasgyro);
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
            DestroyAll<GyroCopter>();
            DestroyAll<FuelControl>();
        }

        #endregion

    }
}