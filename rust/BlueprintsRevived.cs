using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using System;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using Rust;

namespace Oxide.Plugins
{
    [Info("BlueprintsRevived", "Jake_Rich", "1.2.2", ResourceId = 2433)]
    [Description("The original Blueprint System with balance changes!")]

    public class BlueprintsRevived : RustPlugin
    {
        public static BlueprintsRevived _plugin { get; set; }
        public bool serverInitialized { get; set; } = false;
        void Init()
        {
            _plugin = this;
        }

        void OnServerInitialized()
        {
            UnityEngine.Random.InitState((int)DateTime.Now.Ticks);

            serverInitialized = true;

            if (ImageLibrary == null)
            {
                Puts("WARNING! Blueprints was not loaded. Image Library is not installed! Please download ImageLibrary from oxide!");
                return;
            }

            database = new Database(); //Database class has ConfigurationAccessors inside it

            DefaultSettings_BlueprintTiers = new GithubConfig<SavedBlueprintTiers>("Blueprint-Default_Blueprint_Tiers");
            LootTables = new GithubConfig<SavedLootTables>("Blueprint-LootTables");
            Settings_DevOnly = new GithubConfig<SavedSettingsNonEdit>("Blueprint-Developer-Settings");

            ReloadSettings();

            ApplySettings();

            ItemManager.itemList.First(x => x.shortname == "xmas.present.small").stackable = 1000;
            ItemManager.itemList.First(x => x.shortname == "xmas.present.medium").stackable = 100;

            if (componentList == null)
            {
                Puts("Component list is null!");
            }

            foreach (var bp in ItemManager.itemList)
            {
                if (GetItemTier(bp.shortname) != BPType.None) //Leave components in uncraftables like CCTV cameras
                {
                    bp?.Blueprint?.ingredients?.RemoveAll(x => componentList.Contains(x.itemDef.shortname));
                }
            }

            SetupUI();

            foreach (BasePlayer player in UnityEngine.Object.FindObjectsOfType<BasePlayer>())
            {
                OnPlayerInit(player);
            }

            AddImage("http://images.akamai.steamusercontent.com/ugc/172666352576493947/A4E7CA0FF947BF4D9FCAC1CD10695AFF1DD1ADE7/?interpolation=lanczos-none", "xmas.present.small", fragsSkinID);
            AddImage("https://i.gyazo.com/0a3b40b6c84066e1437c9ede8de24ab4.png", "xmas.present.medium", pageSkinID);
            AddImage("http://images.akamai.steamusercontent.com/ugc/172666352576699519/A4B67A925C05035D1E35F6F625319C7C245853C2/?interpolation=lanczos-none", "xmas.present.medium", bookSkinID);
            AddImage("http://images.akamai.steamusercontent.com/ugc/172666352576709367/42AB45CEC824CA43486EFCDF08B19A9C0E2E6558/?interpolation=lanczos-none", "xmas.present.large", librarySkinID);

            //ExportLootTables();
        }

        void Unload()
        {
            CleanupMonumentBenches();

            foreach (var player in messageTimers.Keys.ToList())
            {
                CloseMessage(player);
            }

            ItemManager.itemList.First(x => x.shortname == "xmas.present.small").stackable = 30;
            ItemManager.itemList.First(x => x.shortname == "xmas.present.medium").stackable = 15;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                HandleManagedComponents(player, removeComponents: true, giveComponents: false);
                StopResearch(player);
            }

            database.Unload();

            foreach (var researchComp in GameObject.FindObjectsOfType<ResearchTableCollider>())
            {
                GameObject.Destroy(researchComp);
            }

            openIconPanel.HideAll();
        }

        void ApplySettings()
        {
            ConVar.Server.radiation = Settings.radiation;

            Settings.itemLevels["heavy.plate.helmet"] = Settings.disable_heavyArmour ? BPType.None : BPType.Library;
            Settings.itemLevels["heavy.plate.jacket"] = Settings.disable_heavyArmour ? BPType.None : BPType.Library;
            Settings.itemLevels["heavy.plate.pants"] = Settings.disable_heavyArmour ? BPType.None : BPType.Library;
        }

        #region Variables

        UnityEngine.Random random = new UnityEngine.Random();

        #endregion

        #region Settings

        public GithubConfig<SavedBlueprintTiers> DefaultSettings_BlueprintTiers { get; set; }
        public GithubConfig<SavedLootTables> LootTables { get; set; }
        public SavedSettings Settings { get; set; }
        public GithubConfig<SavedSettingsNonEdit> Settings_DevOnly { get; set; }

        public const ulong fragsSkinID = 869862511u; //Uploaded by Jake through special method
        public const ulong pageSkinID = 884147568u; 
        public const ulong bookSkinID = 884151226u;
        public const ulong librarySkinID = 884152141u;
        public enum BPType : ulong
        {
            Default = 0,
            Frags = 1,
            Page = 2,
            Book = 3,
            Library = 4,
            None = 99999,
        }

        protected override void LoadDefaultConfig()
        {
            SaveConfig();
        }

        public void ReloadSettings()
        {
            Settings = Config.ReadObject<SavedSettings>(); //Finally converted to a configuration file in the right place! Wulf will love me!
            Config.WriteObject(Settings);
            Settings = Config.ReadObject<SavedSettings>();
        }

        public void SaveSettings()
        {
            Config.WriteObject(Settings);
            Settings = Config.ReadObject<SavedSettings>();
        }

        public void ReloadAllSettings()
        {
            DefaultSettings_BlueprintTiers.Reload();
            DefaultSettings_BlueprintTiers.Instance.Initialize();
            DefaultSettings_BlueprintTiers.Save();

            Settings = Config.ReadObject<SavedSettings>(); //Finally converted to a configuration file in the right place! Wulf will love me!
            Config.WriteObject(Settings);
            Settings = Config.ReadObject<SavedSettings>();

            Settings_DevOnly.Reload();
            LootTables.Reload();
        }

        #endregion

        #region Configuration classes

        public class GithubConfig<Type> : ConfigurationAccessor<Type> where Type : BaseConfigClass
        {
            public GithubConfig(string name) : base(name)
            {

            }

            public override void Init()
            {
                Download();
            }

