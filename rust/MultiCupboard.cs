using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MultiCupboard", "Lucky Luciano / Def", "0.0.6")]
    public class MultiCupboard : RustPlugin
    {
        private static float CupboardRadius;
        private bool multicupboard;
        private MultiCupboardPersistence pst = null;
        private bool initialized = false;

        protected override void LoadDefaultConfig()
        {
            Config["CupboardRadius - Radius to place the next ToolCupboard"] = CupboardRadius = GetConfig("CupboardRadius - Radius to place the next ToolCupboard", 5.0f);
            SaveConfig();
        }

        private void Init()
        {
            LoadDefaultConfig();
        }

        private void OnServerInitialized()
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

        private object CanBuild(Planner plan, Construction prefab, Vector3 pos)
        {
            if (!plan || !prefab || !plan.GetOwnerPlayer())
                return null;
            if (!prefab.hierachyName.StartsWith("cupboard.tool"))
                return null;
            var list = Pool.GetList<BuildingPrivlidge>();
            Vis.Entities(pos, CupboardRadius, list, Layers.Server.Deployed, QueryTriggerInteraction.Ignore);
            //var result = list.Any(col => col.GetComponent<BuildPrivilegeTrigger>());
            var result = list.Count > 0;
            Pool.FreeList(ref list);
            if (!result)
                return null;
            var player = plan.GetOwnerPlayer();
            rust.SendChatMessage(player,string.Empty, "You\'re placing cupboard too close to another.", player.UserIDString);
            return false;
        }

        private void onEntitySpawned(BaseNetworkable ent)
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
	    T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)System.Convert.ChangeType(Config[name], typeof(T));
    }
}
