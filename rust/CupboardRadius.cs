using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Facepunch;

namespace Oxide.Plugins {

	[Info("CupboardRadius", "theconezone (formerly playrust.io / dcode)", "2.1.0", ResourceId = 1316)]
	[Description("Fix for Facepunch treating modded servers like second class citizens... and cupboard placement.")]
	public class CupboardRadius : RustPlugin {

		#region configuration values

		private string prefixColor = "#FF0000";
		private ulong maxTime = 300, defaultTime = 300;
		private bool requirePermissions = true, cupboardNotifications = true, ignoreInfluence = true, commandEnabled = true;
		private int radius = 25;
		private MethodInfo reflectionFillPlacement;
        private Dictionary<ulong, bool> notified; //map of all users, with the key userID and notification status

		#endregion

		#region utility functions

		public string GetMessage(BasePlayer player, string key, params object[] args) => string.Format(lang.GetMessage(key, this, player.UserIDString), args);
		public void ChatOutput(BasePlayer player, string message) => PrintToChat(player, GetMessage(player, "MessageFormat", prefixColor, message));
		public void MessageOutput(BasePlayer player, string key, params object[] args) => ChatOutput(player, GetMessage(player, key, args));

		public bool HasPermission(BasePlayer player, string permissionName) => !requirePermissions || permission.UserHasPermission(player.UserIDString, permissionName);

        private void NotifyUserOfPlugin(BasePlayer player) {

            //check if user has permission
            if (HasPermission(player, "cupboardradius.use")) {

                //check if we're using the /cupboard command
                if (commandEnabled) {

                    //output /cupboard command info
                    MessageOutput(player, "CupboardPlacementNotificationCommandEnabled");

                } else {

                    //output stacked cupboard info
                    MessageOutput(player, "CupboardPlacementNotificationCommandDisabled");

                }

            }

        }

		private bool TryConvert<F, T>(F from, out T to, T valueOnFail = default(T)) {

			//we succeed by default
			bool result = true;

			if (from != null) {

				try {

					//try to convert the parameter from from type F to type T
					to = (T) Convert.ChangeType(from, typeof(T));

				} catch {

					//failure, default to fail value and return false
					to = valueOnFail;
					result = false;

				}

			} else {

				to = valueOnFail;
				result = false;

			}

			return result;

		}

		public void FillPlacement(Planner planner, ref Construction.Target target, Construction component) {

			object[] arguments = new object[] { target, component };

			reflectionFillPlacement.Invoke(planner, arguments);

			target = (Construction.Target) arguments[0];

		}

		#endregion

		#region plugin hooks

