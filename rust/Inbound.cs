using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Inbound", "Wulf/lukespragg", "0.3.4", ResourceId = 1457)]
    [Description("Notifies all players when a helicopter or supply drop is inbound")]

    class Inbound : RustPlugin
    {
        #region Initialization

        readonly Dictionary<BaseEntity, Timer> timers = new Dictionary<BaseEntity, Timer>();

        bool arrowOnLanding;
        bool arrowUntilEmpty;
        bool cargoPlaneAlerts;
        bool helicopterAlerts;
        bool showCoordinates;
        bool supplyDropAlerts;

        //string arrowColor;
        int arrowLength;
        int arrowSize;
        int arrowTime;

        protected override void LoadDefaultConfig()
        {
            // Options
            Config["Cargo Plane Alerts (true/false)"] = cargoPlaneAlerts = GetConfig("Cargo Plane Alerts (true/false)", true);
            Config["Helicopter Alerts (true/false)"] = helicopterAlerts = GetConfig("Helicopter Alerts (true/false)", true);
            Config["Show Coordinates (true/false)"] = showCoordinates = GetConfig("Show Coordinates (true/false)", true);
            Config["Supply Drop Alerts (true/false)"] = supplyDropAlerts = GetConfig("Supply Drop Alerts (true/false)", true);
            Config["Supply Drop Arrow On Landing (true/false)"] = arrowOnLanding = GetConfig("Supply Drop Arrow On Landing (true/false)", true);
            Config["Supply Drop Arrow Until Empty (true/false)"] = arrowUntilEmpty = GetConfig("Supply Drop Arrow Until Empty (true/false)", true);

            // Settings
            //Config["Arrow Color (Default #ffffff)"] = arrowColor = GetConfig("Arrow Color (Default #ffffff)", "#ffffff");
            Config["Arrow Length (Default 15)"] = arrowLength = GetConfig("Arrow Length (Default 15)", 15);
            Config["Arrow Size (Default 4)"] = arrowSize = GetConfig("Arrow Size (Default 4)", 4);
            Config["Arrow Time (Seconds, Default 60))"] = arrowTime = GetConfig("Arrow Time (Seconds, Default 60)", 60);

            // Cleanup
            Config.Remove("Show Coordonates (true/false)");
            Config.Remove("HelicopterAlerts");
            Config.Remove("SupplyDropAlerts");
            Config.Remove("SupplyDropArrow");
            Config.Remove("SupplyDropArrowLength");
            Config.Remove("SupplyDropArrowSize");
            Config.Remove("SupplyDropArrowTime");

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CargoPlaneInbound"] = "Cargo plane inbound at {0}!",
                ["HelicopterInbound"] = "Helicopter inbound at {0}!",
                ["SupplyDropInbound"] = "Supply drop inbound at {0}!"
            }, this);
        }

        #endregion

        #region Entity Checks

        void OnEntitySpawned(BaseEntity entity)
        {
            if (!(entity is CargoPlane) && !(entity is BaseHelicopter) && !(entity is SupplyDrop)) return;

            var pos = showCoordinates ? $"{entity.transform.position.x}, {entity.transform.position.y}, {entity.transform.position.z}" : string.Empty;
            if (cargoPlaneAlerts && entity is CargoPlane) Broadcast("CargoPlaneInbound", pos);
            if (helicopterAlerts && entity is BaseHelicopter) Broadcast("HelicopterInbound", pos);
            if (supplyDropAlerts && entity is SupplyDrop) { Broadcast("SupplyDropInbound", pos); LandingCheck(entity); }
        }

        void LandingCheck(BaseEntity entity)
        {
            if (entity == null) return;

            var distance = Vector3.Distance(entity.transform.position, GroundPosition(entity.transform.position));
            if (distance >= 10f) { timer.Once(1f, () => LandingCheck(entity)); return; }

            DrawArrow(entity);
        }

        void DrawArrow(BaseEntity entity)
        {
            //var color = HexToColor(arrowColor); // TODO: Fix this causing NRE, add default color too
            var pos = entity.transform.position;
            var startPos = new Vector3(pos.x, pos.y + 5 + arrowLength, pos.z);
            var endPos = new Vector3(pos.x, pos.y + 5, pos.z);
            var args = new object[] { arrowUntilEmpty ? 0.1f : arrowTime, Color.white, startPos, endPos, arrowSize };

            if (arrowOnLanding && !arrowUntilEmpty) ConsoleNetwork.BroadcastToAllClients("ddraw.arrow", args);
            else if (arrowUntilEmpty) timers[entity] = timer.Every(0.1f, () =>
            {
                if (entity.IsDestroyed) { timers[entity]?.Destroy(); return; }
                ConsoleNetwork.BroadcastToAllClients("ddraw.arrow", args);
            });
        }

        void OnPlayerLootEnd(PlayerLoot loot)
        {
            var entity = loot.entitySource as SupplyDrop;
            if (entity?.inventory?.itemList?.Count == 0 && timers.ContainsKey(loot.entitySource)) timers[loot.entitySource]?.Destroy();
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        void Broadcast(string key, params object[] args)
        {
            foreach (var player in BasePlayer.activePlayerList) PrintToChat(player, Lang(key, player.UserIDString, args));
        }

        /*Color HexToColor(string hex)
        {
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color32(r, g, b, 255);
        }*/

        static Vector3 GroundPosition(Vector3 sourcePos)
        {
            RaycastHit hitInfo;
            var groundLayer = LayerMask.GetMask("Terrain", "World", "Construction");
            if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo, groundLayer)) sourcePos.y = hitInfo.point.y;
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        }

        #endregion
    }
}
