using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PsyArrows", "Ryan", "2.0.2", ResourceId = 1413)]
    [Description("Allows players with permission to use various different custom arrow types")]
    class PsyArrows : RustPlugin
    {
        #region Declaration

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id),
            args);
        private ArrowType arrowType;
        private readonly Dictionary<ulong, ArrowType> ActiveArrows = new Dictionary<ulong, ArrowType>();
        System.Random rnd = new System.Random();
        private enum ArrowType
        {
            Wind,
            Fire,
            Explosive,
            Knockdown,
            Narco,
            Poison,
            None
        }

        #endregion

        #region Config

        private ConfigFile configFile;
        class ConfigFile
        {
            public Dictionary<ArrowType, Arrow> Arrows;
            public RocketSettings RocketSettings;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    Arrows = new Dictionary<ArrowType, Arrow>()
                    {
                        { ArrowType.Wind, new Arrow() },
                        { ArrowType.Fire, new Arrow() },
                        { ArrowType.Explosive, new Arrow() },
                        { ArrowType.Knockdown, new Arrow() },
                        { ArrowType.Narco, new Arrow() },
                        { ArrowType.Poison, new Arrow() },
                    },
                    RocketSettings = new RocketSettings()
                };
            }
        }

        class RocketSettings
        {
            public float DetonationTime;
            public float ProjectileSpeed;
            public float GravityModifier;

            public RocketSettings()
            {
                DetonationTime = 5f;
                ProjectileSpeed = 90f;
                GravityModifier = 0f;
            }
        }

        class Price
        {
            public bool Enabled;
            public string ItemShortname;
            public int ItemAmount;

            public Price()
            {
                Enabled = true;
                ItemShortname = "metal.refined";
                ItemAmount = 30;
            }
        }

        class Arrow
        {
            public Price ArrowPrice;
            public string Permission;
            public Arrow()
            {
                ArrowPrice = new Price();
                Permission = "psyarrows.able";
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            configFile = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configFile = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(configFile);

        #endregion

        #region Lang

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Cmd_Types"] = "<color=orange>ARROW TYPES</color> \n{0}",
                ["Cmd_Switched"] = "You've switched your arrow type to '<color=orange>{0}</color>'",
                ["Cmd_Disabled"] = "You've sucessfully <color=orange>disabled</color> your active arrow",
                ["Cmd_NoPerm"] = "You don't have permission to use arrow '<color=orange>{0}</color>'",

                ["Error_NotSelected"] = "Your bow isn't drawn, you must select am arrow using '<color=orange>/arrow</color>'.",
                ["Error_InvalidEnt"] = "Your arrow didn't hit anything",

                ["Hit_Wind"] = "You hit <color=orange>{0}</color> with your <color=orange>Wind</color> arrow",
                ["Hit_Fire"] = "You hit <color=orange>{0}</color> (<color=orange>{1}</color> HP) with a <color=orange>Fire</color> arrow",
                ["Hit_Explosive"] = "You hit <color=orange>{0}</color> (<color=orange>{1}</color> HP) with an <color=orange>Explosive</color> arrow",
                ["Hit_Knockdown"] = "You knocked <color=orange>{0}</color> out, choose his fate!",
                ["Hit_Narco"] = "You've sent <color=orange>{0}</color> to sleep, act quick before they wake up again!",
                ["Hit_Poison"] = "You've sucessfully poisoned <color=orange>{0}</color> (<color=orange>{1}</color> HP)",

                ["Damaged_Poison"] = "You've been poisoned by an poisoned arrow, there's no cure!",

                ["Resources_Needed"] = "You need <color=orange>{0}</color>x <color=orange>{1}</color> to use that arrow",
                ["Resources_Spent"] = "The arrow you just used costed <color=orange>{0}</color>x <color=orange>{1}</color>",
            }, this);
        }

        #endregion

        #region Methods

        private bool CanUseArrow(BasePlayer player, ArrowType type, out Item outItem)
        {
            var typeConfig = configFile.Arrows[type];

            if (!typeConfig.ArrowPrice.Enabled)
            {
                outItem = null;
                return true;
            }

            var item = player.inventory.FindItemID(typeConfig.ArrowPrice.ItemShortname);
            int amount = player.inventory.GetAmount(item.info.itemid);

            if (amount >= typeConfig.ArrowPrice.ItemAmount)
            {
                outItem = ItemManager.CreateByName(typeConfig.ArrowPrice.ItemShortname,
                    typeConfig.ArrowPrice.ItemAmount);
                return true;
            }

            outItem = null;
            var neededAmount = typeConfig.ArrowPrice.ItemAmount - amount;
            PrintToChat(player, Lang("Resources_Needed", player.UserIDString, neededAmount, item.info.displayName.english));
            return false;
        }

        private void TakeItems(BasePlayer player, Item item)
        {
            player.inventory.Take(player.inventory.FindItemIDs(item.info.itemid), item.info.itemid, item.amount);
            PrintToChat(player, Lang("Resources_Spent", player.UserIDString, item.amount, item.info.displayName.english));
        }

        private BaseEntity CreateRocket(Vector3 startPoint, Vector3 direction, bool isFireRocket)
        {
            ItemDefinition projectileItem;

            if (isFireRocket)
                projectileItem = ItemManager.FindItemDefinition("ammo.rocket.fire");
            else
                projectileItem = ItemManager.FindItemDefinition("ammo.rocket.basic");

            ItemModProjectile component = projectileItem.GetComponent<ItemModProjectile>();
            BaseEntity entity = GameManager.server.CreateEntity(component.projectileObject.resourcePath, startPoint);
            TimedExplosive timedExplosive = entity.GetComponent<TimedExplosive>();
            ServerProjectile serverProjectile = entity.GetComponent<ServerProjectile>();

            serverProjectile.gravityModifier = configFile.RocketSettings.GravityModifier;
            serverProjectile.speed = configFile.RocketSettings.ProjectileSpeed;
            timedExplosive.timerAmountMin = configFile.RocketSettings.DetonationTime;

            entity.Spawn();

            return null;
        }

        private bool CanActivateArrow(BasePlayer player, ArrowType type)
        {
            var typeConfig = configFile.Arrows[type];
            if (!permission.UserHasPermission(player.UserIDString, typeConfig.Permission))
            {
                PrintToChat(player, Lang("Cmd_NoPerm", player.UserIDString, type));
                return false;
            }
            return true;
        }

        #endregion

        #region Hooks

        void Init()
        {
            foreach (var arrow in configFile.Arrows)
            {
                if (!permission.PermissionExists(arrow.Value.Permission, this))
                    permission.RegisterPermission(arrow.Value.Permission, this);
            }
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            if (hitInfo == null || attacker == null) return;
            if (hitInfo.WeaponPrefab.ToString().Contains("hunting") || hitInfo.Weapon.name.Contains("bow") && attacker.IsAdmin)
            {
                var hitPlayer = hitInfo.HitEntity.ToPlayer();

                if (hitPlayer != null)
                {
                    if (ActiveArrows.TryGetValue(attacker.userID, out arrowType))
                    {
                        Item foundItem;
                        switch (arrowType)
                        {
                            case ArrowType.Wind:

                                if (!CanUseArrow(attacker, arrowType, out foundItem))
                                    return;
                                else
                                    TakeItems(attacker, foundItem);
                                var newPos = new Vector3(hitPlayer.transform.position.x + 1,
                                    hitPlayer.transform.position.y + rnd.Next(5, 8),
                                    hitPlayer.transform.position.z + 1);

                                hitPlayer.MovePosition(newPos);
                                PrintToChat(attacker, Lang("Hit_Wind", attacker.UserIDString, hitPlayer.name));
                                ActiveArrows.Remove(attacker.userID);
                                break;

                            case ArrowType.Fire:
                                if (!CanUseArrow(attacker, arrowType, out foundItem))
                                    return;
                                else
                                    TakeItems(attacker, foundItem);

                                CreateRocket(hitPlayer.transform.position, hitPlayer.transform.position, true);
                                PrintToChat(attacker,
                                    Lang("Hit_Fire", attacker.UserIDString, hitPlayer.name, hitPlayer.Health()));
                                ActiveArrows.Remove(attacker.userID);
                                break;

                            case ArrowType.Explosive:
                                if (!CanUseArrow(attacker, arrowType, out foundItem))
                                    return;
                                else
                                    TakeItems(attacker, foundItem);

                                PrintToChat(attacker,
                                    Lang("Hit_Explosive", attacker.UserIDString, hitPlayer.name, hitPlayer.Health()));
                                CreateRocket(hitPlayer.transform.position, hitPlayer.transform.position, false);
                                ActiveArrows.Remove(attacker.userID);
                                break;

                            case ArrowType.Knockdown:
                                if (!CanUseArrow(attacker, arrowType, out foundItem))
                                    return;
                                else
                                    TakeItems(attacker, foundItem);

                                PrintToChat(attacker, Lang("Hit_Knockdown", attacker.UserIDString, hitPlayer.name));
                                hitPlayer.StartWounded();
                                ActiveArrows.Remove(attacker.userID);
                                break;

                            case ArrowType.Narco:
                                if (!CanUseArrow(attacker, arrowType, out foundItem))
                                    return;
                                else
                                    TakeItems(attacker, foundItem);

                                PrintToChat(attacker, Lang("Hit_Narco", attacker.UserIDString, hitPlayer.name));
                                hitPlayer.StartSleeping();
                                ActiveArrows.Remove(attacker.userID);
                                break;

                            case ArrowType.Poison:
                                if (!CanUseArrow(attacker, arrowType, out foundItem))
                                    return;
                                else
                                    TakeItems(attacker, foundItem);

                                PrintToChat(attacker,
                                    Lang("Hit_Poison", attacker.UserIDString, hitPlayer.name, hitPlayer.Health()));
                                PrintToChat(hitPlayer, Lang("Damaged_Poison", hitPlayer.UserIDString));
                                attacker.metabolism.poison.value = 30;
                                ActiveArrows.Remove(attacker.userID);
                                break;
                        }
                    }
                    else
                    {
                        PrintToChat(attacker, Lang("Error_NotSelected", attacker.UserIDString));
                        if (ActiveArrows.ContainsKey(attacker.userID))
                            ActiveArrows.Remove(attacker.userID);
                    }
                }
            }
        }

        [ChatCommand("arrow")]
        void arrow(BasePlayer player, string command, string[] args)
        {
            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "wind":
                        arrowType = ArrowType.Wind;
                        if (!CanActivateArrow(player, arrowType))
                            return;
                        break;

                    case "fire":
                        arrowType = ArrowType.Fire;
                        if (!CanActivateArrow(player, arrowType))
                            return;
                        break;

                    case "explosive":
                        arrowType = ArrowType.Explosive;
                        if (!CanActivateArrow(player, arrowType))
                            return;
                        break;

                    case "knockdown":
                        arrowType = ArrowType.Knockdown;
                        if (!CanActivateArrow(player, arrowType))
                            return;
                        break;

                    case "narco":
                        arrowType = ArrowType.Narco;
                        if (!CanActivateArrow(player, arrowType))
                            return;
                        break;

                    case "poision":
                        arrowType = ArrowType.Poison;
                        if (!CanActivateArrow(player, arrowType))
                            return;
                        break;

                    case "none":
                        if (!ActiveArrows.ContainsKey(player.userID))
                            goto default;
                        ActiveArrows[player.userID] = ArrowType.None;
                        PrintToChat(player, Lang("Cmd_Disabled", player.UserIDString, arrowType));
                        return;

                    default:
                        PrintToChat(player, Lang("Cmd_Types", player.UserIDString, string.Join("\n", Enum.GetNames(typeof(ArrowType)))));
                        break;
                }
                if (!ActiveArrows.ContainsKey(player.userID))
                    ActiveArrows.Add(player.userID, arrowType);
                PrintToChat(player, Lang("Cmd_Switched", player.UserIDString, arrowType));
            }
            else
            {
                PrintToChat(player, Lang("Cmd_Types", player.UserIDString, string.Join("\n", Enum.GetNames(typeof(ArrowType)))));
            }
        }

        #endregion
    }
}
