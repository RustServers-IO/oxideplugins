/*
TODO:
- Add config options to customize sign color, size, etc.
- Add event countdown and player count signs for Zone Manager / Event Manager
- Move all messages to config for localization
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DynamicSigns", "Wulf/lukespragg", "0.2.0", ResourceId = 1462)]
    [Description("Creates dynamic signs that show server stats and more")]
    public class DynamicSigns : CovalencePlugin
    {
        #region Initialization

        private const string permAdmin = "dynamicsigns.admin";
        private bool autoLock;

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            Config["Automatically lock signs (true/false)"] = autoLock = GetConfig("Automatically lock signs (true/false)", true);
            SaveConfig();
        }

        #endregion

        #region Stored data

        private static StoredData storedData;

        private class StoredData
        {
            public Dictionary<uint, SignData> Signs = new Dictionary<uint, SignData>();
            public int Deaths;
        }

        private class SignData
        {
            public uint TextureId;
            /*public string SignColor;
            public string TextColor;
            public int Width;
            public int Height;*/

            public SignData()
            {
            }

            public SignData(Signage sign)
            {
                TextureId = sign.textureID;
                /*SignColor = "ffffff";
                TextColor = "000000";
                Width = 0;
                Height = 0;*/
            }
        }

        #endregion

        #region Localization

        private void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Already dyanmic"] = "Already a dynamic sign",
                ["Death plural"] = "deaths",
                ["Death singular"] = "death",
                ["No signs found"] = "No usable signs could be found",
                ["Not allowed"] = "You are not allowed to use the '{0}' command",
                ["Sign created"] = "Sign created as a {0} sign"
            }, this);
        }

        #endregion

        private void OnServerInitialized()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permAdmin, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            Puts($"{storedData.Signs.Count} dynamic signs found");

            webObject = new GameObject("WebObject");
            uWeb = webObject.AddComponent<UnityWeb>();
        }

        #endregion

        #region Image downloading

        private GameObject webObject;
        private UnityWeb uWeb;

        private class QueueItem
        {
            public string url;
            public IPlayer sender;
            public Signage sign;
            public string type;

            public QueueItem(string ur, IPlayer se, Signage si, string ty)
            {
                url = ur;
                sender = se;
                sign = si;
                type = ty;
            }
        }

        private class UnityWeb : MonoBehaviour
        {
            private const int maxActiveLoads = 3;
            private static readonly List<QueueItem> QueueList = new List<QueueItem>();
            private static byte activeLoads;

            public void Add(string url, IPlayer player, Signage sign, string type)
            {
                QueueList.Add(new QueueItem(url, player, sign, type));
                if (activeLoads < maxActiveLoads) Next();
            }

            private void Next()
            {
                activeLoads++;
                var qi = QueueList[0];
                QueueList.RemoveAt(0);
                var www = new WWW(qi.url);
                StartCoroutine(WaitForRequest(www, qi));
            }

            private IEnumerator WaitForRequest(WWW www, QueueItem info)
            {
                yield return www;
                var player = info.sender;

                if (www.error == null)
                {
                    var sign = info.sign;
                    if (sign.textureID > 0U) FileStorage.server.Remove(sign.textureID, FileStorage.Type.png, sign.net.ID);

                    var stream = new MemoryStream();
                    stream.Write(www.bytes, 0, www.bytes.Length);
                    sign.textureID = FileStorage.server.Store(stream, FileStorage.Type.png, sign.net.ID, 0U);
                    sign.SendNetworkUpdate();

                    //player.Reply(Lang("SignCreated", player.Id));
                }
                else
                    player.Reply(www.error);

                activeLoads--;
                if (QueueList.Count > 0) Next();
            }
        }

        #endregion

        private string SignImage()
        {
            string text = $"{storedData.Deaths}+{(storedData.Deaths == 1 ? Lang("Death singular") : Lang("Death plural"))}"; // TODO: Localization
            const string signColor = "ffffff"; // TODO: Move to config
            const string textColor = "000000"; // TODO: Move to config
            const int textSize = 80; // TODO: Move to config
            const int width = 350; // TODO: Move to config
            const int height = 150; // TODO: Move to config

            return $"http://placeholdit.imgix.net/~text?bg={signColor}&txtclr={textColor}&txtsize={textSize}&txt={text}&w={width}&h={height}";
        }

        private void CreateSign(IPlayer player, Signage sign, string type)
        {
            uWeb.Add(SignImage(), player, sign, type);
            if (!autoLock) return;

            sign.SetFlag(BaseEntity.Flags.Locked, true);
            sign.SendNetworkUpdate();
        }

        #region Commands

        [Command("dsign", "dynsign", "dynamicsign")]
        private void DynamicSignCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                player.Reply(Lang("Not allowed", player.Id, command));
                return;
            }

            RaycastHit hit;
            Signage sign = null;
            var basePlayer = player.Object as BasePlayer;
            if (Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, 2f)) sign = hit.transform.GetComponentInParent<Signage>();
            if (sign == null)
            {
                player.Reply(Lang("No signs found", player.Id));
                return;
            }

            if (storedData.Signs.ContainsKey(sign.net.ID))
            {
                player.Reply(Lang("Already a dynamic sign", player.Id));
                return;
            }

            CreateSign(player, sign, args.Length >= 1 ? args[0] : "death");
            var info = new SignData(sign);
            storedData.Signs.Add(sign.net.ID, info);
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        #endregion

        #region Sign updating/cleanup

        private void OnEntityDeath(BaseEntity entity)
        {
            var player = entity as BasePlayer;
            if (player != null)
            {
                storedData.Deaths++;

                var signs = 0;
                foreach (var id in storedData.Signs)
                {
                    var sign = BaseNetworkable.serverEntities.Find(id.Key) as Signage;
                    if (sign == null) continue;

                    CreateSign(players.FindPlayerByObj(player), sign, "death");
                    signs++;
                }

                Puts($"{storedData.Deaths} {(storedData.Deaths == 1 ? Lang("Death singular") : Lang("Death plural"))}, {signs} signs updated");
            }

            var signage = entity as Signage;
            if (signage) storedData.Signs.Remove(signage.net.ID);

            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        private void Unload() => UnityEngine.Object.Destroy(webObject);

        #endregion

        #region Helpers

        private T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
