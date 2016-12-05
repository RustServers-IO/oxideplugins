using Oxide.Core;
using System.Linq;

namespace Oxide.Plugins
{
	[Info( "OnlineQuarries", "mvrb", "1.0.0" )]
	class OnlineQuarries : RustPlugin
	{
		void OnPlayerDisconnect( BasePlayer player, string reason )
		{
			foreach ( var q in BaseNetworkable.serverEntities.Where( p => ( p as MiningQuarry )?.OwnerID == player.userID ).ToList() )
			{
				var quarry = q as MiningQuarry;
				if ( quarry )
				{
					quarry.SetFlag( BaseEntity.Flags.On, false, false );
					quarry.engineSwitchPrefab.instance.SetFlag( BaseEntity.Flags.On, false, false );
					quarry.SendNetworkUpdate( BasePlayer.NetworkQueue.Update );
					quarry.engineSwitchPrefab.instance.SendNetworkUpdate( BasePlayer.NetworkQueue.Update );
					quarry.CancelInvoke( "ProcessResources" );
				}
			}
		}
	}
}