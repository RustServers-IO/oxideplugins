using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Linq;
using Rust;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("TankCommander", "k1lly0u", "0.1.24", ResourceId = 2560)]
    class TankCommander : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin Friends, Clans;
        static TankCommander ins;

        private FieldInfo spectateFilter = typeof(BasePlayer).GetField("spectateFilter", (BindingFlags.Instance | BindingFlags.NonPublic));
        private static FieldInfo meshLookupField = typeof(MeshColliderBatch).GetField("meshLookup", BindingFlags.Instance | BindingFlags.NonPublic);

        private List<Controller> controllers = new List<Controller>();
        private List<ulong> passengers = new List<ulong>();

        private Dictionary<ulong, Controller> commanders = new Dictionary<ulong, Controller>();        
        private Dictionary<CommandType, BUTTON> controlButtons;

        private bool initialized;

        const string tankPrefab = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("tankcommander.admin", this);
            permission.RegisterPermission("tankcommander.use", this);
        }
        void OnServerInitialized()
        {
            ins = this;
            LoadVariables();
            ConvertControlButtons();
            initialized = true;
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BradleyAPC)
            {
                Controller commander = entity.GetComponent<Controller>();
                if (commander != null)
                    commander.ManageDamage(info);                
            }
        }
        void OnPlayerInput(BasePlayer player, InputState input)
        {            
            if (!initialized || player == null || commanders.ContainsKey(player.userID) || passengers.Contains(player.userID) || !HasPermission(player, "tankcommander.use")) return;

            if (configData.Inventory.Enabled && input.WasJustPressed(controlButtons[CommandType.Inventory]))
            {
                RaycastHit hit;
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 3f))
                {
                    Controller controller = hit.GetEntity()?.GetComponent<Controller>();
                    if (controller != null && !controller.HasCommander())
                        OpenTankInventory(player, controller);                       
                }
                return;
            }
            
            if (input.WasJustPressed(controlButtons[CommandType.EnterExit]))
            {
                if (player.IsFlying)
                {
                    SendReply(player, msg("is_flying", player.UserIDString));
                    return;
                }

                RaycastHit hit;
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 3f))
                {                    
                    Controller controller = hit.GetEntity()?.GetComponent<Controller>();
                    if (controller != null)
                    {
                        if (!controller.HasCommander())
                        {
                            commanders.Add(player.userID, controller);
                            controller.EnterTank(player);
                        }
                        else
                        {                            
                            if (configData.Passengers.Enabled)
                            {
                                BasePlayer commander = controller.GetCommander();

                                if (!configData.Passengers.UseFriends && !configData.Passengers.UseClans)
                                {
                                    controller.PassengerEnter(player);
                                    return;
                                }

                                if (configData.Passengers.UseFriends && AreFriends(commander.userID, player.userID))
                                {
                                    controller.PassengerEnter(player);
                                    return;
                                }

                                if (configData.Passengers.UseClans && IsClanmate(commander.userID, player.userID))
                                {
                                    controller.PassengerEnter(player);
                                    return;
                                }

                                SendReply(player, msg("not_friend", player.UserIDString));
                            }
                            else SendReply(player, msg("in_use", player.UserIDString));
                        }
                    }
                }
            }
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (commanders.ContainsKey(player.userID))            
                commanders[player.userID].ExitTank();            
        }
        void Unload()
        {
            foreach (var controller in controllers)            
                UnityEngine.Object.Destroy(controller);

            var objects = UnityEngine.Object.FindObjectsOfType<Controller>();
            if (objects != null)
            {
                foreach(var obj in objects)
                    UnityEngine.Object.Destroy(obj);
            }          
            controllers.Clear();            
        }
        #endregion

        #region Functions
        private T ParseType<T>(string type) => (T)Enum.Parse(typeof(T), type, true);
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm) || permission.UserHasPermission(player.UserIDString, "tankcommander.admin");

        private void ConvertControlButtons()
        {
            controlButtons = new Dictionary<CommandType, BUTTON>
            {
                [CommandType.EnterExit] = ParseType<BUTTON>(configData.Buttons.Enter),
                [CommandType.Lights] = ParseType<BUTTON>(configData.Buttons.Lights),
                [CommandType.Inventory] = ParseType<BUTTON>(configData.Buttons.Inventory),
                [CommandType.Boost] = ParseType<BUTTON>(configData.Buttons.Boost)
            };            
        }
        void OpenTankInventory(BasePlayer player, Controller controller)
        {
            player.inventory.loot.Clear();
            player.inventory.loot.entitySource = controller.entity;
            player.inventory.loot.itemSource = null;
            player.inventory.loot.AddContainer(controller.inventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "generic", null, null, null, null);
            player.SendNetworkUpdate();
        }
        private void StartSpectating(BasePlayer player, Controller controller, bool isOperator)
        {
            spectateFilter.SetValue(player, $"@123nofilter123");
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
            player.gameObject.SetLayerRecursive(10);
            player.CancelInvoke("InventoryUpdate");
            player.SendNetworkUpdateImmediate();

            timer.In(1.5f, () =>
            {
                player.transform.position = controller.transform.position;
                player.SetParent(controller.entity, 0);
                player.Command("client.camoffset", new object[] { new Vector3(0, 3.5f, 0) });

                SendReply(player, string.Format(msg("leave_help", player.UserIDString), configData.Buttons.Enter));

                if (isOperator)
                {
                    controller.enabled = true;
                    SendReply(player, string.Format(msg("boost_help", player.UserIDString), configData.Buttons.Boost));
                }
                else controller.SetPassengerActive(player);

                if (configData.Inventory.Enabled)
                    SendReply(player, string.Format(msg("inv_help", player.UserIDString), configData.Buttons.Inventory));
            });
        }
        private void EndSpectating(BasePlayer player, Controller commander, bool isOperator)
        {
            spectateFilter.SetValue(player, string.Empty);
            player.SetParent(null);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
            player.gameObject.SetLayerRecursive(17);
            player.InvokeRepeating("InventoryUpdate", 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));
            player.Command("client.camoffset", new object[] { new Vector3(0, 1.2f, 0) });
            player.transform.position = commander.transform.position + Vector3.up + (commander.transform.right * 3);

            if (isOperator)
                commanders.Remove(player.userID);
        }
        #endregion

        #region Component
        enum CommandType { EnterExit, Lights, Inventory, Boost }

        class Controller : MonoBehaviour
        {
            private BasePlayer player;

            public BradleyAPC entity;
            private Rigidbody rigidBody;

            private WheelCollider[] leftWheels;
            private WheelCollider[] rightWheels;

            public ItemContainer inventory;
            
            private float accelTimeTaken;
            private float accelTimeToTake = 3f;

            private float forwardTorque = 2000f;
            private float maxBrakeTorque = 50f;
            private float turnTorque = 1000f;

            private Dictionary<CommandType, BUTTON> controlButtons;
            private List<ulong> enteringPassengers = new List<ulong>();
            private List<BasePlayer> passengers = new List<BasePlayer>();

            private void Awake()
            {
                entity = GetComponent<BradleyAPC>();

                entity.enabled = false;
                enabled = false;

                var collider = entity.gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(3, 1, 5);                
                collider.isTrigger = true;

                gameObject.layer = (int)Layer.Reserved1;                

                rigidBody = entity.myRigidBody;
                leftWheels = entity.leftWheels;
                rightWheels = entity.rightWheels;

                controlButtons = ins.controlButtons;

                if (ins.configData.Inventory.Enabled)
                {
                    inventory = new ItemContainer();
                    inventory.ServerInitialize(null, ins.configData.Inventory.Size);
                    if ((int)inventory.uid == 0)
                        inventory.GiveUID();
                }
            }
            
            private void OnDestroy()
            {                                    
                if (player != null)
                    ExitTank();
                entity.Kill();
            }

            private void FixedUpdate()
            {
                for (int i = 0; i < passengers.Count; i++)              
                {
                    var passenger = passengers[i];
                    if (passenger.serverInput.WasJustPressed(controlButtons[CommandType.EnterExit]))
                        PassengerExit(passenger);
                }

                if (player.serverInput.WasJustPressed(controlButtons[CommandType.EnterExit]))
                {
                    ExitTank();
                    return;
                }

                if (player.serverInput.WasJustPressed(controlButtons[CommandType.Lights]))
                    ToggleLights();

                float accelerate = 0f;
                float steer = 0f;
                
                if (player.serverInput.IsDown(BUTTON.FORWARD)) 
                    accelerate += 1f;                

                if (player.serverInput.IsDown(BUTTON.BACKWARD))
                    accelerate -= 1f;

                if (player.serverInput.IsDown(BUTTON.RIGHT))
                    steer += 1f;

                if (player.serverInput.IsDown(BUTTON.LEFT))
                    steer -= 1f;

                bool boost = player.serverInput.IsDown(controlButtons[CommandType.Boost]);

                SetThrottleSpeed(accelerate, steer, boost);                
            }

            private void OnTriggerEnter(Collider col)
            {
                if (!enabled) return;

                if (ins.configData.Crushables.Players)
                {                    
                    var triggerPlayer = col.gameObject.GetComponentInParent<BasePlayer>();
                    if (triggerPlayer != null && triggerPlayer != player && !passengers.Contains(triggerPlayer) && !enteringPassengers.Contains(triggerPlayer.userID))
                    {
                        triggerPlayer.Die(new HitInfo(player, triggerPlayer, DamageType.Blunt, 200f));
                        return;
                    }                    
                }

                if (ins.configData.Crushables.Animals)
                {
                    var npc = col.gameObject.GetComponentInParent<BaseNpc>();
                    if (npc != null)
                    {
                        npc.Die(new HitInfo(player, npc, DamageType.Blunt, 200f));
                        return;
                    }
                }

                if (ins.configData.Crushables.Buildings)
                {
                    var buildingBlock = col.gameObject.GetComponentInParent<BuildingBlock>();
                    if (buildingBlock != null)
                    {
                        buildingBlock.Die(new HitInfo(player, buildingBlock, DamageType.Blunt, 1000f));
                        return;
                    }

                    var colliderBatch = col.gameObject.GetComponent<MeshColliderBatch>();
                    if (colliderBatch != null)
                    {
                        var colliders = ((MeshColliderLookup)meshLookupField.GetValue(colliderBatch)).src.data;
                        if (colliders == null) return;
                        foreach (var instance in colliders)
                        {
                            var block = instance.collider?.GetComponentInParent<BuildingBlock>();
                            if (Vector3.Distance(block.transform.position, entity.transform.position) < 4)
                            {
                                block.Die(new HitInfo(player, block, DamageType.Blunt, 1000f));
                                return;
                            }
                        }                        
                        return;
                    }

                    var simpleBlock = col.gameObject.GetComponentInParent<SimpleBuildingBlock>();
                    if (simpleBlock != null)
                    {
                        simpleBlock.Die(new HitInfo(player, simpleBlock, DamageType.Blunt, 1500));
                        return;
                    }
                }

                if (ins.configData.Crushables.Loot)
                {
                    var loot = col.gameObject.GetComponentInParent<LootContainer>();
                    if (loot != null)
                    {
                        loot.Die(new HitInfo(player, loot, DamageType.Blunt, 200f));
                        return;
                    }
                }

                if (ins.configData.Crushables.Resources)
                {
                    var resource = col.gameObject.GetComponentInParent<ResourceEntity>();
                    if (resource != null)
                    {
                        resource.Kill(BaseNetworkable.DestroyMode.None);
                        return;
                    }                    
                }
            }            

            private void SetThrottleSpeed(float acceleration, float steering, bool boost)
            {                
                if (acceleration == 0 && steering == 0)
                {
                    ApplyBrakes(0.5f);

                    if (accelTimeTaken > 0)
                        accelTimeTaken = Mathf.Clamp(accelTimeTaken -= (Time.deltaTime * 2), 0, accelTimeToTake);                    
                }
                else
                {
                    ApplyBrakes(0f);

                    accelTimeTaken += Time.deltaTime;
                    float engineRpm = Mathf.InverseLerp(0f, accelTimeToTake, accelTimeTaken);
                    
                    float throttle = Mathf.InverseLerp(0f, 1f, engineRpm);
                    
                    float leftTrack = 0;
                    float rightTrack = 0;
                    float torque = 0;

                    if (acceleration > 0)
                    {
                        torque = forwardTorque;
                        leftTrack = 1f;
                        rightTrack = 1f;
                    }
                    else if (acceleration < 0)
                    {
                        torque = forwardTorque;
                        leftTrack = -1f;
                        rightTrack = -1f;
                    }
                    if (steering > 0)
                    {
                        if (acceleration == 0)
                        {
                            torque = turnTorque;
                            leftTrack = 1f;
                            rightTrack = -1f;
                        }
                        else
                        {                            
                            torque = (forwardTorque + turnTorque) * 0.75f;
                            rightTrack *= 0.5f;
                        }
                    }
                    else if (steering < 0)
                    {
                        if (acceleration == 0)
                        {
                            torque = turnTorque;
                            leftTrack = -1f;
                            rightTrack = 1f;
                        }
                        else
                        {                            
                            torque = (forwardTorque + turnTorque) * 0.75f;
                            leftTrack *= 0.5f;
                        }
                    }

                    if (boost)
                    {
                        if (torque > 0)
                            torque += ins.configData.Movement.BoostTorque;
                        if (torque < 0)
                            torque -= ins.configData.Movement.BoostTorque;
                    }
                   
                    ApplyMotorTorque(Mathf.Clamp(leftTrack * throttle, -1f, 1f) * torque, false);
                    ApplyMotorTorque(Mathf.Clamp(rightTrack * throttle, -1f, 1f) * torque, true);                   
                }
            }
            private void ApplyBrakes(float amount)
            {
                amount = Mathf.Clamp(maxBrakeTorque * amount, 0, maxBrakeTorque);
                ApplyBrakeTorque(amount, true);
                ApplyBrakeTorque(amount, false);
            }

            private void ApplyBrakeTorque(float amount, bool rightSide)
            {
                WheelCollider[] wheelColliderArray = (!rightSide ? leftWheels : rightWheels);

                for (int i = 0; i < wheelColliderArray.Length; i++)
                    wheelColliderArray[i].brakeTorque = maxBrakeTorque * amount;
            }
            private void ApplyMotorTorque(float torque, bool rightSide)
            {                
                WheelCollider[] wheelColliderArray = (!rightSide ? leftWheels : rightWheels);

                // Slightly increase torque to the left side tracks in attempt to fix the pull to the left
                float equalizer = !rightSide ? 1.2f : 1f;
                for (int i = 0; i < wheelColliderArray.Length; i++)
                    wheelColliderArray[i].motorTorque = torque * equalizer;
            }

            public void EnterTank(BasePlayer player)
            {
                this.player = player;
                ins.StartSpectating(player, this, true);                                               
            }
            public void ExitTank()
            {
                ApplyBrakes(1f);
                enabled = false;

                for (int i = 0; i < passengers.Count; i++)               
                    PassengerExit(passengers[i]);                

                ins.EndSpectating(player, this, true);
                player = null;
            }
            public void PassengerEnter(BasePlayer passenger)
            {
                ins.passengers.Add(passenger.userID);
                enteringPassengers.Add(passenger.userID);
                ins.StartSpectating(passenger, this, false);
                ins.SendReply(player, ins.msg("passenger_enter", player.UserIDString));
            }
            private void PassengerExit(BasePlayer passenger)
            {
                ins.passengers.Remove(passenger.userID);
                passengers.Remove(passenger);
                ins.EndSpectating(passenger, this, false);                
            }
            public void SetPassengerActive(BasePlayer passenger)
            {
                passengers.Add(passenger);
                if (enteringPassengers.Contains(passenger.userID))
                    enteringPassengers.Remove(passenger.userID);
            }

            public bool HasCommander() => player != null;

            public BasePlayer GetCommander() => player;

            public bool IsAtMaxCapacity() => passengers.Count >= ins.configData.Passengers.Max;

            private void ToggleLights() => entity.SetFlag(BaseEntity.Flags.Reserved5, !entity.HasFlag(BaseEntity.Flags.Reserved5), false);

            public void ManageDamage(HitInfo info)
            {
                // Temporarily nullify damage until some form of death sequence has been added to the game
                info.damageTypes = new DamageTypeList();
                info.HitEntity = null;
                info.HitMaterial = 0;
                info.PointStart = Vector3.zero;
            }          
        }
        #endregion

        #region Commands
        [ChatCommand("spawntank")]
        void cmdTank(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "tankcommander.admin")) return;

            Vector3 position = player.transform.position + (player.transform.forward * 3) + Vector3.up;

            RaycastHit hit;
            if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 20f))            
                position = hit.point;
            
            BaseEntity entity = GameManager.server.CreateEntity(tankPrefab, position);
            entity.Spawn();

            Controller commander = entity.gameObject.AddComponent<Controller>();
        }
       
        [ConsoleCommand("spawntank")]
        void ccmdSpawnTank(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null || arg.Args == null)
                return;

            if (arg.Args.Length == 1)
            {
                BasePlayer player = covalence.Players.Connected.FirstOrDefault(x => x.Id == arg.GetString(0))?.Object as BasePlayer;
                if (player != null)
                {
                    Vector3 position = player.transform.position + (player.transform.forward * 3) + Vector3.up;

                    RaycastHit hit;
                    if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 20f))
                        position = hit.point;

                    BaseEntity entity = GameManager.server.CreateEntity(tankPrefab, position);
                    entity.Spawn();

                    Controller commander = entity.gameObject.AddComponent<Controller>();
                }
                return;
            }
            if (arg.Args.Length == 3)
            {
                float x;
                float y;
                float z;

                if (float.TryParse(arg.GetString(0), out x))
                {
                    if (float.TryParse(arg.GetString(1), out y))
                    {
                        if (float.TryParse(arg.GetString(2), out z))
                        {
                            BaseEntity entity = GameManager.server.CreateEntity(tankPrefab, new Vector3(x, y, z));
                            entity.Spawn();
                            Controller commander = entity.gameObject.AddComponent<Controller>();
                            return;
                        }
                    }
                }
                PrintError($"Invalid arguments supplied to spawn a tank at position : (x = {arg.GetString(0)}, y = {arg.GetString(1)}, z = {arg.GetString(2)})");
            }
        }
        #endregion

        #region Friends
        private bool AreFriends(ulong playerId, ulong friendId)
        {
            if (Friends && configData.Passengers.UseFriends)
                return (bool)Friends?.Call("AreFriendsS", playerId.ToString(), friendId.ToString());
            return true;
        }
        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (Clans && configData.Passengers.UseClans)
            {
                object playerTag = Clans?.Call("GetClanOf", playerId);
                object friendTag = Clans?.Call("GetClanOf", friendId);
                if (playerTag is string && friendTag is string)
                {
                    if (!string.IsNullOrEmpty((string)playerTag) && !string.IsNullOrEmpty((string)friendTag) && (playerTag == friendTag))
                        return true;
                }
                return false;
            }
            return true;
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Movement Settings")]
            public MovementSettings Movement { get; set; }
            [JsonProperty(PropertyName = "Button Configuration")]
            public ButtonConfiguration Buttons { get; set; }
            [JsonProperty(PropertyName = "Crushable Types")]
            public CrushableTypes Crushables { get; set; }
            [JsonProperty(PropertyName = "Passenger Options")]
            public PassengerOptions Passengers { get; set; }
            [JsonProperty(PropertyName = "Inventory Options")]
            public InventoryOptions Inventory { get; set; }

            public class CrushableTypes
            {
                [JsonProperty(PropertyName = "Buildings")]
                public bool Buildings { get; set; }
                [JsonProperty(PropertyName = "Resources")]
                public bool Resources { get; set; }
                [JsonProperty(PropertyName = "Loot")]
                public bool Loot { get; set; }
                [JsonProperty(PropertyName = "Animals")]
                public bool Animals { get; set; }
                [JsonProperty(PropertyName = "Players")]
                public bool Players { get; set; }
            }
            public class ButtonConfiguration
            {                
                [JsonProperty(PropertyName = "Enter/Exit vehicle")]
                public string Enter { get; set; }
                [JsonProperty(PropertyName = "Toggle light")]
                public string Lights { get; set; }
                [JsonProperty(PropertyName = "Open inventory")]
                public string Inventory { get; set; }
                [JsonProperty(PropertyName = "Speed boost")]
                public string Boost { get; set; }
            }
            public class MovementSettings
            {
                [JsonProperty(PropertyName = "Forward torque (nm)")]
                public float ForwardTorque { get; set; }
                [JsonProperty(PropertyName = "Rotation torque (nm)")]
                public float TurnTorque { get; set; }
                [JsonProperty(PropertyName = "Brake torque (nm)")]
                public float BrakeTorque { get; set; }
                [JsonProperty(PropertyName = "Time to reach maximum acceleration (seconds)")]
                public float Acceleration { get; set; }
                [JsonProperty(PropertyName = "Boost torque (nm)")]
                public float BoostTorque { get; set; }
            }
            public class PassengerOptions
            {
                [JsonProperty(PropertyName = "Allow passengers")]
                public bool Enabled { get; set; }
                [JsonProperty(PropertyName = "Maximum passengers per tank")]
                public int Max { get; set; }
                [JsonProperty(PropertyName = "Require passenger to be a friend (FriendsAPI)")]
                public bool UseFriends { get; set; }
                [JsonProperty(PropertyName = "Require passenger to be a clan mate (Clans)")]
                public bool UseClans { get; set; }
            }
            public class InventoryOptions
            {
                [JsonProperty(PropertyName = "Enable inventory system")]
                public bool Enabled { get; set; }
                [JsonProperty(PropertyName = "Inventory size (max 36)")]
                public int Size { get; set; }
            }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Buttons = new ConfigData.ButtonConfiguration
                {
                    Enter = "USE",
                    Lights = "RELOAD",
                    Inventory = "RELOAD",
                    Boost = "SPRINT"
                },
                Crushables = new ConfigData.CrushableTypes
                {
                    Animals = true,
                    Buildings = true,
                    Loot = true,
                    Players = true,
                    Resources = true
                },
                Movement = new ConfigData.MovementSettings
                {
                    Acceleration = 3f,
                    BrakeTorque = 50f,
                    ForwardTorque = 2000f,
                    TurnTorque = 1000f,
                    BoostTorque = 600f
                },
                Passengers = new ConfigData.PassengerOptions
                {
                    Enabled = true,
                    Max = 4,
                    UseClans = true,
                    UseFriends = true
                },
                Inventory = new ConfigData.InventoryOptions
                {
                    Enabled = true,
                    Size = 36
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Localization
        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["leave_help"] = "<color=#D3D3D3>You can exit the tank by pressing </color><color=#ce422b>{0}</color>",
            ["is_flying"] = "<color=#D3D3D3>You can not enter the tank when you are flying</color>",
            ["in_use"] = "<color=#D3D3D3>This tank is already in use</color>",
            ["not_friend"] = "<color=#D3D3D3>You must be a friend or clanmate with the operator</color>",
            ["passenger_enter"] = "<color=#D3D3D3>You have entered the tank as a passenger</color>",
            ["boost_help"] = "<color=#D3D3D3>Hold </color><color=#ce422b>{0}</color><color=#D3D3D3> to use boost</color>",
            ["inv_help"] = "<color=#D3D3D3>You can access this vehicles inventory from the outside by pressing the </color><color=#ce422b>{0}</color><color=#D3D3D3> key when there is no operator.</color>"
        };
        #endregion
    }
}
