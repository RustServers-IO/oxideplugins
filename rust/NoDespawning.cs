using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("NoDespawning", "Jake_Rich", "1.0.1", ResourceId = 2467)]
    [Description("Fuck despawners")]
    class NoDespawning : RustPlugin
    {
        //This also covers locked chests dropping 50% of loot

        //More dumb shit: non-stackable items wont combine
        //Could override monobehavior (EntityCollisionMessage)

        //When ground is destroyed, it will call PreDie, so need to overwrite monobehavior
        //Could also override DropUtil.DropItems

        //Need to bind / weld items to the objects they are sitting on top of, so when floor is destroyed they move

        //Also need to reassign items once they move between chunks

        //When unloading server, 

        //TODO: When body dies, drop all loot

        //TODO: When decay entity is detroyed, covers all loot barrels yet doesnt cover mining quarries

        //TODO: Move to hook OnContainerDropItems (return true)

        //When combining multiple non-stackable items, store items inside item? (I think items can store items)

        //Despawn has to persist across reloads of server
        public static NoDespawning _plugin;

        public const int GRID_SIZE = 20;
        public const float MovementLoopSpeed = 2f;
        public const float MovementLoopStartDelay = 30f;
        public const float despawnTimerDelay = 30f;
        public static int halfSize;
        public static float drawDelay = 1f;
        public float originalDespawnTime = 180f;
        public static float minimumScaledDespawnTime = 60f * 10;
        public ItemGrid itemGrid { get; set; }
        public Timer _despawnTimer { get; set; }
        public bool initalized = false;

        public ConfigurationAccessor<Settings> settings { get; set; } = new ConfigurationAccessor<Settings>("NoDespawning-Settings");
        public ConfigurationAccessor<Database> data { get; set; } = new ConfigurationAccessor<Database>("NoDespawning-Database");

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

        public class GithubConfig<Type> : ConfigurationAccessor<Type> where Type : BaseConfigClass
        {
            public GithubConfig(string name) : base(name)
            {

            }

            public override void Init()
            {
                base.Init();
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
                            return;
                        }
                        Instance = deserialize;
                        Save();
                        Load();
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

        public class BaseConfigClass
        {
            public virtual void Initialize()
            {

            }
        }

        public class Settings : BaseConfigClass
        {
            public Dictionary<string, string> despawnTimes { get; set; } = new Dictionary<string, string>();

            private Dictionary<int, float> realDespawnTimes { get; set; } = new Dictionary<int, float>();

            private HashSet<int> scaling { get; set; } = new HashSet<int>();

            public override void Initialize()
            {
                foreach (var item in ItemManager.itemList)
                {
                    if (!despawnTimes.ContainsKey(item.shortname))
                    {
                        despawnTimes.Add(item.shortname, $"5m");
                    }
                    realDespawnTimes[item.itemid] = ParseTime(despawnTimes[item.shortname], item);
                }
            }

            private float ParseTime(string time, ItemDefinition definition)
            {
                bool scaling = time.Contains("_s");
                time = time.Replace("_s", "");
                if (scaling)
                {
                    if (!this.scaling.Contains(definition.itemid))
                    {
                        this.scaling.Add(definition.itemid);
                    }
                }
                float modifier = 60f;
                if (time.Contains("m"))
                {
                    modifier = 60f;
                    time = time.Replace("m", "");
                }
                else if (time.Contains("h"))
                {
                    modifier = 3600f;
                    time = time.Replace("h", "");
                }
                else if (time.Contains("d"))
                {
                    modifier = 84600f;
                    time = time.Replace("d", "");
                }
                int amount = 5; //5 minute default if can't parse
                if (!int.TryParse(time, out amount))
                {
                    _plugin.Puts($"Couldn't parse time for {definition.displayName.english} from {time}");
                    return amount;
                }
                return amount * modifier;
            }

            public float GetDespawnTime(Item item)
            {
                if (item == null)
                {
                    //_plugin.Puts($"GetDespawnTime NULL ITEM!");
                    return 0;
                }
                float time = realDespawnTimes[item.info.itemid];
                float scale = item.amount / (float)item.info.stackable;
                //_plugin.Puts($"{item.info.displayName.english} has scale of {scale} with {item.amount} and stacksize of {item.info.stackable} and time of {time}");
                if (scale > 1)
                {
                    time *= scale;
                }
                if (item.contents != null)
                {
                    time *= Mathf.Max(1, item.contents.itemList.Count(x => x.info == item.info)); //Adjust time based on amount of stacked items
                }
                if (!scaling.Contains(item.info.itemid))
                {
                    return time;
                }
                if (item.amount >= item.info.stackable)
                {
                    return time;
                }
                time *= scale * scale;
                return Mathf.Max(minimumScaledDespawnTime, time); //Don't want a few items despawning in too small of a time, or taking entire spawn time
            }
        }

        public class Database
        {
            public Dictionary<uint, float> despawnTimeLeft { get; set; } = new Dictionary<uint, float>();

            public void SaveDespawnTimes()
            {
                despawnTimeLeft.Clear();
                foreach (var item in _plugin.itemGrid.items)
                {
                    despawnTimeLeft[item.Key.net.ID] = item.Value.despawnTimeLeft;
                }
            }

            public void Load()
            {
                foreach (var item in GameObject.FindObjectsOfType<DroppedItem>())
                {
                    float time;
                    if (!despawnTimeLeft.TryGetValue(item.net.ID, out time))
                    {
                        continue;
                    }
                    _plugin.itemGrid.items[item] = new ItemInfo(time);
                }
            }
        }

        public class ItemGrid
        {
            public ItemChunk[,] itemGrid { get; set; }
            public int gridArraySize { get; private set; }

            private Timer _drawTimer { get; set; }
            private List<BasePlayer> drawPlayers { get; set; } = new List<BasePlayer>();
            public Dictionary<DroppedItem, ItemInfo> items { get; set; } = new Dictionary<DroppedItem, ItemInfo>();

            public ItemGrid()
            {

            }

            public void Init()
            {
                gridArraySize = Mathf.CeilToInt(ConVar.Server.worldsize / (float)GRID_SIZE);
                _plugin.Puts($"Created grid of {gridArraySize} by {gridArraySize}");
                itemGrid = new ItemChunk[gridArraySize, gridArraySize];
                _drawTimer?.Destroy();
                _drawTimer = _plugin.timer.Every(drawDelay, DrawChunks);
            }

            public ItemChunk GetGrid(Vector3 position, bool createIfEmpty = true)
            {
                if (itemGrid == null)
                {
                    return null;
                }
                int x = Mathf.Clamp(((int)position.x + halfSize) / GRID_SIZE, 0, gridArraySize - 1);
                int y = Mathf.Clamp(((int)position.z + halfSize) / GRID_SIZE, 0, gridArraySize - 1);
                //_plugin.Puts($"({x},{y})");
                ItemChunk grid = itemGrid[x, y];
                if (grid == null)
                {
                    if (!createIfEmpty)
                    {
                        return null;
                    }
                    grid = new ItemChunk(x, y);
                    itemGrid[x, y] = grid;
                }
                return grid;
            }

            public void UpdateGrid(BaseNetworkable ent, bool createIfEmpty = false)
            {
                GetGrid(ent.transform.position, false)?.ActivateChunk();
            }

            public void OnItemSpawned(DroppedItem item)
            {
                AssignToGrid(item);
            }

            public void OnItemKilled(DroppedItem item)
            {
                GetGrid(item.transform.position)?.RemoveItem(item);
                items.Remove(item);
            }

            public List<ItemChunk> GetChunks(Vector3 position, int radius = 1)
            {
                if (radius < 0)
                {
                    return null;
                }
                int x = ((int)position.x + halfSize) / GRID_SIZE;
                int y = ((int)position.z + halfSize) / GRID_SIZE;
                int xMin = (int)Mathf.Clamp(x - radius, 0, gridArraySize - 1);
                int yMin = (int)Mathf.Clamp(y - radius, 0, gridArraySize - 1);
                int xMax = (int)Mathf.Clamp(x + radius, 0, gridArraySize - 1);
                int yMax = (int)Mathf.Clamp(y + radius, 0, gridArraySize - 1);
                List<ItemChunk> chunks = new List<ItemChunk>((radius * 2 + 1) * (radius * 2 + 1));
                for (int xInc = xMin; xInc <= xMax; xInc++)
                {
                    for (int yInc = yMin; yInc <= yMax; yInc++)
                    {
                        var chunk = itemGrid[xInc, yInc];
                        if (chunk == null)
                        {
                            continue;
                        }
                        chunks.Add(chunk);
                    }
                }
                return chunks;
            }

            private void DrawChunks()
            {
                foreach (var player in drawPlayers)
                {
                    foreach (var chunk in GetChunks(player.transform.position, 3)) //Default draw 2 chunk radius around player
                    {
                        chunk.Draw(player);
                    }
                }
            }

            public void ToggleShowChunks(BasePlayer player)
            {
                if (!drawPlayers.Remove(player))
                {
                    drawPlayers.Add(player);
                }
            }

            public ItemChunk AssignToGrid(DroppedItem item)
            {
                var grid = GetGrid(item.transform.position);
                grid.AddItem(item);
                SetDespawnTime(item);
                return grid;
            }

            public string GetDebugInfo()
            {
                List<ItemChunk> grids = GetAllChunks();
                if (grids.Count == 0)
                {
                    return "No grids found.";
                }
                return $"Chunks: {grids.Count} / {itemGrid.GetLength(0) * itemGrid.GetLength(1)} (Active: {grids.Count(x => x._active)} Inactive: {grids.Count(x => !x._active)})\n" +
                       $"Items: {items.Count} (Active: {grids.Sum(x => x.activeItems.Count)} Inactive: {grids.Sum(x => x.inactiveItems.Count)})\n" +
                       $"Max Per Chunk: {grids.Max(x => x.activeItems.Count + x.inactiveItems.Count)}\n" +
                       $"Avg Per Chunk: {Math.Round(grids.Sum(x => x.activeItems.Count + x.inactiveItems.Count) / (float)grids.Count, 2)}\n" +
                       $"";
            }

            public List<ItemChunk> GetAllChunks()
            {
                List<ItemChunk> grids = new List<ItemChunk>();
                for (int x = 0; x < itemGrid.GetLength(0) - 1; x++)
                {
                    for (int y = 0; y < itemGrid.GetLength(1) - 1; y++)
                    {
                        if (itemGrid[x, y] != null)
                        {
                            grids.Add(itemGrid[x, y]);
                        }
                    }
                }
                return grids;
            }

            public ItemInfo GetItemInfo(DroppedItem item)
            {
                ItemInfo info;
                if (!items.TryGetValue(item, out info))
                {
                    info = new ItemInfo();
                    items.Add(item, info);
                }
                return info;
            }

            public void SetDespawnTime(DroppedItem item, int oldAmount = -1)
            {
                GetItemInfo(item).despawnTimeLeft = _plugin.settings.Instance.GetDespawnTime(item.item);
            }

            public void DeactivateGrids()
            {
                foreach (var grid in GetAllChunks())
                {
                    grid.DeactivateChunk();
                }
            }

            public List<ItemChunk> MostItemsInChunk()
            {
                return GetAllChunks().OrderBy(x => x.activeItems.Count + x.inactiveItems.Count).ToList();
            }
        }

        public class ItemChunk
        {
            public Vector3 center { get; set; }
            public Vector2 gridPosition { get; set; }

            public List<DroppedItem> activeItems { get; set; } = new List<DroppedItem>();
            public List<DroppedItem> inactiveItems { get; set; } = new List<DroppedItem>();

            public float lastActiveTime { get; set; } //May be useful to know how long chunk has been stale
            public Timer _movementTimer { get; set; }
            public Timer _deactivationTimer { get; set; }

            public bool _active { get; set; } = false;

            public Bounds2D bounds { get; set; }

            public ItemChunk(int x, int y)
            {
                center = new Vector3(GRID_SIZE * x + (GRID_SIZE / 2f) - halfSize, 0, GRID_SIZE * y + (GRID_SIZE / 2f) - halfSize);
                gridPosition = new Vector2(x, y);
                bounds = new Bounds2D(center, GRID_SIZE);
            }

            public ItemChunk()
            {

            }

            public void DeactivateChunk()
            {
                if (!_active)
                {
                    return;
                }
                //_plugin.Puts($"Chunk {center} deactivated.");
                foreach (var item in activeItems.ToList()) //Could always improve this code for "efficency" later, take out foreach, use for loop and indexes and shit
                {
                    DeactivateItem(item);
                }
                _movementTimer?.Destroy();
                _active = false;
            }

            public void DeactivateItem(DroppedItem item)
            {
                var rigidbody = item.GetComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = false;
                activeItems.Remove(item);
                inactiveItems.Add(item);
            }

            public void ActivateChunk()
            {
                InvokeDeactivation();
                if (_active)
                {
                    return;
                }
                if (activeItems.Count + inactiveItems.Count == 0)
                {
                    return;
                }
                //_plugin.Puts($"Chunk {center} activated.");
                foreach (var item in inactiveItems.ToList()) //Also could be optimized later
                {
                    if (item == null)
                    {
                        _plugin.Puts($"WARNING: Null item removed from inactive items!");
                        inactiveItems.Remove(item);
                        continue;
                    }
                    if (item.IsDestroyed)
                    {
                        //_plugin.Puts($"WARNING: Destroyed item removed from inactive items!");
                        activeItems.Remove(item);
                        continue;
                    }
                    ActivateItem(item);
                    lastPositions[item] = item.transform.position;
                }
                _active = true;
                lastActiveTime = Time.realtimeSinceStartup;
            }

            public void ActivateItem(DroppedItem item)
            {
                var rigidbody = item.GetComponent<Rigidbody>();
                rigidbody.isKinematic = false;
                rigidbody.detectCollisions = true;
                inactiveItems.Remove(item);
                activeItems.Add(item);
            }

            private Dictionary<DroppedItem, Vector3> lastPositions = new Dictionary<DroppedItem, Vector3>();

            private void MovementLoop()
            {
                if (activeItems.Count == 0) //Finish deactivating chunk once all items have stopped moving
                {
                    _movementTimer?.Destroy();
                    _active = false;
                    return;
                }
                foreach (var item in activeItems.ToList()) //Also could be optimized later
                {
                    if (item == null)
                    {
                        activeItems.Remove(item);
                        continue;
                    }
                    if (item.IsDestroyed)
                    {
                        activeItems.Remove(item);
                        continue;
                    }
                    Vector3 pos;
                    if (!lastPositions.TryGetValue(item, out pos))
                    {
                        lastPositions.Add(item, item.transform.position);
                    }
                    if (!bounds.Contains(item.transform.position))
                    {
                        RemoveItem(item);
                        _plugin.itemGrid.AssignToGrid(item);
                    }
                    if (Vector3.Distance(pos, item.transform.position) < 0.1f)
                    {
                        DeactivateItem(item);
                        continue;
                    }
                    lastPositions[item] = item.transform.position;
                }
            }

            private void InvokeDeactivation()
            {
                _movementTimer?.Destroy();
                _deactivationTimer?.Destroy();
                _deactivationTimer = _plugin.timer.In(MovementLoopStartDelay, StartDeactivation);
            }

            private void StartDeactivation()
            {
                _movementTimer?.Destroy();
                _movementTimer = _plugin.timer.Every(MovementLoopSpeed, MovementLoop);
            }

            public void RemoveItem(DroppedItem item)
            {
                if (!inactiveItems.Remove(item))
                {
                    activeItems.Remove(item);
                    if (activeItems.Count == 0 && inactiveItems.Count == 0)
                    {
                        _active = false;
                    }
                }
                else
                {
                    if (inactiveItems.Count == 0)
                    {
                        _active = false;
                    }
                }
                //_plugin.Puts($"Item removed from chunk {center}\n");
            }

            public void AddItem(DroppedItem item)
            {
                //_plugin.Puts($"Item added to chunk {center}");
                activeItems.Add(item);
                ActivateChunk();
            }

            public void Draw(BasePlayer player)
            {
                player.SendConsoleCommand("ddraw.box", drawDelay, _active ? Color.green : Color.red, center + new Vector3(0, 22.5f, 0), 1.5f);
                DrawBox(player, drawDelay, Color.blue, new Vector3(center.x - GRID_SIZE / 2f, center.y, center.z - GRID_SIZE / 2f), new Vector3(center.x + GRID_SIZE / 2f, center.y + 40f, center.z + GRID_SIZE / 2f));
                DrawText(player, _active ? "Active" : "Disabled", drawDelay, _active ? Color.green : Color.red, center + new Vector3(0, 22.5f, 0));
                DrawText(player, GetDebugInfo(), drawDelay, Color.cyan, center + new Vector3(0, 17.5f, 0));
            }

            public string GetDebugInfo(bool itemInfo = true)
            {
                string text = _active ? "<color=#FF0000>Active</color>" : "<color=#00FF00>Disabled</color>";
                return $"Active: {activeItems.Count}\n" +
                       $"Inactive: {inactiveItems.Count}\n" +
                       string.Join("\n", activeItems.Union(inactiveItems).Select(x => $"{x.item.amount}x {x.item.info.displayName.english}: {_plugin.itemGrid.GetItemInfo(x).despawnTimeLeft} left").ToArray());
            }
        }

        public class ItemInfo
        {
            public float despawnTimeLeft;

            public ItemInfo()
            {

            }

            public ItemInfo(float time)
            {
                despawnTimeLeft = time;
            }
        }

        public struct Bounds2D
        {
            public float xMin { get; set; }
            public float yMin { get; set; }
            public float xMax { get; set; }
            public float yMax { get; set; }

            public bool Contains(Vector3 pos)
            {
                if (pos.x < xMin)
                {
                    return false;
                }
                if (pos.z < yMin)
                {
                    return false;
                }
                if (pos.x > xMax)
                {
                    return false;
                }
                if (pos.z > yMax)
                {
                    return false;
                }
                return true;
            }

            public Bounds2D(Vector3 min, Vector3 max)
            {
                xMin = min.x;
                xMax = max.x;
                yMin = min.z;
                yMax = max.z;
            }

            public Bounds2D(Vector3 center, float size)
            {
                xMin = center.x - size / 2f;
                yMin = center.z - size / 2f;
                xMax = center.x + size / 2f;
                yMax = center.z + size / 2f;
            }
        }

        public string ToTitleCase(string str)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
        }

        public bool CombineItems(Item baseItem, Item addItem)
        {
            if (baseItem.info != addItem.info)
            {
                return false;
            }
            if (addItem.IsBlueprint() && baseItem.IsBlueprint() && addItem.blueprintTarget != baseItem.blueprintTarget)
            {
                return false;
            }

            if (!CombineUnstackable(baseItem, addItem))
            {
                if (addItem.skin != baseItem.skin) //Allow different skined weapons to stack
                {
                    return false;
                }
                baseItem.amount += addItem.amount;
                addItem.Remove();
            }

            baseItem.MarkDirty();
            return true;
        }

        public bool CombineUnstackable(Item baseItem, Item addItem)
        {
            //TODO: Seperate into seperate function, so can combine condition items as well as different skinned items
            if (baseItem.hasCondition) //Handle items with condition (weapons, and food)
            {
                if (baseItem.contents == null) //Initalize contents if it hasnt yet
                {
                    baseItem.contents = new ItemContainer();
                    baseItem.contents.ServerInitialize(baseItem, 0);
                    baseItem.contents.GiveUID();
                }

                if (addItem.contents != null) //Combine containers
                {
                    var items = addItem.contents.itemList.Where(x => x.info == baseItem.info);
                    if (items.Count() > 0) //Stacked weapons combining together
                    {
                        baseItem.contents.capacity += items.Count();
                        foreach (var item in items.ToList())
                        {
                            baseItem.contents.capacity++;
                            item.SetParent(baseItem.contents);
                        }
                    }
                }
                baseItem.contents.capacity++;
                addItem.SetParent(baseItem.contents);

                //Just store all durability items in container item
            }
            else
            {
                return false;
            }
            baseItem.MarkDirty();
            return true;
        }

        [ChatCommand("strip")]
        void StripInventoryCommand(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            player.inventory.Strip();
            player.inventory.containerMain.capacity = 24;
            player.inventory.GiveItem(ItemManager.CreateByPartialName("longsword", 10));
            player.inventory.GiveItem(ItemManager.CreateByPartialName("rifle", 100));
            player.inventory.GiveItem(ItemManager.CreateByPartialName("box.wooden", 100));
        }

        public void DespawnLoop()
        {
            foreach (var item in itemGrid.items.ToList())
            {
                if (item.Key.IsDestroyed)
                {
                    itemGrid.items.Remove(item.Key);
                    continue;
                }
                item.Value.despawnTimeLeft -= despawnTimerDelay;
                if (item.Value.despawnTimeLeft >= 0)
                {
                    continue;
                }
                item.Key.Kill();
                itemGrid.items.Remove(item.Key);
            }
        }

        public void PrintItemAmounts()
        {
            Puts("Printing Debug Info:\n\n" + itemGrid.GetDebugInfo() + "\n");
        }

        [ChatCommand("showchunks")]
        void ToggleShowChunksCommand(BasePlayer player, string command)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            itemGrid.ToggleShowChunks(player);
        }

        [ChatCommand("despawninfo")]
        void DespawnInfo(BasePlayer player, string command)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            PrintToChat(player, itemGrid.GetDebugInfo());
        }

        [ChatCommand("chunkinfo")]
        void ChunkInfoCommand(BasePlayer player, string command)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            var chunk = itemGrid.GetGrid(player.transform.position, false);
            if (chunk == null)
            {
                PrintToChat(player, "You are not in a chunk");
            }
            else
            {
                PrintToChat(player, chunk.GetDebugInfo());

            }

        }

        #region Hooks

        void OnServerInitialized()
        {
            initalized = true;
            originalDespawnTime = ConVar.Server.itemdespawn; //Just set items not to despawn, and handle removing manually
            ConVar.Server.itemdespawn = float.MaxValue;

            settings.Instance.Initialize();
            settings.Save();
            //settings = new ConfigurationAccessor<Settings>("NoDespawning-Settings");


            //Puts($"Worldsize: {ConVar.Server.worldsize}");
            halfSize = ConVar.Server.worldsize / 2;
            itemGrid = new ItemGrid();
            itemGrid.Init();
            var items = GameObject.FindObjectsOfType<DroppedItem>();
            Puts($"Found {items.Length} dropped items");
            int loops = 0;
            try //Just incase shit hits the fan when looping through ALL items
            {
                foreach (var item in items)
                {
                    itemGrid.AssignToGrid(item);
                    loops++;
                }
            }
            catch (Exception ex)
            {
                Puts($"FAILED! Only looped through {loops} items\n {ex}");

                return;
            }
            itemGrid.DeactivateGrids(); //Start them up disabled
            Puts($"Assigned {items.Length} dropped items into the grid.");
            _despawnTimer = timer.Every(despawnTimerDelay, DespawnLoop);
            PrintItemAmounts();
            //timer.Every(300f, PrintItemAmounts);
        }

        void Loaded()
        {
            _plugin = this;
        }

        void Unload()
        {
            ConVar.Server.itemdespawn = originalDespawnTime;
            _despawnTimer?.Destroy();
        }

        void OnEntityKill(BaseNetworkable entity) //Removes entities from chunks
        {
            if (!initalized)
            {
                return;
            }
            if (entity is DroppedItem)
            {
                itemGrid.OnItemKilled((DroppedItem)entity);
            }
            else if (entity is DecayEntity)
            {
                itemGrid.UpdateGrid(entity);
            }
            else if (entity is PlayerCorpse)
            {
                var corpse = entity as PlayerCorpse;
                foreach (var container in corpse.containers) //Simplest way, I doubt people will leave 20k stone on a corpse to decay
                {
                    DropUtil.DropItems(container, container.dropPosition);
                }
            }
        }

        void OnEntitySpawned(BaseNetworkable entity) //Assigns entities to chunks
        {
            if (!initalized)
            {
                return;
            }
            if (entity is DroppedItem)
            {
                itemGrid.OnItemSpawned((DroppedItem)entity);
            }
        }

        object OnItemPickup(Item item, BasePlayer player) //Handles dropped items over stacksize limit
        {
            //Puts("OnItemPickup");
            DroppedItem DroppedItem = item.GetWorldEntity() as DroppedItem;
            if (DroppedItem == null)
            {
                Puts("OnItemPickup() DroppedItem is null");
                return null;
            }
            if (item.contents != null)
            {
                if (item.contents.itemList.Count > 0) //Handles stacked items with conditions (stored in contents)
                {
                    //Puts($"OnItemPickup Contents Count: {item.contents.itemList.Count}");
                    if (item.contents.itemList.Any(x => x.info == item.info)) //If item and contents are the same, we are storing them on purpose
                    {
                        Item newItem = item.contents.itemList.Last(x => x.info == item.info);

                        newItem.SetParent(null);

                        player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
                        player.SignalBroadcast(BaseEntity.Signal.Gesture, "pickup_item", null);
                        itemGrid.SetDespawnTime(DroppedItem); //Update respawn time TODO: change it later so it doesnt reset each time item is added / removed;
                        item.contents.capacity--;
                        return true;
                    }
                }
            }
            if (item.amount > item.info.stackable) //Handles normal stacked items
            {
                var newItem = ItemManager.CreateByItemID(item.info.itemid, item.info.stackable, item.skin);
                newItem.condition = item.condition;
                DroppedItem.item.amount -= item.info.stackable;
                string extraText = item.amount.ToString();
                if (item.amount >= 1000)
                {
                    extraText = $"{Mathf.Floor(item.amount / 1000)}k";
                }
                //DroppedItem.item.name = $"{item.info.displayName.english} ({extraText} left)";
                DroppedItem.item.MarkDirty();
                player.GiveItem(newItem, global::BaseEntity.GiveItemReason.PickedUp);
                player.SignalBroadcast(global::BaseEntity.Signal.Gesture, "pickup_item", null);
                itemGrid.SetDespawnTime(DroppedItem); //Update respawn time TODO: change it later so it doesnt reset each time item is added / removed
                return true;
            }
            else
            {
                //DroppedItem.item.name = $"{ToTitleCase(item.info.shortname)}";
            }
            return null;
        }

        object CanCombineDroppedItem(DroppedItem groundItem, DroppedItem droppedItem) //Combines multiple dropped items of same type, to increase performance
        {
            if (!CombineItems(groundItem.item, droppedItem.item))
            {
                return null;
            }
            /*
            if (num > groundItem.item.info.stackable)
            {
                return false;
            }
            if (num == 0)
            {
                return false;
            }*/
            itemGrid.SetDespawnTime(groundItem); //Update respawn time TODO: change it later so it doesnt reset each time item is added / removed
            droppedItem.Kill(BaseNetworkable.DestroyMode.None);
            //groundItem.Invoke(new Action(() => { groundItem.DestroyItem(); groundItem.Kill(); }), groundItem.GetDespawnDuration());
            Effect.server.Run("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", groundItem, 0u, Vector3.zero, Vector3.zero, null, false);
            return true;
        }

        object OnContainerDropItems(ItemContainer inventory) //Prestacks all items in container before dropping, to increase performance
        {
            Dictionary<int, Item> items = new Dictionary<int, Item>();
            foreach (var item in inventory.itemList.ToList())
            {
                Item stackedItem;
                if (!items.TryGetValue(item.info.itemid, out stackedItem))
                {
                    items.Add(item.info.itemid, item);
                    stackedItem = item;
                    continue;
                }
                if (!CombineItems(stackedItem, item))
                {
                    Puts("Failed to combine items!");
                }
            }
            float num = 0.25f;
            foreach (var item in items.Values)
            {
                float num2 = UnityEngine.Random.Range(0f, 2f);
                item.RemoveFromContainer();
                BaseEntity parent = inventory.entityOwner;
                Vector3 dropPos = inventory.entityOwner == null ? inventory.dropPosition + new Vector3(0, 0.3f, 0) : inventory.entityOwner.transform.position + new Vector3(0, 1, 0);
                BaseEntity baseEntity = item.CreateWorldObject(dropPos + new Vector3(UnityEngine.Random.Range(-num, num), 0f, UnityEngine.Random.Range(-num, num)), default(Quaternion));
                if (baseEntity == null)
                {
                    //Puts($"OnContainerDropItems() baseEntity is NULL!!!");
                    item.Remove(0f);
                }
                else if (num2 > 0f)
                {
                    baseEntity.SetVelocity(new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(-1f, 1f)) * num2);
                    baseEntity.SetAngularVelocity(new Vector3(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-10f, 10f)) * num2);
                }
            }
            //inventory.Kill();
            //inventory = null;
            return true;
        }

        #endregion

        #region DDraw

        public static void DrawCube(BasePlayer player, float duration, UnityEngine.Color color, Vector3 position, float size = 1)
        {
            player.SendConsoleCommand("ddraw.box", duration, color, position, size);
        }

        public static void DrawBox(BasePlayer player, float duration, UnityEngine.Color color, Vector3 min, Vector3 max)
        {
            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z));
            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z));
            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z));
            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z));
            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z));
            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z));

            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z));
            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z));
            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z));
            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z));
            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z));
            player.SendConsoleCommand("ddraw.line", duration, color, new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z));
        }

        public static void DrawText(BasePlayer player, string text, float duration, UnityEngine.Color color, Vector3 position, int size = 12)
        {
            player.SendConsoleCommand("ddraw.text", duration, color, position, size == 12 ? text : $"<size={size}>{text}</size>");
        }

        public static void DrawSphere(BasePlayer player, float duration, UnityEngine.Color color, Vector3 position, float size)
        {
            player.SendConsoleCommand("ddraw.sphere", duration, color, position, size);
        }


        #endregion
    }
}