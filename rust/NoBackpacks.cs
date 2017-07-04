using System;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("NoBackpacks","hoppel","1.0.3")]

    public class NoBackpacks : RustPlugin
    {
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity != null)
            {
                if (entity.name.Contains("item_drop_backpack"))
                {
                    timer.Once(_timer, () =>
                    {
                        if (entity != null)
                        {
                            entity.Kill();
                        }
                    });
                }
            }
        }
        #region config
        float _timer = 0;

        new void LoadConfig()
        {
            GetConfig(ref _timer, "Settings", "Despawn timer (seconds)");
            SaveConfig();
        }

         void Init()
         {
            Unsubscribe(nameof(OnEntitySpawned));
            LoadConfig();
         }

        void OnServerInitialized() => Subscribe(nameof(OnEntitySpawned));

        void GetConfig<T>(ref T variable, params string[] path)
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
        #endregion

    }
}