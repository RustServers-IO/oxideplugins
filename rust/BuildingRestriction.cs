using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildingRestriction", "Wulf/lukespragg", "1.5.1", ResourceId = 2124)]
    [Description("Restricts building height, building in water, number of foundations, and more")]
    public class BuildingRestriction : RustPlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Restrict build height (true/false)")]
            public bool RestrictBuildHeight;

            [JsonProperty(PropertyName = "Maximum build height")]
            public int MaxBuildHeight;

            [JsonProperty(PropertyName = "Restrict foundations (true/false)")]
            public bool RestrictFoundations;

            [JsonProperty(PropertyName = "Maximum foundations")]
            public int MaxFoundations;

            [JsonProperty(PropertyName = "Maximum triangle foundations")]
            public int MaxTriFoundations;

            [JsonProperty(PropertyName = "Restrict tool cupboards (true/false)")]
            public bool RestrictToolCupboards;

            [JsonProperty(PropertyName = "Maximum tool cupboards")]
            public int MaxToolCupboards;

            [JsonProperty(PropertyName = "Restrict water depth (true/false)")]
            public bool RestrictWaterDepth;

            [JsonProperty(PropertyName = "Maximum water depth")]
            public double MaxWaterDepth;

            [JsonProperty(PropertyName = "Refund resources when restricted")]
            public bool RefundResources;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    RestrictBuildHeight = true,
                    MaxBuildHeight = 5,
                    RestrictFoundations = true,
                    MaxFoundations = 16,
                    MaxTriFoundations = 24,
                    RestrictToolCupboards = true,
                    MaxToolCupboards = 5,
                    RestrictWaterDepth = true,
                    MaxWaterDepth = 0.1,
                    RefundResources = true
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.MaxBuildHeight == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Localization

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["MaxBuildHeight"] = "You have reached the max building height! ({0} building blocks)",
                ["MaxFoundations"] = "You have reached the max foundations allowed! ({0} foundations)",
                ["MaxTriFoundations"] = "You have reached the max triangle foundations allowed! ({0} foundations)",
                ["MaxWaterDepth"] = "You are not allowed to build in water!"
            }, this);
        }

        #endregion

        #region Initialization

        private Dictionary<uint, List<BuildingBlock>> buildingIds = new Dictionary<uint, List<BuildingBlock>>();
        private Dictionary<uint, List<PlayerNameID>> toolCupboards = new Dictionary<uint, List<PlayerNameID>>();
        private List<string> allowedBuildingBlocks = new List<string>
        {
            "assets/prefabs/building core/floor/floor.prefab",
            "assets/prefabs/building core/floor.frame/floor.frame.prefab",
            "assets/prefabs/building core/floor.triangle/floor.triangle.prefab",
            "assets/prefabs/building core/roof/roof.prefab",
            "assets/prefabs/building core/wall.low/wall.low.prefab"
        };

        private const string foundation = "assets/prefabs/building core/foundation/foundation.prefab";
        private const string triFoundation = "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab";
        private const string permBypass = "buildingrestriction.bypass";

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permBypass, this);

            FindStructures();
            FindToolCupboards();
        }

        private void FindStructures()
        {
            Puts("Searching for structures, this may take awhile...");
            var foundationBlocks = Resources.FindObjectsOfTypeAll<BuildingBlock>()
                                            .Where(b => b.PrefabName == foundation || b.PrefabName == triFoundation).ToList();
            foreach (var block in foundationBlocks.Where(b => !buildingIds.ContainsKey(b.buildingID)))
            {
                var structure = UnityEngine.Object.FindObjectsOfType<BuildingBlock>()
                                           .Where(b => b.buildingID == block.buildingID && b.PrefabName == foundation || b.PrefabName == triFoundation);
                buildingIds[block.buildingID] = structure.ToList();
            }
            Puts($"Search complete! Found {buildingIds.Count} structures");
        }

        private void FindToolCupboards()
        {
            Puts("Searching for tool cupboards, this may take awhile...");
            var cupboards = UnityEngine.Object.FindObjectsOfType<BuildingPrivlidge>();
            foreach (var cupboard in cupboards.Where(c => !toolCupboards.ContainsKey(c.net.ID)))
                toolCupboards.Add(cupboard.net.ID, cupboard.authorizedPlayers);
            Puts($"Search complete! Found {toolCupboards.Count} tool cupboards");
        }

        #endregion

        #region Refund Handling

        private void RefundResources(BasePlayer player, BuildingBlock buildingBlock)
        {
            foreach(var item in buildingBlock.blockDefinition.grades[(int)buildingBlock.grade].costToBuild)
            {
                var newItem = ItemManager.CreateByItemID(item.itemid, (int)item.amount);
                if (newItem != null)
                {
                    player.inventory.GiveItem(newItem);
                    player.Command("note.inv", item.itemid, item.amount);
                }
            }
        }

        #endregion

        #region Building/Water Handling

        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            var player = planner?.GetOwnerPlayer();
            if (player == null || permission.UserHasPermission(player.UserIDString, permBypass)) return;

            var entity = go.ToBaseEntity();
            var buildingBlock = entity?.GetComponent<BuildingBlock>();
            if (buildingBlock == null) return;

            if (buildingBlock.WaterFactor() >= config.MaxWaterDepth && config.RestrictWaterDepth)
            {
                if (config.RefundResources) RefundResources(player, buildingBlock);
                buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                Message(player, "MaxWaterDepth", config.MaxWaterDepth);
                return;
            }

            var blockName = buildingBlock.PrefabName;
            var buildingId = buildingBlock.buildingID;
            if (buildingIds.ContainsKey(buildingId))
            {
                var connectingStructure = buildingIds[buildingBlock.buildingID];
                if (blockName == foundation || blockName == triFoundation && config.RestrictFoundations)
                {
                    var foundationCount = GetCountOf(connectingStructure, foundation);
                    var triFoundationCount = GetCountOf(connectingStructure, triFoundation);

                    if (blockName == foundation && foundationCount >= config.MaxFoundations)
                    {
                        if (config.RefundResources) RefundResources(player, buildingBlock);
                        buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                        Message(player, "MaxFoundations", config.MaxFoundations);
                    }
                    else if (blockName == triFoundation && triFoundationCount >= config.MaxTriFoundations)
                    {
                        if (config.RefundResources) RefundResources(player, buildingBlock);
                        buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                        Message(player, "MaxTriFoundations", config.MaxTriFoundations);
                    }
                    else
                    {
                        var structure = new List<BuildingBlock>(connectingStructure) { buildingBlock };
                        buildingIds[buildingId] = structure;
                    }
                }
                else
                {
                    if (!allowedBuildingBlocks.Contains(blockName))
                    {
                        BuildingBlock firstFoundation = null;
                        foreach (var block in connectingStructure.Where(b => !string.IsNullOrEmpty(b.PrefabName) && b.PrefabName.Equals(triFoundation) || b.PrefabName.Equals(foundation)))
                            firstFoundation = block;

                        if (firstFoundation != null)
                        {
                            var height = (float)Math.Round(buildingBlock.transform.position.y - firstFoundation.transform.position.y, 0, MidpointRounding.AwayFromZero);
                            if (config.MaxBuildHeight <= height && config.RestrictBuildHeight)
                            {
                                if (config.RefundResources) RefundResources(player, buildingBlock);
                                buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                                Message(player, "MaxBuildHeight", config.MaxBuildHeight / 3);
                            }
                        }
                    }
                }
            }
            else
            {
                var structure = new List<BuildingBlock> { buildingBlock };
                buildingIds[buildingId] = structure;
            }
        }

        private void HandleRemoval(BaseCombatEntity entity)
        {
            var buildingBlock = entity?.GetComponent<BuildingBlock>() ?? null;
            if (buildingBlock == null) return;

            var blockName = buildingBlock.PrefabName;
            if (blockName == null || blockName != foundation && blockName != triFoundation) return;

            if (buildingIds.ContainsKey(buildingBlock.buildingID))
            {
                var blockList = buildingIds[buildingBlock.buildingID].Where(b => b == buildingBlock).ToList();
                foreach (var block in blockList)
                    buildingIds[buildingBlock.buildingID].Remove(buildingBlock);
            }
        }

        private void OnStructureDemolish(BaseCombatEntity entity) => HandleRemoval(entity);

        #endregion

        #region Tool Cupboard Handling

        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            var cupboard = entity as BuildingPrivlidge;
            if (cupboard == null) return;

            var player = deployer.ToPlayer();
            if (config.RestrictToolCupboards && player != null)
            {
                var cupboards = toolCupboards.Where(c => c.Value.Contains(new PlayerNameID { userid = player.userID }));
                if (cupboards.Count() > config.MaxToolCupboards)
                {
                    cupboard.Kill();
                    Message(player, "MaxToolCupboards", config.MaxToolCupboards);
                }
            }
            else
            {
                if (!toolCupboards.ContainsKey(cupboard.net.ID)) toolCupboards.Add(cupboard.net.ID, cupboard.authorizedPlayers);
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity)
        {
            var cupboard = entity as BuildingPrivlidge;
            if (cupboard != null && toolCupboards.ContainsKey(cupboard.net.ID)) toolCupboards.Remove(cupboard.net.ID);
            else HandleRemoval(entity);
        }

        #endregion

        #region Helper Methods

        private int GetCountOf(List<BuildingBlock> ConnectingStructure, string buildingObject)
        {
            var count = 0;
            var blockList = ConnectingStructure.ToList();
            foreach (var block in blockList)
            {
                if (block == null) ConnectingStructure.Remove(block);
                else if (block.PrefabName == buildingObject) count++;
            }
            return count;
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(BasePlayer player, string key, params object[] args) => Player.Reply(player, Lang(key, player.UserIDString, args));

        #endregion
    }
}
