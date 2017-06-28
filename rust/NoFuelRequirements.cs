using System.Collections.Generic;
using Oxide.Core;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("NoFuelRequirements", "k1lly0u", "1.3.5", ResourceId = 1179)]
    class NoFuelRequirements : RustPlugin
    {
        #region Fields        
        bool usingPermissions;
        bool initialized;

        string[] ValidFuelTypes = new string[] { "wood", "lowgradefuel" };
        #endregion

        #region Oxide Hooks        
        void OnServerInitialized()
        {
            LoadVariables();
            RegisterPermissions();
            initialized = true;
        }
        void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (!initialized || oven == null || fuel == null) return;
            ConsumeTypes type = StringToType(oven?.ShortPrefabName ?? string.Empty);
            if (type == ConsumeTypes.None) return;

            if (IsActiveType(type))
            {
                if (usingPermissions && oven.OwnerID != 0U)
                {
                    if (!HasPermission(oven.OwnerID.ToString(), type)) return;
                }
                fuel.amount += 1;
            }
        }
        
        void OnItemUse(Item item, int amount)
        {
            if (!initialized || item == null || amount == 0 || !ValidFuelTypes.Contains(item.info.shortname)) return;
           
            string shortname = item?.parent?.parent?.info?.shortname ?? item?.GetRootContainer()?.entityOwner?.ShortPrefabName;
            if (string.IsNullOrEmpty(shortname)) return;

            ConsumeTypes type = StringToType(shortname);
            if (type == ConsumeTypes.None) return;

            if (IsActiveType(type))
            {
                if (usingPermissions)
                {
                    string playerId = item?.GetRootContainer()?.playerOwner?.UserIDString;
                    string entityId = item?.GetRootContainer()?.entityOwner?.OwnerID.ToString();

                    if (!string.IsNullOrEmpty(playerId))
                        if (!HasPermission(playerId, type)) return;
                    if (!string.IsNullOrEmpty(entityId) && entityId != "0")
                        if (!HasPermission(entityId, type)) return;
                }                
                item.amount += amount;
            }
        }
        #endregion

        #region Functions
        void RegisterPermissions()
        {
            if (configData.UsePermissions)
            {
                usingPermissions = true;
                foreach (var perm in configData.Permissions)
                {
                    permission.RegisterPermission(perm.Value, this);
                }
            }
        }
        bool HasPermission(string ownerId, ConsumeTypes type) => permission.UserHasPermission(ownerId, configData.Permissions[type]);
        bool IsActiveType(ConsumeTypes type) => configData.AffectedTypes[type];
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
                case "refinery_small_deployed":
                    return ConsumeTypes.OilRefinery;
                case "ceilinglight.deployed":
                    return ConsumeTypes.CeilingLight;
                case "lantern.deployed":
                    return ConsumeTypes.Lanterns;
                case "hat.miner":
                    return ConsumeTypes.MinersHat;
                case "hat.candle":
                    return ConsumeTypes.CandleHat;
                case "fuelstorage":
                    return ConsumeTypes.Quarry;
                case "tunalight.deployed":
                    return ConsumeTypes.TunaLight;
                case "searchlight.deployed":
                    return ConsumeTypes.Searchlight;
                default:
                    return ConsumeTypes.None;
            }
        }
        #endregion

        #region Config  
        enum ConsumeTypes
        {
            Campfires, CandleHat, CeilingLight, Furnace, Lanterns, LargeFurnace, MinersHat, OilRefinery, Quarry, TunaLight, Searchlight, None
        }
        private ConfigData configData;
        class ConfigData
        {            
            public Dictionary<ConsumeTypes, bool> AffectedTypes { get; set; }
            public Dictionary<ConsumeTypes, string> Permissions { get; set; }
            public bool UsePermissions { get; set; }
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
                AffectedTypes = new Dictionary<ConsumeTypes, bool>
                {
                    {ConsumeTypes.Campfires, true },
                    {ConsumeTypes.CandleHat, true },
                    {ConsumeTypes.CeilingLight, true },
                    {ConsumeTypes.Furnace, true },
                    {ConsumeTypes.Lanterns, true },
                    {ConsumeTypes.LargeFurnace, true },
                    {ConsumeTypes.MinersHat, true },
                    {ConsumeTypes.OilRefinery, true },
                    {ConsumeTypes.Quarry, true },
                    {ConsumeTypes.TunaLight, true },
                    {ConsumeTypes.Searchlight, true }
                },
                Permissions = new Dictionary<ConsumeTypes, string>
                {
                    {ConsumeTypes.Campfires, "nofuelrequirements.campfire" },
                    {ConsumeTypes.CandleHat, "nofuelrequirements.candlehat" },
                    {ConsumeTypes.CeilingLight, "nofuelrequirements.ceilinglight" },
                    {ConsumeTypes.Furnace, "nofuelrequirements.furnace" },
                    {ConsumeTypes.Lanterns, "nofuelrequirements.lantern" },
                    {ConsumeTypes.LargeFurnace, "nofuelrequirements.largefurnace" },
                    {ConsumeTypes.MinersHat, "nofuelrequirements.minershat" },
                    {ConsumeTypes.OilRefinery, "nofuelrequirements.oilrefinery" },
                    {ConsumeTypes.Quarry, "nofuelrequirements.quarry" },
                    {ConsumeTypes.TunaLight, "nofuelrequirements.tunalight" },
                    {ConsumeTypes.Searchlight, "nofuelrequirements.searchlight" }
                },
                UsePermissions = false
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}