		void OnServerInitialized() {

			//Grab Planner.FillPlacement 
			//TODO: Remove reliance on reflection and implement FillPlacement ourselves
			reflectionFillPlacement = typeof(Planner).GetMethod("FillPlacement", BindingFlags.Instance | BindingFlags.NonPublic);

			//check if FillPlacement was grabbed
			if (reflectionFillPlacement != null) {

			    //Register localization
			    lang.RegisterMessages(new Dictionary<string, string>() {

                    //format of all messages
				    ["MessageFormat"] = "<color={0}>[CupboardRadius]</color>: {1}",

                    //messages for /command
                    ["CupboardPlacementStart"] = "You can now place overlapping cupboards for {0} seconds. Select a cupboard on your toolbar and left click to place. <color=red>Even if the cupboard model is red you can still place!</color>",
				    ["CupboardPlacementTimer"] = "Overlapping cupboard placement ends in {0} seconds.",
                    ["CupboardPlacementEnd"] = "You can no longer place overlapping cupboards.",
                    ["CupboardPlacementEndPlugin"] = "The plugin is being unloaded. You can no longer place overlapping cupboards.",
                    ["NoPermission"] = "You do not have permission to use this command.",
				    ["CommandUsage"] = "USAGE: /cupboard <time in seconds between 0 and {0}>",
                    ["CupboardPlacementNotificationCommandEnabled"] = "This server supports cupboard stacking. Use /cupboard to enable cupboard stacking mode.",
                    ["CupboardPlacementNotificationCommandDisabled"] = "This server supports cupboard stacking. You may place cabinets where you are authorized even though they are red when you try to place them.",
                    ["CommandDisabled"] = "This server has cupboard stacking always enabled. You do not need to use /cupboard before placing stacked cupboards.",

                    //cupboard placement feedback
                    ["NoAuthorization"] = "You do not have building privileges.",
				    ["ConstructionFailed"] = "Unable to build cupboard: {0}",

                    //cupboard notifications
                    ["CupboardPlacementNotificationCommandEnabled"] = "This server supports cupboard stacking. Use /cupboard to enable cupboard stacking mode.",
                    ["CupboardPlacementNotificationCommandDisabled"] = "This server supports cupboard stacking. You may place cabinets whenever you are authorized."

                }, this);

			    //Register permissions
			    permission.RegisterPermission("cupboardradius.use", this);

			    //Load or create configuration
			    LoadConfig();
			    LoadDefaultConfig();

                //check if stacked cupboard placement is always enabled
                if (!commandEnabled) {

                    //Add cupboard placement tool to all authorized players
                    foreach (BasePlayer player in BasePlayer.activePlayerList) {

                        if (HasPermission(player, "cupboardradius.use")) {

                            //create and start cupboard tool for current player (timer value doesn't matter when command is disabled).
                            player.gameObject.AddComponent<CupboardPlacementTool>().StartUsage(player, this);

                        }

                    }

                }

                //check if we're outputting cupboard notifications when a user places the first cupboard
                if (cupboardNotifications) {

                    notified = new Dictionary<ulong, bool>();

                    //add all connected users with a notification status of false
                    BasePlayer.activePlayerList.ForEach((player) => notified.Add(player.userID, false));

                }

			    //TODO: temporary hack
			    OldCupboardRadiusOnServerInitialized();

			} else {

                //Nope, unload the plugin. We need FillPlacement to do anything.
                PrintError("Unable to reflect Planner.FillPlacement, unloading...");
                plugins.PluginManager.RemovePlugin(this);

            }

		}

		//TODO: Clean this mess up
		protected override void LoadDefaultConfig() {

			//Select current configuration values (or defaults, if necessary)
			Config["RequirePermissions"] = Config["RequirePermissions"] ?? requirePermissions;
			Config["MaxPlacementTime"] = Config["MaxPlacementTime"] ?? defaultTime;
			Config["DefaultPlacementTime"] = Config["DefaultPlacementTime"] ?? maxTime;
			Config["PluginPrefixColor"] = Config["PluginPrefixColor"] ?? prefixColor;
			Config["CupboardNotification"] = Config["CupboardNotification"] ?? cupboardNotifications;
			Config["CupboardRadius"] = Config["CupboardRadius"] ?? radius;
            Config["IgnoreInfluenceRestriction"] = Config["IgnoreInfluenceRestriction"] ?? ignoreInfluence;
            Config["CommandEnabled"] = Config["CommandEnabled"] ?? commandEnabled;

            try {

				//Load configuration values
				requirePermissions = Convert.ToBoolean(Config["RequirePermissions"]);
				maxTime = Convert.ToUInt64(Config["MaxPlacementTime"]);
				defaultTime = Convert.ToUInt64(Config["DefaultPlacementTime"]);
				prefixColor = (string) Config["PluginPrefixColor"];
				cupboardNotifications = Convert.ToBoolean(Config["CupboardNotification"]);
				radius = Convert.ToInt32(Config["CupboardRadius"]);
				ignoreInfluence = Convert.ToBoolean(Config["IgnoreInfluenceRestriction"]);
                commandEnabled = Convert.ToBoolean(Config["CommandEnabled"]);

            } catch {

				PrintError("One or more configuration values could not be converted. Setting to default...");

				//backup old configuration
				Config.Save(Config.Filename + ".bak");

				//delete old configuration
				Config["RequirePermissions"] = requirePermissions;
				Config["MaxPlacementTime"] = defaultTime;
				Config["DefaultPlacementTime"] = maxTime;
				Config["PluginPrefixColor"] = prefixColor;
				Config["CupboardNotification"] = cupboardNotifications;
				Config["CupboardRadius"] = radius;
                Config["IgnoreInfluenceRestriction"] = ignoreInfluence;
                Config["CommandEnabled"] = commandEnabled;

            }

		}

