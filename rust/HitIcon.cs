using UnityEngine;
using System;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.IO;
using System.Linq;
using Rust;
namespace Oxide.Plugins {
	[Info("HitIcon", "serezhadelaet", "1.5", ResourceId = 1917)]
    [Description("Configurable precached icon when you hit player|friend|clanmate")]
    class HitIcon : RustPlugin {

        #region Variables
        int dmgtextsize;
        public static float timetodestroy;
        bool showdeathskull;
        bool usefriends;
        bool useclans;
        bool usesound;
        bool Changed;
        bool shownpc;
        bool friendapi = false;
        bool clansapi = false;
        bool showdmg;
        bool showclandmg;
        bool showfrienddmg;
        bool showhit;
        string colordeath;
        string colornpc;
        string colorfriend;
        string colorhead;
        string colorbody;
        string colorclan;
        string dmgcolor;
        string endcolor;
        string matesound;
        [PluginReference]
        private Plugin Friends;
        [PluginReference]
        Plugin Clans;
        ImageCache ImageAssets;
        GameObject HitObject;
        #endregion

        #region API
        private void InitializeAPI()
        {
            if (Friends != null)
            {
                friendapi = true;
                PrintWarning("Plugin Friends work with HitIcon");
            }
            if (Clans != null)
            {
                clansapi = true;
                PrintWarning("Plugin Clans work with HitIcon");
            }
        }
        private bool AreFriendsAPIFriend(string playerId, string friendId)
        {
            try
            {
                bool result = (bool)Friends?.CallHook("AreFriends", playerId, friendId);
                return result;
            }
            catch
            {
                return false;
            }
        }
        public bool AreClanMates(ulong playerID, ulong victimID)
        {
            var playerTag = Clans.Call<string>("GetClanOf", playerID);
            var victimTag = Clans.Call<string>("GetClanOf", victimID);
            if (playerTag != null)
            {
                if (victimTag != null)
                {
                    if (playerTag == victimTag) return true;
                }
            }
            return false;
        }
        #endregion