            public void Download()
            {
                Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    {"User-Agent", "aleks976" },
                    {"Accept", "application/vnd.github.v3+json"}
                };
                _plugin.webrequest.EnqueueGet($"https://raw.githubusercontent.com/Aleks976/PublicRustPlugins/master/Configurations/Data/{name}.json", (code, response) =>
                 {
                     if (code == 200)
                     {
                         Type deserialize;
                         try
                         {
                             deserialize = JsonConvert.DeserializeObject<Type>(response);
                         }
                         catch
                         {
                             _plugin.Puts($"Failed to deserialize {name}\n{response}");
                             return;
                         }
                         Instance = deserialize;
                         Save();
                         //Load();
                         _plugin.Puts($"Downloaded {name}.json");
                     }
                     else
                     {
                         _plugin.Puts($"Failed to download {name} settings!");
                     }
                     Instance.Initialize();
                     Save();
                 }, _plugin, headers, 60);
            }
        }

        public class ConfigurationAccessor<Type> where Type : class
        {
            #region Typed Configuration Accessors

            private Type GetTypedConfigurationModel(string storageName)
            {
                return Interface.Oxide.DataFileSystem.ReadObject<Type>(storageName);
            }

            private void SaveTypedConfigurationModel(string storageName, Type storageModel)
            {
                Interface.Oxide.DataFileSystem.WriteObject(storageName, storageModel);
            }

            #endregion

            public string name { get; set; }
            public Type Instance { get; set; }

            public ConfigurationAccessor(string name)
            {
                this.name = name;
                Init();
            }

            public virtual void Init()
            {
                Reload();
            }

            public virtual void Load()
            {
                Instance = GetTypedConfigurationModel(name);
            }

            public virtual void Save()
            {
                SaveTypedConfigurationModel(name, Instance);
            }

            public virtual void Reload()
            {
                Load(); //Need to load and save to init list
                Save();
                Load();
            }
        }

        public Dictionary<BPType, int> bpCount { get; set; } = new Dictionary<BPType, int>();

        public Database database { get; set; }

        public class BaseConfigClass
        {
            public virtual void Initialize()
            {

            }
        }

        public class SpawnPointConfiguration
        {
            public float xPos { get; set; }
            public float yPos { get; set; }
            public float zPos { get; set; }

            public float xRot { get; set; }
            public float yRot { get; set; }
            public float zRot { get; set; }
            public float wRot { get; set; }

            public static BasePlayer.SpawnPoint ToSpawnPoint(SpawnPointConfiguration configuration)
            {
                if (configuration == null)
                    return null;

                var newSpawn = new BasePlayer.SpawnPoint();

                newSpawn.pos.x = configuration.xPos;
                newSpawn.pos.y = configuration.yPos;
                newSpawn.pos.z = configuration.zPos;
                newSpawn.rot.x = configuration.xRot;
                newSpawn.rot.y = configuration.yRot;
                newSpawn.rot.z = configuration.zRot;
                newSpawn.rot.w = configuration.wRot;

                return newSpawn;
            }

            public SpawnPointConfiguration(BaseNetworkable entity)
            {
                Position = entity.transform.position;
                Rotation = entity.transform.rotation;
            }

            public SpawnPointConfiguration(Vector3 pos, Quaternion rot)
            {
                Position = pos;
                Rotation = rot;
            }

            public SpawnPointConfiguration()
            {

            }

            [JsonIgnore]
            public Vector3 Position
            {
                get { return new Vector3(xPos, yPos, zPos); }
                set { xPos = value.x; yPos = value.y; zPos = value.z; }
            }

            [JsonIgnore]
            public Quaternion Rotation
            {
                get { return new Quaternion(xRot, yRot, zRot, wRot); }
                set { xRot = value.x; yRot = value.y; zRot = value.z; wRot = value.w; }
            }

            public static SpawnPointConfiguration FromVectorAndRotation(Vector3 position, Quaternion rot)
            {
                return new SpawnPointConfiguration
                {
                    xPos = position.x,
                    yPos = position.y,
                    zPos = position.z,

                    xRot = rot.x,
                    yRot = rot.y,
                    zRot = rot.z,
                    wRot = rot.w
                };
            }
        }

        public class SavedLootTables : BaseConfigClass
        {
            public Dictionary<string, string> lootContainerAssignments { get; set; } = new Dictionary<string, string>();

            public Dictionary<string, GroupLootDefinition> allLootTables { get; set; } = new Dictionary<string, GroupLootDefinition>();

            public override void Initialize()
            {
                base.Initialize();
                foreach (var ent in GameObject.FindObjectsOfType<BaseNetworkable>())
                {
                    _plugin.OnEntitySpawned(ent);
                }
            }
        }

        public class SavedSettings
        {
            public bool pistol_Nerf { get; set; } = true;
            public bool spear_UnNerf { get; set; } = true;
            public bool crossbow_UnNerf { get; set; } = true;
            public bool huntingBow_UnNerf { get; set; } = true;
            public bool buildingPriv_UnNerf { get; set; } = true;
            public bool radiation { get; set; } = true;
            public bool unlisted { get; set; } = false;
            public bool disable_heavyArmour { get; set; } = true;
            public bool softside_ladder { get; set; } = true;
            public bool arrowRaiding { get; set; } = true;
            public bool hempSeeds { get; set; } = false;
            public bool blockRecyclingRoadsigns { get; set; } = true;
            public bool p250DamageBuff { get; set; } = true;
            public float blueprintRate { get; set; } = 1f;

            public Dictionary<string, BPType> itemLevels { get; set; } = new Dictionary<string, BPType>();
        }

        public class SavedSettingsNonEdit : BaseConfigClass
        {
            public Dictionary<string, SpawnPointConfiguration> monumentResearchBenches { get; set; } = new Dictionary<string, SpawnPointConfiguration>();

            public override void Initialize()
            {
                base.Initialize();
                _plugin.SpawnMonumentBenches();
            }
        }

        public class SavedBlueprintTiers : BaseConfigClass
        {
            public Dictionary<string, BPType> itemLevels { get; set; } = new Dictionary<string, BPType>();

            public Dictionary<BPType, List<string>> BPGroups = new Dictionary<BPType, List<string>>();

            public SavedBlueprintTiers()
            {
                foreach (var item in itemLevels.Values)
                {
                    if (!_plugin.bpCount.ContainsKey(item))
                    {
                        _plugin.bpCount[item] = 0;
                    }
                    _plugin.bpCount[item]++;
                }
            }

            public override void Initialize()
            {
                foreach (var item in ItemManager.itemList)
                {
                    if (!itemLevels.ContainsKey(item.shortname))
                    {
                        _plugin.Puts($"Found unassigned item: {item.shortname}");
                    }
                }
                BPGroups.Clear();
                for (BPType type = BPType.Frags; type <= BPType.Library; type++) //Nicer looking config
                {
                    BPGroups.Add(type, new List<string>());
                }
                BPGroups.Add(BPType.Default, new List<string>());
                BPGroups.Add(BPType.None, new List<string>());
                foreach (var item in itemLevels)
                {
                    if (!BPGroups.ContainsKey(item.Value))
                    {
                        BPGroups.Add(item.Value, new List<string>());
                    }
                    BPGroups[item.Value].Add(item.Key);
                }
                foreach(var item in itemLevels)
                {
                    if (_plugin.Settings.itemLevels.ContainsKey(item.Key))
                    {
                        continue;
                    }
                    _plugin.Settings.itemLevels.Add(item.Key, item.Value);
                }
                _plugin.SaveSettings();
            }
        }

        public class Database
        {
            private ConfigurationAccessor<DatabaseData> database { get; set; } = new ConfigurationAccessor<DatabaseData>("Blueprint-Database");
            private ConfigurationAccessor<DatabaseData> backup { get; set; } = new ConfigurationAccessor<DatabaseData>("Blueprint-Database-Backup");

            [JsonIgnore]
            private Timer autosave { get; set; }

            public Database()
            {
                autosave?.Destroy();
                autosave = _plugin.timer.Every(60f, Save);
            }

            public bool LearnBlueprint(BasePlayer player, int itemID)
            {
                BlueprintData playerdata;
                if (!database.Instance.playerData.TryGetValue(player.userID, out playerdata))
                {
                    playerdata = new BlueprintData(player.userID);
                    database.Instance.playerData[player.userID] = playerdata;
                }
                if (playerdata.unlockedItems.Contains(itemID))
                {
                    _plugin.DisplayMessage(player, "You already learned this!");
                    return false;
                }
                playerdata.unlockedItems.Add(itemID);
                return true;
            }

            public bool HasBlueprint(BasePlayer player, int itemID)
            {
                BlueprintData playerdata;
                if (!database.Instance.playerData.TryGetValue(player.userID, out playerdata))
                {
                    playerdata = new BlueprintData(player.userID);
                    database.Instance.playerData[player.userID] = playerdata;
                }
                return playerdata.unlockedItems.Contains(itemID);
            }

            public void ClearBlueprints(ulong userID, BPType type)
            {
                BlueprintData playerdata;
                if (!database.Instance.playerData.TryGetValue(userID, out playerdata))
                {
                    playerdata = new BlueprintData(userID);
                    database.Instance.playerData[userID] = playerdata;
                }
                playerdata.unlockedItems.RemoveWhere(x => _plugin.GetItemIDTier(x) == type);
            }

            public void Save()
            {
                database.Save();
                database.Instance.version++;
            }

            public void Unload()
            {
                Save();
                backup.Instance = database.Instance;
                backup.Save();
            }
        }

        public class DatabaseData
        {
            public Dictionary<ulong, BlueprintData> playerData { get; set; } = new Dictionary<ulong, BlueprintData>();
            public uint version = 0;
        }

        public class BlueprintData
        {
            public ulong userID = 0;
            public HashSet<int> unlockedItems { get; set; } = new HashSet<int>();

            public BlueprintData(ulong id)
            {
                userID = id;
            }

            public BlueprintData()
            {

            }
        }

        #endregion

        #region SpawnEntities

        bool TryToPlace(BasePlayer player, out BaseEntity ent, bool facingSky = false)
        {
            ent = null;
            Item item = player.GetActiveItem();
            BaseEntity heldEntity = player.GetHeldEntity();

            if (item == null || heldEntity == null)
            {
                Puts("Item or held entity is null");
                return false;
            }

            if (heldEntity.GetType() != typeof(Planner))
            {
                Puts("Held entity isn't planner");
                return false;
            }

            Planner planner = ((Planner)heldEntity);
            Deployable deployable = planner.GetDeployable();
            if (deployable == null)
            {
                Puts("Deployable is null");
                return false;
            }

            Vector3 targetPosition = player.transform.position;
            Quaternion targetRotation = player.transform.rotation;

            RaycastHit raycastHit;
            Ray ray = player.eyes.BodyRay();

            if (!Physics.Raycast(ray, out raycastHit, 4f, 1101070337))
            {
                targetPosition = player.eyes.position + player.eyes.BodyRay().direction * 4f;
                targetRotation = player.eyes.rotation * Quaternion.Euler(0, 0, 180);
            }
            else
            {
                targetPosition = raycastHit.point;
                targetRotation = Quaternion.LookRotation(raycastHit.normal) * Quaternion.Euler(90, 0, 0);
            }

            if (facingSky)
            {
                targetRotation.x = 0;
                targetRotation.z = 0;
            }

            if (raycastHit.normal.normalized == Vector3.up)
            {

            }

            targetRotation *= Quaternion.Euler(0, 90, 0);

            ItemModDeployable itemModDeployable = planner.GetModDeployable();

            BaseEntity placedObject = GameManager.server.CreateEntity(itemModDeployable.entityPrefab.resourcePath,
                targetPosition, targetRotation);
            placedObject.skinID = item.skin;
            placedObject.SendMessage("SetDeployedBy", player, SendMessageOptions.DontRequireReceiver);
            placedObject.OwnerID = 0;
            placedObject.Spawn();
            UnityEngine.Object.Destroy(placedObject.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.Destroy(placedObject.GetComponent<GroundWatch>());
            ent = placedObject;
            return true;
        }

        void PlaceResearchBench(BasePlayer player)
        {
            BaseEntity entity;
            if (!TryToPlace(player, out entity, true))
            {
                return;
            }
            float lowestDist = float.MaxValue;
            MonumentInfo closest = null;
            foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                float dist = Vector3.Distance(player.transform.position, monument.transform.position);
                if (dist < lowestDist)
                {
                    lowestDist = dist;
                    closest = monument;
                }
            }
            Vector3 pos = closest.transform.InverseTransformPoint(entity.transform.position);
            Quaternion rot = Quaternion.Inverse(entity.transform.rotation) * closest.transform.rotation;
            Settings_DevOnly.Instance.monumentResearchBenches[closest.name] = new SpawnPointConfiguration(pos, rot);
            Settings_DevOnly.Save();
        }

        #endregion

        #region Monuments

        public HashSet<BaseEntity> monumentResearchBenches = new HashSet<BaseEntity>();

        void SpawnMonumentBenches()
        {
            foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                SpawnPointConfiguration spawnPoint;
                if (Settings_DevOnly.Instance.monumentResearchBenches.TryGetValue(monument.name, out spawnPoint))
                {
                    ResearchTable placedObject = GameManager.server.CreateEntity("assets/prefabs/deployable/research table/researchtable_deployed.prefab", monument.transform.TransformPoint(spawnPoint.Position), Quaternion.Inverse(spawnPoint.Rotation) * monument.transform.rotation) as ResearchTable;
                    placedObject.OwnerID = 0;
                    placedObject.Spawn();
                    UnityEngine.Object.Destroy(placedObject.GetComponent<DestroyOnGroundMissing>());
                    UnityEngine.Object.Destroy(placedObject.GetComponent<GroundWatch>());
                    monumentResearchBenches.Add(placedObject);
                }
            }
        }

        void CleanupMonumentBenches()
        {
            foreach (var bench in monumentResearchBenches)
            {
                if (!bench.IsDestroyed)
                {
                    bench.Kill();
                }
            }
        }

        #endregion

        #region BP Functions

        BPType GetRarityType(string name)
        {
            switch (name)
            {
                case "Common": { return BPType.Frags; }
                case "Uncommon": { return BPType.Page; }
                case "Rare": { return BPType.Book; }
                case "VeryRare": { return BPType.Library; }
            }
            return BPType.None;
        }

        BPType GetBPType(Item item)
        {
            switch (item.skin)
            {
                case fragsSkinID: { return BPType.Frags; }
                case pageSkinID: { return BPType.Page; }
                case bookSkinID: { return BPType.Book; }
                case librarySkinID: { return BPType.Library; }
                default: { return BPType.None; }
            }
        }

        int GetBPFragAmount(BPType type)
        {
            switch (type)
            {
                case BPType.Frags: { return 1; }
                case BPType.Page: { return 50; }
                case BPType.Book: { return GetBPFragAmount(BPType.Page) * 5; }
                case BPType.Library: { return GetBPFragAmount(BPType.Book) * 5; ; }
                default: { return 0; }
            }
        }

        string GetBPName(BPType item)
        {
            switch (item)
            {
                case BPType.Frags: { return "Blueprint Fragment"; }
                case BPType.Page: { return "Blueprint Page"; }
                case BPType.Book: { return "Blueprint Book"; }
                case BPType.Library: { return "Blueprint Library"; }
                default: { return ""; }
            }
        }

        public int GetFragsNeeded(BPType type)
        {
            switch (type)
            {
                case BPType.Default: { return -1; }
                case BPType.Frags: { return 100; }
                case BPType.Page: { return 250; }
                case BPType.Book: { return 500; }
                case BPType.Library: { return 1000; }
                default:
                case BPType.None: { return int.MaxValue; }
            }
        }

        public int GetFragsNeeded(Item item)
        {
            return GetFragsNeeded(GetItemTier(item));
        }

        public BPType GetItemTier(Item item)
        {
            return GetItemTier(item.info.shortname);
        }

        public BPType GetItemTier(string shortname)
        {
            BPType type;
            if (!Settings.itemLevels.TryGetValue(shortname, out type))
            {
                return BPType.None;
            }
            return type;
        }

        public BPType GetItemIDTier(int id)
        {
            return GetItemTier(ItemManager.FindItemDefinition(id)?.shortname);
        }

        bool RnG(float chance)
        {
            return UnityEngine.Random.Range(0f, 1f) < chance;
        }

        void InsertBPFrags(ItemContainer inventory)
        {
            int amount = 0;
            if (RnG(0.25f))
            {
                amount += UnityEngine.Random.Range(5, 16);
            }
            int num = 0;
            for (int j = 0; j < inventory.capacity; j++)
            {
                Item slot = inventory.GetSlot(j);

                if (slot == null)
                {
                    break;
                }

                num += (int)slot.info.rarity;
            }
            if (num <= 3)
            {
                if (RnG(0.75f))
                {
                    amount += UnityEngine.Random.Range(5, 16);
                }
            }
            amount = (int)(amount * Settings.blueprintRate);
            if (amount > 0)
            {
                AddBlueprints(inventory, BPType.Frags, amount);
            }
        }

        bool IsLootContainer(BaseNetworkable entity)
        {
            return entity is LootContainer;
        }

        Item CreateBlueprint(BPType type, int amount)
        {
            Item item = null;
            switch (type)
            {
                case BPType.Frags:
                    {
                        item = ItemManager.CreateByName("xmas.present.small", amount, fragsSkinID);
                        break;
                    }
                case BPType.Page:
                    {
                        item = ItemManager.CreateByName("xmas.present.medium", amount, pageSkinID);
                        break;
                    }
                case BPType.Book:
                    {
                        item = ItemManager.CreateByName("xmas.present.medium", amount, bookSkinID);
                        break;
                    }
                case BPType.Library:
                    {
                        item = ItemManager.CreateByName("xmas.present.large", amount, librarySkinID);
                        break;
                    }
            }
            if (item == null)
            {
                Puts("AddBlueprints() error creating BPs!");
                return null;
            }
            item.name = GetBPName(type);
            return item;
        }

        void AddBlueprints(ItemContainer container, BPType type, int amount, bool dropOnFull = false)
        {
            Item item = CreateBlueprint(type, amount);

            if (item == null)
            {
                return;
            }

            if (!item.MoveToContainer(container) && dropOnFull)
            {
                item.Drop(container.dropPosition, container.dropVelocity);
            }
        }

        int GetBPAmountToCombine(BPType type)
        {
            switch (type)
            {
                case BPType.Frags:
                    {
                        return 50;
                    }
                case BPType.Page:
                case BPType.Book:
                    {
                        return 5;
                    }
                default:
                case BPType.Library:
                    {
                        return -1;
                    }
            }
        }

        void CombineFrags(Item item, BPType currentLevel)
        {
            int targetAmount = 0;
            if (GetBPType(item) == BPType.None)
            {
                return;
            }
            targetAmount = GetBPAmountToCombine(currentLevel);
            if (targetAmount <= 0)
            {
                return;
            }
            if (item.amount < targetAmount)
            {
                return;
            }
            item.amount -= targetAmount;
            if (item.amount <= 0)
            {
                item.Remove();
            }
            AddBlueprints(item.parent, currentLevel + 1, 1, true);
        }

        public bool RevealBlueprint(BasePlayer player, BPType type)
        {
            if (type < BPType.Frags || type > BPType.Library)
            {
                return false;
            }

            UnityEngine.Random.InitState((int)Time.time);

            int itemID;

            if (!TryGetRandomUnlearnedBlueprint(player, type, out itemID))
            {
                DisplayMessage(player, $"You already know all about {GetBPName(type)}!");
                return false;
            }

            GiveItemBlueprint(player, itemID);

            return true;
        }

        bool TryGetRandomUnlearnedBlueprint(BasePlayer player, BPType type, out int itemID)
        {
            itemID = -1;
            var playerInventoryBlueprintItemNames = GetBlueprintItemNamesInPlayerInventory(player);

            List<ItemDefinition> unlearnedItems = DefaultSettings_BlueprintTiers.Instance.BPGroups[type]
                .Select(itemId => ItemManager.FindItemDefinition(itemId))
                .Where(itemDef => itemDef != null)
                .Where(itemDef => !playerInventoryBlueprintItemNames.Contains(itemDef.itemid))
                .Where(itemDef => !database.HasBlueprint(player, itemDef.itemid))
                .ToList();

            if (!unlearnedItems.Any())
            {
                return false;
            }

            var randomIndex = UnityEngine.Random.Range(0, unlearnedItems.Count);

            var item = unlearnedItems[randomIndex];

            if (item == null)
            {
                return false;
            }

            itemID = item.itemid;
            return true;
        }

        int GetRandomBlueprint(BPType type)
        {
            string itemName = DefaultSettings_BlueprintTiers.Instance.BPGroups[type][UnityEngine.Random.Range(0, DefaultSettings_BlueprintTiers.Instance.BPGroups[type].Count)];
            var itemDef = ItemManager.FindItemDefinition(itemName);
            if (itemDef == null)
            {
                return -1;
            }

            return itemDef.itemid;
        }

        HashSet<int> GetBlueprintItemNamesInPlayerInventory(BasePlayer player)
        {
            HashSet<int> inventoryBlueprints = new HashSet<int>();

            foreach (var item in player.inventory.AllItems())
            {
                if (item.info.shortname == "blueprintbase")
                {
                    if (!inventoryBlueprints.Contains(item.blueprintTarget))
                    {
                        inventoryBlueprints.Add(item.blueprintTarget);
                    }
                }
            }

            return inventoryBlueprints;
        }

        Item GetRandomItemBlueprint(BPType type)
        {
            return GetItemBlueprint(GetRandomBlueprint(type));
        }

        Item GetItemBlueprint(int id)
        {
            Item itemBlueprint = ItemManager.CreateByName("blueprintbase");
            itemBlueprint.blueprintTarget = id;
            return itemBlueprint;
        }

        public void RevealBlueprint(BasePlayer player, Item item)
        {
            BPType type = GetBPType(item);
            if (type == BPType.Frags)
            {
                if (item.amount < 20)
                {
                    return;
                }
            }
            if (!RevealBlueprint(player, type))
            {
                return;
            }

            item.amount -= (type == BPType.Frags ? 20 : 1);

            if (item.amount <= 0)
            {
                item.Remove();
            }
            item.MarkDirty();
        }

        void TryMoveItem(Item item, ItemContainer container, int slot = -1)
        {
            if (!item.MoveToContainer(container))
            {
                item.Drop(container.dropPosition, container.dropVelocity);
            }
        }

        void GiveItemBlueprint(BasePlayer player, int itemID)
        {
            ItemDefinition itemDefinition = ItemManager.itemList.Find((ItemDefinition x) => x.itemid == itemID);
            GiveItemBlueprint(player, itemDefinition.shortname);
        }

        void GiveItemBlueprint(BasePlayer player, string itemName)
        {
            Item itemBlueprint = ItemManager.CreateByName("blueprintbase");
            ItemDefinition itemDefinition = ItemManager.itemList.Find((ItemDefinition x) => x.shortname == itemName);
            if (itemDefinition == null)
            {
                Puts("GiveItemBlueprint() couldnt find itemID based on name!");
                return;
            }
            itemBlueprint.blueprintTarget = itemDefinition.itemid;

            foreach (var currentSlot in player.inventory.containerMain.itemList)
            {
                if (currentSlot.info.itemid == itemBlueprint.info.itemid)
                {
                    if (currentSlot.blueprintTarget == itemBlueprint.blueprintTarget)
                    {
                        TryMoveItem(itemBlueprint, player.inventory.containerMain, currentSlot.position);
                        return;
                    }
                }
            }
            TryMoveItem(itemBlueprint, player.inventory.containerMain);
        }

        bool LearnBlueprint(BasePlayer player, int itemID)
        {
            return database.LearnBlueprint(player, itemID);
        }

        bool DropBlueprint(Item item, DroppedItem originalItem = null)
        {
            BPType type = GetBPType(item);
            if (type == BPType.None)
            {
                return false;
            }
            item.Remove();
            Item itemToDrop = ItemManager.CreateByName("blueprintbase", item.amount);
            itemToDrop.name = GetBPName(type);
            itemToDrop.blueprintTarget = (int)type;
            ItemContainer container = item.GetRootContainer();
            if (container == null)
            {
                itemToDrop.Drop(originalItem.transform.position, Vector3.zero);
                return true;
            }
            itemToDrop.Drop(container.dropPosition, container.dropVelocity);
            return true;
        }

        public List<ResearchTable> GetResearchTables()
        {
            return GameObject.FindObjectsOfType<ResearchTable>().ToList();
        }

        #endregion

        #region Display Messages

        private Dictionary<BasePlayer, Timer> messageTimers { get; set; } = new Dictionary<BasePlayer, Timer>();

        private CuiElementContainer messagebox = new CuiElementContainer();
        private CuiLabel messageLabel { get; set; }

        void DisplayMessage(BasePlayer player, string message, float time = 2.5f)
        {
            //PrintToChat(player, message);
            CloseMessage(player);
            messageTimers[player] = this.timer.In(time, () =>
            {
                DestroyUI(player, messagebox);
            });

            messageLabel.Text.Text = message;
            CuiHelper.AddUi(player, messagebox);
        }

        void SetupUI()
        {
            messageLabel = new CuiLabel();
            messagebox.Add(messageLabel, "Hud.Menu");
            messageLabel.RectTransform.AnchorMin = "0.64 0.02";
            messageLabel.RectTransform.AnchorMax = "0.84 0.14";
            messageLabel.Text.Align = TextAnchor.MiddleCenter;
            messageLabel.Text.FontSize = 20;
            var panel = new CuiPanel();
            panel.RectTransform.AnchorMin = messageLabel.RectTransform.AnchorMin;
            panel.RectTransform.AnchorMax = messageLabel.RectTransform.AnchorMax;
            panel.Image.Color = "0 0 0 0.4";
            messagebox.Add(panel, "Hud.Menu");
            /*
            foreach(var element in messagebox)
            {
                element.FadeOut = 1f;
            }*/

            SetupResearchUI();
        }

        void CloseMessage(BasePlayer player)
        {
            Timer timer;
            if (messageTimers.TryGetValue(player, out timer))
            {
                timer?.Destroy();
                messageTimers.Remove(player);
            }
            DestroyUI(player, messagebox);
        }

        #endregion

        #region Lang API

        public Dictionary<string, string> LangAPI = new Dictionary<string, string>()
        {
            { "ResearchBench_TopInfoPanel", "Drop items to add them to the research bench." },
            { "ResearchBench_SourceLabel", "Source Item." },
            { "ResearchBench_FragmentLabel", "Fragment Boost" },
            { "ResearchBench_FragmentBoost", "Adding fragments increases your chance of success." },
            { "ResearchBench_ChanceOfSuccessLabel", "Chance of success" },
            { "ResearchBench_ChanceInfo", "Increase the chance of success by adding frags or a better source item." },
            { "ResearchBench_ExitLabel", "Exit" },
            { "ResearchBench_StartResearch", "BEGIN RESEARCH" },
            { "ResearchBench_OpenIcon", "OPEN" },
            { "ComponentsDisabled", "Components are disabled in the Blueprint System!" },
            { "AdminCommand", "This command is for admins only." },

        };

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(LangAPI, this);
        }

        #endregion  

        #region UI

        void DestroyUI(BasePlayer player, CuiElementContainer container)
        {
            foreach (var element in container)
            {
                CuiHelper.DestroyUi(player, element.Name);
            }
        }

        #endregion

        #region Hooks

        object OnOvenFull(BaseOven oven)
        {
            return true;
        }

        void CanBuild(Planner planner, Construction construction, Vector3 position)
        {
            if (Settings.buildingPriv_UnNerf)
            {
                construction.canBypassBuildingPermission = true;
            }
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is ResearchTable)
            {
                if (monumentResearchBenches.Contains(entity))
                {
                    return true;
                }

                return null;
            }

            if (info.InitiatorPlayer == null)
            {
                return null;
            }

            if (info.WeaponPrefab != null)
            {
                //Puts($"Weapon: {info.WeaponPrefab.ShortPrefabName} {info.damageTypes.Total()}");
            }

            bool isArrowProjectile = info.ProjectilePrefab != null && info.ProjectilePrefab.name.Contains("arrow_");

            #region Softside Doors

            if (entity is DecayEntity && (info.ProjectilePrefab == null)) // Is decay entity and NOT an arrow projectile -- Arrows handled elsewhere
            {
                Vector3 hitDirection = info.InitiatorPlayer.eyes.position - info.HitPositionWorld;

                var field = typeof(BaseCombatEntity).GetField("propDirection", BindingFlags.Instance | BindingFlags.NonPublic);

                var propDirection = (DirectionProperties[])field.GetValue(entity);

                for (int i = 0; i < propDirection.Length; i++)
                {
                    if (propDirection[i].extraProtection != null)
                    {
                        if (entity.PrefabName.StartsWith("assets/prefabs/building/door.hinged") && info.HitNormalLocal.z < 0) // Weak Side Single Door
                        {
                            //Puts($"Hit Weak Side Single Door");
                            info.damageTypes.ScaleAll(10f);
                        }
                        else if (entity.PrefabName.StartsWith("assets/prefabs/building/door.double.hinged") && info.HitNormalLocal.x > 0)
                        {
                            //Puts($"Hit Weak Side Double Door");
                            info.damageTypes.ScaleAll(10f);
                        }
                        else if (entity.PrefabName == "assets/prefabs/building/floor.ladder.hatch/floor.ladder.hatch.prefab" && info.HitNormalLocal.y < 0)
                        {
                            // Puts($"Hit Hatch Weak Side");
                            info.damageTypes.ScaleAll(10f);
                        }
                    }
                }
            }

            #endregion

            if (info.ProjectilePrefab != null)
            {
                #region Un-nerf spears
                if (info.ProjectilePrefab.name.Contains("_spear"))
                {
                    if (Settings.spear_UnNerf)
                    {
                        info.damageTypes.ScaleAll(2f / 1.5f);
                    }
                    return null;
                }
                #endregion

                #region Un-nerf P250

                if (Settings.p250DamageBuff)
                {
                    if (info.WeaponPrefab.ShortPrefabName.Contains("pistol_semiauto.entity"))
                    {
                        if (info.isHeadshot)
                        {
                            info.damageTypes.ScaleAll(1.3f);
                        }
                        else
                        {
                            info.damageTypes.ScaleAll(1.10f);
                        }
                    }
                }

                #endregion

                if (info.ProjectilePrefab.name.Contains("arrow_"))
                {
                    if (entity is DecayEntity)
                    {
                        #region Arrow raiding
                        if (entity.PrefabName == "assets/prefabs/building/door.hinged/door.hinged.wood.prefab" || entity.PrefabName == "assets/prefabs/building/door.double.hinged/door.double.hinged.wood.prefab")
                        {
                            entity.baseProtection.amounts[(int)DamageType.Arrow] = Settings.arrowRaiding ? 0.96f : 1f;
                        }
                        if (entity is BuildingBlock)
                        {
                            BuildingBlock block = entity as BuildingBlock;
                            switch (block.grade)
                            {
                                case BuildingGrade.Enum.Wood:
                                    {
                                        entity.baseProtection.amounts[(int)DamageType.Arrow] = Settings.arrowRaiding ? 0.96f : 1f;
                                        break;
                                    }
                                case BuildingGrade.Enum.Stone:
                                    {
                                        entity.baseProtection.amounts[(int)DamageType.Arrow] = Settings.arrowRaiding ? 0.99f : 1f;
                                        break;
                                    }
                            }
                        }
                        #endregion
                    }

                    else if (entity is BasePlayer)
                    {
                        #region Arrow Un-Nerf
                        if (info.isHeadshot)
                        {
                            float num = info.ProjectilePrefab.damageMultipliers.Lerp(info.ProjectilePrefab.modifier.distanceOffset + info.ProjectilePrefab.modifier.distanceScale * info.ProjectilePrefab.damageDistances.x, info.ProjectilePrefab.modifier.distanceOffset + info.ProjectilePrefab.modifier.distanceScale * info.ProjectilePrefab.damageDistances.y, info.ProjectileDistance);
                            float num2 = info.ProjectilePrefab.integrity * (info.ProjectilePrefab.modifier.damageOffset + info.ProjectilePrefab.modifier.damageScale * num);
                            info.damageTypes.ScaleAll(1f / num2);
                            if (info.WeaponPrefab.ShortPrefabName == "crossbow.entity")
                            {
                                if (Settings.crossbow_UnNerf)
                                {
                                    info.damageTypes.ScaleAll(2f / 1.5f);
                                    //Puts($"{info.damageTypes.GetMajorityDamageType()} {info.damageTypes.Total()}");
                                }
                            }
                            else if (info.WeaponPrefab.ShortPrefabName == "bow_hunting.entity")
                            {
                                if (Settings.huntingBow_UnNerf)
                                {
                                    info.damageTypes.ScaleAll(2f / 1.5f);
                                    //Puts($"{info.damageTypes.GetMajorityDamageType()} {info.damageTypes.Total()}");
                                }
                            }
                        }
                        #endregion
                    }
                }
            }
            return null;
        }
        
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.IsDown(BUTTON.USE))
            {
                if (!input.WasDown(BUTTON.USE))
                {
                    var data = GetResearchData(player);
                    if (data.lookingAtTable)
                    {
                        if (!data.usingResearchTable)
                        {
                            StartResearch(player);
                        }
                    }
                }
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            HandleManagedComponents(player, removeComponents: true, giveComponents: true);
        }

        object CanCraft(ItemCrafter crafter, ItemBlueprint blueprint, int amount)
        {
            BasePlayer player = crafter.GetComponentInParent<BasePlayer>();
            if (Settings.itemLevels[blueprint.targetItem.shortname] == BPType.Default)
            {
                return null;
            }
            if (!database.HasBlueprint(player, blueprint.targetItem.itemid))
            {
                DisplayMessage(player, "You haven't learned that yet!");
                return false;
            }
            return null;
        }

        object OnItemAction(Item item, string cmd)
        {
            BPType type = GetBPType(item);

            BasePlayer player = item.GetOwnerPlayer();

            var data = GetResearchData(player);

            #region Let normal items use their commands
            switch (cmd)
            {
                case "upgrade_item":
                case "unwrap":
                case "drop":
                    {
                        if (player != null)
                        {
                            if (IsResearching(player))
                            {
                                if (!data.isResearchingItem)
                                {
                                    OnResearchItemDropped(player, item);
                                }
                                return true;
                            }
                        }
                        if (type == BPType.None)
                        {
                            return null;
                        }
                        break;
                    }
            }
            #endregion

            switch (cmd)
            {
                case "upgrade_item":
                    {
                        if (player == null)
                        {
                            return true;
                        }
                        CombineFrags(item, type);
                        break;
                    }
                case "unwrap":
                    {
                        if (player == null)
                        {
                            return true;
                        }
                        RevealBlueprint(player, item);
                        break;
                    }
                case "craft":
                case "craft_all":
                    {
                        if (!LearnBlueprint(player, item.blueprintTarget))
                        {
                            return true; //Don't consume item if learning fails
                        }
                        DisplayMessage(player, $"You learned {ItemManager.itemList.First(x => x.itemid == item.blueprintTarget).displayName.translated}!", 4f);
                        break;
                    }
                case "drop":
                    {
                        if (DropBlueprint(item))
                        {
                            item.Remove();
                            ItemManager.DoRemoves();
                        }
                        return true;
                    }
                default:
                    {
                        return null;
                    }
            }

            #region Decrease item count by 1

            switch (cmd)
            {
                case "craft":
                case "craft_all":
                    {
                        item.amount -= 1;
                        if (item.amount <= 0)
                        {
                            item.Remove();
                        }
                        item.MarkDirty();
                        break;
                    }
            }

            #endregion

            return true;
        }

        object CanStackItem(Item slotItem, Item item2)
        {
            //Puts($"CanStack {slotItem != null} {slotItem.blueprintTarget} {slotItem.IsBlueprint()} {slotItem.info.shortname} {item2 != null} {item2.blueprintTarget} {item2.IsBlueprint()} {item2.info.shortname}");
            if (slotItem.skin != item2.skin)
            {
                //Puts("CanStack false");
                return false;
            }
            if (slotItem.blueprintTarget == item2.blueprintTarget && slotItem.IsBlueprint() && item2.IsBlueprint())
            {
                //Puts("CanStack blueprints");
                return true;
            }
            return null;
        }

        object OnItemSplit(Item item, int amount)
        {
            if (item.skin != 0)
            {
                item.amount -= amount;
                Item newItem = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
                newItem.amount = amount;
                if (newItem.IsBlueprint())
                {
                    newItem.blueprintTarget = item.blueprintTarget;
                }
                item.MarkDirty();
                return newItem;
            }
            return null;
        }

        void OnItemCraftCancelled(ItemCraftTask task)
        {
            foreach (Item item in task.takenItems.ToList())
            {
                if (componentList.Contains(item.info.shortname))
                {
                    task.takenItems.Remove(item);
                    item.Remove();
                }
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!serverInitialized)
            {
                return;
            }
            if (entity is SupplyDrop)
            {
                SupplyDrop drop = (SupplyDrop)entity;
                Rigidbody rigidbody = drop.GetComponent<Rigidbody>();
                if (rigidbody)
                {
                    rigidbody.drag *= .65f;
                }
            }
            if (entity is ResearchTable)
            {
                if (entity.gameObject.GetComponent<ResearchTableCollider>() == null)
                {
                    entity.gameObject.AddComponent<ResearchTableCollider>();
                }
            }
            else if (IsLootContainer(entity))
            {
                LootContainer container = entity as LootContainer;
                container.lootDefinition = null;
                NextTick(() =>
                {
                    AssignLoot(container);
                    container.minSecondsBetweenRefresh = -1;
                    container.maxSecondsBetweenRefresh = 0;
                    container.CancelInvoke(container.SpawnLoot);
                });
            }
            else if (entity is DroppedItem) //Blueprints dropped from barrels would be shown as presents by default
            {
                DroppedItem drop = entity as DroppedItem;
                BPType type = GetBPType(drop.item);
                if (type == BPType.None)
                {
                    return;
                }
                DropBlueprint(drop.item, drop);
                if (!entity.IsDestroyed)
                {
                    entity.Kill();
                }
            }
            else if (entity is FireBall)
            {
                //Puts("Fireball spawned!");
                FireBall fire = entity as FireBall;
                fire.lifeTimeMin = 50f;
                fire.lifeTimeMax = 60f;
            }
            else if (entity is SleepingBag)
            {
                NextFrame(() =>
                {
                    var field = typeof(SleepingBag).GetField("unlockTime", BindingFlags.Instance | BindingFlags.NonPublic);
                    field.SetValue((entity as SleepingBag), Time.realtimeSinceStartup + 90f);
                });
            }
            else if (entity is CollectibleEntity)
            {
                TryChangeHemp(entity as CollectibleEntity);
            }
        }

        void OnItemPickup(ref Item item, BasePlayer player)
        {
            if ((BPType)item.blueprintTarget < BPType.Frags || (BPType)item.blueprintTarget > BPType.Library)
            {
                return;
            }
            WorldItem worldItem = item.GetWorldEntity() as WorldItem;
            if (worldItem == null)
            {
                Puts("OnItemPickup() WorldItem is null");
                return;
            }
            worldItem.item = CreateBlueprint((BPType)item.blueprintTarget, item.amount);
            NextFrame(() =>
            {
                worldItem.Kill();
            });
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item.info.shortname == "pistol.revolver")
            {
                var gun = item.GetHeldEntity() as BaseProjectile;
                if (gun != null)
                {
                    gun.primaryMagazine.capacity = Settings.pistol_Nerf ? 6 : 8;
                }
            }
            else if (item.info.shortname == "pistol.semiauto")
            {
                var gun = item.GetHeldEntity() as BaseProjectile;
                if (gun != null)
                {
                    gun.primaryMagazine.capacity = Settings.pistol_Nerf ? 8 : 10; ;
                }
            }
            BPType type = GetBPType(item);
            if (type == BPType.None)
            {
                return;
            }
            item.name = GetBPName(type);
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            OnPlayerInit(player);
        }

        void OnPlayerWound(BasePlayer player)
        {
            StopResearch(player);
        }

        void OnPlayerDie(BasePlayer player)
        {
            StopResearch(player);
        }

        object OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity is ResearchTable)
            {
                player.EndLooting();
                StartResearch(player);
                return true;
            }
            else
            {
                StopResearch(player);
            }
            return null;
        }

        #endregion

        #region General Functions

        void CloseInventory(BasePlayer player)
        {
            player.ClientRPCPlayer(null, player, "OnDied"); //I dont get how the fuck this works, but it does
        }

        public bool GetEntityLookingAt(BasePlayer player, out BaseEntity entity, int mask = 3)
        {
            entity = null;
            RaycastHit raycastHit;
            Ray ray = player.eyes.BodyRay();

            if (!Physics.Raycast(ray, out raycastHit, 4f, mask))
            {
                return false;
            }
            else
            {
                entity = raycastHit.GetEntity();
                if (entity != null)
                {
                    return true;
                }

            }
            return false;
        }

        #endregion

        #region Chat Commands

        [ChatCommand("frags")]
        void VisualDebugging(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                lang.GetMessage("AdminCommand", this, player.UserIDString);
                return;
            }

            AddBlueprints(player.inventory.containerMain, BPType.Frags, 1000);
            AddBlueprints(player.inventory.containerMain, BPType.Page, 100);
            AddBlueprints(player.inventory.containerMain, BPType.Book, 100);
            AddBlueprints(player.inventory.containerMain, BPType.Library, 100);
        }

        [ChatCommand("resetbps")]
        void ResetBps(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                lang.GetMessage("AdminCommand", this, player.UserIDString);
                return;
            }
            if (args.Length != 2)
            {
                PrintToChat(player, "Need 2 arguments!");
                return;
            }

            ulong userID;
            if (!ulong.TryParse(args[0], out userID))
            {
                PrintToChat(player, $"{args[0]} is not a valid userID!");
                return;
            }

            int type;
            if (!int.TryParse(args[1], out type))
            {
                PrintToChat(player, $"{args[0]} is not a valid BPType!");
                return;
            }

            database.ClearBlueprints(userID, (BPType)type);
        }

        [ChatCommand("placebench")]
        void PlaceResearchBench_Command(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                lang.GetMessage("AdminCommand", this, player.UserIDString);
                return;
            }
            PlaceResearchBench(player);
        }

        [ChatCommand("reloadblueprintsettings")]
        void ReloadSettingsCommand(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                lang.GetMessage("AdminCommand", this, player.UserIDString);
                return;
            }
            ReloadAllSettings();
            ApplySettings();
        }

        [ChatCommand("resetblueprintloot")]
        void ResetLootCommand(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                lang.GetMessage("AdminCommand", this, player.UserIDString);
                return;
            }
            foreach (var ent in GameObject.FindObjectsOfType<LootContainer>())
            {
                if (!ent.IsDestroyed)
                {
                    ent.Kill();
                }
            }
            rust.RunServerCommand("spawn.fill_populations");
            rust.RunServerCommand("spawn.fill_groups");
        }

        #endregion

        #region Recycling

        object CanRecycle(Recycler recycler, Item item)
        {
            if (item.info.shortname.Contains("xmas.present"))
            {
                BPType currentType = GetBPType(item);
                if (currentType != BPType.None && currentType != BPType.Frags)
                {
                    return true;
                }
            }
            else if (item.info.shortname == "roadsigns")
            {
                if (Settings.blockRecyclingRoadsigns)
                {
                    return false;
                }
            }
            else if (componentList.Contains(item.info.shortname)) //No recycling components (prevent component duping)
            {
                return false;
            }
            return null;
        }

        object OnRecycleItem(Recycler recycler, Item item)
        {
            bool shouldOverride = false;
            if (item.info.shortname.Contains("xmas.present"))
            {
                BPType currentType = GetBPType(item);
                if (currentType != BPType.None && currentType != BPType.Frags)
                {
                    int amount = Mathf.Clamp(item.amount, 1, 2);
                    item.UseItem(amount);
                    ItemManager.DoRemoves();
                    BPType newType = currentType - 1;
                    Item newItem = CreateBlueprint(newType, GetBPAmountToCombine(newType) * amount);
                    recycler.MoveItemToOutput(newItem);
                }
            }
            else if (item.info.shortname == "roadsigns")
            {
                if (Settings.blockRecyclingRoadsigns)
                {
                    recycler.StopRecycling();
                    return true;
                }
            }
            else if (componentList.Contains(item.info.shortname))
            {
                recycler.StopRecycling();
                return true;
            }
            if (item.info.Blueprint != null)
            {
                if (item.info.Blueprint.ingredients.Any(x => componentList.Contains(x?.itemDef?.shortname))) //If item contains a component we give unlimited of (this technically only applys to uncraftables)
                {
                    shouldOverride = true; //Prevent components from being given after recycled (need to override)
                    foreach (var itemAmount in item.info.Blueprint.ingredients)
                    {
                        if (!componentList.Contains(itemAmount.itemDef.shortname))
                        {
                            recycler.MoveItemToOutput(ItemManager.Create(itemAmount.itemDef, Mathf.CeilToInt(itemAmount.amount * recycler.recycleEfficiency))); //Give normal items
                            continue;
                        }
                        foreach (var componentIngredient in itemAmount.itemDef.Blueprint.ingredients) //Directly convert components into sub materials
                        {
                            Item newItem = ItemManager.Create(componentIngredient.itemDef, Mathf.CeilToInt((componentIngredient.amount * recycler.recycleEfficiency)) * Mathf.CeilToInt(itemAmount.amount * recycler.recycleEfficiency), 0uL);
                            recycler.MoveItemToOutput(newItem);
                        }
                    }
                    item.UseItem();
                }
            }
            if (shouldOverride)
            {
                return true;
            }
            return null;
        }

        void SendToOutput(Recycler recycler, string shortname, int amount)
        {
            Item item = ItemManager.CreateByName(shortname, amount);
            if (item != null)
            {
                recycler.MoveItemToOutput(item);
            }
        }

        #endregion

        #region Jake's UI Framework

        private Dictionary<string, UIButton> UIButtonCallBacks { get; set; } = new Dictionary<string, UIButton>();

        void OnButtonClick(ConsoleSystem.Arg arg)
        {
            UIButton button;
            if (UIButtonCallBacks.TryGetValue(arg.cmd.Name, out button))
            {
                button.OnClicked(arg);
                return;
            }
            Puts("Unknown button command: {0}", arg.cmd.Name);
        }

        public class UIElement : UIBaseElement
        {
            public CuiElement Element { get; protected set; }
            public CuiRectTransformComponent transform { get; protected set; }

            public string Name { get { return Element.Name; } }

            public Func<BasePlayer, bool> conditionalShow { get; set; }

            public Func<BasePlayer, Vector2> conditionalSize { get; set; }

            public UIElement(UIBaseElement parent = null) : base(parent)
            {

            }

            public UIElement(Vector2 position, float width, float height, UIBaseElement parent = null) : this(position, new Vector2(position.x + width, position.y + height), parent)
            {

            }

            public UIElement(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent)
            {
                transform = new CuiRectTransformComponent();
                Element = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = this.parent == null ? this.Parent : this.parent.Parent,
                    Components =
                        {
                            transform
                        }
                };
                UpdatePlacement();

                Init();
            }

            public virtual void Init()
            {

            }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (conditionalShow != null)
                {
                    if (!conditionalShow(player))
                    {
                        return;
                    }
                }

                if (conditionalSize != null)
                {
                    Vector2 returnSize = conditionalSize.Invoke(player);
                    if (returnSize != null)
                    {
                        SetSize(returnSize.x, returnSize.y);
                    }
                }
                if (AddPlayer(player))
                {
                    SafeAddUi(player, Element);
                }
                base.Show(player, children);
            }

            public override void Hide(BasePlayer player, bool children = true)
            {
                base.Hide(player, children);
                if (RemovePlayer(player))
                {
                    SafeDestroyUi(player, Element);
                }
            }

            public override void UpdatePlacement()
            {
                base.UpdatePlacement();
                transform.AnchorMin = $"{globalPosition.x} {globalPosition.y}";
                transform.AnchorMax = $"{globalPosition.x + globalSize.x} {globalPosition.y + globalSize.y}";
                RefreshAll();
            }

            public void SetPositionAndSize(CuiRectTransformComponent trans)
            {
                transform.AnchorMin = trans.AnchorMin;
                transform.AnchorMax = trans.AnchorMax;

                //_plugin.Puts($"POSITION [{transform.AnchorMin},{transform.AnchorMax}]");

                RefreshAll();
            }

            public void SetParent(UIElement element)
            {
                Element.Parent = element.Element.Name;
            }

        }

        public class UIButton : UIElement
        {
            public CuiButtonComponent buttonComponent { get; private set; }
            public CuiTextComponent textComponent { get; private set; }
            private UILabel label { get; set; }
            private string _textColor { get; set; }
            private string _buttonText { get; set; }

            private int _fontSize;

            public Action<ConsoleSystem.Arg> onClicked;

            public UIButton(Vector2 min = default(Vector2), Vector2 max = default(Vector2), string buttonText = "", string buttonColor = "0 0 0 0.85", string textColor = "1 1 1 1", int fontSize = 15, UIBaseElement parent = null) : base(min, max, parent)
            {
                buttonComponent = new CuiButtonComponent();

                _fontSize = fontSize;
                _textColor = textColor;
                _buttonText = buttonText;

                buttonComponent.Command = CuiHelper.GetGuid();
                buttonComponent.Color = buttonColor;

                Element.Components.Insert(0, buttonComponent);

                _plugin.cmd.AddConsoleCommand(buttonComponent.Command, _plugin, "OnButtonClick");

                _plugin.UIButtonCallBacks[buttonComponent.Command] = this;

                label = new UILabel(new Vector2(0, 0), new Vector2(1, 1), fontSize: _fontSize, parent: this);

                textComponent = label.text;

                label.text.Align = TextAnchor.MiddleCenter;
                label.text.Color = _textColor;
                label.Text = _buttonText;
                label.text.FontSize = _fontSize;

            }

            public override void Init()
            {
                base.Init();

            }

            public virtual void OnClicked(ConsoleSystem.Arg args)
            {
                onClicked.Invoke(args);
            }

            public void AddChatCommand(string fullCommand)
            {
                if (fullCommand == null)
                {
                    return;
                }
                /*
                List<string> split = fullCommand.Split(' ').ToList();
                string command = split[0];
                split.RemoveAt(0); //Split = command args now*/
                onClicked += (arg) =>
                {
                    _plugin.rust.RunClientCommand(arg.Player(), $"chat.say \"/{fullCommand}\"");
                    //plugin.Puts($"Calling chat command {command} {string.Join(" ",split.ToArray())}");
                    //Need to call chat command somehow here
                };
            }

            public void AddCallback(Action<BasePlayer> callback)
            {
                if (callback == null)
                {
                    return;
                }
                onClicked += (args) => { callback(args.Player()); };
            }
        }

        public class UILabel : UIElement
        {
            public CuiTextComponent text { get; private set; }

            public UILabel(Vector2 min = default(Vector2), Vector2 max = default(Vector2), string labelText = "", int fontSize = 12, string fontColor = "1 1 1 1", UIBaseElement parent = null, TextAnchor alignment = TextAnchor.MiddleCenter) : base(min, max, parent)
            {

                if (min == Vector2.zero && max == Vector2.zero)
                {
                    max = Vector2.one;
                }

                text = new CuiTextComponent();

                text.Text = labelText;
                text.Color = fontColor;
                text.Align = alignment;
                text.FontSize = fontSize;

                Element.Components.Insert(0, text);
            }

            public string Text { set { text.Text = value; } }
            public TextAnchor Allign { set { text.Align = value; } }
            public Color Color { set { text.Color = value.ToString(); } }
            public string ColorString { set { text.Color = value; } }

            public Func<BasePlayer, string> variableText { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableText != null)
                {
                    Text = variableText.Invoke(player);
                }
                base.Show(player, children);
            }

            public override void Init()
            {
                base.Init();

                if (parent != null)
                {
                    if (parent is UIButton)
                    {
                        Element.Parent = (parent as UIButton).Name;
                        transform.AnchorMin = $"{position.x} {position.y}";
                        transform.AnchorMax = $"{position.x + size.x} {position.y + size.y}";
                    }
                }
            }

        }

        public class UIImageBase : UIElement
        {
            public UIImageBase(Vector2 min, Vector2 max, UIBaseElement parent) : base(min, max, parent)
            {
            }

            private CuiNeedsCursorComponent needsCursor { get; set; }

            private bool requiresFocus { get; set; }

            public bool CursorEnabled
            {
                get
                {
                    return requiresFocus;
                }
                set
                {
                    if (value)
                    {
                        needsCursor = new CuiNeedsCursorComponent();
                        Element.Components.Add(needsCursor);
                    }
                    else
                    {
                        Element.Components.Remove(needsCursor);
                    }

                    requiresFocus = value;
                }
            }
        }

        public class UIPanel : UIImageBase
        {
            private CuiImageComponent panel;

            public UIPanel(Vector2 min, Vector2 max, string color = "0 0 0 .85", UIBaseElement parent = null) : base(min, max, parent)
            {
                panel = new CuiImageComponent
                {
                    Color = color
                };

                Element.Components.Insert(0, panel);
            }

            public UIPanel(Vector2 position, float width, float height, UIBaseElement parent = null, string color = "0 0 0 .85") : this(position, new Vector2(position.x + width, position.y + height), color, parent)
            {

            }
        }

        public class UIButtonContainer : UIBaseElement
        {
            private IEnumerable<UIButtonConfiguration> _buttonConfiguration;
            private Vector2 _position;
            private float _width;
            private float _height;
            private string _title;
            private string _panelColor;
            private bool _stackedButtons;
            private float _paddingPercentage;
            private int _titleSize;
            private int _buttonFontSize;


            const float TITLE_PERCENTAGE = 0.20f;

            private float _paddingAmount;
            private bool _hasTitle;

            public UIButtonContainer(IEnumerable<UIButtonConfiguration> buttonConfiguration, string panelBgColor, Vector2 position, float width, float height, float paddingPercentage = 0.05f, string title = "", int titleSize = 30, int buttonFontSize = 15, bool stackedButtons = true, UIBaseElement parent = null) : base(parent)
            {
                _buttonConfiguration = buttonConfiguration;
                _position = position;
                _width = width;
                _height = height;
                _title = title;
                _titleSize = titleSize;
                _panelColor = panelBgColor;
                _stackedButtons = stackedButtons;
                _paddingPercentage = paddingPercentage;
                _buttonFontSize = buttonFontSize;

                Init();
            }

            private void Init()
            {
                var panel = new UIPanel(new Vector2(_position.x, _position.y), _width, _height, this, _panelColor);

                _paddingAmount = (_stackedButtons ? _height : _width) * _paddingPercentage / _buttonConfiguration.Count();

                var firstButtonPosition = new Vector2(_position.x + _paddingAmount, _position.y + _paddingAmount);
                var titleHeight = TITLE_PERCENTAGE * _height;

                if (!string.IsNullOrEmpty(_title))
                {
                    _hasTitle = true;

                    var titlePanel = new UIPanel(new Vector2(_position.x, _position.y + _height - titleHeight), _width, titleHeight, this);
                    var titleLabel = new UILabel(Vector2.zero, Vector2.zero, _title, fontSize: _titleSize, parent: titlePanel);
                }

                var buttonHeight = (_height - (_paddingAmount * 2) - (_hasTitle ? titleHeight : 0) - (_paddingAmount * (_buttonConfiguration.Count() - 1))) / (_stackedButtons ? _buttonConfiguration.Count() : 1);
                var buttonWidth = _stackedButtons
                    ? (_width - (_paddingAmount * 2))
                    : ((_width - (_paddingAmount * 2) - (_paddingAmount * (_buttonConfiguration.Count() - 1))) / _buttonConfiguration.Count());

                for (var buttonId = 0; buttonId < _buttonConfiguration.Count(); buttonId++)
                {
                    var buttonConfig = _buttonConfiguration.ElementAt(buttonId);
                    var button = new UIButton(buttonText: buttonConfig.ButtonName, buttonColor: buttonConfig.ButtonColor, fontSize: _buttonFontSize);

                    if (!_stackedButtons)
                    {
                        button.SetPosition(
                            firstButtonPosition.x + ((buttonWidth + _paddingAmount) * buttonId + _paddingAmount),
                            firstButtonPosition.y + (_paddingAmount) * 2);
                    }
                    else
                    {
                        button.SetPosition(
                            firstButtonPosition.x,
                            firstButtonPosition.y + ((buttonHeight + _paddingAmount) * buttonId + _paddingAmount));
                    }

                    button.SetSize(
                        buttonWidth - (_stackedButtons ? 0 : _paddingAmount * 2),
                        buttonHeight - (_stackedButtons ? _paddingAmount * 2 : 0));

                    button.AddCallback(buttonConfig.callback);
                    button.AddChatCommand(buttonConfig.ButtonCommand);
                }
            }
        }

        public class UIButtonConfiguration
        {
            public string ButtonName { get; set; }
            public string ButtonCommand { get; set; }
            public string ButtonColor { get; set; }
            public Action<BasePlayer> callback { get; set; }
        }

        public class UIImage : UIImageBase
        {
            public CuiImageComponent Image { get; private set; }

            public UIImage(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent)
            {
                Image = new CuiImageComponent();
                Element.Components.Insert(0, Image);
            }

            public UIImage(Vector2 position, float width, float height, UIBaseElement parent = null) : this(position, new Vector2(position.x + width, position.y + height), parent)
            {
                Image = new CuiImageComponent();
                Element.Components.Insert(0, Image);
            }

            public Func<BasePlayer, string> variableSprite { get; set; }
            public Func<BasePlayer, string> variablePNG { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableSprite != null)
                {
                    Image.Sprite = variableSprite.Invoke(player);
                }
                if (variablePNG != null)
                {
                    Image.Png = variablePNG.Invoke(player);
                }
                base.Show(player, children);
            }
        }

        public class UIRawImage : UIImageBase
        {
            public CuiRawImageComponent Image { get; private set; }

            public UIRawImage(Vector2 position, float width, float height, UIBaseElement parent = null, string url = "") : this(position, new Vector2(position.x + width, position.y + height), parent, url)
            {

            }

            public UIRawImage(Vector2 min, Vector2 max, UIBaseElement parent = null, string url = "") : base(min, max, parent)
            {
                Image = new CuiRawImageComponent()
                {
                    Url = url,
                    Sprite = "assets/content/textures/generic/fulltransparent.tga"
                };

                Element.Components.Insert(0, Image);
            }

            public Func<BasePlayer, string> variablePNG { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variablePNG != null)
                {
                    Image.Png = variablePNG.Invoke(player);
                }
                base.Show(player, children);
            }
        }

        public class UIBaseElement
        {
            public Vector2 position { get; set; } = new Vector2();
            public Vector2 size { get; set; } = new Vector2();
            public Vector2 globalSize { get; set; } = new Vector2();
            public Vector2 globalPosition { get; set; } = new Vector2();
            public HashSet<BasePlayer> players { get; set; } = new HashSet<BasePlayer>();
            public UIBaseElement parent { get; set; }
            public HashSet<UIBaseElement> children { get; set; } = new HashSet<UIBaseElement>();
            public Vector2 min { get { return position; } }
            public Vector2 max { get { return position + size; } }
            public string Parent { get; set; } = "Hud.Menu";

            public UIBaseElement(UIBaseElement parent = null)
            {
                this.parent = parent;
            }

            public UIBaseElement(Vector2 min, Vector2 max, UIBaseElement parent = null) : this(parent)
            {
                position = min;
                size = max - min;
                if (parent != null)
                {
                    parent.AddElement(this);
                }
                if (!(this is UIElement))
                {
                    UpdatePlacement();
                }
            }

            public void AddElement(UIBaseElement element)
            {
                if (!children.Contains(element))
                {
                    children.Add(element);
                }
            }

            public void RemoveElement(UIBaseElement element)
            {
                children.Remove(element);
            }

            public void Refresh(BasePlayer player)
            {
                Hide(player);
                Show(player);
            }

            public bool AddPlayer(BasePlayer player)
            {
                if (!players.Contains(player))
                {
                    players.Add(player);
                    return true;
                }

                foreach (var child in children)
                {

                }

                return false;
            }

            public bool RemovePlayer(BasePlayer player)
            {
                return players.Remove(player);
            }

            public void Show(List<BasePlayer> players)
            {
                foreach (BasePlayer player in players)
                {
                    Show(player);
                }
            }

            public void Show(HashSet<BasePlayer> players)
            {
                foreach (BasePlayer player in players)
                {
                    Show(player);
                }
            }

            public virtual void Hide(BasePlayer player, bool hideChildren = true)
            {
                foreach (var child in children)
                {
                    child.Hide(player, hideChildren);
                }
            }

            public virtual void Show(BasePlayer player, bool showChildren = true)
            {
                foreach (var child in children)
                {
                    child.Show(player, showChildren);
                }
            }

            public void HideAll()
            {
                foreach (BasePlayer player in players.ToList())
                {
                    Hide(player);
                }
            }

            public void RefreshAll()
            {
                foreach (BasePlayer player in players.ToList())
                {
                    Refresh(player);
                }
            }

            public void SafeAddUi(BasePlayer player, CuiElement element)
            {
                try
                {
                    //_plugin.Puts($"Adding {element.Name} to {player.userID}");
                    List<CuiElement> elements = new List<CuiElement>();
                    elements.Add(element);
                    CuiHelper.AddUi(player, elements);
                }
                catch (Exception ex)
                {

                }
            }

            public void SafeDestroyUi(BasePlayer player, CuiElement element)
            {
                try
                {
                    //_plugin.Puts($"Deleting {element.Name} to {player.userID}");
                    CuiHelper.DestroyUi(player, element.Name);
                }
                catch (Exception ex)
                {

                }
            }

            public void SetSize(float x, float y)
            {
                size = new Vector2(x, y);
                UpdatePlacement();
            }

            public void SetPosition(float x, float y)
            {
                position = new Vector2(x, y);
                UpdatePlacement();
            }

            public virtual void UpdatePlacement()
            {
                if (parent == null)
                {
                    globalSize = size;
                    globalPosition = position;
                }
                else
                {
                    globalSize = Vector2.Scale(parent.globalSize, size);
                    globalPosition = parent.globalPosition + Vector2.Scale(parent.globalSize, position);
                }

                /*
                foreach (var child in children)
                {
                    _plugin.Puts("1.4");
                    UpdatePlacement();
                }*/
            }
        }

        public class UICheckbox : UIButton
        {
            public UICheckbox(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent: parent)
            {

            }
        }

        #endregion

        #region Timers

        public List<Timer> allTimers { get; set; } = new List<Timer>();

        public void SafeSetTimerRepeat(Timer Timer, float interval, int repeats, Action callback)
        {
            Timer?.Destroy();
            Timer = _plugin.timer.Repeat(interval, repeats, () =>
            {
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    Puts($"{callback.ToString()} ERROR:");
                    Puts(ex.ToString());
                }
            });
            allTimers.Add(Timer);
        }

        public void SafeSetTimerIn(Timer Timer, float seconds, Action callback)
        {
            Timer?.Destroy();
            Timer = _plugin.timer.In(seconds, callback);
            allTimers.Add(Timer);
        }

        public void SafeSetTimerEvery(Timer Timer, float seconds, Action callback)
        {
            Timer?.Destroy();
            Timer = _plugin.timer.Every(seconds, () =>
            {
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    Puts($"{callback.ToString()} ERROR:");
                    Puts(ex.ToString());
                }
            });
            allTimers.Add(Timer);
        }

        #endregion

        #region ImageLibrary

        [PluginReference]
        RustPlugin ImageLibrary;

        private string TryForImage(string shortname, ulong skin = 99, bool localimage = true)
        {
            if (localimage)
                if (skin == 99)
                    return GetImage(shortname, (ulong)ResourceId);
                else return GetImage(shortname, skin);
            else if (skin == 99)
                return GetImageURL(shortname, (ulong)ResourceId);
            else return GetImageURL(shortname, skin);
        }

        public string GetImageURL(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImageURL", shortname, skin);
        public uint GetTextureID(string shortname, ulong skin = 0) => (uint)ImageLibrary?.Call("GetTextureID", shortname, skin);
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        public bool HasImage(string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("HasImage", shortname, skin);
        public void TryAddImage(string url, string shortname, ulong skin = 0)
        {
            if (!HasImage(shortname, skin))
            {
                AddImage(url, shortname, skin);
            }
        }

        public List<ulong> GetImageList(string shortname) => (List<ulong>)ImageLibrary?.Call("GetImageList", shortname);

        #endregion

        #region Research Table And UI

        private UIBaseElement researchPanel { get; set; }

        private UILabel fragAmountLabel { get; set; }
        private UILabel successChanceLabel { get; set; }
        private UILabel researchCountdownLabel { get; set; }
        private UILabel sourceItemLabel { get; set; }
        private UIRawImage fragIconImage { get; set; }
        private UIRawImage itemIconImage { get; set; }
        private UIPanel itemDurabilityPanel { get; set; }
        public UIBaseElement openIconPanel { get; set; }

        public void SetupResearchUI()
        {
            openIconPanel = new UIRawImage(new Vector2(0.40f, 0.40f), new Vector2(0.6f, 0.6f));
            openIconPanel.Parent = "Hud";
            researchPanel = new UIBaseElement(new Vector2(0.64f, 0.49f), new Vector2(0.92f, 0.99f));
            UIPanel background = new UIPanel(new Vector2(0.00f, 0.00f), new Vector2(1f, 1f), "0.5 0.5 0.5 0", researchPanel);

            #region Panels
            string panelColor = "0.7 0.7 0.7 0.12";

            UIPanel topInfoPanel = new UIPanel(new Vector2(0, 0.85f), new Vector2(1, 1), panelColor, background);

            UIPanel itemResearchPanel = new UIPanel(new Vector2(0, 0.56f), new Vector2(0.28f, 0.84f), panelColor, background);

            UIPanel panel3 = new UIPanel(new Vector2(0.29f, 0.56f), new Vector2(1f, 0.84f), panelColor, background);

            UIPanel fragmentBoostInfoPanel = new UIPanel(new Vector2(0f, 0.27f), new Vector2(0.67f, 0.55f), panelColor, background);

            UIPanel fragmentPanel = new UIPanel(new Vector2(0.68f, 0.27f), new Vector2(1f, 0.55f), panelColor, background);

            UIPanel researchChancePanel = new UIPanel(new Vector2(0f, 0.11f), new Vector2(0.28f, 0.26f), panelColor, background);

            UIPanel chanceOfSuccessInfoPanel = new UIPanel(new Vector2(0.29f, 0.11f), new Vector2(1f, 0.26f), panelColor, background);

            UIPanel bottomPanel = new UIPanel(new Vector2(0f, 0f), new Vector2(1f, 0.10f), panelColor, background);

            itemDurabilityPanel = new UIPanel(new Vector2(0f, 0f), new Vector2(0.05f, 1f), "0.44 0.54 0.26 1", itemResearchPanel);

            #endregion

            #region Labels

            string textColor = "1 1 1 0.5";

            UILabel topLabel = new UILabel(new Vector2(0, 0), new Vector2(1, 1), lang.GetMessage("ResearchBench_TopInfoPanel", this), 18, textColor, topInfoPanel, TextAnchor.MiddleCenter);

            UILabel sourceLabel = new UILabel(new Vector2(0.02f, 0.02f), new Vector2(1, 0.98f), lang.GetMessage("ResearchBench_SourceLabel", this), 20, textColor, panel3, TextAnchor.UpperLeft);

            sourceItemLabel = new UILabel(new Vector2(0.02f, 0), new Vector2(1, 1), "", 12, textColor, panel3, TextAnchor.MiddleLeft);

            UILabel fragmentBoostInfoLabel1 = new UILabel(new Vector2(0.02f, 0.02f), new Vector2(1, 0.98f), lang.GetMessage("ResearchBench_FragmentLabel", this), 20, textColor, fragmentBoostInfoPanel, TextAnchor.UpperLeft);

            UILabel fragmentBoostInfoLabel2 = new UILabel(new Vector2(0.02f, 0), new Vector2(1, 1), lang.GetMessage("ResearchBench_FragmentBoost", this), 12, textColor, fragmentBoostInfoPanel, TextAnchor.MiddleLeft);

            fragAmountLabel = new UILabel(new Vector2(0, 0), new Vector2(1, 1), "x0", 12, textColor, fragmentPanel, TextAnchor.LowerRight);

            successChanceLabel = new UILabel(new Vector2(0, 0), new Vector2(1, 1), "0%", 40, textColor, researchChancePanel, TextAnchor.MiddleCenter);

            UILabel chanceOfSuccessInfoLabel1 = new UILabel(new Vector2(0.02f, 0.02f), new Vector2(1, 0.98f), lang.GetMessage("ResearchBench_ChanceOfSuccessLabel", this), 20, textColor, chanceOfSuccessInfoPanel, TextAnchor.UpperLeft);

            UILabel chanceOfSuccessInfoLabel2 = new UILabel(new Vector2(0.02f, 0), new Vector2(1, 1), lang.GetMessage("ResearchBench_ChanceInfo", this), 12, textColor, chanceOfSuccessInfoPanel, TextAnchor.LowerLeft);

            researchCountdownLabel = new UILabel(new Vector2(0.025f, 0f), new Vector2(0.1f, 1f), "", 24, "0.7 0.7 0.7 0.9", bottomPanel, TextAnchor.MiddleLeft);

            #endregion

            #region Button

            UIButton startResearchButton = new UIButton(new Vector2(0.6f, 0.15f), new Vector2(0.96f, 0.85f), lang.GetMessage("ResearchBench_StartResearch", this), "0.44 0.54 0.26 1", fontSize: 14, parent: bottomPanel);
            startResearchButton.AddCallback((player) =>
            {
                var data = GetResearchData(player);
                if (data.isResearchingItem)
                {
                    return;
                }
                DoResearch(player);
            });

            UIImage exitButtonImage = new UIImage(new Vector2(0.20f, 0.15f), new Vector2(0.25f, 0.85f), bottomPanel);
            //UIImage exitButtonImage = new UIImage(new Vector2(0.92f, 0.25f), new Vector2(0.98f, 0.75f), topInfoPanel);
            exitButtonImage.Image.Sprite = "assets/icons/exit.png";
            exitButtonImage.Image.Color = "0.7 0.7 0.7 0.7";

            UILabel exitButtonLabel = new UILabel(new Vector2(0.05f, 0.15f), new Vector2(0.18f, 0.85f), lang.GetMessage("ResearchBench_ExitLabel", this), 22, "0.7 0.7 0.7 0.9", bottomPanel, TextAnchor.MiddleRight);

            UIButton stopResearchButton = new UIButton(exitButtonLabel.min, exitButtonImage.max, "", "0 0 0 0", fontSize: 14, parent: bottomPanel);
            stopResearchButton.AddCallback((player) =>
            {
                var data = GetResearchData(player);
                if (data.isResearchingItem)
                {
                    return;
                }
                StopResearch(player);
            });

            #endregion

            #region Images

            itemIconImage = new UIRawImage(new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), itemResearchPanel);

            UIRawImage destroyedIconImage = new UIRawImage(new Vector2(0, 0), new Vector2(1, 1), itemResearchPanel);

            fragIconImage = new UIRawImage(new Vector2(0, 0), new Vector2(1, 1), fragmentPanel);

            #endregion

            #region Conditionals

            itemIconImage.variablePNG = delegate (BasePlayer player)
            {
                Item item = GetResearchData(player).targetItem;
                if (item == null)
                {
                    return null;
                }
                return (string)ImageLibrary?.Call("GetImage", item.info.shortname);
            };

            fragAmountLabel.variableText = delegate (BasePlayer player)
            {
                var data = GetResearchData(player);
                Item item = data.fragItem;
                if (item == null)
                {
                    return "x0";
                }
                int maxAmount = data.targetItem != null && CanResearchItem(data.targetItem) ? GetMaxFragsToResearch(data.targetItem, data.fragItem) : data.fragItem.amount;
                int amount = Mathf.Clamp(item.amount, 0, maxAmount);
                data.fragsToUse = amount;
                return 'x' + amount.ToString();
            };

            successChanceLabel.variableText = delegate (BasePlayer player)
            {
                return $"{Mathf.Floor(GetResearchChance(player) * 100f)}%";
            };

            itemDurabilityPanel.conditionalShow = delegate (BasePlayer player)
            {
                var data = GetResearchData(player);
                if (!data.usingResearchTable)
                {
                    return false;
                }
                if (data.targetItem == null)
                {
                    return false;
                }
                return data.targetItem.hasCondition;
            };

            itemDurabilityPanel.conditionalSize = delegate (BasePlayer player)
            {
                var data = GetResearchData(player);
                if (data.targetItem == null)
                {
                    return itemDurabilityPanel.size;
                }
                return new Vector2(itemDurabilityPanel.size.x, data.targetItem.conditionNormalized * data.targetItem.maxConditionNormalized * 1f);
            };

            destroyedIconImage.conditionalShow = delegate (BasePlayer player)
            {
                var data = GetResearchData(player);
                if (data.targetItem == null)
                {
                    return false;
                }
                return data.targetItem.isBroken;
            };

            fragIconImage.variablePNG = delegate (BasePlayer player)
            {
                var data = GetResearchData(player);
                if (data.fragItem == null)
                {
                    return null;
                }
                return (string)ImageLibrary?.Call("GetImage", data.fragItem.info.shortname, data.fragItem.skin);
            };

            researchCountdownLabel.variableText = delegate (BasePlayer player)
            {
                var data = GetResearchData(player);
                if (!(data.isResearchingItem && data.usingResearchTable))
                {
                    return "";
                }
                return data.timeLeft.ToString();

            };

            sourceItemLabel.variableText = delegate (BasePlayer player)
            {
                var data = GetResearchData(player);
                if (data.targetItem != null)
                {
                    if (!CanResearchItem(data.targetItem))
                    {
                        return "This item cannot be researched.";
                    }
                }
                return "The item will be broken on failure and destroyed on success.";
            };

            #endregion

            #region OpenIcon

            float dotSize = 0.007f;
            float fadeIn = 0.25f;

            UIImage crosshairDot = new UIImage(new Vector2(0.5f - dotSize, 0.5f - dotSize * 2), new Vector2(0.5f + dotSize, 0.5f + dotSize * 2), openIconPanel);
            crosshairDot.Image.Sprite = "assets/icons/circle_closed.png";

            UIImage openIconSprite = new UIImage(new Vector2(0.46f, 0.73f), new Vector2(0.54f, 0.90f), openIconPanel);
            openIconSprite.Image.Sprite = "assets/icons/open.png";
            openIconSprite.Image.FadeIn = fadeIn;

            UILabel openIconText = new UILabel(new Vector2(0, 0.5f), new Vector2(1f, 0.8f), lang.GetMessage("ResearchBench_OpenIcon", this), 14, parent: openIconPanel);
            openIconText.text.FadeIn = fadeIn;

            #endregion

        }

        public class ResearchData
        {
            public BasePlayer player { get; set; }
            public Item targetItem { get; set; }
            public Item fragItem { get; set; }
            public bool usingResearchTable { get; set; } = false;
            public bool lookingAtTable { get; set; }
            public bool isResearchingItem { get; set; }
            public int timeLeft { get; set; }
            public int fragsToUse { get; set; }
            public ResearchTableCollider currentCollider { get; set; }
            public ItemContainer itemsBeingResearched { get; set; }
            public Timer researchTimer { get; set; }
            public Timer researchTimerCountdown { get; set; }

            public bool IsNearTable()
            {
                return currentCollider != null;
            }
        }

        public Dictionary<BasePlayer, ResearchData> researchData { get; set; } = new Dictionary<BasePlayer, ResearchData>();

        private ResearchData GetResearchData(BasePlayer player)
        {
            if (player == null)
            {
                //Puts("GetResearchData() null player!");
                return null;
            }
            ResearchData data;
            if (!researchData.TryGetValue(player, out data))
            {
                data = new ResearchData();
                researchData[player] = data;
            }
            return data;
        }

        public bool IsResearching(BasePlayer player)
        {
            if (player == null)
            {
                return false;
            }
            var data = GetResearchData(player);
            return data.usingResearchTable;
        }

        public void StartResearch(BasePlayer player)
        {
            var data = GetResearchData(player);
            if (data.usingResearchTable)
            {
                return;
            }
            ShowResearchPanel(player);
            data.usingResearchTable = true;
        }

        public void StopResearch(BasePlayer player)
        {
            var data = GetResearchData(player);
            if (!data.usingResearchTable)
            {
                return;
            }
            if (data.isResearchingItem)
            {
                data.researchTimer?.Destroy();
                data.researchTimerCountdown?.Destroy();
                data.isResearchingItem = false;
                foreach (var item in data.itemsBeingResearched.itemList.ToList())
                {
                    player.GiveItem(item);
                }
                StopResearch(player);
            }
            data.usingResearchTable = false;
            //Need to return items
            data.fragItem = null;
            data.targetItem = null;
            researchPanel.Hide(player);
            CloseInventory(player);
        }

        private void ShowResearchPanel(BasePlayer player)
        {
            researchPanel.AddPlayer(player);
            researchPanel.Refresh(player);
            player.inventory.loot.StartLootingEntity(player, false);
            player.inventory.loot.AddContainer(player.inventory.containerMain);
            player.inventory.loot.SendImmediate();
            player.EndLooting();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "largewoodbox");
        }

        private void LoadResearchItem(BasePlayer player, Item item)
        {
            var data = GetResearchData(player);
            if (!data.usingResearchTable)
            {
                return;
            }
            data.targetItem = item;
            itemIconImage.Refresh(player);
            fragAmountLabel.Refresh(player);
        }

        private void OnResearchItemDropped(BasePlayer player, Item item)
        {
            var data = GetResearchData(player);
            if (!data.usingResearchTable)
            {
                return;
            }
            if (GetBPType(item) == BPType.None) //Research item dropped
            {
                LoadResearchItem(player, item);
            }
            else //Fragment item dropped
            {
                data.fragItem = item;
                fragAmountLabel.Refresh(player);
                fragIconImage.Refresh(player);
            }
            successChanceLabel.Refresh(player);
            itemDurabilityPanel.Refresh(player);
            sourceItemLabel.Refresh(player);
        }

        private bool CanResearchItem(Item item)
        {
            if (item == null)
            {
                return false;
            }
            BPType type = GetItemTier(item);
            return !(type == BPType.None || type == BPType.Default);
        }

        public float GetResearchChance(BasePlayer player)
        {
            var data = GetResearchData(player);
            if (!data.usingResearchTable)
            {
                return 0;
            }
            if (data.targetItem == null)
            {
                return 0;
            }
            if (!CanResearchItem(data.targetItem))
            {
                return 0;
            }
            float chance = 0.3f;
            if (data.targetItem.hasCondition)
            {
                chance *= data.targetItem.conditionNormalized * data.targetItem.maxConditionNormalized;
            }
            if (data.fragItem != null)
            {
                BPType type = GetBPType(data.fragItem);
                if (type == BPType.None)
                {
                    return chance;
                }
                chance += Mathf.Clamp(0.7f * (GetBPFragAmount(type) * (float)Mathf.Min(data.fragsToUse, data.fragItem.amount) / GetFragsNeeded(data.targetItem)), 0, 0.7f);
            }
            return chance;
        }

        public int GetMaxFragsToResearch(Item targetItem, Item fragItem)
        {
            return Mathf.CeilToInt(GetFragsNeeded(targetItem) / (float)GetBPFragAmount(GetBPType(fragItem)));
        }

        public void DoResearch(BasePlayer player)
        {
            var data = GetResearchData(player);
            if (data.targetItem == null)
            {
                return;
            }
            if (!CanResearchItem(data.targetItem))
            {
                return;
            }
            float chance = GetResearchChance(player);

            ItemContainer tempContainer = new ItemContainer();
            tempContainer.ServerInitialize(null, 10);
            tempContainer.GiveUID();
            tempContainer.allowedContents = ItemContainer.ContentsType.Generic;
            tempContainer.SetLocked(false);
            data.itemsBeingResearched = tempContainer;
            fragAmountLabel.Refresh(player);
            successChanceLabel.Refresh(player);
            if (data.targetItem != null)
            {
                Item item = data.targetItem;
                if (data.targetItem.amount > 1)
                {
                    item = data.targetItem.SplitItem(1);
                }
                item.MoveToContainer(tempContainer);
            }
            if (data.fragItem != null)
            {
                Item item = data.fragItem;
                if (data.fragItem.amount > data.fragsToUse)
                {
                    item = data.fragItem.SplitItem(data.fragsToUse);
                }
                item.MoveToContainer(tempContainer);
            }
            data.isResearchingItem = true;
            data.timeLeft = 10;
            data.researchTimer = timer.In(10f, () => //Could precalculate this, but better to do it when the timer actually ends
            {
                float rng = UnityEngine.Random.Range(0f, 1f);
                //Puts($"Chance: {chance} Result: {rng}");
                bool succeeded = rng <= chance;
                if (!succeeded)
                {
                    if (data.targetItem.hasCondition)
                    {
                        data.targetItem.LoseCondition(data.targetItem.condition * 2f);
                        player.inventory.GiveItem(data.targetItem, player.inventory.containerMain);
                    }
                    Effect.server.Run("assets/bundled/prefabs/fx/item_break.prefab", player.transform.position, Vector3.zero, null);
                }
                else
                {
                    GiveItemBlueprint(player, data.targetItem.info.itemid);
                }
                ItemManager.DoRemoves();
                data.targetItem = null;
                data.fragItem = null;
                data.isResearchingItem = false;
                if (!data.IsNearTable())
                {
                    StopResearch(player);
                }
                else
                {
                    //fragIconImage.Refresh(player);
                    //itemIconImage.Refresh(player);
                    //successChanceLabel.Refresh(player);
                    //fragAmountLabel.Refresh(player);
                    researchPanel.Refresh(player);
                }
                tempContainer.Kill();
            });
            researchCountdownLabel.Refresh(player);
            data.researchTimerCountdown = timer.Repeat(1f, 10, () =>
            {
                data.timeLeft--;
                researchCountdownLabel.Refresh(player);
            });

        }

        [ChatCommand("research")]
        void ResearchTable(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                lang.GetMessage("AdminCommand", this, player.UserIDString);
                return;
            }

            StartResearch(player);
        }

        #endregion

        #region MonoBehaviors

        public const float ColliderRadius = 2.5f;
        public const float ColliderHeight = 3f;
        public const float ColliderHeightDifference = 0.3f;

        public class MyBaseTrigger : MonoBehaviour
        {
            public List<BasePlayer> _players { get; set; }
            private CapsuleCollider collider { get; set; }
            private Rigidbody rigidbody { get; set; }
            private Timer _timer { get; set; }

            void Awake()
            {
                _players = new List<BasePlayer>();
                gameObject.layer = 3; //hack to get all trigger layers (Found in zone manager)

                collider = gameObject.AddComponent<CapsuleCollider>();
                collider.radius = ColliderRadius;
                collider.height = ColliderHeight;
                collider.isTrigger = true;

                Init();
            }

            void OnDestroy()
            {
                OnDestroyed();
            }

            public virtual void OnDestroyed()
            {
                GameObject.Destroy(collider);
            }

            public virtual void Init()
            {

            }

            void OnTriggerEnter(Collider collider)
            {
                BasePlayer player = collider.gameObject?.GetComponentInParent<BasePlayer>();
                if (player)
                {
                    if (!player.IsConnected || player.IsSleeping())
                    {
                        return;
                    }
                    OnPlayerEnter(player);
                }
            }

            void OnTriggerExit(Collider collider)
            {
                BasePlayer player = collider.gameObject?.GetComponentInParent<BasePlayer>();
                if (player)
                {
                    OnPlayerExit(player);
                }
            }

            public virtual void OnPlayerExit(BasePlayer player)
            {
                _players.Remove(player);
            }

            public virtual void OnPlayerEnter(BasePlayer player)
            {
                _players.Add(player);
            }

        }

        public class ResearchTableCollider : MyBaseTrigger
        {
            public Timer _timer { get; set; }

            public override void Init()
            {
                base.Init();
                gameObject.GetComponent<StorageContainer>().isLootable = false;
                _plugin.SafeSetTimerEvery(_timer, 0.2f, TimerLoop);
            }

            bool LookingAtObject<T>(BasePlayer player, string prefabName = "") where T : BaseNetworkable
            {
                if (_plugin.IsResearching(player))
                {
                    return false;
                }
                RaycastHit hit;
                if (!Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, 3f, LayerMask.GetMask("Deployed")))
                {
                    return false;
                }
                BaseEntity entity = hit.collider.GetComponentInParent<BaseEntity>();
                if (entity == null)
                {
                    return false;
                }
                if (!(entity is T))
                {
                    return false;
                }
                return true;
            }

            private void TimerLoop()
            {
                if (_players.Count == 0)
                {
                    return;
                }

                foreach (var player in _players.ToList())
                {
                    if (player == null)
                    {
                        _players.Remove(player);
                        break;
                    }
                    bool looking = LookingAtObject<ResearchTable>(player);
                    var data = _plugin.GetResearchData(player);
                    data.lookingAtTable = looking;
                    /*if (looking)
                    {
                        _plugin.openIconPanel.Show(player);
                    }
                    else
                    {
                        _plugin.openIconPanel.Hide(player);
                    }*/
                }
            }

            public override void OnPlayerExit(BasePlayer player)
            {
                var data = _plugin.GetResearchData(player);
                if (data.currentCollider != this)
                {
                    return;
                }
                data.currentCollider = null;
                base.OnPlayerExit(player);
                _plugin.openIconPanel.Hide(player);
                data.lookingAtTable = false;
                if (data.isResearchingItem)
                {
                    return;
                }
                _plugin.StopResearch(player);
            }

            public override void OnPlayerEnter(BasePlayer player)
            {
                var data = _plugin.GetResearchData(player);
                if (data.IsNearTable())
                {
                    return;
                }
                data.currentCollider = this;
                base.OnPlayerEnter(player);
            }

            public override void OnDestroyed()
            {
                base.OnDestroyed();
                _timer?.Destroy();
                foreach (BasePlayer player in _players)
                {
                    var data = _plugin.GetResearchData(player);
                    data.lookingAtTable = false;
                    _plugin.openIconPanel.Hide(player);
                    _plugin.StopResearch(player);
                }

                gameObject.GetComponent<StorageContainer>().isLootable = true;
            }
        }

        #endregion

        #region Loot Tables

        public Dictionary<string, GroupLootDefinition> allLootTables { get { return LootTables.Instance.allLootTables; } }

        public Dictionary<string, string> lootContainerAssignments { get { return LootTables.Instance.lootContainerAssignments; } }

        public class LootDefinitionChild
        {
            public string definition { get; set; }
            public float weight { get; set; }

            public LootDefinitionChild()
            {

            }

            public LootDefinitionChild(string categoryName, float _weight)
            {
                definition = categoryName;
                weight = _weight;
            }
        }

        public class ItemLootDefinition
        {
            public string itemName { get; set; }
            public int minAmount { get; set; }
            public int maxAmount { get; set; }

            public ItemLootDefinition(ItemDefinition def, int min, int max)
            {
                itemName = def.shortname;
                minAmount = min;
                maxAmount = max;
            }

            public ItemLootDefinition()
            {

            }
        }

        public class GroupLootDefinition
        {
            public string itemName { get; set; }

            public List<LootDefinitionChild> children { get; set; } = new List<LootDefinitionChild>(); //Possibilities

            public List<ItemLootDefinition> items { get; set; } = new List<ItemLootDefinition>(); //For sure choices

            public GroupLootDefinition()
            {

            }

            public GroupLootDefinition(LootSpawn category)
            {
                itemName = category.name;
                foreach (var item in category.subSpawn)
                {
                    AddChild(item);
                }
                foreach (var item in category.items)
                {
                    AddItem(item);
                }
            }

            public GroupLootDefinition GetRandomChild()
            {
                if (children.Count == 0)
                {
                    return this;
                }

                float totalSum = children.Sum(x => x.weight);

                float random = UnityEngine.Random.Range(0, totalSum);

                float i = 0;

                LootDefinitionChild selectedChild = null;

                foreach (var child in children)
                {
                    i += child.weight;
                    if (random <= i)
                    {
                        selectedChild = child;
                        break;
                    }
                }

                if (selectedChild == null)
                {
                    selectedChild = children.Last();
                }

                if (selectedChild.definition.Contains("Blueprints."))
                {
                    return ReturnChild(selectedChild);
                }

                return ReturnChild(selectedChild).GetRandomChild();
            }

            public GroupLootDefinition ReturnChild(LootDefinitionChild child)
            {
                GroupLootDefinition definition;
                if (!_plugin.allLootTables.TryGetValue(child.definition, out definition))
                {
                    _plugin.Puts($"ERROR: GetRandomChild() returned null! Look at ({this.itemName} {child.definition})");
                    return null;
                }
                return definition;
            }

            public float GetChildrenWeight()
            {
                return children.Sum(x => x.weight);
            }

            public void AddChild(GroupLootDefinition definition, float weight = 0)
            {
                children.Add(new LootDefinitionChild(definition.itemName, weight));
            }

            public void AddChild(string definition, float weight = 0)
            {
                children.Add(new LootDefinitionChild(definition, weight));
            }

            public void AddChild(LootSpawn.Entry entry)
            {
                AddChild(entry.category.name, entry.weight);
            }

            public void AddItem(ItemDefinition item, int minAmount, int maxAmount)
            {
                items.Add(new ItemLootDefinition(item, minAmount, maxAmount));
            }

            public void AddItem(ItemAmountRanged item)
            {
                AddItem(item.itemDef, (int)item.startAmount, (int)item.maxAmount);
            }

            public List<Item> FillContainer(ItemContainer container, bool allowNothing = true)
            {
                List<Item> items = new List<Item>();
                GroupLootDefinition def = GetRandomChild();
                int loops = 0;
                if (!allowNothing)
                {
                    while (def.itemName == "Nothing")
                    {
                        def = GetRandomChild();
                        loops++;
                        if (loops > 100)
                        {
                            _plugin.Puts("WARNING: FillContainer() returned nothing over 100 times!");
                            break;
                        }
                    }
                }
                string name = def.itemName;
                if (name.Contains("Blueprints."))
                {
                    name = name.Replace("Blueprints.", "");
                    BPType type = _plugin.GetRarityType(name);
                    if (type >= BPType.Book)
                    {
                        //_plugin.Puts($"BP of type {type} from {name}");
                    }
                    if (type != BPType.None && type != BPType.Default)
                    {
                        Item item = _plugin.GetRandomItemBlueprint(type);
                        item.MoveToContainer(container);
                        items.Add(item);
                    }
                    return items;
                }
                if (name == "Blueprint Fragments")
                {
                    Item item = _plugin.CreateBlueprint(BPType.Frags, (int)(50 * _plugin.Settings.blueprintRate));
                    item.MoveToContainer(container);
                    items.Add(item);
                    return items;
                }
                foreach (var item in def.items)
                {
                    items.Add(AddLootItem(container, item.itemName, UnityEngine.Random.Range(item.minAmount, item.maxAmount)));
                }

                //if (name.Contains("Items.Rare") || name.Contains("Items.VeryRare") || )

                return items;
            }

            public Item AddLootItem(ItemContainer container, string itemName, int amount)
            {
                if (itemName == "blueprint_fragment")
                {
                    return _plugin.CreateBlueprint(BPType.Frags, (int)(50 * _plugin.Settings.blueprintRate));
                }
                Item item = ItemManager.CreateByPartialName(itemName, amount);
                if (item == null)
                {
                    _plugin.Puts($"AddLootItem() failed to spawn {itemName}");
                    return null;
                }
                item.MoveToContainer(container);
                return item;
            }
        }

        HashSet<string> nearlyDestroyedItems { get; set; } = new HashSet<string>()
        {
            "rifle.ak",
            "rifle.lr300",
            "rifle.bolt",
            "smg.mp5",
            "pistol.m92",
            "smg.thompson",
            "smg.2",
        };

        public void ReduceCondition(List<Item> items, float durability = 0.06f, HashSet<string> filter = null)
        {
            foreach (var item in items)
            {
                if (filter != null)
                {
                    if (!filter.Contains(item.info.shortname))
                    {
                        continue;
                    }
                }
                if (item.hasCondition)
                {
                    item.condition = item.maxCondition * durability;
                }
            }
        }

        public void AssignLoot(LootContainer container)
        {
            if (container == null)
            {
                return;
            }
            if (!container.initialLootSpawn)
            {
                return;
            }
            if (container.inventory == null)
            {
                return;
            }
            container.inventory.Clear();
            if (container.PrefabName == "assets/prefabs/npc/patrol helicopter/heli_crate.prefab") //Ignore heli crates
            {
                return;
            }

            string name = "";
            if (!lootContainerAssignments.TryGetValue(container.name, out name))
            {
                lootContainerAssignments[container.PrefabName] = "";
                LootTables.Save();
            }

            if (name == "" || name == null)
            {
                Puts($"Loot table assignment: {container.PrefabName} has not been assigned to a loot category!");
                return;
            }

            List<Item> items = new List<Item>();

            if (name == "LootSpawn.Barrel")
            {
                items.AddRange(allLootTables["LootSpawn.Components"].FillContainer(container.inventory));
            }

            GroupLootDefinition def;
            if (!allLootTables.TryGetValue(name, out def))
            {
                Puts($"WARNING: AssignLoot() {name} is not a valid loot table!");
                return;
            }

            if (name == "LootSpawn.SupplyDrop")
            {
                container.inventory.capacity = 30;
                while (container.inventory.itemList.Count <= 7)
                {
                    items.AddRange(def.FillContainer(container.inventory, false));
                }
                //ReduceCondition(items, 0.5f);
                //Puts($"{items.Count} spawned into airdrop");
            }
            else if (name == "LootSpawn.TrashPile")
            {
                for (int i = 0; i < 3; i++)
                {
                    items.AddRange(def.FillContainer(container.inventory, false));
                }
            }
            else
            {
                if (name == "LootSpawn.Barrel" || name.Contains("LootSpawn.RadTown"))
                {
                    items.AddRange(allLootTables["LootSpawn.ResearchTable"].FillContainer(container.inventory));
                }

                ReduceCondition(def.FillContainer(container.inventory, false), filter: nearlyDestroyedItems);
            }
            if (name != "LootSpawn.OilBarrel" || name == "LootSpawn.SupplyDrop")
            {
                InsertBPFrags(container.inventory);
            }
            foreach (var item in items)
            {
                if (item.info.shortname == "smallwaterbottle")
                {
                    item.contents.AddItem(ItemManager.FindItemDefinition(112903447), 120);
                }
            }
        }

        public void ExportLootTables()
        {
            HashSet<LootSpawn> lootDefinitions = new HashSet<LootSpawn>();
            Queue<LootSpawn> queue = new Queue<LootSpawn>();
            foreach (var item in GameObject.FindObjectsOfType<LootContainer>())
            {
                if (!lootDefinitions.Contains(item.lootDefinition))
                {
                    lootDefinitions.Add(item.lootDefinition);
                    queue.Enqueue(item.lootDefinition);
                }
            }

            HashSet<LootSpawn> checkedValues = new HashSet<LootSpawn>(); //Decent the trees and get all unique

            while (queue.Count > 0)
            {
                LootSpawn current = queue.Dequeue();
                if (!checkedValues.Contains(current))
                {
                    checkedValues.Add(current);
                }
                foreach (var item in current.subSpawn)
                {
                    queue.Enqueue(item.category);
                }
            }

            //Puts($"{checkedValues.Count} unique loot tables");

            foreach (var item in checkedValues)
            {
                allLootTables[item.name] = new GroupLootDefinition(item);
            }

            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("DumpedLootTables", allLootTables);
        }

        #endregion

        #region Unlimited Components

        //Roadsign left in for roadsign armour
        //Sheet metal left in for heavy armour
        //Blades left in for salvaged tools
        //Double barrel is only 150 metal frags. Have to figure that out
        public HashSet<string> componentList { get; set; } = new HashSet<string>()
        {
        "bleach",
        "ducttape",
        "gears",
        "glue",
        "techparts",
        "tarp",
        "sticks",
        "metalspring",
        "sewingkit",
        "rope",
        "metalpipe",
        "riflebody",
        "smgbody",
        "semibody",
        };

        public void HandleManagedComponents(BasePlayer player, bool removeComponents, bool giveComponents)
        {
            if (removeComponents && player.inventory.containerMain.capacity > 24)
            {
                if (player?.inventory?.containerMain == null)
                {
                    return;
                }

                var retainedMainContainer = player.inventory.containerMain.uid;
                
                foreach (Item item in player.inventory.containerMain.itemList.ToList())
                {
                    if (componentList.Contains(item?.info.shortname))
                    {
                        item.RemoveFromContainer();
                    }
                }

                ItemManager.DoRemoves();

                Puts($"Player inventory items {player.inventory.containerMain.itemList.Count()}");
                player.inventory.containerMain.capacity = 24;
            }

            if (giveComponents)
            {
                player.inventory.containerMain.capacity = 24 + componentList.Count;

                NextFrame(() =>
                {
                    int hiddenSlotNumber = 0;

                    foreach (string itemName in componentList)
                    {
                        Item item = ItemManager.CreateByName(itemName, 99999);
                        item.MoveToContainer(player.inventory.containerMain,
                                24 + hiddenSlotNumber, false);
                        item.LockUnlock(true, player);

                        hiddenSlotNumber++;

                    }
                });
            }
        }

        private void EmptyContainer(ItemContainer container)
        {
            foreach (var item in container.itemList)
            {
                item.Remove();
            }

            ItemManager.DoRemoves();
        }

        private void PutAllItemsInContainer(IEnumerable<KeyValuePair<ItemDefinition, int>> items, ItemContainer container)
        {
            foreach (var item in items)
            {
                container.AddItem(item.Key, item.Value);
            }
        }

        private IEnumerable<KeyValuePair<ItemDefinition, int>> SaveInventory(ItemContainer containerMain)
        {
            return containerMain.itemList.Select(x => new KeyValuePair<ItemDefinition, int>(x.blueprintTargetDef, x.amount));
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer)
            {
                BasePlayer player = entity as BasePlayer;
                HandleManagedComponents(player, removeComponents: true, giveComponents: false);
            }
        }

        #endregion

        #region Vending Machine

        //Didn't work as both pages and books are medium presents. Not worth effort to get going

        //public UICheckbox blueprintCheckbox { get; set; } = new UICheckbox(new Vector2(0.9f,0.9f), new Vector2(0.92f,0.92f));

        public Dictionary<BasePlayer, VendingData> vendingData = new Dictionary<BasePlayer, VendingData>();

        public class VendingData
        {
            public bool adminPanelOpen { get; set; }
        }

        private VendingData GetVendingData(BasePlayer player)
        {
            if (player == null)
            {
                return null;
            }
            VendingData data;
            if (!vendingData.TryGetValue(player, out data))
            {
                data = new VendingData();
                vendingData.Add(player, data);
            }
            return data;
        }

        void OnBuyVendingItem(VendingMachine vendor, BasePlayer player, int sellOrderId, int transactionAmount) //Components don't do anything, but lets prevent players from getting them from inventory through vending machines
        {
            transactionAmount = Mathf.Clamp(transactionAmount, 1, 100000); //No hackers duping items please (not needed but can't be too safe)
            if (sellOrderId < 0 || sellOrderId > vendor.sellOrders.sellOrders.Count)
            {
                return;
            }
            string itemName = ItemManager.itemList.First(x => x.itemid == vendor.sellOrders.sellOrders[sellOrderId].currencyID).shortname;
            if (componentList.Contains(itemName))
            {
                PrintToChat(player, lang.GetMessage("ComponentsDisabled", this, player.UserIDString));
                timer.In(0.05f, vendor.ClearPendingOrder);
            }

        }

        #endregion

        #region Hemp

        void TryChangeHemp(CollectibleEntity entity)
        {
            if (entity.PrefabName == "assets/bundled/prefabs/autospawn/collectable/hemp/hemp-collectable.prefab")
            {
                if (!Settings.hempSeeds)
                {
                    for (int i = 0; i < entity.itemList.Length; i++)
                    {
                        ItemAmount item = entity.itemList[i];
                        if (item.itemDef.shortname == "cloth")
                        {
                            item.amount = 20;
                            entity.itemList = new ItemAmount[]
                            {
                                item
                            };
                            break;
                        }
                    }
                }
            }
        }

        #endregion

    }

}