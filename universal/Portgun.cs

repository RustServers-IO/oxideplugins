#if REIGNOFKINGS
using CodeHatch;
using CodeHatch.Engine.Networking;
#endif
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
#if HURTWORLD || REIGNOFKINGS || RUST || RUSTLEGACY || THEFOREST
using UnityEngine;
#endif
#if HURTWORLD
using uLink;
#endif

// TODO: Protect players from damage (in all games) until second after teleporting

namespace Oxide.Plugins
{
    [Info("Portgun", "Wulf/lukespragg", "3.2.0", ResourceId = 664)]
    [Description("Teleports players with permission to object or terrain they are looking at")]
    public class Portgun : CovalencePlugin
    {
        #region Localization

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["CommandPort"] = "port",
                ["NoDestination"] = "Could not find a valid destination to port to",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private HashSet<string> protection = new HashSet<string>();
        private const string permUse = "portgun.use";
        private int layers;

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permUse, this);

            AddCovalenceCommand("portgun", "PortCommand");
            AddLocalizedCommand("CommandPort", "PortCommand");

#if HURTWORLD
            layers = LayerMaskManager.TerrainConstructionsMachines;
#elif REIGNOFKINGS
            layers = LayerMask.GetMask("Cubes", "Environment", "Terrain");
#elif RUST
            layers = LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World");
#elif RUSTLEGACY
            //layers = LayerMask.GetMask();
#endif
        }

        #endregion Initialization

        #region Port Command

        private void PortCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            var destination = new GenericPosition();
#if HURTWORLD || REIGNOFKINGS || RUST || RUSTLEGACY || THEFOREST
            RaycastHit hit;
#endif
#if HURTWORLD
            var entity = (player.Object as PlayerSession).WorldPlayerEntity;
            var simData = entity.GetComponent<PlayerStatManager>().RefCache.PlayerCamera.SimData;
            var controller = entity.GetComponent<CharacterController>();
            var point1 = simData.FirePositionWorldSpace + controller.center + Vector3.up * -controller.height * 0.5f;
            var point2 = point1 + Vector3.up * controller.height;
            var direction = simData.FireRotationWorldSpace * Vector3.forward;
            if (!Physics.CapsuleCast(point1, point2, controller.radius, direction, out hit, float.MaxValue, layers))
#elif REIGNOFKINGS
            var entity = (player.Object as Player).Entity;
            if (!Physics.Raycast(entity.Position, entity.GetOrCreate<LookBridge>().Forward, out hit, float.MaxValue, layers))
#elif RUST
            if (!Physics.Raycast((player.Object as BasePlayer).eyes.HeadRay(), out hit, float.MaxValue, layers))
#elif RUSTLEGACY
            var character = (player.Object as NetUser).playerClient.controllable.idMain;
            if (!Physics.Raycast(character.eyesRay, out hit, float.MaxValue, 67108864))
#elif THEFOREST
            var entity = (player.Object as BoltEntity);
            Physics.Raycast(entity.transform.position, entity.transform.rotation * Vector3.forward, out hit, float.MaxValue, layers);
            if (!hit.collider.CompareTag("TerrainMain") || !hit.collider.CompareTag("structure"))
#endif
            {
                player.Reply(Lang("NoDestination", player.Id));
                return;
            }

#if HURTWORLD
            var safePos = simData.FirePositionWorldSpace + direction * hit.distance;
            destination = new GenericPosition(safePos.x, safePos.y, safePos.z);
#elif REIGNOFKINGS || RUST || RUSTLEGACY || THEFOREST
            destination = new GenericPosition(hit.point.x, hit.point.y, hit.point.z);
#endif
            protection.Add(player.Id); // TODO: Remove to reset before adding if using timer?
            player.Teleport(destination.X, destination.Y, destination.Z);
        }

        #endregion Port Command

        #region Damage Protection

#if HURTWORLD
        private void OnPlayerTakeDamage(PlayerSession session, EntityEffectSourceData source)
        {
            var id = session.SteamId.ToString();
            if (!protection.Contains(id)) return;

            source.Value = 0f;
            timer.Once(10f, () => protection.Remove(id)); // TODO: Detect ground instead of timer
        }
#elif RUST
        private void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            var basePlayer = entity as BasePlayer;
            if (basePlayer == null || !protection.Contains(basePlayer.UserIDString)) return;

            info.damageTypes = new Rust.DamageTypeList();
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
            timer.Once(10f, () => protection.Remove(basePlayer.UserIDString)); // TODO: Detect ground instead of timer
        }
#endif

        #endregion Damage Protection

        #region Helpers

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.Equals(key)))
                    if (!string.IsNullOrEmpty(message.Value)) AddCovalenceCommand(message.Value, command);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion Helpers
    }
}
