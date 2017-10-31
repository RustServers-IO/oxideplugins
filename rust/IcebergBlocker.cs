using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Iceberg Blocker", "Slut", "1.0.0")]
    class IcebergBlocker : RustPlugin
    {
        private object CanBuild(Planner plan, Construction prefab)
        {
            var player = plan.GetOwnerPlayer();
            Vector3 pos = plan.GetEstimatedWorldPosition();
            if (player != null)
            {
                List<Collider> list = new List<Collider>();
                Vis.Colliders(pos, 10f, list);
                for (int x = 0; x < list.Count; x++)
                {
                    if (list[x].name.Equals("iceberg_COL"))
                    {
                        return false;
                    }
                }
            }
            return null;
        }
    }
}
