using System;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("No Backpacks", "hoppel", "1.0.4")]
    [Description("Removes backpacks after the configured amount of time")]
    public class NoBackpacks : RustPlugin
    {
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity != null && entity.name.Contains("item_drop_backpack"))
            {
                {
                    timer.Once(despawnTimer, () =>
                    {
                        entity?.Kill();
                    });
                }
            }
        }

        #region config

        private int despawnTimer;

        private new void LoadConfig()
        {
            GetConfig(ref despawnTimer, "Settings", "Despawn timer (seconds)");
            SaveConfig();
        }

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
            LoadConfig();
        }

        private void OnServerInitialized() => Subscribe(nameof(OnEntitySpawned));

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");

        #endregion config
    }
}
