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
    [Info("No Friendly Fire", "D-Kay", "1.1", ResourceId = 1091)]
    public class NoFriendlyFire : ReignOfKingsPlugin
    {
        #region Vriables

        private bool Active = true;
        private Collection<string> _NoFriendlyFire = new Collection<string>();

        #endregion

        #region Save and Load Data

        private void LoadData()
        {
            _NoFriendlyFire = Interface.GetMod().DataFileSystem.ReadObject<Collection<string>>("SavedExceptionList");
            Active = Interface.GetMod().DataFileSystem.ReadObject<bool>("SavedNffActivity");
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
            var onoffall = input[0];
            switch (onoffall)
            {
                case "off":
                    var position = -1;
                    for (var i = 0; i < _NoFriendlyFire.Count; i++)
                    {
                        if (_NoFriendlyFire[i] == player.DisplayName.ToLower())
                        {
                            position = i;
                            break;
                        }
                    }

                    if (position < 0)
                    {
                        PrintToChat(player, string.Format(GetMessage("AlreadyTurnedOnOff", player.Id.ToString()), "off") );
                        return;
                    }

                    _NoFriendlyFire.RemoveAt(position);
                    PrintToChat(player, string.Format(GetMessage("TurnedOnOff", player.Id.ToString()), "off"));
                    SaveData();
                    break;
                case "on":
                    //Check if player is already on the list
                    foreach (var tradeMaster in _NoFriendlyFire)
                    {
                        if (tradeMaster.ToLower() == player.DisplayName.ToLower())
                        {
                            PrintToChat(player, string.Format(GetMessage("AlreadyTurnedOnOff", player.Id.ToString()), "on"));
                            return;
                        }
                    }

                    // Add the player to the list
                    _NoFriendlyFire.Add(player.DisplayName.ToLower());
                    PrintToChat(player, string.Format(GetMessage("TurnedOnOff", player.Id.ToString()), "on"));
                    SaveData();
                    break;
                case "all":
                    if (Active) { Active = false; PrintToChat(player, string.Format(GetMessage("ToggleAll", player.Id.ToString()), "deactivated")); }
                    else { Active = true; PrintToChat(player, string.Format(GetMessage("ToggleAll", player.Id.ToString()), "activated")); }
                    SaveData();
                    break;
                case "help":
                    PrintToChat(player, GetMessage("Help", player.Id.ToString()) );
                    PrintToChat(player, GetMessage("HelpOn", player.Id.ToString()) );
                    PrintToChat(player, GetMessage("HelpOff", player.Id.ToString()) );
                    if (player.HasPermission("NoFriendlyFire.Toggle"))
                    {
                        PrintToChat(player, GetMessage("HelpToggleAll", player.Id.ToString()) );
                    }
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
            if (damageEvent.Damage.DamageSource == null) return;
            if (damageEvent.Entity == null) return;
            foreach (var player in _NoFriendlyFire)
            {
                if (player.ToLower() == damageEvent.Entity.Owner.DisplayName.ToLower())
                {
                    if (
                        damageEvent.Damage.Amount > 0 // taking damage
                        && damageEvent.Entity.IsPlayer // entity taking damage is player
                        && damageEvent.Damage.DamageSource.IsPlayer // entity delivering damage is a player
                        && damageEvent.Entity != damageEvent.Damage.DamageSource // entity taking damage is not taking damage from self
                        && damageEvent.Entity.Owner.GetGuild().DisplayName == damageEvent.Damage.DamageSource.Owner.GetGuild().DisplayName // both entities are in the same guild
                        )
                    {
                        damageEvent.Cancel("No Friendly Fire");
                        damageEvent.Damage.Amount = 0f;
                    }
                }
            }
        }

        #endregion

        #region Helpers

        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}
