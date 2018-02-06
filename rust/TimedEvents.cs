using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TimedEvents", "Akeno Himejima", "1.0.3", ResourceId = 0)]
    class TimedEvents : RustPlugin
    {
        #region Fields       
        private Timer xMas;
        private Timer helicopter;
        private Timer airDrop;
        #endregion

        #region Oxide Hooks
        void OnServerInitialized()
        {
            LoadVariables();
            InitializeTimers();
        }
        void Unload()
        {
            if (xMas != null)
                xMas.Destroy();
            if (helicopter != null)
                helicopter.Destroy();
            if (airDrop != null)
                airDrop.Destroy();
        }
        #endregion

        #region Functions
        void InitializeTimers()
        {
            InitHelicopter();
            InitAirdrop();
            InitXMas();
        }
        void InitHelicopter()
        {
            if (configData.TimeBetween_Helicopters <= 0) return;
            helicopter = timer.Every(configData.TimeBetween_Helicopters * 60, () =>
            {
                var heli = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", new Vector3(), new Quaternion(), true) as BaseHelicopter;
                if (heli != null)
                {
                heli.Spawn();
                }
            });
        }
        void InitXMas()
        {
            if (configData.TimeBetween_XMas <= 0) return;
            xMas = timer.Every(configData.TimeBetween_XMas * 60, () =>
            {
                rust.RunServerCommand("xmas.refill");
            });
        }
        void InitAirdrop()
        {
            if (configData.TimeBetween_Airdrops <= 0) return;
            airDrop = timer.Every(configData.TimeBetween_Airdrops * 60, () =>
            {
                var plane = (CargoPlane)GameManager.server.CreateEntity("assets/prefabs/npc/cargo plane/cargo_plane.prefab", new Vector3(), new Quaternion(), true);
                if (plane != null)
                {
                    plane.Spawn();
                }
            });
        }
        #endregion        

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public int TimeBetween_Airdrops { get; set; }
            public int TimeBetween_Helicopters { get; set; }
            public int TimeBetween_XMas { get; set; }
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
                TimeBetween_Airdrops = 0,
                TimeBetween_Helicopters = 0,
                TimeBetween_XMas = 0
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion       
    }
}
