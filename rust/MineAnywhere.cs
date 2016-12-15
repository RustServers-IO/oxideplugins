using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Text;

namespace Oxide.Plugins
{
    [Info("MineAnywhere", "Calytic @ RustServers.IO", "0.0.2", ResourceId = 2240)]
    [Description("Mine all the rocks, all the wood, all the junk, even the ground")]
    class MineAnywhere : RustPlugin
    {
        enum ResourceType
        {
            Wood = 1,
            Stone = 2,
            Metal = 3,
            Junk = 4,
            Snow = 5,
            Grass = 6,
            Sand = 7
        }

        class PluginSettings
        {
            public float depletionRate = 25f;
            public float gatherRate = 1f;
            public float gatherRNG = 0.5f;
            public float nodeDecayMinutes = 15f;
            public float nodeDistance = 30f;

            public bool canMineDeployables = true;

            public Dictionary<ResourceType, Dictionary<string, float>> nodeResources;
            public List<string> nodeDeployables;

            public string VERSION;
        }

        abstract class ResourceNode
        {
            public float depletion = 0;
            public long created;
            public ResourceType type;

            private DateTime _time;
            public DateTime time
            {
                get
                {
                    if (_time != null)
                    {
                        return _time;
                    }
                    return this._time = new DateTime(created);
                }
            }
        }

        class StaticResourceNode : ResourceNode
        {
            public Vector3 position;
        }

        class WorldResourceNode : ResourceNode
        {
            public uint netID;
        }

        PluginSettings settings;
        Dictionary<string, StaticResourceNode> staticNodes = new Dictionary<string, StaticResourceNode>();
        Dictionary<string, WorldResourceNode> worldNodes = new Dictionary<string, WorldResourceNode>();

        [PluginReference]
        Plugin HooksExtended;

        void OnServerInitialized()
        {
            if (!plugins.Exists("HooksExtended"))
            {
                PrintError("HooksExtended is required to use this MineAnywhere! http://oxidemod.org/plugins/hooksextended.2239/");
                return;
            }

            HooksExtended.Call("EnableHook", "OnPlayerAttack");

            LoadConfig();
        }

        void LoadConfig()
        {
            settings = Config.ReadObject<PluginSettings>();
        }

        void LoadDefaultConfig()
        {
            settings = DefaultConfig();
            SaveConfig();
        }

        void SaveConfig()
        {
            Config.WriteObject<PluginSettings>(settings, true);
        }

        PluginSettings DefaultConfig()
        {
            return new PluginSettings()
            {
                nodeResources = DefaultNodeResources(),
                nodeDeployables = DefaultNodeDeployables(),
                VERSION = Version.ToString()
            };
        }

        List<string> DefaultNodeDeployables()
        {
            return new List<string>()
            {
                "barricade",
                "cupboard",
            };
        }

        Dictionary<ResourceType, Dictionary<string, float>> DefaultNodeResources()
        {
            return new Dictionary<ResourceType, Dictionary<string, float>>()
            {
                {ResourceType.Wood, new Dictionary<string, float>() {
                    {"wood", 5}, 
                    {"sticks", 2}
                }},
                {ResourceType.Stone, new Dictionary<string, float>() {
                    {"stones", 8}
                }},
                {ResourceType.Metal, new Dictionary<string, float>() {
                    {"metalblade", 1}, 
                    {"metal.ore", 5},
                    {"metalpipe", 1},
                    {"metal.fragments", 2}
                }},
                {ResourceType.Junk, new Dictionary<string, float>() {
                    {"metal.pipe", 1}, 
                    {"metalspring", 1}, 
                    {"sheetmetal", 3}, 
                    {"wood", 4}, 
                    {"stones", 5}
                }},
                {ResourceType.Snow, new Dictionary<string, float>() {
                    {"water", 1}, 
                }},
                {ResourceType.Grass, new Dictionary<string, float>() {
                    {"cloth", 1}, 
                }},
                {ResourceType.Sand, new Dictionary<string, float>() {
                    {"stones", 1}, 
                }}
            };
        }

        private void OnHitSnow(BasePlayer player, HitInfo info)
        {
            TriggerHit(player, ResourceType.Snow, info.HitEntity);
        }

        private void OnHitGrass(BasePlayer player, HitInfo info)
        {
            TriggerHit(player, ResourceType.Grass, info.HitEntity);
        }

        private void OnHitSand(BasePlayer player, HitInfo info)
        {
            TriggerHit(player, ResourceType.Sand, info.HitEntity);
        }

        private void OnHitMetal(BasePlayer player, HitInfo info)
        {
            TriggerHit(player, ResourceType.Metal, info.HitEntity);
        }

        private void OnHitRock(BasePlayer player, HitInfo info)
        {
            TriggerHit(player, ResourceType.Stone, info.HitEntity);
        }

        private void OnHitWood(BasePlayer player, HitInfo info)
        {
            TriggerHit(player, ResourceType.Wood, info.HitEntity);
        }

        private void OnHitJunk(BasePlayer player, HitInfo info)
        {
            if (info.HitEntity != null)
            {
                HitNode<WorldResourceNode>(player, ResourceType.Junk, info.HitEntity);
            }
        }

        void TriggerHit(BasePlayer player, ResourceType type, BaseEntity hitEntity = null)
        {
            if (hitEntity != null && settings.canMineDeployables)
            {
                foreach (string name in settings.nodeDeployables)
                {
                    if (hitEntity.name.Contains(name))
                    {
                        HitNode<WorldResourceNode>(player, type, hitEntity);
                        break;
                    }
                }
            }
            else
            {
                HitNode<StaticResourceNode>(player, type);
            }
        }

