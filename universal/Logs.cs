
// WARNING: Date format is dd/M/yyyy (Day/Month/Year)
// If you have any dayfirstophobia or cannot read this format properly, you'll need to get used to it.

using System;

using CodeHatch.Engine.Networking;
using CodeHatch.Engine.Core.Cache;

using CodeHatch.Blocks.Networking.Events;

using CodeHatch.Common;

using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Entities.Players;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Networking.Events.Social;

namespace Oxide.Plugins
{
    [Info("Server Logs", "Sydney", "0.1")]
    public class Logs : ReignOfKingsPlugin
    {
        #region LogSettings
        static bool LogEvents = true;
        static bool LogCubes = true;
        static bool LogEntities = true;
        static bool LogChat = true;

        static bool CrestSpecial = true;
        static bool SleeperSpecial = true;
        #endregion

        private void OnPlayerConnected(Player player)
        {
            if (LogEvents == false) return;
            Log("Events", player.DisplayName + " connected.");
        }

        private void OnPlayerDisconnected(Player player)
        {
            if (LogEvents == false) return;
            Log("Events", player.DisplayName + " disconnected.");
        }

        private void OnPlayerRespawn(PlayerRespawnEvent e)
        {
            if (LogEvents == false) return;
            string entityPosition = e.Player.Entity.Position.x + "," + e.Player.Entity.Position.y + "," + e.Player.Entity.Position.x;

            string str = "";

            str += e.Player.DisplayName + " has respawned at [" + entityPosition + "].";

            Log("Events", str);

        }

        private void OnPlayerChat(PlayerEvent e)
        {
            if (LogChat == false) return;
            string str = "";

            if (e is GuildMessageEvent)
            {
                var chat = (GuildMessageEvent)e;

                str += "[" + chat.GuildName + "] " + chat.PlayerName + ": " + chat.Message;

                Log("Chat.Guild", str);
            }

            else if (e is PlayerChatEvent)
            {
                var chat = (PlayerChatEvent)e;

                str += chat.PlayerName +  ": " + chat.Message;

                Log("Chat.Global", str);
            }

            
        }

        private void OnEntityHealthChange(EntityDamageEvent e)
        {
            if (LogEntities == false) return;
            string str = "";
            string entityPosition = e.Entity.Position.x + "," + e.Entity.Position.y + "," + e.Entity.Position.x;
            if (e.Damage.Amount > 0)
            {

                if (e.Damage.DamageSource.IsPlayer) str += e.Damage.DamageSource.Owner.Name + " has done ";
                else str += e.Damage.DamageSource.name + " has done ";

                str += e.Damage.Amount + " [" + e.Damage.DamageTypes + "] damage points to ";

                if (e.Entity.IsPlayer) str += e.Entity.Owner.Name;
                else if(IsAnimal(e)) str += e.Entity.name;
                else
                    str += e.Entity.name;

                str += " with a " + e.Damage.Damager.name + " at [" + entityPosition + "].";
                if((CrestSpecial == true && e.Entity.name.Contains("Crest")) || (SleeperSpecial == true && e.Entity.name.Contains("Sleep")))
                {
                    Log("Special", str);
                    return;
                }
                else Log("Entities", str);
            }

            if (e.Damage.Amount < 0)
            {
                if(e.Entity.IsPlayer)
                {
                    str += e.Entity.Owner.Name + " has gained " + (e.Damage.Amount * -1) + " health points.";

                    Log("Entities", str);
                }
                else
                {
                    str += e.Entity.name + " has gained " + (e.Damage.Amount * -1) + " health points";
                    if (e.Damage.DamageSource.IsPlayer) str += " from " + e.Damage.DamageSource.Owner.Name;
                    str += ".";

                    Log("Entities", str);
                }
                
                
            }
        }

