namespace Oxide.Plugins
{
    [Info("BodiesToBags", "Ryan", "1.0.1", ResourceId = 2548)]
    [Description("Instantly turns player corpses into backpacks")]
    public class BodiesToBags : RustPlugin
    {
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            var corpse = entity as LootableCorpse;
            if (corpse == null || entity is NPCPlayer) return;

            timer.Once(5f, () =>
            {
                corpse.Kill();
                corpse.DropItems();
            });
        }
    }
}
