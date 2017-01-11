// Reference: Assembly-CSharp-firstpass

using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Steamworks;

namespace Oxide.Plugins
{
    [Info("Prod", "Reneb", "1.0.0")]
    class Prod : HideHoldOutPlugin
    {
        [PluginReference]
        Plugin PlayerDatabase;

        private static readonly FieldInfo ChatNetViewField = typeof(ChatManager).GetField("Chat_NetView", BindingFlags.NonPublic | BindingFlags.Instance);
        public static uLink.NetworkView ChatNetView = ChatNetViewField.GetValue(NetworkController.NetManager_.chatManager) as uLink.NetworkView;

        int ConstructionMask = LayerMask.GetMask(new string[] { "Construction", "Construction_Snap", "Decors", "Wildlife", "Ressource", "Electric", "Ladder", "Damage_Receiver"});


        void Loaded()
        {
            permission.RegisterPermission("prod.view", this);
        }

        void OnServerInitialized()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Owner steamid is: {0}.", "Owner steamid is: {0}."},
                {"You are not allowed to use this command.","You are not allowed to use this command."},
                {"Couldn't find anything in front of you.", "Couldn't find anything in front of you."},
                {"Offline", "Offline"},
                {"Online", "Online"},
                {"Owner is part of: {0} ({1} members)", "Owner is part of: {0} ({1} members)"},
                {"Couldn't get the list of players", "Couldn't get the list of players"},
                {"Owner is currently: {0}.", "Owner is currently: {0}."},
                {"Owner name is: {0}.","Owner name is: {0}."},
                { "You are looking at: {0}.","You are looking at: {0}."}
            }, this);
        }


        string GetMsg(string key, object steamid = null)
        {
            return lang.GetMessage(key, this, steamid.ToString());
        }

        void SendReply(PlayerInfos player, string msg)
        {
            if (player.NetPlayer != null)
                ChatNetView.RPC("NET_Receive_msg", player.NetPlayer, new object[] { "\r\n" + msg, chat_msg_type.standard, player.account_id });
        }
        bool hasPermission(PlayerInfos player)
        {
            if (player.isADMIN) return true;
            return permission.UserHasPermission(player.account_id, "prod.view");
        }


        Collider FindObjectCollider(Vector3 sourcePos, Vector3 sourceRot, float dist = 100f, int layer = -1)
        {
            RaycastHit rayhit;
            bool flag = Physics.Raycast(sourcePos, sourceRot, out rayhit, dist, layer);
            if (!flag) return null;
            return rayhit.collider;
        }
        
        [ChatCommand("prod")]
        private void cmdChatProd(PlayerInfos id, string command, string[] args)
        {
            if(!hasPermission(id)) { SendReply(id, GetMsg("You are not allowed to use this command.",id.account_id)); return; }

            var col = FindObjectCollider(id.PManager.Cam_Ref_T.position + new Vector3(0f,1f,0f), id.PManager.Cam_Ref_T.rotation * Vector3.forward, 10000f, ConstructionMask);
            if(col == null)
            {
                SendReply(id, GetMsg("Couldn't find anything in front of you.", id.account_id));
                return;
            }
            SendReply(id, string.Format(GetMsg("You are looking at: {0}.",id.account_id),col.gameObject.name));
            string ownerid = string.Empty;
            try
            {
                ownerid = col.GetComponent<Damage_Receiver>().DamageHandler.Construction_Ref.owner_id;
            }
            catch(Exception e) { }

            if(ownerid != string.Empty)
            {
                SendReply(id, string.Format(GetMsg("Owner steamid is: {0}.", id.account_id), id.account_id));
                string name = "Unknown";
                string status = GetMsg("Offline", ownerid);
                int clan = -1;
                PlayerInfos player = NetworkController.NetManager_.ServManager.GetPlayerInfos_accountID(ownerid);
                if (player != null)
                {
                    name = player.Nickname;
                    if (player.connected)
                        status = GetMsg("Online", ownerid);
                    if (player.clan != -1)
                    {
                        clan = player.clan;
                        ClanInfos infos = NetworkController.NetManager_.ServManager.Clans[clan];
                        int clanCount = 0;
                        foreach(string member in infos.Members)
                        {
                            if (member != string.Empty)
                                clanCount++;
                        }
                        SendReply(id, string.Format(GetMsg("Owner is part of: {0} ({1} members)", id.account_id), infos.Acronym, clanCount.ToString()));
                    }
                }
                if (PlayerDatabase && name == "Unknown")
                {
                    var playerdata = PlayerDatabase.Call("GetPlayerData", ownerid.ToString(), "default");
                    if (playerdata is Dictionary<string, object>)
                        name = ((Dictionary<string, object>)playerdata).ContainsKey("name") ? ((Dictionary<string, object>)playerdata)["name"].ToString() : "Unknown";
                }
                SendReply(id, string.Format(GetMsg("Owner name is: {0}.", name), id.account_id));
                SendReply(id, string.Format(GetMsg("Owner is currently: {0}.", id.account_id),status));
            }
        }
    }
}