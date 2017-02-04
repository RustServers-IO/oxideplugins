using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Chute", "ColonBlow", "1.0.4", ResourceId = 2279)]
    class Chute : RustPlugin
    {
        static readonly FieldInfo serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Instance | BindingFlags.NonPublic));

        void Loaded()
        {
		LoadVariables();
		layerMask = (1 << 29);
 		layerMask |= (1 << 18);
		layerMask = ~layerMask;
		lang.RegisterMessages(messages, this);
		permission.RegisterPermission("chute.allowed", this);
	}

        void LoadDefaultConfig()
        {
            	Puts("Creating a new config file");
            	Config.Clear();
            	LoadVariables();
        }

      	Dictionary<string, string> messages = new Dictionary<string, string>()
        {
		{"notallowed", "You are not allowed to access that command." },
		{"addchute", "You are now using a parachute !!" },
		{"removechuteground", "You are too close to ground, your parachute has been removed !!" },
		{"removechute", "Your parachute has been removed !!" }
        };

	bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

	////////////////////////////////////////////////////////////////////////////////
	///// Configuration Stuff
	////////////////////////////////////////////////////////////////////////////////

	bool Changed;
	bool BlockDamageToParachute = true;
	bool BlockDamageToPlayer = true;
	static float parachuteFromHeight = 300f;
	static float parachuteSpeed = 9f;
        static float parachuteDownSpeed = -5f;
	static LayerMask layerMask;
	string SteamID;

        private void LoadConfigVariables()
        {	
        	CheckCfg("Block Damage to Parachutes : ", ref BlockDamageToParachute);
		CheckCfg("Block Damage to Player with Parachute On : ", ref BlockDamageToPlayer);	
        	CheckCfgFloat("Parachute from Height using ChuteUp : ", ref parachuteFromHeight);
		CheckCfgFloat("Parachute Movement Speed : ", ref parachuteSpeed);
		CheckCfgFloat("Parachute Downward Speed : ", ref parachuteDownSpeed);	
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

	////////////////////////////////////////////////////////////////////////////////

        class PlayerParachute : MonoBehaviour
        {
            InputState input;
            BasePlayer player;
            BaseEntity parachute;
            Vector3 direction;
            Vector3 position;
	    string chutecolorstr;
	    ulong chuteskinID;
	    static Chute instance;
	   

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                input = serverinput.GetValue(player) as InputState;
                position = transform.position;
		chuteskinID = 0;
		instance = new Chute();
            }

	    public void AddChute(string chutecolor)
		{
			if (chutecolor != null && chutecolor.Length > 0)
			{
				chutecolorstr = chutecolor;
				if (chutecolorstr == null || chutecolorstr == "0") { chuteskinID = 0; }
				if (chutecolorstr == "blue") { chuteskinID = 847412964; } 	//My workshop blue skin ID
				if (chutecolorstr == "yellow") { chuteskinID = 847407128; } 	//My workshop yellow skin ID
				if (chutecolorstr == "red") { chuteskinID = 846788415; } 	//My workshop red skin ID
				if (chutecolorstr == "camo") { chuteskinID = 795286751; } 	//My workshop camo skin ID
			}
			parachute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", new Vector3(0,0,0), new Quaternion(), true); 
			parachute.SetParent(player);
			parachute.skinID = chuteskinID;
                	parachute.Spawn();
		}

            void FixedUpdate()
            {
                direction.z++;
                direction = Quaternion.Euler(input.current.aimAngles) * direction * Time.fixedDeltaTime * parachuteSpeed;
                direction.y = parachuteDownSpeed * Time.fixedDeltaTime;

                if ((!player.IsFlying()) || Physics.Raycast(new Ray(player.transform.position, Vector3.down), 1f, layerMask))
                {
			if (parachute == null) return;
			instance.RemoveParachute(player);    
                    	return;
                }
                position += direction;
		player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
		player.SendNetworkUpdate();
            }

            void OnDestroy()
            	{
			if (parachute == null) return;
			parachute.Kill(BaseNetworkable.DestroyMode.None);
             
            	}
        }

	////////////////////////////////////////////////////////////////////////////////

        [ChatCommand("chute")]
        void chatChute(BasePlayer player, string command, string[] args)
        {
		var str0 = "0";
		if (args != null && args.Length > 0) { str0 = args[0].ToLower(); }
		ChuteVerification(player, false, str0);
        }

	[ConsoleCommand("chute")]
        void cmdConsoleChute(ConsoleSystem.Arg arg)
	{
		BasePlayer player = arg.Player();
		var str0 = arg.GetString(0).ToLower();
		ChuteVerification(player, false, str0);
	}

        [ChatCommand("chuteup")]
        void chatChuteUp(BasePlayer player, string command, string[] args)
        {
		var str0 = "0";
		if (args != null && args.Length > 0) { str0 = args[0].ToLower(); }
		ChuteVerification(player, true, str0);
        }

	[ConsoleCommand("chuteup")]
        void cmdConsoleChuteUp(ConsoleSystem.Arg arg)
	{
		BasePlayer player = arg.Player();
		var str0 = arg.GetString(0).ToLower();
		ChuteVerification(player, true, str0);
	}

	void ChuteVerification(BasePlayer player, bool isUp, string chutecolor)
	{
		SteamID = player.userID.ToString();
		if (!isAllowed(player, "chute.allowed"))
		{
			SendReply(player, lang.GetMessage("notallowed", this, SteamID));
			return;	
		}
		if (isAllowed(player, "chute.allowed"))
		{
                	if (!isUp && Physics.Raycast(new Ray(player.transform.position, Vector3.down), 5f, layerMask))
                	{
				SendReply(player, lang.GetMessage("removechuteground", this, SteamID));      
                    		return;
                	}
			var playerchute = player.GetComponent<PlayerParachute>();
			if (playerchute != null)
			{
				SendReply(player, lang.GetMessage("removechute", this, SteamID));
				RemoveParachute(player);
				return;
			}
			if (playerchute == null)
			{
				if (!player.IsFlying()) { rust.RunClientCommand(player, "noclip"); }
				if (!isUp) timer.Once(0.5f, () => ExternalAddPlayerChute(player, chutecolor));
				if (isUp) timer.Once(0.5f, () => AddPlayerChute(player, chutecolor));
				return;
			}
		}
	}

	void AddPlayerChute(BasePlayer player, string chutecolor)
	{
		if (!player.IsFlying()) return;
		SendReply(player, lang.GetMessage("addchute", this, SteamID));
            	player.transform.position += new Vector3(0, parachuteFromHeight, 0);
            	player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position + new Vector3(0, parachuteFromHeight, 0));
            	var mychute = player.gameObject.AddComponent<PlayerParachute>();
		mychute.AddChute(chutecolor);
            	player.SendNetworkUpdate();
	}

	void RemoveParachute(BasePlayer player)
	{
		SteamID = player.userID.ToString();
		var playerchute = player.GetComponent<PlayerParachute>();
		if (playerchute == null) return;
		GameObject.Destroy(playerchute);
		if (player.IsFlying()) { rust.RunClientCommand(player, "noclip"); }
		SendReply(player, lang.GetMessage("removechute", this, SteamID));
		return;
	}

	//external or local call to add parachute to player at players current location
	private void ExternalAddPlayerChute(BasePlayer player, string chutecolor)
	{
		SteamID = player.userID.ToString();
		if (!player.IsFlying()) { rust.RunClientCommand(player, "noclip"); }
		SendReply(player, lang.GetMessage("addchute", this, SteamID));
            	var mychute = player.gameObject.AddComponent<PlayerParachute>();
		mychute.AddChute(chutecolor);
	}

	void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
             if (entity.PrefabName == "assets/prefabs/misc/parachute/parachute.prefab" && BlockDamageToParachute)
             {
                 hitInfo.damageTypes.ScaleAll(0);
             }
             if (entity is BasePlayer && BlockDamageToPlayer)
             {
                 var player = entity.ToPlayer();
                 if (player.GetComponent<PlayerParachute>())
                 {
                     hitInfo.damageTypes.ScaleAll(0);
                 }
             }
             return;
        }

	//////////////////////////////////////////////////////////////////////////////////////

        void Unload()
        {
            DestroyAll<PlayerParachute>();
        }

        static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

	void OnPlayerDisconnected(BasePlayer player, string reason)
	{
		RemoveParachute(player);
	}

	void OnPlayerRespawned(BasePlayer player)
	{
		RemoveParachute(player);
	}
    }
}
        