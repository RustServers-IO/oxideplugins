//HighWallBarricades.cs

using System;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("HighWallBarricades", "Guilty Spark", "1.0.0")]
    class HighWallBarricades : RustPlugin
    {
		const string HighStoneWallName = "assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab";
		
		const string HighWoodWallName = "assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab";
		
		const float defaultBarricadeHealthPercentage = 0.20f;
		
		const float barricadeHealthPercentageMinimum = 0.10f;
		
		const float barricadeHealthPercentageMaximum = 0.90f;

		const int defaultDecayTime = 600;
		
		const int decayTimeMinimum = 60;
		
		const int decayTimeMaximum = 28800;
		
		float barricadeHealthPercentage = defaultBarricadeHealthPercentage;
		
		int decayTime = defaultDecayTime;
		
		void Init()
        {
			barricadeHealthPercentage = GetConfigEntry<float>("health", defaultBarricadeHealthPercentage);
			
            decayTime = GetConfigEntry<int>("decaytime", defaultDecayTime);
        }
		
		protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file for High Wall Barricades");
            
			Config.Clear();
			
			Config["health"] = defaultBarricadeHealthPercentage;
            
			Config["decaytime"] = defaultDecayTime;
			
            SaveConfig();
        }
		
		T GetConfigEntry<T>(string configEntry, T defaultValue)
        {
            if ( Config[configEntry] == null )
            {
                Config[configEntry] = defaultValue;
				
                SaveConfig();
            }
			
            return (T) Convert.ChangeType(Config[configEntry], typeof(T));
        }
		
		[ConsoleCommand("hwb.health")]
        void commandSetBarricadeHealth(ConsoleSystem.Arg arg)
        {
			BasePlayer player = arg.Player();
			
			if ( player != null && !player.IsAdmin ) return;
			
			float value;
			
			if ( arg.HasArgs() && float.TryParse(arg.Args[0], out value) )
			{
				if ( value < barricadeHealthPercentageMinimum ) value = barricadeHealthPercentageMinimum;
				else if ( value > barricadeHealthPercentageMaximum ) value = barricadeHealthPercentageMaximum;
				
				barricadeHealthPercentage = value;
				
				Config["health"] = barricadeHealthPercentage;
				
				SaveConfig();
			}
			
            arg.ReplyWith("hwb.health: " + Math.Round(barricadeHealthPercentage * 100.0f).ToString() + "%");
        }
		
		[ConsoleCommand("hwb.decaytime")]
        void commandSetDecayTime(ConsoleSystem.Arg arg)
        {
			BasePlayer player = arg.Player();
			
			if ( player != null && !player.IsAdmin ) return;
			
			int value;
			
			if ( arg.HasArgs() && int.TryParse(arg.Args[0], out value) )
			{
				if ( value < decayTimeMinimum ) value = decayTimeMinimum;
				else if ( value > decayTimeMaximum ) value = decayTimeMaximum;
				
				decayTime = value;
				
				Config["decaytime"] = decayTime;
				
				SaveConfig();
			}
			
            arg.ReplyWith("hwb.decaytime: " + decayTime.ToString() + " second(s)");
        }
		
		void OnEntityBuilt(Planner plan, GameObject go)
		{
			if ( plan == null || go == null ) return;
			
			string name = go.name;
			
			if ( name == null || ( name.Length != HighStoneWallName.Length && name.Length != HighWoodWallName.Length ) ) return;
			else if ( name != HighStoneWallName && name != HighWoodWallName ) return;
			
			BasePlayer ownerPlayer = plan.GetOwnerPlayer();
			
			if ( ownerPlayer == null ) return;
			
			if ( ownerPlayer.HasPlayerFlag(BasePlayer.PlayerFlags.InBuildingPrivilege) ) return;
			
			BaseCombatEntity entity = go.GetComponent<BaseCombatEntity>();
			
			if ( entity == null ) return;
			
			float barricadeHealth = entity.MaxHealth() * barricadeHealthPercentage;
			
			entity.health = barricadeHealth;
			
			entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
			
			InvokeHandler.Invoke(entity, (Action) delegate
			{
				if ( entity.health > barricadeHealth ) return;
				
				entity.Kill();
			}, decayTime);
		}
    }
}