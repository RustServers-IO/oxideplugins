using System.Collections.Generic;
using UnityEngine;
using System;
using Oxide.Core;
using System.Linq;
using System.Collections;

namespace Oxide.Plugins
{
    #region Changelog
    /*
    Fixed:
        * For last hurtworld patch (3.6.4)
    Added:
        * 
    Removed:
        * 
    Changed:
        * 
    */
    #endregion Changelog
    [Info("CustomWipe", "SouZa", "1.0.3", ResourceId = 1977)]
    [Description("Wipe a server with custom options.")]
    class CustomWipe : HurtworldPlugin
    {
        #region Variables
        List<Cell> safeCells;
        List<ulong> safeInv;
        #endregion Variables
        
        #region Classes
        public class Cell
        {
            public long cellid { get; set; }
            public string pos { get; set; }
            public string description { get; set; }
        }
        #endregion Classes

        #region Methods
        void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"cmds","<color=yellow>CustomWipe Commands: </color>"},
                {"cmds_safecell","<color=orange>/safecell [description]</color> - Add new safecell."},
                {"cmds_removecell","<color=orange>/removecell</color> - Removes a safecell."},
                {"cmds_list","<color=orange>/listcell | listinv</color> - Lists all safecells | safe inventories."},
                {"cmds_safehome", "<color=orange>/safehome <playername|id></color> - Saves player homes." },
                {"cmds_safeinv", "<color=orange>/safeinv <playername|id></color> - Saves player inventory." },
                {"cmds_tpcell","<color=orange>/tpcell <index></color> - Teleports to a safecell."},
                {"cmds_wipeinv","<color=orange>/wipeinv</color> - Wipe all players inventory."},
                {"cmds_wipeobj","<color=orange>/wipeobj</color> - Wipe all server objects (except safecells)."},
                {"nopermission","You dont have Permission to do this!"},
                {"destroyed","Destroyed {Count} objects"},
                {"safecell","Cell <color=orange>{cell}</color> is now safe."},
                {"safecell_safe","Cell <color=orange>{cell}</color> is already safe."},
                {"safeinv_safe", "Inventory from {playername} is now safe." },
                {"safeinv_remove", "Removed {playername} inventory from safe inventories." },
                {"safehome_safe", "{count} homes from {playername} are now safe." },
                {"safehome_notFound", "Not found any home for {playername}." },
                {"removecell","Removed cell <color=orange>{cell}</color> from safe cells."},
                {"removecell_removed","Cell <color=orange>{cell}</color> is not a safe cell."},
                {"celllist", "<color=yellow>SafeCell List</color>" },
                {"celllist_format", "<color=yellow>{index}</color> - {cellid} - {description}"},
                {"celllist_empty", "Empty."},
                {"invlist", "<color=yellow>SafeInventory List</color>" },
                {"invlist_format", "<color=yellow>{index}</color> - {playerid} - {playerName}"},
                {"invlist_empty", "Empty."},
                {"tpcell", "Type /tpcell <index> to teleport to that cell." },
                {"defaultdescription", "No Description" }
            };
            lang.RegisterMessages(messages, this);
        }
        string Msg(string msg, string SteamId = null) => lang.GetMessage(msg, this, SteamId);

        void LoadData()
        {
            safeCells = Interface.GetMod().DataFileSystem.ReadObject<List<Cell>>("CustomWipe/SafeCells");
            safeInv = Interface.GetMod().DataFileSystem.ReadObject<List<ulong>>("CustomWipe/SafeInv");
        }
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("CustomWipe/SafeCells", safeCells);
            Interface.GetMod().DataFileSystem.WriteObject("CustomWipe/SafeInv", safeInv);
        }

        void Loaded()
        {
            permission.RegisterPermission("customwipe.use", this);
            LoadDefaultMessages();
            LoadData();
        }

        #region Helpers
        private bool objectNameCheck(string objectName)
        {
            if (!objectName.Contains("Desert") && !objectName.Contains("Cliff") && !objectName.Contains("Player") && !objectName.Contains("Cam"))
            {
                if (objectName.Contains("(Clone)"))
                {
                    return true;
                }
            }
            return false;
        }

        private void ForcePlayerRemoval(PlayerSession session)
        {
            // save offline data
            session.Identity.WriteFromEntity();

            // GameManager.RemovePlayerWorldEntity(playerSession) effective code
            Singleton<HNetworkManager>.Instance.FinalDestroyPlayerObjects(session.Player);
            Singleton<GameManager>.Instance.OnPlayerDisconnected(session);
            Singleton<HNetworkManager>.Instance.NetDestroy(session.WorldPlayerEntity.uLinkNetworkView());
            session.WorldPlayerEntity = null;
            session.Identity.ConnectedSession = null;
        }

        IEnumerator wipeIdentities()
        {
            List<PlayerIdentity> newIdentities = new List<PlayerIdentity>();
            foreach (PlayerIdentity identity in GameManager.Instance.GetIdentifierMap().Values)
            {
                while (identity.ConnectedSession != null)
                {
                    GameManager.Instance.KickPlayer(identity.ConnectedSession, "Server Wipe");
                    ForcePlayerRemoval(identity.ConnectedSession);
                    yield return new WaitForSeconds(0.5f);
                }

                if (!safeInv.Contains(identity.SteamId.m_SteamID))
                {
                    PlayerIdentity newIdentity = new PlayerIdentity()
                    {
                        SteamId = new Steamworks.CSteamID(identity.SteamId.m_SteamID),
                        Name = identity.Name,
                        PlayerStateByes = null,
                        PlayerStateString = String.Empty
                    };

                    newIdentities.Add(newIdentity);
                }
            }

            newIdentities.ForEach(newIdentity => GameManager.Instance.OverwriteIdentity(newIdentity.SteamId, newIdentity));

            PrintWarning("All player wiped. Saving now.");
            SaveServer();
        }

        void SaveServer()
        {
            GameManager.Instance.StartCoroutine(Singleton<GameSerializer>.Instance.SaveServer(string.Concat("autosave_", Singleton<GameManager>.Instance.ServerConfig.Map), true));
        }

        public Vector3 StringToVector3(string v3)
        {
            var split = v3.Split(' ').Select(Convert.ToSingle).ToArray();
            return split.Length == 3 ? new Vector3(split[0], split[1], split[2]) : Vector3.zero;
        }

        public string Vector3ToString(Vector3 v3, int decimals = 2, string separator = " ")
        {
            return
                $"{Math.Round(v3.x, decimals)}{separator}{Math.Round(v3.y, decimals)}{separator}{Math.Round(v3.z, decimals)}";
        }

        public PlayerSession FindSession(string nameOrIdOrIp)
        {
            var sessions = GameManager.Instance.GetSessions();
            PlayerSession session = null;
            foreach (var i in sessions)
            {
                if (!nameOrIdOrIp.Equals(i.Value.Name, StringComparison.OrdinalIgnoreCase) &&
                    !nameOrIdOrIp.Equals(i.Value.SteamId.ToString()) && !nameOrIdOrIp.Equals(i.Key.ipAddress)) continue;
                session = i.Value;
                break;
            }
            return session;
        }

        #endregion Helpers
        #endregion Methods

        #region ChatCommands
        [ChatCommand("customwipe")]
        void cmdcustomwipe(PlayerSession session, string command, string[] args)
        {
            if (permission.UserHasPermission(session.SteamId.ToString(), "customwipe.use") || session.IsAdmin)
            {
                hurt.SendChatMessage(session, Msg("cmds", session.SteamId.ToString()));

                hurt.SendChatMessage(session, Msg("cmds_safecell", session.SteamId.ToString()));
                hurt.SendChatMessage(session, Msg("cmds_removecell", session.SteamId.ToString()));
                hurt.SendChatMessage(session, Msg("cmds_list", session.SteamId.ToString()));
                hurt.SendChatMessage(session, Msg("cmds_safehome", session.SteamId.ToString()));
                hurt.SendChatMessage(session, Msg("cmds_safeinv", session.SteamId.ToString()));
                hurt.SendChatMessage(session, Msg("cmds_tpcell", session.SteamId.ToString()));
                hurt.SendChatMessage(session, Msg("cmds_wipeinv", session.SteamId.ToString()));
                hurt.SendChatMessage(session, Msg("cmds_wipeobj", session.SteamId.ToString()));
            }
            else
                hurt.SendChatMessage(session, Msg("nopermission", session.SteamId.ToString()));
        }
        [ChatCommand("tpcell")]
        void cmdtpcell(PlayerSession session, string command, string[] args)
        {
            if (permission.UserHasPermission(session.SteamId.ToString(), "customwipe.use") || session.IsAdmin)
            {
                if(args.Length == 1)
                {
                    Vector3 pos = StringToVector3(safeCells[int.Parse(args[0])].pos);
                    session.WorldPlayerEntity.transform.position = pos;
                }
            }
            else
                hurt.SendChatMessage(session, Msg("nopermission", session.SteamId.ToString()));
        }
        [ChatCommand("listcell")]
        void cmdlistcells(PlayerSession session, string command, string[] args)
        {
            if (permission.UserHasPermission(session.SteamId.ToString(), "customwipe.use") || session.IsAdmin)
            {
                var index = 0;
                var steamid = session.SteamId.ToString();
                hurt.SendChatMessage(session, Msg("celllist", session.SteamId.ToString()));
                foreach(Cell cell in safeCells)
                {
                    hurt.SendChatMessage(session, Msg("celllist_format", steamid).Replace("{index}", index + "").Replace("{cellid}", cell.cellid+"").Replace("{description}", cell.description));
                    index++;
                }
                if(index == 0)
                    hurt.SendChatMessage(session, Msg("celllist_empty", session.SteamId.ToString()));
                else
                    hurt.SendChatMessage(session, Msg("tpcell", session.SteamId.ToString()));
            }
            else
                hurt.SendChatMessage(session, Msg("nopermission", session.SteamId.ToString()));
        }
        [ChatCommand("listinv")]
        void cmdlistinv(PlayerSession session, string command, string[] args)
        {
            if (permission.UserHasPermission(session.SteamId.ToString(), "customwipe.use") || session.IsAdmin)
            {
                var steamid = session.SteamId.ToString();
                var index = 0;
                hurt.SendChatMessage(session, Msg("invlist", steamid));
                foreach (ulong playerid in safeInv)
                {
                    var playername = GameManager.Instance.GetIdentity(playerid)?.Name;
                    if (playername == null)
                        playername = "NoName";
                    hurt.SendChatMessage(session, Msg("invlist_format", steamid).Replace("{index}", index + "").Replace("{playerid}", playerid + "").Replace("{playerName}", playername));
                    index++;
                }
                if (index == 0)
                    hurt.SendChatMessage(session, Msg("invlist_empty", session.SteamId.ToString()));
            }
            else
                hurt.SendChatMessage(session, Msg("nopermission", session.SteamId.ToString()));
        }
        [ChatCommand("removecell")]
        void cmdremovecell(PlayerSession session, string command, string[] args)
        {
            if (permission.UserHasPermission(session.SteamId.ToString(), "customwipe.use") || session.IsAdmin)
            {
                var cell = ConstructionUtilities.GetOwnershipCell(session.WorldPlayerEntity.transform.position);
                if (safeCells.Any(c => c.cellid == cell))
                {
                    Cell cellToRemove = safeCells.Find(c => c.cellid == cell);
                    safeCells.Remove(cellToRemove);
                    SaveData();
                    hurt.SendChatMessage(session, Msg("removecell", session.SteamId.ToString()).Replace("{cell}", cell + ""));
                }
                else
                    hurt.SendChatMessage(session, Msg("removecell_removed", session.SteamId.ToString()).Replace("{cell}", cell + ""));
            }
            else
                hurt.SendChatMessage(session, Msg("nopermission", session.SteamId.ToString()));
        }
        
        [ChatCommand("safeinv")]
        void cmdsafeinv(PlayerSession session, string command, string[] args)
        {
            if (permission.UserHasPermission(session.SteamId.ToString(), "customwipe.use") || session.IsAdmin)
            {
                if(args.Length == 1)
                {
                    foreach(PlayerIdentity identity in GameManager.Instance.GetIdentifierMap().Values)
                    {
                        if( identity.Name.ToLower().Equals(args[0].ToLower()) ||
                            identity.SteamId.ToString().Equals(args[0]))
                        {
                            var playerid = identity.SteamId.m_SteamID;
                            if (!safeInv.Contains(playerid))
                            {
                                safeInv.Add(playerid);
                                hurt.SendChatMessage(session, Msg("safeinv_safe", session.SteamId.ToString()).Replace("{playername}", identity.Name));
                            }
                            else
                            {
                                safeInv.Remove(playerid);
                                hurt.SendChatMessage(session, Msg("safeinv_remove", session.SteamId.ToString()).Replace("{playername}", identity.Name));
                            }
                            SaveData();
                            return;
                        }
                    }
                }
            }
            else
                hurt.SendChatMessage(session, Msg("nopermission", session.SteamId.ToString()));
        }
        [ChatCommand("safehome")]
        void cmdsafehome(PlayerSession session, string command, string[] args)
        {
            if (permission.UserHasPermission(session.SteamId.ToString(), "customwipe.use") || session.IsAdmin)
            {
                if (args.Length == 1)
                {
                    bool found = false;
                    foreach (PlayerIdentity identity in GameManager.Instance.GetIdentifierMap().Values)
                    {
                        if (identity.Name.ToLower().Equals(args[0].ToLower()) ||
                            identity.SteamId.ToString().Equals(args[0]))
                        {
                            found = true;
                            var count = 0;
                            var enumerator = RefTrackedBehavior<OwnershipStakeServer>.GetEnumerator();
                            while (enumerator.MoveNext())
                            {
                                count++;
                                var stake = enumerator.Current.Value;
                                if (stake.AuthorizedPlayers.Contains(identity))
                                {
                                    var cell = ConstructionUtilities.GetOwnershipCell(stake.transform.position);
                                    var desc = "Player Home - " + identity.Name;
                                    safeCells.Add(new Cell { cellid = cell, pos = Vector3ToString(stake.transform.position), description = desc });
                                }
                            }
                            SaveData();
                            hurt.SendChatMessage(session, Msg("safehome_safe", session.SteamId.ToString()).Replace("{playername}", identity.Name).Replace("{count}", count+""));
                            return;
                        }
                    }
                    if(!found)
                        hurt.SendChatMessage(session, Msg("safehome_notFound", session.SteamId.ToString()).Replace("{playername}", args[0]));
                }
            }
            else
                hurt.SendChatMessage(session, Msg("nopermission", session.SteamId.ToString()));
        }

        [ChatCommand("safecell")]
        void cmdsafecell(PlayerSession session, string command, string[] args)
        {
            if (permission.UserHasPermission(session.SteamId.ToString(), "customwipe.use") || session.IsAdmin)
            {
                var cell = ConstructionUtilities.GetOwnershipCell(session.WorldPlayerEntity.transform.position);
                var desc = args.Length >= 1 ? string.Join(" ", args) : Msg("defaultdescription", session.SteamId.ToString());
                
                if (!safeCells.Any(c => c.cellid == cell))
                {
                    safeCells.Add(new Cell { cellid = cell, pos = Vector3ToString(session.WorldPlayerEntity.transform.position), description = desc });
                    SaveData();
                    hurt.SendChatMessage(session, Msg("safecell", session.SteamId.ToString()).Replace("{cell}", cell+""));
                }
                else
                    hurt.SendChatMessage(session, Msg("safecell_safe", session.SteamId.ToString()).Replace("{cell}", cell + ""));
            }
            else
                hurt.SendChatMessage(session, Msg("nopermission", session.SteamId.ToString()));
        }
        
        [ChatCommand("wipeinv")]
        void cmdwipeinv(PlayerSession session, string command, string[] args)
        {
            if (permission.UserHasPermission(session.SteamId.ToString(), "customwipe.use") || session.IsAdmin)
            {
                GameManager.Instance.StartCoroutine(wipeIdentities());
            }
            else
                hurt.SendChatMessage(session, Msg("nopermission", session.SteamId.ToString()));
        }
        
        [ChatCommand("wipeobj")]
        void cmdwipe(PlayerSession session, string command, string[] args)
        {
			if(permission.UserHasPermission(session.SteamId.ToString(),"customwipe.use") || session.IsAdmin)
			{
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                List<GameObject> removedObjects = new List<GameObject>();
                var count = 0;
				foreach (GameObject objects in allObjects)
				{
					if(objectNameCheck(objects.name))
					{
                        var cell = ConstructionUtilities.GetOwnershipCell(objects.transform.position);
                        if (!safeCells.Any(c => c.cellid == cell))
                        {
                            GameObject root = objects.transform.root.gameObject;
                            uLink.NetworkView rootNV = root.GetComponent<uLink.NetworkView>();
                            if(rootNV != null)
                            {
                                if (!removedObjects.Contains(root))
                                {
                                    removedObjects.Add(root);
                                    count++;
                                    uLink.Network.Destroy(rootNV);
                                }
                            }
                        }
					}
				}
                hurt.SendChatMessage(session, Msg("destroyed",session.SteamId.ToString()).Replace("{Count}",count.ToString()));
                PrintWarning("All server objects (except safe cells) wiped. Saving now.");
                SaveServer();
            }
			else
				hurt.SendChatMessage(session, Msg("nopermission",session.SteamId.ToString()));
        }
        #endregion ChatCommands
    }
}