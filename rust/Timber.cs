using System;
using System.Collections.Generic;

using UnityEngine;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("Timber", "Mattparks", "0.1.7", ResourceId = 2565)]
	[Description("Makes trees and cacti fall before being destroyed.")]
	class Timber : RustPlugin 
	{
		private readonly static float MAX_FIRST_DISTANCE = 18.0f;
		private readonly string PERMISSION_FIRST = "timber.first";

		private readonly static string soundWoundedPrefab = "assets/bundled/prefabs/fx/player/beartrap_scream.prefab";
		private readonly static string soundFallNormalPrefab = "assets/bundled/prefabs/fx/player/groundfall.prefab";
		private readonly static string soundFallLargePrefab = "assets/bundled/prefabs/fx/player/groundfall.prefab";
		private readonly static string soundGroundPrefab = "assets/bundled/prefabs/fx/player/groundfall.prefab";
		private readonly static string soundDespawnPrefab = "assets/bundled/prefabs/fx/player/groundfall.prefab";
		private readonly static string despawnPrefab = "assets/prefabs/misc/junkpile/effects/despawn.prefab";
		private readonly static string stumpPrefab = "assets/bundled/prefabs/autospawn/collectable/stone/wood-collectable.prefab";

		private float harvestStanding => Config.Get<float>("HarvestStanding"); // 
		private float harvestFallen => Config.Get<float>("HarvestFallen"); // 
		private float despawnLength => Config.Get<float>("DespawnLength"); // How long the fallen tree will sit on the ground before despawning.
		private float screamPercent => Config.Get<float>("ScreamPercent"); // The percent of trees that will scream when chopped down.
		private bool includeCacti => Config.Get<bool>("IncludeCacti"); // If cacti will be included in the timber plugin.
		private bool logToPlayer => Config.Get<bool>("LogToPlayer"); // If enabled the message "Timber!" will be displayed to the player when they chop down there first tree.

		private void LoadDefaultConfig()
		{
			PrintWarning("Creating default configuration.");
			Config.Clear();
			Config["HarvestStanding"] = 0.5f; 
			Config["HarvestFallen"] = 2.805f; 
			Config["DespawnLength"] = 30.0f; 
			Config["ScreamPercent"] = 0.06f; 
			Config["IncludeCacti"] = true; 
			Config["LogToPlayer"] = true;
			SaveConfig();
		}

		private void Init()
		{
			permission.RegisterPermission(PERMISSION_FIRST, this);

			// English messages.
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["TIMBER_ABOUT"] = "<color=red>Timber " + Version + "</color>: by <color=green>mattparks</color>. Timber is a plugin that animates the destruction of trees and cacti.",
				["TIMBER_FIRST"] = "<color=red>Timber!</color> Tree falling is not in vanilla Rust, read more from the command <color=green>/timber</color>",
			}, this, "en");

			// French messages.
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["TIMBER_ABOUT"] = "<color=red>Timber " + Version + "</color>: by <color=green>mattparks</color>. Le bois est un plugin qui anime la destruction des arbres et des cactus.",
				["TIMBER_FIRST"] = "<color=red>Bois!</color> L'arbre qui tombe n'est pas dans la rouille vanille, lis plus de la commande <color=green>/timber</color>",
			}, this, "fr");

			// German messages.
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["TIMBER_ABOUT"] = "<color=red>Timber " + Version + "</color>: by <color=green>mattparks</color>. Holz ist ein Plugin, das die Zerstörung von Bäumen und Kakteen animiert.",
				["TIMBER_FIRST"] = "<color=red>Bauholz!</color> Baum fallen ist nicht in Vanille Rust, lesen Sie mehr aus dem Befehl <color=green>/timber</color>",
			}, this, "de");

			// Russian messages.
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["TIMBER_ABOUT"] = "<color=red>Timber " + Version + "</color>: by <color=green>mattparks</color>. Древесина - это плагин, который оживляет разрушение деревьев и кактусов.",
				["TIMBER_FIRST"] = "<color=red>Древесина!</color> Падение дерева не в ванильном ржавчине, больше читайте из команды <color=green>/timber</color>",
			}, this, "ru");

			// Spanish messages.
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["TIMBER_ABOUT"] = "<color=red>Timber " + Version + "</color>: by <color=green>mattparks</color>. Timber es un plugin que anima la destrucción de árboles y cactus.",
				["TIMBER_FIRST"] = "<color=red>¡Madera!</color> Árbol que cae no está en el moho de la vainilla, leyó más del comando <color=green>/timber</color>",
			}, this, "es");
		}

		[ChatCommand("timber")]
		private void TimberCmd(BasePlayer player, string command, string[] args)
		{
			player.ChatMessage(lang.GetMessage("TIMBER_ABOUT", this, player.UserIDString));
		}

		private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
		{
			var gatherType = dispenser.gatherType.ToString("G");
			var fallDefined = dispenser.GetComponent<FallControl>() != null;
			var despawnDefined = dispenser.GetComponent<DespawnControl>() != null;

			// Does not change cacti when disabled.
			if (dispenser.containedItems.Count == 0)
			{
				return;
			}

			// Changes the harvest amount in objects that are falling or despawning.
			if (fallDefined || despawnDefined)
			{
				item.amount = (int) (item.amount * harvestFallen);
			}
			else if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
			{
				item.amount = (int) (item.amount * harvestStanding);
			}
		}

		private void OnEntityKill(BaseNetworkable entity)
		{
			bool fallDefined = entity.GetComponent<FallControl>() != null;
			bool despawnDefined = entity.GetComponent<DespawnControl>() != null;

			var entityPosition = entity.transform.position;
			var entityRotation = entity.transform.rotation;

			// Creates the fall behaviour if none is defined.
			if (!fallDefined && !despawnDefined)
			{
				if (entity is TreeEntity || (includeCacti && StringPool.Get(entity.prefabID).Contains("cactus")))
				{
					Effect.server.Run(despawnPrefab, entityPosition);

					if (logToPlayer)
					{
						foreach (var player in BasePlayer.activePlayerList)
						{
							float distance = Vector3.Distance(player.transform.position, entityPosition);
							
							if (distance < MAX_FIRST_DISTANCE)
							{
								if (!permission.UserHasPermission(player.UserIDString, PERMISSION_FIRST))
								{
									player.ChatMessage(lang.GetMessage("TIMBER_FIRST", this, player.UserIDString));
									permission.GrantUserPermission(player.UserIDString, PERMISSION_FIRST, this);
								}
							}
						}
					}

					var newFalling = GameManager.server.CreateEntity(StringPool.Get(entity.prefabID), entityPosition, entityRotation, true);

					var controlFall = newFalling.gameObject.AddComponent<FallControl>();
					controlFall.Load(newFalling, despawnLength, new System.Random().NextDouble() <= screamPercent);

					newFalling.Spawn();
				}
			}
			// Creates the despawn behaviour if fall is defined.
			else if (fallDefined && !despawnDefined)
			{
				Effect.server.Run(soundDespawnPrefab, entityPosition);

				// TODO: Effects down length of fallen tree.
				Effect.server.Run(despawnPrefab, entityPosition);

				var newFalling = GameManager.server.CreateEntity(StringPool.Get(entity.prefabID), entityPosition, entityRotation, true);

				var controlDespawn = newFalling.gameObject.AddComponent<DespawnControl>();
				controlDespawn.Load(newFalling);

				newFalling.Spawn();
			}
		}

		public class FallControl : MonoBehaviour
		{
			private readonly static float ACCELERATION_Y = 0.07f;
			private readonly static float RADIUS_OFFSET_SPEED = 0.02f;
			private readonly static float MIN_STUMP_RADIUS = 0.3f;
			private readonly static float LARGE_SOUND_HEIGHT = 10.0f;

			private BaseEntity parentEntity;
			private float entityHeight;
			private float despawnLength;

			private float colliderHeight;
			private float colliderRadius;
			private float targetAngle;

			private float currentSpeed;
			private Vector3 currentAngle;
			private float currentOffsetY;
			private float timeDespawn;

			public FallControl()
			{
				this.parentEntity = null;
				this.despawnLength = 30.0f;

				this.colliderHeight = 0.0f;
				this.colliderRadius = 0.0f;
				this.targetAngle = 82.0f;

				this.currentSpeed = 0.0f;
				this.currentAngle = new Vector3();
				this.currentOffsetY = 0.0f;
				this.timeDespawn = 0.0f;
			}

			public void Load(BaseEntity parentEntity, float despawnLength, bool scream)
			{
				this.parentEntity = parentEntity;
				this.despawnLength = despawnLength;

				var capsuleCollider = parentEntity.GetComponent<CapsuleCollider>();

				if (capsuleCollider != null)
				{
					this.colliderHeight = capsuleCollider.height;
					this.colliderRadius = capsuleCollider.radius;
					// TODO: Calculate target angle from terrain.
				}

				if (colliderRadius >= MIN_STUMP_RADIUS)
				{
					var stumpPosition = gameObject.transform.position;
					var stumpHeight = TerrainMeta.HeightMap.GetHeight(stumpPosition);
					var stumpEntity = GameManager.server.CreateEntity(stumpPrefab, new Vector3(stumpPosition.x, stumpHeight, stumpPosition.z));
					stumpEntity.Spawn();
				}

				if (scream)
				{
					Effect.server.Run(soundWoundedPrefab, gameObject.transform.position);
				}
				else 
				{
					if (colliderHeight >= LARGE_SOUND_HEIGHT)
					{
						Effect.server.Run(soundFallLargePrefab, gameObject.transform.position);
					}
					else
					{
						Effect.server.Run(soundFallNormalPrefab, gameObject.transform.position);
					}
				}
			}

			private void Update()
			{
				// Falls until the target angle has been reached.
				if (Math.Abs(currentAngle.x) <= targetAngle) 
				{
					currentSpeed += ACCELERATION_Y * Time.deltaTime;
					Vector3 deltaAngle = Vector3.left * currentSpeed;
					currentAngle += deltaAngle;
					gameObject.transform.rotation *= Quaternion.Euler(deltaAngle.x, deltaAngle.y, deltaAngle.z);
					gameObject.transform.hasChanged = true;

					if (currentOffsetY < colliderRadius)
					{
						currentOffsetY += RADIUS_OFFSET_SPEED * currentSpeed;
						parentEntity.transform.position += new Vector3(0.0f, RADIUS_OFFSET_SPEED * currentSpeed, 0.0f);
					}

					// TODO: Fix rendering rotation from far distance.
					parentEntity.SendNetworkUpdateImmediate();
				}
				// This is when the tree has hit the ground.
				else if (currentSpeed != 0.0f)
				{
					Effect.server.Run(soundGroundPrefab, gameObject.transform.position);
					currentSpeed = 0.0f;
				}
				else
				{
					timeDespawn += Time.deltaTime;
				}

				if (timeDespawn > despawnLength)
				{
					parentEntity.Kill();
				}
			}
		}

		public class DespawnControl : MonoBehaviour
		{
			private readonly static float ACCELERATION_Y = 0.001f;
			private readonly static float DESPAWN_HEIGHT = -15.0f;

			private BaseEntity parentEntity;
			private float currentSpeed;

			public DespawnControl()
			{
				this.parentEntity = null;
				this.currentSpeed = 0.0f;
			}

			public void Load(BaseEntity parentEntity)
			{
				this.parentEntity = parentEntity;
			}

			private void Update()
			{
				currentSpeed += ACCELERATION_Y * Time.deltaTime;
				gameObject.transform.position += new Vector3(0.0f, -currentSpeed, 0.0f);
				gameObject.transform.hasChanged = true;

				parentEntity.SendNetworkUpdateImmediate();

				if (gameObject.transform.position.y < DESPAWN_HEIGHT)
				{
					parentEntity.Kill();
				}
			}
		}
	}
}
