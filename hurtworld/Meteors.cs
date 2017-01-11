using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;
using System.Collections;
using System;

namespace Oxide.Plugins
{
	[Info("Meteors", "Smeag", "1.1.3")]
	[Description("Meteors fall and bring stuff from space.")]

	public class Meteors : HurtworldPlugin
	{
		public static Meteors Instance;
		public static readonly string FallSitesFilePath = "MeteorsFallSites";
		public static readonly string LootItemsFilePath = "MeteorsLootItems";

		public static List<MeteorInstance> InstantiatedMeteors = new List<MeteorInstance>();

		#region configs_base

		public class ConfigurationField<T>
		{
			public string name;
			public DynamicConfigFile config;

			protected virtual string TypeAsString(T v) { return ""; }
			protected virtual T Parse(string str) { return default(T); }

			public T value
			{
				get
				{
					return Parse(this.config.Get<string>(this.Path));
				}
				set
				{
					object[] arr = new object[4] { this.Path[0], this.Path[1], this.Path[2], this.TypeAsString(value) };
					this.config.Set(arr);
					this.config.Save();
				}
			}

			public string[] Path
			{
				get { return new string[3] { "Settings", "Meteors", this.name }; }
			}

			public ConfigurationField(DynamicConfigFile Config, string Name, T DefaultValue)
			{
				this.name = Name;
				this.config = Config;

				if (this.config.Get(this.Path) == null)
					this.value = DefaultValue;
			}
		}

		public class ConfigurationField_Vector3 : ConfigurationField<Vector3>
		{
			public ConfigurationField_Vector3(DynamicConfigFile Config, string Name, Vector3 DefaultValue) : base(Config, Name, DefaultValue)
			{

			}

			protected override string TypeAsString(Vector3 v)
			{
				return v[0].ToString() + " " + v[1].ToString() + " " + v[2].ToString();
			}

			protected override Vector3 Parse(string str)
			{
				string[] s = str.Split(new char[] { ' ' });
				return new Vector3(float.Parse(s[0]), float.Parse(s[1]), float.Parse(s[2]));
			}
		}

		public class ConfigurationField_Bool : ConfigurationField<bool>
		{
			public ConfigurationField_Bool(DynamicConfigFile Config, string Name, bool DefaultValue) : base(Config, Name, DefaultValue)
			{

			}

			protected override string TypeAsString(bool v)
			{
				return (v ? "1" : "0");
			}

			protected override bool Parse(string str)
			{
				return (str == "1");
			}
		}

		public class ConfigurationField_Int : ConfigurationField<int>
		{
			public ConfigurationField_Int(DynamicConfigFile Config, string Name, int DefaultValue) : base(Config, Name, DefaultValue)
			{

			}

			protected override string TypeAsString(int v)
			{
				return v.ToString();
			}

			protected override int Parse(string str)
			{
				return int.Parse(str);
			}
		}

		public class ConfigurationField_Float : ConfigurationField<float>
		{
			public ConfigurationField_Float(DynamicConfigFile Config, string Name, float DefaultValue) : base(Config, Name, DefaultValue)
			{

			}

			protected override string TypeAsString(float v)
			{
				return v.ToString();
			}

			protected override float Parse(string str)
			{
				return float.Parse(str);
			}
		}
		#endregion

		public class MeteorInstance
		{
			public GameObject gameObject;
			public FallSiteInfo? targetFallSite;

			public float heat_StartingTime;
			public float heatDuration;
			private bool spawnLoot;
			private bool destroyOnFall;
			private Vector3 startingPos;
			private Vector3 finalHitPos;
			private float distance;
			private bool damageBuildings;

			private Coroutine updaterCoroutine;
			private IProjectile projectile;
			private IProjectileImpactBehavior meteorImpact;
			private int oldLayer = 0;

			public MeteorInstance(float heatTimeDuration, bool spawnLoot, bool destroyOnFall, FallSiteInfo? fallSite)
			{
				InstantiatedMeteors.Add(this);

				this.targetFallSite = null;
				if (fallSite.HasValue)
					this.targetFallSite = fallSite.Value;

				this.heat_StartingTime = Time.time;
				this.heatDuration = heatTimeDuration;
				this.spawnLoot = spawnLoot;
				this.destroyOnFall = destroyOnFall;
				this.damageBuildings = Meteors.Instance.cfg_MeteorsDamageBuildings.value;

				this.gameObject = Singleton<HNetworkManager>.Instance.NetInstantiate(
					uLink.NetworkPlayer.server,
					"WorldItemMeteor",
					Vector3.zero,
					Quaternion.Euler(0, 180, 0),
					GameManager.GetSceneTime());


				this.oldLayer = this.gameObject.layer;

				NetworkEntityManagerProjectileServer nemps = this.gameObject.GetComponent<NetworkEntityManagerProjectileServer>();
				nemps.LongLivingProjectile = true;
				nemps.TimeToLive = 60 * 60 * 60 * 24;

				// Fuck this
				this.gameObject.GetComponent<MeteorController>().enabled = false;

				this.meteorImpact = this.gameObject.GetComponentByInterface<IProjectileImpactBehavior>();
				this.projectile = this.gameObject.GetComponentByInterface<IProjectile>();
			}

			public void Fire(Vector3 startingPos, Vector3 finalPos, float speed)
			{
				Vector3 sp = startingPos;
				sp.x = Mathf.Max(-3800, sp.x);
				sp.z = Mathf.Max(-3800, sp.z);

				sp.x = Mathf.Min(2200, sp.x);
				sp.z = Mathf.Min(2200, sp.z);
				Vector3 direction = (finalPos - sp).normalized;

				// Do a raycast to foresee where the meteor will hit
				RaycastHit[] raycastHitArray = Physics.SphereCastAll(sp, 0.1f, direction, 10000, -135169);

				if (raycastHitArray.Length == 0)
					Meteors.Instance.PrintError("Invalid meteor raycast!");

				this.finalHitPos = raycastHitArray[0].point;
				this.distance = raycastHitArray[0].distance;

				this.projectile.Launch(sp, Quaternion.identity, direction * speed, this.gameObject, false, 0f);
				this.updaterCoroutine = Singleton<GameManager>.Instance.StartCoroutine(this.UpdateMeteor(0.05f));
			}


