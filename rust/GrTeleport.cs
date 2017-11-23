using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("GrTeleport", "carny666", "1.1.2", ResourceId = 2665)]
    class GrTeleport : RustPlugin
    {
        #region permissions
        private const string adminPermission = "GrTeleport.admin";
        private const string grtPermission = "GrTeleport.use";
        #endregion

        #region local variabls / supporting classes
        GrTeleportData grTeleportData;
        
        int lastGridTested = 0;
        List<SpawnPosition> spawnGrid = new List<SpawnPosition>();
        List<Cooldown> coolDowns = new List<Cooldown>();
        List<RustUser> rustUsers = new List<RustUser>();

        private bool debug = false;

        class GrTeleportData
        {
            public int CooldownInSeconds { get; set; }
            public bool AvoidWater { get; set; }
            public bool AllowBuildingBlocked { get; set; }
            public int LimitPerDay { get; set; }
            public string RestrictedZones { get; set; }
            public int DistanceToWarnConstructions { get; set;}
            public float AllowableWaterDepth { get; set; }
            public Dictionary<string, GroupData> groupData = new Dictionary<string, GroupData>();
        }

        class SpawnPosition
        {
            public Vector3 Position;
            public Vector3 GroundPosition;
            public string GridReference;

            public SpawnPosition(Vector3 position)
            {
                Position = position;
                GroundPosition = GetGroundPosition(new Vector3(position.x, 25, position.z));
            }

            public bool isPositionAboveWater()
            {
                if ((TerrainMeta.HeightMap.GetHeight(Position) - TerrainMeta.WaterMap.GetHeight(Position)) >= 0)
                    return false;
                return true;
            }

            public float WaterDepthAtPosition()
            {
                return (TerrainMeta.WaterMap.GetHeight(Position) - TerrainMeta.HeightMap.GetHeight(Position));
            }

            Vector3 GetGroundPosition(Vector3 sourcePos)
            {
                LayerMask GROUND_MASKS = LayerMask.GetMask("Terrain", "World", "Construction");  // TODO: mountain? wtf?
                RaycastHit hitInfo;

                if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo, GROUND_MASKS))
                    sourcePos.y = hitInfo.point.y;

                sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));

                return sourcePos;
            }

        }

        class Cooldown
        {
            public string name;
            public int cooldownPeriodSeconds;
            public DateTime lastUse;
            public DateTime expirtyDateTime;

            public Cooldown(string PlayerName, int CoolDownInSeconds) 
            {
                name = PlayerName;
                cooldownPeriodSeconds = CoolDownInSeconds;
                lastUse = DateTime.Now;
                expirtyDateTime = lastUse.AddSeconds(cooldownPeriodSeconds);
            }

        }

        class RustUser
        {
            public BasePlayer Player { get; set; }
            public int TeleportsRemaining { get; set; }
            public DateTime ResetDateTime { get; set; }
            public int CoolDownInSeconds { get; set; }
        }

        class GroupData
        {
            public int coolDownPeriodSeconds { get; set; }
            public int dailyTeleports { get; set; }
            public string groupName { get; set; }
        }
        #endregion

        #region events

        void Init()
        {
            grTeleportData = Config.ReadObject<GrTeleportData>();
            if (Config["Messages"] != null)
                Config.WriteObject(grTeleportData, true);

            PrintToChat($"{this.Title} {this.Version} Initialized @ {DateTime.Now.ToLongTimeString()}...");
        }

        void Loaded()
        {            
            try
            {
                permission.RegisterPermission(adminPermission, this);
                permission.RegisterPermission(grtPermission, this);

                spawnGrid = CreateSpawnGrid();

                lang.RegisterMessages(new Dictionary<string, string>
                {
                    { "buildingblocked", "You cannot grTeleport into or out from a building blocked area." },
                    { "noinit", "spawnGrid was not initialized. 0 spawn points available." },
                    { "teleported", "You have GrTeleported to {gridreference}, {position}. You're {groupname}, so you have a {cooldown} second cooldown between uses and {limit} grTeleports." }, // {gridreference}
                    { "teleportlimit", "You have {teleportsremaining} remaining." }, // {teleportsremaining}
                    { "teleportminutesexhauseted", "You have exhausted your remaining grTeleports for today. You can use grTeleport in {minutesleft} minutes." },  // minutesleft hoursleft
                    { "teleporthoursexhauseted", "You have exhausted your remaining grTeleports for today. You can use grTeleport in {hoursleft} hours." },  // minutesleft hoursleft
                    { "overwater", "That refernce point is above water." },
                    { "cmdusage", "usage ex: /grt n10  (where n = a-zz and 10=0-60" },
                    { "noaccess", "You do not have sufficient access to execute this command." },
                    { "sgerror", "Error creating spawnpoints, too much water? contact dev." },
                    { "cooldown", "Sorry, your are currently in a {cooldownperiod} second cooldown, you have another {secondsleft} seconds remaining." }, // {cooldownperiod} {secondsleft}
                    { "cooldownreply", "Cooldown has been set to {cooldownperiod} seconds" },
                    { "gridwidthreply", "Gridwidth has been set to {gridwidth}x{gridwidth}" },
                    { "cuboardreply", "Buidling block teleportation is {togglebuildingblocked}" },
                    { "avoidwaterreplay", "Avoid water has been set tp {avoidwater}" },
                    { "dailylimitreply", "Daily limit has been set to {dailylimit}." },
                    { "cupboard", "Sorry, you cannot teleport within {distance} of a cupboard." },
                    { "zoneadded", "Restricted Zone ({zone}) has been added, you now have {zones}." },
                    { "zonenotadded", "You need to enter a zone (or a comma seperated list of zones) as well." },
                    { "restrictedzone", "You cannot teleport her, {zone} is restricted." },
                    { "zonecomma", "You need to supply a commaseperated list of zones." },
                    { "zonesadded", "Restricted Zones ({zone}) have been added, you now have {zones}." },
                    { "zonenotremoved", "Restricted Zone ({zone}) has not been removed, you now have {zones}." },
                    { "zoneremoved", "Restricted Zone ({zone}) have been removed, you now have {zones}." },
                    { "zonesremoved", "Restricted Zones ({zone}) have been removed, you now have {zones}." },
                    { "nozones", "There are no zones to remove." },
                    { "buildonref", "Your construction is within the vicinty of a grTransport reference. You may want to reconsider building here within {amount}m. your are now {distance}." },
                    { "constructionreply", "Construction/Grid Reference distance has been set to {DistanceToWarnConstructions}m." },
                    { "setgroupusage", "type /setgroup groupName 30 10 - where 30 is cooldown and 10 is daily teleport limit." },
                    { "setgroupusageerror", "Must have 3 arguments. /setgroup groupName 30 10" },
                    { "setwaterdepthreply", "Allowable water depth has been set to {waterdepth}" },
                    { "setwaterdeptherror", "usage: /setwaterdepth 1.0" }
                }, this, "en");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error: Loaded {ex.Message}");
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            try
            {
                var amount = grTeleportData.DistanceToWarnConstructions;
                foreach (SpawnPosition sp in spawnGrid)
                {
                    var distance = Vector3.Distance(sp.GroundPosition, GetGroundPosition(plan.GetOwnerPlayer().transform.position));

                    if (distance <= amount)
                    {
                        PrintToChat(plan.GetOwnerPlayer(), lang.GetMessage("buildonref", this, plan.GetOwnerPlayer().UserIDString).Replace("{amount}", amount.ToString()).Replace("{distance}", distance.ToString("0.0")));
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError($"{ex.Message}");
            }

        }

        protected override void LoadDefaultConfig()
        {
            var data = new GrTeleportData
            {
                CooldownInSeconds = 15,
                AvoidWater = true,
                AllowBuildingBlocked = false,
                LimitPerDay = -1, // -1 = unlim
                RestrictedZones = "ZZZ123,YYY666",
                DistanceToWarnConstructions = 15,
                AllowableWaterDepth = 1.0f,
                groupData = new Dictionary<string, GroupData>()
        };
            Config.WriteObject(data, true);
        }
        #endregion

        #region commands
        [ChatCommand("grt")]
        void chatCommandGrt(BasePlayer player, string command, string[] args)
        {
            try
            {
                var userGroupData = GetUsersGroupDataOrDefault(player);

                if (userGroupData == null)
                {
                    CheckAccess(player, "grt", "userGroupDataNull", true);
                    return;
                }

                var tmp = GetCooldown(player.displayName);
                if (tmp != null)  
                {
                    PrintToChat(player, lang.GetMessage("cooldown", this, player.UserIDString).Replace("{cooldownperiod}", tmp.cooldownPeriodSeconds.ToString()).Replace("{secondsleft}", tmp.expirtyDateTime.Subtract(DateTime.Now).TotalSeconds.ToString("0")));
                    return;
                }

                if (spawnGrid == null || spawnGrid.Count <= 0)
                    spawnGrid = CreateSpawnGrid();

                if (args.Length > 0)
                {
                    var gr = args[0];
                    var index = GridIndexFromReference(gr);

                    if (TestRestrictedZone(gr)) // vip have zones? later..
                    {
                        PrintToChat(player, lang.GetMessage("restrictedzone", this, player.UserIDString).Replace("{zone}", gr.ToUpper()));
                        return;
                    }


                    if (player.IsBuildingBlocked(spawnGrid[index].GroundPosition, new Quaternion(0, 0, 0, 0), new Bounds(Vector3.zero, Vector3.zero)) && !grTeleportData.AllowBuildingBlocked) 
                    {
                        PrintToChat(player, lang.GetMessage("buildingblocked", this, player.UserIDString));
                        return;
                    }

                    if (spawnGrid[index].isPositionAboveWater() && grTeleportData.AvoidWater && (spawnGrid[index].WaterDepthAtPosition() > grTeleportData.AllowableWaterDepth))
                    {
                        PrintToChat($"{spawnGrid[index].WaterDepthAtPosition().ToString("0.00")} meters. deep");
                        PrintToChat(player, lang.GetMessage("overwater", this, player.UserIDString).Replace("{depth}", spawnGrid[index].WaterDepthAtPosition().ToString("0.00")));
                        return;
                    }

                    var user = GetOrCreateUser(player, userGroupData);
                    if (user.TeleportsRemaining == 0) // none left
                    {
                        if ((user.ResetDateTime - DateTime.Now).TotalHours < 1) 
                            PrintToChat(player, lang.GetMessage("teleportminutesexhauseted", this, player.UserIDString).Replace("{minutesleft}", (user.ResetDateTime - DateTime.Now).TotalMinutes.ToString("0")).Replace("{hoursleft}", (user.ResetDateTime - DateTime.Now).TotalHours.ToString("0")));
                        else
                            PrintToChat(player, lang.GetMessage("teleporthoursexhauseted", this, player.UserIDString).Replace("{minutesleft}", (user.ResetDateTime - DateTime.Now).TotalMinutes.ToString("0")).Replace("{hoursleft}", (user.ResetDateTime - DateTime.Now).TotalHours.ToString("0")));
                        
                        return;
                    }


                    if (TeleportToGridReference(player, gr, false))                    
                    {
                        if (user.TeleportsRemaining != -1)
                            user.TeleportsRemaining--;

                        PrintToChat(player, lang.GetMessage("teleported", this, player.UserIDString)
                            .Replace("{position}", player.transform.position.ToString())
                            .Replace("{gridreference}", gr.ToUpper())
                            .Replace("{groupname}", userGroupData.groupName)
                            .Replace("{cooldown}", userGroupData.coolDownPeriodSeconds.ToString())
                            .Replace("{limit}", ((userGroupData.dailyTeleports == -1) ? "unlimited" : userGroupData.dailyTeleports.ToString()) ));

                        if (user.TeleportsRemaining != -1)  
                            PrintToChat(player, lang.GetMessage("teleportlimit", this, player.UserIDString)
                                .Replace("{teleportsremaining}", user.TeleportsRemaining.ToString()));

                        AddToCoolDown(player.displayName, userGroupData.coolDownPeriodSeconds);


                        return;
                    }
                    
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error chatCommandGrt:{ex.Message}");
                return;
            }

            PrintToChat(player, lang.GetMessage("cmdusage", this, player.UserIDString));
        }

        [ConsoleCommand("grt.nextspawn")]
        void ccGrtNextspawn(ConsoleSystem.Arg arg)
        {
            try
            {
                if (arg.Player() == null) return;

                BasePlayer player = arg.Player();

                if (spawnGrid.Count <= 0)
                    throw new Exception(lang.GetMessage("noinit", this, player.UserIDString));

                if (!CheckAccess(player, "grt.nextspawn", adminPermission)) return;

                do
                {
                    if (++lastGridTested >= spawnGrid.Count) lastGridTested = 0;
                } while (spawnGrid[lastGridTested].isPositionAboveWater());

                Teleport(player, spawnGrid[lastGridTested].GroundPosition, false);

                PrintToChat(player, lang.GetMessage("teleported", this, player.UserIDString)
                    .Replace("{gridreference}", spawnGrid[lastGridTested].GridReference)
                    .Replace("{position}", player.transform.position.ToString()) );
            }
            catch (Exception ex)
            {
                throw new Exception($"ccGrtNextspawn {ex.Message}");
            }
        }

        [ConsoleCommand("grt.prevspawn")]
        void ccGrtPrevspawn(ConsoleSystem.Arg arg)
        {
            try
            {
                if (arg.Player() == null) return;

                BasePlayer player = arg.Player();

                if (spawnGrid.Count <= 0)
                    throw new Exception(lang.GetMessage("noinit", this, player.UserIDString));

                if (!CheckAccess(player, "grt.prevspawn", adminPermission)) return;

                do
                {
                    if (--lastGridTested < 0) lastGridTested = spawnGrid.Count-1;
                } while (spawnGrid[lastGridTested].isPositionAboveWater());

                Teleport(player, spawnGrid[lastGridTested].GroundPosition, false);

                PrintToChat(player, lang.GetMessage("teleported", this, player.UserIDString)
                    .Replace("{gridreference}", spawnGrid[lastGridTested].GridReference)
                    .Replace("{position}", player.transform.position.ToString()));
            }
            catch (Exception ex)
            {
                throw new Exception($"ccGrtPrevspawn {ex.Message}");
            }
        }

        [ChatCommand("setcooldown")]
        void chmSetCooldown(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (!CheckAccess(player, "setcooldown", adminPermission)) return;
                if (args.Length > 0)
                    grTeleportData.CooldownInSeconds = int.Parse(args[0]);

                coolDowns.Clear();

                Config.WriteObject(grTeleportData, true);
                PrintToChat(player, lang.GetMessage("cooldownreply", this, player.UserIDString).Replace("{cooldownperiod}", grTeleportData.CooldownInSeconds.ToString()));
            }
            catch (Exception ex)
            {
                throw new Exception($"chmSetCooldown {ex.Message}");
            }

        }

        [ChatCommand("setwaterdepth")]
        void chmsetwaterdepth(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (!CheckAccess(player, "setwaterdepth", adminPermission)) return;
                try
                {
                    if (args.Length > 0)
                        grTeleportData.AllowableWaterDepth = float.Parse(args[0]);
                } catch(Exception  ex)
                {
                    PrintToChat(player, lang.GetMessage("setwaterdeptherror", this, player.UserIDString)
                        .Replace("{waterdepth}", grTeleportData.AllowableWaterDepth.ToString("0.00")));
                }

                Config.WriteObject(grTeleportData, true);
                PrintToChat(player, lang.GetMessage("setwaterdepthreply", this, player.UserIDString)
                    .Replace("{waterdepth}", grTeleportData.AllowableWaterDepth.ToString("0.00")));
            }
            catch (Exception ex)
            {
                throw new Exception($"chmSetwaterdepth {ex.Message}");
            }

        }

        [ChatCommand("setconstruction")]
        void chmSetconstruction(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (!CheckAccess(player, "setconstruction", adminPermission)) return;
                if (args.Length > 0)
                    grTeleportData.DistanceToWarnConstructions = int.Parse(args[0]);

                coolDowns.Clear();

                Config.WriteObject(grTeleportData, true);
                PrintToChat(player, lang.GetMessage("constructionreply", this, player.UserIDString).Replace("{DistanceToWarnConstructions}",grTeleportData.DistanceToWarnConstructions.ToString()));
            }
            catch (Exception ex)
            {
                throw new Exception($"chmSetconstruction {ex.Message}");
            }
        }

        [ChatCommand("setdailylimit")]
        void chmSetDailyLimit(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (!CheckAccess(player, "setdailylimit", adminPermission)) return;
                if (args.Length > 0)
                    grTeleportData.LimitPerDay = int.Parse(args[0]);

                rustUsers.Clear();
                Config.WriteObject(grTeleportData, true);
                PrintToChat(player, lang.GetMessage("dailylimitreply", this, player.UserIDString).Replace("{dailylimit}", grTeleportData.LimitPerDay.ToString()));
            }
            catch (Exception ex)
            {
                throw new Exception($"chmSetCooldown {ex.Message}");
            }
        }

        [ChatCommand("addzone")]
        void chmAddzone(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (!CheckAccess(player, "chmAddzone", adminPermission)) return;
                if (args.Length > 0)
                {
                    if (string.IsNullOrEmpty(grTeleportData.RestrictedZones))
                        grTeleportData.RestrictedZones = "";

                    if (args[0].Contains(","))
                    {
                        foreach (var z in args[0].ToUpper().Split(','))
                        {
                            if (grTeleportData.RestrictedZones.Length > 0)
                                grTeleportData.RestrictedZones += $",{z}";
                            else
                                grTeleportData.RestrictedZones += $"{z}";
                        }
                        Config.WriteObject(grTeleportData, true);
                        PrintToChat(player, lang.GetMessage("zoneadded", this, player.UserIDString).Replace("{zone}", args[0].ToUpper()).Replace("{zones}", grTeleportData.RestrictedZones));
                    }
                    else
                    {
                        if (grTeleportData.RestrictedZones.Length > 0)
                            grTeleportData.RestrictedZones += $",{args[0].ToUpper()}";
                        else
                            grTeleportData.RestrictedZones += $"{args[0].ToUpper()}";

                        Config.WriteObject(grTeleportData, true);
                        PrintToChat(player, lang.GetMessage("zonesadded", this, player.UserIDString).Replace("{zone}", args[0].ToUpper()).Replace("{zones}", grTeleportData.RestrictedZones));
                    }
                }
                else
                {
                    PrintToChat(player, lang.GetMessage("zonenotadded", this, player.UserIDString).Replace("{zones}", grTeleportData.RestrictedZones));
                }



            }
            catch (Exception ex)
            {
                throw new Exception($"chmSetCooldown {ex.Message}");
            }
        }

        [ChatCommand("removezone")]
        void chmRemoveZone(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (!CheckAccess(player, "chmRemovezone", adminPermission)) return;
                if (args.Length > 0)
                {
                    if (string.IsNullOrEmpty(grTeleportData.RestrictedZones))
                    {
                        PrintToChat(player, lang.GetMessage("nozones", this, player.UserIDString));
                        return;
                    }
                    List<string> tmpZoneList = new List<string>(grTeleportData.RestrictedZones.Split(','));

                    if (args[0].Contains(","))
                    {
                        foreach (var z in args[0].ToUpper().Split(','))
                        {
                            if (tmpZoneList.Contains(z))
                                tmpZoneList.Remove(z);                            
                        }

                        grTeleportData.RestrictedZones = string.Join(",", tmpZoneList.ToArray());
                        Config.WriteObject(grTeleportData, true);
                        PrintToChat(player, lang.GetMessage("zonesremoved", this, player.UserIDString).Replace("{zone}", args[0].ToUpper()).Replace("{zones}", grTeleportData.RestrictedZones));
                    }
                    else
                    {
                        if (tmpZoneList.Contains(args[0].ToUpper()))
                            tmpZoneList.Remove(args[0].ToUpper());

                        grTeleportData.RestrictedZones = string.Join(",", tmpZoneList.ToArray());
                        Config.WriteObject(grTeleportData, true);
                        PrintToChat(player, lang.GetMessage("zoneremoved", this, player.UserIDString).Replace("{zone}", args[0].ToUpper()).Replace("{zones}", grTeleportData.RestrictedZones));
                    }
                }
                else
                {
                    PrintToChat(player, lang.GetMessage("zonenotremoved", this, player.UserIDString).Replace("{zones}", grTeleportData.RestrictedZones));
                }



            }
            catch (Exception ex)
            {
                throw new Exception($"chmSetCooldown {ex.Message}");
            }
        }

        [ChatCommand("togglebuildingblocked")]
        void chmtogglebuildingblocked(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (!CheckAccess(player, "togglebuildingblocked", adminPermission)) return;
                grTeleportData.AllowBuildingBlocked = !grTeleportData.AllowBuildingBlocked;
                Config.WriteObject(grTeleportData, true);
                PrintToChat(player, lang.GetMessage("cuboardreply", this, player.UserIDString).Replace("{togglebuildingblocked}", grTeleportData.AllowBuildingBlocked.ToString()));
            }
            catch (Exception ex)
            {
                throw new Exception($"setcupboard {ex.Message}");
            }

        }

        [ChatCommand("toggleavoidwater")]
        void chmSetAvoidWater(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (!CheckAccess(player, "toggleavoidwater", adminPermission)) return;
                grTeleportData.AvoidWater = !grTeleportData.AvoidWater;
                Config.WriteObject(grTeleportData, true);
                PrintToChat(player, lang.GetMessage("avoidwaterreplay", this, player.UserIDString).Replace("{avoidwater}", grTeleportData.AvoidWater.ToString()));
            }
            catch (Exception ex)
            {
                throw new Exception($"chmSetAvoidWater {ex.Message}");
            }

        }

        [ChatCommand("setgroup")]
        void chmSetGroup(BasePlayer player, string command, string[] args)
        {
            try
            {
                //if (!CheckAccess(player, "setgroup", adminPermission)) return;

                if (args.Length == 0) // help
                {
                    PrintToChat(player, lang.GetMessage("setgroupusage", this, player.UserIDString));
                    return;
                }

                string GroupName = "";
                int cooldownInSeconds = grTeleportData.CooldownInSeconds;
                int dailyLimit = grTeleportData.LimitPerDay;

                if (args.Length == 3) // set..
                {
                    try
                    {
                        GroupName = args[0];
                        cooldownInSeconds = int.Parse(args[1]);
                        dailyLimit = int.Parse(args[2]);
                    }
                    catch (Exception)
                    {
                        PrintToChat(player, lang.GetMessage("setgroupusageerror", this, player.UserIDString));
                        return;
                    }
                }
                else // bad comd, help
                {
                    PrintToChat(player, lang.GetMessage("setgroupusageerror", this, player.UserIDString));
                    return;
                }

                if (!grTeleportData.groupData.ContainsKey(GroupName))
                    grTeleportData.groupData.Add(GroupName, new GroupData { coolDownPeriodSeconds = cooldownInSeconds, dailyTeleports = dailyLimit, groupName = GroupName });
                else
                {
                    grTeleportData.groupData[GroupName].coolDownPeriodSeconds = cooldownInSeconds;
                    grTeleportData.groupData[GroupName].dailyTeleports = dailyLimit;
                }
                Config.WriteObject(grTeleportData, true);
                PrintToChat(player, $"Saved {GroupName} cooldown:{grTeleportData.groupData[GroupName].coolDownPeriodSeconds} Limit:{grTeleportData.groupData[GroupName].dailyTeleports}.");
                //PrintToChat(player, lang.GetMessage("cooldownreply", this, player.UserIDString).Replace("{cooldownperiod}", grTeleportData.CooldownInSeconds.ToString()));
            }
            catch (Exception ex)
            {
                throw new Exception($"chmSetCooldown {ex.Message}");
            }

        }

        [ChatCommand("getgroups")]
        void chmGetGroups(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (!CheckAccess(player, "getgroups", adminPermission)) return;

                foreach(var g in grTeleportData.groupData)
                    PrintToChat(player, $"{g.Key} cooldown:{g.Value.coolDownPeriodSeconds} Limit:{g.Value.dailyTeleports}.");
                return;
            }
            catch (Exception ex)
            {
                throw new Exception($"chmGetGroup {ex.Message}");
            }

        }

        [ChatCommand("clearcooldown")]
        void chmClearCooldown(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (!CheckAccess(player, "clearcooldown", adminPermission)) return;

                if (args.Length == 1)
                {
                    var cd = coolDowns.Find(x => x.name.ToLower().Contains(args[0].ToLower()));
                    if (cd != null)
                    {
                        coolDowns.Remove(cd);
                        PrintToChat($"Cleared {cd.name}..");
                    }
                    else
                    {
                        PrintToChat($"{cd.name} not found.");
                    }
                    return;
                }
                coolDowns.Clear();
                PrintToChat($"Cleared Cooldowns");
                return;
            }
            catch (Exception ex)
            {
                throw new Exception($"chmClearCooldown {ex.Message}");
            }

        }

        #endregion

        #region API
        [HookMethod("TeleportToGridReference")]
        private bool TeleportToGridReference(BasePlayer player, string gridReference, bool avoidWater = true)
        {
            try
            {
                var index = GridIndexFromReference(gridReference);
                if (avoidWater && spawnGrid[index].isPositionAboveWater()) return false;
                Teleport(player, spawnGrid[index].GroundPosition);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"TeleportToGridReference {ex.Message}");
            }

        }

        [HookMethod("IsGridReferenceAboveWater")]
        private bool IsGridReferenceAboveWater(string gridReference)
        {
            try
            {
                var index = GridIndexFromReference(gridReference);
                return spawnGrid[index].isPositionAboveWater();
            }
            catch (Exception ex)
            {
                throw new Exception($"IsGridReferenceAboveWater {ex.Message}");
            }

        }
        #endregion

        #region supporting fuctions

        int GridIndexFromReference(string gridReference)
        {
            try
            {
                foreach(SpawnPosition s in spawnGrid)
                {
                    if (gridReference.ToUpper().Trim() == s.GridReference.ToUpper().Trim())
                        return spawnGrid.IndexOf(s);
                }
                throw new Exception($"GridIndexFromReference {gridReference.ToUpper()} was not found in spawnGrid {spawnGrid.Count}");
            }
            catch (Exception ex)
            {
                throw new Exception($"GridIndexFromReference {ex.Message}");
            }

        }

        bool TestRestrictedZone(string gridReference)
        {
            if (string.IsNullOrEmpty(grTeleportData.RestrictedZones)) return false;

            if (grTeleportData.RestrictedZones.Length > 0)
            {
                if (grTeleportData.RestrictedZones.Contains(","))
                {
                    var zones = grTeleportData.RestrictedZones.Split(',');
                    foreach(var z in zones)
                    {
                        if (z.ToUpper() == gridReference.ToUpper())
                            return true;
                    }                    
                }
                else // only one
                {
                    if (grTeleportData.RestrictedZones.ToUpper() == gridReference.ToUpper())
                        return true;
                }               
            }
            return false;
        }

        Cooldown GetCooldown(string playerName)
        {
            try
            {
                var cnt = coolDowns.RemoveAll(x => x.expirtyDateTime <= DateTime.Now);
                var index = coolDowns.FindIndex(x => x.name.ToLower() == playerName.ToLower());
                if (index == -1) return null;

                return coolDowns[index];
            }
            catch (Exception ex)
            {
                throw new Exception($"GetCooldown {ex.Message}", ex);
            }
        }

        List<SpawnPosition> CreateSpawnGrid()
        {
            try
            {
                List<SpawnPosition> retval = new List<SpawnPosition>();

                var worldSize = (ConVar.Server.worldsize);
                float offset = worldSize / 2;
                var gridWidth = (0.0066666666666667f * worldSize);
                float step = worldSize / gridWidth;
                string start = "";

                char letter = 'A';
                int number = 0;

                for (float zz = offset; zz > -offset; zz -= step)
                {
                    for (float xx = -offset; xx < offset; xx += step)
                    {
                        var sp = new SpawnPosition(new Vector3(xx, 0, zz));
                        sp.GridReference = $"{start}{letter}{number}";
                        retval.Add(sp);
                        number++;
                    }

                    number = 0;
                    if (letter.ToString().ToUpper() == "Z")
                    {
                        start = "A";
                        letter = 'A';
                    }
                    else
                    {
                        letter = (char)(((int)letter) + 1);
                    }
                }
                return retval;
            } catch(Exception ex)
            {
                throw new Exception($"CreateSpawnGrid {ex.Message}");
            }
        }

        void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;

            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);

            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);

            player.CancelInvoke("InventoryUpdate");
            //player.inventory.crafting.CancelAll(true);
            //player.UpdatePlayerCollider(true, false);
        }

        void Teleport(BasePlayer player, Vector3 position, bool startSleeping = true)
        {
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");

            if (startSleeping)
                StartSleeping(player);

            player.MovePosition(position);

            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);

            player.SendNetworkUpdate();

            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);

            player.UpdateNetworkGroup();

            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;

            //TODO temporary for potential rust bug
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }

        bool CheckAccess(BasePlayer player, string command, string sPermission, bool onErrorDisplayMessageToUser = true)
        {
            try
            {
                if (!permission.UserHasPermission(player.UserIDString, sPermission))
                {
                    if (onErrorDisplayMessageToUser)
                        PrintToChat(player, lang.GetMessage("noaccess", this, player.UserIDString));

                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"CheckAccess {ex.Message}");
            }
        }

        void AddToCoolDown(string userName, int seconds)
        {
            coolDowns.Add(new Cooldown(userName.ToLower(), seconds));
        }

        bool AreThereCupboardsWithinDistance(Vector3 position, int distance)
        {
            try
            {
                var spawns = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject s in spawns)
                {
                    if (Vector3.Distance(s.transform.position, position) < distance)
                    {
                        if (s.name.Contains("tool_cupboard"))
                            return true;
                    }
                }
                return false;
            } catch(Exception ex)
            {
                throw new Exception($"AreThereCupboardsWithinDistance {ex.Message}");
            }
        }

        RustUser GetOrCreateUser(BasePlayer player, GroupData groupData)
        {
            var index = rustUsers.FindIndex(x => x.Player== player);

            if (index == -1)
            {
                var user = new RustUser {
                    Player = player,
                    TeleportsRemaining = groupData.dailyTeleports,
                    ResetDateTime = DateTime.Now.AddHours(24),
                    CoolDownInSeconds = groupData.coolDownPeriodSeconds }
                ;
                rustUsers.Add(user);
                return rustUsers[rustUsers.IndexOf(user)];
            } 

            if (rustUsers[index].ResetDateTime <= DateTime.Now) // reset daily limit 
            {
                rustUsers[index].ResetDateTime = DateTime.Now.AddHours(24);
                rustUsers[index].TeleportsRemaining = groupData.dailyTeleports; //grTeleportData.LimitPerDay;
                rustUsers[index].CoolDownInSeconds = groupData.coolDownPeriodSeconds;
            }

            return rustUsers[index];
        }

        Vector3 GetGroundPosition(Vector3 sourcePos)
        {   // wish i understood this.
            LayerMask GROUND_MASKS = LayerMask.GetMask("Terrain", "World", "Construction");  // TODO: mountain? wtf?
            RaycastHit hitInfo;

            if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo, GROUND_MASKS))
                sourcePos.y = hitInfo.point.y;
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));

            return sourcePos;
        }

        GroupData GetUsersGroupDataOrDefault(BasePlayer user)
        {

            // test admin
            if (permission.UserHasPermission(user.UserIDString, adminPermission))
            {
                GroupData groupData = new GroupData { coolDownPeriodSeconds = 0, dailyTeleports = 9999, groupName = "admin" };
                if (debug)
                    PrintToChat($"{user.displayName} is in the {groupData.groupName} group with {groupData.coolDownPeriodSeconds} cd and {(groupData.dailyTeleports == -1 ? "unlimited teleports" : groupData.dailyTeleports.ToString() + " daily teleport limit")}.");
                return groupData;
            }

            // test for each groups defined..
            foreach (var p in grTeleportData.groupData)
            {
                string groupName = p.Key;
                GroupData groupData = p.Value;
                // on match return group data
                if (permission.UserHasGroup(user.UserIDString, groupName))
                {
                    if (debug)
                        PrintToChat($"{user.displayName} is in the {groupData.groupName} group with {groupData.coolDownPeriodSeconds} cd and {(groupData.dailyTeleports == -1 ? "unlimited teleports" : groupData.dailyTeleports.ToString() + " daily teleport limit")}.");

                    return groupData;
                }
            }

            // if has def permission 
            if (permission.UserHasPermission(user.UserIDString, grtPermission))
            {
                GroupData groupData = new GroupData { coolDownPeriodSeconds = grTeleportData.CooldownInSeconds, dailyTeleports = grTeleportData.LimitPerDay, groupName = "default" };
                if (debug)
                    PrintToChat($"{user.displayName} is in the {groupData.groupName} group with {groupData.coolDownPeriodSeconds} cd and {(groupData.dailyTeleports == -1 ? "unlimited teleports" : groupData.dailyTeleports.ToString() + " daily teleport limit")}.");

                return groupData;
            }

            if (debug)
                PrintToChat($"{user.displayName} has no permission.");

            return null; // no permission!!
        }

        #endregion

    }
}

