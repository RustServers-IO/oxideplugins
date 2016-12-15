using Oxide.Core;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MultiCupboard", "Lucky Luciano & Frank Costello", "0.0.2")]
    public class MultiCupboard : RustPlugin
    {
        private bool multicupboard;
        private MultiCupboardPersistence pst = null;
        private bool initialized = false;

        void OnServerInitialized ()
        {
            if (initialized)
                return;
            multicupboard = true;
            Puts("MultiCupboard is {0}", multicupboard ? "enabled!" : "disabled!");

            bool reloaded = false;
            foreach (var prevPst in ServerMgr.Instance.gameObject.GetComponents<MonoBehaviour>())
            {
                if (prevPst.GetType().Name == "MultiCupboardPersistence")
                {
                    reloaded = true;
                    pst = ServerMgr.Instance.gameObject.AddComponent<MultiCupboardPersistence>();
                    UnityEngine.Object.Destroy(prevPst);
                    break;
                }
            } 
            if (!reloaded)
                pst = ServerMgr.Instance.gameObject.AddComponent<MultiCupboardPersistence>();

            var bpts = UnityEngine.Object.FindObjectsOfType<BuildPrivilegeTrigger>();
            var updated = 0;
            foreach (var bpt in bpts)
                if (updateTrigger(bpt))
                    ++updated;

            if (bpts.Length > 0)
                updateInfluence(bpts[0].privlidgeEntity.prefabID);

            initialized = true;
        }

        void OnEntitySpawned (BaseNetworkable ent)
        {
            if (!initialized || !(ent is BuildingPrivlidge))
                return;

            updateInfluence(ent.prefabID);

            var trig = ent.GetComponentInChildren<BuildPrivilegeTrigger>();
            if (trig == null)
                Interface.Oxide.NextTick(() =>
                {
                    trig = ent.GetComponentInChildren<BuildPrivilegeTrigger>();
                    if (trig == null)
                    {
                        PrintWarning("Missing BuildPrivilegeTrigger");
                        return;
                    }
                    updateTrigger(trig);
                });
            else
                updateTrigger(trig);
        }

        private bool updateTrigger(BuildPrivilegeTrigger bpt)
        {
            var col = bpt.GetComponent<UnityEngine.Collider>();
            return true;
        }

        private void updateInfluence(uint privlidgePrefabID)
        {
            if (multicupboard == pst.ignorecupboard)
                return;
            var attr = PrefabAttribute.server.Find(privlidgePrefabID);
            var socketBases = attr.Find<Socket_Base>();
            var socketBase = socketBases[0];
            if (multicupboard)
            {
                socketBase.socketMods = socketBase.socketMods.Where(mod => mod.FailedPhrase.english != "You're trying to place too close to another cupboard").ToArray();
            }
        }
        private class MultiCupboardPersistence : MonoBehaviour
        {
            public bool ignorecupboard = false;

        }
    }
}
