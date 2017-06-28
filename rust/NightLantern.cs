using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("NightLantern", "k1lly0u", "2.0.8", ResourceId = 1182)]
    class NightLantern : RustPlugin
    {
        #region Fields
        private Timer timeCheck;
        private List<BaseOven> lights = new List<BaseOven>();
        List<SearchLight> searchLights = new List<SearchLight>();
        private bool isActivated;
        private bool isEnabled;
        private bool lightsOn;

        private bool nfrInstalled;
        private static FieldInfo secondsRemaining = typeof(SearchLight).GetField("secondsRemaining", (BindingFlags.Instance | BindingFlags.NonPublic));

        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            permission.RegisterPermission("nightlantern.use", this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"You have disabled auto lights","You have disabled auto lights" },
                {"You have enabled auto lights","You have enabled auto lights" }
            }, this);
        }
        void OnServerInitialized()
        {
            LoadVariables();
            if (plugins.Exists("NoFuelRequirements"))
            {
                nfrInstalled = true;
                configData.ConsumeFuel = false;
            }
            isActivated = true;
            isEnabled = true;
            lightsOn = false;
            FindLights();            
        }
        void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (!isActivated || nfrInstalled || configData.ConsumeFuel || oven == null || fuel == null || !lights.Contains(oven)) return;

            ConsumeTypes type = StringToType(oven?.ShortPrefabName ?? string.Empty);
            if (type == ConsumeTypes.None) return;

            if (configData.LightTypes[type])
                fuel.amount++;
        }
        void OnEntitySpawned(BaseEntity entity)
        {
            if (isActivated)
            {
                if (entity == null) return;
                if (entity is BaseOven)
                    CheckType(entity as BaseOven);
                if (entity is SearchLight)
                    AddSearchlight(entity as SearchLight);
            }
        }
        void OnEntityDeath(BaseEntity entity, HitInfo hitInfo)
        {
            if (!isActivated) return;
            if (entity == null) return;
            if (entity is BaseOven && lights.Contains(entity as BaseOven))
                lights.Remove(entity as BaseOven);
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            if (!isActivated) return;
            if (entity == null || !(entity is BaseOven)) return;
            if (entity is BaseOven && lights.Contains(entity as BaseOven))
                lights.Remove(entity as BaseOven);
        }
        void Unload()
        {
            if (timeCheck != null)
                timeCheck.Destroy();
        }
        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Title == "NoFuelRequirements")
            {
                nfrInstalled = true;
                configData.ConsumeFuel = false;
            }
        }
        #endregion

        #region Functions
        void FindLights()
        {
            var ovens = BaseEntity.serverEntities.Where(x => x is BaseOven).ToList();
            foreach (var oven in ovens)            
                CheckType(oven as BaseOven);
            if (configData.LightTypes[ConsumeTypes.Searchlights])
                searchLights.AddRange(BaseEntity.serverEntities.Where(x => x is SearchLight).ToList().Cast<SearchLight>());           
            TimeLoop();
        }
        void CheckType(BaseOven oven)
        {
            if (oven == null) return;
            ConsumeTypes type = StringToType(oven?.ShortPrefabName ?? string.Empty);
            if (type == ConsumeTypes.None) return;
            if(configData.LightTypes[type])            
                lights.Add(oven);
        }
        void AddSearchlight(SearchLight light)
        {
            if (!configData.LightTypes[ConsumeTypes.Searchlights]) return;
            if (light != null)
                searchLights.Add(light);
        }
        void TimeLoop()
        {
            timeCheck = timer.Once(20, () =>
            {
                if (isEnabled)
                    CheckTime();
                TimeLoop();
            });
        }
        void CheckTime()
        {
            var time = TOD_Sky.Instance.Cycle.Hour;
            if (time >= configData.SunsetHour)
            {
                if (!lightsOn)
                {
                    ToggleLanterns(true);
                    ToggleSearchlights(true);
                }

            }
            else if (time >= configData.SunriseHour && time < configData.SunsetHour)
            {
                if (lightsOn)
                {
                    ToggleLanterns(false);
                    ToggleSearchlights(false);
                }
            }
        }        
        void ToggleLanterns(bool status)
        {
            lightsOn = status;
            for (int i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                if (light == null || light.IsDestroyed) continue;

                if (configData.ConsumeFuel)
                {
                    if (status)
                        light.StartCooking();
                    else light.StopCooking();
                }
                else
                {
                    if (light.IsOn() == status) continue;
                    light.SetFlag(BaseEntity.Flags.On, status);
                }                
            }
        }
        void ToggleSearchlights(bool status)
        {
            for (int i = 0; i < searchLights.Count; i++)
            {
                var light = searchLights[i];
                if (light == null || light.IsDestroyed) continue;
                if (configData.ConsumeFuel)
                {
                    if (status)
                    {
                        if (!light.HasFlag(BaseEntity.Flags.On))
                            light.SetFlag(BaseEntity.Flags.On, true);
                    }
                    else
                    {
                        if(light.HasFlag(BaseEntity.Flags.On))
                            light.SetFlag(BaseEntity.Flags.On, false);
                    }
                }
                else
                {
                    if (status)
                    {
                        if (!light.HasFlag(BaseEntity.Flags.On))
                        {
                            light.SetFlag(BaseEntity.Flags.On, true);
                            secondsRemaining.SetValue(light, 900);
                        }
                    }
                    else
                    {
                        if (light.HasFlag(BaseEntity.Flags.On))
                        {
                            light.SetFlag(BaseEntity.Flags.On, false);
                            secondsRemaining.SetValue(light, 0);
                        }
                    }
                }
            }
        }
        ConsumeTypes StringToType(string name)
        {
            switch (name)
            {
                case "campfire":
                    return ConsumeTypes.Campfires;
                case "furnace":
                    return ConsumeTypes.Furnace;
                case "furnace.large":
                    return ConsumeTypes.LargeFurnace;               
                case "ceilinglight.deployed":
                    return ConsumeTypes.CeilingLight;
                case "lantern.deployed":
                    return ConsumeTypes.Lanterns;
                case "jackolantern.angry":
                    return ConsumeTypes.JackOLantern;
                case "jackolantern.happy":
                    return ConsumeTypes.JackOLantern;
                case "tunalight.deployed":
                    return ConsumeTypes.TunaLight;
                case "searchlight.deployed":
                    return ConsumeTypes.Searchlights;
                default:
                    return ConsumeTypes.None;
            }
        }
        #endregion

        #region Commands
        [ChatCommand("lantern")]
        void cmdLantern(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "nightlantern.use")) return;
            if (isEnabled)
            {
                isEnabled = false;
                if (lightsOn)
                {
                    ToggleLanterns(false);
                    ToggleSearchlights(false);
                }
                SendReply(player, lang.GetMessage("You have disabled auto lights", this, player.UserIDString));
                return;
            }
            else
            {
                isEnabled = true;
                if (!lightsOn)
                     CheckTime();
                SendReply(player, lang.GetMessage("You have enabled auto lights", this, player.UserIDString));
            }
        }
        #endregion

        #region Config   
        enum ConsumeTypes
        {
            Campfires, CeilingLight, Furnace, LargeFurnace, Lanterns, JackOLantern, TunaLight, Searchlights, None
        }
        private ConfigData configData;        
        class ConfigData
        {
            public bool ConsumeFuel { get; set; } 
            public Dictionary<ConsumeTypes, bool> LightTypes { get; set; }           
            public float SunriseHour { get; set; }
            public float SunsetHour { get; set; }            
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
                ConsumeFuel = true,
                LightTypes = new Dictionary<ConsumeTypes, bool>
                {
                    {ConsumeTypes.Campfires, true },
                    {ConsumeTypes.CeilingLight, true },
                    {ConsumeTypes.Furnace, true },
                    {ConsumeTypes.LargeFurnace, true },
                    {ConsumeTypes.JackOLantern, true },
                    {ConsumeTypes.Lanterns, true },
                    {ConsumeTypes.TunaLight, true },
                    {ConsumeTypes.Searchlights, true }
                },
                SunriseHour = 7.5f,
                SunsetHour = 18.5f
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}