        private void OnEntityDeath(EntityDeathEvent e)
        {
            if (LogEntities == false) return;
            string str = "";
            string entityPosition = e.Entity.Position.x + "," + e.Entity.Position.y + "," + e.Entity.Position.x;

            if (e.KillingDamage != null)
            {
                if (e.KillingDamage.DamageSource.IsPlayer)
                    str += e.KillingDamage.DamageSource.Owner.Name;
                else if (IsEntityAnimal(e.KillingDamage.DamageSource))
                    str += e.KillingDamage.DamageSource.name;
                else if (e.Entity.IsPlayer)
                    str += e.Entity.Owner.DisplayName;


                if (e.Entity.IsPlayer && !e.Entity.IsPlayer)
                    str += " has killed " + e.Entity.Owner.Name;
                else if (IsAnimal(e))
                    str += " has killed " + e.Entity.name;
                else
                    str += " has killed himself";

                str += " [" + e.KillingDamage.DamageTypes + "," + e.KillingDamage.Amount + "] with a " + e.KillingDamage.Damager.name + " at [" + entityPosition + "].";

                Log("Entities", str);
            }
            else
            {
                if (e.Entity.IsPlayer)
                    str += e.Entity.Owner.Name + " has died.";
                else if (IsAnimal(e))
                    str += e.Entity.name + " has died.";
                else
                    str += e.Entity.name + " has been destroyed.";
                Log("Entities", str);
            }
        }

        void OnCubePlacement(CubePlaceEvent e)
        {
            if (LogCubes == false) return;
            string cubePosition = e.Position.x + "," + e.Position.y + "," + e.Position.z;
            string str = "";

            var bp = CodeHatch.Blocks.Inventory.InventoryUtil.GetTilesetBlueprint(e.Material, (int)e.PrefabId);
            if (bp == null) return;

            Player Owner = Server.GetPlayerById(e.SenderId);

            if (Owner != null)
                    str += Owner.DisplayName + " has placed a " + bp.Name + "";
            else str += "A " + bp.Name + " has been placed";

            str += " at [" + cubePosition + "].";

            Log("Cubes", str);
        }

        void OnCubeTakeDamage(CubeDamageEvent e)
        {
            if (e.Damage.Amount > 0)
            {
                if (LogCubes == false) return;
                // var bp = CodeHatch.Blocks.Inventory.InventoryUtil.GetTilesetBlueprint(e.Material, (int)e.PrefabId);
                //if (bp == null) return;

                string cubePosition = e.Position.x + "," + e.Position.y + "," + e.Position.z;

                string str = "";
                if (e.Damage.DamageSource.IsPlayer)
                    str = str + e.Damage.DamageSource.Owner.Name;
                else str = str + e.Damage.DamageSource.name;

                str +=" has done " + e.Damage.Amount + " [" + e.Damage.DamageTypes + "] damage points to a cube";

                str += " with a " + e.Damage.Damager.name + " at [" + cubePosition + "].";

                Log("Cubes", str);

            }

        }
        void OnCubeDestroyed(CubeDestroyEvent e)
        {
            if (LogCubes == false) return;
            string cubePosition = e.Position.x + "," + e.Position.y + "," + e.Position.z;

            string str = "";

            str = str + "A cube has been destroyed at [" + cubePosition + "].";

            Log("Cubes", str);
        }

        void Log(string file, string msg)
        {
            string filepath = file + ".log";
            string text = DateTime.Now.ToString("d/M/yyyy") + " " + DateTime.Now.ToString("HH:mm:ss") + " " + file + ": " + msg + "\r\n";
            LogFileUtil.LogTextToFile("scriptfiles/logs", filepath, text);
        }

        bool IsEntityAnimal(Entity e)
        {
            if (e.Has<MonsterEntity>() || e.Has<CritterEntity>()) return true;
            return false;
        }

        bool IsAnimal(EntityEvent e) {
            if (e.Entity.Has<MonsterEntity>() || e.Entity.Has<CritterEntity>()) return true;
            return false; }
    }
}
