using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FauxAdmin", "Colon Blow", "1.0.12", ResourceId = 1933)]
    class FauxAdmin : RustPlugin
    {

        #region Config and Init

        public bool DisableFlyHackProtection => Config.Get<bool>("DisableFlyHackProtection");
        public bool DisableNoclipProtection => Config.Get<bool>("DisableNoclipProtection");
        public bool DisableFauxAdminDemolish => Config.Get<bool>("DisableFauxAdminDemolish");
        public bool DisableFauxAdminRotate => Config.Get<bool>("DisableFauxAdminRotate");
        public bool DisableFauxAdminUpgrade => Config.Get<bool>("DisableFauxAdminUpgrade");
        public bool DisableNoclipOnNoBuild => Config.Get<bool>("DisableNoclipOnNoBuild");
        public bool EntKillOwnOnly => Config.Get<bool>("EntKillOwnOnly");
        public bool UseFauxAdminBanBlocker => Config.Get<bool>("UseFauxAdminBanBlocker");

        Dictionary<ulong, RestrictedData> _restricted = new Dictionary<ulong, RestrictedData>();

        class RestrictedData
        {
            public BasePlayer player;
        }

        protected override void LoadDefaultConfig()
        {
            Config["DisableFlyHackProtection"] = true;
            Config["DisableNoclipProtection"] = true;
            Config["DisableFauxAdminDemolish"] = true;
            Config["DisableFauxAdminRotate"] = true;
            Config["DisableFauxAdminUpgrade"] = true;
            Config["DisableNoclipOnNoBuild"] = true;
            Config["EntKillOwnOnly"] = true;
            Config["UseFauxAdminBanBlocker"] = true;
            SaveConfig();
        }

        void Init()
        {
            if (DisableFlyHackProtection) ConVar.AntiHack.flyhack_protection = 0;
            if (DisableNoclipProtection) ConVar.AntiHack.noclip_protection = 0;
            lang.RegisterMessages(messages, this);
            permission.RegisterPermission("fauxadmin.allowed", this);
            permission.RegisterPermission("fauxadmin.bypass", this);
            permission.RegisterPermission("fauxadmin.blocked", this);
        }

        bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"restricted", "You are not allowed to noclip here." },
            {"notallowed", "You are not worthy yet!" }
        };

        #endregion

        #region EntKill

        BaseEntity baseEntity;
        RaycastHit RayHit;
        static int layermask = LayerMask.GetMask("Construction", "Deployed", "Default");

        [ConsoleCommand("entkill")]
        void cmdConsoleEntKill(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!isAllowed(player, "fauxadmin.allowed"))
            {
                SendReply(player, lang.GetMessage("notallowed", this));
                return;
            }
            EntKillProcess(player);
        }

        void EntKillProcess(BasePlayer player)
        {
            bool flag1 = Physics.Raycast(player.eyes.HeadRay(), out RayHit, 10f, layermask);
            baseEntity = flag1 ? RayHit.GetEntity() : null;
            if (baseEntity == null) return;
            if (baseEntity is BasePlayer) return;
            if (EntKillOwnOnly && player.userID != baseEntity.OwnerID) return;
            baseEntity.Kill(BaseNetworkable.DestroyMode.Gib);

        }

        #endregion

        #region EntWho

        [ConsoleCommand("entwho")]
        void cmdConsoleEntWho(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!isAllowed(player, "fauxadmin.allowed"))
            {
                SendReply(player, lang.GetMessage("notallowed", this));
                return;
            }
            EntWhoProcess(player);
        }

        void EntWhoProcess(BasePlayer player)
        {
            bool flag2 = Physics.Raycast(player.eyes.HeadRay(), out RayHit, 10f, layermask);
            baseEntity = flag2 ? RayHit.GetEntity() : null;
            if (baseEntity == null) return;
            SendReply(player, "Owner ID: " + baseEntity.OwnerID.ToString());
        }

        #endregion

        #region Noclip

        [ChatCommand("noclip")]
        void cmdChatnoclip(BasePlayer player, string command, string[] args)
        {
            if (player.net?.connection?.authLevel > 0)
            {
                rust.RunClientCommand(player, "noclip");
                return;
            }
            if (!isAllowed(player, "fauxadmin.allowed"))
            {
                SendReply(player, lang.GetMessage("notallowed", this));
                return;
            }
            if (isAllowed(player, "fauxadmin.allowed"))
            {
                rust.RunClientCommand(player, "noclip");
                return;
            }
            return;
        }

        private void DeactivateNoClip(BasePlayer player, Vector3 newPos)
        {
            if (player == null) return;
            if (_restricted.ContainsKey(player.userID)) return;
            timer.Repeat(0.1f, 10, () => ForcePlayerPosition(player, newPos));

            _restricted.Add(player.userID, new RestrictedData
            {
                player = player
            });
            SendReply(player, lang.GetMessage("restricted", this));
            rust.RunClientCommand(player, "noclip");
            timer.Once(1, () => _restricted.Remove(player.userID));
            return;
        }

        #endregion

        #region Player Hooks

        void OnPlayerTick(BasePlayer player)
        {
            if (player.net?.connection?.authLevel > 0) return;
            if (DisableNoclipOnNoBuild)
            {
                if (_restricted.ContainsKey(player.userID)) return;

                if (player.CanBuild()) return;
                if (!player.CanBuild())
                {
                    if (!player.IsFlying) return;
                    if (isAllowed(player, "fauxadmin.bypass")) return;
                    if (player.IsFlying && isAllowed(player, "fauxadmin.allowed"))
                    {
                        player.violationLevel = 0;
                        var newPos = player.transform.position;
                        DeactivateNoClip(player, newPos);
                        return;
                    }
                }
            }
            return;
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (isAllowed(player, "fauxadmin.blocked"))
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                return;
            }
            if (player.net?.connection?.authLevel > 0) return;
            if (!isAllowed(player, "fauxadmin.allowed"))
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                return;
            }
            if (isAllowed(player, "fauxadmin.allowed"))
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                return;
            }
            return;
        }

        #endregion

        #region Ban Blocker

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.cmd == null) return null;
            string command = arg.cmd.Name;
            string reason = arg.GetString(1).ToString();
            if (command.Equals("ban") || command.Equals("banid"))
            {
                if (UseFauxAdminBanBlocker && reason.Equals("Cheat Detected!"))
                {
                    BasePlayer player = arg.GetPlayer(0);
                    if ((player) && isAllowed(player, "fauxadmin.allowed"))
                    {
                        PrintWarning($"FauxAdmin Ban Blocker stopped a ban of " + player.ToString() + " for " + reason);
                        return false;
                    }
                }
            }
            return null;
        }

        #endregion

        #region Structure Hooks

        object OnStructureDemolish(BuildingBlock block, BasePlayer player)
        {
            if (isAllowed(player, "fauxadmin.allowed"))
            {
                if (block.OwnerID == 0 || player.userID == 0) return null;
                if (block.OwnerID == player.userID) return null;
                if (block.OwnerID != player.userID && DisableFauxAdminDemolish)
                {
                    return true;
                }
            }
            return null;
        }

        object OnStructureRotate(BuildingBlock block, BasePlayer player)
        {
            if (isAllowed(player, "fauxadmin.allowed"))
            {
                if (block.OwnerID == 0 || player.userID == 0) return null;
                if (block.OwnerID == player.userID) return null;
                if (block.OwnerID != player.userID && DisableFauxAdminRotate)
                {
                    return true;
                }
            }
            return null;
        }

        object OnStructureUpgrade(BuildingBlock block, BasePlayer player)
        {
            if (isAllowed(player, "fauxadmin.allowed"))
            {
                if (block.OwnerID == 0 || player.userID == 0) return null;
                if (block.OwnerID == player.userID) return null;
                if (block.OwnerID != player.userID && DisableFauxAdminUpgrade)
                {
                    return true;
                }
            }
            return null;
        }

        #endregion

    }
}