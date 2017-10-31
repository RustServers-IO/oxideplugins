using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("MonumentRadiation", "k1lly0u", "0.2.5", ResourceId = 1562)]
    class MonumentRadiation : RustPlugin
    {
        static MonumentRadiation ins;
        private bool radsOn;
        private int offTimer;
        private int onTimer;

        private List<RZ> radiationZones = new List<RZ>();
        private ConfigData configData;

        #region Oxide Hooks       
        void Loaded()
        {
            lang.RegisterMessages(Messages, this);            
            LoadVariables();
        }        
        void OnServerInitialized()
        {
            ins = this;
            if (!ConVar.Server.radiation)
            {
                radsOn = false;
                ConVar.Server.radiation = true;
            }
            else radsOn = true;
            DestroyAllComponents();
            FindMonuments();           
        }
        void Unload()
        {
            for (int i = 0; i < radiationZones.Count; i++)            
                UnityEngine.Object.Destroy(radiationZones[i]);            
            radiationZones.Clear();
            DestroyAllComponents(); 
            if (!radsOn) ConVar.Server.radiation = false;
        }
        #endregion
      
        #region Functions
        private void DestroyAllComponents()
        {
            var components = UnityEngine.Object.FindObjectsOfType<RZ>();
            if (components != null)
                foreach (var comp in components)
                    UnityEngine.Object.Destroy(comp);
        }
        private void FindMonuments()
        {
            if (configData.Settings.IsHapis) { CreateHapis(); return; }

            var allobjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (var gobject in allobjects)
            {
                if (gobject.name.Contains("autospawn/monument"))
                {
                    var pos = gobject.transform.position;

                    if (gobject.name.Contains("lighthouse"))
                    {
                        if (configData.Zones.Lighthouse.Activate)
                            CreateZone(configData.Zones.Lighthouse, pos);
                        continue;
                    }

                    if (gobject.name.Contains("powerplant_1"))
                    {
                        if (configData.Zones.Powerplant.Activate)
                            CreateZone(configData.Zones.Powerplant, pos);
                        continue;
                    }

                    if (gobject.name.Contains("military_tunnel_1"))
                    {
                        if (configData.Zones.Tunnels.Activate)
                            CreateZone(configData.Zones.Tunnels, pos);
                        continue;
                    }

                    if (gobject.name.Contains("harbor_1"))
                    {
                        if (configData.Zones.LargeHarbor.Activate)
                            CreateZone(configData.Zones.LargeHarbor, pos);
                        continue;
                    }

                    if (gobject.name.Contains("harbor_2"))
                    {
                        if (configData.Zones.SmallHarbor.Activate)
                            CreateZone(configData.Zones.SmallHarbor, pos);
                        continue;
                    }

                    if (gobject.name.Contains("airfield_1"))
                    {
                        if (configData.Zones.Airfield.Activate)
                            CreateZone(configData.Zones.Airfield, pos);
                        continue;
                    }

                    if (gobject.name.Contains("trainyard_1"))
                    {
                        if (configData.Zones.Trainyard.Activate)
                            CreateZone(configData.Zones.Trainyard, pos);
                        continue;
                    }

                    if (gobject.name.Contains("water_treatment_plant_1"))
                    {
                        if (configData.Zones.WaterTreatment.Activate)
                            CreateZone(configData.Zones.WaterTreatment, pos);
                        continue;
                    }

                    if (gobject.name.Contains("warehouse"))
                    {
                        if (configData.Zones.Warehouse.Activate)
                            CreateZone(configData.Zones.Warehouse, pos);
                        continue;
                    }

                    if (gobject.name.Contains("satellite_dish"))
                    {

                        if (configData.Zones.Satellite.Activate)
                            CreateZone(configData.Zones.Satellite, pos);
                        continue;
                    }

                    if (gobject.name.Contains("sphere_tank"))
                    {
                        if (configData.Zones.Dome.Activate)
                            CreateZone(configData.Zones.Dome, pos);
                        continue;
                    }

                    if (gobject.name.Contains("radtown_small_3"))
                    {
                        if (configData.Zones.Radtown.Activate)
                            CreateZone(configData.Zones.Radtown, pos);
                        continue;
                    }
                    if (gobject.name.Contains("launch_site_1"))
                    {
                        if (configData.Zones.RocketFactory.Activate)
                        {
                            CreateZone(configData.Zones.RocketFactory, pos + -(gobject.transform.right * 80));
                            CreateZone(configData.Zones.RocketFactory, pos + gobject.transform.right * 150);
                        }
                        continue;
                    }
                    if (gobject.name.Contains("gas_station_1"))
                    {
                        if (configData.Zones.GasStation.Activate)
                            CreateZone(configData.Zones.GasStation, pos);
                        continue;
                    }
                    if (gobject.name.Contains("supermarket_1"))
                    {
                        if (configData.Zones.Supermarket.Activate)
                            CreateZone(configData.Zones.Supermarket, pos);
                        continue;
                    }
                }                
            }
            ConfirmCreation();
        }
        private void CreateHapis()
        {
            if (configData.Zones.Lighthouse.Activate)
            {
                CreateZone(new ConfigData.RadZones.MonumentSettings() { Name = "Lighthouse", Radiation = configData.Zones.Lighthouse.Radiation, Radius = HIMon["lighthouse_1"].Radius }, HIMon["lighthouse_1"].Position);
                CreateZone(new ConfigData.RadZones.MonumentSettings() { Name = "Lighthouse", Radiation = configData.Zones.Lighthouse.Radiation, Radius = HIMon["lighthouse_2"].Radius }, HIMon["lighthouse_2"].Position);
            }
            if (configData.Zones.WaterTreatment.Activate) CreateZone(new ConfigData.RadZones.MonumentSettings() { Name = "WaterTreatment", Radiation = configData.Zones.WaterTreatment.Radiation, Radius = HIMon["water"].Radius }, HIMon["water"].Position);
            if (configData.Zones.Tunnels.Activate) CreateZone(new ConfigData.RadZones.MonumentSettings() { Name = "Tunnels", Radiation = configData.Zones.Tunnels.Radiation, Radius = HIMon["tunnels"].Radius }, HIMon["tunnels"].Position);
            if (configData.Zones.Satellite.Activate) CreateZone(new ConfigData.RadZones.MonumentSettings() { Name = "Satellite", Radiation = configData.Zones.Satellite.Radiation, Radius = HIMon["satellite"].Radius }, HIMon["satellite"].Position);
            ConfirmCreation();
        }
        private void ConfirmCreation()
        {
            if (radiationZones.Count > 0)
            {
                if (configData.Settings.UseTimers) StartRadTimers();
                Puts("Created " + radiationZones.Count + " monument radiation zones");
                if (!ConVar.Server.radiation)
                {
                    radsOn = false;
                    ConVar.Server.radiation = true;
                }
            }
        }
        private void CreateZone(ConfigData.RadZones.MonumentSettings zone, Vector3 pos)
        {           
            var newZone = new GameObject().AddComponent<RZ>();
            newZone.Activate($"{zone.Name}_{GetRandom()}", pos, zone.Radius, zone.Radiation);
            radiationZones.Add(newZone);
        }                       
        private void StartRadTimers()
        {
            int ontime = configData.Timers.StaticOn;
            int offtime = configData.Timers.StaticOff;
            if (configData.Settings.UseRandomTimers)
            {
                ontime = GetRandom(configData.Timers.ROnMin, configData.Timers.ROnmax);
                offtime = GetRandom(configData.Timers.ROffMin, configData.Timers.ROffMax);
            }
            onTimer = ontime * 60;
            timer.Repeat(1, onTimer, () =>
            {
                onTimer--;
                if (onTimer == 0)
                {                    
                    foreach (var zone in radiationZones)
                        zone.Deactivate();

                    if (configData.Settings.Infopanel)
                        ConVar.Server.radiation = false;

                    if (configData.Settings.ShowTimers)                    
                        MessageAllPlayers(lang.GetMessage("RadsOffMsg", this), offtime);
                    
                    offTimer = offtime * 60;
                    timer.Repeat(1, offTimer, () =>
                    {
                        offTimer--;
                        if (offTimer == 0)
                        {
                            foreach (var zone in radiationZones)
                                zone.Reactivate();

                            if (configData.Settings.Infopanel)
                                ConVar.Server.radiation = true;

                            if (configData.Settings.ShowTimers)                            
                                MessageAllPlayers(lang.GetMessage("RadsOnMsg", this), ontime);
                            
                            StartRadTimers();
                        }
                    });
                }
            });
        }
        private int GetRandom() => UnityEngine.Random.Range(1, 1000);
        private int GetRandom(int min, int max) => UnityEngine.Random.Range(min, max);        
        private void MessageAllPlayers(string msg, int time) => PrintToChat(string.Format(msg, time));           
        
        void EnterRadiation(BasePlayer player)
        {
            if (configData.Messages.Enter)
            {
                if (ConVar.Server.radiation == false) return;                              
                SendReply(player, lang.GetMessage("enterMessage", this, player.UserIDString));
            }
        }
        void LeaveRadiation(BasePlayer player)
        {
            if (configData.Messages.Exit)
            {
                if (ConVar.Server.radiation == false) return;
                SendReply(player, lang.GetMessage("leaveMessage", this, player.UserIDString));
            }
        }
        #endregion

        #region Commands   
        bool isAdmin(BasePlayer player)
        {
            if (player.net.connection != null)            
                if (player.net.connection.authLevel <= 1)
                {
                    SendReply(player, lang.GetMessage("title", this) + lang.GetMessage("noPerms", this, player.UserIDString));
                    return false;
                }
            return true;
        }
        bool isAuth(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)            
                if (arg.Connection.authLevel < 1)
                {
                    SendReply(arg, lang.GetMessage("noPerms", this));
                    return false;
                }
            return true;
        }
                

        [ConsoleCommand("mr_list")]
        void ccmdRadZoneList(ConsoleSystem.Arg arg)
        {
            if (!isAuth(arg)) return;
            Puts(lang.GetMessage("monList", this));
            if (radiationZones.Count == 0) Puts("none");
            foreach (var zone in radiationZones)
                Puts(zone.name + " ------ " + zone.position);
        }

        [ChatCommand("mr_list")]
        void chatRadZoneList(BasePlayer player, string command, string[] args)
        {
            if (!isAdmin(player)) return;
            Puts(lang.GetMessage("title", this) + lang.GetMessage("monList", this));
            if (radiationZones.Count == 0) Puts("none");
            foreach (var zone in radiationZones)
                Puts(zone.name + "====" + zone.position);
            SendReply(player, lang.GetMessage("title", this, player.UserIDString) + lang.GetMessage("checkConsole", this, player.UserIDString));
        }

        [ChatCommand("mr")]
        void chatCheckTimers(BasePlayer player, string command, string[] args)
        {
            if (onTimer != 0)
            {
                float timeOn = onTimer / 60;
                string min = "minutes";
                if (timeOn < 1) { timeOn = onTimer; min = "seconds"; }
                SendReply(player, string.Format(lang.GetMessage("RadsDownMsg", this), timeOn.ToString(), min));
            }
            else if (offTimer != 0)
            {
                int timeOff = offTimer / 60;
                string min = "minutes";
                if (timeOff < 1) { timeOff = offTimer; min = "seconds"; }
                SendReply(player, string.Format(lang.GetMessage("RadsUpMsg", this), timeOff.ToString(), min));
            }
        }
        [ChatCommand("mr_show")]
        void chatShowZones(BasePlayer player, string command, string[] args)
        {
            if (!isAdmin(player)) return;
            foreach(var zone in radiationZones)            
                player.SendConsoleCommand("ddraw.sphere", 20f, Color.blue, zone.transform.position, zone.radius);            
        }
        #endregion

        #region Classes        
        public class RZ : MonoBehaviour
        {
            private TriggerRadiation rads;
            public Vector3 position;
            public float radius;
            private float amount;

            private void Awake()
            {
                gameObject.layer = (int)Rust.Layer.Reserved1;
                enabled = false;
            }
            private void OnDestroy() => Destroy(gameObject);            
            private void OnTriggerEnter(Collider obj)
            {
                if (obj?.gameObject?.layer != (int)Rust.Layer.Player_Server) return;
                var player = obj?.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    ins.EnterRadiation(player);
                }
            }
            private void OnTriggerExit(Collider obj)
            {
                if (obj?.gameObject?.layer != (int)Rust.Layer.Player_Server) return;
                var player = obj?.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    ins.LeaveRadiation(player);
                }
            }
            public void Activate(string type, Vector3 position, float radius, float amount)
            {
                this.position = position;
                this.radius = radius;
                this.amount = amount;

                gameObject.name = type;
                transform.position = position;
                transform.rotation = new Quaternion();
                UpdateCollider();

                rads = gameObject.AddComponent<TriggerRadiation>();
                rads.RadiationAmountOverride = amount;
                rads.radiationSize = radius;
                rads.interestLayers = LayerMask.GetMask("Player (Server)");
                rads.enabled = true;
            }
            public void Deactivate() => rads.enabled = false;
            public void Reactivate() => rads.enabled = true;
            private void UpdateCollider()
            {
                var sphereCollider = gameObject.GetComponent<SphereCollider>();
                if (sphereCollider == null)
                {
                    sphereCollider = gameObject.AddComponent<SphereCollider>();
                    sphereCollider.isTrigger = true;
                }
                sphereCollider.radius = radius;
            }
        }       
       
        class HapisIslandMonuments
        {
            public Vector3 Position;
            public float Radius;
        }
        Dictionary<string, HapisIslandMonuments> HIMon = new Dictionary<string, HapisIslandMonuments>
        {
            {"lighthouse_1", new HapisIslandMonuments {Position = new Vector3(1562.30981f, 45.05141f, 1140.29382f), Radius = 15 } },
            {"lighthouse_2", new HapisIslandMonuments {Position = new Vector3(-1526.65112f, 45.3333473f, -280.0514f), Radius = 15 } },
            {"water", new HapisIslandMonuments {Position = new Vector3(-1065.191f, 125.3655f, 439.2279f), Radius = 100 } },
            {"tunnels", new HapisIslandMonuments {Position = new Vector3(-854.7694f, 72.34925f, -241.692f), Radius = 100 } },
            {"satellite", new HapisIslandMonuments {Position = new Vector3(205.2501f, 247.8247f, 252.5204f), Radius = 80 } }
        };        
        #endregion

        #region Config      
       
        class ConfigData
        {
            [JsonProperty(PropertyName = "Messaging Settings")]
            public Messaging Messages { get; set; }
            public RadiationTimers Timers { get; set; }
            public Options Settings { get; set; }
            [JsonProperty(PropertyName = "Zone Settings")]
            public RadZones Zones { get; set; }

            public class Messaging
            {
                [JsonProperty(PropertyName = "Display message to player when they enter a radiation zone")]
                public bool Enter { get; set; }
                [JsonProperty(PropertyName = "Display message to player when they leave a radiation zone")]
                public bool Exit { get; set; }
            }
            public class RadiationTimers
            {
                [JsonProperty(PropertyName = "Random on time (minimum minutes)")]
                public int ROnMin { get; set; }
                [JsonProperty(PropertyName = "Random on time (maximum minutes)")]
                public int ROnmax { get; set; }
                [JsonProperty(PropertyName = "Random off time (minimum minutes)")]
                public int ROffMin { get; set; }
                [JsonProperty(PropertyName = "Random off time (maximum minutes)")]
                public int ROffMax { get; set; }
                [JsonProperty(PropertyName = "Forced off time (minutes)")]
                public int StaticOff { get; set; }
                [JsonProperty(PropertyName = "Forced on time (minutes)")]
                public int StaticOn { get; set; }
            }
            public class RadZones
            {
                public MonumentSettings Airfield { get; set; }
                public MonumentSettings Dome { get; set; }
                public MonumentSettings Lighthouse { get; set; }
                public MonumentSettings LargeHarbor { get; set; }
                public MonumentSettings GasStation { get; set; }
                public MonumentSettings Powerplant { get; set; }
                public MonumentSettings Radtown { get; set; }
                public MonumentSettings RocketFactory { get; set; }
                public MonumentSettings Satellite { get; set; }
                public MonumentSettings SmallHarbor { get; set; }
                public MonumentSettings Supermarket { get; set; }
                public MonumentSettings Trainyard { get; set; }
                public MonumentSettings Tunnels { get; set; }
                public MonumentSettings Warehouse { get; set; }
                public MonumentSettings WaterTreatment { get; set; }

                public class MonumentSettings
                {
                    [JsonProperty(PropertyName = "Enable radiation at this monument")]
                    public bool Activate;
                    [JsonProperty(PropertyName = "Monument name (internal use)")]
                    public string Name;
                    [JsonProperty(PropertyName = "Radius of radiation")]
                    public float Radius;
                    [JsonProperty(PropertyName = "Radiation amount")]
                    public float Radiation;
                }
            }
            public class Options
            {
                [JsonProperty(PropertyName = "Broadcast radiation status changes")]
                public bool ShowTimers { get; set; }
                [JsonProperty(PropertyName = "Using Hapis Island map")]
                public bool IsHapis { get; set; }
                [JsonProperty(PropertyName = "Enable InfoPanel integration")]
                public bool Infopanel { get; set; }
                [JsonProperty(PropertyName = "Use radiation toggle timers")]
                public bool UseTimers { get; set; }
                [JsonProperty(PropertyName = "Randomise radiation timers")]
                public bool UseRandomTimers { get; set; }
            }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            var config = new ConfigData
            {
               Messages = new ConfigData.Messaging
               {
                   Enter = true,
                   Exit = false
               },
               Timers = new ConfigData.RadiationTimers
               {
                   ROffMax = 60,
                   ROffMin = 25,
                   ROnmax = 30,
                   ROnMin = 5,
                   StaticOff = 15,
                   StaticOn = 45
               },
               Settings = new ConfigData.Options
               {
                   ShowTimers = true,
                   UseRandomTimers = false,
                   UseTimers = true,
                   IsHapis = false,
                   Infopanel = false
               },
               Zones = new ConfigData.RadZones
               {
                   Airfield = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "Airfield",
                       Radiation = 10,
                       Radius = 85
                   },
                   Dome = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "Dome",
                       Radiation = 10,
                       Radius = 50
                   },
                   GasStation = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "GasStation",
                       Radiation = 10,
                       Radius = 15
                   },
                   LargeHarbor = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "Large Harbor",
                       Radiation = 10,
                       Radius = 120
                   },
                   Lighthouse = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "Lighthouse",
                       Radiation = 10,
                       Radius = 15
                   },
                   Powerplant = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "Powerplant",
                       Radiation = 10,
                       Radius = 120
                   },
                   Radtown = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = true,
                       Name = "Radtown",
                       Radiation = 10,
                       Radius = 85
                   },
                   RocketFactory = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = true,
                       Name = "Rocket Factory",
                       Radiation = 10,
                       Radius = 140
                   },
                   Satellite = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "Satellite",
                       Radiation = 10,
                       Radius = 60
                   },
                   SmallHarbor = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = true,
                       Name = "Small Harbor",
                       Radiation = 10,
                       Radius = 85
                   },
                   Supermarket = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "Supermarket",
                       Radiation = 10,
                       Radius = 20
                   },
                   Trainyard = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "Trainyard",
                       Radiation = 10,
                       Radius = 100
                   },
                   Tunnels = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "Tunnels",
                       Radiation = 10,
                       Radius = 90
                   },
                   Warehouse = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "Warehouse",
                       Radiation = 10,
                       Radius = 15
                   },
                   WaterTreatment = new ConfigData.RadZones.MonumentSettings
                   {
                       Activate = false,
                       Name = "WaterTreatment",
                       Radiation = 10,
                       Radius = 120
                   }
               }
        };
            SaveConfig(config);
        }
        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
       
        #endregion

        #region Localization      
        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"nullList", "Error getting a list of monuments" },
            {"noPerms", "You have insufficient permission" },
            {"noZoneM", "ZoneManager is not installed, can not proceed" },
            {"title", "<color=orange>MonumentRadiation</color> : "},
            {"monList", "------ Monument Radiation List ------"},
            {"clearAll", "All monument radiation removed!"},
            {"enterMessage", "<color=#B30000>WARNING: </color><color=#B6B6B6>You are entering a irradiated area! </color>" },
            {"leaveMessage", "<color=#B30000>CAUTION: </color><color=#B6B6B6>You are leaving a irradiated area! </color>" },
            {"RadsOnMsg", "<color=#B6B6B6>Monument radiation levels are back up for </color><color=#00FF00>{0} minutes</color><color=grey>!</color>" },
            {"RadsOffMsg", "<color=#B6B6B6>Monument radiation levels are down for </color><color=#00FF00>{0} minutes</color><color=grey>!</color>"},
            {"RadsUpMsg", "<color=#B6B6B6>Monument radiation levels will be back up in </color><color=#00FF00>{0} {1}</color><color=#B6B6B6>!</color>"},
            {"RadsDownMsg", "<color=#B6B6B6>Monument radiation levels will be down in </color><color=#00FF00>{0} {1}</color><color=#B6B6B6>!</color>"}

        };
        #endregion


    }
}
