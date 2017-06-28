using System;
using System.Collections.Generic;
using System.Reflection; // enable for ListComponentDebug
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System.Collections;

namespace Oxide.Plugins {

    [Info("JPipes","TheGreatJ","0.4.0",ResourceId = 2402)]
    class JPipes : RustPlugin {

        [PluginReference]
        private Plugin FurnaceSplitter;

        private Dictionary<ulong,UserInfo> users;
        private Dictionary<ulong,jPipe> regpipes = new Dictionary<ulong,jPipe>();
        private PipeSaveData storedData;
        
        #region Hooks

        void Init() {

            lang.RegisterMessages(new Dictionary<string,string> {
                ["ErrorFindFirst"] = "Failed to find first StorageContainer",
                ["ErrorFindSecond"] = "Failed to find second StorageContainer",
                ["ErrorAlreadyConnected"] = "Error: StorageContainers are already connected",
                ["ErrorTooFar"] = "Error: StorageContainers are too far apart",
                ["ErrorTooClose"] = "Error: StorageContainers are too close together",
                ["ErrorPrivilegeAttach"] = "Error: You do not have building privilege to attach a pipe to this StorageContainer",
                ["ErrorPrivilegeModify"] = "Error: You do not have building privilege to modify this pipe",
                ["ErrorCmdPerm"] = "You don't have permission to use this command.",
                ["ErrorPipeLimitReached"] = "Error: You have reached your pipe limit of {0}",
                ["ErrorUpgradeLimit"] = "Error: You can only upgrade your pipes up to {0} level",

                ["SelectFirst"] = "Use the Hammer to select the First Container",
                ["SelectSecond"] = "Use the Hammer to select the Second Container",
                //["SelectCancel"] = "Canceled Pipe Creation",
                ["SelectSubtextBind"] = "Press [P] to Cancel",
                ["SelectSubtextCmd"] = "Do /p to Cancel",
                ["PipeCreated"] = "Pipe has been created!",

                ["CopyingTextFirst"] = "Use the Hammer to select the jPipe to copy from",
                ["CopyingText"] = "Use the Hammer to Paste",
                ["CopyingSubtext"] = "Do /pc to Exit",

                ["MenuTitle"] = "<color=#80c5ff>j</color>Pipe",
                ["MenuTurnOn"] = "Turn On",
                ["MenuTurnOff"] = "Turn Off",
                ["MenuAutoStarter"] = "Auto Starter",
                ["MenuChangeDirection"] = "Change Direction",
                ["MenuSingleStack"] = "Single Stack",
                ["MenuMultiStack"] = "Multi Stack",
                ["MenuItemFilter"] = "Item Filter",
                ["MenuInfo"] = "Owner  <color=#80c5ff>{0}</color>\nFlowrate  <color=#80c5ff>{1}/sec</color>\nLength  <color=#80c5ff>{2}</color>",

                ["HelpCmdTitle"] = "<size=28><color=#80c5ff>j</color>Pipes</size> <size=10>by TheGreatJ</size>",
                ["HelpCmdCommands"] = "<size=18>Commands</size>\n<color=#80c5ff>/p</color> create a pipe",
                ["HelpCmdMenu"] = "<size=18>Pipe Menu</size><size=12> - hit pipe with hammer to open</size>\n<color=#80c5ff>Turn On / Turn Off</color> toggle item transfer\n<color=#80c5ff>Auto Starter</color> after a pipe sends an item to a furnace, recycler, refinery, mining quarry, or pump jack, it will attempt to start it\n<color=#80c5ff>Change Direction</color> makes the items go the other direction through the pipe\n<color=#80c5ff>Multi Stack / Single Stack</color> Multi Stack mode allows the pipe to create multiple stacks of the same item. Single Stack mode prevents the pipe from creating more than one stack of an item. Single Stack mode is mostly just for fueling furnaces to leave room for other items.\n<color=#80c5ff>Item Filter</color> when items are in the filter, only those items will be transferred through the pipe. When the filter is empty, all items will be transferred.",
                ["HelpCmdUpgrade"] = "<size=18>Upgrading Pipes</size>\nUse a Hammer and upgrade the pipe just like any other building\nEach upgrade level increases the pipe's flow rate and Item Filter size.",
                ["HelpBindTip"] = "JPipes Tip:\nYou can bind the /p command to a hotkey by putting\n\"bind p jpipes.create\" into the F1 console",

                ["StatsCmd"] = "<size=20><color=#80c5ff>j</color>Pipes Stats</size>\nYou have {0} jpipes currently in use.",
                ["StatsCmdLimit"] = "<size=20><color=#80c5ff>j</color>Pipes Stats</size>\nYou have {0} of {1} jpipes currently in use."
            },this);

            LoadConfig();

            users = new Dictionary<ulong,UserInfo>();
            storedData = new PipeSaveData();
        }

        void OnServerInitialized() {
            LoadData(ref storedData);

            foreach (var p in storedData.p) {
                jPipe newpipe = new jPipe();
                if (newpipe.init(this,p.Key,p.Value,RemovePipe,MoveItem))
                    RegisterPipe(newpipe);
                else
                    PrintWarning(newpipe.initerr);
            }

            Puts($"{regpipes.Count} pipes loaded");
        }

        private void Loaded() {
            permission.RegisterPermission("jpipes.use",this);
            permission.RegisterPermission("jpipes.admin",this);


        }

        void Unload() {
            foreach (var player in BasePlayer.activePlayerList) {
                UserInfo userinfo;
                if (!users.TryGetValue(player.userID,out userinfo))
                    continue;
                if (!string.IsNullOrEmpty(userinfo.menu))
                    CuiHelper.DestroyUi(player,userinfo.menu);
                if (!string.IsNullOrEmpty(userinfo.overlay))
                    CuiHelper.DestroyUi(player,userinfo.overlay);
            }

            SavePipes();
            UnloadPipes();
        }

        void OnServerSave() => SavePipes();

        void OnPlayerInit(BasePlayer player) {

            GetUserInfo(player);

            player.SendConsoleCommand($"bind {pipehotkey} jpipes.create");
        }

        void OnPlayerDisconnected(BasePlayer player) {
            users.Remove(player.userID);
        }

        //void OnUserPermissionGranted(string id,string perm) {
        //    if (perm == "backpacks.use") {
        //        BasePlayer player = BasePlayer.Find(id)
        //    }
        //}

        void OnHammerHit(BasePlayer player,HitInfo hit) {

            //ListComponentsDebug(player, hit.HitEntity);

            UserInfo userinfo = GetUserInfo(player);

            if (hit.HitEntity.GetComponent<StorageContainer>() != null) {

                if (userinfo.isPlacing && userinfo.placeend == null && checkcontwhitelist(hit.HitEntity)) {
                    if (checkcontprivlage(hit.HitEntity,player)) {
                        // select first
                        if (userinfo.placestart == null) {
                            userinfo.placestart = hit.HitEntity;

                            ShowOverlayText(player,lang.GetMessage("SelectSecond",this,player.UserIDString),lang.GetMessage(userinfo.isUsingBind ? "SelectSubtextBind" : "SelectSubtextCmd",this,player.UserIDString));
                        } else if (userinfo.placestart != null) { // select second
                            userinfo.placeend = hit.HitEntity;
                            NewPipe(player,userinfo);
                        }
                    } else {
                        ShowOverlayText(player,lang.GetMessage("ErrorPrivilegeAttach",this,player.UserIDString));
                        timer.Once(2f,() => {
                            ShowOverlayText(player,lang.GetMessage((userinfo.placestart == null) ? "SelectFirst":"SelectSecond",this,player.UserIDString),lang.GetMessage(userinfo.isUsingBind ? "SelectSubtextBind" : "SelectSubtextCmd",this,player.UserIDString));
                        });
                    }
                }
            } else {
                jPipeSegChild s = hit.HitEntity.GetComponent<jPipeSegChild>();
                if (s != null) {
                    if (!commandperm(player))
                        return;
                    if (checkbuildingprivlage(player)) {
                        if (userinfo.isCopying) {
                            if (userinfo.clipboard == null) {

                                userinfo.clipboard = new jPipeData();
                                userinfo.clipboard.fromPipe(s.pipe);

                                ShowOverlayText(player,lang.GetMessage("CopyingText",this,player.UserIDString),lang.GetMessage("CopyingSubtext",this,player.UserIDString));

                            } else {
                                s.pipe.Destroy();

                                userinfo.clipboard.s = s.pipe.sourcecont.net.ID;
                                userinfo.clipboard.d = s.pipe.destcont.net.ID;

                                jPipe newpipe = new jPipe();

                                // initalize pipe
                                if (newpipe.init(this,pipeidgen(),userinfo.clipboard,RemovePipe,MoveItem)) {
                                    // pipe was created so register it
                                    RegisterPipe(newpipe);
                                } else {
                                    // pipe error
                                    ShowOverlayText(player,lang.GetMessage(newpipe.initerr,this,player.UserIDString));
                                    newpipe = null;
                                }
                            }

                        } else {
                            s.pipe.OpenMenu(player,userinfo);
                        }
                    } else {
                        ShowOverlayText(player,lang.GetMessage("ErrorPrivilegeModify",this,player.UserIDString));
                        HideOverlayText(player,2f);
                    }
                }
            }
        }

