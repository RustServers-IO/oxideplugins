using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using Rust;

namespace Oxide.Plugins
{
    [Info("TankCommander", "k1lly0u", "0.1.0", ResourceId = 0)]
    class TankCommander : RustPlugin
    {
        #region Fields
        static TankCommander ins;

        private FieldInfo spectateFilter = typeof(BasePlayer).GetField("spectateFilter", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));        
        private Dictionary<ulong, Commander> commanders = new Dictionary<ulong, Commander>();
        private bool initialized;

        const string tankPrefab = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            permission.RegisterPermission("tankcommander.use", this);
        }
        void OnServerInitialized()
        {
            ins = this;
            //LoadVariables();
            initialized = true;
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BradleyAPC)
            {
                Commander commander = entity.GetComponent<Commander>();
                if (commander != null)
                    commander.ManageDamage(info);                
            }
        }
        void Unload()
        {
            foreach (var commander in commanders)
            {
                EndSpectating(BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == commander.Key));
                UnityEngine.Object.Destroy(commander.Value);
            }
            commanders.Clear();

            var objects = UnityEngine.Object.FindObjectsOfType<Commander>();
            if (objects != null)
            {
                foreach (var obj in objects)
                    UnityEngine.Object.Destroy(obj);
            }
        }        
        #endregion

        #region Functions

        #endregion

        #region Component
        class Commander : MonoBehaviour
        {
            private BasePlayer player;

            private BradleyAPC entity;
            private Rigidbody rigidBody;

            private WheelCollider[] leftWheels;
            private WheelCollider[] rightWheels;                        
            
            private float accelTimeTaken;
            private float accelTimeToTake = 3f;

            private float forwardTorque = 2000f;
            private float maxBrakeTorque = 50f;
            private float turnTorque = 1000f;

            private void Awake()
            {
                entity = GetComponent<BradleyAPC>();
                entity.enabled = false;
                enabled = false;

                rigidBody = entity.myRigidBody;
                leftWheels = entity.leftWheels;
                rightWheels = entity.rightWheels;
            }

            private void OnDestroy()
            {
                ins.EndSpectating(player);
                player.MovePosition(entity.transform.position + Vector3.up);
                entity.Kill();
            }

            private void FixedUpdate()
            {               
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
            
            private void ToggleLights() => entity.SetFlag(BaseEntity.Flags.Reserved5, TOD_Sky.Instance.IsNight, false);  

            public void SetThrottleSpeed(float acceleration, float steering)
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

            public void SetCommander(BasePlayer player)
            {
                this.player = player;                
                enabled = true;                
            }
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
        [ChatCommand("tank")]
        void cmdTank(BasePlayer player, string command, string[] args)
        {
            if (!commanders.ContainsKey(player.userID))
            {
                if (!permission.UserHasPermission(player.UserIDString, "tankcommander.use")) return;

                BaseEntity entity = GameManager.server.CreateEntity(tankPrefab, player.transform.position);
                entity.Spawn();
                
                Commander commander = entity.gameObject.AddComponent<Commander>();
                commander.SetCommander(player);
                commanders.Add(player.userID, commander);
                StartSpectating(player, entity);
            }
            else
            {
                EndSpectating(player);
                UnityEngine.Object.Destroy(commanders[player.userID]);
                commanders.Remove(player.userID);
            }
        }
       
        private void StartSpectating(BasePlayer player, BaseEntity entity)
        {         
            spectateFilter.SetValue(player, $"@123nofilter123");
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
            player.gameObject.SetLayerRecursive(10);
            player.CancelInvoke("InventoryUpdate");
            player.SendNetworkUpdateImmediate();

            NextTick(() =>
            {
                player.SetParent(entity, 0);
                player.Command("client.camoffset", new object[] { new Vector3(0, 3.5f, 0) });
            });  
        }
        private void EndSpectating(BasePlayer player)
        {            
            spectateFilter.SetValue(player, string.Empty);
            player.SetParent(null);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
            player.gameObject.SetLayerRecursive(17);
            player.InvokeRepeating("InventoryUpdate", 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));
            player.Command("client.camoffset", new object[] { new Vector3(0, 1.2f, 0) });            
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {

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

            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion       
    }
}