		void Unloaded() {

			//Remove all active CupboardPlacementTool from all players
			Array.ForEach(UnityEngine.Object.FindObjectsOfType<CupboardPlacementTool>(), (tool) => {

                //check if player is currently connected
                if (tool.player.IsConnected) {

                    //let player know why they can no longer place stacked cabinets
                    MessageOutput(tool.player, "CupboardPlacementEndPlugin");

                }

                //remove current cupboard placement tool
                tool.Destroy();

            });

		}

        #endregion

        #region game hooks

        void OnPlayerInit(BasePlayer player) {

            //check if /cupboard is disabled on this server, and if player is authorized to place stacked cupboards
            if (!commandEnabled && HasPermission(player, "cupboardradius.use")) {

                //create and start cupboard tool for current player (timer value doesn't matter when command is disabled).
                player.gameObject.AddComponent<CupboardPlacementTool>().StartUsage(player, this);

            }

            //check if plugin notification is enabled
            if (cupboardNotifications) {

                //user has not been notified of the plugin
                notified.Add(player.userID, false);

            }

        }

        void OnPlayerDisconnected(BasePlayer player, string reason) {

            //check if plugin notification is enabled
            if (cupboardNotifications) {

                //user no longer needs to be notified
                notified.Remove(player.userID);

            }

        }

        void OnEntityBuilt(Planner planner, GameObject @object) {

            BasePlayer player;

            //check if we're notifying users of plugin functionality on first placement, and if we've not already notified the user
            if (cupboardNotifications && @object.ToBaseEntity().ShortPrefabName == "cupboard.tool.deployed" && (player = planner.GetOwnerPlayer()) != null && !notified[player.userID]) {

                //output plugin information
                NotifyUserOfPlugin(player);

                //user has been notified once, notify them no longer
                notified[player.userID] = true;

            }

        }

        #endregion

        #region GameObject components

        public class CupboardPlacementTool : MonoBehaviour {

			private CupboardRadius plugin;
			private ulong timer, startTimer;
			public BasePlayer player;
			private float lastUpdate;
			private float halfTime, thirdTime;
			private int alertCounter = 0;

			//called when component is added before the first update
			public void StartUsage(BasePlayer player, CupboardRadius plugin, ulong timer = 0) {

				this.plugin = plugin;
				this.player = player;

                lastUpdate = UnityEngine.Time.realtimeSinceStartup;

                if (plugin.commandEnabled) {

                    this.timer = timer;
                    startTimer = timer;
                    halfTime = Mathf.Ceil(timer * 0.5f);
                    thirdTime = Mathf.Ceil(timer * 0.25f);

                    plugin.MessageOutput(player, "CupboardPlacementStart", timer);

                    InvokeRepeating("TimeUpdate", 0.0f, 1.0f);

                }

			}

			public void StopUsage() {

                if (player.IsConnected) {

                    plugin.MessageOutput(player, "CupboardPlacementEnd");

                }

                Destroy();

			}

			//called when component is about to be destroyed
			void OnDestroy() {

                if (plugin.commandEnabled) {

                    CancelInvoke("TimeUpdate");

                }

			}

			void FixedUpdate() {

				//check if we need to continue updating
				if (player.IsConnected) {

                    //check if player is awake
                    if (!player.IsSleeping()) {

                        float now = UnityEngine.Time.realtimeSinceStartup;
                        float elapsed = now - lastUpdate;

                        //We use WasJustPressed because we only care when FIRE_PRIMARY is completely pressed and released.
                        if (player.serverInput.IsDown(BUTTON.FIRE_PRIMARY)) {

                            //check for spam (throttle placement)
                            if (elapsed >= 0.5f) {

                                //update elapsed time
                                lastUpdate = now;

                                if (IsHoldingCabinet() && player.CanInteract()) {

                                    //check for building authorization (at least one cabinet)
                                    if (player.CanBuild()) {

                                        DeployCupboard();

                                    } else {

                                        plugin.MessageOutput(player, "NoAuthorization");

                                    }

                                }

                            }

                        }

                    } else {

                        //if /cupboard is enabled, and the user is asleep, we stop usage. Otherwise we ignore sleeping.
                        if (plugin.commandEnabled) {

                            //player is asleep, stop using /cupboard
                            StopUsage();

                        }

                    }

				} else {

					//player is no longer connected, destroy the cupboard tool
					Destroy();

				}

			}

