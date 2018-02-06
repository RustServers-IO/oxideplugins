// Reference: UnityEngine.UI

using System.Collections.Generic;
using System;
using Steamworks;
using uLink;
using Oxide.Game.Hurtworld.Libraries;

namespace Oxide.Plugins
{
    [Info("RemoveClaim", "Mortu", "1.1.0")]
    [Description("Remove the claim of the vehicle you're driving or remove your own claim.")]

    class RemoveClaim : HurtworldPlugin
    {
        Hurtworld hurt = new Hurtworld();
        private const string MOD_NAME = "RemoveClaim";
        //Change this string if you like, the rest can be changed through the config
        private const string COMMAND_REMOVE_CLAIM = "removeclaim";
        private const string COMMAND_REMOVE_CLAIM_SHORT = "rc";
        private const string ARG_HELP = "help";

        private const string CONFIG_MUTED = "ModIsMuted";
        private const string CONFIG_SHORT_ENABLED = "ShorthandsEnabled";
        private const string CONFIG_COLOR_PREFIX = "PrefixColor";
        private const string CONFIG_COLOR_TEXT = "TextColor";
        private const string CONFIG_COLOR_ERROR = "ErrorColor";
        private const string CONFIG_COLOR_COMMAND = "CommandColor";
        private const string CONFIG_DEFAULT_PERMISSION_REMOVE_CLAIM = "RemoveClaimPermissionByDefault";

        private const string PERMISSION_REMOVE_CLAIM = "removeclaim.use";

        private const string DEFAULT_MUTED = "false";
        private const string DEFAULT_SHORT_ENABLED = "true";
        private const string DEFAULT_COLOR_PREFIX = "orange";
        private const string DEFAULT_COLOR_TEXT = "white";
        private const string DEFAULT_COLOR_ERROR = "red";
        private const string DEFAULT_COLOR_COMMAND = "orange";
        private const string DEFAULT_DEFAULT_PERMISSION_REMOVE_CLAIM = "true";

        private const string ERROR_CLAIM_NOT_REMOVED = "ClaimNotRemoved";
        private const string MSG_CLAIM_REMOVED = "ClaimRemoved";
        private const string MSG_HELP_REMOVE_CLAIM = "RemoveClaimHelp";
        private const string MSG_NO_PERMISSION = "NoPermission";

        void RemoveSomeonesClaim(PlayerSession session, string command, string[] args)
        {
            if (!userHasPerm(session, PERMISSION_REMOVE_CLAIM)) {
                SendErrorMessage(MSG_NO_PERMISSION, session);
                return;
            }

            if (isHelpCalled(args))
            {
                SendHelpMessage(MSG_HELP_REMOVE_CLAIM, session);
                return;
            }

            var vehicleOwnershipMgr = Singleton<VehicleOwnershipManager>.Instance;
            foreach (KeyValuePair<CSteamID, PlayerIdentity> pair in (Dictionary<CSteamID, PlayerIdentity>)GameManager.Instance.GetIdentifierMap())
            {
                if (vehicleOwnershipMgr.HasClaim(pair.Value) && isPlayerInsideCar(session, pair.Value))
                {
                    vehicleOwnershipMgr.Unclaim(pair.Value);
                    SendMessage(MSG_CLAIM_REMOVED, session);
                    return;
                }
            }
            SendErrorMessage(ERROR_CLAIM_NOT_REMOVED, session);
        }

        private bool isPlayerInsideCar(PlayerSession player, PlayerIdentity carOwner)
        {
            // You might wanna find a more solid way of doing this.
            float distance = getPlayerCarDistance(player, carOwner);

            if (0 == distance)
            {
                return true; //roach
            }

            // was tested with sandHopper and Mangler wheels
            if (distance > 0.39f && distance < 0.41f)
            {
                return true; //goat (driver or passenger)
            }
            return false;
        }

        private float getPlayerCarDistance(PlayerSession session, PlayerIdentity carOwner)
        {
            UnityEngine.Vector3 carPos = Singleton<VehicleOwnershipManager>.Instance.GetClaimPosition(carOwner);

            if (null == carPos)
            {
                return float.MaxValue;
            }

            var position = session.WorldPlayerEntity.transform.position;
            return UnityEngine.Vector3.Magnitude(carPos - position);
        }

        private bool isHelpCalled(string[] args)
        {
            return (args.Length != 1) ? false : ARG_HELP.Equals(args[0].ToLower());
        }

