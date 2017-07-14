using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Bloodtrail", "hoppel", "1.0.2")]
    public class Bloodtrail : RustPlugin
    {
        const string permname = "bloodtrail.allow";

        void Init()
        {
            permission.RegisterPermission(permname, this);

        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, permname))
                return;
            {
                if (player.metabolism.bleeding.value > 0)
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/player/beartrap_blood.prefab", player.transform.position, Vector3.up, null, true);
                }
            }
        }
    }
}