			//actually deploys the cupboard
			private void DeployCupboard() {

				//Get the current object the player is holding (if any)
				HeldEntity entity = player.GetHeldEntity();

				//Check if the player is holding a Planner (IE: About to place a Deployable)
				if (entity is Planner) {

					//Get the Planner the player is holding.
					Planner planner = (Planner) entity;

					//Get the item the player is attempting to deploy (if any)
					ItemModDeployable deployableItem = planner.GetModDeployable();

					//Check if the player is trying to deploy a cupboard
					if (deployableItem != null && deployableItem.name == "cupboard.tool.item") {

						//Get Construction object for the cupboard
						//TODO: prefab ID rarely if ever changes, optimize this
						Construction component = PrefabAttribute.server.Find<Construction>(planner.GetDeployable().prefabID);
						
						//Create Construction target for the given target block
						Construction.Target target = default(Construction.Target);
						target.ray = player.eyes.BodyRay();
						target.player = player;

						//Update Construction target (aligns with ground, finds matching socket)
						plugin.FillPlacement(planner, ref target, component);

						if (target.valid) {

							using (ProtoBuf.CreateBuilding createBuilding = Pool.Get<ProtoBuf.CreateBuilding>()) {

								createBuilding.blockID = component.prefabID;
								createBuilding.ray = target.ray;
								createBuilding.onterrain = target.onTerrain;
								createBuilding.position = target.position;
								createBuilding.normal = target.normal;
								createBuilding.rotation = target.rotation;

								planner.DoBuild(createBuilding);

							}

						} else {

							plugin.MessageOutput(player, "ConstructionFailed", Construction.lastPlacementError);

						}

					}

				}

			}

			//called once every second if plugin.commandEnabled is true
			private void TimeUpdate() {

				timer--;

				if ((alertCounter == 0 && timer <= halfTime) || (alertCounter == 1 && timer <= thirdTime)) {

					alertCounter++;

					plugin.MessageOutput(player, "CupboardPlacementTimer", timer);

				} else if (timer == 0) {

					StopUsage();

				}

			}

			//Returns true if the player is holding a cabinet
			private bool IsHoldingCabinet() => player.GetActiveItem()?.info?.name == "cupboard.tool.item";

			//helper to destroy object
			public void Destroy() => Destroy(this);

		}

		#endregion

		#region chat commands

		[ChatCommand("cupboard")]
		void CupboardCommand(BasePlayer player, string command, string[] args) {

			//check if the user has permission to use the command, or if permissions are disabled
			if (HasPermission(player, "cupboardradius.use")) {

                //check if /cupboard is necessary
                if (commandEnabled) {

                    //get the active CupboardTool for the player (if any)
                    CupboardPlacementTool tool = player.GetComponent<CupboardPlacementTool>();

                    //check if the player has *NO* active cupboard tool -- create one!
                    if (tool == null) {

                        //time in seconds the cupboard tool is active
                        ulong timer = defaultTime;

                        //Check if we have a valid time in seconds the tool will be active.
                        if (args.Length == 0 || (args.Length == 1 && TryConvert(args[0], out timer)) && timer <= maxTime) {

                            //give the player a cupboard tool
                            tool = player.gameObject.AddComponent<CupboardPlacementTool>();

                            //start the tool
                            tool.StartUsage(player, this, timer);

                        } else {

                            //output command usage
                            MessageOutput(player, "CommandUsage", maxTime);

                        }

                    } else {

                        //Player has an active cupboard tool, disable it
                        tool.StopUsage();

                    }

                } else {

                    //using /cupboard is not necessary, you can always place cabinets.
                    MessageOutput(player, "CommandDisabled");

                }

			} else {

				//inform the user they can't use this cool command
				SendReply(player, GetMessage(player, "NoPermission"));

			}

		}

		#endregion

