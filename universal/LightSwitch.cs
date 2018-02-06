using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("LightSwitch", "Kodfod", "1.0.2")]
    [Description("Allows you to control all the lights on the server.")]
    class LightSwitch : HurtworldPlugin
    {

        /*
            A Quick Thanks to LaserHydra, for their
            "Stake Arthorizer" plugin. Due to this
            Release, I was able to finish this Plugin.

            -Kodfod
        */

        private bool HasTurnedOnForTheNight;

        ////////////////////////////////////////
        ///     On Plugin Loaded
        ////////////////////////////////////////

        void Loaded()
        {
            permission.RegisterPermission("lightswitch.switch", this);

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"SuccessOn", "Successfully turned on all lights."},
                {"SuccessOff", "Successfully turned off all lights."},
                {"ErrorNoPermission", "You do not have permission to do switch the lights."},
                {"ErrorSyntax", "Incorrect Syntax. /lights <on|off>"}
            }, this);

            HasTurnedOnForTheNight = false;

            StartLightLoop();
        }

        ////////////////////////////////////////
        ///     Chat Commands
        ////////////////////////////////////////

        [ChatCommand("lights")]
        void LightManagerLightsCommand(PlayerSession session, string cmd, string[] args)
        {
            if (args.Length >= 3 || args.Length == 0)
            {
                SendChatMessage(session, "ErrorSyntax");
                return;
            }
            if (!CheckPlayerPermission(session, "switch"))
            {
                SendChatMessage(session, "ErrorNoPermission");
                return;
            }
            string response = null;
            float radius;
            if (args.Length == 1)
            {
                radius = 0;
            } else
            {
                float.TryParse(args[1], out radius);
            }
            switch (args[0])
            {
                case "on":
                    response = TurnOnLights(session, radius);
                    SendChatMessage(session, response);
                    break;
                case "off":
                    response = TurnOffLights(session, radius);
                    SendChatMessage(session, response);
                    break;
                default:
                    SendChatMessage(session, "ErrorSyntax");
                    break;
            }
        }

        ////////////////////////////////////////
        ///     Command Logic Handling
        //////////////////////////////////////// 

        string TurnOnLights(PlayerSession s, float radius)
        {
            List<FireTorchServer> torches = GetFireTorches(s.WorldPlayerEntity.transform.position, radius);

            foreach (FireTorchServer light in torches)
            {
                if (!light.IsOn)
                {
                    light.StartUse(s.WorldPlayerEntity.GetComponent<WorldItemInteractServer>());
                }
            }
            return "SuccessOn";
        }

        string TurnOffLights(PlayerSession s, float radius)
        {
            List<FireTorchServer> torches = GetFireTorches(s.WorldPlayerEntity.transform.position, radius);

            var skin = s.WorldPlayerEntity.GetComponent<SkinChanger>();

            foreach (FireTorchServer light in torches)
            {
                if (light.IsOn)
                {
                    light.StartUse(s.WorldPlayerEntity.GetComponent<WorldItemInteractServer>());
                }
            }
            return "SuccessOff";
        }


        // Thanks for this LaserHydra!
        // Gets all the torches in a given area or whole server.
        List<FireTorchServer> GetFireTorches(Vector3 pos, float radius)
        {
            List<FireTorchServer> torches = new List<FireTorchServer>();

            foreach (FireTorchServer light in Resources.FindObjectsOfTypeAll<FireTorchServer>())
            {
                if (radius != 0)
                {
                    if (Vector3.Distance(light.transform.position, pos) <= radius) torches.Add(light);
                }
                else torches.Add(light);
            }

            return torches;
        }

        ////////////////////////////////////////
        ///     Permission Checking
        ////////////////////////////////////////
        bool CheckPlayerPermission(PlayerSession session, string cmd)
        {
            string _prefix = "lightswitch.";
            string _id = session.Identity.SteamId.ToString();

            if (permission.UserHasPermission(_id, _prefix + cmd)) return true;

            return false;
        }

        ////////////////////////////////////////
        ///     Chat Handling
        ////////////////////////////////////////

        void SendChatMessage(PlayerSession session, string reason)
        {
            string _reason = lang.GetMessage(reason, this);
            hurt.SendChatMessage(session, _reason);
        }

        void StartLightLoop()
        {
            TimeManager t = GameManager.Instance.GetComponent<TimeManager>();
            timer.Repeat(5, 0, () => {
                PlayerSession session = FindSession();
                try
                {
                    //hurt.BroadcastChat("", ""+HasTurnedOnForTheNight);
                    if (!t.GetIsDay())
                    {
                        //hurt.BroadcastChat("[TEST]:", "" + HasTurnedOnForTheNight);
                        if (!HasTurnedOnForTheNight)
                        {
                            HasTurnedOnForTheNight = true;
                            TurnOnLights(session, 0);
                        }
                    } else
                    {
                        if (HasTurnedOnForTheNight) HasTurnedOnForTheNight = false;
                    }
                }
                catch { }
            });
        }

        private PlayerSession FindSession()
        {
            var sessions = GameManager.Instance.GetSessions();
            PlayerSession session = null;
            foreach (var i in sessions)
            {
                return i.Value;
            }
            return session;
        }
    }
}