			public IEnumerator UpdateMeteor(float interval)
			{
				try
				{
					float currentSquareDistance;

					// Still flying
					while (!this.meteorImpact.IsDone())
					{
						// Check with square distance to avoid root-square, as it is heavy for processing.
						currentSquareDistance = (this.gameObject.transform.position - this.finalHitPos).sqrMagnitude;

						// If the meteor ball is in less than 200m of distance from the hit position, do simplified explosions.
						if (currentSquareDistance <= 200*200)
							this.MeteorExplosion(this.gameObject.transform.position, true, false);

						yield return new WaitForSeconds(interval);
						continue;
					}

					// Big bad landing explosion. Those will do damage
					yield return new WaitForSeconds(0.2f);
					DoExplosionsRing(4, 5, 0, this.damageBuildings);
					DoExplosionsRing(3, 5, 2, this.damageBuildings);
					yield return new WaitForSeconds(0.2f);
					DoExplosionsRing(7, 15, 0, this.damageBuildings);
					DoExplosionsRing(5, 15, 2, this.damageBuildings);
					yield return new WaitForSeconds(0.2f);
					DoExplosionsRing(16, 25, 0, this.damageBuildings);
					yield return new WaitForSeconds(0.2f);
					DoExplosionsRing(5, 10, 0, false);
					yield return new WaitForSeconds(0.2f);
					DoExplosionsRing(9, 20, 0, false);
					yield return new WaitForSeconds(0.2f);
					DoExplosionsRing(12, 30, 0, false);
					yield return new WaitForSeconds(0.2f);

					// Read below
					if (this.destroyOnFall)
					{
						// Spawn goodies!
						if (this.spawnLoot)
							this.SpawnLoot();

						yield break;
					}

					this.heat_StartingTime = Time.time;

					// Make a collider and make interact with players/explosions as terrain
					SphereCollider sprCollider = this.gameObject.AddComponent<SphereCollider>();
					sprCollider.radius = 1;
					this.gameObject.layer = Resources.FindObjectsOfTypeAll<TerrainSettingsManager>()[0].gameObject.layer;

					// Warning about the damage in the area.
					yield return new WaitForSeconds(1.0f);
					Meteors.Instance.hurt.BroadcastChat(string.Format(Meteors.Instance.lang.GetMessage(msg_meteor_hot, Meteors.Instance), this.heatDuration.ToString()));

					// Burn players in the fall site
					const float hitsInterval = 1.0f;
					int hits = (int)((this.heatDuration * 60.0f) / hitsInterval);

					for (int i = 0; i < hits; i++)
					{
						BurnPlayersInRadius(this.gameObject.transform.position, 30, 10);
						yield return new WaitForSeconds(hitsInterval);
					}

					// Stop doing damage and tell everybody that.
					Meteors.Instance.hurt.BroadcastChat(
						string.Format(Meteors.Instance.lang.GetMessage(msg_meteor_cold, Meteors.Instance),
								      this.heatDuration));

					// Do a one last explosion to celebrate.
					DoExplosionsRing(3, 2, 0, false); 
					 
					// Spawn goodies!
					if (this.spawnLoot)
					{
						// Make the collider interact with players
						this.gameObject.layer = this.oldLayer;

						// Remove collider to avoid strange loot boxes behaviour
						GameObject.DestroyImmediate(sprCollider);

						this.SpawnLoot();
					}

					// Stop coroutine and destroy meteor from game.
					yield break;
				}
				finally
				{
					this.Destroy(true);
				}
			}

			bool IsValidSession(PlayerSession session)
			{
				return (session != null) &&
					(session.IsLoaded) &&
					(session.Identity != null) &&
					(session.Player.isConnected) &&
					(session.WorldPlayerEntity != null);
			}

			// Yeah.. this is ugly.. but a maximum of 60 players is light enough.
			private void BurnPlayersInRadius(Vector3 pos, float radius, float damage)
			{
				List<PlayerSession> players = Singleton<GameManager>.Instance.GetSessions().Values.ToList();

				foreach (PlayerSession pl in players)
				{
					if (!IsValidSession(pl))
						continue;

					if ((pos - pl.WorldPlayerEntity.transform.position).sqrMagnitude >= (radius * radius))
						continue;

					EntityStats es = pl.WorldPlayerEntity.GetComponent<EntityStats>();
					IEntityFluidEffect hp = es.GetFluidEffect(EEntityFluidEffectType.Health);
					hp.SetValue(hp.GetValue() - damage);

					es.AddBinaryEffect(EEntityBinaryEffectType.Burning,
						new AppliedEntityBinaryEffect("StatBarsBurning", "EntityStats/BinaryEffects/Burning")
						.SetDescriptionKey("EntityStats/BinaryEffects/Descriptions/Burning")
						.AutoExpire(2.5f));

					EffectManagerServer.TakeDamageServer(Vector3.up, damage, pl.Player);
				}
			}

			public void SpawnLoot()
			{
				Vector3 baseItemSpawnPos = this.gameObject.transform.position + (5 * Vector3.up);
				float globalRate = Meteors.Instance.GetGlobaDropRates();

				foreach (MeteorLootItem mli in Meteors.Instance.lootItemsDB.lootItems)
				{
					if (UnityEngine.Random.Range(0.0f, 1.0f) >= mli.chance * globalRate)
						continue;

					GlobalItemManager.SpawnWorldItemNice(
						new ItemInstance(GlobalItemManager.Instance.GetItem(mli.itemID),
							UnityEngine.Random.Range(mli.minStackSize, mli.maxStackSize)),
						baseItemSpawnPos + UnityEngine.Random.insideUnitSphere * 2);
				}
			}

			public void Destroy(bool removeFromList)
			{
				if (removeFromList)
					InstantiatedMeteors.Remove(this);

				if (this.updaterCoroutine != null)
					GameManager.Instance.StopCoroutine(this.updaterCoroutine);

				if (this.gameObject != null)
				{
					// Destroy the meteor ball at clients
					uLink.NetworkView ulnv = this.gameObject.GetComponent<uLink.NetworkView>();
					Singleton<HNetworkManager>.Instance.NetDestroy(ulnv);

					// And at server
					GameObject.Destroy(this.gameObject);
				}

				GC.Collect();
			}

			public void DoExplosionsRing(int elementsCount, float Radius, float elevation, bool shouldDamageBuildings)
			{
				float x = 0;
				float y = 0;
				float radians;
				for (int i = 0; i < elementsCount; i++)
				{
					radians = ((float)i / (float)elementsCount) * 2.0f * Mathf.PI;
					x = Radius * Mathf.Cos(radians);
					y = Radius * Mathf.Sin(radians);

					this.MeteorExplosion(this.gameObject.transform.position + new Vector3(x, elevation, y), false, shouldDamageBuildings);
				}
			}

