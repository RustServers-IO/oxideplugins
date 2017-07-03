namespace Oxide.Plugins
{
    [Info("BodiesToBags", "Ryan", "1.0.0")]

    class BodiesToBags : RustPlugin
    {
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            var corpse = entity as LootableCorpse;

            if (corpse == null)
                return;

            timer.Once(5f, () =>
            {
                corpse.Kill();
                corpse.DropItems();
            });
        }
    }
}