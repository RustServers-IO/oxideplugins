using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("GrTeleport", "carny666", "1.0.5", ResourceId = 2665)]
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

        class GrTeleportData
        {
            public int CooldownInSeconds { get; set; }
            public int GridWidth { get; set; }
            public bool AvoidWater { get; set; }
            public int CupboardDistance { get; set; }
        }

        class SpawnPosition
        {
            public Vector3 Position;
            public Vector3 GroundPosition;
            public bool aboveWater;

            public SpawnPosition(Vector3 position)
            {
                Position = position;
                aboveWater = PositionAboveWater(Position);
                GroundPosition = GetGroundPosition(new Vector3(position.x, 0, position.z));
            }

            bool PositionAboveWater(Vector3 Position)
            {
                if ((TerrainMeta.HeightMap.GetHeight(Position) - TerrainMeta.WaterMap.GetHeight(Position)) >= 0)
                    return false;
                return true;
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
        #endregion

        #region events

        void Init()
        {
            grTeleportData = Config.ReadObject<GrTeleportData>();
            if (Config["Messages"] != null)
                Config.WriteObject(grTeleportData, true);
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
                    { "nosquares", "Admin must configure set the grid width using setgridwidth ##" },
                    { "noinit", "spawnGrid was not initialized. 0 spawn points available." },
                    { "teleported", "You have GrTeleported to {gridreference}" }, // {playerPosition}
                    { "overwater", "That refernce point is above water." },
                    { "cmdusage", "usage ex: /grt n10  (where n = a-zz and 10=0-60" },
                    { "noaccess", "You do not have sufficient access to execute this command." },
                    { "sgerror", "Error creating spawnpoints, too much water? contact dev." },
                    { "cooldown", "Sorry, your are currently in a {cooldownperiod} second cooldown, you have another {secondsleft} seconds remaining." },
                    { "cooldownreply", "Cooldown has been set to {cooldownperiod} seconds" },
                    { "gridwidthreply", "Gridwidth has been set to {gridwidth}x{gridwidth}" },
                    { "cuboardreply", "Cupboard distance has been set to {distance}" },
                    { "avoidwaterreplay", "Avoid water has been set tp {avoidwater}" },
                    { "cupboard", "Sorry, you cannot teleport within {distance}f of a cupboard." }
                }, this, "en");

                PrintWarning("Loaded " + lang.GetMessages("en", this).Count + " mesages registerd.");
            }
            catch (Exception ex)
            {
                PrintToConsole($"Loaded: {ex.Message}");
            }
        }

        protected override void LoadDefaultConfig()
        {
            var data = new GrTeleportData
            {
                CooldownInSeconds = 30,
                CupboardDistance = 50,
                AvoidWater = true,
                GridWidth = 0
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
                if (!CheckAccess(player, command, grtPermission)) return;

                if (grTeleportData.GridWidth <= 0)
                {
                    PrintToChat(player, lang.GetMessage("nosquares", this, player.UserIDString));
                    PrintError(lang.GetMessage("nosquares", this));
                    return;
                }

                var tmp = GetCooldown(player.displayName);
                if (tmp != null)
                {
                    PrintToChat(player, lang.GetMessage("cooldown", this, player.UserIDString).Replace("{cooldownperiod}", tmp.cooldownPeriodSeconds.ToString()).Replace("{secondsleft}", tmp.expirtyDateTime.Subtract(DateTime.Now).TotalSeconds.ToString("0")));
                    return;
                }

                if (spawnGrid == null)
                {
                    spawnGrid = CreateSpawnGrid();
                    if (spawnGrid == null)
                        throw new Exception("spawnGrid=null!");

                }

                if (args.Length > 0)
                {
                    var gr = args[0];
                    var index = GridIndexFromReference(gr);

                    if (spawnGrid[index].aboveWater && grTeleportData.AvoidWater)
                    {
                        PrintToChat(player, lang.GetMessage("overwater", this, player.UserIDString));
                        return;
                    }

                    if (AreThereCupboardsWithinDistance(spawnGrid[index].Position, grTeleportData.CupboardDistance))
                    {
                        PrintToChat(player, lang.GetMessage("cupboard", this, player.UserIDString).Replace("{distance}", grTeleportData.CupboardDistance.ToString()));
                        return;
                    }

                    else
                    {
                        if (TeleportToGridReference(player, gr, grTeleportData.AvoidWater))
                        {
                            PrintToChat(player, lang.GetMessage("teleported", this, player.UserIDString).Replace("{playerPosition}", player.transform.position.ToString()).Replace("{gridreference}", gr.ToUpper()));
                            AddToCoolDown(player.displayName, grTeleportData.CooldownInSeconds);
                            return;
                        }
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
            if (arg.Player() == null) return;

            BasePlayer player = arg.Player();

            if (spawnGrid.Count <= 0)
                throw new Exception(lang.GetMessage("noinit", this, player.UserIDString));

            if (!CheckAccess(player, "grt.nextspawn", adminPermission)) return;

            while (spawnGrid[++lastGridTested].aboveWater)
                if (lastGridTested > 1000) // endless loop               
                    throw new Exception(lang.GetMessage("sgerror", this, player.UserIDString));

            Teleport(player, spawnGrid[lastGridTested].GroundPosition, false);

            PrintToChat(player, lang.GetMessage("teleported", this, player.UserIDString).Replace("{playerPosition}", player.transform.position.ToString()));
            
        }

        [ChatCommand("setcooldown")]
        void chmSetCooldown(BasePlayer player, string command, string[] args)
        {
            if (!CheckAccess(player, "setcooldown", adminPermission)) return;
            if (args.Length > 0)
                grTeleportData.CooldownInSeconds = int.Parse(args[0]);
            Config.WriteObject(grTeleportData, true);
            PrintToChat(player, lang.GetMessage("cooldownreply", this, player.UserIDString).Replace("{cooldownperiod}", grTeleportData.CooldownInSeconds.ToString()));
        }

        [ChatCommand("setgridwidth")]
        void chmSetGridWidth(BasePlayer player, string command, string[] args)
        {
            if (!CheckAccess(player, "setgridwidth", adminPermission)) return;
            if (args.Length > 0)
                grTeleportData.GridWidth = int.Parse(args[0]);
            Config.WriteObject(grTeleportData, true);
            CreateSpawnGrid();
            PrintToChat(player, lang.GetMessage("gridwidthreply", this, player.UserIDString).Replace("{gridwidth}", grTeleportData.GridWidth.ToString()));
        }

        [ChatCommand("setcupboard")]
        void chmSetCupboard(BasePlayer player, string command, string[] args)
        {
            if (!CheckAccess(player, "setcupboard", adminPermission)) return;
            if (args.Length > 0)
                grTeleportData.CupboardDistance = int.Parse(args[0]);
            Config.WriteObject(grTeleportData, true);
            PrintToChat(player, lang.GetMessage("cuboardreply", this, player.UserIDString).Replace("{distance}", grTeleportData.CupboardDistance.ToString()));
        }

        [ChatCommand("avoidwater")]
        void chmSetAvoidWater(BasePlayer player, string command, string[] args)
        {
            if (!CheckAccess(player, "avoidwater", adminPermission)) return;
            if (args.Length > 0)
                grTeleportData.AvoidWater = bool.Parse(args[0]);
            Config.WriteObject(grTeleportData, true);
            PrintToChat(player, lang.GetMessage("avoidwaterreplay", this, player.UserIDString).Replace("{avoidwater}", grTeleportData.AvoidWater.ToString()));
        }
        #endregion

        #region API
        [HookMethod("TeleportToGridReference")]
        private bool TeleportToGridReference(BasePlayer player, string gridReference, bool avoidWater = true)
        {
            var index = GridIndexFromReference(gridReference);
            if (avoidWater && spawnGrid[index].aboveWater) return false;
            Teleport(player, spawnGrid[index].GroundPosition);
            return true;
        }

        [HookMethod("IsGridReferenceAboveWater")]
        private bool IsGridReferenceAboveWater(string gridReference)
        {
            var index = GridIndexFromReference(gridReference);
            return spawnGrid[index].aboveWater;
        }
        #endregion

        #region supporting fuctions

        int GridIndexFromReference(string gridReference)
        {
            var tmp = ExtractGridReference(gridReference);
            return ((int)tmp.x * grTeleportData.GridWidth + (int)tmp.y);
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
            if (grTeleportData.GridWidth <= 0)
            {
                PrintError("You must set TotalSquares"); //TODO: fix
                return null;
            }

            List<SpawnPosition> retval = new List<SpawnPosition>();

            var worldSize = (ConVar.Server.worldsize);
            var offset = worldSize / 2;

            for (int zz = offset; zz > -offset; zz -= (worldSize / grTeleportData.GridWidth))
                for (int xx = -offset; xx < offset; xx += (worldSize / grTeleportData.GridWidth))
                    retval.Add(new SpawnPosition(new Vector3(xx, 0, zz)));

            return retval;
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
            if (!permission.UserHasPermission(player.UserIDString, sPermission))
            {
                if (onErrorDisplayMessageToUser)
                    PrintToChat(player, lang.GetMessage("noaccess", this, player.UserIDString));

                return false;
            }
            return true;
        }

        void AddToCoolDown(string userName, int seconds)
        {
            coolDowns.Add(new Cooldown(userName.ToLower(), seconds));
        }

        Vector2 ExtractGridReference(string GridReference)
        {
            var letter = "";
            var numeric = "";

            foreach (char c in GridReference.ToLower().ToCharArray())
            {
                if ((c >= 97) && (c <= 122)) letter += c;
                if ((c >= 48) && (c <= 57)) numeric += c;
            }

            int parta = (letter.Length > 1) ? (26 + (int)(letter.ToCharArray()[1] - 97)) : (int)(letter.ToCharArray()[0] - 97);
            int partb = int.Parse(numeric);
            return new Vector2(parta, partb); 
        }

        bool AreThereCupboardsWithinDistance(Vector3 position, int distance)
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
        }


        #endregion

    }
}