        void HitNode<T>(BasePlayer player, ResourceType type, BaseEntity hitEntity = null) where T : ResourceNode
        {
            T node = null;

            string key = player.transform.position.ToString();
            StaticResourceNode staticNode = null;
            WorldResourceNode worldNode = null;

            if (typeof(T) == typeof(StaticResourceNode) && staticNodes.TryGetValue(key, out staticNode))
            {
                node = staticNode as T;
            }
            else if (typeof(T) == typeof(WorldResourceNode) && worldNodes.TryGetValue(key, out worldNode))
            {
                if (hitEntity != null)
                {
                    node = worldNode as T;
                }
            }
            else
            {
                if (!TryGetNode<T>(player.transform.position, type, out node, hitEntity))
                {
                    if (typeof(T) == typeof(StaticResourceNode))
                    {
                        node = CreateStaticNode(player.transform.position, type) as T;
                        staticNodes.Add(key, node as StaticResourceNode);
                    }
                    else if (typeof(T) == typeof(WorldResourceNode))
                    {
                        if (hitEntity != null)
                        {
                            node = CreateWorldNode(hitEntity.net.ID, type) as T;
                            worldNodes.Add(key, node as WorldResourceNode);
                        }
                    }
                }
            }
            

            if (node != null)
            {
                if (node.depletion < 100)
                {
                    MineNode(player, node);
                }


                if (node.depletion >= 100 && typeof(T) == typeof(WorldResourceNode) && hitEntity != null)
                {
                    if (hitEntity.MaxHealth() == 0)
                    {
                        hitEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                }
            }
        }

        void MineNode<T>(BasePlayer player, T node) where T : ResourceNode
        {
            if (!settings.nodeResources.ContainsKey(node.type)) return;
            Dictionary<string, float> items = new Dictionary<string,float>(settings.nodeResources[node.type]);
            if (items.Count == 0) return;
            int numMax = new System.Random().Next(1, items.Count);
            node.depletion += settings.depletionRate;

            var rand = new System.Random();

            for (int i = numMax; i >= 0; i--)
            {
                if (items.Count == 0) continue;
                var randKey = rand.Next(0, items.Count - 1);
                KeyValuePair<string, float> kvp = items.ElementAt(randKey);
                items.Remove(kvp.Key);
                string itemName = kvp.Key;
                float itemAmount = kvp.Value * settings.gatherRate;
                float rng = itemAmount * settings.gatherRNG;
                if (GetRandomBoolean())
                {
                    itemAmount += rng;
                }
                else
                {
                    itemAmount -= rng;
                }

                if (itemAmount > 1)
                {
                    int intAmount = Convert.ToInt32(itemAmount);
                    Item item = ItemManager.CreateByName(itemName, intAmount);
                    if (item != null)
                    {
                        if (!item.MoveToContainer(player.inventory.containerMain))
                        {
                            item.Drop(player.GetDropPosition(), player.GetDropVelocity());
                        }

                        player.Command("note.inv", item.info.itemid, intAmount);
                    }
                }
            }
        }

        bool TryGetNode<T>(Vector3 position, ResourceType type, out T node, BaseEntity hitEntity = null) where T : ResourceNode
        {
            node = null;
            string key = null;
            Dictionary<string, T> nodes = new Dictionary<string, T>();
            if (typeof(T) == typeof(StaticResourceNode))
            {
                nodes = staticNodes as Dictionary<string, T>;
            }
            else if (typeof(T) == typeof(WorldResourceNode))
            {
                nodes = worldNodes as Dictionary<string, T>;
            }

            bool found = false;

            foreach (KeyValuePair<string, T> kvp in nodes)
            {
                var n = kvp.Value;
                if(n.type == type) {
                    if (n is StaticResourceNode)
                    {
                        if (Distance(position, (n as StaticResourceNode).position) <= settings.nodeDistance)
                        {
                            node = n;
                            key = kvp.Key;
                            found = true;
                            break;
                        }
                    }
                    else if (n is WorldResourceNode && hitEntity != null)
                    {
                        if ((n as WorldResourceNode).netID == hitEntity.net.ID)
                        {
                            node = n;
                            key = kvp.Key;
                            found = true;
                            break;
                        }
                    }
                }
            }

            if (node != null && !string.IsNullOrEmpty(key))
            {
                TimeSpan ts = node.time - DateTime.Now;
                if (ts.TotalMinutes > settings.nodeDecayMinutes)
                {
                    nodes.Remove(key);
                    found = false;
                }
            }

            return found;
        }

        StaticResourceNode CreateStaticNode(Vector3 position, ResourceType type)
        {
            return new StaticResourceNode()
            {
                position = position,
                type = type,
                created = DateTime.Now.ToBinary()
            };
        }

        WorldResourceNode CreateWorldNode(uint netID, ResourceType type)
        {
            return new WorldResourceNode()
            {
                netID = netID,
                type = type,
                created = DateTime.Now.ToBinary()
            };
        }

        void OnEntityDeath(BaseEntity entity)
        {
            if (entity == null) return;
            if (entity.net == null) return;
            string netID = entity.net.ID.ToString();
            if (worldNodes.ContainsKey(netID))
            {
                worldNodes.Remove(netID);
            }
        }

        public bool GetRandomBoolean()
        {
            return (new System.Random().Next(100) % 2) == 0 ? true : false;
        }

        public float Distance(Vector3 start, Vector3 finish)
        {
            return (start - finish).magnitude;
        }

        #region HelpText
        private void SendHelpText(BasePlayer player)
        {
            var sb = new StringBuilder()
               .Append("MineAnywhere by <color=#ce422b>http://rustservers.io</color>\n")
               .Append("  ").Append("Mine logs, big rocks, junk piles, buildings, ground, etc").Append("\n");

            player.ChatMessage(sb.ToString());
        }
        #endregion
    }
}
