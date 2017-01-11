using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Healthy Guns", "Evanonian", "1.0.2", ResourceId = 2262)]
    public class HealthyGuns : CovalencePlugin
    {
		private bool isDebug = false;
		private Timer refresher;
		
		void OnServerInitialized() 
		{
			RefreshLootContainers();
		}
		void Loaded() 
		{
			refresher = timer.Repeat(60, 0, () => RefreshLootContainers());
			Puts("Overriding default weapon conditions.");
		}
		void Unload()
		{
			if (refresher != null)
            {
                refresher.Destroy();
				
				if(isDebug == true)
				{
					Puts("Destroying timer!");
				}
            }

			Puts("Using default weapon conditions.");
		}
		private void RefreshLootContainers()
        {
            int count = 0;
			
            foreach (var container in UnityEngine.Object.FindObjectsOfType<LootContainer>())
            {
                CheckIfExists(container);
                count++;
            }
			if(isDebug == true)
			{
				Puts("Refreshed " + count.ToString() + " loot containers.");
			}
        }
        void CheckIfExists(LootContainer container)
        {
            if (container != null)
            {
                RepairContainerContents(container);
            }
        }
		void RepairContainerContents(BaseNetworkable entity)
		{
			var lootspawn = entity as LootContainer; if (lootspawn == null) return;

            foreach (Item lootitem in lootspawn.inventory.itemList.ToList())
            {	
				var definition = ItemManager.FindItemDefinition(lootitem.info.itemid);
				
                if (lootitem.hasCondition && definition.category == ItemCategory.Weapon && lootitem.condition != lootitem.info.condition.max)
                {
                    lootitem.condition = lootitem.info.condition.max;
					
                    if(isDebug == true)	
					{
						Puts(lootitem + " condition set to " + lootitem.condition);
					}
                }
            }
        }
	}
}

