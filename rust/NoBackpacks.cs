namespace Oxide.Plugins
{
    [Info("NoBackpacks", "hoppel", "1.0.0 ")]

    public class NoBackpacks : RustPlugin
    {

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.name.Contains("item_drop_backpack"))
            {
                entity.Kill();
            }
        }

    }
}