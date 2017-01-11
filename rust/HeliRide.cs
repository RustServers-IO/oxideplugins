using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using Network;
using System;
using Rust;

namespace Oxide.Plugins
{
    [Info("HeliRide", "ColonBlow", "1.1.4")]
    public class HeliRide : RustPlugin
    {
	static Dictionary<ulong, HeliData> HeliFlying = new Dictionary<ulong, HeliData>();
	static Dictionary<ulong, HeliDamage> DamagedHeli = new Dictionary<ulong, HeliDamage>();

        public class HeliData
        {
             	public BasePlayer player;
        }

	public class HeliDamage
        {
             	public BasePlayer player;
        }

        static readonly FieldInfo serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Instance | BindingFlags.NonPublic));

        void Loaded()
        {
		LoadVariables();
		lang.RegisterMessages(messages, this);
		permission.RegisterPermission("heliride.allowed", this);
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
		{"notflying", "You must be noclipping to activate Helicopter." }
        };

	bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

	////////////////////////////////////////////////////////////////////////////////

	bool Changed;
	private static bool ShowCockpitOverlay = true;
	private static bool ShowCrosshair = true;

        private void LoadConfigVariables()
        {		
        	CheckCfg("ShowCockpitOverlay", ref ShowCockpitOverlay);
		CheckCfg("ShowCrosshair", ref ShowCrosshair);
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

	public bool CockpitOverlay=> Config.Get<bool>("Show Custom Cockpit Overlay");
	public bool CrossHair => Config.Get<bool>("Show Custom Crosshair");

        class FlyHelicopter : MonoBehaviour
        {
            	public BasePlayer player;
            	public BaseEntity helicopterBase;
		BaseEntity rockets;
		
		public PatrolHelicopterAI heliAI;
		public BaseHelicopter heli;
		public HelicopterTurret heliturret;
            	public InputState input;

		RaycastHit hitInfo;
		
		public Vector3 PlayerPOS;
		public Vector3 target;
		public Vector3 CurrentPOS;
		Vector3 direction;

		bool leftTubeFiredLast = false;

            	void Awake()
            	{
                	player = GetComponent<BasePlayer>();
			input = serverinput.GetValue(player) as InputState;
			PlayerPOS = player.transform.position+player.eyes.BodyForward()*3f;

			string prefab = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
			helicopterBase = GameManager.server.CreateEntity(prefab, new Vector3(),  new Quaternion(), true);
			heliAI = helicopterBase.GetComponent<PatrolHelicopterAI>();
			heliAI.enabled = false;

			heliturret = helicopterBase.GetComponent<HelicopterTurret>();

			heli = helicopterBase.GetComponent<BaseHelicopter>();
			heli.maxCratesToSpawn = 10;
			heli.bulletDamage = 50f;
			heli.spotlightTarget = FindTarget(target);
                	helicopterBase.Spawn();

			if (ShowCockpitOverlay) CockpitOverlay(player);
			if (ShowCrosshair) CrosshairOverlay(player);

			helicopterBase.transform.position = PlayerPOS;
			helicopterBase.transform.rotation = player.eyes.rotation;
            	}

		///////////////////////////////////////////

        	void CockpitOverlay(BasePlayer player)
        	{
			var cockpitcui = new CuiElementContainer();

            		cockpitcui.Add(new CuiElement
                	{
                    	Name = "CockpitGuiOverlay",
			Parent = "Hud.Under",
                    	Components =
                    		{
                        	new CuiRawImageComponent { Color = "1 1 1 1", Url = "http://i.imgur.com/6O0hMC5.png", Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        	new CuiRectTransformComponent { AnchorMin = "0 0",  AnchorMax = "1 1"},
                    		}
                	});
            		CuiHelper.AddUi(player, cockpitcui);
        	}

        	void CrosshairOverlay(BasePlayer player)
        	{
			var crosshaircui = new CuiElementContainer();

            		crosshaircui.Add(new CuiElement
                	{
                    	Name = "CrosshairGuiOverlay",
			Parent = "Hud.Under",
                    	Components =
                    		{
                        	new CuiRawImageComponent { Color = "1 1 1 1", Url = "http://i.imgur.com/yweKHFT.png", Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        	new CuiRectTransformComponent { AnchorMin = "0.450 0.450",  AnchorMax = "0.540 0.550"},
                    		}
                	});
            		CuiHelper.AddUi(player, crosshaircui);
        	}

        	void DamageOverlay(BasePlayer player)
        	{
			var damageoverlay = new CuiElementContainer();

            		damageoverlay.Add(new CuiElement
                	{
                    	Name = "DamageGuiOverlay",
			Parent = "Hud.Under",
                    	Components =
                    		{
                        	new CuiRawImageComponent { Color = "1 1 1 1", Url = "http://i.imgur.com/XrpqTdP.png", Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        	new CuiRectTransformComponent { AnchorMin = "0.35 0.25",  AnchorMax = "0.60 0.70"},
                    		}
                	});
            		CuiHelper.AddUi(player, damageoverlay);
        	}
		
		void FixedUpdate()
		{
			player = GetComponent<BasePlayer>();
			if (player.IsDead())
			{
				OnDestroy();
				return;
			}
			if (!player.IsFlying()) 
			{
				OnDestroy();
				return;
			}
			if (heliAI._currentState == PatrolHelicopterAI.aiState.DEATH)
			{
				heliAI.enabled = true;
				DestroyCui(player);
				GameObject.Destroy(this);
				return;
			}

			Vector3 PlayerPOS = player.GetEstimatedWorldPosition()-(player.eyes.BodyForward()*5)+(Vector3.down*0.45f);
			CurrentPOS = helicopterBase.transform.position;
			Vector3 direction = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;

			heli.spotlightTarget = FindTarget(target);

			helicopterBase.transform.position = Vector3.Lerp(CurrentPOS, PlayerPOS, 1f);
			helicopterBase.transform.rotation = player.eyes.rotation;

			BaseCombatEntity helientity = helicopterBase.GetComponent<BaseCombatEntity>();
			float health = helientity.Health();
			if (health <= 3000f && (ShowCockpitOverlay))
			{
				if (!(DamagedHeli.ContainsKey(player.userID)))	
				{
					DamageOverlay(player);
					DamagedHeli.Add(player.userID, new HeliDamage
					{
					player = player,
					});
				}	
			}

			if (health > 3000f && (ShowCockpitOverlay))
			{
				if (DamagedHeli.ContainsKey(player.userID))
				{
					CuiHelper.DestroyUi(player, "DamageGuiOverlay");
					DamagedHeli.Remove(player.userID);
				}	
			}

			if (input.IsDown(BUTTON.FIRE_PRIMARY))
			{
				target = FindTarget(target);
				FireGuns(target);
			}
			if (input.IsDown(BUTTON.FIRE_SECONDARY))
			{
				leftTubeFiredLast = !leftTubeFiredLast;
				FireRocket(leftTubeFiredLast, direction, PlayerPOS);
			}
			if (input.IsDown(BUTTON.FIRE_THIRD))
			{
				FireNapalm(leftTubeFiredLast, direction, PlayerPOS);
			}

		}

		void FireGuns(Vector3 target)
		{
			heliAI.FireGun(target, ConVar.PatrolHelicopter.bulletAccuracy, true);
			heliAI.FireGun(target, ConVar.PatrolHelicopter.bulletAccuracy, false);
		}

		void FireRocket(bool leftTubeFiredLast, Vector3 direction, Vector3 PlayerPOS)
		{

			RaycastHit hit;
                	float num = 4f;
			Vector3 origin = PlayerPOS;
                	if (num > 0f)
                	{
                    		direction = (Vector3)(Quaternion.Euler(UnityEngine.Random.Range((float)(-num * 0.5f), (float)(num * 0.5f)), UnityEngine.Random.Range((float)(-num * 0.5f), (float)(num * 0.5f)), UnityEngine.Random.Range((float)(-num * 0.5f), (float)(num * 0.5f))) * direction);
                	}
                	float maxDistance = 1f;
                	if (Physics.Raycast(origin, direction, out hit, maxDistance, -1063040255))
                	{
                    		maxDistance = hit.distance - 0.1f;
                	}
			Transform transform = !leftTubeFiredLast ? heliAI.helicopterBase.rocket_tube_right.transform : heliAI.helicopterBase.rocket_tube_left.transform;
			Effect.server.Run(heliAI.helicopterBase.rocket_fire_effect.resourcePath, heliAI.helicopterBase, StringPool.Get(!leftTubeFiredLast ? "rocket_tube_right" : "rocket_tube_left"), Vector3.zero, Vector3.forward, null, true);

			rockets = GameManager.server.CreateEntity(heliAI.rocketProjectile.resourcePath, origin, new Quaternion(), true);
			if (rockets != null)
                	{
				rockets.SendMessage("InitializeVelocity", (Vector3)(direction * 1f));
				rockets.Spawn();
			}
		}

		void FireNapalm(bool leftTubeFiredLast, Vector3 direction, Vector3 PlayerPOS)
		{
			RaycastHit hit;
                	float num = 4f;
			Vector3 origin = PlayerPOS;
                	if (num > 0f)
                	{
                    		direction = (Vector3)(Quaternion.Euler(UnityEngine.Random.Range((float)(-num * 0.5f), (float)(num * 0.5f)), UnityEngine.Random.Range((float)(-num * 0.5f), (float)(num * 0.5f)), UnityEngine.Random.Range((float)(-num * 0.5f), (float)(num * 0.5f))) * direction);
                	}
                	float maxDistance = 1f;
                	if (Physics.Raycast(origin, direction, out hit, maxDistance, -1063040255))
                	{
                    		maxDistance = hit.distance - 0.1f;
                	}
			Transform transform = !leftTubeFiredLast ? heliAI.helicopterBase.rocket_tube_right.transform : heliAI.helicopterBase.rocket_tube_left.transform;
			Effect.server.Run(heliAI.helicopterBase.rocket_fire_effect.resourcePath, heliAI.helicopterBase, StringPool.Get(!leftTubeFiredLast ? "rocket_tube_right" : "rocket_tube_left"), Vector3.zero, Vector3.forward, null, true);

			rockets = GameManager.server.CreateEntity(heliAI.rocketProjectile_Napalm.resourcePath, origin, new Quaternion(), true);
			if (rockets != null)
                	{
				rockets.SendMessage("InitializeVelocity", (Vector3)(direction * 1f));
				rockets.Spawn();
			}
		}

		Vector3 FindTarget(Vector3 target)
		{
            		if(!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hitInfo, Mathf.Infinity, -1063040255))
            		{
            		}
			Vector3 hitpoint = hitInfo.point;
			return hitpoint;

		}

		public void DestroyCui(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, "CockpitGuiOverlay");
			CuiHelper.DestroyUi(player, "CrosshairGuiOverlay");
			CuiHelper.DestroyUi(player, "DamageGuiOverlay");
			DamagedHeli.Remove(player.userID);
		}

            	public void OnDestroy()
            	{
			DestroyCui(player);
			DamagedHeli.Remove(player.userID);
			HeliFlying.Remove(player.userID);

			if (helicopterBase == null) return;
               		if (heliAI._currentState == PatrolHelicopterAI.aiState.DEATH)
			{
				GameObject.Destroy(this);
				return;
			}
			helicopterBase.Kill(BaseNetworkable.DestroyMode.None);
			GameObject.Destroy(this);
            	}
        }


	////////////////////////////////////////////////////////////////////////////////

        [ChatCommand("flyheli")]
        void chatFlyHeli(BasePlayer player, string command, string[] args)
	{

		if (isAllowed(player, "heliride.allowed"))
		{
			var playerheli = player.GetComponent<FlyHelicopter>();

			if (HeliFlying.ContainsKey(player.userID))
			{
				GameObject.Destroy(playerheli);
				HeliFlying.Remove(player.userID);
				return;
			}
			if (playerheli != null)
			{
				GameObject.Destroy(playerheli);
				HeliFlying.Remove(player.userID);
				return;
			}

			if (playerheli == null)
			{
				if (!player.IsFlying()) { rust.RunClientCommand(player, "noclip"); }
				timer.Once(1f, () => AddHeli(player));
				return;
			}
		}
		if (!isAllowed(player, "heliride.allowed"))
		{
			string SteamID = player.userID.ToString();
			SendReply(player, lang.GetMessage("notallowed", this, SteamID));
			return;	
		}
	
        }

	[ConsoleCommand("flyheli")]
        void cmdConsoleFlyHeli(ConsoleSystem.Arg arg)
	{
		BasePlayer player = arg.Player();

		if (isAllowed(player, "heliride.allowed"))
		{
			var playerheli = player.GetComponent<FlyHelicopter>();

			if (HeliFlying.ContainsKey(player.userID))
			{
				GameObject.Destroy(playerheli);
				HeliFlying.Remove(player.userID);
				return;
			}
			if (playerheli != null)
			{
				GameObject.Destroy(playerheli);
				HeliFlying.Remove(player.userID);
				return;
			}

			if (playerheli == null)
			{
				if (!player.IsFlying()) { rust.RunClientCommand(player, "noclip"); }
				timer.Once(1f, () => AddHeli(player));
				return;
			}
		}
		if (!isAllowed(player, "heliride.allowed"))
		{
			string SteamID = player.userID.ToString();
			SendReply(player, lang.GetMessage("notallowed", this, SteamID));
			return;	
		}
		return;
        }

	void AddHeli(BasePlayer player)
	{
		if (player.IsFlying()) 
		{ 
            		player.gameObject.AddComponent<FlyHelicopter>();
			HeliFlying.Add(player.userID, new HeliData
			{
			player = player,
			});
			return;
		}
		if (!player.IsFlying()) 
		{ 
			string SteamID = player.userID.ToString();
            		SendReply(player, lang.GetMessage("notflying", this, SteamID));
			return;
		}
		return;
	}

//////////////////////////////////////////////////////////////////////////////////////////////////

        void Unload()
        {
            	DestroyAll<FlyHelicopter>();
		foreach (var player in BasePlayer.activePlayerList)
            	{
			RemoveHeliComponents(player);
           	}
        }

        static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }



	void RemoveHeliComponents(BasePlayer player)
	{
		var playerheli = player.GetComponent<FlyHelicopter>();
		if (playerheli != null)
		{
			playerheli.DestroyCui(player);
                	GameObject.Destroy(playerheli);
		}
		return;
	}

	void OnPlayerDisconnected(BasePlayer player)
	{
		RemoveHeliComponents(player);
	}

	void OnPlayerRespawned(BasePlayer player)
	{
		RemoveHeliComponents(player);
	}


    }
}