			private void MeteorExplosion(Vector3 pos, bool simplified, bool shouldDamageBuildings)
			{
				GameObject explosionGO= Singleton<HNetworkManager>.Instance.NetInstantiate("ExplosionServer", pos, Quaternion.identity, GameManager.GetSceneTime());
				ExplosionServer explosion = explosionGO.GetComponent<ExplosionServer>();
				explosion.SetData(landcrabExplosionConfig);

				// Simplified means that the hit scan won't be accurate. The hit scan sphere becomes uselessly simpliflied.
				if (simplified)
				{
					explosion.LatRows = 1;
					explosion.LongRows = 1;
					explosion.Radius = 0.005f;
					explosion.SetData(landcrabExplosionConfig);
				}
				else
				{
				//	explosion.LatRows = 16;
				//	explosion.LongRows = 16;
				//	explosion.Radius = 1;

					if (shouldDamageBuildings)
						explosion.SetData(C4ExplosionConfig);
					else
						explosion.SetData(landcrabExplosionConfig);
				}

				explosion.Explode();
			}
		}

		#region meteors_fallsites_db

		public struct FallSiteInfo
		{
			public Vector3 position;
			public float radius;
			public string Name;

			public FallSiteInfo(Vector3 p, float r, string n)
			{
				this.position = p;
				this.radius = r;
				this.Name = n;
			}

			public FallSiteInfo(string src)
			{
				position = Vector3.zero;
				radius = 0;
				Name = "";
				Parse(src);
			}

			public new string ToString()
			{
				string result = "";
				result += this.position[0].ToString() + " " +
					this.position[1].ToString() + " " +
					this.position[2].ToString();
				result += ";";
				result += this.radius.ToString();
				result += ";";
				result += this.Name;

				return result;
			}

			public void Parse(string str)
			{
				string[] strs = str.Split(new char[] { ';' });

				// position
				string[] v = strs[0].Split(new char[] { ' ' });
				this.position = new Vector3(float.Parse(v[0]), float.Parse(v[1]), float.Parse(v[2]));

				this.radius = int.Parse(strs[1]);
				this.Name = strs[2];
			}

		}

		public class MeteorsFallSitesDB
		{
			public string fileName;
			public List<FallSiteInfo> fallSites;

			public MeteorsFallSitesDB()
			{
				this.fallSites = new List<FallSiteInfo>();
				this.Load();
			}

			public void Add(Vector3 pos, float r, string name)
			{
				this.fallSites.Add(new FallSiteInfo(pos, r, name));
				this.Save();
			}

			public void Add(string src)
			{
				this.fallSites.Add(new FallSiteInfo(src));
				this.Save();
			}

			public void RemoveAt(int index)
			{
				this.fallSites.RemoveAt(index);
				this.Save();
			}

			public void Save()
			{
				List<string> buffer = new List<string>();

				foreach (FallSiteInfo fsi in this.fallSites)
					buffer.Add(fsi.ToString());

				Interface.GetMod().DataFileSystem.WriteObject(FallSitesFilePath, buffer);
			}

			public void Load()
			{
				this.fallSites.Clear();

				List<string> buffer = Interface.GetMod().DataFileSystem.ReadObject<List<string>>(FallSitesFilePath);

				if (buffer.Count == 0)
				{
					this.LoadDefaults();
					this.Save();
					return;
				}

				foreach (string entry in buffer)
					this.Add(entry);
			}

			public void LoadDefaults()
			{
				this.Add("-2068.78 182.875 1208.731;800;The Snow Biome");
				this.Add("-2674.589 283.6281 -813.9224;800;The Forest");
				this.Add("-645.3057 241.7302 -2214.017;700;The Sand Desert");
				this.Add("1233.461 299.4619 -1884.715;1000;The Red Desert");
				this.Add("-2678.557 269.2448 -2528.645;50;The Arch");
				this.Add("-915.2287 233.812 -2438.202;150;The Desert Ship");
				this.Add("1475.228 204.7502 -1899.655;200;The Crossroads");
				this.Add("1124.911 194.4053 -3352.688;200;The Arifield");
				this.Add("-1269.364 178.0141 1185.167;200;Boonies");
				this.Add("-2603.134 178.2825 1609.538;200;Transit");
				this.Add("-2378.846 200.2309 -927.0706;150;The Fortress");
				this.Add("-3319.789 202.7251 -1482.855;200;The Valley");
			}
		}

		#endregion

		#region meteors_loot_db

		public struct MeteorLootItem
		{
			public int group;
			public int itemID;
			public float chance;
			public int minStackSize;
			public int maxStackSize;
			public string description;

			public MeteorLootItem(int prm_group,
				int prm_itemID,
				float prm_chance,
				int prm_minStackSize,
				int prm_maxStackSize,
				string prm_description)
			{
				this.group = prm_group;
				this.itemID = prm_itemID;
				this.chance = prm_chance;
				this.minStackSize = prm_minStackSize;
				this.maxStackSize = prm_maxStackSize;
				this.description = prm_description;
			}

			public MeteorLootItem(string src)
			{
				this.group = 0;
				this.itemID = 0;
				this.chance = 0;
				this.minStackSize = 0;
				this.maxStackSize = 0;
				this.description = "";
				Parse(src);
			}

			public new string ToString()
			{
				string result = "";
				result += this.group.ToString() + ";";
				result += this.itemID.ToString() + ";";
				result += this.chance.ToString() + ";";
				result += this.minStackSize.ToString() + ";";
				result += this.maxStackSize.ToString() + ";";
				result += this.description;

				return result;
			}

			public void Parse(string str)
			{
				string[] strs = str.Split(new char[] { ';' });

				this.group = int.Parse(strs[0]);
				this.itemID = int.Parse(strs[1]);
				this.chance = float.Parse(strs[2]);
				this.minStackSize = int.Parse(strs[3]);
				this.maxStackSize = int.Parse(strs[4]);
				this.description = strs[5];
			}
		}

		public class MeteorsLootItemsDB
		{
			public string fileName;
			public List<MeteorLootItem> lootItems;

			public MeteorsLootItemsDB()
			{
				this.lootItems = new List<MeteorLootItem>();
				this.Load();
			}

			public void Add(int prm_group,
				int prm_itemID,
				float prm_chance,
				int prm_minStackSize,
				int prm_maxStackSize,
				string prm_description)
			{
				this.lootItems.Add(new MeteorLootItem(prm_group, prm_itemID, prm_chance, prm_minStackSize, prm_maxStackSize, prm_description));
				this.Save();
			}

			public void Add(string src)
			{
				this.lootItems.Add(new MeteorLootItem(src));
				this.Save();
			}

			public void Save()
			{
				List<string> buffer = new List<string>();

				foreach (MeteorLootItem mli in this.lootItems)
					buffer.Add(mli.ToString());

				Interface.GetMod().DataFileSystem.WriteObject(LootItemsFilePath, buffer);
			}

			public void Load()
			{
				this.lootItems.Clear();

				List<string> buffer = Interface.GetMod().DataFileSystem.ReadObject<List<string>>(LootItemsFilePath);

				if (buffer.Count == 0)
				{
					this.LoadDefaults();
					return;
				}

				foreach (string entry in buffer)
					this.Add(entry);
			}