        #region Lang&Config
        void language()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Enabled", "Hit icon was <color=green>enabled</color>"},
                {"Disabled", "Hit icon was <color=red>disabled</color>"}
            }, this);
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

        void LoadVariables()
        {
            colorclan = Convert.ToString(GetConfig("Color", "Hit clanmate color", "0 1 0 1"));
            colorfriend = Convert.ToString(GetConfig("Color", "Hit friend color", "0 1 0 1"));
            colorhead = Convert.ToString(GetConfig("Color", "Hit head color", "1 0 0 1"));
            colorbody = Convert.ToString(GetConfig("Color", "Hit body color", "1 1 1 1"));
            colornpc = Convert.ToString(GetConfig("Color", "Hit body color", "1 1 1 1"));
            colordeath = Convert.ToString(GetConfig("Color", "Hit body color", "1 0 0 1"));
            dmgcolor = Convert.ToString(GetConfig("Color", "Text damage color", "1 1 1 1"));
            dmgtextsize = Convert.ToInt32(GetConfig("Configuration", "Damage text size", 15));
            usefriends = Convert.ToBoolean(GetConfig("Configuration", "Use Friends", true));
            useclans = Convert.ToBoolean(GetConfig("Configuration", "Use Clans", true));
            usesound = Convert.ToBoolean(GetConfig("Configuration", "Use sound when mate get attacked", true));
            showhit = Convert.ToBoolean(GetConfig("Configuration", "Show hit icon", true));
            shownpc = Convert.ToBoolean(GetConfig("Configuration", "Show hits/deaths on NPC (Bears, wolfs, etc.)", false));
            showdmg = Convert.ToBoolean(GetConfig("Configuration", "Show damage", true));
            showdeathskull = Convert.ToBoolean(GetConfig("Configuration", "Show death skull", true));
            showclandmg = Convert.ToBoolean(GetConfig("Configuration", "Show clanmate damage", false));
            showfrienddmg = Convert.ToBoolean(GetConfig("Configuration", "Show friend damage", true));
            matesound = Convert.ToString(GetConfig("Configuration", "When mate get attacked sound fx", "assets/prefabs/instruments/guitar/effects/guitarpluck.prefab"));
            timetodestroy = Convert.ToSingle(GetConfig("Configuration", "Time to destroy", 0.45f));
            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file!");
            Config.Clear();
            LoadVariables();
        }
        #endregion

        #region Data
        public class StoredData
        {
            public List<ulong> DisabledUsers = new List<ulong>();
        }
        static StoredData storedData;
        
        
		static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("HitIcon", storedData);
		static void LoadData()
        {
			try
            {
				storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("HitIcon");
			}
			catch
            {
				storedData = new StoredData();
			}
		}
        #endregion

        #region ChatCommand
        [ChatCommand("hit")]
        void toggle(BasePlayer player)
        {
            if (!storedData.DisabledUsers.Contains(player.userID))
            {
                storedData.DisabledUsers.Add(player.userID);
                PrintToChat(player, lang.GetMessage("Disabled", this, player.UserIDString));
            }
            else
            {
                storedData.DisabledUsers.Remove(player.userID);
                PrintToChat(player, lang.GetMessage("Enabled", this, player.UserIDString));
            }
        }
        #endregion

        #region ImageDownloader

        private void cacheImage()
        {
            HitObject = new GameObject();
            ImageAssets = HitObject.AddComponent<ImageCache>();
            ImageAssets.imageFiles.Clear();
            string dataDirectory = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar;
            ImageAssets.getImage("hitimage", dataDirectory + "hit.png");
            ImageAssets.getImage("deathimage", dataDirectory + "death.png");
            download();
        }

        class ImageCache : MonoBehaviour
        {
            public Dictionary<string, string> imageFiles = new Dictionary<string, string>();

            List<Queue> queued = new List<Queue>();

            class Queue
            {
                public string url { get; set; }
                public string name { get; set; }
            }

            public void OnDestroy()
            {
                foreach (var value in imageFiles.Values)
                {
                    FileStorage.server.RemoveEntityNum(uint.MaxValue, Convert.ToUInt32(value));
                }
            }

            public void getImage(string name, string url)
            {
                queued.Add(new Queue
                {
                    url = url,
                    name = name
                });
            }

            IEnumerator WaitForRequest(Queue queue)
            {
                using (var www = new WWW(queue.url))
                {
                    yield return www;

                    if (string.IsNullOrEmpty(www.error))
                    {
                        var stream = new MemoryStream();
                        stream.Write(www.bytes, 0, www.bytes.Length);
                        imageFiles.Add(queue.name, FileStorage.server.Store(stream, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString());
                    }
                    else
                    {
                        Debug.LogWarning("\n\n!!!!!!!!!!!!!!!!!!!!!\n\nError downloading image files (death.png and hit.png)\nThey must be in your oxide/data/ !\n\n!!!!!!!!!!!!!!!!!!!!!\n\n");
                        ConsoleSystem.Run.Server.Normal("oxide.unload HitIcon");
                    }
                }
            }

           public void process()
            {
                for(int i = 0; i < 2; i++)
                StartCoroutine(WaitForRequest(queued[i]));
            }
        }

        public string fetchImage(string name)
        {
            string result;
            if (ImageAssets.imageFiles.TryGetValue(name, out result))
                return result;
            return string.Empty;
        }

        void download()
        {
            ImageAssets.process();
        }
        #endregion

        #region GUI
        public class GUIv4
        {
            public string guiname { get; set; }
            public CuiElementContainer container = new CuiElementContainer();

            public void add(string uiname, string image, string start, string end, string colour)
            {
                guiname = uiname;
                CuiElement element = new CuiElement
                {
                    Name = guiname,
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
                };
                container.Add(element);
            }

            public void dmg(string uiname, string uitext, string start, string end, string uicolor, int uisize)
            {
                guiname = uiname;
                CuiElement element = new CuiElement
                {
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
                };
                container.Add(element);
            }

            public void send(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, guiname);
                CuiHelper.AddUi(player, container);
            }
        }
        #endregion

        #region Oxide

        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            if (hitinfo == null) return;
            SendHit(attacker, hitinfo);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info) => SendDeath(entity, info);
        
        void OnServerInitialized()
        {
            cacheImage();
            InitializeAPI();
        }

        void Loaded()
        {
            LoadData();
            language();
            LoadVariables();
        }

        void Unloaded()
        {
            if (BasePlayer.activePlayerList.Count > 0)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    HitGui hitGui = player.GetComponent<HitGui>();
                    if (hitGui != null)
                        hitGui.DestroyClass();
                    CuiHelper.DestroyUi(player, "hitpng");
                    CuiHelper.DestroyUi(player, "hitdmg");
                }
            }
            SaveData();
            UnityEngine.Object.Destroy(HitObject);
        }
        #endregion

        #region GUI Class
        class HitGui : MonoBehaviour
        {
            public BasePlayer player;
            public bool isDestroyed = false;
            
            void Awake()
            {
                player = GetComponent<BasePlayer>();
            }
            public void DestroyUI()
            {
                if (!isDestroyed)
                {
                    CancelInvoke("DestroyUI");
                    CuiHelper.DestroyUi(player, "hitdmg");
                    CuiHelper.DestroyUi(player, "hitpng");
                    Invoke("DestroyUI", timetodestroy);
                    isDestroyed = true;
                    return;
                }
                CuiHelper.DestroyUi(player, "hitdmg");
                CuiHelper.DestroyUi(player, "hitpng");
            }
            
            public void DestroyClass()
            {
                UnityEngine.Object.Destroy(this);
            }
        }
        #endregion

        #region MainMethods
        
        private void SendHit(BasePlayer attacker, HitInfo hitinfo)
        {
            var npc = hitinfo.HitEntity as BaseNPC;
            if (npc != null) //NPC
            {
                if (shownpc) GuiDisplay(attacker, colornpc, hitinfo);
                return;
            }
            var victim = hitinfo.HitEntity as BasePlayer;
            if (victim == null) return;
            if (victim == attacker) return;
            if (storedData.DisabledUsers.Contains(attacker.userID)) return;
            
            if (useclans && clansapi) //clans
            {
                if (AreClanMates(attacker.userID, victim.userID))
                {
                    GuiDisplay(attacker, colorclan, hitinfo, false, "clans");
                    if (usesound) Effect.server.Run(matesound, attacker.transform.position, Vector3.zero, null, false);
                    return;
                }
            }

            if (friendapi && usefriends && AreFriendsAPIFriend(victim.userID.ToString(), attacker.userID.ToString())) //friends
            {
                GuiDisplay(attacker, colorfriend, hitinfo, false, "friends");
                if (usesound) Effect.server.Run(matesound, attacker.transform.position, Vector3.zero, null, false);
                return;
            }

            if (hitinfo.isHeadshot) //head
            {
                GuiDisplay(attacker, colorhead, hitinfo);
                return;
            }
            GuiDisplay(attacker, colorbody, hitinfo); //body
        }

        void SendDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!showdeathskull) return;
            var npc = (entity as BaseNPC);
            var initiator = (info?.Initiator as BasePlayer);
            if (initiator == null) return;
            if (storedData.DisabledUsers.Contains(initiator.userID)) return;
            if (npc != null)
            {
                if (shownpc)
                {
                    NextTick(() => GuiDisplay(initiator, colorbody, info, true)); //npc death
                    return;
                }
            }
            var player = (entity as BasePlayer);
            if (player == null) return;
            if (player == initiator) return;
            NextTick(() => GuiDisplay(initiator, colorbody, info, true)); //death
        }

        void GuiDisplay(BasePlayer player, string color, HitInfo hitinfo, bool isKill = false, string whatIsIt = "")
        {
            HitGui hitGui = player.GetComponent<HitGui>() ?? player.gameObject.AddComponent<HitGui>();
            hitGui.player = player;
            hitGui.isDestroyed = false;
            hitGui.DestroyUI();
            GUIv4 gui = new GUIv4();
            if (isKill)
            {
                gui.add("hitpng", fetchImage("deathimage"), "0.487 0.482", "0.513 0.518", colordeath); //death png
                gui.send(player);
            }
            if (showhit && !isKill)
            {
                gui.add("hitpng", fetchImage("hitimage"), "0.492 0.4905", "0.506 0.5095", color); // hit png
                gui.send(player);
            }
            if (showdmg)
            {
                NextTick(() => 
                {
                    if (whatIsIt == "clans" && !showclandmg) return;
                    if (whatIsIt == "friends" && !showfrienddmg) return;
                    if (!isKill && !showdeathskull || !isKill)
                    {
                        CuiHelper.DestroyUi(player, "hitdmg");
                        float damage = (int)hitinfo.damageTypes.Total();
                        gui.dmg("hitdmg", damage.ToString(), "0.45 0.45", "0.55 0.50", dmgcolor, dmgtextsize);
                        gui.send(player);
                    }
                });
            }
        }

        #endregion
    }
}	