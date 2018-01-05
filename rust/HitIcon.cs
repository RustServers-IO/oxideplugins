using UnityEngine;
using System;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.IO;
namespace Oxide.Plugins {
    [Info("HitIcon", "serezhadelaet", "1.5.6", ResourceId = 1917)]
    [Description("Configurable precached icon when you hit player|friend|clanmate")]
    class HitIcon : RustPlugin {

        #region Variables
        private int _dmgTextSize;
        private static float _timeToDestroy;
        private bool _showDeathSkull;
        private bool _useFriends;
        private bool _useClans;
        private bool _useSound;
        private bool _changed;
        private bool _showNpc;
        private bool _friendAPI = false;
        private bool _clansAPI = false;
        private bool _showDamage;
        private bool _showClanDamage;
        private bool _showFriendDamage;
        private bool _showHit;
        private string _colorDeath;
        private string _colorNpc;
        private string _colorFriend;
        private string _colorHead;
        private string _colorBody;
        private string _colorClan;
        private string _colorDamage;
        private string _mateSound;
        [PluginReference]
        private Plugin Friends;
        [PluginReference]
        Plugin Clans;
        private ImageCache _imageAssets;
        private GameObject _hitObject;
        private StoredData storedData;
        private Dictionary<ulong, UIHandler> _playersUIHandler = new Dictionary<ulong, UIHandler>();
        #endregion

        #region API
        private void InitializeAPI() {
            if (Friends != null) {
                _friendAPI = true;
                PrintWarning("Plugin Friends work with HitIcon");
            }
            if (Clans != null) {
                _clansAPI = true;
                PrintWarning("Plugin Clans work with HitIcon");
            }
        }
        private bool AreFriendsAPIFriend(string playerId, string friendId) {
            try {
                bool result = (bool)Friends?.CallHook("AreFriends", playerId, friendId);
                return result;
            } catch {
                return false;
            }
        }
        private bool AreClanMates(ulong playerID, ulong victimID) {
            var playerTag = Clans.Call<string>("GetClanOf", playerID);
            var victimTag = Clans.Call<string>("GetClanOf", victimID);
            if (playerTag != null)
                if (victimTag != null)
                    if (playerTag == victimTag) return true;
            return false;
        }
        #endregion

