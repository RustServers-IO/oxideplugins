using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.IO.Compression;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("UpgradeTimer", "Jake_Rich", "1.0.1", ResourceId = 2740)]
    [Description("Time limit to upgrade twig after it has been placed")]

    public class UpgradeTimer : RustPlugin
    {
        public static UpgradeTimer _plugin;
        public JSONFile<ConfigData> _settingsFile;
        public ConfigData Settings { get { return _settingsFile.Instance; } }
        public PlayerDataController<TemplatePlayerData> PlayerData;

        void Init()
        {
            _plugin = this;
        }

        void Loaded()
        {
            _settingsFile = new JSONFile<ConfigData>($"{Name}", ConfigLocation.Config, extension: ".cfg");
            PlayerData = new PlayerDataController<TemplatePlayerData>();
        }

        void Unload()
        {
            PlayerData.Unload();
        }

        public class ConfigData
        {
            public int UpgradeDelay = 10;
        }

        public class TemplatePlayerData : BasePlayerData
        {

        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            BaseCombatEntity entity = go.ToBaseEntity() as BuildingBlock;

            if (entity == null)
            {
                return;
            }

            Timer _timer = null;
            entity.healthFraction = 0;
            entity.lastAttackedTime = Time.time;
            _timer = timer.Repeat(1f, Settings.UpgradeDelay, () =>
            {
                if (entity == null || entity.IsDestroyed || _timer == null)
                {
                    _timer?.Destroy();
                    return;
                }
                entity.lastAttackedTime = Time.time;
                entity.healthFraction = 1f / Settings.UpgradeDelay * (Settings.UpgradeDelay - _timer.Repetitions);
                if (_timer.Repetitions == 0)
                {
                    entity.lastAttackedTime = 0;
                    timers.Remove(entity);
                }
            });
            timers.Add(entity, _timer);
        }

        void OnEntityKill(BaseNetworkable net)
        {
            var entity = net as BaseCombatEntity;
            if (entity == null)
            {
                return;
            }
            Timer _timer;
            if (!timers.TryGetValue(entity, out _timer))
            {
                return;
            }
            _timer?.Destroy();
            timers.Remove(entity);
        }

         void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
         {
            if (!(entity is BuildingBlock))
            {
                return;
            }
            Timer _timer;
            if (!timers.TryGetValue(entity, out _timer))
            {
                return;
            }
            _timer?.Destroy();
            timers.Remove(entity);
         }

        public Dictionary<BaseCombatEntity, Timer> timers = new Dictionary<BaseCombatEntity, Timer>();


        #region PlayerData

        public class BasePlayerData
        {
            [JsonIgnore]
            public BasePlayer Player { get; set; }

            public string userID { get; set; } = "";

            public BasePlayerData()
            {

            }
            public BasePlayerData(BasePlayer player) : base()
            {
                userID = player.UserIDString;
                Player = player;
            }
        }

        public class PlayerDataController<T> where T : BasePlayerData
        {
            [JsonPropertyAttribute(Required = Required.Always)]
            private Dictionary<string, T> playerData { get; set; } = new Dictionary<string, T>();
            private JSONFile<Dictionary<string, T>> _file;
            private Timer _timer;
            public IEnumerable<T> All { get { return playerData.Values; } }

            public PlayerDataController()
            {

            }

            public PlayerDataController(string filename = null)
            {
                if (filename == null)
                {
                    return;
                }
                _file = new JSONFile<Dictionary<string, T>>(filename);
                _timer = _plugin.timer.Every(120f, () =>
                {
                    _file.Save();
                });
            }

            public void Unload()
            {
                if (_file == null)
                {
                    return;
                }
                _file.Save();
            }

            public T Get(string identifer)
            {
                T data;
                if (!playerData.TryGetValue(identifer, out data))
                {
                    data = Activator.CreateInstance<T>();
                    playerData[identifer] = data;
                }
                return data;
            }

            public T Get(ulong userID)
            {
                return Get(userID.ToString());
            }

            public T Get(BasePlayer player)
            {
                var data = Get(player.UserIDString);
                data.Player = player;
                return data;
            }

            public bool Has(ulong userID)
            {
                return playerData.ContainsKey(userID.ToString());
            }

            public void Set(string userID, T data)
            {
                playerData[userID] = data;
            }

            public bool Remove(string userID)
            {
                return playerData.Remove(userID);
            }

            public void Update(T data)
            {
                playerData[data.userID] = data;
            }
        }

        #endregion

        #region Configuration Files

        public enum ConfigLocation
        {
            Data = 0,
            Config = 1,
            Logs = 2,
            Plugins = 3,
            Lang = 4,
            Custom = 5,
        }

        public class JSONFile<Type> where Type : class
        {
            private DynamicConfigFile _file;
            public string _name { get; set; }
            public Type Instance { get; set; }
            private ConfigLocation _location { get; set; }
            private string _path { get; set; }
            public bool SaveOnUnload = false;
            public bool Compressed = false;

            public JSONFile(string name, ConfigLocation location = ConfigLocation.Data, string path = null, string extension = ".json", bool saveOnUnload = false)
            {
                SaveOnUnload = saveOnUnload;
                _name = name.Replace(".json", "");
                _location = location;
                switch (location)
                {
                    case ConfigLocation.Data:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.DataDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Config:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.ConfigDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Logs:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LogDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Lang:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LangDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Custom:
                        {
                            _path = $"{path}/{name}{extension}";
                            break;
                        }
                }
                _file = new DynamicConfigFile(_path);
                _file.Settings = new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                Init();
            }

            public virtual void Init()
            {
                _plugin.OnRemovedFromManager.Add(new Action<Plugin, PluginManager>(Unload));
                Load();
                Save();
                Load();
            }

            public virtual void Load()
            {
                if (Compressed)
                {
                    LoadCompressed();
                    return;
                }

                if (!_file.Exists())
                {
                    Save();
                }
                Instance = _file.ReadObject<Type>();
                if (Instance == null)
                {
                    Instance = Activator.CreateInstance<Type>();
                    Save();
                }
                return;
            }

            private void LoadCompressed()
            {
                string str = _file.ReadObject<string>();
                if (str == null || str == "")
                {
                    Instance = Activator.CreateInstance<Type>();
                    return;
                }
                using (var compressedStream = new MemoryStream(Convert.FromBase64String(str)))
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                using (var resultStream = new MemoryStream())
                {
                    var buffer = new byte[4096];
                    int read;

                    while ((read = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        resultStream.Write(buffer, 0, read);
                    }

                    Instance = JsonConvert.DeserializeObject<Type>(Encoding.UTF8.GetString(resultStream.ToArray()));
                }
            }

            public virtual void Save()
            {
                if (Compressed)
                {
                    SaveCompressed();
                    return;
                }

                _file.WriteObject(Instance);
                return;
            }

            private void SaveCompressed()
            {
                using (var stream = new MemoryStream())
                {
                    using (GZipStream zipStream = new GZipStream(stream, CompressionMode.Compress))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Instance));
                        zipStream.Write(bytes, 0, bytes.Length);
                        zipStream.Close();
                        _file.WriteObject(Convert.ToBase64String(stream.ToArray()));
                    }
                }
            }

            public virtual void Reload()
            {
                Load();
            }

            private void Unload(Plugin sender, PluginManager manager)
            {
                if (SaveOnUnload)
                {
                    Save();
                }
            }
        }

        #endregion

    }
}