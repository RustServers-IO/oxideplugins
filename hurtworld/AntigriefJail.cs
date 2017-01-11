using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    /*
    [B]Changelog 1.1.4[/B]
    [LIST]
    [*] Better check to see if user actually escaped. 
    [*] New command [B]/protectadmins[/B], this command accepts booleans I.E (true/false). If set to true your admins are immune to tickets.
    [*] When an admin issues a ticket now, the user will be sent to jail immediately. 
    [/LIST]
    */

    public static class IntegerExtensions
    {
        public static void Times(this int count, Action action)
        {
            for (int i = 0; i < count; i++)
            {
                action();
            }
        }
    }

    [Info("AntigriefJail", "Pho3niX90", "1.1.4")]
    class AntigriefJail : HurtworldPlugin
    {
        #region Configuration
        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"helpMsg", "When a player crosses that fine line from playing the game to just griefing players you can type /giveticket PlayerName. If the player gets more than {0} tickets in the allotted timeframe then that player is sent to jail for {1} minutes."}
            }, this);
        }
        private string chatServerColor = "#ff0000";
        private string chatServerTitle = "Sheriff->";

        private string jailCoordinates_x = "-3132.176";
        private string jailCoordinates_y = "206.25";
        private string jailCoordinates_z = "-2520.553";

        private int ticketsBeforeJail = 5;
        private int jailLength = 60;
        private Boolean protectAdmins = false;

        private List<IssuedTicket> tickets = new List<IssuedTicket>();

        
        void Loaded()
        {
            LoadMessages();
        }
        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        protected override void LoadDefaultConfig()
        {
            this.SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config["chatServerTitle"] = chatServerTitle;
            Config["chatServerColor"] = chatServerColor;
            Config["ticketsBeforeJail"] = ticketsBeforeJail;
            Config["jailLength"] = jailLength; 

            Config["tickets"] = tickets;

            Config["jailCoordinates_x"] = jailCoordinates_x;
            Config["jailCoordinates_y"] = jailCoordinates_y;
            Config["jailCoordinates_z"] = jailCoordinates_z;
            Config["autoteleportbackdistance"] = 50;
            Config["protectAdmins"] = true;
            base.SaveConfig();
        }
        #endregion

        void Init()
        {
            CheckCfg<string>("chatServerColor", ref chatServerColor);
            CheckCfg<string>("chatServerTitle", ref chatServerTitle);

            CheckCfg<string>("jailCoordinates_x", ref jailCoordinates_x);
            CheckCfg<string>("jailCoordinates_y", ref jailCoordinates_y);
            CheckCfg<string>("jailCoordinates_z", ref jailCoordinates_z);

            CheckCfg<int>("ticketsBeforeJail", ref ticketsBeforeJail);
            CheckCfg<int>("jailLength", ref jailLength);
            CheckCfg<Boolean>("protectAdmins", ref protectAdmins);
            
            CheckCfg<List<IssuedTicket>>("tickets", ref tickets);
        }


        #region Chat Commands

        [ChatCommand("sheriff")]
        void HelpMessage(PlayerSession player, string command, string[] args)
        {
            hurt.SendChatMessage(player, GetMsg("helpMsg", player).Replace("{0}", ticketsBeforeJail.ToString()).Replace("{1}", jailLength.ToString()));
        }


        [ChatCommand("getjailcoords")]
        void GetJailCoords(PlayerSession player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;
            hurt.SendChatMessage(player, string.Format("The Jail is currently located at {0}, {1}, {2}", jailCoordinates_x, jailCoordinates_y, jailCoordinates_z));
        }

        [ChatCommand("gotojail")]
        void TelePortToJail(PlayerSession player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            player.WorldPlayerEntity.transform.position = GetJailCoords();
        }

        [ChatCommand("protectadmins")]
        void CmdProtectAdmins(PlayerSession player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;
            Boolean protect = true;
            Boolean.TryParse(args[0], out protect);
            Config["protectAdmins"] = protect;
            SaveConfig();
            hurt.SendChatMessage(player,
                (protect) ? "You admins are now safe from tickets" : "Your admins are on their own now");
        }

        [ChatCommand("giveticket")]
        void GiveTicket(PlayerSession player, string command, string[] args)
        {
            //get the ID of the player for whom to give the ticket
            PlayerSession ticketTo = null;
            ticketTo = getPlayerFromName(string.Join(" ", args));
            if(ticketTo.IsAdmin)
            {
                hurt.SendChatMessage(player, "Ticket was not issued. Can't give admins tickets");
                hurt.SendChatMessage(ticketTo, player.Name + " tried giving you a ticket, and failed miserably.");
                return;
            }
            if (ticketTo == null)
            {
                hurt.SendChatMessage(player, "Ticket was not issued. Unable to locate the player.");
                return;
            }
            else
            {
                int currentTicketCount = 1;
                bool alreadyIssued = false;

                foreach (var t in tickets)
                {
                    if (t.To == ticketTo.SteamId.m_SteamID)
                    {
                        currentTicketCount++;
                    }
                    if (t.From == player.SteamId.m_SteamID)
                    {
                        //this player has already issued a ticket to this
                        alreadyIssued = true;
                        break;
                    }
                }

                if (alreadyIssued)
                {
                    hurt.SendChatMessage(player, "You can only issue a single ticket to a player.");
                    return;
                }

                if (currentTicketCount >= ticketsBeforeJail || player.IsAdmin)
                {

                    float distance = Vector3.Distance(ticketTo.WorldPlayerEntity.transform.position, GetJailCoords());
                    int repeats = (int)Math.Floor(Math.Round(jailLength / 5d));

                   timer.Repeat(5, repeats, () => TransferUserBack(ticketTo));
                    
                    //Create the time for unjailing the player
                    timer.Once(jailLength, () => UnJailPlayer(ticketTo));

                    //Send a message to the issuer
                    hurt.BroadcastChat(ticketTo.Name + " has been put in jail.");

                    //send a message to the defendant
                    if (player.IsAdmin)
                    {
                        ticketsBeforeJail.Times(() => IssueTicket(player, ticketTo));

                        hurt.SendChatMessage(ticketTo, "Admin " + player.Name + " has issued you a ticket for griefing. You have now been transported to jail.");
                    }
                    else
                    {
                        hurt.SendChatMessage(ticketTo, player.Name + " has issued you a ticket for griefing. You have exceeded the maximum number of tickets and have now been transported to jail.");
                    }
                }
                else
                {
                    IssueTicket(player, ticketTo);
                    //Send a message to the issuer
                    hurt.SendChatMessage(player, "A ticket has been issued to " + ticketTo.Name + " from " + player.Name);

                    //send a message to the defendant
                    hurt.SendChatMessage(ticketTo, player.Name + " has issued you a ticket for griefing. You have a total of " + currentTicketCount.ToString() + ". If you get " + ticketsBeforeJail.ToString() + ", you will be sent to jail for " + getTimeStringFromSeconds(jailLength) + ".");
                }
            }
        }
        private void IssueTicket(PlayerSession from, PlayerSession to)
        {
            var newTicket = new IssuedTicket();
            newTicket.From = from.SteamId.m_SteamID;
            newTicket.To = to.SteamId.m_SteamID;
            tickets.Add(newTicket);
        }
        [ChatCommand("setjailcoords")]
        void SetJailCoords(PlayerSession player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            jailCoordinates_x = player.WorldPlayerEntity.transform.position.x.ToString();
            jailCoordinates_y = player.WorldPlayerEntity.transform.position.y.ToString();
            jailCoordinates_z = player.WorldPlayerEntity.transform.position.z.ToString();
            SaveConfig();
            hurt.SendChatMessage(player, string.Format("Jail Coordinates have been set to {0}, {1}, {2}", jailCoordinates_x, jailCoordinates_y, jailCoordinates_z));
        }

        [ChatCommand("setjailtime")]
        void SetJailLength(PlayerSession player, string command, string[] args)
        {
            var isValid = int.TryParse(args[0], out jailLength);
            if (!player.IsAdmin || args.Length != 1 || !isValid)
                return;

            SaveConfig();
            hurt.SendChatMessage(player, "Jail time has been set to " + getTimeStringFromSeconds(jailLength) + ".");
        }

        [ChatCommand("setticketcountforjail")]
        void SetTicketCountBeforeJail(PlayerSession player, string command, string[] args)
        {
            var isValid = int.TryParse(args[0], out ticketsBeforeJail);
            if (!player.IsAdmin || args.Length != 1 || !isValid)
                return;

            SaveConfig();
            hurt.SendChatMessage(player, "# of tickets you get before going to jail has been set to " + ticketsBeforeJail + ".");
        }

        [ChatCommand("distancetojail")]
        void CmdDistanceToJail(PlayerSession player, string command, string[] args)
        {
            hurt.SendChatMessage(player, "The distance to the jail is " + DistanceToJail(player.WorldPlayerEntity.transform.position));
        }
        #endregion

        void UnJailPlayer(PlayerSession player)
        {
            if (player != null)
            {
                player.WorldPlayerEntity.transform.position += player.WorldPlayerEntity.transform.forward * 20;
                hurt.SendChatMessage(player, "You have served your Jail time.");
                hurt.BroadcastChat(player.Name + " has completed his jail time and has been set free.");

                //Remove all his tickets
                tickets.RemoveAll(x => x.To == player.SteamId.m_SteamID);
            }

        }

        #region common util functions
        bool isCoord(string arg)
        {
            double testDbl = 0;
            return Double.TryParse(arg, out testDbl);
        }

        Vector3 GetJailCoords()
        {
            Vector3 coord = new Vector3(float.Parse(jailCoordinates_x), float.Parse(jailCoordinates_y), float.Parse(jailCoordinates_z));
            return coord;
        }
        float DistanceToJail(Vector3 from)
        {
            return Vector3.Distance(from, GetJailCoords());
        }
        void TransferUserBack(PlayerSession player)
        {
            if (DistanceToJail(player.WorldPlayerEntity.transform.position) > int.Parse(Config["autoteleportbackdistance"].ToString()))
                player.WorldPlayerEntity.transform.position = GetJailCoords();
        }
        string getTimeStringFromSeconds(int seconds)
        {
            int s = seconds % 60;
            int minutes = seconds / 60;
            if (s > 0)
                return minutes.ToString() + " minutes and " + s.ToString() + " seconds";
            else
                return minutes.ToString() + " minutes";
        }

        PlayerSession getPlayerFromName(string identifier)
        {
            var sessions = GameManager.Instance.GetSessions();
            PlayerSession session = null;
            foreach (var i in sessions)
            {
                if (i.Value.Name.ToLower().Contains(identifier.ToLower()) || identifier.Equals(i.Value.SteamId.ToString()))
                {
                    session = i.Value;
                    break;
                }
            }

            return session;
        }


        #endregion

        #region Additional Classes
        class IssuedTicket
        {
            public ulong To { get; set; }
            public ulong From { get; set; }
        }
        string GetMsg(string key, object userID = null)
        {
            return (userID == null) ? lang.GetMessage(key, this) : lang.GetMessage(key, this, userID.ToString());
        }
        #endregion
    }



}
