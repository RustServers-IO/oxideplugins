using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("NoRespawnCooldowns", "Absolut", "1.0.1", ResourceId = 2349)]

    class NoRespawnCooldowns : RustPlugin
    {
        private FieldInfo BagCooldown;

        void Loaded()
        {
            BagCooldown = typeof(SleepingBag).GetField("unlockTime", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            permission.RegisterPermission(this.Title + ".allow", this);
        }

        void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (player == null) return;
            if (permission.UserHasPermission(player.UserIDString, this.Title + ".allow"))
                ResetSpawnTargets(player);
        }

        private void ResetSpawnTargets(BasePlayer player)
        {
            SleepingBag[] bags = SleepingBag.FindForPlayer(player.userID, true);
            foreach (var entry in bags)
                    BagCooldown.SetValue(entry, 0);
        }
    }
}
