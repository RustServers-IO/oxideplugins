using System.Collections.Generic;
using Facepunch;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;


namespace Oxide.Plugins
{
    [Info("RadtownAnimals", "k1lly0u", "0.2.6", ResourceId = 1561)]
    class RadtownAnimals : RustPlugin
    {
        #region Fields
        private Dictionary<BaseEntity, Vector3> animalList = new Dictionary<BaseEntity, Vector3>();
        private List<Timer> refreshTimers = new List<Timer>();
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            lang.RegisterMessages(messages, this);
        }
        void OnServerInitialized()
        {            
            LoadVariables();
            InitializeAnimalSpawns();
        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (entity.GetComponent<BaseNpc>() != null)
                {
                    if (animalList.ContainsKey(entity as BaseEntity))
                    {
                        UnityEngine.Object.Destroy(entity.GetComponent<RAController>());
                        InitiateRefresh(entity as BaseEntity);
                    }
                }
            }
            catch { }
        }
        void Unload()
        {
            foreach (var time in refreshTimers)
                time.Destroy();

            foreach (var animal in animalList)
            {
                if (animal.Key != null)
                {
                    UnityEngine.Object.Destroy(animal.Key.GetComponent<RAController>());
                    animal.Key.KillMessage();
                }
            }
            var objects = UnityEngine.Object.FindObjectsOfType<RAController>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
            animalList.Clear();
        }
        #endregion

        #region Initial Spawning
        private void InitializeAnimalSpawns()
        {
            var allobjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (var gobject in allobjects)
            {
                if (gobject.name.Contains("autospawn/monument"))
                {
                    var position = gobject.transform.position;
                    if (gobject.name.ToLower().Contains("lighthouse"))
                    {
                        if (configData.Lighthouses.Enabled)
                        {
                            SpawnAnimals(position, GetSpawnList(configData.Lighthouses.AnimalCounts));
                            continue;
                        }
                    }
                    if (gobject.name.Contains("powerplant_1"))
                    {
                        if (configData.Powerplant.Enabled)
                        {
                            SpawnAnimals(position, GetSpawnList(configData.Powerplant.AnimalCounts));
                            continue;
                        }
                    }

                    if (gobject.name.Contains("military_tunnel_1"))
                    {
                        if (configData.MilitaryTunnels.Enabled)
                        {
                            SpawnAnimals(position, GetSpawnList(configData.MilitaryTunnels.AnimalCounts));
                            continue;
                        }
                    }

                    if (gobject.name.Contains("airfield_1"))
                    {
                        if (configData.Airfield.Enabled)
                        {
                            SpawnAnimals(position, GetSpawnList(configData.Airfield.AnimalCounts));
                            continue;
                        }
                    }

                    if (gobject.name.Contains("trainyard_1"))
                    {
                        if (configData.Trainyard.Enabled)
                        {
                            SpawnAnimals(position, GetSpawnList(configData.Trainyard.AnimalCounts));
                            continue;
                        }
                    }

                    if (gobject.name.Contains("water_treatment_plant_1"))
                    {
                        if (configData.WaterTreatmentPlant.Enabled)
                        {
                            SpawnAnimals(position, GetSpawnList(configData.WaterTreatmentPlant.AnimalCounts));
                            continue;
                        }
                    }

                    if (gobject.name.Contains("warehouse"))
                    {
                        if (configData.Warehouses.Enabled)
                        {
                            SpawnAnimals(position, GetSpawnList(configData.Warehouses.AnimalCounts));
                            continue;
                        }
                    }

                    if (gobject.name.Contains("satellite_dish"))
                    {
                        if (configData.Satellite.Enabled)
                        {
                            SpawnAnimals(position, GetSpawnList(configData.Satellite.AnimalCounts));
                            continue;
                        }
                    }

                    if (gobject.name.Contains("sphere_tank"))
                    {
                        if (configData.SphereTank.Enabled)
                        {
                            SpawnAnimals(position, GetSpawnList(configData.SphereTank.AnimalCounts));
                            continue;
                        }
                    }

                    if (gobject.name.Contains("radtown_small_3"))
                    {
                        if (configData.Radtowns.Enabled)
                        {
                            SpawnAnimals(position, GetSpawnList(configData.Radtowns.AnimalCounts));
                            continue;
                        }
                    }
                }               
            }
        }
        private Dictionary<string, int> GetSpawnList(AnimalCounts counts)
        {
            var spawnList = new Dictionary<string, int>
            {
                {"bear", counts.Bears},
                {"boar", counts.Boars },
                {"chicken", counts.Chickens },
                {"horse", counts.Horses },
                {"stag", counts.Stags },
                {"wolf", counts.Wolfs },
                {"zombie", counts.Zombies }
            };
            return spawnList;
        }        
        private void SpawnAnimals(Vector3 position, Dictionary<string,int> spawnList)
        {
            if (animalList.Count >= configData.a_Options.TotalMaximumAmount)
            {
                PrintError(lang.GetMessage("spawnLimit", this));
                return;
            }
            foreach (var type in spawnList)
            {                
                for (int i = 0; i < type.Value; i++)
                {
                    SpawnAnimalEntity(type.Key, position); 
                }
            }
        }
        #endregion

        #region Spawn Control
        private void InitiateRefresh(BaseEntity animal)
        {
            var position = animal.transform.position;
            var type = animal.ShortPrefabName.Replace(".prefab", "");
            refreshTimers.Add(timer.Once(configData.a_Options.RespawnTimer * 60, () => InitializeNewSpawn(type, position)));
            animalList.Remove(animal);
        }
        private void InitializeNewSpawn(string type, Vector3 position) => SpawnAnimalEntity(type, position);          
        private void SpawnAnimalEntity(string type, Vector3 pos)
        {
            Vector3 point;
            if (FindPointOnNavmesh(pos, 50, out point))
            {
                BaseEntity entity = InstantiateEntity($"assets/rust.ai/agents/{type}/{type}.prefab", point);                
                entity.Spawn();
                var npc = entity.gameObject.AddComponent<RAController>();
                npc.SetHome(point);
                animalList.Add(entity, point);
            }
        }
        private BaseEntity InstantiateEntity(string type, Vector3 position)
        {
            var prefab = GameManager.server.FindPrefab(type);
            var gameObject = Instantiate.GameObject(prefab, position, new Quaternion());
            gameObject.name = type;
            SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);
            if (!gameObject.activeSelf)                                       
                gameObject.SetActive(true);            
            if (gameObject.GetComponent<Spawnable>())
                UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());
            BaseEntity component = gameObject.GetComponent<BaseEntity>();
            return component;
        }

        private bool FindPointOnNavmesh(Vector3 center, float range, out Vector3 result)
        {
            for (int i = 0; i < 30; i++)
            {
                Vector3 randomPoint = center + Random.insideUnitSphere * range;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomPoint, out hit, 50f, NavMesh.AllAreas))
                {
                    if (hit.position.y - TerrainMeta.HeightMap.GetHeight(hit.position) > 3)
                        continue;
                    result = hit.position;
                    return true;
                }
            }
            result = Vector3.zero;
            return false;
        }
        #endregion

        #region NPCController
        class RAController : MonoBehaviour
        {
            public BaseNpc npc;
            private Vector3 homePos;

            private void Awake()
            {
                npc = GetComponent<BaseNpc>();
                enabled = false;
            }
            private void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, CheckLocation);
            }
            public void SetHome(Vector3 homePos)
            {
                this.homePos = homePos;
                InvokeHandler.InvokeRepeating(this, CheckLocation, 1f, 20f);
            }

            private void CheckLocation()
            {
                if (Vector3.Distance(npc.transform.position, homePos) > 100)
                {
                    npc.UpdateDestination(homePos);
                }
            }
        }
        #endregion

        #region Commands
        [ChatCommand("ra_killall")]
        private void chatKillAnimals(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            foreach(var animal in animalList)
            {
                UnityEngine.Object.Destroy(animal.Key.GetComponent<RAController>());
                animal.Key.KillMessage();
            }
            animalList.Clear();
            SendReply(player, lang.GetMessage("title", this, player.UserIDString) + lang.GetMessage("killedAll", this, player.UserIDString));
        }

        [ConsoleCommand("ra_killall")]
        private void ccmdKillAnimals(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                foreach (var animal in animalList)
                {
                    UnityEngine.Object.Destroy(animal.Key.GetComponent<RAController>());
                    animal.Key.KillMessage();
                }
                animalList.Clear();
                SendReply(arg, lang.GetMessage("killedAll", this));
            }
        }
        #endregion

        #region Config 
        #region Options       
        class AnimalCounts
        {
            public int Bears;
            public int Boars;
            public int Chickens;
            public int Horses;
            public int Stags;
            public int Wolfs;
            public int Zombies;
        }
        class LightHouses
        {
            public AnimalCounts AnimalCounts { get; set; }  
            public bool Enabled { get; set; }          
        }
        class Airfield
        {
            public AnimalCounts AnimalCounts { get; set; }
            public bool Enabled { get; set; }
        }

        class Powerplant
        {
            public AnimalCounts AnimalCounts { get; set; }
            public bool Enabled { get; set; }
        }

        class Trainyard
        {
            public AnimalCounts AnimalCounts { get; set; }
            public bool Enabled { get; set; }
        }

        class WaterTreatmentPlant
        {
            public AnimalCounts AnimalCounts { get; set; }
            public bool Enabled { get; set; }
        }

        class Warehouses
        {
            public AnimalCounts AnimalCounts { get; set; }
            public bool Enabled { get; set; }
        }

        class Satellite
        {
            public AnimalCounts AnimalCounts { get; set; }
            public bool Enabled { get; set; }
        }

        class SphereTank
        {
            public AnimalCounts AnimalCounts { get; set; }
            public bool Enabled { get; set; }
        }

        class Radtowns
        {
            public AnimalCounts AnimalCounts { get; set; }
            public bool Enabled { get; set; }
        }
        class MilitaryTunnels
        {
            public AnimalCounts AnimalCounts { get; set; }
            public bool Enabled { get; set; }
        }
        class Options
        {
            public int RespawnTimer;
            public float SpawnSpread;
            public int TotalMaximumAmount;           
        }
        #endregion

        private ConfigData configData;
        class ConfigData
        {
            public LightHouses Lighthouses { get; set; }
            public Airfield Airfield { get; set; }
            public Powerplant Powerplant { get; set; }
            public Trainyard Trainyard { get; set; }
            public WaterTreatmentPlant WaterTreatmentPlant { get; set; }
            public Warehouses Warehouses { get; set; }
            public Satellite Satellite { get; set; }
            public SphereTank SphereTank { get; set; }
            public Radtowns Radtowns { get; set; }
            public MilitaryTunnels MilitaryTunnels { get; set; }
            public Options a_Options { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Airfield = new Airfield
                {
                    AnimalCounts = new AnimalCounts
                    {
                        Bears = 0,
                        Boars = 0,
                        Chickens = 0,
                        Horses = 0,
                        Stags = 0,
                        Wolfs = 0,
                        Zombies = 0
                    },
                    Enabled = false
                },
                Lighthouses = new LightHouses
                {
                    AnimalCounts = new AnimalCounts
                    {
                        Bears = 0,
                        Boars = 0,
                        Chickens = 0,
                        Horses = 0,
                        Stags = 0,
                        Wolfs = 0,
                        Zombies = 0
                    },
                    Enabled = false
                },
                MilitaryTunnels = new MilitaryTunnels
                {
                    AnimalCounts = new AnimalCounts
                    {
                        Bears = 0,
                        Boars = 0,
                        Chickens = 0,
                        Horses = 0,
                        Stags = 0,
                        Wolfs = 0,
                        Zombies = 0
                    },
                    Enabled = false
                },
                Powerplant = new Powerplant
                {
                    AnimalCounts = new AnimalCounts
                    {
                        Bears = 0,
                        Boars = 0,
                        Chickens = 0,
                        Horses = 0,
                        Stags = 0,
                        Wolfs = 0,
                        Zombies = 0
                    },
                    Enabled = false
                },
                Radtowns = new Radtowns
                {
                    AnimalCounts = new AnimalCounts
                    {
                        Bears = 0,
                        Boars = 0,
                        Chickens = 0,
                        Horses = 0,
                        Stags = 0,
                        Wolfs = 0,
                        Zombies = 0
                    },
                    Enabled = false
                },
                Satellite = new Satellite
                {
                    AnimalCounts = new AnimalCounts
                    {
                        Bears = 0,
                        Boars = 0,
                        Chickens = 0,
                        Horses = 0,
                        Stags = 0,
                        Wolfs = 0,
                        Zombies = 0
                    },
                    Enabled = false
                },
                SphereTank = new SphereTank
                {
                    AnimalCounts = new AnimalCounts
                    {
                        Bears = 0,
                        Boars = 0,
                        Chickens = 0,
                        Horses = 0,
                        Stags = 0,
                        Wolfs = 0,
                        Zombies = 0
                    },
                    Enabled = false
                },
                Trainyard = new Trainyard
                {
                    AnimalCounts = new AnimalCounts
                    {
                        Bears = 0,
                        Boars = 0,
                        Chickens = 0,
                        Horses = 0,
                        Stags = 0,
                        Wolfs = 0,
                        Zombies = 0
                    },
                    Enabled = false
                },
                Warehouses = new Warehouses
                {
                    AnimalCounts = new AnimalCounts
                    {
                        Bears = 0,
                        Boars = 0,
                        Chickens = 0,
                        Horses = 0,
                        Stags = 0,
                        Wolfs = 0,
                        Zombies = 0
                    },
                    Enabled = false
                },
                WaterTreatmentPlant = new WaterTreatmentPlant
                {
                    AnimalCounts = new AnimalCounts
                    {
                        Bears = 0,
                        Boars = 0,
                        Chickens = 0,
                        Horses = 0,
                        Stags = 0,
                        Wolfs = 0,
                        Zombies = 0
                    },
                    Enabled = false
                },
                a_Options = new Options
                {
                    TotalMaximumAmount = 40,
                    RespawnTimer = 15,
                    SpawnSpread = 100
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion      

        #region Messaging
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"nullList", "<color=#939393>Error getting a list of monuments</color>" },
            {"title", "<color=orange>Radtown Animals:</color> " },
            {"killedAll", "<color=#939393>Killed all animals</color>" },
            {"spawnLimit", "<color=#939393>The animal spawn limit has been hit.</color>" }
        };
        #endregion
    }
}
