using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Underworld", "nivex", "0.1.1", ResourceId = 25895)]
    [Description("Teleports admins/developer under the world when they disconnect.")]
    public class Underworld : RustPlugin
	{
        void OnServerInitialized()
        {
            LoadVariables();

            if (!teleportToLand)
                Unsubscribe(nameof(OnPlayerSleepEnded));
                
        }

		void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			if (player.IsAdmin || player.IsDeveloper)
				player.Teleport(new Vector3(player.transform.position.x, TerrainMeta.HeightMap.GetHeight(player.transform.position) - 5f, player.transform.position.z));
		}
		
		void OnPlayerSleepEnded(BasePlayer player)
		{
			if (player.IsAdmin || player.IsDeveloper)
			{
				float y = TerrainMeta.HeightMap.GetHeight(player.transform.position);
				
				if (player.transform.position.y == y - 5f) 
					player.Teleport(new Vector3(player.transform.position.x, y + 1f, player.transform.position.z));
			}
		}

        #region Config
        bool Changed;
        bool teleportToLand;

        void LoadVariables()
        {
            teleportToLand = Convert.ToBoolean(GetConfig("Settings", "Teleport Admin/Developer Back To Land After Waking Up", true));
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
        #endregion
    }
}