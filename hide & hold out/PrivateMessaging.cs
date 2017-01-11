using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Private Messaging", "k1lly0u", "0.1.11", ResourceId = 1868)]
    public class PrivateMessaging : HideHoldOutPlugin
    {
        ServerManager sm = GameObject.Find("ServerManager").GetComponent<ServerManager>();

        private static readonly FieldInfo ChatNetViewField = typeof(ChatManager).GetField("Chat_NetView", BindingFlags.NonPublic | BindingFlags.Instance);
        public static uLink.NetworkView ChatNetView = ChatNetViewField.GetValue(NetworkController.NetManager_.chatManager) as uLink.NetworkView;

        private Dictionary<string, string> MessageHistory = new Dictionary<string, string>();

        void Loaded()
        {           
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"noPlayers", "Could not find any players with the name <color=#C4FF00>{0}</color>" },
                {"multiPlayers", "Found multiple players with the name <color=#C4FF00>{0}</color>" },
                {"pmSyn", "<color=#C4FF00>/pm <playername> Message goes here</color> - Send a PM to <playername>" },
                {"noReply", "<color=#C4FF00>You do not have any recent messages to respond to</color>" }
            }, this);
        }
        void OnPlayerDisconnected(PlayerInfos player)
        {
            if (MessageHistory.ContainsKey(player.account_id))
                MessageHistory.Remove(player.account_id);
        }
        private void SendReply(PlayerInfos player, string msg)
        {
            if (player != null && player.connected)
            ChatNetView.RPC("NET_Receive_msg", player.NetPlayer, new object[] { "\r\n" + msg, chat_msg_type.standard, player.account_id });
        }
        private List<PlayerInfos> GetOnlinePlayers()
        {
            List<PlayerInfos> players = new List<PlayerInfos>();
            foreach (var entry in sm.Connected_Players)
                if (entry != null)
                    if (entry.connected)
                        players.Add(entry);
            return players;
        }
        private List<PlayerInfos> FindPlayers(string name)
        {
            var playerList = GetOnlinePlayers();
            List<PlayerInfos> foundPlayers = new List<PlayerInfos>();
            foreach (var player in playerList)
            {
                if (player.Nickname.ToLower().Contains(name.ToLower()))
                    foundPlayers.Add(player);
                else if (player.account_id == name)
                {
                    foundPlayers.Add(player);
                    break;
                }
            }
            return foundPlayers;
        }
        private void AddToHistory(string playerID, string recieverID)
        {
            if (!MessageHistory.ContainsKey(playerID))
                MessageHistory.Add(playerID, recieverID);
            else MessageHistory[playerID] = recieverID;
        }
        [ChatCommand("pm")]
        void cmdPM(PlayerInfos player, string command, string[] args)
        {
            if (args.Length > 1)
            {
                var foundPlayers = FindPlayers(args[0]);
                if (foundPlayers.Count == 0)
                {
                    SendReply(player, string.Format(lang.GetMessage("noPlayers", this, player.account_id), args[0]));
                    return;
                }
                if (foundPlayers.Count > 1)
                {
                    SendReply(player, string.Format(lang.GetMessage("multiPlayers", this, player.account_id), args[0]));
                    return;
                }

                string reciever = $"<color=#C4FF00>PM from [{player.Nickname}]:</color> ";
                string sender = $"<color=#C4FF00>PM to [{foundPlayers[0].Nickname}]:</color> ";
                string message = "";
                for (int i = 1; i < args.Length; i++)
                    message = $"{message} {args[i]}";
                SendReply(foundPlayers[0], reciever + message);
                SendReply(player, sender + message);
                AddToHistory(player.account_id, foundPlayers[0].account_id);
                AddToHistory(foundPlayers[0].account_id, player.account_id);
                return;
            }
            SendReply(player, lang.GetMessage("pmSyn", this, player.account_id));
        }
        [ChatCommand("r")]
        void cmdReply(PlayerInfos player, string command, string[] args)
        {
            if (args.Length > 0)
            {
                if (MessageHistory.ContainsKey(player.account_id))
                {
                    var reciever = MessageHistory[player.account_id];
                    if (!string.IsNullOrEmpty(reciever))
                    {
                        var foundPlayer = FindPlayers(reciever);
                        if (foundPlayer != null)
                        {
                            string message = $"<color=#C4FF00>PM - [{player.Nickname}]:</color> ";
                            for (int i = 0; i < args.Length; i++)
                                message = $"{message} {args[i]}";
                            SendReply(foundPlayer[0], message);
                            return;
                        }
                    }
                }
                SendReply(player, lang.GetMessage("noReply", this, player.account_id));
            }
        }
        
    }
}
