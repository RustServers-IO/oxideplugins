using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Telekinesis", "redBDGR", "2.0.4", ResourceId = 823)]
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
        Dictionary<string, UndoInfo> undoDic = new Dictionary<string, UndoInfo>();

        class UndoInfo
        {
            public Vector3 pos;
            public Quaternion rot;
            public BaseEntity entity;
        }

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
                ["No Undo Found"] = "No undo data was found!",
                ["Undo Success"] = "Your last telekinesis movement was undone",
                ["TLS Mode Changed"] = "Current mode: {0}",

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

        void OnEntityKill(BaseNetworkable entity)
        {
            BaseEntity ent = entity as BaseEntity;
            if (ent == null)
                return;
            TelekinesisComponent tls = ent.GetComponent<TelekinesisComponent>();
            if (!tls)
                return;
            tls.DestroyThis();
            /*
            string x = "0";
            foreach (var entry in grabList)
                if (ent = entry.Value)
                    x = entry.Key;
            if (x != "0")
            {
                grabList.Remove(x);
                undoDic.Remove(x);
            }
            */
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
            if (args.Length == 1)
                if (args[0] == "undo")
                {
                    if (!undoDic.ContainsKey(player.UserIDString))
                    {
                        player.ChatMessage(msg("No Undo Found", player.UserIDString));
                        return;
                    }
                    if (!undoDic[player.UserIDString].entity.IsValid())
                        return;
                    undoDic[player.UserIDString].entity.transform.position = undoDic[player.UserIDString].pos;
                    undoDic[player.UserIDString].entity.transform.rotation = undoDic[player.UserIDString].rot;
                    undoDic[player.UserIDString].entity.SendNetworkUpdate();
                    player.ChatMessage(msg("Undo Success", player.UserIDString));
                    undoDic.Remove(player.UserIDString);
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
            if (undoDic.ContainsKey(player.UserIDString))
                undoDic[player.UserIDString] = new UndoInfo { pos = ent.transform.position, rot = ent.transform.rotation, entity = ent };
            else
                undoDic.Add(player.UserIDString, new UndoInfo { pos = ent.transform.position, rot = ent.transform.rotation, entity = ent });
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
            float vertOffset = 1f;
            float maxDis = plugin.maxDist;
            bool isRestricted = false;
            float nextTime;
            public string mode = "distance";

            private void Awake()
            {
                nextTime = Time.time + 0.5f;
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
                if (originPlayer.serverInput.IsDown(BUTTON.RELOAD))
                {
                    if (Time.time > nextTime)
                    {
                        if (mode == "distance")
                        {
                            mode = "rotate (horizontal)";
                            originPlayer.ChatMessage(string.Format(plugin.msg("TLS Mode Changed", originPlayer.UserIDString), mode));
                        }
                        else if (mode == "rotate (horizontal)")
                        {
                            mode = "rotate (vertical)";
                            originPlayer.ChatMessage(string.Format(plugin.msg("TLS Mode Changed", originPlayer.UserIDString), mode));
                        }
                        else if (mode == "rotate (vertical)")
                        {
                            mode = "rotate (horizontal2)";
                            originPlayer.ChatMessage(string.Format(plugin.msg("TLS Mode Changed", originPlayer.UserIDString), mode));
                        }
                        else if (mode == "rotate (horizontal2)")
                        {
                            mode = "vertical offset";
                            originPlayer.ChatMessage(string.Format(plugin.msg("TLS Mode Changed", originPlayer.UserIDString), mode));
                        }
                        else if (mode == "vertical offset")
                        {
                            mode = "distance";
                            originPlayer.ChatMessage(string.Format(plugin.msg("TLS Mode Changed", originPlayer.UserIDString), mode));
                        }
                        nextTime = Time.time + 0.5f;
                    }
                }
                if (originPlayer.serverInput.IsDown(BUTTON.FIRE_PRIMARY))
                {
                    if (mode == "distance")
                        if (isRestricted)
                        {
                            if (entDis <= maxDis)
                                entDis = entDis + 0.01f;
                        }
                        else
                            entDis = entDis + 0.01f;
                    else if (mode == "rotate (horizontal)")
                        gameObject.transform.Rotate(0, +0.5f, 0);
                    else if (mode == "rotate (vertical)")
                        gameObject.transform.Rotate(0, 0, -0.5f);
                    else if (mode == "rotate (horizontal2)")
                        gameObject.transform.Rotate(+0.5f, 0, 0);
                    else if (mode == "vertical offset")
                        vertOffset += 0.10f;
                }
                if (originPlayer.serverInput.IsDown(BUTTON.FIRE_SECONDARY))
                {
                    if (mode == "distance")
                        entDis = entDis - 0.01f;
                    else if (mode == "rotate (horizontal)")
                        gameObject.transform.Rotate(0, -0.5f, 0);
                    else if (mode == "rotate (vertical)")
                        gameObject.transform.Rotate(0, 0, +0.5f);
                    else if (mode == "rotate (horizontal2)")
                        gameObject.transform.Rotate(-0.5f, 0, 0);
                    else if (mode == "vertical offset")
                        vertOffset -= 0.10f;
                }
                //if (!rotateMode)
                    //gameObject.transform.LookAt(originPlayer.transform);
                //else
                    //gameObject.transform.Rotate(gameObject.transform.rotation.x, roty, gameObject.transform.rotation.z);
                target.transform.position = Vector3.Lerp(target.transform.position, originPlayer.transform.position + originPlayer.eyes.HeadRay().direction * entDis + new Vector3(0, vertOffset, 0), UnityEngine.Time.deltaTime * 5);
                target.SendNetworkUpdateImmediate();
            }

            public void DestroyThis()
            {
                if (plugin.grabList.ContainsKey(originPlayer.UserIDString))
                    plugin.grabList.Remove(originPlayer.UserIDString);
                if (plugin.undoDic.ContainsKey(originPlayer.UserIDString))
                    plugin.undoDic.Remove(originPlayer.UserIDString);
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