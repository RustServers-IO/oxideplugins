using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Building Restriction", "Wulf/lukespragg", "1.3.1", ResourceId = 2124)]
    [Description("Restricts building height, building in water, and number of foundations")]
    public class BuildingRestriction : RustPlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Maximum build height")]
            public int MaxBuildHeight;

            [JsonProperty(PropertyName = "Maximum foundations")]
            public int MaxFoundations;

            [JsonProperty(PropertyName = "Maximum triangle foundations")]
            public int MaxTriFoundations;

            [JsonProperty(PropertyName = "Maximum water depth")]
            public double MaxWaterDepth;

            [JsonProperty(PropertyName = "Refund resources when restricted")]
            public bool RefundResources;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    MaxBuildHeight = 5,
                    MaxFoundations = 16,
                    MaxTriFoundations = 24,
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
        private List<string> allowedBuildingBlocks = new List<string>
        {
            "assets/prefabs/building core/floor/floor.prefab",
            "assets/prefabs/building core/floor.frame/floor.frame.prefab",
            "assets/prefabs/building core/floor.triangle/floor.triangle.prefab",
            "assets/prefabs/building core/roof/roof.prefab",
            "assets/prefabs/building core/wall.low/wall.low.prefab"
        };

        private const string triFoundation = "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab";
        private const string foundation = "assets/prefabs/building core/foundation/foundation.prefab";
        private const string permBypass = "buildingrestriction.bypass";

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permBypass, this);

            FindStructures();
        }

        private void FindStructures()
        {
            buildingIds.Clear();
            Puts("Searching for structures, this may awhile...");
            var foundationBlocks = Resources.FindObjectsOfTypeAll<BuildingBlock>().Where(b => b.name == foundation || b.name == triFoundation).ToList();
            foreach (var block in foundationBlocks.Where(b => !buildingIds.ContainsKey(b.buildingID)))
            {
                var structure = GameObject.FindObjectsOfType<BuildingBlock>().Where(b => b.buildingID == block.buildingID && b.name == foundation || b.name == triFoundation);
                buildingIds[block.buildingID] = structure.ToList();
            }
            Puts($"Search complete! Found {buildingIds.Count} structures");
        }

        #endregion

        #region Game Hooks

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

        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            var player = planner?.GetOwnerPlayer();
            if (player == null || permission.UserHasPermission(player.UserIDString, permBypass)) return;

            var entity = GameObjectEx.ToBaseEntity(go);
            var buildingBlock = entity?.GetComponent<BuildingBlock>();
            if (buildingBlock == null) return;

            if (buildingBlock.WaterFactor() >= config.MaxWaterDepth)
            {
                if (config.RefundResources) RefundResources(player, buildingBlock);
                buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                Player.Reply(player, Lang("MaxWaterDepth", player.UserIDString, config.MaxWaterDepth));
                return;
            }

            var buildingId = buildingBlock.buildingID;
            if (buildingIds.ContainsKey(buildingId))
            {
                var connectingStructure = buildingIds[buildingBlock.buildingID];
                if (buildingBlock.name == foundation || buildingBlock.name == triFoundation)
                {
                    var foundationCount = GetCountOf(connectingStructure, foundation);
                    var triFoundationCount = GetCountOf(connectingStructure, triFoundation);

                    if (buildingBlock.name == foundation && foundationCount >= config.MaxFoundations)
                    {
                        if (config.RefundResources) RefundResources(player, buildingBlock);
                        buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                        Player.Reply(player, Lang("MaxFoundations", player.UserIDString, config.MaxFoundations));
                    }
                    else if (buildingBlock.name == triFoundation && triFoundationCount >= config.MaxTriFoundations)
                    {
                        if (config.RefundResources) RefundResources(player, buildingBlock);
                        buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                        Player.Reply(player, Lang("MaxTriFoundations", player.UserIDString, config.MaxTriFoundations));
                    }
                    else
                    {
                        var structure = new List<BuildingBlock>(connectingStructure);
                        structure.Add(buildingBlock);
                        buildingIds[buildingId] = structure;
                    }
                }
                else
                {
                    if (!allowedBuildingBlocks.Contains(buildingBlock.name))
                    {
                        BuildingBlock firstFoundation = null;
                        foreach (var block in connectingStructure.Where(b => !string.IsNullOrEmpty(b.name) && b.name.Contains(triFoundation) || b.name.Contains(foundation)))
                            firstFoundation = block;

                        if (firstFoundation != null)
                        {
                            var height = (float)Math.Round(buildingBlock.transform.position.y - firstFoundation.transform.position.y, 0, MidpointRounding.AwayFromZero);
                            if (config.MaxBuildHeight <= height)
                            {
                                if (config.RefundResources) RefundResources(player, buildingBlock);
                                buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib);
                                Player.Reply(player, Lang("MaxBuildHeight", player.UserIDString, (config.MaxBuildHeight / 3)));
                            }
                        }
                    }
                }
            }
            else
            {
                var structure = new List<BuildingBlock>();
                structure.Add(buildingBlock);
                buildingIds[buildingId] = structure;
            }
        }

        private void HandleRemoval(BaseCombatEntity entity)
        {
            var buildingBlock = entity?.GetComponent<BuildingBlock>() ?? null;
            if (buildingBlock == null || buildingBlock.name != foundation && buildingBlock.name != triFoundation) return;

            if (buildingIds.ContainsKey(buildingBlock.buildingID))
            {
                var blockList = buildingIds[buildingBlock.buildingID].Where(b => b == buildingBlock).ToList();
                foreach (var block in blockList)
                    buildingIds[buildingBlock.buildingID].Remove(buildingBlock);
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity) => HandleRemoval(entity);

        private void OnStructureDemolish(BaseCombatEntity entity) => HandleRemoval(entity);

        #endregion

        #region Helper Methods

        private int GetCountOf(List<BuildingBlock> ConnectingStructure, string buildingObject)
        {
            var count = 0;
            var blockList = ConnectingStructure.ToList();
            foreach (var block in blockList)
            {
                if (block == null) ConnectingStructure.Remove(block);
                else if (block.name == buildingObject) count++;
            }
            return count;
        }

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