        void OnStructureDemolish(BaseCombatEntity entity,BasePlayer player) {
            jPipeSegChild p = entity.GetComponent<jPipeSegChild>();
            if (p != null && p.pipe != null)
                p.pipe.OnSegmentKilled();
        }

        void OnEntityDeath(BaseCombatEntity entity,HitInfo info) {
            if (entity is BuildingBlock) {
                jPipeSegChild p = entity.GetComponent<jPipeSegChild>();
                if (p != null && p.pipe != null)
                    p.pipe.OnSegmentKilled();
            }
        }

        bool? OnStructureUpgrade(BaseCombatEntity entity,BasePlayer player,BuildingGrade.Enum grade) {
            jPipeSegChild p = entity.GetComponent<jPipeSegChild>();
            if (p != null && p.pipe != null) {
                if (!commandperm(player)) return false;
                int upgradelimit = getplayerupgradelimit(player);
                if (upgradelimit != -1 && upgradelimit < (int) grade) {
                    Puts(upgradelimit.ToString());
                    Puts(((int) grade).ToString());

                    ShowOverlayText(player,string.Format(lang.GetMessage("ErrorUpgradeLimit",this,player.UserIDString), (BuildingGrade.Enum) upgradelimit));
                    HideOverlayText(player,2f);

                    return false;
                }
                p.pipe.Upgrade(grade);
            }
            return null;
        }

        void OnStructureRepair(BaseCombatEntity entity,BasePlayer player) {
            if (GetUserInfo(player).isPlacing) return;

            jPipeSegChild p = entity.GetComponent<jPipeSegChild>();
            if (p != null && p.pipe != null)
                p.pipe.SetHealth(entity.GetComponent<BuildingBlock>().health);
        }

        // pipe damage handling
        bool? OnEntityTakeDamage(BaseCombatEntity entity,HitInfo hitInfo) {
            jPipeSegChild p = entity.GetComponent<jPipeSegChild>();
            if (p != null && p.pipe != null) {
                if (nodecay)
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay,0f); // no decay damage
                float damage = hitInfo.damageTypes.Total();
                if (damage > 0) {
                    float newhealth = entity.GetComponent<BuildingBlock>().health - damage;
                    if (newhealth >= 1f)
                        p.pipe.SetHealth(newhealth);
                    else
                        p.pipe.OnSegmentKilled();
                }
                return true;
            }
            return null;
        }

        // When item is added to filter
        object CanAcceptItem(ItemContainer container,Item item) {
            if (container == null || item == null || container.entityOwner == null || container.entityOwner.GetComponent<jPipeFilterStash>() == null)
                return null;

            if (container.itemList.Exists(x => x.info == item.info))
                return ItemContainer.CanAcceptResult.CannotAccept;

            jPipeFilterStash f = container.entityOwner.GetComponent<jPipeFilterStash>();

            if (f.loading)
                return null;

            if (f.itemadded) {
                f.itemadded = false;
                return null;
            }

            f.itemadded = true;
            f.UpdateFilter(item);
            ItemManager.Create(item.info).MoveToContainer(container);

            return ItemContainer.CanAcceptResult.CannotAccept;
        }

        // when item is removed from filter it is destroyed
        void OnItemRemovedFromContainer(ItemContainer container,Item item) {
            if (container == null || item == null || container.entityOwner == null || container.entityOwner.GetComponent<jPipeFilterStash>() == null)
                return;
            item.Remove();
        }

        // when item is taken from filter, it can't be stacked
        bool? CanStackItem(Item targetItem,Item item) {
            if (item.parent == null || item.parent.entityOwner == null || item.parent.entityOwner.GetComponent<jPipeFilterStash>() == null)
                return null;
            return false;
        }

        #endregion

        #region Commands

        private bool commandperm(BasePlayer player) {
            if (!(permission.UserHasPermission(player.UserIDString,"jpipes.use") || permission.UserHasPermission(player.UserIDString,"jpipes.admin"))) {
                ShowOverlayText(player,lang.GetMessage("ErrorCmdPerm",this,player.UserIDString));
                HideOverlayText(player,2f);
                return false;
            }
            return true;
        }

        [ChatCommand("p")]
        private void pipecreatechat(BasePlayer player,string cmd,string[] args) {
            if (!commandperm(player)) return;

            if (args.Length > 0) {
                switch (args[0]) {
                    case "h": pipehelp(player,cmd,args);
                        break;
                    case "c": pipecopy(player,cmd,args);
                        break;
                    case "s": pipestats(player,cmd,args);
                        break;
                    case "list": pipelist(player,cmd,args);
                        break;
                }
            } else {
                startplacingpipe(player,false);
            }
        }

        [ChatCommand("ph")]
        private void pipehelp(BasePlayer player,string cmd,string[] args) {
            if (!commandperm(player))
                return;
            PrintToChat(player,lang.GetMessage("HelpCmdTitle",this,player.UserIDString));
            PrintToChat(player,lang.GetMessage("HelpCmdCommands",this,player.UserIDString));
            PrintToChat(player,lang.GetMessage("HelpCmdMenu",this,player.UserIDString));
            PrintToChat(player,lang.GetMessage("HelpCmdUpgrade",this,player.UserIDString));
        }

        [ChatCommand("pc")]
        private void pipecopy(BasePlayer player,string cmd,string[] args) {
            if (!commandperm(player))
                return;
            UserInfo userinfo = GetUserInfo(player);

            userinfo.isPlacing = false;
            userinfo.placeend = null;
            userinfo.placestart = null;

            userinfo.isCopying = !userinfo.isCopying;

            if (userinfo.isCopying) {
                ShowOverlayText(player,lang.GetMessage("CopyingTextFirst",this,player.UserIDString),lang.GetMessage("CopyingSubtext",this,player.UserIDString));
            } else {
                //ShowOverlayText(player,"",lang.GetMessage("SelectCancel",this,player.UserIDString));
                HideOverlayText(player);
                userinfo.clipboard = null;
            }

        }

        [ChatCommand("ps")]
        private void pipestats(BasePlayer player,string cmd,string[] args) {
            if (!commandperm(player))
                return;
            UserInfo userinfo = GetUserInfo(player);
            int pipelimit = getplayerpipelimit(player);

            if (pipelimit == -1) PrintToChat(player,string.Format(lang.GetMessage("StatsCmd",this,player.UserIDString),userinfo.pipes.Count));
            else PrintToChat(player,string.Format(lang.GetMessage("StatsCmdLimit",this,player.UserIDString),userinfo.pipes.Count,pipelimit));
        }

        [ChatCommand("plist")]
        private void pipelist(BasePlayer player,string cmd,string[] args) {

            if (!permission.UserHasPermission(player.UserIDString,"jpipes.admin")) {
                ShowOverlayText(player,lang.GetMessage("ErrorCmdPerm",this,player.UserIDString));
                HideOverlayText(player,2f);
                return;
            }

            foreach (var u in users) {
                PrintToChat(player,$"{u.Value.pipes.Count}");
            }
        }

        [ConsoleCommand("jpipes.create")]
        private void pipecreate(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            if (!commandperm(p))
                return;
            startplacingpipe(p,true);
        }

