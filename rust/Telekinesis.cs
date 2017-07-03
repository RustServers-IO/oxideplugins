using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Telekinesis", "redBDGR", "2.0.0")]
    [Description("Control objects with your mind!")]

    class Telekinesis : RustPlugin
    {
        bool Changed = false;
        static Telekinesis plugin = null;
        const string permissionName = "telekinesis.use";
        const string permissionNameRESTRICTED = "telekinesis.restricted";

        float maxDist = 3f;
        float autoDisableLength = 60f;

        Dictionary<string, BaseEntity> grabList = new Dictionary<string, BaseEntity>();

        void Init()
        {
            plugin = this;
            permission.RegisterPermission(permissionName, this);
            permission.RegisterPermission(permissionNameRESTRICTED, this);
            LoadVariables();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You are not allowed to use this command!",
                ["Grab tool start"] = "The telekinesis tool has been enabled",
                ["Grab tool end"] = "The telekinesis tool has been disabled",
                ["Invalid entity"] = "No valid entity was found",
                ["Building Blocked"] = "You are not allowed to use this tool if you are building blocked",

            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            maxDist = Convert.ToSingle(GetConfig("Settings", "Restricted max distance", 3f));
            autoDisableLength = Convert.ToSingle(GetConfig("Settings", "Auto disable length", 60f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        [ChatCommand("tls")]
        void GrabCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (permission.UserHasPermission(player.UserIDString, permissionNameRESTRICTED))
                if (!player.CanBuild())
                {
                    player.ChatMessage(msg("Building Blocked", player.UserIDString));
                    return;
                }
            if (grabList.ContainsKey(player.UserIDString))
            {
                BaseEntity ent = grabList[player.UserIDString];
                TelekinesisComponent grab = ent.GetComponent<TelekinesisComponent>();
                if (grab)
                    grab.DestroyThis();
                grabList.Remove(player.UserIDString);
                return;
            }
            if (GrabEntity(player) == false)
            {
                player.ChatMessage(msg("Invalid entity", player.UserIDString));
                return;
            }
            player.ChatMessage(msg("Grab tool start", player.UserIDString));
            return;
        }

        bool GrabEntity(BasePlayer player)
        {
            BaseEntity ent = FindEntity(player);
            if (ent == null)
                return false;
            TelekinesisComponent grab = ent.gameObject.AddComponent<TelekinesisComponent>();
            grab.originPlayer = player;
            grabList.Add(player.UserIDString, ent);
            timer.Once(autoDisableLength, () =>
            {
                if (grab)
                    grab.DestroyThis();
            });
            return true;
        }

        BaseEntity FindEntity(BasePlayer player)
        {
            RaycastHit hit;
            if (UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hit))
            {
                if (hit.GetEntity() == null)
                    return null;
                BaseEntity entity = hit.GetEntity();
                if (entity == null)
                    return null;
                else
                    return entity;
            }
            return null;
        }

        class TelekinesisComponent : MonoBehaviour
        {
            public BasePlayer originPlayer = null;
            BaseNetworkable target;
            float entDis = 2f;
            float maxDis = plugin.maxDist;
            bool isRestricted = false;

            private void Awake()
            {
                target = gameObject.GetComponent<BaseNetworkable>();
                plugin.NextTick(() =>
                {
                    if (originPlayer)
                        if (plugin.permission.UserHasPermission(originPlayer.UserIDString, permissionNameRESTRICTED))
                            isRestricted = true;
                });
            }

            private void Update()
            {
                if (originPlayer == null)
                    return;
                if (!originPlayer.CanBuild())
                    DestroyThis();
                if (originPlayer.serverInput.IsDown(BUTTON.FIRE_PRIMARY))
                    if (isRestricted)
                    {
                        if (entDis <= maxDis)
                            entDis = entDis + 0.01f;
                    }
                    else
                        entDis = entDis + 0.01f;
                if (originPlayer.serverInput.IsDown(BUTTON.FIRE_SECONDARY))
                            entDis = entDis - 0.01f;
                Vector3 pos = Vector3.Lerp(target.transform.position, originPlayer.transform.position + originPlayer.eyes.HeadRay().direction * entDis + new Vector3(0, 1, 0), UnityEngine.Time.deltaTime * 5);
                gameObject.transform.LookAt(originPlayer.transform);
                target.transform.position = Vector3.Lerp(target.transform.position, originPlayer.transform.position + originPlayer.eyes.HeadRay().direction * entDis + new Vector3(0, 1, 0), UnityEngine.Time.deltaTime * 5);
                target.SendNetworkUpdateImmediate();
            }

            public void DestroyThis()
            {
                if (plugin.grabList.ContainsKey(originPlayer.UserIDString))
                    plugin.grabList.Remove(originPlayer.UserIDString);
                originPlayer.ChatMessage(plugin.msg("Grab tool end", originPlayer.UserIDString));
                Destroy(this);
            }
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}