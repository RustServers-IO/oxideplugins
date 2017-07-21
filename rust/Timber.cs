using System;
using System.Collections.Generic;

using UnityEngine;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("Timber", "Mattparks", "0.1.6", ResourceId = 0)]
	[Description("Makes trees and cacti fall before being destroyed.")]
	class Timber : RustPlugin 
	{
		private static string woundedPrefab = "assets/bundled/prefabs/fx/player/beartrap_scream.prefab";
		private static string sound1Prefab = "assets/bundled/prefabs/fx/player/groundfall.prefab";
		private static string sound2Prefab = "assets/bundled/prefabs/fx/player/groundfall.prefab";
		private static string despawnPrefab = "assets/prefabs/misc/junkpile/effects/despawn.prefab";
		private static string stumpPrefab = "assets/bundled/prefabs/autospawn/collectable/stone/wood-collectable.prefab";
		private static string logPrefab = "assets/bundled/prefabs/autospawn/resource/logs_wet/dead_log_c.prefab";

		private float fallHarvestRatio => Config.Get<float>("FallHarvestRatio"); // The ratio of wood given from the fallen tree vs the original, a value of 0.0 gives all wood in fallen tree, 1.0 gives all wood in the original.
		private float fallHarvestScalar => Config.Get<float>("FallHarvestScalar"); // This value is used more to balance the plugin with vanilla, if trees give to much or not enough wood change this value.
		private float despawnLength => Config.Get<float>("DespawnLength"); // How long the fallen tree will sit on the ground before despawning.
		private float screamPercent => Config.Get<float>("ScreamPercent"); // The percent of trees that will scream when chopped down.
		private bool includeCacti => Config.Get<bool>("IncludeCacti"); // If cacti will be included in the timber plugin.
		private bool logToPlayer => Config.Get<bool>("LogToPlayer"); // If enabled the message "Timber!" will be displayed to the player when they chop down there first few trees.

		private void LoadDefaultConfig()
		{
			PrintWarning("Creating default configuration.");
			Config.Clear();
			Config["FallHarvestRatio"] = 0.3f; 
			Config["FallHarvestScalar"] = 1.6180339f; 
			Config["DespawnLength"] = 24.0f; 
			Config["ScreamPercent"] = 0.08f; 
			Config["IncludeCacti"] = true; 
			Config["LogToPlayer"] = true; // TODO
			SaveConfig();
		}

		protected override void LoadDefaultMessages()
		{
			// English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["TIMBER_ABOUT"] = "Timber is a plugin that animates the destruction of trees and cacti.",
			}, this, "en");
			
			// French
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["TIMBER_ABOUT"] = "Le bois est un plugin qui anime la destruction des arbres et des cactus.",
			}, this, "fr");

			// German
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["TIMBER_ABOUT"] = "Holz ist ein Plugin, das die Zerstörung von Bäumen und Kakteen animiert.",
			}, this, "de");

			// Russian
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["TIMBER_ABOUT"] = "Древесина - это плагин, который оживляет разрушение деревьев и кактусов.",
			}, this, "ru");

			// Spanish
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["TIMBER_ABOUT"] = "Timber es un plugin que anima la destrucción de árboles y cactus.",
			}, this, "es");
		}

		private void Init()
		{
			cmd.AddChatCommand("timber", this, TimberCmd);
		}

		private void TimberCmd(BasePlayer player, string command, string[] args)
		{
			if (player == null) 
			{
				return;
			}

			player.ChatMessage("<color=red>Timber " + Version + "</color>: by <color=green>mattparks</color>. " + lang.GetMessage("TIMBER_ABOUT", this, player.UserIDString));
		}
		
		private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
		{
			if (dispenser == null || entity == null || item == null)
			{
				return;
			}

			var gatherType = dispenser.gatherType.ToString("G");
			bool fallDefined = dispenser.GetComponent<FallControl>() != null;
			bool despawnDefined = dispenser.GetComponent<DespawnControl>() != null;

			if (fallDefined || despawnDefined)
			{
				item.amount = (int) (item.amount * fallHarvestScalar * (1.0f - fallHarvestRatio + 1.0f));
			}
			else if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
			{
				// TODO: Remove this from cacti.
				item.amount = (int) (item.amount * fallHarvestScalar * fallHarvestRatio);
			}
		}

		private void OnEntityKill(BaseNetworkable entity)
		{
			if (entity == null)
			{
				return;
			}

			bool fallDefined = entity.GetComponent<FallControl>() != null;
			bool despawnDefined = entity.GetComponent<DespawnControl>() != null;

			var entityPosition = entity.transform.position;
			var entityRotation = entity.transform.rotation;

			if (!fallDefined && !despawnDefined)
			{
				if (entity is TreeEntity || (includeCacti && StringPool.Get(entity.prefabID).Contains("cactus")))
				{
					if (new System.Random().NextDouble() <= screamPercent)
					{
						Effect.server.Run(woundedPrefab, entityPosition);
					}
					else 
					{
						Effect.server.Run(sound1Prefab, entityPosition);
					}

					Effect.server.Run(despawnPrefab, entityPosition);

					var newFalling = GameManager.server.CreateEntity(StringPool.Get(entity.prefabID), entityPosition, entityRotation, true);
					newFalling.transform.position += new Vector3(0.0f, 0.05f, 0.0f);

					var controlFall = newFalling.gameObject.AddComponent<FallControl>();
					controlFall.parentEntity = newFalling;
					controlFall.despawnLength = despawnLength;

					newFalling.Spawn(); // TODO: Fix console spam.
				}
			}
			else if (fallDefined && !despawnDefined)
			{
				Effect.server.Run(despawnPrefab, entityPosition);
				Effect.server.Run(sound1Prefab, entityPosition);

				var newFalling = GameManager.server.CreateEntity(StringPool.Get(entity.prefabID), entityPosition, entityRotation, true);

				var controlDespawn = newFalling.gameObject.AddComponent<DespawnControl>();
				controlDespawn.parentEntity = newFalling;

				newFalling.Spawn();
			}
		}

		public class FallControl : MonoBehaviour
		{
			private Vector3 displacement = new Vector3(0.0f, 0.0f, 0.0f);
			private float timeDespawn = 0.0f;
			private float currentSpeed = 0.0f;
			private bool startedFall = false;

			public BaseEntity parentEntity = null;
			public float despawnLength = 0.0f;

			private void Update()
			{
				if (parentEntity == null)
				{
					Destroy(gameObject);
					return;
				}

				timeDespawn += Time.deltaTime;
				
				// TODO: Render rotation from distance.

				if (Math.Abs(displacement.x) <= 80.0f) 
				{
					currentSpeed += 0.1f * Time.deltaTime;
					Vector3 deltaAngle = Vector3.left * currentSpeed;
					displacement += deltaAngle;
					gameObject.transform.rotation *= Quaternion.Euler(deltaAngle.x, deltaAngle.y, deltaAngle.z);
					gameObject.transform.hasChanged = true;

					parentEntity.SendNetworkUpdateImmediate();
				}
				else if (currentSpeed != 0.0f)
				{
					Effect.server.Run(sound2Prefab, gameObject.transform.position);
					currentSpeed = 0.0f;
				}

				if (!startedFall)
				{
					// float groundHeight = TerrainMeta.HeightMap.GetHeight(pos);
					var entityPosition = gameObject.transform.position;

					float stumpHeight = TerrainMeta.HeightMap.GetHeight(entityPosition);
					var stumpEntity = GameManager.server.CreateEntity(stumpPrefab, new Vector3(entityPosition.x, stumpHeight, entityPosition.z));
					stumpEntity.Spawn();

					currentSpeed = 0.0f;
					startedFall = true;
				}

				if (timeDespawn > despawnLength)
				{
					parentEntity.Kill();
				}
			}
		}

		public class DespawnControl : MonoBehaviour
		{
			private float timeDespawn = 0.0f;
			private float currentSpeed = 0.0f;

			public BaseEntity parentEntity = null;

			private void Update()
			{
				if (parentEntity == null)
				{
					Destroy(gameObject);
					return;
				}

				timeDespawn += Time.deltaTime;
				currentSpeed += 0.001f * Time.deltaTime;
				gameObject.transform.position += new Vector3(0.0f, -currentSpeed, 0.0f);
				gameObject.transform.hasChanged = true;

				parentEntity.SendNetworkUpdateImmediate();

				if (gameObject.transform.position.y < -10.0f)
				{
					parentEntity.Kill();
				}
			}
		}
	}
}