			public void LoadDefaults()
			{
				this.Add(0, (int)EItemCode.Amber, 0.9f, 05, 20, "Amber");
				this.Add(0, (int)EItemCode.Amber, 0.8f, 15, 50, "MoreAmber");
				this.Add(0, (int)EItemCode.Coal, 1.0f, 50, 120, "Coal");
				this.Add(0, (int)EItemCode.Coal, 0.8f, 150, 200, "MoreCoal");
				this.Add(0, (int)EItemCode.Stone, 1.0f, 30, 50, "Stone");
				this.Add(0, (int)EItemCode.Stone, 1.0f, 30, 50, "MoreStone");
				this.Add(0, (int)EItemCode.Stone, 1.0f, 30, 50, "EvenMoreStone");
				this.Add(0, (int)EItemCode.Stone, 1.0f, 100, 150, "ALotOfStone");
				this.Add(0, (int)EItemCode.IronOre, 0.9f, 10, 30, "IronOre");
				this.Add(0, (int)EItemCode.IronOre, 0.8f, 30, 60, "MoreIronOre");
				this.Add(0, (int)EItemCode.Metal2Ore, 0.9f, 50, 100, "Titr");
				this.Add(0, (int)EItemCode.Metal2Ore, 0.8f, 120, 230, "MoreTitr");
				this.Add(0, (int)EItemCode.ShapedMetal3, 0.8f, 30, 60, "Mond");
				this.Add(0, (int)EItemCode.ShapedMetal3, 0.7f, 80, 150, "MoreMond");
				this.Add(0, (int)EItemCode.ShapedMetal4, 0.8f, 20, 40, "Ultr");
				this.Add(0, (int)EItemCode.ShapedMetal4, 0.7f, 60, 120, "MoreUltr");

				this.Add(0, (int)EItemCode.ShotgunShell, 0.6f, 10, 30, "ShotgunShells");
				this.Add(0, (int)EItemCode.ARBullet, 0.6f, 50, 150, "ARBullets");
				this.Add(0, (int)EItemCode.Bullet, 0.6f, 10, 40, "RifleBullets");
				this.Add(0, (int)EItemCode.PowerBow2, 0.5f, 1, 1, "Bow3");
				this.Add(0, (int)EItemCode.GoldAxe, 0.5f, 1, 1, "GoldAxe");
				this.Add(0, (int)EItemCode.TorettoWheel, 0.35f, 1, 1, "Wheel1");
				this.Add(0, (int)EItemCode.SmallCartWheel, 0.35f, 1, 1, "Wheel2");
				this.Add(0, (int)EItemCode.ManglerWheel, 0.35f, 1, 1, "Wheel3");
				this.Add(0, (int)EItemCode.LargeTractorWheel, 0.35f, 1, 1, "Wheel4"); 
				this.Add(0, (int)EItemCode.GoatWheelNipples, 0.35f, 1, 1, "Wheel5");
				this.Add(0, (int)EItemCode.DriftLargeWheel, 0.35f, 1, 1, "Wheel6");
				this.Add(0, (int)EItemCode.GoatRoadGearbox, 0.35f, 1, 1, "Gearbox1");
				this.Add(0, (int)EItemCode.GoatStockGearbox, 0.35f, 1, 1, "Gearbox2");
				this.Add(0, (int)EItemCode.RoachRoadGearbox, 0.35f, 1, 1, "Gearbox3");
				this.Add(0, (int)EItemCode.RoachStockGearbox, 0.35f, 1, 1, "Gearbox4");

 				this.Add(0, (int)EItemCode.DiamondSaw, 0.15f, 1, 1, "DiamondSaw");
				this.Add(0, (int)EItemCode.RoughDiamond, 0.15f, 1, 1, "RoughDiamong");
				this.Add(0, (int)EItemCode.DetonatorCap, 0.15f, 1, 1, "DetonatorCap");
				this.Add(0, (int)EItemCode.C4Explosive, 0.05f, 1, 1, "C4");
				this.Add(0, (int)EItemCode.PowerfullEngine, 0.05f, 1, 1, "PowerfullEngine1");
				this.Add(0, (int)EItemCode.GoatPowerfullEngine, 0.05f, 1, 1, "PowerfullEngine2");
				this.Save();
			}
		}
		#endregion

		public MeteorsLootItemsDB lootItemsDB;
		public MeteorsFallSitesDB fallsSitesDB;

		public static ExplosionConfiguration C4ExplosionConfig;
		public static ExplosionConfiguration landcrabExplosionConfig;

		public ConfigurationField_Int cfg_MeteorsInterval;
		public ConfigurationField_Int cfg_MeteorsHeatDuration;
		public ConfigurationField_Float cfg_MeteorsLootFactor;
		public ConfigurationField_Bool cfg_MeteorsEnabled;
		public ConfigurationField_Bool cfg_MeteorsDamageBuildings;
		public ConfigurationField_Int cfg_MeteorsEarlyAnnouncementTime;
		public ConfigurationField_Int cfg_MaximumLootPlayersCount;


		public Timer randomMeteorTimer1 = null;
		public Timer randomMeteorTimer2 = null;
		public float nextRandomMeteorTimerStart = 2;

		public int maxLootPlayersCount;

		void Init()
		{
			Instance = this;

			landcrabExplosionConfig = (from o in Resources.FindObjectsOfTypeAll<ExplosiveDynamicServer>() where o.transform.name.Equals("LandCrabDynamicObject") select o).First().Configuration;
			C4ExplosionConfig = (from o in Resources.FindObjectsOfTypeAll<ExplosiveDynamicServer>() where o.transform.name.Equals("C4DynamicObject") select o).First().Configuration;

			LoadConfig();
			this.cfg_MeteorsInterval = new ConfigurationField_Int(this.Config, "MeteorInterval", 60);
			this.cfg_MeteorsHeatDuration = new ConfigurationField_Int(this.Config, "MeteorsHeatDuration", 5);
			this.cfg_MeteorsLootFactor = new ConfigurationField_Float(this.Config, "MeteorsLootFactor", 1.0f);
			this.cfg_MeteorsEnabled = new ConfigurationField_Bool(this.Config, "MeteorsEnabled", true);
			this.cfg_MeteorsDamageBuildings = new ConfigurationField_Bool(this.Config, "MeteorsDamageBuildings", true);
			this.cfg_MeteorsEarlyAnnouncementTime = new ConfigurationField_Int(this.Config, "MeteorsEarlyAnnouncementTime", 5);
			this.cfg_MaximumLootPlayersCount = new ConfigurationField_Int(this.Config, "MaximumLootPlayersCount", 60);

			this.maxLootPlayersCount = this.cfg_MaximumLootPlayersCount.value;

			this.fallsSitesDB = new MeteorsFallSitesDB();
			this.lootItemsDB = new MeteorsLootItemsDB();

			LoadMessages();

			if (this.cfg_MeteorsEnabled.value)
				StartRandomMeteors();
		}

