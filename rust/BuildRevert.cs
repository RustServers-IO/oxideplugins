using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildRevert", "nivex", "1.0.1", ResourceId = 2465)]
    [Description("Prevent building in blocked area.")]
    public class BuildRevert : RustPlugin
    {
        Dictionary<string, bool> constructions = new Dictionary<string, bool>();
        readonly int layerMask = LayerMask.GetMask("Trigger");
        readonly Vector3 upOffset = new Vector3(0f, 0.6f, 0f);

        void OnServerInitialized() => LoadVariables();
        void Unload() => constructions.Clear();
        object CanBuild(Planner plan, Construction prefab) => plan?.GetOwnerPlayer() != null && constructions.ContainsKey(prefab.hierachyName) && !constructions[prefab.hierachyName] && !canBuild(plan) ? (object)false : null;
        
        bool canBuild(Planner plan)
        {
            var player = plan.GetOwnerPlayer();
            bool _canBuild = true;

            if (!player || (!string.IsNullOrEmpty(permName) && permission.UserHasPermission(player.UserIDString, permName)))
                return _canBuild;

            BuildingPrivlidge priv = null;
            var colliders = Pool.GetList<Collider>();
            var position = player.transform.position;
            var buildPos = position + (player.eyes.BodyForward() * 4f);
            var up = buildPos + Vector3.up + upOffset;

            buildPos.y = Mathf.Max(position.y, up.y);
            Vis.Colliders(buildPos, 1.875f, colliders, layerMask, QueryTriggerInteraction.Collide);

            foreach(var collider in colliders)
            {
                var p = collider.GetComponentInParent<BuildingPrivlidge>();

                if (p != null && p.IsOlderThan(priv))
                    priv = p;
            }
            
            Pool.FreeList<Collider>(ref colliders);

            if (priv != null && !priv.authorizedPlayers.Any(x => x.userid == player.userID))
            {
                player.ChatMessage(msg("Building is blocked!", player.UserIDString));
                _canBuild = false;
            }

            return _canBuild;
        }
                
        #region Config
        bool Changed;
        string permName;

        void LoadVariables()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Building is blocked!"] = "<color=red>Building is blocked!</color>",
            }, this);

            foreach (var construction in PrefabAttribute.server.GetAll<Construction>())
                if (construction.grades[(int)BuildingGrade.Enum.Twigs] != null || construction.hierachyName.Contains("ladder"))
                    constructions[construction.hierachyName] = Convert.ToBoolean(GetConfig("Constructions", string.Format("Allow {0}", construction.hierachyName), true));

            permName = Convert.ToString(GetConfig("Settings", "Bypass Permission Name", "buildrevert.bypass"));

            if (!string.IsNullOrEmpty(permName))
                permission.RegisterPermission(permName, this);

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        #endregion
    }
}