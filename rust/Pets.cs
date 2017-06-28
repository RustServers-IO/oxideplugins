using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Pets", "Nogrod/k1lly0u", "0.6.1", ResourceId = 851)]
    class Pets : RustPlugin
    {
        #region Fields        
        static Pets ins;
        static BUTTON Main;
        static BUTTON Secondary;
        static Dictionary<ulong, PetData> npcSaveList = new Dictionary<ulong, PetData>();

        enum Act { Move, Attack, Eat, Drink, Follow, Sleep, Idle }
        #endregion

        #region Oxide Hooks       
        void OnServerInitialized()
        {
            ins = this;
            lang.RegisterMessages(Messages, this);
            RegisterPermissions();
            LoadVariables();
            LoadData();
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity is BaseNpc)
                entity.GetComponent<NpcAI>()?.OnAttacked(hitInfo);
        }
        void OnPlayerInit(BasePlayer player)
        {
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(2, () => OnPlayerInit(player));
                return;
            }
            PetData info;
            if (!npcSaveList.TryGetValue(player.userID, out info) || !info.NeedToSpawn)
                return;

            Puts("Loading pet...");
            var pet = GameManager.server.CreateEntity(StringPool.Get(info.prefabID), new Vector3(info.x, info.y, info.z));
            if (pet == null) return;

            var controller = player.gameObject.AddComponent<NPCController>();
            pet.Spawn();
            controller.npcAi = pet.gameObject.AddComponent<NpcAI>();
            controller.npcAi.owner = controller;
            controller.npcAi.inventory.Load(ProtoBuf.ItemContainer.Deserialize(info.inventory));
            info.NeedToSpawn = false;
        }
        object CanNpcAttack(BaseNpc entity, BaseEntity target)
        {            
            var npcAi = entity.GetComponent<NpcAI>();           
            if (npcAi != null && target is BasePlayer && npcAi.owner.player == target as BasePlayer)            
                return false;            
            return null;
        }
        void OnServerSave()
        {
            var pets = UnityEngine.Object.FindObjectsOfType<NpcAI>();
            if (pets == null) return;
            foreach (var pet in pets)
                npcSaveList[pet.owner.player.userID] = new PetData(pet);
            SaveData();           
        }
        void Unload()
        {
            OnServerSave();
            DestroyAll<NPCController>();
            DestroyAll<NpcAI>();
        }
        #endregion

        #region Functions
        private void RegisterPermissions()
        {
            string[] names = new string[] { "bear", "boar", "chicken", "horse", "stag", "wolf" };
            foreach (var name in names)
                permission.RegisterPermission($"pets.{name}", this);
        }
        private BUTTON ConvertToButton(string button)
        {
            try
            {
                return (BUTTON)Enum.Parse(typeof(BUTTON), button);
            }
            catch (Exception)
            {
                return BUTTON.USE;
            }
        }
        private void DestroyAll<T>()
        {
            var objects = UnityEngine.Object.FindObjectsOfType(typeof(T));
            if (objects == null) return;
            foreach (var gameObj in objects)
                UnityEngine.Object.Destroy(gameObj);
        }
        #endregion

        #region Components
        class NPCController : MonoBehaviour
        {
            public BasePlayer player;
            public NpcAI npcAi;

            private float nextPressTime = 0;
            private float nextDrawTime = 0;
            private float nextControlTime = 0;

            private float lootDistance;
            private float tameTimer;
                        
            private bool usePermissions;
            internal bool drawEnabled;

            private void Awake()
            {
                enabled = false;
                player = GetComponent<BasePlayer>();
                drawEnabled = ins.configData.Options.UseDrawSystem;
                tameTimer = ins.configData.Options.TameTimer;
                lootDistance = ins.configData.Options.LootDistance;
                usePermissions = ins.configData.Options.UsePermissions;
            }
            private void FixedUpdate()
            {
                var time = UnityEngine.Time.realtimeSinceStartup;

                if (player.serverInput.WasJustPressed(Main))
                {
                    if (nextPressTime < time)
                    {
                        nextPressTime = time + 0.2f;
                        UpdateAction();
                    }
                }
                else if (player.serverInput.WasJustPressed(Secondary))
                {
                    if (npcAi != null && nextPressTime < time)
                    nextPressTime = time + 0.2f;
                    ChangeFollowAction();
                }

                if (drawEnabled && npcAi != null && npcAi.action < Act.Follow && nextDrawTime < time)
                {
                    nextDrawTime = time + 0.05f;
                    UpdateDraw();
                }                
            }

            private void UpdateDraw()
            {
                var drawpos = (npcAi.action == Act.Move || npcAi.action == Act.Drink ? npcAi.targetPos : (npcAi.targetEnt == null ? Vector3.zero : npcAi.targetEnt.transform.position));
                if (drawpos != Vector3.zero)
                {
                    bool tempAdmin = false;
                    if (!player.IsAdmin)
                    {
                        tempAdmin = true;
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    }
                    player.SendConsoleCommand("ddraw.arrow", 0.05f + 0.02f, npcAi.action == Act.Move ? Color.cyan : npcAi.action == Act.Attack ? Color.red : Color.yellow, drawpos + new Vector3(0, 5f, 0), drawpos, 1.5f);
                    if (tempAdmin)
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                }
            }

            private void UpdateAction()
            {               
                RaycastHit hit;
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit) || hit.transform == transform)
                {
                    if (npcAi == null)
                    {
                        BaseNpc npcPet = hit.GetEntity()?.GetComponent<BaseNpc>();
                        if (npcPet == null)
                            return;

                        if (hit.distance >= 10)
                        {
                            UserMessage(player, "tooFar");
                            return;
                        }

                        TryGetNewPet(npcPet);
                        return;
                    }

                    if (hit.collider?.gameObject.layer == 4)
                    {
                        if (npcAi.entity.Hydration.Level < 1)
                        {
                            UserMessage(player, "isDrinking");
                            npcAi.targetPos = hit.point;
                            npcAi.action = Act.Drink;                           
                        }
                        else UserMessage(player, "notThirsty");
                        return;
                    }

                    BaseCombatEntity targetEnt = hit.GetEntity()?.GetComponent<BaseCombatEntity>();                    
                    if (targetEnt == null)
                    {
                        npcAi.targetPos = hit.point;
                        npcAi.action = Act.Move;
                        if (npcAi.entity.IsSleeping)
                            npcAi.SetState(BaseNpc.Behaviour.Attack);
                        return;
                    }

                    if (targetEnt == npcAi.entity)
                    {
                        if (hit.distance <= lootDistance)
                            OpenPetInventory();
                    }
                    else if (targetEnt is BaseCorpse)
                    {
                        UserMessage(player, "isEating");
                        npcAi.entity.FoodTarget = targetEnt;
                        npcAi.Attack(targetEnt, Act.Eat);
                    }
                    else
                    {
                        UserMessage(player, "isAttacking");
                        npcAi.Attack(targetEnt);
                    }
                }
            }

            void OpenPetInventory()
            {                
                player.inventory.loot.Clear();
                player.inventory.loot.entitySource = npcAi.entity;
                player.inventory.loot.itemSource = null;
                player.inventory.loot.AddContainer(npcAi.inventory);
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "smallwoodbox", null, null, null, null);
                player.SendNetworkUpdate();

                UserMessage(player, "openInv");
            }
            void ChangeFollowAction()
            {
                if (npcAi == null) return;
                if (!npcAi.stopFollow)
                {
                    UserMessage(player, "stopFollow");
                    npcAi.action = Act.Idle;
                    npcAi.stopFollow = true;
                }
                else
                {
                    UserMessage(player, "startFollow");
                    npcAi.Attack(player, Act.Follow);
                    npcAi.stopFollow = false;
                }
            }

            void TryGetNewPet(BaseNpc npcPet)
            {
                var ownedAi = npcPet.GetComponent<NpcAI>();
                if (ownedAi != null && ownedAi.owner != this)
                {
                    UserMessage(player, "isPet");
                    return;
                }

                if (nextControlTime >= UnityEngine.Time.realtimeSinceStartup)
                {
                    UserMessage(player, "tooFast");
                    return;
                }

                if (usePermissions && !ins.permission.UserHasPermission(player.UserIDString, $"pets.{npcPet.ShortPrefabName}"))
                {
                    UserMessage(player, "noPerms");
                    return;
                }

                nextControlTime = UnityEngine.Time.realtimeSinceStartup + tameTimer;

                npcAi = npcPet.gameObject.AddComponent<NpcAI>();
                npcAi.owner = this;

                UserMessage(player, "petSet");
            }
        }

        class NpcAI : MonoBehaviour
        {
            internal Act action;
            internal Vector3 targetPos = Vector3.zero;
            internal BaseCombatEntity targetEnt;
            internal bool stopFollow = false;

            public NPCController owner;
            public ItemContainer inventory;
            public BaseNpc entity;

            private double attackRange;
            private float targetIgnoreDistance;
            private float healthModifier;
            private float speedModifier;
            private float attackModifier;

            private float nextMessageTime;

            private void Awake()
            {
                entity = GetComponent<BaseNpc>();
                action = Act.Idle;

                targetIgnoreDistance = ins.configData.Options.AttackDistance;
                healthModifier = ins.configData.NPCMods.Health;
                speedModifier = ins.configData.NPCMods.Speed;
                attackModifier = ins.configData.NPCMods.Attack;
                nextMessageTime = UnityEngine.Time.realtimeSinceStartup;

                inventory = new ItemContainer();
                inventory.ServerInitialize(null, 6);
                if ((int)inventory.uid == 0)
                    inventory.GiveUID();

                entity.enableSaving = false;
                BaseEntity.saveList.Remove(entity);
                entity.InitializeHealth(entity.Health() * healthModifier, entity.MaxHealth() * healthModifier);

                entity.AttackDamage *= attackModifier;
                entity.Stats.TurnSpeed *= speedModifier;
                entity.Stats.Speed *= speedModifier;
            }
            private void OnDestroy()
            {
                DropUtil.DropItems(inventory, transform.position);
                owner.npcAi = null;

                if (entity.health <= 0)
                    return;
                
                entity.InitializeHealth(entity.Health() / healthModifier, entity.MaxHealth() / healthModifier);

                entity.AttackDamage /= attackModifier;
                entity.Stats.TurnSpeed /= speedModifier;
                entity.Stats.Speed /= speedModifier;
            }

            internal void OnAttacked(HitInfo info)
            {
                if (info.Initiator && info.Initiator != owner.player && action != Act.Attack)
                    Attack(info.Initiator.GetComponent<BaseCombatEntity>(), Act.Attack);
            }

            private void FixedUpdate()
            {
                if (entity.Sleep < 0.01f)
                {
                    SetState(BaseNpc.Behaviour.Sleep);
                    action = Act.Sleep;
                    var time = UnityEngine.Time.realtimeSinceStartup;
                    if (owner.player.enabled && nextMessageTime < time)
                    {
                        UserMessage(owner.player, "isTired");
                        nextMessageTime = time + 60f;
                    }
                    return;
                }
                if (entity.Energy.Level < 0.01f)
                {
                    var time = UnityEngine.Time.realtimeSinceStartup;
                    if (owner.player.enabled && nextMessageTime < time)
                    {
                        UserMessage(owner.player, "isHungry");
                        nextMessageTime = time + 60f;
                    }
                }
                if (action == Act.Idle || action == Act.Follow)
                {                    
                    if (entity.IsSleeping)
                        SetState(BaseNpc.Behaviour.Idle);   

                    if (!stopFollow && Vector3.Distance(entity.transform.position, owner.transform.position) > 10)
                    {
                        entity.UpdateDestination(owner.transform.position + (Vector3.back * 2));
                        return;
                    }
                    if (action == Act.Idle)
                    {
                        SetState(BaseNpc.Behaviour.Idle);
                        entity.StopMoving();
                    }
                    return;
                }
                if (action == Act.Move)
                {
                    if (Vector3.Distance(transform.position, targetPos) < 1)
                        action = Act.Idle;
                    else
                    {
                        entity.UpdateDestination(targetPos);
                        SetState(BaseNpc.Behaviour.Attack);
                    }
                }
                else if (action == Act.Drink)
                {
                    if (entity.Hydration.Level >= 1)
                    {
                        action = Act.Idle;
                        SetState(BaseNpc.Behaviour.Idle);
                        return;
                    }
                    if (Vector3.Distance(transform.position, targetPos) < 1)
                    {
                        SetState(BaseNpc.Behaviour.Eat);
                        entity.Hydration.Level += 0.005f;
                        entity.Hydration.Level = Mathf.Clamp01(entity.Hydration.Level);
                    }
                    else
                    {
                        entity.UpdateDestination(targetPos);
                        SetState(BaseNpc.Behaviour.Attack);
                    }
                }
                else if (action == Act.Sleep)
                {
                    SetState(BaseNpc.Behaviour.Sleep);
                    entity.health += 0.003f;
                    entity.health = Mathf.Clamp(entity.health, 0, entity.MaxHealth());

                    entity.Sleep += 0.003f;
                    entity.Sleep = Mathf.Clamp01(entity.Sleep);
                    entity.StopMoving();
                }
                else
                {
                    if (targetEnt == null)
                    {
                        if (!stopFollow)
                            targetEnt = owner.player;

                        action = Act.Idle;
                        SetState(BaseNpc.Behaviour.Idle);
                    }
                    else
                    {
                        var distance = Vector3.Distance(transform.position, targetEnt.transform.position);
                        if (distance > targetIgnoreDistance)
                        {
                            action = Act.Idle;
                            SetState(BaseNpc.Behaviour.Idle);
                            return;
                        }                       
                        if ((action == Act.Eat || entity.Energy.Level < 0.25f) && targetEnt != owner.player)
                        {
                            if (distance <= attackRange)
                            {
                                entity.FoodTarget = targetEnt;
                                if (distance <= 2)
                                {
                                    SetState(BaseNpc.Behaviour.Eat);
                                    entity.Eat();
                                }
                            }
                            else entity.UpdateDestination(targetEnt.transform.position);                          
                        }
                        if (action == Act.Attack && targetEnt != owner.player && distance < targetIgnoreDistance)
                        {
                            if (entity.AttackTarget != targetEnt)
                                entity.AttackTarget = targetEnt;                          

                            SetState(BaseNpc.Behaviour.Attack);
                            entity.UpdateDestination(targetEnt.transform.position);
                        }                       
                    }
                }
            }

            internal void SetState(BaseNpc.Behaviour state)
            {       
                if (state != BaseNpc.Behaviour.Sleep && entity.IsSleeping)                
                    entity.SetAiFlag(BaseNpc.AiFlags.Sleeping, false);
                
                if (entity.CurrentBehaviour != state)
                    entity.CurrentBehaviour = state;
            }           

            internal void Attack(BaseCombatEntity targetEnt, Act action = Act.Attack)
            {
                this.targetEnt = targetEnt;
                this.action = action;
                entity.AttackTarget = targetEnt;                
                attackRange = Math.Pow(targetEnt._collider.bounds.XZ3D().extents.Max() + entity.AttackRange + targetEnt._collider.bounds.XZ3D().extents.Max(), 2);
            }
        }
        #endregion

        #region Commands
        [ChatCommand("pet")]
        void pet(BasePlayer player, string command, string[] args)
        {
            var component = player.GetComponent<NPCController>() ?? player.gameObject.AddComponent<NPCController>();
            if (args.Length == 0)
            {
                component.enabled = !component.enabled;
                UserMessage(player, component.enabled ? "isEnabled" : "isDisabled");                
                return;
            }

            if (args[0].ToLower() == "help")
            {
                UserMessage(player, "help0", new string[] {Title, Version.ToString()});
                UserMessage(player, "help1");
                UserMessage(player, "help2");
                UserMessage(player, "help3");
                UserMessage(player, "help4");
                UserMessage(player, "help5");
                UserMessage(player, "help6");
                return;
            }
            if (args[0] == "draw")
            {
                if (configData.Options.UseDrawSystem)
                {
                    component.drawEnabled = !component.drawEnabled;
                    UserMessage(player, component.drawEnabled ? "drawEnabled" : "drawDisabled");                    
                }
                else UserMessage(player, "noDdraw");
            }

            if (component.npcAi != null)
            {
                switch (args[0].ToLower())
                {
                    case "free":
                        UnityEngine.Object.Destroy(component.npcAi);
                        UserMessage(player, "hasReleased");
                        return;
                    case "sleep":
                        if (component.npcAi.action == Act.Sleep)
                        {
                            component.npcAi.action = Act.Idle;
                            UserMessage(player, "awoken");
                        }
                        else
                        {
                            component.npcAi.action = Act.Sleep;
                            UserMessage(player, "sleeping");
                        }
                        return;
                    case "info":
                        UserMessage(player, "statistics", new string[] {
                            Math.Round(component.npcAi.entity.health, 2).ToString(),
                            Math.Round(component.npcAi.entity.Energy.Level * 100, 2).ToString(),
                            Math.Round(component.npcAi.entity.Hydration.Level * 100, 2).ToString(),
                            Math.Round(component.npcAi.entity.Stamina.Level * 100, 2).ToString(),
                            Math.Round(component.npcAi.entity.Sleep * 100, 2).ToString()
                        });
                        return;
                    default:
                        break;
                }
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class Modifiers
        {
            [JsonProperty(PropertyName = "Attack damage")]
            public float Attack { get; set; }
            public float Health { get; set; }
            public float Speed { get; set; }
        }
        class Controls
        {
            [JsonProperty(PropertyName = "Npc command control button")]
            public string Main { get; set; }
            [JsonProperty(PropertyName = "Follow player toggle button")]
            public string Secondary { get; set; }
        }
        class Options
        {
            [JsonProperty(PropertyName = "Maximum distance to tame an pet")]
            public float TameDistance { get; set; }
            [JsonProperty(PropertyName = "Time between taming pets")]
            public float TameTimer { get; set; }
            [JsonProperty(PropertyName = "Maximum distance to open your pets inventory")]
            public float LootDistance { get; set; }
            [JsonProperty(PropertyName = "Maximum distance before your pet will ignore a target")]
            public float AttackDistance { get; set; }
            [JsonProperty(PropertyName = "Use the permission system")]
            public bool UsePermissions { get; set; }
            [JsonProperty(PropertyName = "Use the Ddraw system")]
            public bool UseDrawSystem { get; set; }
        }
        class ConfigData
        {
            [JsonProperty(PropertyName = "Pet statistic modifiers")]
            public Modifiers NPCMods { get; set; }
            [JsonProperty(PropertyName = "Control buttons")]
            public Controls UserControl { get; set; }
            public Options Options { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
            Main = ConvertToButton(configData.UserControl.Main);
            Secondary = ConvertToButton(configData.UserControl.Secondary);
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {               
                NPCMods = new Modifiers
                {
                    Attack = 2f,
                    Health = 1.5f,
                    Speed = 1f
                },
                Options = new Options
                {
                    AttackDistance = 70,
                    LootDistance = 1f,
                    TameDistance = 10f,
                    TameTimer = 60f,
                    UsePermissions = false,
                    UseDrawSystem = true
                },
                UserControl = new Controls
                {
                    Main = "USE",
                    Secondary = "RELOAD"
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data Management
        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("Pets_data", npcSaveList);
        void LoadData()
        {
            try
            {
                npcSaveList = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PetData>>("Pets_data");
            }
            catch
            {
                npcSaveList = new Dictionary<ulong, PetData>();
            }
        }

        class PetData
        {
            public uint prefabID;
            public float x, y, z;
            public byte[] inventory;
            internal bool NeedToSpawn;

            public PetData()
            {
                NeedToSpawn = true;
            }

            public PetData(NpcAI pet)
            {
                x = pet.transform.position.x;
                y = pet.transform.position.y;
                z = pet.transform.position.z;
                prefabID = pet.entity.prefabID;
                inventory = pet.inventory.Save().ToProtoBytes();
                NeedToSpawn = false;
            }
        }
        #endregion

        #region Localization
        static void UserMessage(BasePlayer player, string key, string[] args = null) => player.ChatMessage(args == null ? ins.lang.GetMessage(key, ins, player.UserIDString) : string.Format(ins.lang.GetMessage(key, ins, player.UserIDString), args));

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"tooFar", "<color=#939393>You are too far away</color>"},
            {"isTired", "<color=#939393>Your pet tired and needs to rest</color>"},
            {"isHungry", "<color=#939393>Your pet is hungry and needs to eat</color>"},
            {"isEating", "<color=#939393>Your pet is now eating</color>"},
            {"isDrinking", "<color=#939393>Your pet is going to get a drink</color>"},
            {"notThirsty", "<color=#939393>Your pet is not thirsty</color>"},
            {"isAttacking", "<color=#939393>Your pet is now attacking</color>"},
            {"openInv", "<color=#939393>Opening your pets inventory</color>"},
            {"stopFollow", "<color=#939393>Your pet has stopped following you</color>"},
            {"startFollow", "<color=#939393>Your pet is now following you</color>"},
            {"isPet", "<color=#939393>This animal is already someones a pet</color>"},
            {"tooFast", "<color=#939393>You are trying to tame animals too fast</color>"},
            {"noPerms", "<color=#939393>You do not have the required permissions to tame this animal</color>"},
            {"petSet", "<color=#939393>You have set this animal as your pet</color>"},
            {"isEnabled", "<color=#939393>Pet control activated\nType </color><color=#ce422b>\"/pet help\" </color><color=#939393>to show the help menu!</color>"},
            {"isDisabled", "<color=#939393>Pet control deactivated</color>"},
            {"help0", "<color=#ce422b>{0}  </color><color=#939393>v</color><color=#ce422b>{1}</color>" },
            {"help1", "<color=#ce422b>/pet </color><color=#939393>- Enable/disable pet control</color>"},
            {"help2", "<color=#ce422b>/pet help </color><color=#939393>- Show this menu</color>"},
            {"help3", "<color=#ce422b>/pet draw </color><color=#939393>- Enable/disable ddraw control markers</color>"},
            {"help4", "<color=#ce422b>/pet free </color><color=#939393>- Release your pet back into the wild</color>"},
            {"help5", "<color=#ce422b>/pet sleep </color><color=#939393>- Put your pet to sleep, or wake them up</color>"},
            {"help6", "<color=#ce422b>/pet info </color><color=#939393>- Display statistics for your pet</color>"},
            {"drawEnabled", "<color=#939393>Activated ddraw display</color>"},
            {"drawDisabled", "<color=#939393>Deactivated ddraw display</color>"},
            {"noDdraw", "<color=#939393>DDraw display is not enabled on this server</color>"},
            {"hasReleased", "<color=#939393>You released your pet back into the wild</color>"},
            {"awoken", "<color=#939393>Your pet has awoken</color>"},
            {"sleeping", "<color=#939393>Your pet is sleeping</color>"},
            {"statistics", "<color=#ce422b>Pet Statistics:</color><color=#939393>\nHealth :</color><color=#ce422b>{0}</color><color=#939393>\nEnergy :</color><color=#ce422b>{1}</color><color=#939393>\nHydration :</color><color=#ce422b>{2}</color><color=#939393>\nStamina :</color><color=#ce422b>{3}</color><color=#939393>\nSleep :</color><color=#ce422b>{4}</color>"}
        };
        #endregion
    }


}