        void AddCommands()
        {
            cmd.AddChatCommand(COMMAND_REMOVE_CLAIM, this, "RemoveSomeonesClaim");

            if (GetConfigItemAsBool(CONFIG_SHORT_ENABLED))
            {
                cmd.AddChatCommand(COMMAND_REMOVE_CLAIM_SHORT, this, "RemoveSomeonesClaim");
            }
        }

        //Config functions
        protected override void LoadDefaultConfig() { }

        void Loaded()
        {
            LoadConfig();
            LoadMessages();
            AddCommands();
            SetPermissions();
        }

        void LoadConfig()
        {
            SetValueIfAbsentInConfig(CONFIG_DEFAULT_PERMISSION_REMOVE_CLAIM, DEFAULT_DEFAULT_PERMISSION_REMOVE_CLAIM);
            SetValueIfAbsentInConfig(CONFIG_MUTED, DEFAULT_MUTED);
            SetValueIfAbsentInConfig(CONFIG_SHORT_ENABLED, DEFAULT_SHORT_ENABLED);
            SetValueIfAbsentInConfig(CONFIG_COLOR_PREFIX, DEFAULT_COLOR_PREFIX);
            SetValueIfAbsentInConfig(CONFIG_COLOR_TEXT, DEFAULT_COLOR_TEXT);
            SetValueIfAbsentInConfig(CONFIG_COLOR_ERROR, DEFAULT_COLOR_ERROR);
            SetValueIfAbsentInConfig(CONFIG_COLOR_COMMAND, DEFAULT_COLOR_COMMAND);
            SaveConfig();
        }

        private void SetValueIfAbsentInConfig(string configName, string defaultValue)
        {
            if (null == Config[MOD_NAME, configName])
            {
                Config.Set(MOD_NAME, configName, defaultValue);
            }
        }

        private string GetConfigItem(string configName)
        {
            return (string)Config[MOD_NAME, configName];
        }

        private bool GetConfigItemAsBool(string configName)
        {
            return null == GetConfigItem(configName) ? false : Boolean.Parse(GetConfigItem(configName));
        }

        void LoadMessages()
        {
            // Defines default messages
            // Please modify .../oxide/lang/RemoveClaim.en, or create a new translation!
            Dictionary<string, string> msgs = new Dictionary<string, string>();
            msgs.Add(ERROR_CLAIM_NOT_REMOVED, "Claim wasn't removed.");
            msgs.Add(MSG_CLAIM_REMOVED, "Claim was removed.");
            msgs.Add(MSG_HELP_REMOVE_CLAIM, "Removes the claim from the vehicle you're driving.");
            msgs.Add(MSG_NO_PERMISSION, "You don't have permission to use this command.");
            lang.RegisterMessages(msgs, this);
        }

        //Message functions
        private void SendMessage(string key, PlayerSession session)
        {
            SendMessage(key, session, CONFIG_COLOR_TEXT);
        }

        private void SendErrorMessage(string errorKey, PlayerSession session)
        {
            SendMessage(errorKey, session, CONFIG_COLOR_ERROR);
        }

        private void SendHelpMessage(string helpKey, PlayerSession session)
        {
            string cmdString = "/" + COMMAND_REMOVE_CLAIM;
            cmdString = InColor(cmdString, GetConfigItem(CONFIG_COLOR_COMMAND));
            string helpMsg = lang.GetMessage(helpKey, this, session.SteamId.ToString());
            hurt.SendChatMessage(session, cmdString + " : " + helpMsg);
        }

        private void SendMessage(string key, PlayerSession session, string colorKey)
        {
            if (!GetConfigItemAsBool(CONFIG_MUTED))
            {
                string prefixcolor = GetConfigItem(CONFIG_COLOR_PREFIX);
                string coloredPrefixStr = InColor("[" + MOD_NAME + "]", prefixcolor);

                string msgStr = lang.GetMessage(key, this, session.SteamId.ToString());
                string msgColor = GetConfigItem(colorKey);
                string coloredMsgStr = InColor(msgStr, msgColor);

                hurt.SendChatMessage(session, coloredPrefixStr + " " + coloredMsgStr);
            }
        }

        private string InColor(string str, string color)
        {
            return "<color=" + color + ">" + str + "</color>";
        }

        #region [PERMISSIONS]
        void SetPermissions() {
            permission.RegisterPermission(PERMISSION_REMOVE_CLAIM, this);

            if (GetConfigItemAsBool(CONFIG_DEFAULT_PERMISSION_REMOVE_CLAIM)) {
                permission.GrantGroupPermission("default", PERMISSION_REMOVE_CLAIM, this);
            }
        }

        bool userHasPerm(PlayerSession session, string perm) {
            return permission.UserHasPermission(session.SteamId.ToString(), perm);
        }

        #endregion
    }
}