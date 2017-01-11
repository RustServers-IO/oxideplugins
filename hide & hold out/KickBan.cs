using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("KickBan", "k1lly0u", "0.1.2", ResourceId = 1867)]
    public class KickBan : HideHoldOutPlugin
    {
        ServerManager sm = GameObject.Find("ServerManager").GetComponent<ServerManager>();
        NetworkController nc = UnityEngine.Object.FindObjectOfType<NetworkController>();

        private static readonly FieldInfo ChatNetViewField = typeof(ChatManager).GetField("Chat_NetView", BindingFlags.NonPublic | BindingFlags.Instance);
        public static uLink.NetworkView ChatNetView = ChatNetViewField.GetValue(NetworkController.NetManager_.chatManager) as uLink.NetworkView;

        public Dictionary<string, string> PlayerIPs;
        void Loaded()
        {
            lang.RegisterMessages(messages, this);
            permission.RegisterPermission("kickban.admin", this);
            PlayerIPs = new Dictionary<string, string>();
        }   
        void OnServerInitialized()
        {
            foreach (var player in sm.Connected_Players)
                OnPlayerConnected(player);
        }     
        private void OnPlayerConnected(PlayerInfos player)
        {
            if (player != null && player.connected)
            {
                if (!PlayerIPs.ContainsKey(player.NetPlayer.ipAddress))
                    PlayerIPs.Add(player.NetPlayer.ipAddress, player.Nickname);
            }
        }
        private void OnPlayerDisconnected(PlayerInfos player)
        {
            if (player != null)
            {
                if (PlayerIPs.ContainsKey(player.NetPlayer.ipAddress))
                    PlayerIPs.Remove(player.NetPlayer.ipAddress);
            }
        }
        private void BanPlayer(string name)
        {
            if (uLink.Network.isServer)
            {                
                PlayerInfos playerInfo = sm.FindPlayer(name);
                if (!playerInfo.isDefined || !playerInfo.connected)
                {
                    if (PlayerIPs.ContainsKey(name))
                        playerInfo = sm.FindPlayer(PlayerIPs[name]);
                }
                if (playerInfo.isDefined && playerInfo.connected)
                {
                    nc.chatManager.Send_msg(string.Format(lang.GetMessage("banned", this), playerInfo.Nickname));
                    if (!nc.DBManager.DB_Check_BAN(playerInfo.account_id))
                    {
                        nc.DBManager.DB_AddBan(playerInfo.account_id);
                    }
                    uLink.Network.CloseConnection(playerInfo.NetPlayer, true);
                }
            }
        }
        private void KickPlayer(string name)
        {
            if (uLink.Network.isServer)
            {
                PlayerInfos playerInfo = sm.FindPlayer(name);
                if (!playerInfo.isDefined || !playerInfo.connected)
                {
                    if (PlayerIPs.ContainsKey(name))
                        playerInfo = sm.FindPlayer(PlayerIPs[name]);
                }
                if (playerInfo.isDefined && playerInfo.connected)
                {
                    nc.chatManager.Send_msg(string.Format(lang.GetMessage("kicked", this), playerInfo.Nickname));
                    uLink.Network.CloseConnection(playerInfo.NetPlayer, true);
                }
            }
        }
        [ChatCommand("kick")]
        private void cmdKick(PlayerInfos player, string command, string[] args)
        {
            if (HasPerm(player))
            {
                if (args == null || args.Length == 0)
                {
                    SendReply(player, lang.GetMessage("kickSyn", this, player.account_id));
                    return;
                }
                if (args.Length >= 1)
                    KickPlayer(args[0]);
            }
        }
        [ChatCommand("ban")]
        private void cmdBan(PlayerInfos player, string command, string[] args)
        {
            if (HasPerm(player))
            {
                if (args == null || args.Length == 0)
                {
                    SendReply(player, lang.GetMessage("banSyn", this, player.account_id));
                    return;
                }
                if (args.Length >= 1)
                    BanPlayer(args[0]);
            }
        }      
        private void SendReply(PlayerInfos player, string msg)
        {
            if (player.NetPlayer != null) ChatNetView.RPC("NET_Receive_msg", player.NetPlayer, new object[] { "\r\n" + msg, chat_msg_type.standard, player.account_id });
        }
        private bool HasPerm(PlayerInfos player)
        {
            if (permission.UserHasPermission(player.account_id.ToString(), "kickban.admin")) return true;
            SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("noPerms", this, player.account_id.ToString()));
            return false;
        }
        Dictionary<string, string> messages = new Dictionary<string, string>
        {
            {"title", "<color=#C4FF00>KickBan:</color> " },
            {"noPerms", "You do not have permission to use this command" },
            {"kickSyn", "<color=#C4FF00>/kick <playername></color> - Kicks a player" },
            {"banSyn", "<color=#C4FF00>/ban <playername></color> - Bans a player" },
            {"kicked", "<color=#C4FF00>{0}</color> has been kicked!" },
            {"banned", "<color=#C4FF00>{0}</color> has been banned!" }
        };
    }
}
