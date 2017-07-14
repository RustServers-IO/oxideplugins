using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using Rust;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("TankCommander", "k1lly0u", "0.1.1", ResourceId = 2560)]
    class TankCommander : RustPlugin
    {
        #region Fields
        static TankCommander ins;

        private FieldInfo spectateFilter = typeof(BasePlayer).GetField("spectateFilter", (BindingFlags.Instance | BindingFlags.NonPublic));
        private static FieldInfo meshLookupField = typeof(MeshColliderBatch).GetField("meshLookup", BindingFlags.Instance | BindingFlags.NonPublic);

        private List<Controller> controllers = new List<Controller>();
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
            if (!initialized || player == null || commanders.ContainsKey(player.userID) || !HasPermission(player, "tankcommander.use")) return;

            if (input.WasJustPressed(controlButtons[CommandType.EnterExit]))
            {
                RaycastHit hit;
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit, 3f))
                {                    
                    Controller controller = hit.GetEntity()?.GetComponent<Controller>();
                    if (controller != null)
                    {
                        commanders.Add(player.userID, controller);
                        controller.EnterTank(player);
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
                [CommandType.Lights] = ParseType<BUTTON>(configData.Buttons.Lights)
            };            
        }
        #endregion

        #region Component
        enum CommandType { EnterExit, Lights }

        class Controller : MonoBehaviour
        {
            private BasePlayer player;

            public BradleyAPC entity;
            private Rigidbody rigidBody;

            private WheelCollider[] leftWheels;
            private WheelCollider[] rightWheels;                        
            
            private float accelTimeTaken;
            private float accelTimeToTake = 3f;

            private float forwardTorque = 2000f;
            private float maxBrakeTorque = 50f;
            private float turnTorque = 1000f;

            private Dictionary<CommandType, BUTTON> controlButtons;

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
            }
            
            private void OnDestroy()
            {
                if (player != null)
                    ExitTank();
                entity.Kill();
            }

            private void FixedUpdate()
            {          
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

                SetThrottleSpeed(accelerate, steer);                
            }

            private void OnTriggerEnter(Collider col)
            {
                if (!enabled) return;

                if (ins.configData.Crushables.Players)
                {                    
                    var player = col.gameObject.GetComponentInParent<BasePlayer>();
                    if (player != null && player != this.player)
                    {                        
                        player.Die(new HitInfo(this.player, player, DamageType.Blunt, 200f));
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

            private void SetThrottleSpeed(float acceleration, float steering)
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

                for (int i = 0; i < wheelColliderArray.Length; i++)
                    wheelColliderArray[i].motorTorque = torque;
            }

            public void EnterTank(BasePlayer player)
            {
                print("enter tank");
                this.player = player;
                ins.StartSpectating(player, this);
                Invoke("EnableComponent", 3f);                               
            }
            public void ExitTank()
            {
                ApplyBrakes(1f);
                enabled = false;
                ins.EndSpectating(player, this);
                player = null;
            }
            private void EnableComponent() => enabled = true;

            private void ToggleLights(bool toggle = false) => entity.SetFlag(BaseEntity.Flags.Reserved5, toggle, false);

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

        #region Command
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
       
        private void StartSpectating(BasePlayer player, Controller controller)
        {
            spectateFilter.SetValue(player, $"@123nofilter123");
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
            player.gameObject.SetLayerRecursive(10);
            player.CancelInvoke("InventoryUpdate");
            player.SendNetworkUpdateImmediate();

            NextTick(() =>
            {
                player.transform.position = controller.transform.position;
                player.SetParent(controller.entity, 0);
                player.Command("client.camoffset", new object[] { new Vector3(0, 3.5f, 0) });
                SendReply(player, string.Format(msg("leave_help", player.UserIDString), configData.Buttons.Enter));
            });  
        }
        private void EndSpectating(BasePlayer player, Controller commander)
        {
            spectateFilter.SetValue(player, string.Empty);
            player.SetParent(null);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
            player.gameObject.SetLayerRecursive(17);
            player.InvokeRepeating("InventoryUpdate", 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));
            player.Command("client.camoffset", new object[] { new Vector3(0, 1.2f, 0) });
            player.transform.position = commander.transform.position + Vector3.up + (commander.transform.right * 3);
            commanders.Remove(player.userID);
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
                    Lights = "RELOAD"
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
                    TurnTorque = 1000f
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
            ["leave_help"] = "You can exit the tank by pressing the \"{0}\" key"
        };
        #endregion
    }
}
