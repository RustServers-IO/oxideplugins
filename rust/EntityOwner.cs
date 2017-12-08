using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Entity Owner", "rustservers.io", "3.1.7", ResourceId = 1255)]
    [Description("Modify entity ownership and cupboard/turret authorization")]      
    class EntityOwner : RustPlugin
    {
        #region Data & Config
        private Dictionary<string, string> messages = new Dictionary<string, string>();
        private readonly int layerMasks = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed");

        private bool prodKeyCode = true;
        private int EntityLimit = 8000;
        private float DistanceThreshold = 3f;
        private float CupboardDistanceThreshold = 20f;

        private bool debug = false;

        #endregion

        #region Data Handling & Initialization

        private List<string> texts = new List<string>() {
            "You are not allowed to use this command",
            "Ownership data wiped!",
            "No target found",
            "Owner: {0}",
            "Target player not found",
            "Invalid syntax: /owner",
            "Invalid Syntax. \n/own type player\nTypes: all/block/storage/cupboard/sign/sleepingbag/plant/oven/door/turret\n/own player",
            "Invalid Syntax. \n/unown type player\nTypes: all/block/storage/cupboard/sign/sleepingbag/plant/oven/door/turret\n/unown player",
            "Invalid Syntax. \n/prod2 type \nTypes:\n all/block/entity/storage/cupboard/sign/sleepingbag/plant/oven/door/turret",
            "Invalid Syntax. \n/auth turret player\n/auth cupboard player/auth player\n/auth",
            "No building or entities found.",
            "Changing ownership..",
            "Removing ownership..",
            "Exceeded entity limit.",
            "Counted {0} entities ({1}/{2})",
            "New owner of all around is: {0}",
            "Owner: You were given ownership of this house and nearby deployables",
            "No entities found.",
            "Prodding structure..",
            "Prodding cupboards..",
            "Count ({0})",
            "Unknown player",
            "Unknown: {0}%",
            "Authorizing cupboards..",
            "Authorized {0} on {1} cupboards",
            "({0}) Authorized",
            "Ownership data expired!",
            "Authorized {0} on {1} turrets",
            "Authorizing turrets..",
            "Prodding turrets..",
            "Deauthorized {0} on {1} turrets",
            "Deauthorizing turrets..",
            "Deauthorizing cupboards..",
            "Deauthorized {0} on {1} cupboards",
            "Code: {0}",
        };

        // Loads the default configuration
        protected override void LoadDefaultConfig()
        {
            PrintToConsole("Creating new configuration file");

            var messages = new Dictionary<string, object>();

            foreach (var text in texts)
            {
                if (messages.ContainsKey(text))
                {
                    PrintWarning("Duplicate translation string: {0}", text);
                }
                else
                {
                    messages.Add(text, text);
                }
            }

            Config["messages"] = messages;
            Config["VERSION"] = Version.ToString();
            Config["EntityLimit"] = 8000;
            Config["DistanceThreshold"] = 3.0f;
            Config["CupboardDistanceThreshold"] = 20f;
            Config["prodKeyCode"] = true;

            Config.Save();
        }

        protected void ReloadConfig()
        {
            var messages = new Dictionary<string, object>();

            foreach (var text in texts)
            {
                if (!messages.ContainsKey(text))
                {
                    messages.Add(text, text);
                }
            }

            Config["messages"] = messages;
            Config["VERSION"] = Version.ToString();

            // NEW CONFIGURATION OPTIONS HERE
            Config["prodKeyCode"] = GetConfig("prodKeyCode", true);
            // END NEW CONFIGURATION OPTIONS

            PrintToConsole("Upgrading Configuration File");
            SaveConfig();
            LoadMessages();
        }

        // Gets a config value of a specific type
        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null)
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        void OnServerInitialized()
        {
            try
            {
                LoadConfig();

                debug = GetConfig("Debug", false);
                EntityLimit = GetConfig("EntityLimit", 8000);
                DistanceThreshold = GetConfig("DistanceThreshold", 3f);
                CupboardDistanceThreshold = GetConfig("CupboardDistanceThreshold", 20f);
                prodKeyCode = GetConfig("prodKeyCode", true);

                if (DistanceThreshold >= 5)
                {
                    PrintWarning("ALERT: Distance threshold configuration option is ABOVE 5.  This may cause serious performance degradation (lag) when using EntityOwner commands");
                }

                LoadMessages();

                if (!permission.PermissionExists("entityowner.cancheckowners")) permission.RegisterPermission("entityowner.cancheckowners", this);
                if (!permission.PermissionExists("entityowner.canchangeowners")) permission.RegisterPermission("entityowner.canchangeowners", this);

                LoadData();
            }
            catch (Exception ex)
            {
                PrintError("OnServerInitialized failed: {0}", ex.Message);
            }
        }

        void LoadMessages() {
            var customMessages = GetConfig<Dictionary<string, object>>("messages", null);
            if (customMessages != null)
            {
                foreach (var kvp in customMessages.ToList())
                {
                    messages[kvp.Key] = kvp.Value.ToString();
                }
            }
        }

        void LoadData()
        {
            if (Config["VERSION"] == null)
            {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig();
            }
            else if (GetConfig("VERSION", Version.ToString()) != Version.ToString())
            {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig();
            }
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            var sb = new StringBuilder();
            if (canCheckOwners(player) || canChangeOwners(player))
            {
                sb.Append("<size=18>EntityOwner</size> by <color=#ce422b>Calytic</color> at <color=#ce422b>http://rustservers.io</color>\n");
            }

            if (canCheckOwners(player))
            {
                sb.Append("  ").Append("<color=\"#ffd479\">/prod</color> - Check ownership of entity you are looking at").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/prod2</color> - Check ownership of entire structure/all deployables").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/prod2 block</color> - Check ownership structure only").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/prod2 cupboard</color> - Check authorization on all nearby cupboards").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/auth</color> - Check authorization list of tool cupboard you are looking at").Append("\n");
            }

            if (canChangeOwners(player))
            {
                sb.Append("  ").Append("<color=\"#ffd479\">/own [all/block]</color> - Take ownership of entire structure").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/own [all/block] PlayerName</color> - Give ownership of entire structure to specified player").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/unown [all/block]</color> - Remove ownership from entire structure").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/auth PlayerName</color> - Authorize specified player on all nearby cupboards").Append("\n");
            }

            player.ChatMessage(sb.ToString());
        }

        #endregion



        #region Chat Commands

        [ChatCommand("prod")]
        void cmdProd(BasePlayer player, string command, string[] args)
        {
            if (!canCheckOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }
            if (args == null || args.Length == 0)
            {
                //var input = serverinput.GetValue(player) as InputState;
                //var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
                //var target = RaycastAll<BaseEntity>(player.transform.position + new Vector3(0f, 1.5f, 0f), currentRot);
                var target = RaycastAll<BaseEntity>(player.eyes.HeadRay());
                if (target is bool)
                {
                    SendReply(player, messages["No target found"]);
                    return;
                }
                if (target is BaseEntity)
                {
                    var targetEntity = target as BaseEntity;
                    var owner = GetOwnerName((BaseEntity)target);
                    if (string.IsNullOrEmpty(owner))
                    {
                        owner = "N/A";
                    }

                    string msg = string.Format(messages["Owner: {0}"], owner) + "\n<color=lightgrey>" + targetEntity.ShortPrefabName + "</color>";

                    if(prodKeyCode) {
                        if(target is Door) {
                            Door door = (Door)target;
                            BaseLock baseLock = door.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
                            if(baseLock is CodeLock) {
                                CodeLock codeLock = (CodeLock)baseLock;
                                string keyCode = codeLock.code;
                                msg += "\n" + string.Format(messages["Code: {0}"], keyCode);
                            }
                        }
                    }

                    SendReply(player, msg);
                }
            }
            else
            {
                SendReply(player, messages["Invalid syntax: /owner"]);
            }
        }

        [ChatCommand("own")]
        void cmdOwn(BasePlayer player, string command, string[] args)
        {
            if (!canChangeOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            var massTrigger = false;
            string type = null;
            ulong target = 0;;

            if (args.Length == 0)
            {
                args = new string[1] { "all" };
            }

            if (args.Length > 2)
            {
                SendReply(player, messages["Invalid Syntax. \n/own type player\nTypes: all/block/storage/cupboard/sign/sleepingbag/plant/oven/door/turret\n/own player"]);
                return;
            }
            else if (args.Length == 1)
            {
                type = args[0];
                if (type == "all" || type == "storage" || type == "block" || type == "cupboard" || type == "sign" || type == "sleepingbag" || type == "plant" || type == "oven" || type == "door" || type == "turret")
                {
                    massTrigger = true;
                    target = player.userID;
                }
                else
                {
                    target = FindUserIDByPartialName(type);
                    type = "all";
                    if (target == 0)
                    {
                        SendReply(player, messages["Target player not found"]);
                    }
                    else
                    {
                        massTrigger = true;
                    }
                }

            }
            else if (args.Length == 2)
            {
                type = args[0];
                target = FindUserIDByPartialName(args[1]);
                if (target == 0)
                {
                    SendReply(player, messages["Target player not found"]);
                }
                else
                {
                    massTrigger = true;
                }
            }

            if (!massTrigger || type == null || target == null) return;
                switch (type)
                {
                    case "all":
                        massChangeOwner<BaseEntity>(player, target);
                        break;
                    case "block":
                        massChangeOwner<BuildingBlock>(player, target);
                        break;
                    case "storage":
                        massChangeOwner<StorageContainer>(player, target);
                        break;
                    case "sign":
                        massChangeOwner<Signage>(player, target);
                        break;
                    case "sleepingbag":
                        massChangeOwner<SleepingBag>(player, target);
                        break;
                    case "plant":
                        massChangeOwner<PlantEntity>(player, target);
                        break;
                    case "oven":
                        massChangeOwner<BaseOven>(player, target);
                        break;
                    case "turret":
                        massChangeOwner<AutoTurret>(player, target);
                        break;
                    case "door":
                        massChangeOwner<Door>(player, target);
                        break;
                    case "cupboard":
                        massChangeOwner<BuildingPrivlidge>(player, target);
                        break;
                }
            }

        [ChatCommand("unown")]
        void cmdUnown(BasePlayer player, string command, string[] args)
        {
            if (!canChangeOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            if (args.Length == 0)
            {
                args = new[] { "all" };
            }

            if (args.Length > 1)
            {
                SendReply(player, messages["Invalid Syntax. \n/unown type player\nTypes: all/block/storage/cupboard/sign/sleepingbag/plant/oven/door/turret\n/unown player"]);
                return;
            }
            if (args.Length != 1) return;
                switch (args[0])
                {
                    case "all":
                        massChangeOwner<BaseEntity>(player);
                        break;
                    case "block":
                        massChangeOwner<BuildingBlock>(player);
                        break;
                    case "storage":
                        massChangeOwner<StorageContainer>(player);
                        break;
                    case "sign":
                        massChangeOwner<Signage>(player);
                        break;
                    case "sleepingbag":
                        massChangeOwner<SleepingBag>(player);
                        break;
                    case "plant":
                        massChangeOwner<PlantEntity>(player);
                        break;
                    case "oven":
                        massChangeOwner<BaseOven>(player);
                        break;
                    case "turret":
                        massChangeOwner<AutoTurret>(player);
                        break;
                    case "door":
                        massChangeOwner<Door>(player);
                        break;
                    case "cupboard":
                        massChangeOwner<BuildingPrivlidge>(player);
                        break;
                }
            }

        [ChatCommand("auth")]
        void cmdAuth(BasePlayer player, string command, string[] args)
        {
            if (!canChangeOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            var massCupboard = false;
            var massTurret = false;
            var checkCupboard = false;
            var checkTurret = false;
            var error = false;
            BasePlayer target = null;

            if (args.Length > 2)
            {
                error = true;
            }
            else if (args.Length == 1)
            {
                if (args[0] == "cupboard")
                {
                    checkCupboard = true;
                }
                else if (args[0] == "turret")
                {
                    checkTurret = true;
                }
                else
                {
                    massCupboard = true;
                    target = FindPlayerByPartialName(args[0]);
                }
            }
            else if (args.Length == 0)
            {
                checkCupboard = true;
            }
            else if (args.Length == 2)
            {
                if (args[0] == "cupboard")
                {
                    massCupboard = true;
                    target = FindPlayerByPartialName(args[1]);
                }
                else if (args[0] == "turret")
                {
                    massTurret = true;
                    target = FindPlayerByPartialName(args[1]);
                }
                else
                {
                    error = true;
                }
            }

            if ((massTurret || massCupboard) && target?.net?.connection == null)
            {
                SendReply(player, messages["Target player not found"]);
                return;
            }

            if (error)
            {
                SendReply(player, messages["Invalid Syntax. \n/auth turret player\n/auth cupboard player/auth player\n/auth"]);
                return;
            }

            if (massCupboard)
            {
                massCupboardAuthorize(player, target);
            }

            if (checkCupboard)
            {
                var priv = RaycastAll<BuildingPrivlidge>(player.eyes.HeadRay());
                if (priv is bool)
                {
                    SendReply(player, messages["No target found"]);
                    return;
                }
                if (priv is BuildingPrivlidge)
                {
                    ProdCupboard(player, (BuildingPrivlidge)priv);
                }
            }

            if (massTurret)
            {
                massTurretAuthorize(player, target);
            }

            if (checkTurret)
            {
                var turret = RaycastAll<AutoTurret>(player.eyes.HeadRay());
                if (turret is bool)
                {
                    SendReply(player, messages["No target found"]);
                    return;
                }
                if (turret is AutoTurret)
                {
                    ProdTurret(player, (AutoTurret)turret);
                }
            }
        }

        [ChatCommand("deauth")]
        void cmdDeauth(BasePlayer player, string command, string[] args)
        {
            if (!canChangeOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            var massCupboard = false;
            var massTurret = false;
            var error = false;
            BasePlayer target = null;

            if (args.Length > 2)
            {
                error = true;
            }
            else if (args.Length == 1)
            {
                if (args[0] == "cupboard")
                {
                    SendReply(player, "Invalid Syntax. /deauth cupboard PlayerName");
                    return;
                }
                else if (args[0] == "turret")
                {
                    SendReply(player, "Invalid Syntax. /deauth turret PlayerName");
                    return;
                }
                else
                {
                    massCupboard = true;
                    target = FindPlayerByPartialName(args[0]);
                }
            }
            else if (args.Length == 0)
            {
                SendReply(player, "Invalid Syntax. /deauth PlayerName\n/deauth turret/cupboard PlayerName");
                return;
            }
            else if (args.Length == 2)
            {
                if (args[0] == "cupboard")
                {
                    massCupboard = true;
                    target = FindPlayerByPartialName(args[1]);
                }
                else if (args[0] == "turret")
                {
                    massTurret = true;
                    target = FindPlayerByPartialName(args[1]);
                }
                else
                {
                    error = true;
                }
            }

            if ((massTurret || massCupboard) && target?.net?.connection == null)
            {
                    SendReply(player, messages["Target player not found"]);
                    return;
                }

            if (error)
            {
                SendReply(player, messages["Invalid Syntax. \n/auth turret player\n/auth cupboard player/auth player\n/auth"]);
                return;
            }

            if (massCupboard)
            {
                massCupboardDeauthorize(player, target);
            }

            if (massTurret)
            {
                massTurretDeauthorize(player, target);
            }
        }

        [ChatCommand("prod2")]
        void cmdProd2(BasePlayer player, string command, string[] args)
        {
            if (!canCheckOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            bool highlight = false;
            if (args.Length > 0)
            {
                if(args[0] == "highlight") {
                    highlight = true;
                    args = args.Skip(1).ToArray();
                }
                if (args.Length == 0)
                {
                    massProd<BaseEntity>(player, highlight);
                    return;
                }

                switch (args[0])
                {
                    case "all":
                        args = args.Skip(1).ToArray();
                        massProd<BaseEntity>(player, highlight, args);
                        break;
                    case "block":
                        massProd<BuildingBlock>(player, highlight);
                        break;
                    case "storage":
                        massProd<StorageContainer>(player, highlight);
                        break;
                    case "sign":
                        massProd<Signage>(player, highlight);
                        break;
                    case "sleepingbag":
                        massProd<SleepingBag>(player, highlight);
                        break;
                    case "plant":
                        massProd<PlantEntity>(player, highlight);
                        break;
                    case "oven":
                        massProd<BaseOven>(player, highlight);
                        break;
                    case "turret":
                        massProdTurret(player, highlight);
                        break;
                    case "cupboard":
                        massProdCupboard(player, highlight);
                        break;
                    default:
                        massProd<BaseEntity>(player, highlight, args);
                        break;
                }
            }
            else if (args.Length == 0)
            {
                massProd<BaseEntity>(player);
            }
            else
            {
                SendReply(player, messages["Invalid Syntax. \n/prod2 type \nTypes:\n all/block/entity/storage/cupboard/sign/sleepingbag/plant/oven/door/turret"]);
            }
        }

        #endregion

        #region Permission Checks

        bool canCheckOwners(BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission(player.UserIDString, "entityowner.cancheckowners");
        }

        bool canChangeOwners(BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission(player.UserIDString, "entityowner.canchangeowners");
        }

        #endregion

        #region Ownership Methods

        private void massChangeOwner<T>(BasePlayer player, ulong target = 0) where T : BaseEntity
        {
            object entityObject = false;

            if (typeof(T) == typeof(BuildingBlock))
            {
                entityObject = FindBuilding(player.transform.position, DistanceThreshold);
            }
            else
            {
                entityObject = FindEntity(player.transform.position, DistanceThreshold);
            }

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                if (target == 0)
                {
                    SendReply(player, messages["Removing ownership.."]);
                }
                else
                {
                    SendReply(player, messages["Changing ownership.."]);
                }

                var entity = entityObject as T;
                var entityList = new HashSet<T>();
                var checkFrom = new List<Vector3>();
                entityList.Add((T)entity);
                checkFrom.Add(entity.transform.position);
                var c = 1;
                if (target == 0)
                {
                    RemoveOwner(entity);
                }
                else
                {
                    ChangeOwner(entity, target);
                }
                var current = 0;
                var bbs = 0;
                var ebs = 0;
                if (entity is BuildingBlock)
                {
                    bbs++;
                }
                else
                {
                    ebs++;
                }
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (debug)
                        {
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit);
                        }
                        SendReply(player, string.Format(messages["Counted {0} entities ({1}/{2})"], c, bbs, ebs));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Counted {0} entities ({1}/{2})"], c, bbs, ebs));
                        break;
                    }

                    var hits = FindEntities<T>(checkFrom[current - 1], DistanceThreshold);

                    foreach (var entityComponent in hits.ToList())
                    {
                        if (!entityList.Add(entityComponent)) continue;
                        c++;
                        checkFrom.Add(entityComponent.transform.position);

                        if (entityComponent is BuildingBlock)
                        {
                            bbs++;
                        }
                        else
                        {
                            ebs++;
                        }

                        if (target == 0)
                        {
                            RemoveOwner(entityComponent);
                        }
                        else
                        {
                            ChangeOwner(entityComponent, target);
                        }
                    }
                    Pool.FreeList(ref hits);
                }

                if(target == 0) {
                    SendReply(player, string.Format(messages["New owner of all around is: {0}"], "No one"));
                } else {
                    BasePlayer targetPlayer = BasePlayer.FindByID(target);

                    if (targetPlayer != null)
                    {
                        SendReply(player, string.Format(messages["New owner of all around is: {0}"], targetPlayer.displayName));
                        SendReply(targetPlayer, messages["Owner: You were given ownership of this house and nearby deployables"]);
                    }
                    else
                    {
                        IPlayer pl = covalence.Players.FindPlayerById(target.ToString());
                        SendReply(player, string.Format(messages["Owner: {0}"], pl.Name));
                    }
                }
            }
        }

        private void massProd<T>(BasePlayer player, bool highlight = false, params string[] filter) where T : BaseEntity
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, DistanceThreshold);
            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                var prodOwners = new Dictionary<ulong, int>();
                var entity = entityObject as BaseEntity;
                if (entity.transform == null)
                {
                    SendReply(player, messages["No entities found."]);
                    return;
                }

                SendReply(player, messages["Prodding structure.."]);

                var entityList = new HashSet<T>();
                var checkFrom = new List<Vector3>();

                if (entity is T) {
                    entityList.Add((T)entity);
                }

                var total = 0;
                var skip = false;
                if (entity is T)
                {
                    if(filter.Length > 0) {
                        skip = true;
                        foreach(var f in filter) {
                            if(entity.name.ToLower().Contains(f.ToLower())) {
                                skip = false;
                                break;
                            }
                        }
                    }

                    if(!skip) {
                        prodOwners.Add(entity.OwnerID, 1);
                        total++;
                    }
                }

                var current = -1;
                var distanceThreshold = DistanceThreshold;
                if (typeof(T) != typeof(BuildingBlock) && typeof(T) != typeof(BaseEntity))
                    distanceThreshold += 30;

                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (debug)
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit);

                        SendReply(player, $"Count ({total})");
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, $"Count ({total})");
                        break;
                    }

                    var hits = FindEntities<T>(checkFrom.Count > 0 ? checkFrom[current-1] : entity.transform.position, distanceThreshold);
                    skip = false;
                    foreach (var fentity in hits)
                    {
                        if (fentity.transform == null || !entityList.Add(fentity) || fentity.name == "player/player")
                            continue;

                        if(filter.Length > 0) {
                            skip = true;
                            foreach(var f in filter) {
                                if(fentity.name.ToLower().Contains(f.ToLower())) {
                                    skip = false;
                                    break;
                                }
                            }
                        }

                        checkFrom.Add(fentity.transform.position);

                        if(!skip) {
                            total++;
                            if(highlight) {
                                SendHighlight(player, fentity.transform.position);
                            }

                            var pid = fentity.OwnerID;
                            if (prodOwners.ContainsKey(pid))
                            {
                                prodOwners[pid]++;
                            }
                            else
                            {
                                prodOwners.Add(pid, 1);
                            }
                        }
                    }

                    Pool.FreeList(ref hits);
                }

                var percs = new Dictionary<ulong, int>();
                var unknown = 100;
                if (total > 0)
                {
                    foreach (var kvp in prodOwners)
                    {
                        var perc = kvp.Value * 100 / total;
                        percs.Add(kvp.Key, perc);
                        var n = FindPlayerName(kvp.Key);

                        if (n != messages["Unknown player"])
                        {
                            SendReply(player, $"{n}: {perc}%");
                            unknown -= perc;
                        }
                    }
                }

                if (unknown > 0)
                    SendReply(player, string.Format(messages["Unknown: {0}%"], unknown));

            }
        }

        void SendHighlight(BasePlayer player, Vector3 position) {
            player.SendConsoleCommand("ddraw.sphere",  30f, Color.magenta, position, 2f);
            player.SendNetworkUpdateImmediate();
        }

        private void ProdCupboard(BasePlayer player, BuildingPrivlidge cupboard)
        {
            List<string> authorizedUsers;
            var sb = new StringBuilder();
            if(TryGetCupboardUserNames(cupboard, out authorizedUsers)) {
                sb.AppendLine(string.Format(messages["({0}) Authorized"], authorizedUsers.Count));
                foreach (var n in authorizedUsers)
                    sb.AppendLine(n);

            } else
                sb.Append(string.Format(messages["({0}) Authorized"], 0));

            SendReply(player, sb.ToString());
        }

        private void ProdTurret(BasePlayer player, AutoTurret turret)
        {
            List<string> authorizedUsers;
            var sb = new StringBuilder();
            if(TryGetTurretUserNames(turret, out authorizedUsers)) {
                sb.Append(string.Format(messages["({0}) Authorized"], 0));
            } else {
                sb.AppendLine(string.Format(messages["({0}) Authorized"], authorizedUsers.Count));
                foreach (var n in authorizedUsers)
                    sb.AppendLine(n);
            }

            SendReply(player, sb.ToString());
        }

        private void massProdCupboard(BasePlayer player, bool highlight = false)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                var total = 0;
                var prodOwners = new Dictionary<ulong, int>();
                SendReply(player, messages["Prodding cupboards.."]);
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity>();
                var checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (debug)
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit);

                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }

                    var entities = FindEntities<BuildingPrivlidge>(checkFrom[current - 1], CupboardDistanceThreshold);

                    foreach (var e in entities)
                    {
                        if (!entityList.Add(e)) { continue; }
                        if(highlight) {
                            SendHighlight(player, e.transform.position);
                        }
                        checkFrom.Add(e.transform.position);

                        foreach (var pnid in e.authorizedPlayers)
                        {
                            if (prodOwners.ContainsKey(pnid.userid))
                                prodOwners[pnid.userid]++;
                            else
                                prodOwners.Add(pnid.userid, 1);
                        }

                        total++;
                    }
                    Pool.FreeList(ref entities);
                }

                var percs = new Dictionary<ulong, int>();
                var unknown = 100;
                if (total > 0)
                {
                    foreach (var kvp in prodOwners)
                    {
                        var perc = kvp.Value * 100 / total;
                        percs.Add(kvp.Key, perc);
                        var n = FindPlayerName(kvp.Key);

                        if (n != messages["Unknown player"])
                        {
                            SendReply(player, n + ": " + perc + "%");
                            unknown -= perc;
                        }
                    }

                    if (unknown > 0)
                        SendReply(player, string.Format(messages["Unknown: {0}%"], unknown));
                }
            }
        }

        private void massProdTurret(BasePlayer player, bool highlight = false)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                var total = 0;
                var prodOwners = new Dictionary<ulong, int>();
                SendReply(player, messages["Prodding turrets.."]);
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity>();
                var checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (debug)
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit);

                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }

                    var entities = FindEntities<BaseEntity>(checkFrom[current - 1], DistanceThreshold);

                    foreach (var e in entities)
                    {
                        if (!entityList.Add(e)) { continue; }
                        if(highlight) {
                            SendHighlight(player, e.transform.position);
                        }
                        checkFrom.Add(e.transform.position);

                        if(e is AutoTurret) {
                            var turret = e as AutoTurret;
                            if(turret.OwnerID.IsSteamId()) {
                                if (prodOwners.ContainsKey(turret.OwnerID))
                                    prodOwners[turret.OwnerID]++;
                                else
                                    prodOwners.Add(turret.OwnerID, 1);
                            }

                            foreach (var pnid in turret.authorizedPlayers)
                            {
                                if (prodOwners.ContainsKey(pnid.userid))
                                    prodOwners[pnid.userid]++;
                                else
                                    prodOwners.Add(pnid.userid, 1);
                            }
                        } else if(e is FlameTurret) {
                            var turret = e as FlameTurret;
                            if(turret.OwnerID.IsSteamId()) {
                                if (prodOwners.ContainsKey(turret.OwnerID))
                                    prodOwners[turret.OwnerID]++;
                                else
                                    prodOwners.Add(turret.OwnerID, 1);
                            }
                        } else {
                            continue;
                        }



                        total++;
                    }

                    Pool.FreeList(ref entities);
                }

                var percs = new Dictionary<ulong, int>();
                var unknown = 100;
                if (total > 0)
                {
                    foreach (var kvp in prodOwners)
                    {
                        var perc = kvp.Value * 100 / total;
                        percs.Add(kvp.Key, perc);
                        var n = FindPlayerName(kvp.Key);

                        if (n != messages["Unknown player"])
                        {
                            SendReply(player, n + ": " + perc + "%");
                            unknown -= perc;
                        }
                    }

                    if (unknown > 0)
                        SendReply(player, string.Format(messages["Unknown: {0}%"], unknown));
                }
            }
        }

        private void massCupboardAuthorize(BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                var total = 0;
                SendReply(player, messages["Authorizing cupboards.."]);
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity>();
                var checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (debug)
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit);

                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }

                    var entities = FindEntities<BuildingPrivlidge>(checkFrom[current - 1], CupboardDistanceThreshold);

                    foreach (var priv in entities)
                    {
                        if (!entityList.Add(priv)) continue;
                        checkFrom.Add(priv.transform.position);
                        if (HasCupboardAccess(priv, target)) continue;
                        priv.authorizedPlayers.Add(new ProtoBuf.PlayerNameID()
                        {
                            userid = target.userID,
                            username = target.displayName
                        });

                        priv.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                        
                        total++;
                    }
                    Pool.FreeList(ref entities);
                }

                SendReply(player, string.Format(messages["Authorized {0} on {1} cupboards"], target.displayName, total));
            }
        }

        private void massCupboardDeauthorize(BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                var total = 0;
                SendReply(player, messages["Deauthorizing cupboards.."]);
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity>();
                var checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (debug)
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit);

                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }

                    var entities = FindEntities<BuildingPrivlidge>(checkFrom[current - 1], CupboardDistanceThreshold);

                    foreach (var priv in entities)
                    {
                        if (!entityList.Add(priv)) continue;
                        checkFrom.Add(priv.transform.position);

                        if (!HasCupboardAccess(priv, target)) continue;
                        foreach (var p in priv.authorizedPlayers.ToArray())
                        {
                            if (p.userid == target.userID)
                            {
                                priv.authorizedPlayers.Remove(p);
                                priv.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            }
                        }

                        total++;
                    }
                    Pool.FreeList(ref entities);
                }

                SendReply(player, string.Format(messages["Deauthorized {0} on {1} cupboards"], target.displayName, total));
            }
        }

        private void massTurretAuthorize(BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                var total = 0;
                SendReply(player, messages["Authorizing turrets.."]);
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity>();
                var checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (debug)
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit);

                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }

                    var entities = FindEntities<BaseEntity>(checkFrom[current - 1], DistanceThreshold);

                    foreach (var e in entities)
                    {
                        if (!entityList.Add(e)) continue;
                            checkFrom.Add(e.transform.position);

                        var turret = e as AutoTurret;
                        if (turret == null || HasTurretAccess(turret, target)) continue;
                        turret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID()
                        {
                            userid = target.userID,
                            username = target.displayName
                        });

                        turret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                        turret.SetTarget(null);
                        total++;
                    }
                    Pool.FreeList(ref entities);
                }

                SendReply(player, string.Format(messages["Authorized {0} on {1} turrets"], target.displayName, total));
            }
        }

        private void massTurretDeauthorize(BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                var total = 0;
                SendReply(player, messages["Deauthorizing turrets.."]);
                var entity = entityObject as BaseEntity;
                var entityList = new HashSet<BaseEntity>();
                var checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (debug)
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit);

                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total));
                        break;
                    }

                    var entities = FindEntities<BaseEntity>(checkFrom[current - 1], DistanceThreshold);

                    foreach (var e in entities)
                    {
                        if (!entityList.Add(e)) continue;
                            checkFrom.Add(e.transform.position);

                        var turret = e as AutoTurret;
                        if (turret == null || !HasTurretAccess(turret, target)) continue;
                        foreach (var p in turret.authorizedPlayers.ToArray())
                        {
                            if (p.userid == target.userID)
                            {
                                turret.authorizedPlayers.Remove(p);
                                turret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                                turret.SetTarget(null);
                                total++;
                            }
                        }
                    }
                    Pool.FreeList(ref entities);
                }

                SendReply(player, string.Format(messages["Deauthorized {0} on {1} turrets"], target.displayName, total));
            }
        }

        private bool TryGetCupboardUserNames(BuildingPrivlidge cupboard, out List<string> names)
        {
            names = new List<string>();
            if(cupboard.authorizedPlayers == null)
                return false;
            if (cupboard.authorizedPlayers.Count == 0)
                return false;

            foreach (var pnid in cupboard.authorizedPlayers)
                names.Add($"{FindPlayerName(pnid.userid)} - {pnid.userid}");

            return true;
        }

        private bool TryGetTurretUserNames(AutoTurret turret, out List<string> names)
        {
            names = new List<string>();
            if (turret.authorizedPlayers == null)
                return false;
            if (turret.authorizedPlayers.Count == 0)
                return false;

            foreach (var pnid in turret.authorizedPlayers)
                names.Add($"{FindPlayerName(pnid.userid)} - {pnid.userid}");

            return true;
        }

        private bool HasCupboardAccess(BuildingPrivlidge cupboard, BasePlayer player)
        {
            return cupboard.IsAuthed(player);
        }

        private bool HasTurretAccess(AutoTurret turret, BasePlayer player)
        {
            return turret.IsAuthed(player);
        }

        string GetOwnerName(BaseEntity entity)
        {
            return FindPlayerName(entity.OwnerID);
        }

        BasePlayer GetOwnerPlayer(BaseEntity entity)
        {
            if(entity.OwnerID.IsSteamId()) {
                return BasePlayer.FindByID(entity.OwnerID);
            }

            return null;
        }

        IPlayer GetOwnerIPlayer(BaseEntity entity) {
            if(entity.OwnerID.IsSteamId()) {
                return covalence.Players.FindPlayerById(entity.OwnerID.ToString());
            }

            return null;
        }

        void RemoveOwner(BaseEntity entity)
        {
            entity.OwnerID = 0;
        }

        bool ChangeOwner(BaseEntity entity, object player)
        {
            if(player is BasePlayer) {
                entity.OwnerID = ((BasePlayer)player).userID;
                return true;
            } if(player is IPlayer) {
                entity.OwnerID = Convert.ToUInt64(((IPlayer)player).Id);
                return true;
            } else if(player is ulong && ((ulong)player).IsSteamId()) {
                entity.OwnerID = (ulong)player;
                return true;
            } else if(player is string) {
                ulong id;
                if(ulong.TryParse((string)player, out id) && id.IsSteamId()) {
                    entity.OwnerID = (ulong)player;
                    return true;
                } else {
                    var basePlayer = BasePlayer.Find((string)player);
                    if(basePlayer is BasePlayer) {
                        entity.OwnerID = basePlayer.userID;
                        return true;
                    }
                }
            }

            return false;
        }

        object FindEntityData(BaseEntity entity)
        {
            if (!entity.OwnerID.IsSteamId())
            {
                return false;
            }

            return entity.OwnerID.ToString();
        }

        #endregion

        #region Utility Methods

        private object RaycastAll<T>(Vector3 Pos, Vector3 Aim) where T : BaseEntity
        {
            var hits = Physics.RaycastAll(Pos, Aim);
            GamePhysics.Sort(hits);
            var distance = 100f;
            object target = false;
            foreach (var hit in hits)
            {
                var ent = hit.GetEntity();
                if (ent is T && hit.distance < distance)
                {
                    target = ent;
                    break;
                }
            }

            return target;
        }

        private object RaycastAll<T>(Ray ray) where T : BaseEntity
        {
            var hits = Physics.RaycastAll(ray);
            GamePhysics.Sort(hits);
            var distance = 100f;
            object target = false;
            foreach (var hit in hits)
            {
                var ent = hit.GetEntity();
                if (ent is T && hit.distance < distance)
                {
                    target = ent;
                    break;
                }
            }

            return target;
        }

        object FindBuilding(Vector3 position, float distance = 3f)
        {
            var hit = FindEntity<BuildingBlock>(position, distance);

            if (hit != null)
            {
                return hit;
            }

            return false;
        }

        object FindEntity(Vector3 position, float distance = 3f, params string[] filter)
        {
            var hit = FindEntity<BaseEntity>(position, distance, filter);

            if (hit != null)
            {
                return hit;
            }

            return false;
        }

        T FindEntity<T>(Vector3 position, float distance = 3f, params string[] filter) where T : BaseEntity
        {
            var list = Pool.GetList<T>();
            Vis.Entities(position, distance, list, layerMasks);

            if (list.Count > 0)
            {
                foreach(var e in list) {
                    if(filter.Length > 0) {
                        foreach(var f in filter) {
                            if(e.name.Contains(f)) {
                                return e;
                            }
                        }
                    } else {
                        return e;
                    }
                }
                Pool.FreeList(ref list);
            }

            return null;
        }

        List<T> FindEntities<T>(Vector3 position, float distance = 3f) where T : BaseEntity
        {
            var list = Pool.GetList<T>();
            Vis.Entities(position, distance, list, layerMasks);
            return list;
        }

        List<BuildingBlock> GetProfileConstructions(BasePlayer player)
        {
            var result = new List<BuildingBlock>();
            var blocks = UnityEngine.Object.FindObjectsOfType<BuildingBlock>();
            foreach (var block in blocks)
            {
                if (block.OwnerID == player.userID)
                {
                    result.Add(block);
                }
            }

            return result;
        }

        List<BaseEntity> GetProfileDeployables(BasePlayer player)
        {
            var result = new List<BaseEntity>();
            var entities = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
            foreach (var entity in entities)
            {
                if (entity.OwnerID == player.userID && !(entity is BuildingBlock))
                {
                    result.Add(entity);
                }
            }

            return result;
        }

        void ClearProfile(BasePlayer player)
        {
            var entities = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
            foreach (var entity in entities)
            {
                if (entity.OwnerID == player.userID && !(entity is BuildingBlock))
                {
                    RemoveOwner(entity);
                }
            }
        }

        private void SendChatMessage(BasePlayer player, string message)
        {
            player.ChatMessage(message);
        }

        private string FindPlayerName(ulong playerID)
        {
            if(playerID.IsSteamId()) {
                var player = FindPlayerByPartialName(playerID.ToString());
                if (player)
                {
                    if (player.IsSleeping())
                    {
                        return $"{player.displayName} [<color=lightblue>Sleeping</color>]";
                    }
                    else {
                        return $"{player.displayName} [<color=lime>Online</color>]";
                    }
                }

                var p = covalence.Players.FindPlayerById(playerID.ToString());
                if (p != null)
                {
                    return $"{p.Name} [<color=red>Offline</color>]";
                }
            }

            return $"Unknown : {playerID}";
        }

        void SetValue(object inputObject, string propertyName, object propertyVal)
        {
            var type = inputObject.GetType();
            var propertyInfo = type.GetField(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);

            var propertyType = propertyInfo.FieldType;

            var targetType = IsNullableType(propertyType) ? Nullable.GetUnderlyingType(propertyType) : propertyType;

            propertyVal = Convert.ChangeType(propertyVal, targetType);

            propertyInfo.SetValue(inputObject, propertyVal);
        }

        private bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>));
        }

        protected ulong FindUserIDByPartialName(string name) {
            if (string.IsNullOrEmpty(name))
                return 0;

            ulong userID;
            if(ulong.TryParse(name, out userID)) {
                return userID;
            }

            IPlayer player = covalence.Players.FindPlayer(name);

            if(player != null) {
                return Convert.ToUInt64(player.Id);
            }

            return 0;
        }

        protected BasePlayer FindPlayerByPartialName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            IPlayer player = covalence.Players.FindPlayer(name);

            if(player != null) {
                return (BasePlayer)player.Object;
            }

            return null;
        }

        #endregion
    }
}