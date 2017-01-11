
using System;
using System.Collections.Generic;
using CodeHatch.Build;
using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Engine.Core.Cache;
using UnityEngine;

namespace Oxide.Plugins
{
    //Need a Script?
    //Patrick Shearon aka Hawthorne
    //www.theregulators.org
    //pat.shearon@gmail.com
    //05-21-2015
    [Info("No Attack", "Hawthorne", "1.0.")]
    public class NoAttack : ReignOfKingsPlugin
    {
        float maxDistance = 135f; //Set distanace radius for marks
        List<Vector2> marks = new List<Vector2>();
        void Loaded()
        {          
            //Add marks
            marks.Add(new Vector2(-78.62f,225f));
        }

        [ChatCommand("loc")]
        private void LocationCommand(Player player, string cmd, string[] args)
        {
            //use the loc command to get locations only use the X and Z for marks.
            PrintToChat(player, string.Format("Current Location: x:{0} y:{1} z:{2}", player.Entity.Position.x.ToString(), player.Entity.Position.y.ToString(), player.Entity.Position.z.ToString()));
        }

        private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        {
            if (damageEvent.Entity.IsPlayer)
            {
                foreach (Vector2 mark in marks)
                {
                    float distance = Math.Abs(Vector2.Distance(mark, new Vector2(damageEvent.Entity.Position.x, damageEvent.Entity.Position.z)));                    
                    if (distance <= maxDistance)
                    {
                        damageEvent.Cancel("No damage area");
                        damageEvent.Damage.Amount = 0f;                        
                        PrintToChat(damageEvent.Damage.DamageSource.Owner, "[FF0000]You can't attack a person in this area.");
                    }
                }
            }
        }
    }
}