		#region msgs_consts
		const string msg_prefix = "msg_prefix";
		const string msg_notadmin = "msg_notadmin";
		const string msg_setenabled_badusage = "msg_setenabled_badusage";
		const string msg_setenabled_changed = "msg_setenabled_changed";
		const string msg_throwmeteor_badusage = "msg_throwmeteor_badusage ";
		const string msg_throwmeteor_playernotfound = "msg_throwmeteor_playernotfound";
		const string msg_throwmeteor_fall_warning = "msg_throwmeteor_fall_warning";
		const string msg_meteor_located_fallwarning = "msg_meteor_located_fallwarning";
		const string msg_meteor_hot = "msg_meteor_heatduration";
		const string msg_meteor_cold = "msg_meteor_cold";
		const string msg_fallsite_added = "msg_fallsite_added";
		const string msg_fallsite_badusage1 = "msg_fallsite_badusage1";
		const string msg_fallsite_badusage2 = "msg_fallsite_badusage2";
		const string msg_fallsite_badusage3 = "msg_fallsite_badusage3";
		const string msg_fallsite_notfound = "msg_fallsite_notfound";
		const string msg_setinterval_value = "msg_interval_value";
		const string msg_setinterval_badusage = "msg_interval_badusage";
		const string msg_meteor_earlyfallwarning = "msg_meteor_earlyfallwarning";
		const string msg_setheatduration_value = "msg_setheatduration_value";
		const string msg_setheatduration_badusage = "msg_setheatduration_badusage";
		const string msg_setlootfactor_value = "msg_setlootfactor_value";
		const string msg_setlootfactor_badusage = "msg_setlootfactor_badusage";
		const string msg_removefallsite_badusage = "msg_removefallsite_badusage";
		const string msg_removefallsite_notfound = "msg_removefallsite_notfound";
		const string msg_removefallsite_done = "msg_removefallsite_done";
		const string msg_meteorinfo_next = "msg_meteorinfo_next";
		const string msg_meteorinfo_current = "msg_meteorinfo_current";
		const string msg_meteorinfo_none = "msg_meteorinfo_none";
		const string msg_help_cmdsheader = "msg_help_cmdsheader";
		const string msg_help_cmd0 = "msg_help_cmd0";
		const string msg_help_cmd1 = "msg_help_cmd1";
		const string msg_help_cmd2 = "msg_help_cmd2";
		const string msg_help_cmd3 = "msg_help_cmd3";
		const string msg_help_cmd4 = "msg_help_cmd4";
		const string msg_help_cmd5 = "msg_help_cmd5";
		const string msg_help_cmd6 = "msg_help_cmd6";
		const string msg_help_cmd7 = "msg_help_cmd7";
		#endregion

		private void LoadMessages()
		{ 
			lang.RegisterMessages(new Dictionary<string, string>
				{
					{msg_prefix, "<color=lime>[Meteors]</color>"},
					{msg_notadmin, "<color=orange>You must be an admin to use this command.</color>"},
					{msg_setenabled_changed, "<color=orange>Random meteors falls enabled is {0}.</color>"},
					{msg_setenabled_badusage, "<color=orange>Invalid usage. use it like: /setmeteorsenabled true/false/1/0</color>"},
					{msg_throwmeteor_badusage , "<color=orange>Bad usage of /throwmeteor command. Please inform a target (location index or player's name)!</color>" },
					{msg_throwmeteor_playernotfound, "<color=orange>This player has not been found.</color>"},
					{msg_throwmeteor_fall_warning, "<color=red>A fiery meteor is falling near </color><color=lime>{0}!</color>"},
					{msg_meteor_located_fallwarning, "<color=red>A fiery meteor is falling near </color><color=orange>{0}!</color>"},
					{msg_meteor_hot, "<color=orange>The meteor area is way too hot to approach. It should be colder in </color><color=lightblue>{0} minute(s).</color>"},
					{msg_meteor_cold, "<color=orange>The meteor area is now cold!</color><color=lightblue> It is safe to approach!</color>"},
					{msg_fallsite_added, "<color=orange>Fall site added!</color>"},
					{msg_fallsite_badusage1, "<color=orange>Invalid usage. Use it like:</color>"},
					{msg_fallsite_badusage2, "<color=orange>/addfallsite <radius> <name></color>"},
					{msg_fallsite_badusage3, "<color=orange>Ex: /addfallsite 150 Valley</color>"},
					{msg_fallsite_notfound, "<color=orange>The specified fall site has not been found.</color>"},
					{msg_setinterval_value, "<color=orange>Meteors fall interval is set at {0} minute(s).</color>"},
					{msg_setinterval_badusage, "<color=orange>Invalid usage. use it like \"/setmeteorsinterval 60\" (in minutes).</color>"},

					{msg_meteor_earlyfallwarning, "<color=orange>A meteor has been spotted! Looks like it will fall at </color><color=green>{0}</color><color=orange> in {1} minute(s)!</color>"},

					{msg_setheatduration_value, "<color=orange>Meteors heat duration is set at {0} minute(s).</color>"},
					{msg_setheatduration_badusage, "<color=orange>Invalid usage. use it like \"/setmeteorsheatduration 5\" (in minutes).</color>"},
					{msg_setlootfactor_value, "<color=orange>Meteors loot factor is set at {0}.</color>"},
					{msg_setlootfactor_badusage, "<color=orange>Invalid usage. use it like \"/setmeteorslootfactor 1.3\"</color>"},

					{ msg_removefallsite_badusage, "<color=orange>Invalid usage. use it like \"/removefallsite 4\" (index number of the fall site).</color>"},
					{msg_removefallsite_notfound, "<color=orange>There is no fall site with this index.</color>"},
					{msg_removefallsite_done, "<color=orange>Fall site removed!</color>"},
				    
					{msg_meteorinfo_next, "<color=orange>Next meteor fall is in {0} minute(s).</color>"},
					{msg_meteorinfo_current, "<color=orange>There is a hot meteor at {0}. It should be colder in </color><color=lightblue>{1} minute(s).</color>"},
					{msg_meteorinfo_none, "<color=orange>There is not meteor being expected right now.</color>"},

					{msg_help_cmdsheader, "<color=orange>Admin commands:</color>"},
					{msg_help_cmd0, "<color=orange>/setmeteorsheatduration</color><color=lightblue> <minutes></color>"},
					{msg_help_cmd1, "<color=orange>/setmeteorslootfactor</color><color=lightblue> <rate number></color>"},
					{msg_help_cmd2, "<color=orange>/setmeteorsinterval</color><color=lightblue> <minutes></color>"}, 
					{msg_help_cmd3, "<color=orange>/setmeteorsenabled</color><color=lightblue> <1/0/true/false></color>"},
					{msg_help_cmd4, "<color=orange>/listfallsites</color>"},
					{msg_help_cmd5, "<color=orange>/addfallsite</color><color=lightblue> <area radius> <display name></color>"},
					{msg_help_cmd6, "<color=orange>/removefallsite</color><color=lightblue> <fallsite ID></color>"},
					{msg_help_cmd7, "<color=orange>/throwmeteor</color><color=lightblue> <player name or fallsite ID></color>"},
				}, this);
		}