        [ConsoleCommand("jpipes.openmenu")]
        private void pipeopenmenu(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            if (!commandperm(p))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe)) {
                pipe.OpenMenu(p,GetUserInfo(p));
            }
        }

        [ConsoleCommand("jpipes.closemenu")]
        private void pipeclosemenu(ConsoleSystem.Arg arg) {
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe)) {
                BasePlayer p = arg.Player();
                pipe.CloseMenu(p,GetUserInfo(p));
            }
        }

        [ConsoleCommand("jpipes.closemenudestroy")]
        private void pipeclosemenudestroy(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            UserInfo userinfo = GetUserInfo(p);

            if (!string.IsNullOrEmpty(userinfo.menu))
                CuiHelper.DestroyUi(p,userinfo.menu);
            userinfo.isMenuOpen = false;
        }

        [ConsoleCommand("jpipes.refreshmenu")]
        private void piperefreshmenu(ConsoleSystem.Arg arg) {
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe)) {
                BasePlayer p = arg.Player();
                UserInfo userinfo = GetUserInfo(p);
                pipe.CloseMenu(p,userinfo);
                pipe.OpenMenu(p,userinfo);
            }
        }

        [ConsoleCommand("jpipes.changedir")]
        private void cmdpipechangedir(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe)) {
                pipe.ChangeDirection();
            }
        }

        [ConsoleCommand("jpipes.openfilter")]
        private void cmdpipeopenfilter(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            if (!commandperm(p))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe)) {
                UserInfo userinfo = GetUserInfo(p);
                pipe.OpenFilter(p);
                pipe.CloseMenu(p,userinfo);
            }
        }

        [ConsoleCommand("jpipes.turnon")]
        private void pipeturnon(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            if (!commandperm(p))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe))
                pipe.mainlogic.pipeEnable(p);
        }

        [ConsoleCommand("jpipes.turnoff")]
        private void pipeturnoff(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            if (!commandperm(p))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe))
                pipe.mainlogic.pipeDisable(p);
        }

        [ConsoleCommand("jpipes.autostarton")]
        private void pipeautostarton(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe))
                pipe.autostarter = true;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.autostartoff")]
        private void pipeautostartoff(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe))
                pipe.autostarter = false;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.stackon")]
        private void pipestackon(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe))
                pipe.singlestack = true;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.stackoff")]
        private void pipestackoff(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe))
                pipe.singlestack = false;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.fsenable")]
        private void pipeFSenable(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe))
                pipe.fsplit = true;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.fsdisable")]
        private void pipeFSdisable(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe))
                pipe.fsplit = false;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.fsstack")]
        private void pipeFSstack(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]),out pipe))
                pipe.fsstacks = int.Parse(arg.Args[1]);
            pipe.RefreshMenu();
        }
        
        #endregion

        #region Classes

        // user data for chat commands
        private class UserInfo {
            public bool isPlacing = false;
            public bool isCopying = false;
            public bool isUsingBind = false;
            public BaseEntity placestart;
            public BaseEntity placeend;
            public jPipeData clipboard;
            // menu stuff
            public bool isMenuOpen = false;
            public string menu;

            public string overlay;
            public string overlaytext;
            public string overlaysubtext;

            // pipes
            public Dictionary<ulong,jPipe> pipes = new Dictionary<ulong,jPipe>();
        }

        private UserInfo GetUserInfo(BasePlayer player) => GetUserInfo(player.userID);

        private UserInfo GetUserInfo(ulong id) {
            UserInfo userinfo;
            if (!users.TryGetValue(id,out userinfo))
                users[id] = userinfo = new UserInfo();
            return userinfo;
        }

        // main pipe class
        private class jPipe {

            public Action<ulong,bool> remover;
            public Action<Item,int,StorageContainer,int> moveitem;

            private JPipes pipeplugin;

            public ulong id;
            public string initerr = string.Empty;

            public ulong ownerid;
            public string ownername;

            public bool isEnabled = true;

            public BaseEntity mainparent;

            // parents of storage containers
            public BaseEntity source;
            public BaseEntity dest;

            // storage containers
            public StorageContainer sourcecont;
            public StorageContainer destcont;
            
            // storage container child id
            public uint sourcechild = 0;
            public uint destchild = 0;

            public jPipeLogic mainlogic;

            public BuildingGrade.Enum pipegrade = BuildingGrade.Enum.Twigs;
            public float health;

            private List<BaseEntity> pillars = new List<BaseEntity>();
            private BaseEntity filterstash;
            private StorageContainer stashcont;
            private int lookingatstash = 0;

            public bool singlestack = false;
            public bool autostarter = false;

            private bool destisstartable = false;

            public List<int> filteritems = new List<int>();

            public bool fsplit = false;
            public int fsstacks = 2;

            public List<BasePlayer> playerslookingatmenu = new List<BasePlayer>();
            public List<BasePlayer> playerslookingatfilter = new List<BasePlayer>();

            // constructor
            public jPipe() { }

            // init
            public bool init(JPipes pplug,ulong nid,jPipeData data,Action<ulong,bool> rem,Action<Item,int,StorageContainer,int> mover) {

                pipeplugin = pplug;

                data.toPipe(this);
                
                if (source == null) {
                    initerr = "ErrorFindFirst";
                    return false;
                }
                if (dest == null) {
                    initerr = "ErrorFindSecond";
                    return false;
                }

                if (dest is BaseOven || dest is Recycler)
                    destisstartable = true;

                remover = rem;
                moveitem = mover;
                id = nid;

                Vector3 sourcepos = sourcecont.CenterPoint() + containeroffset(sourcecont);
                Vector3 endpos = destcont.CenterPoint() + containeroffset(destcont);

                float distance = Vector3.Distance(sourcepos,endpos);

                Quaternion rotation = Quaternion.LookRotation(endpos - sourcepos) * Quaternion.Euler(90,0,0);

                // create pillars

                int segments = (int) Mathf.Ceil(distance / pipesegdist);
                float segspace = (distance - pipesegdist) / (segments - 1);

                for (int i = 0;i < segments;i++) {

                    //float offset = (segspace * i);
                    //Vector3 pos = sourcepos + ((rotation * Vector3.up) * offset);

                    BaseEntity ent;

                    if (i == 0) {
                        // the position thing centers the pipe if there is only one segment
                        ent = GameManager.server.CreateEntity("assets/prefabs/building core/pillar/pillar.prefab",(segments == 1) ? (sourcepos + ((rotation * Vector3.up) * ((distance - pipesegdist) * 0.5f))) : sourcepos,rotation);
                        mainlogic = jPipeLogic.Attach(ent,this,updaterate,pipeplugin.flowrates[0]);
                        mainparent = ent;
                    } else {
                        ent = GameManager.server.CreateEntity("assets/prefabs/building core/pillar/pillar.prefab",Vector3.up * (segspace * i) + ((i % 2 == 0) ? Vector3.zero : pipefightoffset));
                    }

                    ent.enableSaving = false;

                    BuildingBlock block = ent.GetComponent<BuildingBlock>();

                    if (block != null) {
                        block.grounded = true;
                        block.grade = pipegrade;
                        block.enableSaving = false;
                        block.Spawn();
                        block.SetHealthToMax();
                    }

                    jPipeSegChild.Attach(ent,this);

                    if (i != 0)
                        ent.SetParent(mainparent);

                    pillars.Add(ent);
                    ent.enableSaving = false;
                }

                mainlogic.distance = distance;
                mainlogic.flowrate = ((int) pipegrade == -1) ? pipeplugin.flowrates[0] : pipeplugin.flowrates[(int) pipegrade];

                if (health != 0)
                    SetHealth(health);

                return true;

            }

            private Vector3 containeroffset(BaseEntity e) {
                if (e is BoxStorage)
                    return Vector3.zero;
                else if (e is BaseOven) {
                    string panel = e.GetComponent<BaseOven>().panelName;

                    if (panel == "largefurnace")
                        return contoffset.largefurnace;
                    else if (panel == "smallrefinery")
                        return e.transform.rotation * contoffset.refinery;
                    else
                        return contoffset.furnace;
                    //} else if (e is ResourceExtractorFuelStorage) {
                    //if (e.GetComponent<StorageContainer>().panelName == "fuelstorage") {
                    //    return contoffset.pumpfuel;
                    //} else {
                    //    return e.transform.rotation * contoffset.pumpoutput;
                    //}
                } else if (e is AutoTurret) { return contoffset.turret;
                } else if (e is SearchLight) { return contoffset.searchlight;
                }
                return Vector3.zero;
            }

            public void OpenFilter(BasePlayer player) {
                if (filterstash != null) {
                    LookInFilter(player,filterstash.GetComponent<StashContainer>());
                    return;
                }

                if (pipeplugin.filtersizes[(int) pipegrade] == 0) return;

                filterstash = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab",new Vector3(0,0,-10000f),Quaternion.Euler(-90,0,0));

                filterstash.SetParent(mainparent);

                stashcont = filterstash.GetComponent<StorageContainer>();

                if (stashcont != null) {
                    stashcont.inventorySlots = pipeplugin.filtersizes[(int) pipegrade];
                    stashcont.SendNetworkUpdate();
                    filterstash.Spawn();
                }

                // load content

                jPipeFilterStash f = jPipeFilterStash.Attach(filterstash,FilterCallback,UpdateFilterItems);

                foreach (int i in filteritems) {
                    Item item = ItemManager.CreateByItemID(i,1);
                    item.MoveToContainer(stashcont.inventory);
                }

                f.loading = false;

                //stashcont.DecayTouch();
                stashcont.UpdateNetworkGroup();
                stashcont.SendNetworkUpdateImmediate();

                stashcont.globalBroadcast = true;

                LookInFilter(player,stashcont);
            }

            public void LookInFilter(BasePlayer player,StorageContainer stash) {
                stash.SetFlag(BaseEntity.Flags.Open,true,false);
                player.inventory.loot.StartLootingEntity(stash,false);
                player.inventory.loot.AddContainer(stash.inventory);
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null,player,"RPC_OpenLootPanel",stash.panelName);
                playerslookingatfilter.Add(player);
            }

            public void FilterCallback(BasePlayer player) {
                playerslookingatfilter.Remove(player);
                if (playerslookingatfilter.Count < 1)
                    DestroyFilter();
            }

            private void DestroyFilter() {
                if (filterstash == null)
                    return;

                filteritems.Clear();
                foreach (var i in stashcont.inventory.itemList)
                    filteritems.Add(i.info.itemid);

                filterstash.Kill();
            }

            public void UpdateFilterItems(Item item) {
                filteritems.Clear();
                foreach (var i in stashcont.inventory.itemList)
                    filteritems.Add(i.info.itemid);
            }

            public void ChangeDirection() {
                StorageContainer newdest = sourcecont;
                sourcecont = destcont;
                destcont = newdest;

                destisstartable = ((BaseEntity) destcont is BaseOven || (BaseEntity) destcont is Recycler);
                RefreshMenu();
            }

            // destroy entire pipe when one segment fails
            public void OnSegmentKilled() {
                Destroy();
            }

            public void Destroy(bool removeme = true) {
                // close any open menus
                foreach (BasePlayer p in playerslookingatmenu)
                    p.SendConsoleCommand("jpipes.closemenudestroy");

                DestroyFilter();

                remover(id,removeme);
            }

            public void Upgrade(BuildingGrade.Enum grade) {
                foreach (var seg in pillars) {
                    BuildingBlock b = seg.GetComponent<BuildingBlock>();
                    b.SetGrade(grade);
                    b.SetHealthToMax();
                    health = b.health;
                    b.SendNetworkUpdate(BasePlayer.NetworkQueue.UpdateDistance);
                }
                pipegrade = grade;
                mainlogic.flowrate = ((int) grade == -1) ? pipeplugin.flowrates[0] : pipeplugin.flowrates[(int) grade];

                RefreshMenu();

                DestroyFilter();
                foreach (BasePlayer p in playerslookingatfilter)
                    OpenFilter(p);
            }

            public void SetHealth(float nhealth) {
                foreach (var seg in pillars) {
                    BuildingBlock b = seg.GetComponent<BuildingBlock>();
                    b.health = nhealth;
                    b.SendNetworkUpdate(BasePlayer.NetworkQueue.UpdateDistance);
                }
                health = nhealth;
            }

            public void OpenMenu(BasePlayer player,UserInfo userinfo) {

                playerslookingatmenu.Add(player);

                Vector2 size = new Vector2(0.125f,0.175f);
                float margin = 0.05f;

                var elements = new CuiElementContainer();

                userinfo.menu = elements.Add(
                    new CuiPanel {
                        Image = { Color = "0.15 0.15 0.15 0.86" },
                        RectTransform = { AnchorMin = "0 0",AnchorMax = "1 1" },
                        CursorEnabled = true
                    }
                );

                // close when you click outside of the window
                elements.Add(
                    new CuiButton {
                        Button = { Command = $"jpipes.closemenu {id}",Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0",AnchorMax = "1 1" },
                        Text = { Text = string.Empty }
                    },userinfo.menu
                );

                string window = elements.Add(new CuiPanel {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = {
                            AnchorMin = $"{0.5f-size.x} {0.5f-size.y}",
                            AnchorMax = $"{0.5f+size.x} {0.5f+size.y}"
                        }
                },userinfo.menu
                );

                string contentleft = elements.Add(new CuiPanel {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = {
                            AnchorMin = $"{margin} {0-margin*0.25f}",
                            AnchorMax = $"{0.5f-margin} {1-margin*0.5f}"
                        },
                    CursorEnabled = false
                },window
                );

                string contentright = elements.Add(new CuiPanel {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = {
                            AnchorMin = "0.5 0",
                            AnchorMax = "1 1"
                        }
                },window
                );

                // title
                elements.Add(
                    CreateLabel(pipeplugin.lang.GetMessage("MenuTitle",pipeplugin,player.UserIDString),1,1,TextAnchor.UpperLeft,32,"0","1","1 1 1 0.8"),
                    contentleft
                );

                //Furnace Splitter
                if ((BaseEntity) destcont is BaseOven && pipeplugin.FurnaceSplitter != null) {

                    string FSmain = elements.Add(new CuiPanel {
                        Image = { Color = "1 1 1 0" },
                        RectTransform = {
                               AnchorMin = $"{margin*0.5f} 0.33",
                               AnchorMax = $"{0.5f-(margin*0.5f)} 0.76"
                           }
                    },window
                    );

                    string FShead = elements.Add(new CuiPanel {
                        Image = { Color = "1 1 1 0.04" },
                        RectTransform = {
                               AnchorMin = "0 0.775",
                               AnchorMax = "1 1"
                           }
                    },FSmain
                    );

                    elements.Add(
                       CreateLabel("Furnace Splitter",1,1,TextAnchor.MiddleCenter,13,"0","1","1 1 1 0.8"),
                       FShead
                    );

                    string FScontent = elements.Add(new CuiPanel {
                        Image = { Color = "1 1 1 0" },
                        RectTransform = {
                               AnchorMin = "0 0",
                               AnchorMax = "1 0.74"
                           }
                    },FSmain
                    );

                    // elements.Add(
                    //    CreateLabel("ETA: 0s (0 wood)", 1, 0.3f, TextAnchor.MiddleLeft, 10, $"{margin}", "1", "1 1 1 0.8"),
                    //    FScontent
                    // );

                    if (fsplit) {
                        elements.Add(
                            CreateButton($"jpipes.fsdisable {id}",1.25f,0.25f,12,pipeplugin.lang.GetMessage("MenuTurnOff",pipeplugin,player.UserIDString),$"{margin}",$"{0.5f - (margin * 0.5f)}","0.59 0.27 0.18 0.8","0.89 0.49 0.31 1"),
                            FScontent
                        );
                    } else {
                        elements.Add(
                            CreateButton($"jpipes.fsenable {id}",1.25f,0.25f,12,pipeplugin.lang.GetMessage("MenuTurnOn",pipeplugin,player.UserIDString),$"{margin}",$"{0.5f - (margin * 0.5f)}","0.43 0.51 0.26 0.8","0.65 0.76 0.47 1"),
                            FScontent
                        );
                    }

                    // elements.Add(
                    //    CreateButton($"jpipes.autostartoff {id}", 2.2f, 0.25f, 11, "Trim fuel", $"{0.5f + (margin * 0.5f)}", $"{1 - (margin)}", "1 1 1 0.08", "1 1 1 0.8"),
                    //    FScontent
                    // );

                    float arrowbuttonmargin = 0.1f;
                    elements.Add(
                        CreateButton($"jpipes.fsstack {id} {fsstacks - 1}",2.5f,0.25f,12,"<",$"{margin}",$"{margin + arrowbuttonmargin}","1 1 1 0.08","1 1 1 0.8"),
                        FScontent
                    );
                    elements.Add(
                        CreateLabel($"{fsstacks}",3,0.205f,TextAnchor.MiddleCenter,12,$"{margin + arrowbuttonmargin}",$"{0.5f - (margin * 0.5f) - arrowbuttonmargin}","1 1 1 0.8"),
                        FScontent
                    );

                    //elements.Add(
                    //    CuiInputField(FScontent,$"jpipes.fsstack {id} ",$"{fsstacks}",12,2)
                    //);

                    elements.Add(
                        CreateButton($"jpipes.fsstack {id} {fsstacks + 1}",2.5f,0.25f,12,">",$"{0.5f - (margin * 0.5f) - arrowbuttonmargin}",$"{0.5f - (margin * 0.5f)}","1 1 1 0.08","1 1 1 0.8"),
                        FScontent
                    );

                    elements.Add(
                        CreateLabel("Total Stacks",3,0.205f,TextAnchor.MiddleLeft,12,$"{(margin * 0.5f) + 0.5f}","1","1 1 1 0.8"),
                        FScontent
                    );
                }

                // info
                elements.Add(
                    CreateLabel(
                        string.Format(pipeplugin.lang.GetMessage("MenuInfo",pipeplugin,player.UserIDString),ownername,mainlogic.flowrate,mainlogic.distance),
                        1,1,TextAnchor.LowerLeft,16,"0","1","1 1 1 0.4"
                    ),contentleft
                );

                //elements.Add(
                //    CreateLabel(
                //        $"start {sourcecont.net.ID}\nend {destcont.net.ID}",
                //        1,1.2f,TextAnchor.LowerLeft,16,"0","1","1 1 1 0.4"
                //    ),contentleft
                //);

                // buttons

                //0.13 0.38 0.58

                float buttonspacing = 0.1f;
                float buttonratio = (destisstartable) ? 0.2f : 0.25f;
                float buttonsize = buttonratio - (buttonspacing * buttonratio);
                float buttonoffset = buttonspacing + (buttonspacing * buttonratio);

                // toggle button
                if (isEnabled) {
                    elements.Add(
                        CreateButton($"jpipes.turnoff {id}",1 + buttonoffset * 0,buttonsize,18,pipeplugin.lang.GetMessage("MenuTurnOff",pipeplugin,player.UserIDString),"0","1","0.59 0.27 0.18 0.8","0.89 0.49 0.31 1"),
                        contentright
                    );
                } else {
                    elements.Add(
                        CreateButton($"jpipes.turnon {id}",1 + buttonoffset * 0,buttonsize,18,pipeplugin.lang.GetMessage("MenuTurnOn",pipeplugin,player.UserIDString),"0","1","0.43 0.51 0.26 0.8","0.65 0.76 0.47 1"),
                        contentright
                    );
                }

                if (destisstartable) {
                    if (autostarter) {
                        elements.Add(
                            CreateButton($"jpipes.autostartoff {id}",2 + buttonoffset * 1,buttonsize,18,pipeplugin.lang.GetMessage("MenuAutoStarter",pipeplugin,player.UserIDString),"0","1","0.43 0.51 0.26 0.8","0.65 0.76 0.47 1"),
                            contentright
                        );
                    } else {
                        elements.Add(
                            CreateButton($"jpipes.autostarton {id}",2 + buttonoffset * 1,buttonsize,18,pipeplugin.lang.GetMessage("MenuAutoStarter",pipeplugin,player.UserIDString),"0","1","0.59 0.27 0.18 0.8","0.89 0.49 0.31 1"),
                            contentright
                        );
                    }
                }

                elements.Add(
                    CreateButton($"jpipes.changedir {id}",(destisstartable) ? 3 + buttonoffset * 2 : 2 + buttonoffset * 1,buttonsize,18,pipeplugin.lang.GetMessage("MenuChangeDirection",pipeplugin,player.UserIDString),"0","1","1 1 1 0.08","1 1 1 0.8"),
                    contentright
                );

                if (!fsplit || pipeplugin.FurnaceSplitter == null) {
                    if (singlestack) {
                        elements.Add(
                            CreateButton($"jpipes.stackoff {id}",(destisstartable) ? 4 + buttonoffset * 3 : 3 + buttonoffset * 2,buttonsize,18,pipeplugin.lang.GetMessage("MenuSingleStack",pipeplugin,player.UserIDString),"0","1","1 1 1 0.08","1 1 1 0.8"),
                            contentright
                        );
                    } else {
                        elements.Add(
                            CreateButton($"jpipes.stackon {id}",(destisstartable) ? 4 + buttonoffset * 3 : 3 + buttonoffset * 2,buttonsize,18,pipeplugin.lang.GetMessage("MenuMultiStack",pipeplugin,player.UserIDString),"0","1","1 1 1 0.08","1 1 1 0.8"),
                            contentright
                        );
                    }
                } else {
                    elements.Add(
                        CreateButton("",(destisstartable) ? 4 + buttonoffset * 3 : 3 + buttonoffset * 2,buttonsize,18,pipeplugin.lang.GetMessage("MenuMultiStack",pipeplugin,player.UserIDString),"0","1","1 1 1 0.08","1 1 1 0.2"),
                        contentright
                    );
                }

                // disable filter button if filtersize is 0
                if (pipeplugin.filtersizes[(int) pipegrade] == 0) {
                    elements.Add(
                        CreateButton("",(destisstartable) ? 5 + buttonoffset * 4 : 4 + buttonoffset * 3,buttonsize,18,pipeplugin.lang.GetMessage("MenuItemFilter",pipeplugin,player.UserIDString),"0","1","1 1 1 0.08","1 1 1 0.2"),
                        contentright
                    );
                } else {
                    elements.Add(
                        CreateButton($"jpipes.openfilter {id}",(destisstartable) ? 5 + buttonoffset * 4 : 4 + buttonoffset * 3,buttonsize,18,pipeplugin.lang.GetMessage("MenuItemFilter",pipeplugin,player.UserIDString),"0","1","1 1 1 0.08","1 1 1 0.8"),
                        contentright
                    );
                }


                CuiHelper.AddUi(player,elements);
                userinfo.isMenuOpen = true;
            }

            public void CloseMenu(BasePlayer player,UserInfo userinfo) {
                if (!string.IsNullOrEmpty(userinfo.menu))
                    CuiHelper.DestroyUi(player,userinfo.menu);
                userinfo.isMenuOpen = false;

                playerslookingatmenu.Remove(player);
            }

            // this refreshes the menu for each playerslookingatmenu
            public void RefreshMenu() {
                foreach (BasePlayer p in playerslookingatmenu) {
                    p.SendConsoleCommand($"jpipes.refreshmenu {id}");
                }
            }
        }

        #endregion

        #region Pipe Parameters 

        // length of a segment
        private static float pipesegdist = 3;

        // every other pipe segment is offset by this to remove z fighting
        private static Vector3 pipefightoffset = new Vector3(0.0001f,0,0.0001f);

        // offset of pipe inside different containers
        private static class contoffset {
            public static Vector3 turret = new Vector3(0,-0.58f,0);
            public static Vector3 refinery = new Vector3(-1,0,-0.1f);
            public static Vector3 furnace = new Vector3(0,-0.3f,0);
            public static Vector3 largefurnace = new Vector3(0,-1.5f,0);
            public static Vector3 searchlight = new Vector3(0,-0.5f,0);
            public static Vector3 pumpfuel = new Vector3(0,0,0);
            public static Vector3 pumpoutput = new Vector3(-1,2,0);
            public static Vector3 recycler = new Vector3(0,0,0);
            //public static Vector3 quarryfuel = new Vector3(1,-0.2f,0);
            //public static Vector3 quarryoutput = new Vector3(1,0,0);
        }

        #endregion

        #region Pipe Functions

        private void startplacingpipe(BasePlayer player,bool isUsingBind = false) {

            UserInfo userinfo = GetUserInfo(player);

            int playerpipelimit;
            if (!checkplayerpipelimit(player,userinfo,out playerpipelimit)) {
                ShowOverlayText(player,string.Format(lang.GetMessage("ErrorPipeLimitReached",this,player.UserIDString),playerpipelimit.ToString()));
                HideOverlayText(player,2f);
                userinfo.placestart = null;
                userinfo.placeend = null;
                return;
            }

            userinfo.isPlacing = !userinfo.isPlacing;
            userinfo.isUsingBind = isUsingBind;
            userinfo.isCopying = false;
            userinfo.clipboard = null;

            if (userinfo.isPlacing) {
                if (!isUsingBind)
                    PrintToChat(player,lang.GetMessage("HelpBindTip",this,player.UserIDString));

                ShowOverlayText(player,lang.GetMessage("SelectFirst",this,player.UserIDString),lang.GetMessage(isUsingBind ? "SelectSubtextBind" : "SelectSubtextCmd",this,player.UserIDString));
            } else {
                //ShowOverlayText(player,"",lang.GetMessage("SelectCancel",this,player.UserIDString));
                HideOverlayText(player);
                userinfo.placestart = null;
                userinfo.placeend = null;
            }
        }

        private void NewPipe(BasePlayer player,UserInfo userinfo) {

            jPipeData newdata = new jPipeData();
            newdata.setContainers(userinfo.placestart, userinfo.placeend);

            
            if (!checkpipeoverlap(regpipes,newdata)) {

                float dist = Vector3.Distance(userinfo.placestart.CenterPoint(),userinfo.placeend.CenterPoint());

                if (dist > maxpipedist) { ShowOverlayText(player,lang.GetMessage("ErrorTooFar",this,player.UserIDString)); } else if (dist <= minpipedist) { ShowOverlayText(player,lang.GetMessage("ErrorTooClose",this,player.UserIDString)); } else {

                    jPipe newpipe = new jPipe();
                    newdata.o = player.userID;
                    newdata.on = player.displayName;

                    // initalize pipe
                    if (newpipe.init(this,pipeidgen(),newdata,RemovePipe,MoveItem)) {
                        // pipe was created so register it
                        RegisterPipe(newpipe);
                        ShowOverlayText(player,lang.GetMessage("PipeCreated",this,player.UserIDString));
                    } else {
                        // pipe error
                        ShowOverlayText(player,lang.GetMessage(newpipe.initerr,this,player.UserIDString));
                        newpipe = null;
                    }
                }
            } else {
                ShowOverlayText(player,lang.GetMessage("ErrorAlreadyConnected",this,player.UserIDString));
            }

            HideOverlayText(player,3f);
            userinfo.isPlacing = false;
            userinfo.placestart = null;
            userinfo.placeend = null;
        }

        private System.Random randomidgen = new System.Random();
        private ulong pipeidgen() {
            ulong id = (ulong) randomidgen.Next(1000000,9999999);
            if (regpipes.ContainsKey(id))
                return pipeidgen();
            else
                return id;
        }
        
        // TODO this could be improved by only compairing ids
        private static bool checkpipeoverlap(Dictionary<ulong,jPipe> rps,jPipeData data) {
            StorageContainer s = getcontfromid(data.s,data.cs);
            StorageContainer e = getcontfromid(data.d,data.cd);
            
            foreach (var p in rps)
                if ((p.Value.sourcecont == s && p.Value.destcont == e) || (p.Value.sourcecont == e && p.Value.destcont == s))
                    return true;
            return false;
        }

        private static bool checkcontwhitelist(BaseEntity e) {
            if (e is VendingMachine || e is BaseFuelLightSource || e is Locker || e is LiquidContainer || e is ShopFront || e is RepairBench)
                return false;
            return true;
        }

        private bool checkcontprivlage(BaseEntity e,BasePlayer p) => e.GetComponent<StorageContainer>().CanOpenLootPanel(p) && checkbuildingprivlage(p); 

        private bool checkbuildingprivlage(BasePlayer p) {
            if (permission.UserHasPermission(p.UserIDString,"jpipes.admin"))
                return true;
            BuildingPrivlidge priv = p.GetBuildingPrivilege();
            return (priv != null) ? priv.IsAuthed(p) : true;
        }

        private bool checkplayerpipelimit(BasePlayer p,UserInfo user) {
            int limit = getplayerpipelimit(p);
            return (limit >= (user.pipes.Keys.Count + 1)) || limit == -1;
        }
        private bool checkplayerpipelimit(BasePlayer p,UserInfo user, out int pipelimit) {
            pipelimit = getplayerpipelimit(p);
            return (pipelimit >= (user.pipes.Keys.Count + 1)) || pipelimit == -1;
        }

        // TODO combine limit functions into one

        private int getplayerpipelimit(BasePlayer p) {
            string id = p.UserIDString;
            if (permission.UserHasPermission(id,"jpipes.admin"))
                return -1;

            List<string> uperms = permission.GetUserPermissions(p.UserIDString).ToList();
            List<string> pperms = new List<string>();

            foreach (var s in permlevels.Keys) {
                if (uperms.Contains($"jpipes.level.{s}"))
                    pperms.Add(s);
            }

            int curlimit = 0;
            foreach (var s in pperms) {
                int l = permlevels[s].pipelimit;
                if (l == -1)
                    return -1;
                curlimit = Mathf.Max(curlimit, l);
            }
            
            return curlimit == 0 ? -1 : curlimit;
        }
        private int getplayerupgradelimit(BasePlayer p) {
            string id = p.UserIDString;
            if (permission.UserHasPermission(id,"jpipes.admin"))
                return -1;

            List<string> uperms = permission.GetUserPermissions(p.UserIDString).ToList();
            List<string> pperms = new List<string>();

            foreach (var s in permlevels.Keys) {
                if (uperms.Contains($"jpipes.level.{s}"))
                    pperms.Add(s);
            }

            int curlimit = -1;
            foreach (var s in pperms) {
                int l = permlevels[s].upgradelimit;
                if (l == -1)
                    return -1;
                curlimit = Mathf.Max(curlimit,l);
            }

            return curlimit > 3 ? -1 : curlimit;
        }

        // find storage container from id and child id
        private static StorageContainer getcontfromid(uint id, uint cid = 0) => getchildcont((BaseEntity) BaseNetworkable.serverEntities.Find(id), cid);

        // find storage container from parent and child id
        private static StorageContainer getchildcont(BaseEntity parent,uint id = 0) {
            if (id != 0) {
                BaseResourceExtractor ext = parent.GetComponent<BaseResourceExtractor>();
                if (ext != null) {
                    foreach (var c in ext.children) {
                        if (c is ResourceExtractorFuelStorage && c.GetComponent<ResourceExtractorFuelStorage>().panelName == ((id == 2) ? "fuelstorage" : "generic")) 
                            return c.GetComponent<StorageContainer>();
                    }
                }
                //return parent.GetComponent<StorageContainer>();
            }
            return parent.GetComponent<StorageContainer>();
        }

        private void RegisterPipe(jPipe pipe) {
            GetUserInfo(pipe.ownerid).pipes.Add(pipe.id,pipe);
            regpipes.Add(pipe.id,pipe);
        }
        private void UnRegisterPipe(jPipe pipe) {
            GetUserInfo(pipe.ownerid).pipes.Remove(pipe.id);
            regpipes.Remove(pipe.id);
        }
        private void UnRegisterPipe(ulong id) {
            jPipe pipe;
            if (regpipes.TryGetValue(id,out pipe)) {
                UnRegisterPipe(pipe);
            }
        }

        public void RemovePipe(ulong id,bool remove = true) {

			jPipe pipe;
			if (regpipes.TryGetValue(id,out pipe)) {
				NextFrame(() => {
					if (!pipe.mainparent.IsDestroyed)
						pipe.mainparent.Kill();
					//Puts($"Pipe {id} removed");
				});
			}

            if (remove)
                UnRegisterPipe(id);
        }

		private void UnloadPipes() {
            foreach (var p in regpipes)
                UnloadPipe(p.Value);
        }

		private void UnloadPipe(jPipe p) {
			NextFrame(() => {
                if (!p.mainparent.IsDestroyed)
                    p.mainparent.Kill();
			});
		}

		private void SavePipes() {
			storedData.p = new Dictionary<ulong,jPipeData>();

			foreach (var p in regpipes) {
                if (!p.Value.mainparent.IsDestroyed) {
                    jPipeData d = new jPipeData();
                    d.fromPipe(p.Value);
                    storedData.p[p.Key] = d; // creates or updates 
                }
			}

			SaveData(storedData);

			Puts(storedData.p.Count.ToString() + " pipes saved");
		}

		public void MoveItem(Item itemtomove,int amounttotake,StorageContainer cont,int stacks) {

			if (itemtomove.amount > amounttotake)
				itemtomove = itemtomove.SplitItem(amounttotake);

			if ((BaseEntity) cont is BaseOven && stacks > -1) {
				if (FurnaceSplitter != null)
					FurnaceSplitter?.Call("MoveSplitItem",itemtomove,(BaseEntity) cont,stacks);
				else
					itemtomove.MoveToContainer(cont.inventory);
			} else {
				itemtomove.MoveToContainer(cont.inventory);
			}
		}

		#endregion

		#region Pipe Components

		private class jPipeLogic : MonoBehaviour {

			public jPipe pipe;
			public int tickdelay;
			public int flowrate;
			public float distance;

			public static jPipeLogic Attach(BaseEntity entity,jPipe pipe,int tickdelay,int flowrate) {
				jPipeLogic n = entity.gameObject.AddComponent<jPipeLogic>();
				n.pipe = pipe;
				n.tickdelay = tickdelay;
				n.flowrate = flowrate;
				return n;
			}

			private float period = 0f;

			void Update() {

				// if either container is destroyed
				if (pipe.sourcecont == null || pipe.destcont == null)
					pipe.Destroy();
                
                if (period > tickdelay) {

					// source isn't empty
					if (pipe.sourcecont.inventory.itemList.Count > 0 && pipe.sourcecont.inventory.itemList[0] != null && pipe.isEnabled) {

						Item itemtomove = FindItem();

						// move the item
						if (itemtomove != null && pipe.destcont.inventory.CanAcceptItem(itemtomove) == ItemContainer.CanAcceptResult.CanAccept && pipe.destcont.inventory.CanTake(itemtomove)) {

							//if ( !((BaseEntity) pipe.destcont is Recycler) || (((BaseEntity) pipe.destcont is Recycler) && (i.position > 5))) {

							int amounttotake = tickdelay * flowrate;

							if (pipe.singlestack) {

								Item slot = pipe.destcont.inventory.FindItemsByItemID(itemtomove.info.itemid).OrderBy<Item,int>((Func<Item,int>) (x => x.amount)).FirstOrDefault<Item>();

								if (slot != null) {
									if (slot.CanStack(itemtomove)) {

										int maxstack = slot.MaxStackable();
										if (slot.amount < maxstack) {
											if ((maxstack - slot.amount) < amounttotake)
												amounttotake = maxstack - slot.amount;
											pipe.moveitem(itemtomove,amounttotake,pipe.destcont,(pipe.fsplit) ? pipe.fsstacks : -1);
											TurnOnDest();
										}
									}
								} else {
									pipe.moveitem(itemtomove,amounttotake,pipe.destcont,(pipe.fsplit) ? pipe.fsstacks : -1);
									TurnOnDest();
								}
							} else {
								pipe.moveitem(itemtomove,amounttotake,pipe.destcont,(pipe.fsplit) ? pipe.fsstacks : -1);
								TurnOnDest();
							}
						}

					}
					period = 0;
				}
				period += UnityEngine.Time.deltaTime;
			}

			public void pipeEnable(BasePlayer player) {
				if (!pipe.isEnabled)
					period = 0;
				pipe.isEnabled = true;
				pipe.RefreshMenu();
			}
			public void pipeDisable(BasePlayer player) {
				pipe.isEnabled = false;
				pipe.RefreshMenu();
			}

			private static Item FilterItem(List<Item> cont,List<int> filter) {
				foreach (Item i in cont)
					if (filter.Contains(i.info.itemid))
						return i;
				return null;
			}

			private Item FindItem() {
                
                foreach (Item i in pipe.sourcecont.inventory.itemList) {
					if (pipe.filteritems.Count == 0 || pipe.filteritems.Contains(i.info.itemid)) {
						if (!((BaseEntity) pipe.sourcecont is Recycler) || (((BaseEntity) pipe.sourcecont is Recycler) && (i.position > 5))) {

							if ((BaseEntity) pipe.destcont is BaseOven) {
								if ((bool) ((UnityEngine.Object) i.info.GetComponent<ItemModBurnable>()) || (bool) ((UnityEngine.Object) i.info.GetComponent<ItemModCookable>()))
									return i;
							} else if ((BaseEntity) pipe.destcont is Recycler) {
								if ((UnityEngine.Object) i.info.Blueprint != (UnityEngine.Object) null)
									return i;
							} else {
								return i;
							}
						}
					}
				}
				return null;
			}

			private void TurnOnDest() {
				if (!pipe.autostarter)
					return;

				BaseEntity e = (BaseEntity) pipe.destcont;
				if (e is BaseOven) {
					e.GetComponent<BaseOven>().StartCooking();
				} else if (e is Recycler) {
					e.GetComponent<Recycler>().StartRecycling();
				}
			}

		}

		private class jPipeSegChild : MonoBehaviour {

			public jPipe pipe;

			public static void Attach(BaseEntity entity,jPipe pipe) {
				jPipeSegChild n = entity.gameObject.AddComponent<jPipeSegChild>();
				n.pipe = pipe;
			}
		}

		private class jPipeFilterStash : MonoBehaviour {

			private Action<BasePlayer> callback;
			private Action<Item> itemcallback;

			public BaseEntity entityOwner;
			public bool itemadded = false; // used to prevent stack overflow in CanAcceptItem
			public bool loading = true;

			public static jPipeFilterStash Attach(BaseEntity entity,Action<BasePlayer> callback,Action<Item> itemcallback) {
				jPipeFilterStash f = entity.gameObject.AddComponent<jPipeFilterStash>();
				f.callback = callback;
				f.itemcallback = itemcallback;
				f.entityOwner = entity;
				return f;
			}

			private void PlayerStoppedLooting(BasePlayer player) => callback(player);
			public void UpdateFilter(Item item) => itemcallback(item);
		}

		#endregion

		#region CUI elements

		private static CuiLabel CreateLabel(string text,int i,float rowHeight,TextAnchor align = TextAnchor.MiddleLeft,int fontSize = 15,string xMin = "0",string xMax = "1",string color = "1.0 1.0 1.0 1.0") {
			return new CuiLabel {
				Text = { Text = text,FontSize = fontSize,Align = align,Color = color },
				RectTransform = { AnchorMin = $"{xMin} {1 - rowHeight * i + i * .002f}",AnchorMax = $"{xMax} {1 - rowHeight * (i - 1) + i * .002f}" }
			};
		}
		private static CuiButton CreateButton(string command,float i,float rowHeight,int fontSize = 15,string content = "+",string xMin = "0",string xMax = "1",string color = "0.8 0.8 0.8 0.2",string textcolor = "1 1 1 1",float offset = -.005f) {
			return new CuiButton {
				Button = { Command = command,Color = color },
				RectTransform = { AnchorMin = $"{xMin} {1 - rowHeight * i + i * offset}",AnchorMax = $"{xMax} {1 - rowHeight * (i - 1) + i * offset}" },
				Text = { Text = content,FontSize = fontSize,Align = TextAnchor.MiddleCenter,Color = textcolor }
			};
		}
		private static CuiPanel CreatePanel(string anchorMin,string anchorMax,string color = "0 0 0 0") {
			return new CuiPanel {
				Image = { Color = color },
				RectTransform = { AnchorMin = anchorMin,AnchorMax = anchorMax }
			};
		}
		private static CuiElement CuiInputField(string parent = "Hud", string command = "", string text = "",int fontsize = 14,int charlimit = 100, string name = null) {
			
			if (string.IsNullOrEmpty(name))
				name = CuiHelper.GetGuid();
			CuiElement cuiElement = new CuiElement();
			cuiElement.Name = name;
			cuiElement.Parent = parent;
			cuiElement.Components.Add((ICuiComponent) new CuiInputFieldComponent { Text = "he",Align = TextAnchor.MiddleCenter, CharsLimit = charlimit, Command = command, FontSize = fontsize});
			cuiElement.Components.Add((ICuiComponent) new CuiNeedsCursorComponent());

			return cuiElement;
		}
		private static CuiElement CuiLabelWithOutline(CuiLabel label,string parent = "Hud",string color = "0.15 0.15 0.15 0.43", string dist = "1.1 -1.1",bool usealpha = false,string name = null) {
			if (string.IsNullOrEmpty(name))
				name = CuiHelper.GetGuid();
			CuiElement cuiElement = new CuiElement();
			cuiElement.Name = name;
			cuiElement.Parent = parent;
			cuiElement.FadeOut = label.FadeOut;
			cuiElement.Components.Add((ICuiComponent) label.Text);
			cuiElement.Components.Add((ICuiComponent) label.RectTransform);
			cuiElement.Components.Add((ICuiComponent) new CuiOutlineComponent {
				Color = color,
				Distance = dist,
				UseGraphicAlpha = usealpha
			});
			return cuiElement;
		}

		private void ShowOverlayText(BasePlayer player, string text, string subtext = "") {

			HideOverlayText(player);

            UserInfo userinfo = GetUserInfo(player);

            var elements = new CuiElementContainer();

			userinfo.overlay = elements.Add(
				CreatePanel("0.3 0.3", "0.7 0.35","0.15 0.15 0.15 0")
			);

			elements.Add(
				CuiLabelWithOutline(
				new CuiLabel {
					Text = { Text = (subtext != "") ? $"{text}\n<size=12>{subtext}</size>" : text,FontSize = 14,Align = TextAnchor.MiddleCenter,Color = "1.0 1.0 1.0 1.0" },
					RectTransform = { AnchorMin = "0 0",AnchorMax = "1 1" },
					FadeOut = 2f
				},
				userinfo.overlay)
			);

			CuiHelper.AddUi(player, elements);

            userinfo.overlaytext = text;
            userinfo.overlaysubtext = subtext;
        }
		
		private void HideOverlayText(BasePlayer player, float delay = 0) {
            UserInfo userinfo = GetUserInfo(player);

            if (delay > 0) {
				string overlay = userinfo.overlay;
                string beforetext = userinfo.overlaytext;
                string beforesub = userinfo.overlaysubtext;
				timer.Once(delay,() => {
					if (!string.IsNullOrEmpty(overlay))
						CuiHelper.DestroyUi(player,overlay);
                    if (beforetext == userinfo.overlaytext)
                        userinfo.overlaytext = string.Empty;
                    if (beforesub == userinfo.overlaysubtext)
                        userinfo.overlaysubtext = string.Empty;
                });
			} else {
				if (!string.IsNullOrEmpty(userinfo.overlay))
					CuiHelper.DestroyUi(player,userinfo.overlay);
                userinfo.overlaytext = string.Empty;
                userinfo.overlaysubtext = string.Empty;
            }
		}

        #endregion

        #region Config

        private class permlevel {
            public int pipelimit;
            public int upgradelimit;
        }

        private void registerpermlevels() {
            foreach (var l in permlevels) {
                permission.RegisterPermission($"jpipes.level.{l.Key}",this);
            }
        }

        private static float maxpipedist;
		private static float minpipedist;
		private static int updaterate;
        private string pipehotkey;
		private List<int> flowrates;
		private List<int> filtersizes;
		private bool nodecay;
        private Dictionary<string,permlevel> permlevels = new Dictionary<string, permlevel>();

        
        protected override void LoadDefaultConfig() {
			PrintWarning("Creating a new configuration file");
			Config.Clear();
			LoadConfig();
		}

		private void LoadConfig() {
            
            maxpipedist = ConfigGet<float>("maxpipedist",64);
			minpipedist = ConfigGet<float>("minpipedist",2);
			updaterate = ConfigGet("updaterate",2,(int x) => x > 0,"should be greater than 0");
			pipehotkey = ConfigGet("pipehotkey","p");
			flowrates = ConfigGet("flowrates",new List<int>() { 1,5,10,30,50 },(List<int> l) => l.Count == 5,"should contain 5 integers");
			filtersizes = ConfigGet("filtersizes",new List<int>() { 0,6,12,18,30 },(List<int> l) => l.Count == 5 && !l.Exists(x => x < 0 || x > 30),"should contain 5 integers with each val ue between 0 and 30");
            nodecay = ConfigGet("nodecay",true);

            var permlevelsval = Config["permlevels"];

            if (permlevelsval != null) {

                IDictionary valueDictionary = (IDictionary) permlevelsval;
                Dictionary<string,object> levels = new Dictionary<string,object>();

                foreach (object key in valueDictionary.Keys) {

                    IDictionary lvd = (IDictionary) valueDictionary[key];
                    Dictionary<string,object> permvals = new Dictionary<string,object>();

                    foreach (object lkey in lvd.Keys)
                        permvals.Add((string) lkey,lvd[lkey]);

                    permlevel npl = new permlevel();

                    if (permvals.ContainsKey("pipelimit"))
                        npl.pipelimit = (int) permvals["pipelimit"];
                    if (permvals.ContainsKey("upgradelimit"))
                        npl.upgradelimit = (int) permvals["upgradelimit"];

                    if (permvals.ContainsKey("pipelimit") || permvals.ContainsKey("upgradelimit")) 
                        permlevels.Add((string) key,npl);
                }
            } else {
                Config["permlevels"] = new Dictionary<string,object>();
                SaveConfig();
            }

            registerpermlevels();
        }

		// Config.Get<T>() with fallback, conditional warning, and saving
		// if val is null then set to fallback
		// if cond returns false then set to fallback (no saving) and print warning
		private T ConfigGet<T>(string configstring,T fallback,Func<T,bool> cond = null,string warning = null) {
			var val = Config.Get(configstring);
			if (val != null) {
				var valc = Config.ConvertValue<T>(val);
				if (cond != null && !cond(valc)) {
					if (warning != null)
						PrintWarning($"Config Error: \"{configstring}\" {warning}.  Reverting to default value.");
					return fallback;
				}
				return valc;
			}
			Config[configstring] = fallback;
			SaveConfig();
			return fallback;
		}

        #endregion

        #region Data

        // data structure for jPipeData.json file
        private class PipeSaveData {
			public Dictionary<ulong,jPipeData> p = new Dictionary<ulong,jPipeData>();
			public PipeSaveData() { }
		}

		// data structure for pipe save data
		private class jPipeData {
			public bool e = true;   // On/Off
			public int g;           // grade
			public uint s;          // source storage container id
			public uint d;          // destination storage container id
            public uint cs;         // source child storage container
            public uint cd;         // destination child storage container
            public float h;         // health
			public List<int> f;     // filter item ids
			public bool st;         // single stack mode
			public bool a;          // auto starter
			public bool fs;         // FurnaceSplitter On/Off
			public int fss;         // FurnaceSplitter starter
            public ulong o;         // Player ID of pipe owner
            public string on;       // name of pipe owner

            public jPipeData() { }

			public void fromPipe(jPipe p) {
				e = p.isEnabled;
				g = ((int) p.pipegrade == -1) ? 0 : (int) p.pipegrade;
				s = p.source.net.ID;
				d = p.dest.net.ID;
                cs = p.sourcechild;
                cd = p.destchild;
				h = p.health;
				f = p.filteritems;
				st = p.singlestack;
				a = p.autostarter;
				fs = p.fsplit;
				fss = p.fsstacks;
                o = p.ownerid;
                on = p.ownername;
            }

            public void toPipe(jPipe p) {
                p.isEnabled = e;
                p.pipegrade = (BuildingGrade.Enum) g;

                p.source = (BaseEntity) BaseNetworkable.serverEntities.Find(s);
                p.dest = (BaseEntity) BaseNetworkable.serverEntities.Find(d);
                p.sourcecont = getchildcont(p.source,cs);
                p.destcont = getchildcont(p.dest,cd);
                p.sourcechild = cs;
                p.destchild = cd;
                p.health = h;
                if (f != null) p.filteritems = f;
                p.singlestack = st;
                p.autostarter = a;
                p.fsplit = fs;
                p.fsstacks = fss;
                p.ownerid = o;
                p.ownername = on;
            }

            public void setContainers(BaseEntity start, BaseEntity end) {
                s = setCont(start,out cs);
                d = setCont(end,out cd);
            }

            private uint setCont(BaseEntity cont, out uint cid) {

                ResourceExtractorFuelStorage stor = cont.GetComponent<ResourceExtractorFuelStorage>();

                if (stor != null) {
                    switch (stor.panelName) {
                        case "generic":
                            cid = 1;
                            break;
                        case "fuelstorage":
                            cid = 2;
                            break;
                        default:
                            cid = 0;
                            break;
                    }

                    return stor.parentEntity.uid;
                }
                    
                cid = 0;
                return cont.net.ID;
            }
		}

		private static void LoadData<T>(ref T data) => data = Core.Interface.Oxide.DataFileSystem.ReadObject<T>("JPipes");
		private static void SaveData<T>(T data) => Core.Interface.Oxide.DataFileSystem.WriteObject("JPipes",data);

		#endregion

		#region Debug tools


		// Lists the ent's components and variables to player's chat

		void ListComponentsDebug(BasePlayer player,BaseEntity ent) {

			List<string> lines = new List<string>();
			string s = "-----------------------------------------\n";
			int limit = 1040;

			foreach (var c in ent.GetComponents<Component>()) {

				s += "[ " + c.GetType() + " : " + c.GetType().BaseType + " ]\n";
				//s += " <"+c.name+">\n";
				//if (c.sharedMesh != null) s += "-> "+c.sharedMesh.triangles.Length.ToString()+"\n";

				List<string> types = new List<string>();
				List<string> names = new List<string>();
				List<string> values = new List<string>();
				int typelength = 0;

				foreach (FieldInfo fi in c.GetType().GetFields()) {

					System.Object obj = (System.Object) c;
					string ts = fi.FieldType.Name;
					if (ts.Length > typelength)
						typelength = ts.Length;

					types.Add(ts);
					names.Add(fi.Name);

					var val = fi.GetValue(obj);
					if (val != null)
						values.Add(val.ToString());
					else
						values.Add("null");

				}

				for (int i = 0;i < types.Count;i++) {
					string typestring = types[i];

					string ns = "<size=11><color=#80c5ff>    " + typestring + "</color>" + "  " + names[i] + " = <color=#00ff00>" + values[i] + "</color></size>\n";

					if (s.Length + ns.Length > limit) {
						lines.Add(s);
						s = string.Empty;
					}

					s += ns;
				}
			}

			lines.Add(s);

			foreach (string ls in lines)
				PrintToChat(player,ls);

		}
		public static string stringspacing(int length) {
			string s = string.Empty;
			for (int i = 0;i < length;i++)
				s += "  ";
			return s;
		}

		#endregion

	}
}