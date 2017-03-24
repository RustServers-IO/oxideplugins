using System.Collections.Generic;
using Facepunch;
using UnityEngine;

namespace Oxide.Plugins
{ 
    [Info("PrivilegeDeploy", "k1lly0u", "0.1.3", ResourceId = 1800)]
    class PrivilegeDeploy : RustPlugin
    {
        private readonly int triggerMask = LayerMask.GetMask("Trigger", "Construction");
        private bool Loaded = false;

        Dictionary<string, string> prefabToItem = new Dictionary<string, string>();
        Dictionary<string, List<ItemAmount>> constructionToIngredients = new Dictionary<string, List<ItemAmount>>();

        private Dictionary<ulong, PendingItem> pendingItems = new Dictionary<ulong, PendingItem>();

        void OnServerInitialized()
        {
            LoadVariables();
            InitValidList();
        }
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (Loaded)
            {
                if (configData.deployables.Contains(entity.ShortPrefabName) || configData.deployables.Contains(entity.PrefabName))
                {
                    var ownerID = entity.GetComponent<BaseEntity>().OwnerID;
                    if (ownerID != 0)
                    {
                        BasePlayer player = BasePlayer.FindByID(ownerID);
                        if (player == null || player.IsAdmin) return;
                        if (!HasPriv(player))
                        {
                            List<ItemAmount> items = new List<ItemAmount>();
                            if (entity is BuildingBlock && constructionToIngredients.ContainsKey(entity.PrefabName))
                            {
                                foreach (var ingredient in constructionToIngredients[entity.PrefabName])
                                    items.Add(ingredient);
                            }
                            else if (prefabToItem.ContainsKey(entity.PrefabName))                            
                                items.Add(new ItemAmount { amount = 1, startAmount = 1, itemDef = ItemManager.FindItemDefinition(prefabToItem[entity.PrefabName]) });
                            
                            if (!pendingItems.ContainsKey(player.userID))
                                pendingItems.Add(player.userID, new PendingItem());
                            pendingItems[player.userID].items = items;

                            CheckForDuplicate(player);

                            if (entity is BaseCombatEntity)
                                (entity as BaseCombatEntity).DieInstantly();
                            else entity.Kill();
                        }
                    }
                }
            }
        }      
        private void CheckForDuplicate(BasePlayer player)
        {
            if (pendingItems[player.userID].timer != null) pendingItems[player.userID].timer.Destroy();               
            pendingItems[player.userID].timer = timer.Once(0.01f, () => GivePlayerItem(player));
        }
        private void GivePlayerItem(BasePlayer player)
        {
            foreach(var itemAmount in pendingItems[player.userID].items)
            {
                Item item = ItemManager.Create(itemAmount.itemDef, (int)itemAmount.amount);
                var deployable = item.info.GetComponent<ItemModDeployable>();
                if (deployable != null)
                {
                    var oven = deployable.entityPrefab.Get()?.GetComponent<BaseOven>();
                    if (oven != null)
                        oven.startupContents = null;
                }
                player.GiveItem(item);
            }            
            SendReply(player, lang.GetMessage("blocked", this, player.UserIDString));
            pendingItems.Remove(player.userID);
        }
        
        private bool HasPriv(BasePlayer player)
        {
            var colliders = Pool.GetList<Collider>();
            Vis.Colliders(player.transform.position + new Vector3(0, player.bounds.max.y, 0), 0.2f, colliders, LayerMask.GetMask("Trigger"));
            foreach (var collider in colliders)
            {
                if (collider.gameObject != null && collider.gameObject.name == "areaTrigger" && collider.gameObject.layer == 18)
                {
                    var cupboard = collider.gameObject.GetComponentInParent<BuildingPrivlidge>();
                    if (cupboard != null)
                    {
                        if (cupboard.IsAuthed(player)) return true;
                    }
                }
            }
            Pool.FreeList(ref colliders);
            return false;           
        }

        #region Prefab to Item links
        void InitValidList()
        {
            foreach (var item in ItemManager.GetItemDefinitions())
            {
                var deployable = item?.GetComponent<ItemModDeployable>();
                if (deployable == null) continue;
                
                if (!prefabToItem.ContainsKey(deployable.entityPrefab.resourcePath))                
                    prefabToItem.Add(deployable.entityPrefab.resourcePath, item.shortname);                
            }
            foreach (var construction in PrefabAttribute.server.GetAll<Construction>())
            {
                if (construction.deployable == null && !string.IsNullOrEmpty(construction.info.name.english))
                {
                    if (!constructionToIngredients.ContainsKey(construction.fullName))                    
                        constructionToIngredients.Add(construction.fullName, construction.defaultGrade.costToBuild);
                }
            }
        }
        #endregion

        #region Config

        private ConfigData configData;
        class ConfigData
        {
            public List<string> deployables { get; set; }
        }
        private void LoadVariables()
        {
            Loaded = true;
            RegisterMessages();
            LoadConfigVariables();
            SaveConfig();
        }
        private void RegisterMessages() => lang.RegisterMessages(messages, this);
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                deployables = new List<string>
                    {
                        "barricade.concrete",
                        "barricade.metal",
                        "barricade.sandbags",
                        "barricade.stone",
                        "barricade.wood",
                        "barricade.woodwire",
                        "campfire",
                        "gates.external.high.stone",
                        "gates.external.high.wood",
                        "wall.external.high",
                        "wall.external.high.stone",
                        "landmine",
                        "beartrap"
                    }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        class PendingItem
        {
            public Timer timer;
            public List<ItemAmount> items = new List<ItemAmount>();
        }
        #endregion

        Dictionary<string, string> messages = new Dictionary<string, string>
        {
            {"blocked", "You can not build this outside of a building privileged area!" }
        };
    }
}

