using Oxide.Core;
using System;
using System.Reflection;
using Assets.Scripts.Core;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using uLink;

namespace Oxide.Plugins
{
	[Info("InstaRespawn", "Jojolepro", "1.0.0")]
	[Description("Instant respawning; no cooldown.")]
	class InstaRespawn : HurtworldPlugin
	{
		private void OnPlayerConnected(PlayerSession session)
        {
            var stats = session.WorldPlayerEntity.GetComponent<EntityStats>();
			stats.GetFluidEffect(EEntityFluidEffectType.RespawnTime).SetValue(0.55f);
			((StandardEntityFluidEffect) stats.GetFluidEffect(EEntityFluidEffectType.RespawnTime)).MinValue(0.5f);
			((StandardEntityFluidEffect) stats.GetFluidEffect(EEntityFluidEffectType.RespawnTime)).MaxValue(0.6f);
        }
	}
}
