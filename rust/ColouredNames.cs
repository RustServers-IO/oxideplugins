using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("ColouredNames", "PsychoTea", "1.0.6")]
    internal class ColouredNames : RustPlugin
    {
        class StoredData
        {
            public Dictionary<ulong, string> colour = new Dictionary<ulong, string>();
        }
        StoredData storedData;

        void Init()
        {
            permission.RegisterPermission("colourednames.use", this);
        }

        void Loaded()
        {
            storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("ColouredNames");
        }

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            BasePlayer player = (BasePlayer)arg.connection.player;

            if (!storedData.colour.ContainsKey(player.userID)) return null;
            if (storedData.colour[player.userID] == "clear") return null;

            string message = $"<color={storedData.colour[player.userID]}>{player.displayName}</color>: {arg.GetString(0, "text")}";

            foreach (BasePlayer bp in BasePlayer.activePlayerList)
                rust.SendChatMessage(bp, message, null, player.UserIDString);
            Interface.Oxide.LogInfo("[CHAT] {0}[/{1}] : {2}", player.displayName, player.UserIDString, arg.GetString(0, "text"));
            return true;
        }

        [ChatCommand("colour")]
        void colourCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "colourednames.use"))
            {
                SendReply(player, "You do not have permission to use this!");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "<color=aqua>Incorrect syntax!</color><color=orange> /colour {colour}.\nFor a more information do /colours.</color>");
                return;
            }

            if (args[0] == "clear" || args[0] == "remove")
            {
                storedData.colour.Remove(player.userID);
                Interface.GetMod().DataFileSystem.WriteObject("ColouredNames", storedData);
                SendReply(player, "<color=aqua>ColouredNames: </color><color=orange>Name colour removed!</color>");
                return;
            }

            if (args[0].ToLower().Contains("size"))
            {
                SendReply(player, "You may not try and change your size! You sneaky player...");
                return;
            }

            if (!storedData.colour.ContainsKey(player.userID))
                storedData.colour.Add(player.userID, "");
            storedData.colour[player.userID] = args[0];
            Interface.GetMod().DataFileSystem.WriteObject("ColouredNames", storedData);

            SendReply(player, "<color=aqua>ColouredNames: </color><color=orange>Name colour changed to </color><color={0}>{0}</color><color=orange>!</color>", args[0]);
        }

        [ChatCommand("colours")]
        void coloursCommand(BasePlayer player, string command, string[] args)
        {
            SendReply(player, @"<color=aqua>ColouredNames:</color><color=orange> You may use any colour used in HTML.
                                Eg: ""</color><color=red>red</color><color=orange>"", ""</color><color=blue>blue</color><color=orange>"", ""</color><color=green>green</color><color=orange>"" etc.
                                Or you may use any hexcode, eg ""</color><color=#FFFF00>#FFFF00</color><color=orange>"".
                                To remove your colour, use ""clear"" or ""remove"".
                                An invalid colour will default to </color>white<color=orange>.</color>");
        }
    }
}