        #region Lang&Config&Data
        private void InitLanguage() {
            lang.RegisterMessages(new Dictionary<string, string> {
                {"Enabled", "Hit icon was <color=green>enabled</color>"},
                {"Disabled", "Hit icon was <color=red>disabled</color>"}
            }, this);
        }
        private object GetConfig(string menu, string datavalue, object defaultValue) {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null) {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                _changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value)) {
                value = defaultValue;
                data[datavalue] = value;
                _changed = true;
            }
            return value;
        }
        private void LoadVariables() {
            _colorClan = Convert.ToString(GetConfig("Color", "Hit clanmate color", "0 1 0 1"));
            _colorFriend = Convert.ToString(GetConfig("Color", "Hit friend color", "0 1 0 1"));
            _colorHead = Convert.ToString(GetConfig("Color", "Hit head color", "1 0 0 1"));
            _colorBody = Convert.ToString(GetConfig("Color", "Hit body color", "1 1 1 1"));
            _colorNpc = Convert.ToString(GetConfig("Color", "Hit body color", "1 1 1 1"));
            _colorDeath = Convert.ToString(GetConfig("Color", "Hit body color", "1 0 0 1"));
            _colorDamage = Convert.ToString(GetConfig("Color", "Text damage color", "1 1 1 1"));
            _dmgTextSize = Convert.ToInt32(GetConfig("Configuration", "Damage text size", 15));
            _useFriends = Convert.ToBoolean(GetConfig("Configuration", "Use Friends", true));
            _useClans = Convert.ToBoolean(GetConfig("Configuration", "Use Clans", true));
            _useSound = Convert.ToBoolean(GetConfig("Configuration", "Use sound when mate get attacked", true));
            _showHit = Convert.ToBoolean(GetConfig("Configuration", "Show hit icon", true));
            _showNpc = Convert.ToBoolean(GetConfig("Configuration", "Show hits/deaths on NPC (Bears, wolfs, etc.)", false));
            _showDamage = Convert.ToBoolean(GetConfig("Configuration", "Show damage", true));
            _showDeathSkull = Convert.ToBoolean(GetConfig("Configuration", "Show death skull", true));
            _showClanDamage = Convert.ToBoolean(GetConfig("Configuration", "Show clanmate damage", false));
            _showFriendDamage = Convert.ToBoolean(GetConfig("Configuration", "Show friend damage", true));
            _mateSound = Convert.ToString(GetConfig("Configuration", "When mate get attacked sound fx", "assets/prefabs/instruments/guitar/effects/guitarpluck.prefab"));
            _timeToDestroy = Convert.ToSingle(GetConfig("Configuration", "Time to destroy", 0.45f));
            if (_changed) {
                SaveConfig();
                _changed = false;
            }
        }
        protected override void LoadDefaultConfig() {
            Puts("Creating a new configuration file!");
            Config.Clear();
            LoadVariables();
        }
        private class StoredData {
            public List<ulong> DisabledUsers = new List<ulong>();
        }
        private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("HitIcon", storedData);
        private void LoadData() {
            try {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("HitIcon");
            } catch {
                storedData = new StoredData();
            }
        }
        #endregion

        #region ChatCommand
        [ChatCommand("hit")]
        private void ToggleHit(BasePlayer player) {
            if (!storedData.DisabledUsers.Contains(player.userID)) {
                storedData.DisabledUsers.Add(player.userID);
                PrintToChat(player, lang.GetMessage("Disabled", this, player.UserIDString));
            } else {
                storedData.DisabledUsers.Remove(player.userID);
                PrintToChat(player, lang.GetMessage("Enabled", this, player.UserIDString));
            }
        }
        #endregion

        #region ImageDownloader
        private void CacheImage() {
            _hitObject = new GameObject();
            _imageAssets = _hitObject.AddComponent<ImageCache>();
            _imageAssets.imageFiles.Clear();
            string dataDirectory = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar;
            _imageAssets.GetImage("hitimage", dataDirectory + "hit.png");
            _imageAssets.GetImage("deathimage", dataDirectory + "death.png");
            Download();
        }
        class ImageCache : MonoBehaviour {
            public Dictionary<string, string> imageFiles = new Dictionary<string, string>();
            private List<Queue> queued = new List<Queue>();
            class Queue {
                public string Url { get; set; }
                public string Name { get; set; }
            }
            public void OnDestroy() {
                foreach (var value in imageFiles.Values) {
                    FileStorage.server.RemoveEntityNum(uint.MaxValue, Convert.ToUInt32(value));
                }
            }
            public void GetImage(string name, string url) {
                queued.Add(new Queue {
                    Url = url,
                    Name = name
                });
            }
            IEnumerator WaitForRequest(Queue queue) {
                using (var www = new WWW(queue.Url)) {
                    yield return www;

                    if (string.IsNullOrEmpty(www.error))
                        imageFiles.Add(queue.Name, FileStorage.server.Store(www.bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString());
                    else {
                        Debug.LogWarning("\n\n!!!!!!!!!!!!!!!!!!!!!\n\nError downloading image files (death.png and hit.png)\nThey must be in your oxide/data/ !\n\n!!!!!!!!!!!!!!!!!!!!!\n\n");
                        ConsoleSystem.Run(ConsoleSystem.Option.Unrestricted, "oxide.unload HitIcon");
                    }
                }
            }
            public void Process() {
                for (int i = 0; i < 2; i++)
                    StartCoroutine(WaitForRequest(queued[i]));
            }
        }
        private string FetchImage(string name) {
            string result;
            if (_imageAssets.imageFiles.TryGetValue(name, out result))
                return result;
            return string.Empty;
        }
        private void Download() => _imageAssets.Process();
        #endregion

        #region CUI
        private void Png(BasePlayer player, string uiname, string image, string start, string end, string colour) {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement {
                Name = uiname,
                Components = {
                    new CuiRawImageComponent {
                        Png = image,
                        Color = colour,
                        Sprite = "assets/content/textures/generic/fulltransparent.tga"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = start,
                        AnchorMax = end
                    }
                }
            });
            CuiHelper.AddUi(player, container);
        }
        private void Dmg(BasePlayer player, string uiname, string uitext, string start, string end, string uicolor, int uisize) {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement {
                Name = uiname,
                Components = {
                        new CuiTextComponent {
                            Text = uitext,
                            FontSize = uisize,
                            Font = "robotocondensed-regular.ttf",
                            Color = uicolor,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = start,
                            AnchorMax = end
                        }
                    }
            });
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Oxide
        private void OnPlayerDisconnected(BasePlayer player) {
            UIHandler value;
            if (!_playersUIHandler.TryGetValue(player.userID, out value)) return;
            _playersUIHandler[player.userID]?.Destroy();
            _playersUIHandler.Remove(player.userID);
        }
        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo) => SendHit(attacker, hitinfo);
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info) => SendDeath(entity, info);
        private void OnServerInitialized() {
            CacheImage();
            InitializeAPI();
            foreach (var player in BasePlayer.activePlayerList)
                GetUIHandler(player);
        }
        private void Loaded() {
            LoadData();
            InitLanguage();
            LoadVariables();
        }
        private void Unload() {
            foreach (BasePlayer player in BasePlayer.activePlayerList) {
                player.GetComponent<UIHandler>()?.Destroy();
                CuiHelper.DestroyUi(player, "hitpng");
                CuiHelper.DestroyUi(player, "hitdmg");
            }
            SaveData();
            UnityEngine.Object.Destroy(_hitObject);
        }
        #endregion

        #region GuiHandler
        class UIHandler : MonoBehaviour {
            public BasePlayer player;
            public bool isDestroyed = false;
            private void Awake() {
                player = GetComponent<BasePlayer>();
            }
            public void DestroyUI() {
                if (!isDestroyed) {
                    CancelInvoke("DestroyUI");
                    CuiHelper.DestroyUi(player, "hitdmg");
                    CuiHelper.DestroyUi(player, "hitpng");
                    Invoke("DestroyUI", _timeToDestroy);
                    isDestroyed = true;
                    return;
                }
                CuiHelper.DestroyUi(player, "hitdmg");
                CuiHelper.DestroyUi(player, "hitpng");
            }
            public void Destroy() => UnityEngine.Object.Destroy(this);
        }

        #endregion

        #region Helpers
        private void SendHit(BasePlayer attacker, HitInfo hitinfo) {
            if (hitinfo == null || attacker == null || !attacker.IsConnected) return;
            if (storedData.DisabledUsers.Contains(attacker.userID)) return;
            if (hitinfo.HitEntity is BaseNpc && _showNpc) {
                GuiDisplay(attacker, _colorNpc, hitinfo);
                return;
            }
            var victim = hitinfo.HitEntity as BasePlayer;
            if (victim == null) return;
            if (victim == attacker) return;
            if (_useClans && _clansAPI) {
                if (AreClanMates(attacker.userID, victim.userID)) {
                    GuiDisplay(attacker, _colorClan, hitinfo, false, "clans");
                    if (_useSound)
                        EffectNetwork.Send(new Effect(_mateSound, attacker.transform.position, Vector3.zero), attacker.net.connection);
                    return;
                }
            }
            if (_friendAPI && _useFriends && AreFriendsAPIFriend(victim.userID.ToString(), attacker.userID.ToString())) {
                GuiDisplay(attacker, _colorFriend, hitinfo, false, "friends");
                if (_useSound)
                    EffectNetwork.Send(new Effect(_mateSound, attacker.transform.position, Vector3.zero), attacker.net.connection);
                return;
            }
            if (hitinfo.isHeadshot) {
                GuiDisplay(attacker, _colorHead, hitinfo);
                return;
            }
            GuiDisplay(attacker, _colorBody, hitinfo);
        }
        private void SendDeath(BaseCombatEntity entity, HitInfo info) {
            if (info == null || entity == null) return;
            if (!_showDeathSkull) return;
            var initiator = (info?.Initiator as BasePlayer);
            if (initiator == null) return;
            if (storedData.DisabledUsers.Contains(initiator.userID)) return;
            var npc = (entity as BaseNpc);
            if (npc != null) {
                if (_showNpc) {
                    NextTick(() => GuiDisplay(initiator, _colorBody, info, true)); //npc death
                    return;
                }
            }
            var player = entity as BasePlayer;
            if (player == null) return;
            if (player == initiator) return;
            NextTick(() => GuiDisplay(initiator, _colorBody, info, true)); //death
        }
        private void GuiDisplay(BasePlayer player, string color, HitInfo hitinfo, bool isKill = false, string whatIsIt = "") {
            var uiHandler = GetUIHandler(player);
            uiHandler.isDestroyed = false;
            uiHandler.DestroyUI();
            if (isKill)
                Png(player, "hitpng", FetchImage("deathimage"), "0.487 0.482", "0.513 0.518", _colorDeath); //death png
            if (_showHit && !isKill)
                Png(player, "hitpng", FetchImage("hitimage"), "0.492 0.4905", "0.506 0.5095", color); // hit png
            if (_showDamage) {
                NextTick(() => {
                    if (whatIsIt == "clans" && !_showClanDamage) return;
                    if (whatIsIt == "friends" && !_showFriendDamage) return;
                    if (!isKill && !_showDeathSkull || !isKill) {
                        CuiHelper.DestroyUi(player, "hitdmg");
                        float damage = (int)hitinfo.damageTypes.Total();
                        Dmg(player, "hitdmg", damage.ToString(), "0.45 0.45", "0.55 0.50", _colorDamage, _dmgTextSize);
                    }
                });
            }
        }
        private UIHandler GetUIHandler(BasePlayer player) {
            UIHandler value;
            if (!_playersUIHandler.TryGetValue(player.userID, out value)) {
                _playersUIHandler[player.userID] = player.gameObject.AddComponent<UIHandler>();
                return _playersUIHandler[player.userID];
            }
            return value;
        }
        #endregion
    }
}