		#region Untouched CupboardRadius (TODO: Update, clean)

		private class CupboardRadiusPersistence : MonoBehaviour {
			public bool influenceIgnored = false;
			public SocketMod[] influenceBackup = null;
		}

		private CupboardRadiusPersistence pst = null;
		private bool initialized = false;

		private void OldCupboardRadiusOnServerInitialized() {
			if (initialized)
				return;

			Puts("Using a radius of {0}, {1} influence restrictions", radius, ignoreInfluence ? "ignoring" : "not ignoring");

			bool reloaded = false;
			foreach (var prevPst in ServerMgr.Instance.gameObject.GetComponents<MonoBehaviour>()) {
				if (prevPst.GetType().Name == "CupboardRadiusPersistence") {
					reloaded = true;
					pst = ServerMgr.Instance.gameObject.AddComponent<CupboardRadiusPersistence>();
					pst.influenceIgnored = (bool) prevPst.GetType().GetField("influenceIgnored").GetValue(prevPst);
					pst.influenceBackup = (SocketMod[]) prevPst.GetType().GetField("influenceBackup").GetValue(prevPst);
					UnityEngine.Object.Destroy(prevPst);
					break;
				}
			}
			if (!reloaded)
				pst = ServerMgr.Instance.gameObject.AddComponent<CupboardRadiusPersistence>();

			var bpts = UnityEngine.Object.FindObjectsOfType<BuildPrivilegeTrigger>();
			var updated = 0;
			foreach (var bpt in bpts)
				if (updateTrigger(bpt))
					++updated;

			Puts("Updated {0} of {1} cupboards to use a sphere trigger", updated, bpts.Length);

			if (bpts.Length > 0)
				updateInfluence(bpts[0].privlidgeEntity.prefabID);

			initialized = true;
		}

		void OnEntitySpawned(BaseNetworkable ent) {
			if (!initialized || !(ent is BuildingPrivlidge))
				return;

			updateInfluence(ent.prefabID);

			var trig = ent.GetComponentInChildren<BuildPrivilegeTrigger>();
			if (trig == null)
				NextTick(() => {
					trig = ent.GetComponentInChildren<BuildPrivilegeTrigger>();
					if (trig == null) {
						PrintWarning("Failed to update BuildingPrivlige: Missing BuildPrivilegeTrigger");
						return;
					}
					updateTrigger(trig);
				});
			else
				updateTrigger(trig);
		}

		private bool updateTrigger(BuildPrivilegeTrigger bpt) {
			var col = bpt.GetComponent<UnityEngine.Collider>();
			var wasTrigger = true;
			if (col != null) { // should always be the case
				if (col is SphereCollider && Mathf.Approximately((col as SphereCollider).radius, radius))
					return false; // Already a sphere with that radius
				wasTrigger = col.isTrigger;
				UnityEngine.Object.Destroy(col);
			}
			col = bpt.gameObject.AddComponent<SphereCollider>();
			col.transform.localPosition = Vector3.zero;
			col.transform.localScale = Vector3.one;
			(col as SphereCollider).radius = radius;
			col.isTrigger = wasTrigger;
			return true;
		}

		private void updateInfluence(uint privlidgePrefabID) {
			if (ignoreInfluence == pst.influenceIgnored)
				return;
			var attr = PrefabAttribute.server.Find(privlidgePrefabID);
			var socketBases = attr.Find<Socket_Base>();
			if (socketBases.Length < 1) {
				PrintWarning("Failed to update cupboard influence: Missing Socket_Base attribute");
				return;
			}
			var socketBase = socketBases[0];
			if (ignoreInfluence) {
				if (pst.influenceBackup == null)
					pst.influenceBackup = socketBase.socketMods;
				socketBase.socketMods = socketBase.socketMods.Where(mod => mod.FailedPhrase.english != "You're trying to place too close to another cupboard").ToArray();
				pst.influenceIgnored = true;
				Puts("Cupboard influence restrictions are now ignored");
			} else {
				socketBase.socketMods = pst.influenceBackup;
				pst.influenceIgnored = false;
				Puts("Cupboard influence restrictions are no longer ignored");
			}
		}

		#endregion

	}

}