		public float GetGlobaDropRates()
		{
			float pc = ((float)GameManager.Instance.GetPlayerCount() / this.maxLootPlayersCount);
			pc = Mathf.Min(pc, 1) * 0.8f;
			return ((pc * pc) + 0.2f) * this.cfg_MeteorsLootFactor.value;
		}

		public void StartRandomMeteors()
		{
			if (this.randomMeteorTimer1 != null)
				this.randomMeteorTimer1.Destroy();
			
			if (this.cfg_MeteorsInterval.value <= 0)
			{
				PrintError("Meteors Interval cannot be smaller or equal to zero! Fix the value and reload plugin.");
				return;
			}

			if (this.cfg_MeteorsEarlyAnnouncementTime.value > this.cfg_MeteorsInterval.value)
			{
				PrintError("Meteors early annoucement \"MeteorsEarlyAnnouncementTime\" time cannot be greater or equal to the meteors interval \"MeteorsInterval\"! Fix the value and reload plugin.");
				return;
			}

			if (this.cfg_MeteorsEarlyAnnouncementTime.value > 0)
			{
				this.nextRandomMeteorTimerStart = Time.time;
				this.randomMeteorTimer1 = this.timer.Once((this.cfg_MeteorsInterval.value - this.cfg_MeteorsEarlyAnnouncementTime.value) * 60, this.ScheduleMeteor);
				return;
			}
			 
			this.nextRandomMeteorTimerStart = Time.time;
			this.randomMeteorTimer2 = this.timer.Once(this.cfg_MeteorsInterval.value * 60, () => { this.SpawnRandomMeteor(null, true); });
		}
		 

		public void StopRandomMeteors()
		{
			if (this.randomMeteorTimer1 != null)
			{
				this.randomMeteorTimer1.Destroy();
				this.randomMeteorTimer1 = null;
			}

			if (this.randomMeteorTimer2 != null)
			{
				this.randomMeteorTimer2.Destroy();
				this.randomMeteorTimer2 = null;
			}
		}


		public void ScheduleMeteor()
		{
			if (this.randomMeteorTimer1 != null)
			{
				this.randomMeteorTimer1.Destroy();
				this.randomMeteorTimer1 = null;
			}

			int earlyAnnouncementTime = this.cfg_MeteorsEarlyAnnouncementTime.value;
			int meteorsInterval = this.cfg_MeteorsInterval.value;

			FallSiteInfo futureFallSite = this.fallsSitesDB.fallSites[Oxide.Core.Random.Range(this.fallsSitesDB.fallSites.Count)];

			hurt.BroadcastChat(string.Format(lang.GetMessage(msg_meteor_earlyfallwarning, this), futureFallSite.Name, earlyAnnouncementTime));

			if (this.randomMeteorTimer2 != null)
				this.randomMeteorTimer2.Destroy();

			// Prepares the next scheduled meteor
			this.randomMeteorTimer1 = this.timer.Once(meteorsInterval * 60, this.ScheduleMeteor);

			// Prepare the meteor throw
			this.randomMeteorTimer2 = this.timer.Once(earlyAnnouncementTime * 60, () => { this.SpawnRandomMeteor(futureFallSite, false); });
			return;
		}
		 
		public void SpawnRandomMeteor(FallSiteInfo? fallSite, bool prepareNext)
		{
			if (this.randomMeteorTimer2 != null)
			{
				this.randomMeteorTimer2.Destroy();
				this.randomMeteorTimer2 = null;
			}

			if (this.fallsSitesDB.fallSites.Count == 0)
			{
				PrintWarning("Random meteor falls are enabled, but no meteor fall site is defined!");
				return;
			}

			int rndIndex = Oxide.Core.Random.Range(this.fallsSitesDB.fallSites.Count);

			if (fallSite == null)
				fallSite = this.fallsSitesDB.fallSites[rndIndex];

			ThrowMeteorAtSite(fallSite.Value);

			if (prepareNext)
				this.randomMeteorTimer2 = this.timer.Once(this.cfg_MeteorsInterval.value * 60, () => { this.SpawnRandomMeteor(null, true); });
			
			this.nextRandomMeteorTimerStart = Time.time;
		}


		public void ThrowMeteorAtSite(FallSiteInfo fallSite)
		{
			float speed = 100;

			Vector3 targetPos = fallSite.position +
				new Vector3(UnityEngine.Random.Range(-fallSite.radius, fallSite.radius),
					0,
					UnityEngine.Random.Range(-fallSite.radius, fallSite.radius)
				);

			Vector3 startingPos = fallSite.position +
				new Vector3(UnityEngine.Random.Range(-1500, 1500),
					1000,
					UnityEngine.Random.Range(-1500, 1500)
				);

			hurt.BroadcastChat(string.Format(lang.GetMessage(msg_meteor_located_fallwarning, this), fallSite.Name));

			MeteorInstance newMeteor = new MeteorInstance(this.cfg_MeteorsHeatDuration.value, true, false, fallSite);
			newMeteor.Fire(startingPos, targetPos, speed);
		}


		public void ThrowMeteorAtPlayer(PlayerSession targetPlayer)
		{
			hurt.BroadcastChat(string.Format(lang.GetMessage(msg_throwmeteor_fall_warning, this), targetPlayer.Name));

			float speed = 100f;

			Vector3 final = targetPlayer.WorldPlayerEntity.transform.position;

			Vector3 starting = final + new Vector3(UnityEngine.Random.Range(-2500, 2500),
				1000,
				UnityEngine.Random.Range(-2500, 2500)
			);

			MeteorInstance newMeteor = new MeteorInstance(0, false, true, null);

			newMeteor.Fire(starting, final, speed);
		}

