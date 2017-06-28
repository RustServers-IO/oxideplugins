using System.Collections.Generic;
using System.Linq;
#if HURTWORLD || RUST
using UnityEngine;
#endif
#if HURTWORLD
using uLink;
#endif
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Portgun", "Wulf/lukespragg", "3.0.0", ResourceId = 664)]
    [Description("Teleports a player to where they are looking at")]
    public class Portgun : CovalencePlugin
    {
        #region Localization

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["CommandAlias"] = "port",
                ["NoDestinationFound"] = "Could not find a valid destination to port to",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command"
            }, this);
        }

        #endregion

        #region Initialization

        private const string permUse = "portgun.use";

        private int layers;

        private void OnServerInitialized()
        {
            AddCommandAliases("CommandAlias", "PortCommand");
            AddCovalenceCommand("portgun", "PortCommand");
            permission.RegisterPermission(permUse, this);

#if HURTWORLD
            layers = LayerMask.GetMask("Constructions", "Terrain");
#endif
#if RUST
            layers = LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Tree", "Water", "World");
#endif
        }

        #endregion

        #region Port Command

        private void PortCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            var pos = new GenericPosition();
#if HURTWORLD
            RaycastHit hit;
            var entity = (player.Object as PlayerSession).WorldPlayerEntity;
            var thirdPerson = entity.GetComponentInChildren<Assets.Scripts.Core.CamPosition>().transform;
            var rotation = thirdPerson.rotation * Vector3.forward;
            if (!Physics.Raycast(entity.transform.position + new Vector3(0f, 1.5f, 0f), rotation, out hit, float.MaxValue, layers))
#endif
#if RUST
            RaycastHit hit;
            var basePlayer = player.Object as BasePlayer;
            if (!Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, UnityEngine.Mathf.Infinity, layers))
#endif
            {
                player.Reply(Lang("NoDestinationFound", player.Id));
                return;
            }

#if HURTWORLD || RUST
            pos = new GenericPosition(hit.point.x, hit.point.y, hit.point.z);
#endif
            player.Teleport(pos.X, pos.Y, pos.Z);
        }

        #endregion

        #region Helpers

        private void AddCommandAliases(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.StartsWith(key))) AddCovalenceCommand(message.Value, command);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
