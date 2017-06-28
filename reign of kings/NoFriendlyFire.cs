using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CodeHatch.Damaging;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using CodeHatch.Inventory.Blueprints;
using Oxide.Core;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.ItemContainer;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch.Engine.Events.Prefab;
using CodeHatch.Blocks.Networking.Events;

namespace Oxide.Plugins
{
    [Info("No Friendly Fire", "CrZy & D-Kay", "1.2", ResourceId = 1091)]
    public class NoFriendlyFire : ReignOfKingsPlugin
    {
        #region Variables

        private bool Active = true;
        private Collection<ulong> _NoFriendlyFire = new Collection<ulong>();

        #endregion

        #region Save and Load Data

        private void LoadData()
        {
            _NoFriendlyFire = Interface.GetMod().DataFileSystem.ReadObject<Collection<ulong>>("NffExceptionList");
            Active = Interface.GetMod().DataFileSystem.ReadObject<bool>("NffActivity");
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("SavedExceptionList", _NoFriendlyFire);
            Interface.GetMod().DataFileSystem.WriteObject("SavedNffActivity", Active);
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "AlreadyTurnedOnOff", "[4444FF]NFF[FFFFFF] : You already have NFF turned {0}." },
                { "TurnedOnOff", "[4444FF]NFF[FFFFFF] : NFF was turned {0}." },
                { "ToggleAll", "[4444FF]NFF[FFFFFF] : NFF was {0}." },
                { "Help", "[0000FF]No Friendly Fire[FFFFFF]" },
                { "HelpOn", "[00FF00]/nff on[FFFFFF] - Turn the friendly fire safety on." },
                { "HelpOff", "[00FF00]/nff off[FFFFFF] - Turn the friendly fire safety off." },
                { "HelpToggleAll", "[00FF00]/nff all[FFFFFF] - Activate or deactivate NFF." }
            }, this);
        }

        void Loaded()
        {
            LoadDefaultMessages();
            LoadData();
            permission.RegisterPermission("NoFriendlyFire.Toggle", this);
        }

        #endregion

        #region Player Commands

        [ChatCommand("nff")]
        private void NoFriendlyFireOnOrOff(Player player, string cmd, string[] input)
        {
            switch (input[0])
            {
                case "off":
                    if (!_NoFriendlyFire.Contains(player.Id)) { PrintToChat(player, string.Format(GetMessage("AlreadyTurnedOnOff", player), "off")); return; }

                    _NoFriendlyFire.Remove(player.Id);
                    PrintToChat(player, string.Format(GetMessage("TurnedOnOff", player), "off"));
                    SaveData();
                    break;
                case "on":
                    if (_NoFriendlyFire.Contains(player.Id)) { PrintToChat(player, string.Format(GetMessage("AlreadyTurnedOnOff", player), "on")); return; }

                    _NoFriendlyFire.Add(player.Id);
                    PrintToChat(player, string.Format(GetMessage("TurnedOnOff", player), "on"));
                    SaveData();
                    break;
                case "all":
                    if (Active) { Active = false; PrintToChat(player, string.Format(GetMessage("ToggleAll", player), "deactivated")); }
                    else { Active = true; PrintToChat(player, string.Format(GetMessage("ToggleAll", player), "activated")); }
                    SaveData();
                    break;
                case "help":
                    SendHelpText(player);
                    break;
            }
        }

        #endregion

        #region Hooks

        private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        {
            if (!Active) return;
            if (damageEvent == null) return;
            if (damageEvent.Damage == null) return;
            if (damageEvent.Damage.Amount <= 0) return;
            if (damageEvent.Damage.DamageSource == null) return;
            if (damageEvent.Entity == null) return;
            if (damageEvent.Damage.DamageSource == damageEvent.Entity) return;
            if (!damageEvent.Damage.DamageSource.IsPlayer) return;
            if (!damageEvent.Entity.IsPlayer) return;

            if ((_NoFriendlyFire.Contains(damageEvent.Damage.DamageSource.Owner.Id) || _NoFriendlyFire.Contains(damageEvent.Entity.Owner.Id))
                && damageEvent.Entity.Owner.GetGuild() == damageEvent.Damage.DamageSource.Owner.GetGuild())
            {
                damageEvent.Cancel("No Friendly Fire");
                damageEvent.Damage.Amount = 0f;
                return;
            }
        }

        private void SendHelpText(Player player)
        {
            PrintToChat(player, GetMessage("Help", player));
            PrintToChat(player, GetMessage("HelpOn", player));
            PrintToChat(player, GetMessage("HelpOff", player));
            if (player.HasPermission("NoFriendlyFire.Toggle"))
            {
                PrintToChat(player, GetMessage("HelpToggleAll", player));
            }
        }

        #endregion

        #region Helpers

        string GetMessage(string key, Player player = null) => lang.GetMessage(key, this, player == null ? null : player.Id.ToString());

        #endregion
    }
}
