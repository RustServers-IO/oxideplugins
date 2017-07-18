using System;
using Rust;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CupboardNoDecay", "Dubz", "1.0.4", ResourceId = 2341)]
    [Description("Decay items without build priv")]
    public class CupboardNoDecay : RustPlugin
    {
		ConfigData configData;
		
		void OnServerInitialized()
        {
            LoadVariables();
			permission.RegisterPermission("CupboardNoDecay.Enabled", this);
		}
		
		void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
		{
			if (!hitInfo.damageTypes.Has(Rust.DamageType.Decay)) return;
			var multiplier = 1.0f;

			if (CupboardPrivlidge(entity.transform.position))
			{
				multiplier = 0.0f;
				//var block = entity as BuildingBlock;
				//if (block != null){
				//	switch (block.grade)
				//	{
				//		case BuildingGrade.Enum.Twigs:
				//			multiplier = 1.0f;
				//			break;
				//	};
				//}
			}
			
			hitInfo.damageTypes.Scale(Rust.DamageType.Decay, multiplier);
			
        }
		
					
		private bool CupboardPrivlidge(Vector3 position)
        {
		    float distance = 1f;
            List<BaseEntity> list = new List<BaseEntity>();
            Vis.Entities<BaseEntity>(position, distance, list);

            foreach (var ent in list)
            {
			    var buildingPrivlidge = ent.GetComponentInParent<BuildingPrivlidge>();
                if (buildingPrivlidge != null)
                {
					if (configData.CheckAuth)
					{
						foreach (var auth in buildingPrivlidge.authorizedPlayers.Select(x => x.userid).ToArray())
						{
							if (auth.ToString() == buildingPrivlidge.OwnerID.ToString())
							{
								if (HasPerm(auth.ToString()))
								{
									return true;
								}
							}
						}
						return false;
					}
					
					return true;
                }
            }
		    return false;
        }
		
		class ConfigData
        {
            public bool CheckAuth { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                CheckAuth = true,
            };
            SaveConfig(config);
        }

        void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        private bool HasPerm(string steamId) => permission.UserHasPermission(steamId, "CupboardNoDecay.Enabled");
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
	}
}