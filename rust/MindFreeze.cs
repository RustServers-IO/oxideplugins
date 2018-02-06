using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Mind Freeze", "PaiN", "1.1.1", ResourceId = 1198)]
    [Description("Allows you to freeze players with a legit way.")]
    class MindFreeze : RustPlugin
    {
        private Timer _timer;

        private class FrozenPlayerInfo
        {
            public BasePlayer Player { get; set; }
            public Vector3 FrozenPosition { get; set; }

            public FrozenPlayerInfo(BasePlayer player)
            {
                Player = player;
                FrozenPosition = player.transform.position;
            }
        }

        List<FrozenPlayerInfo> frozenPlayers = new List<FrozenPlayerInfo>();

        void Loaded()
        {
            permission.RegisterPermission(this.Name.ToLower() + ".admin", this);
            _timer = timer.Every(1, OnTimer);
        }

        [ChatCommand("freeze")]
        void cmdFreeze(BasePlayer player, string cmd, string[] args)
        {

            if (!permission.UserHasPermission(player.UserIDString, this.Name.ToLower() + ".admin"))
            {
                SendReply(player, "No Permission!");
                return;
            }

            if (args.Length == 1)
            {
                if (args[0].ToLower() == "all")
                {
                    foreach (BasePlayer current in BasePlayer.activePlayerList)
                    {
                        if (!frozenPlayers.Any(x => x.Player == current))
                        {
                            frozenPlayers.Add(new FrozenPlayerInfo(current));
                            SendReply(current, "You have been frozen by " + player.displayName);
                        }
                    }
                    SendReply(player, "You froze every player");
                    PrintToChat(player.displayName + " froze every player");
                }
                else
                {

                    var target = BasePlayer.Find(args[0]);
                    if (!target)
                    {
                        SendReply(player, "Player not found!");
                        return;
                    }
                    if (target == null) return;
                    if (frozenPlayers.Any(t => t.Player == target))
                    {
                        SendReply(player, "This player is already frozen");
                        return;
                    }
                    frozenPlayers.Add(new FrozenPlayerInfo(target));
                    SendReply(target, "You have been frozen by " + player.displayName);
                    SendReply(player, "You have frozen " + target.displayName);
                }
            }
            else
            {
                SendReply(player, "Syntax: /freeze <player/all? ");
                return;
            }
        }



        [ChatCommand("unfreeze")]
        void cmdUnFreeze(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, this.Name.ToLower() + ".admin"))
            {
                SendReply(player, "No Permission!");
                return;
            }
            
            if (args.Length == 1)
            {
                if (args[0].ToLower() == "all")
                {
                    frozenPlayers.Clear();
                    SendReply(player, "You have unfrozen every frozen player.");
                    PrintToChat(player.displayName + " unfroze every player");
                    foreach (FrozenPlayerInfo frozenp in frozenPlayers)
                        frozenp.Player.ChatMessage("You got unfrozen by " + player.displayName);

                }
                else
                {
                    var target = BasePlayer.Find(args[0]);

                    if (!target)
                    {
                        SendReply(player, "Player not found!");
                        return;
                    }

                    if (target == null) return;
                    frozenPlayers.RemoveAll(t => t.Player == target);
                    SendReply(target, "You have been unfrozen by " + player.displayName);
                    SendReply(player, "You have unfrozen " + target.displayName);
                }
            }
            else
            {
                SendReply(player, "Syntax: /unfreeze <player/all> ");
                return;
            }
        }

        void OnTimer()
        {
            foreach (FrozenPlayerInfo current in frozenPlayers)
            {
                if (Vector3.Distance(current.Player.transform.position, current.FrozenPosition) < 1) continue;
                current.Player.MovePosition(current.FrozenPosition);
                current.Player.ClientRPCPlayer(null, current.Player, "ForcePositionTo", current.FrozenPosition, null, null, null, null);
            }
        }

        void Unloaded()
        {
            _timer.Destroy();
            frozenPlayers.Clear();
        }
    }
}