		[ChatCommand("removefallsite")]
		void cmdRemoveFallSite(PlayerSession session, string command, string[] args)
		{
			if (!session.IsAdmin)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_notadmin, this, session.SteamId.ToString()));
				return;
			}

			int index;
			if ((args.Length != 1) || (!int.TryParse(args[0], out index)))
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_removefallsite_badusage, this, session.SteamId.ToString()));
				return;
			}

			if (index >= this.fallsSitesDB.fallSites.Count)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_removefallsite_notfound, this, session.SteamId.ToString()));
				return;
			}

			this.fallsSitesDB.RemoveAt(index);
			hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_removefallsite_done, this, session.SteamId.ToString()));
		}


		[ChatCommand("addfallsite")]
		void cmdAddFallSite(PlayerSession session, string command, string[] args)
		{
			if (!session.IsAdmin)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_notadmin, this, session.SteamId.ToString()));
				return;
			}

			if (args.Length <= 1)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_fallsite_badusage1, this, session.SteamId.ToString()));
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_fallsite_badusage2, this, session.SteamId.ToString()));
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_fallsite_badusage3, this, session.SteamId.ToString()));
				return;
			}

			string siteName = args[1];
			if (args.Length > 2)
			{
				for (int i = 1; i < (args.Length - 1); i++)
					siteName += " " + args[i + 1];
			}

			Vector3 pos = session.WorldPlayerEntity.transform.position;
			float radius;

			if (!float.TryParse(args[0], out radius))
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_fallsite_badusage1, this, session.SteamId.ToString()));
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_fallsite_badusage2, this, session.SteamId.ToString()));
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_fallsite_badusage3, this, session.SteamId.ToString()));
				return;
			}

			this.fallsSitesDB.Add(pos, radius, siteName);

			hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_fallsite_added, this, session.SteamId.ToString()));
		}


		[ChatCommand("listfallsites")]
		void cmdListFallSites(PlayerSession session, string command, string[] args)
		{
			if (!session.IsAdmin)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_notadmin, this, session.SteamId.ToString()));
				return;
			}

			string pos;
			string msg = "";
			hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_notadmin, this, session.SteamId.ToString()));
			for (int i = 0; i < this.fallsSitesDB.fallSites.Count; i++)
			{
				msg = "";

				msg += "<color=lime>";
				msg += i.ToString().PadLeft(2, '0') + ": ";

				msg += "</color><color=orange>";
				pos = this.fallsSitesDB.fallSites[i].position.x.ToString("0").PadLeft(5, ' ') + "," + this.fallsSitesDB.fallSites[i].position.z.ToString("0").PadLeft(5, ' ');
				msg += pos + "; ";

				msg += "Radius: " + this.fallsSitesDB.fallSites[i].radius.ToString() + "; ";
				msg += this.fallsSitesDB.fallSites[i].Name.PadLeft(20);
				msg += "</color>";
				hurt.SendChatMessage(session, msg);
			}
		}


		[ChatCommand("setmeteorsenabled")]
		void cmdSetMeteorsEnabled(PlayerSession session, string command, string[] args)
		{
			if (!session.IsAdmin)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_notadmin, this, session.SteamId.ToString()));
				return;
			}

			if (args.Length != 1)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_setenabled_badusage, this, session.SteamId.ToString()));
				return;
			}

			bool enabled = false;
			if ((args[0] == "false") || (args[0] == "0"))
				enabled = false;
			else
				if ((args[0] == "true") || (args[0] == "1"))
				enabled = true;
			else
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_setenabled_badusage, this, session.SteamId.ToString()));
				return;
			}

			this.cfg_MeteorsEnabled.value = enabled;

			hurt.SendChatMessage(session,
				lang.GetMessage(msg_prefix, this),
				string.Format(lang.GetMessage(msg_setenabled_changed, this, session.SteamId.ToString()), enabled.ToString())
			);

			if (enabled)
				this.StartRandomMeteors();
			else
				this.StopRandomMeteors();
		}


		[ChatCommand("setmeteorsinterval")]
		void cmdSetMeteorsInterval(PlayerSession session, string command, string[] args)
		{
			if (!session.IsAdmin)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_notadmin, this, session.SteamId.ToString()));
				return;
			}

			if (args.Length > 1)
			{
				hurt.SendChatMessage(session,
					lang.GetMessage(msg_prefix, this),
					lang.GetMessage(msg_setinterval_badusage, this, session.SteamId.ToString())
				);
				return;
			}

			if (args.Length == 1)
			{
				int val;

				if (!int.TryParse(args[0], out val))
				{
					hurt.SendChatMessage(session,
						lang.GetMessage(msg_prefix, this),
						lang.GetMessage(msg_setinterval_badusage, this, session.SteamId.ToString())
					);
					return;
				}

				this.cfg_MeteorsInterval.value = val;

				if (this.cfg_MeteorsEnabled.value)
					this.StartRandomMeteors();
			}

			hurt.SendChatMessage(session,
				lang.GetMessage(msg_prefix, this),
				string.Format(lang.GetMessage(msg_setinterval_value, this, session.SteamId.ToString()),
					this.cfg_MeteorsInterval.value.ToString())
			);
		}


		[ChatCommand("setmeteorslootfactor")]
		void cmdSetMeteorsLootFactor(PlayerSession session, string command, string[] args)
		{
			if (!session.IsAdmin)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_notadmin, this, session.SteamId.ToString()));
				return;
			}

			if (args.Length > 1)
			{
				hurt.SendChatMessage(session,
					lang.GetMessage(msg_prefix, this),
					lang.GetMessage(msg_setlootfactor_badusage, this, session.SteamId.ToString())
				);
				return;
			}

			if (args.Length == 1)
			{
				float val;

				if (!float.TryParse(args[0], out val))
				{
					hurt.SendChatMessage(session,
						lang.GetMessage(msg_prefix, this),
						lang.GetMessage(msg_setlootfactor_badusage, this, session.SteamId.ToString())
					);
					return;
				}

				this.cfg_MeteorsLootFactor.value = val;
			}

			hurt.SendChatMessage(session,
				lang.GetMessage(msg_prefix, this),
				string.Format(lang.GetMessage(msg_setlootfactor_value, this, session.SteamId.ToString()),
					this.cfg_MeteorsLootFactor.value.ToString())
			);
		}

		[ChatCommand("setmeteorsheatduration")]
		void cmdSetMeteorsHeatDuration(PlayerSession session, string command, string[] args)
		{
			if (!session.IsAdmin)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_notadmin, this, session.SteamId.ToString()));
				return;
			}

			if (args.Length > 1)
			{
				hurt.SendChatMessage(session,
					lang.GetMessage(msg_prefix, this),
					lang.GetMessage(msg_setheatduration_badusage, this, session.SteamId.ToString())
				);
				return;
			}

			if (args.Length == 1)
			{
				int val;

				if (!int.TryParse(args[0], out val))
				{
					hurt.SendChatMessage(session,
						lang.GetMessage(msg_prefix, this),
						lang.GetMessage(msg_setheatduration_badusage, this, session.SteamId.ToString())
					);
					return;
				}

				this.cfg_MeteorsHeatDuration.value = val;
			}

			hurt.SendChatMessage(session,
				lang.GetMessage(msg_prefix, this),
				string.Format(lang.GetMessage(msg_setheatduration_value, this, session.SteamId.ToString()),
					this.cfg_MeteorsHeatDuration.value.ToString())
			);
		}

		public PlayerSession FindPlayer(string name)
		{
			PlayerSession session = null;
			Dictionary<uLink.NetworkPlayer, PlayerSession> sessions = Singleton<GameManager>.Instance.GetSessions();

			foreach (KeyValuePair<uLink.NetworkPlayer, PlayerSession> entry in sessions)
				if (entry.Value != null)
					if (entry.Value.Name.ToLower().Contains(name.ToLower()))
						return entry.Value;
			return session;
		}

		private void Unload()
		{
			Puts("Destroying meteors");


			if (this.randomMeteorTimer1 != null)
			{
				this.randomMeteorTimer1.Destroy();
				this.randomMeteorTimer1 = null;
			}

			if (this.randomMeteorTimer2 != null)
			{
				this.randomMeteorTimer2.Destroy();
				this.randomMeteorTimer2 = null;
			}

			foreach (MeteorInstance mi in InstantiatedMeteors)
				mi.Destroy(false);

			InstantiatedMeteors.Clear();
		}

		private object OnServerCommand(string arg)
		{
			string[] args = arg.Split(new char[] { ' ' });

			if ((args.Length == 2) && (args[0].ToLower() == "destroyall") && (args[1].ToLower() == "meteor"))
			{
				if (InstantiatedMeteors.Count == 0)
				{
					Puts("No meteors alive to be destroyed.");
					return "1";
				}

				foreach (MeteorInstance mi in InstantiatedMeteors)
				{
					if (mi != null)
						mi.Destroy(false);
				}

				InstantiatedMeteors.Clear();

				Puts("All meteors were destroyed!");
				return "1";
			}

			if ((args[0].ToLower() == "throwmeteor"))
			{
				if (args.Length != 2)
				{
					Puts(lang.GetMessage(msg_throwmeteor_badusage, this));
					return "1";
				}

				// If the arg is a valid integer, consider it a fall site.
				int fallSiteIndex;
				if (int.TryParse(args[1], out fallSiteIndex))
				{
					if (fallSiteIndex >= this.fallsSitesDB.fallSites.Count)
					{
						Puts(lang.GetMessage(msg_fallsite_notfound, this));
						return "1";
					}

					ThrowMeteorAtSite(this.fallsSitesDB.fallSites[fallSiteIndex]);
					return "1";
				}

				// As it is not an valid integer, consider it a player name.
				PlayerSession targetPlayer = FindPlayer(args[1]);

				if (targetPlayer == null)
				{
					Puts(lang.GetMessage(msg_throwmeteor_playernotfound, this));
					return "1";
				}


				ThrowMeteorAtPlayer(targetPlayer);
				return "1";
			}

			return null;
		}


		[ChatCommand("throwmeteor")]
		void cmdThrowMeteor(PlayerSession session, string command, string[] args)
		{
			if (!session.IsAdmin)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_notadmin, this, session.SteamId.ToString()));
				return;
			}

			// No args -> throw at the admin.
			if (args.Length == 0)
			{
				this.ThrowMeteorAtPlayer(session);
				return;
			}

			// If the arg is a valid integer, consider it a fall site.
			int fallSiteIndex;
			if (int.TryParse(args[0], out fallSiteIndex))
			{
				if (fallSiteIndex >= this.fallsSitesDB.fallSites.Count)
				{
					hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_throwmeteor_playernotfound, this, session.SteamId.ToString()));
					return;
				}

				ThrowMeteorAtSite(this.fallsSitesDB.fallSites[fallSiteIndex]);
				return;
			}

			// As it is not an valid integer, consider it a player name.
			PlayerSession targetPlayer = FindPlayer(args[0]);

			if (targetPlayer == null)
			{
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_throwmeteor_playernotfound, this, session.SteamId.ToString()));
				return;
			}

			this.ThrowMeteorAtPlayer(targetPlayer);
		}

		[ChatCommand("meteor")]
		void cmdMeteorInfo(PlayerSession session, string command, string[] args)
		{
			if ((args.Length == 1) && (args[0].ToLower() == "help"))
			{
				if (!session.IsAdmin)
				{
					hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_notadmin, this, session.SteamId.ToString()));
					return;
				}

				string steamID = session.SteamId.ToString();
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this, steamID), lang.GetMessage(msg_help_cmdsheader, this, steamID));
				hurt.SendChatMessage(session, lang.GetMessage(msg_help_cmd0, this, steamID));
				hurt.SendChatMessage(session, lang.GetMessage(msg_help_cmd1, this, steamID));
				hurt.SendChatMessage(session, lang.GetMessage(msg_help_cmd2, this, steamID));
				hurt.SendChatMessage(session, lang.GetMessage(msg_help_cmd3, this, steamID));
				hurt.SendChatMessage(session, lang.GetMessage(msg_help_cmd4, this, steamID));
				hurt.SendChatMessage(session, lang.GetMessage(msg_help_cmd5, this, steamID));
				hurt.SendChatMessage(session, lang.GetMessage(msg_help_cmd0, this, steamID));
				hurt.SendChatMessage(session, lang.GetMessage(msg_help_cmd7, this, steamID));
				return;
			}

			IEnumerable<MeteorInstance> lootMeteors = InstantiatedMeteors.Where<MeteorInstance>(o => o.targetFallSite.HasValue);

			if (lootMeteors.Count() == 0)
			{
				if (this.cfg_MeteorsEnabled.value == false)
				{
					hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), lang.GetMessage(msg_meteorinfo_none, this, session.SteamId.ToString()));
					return;
				}

				int minutesLeft = (int)(1 + this.cfg_MeteorsInterval.value - ((Time.time - nextRandomMeteorTimerStart) / 60.0f) );
				hurt.SendChatMessage(session, lang.GetMessage(msg_prefix, this), string.Format(lang.GetMessage(msg_meteorinfo_next, this, session.SteamId.ToString()), minutesLeft));
				return;
			}

			foreach (MeteorInstance mi in lootMeteors)
			{
				int heatMinutesLeft = (int)(((mi.heatDuration * 60) - (Time.time - mi.heat_StartingTime)) / 60.0f);
				string fallsiteName = mi.targetFallSite.Value.Name;

				hurt.SendChatMessage(
					session,
					lang.GetMessage(msg_prefix, this),
					string.Format(lang.GetMessage(msg_meteorinfo_current, this, session.SteamId.ToString()), fallsiteName, heatMinutesLeft)
				);
			}
		}

	}
}