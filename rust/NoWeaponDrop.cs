using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoWeaponDrop", "Fujikura", "1.0.0", ResourceId = 1960)]
	[Description("Prevents dropping of active weapon when players start to die")]
    class NoWeaponDrop : RustPlugin
    {
		[PluginReference]
		Plugin RestoreUponDeath;
		
		private bool Changed = false;
		private bool usePermission;
		private string permissionName;
		private bool disableForROD;
		
		private object GetConfig(string menu, string datavalue, object defaultValue)
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
		
		void LoadVariables()
        {
			usePermission = Convert.ToBoolean(GetConfig("Settings", "Use permissions", false));
			permissionName = Convert.ToString(GetConfig("Settings", "Permission name", "noweapondrop.active"));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }
		
		protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }
		
		void Loaded()
		{
			LoadVariables();
			if (!permission.PermissionExists(permissionName)) permission.RegisterPermission(permissionName, this);
		}
		
		void OnPlayerDie(BasePlayer player, HitInfo info)
		{
			if (player == null || player.svActiveItemID == 0u)
				return;
			if(usePermission && !permission.UserHasPermission(player.UserIDString, permissionName))
				return;
			player.svActiveItemID = 0u;
			player.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
		}
	}
}

