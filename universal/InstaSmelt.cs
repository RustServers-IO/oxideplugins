using Oxide.Core;
using System;
using System.Reflection;
using Assets.Scripts.Core;
using System.Collections.Generic;
using uLink;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("InstaSmelt","Jojolepro","1.1.0")]
	[Description("Insta Smelting Furnaces.")]

	public class InstaSmelt : HurtworldPlugin
	{
		void OnServerInitialized()
		{
			GlobalItemManager GIM = Singleton<GlobalItemManager>.Instance;
			GIM.GetItem ((int)EItemCode.IronOre).GetType ().BaseType.GetProperty ("ResourceTransitions").SetValue (GIM.GetItem ((int)EItemCode.IronOre) as IItem, GenerateTransition ((int)EItemCode.ShapedIron), null);
			GIM.GetItem ((int)EItemCode.Metal2Ore).GetType ().BaseType.GetProperty ("ResourceTransitions").SetValue (GIM.GetItem ((int)EItemCode.Metal2Ore) as IItem, GenerateTransition((int)EItemCode.ShapedMetal2), null);
			GIM.GetItem ((int)EItemCode.Metal3Ore).GetType ().BaseType.GetProperty ("ResourceTransitions").SetValue (GIM.GetItem ((int)EItemCode.Metal3Ore) as IItem, GenerateTransition ((int)EItemCode.ShapedMetal3), null);
			GIM.GetItem ((int)EItemCode.Metal4Ore).GetType ().BaseType.GetProperty ("ResourceTransitions").SetValue (GIM.GetItem ((int)EItemCode.Metal4Ore) as IItem, GenerateTransition ((int)EItemCode.ShapedMetal4), null);
		}
		protected List<EntityStatResourceTransition> GenerateTransition(int result){
			EntityStatResourceTransition tr = new EntityStatResourceTransition{
				SourceEffectType = EEntityFluidEffectType.InternalTemperature,
				MinValue = 500f,
				MaxValue = 15000f,
				RequiredDuration = 1f,//Does not work
				ResultItemId = result,
				Description = "Can be smelted at 500 degrees",//Server side only
				TransitionStack = true//This is the magic bit that makes that plugin work
			};
			List<EntityStatResourceTransition> r = new List<EntityStatResourceTransition>();
			r.Add(tr);
			return r;
		}
	}
}
