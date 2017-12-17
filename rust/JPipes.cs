using System;
using System.Collections;
using System.Collections.Generic;
//using System.Reflection; // enable for ListComponentDebug
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;
//using Rust;

namespace Oxide.Plugins {

    [Info("JPipes", "TheGreatJ", "0.6.0", ResourceId = 2402)]
    class JPipes : RustPlugin {

        [PluginReference]
        private Plugin FurnaceSplitter;

        private Dictionary<ulong, UserInfo> users;
        private Dictionary<ulong, jPipe> regpipes = new Dictionary<ulong, jPipe>();
        private Dictionary<ulong, jSyncBox> regsyncboxes = new Dictionary<ulong, jSyncBox>();
        private PipeSaveData storedData;

        private static Color blue = new Color(0.2f, 0.4f, 1);
        private static Color orange = new Color(1f, 0.4f, 0.2f);
        private static string bluestring = "0.5 0.75 1.0 1.0";
        private static string orangestring = "1.0 0.75 0.5 1.0";

        #region Hooks

        void Init() {

            lang.RegisterMessages(new Dictionary<string, string> {
                ["ErrorFindFirst"] = "Failed to find first StorageContainer",
                ["ErrorFindSecond"] = "Failed to find second StorageContainer",
                ["ErrorAlreadyConnected"] = "Error: StorageContainers are already connected",
                ["ErrorNotItemCont"] = "Error: Second Container does not accept Items",
                ["ErrorNotLiquidCont"] = "Error: Second Container does not accept Liquid",
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
                ["SelectSubtextBind"] = "Press [{0}] to Cancel",
                ["SelectSubtextCmd"] = "Do /{0} to Cancel",
                ["PipeCreated"] = "Pipe has been created!",

                ["CopyingTextFirst"] = "Use the Hammer to select the jPipe to copy from",
                ["CopyingText"] = "Use the Hammer to Paste",
                ["CopyingSubtext"] = "Do /{0} c to Exit",

                ["RemovingText"] = "Use the Hammer to Remove Pipes",
                ["RemovingSubtext"] = "Do /{0} r to Exit",

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
                ["HelpCmdCommands"] = "<size=18>Commands</size>\n<color=#80c5ff>/{0} </color> start or stop placing a pipe\n<color=#80c5ff>/{0} c /{0}copy </color>or<color=#80c5ff> /{0} copy </color> copy pipe settings from one pipe to another\n <color=#80c5ff>/{0} r /{0}remove </color>or<color=#80c5ff> /{0} remove </color> remove pipe with hammer\n <color=#80c5ff>/{0} s /{0}stats </color>or<color=#80c5ff> /{0} stats </color> pipe status with how many pipes you are using\n <color=#80c5ff>/{0} h /{0}help </color>or<color=#80c5ff> /{0} help </color> JPipes in-game help",
                ["HelpCmdMenu"] = "<size=18>Pipe Menu</size><size=12> - hit pipe with hammer to open</size>\n<color=#80c5ff>Turn On / Turn Off</color> toggle item/liquid transfer\n<color=#80c5ff>Auto Starter</color> after a pipe sends an item to a furnace, recycler, refinery, mining quarry, or pump jack, it will attempt to start it\n<color=#80c5ff>Change Direction</color> makes the items go the other direction through the pipe\n<color=#80c5ff>Multi Stack / Single Stack</color> Multi Stack mode allows the pipe to create multiple stacks of the same item. Single Stack mode prevents the pipe from creating more than one stack of an item. Single Stack mode is mostly just for fueling furnaces to leave room for other items.\n<color=#80c5ff>Item Filter</color> when items are in the filter, only those items will be transferred through the pipe. When the filter is empty, all items will be transferred.",
                ["HelpCmdUpgrade"] = "<size=18>Upgrading Pipes</size>\nUse a Hammer and upgrade the pipe just like any other building\nEach upgrade level increases the pipe's flow rate and Item Filter size.",
                ["HelpBindTip"] = "JPipes Tip:\nYou can bind the /{0} command to a hotkey by putting\n\"bind {1} jpipes.create\" into the F1 console",

                ["StatsCmd"] = "<size=20><color=#80c5ff>j</color>Pipes Stats</size>\nYou have {0} jpipes currently in use.",
                ["StatsCmdLimit"] = "<size=20><color=#80c5ff>j</color>Pipes Stats</size>\nYou have {0} of {1} jpipes currently in use."
            }, this);

            LoadConfig();
            LoadCommands();

            users = new Dictionary<ulong, UserInfo>();
            storedData = new PipeSaveData();
        }

        void OnServerInitialized() {
            LoadData(ref storedData);

            //PipeLazyLoad();

            foreach (var p in storedData.p) {
                jPipe newpipe = new jPipe();
                if (newpipe.init(this, p.Key, p.Value, RemovePipe, MoveItem))
                    RegisterPipe(newpipe);
                else
                    PrintWarning(newpipe.initerr);
            }

            LoadEnd();
        }

        private int loadindex = 0;
        void PipeLazyLoad() {
            var p = storedData.p.ElementAt(loadindex);
            jPipe newpipe = new jPipe();
            if (newpipe.init(this, p.Key, p.Value, RemovePipe, MoveItem))
                RegisterPipe(newpipe);
            else
                PrintWarning(newpipe.initerr);

            loadindex += 1;
            if (loadindex >= storedData.p.Keys.Count) {
                LoadEnd();
                return;
            }

            NextFrame(PipeLazyLoad);
        }

        void LoadEnd() {
            Puts($"{regpipes.Count} Pipes Loaded");
            //Puts($"{regsyncboxes.Count} SyncBoxes Loaded");
        }

        private void Loaded() {
            permission.RegisterPermission("jpipes.use", this);
            permission.RegisterPermission("jpipes.admin", this);
        }

        void Unload() {
            foreach (var player in BasePlayer.activePlayerList) {
                UserInfo userinfo;
                if (!users.TryGetValue(player.userID, out userinfo))
                    continue;
                if (!string.IsNullOrEmpty(userinfo.menu))
                    CuiHelper.DestroyUi(player, userinfo.menu);
                if (!string.IsNullOrEmpty(userinfo.overlay))
                    CuiHelper.DestroyUi(player, userinfo.overlay);
            }

            SavePipes();
            UnloadPipes();
        }

        void OnNewSave(string filename) {
            regpipes.Clear();
            regsyncboxes.Clear();
            SavePipes();
        }

        void OnServerSave() => SavePipes();
		

        void OnPlayerInit(BasePlayer player) {

            GetUserInfo(player);

            player.SendConsoleCommand($"bind {pipehotkey} jpipes.create");
        }

        void OnPlayerDisconnected(BasePlayer player) {
            users.Remove(player.userID);
        }
		
        void OnHammerHit(BasePlayer player, HitInfo hit) {

			//Puts(hit.HitEntity.ToString());
			//ListComponentsDebug(player, hit.HitEntity);
			//ListComponentsDebug(player, player);

			UserInfo userinfo = GetUserInfo(player);

            if (hit.HitEntity.GetComponent<StorageContainer>() != null) { // if we hit a StorageContainer

                if (userinfo.state == UserState.placing && userinfo.placeend == null && checkcontwhitelist(hit.HitEntity)) {
                    if (checkcontprivlage(hit.HitEntity, player)) {
                        // select first
                        if (userinfo.placestart == null) {
                            userinfo.placestart = hit.HitEntity;

                            ShowOverlayText(player, lang.GetMessage("SelectSecond", this, player.UserIDString), string.Format(lang.GetMessage(userinfo.isUsingBind ? "SelectSubtextBind" : "SelectSubtextCmd", this, player.UserIDString), userinfo.isUsingBind ? pipehotkey.ToUpper() : pipecommandprefix));
                        } else if (userinfo.placestart != null) { // select second
                            userinfo.placeend = hit.HitEntity;
                            NewPipe(player, userinfo);
                        }
                    } else {
                        ShowOverlayText(player, lang.GetMessage("ErrorPrivilegeAttach", this, player.UserIDString));
                        timer.Once(2f, () => {
                            ShowOverlayText(player, lang.GetMessage((userinfo.placestart == null) ? "SelectFirst" : "SelectSecond", this, player.UserIDString), string.Format(lang.GetMessage(userinfo.isUsingBind ? "SelectSubtextBind" : "SelectSubtextCmd", this, player.UserIDString), userinfo.isUsingBind ? pipehotkey.ToUpper() : pipecommandprefix));
                        });
                    }
                }
            } else {
                jPipeSegChild s = hit.HitEntity.GetComponent<jPipeSegChild>();
                if (s != null) { // if we hit a pipe
                    if (!commandperm(player))
                        return;
                    if (checkbuildingprivlage(player)) {
                        if (userinfo.state == UserState.copying) { // if user is copying
                            if (userinfo.clipboard == null) {

                                userinfo.clipboard = new jPipeData();
                                userinfo.clipboard.fromPipe(s.pipe);

                                ShowOverlayText(player, lang.GetMessage("CopyingText", this, player.UserIDString), string.Format(lang.GetMessage("CopyingSubtext", this, player.UserIDString), pipecommandprefix));

                            } else {
                                userinfo.clipboard.s = s.pipe.sourcecont.net.ID;
                                userinfo.clipboard.d = s.pipe.destcont.net.ID;

                                s.pipe.Destroy();

                                jPipe newpipe = new jPipe();

                                // initalize pipe
                                if (newpipe.init(this, pipeidgen(), userinfo.clipboard, RemovePipe, MoveItem)) {
                                    // pipe was created so register it
                                    RegisterPipe(newpipe);
                                } else {
                                    // pipe error
                                    ShowOverlayText(player, lang.GetMessage(newpipe.initerr, this, player.UserIDString));
                                    newpipe = null;
                                }
                            }

                        } else if (userinfo.state == UserState.removing) { // if user is removing

                            s.pipe.Destroy();

                        } else if (userinfo.state == UserState.none) { // if user is not in a command
                            s.pipe.OpenMenu(player, userinfo);
                        }
                    } else {
                        ShowOverlayText(player, lang.GetMessage("ErrorPrivilegeModify", this, player.UserIDString));
                        HideOverlayText(player, 2f);
                    }
                }
            }
        }

        void OnStructureDemolish(BaseCombatEntity entity, BasePlayer player) {
            jPipeSegChild p = entity.GetComponent<jPipeSegChild>();
            if (p != null && p.pipe != null)
                p.pipe.OnSegmentKilled();
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info) {
            if (entity is BuildingBlock) {
                jPipeSegChild p = entity.GetComponent<jPipeSegChild>();
                if (p != null && p.pipe != null)
                    p.pipe.OnSegmentKilled();
            }
        }

        void OnEntityKill(BaseNetworkable entity) {
            if (entity is BuildingBlock) {
                jPipeSegChild p = entity.GetComponent<jPipeSegChild>();
                if (p != null && p.pipe != null)
                    p.pipe.OnSegmentKilled();
            }
        }

        bool? OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade) {
            jPipeSegChild p = entity.GetComponent<jPipeSegChild>();
            if (p != null && p.pipe != null) {
                if (!commandperm(player))
                    return false;
                int upgradelimit = getplayerupgradelimit(player);
                if (upgradelimit != -1 && upgradelimit < (int) grade) {
                    //Puts(upgradelimit.ToString());
                    //Puts(((int) grade).ToString());

                    ShowOverlayText(player, string.Format(lang.GetMessage("ErrorUpgradeLimit", this, player.UserIDString), (BuildingGrade.Enum) upgradelimit));
                    HideOverlayText(player, 2f);

                    return false;
                }
                p.pipe.Upgrade(grade);
            }
            return null;
        }

        void OnStructureRepair(BaseCombatEntity entity, BasePlayer player) {
            if (GetUserInfo(player).state != UserState.none)
                return;

            jPipeSegChild p = entity.GetComponent<jPipeSegChild>();
            if (p != null && p.pipe != null)
                p.pipe.SetHealth(entity.GetComponent<BuildingBlock>().health);
        }

        // pipe damage handling
        bool? OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo) {

			if (entity != null && hitInfo != null) {

				jPipeSegChild p = entity.GetComponent<jPipeSegChild>();
				if (p != null && p.pipe != null) {

					if (nodecay)
						hitInfo.damageTypes.Scale(Rust.DamageType.Decay, 0f); // no decay damage
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
			}
			return null;
		}

		// disable xmas lights pickup
		bool? CanPickupEntity(BaseCombatEntity entity, BasePlayer player) {
			if (entity.GetComponent<jPipeSegChildLights>() != null) return false;
			return null;
		}


		// When item is added to filter
		ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item) {

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

        bool? CanVendingAcceptItem(VendingMachine container, Item item) {
            //Puts(item.ToString());

            BasePlayer ownerPlayer = item.GetOwnerPlayer();
            if (item.parent == null || container.inventory.itemList.Contains(item))
                return true;
            if ((UnityEngine.Object) ownerPlayer == (UnityEngine.Object) null)
                return true;
            return container.CanPlayerAdmin(ownerPlayer);
        }

        // when item is removed from filter it is destroyed
        void OnItemRemovedFromContainer(ItemContainer container, Item item) {
            if (container == null || item == null || container.entityOwner == null || container.entityOwner.GetComponent<jPipeFilterStash>() == null)
                return;
            item.Remove();
        }

        // when item is taken from filter, it can't be stacked
        bool? CanStackItem(Item targetItem, Item item) {
            if (item.parent == null || item.parent.entityOwner == null || item.parent.entityOwner.GetComponent<jPipeFilterStash>() == null)
                return null;
            return false;
        }

        #endregion

        #region Commands

        private bool commandperm(BasePlayer player) {
            if (!(permission.UserHasPermission(player.UserIDString, "jpipes.use") || permission.UserHasPermission(player.UserIDString, "jpipes.admin"))) {
                ShowOverlayText(player, lang.GetMessage("ErrorCmdPerm", this, player.UserIDString));
                HideOverlayText(player, 2f);
                return false;
            }
            return true;
        }

        private void LoadCommands() {
            AddCovalenceCommand(pipecommandprefix, "pipemainchat");
            AddCovalenceCommand($"{pipecommandprefix}help", "cmdpipehelp");
            AddCovalenceCommand($"{pipecommandprefix}copy", "cmdpipecopy");
            AddCovalenceCommand($"{pipecommandprefix}remove", "cmdpiperemove");
            AddCovalenceCommand($"{pipecommandprefix}stats", "cmdpipestats");
            //AddCovalenceCommand($"{pipecommandprefix}link", "cmdpipelink");
        }
        private void cmdpipehelp(IPlayer cmdplayer, string cmd, string[] args) => pipehelp(BasePlayer.Find(cmdplayer.Id), cmd, args);
        private void cmdpipecopy(IPlayer cmdplayer, string cmd, string[] args) => pipecopy(BasePlayer.Find(cmdplayer.Id), cmd, args);
        private void cmdpiperemove(IPlayer cmdplayer, string cmd, string[] args) => piperemove(BasePlayer.Find(cmdplayer.Id), cmd, args);
        private void cmdpipestats(IPlayer cmdplayer, string cmd, string[] args) => pipestats(BasePlayer.Find(cmdplayer.Id), cmd, args);
        private void cmdpipelink(IPlayer cmdplayer, string cmd, string[] args) => pipelink(BasePlayer.Find(cmdplayer.Id), cmd, args);

        // [ChatCommand("p")]
        private void pipemainchat(IPlayer cmdplayer, string cmd, string[] args) {
            BasePlayer player = BasePlayer.Find(cmdplayer.Id);

            if (!commandperm(player))
                return;

            if (args.Length > 0) {
                switch (args[0]) {
                    case "h":
                    case "help":
                        pipehelp(player, cmd, args);
                        break;
                    case "c":
                    case "copy":
                        pipecopy(player, cmd, args);
                        break;
                    case "r":
                    case "remove":
                        piperemove(player, cmd, args);
                        break;
                    case "s":
                    case "stats":
                        pipestats(player, cmd, args);
                        break;
                    case "l":
                    case "link":
                        pipelink(player, cmd, args);
                        break;
                }
            } else {
                startplacingpipe(player, false);
            }
        }

        //[ChatCommand("phelp")]
        private void pipehelp(BasePlayer player, string cmd, string[] args) {
            if (!commandperm(player))
                return;
            PrintToChat(player, lang.GetMessage("HelpCmdTitle", this, player.UserIDString));
            PrintToChat(player, string.Format(lang.GetMessage("HelpCmdCommands", this, player.UserIDString), pipecommandprefix));
            PrintToChat(player, lang.GetMessage("HelpCmdMenu", this, player.UserIDString));
            PrintToChat(player, lang.GetMessage("HelpCmdUpgrade", this, player.UserIDString));
        }

        //[ChatCommand("pcopy")]
        private void pipecopy(BasePlayer player, string cmd, string[] args) {
            if (!commandperm(player))
                return;
            UserInfo userinfo = GetUserInfo(player);

            userinfo.state = userinfo.state == UserState.copying ? UserState.none : UserState.copying;
            userinfo.placeend = null;
            userinfo.placestart = null;

            if (userinfo.state == UserState.copying) {
                ShowOverlayText(player, lang.GetMessage("CopyingTextFirst", this, player.UserIDString), string.Format(lang.GetMessage("CopyingSubtext", this, player.UserIDString), pipecommandprefix));
            } else {
                HideOverlayText(player);
                userinfo.clipboard = null;
            }

        }

        //[ChatCommand("premove")]
        private void piperemove(BasePlayer player, string cmd, string[] args) {
            if (!commandperm(player))
                return;
            UserInfo userinfo = GetUserInfo(player);

            userinfo.state = userinfo.state == UserState.removing ? UserState.none : UserState.removing;
            userinfo.placeend = null;
            userinfo.placestart = null;
            userinfo.clipboard = null;

            if (userinfo.state == UserState.removing) {
                ShowOverlayText(player, lang.GetMessage("RemovingText", this, player.UserIDString), string.Format(lang.GetMessage("RemovingSubtext", this, player.UserIDString), pipecommandprefix));
            } else {
                HideOverlayText(player);
            }
        }

        //[ChatCommand("pstats")]
        private void pipestats(BasePlayer player, string cmd, string[] args) {
            if (!commandperm(player))
                return;
            UserInfo userinfo = GetUserInfo(player);
            int pipelimit = getplayerpipelimit(player);

            if (pipelimit == -1)
                PrintToChat(player, string.Format(lang.GetMessage("StatsCmd", this, player.UserIDString), userinfo.pipes.Count));
            else
                PrintToChat(player, string.Format(lang.GetMessage("StatsCmdLimit", this, player.UserIDString), userinfo.pipes.Count, pipelimit));
        }

        //[ChatCommand("plink")]
        private void pipelink(BasePlayer player, string cmd, string[] args) {
            if (!commandperm(player))
                return;
            startlinking(player, false);
        }

        [ConsoleCommand("jpipes.create")]
        private void pipecreate(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            if (!commandperm(p))
                return;
            startplacingpipe(p, true);
        }

        //[ConsoleCommand("jpipes.link")]
        //private void pipelink(ConsoleSystem.Arg arg) {
        //    BasePlayer p = arg.Player();
        //    if (!commandperm(p))
        //        return;
        //    startlinking(p, true);
        //}

        [ConsoleCommand("jpipes.openmenu")]
        private void pipeopenmenu(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            if (!commandperm(p))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe)) {
                pipe.OpenMenu(p, GetUserInfo(p));
            }
        }

        [ConsoleCommand("jpipes.closemenu")]
        private void pipeclosemenu(ConsoleSystem.Arg arg) {
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe)) {
                BasePlayer p = arg.Player();
                pipe.CloseMenu(p, GetUserInfo(p));
            }
        }

        [ConsoleCommand("jpipes.closemenudestroy")]
        private void pipeclosemenudestroy(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            UserInfo userinfo = GetUserInfo(p);

            if (!string.IsNullOrEmpty(userinfo.menu))
                CuiHelper.DestroyUi(p, userinfo.menu);
            userinfo.isMenuOpen = false;
        }

        [ConsoleCommand("jpipes.refreshmenu")]
        private void piperefreshmenu(ConsoleSystem.Arg arg) {
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe)) {
                BasePlayer p = arg.Player();
                UserInfo userinfo = GetUserInfo(p);
                pipe.OpenMenu(p, userinfo);
            }
        }

        [ConsoleCommand("jpipes.changedir")]
        private void cmdpipechangedir(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe)) {
                pipe.ChangeDirection();
            }
        }

        [ConsoleCommand("jpipes.openfilter")]
        private void cmdpipeopenfilter(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            if (!commandperm(p))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe)) {
                UserInfo userinfo = GetUserInfo(p);
                pipe.OpenFilter(p);
                pipe.CloseMenu(p, userinfo);
            }
        }

        [ConsoleCommand("jpipes.turnon")]
        private void pipeturnon(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            if (!commandperm(p))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe))
                pipe.mainlogic.pipeEnable(p);
        }

        [ConsoleCommand("jpipes.turnoff")]
        private void pipeturnoff(ConsoleSystem.Arg arg) {
            BasePlayer p = arg.Player();
            if (!commandperm(p))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe))
                pipe.mainlogic.pipeDisable(p);
        }

        [ConsoleCommand("jpipes.autostarton")]
        private void pipeautostarton(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe))
                pipe.autostarter = true;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.autostartoff")]
        private void pipeautostartoff(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe))
                pipe.autostarter = false;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.stackon")]
        private void pipestackon(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe))
                pipe.singlestack = true;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.stackoff")]
        private void pipestackoff(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe))
                pipe.singlestack = false;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.fsenable")]
        private void pipeFSenable(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe))
                pipe.fsplit = true;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.fsdisable")]
        private void pipeFSdisable(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe))
                pipe.fsplit = false;
            pipe.RefreshMenu();
        }

        [ConsoleCommand("jpipes.fsstack")]
        private void pipeFSstack(ConsoleSystem.Arg arg) {
            if (!commandperm(arg.Player()))
                return;
            jPipe pipe;
            if (regpipes.TryGetValue((ulong) int.Parse(arg.Args[0]), out pipe))
                pipe.fsstacks = int.Parse(arg.Args[1]);
            pipe.RefreshMenu();
        }

        #endregion

        #region Classes

        // user data for chat commands
        private class UserInfo {
            public UserState state = UserState.none;
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
            public Dictionary<ulong, jPipe> pipes = new Dictionary<ulong, jPipe>();
        }

        private UserInfo GetUserInfo(BasePlayer player) => GetUserInfo(player.userID);

        private UserInfo GetUserInfo(ulong id) {
            UserInfo userinfo;
            if (!users.TryGetValue(id, out userinfo))
                users[id] = userinfo = new UserInfo();
            return userinfo;
        }

        private enum UserState {
            none,
            placing,
            copying,
            removing,
            linking
        };

        // main pipe class
        private class jPipe {

            public Action<ulong, bool> remover;
            public Action<Item, int, StorageContainer, int> moveitem;

            private JPipes pipeplugin;

            public ulong id;
            public string initerr = string.Empty;
            public string debugstring = string.Empty;

            public ulong ownerid;
            public string ownername;

            public bool isEnabled = true;
            public bool isWaterPipe = false;

            public BaseEntity mainparent;

            // parents of storage containers
            public BaseEntity source;
            public BaseEntity dest;

            public Vector3 sourcepos;
            public Vector3 endpos;

            public string sourceiconurl;
            public string endiconurl;

            // storage containers
            public StorageContainer sourcecont;
            public StorageContainer destcont;

            // storage container child id
            public uint sourcechild = 0;
            public uint destchild = 0;

            public jPipeLogic mainlogic;

            public BuildingGrade.Enum pipegrade = BuildingGrade.Enum.Twigs;
            public float health;

            public List<BaseEntity> pillars = new List<BaseEntity>();
            private BaseEntity filterstash;
            private StorageContainer stashcont;
            private int lookingatstash = 0;

            public bool singlestack = false; // change this to enum and add fuel mode
            public bool autostarter = false;

            private bool destisstartable = false;

            public List<int> filteritems = new List<int>();

            public bool fsplit = false;
            public int fsstacks = 2;

            public List<BasePlayer> playerslookingatmenu = new List<BasePlayer>();
            public List<BasePlayer> playerslookingatfilter = new List<BasePlayer>();

            private float distance;
            private Quaternion rotation;

            // constructor
            public jPipe() { }

            // init
            public bool init(JPipes pplug, ulong nid, jPipeData data, Action<ulong, bool> rem, Action<Item, int, StorageContainer, int> mover) {

                pipeplugin = pplug;

                data.toPipe(this);

                if (source == null || sourcecont == null) {
                    initerr = "ErrorFindFirst";
                    return false;
                }
                if (dest == null || destcont == null) {
                    initerr = "ErrorFindSecond";
                    return false;
                }

                destisstartable = isStartable(dest);
                isWaterPipe = dest is LiquidContainer && source is LiquidContainer;

                remover = rem;
                moveitem = mover;
                id = nid;

                sourcepos = sourcecont.CenterPoint() + containeroffset(sourcecont);
                endpos = destcont.CenterPoint() + containeroffset(destcont);

                distance = Vector3.Distance(sourcepos, endpos);
                rotation = Quaternion.LookRotation(endpos - sourcepos) * Quaternion.Euler(90, 0, 0);

                // create pillars

                int segments = (int) Mathf.Ceil(distance / pipesegdist);
                float segspace = (distance - pipesegdist) / (segments - 1);

                initerr = "";

                for (int i = 0; i < segments; i++) {

                    //float offset = (segspace * i);
                    //Vector3 pos = sourcepos + ((rotation * Vector3.up) * offset);

                    // create pillar

                    BaseEntity ent;

                    if (i == 0) {
                        // the position thing centers the pipe if there is only one segment
                        ent = GameManager.server.CreateEntity("assets/prefabs/building core/pillar/pillar.prefab", (segments == 1) ? (sourcepos + ((rotation * Vector3.up) * ((distance - pipesegdist) * 0.5f))) : sourcepos, rotation);
                        mainlogic = jPipeLogic.Attach(ent, this, updaterate, pipeplugin.flowrates[0]);
                        mainparent = ent;
					} else {
                        //ent = GameManager.server.CreateEntity("assets/prefabs/building core/pillar/pillar.prefab", sourcepos + rotation * (Vector3.up * (segspace * i) + ((i % 2 == 0) ? Vector3.zero : pipefightoffset)), rotation);
                        // position based on parent
                        ent = GameManager.server.CreateEntity("assets/prefabs/building core/pillar/pillar.prefab", Vector3.up * (segspace * i) + ((i % 2 == 0) ? Vector3.zero : pipefightoffset));
						
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

					jPipeSegChild.Attach(ent, this);

                    if (i != 0)
                    ent.SetParent(mainparent);

					if (pipeplugin.xmaslights) {

						BaseEntity lights = GameManager.server.CreateEntity("assets/prefabs/misc/xmas/christmas_lights/xmas.lightstring.deployed.prefab", (Vector3.up * pipesegdist * 0.5f) + (Vector3.forward * 0.13f) + (Vector3.up * (segspace * i) + ((i % 2 == 0) ? Vector3.zero : pipefightoffset)), Quaternion.Euler(0, -60, 90));
						lights.enableSaving = false;
						lights.Spawn();
						lights.SetParent(mainparent);
						jPipeSegChildLights.Attach(lights, this);
					}

					pillars.Add(ent);
                    ent.enableSaving = false;

                }

                mainlogic.flowrate = ((int) pipegrade == -1) ? pipeplugin.flowrates[0] : pipeplugin.flowrates[(int) pipegrade];

                if (health != 0)
                    SetHealth(health);

                // cache container icon urls
                sourceiconurl = GetContIcon(source);
                endiconurl = GetContIcon(dest);

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
                    else if (panel == "bbq")
                        return contoffset.bbq;
                    else
                        return contoffset.furnace;
                    //} else if (e is ResourceExtractorFuelStorage) {
                    //if (e.GetComponent<StorageContainer>().panelName == "fuelstorage") {
                    //    return contoffset.pumpfuel;
                    //} else {
                    //    return e.transform.rotation * contoffset.pumpoutput;
                    //}
                } else if (e is AutoTurret) {
                    return contoffset.turret;
                } else if (e is SearchLight) {
                    return contoffset.searchlight;
                } else if (e is WaterCatcher) {
                    if (e.GetComponent<WaterCatcher>()._collider.ToString().Contains("small"))
                        return contoffset.smallwatercatcher;
                    return contoffset.largewatercatcher;
                } else if (e is LiquidContainer) {
                    if (e.GetComponent<LiquidContainer>()._collider.ToString().Contains("purifier"))
                        return contoffset.waterpurifier;
                    return contoffset.waterbarrel;
                }
                return Vector3.zero;
            }
            private bool isStartable(BaseEntity e) => e is BaseOven || e is Recycler || destchild == 2;

            public void OpenFilter(BasePlayer player) {
                if (filterstash != null) {
                    LookInFilter(player, filterstash.GetComponent<StashContainer>());
                    return;
                }

                if (pipeplugin.filtersizes[(int) pipegrade] == 0)
                    return;

                filterstash = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", new Vector3(0, 0, -10000f), Quaternion.Euler(-90, 0, 0));

                filterstash.SetParent(mainparent);

                stashcont = filterstash.GetComponent<StorageContainer>();

                if (stashcont != null) {
                    stashcont.inventorySlots = pipeplugin.filtersizes[(int) pipegrade];
                    stashcont.SendNetworkUpdate();
                    filterstash.Spawn();
                }

                // load content

                jPipeFilterStash f = jPipeFilterStash.Attach(filterstash, FilterCallback, UpdateFilterItems);

                foreach (int i in filteritems) {
                    Item item = ItemManager.CreateByItemID(i, 1);
                    item.MoveToContainer(stashcont.inventory);
                }

                f.loading = false;

                //stashcont.DecayTouch();
                stashcont.UpdateNetworkGroup();
                stashcont.SendNetworkUpdateImmediate();

                stashcont.globalBroadcast = true;

                LookInFilter(player, stashcont);
            }

            public void LookInFilter(BasePlayer player, StorageContainer stash) {
                stash.SetFlag(BaseEntity.Flags.Open, true, false);
                player.inventory.loot.StartLootingEntity(stash, false);
                player.inventory.loot.AddContainer(stash.inventory);
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", stash.panelName);
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
                // swap the containers
                BaseEntity newdest = source;
                source = dest;
                dest = newdest;

                StorageContainer newdestcont = sourcecont;
                sourcecont = destcont;
                destcont = newdestcont;

                uint newdestchild = sourcechild;
                sourcechild = destchild;
                destchild = newdestchild;

                sourceiconurl = GetContIcon(source);
                endiconurl = GetContIcon(dest);

                destisstartable = isStartable(dest);
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

                remover(id, removeme);
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

			private static string ArrowString(int count) {
				if (count == 1)
					return ">>";
				if (count == 2)
					return ">>>";
				if (count == 3)
					return ">>>>";
				if (count == 4)
					return ">>>>>";
				return ">";
			}

			public void OpenMenu(BasePlayer player, UserInfo userinfo) {

                CloseMenu(player, userinfo);

                playerslookingatmenu.Add(player);

                Vector2 size = new Vector2(0.125f, 0.175f);
                float margin = 0.05f;

                var elements = new CuiElementContainer();

                userinfo.menu = elements.Add(
                    new CuiPanel {
                        Image = { Color = "0.15 0.15 0.15 0.86" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        CursorEnabled = true
                    }
                );

                // close when you click outside of the window
                elements.Add(
                    new CuiButton {
                        Button = { Command = $"jpipes.closemenu {id}", Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = string.Empty }
                    }, userinfo.menu
                );

                string window = elements.Add(new CuiPanel {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = {
                            AnchorMin = $"{0.5f-size.x} {0.5f-size.y}",
                            AnchorMax = $"{0.5f+size.x} {0.5f+size.y}"
                        }
                }, userinfo.menu
                );

                string contentleft = elements.Add(new CuiPanel {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = {
                            AnchorMin = $"{margin} {0-margin*0.25f}",
                            AnchorMax = $"{0.5f-margin} {1-margin*0.5f}"
                        },
                    CursorEnabled = false
                }, window
                );

                string contentright = elements.Add(new CuiPanel {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = {
                            AnchorMin = "0.5 0",
                            AnchorMax = "1 1"
                        }
                }, window
                );

                // title
                elements.Add(
                    CreateLabel(pipeplugin.lang.GetMessage("MenuTitle", pipeplugin, player.UserIDString), 1, 1, TextAnchor.UpperLeft, 32, "0", "1", "1 1 1 0.8"),
                    contentleft
                );

                // flow 
                string FlowMain = elements.Add(new CuiPanel {
                    Image = { Color = "1 1 1 0" },
                    RectTransform = {
                            AnchorMin = $"{margin*0.75f} 0.59",
                            AnchorMax = $"{0.5f-(margin*0.75f)} 0.78"
                        }
                }, window
                );

                string FlowPipe = elements.Add(new CuiPanel {
                    Image = { Color = "1 1 1 0.04" },
                    RectTransform = {
                            AnchorMin = "0.2 0.33",
                            AnchorMax = "0.8 0.66"
                        }
                }, FlowMain
                );

                elements.Add(
                    CreateLabel(ArrowString((int) pipegrade), 1, 1, TextAnchor.MiddleCenter, 12, "0", "1", "1 1 1 1"),
                    FlowPipe
                );

                elements.Add(
                    CreateItemIcon(FlowMain, "0 0", "0.35 1", sourceiconurl)
                );
                elements.Add(
                    CreateItemIcon(FlowMain, "0.65 0", "1 1", endiconurl)
                );

                //Furnace Splitter
                if ((BaseEntity) destcont is BaseOven && pipeplugin.FurnaceSplitter != null) {

                    string FSmain = elements.Add(new CuiPanel {
                        Image = { Color = "1 1 1 0" },
                        RectTransform = {
                               AnchorMin = $"{margin*0.5f} 0.23",
                               AnchorMax = $"{0.5f-(margin*0.5f)} 0.53"
                           }
                    }, window
                    );

                    elements.Add(
                        CreateItemIcon(window, "0.348 0.538", "0.433 0.59", "http://i.imgur.com/BwJN0rt.png", "1 1 1 0.04")
                    );

                    string FShead = elements.Add(new CuiPanel {
                        Image = { Color = "1 1 1 0.04" },
                        RectTransform = {
                               AnchorMin = "0 0.7",
                               AnchorMax = "1 1"
                           }
                    }, FSmain
                    );

                    elements.Add(
                       CreateLabel("Furnace Splitter", 1, 1, TextAnchor.MiddleCenter, 12, "0", "1", "1 1 1 0.8"),
                       FShead
                    );

                    string FScontent = elements.Add(new CuiPanel {
                        Image = { Color = "1 1 1 0" },
                        RectTransform = {
                               AnchorMin = "0 0",
                               AnchorMax = "1 0.66"
                           }
                    }, FSmain
                    );

                    // elements.Add(
                    //    CreateLabel("ETA: 0s (0 wood)", 1, 0.3f, TextAnchor.MiddleLeft, 10, $"{margin}", "1", "1 1 1 0.8"),
                    //    FScontent
                    // );

                    if (fsplit) {
                        elements.Add(
                            CreateButton($"jpipes.fsdisable {id}", 1.2f, 0.4f, 12, pipeplugin.lang.GetMessage("MenuTurnOff", pipeplugin, player.UserIDString), $"{margin}", $"{0.5f - (margin * 0.5f)}", "0.59 0.27 0.18 0.8", "0.89 0.49 0.31 1"),
                            FScontent
                        );
                    } else {
                        elements.Add(
                            CreateButton($"jpipes.fsenable {id}", 1.2f, 0.4f, 12, pipeplugin.lang.GetMessage("MenuTurnOn", pipeplugin, player.UserIDString), $"{margin}", $"{0.5f - (margin * 0.5f)}", "0.43 0.51 0.26 0.8", "0.65 0.76 0.47 1"),
                            FScontent
                        );
                    }

                    // elements.Add(
                    //    CreateButton($"jpipes.autostartoff {id}", 2.2f, 0.25f, 11, "Trim fuel", $"{0.5f + (margin * 0.5f)}", $"{1 - (margin)}", "1 1 1 0.08", "1 1 1 0.8"),
                    //    FScontent
                    // );

                    float arrowbuttonmargin = 0.1f;
                    elements.Add(
                        CreateButton($"jpipes.fsstack {id} {fsstacks - 1}", 2.4f, 0.4f, 12, "<", $"{margin}", $"{margin + arrowbuttonmargin}", "1 1 1 0.08", "1 1 1 0.8"),
                        FScontent
                    );
                    elements.Add(
                        CreateLabel($"{fsstacks}", 3, 0.31f, TextAnchor.MiddleCenter, 12, $"{margin + arrowbuttonmargin}", $"{0.5f - (margin * 0.5f) - arrowbuttonmargin}", "1 1 1 0.8"),
                        FScontent
                    );

                    //elements.Add(
                    //    CuiInputField(FScontent,$"jpipes.fsstack {id} ",$"{fsstacks}",12,2)
                    //);

                    elements.Add(
                        CreateButton($"jpipes.fsstack {id} {fsstacks + 1}", 2.4f, 0.4f, 12, ">", $"{0.5f - (margin * 0.5f) - arrowbuttonmargin}", $"{0.5f - (margin * 0.5f)}", "1 1 1 0.08", "1 1 1 0.8"),
                        FScontent
                    );

                    elements.Add(
                        CreateLabel("Total Stacks", 3, 0.31f, TextAnchor.MiddleLeft, 12, $"{(margin * 0.5f) + 0.5f}", "1", "1 1 1 0.8"),
                        FScontent
                    );
                }

                string infostring = string.Format(pipeplugin.lang.GetMessage("MenuInfo", pipeplugin, player.UserIDString), ownername, isWaterPipe ? $"{mainlogic.flowrate}ml" : mainlogic.flowrate.ToString(), Math.Round(distance, 2));

                // info
                elements.Add(
                    CreateLabel(
                        debugstring == string.Empty ? infostring : $"{infostring}\nDebug:\n{debugstring}",
                        1, 1, TextAnchor.LowerLeft, 16, "0", "1", "1 1 1 0.4"
                    ), contentleft
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
                        CreateButton($"jpipes.turnoff {id}", 1 + buttonoffset * 0, buttonsize, 18, pipeplugin.lang.GetMessage("MenuTurnOff", pipeplugin, player.UserIDString), "0", "1", "0.59 0.27 0.18 0.8", "0.89 0.49 0.31 1"),
                        contentright
                    );
                } else {
                    elements.Add(
                        CreateButton($"jpipes.turnon {id}", 1 + buttonoffset * 0, buttonsize, 18, pipeplugin.lang.GetMessage("MenuTurnOn", pipeplugin, player.UserIDString), "0", "1", "0.43 0.51 0.26 0.8", "0.65 0.76 0.47 1"),
                        contentright
                    );
                }

                if (destisstartable) {
                    if (autostarter) {
                        elements.Add(
                            CreateButton($"jpipes.autostartoff {id}", 2 + buttonoffset * 1, buttonsize, 18, pipeplugin.lang.GetMessage("MenuAutoStarter", pipeplugin, player.UserIDString), "0", "1", "0.43 0.51 0.26 0.8", "0.65 0.76 0.47 1"),
                            contentright
                        );
                    } else {
                        elements.Add(
                            CreateButton($"jpipes.autostarton {id}", 2 + buttonoffset * 1, buttonsize, 18, pipeplugin.lang.GetMessage("MenuAutoStarter", pipeplugin, player.UserIDString), "0", "1", "0.59 0.27 0.18 0.8", "0.89 0.49 0.31 1"),
                            contentright
                        );
                    }
                }

                elements.Add(
                    CreateButton($"jpipes.changedir {id}", (destisstartable) ? 3 + buttonoffset * 2 : 2 + buttonoffset * 1, buttonsize, 18, pipeplugin.lang.GetMessage("MenuChangeDirection", pipeplugin, player.UserIDString), "0", "1", "1 1 1 0.08", "1 1 1 0.8"),
                    contentright
                );

                if ((!fsplit || pipeplugin.FurnaceSplitter == null) && !isWaterPipe) {
                    if (singlestack) {
                        elements.Add(
                            CreateButton($"jpipes.stackoff {id}", (destisstartable) ? 4 + buttonoffset * 3 : 3 + buttonoffset * 2, buttonsize, 18, pipeplugin.lang.GetMessage("MenuSingleStack", pipeplugin, player.UserIDString), "0", "1", "1 1 1 0.08", "1 1 1 0.8"),
                            contentright
                        );
                    } else {
                        elements.Add(
                            CreateButton($"jpipes.stackon {id}", (destisstartable) ? 4 + buttonoffset * 3 : 3 + buttonoffset * 2, buttonsize, 18, pipeplugin.lang.GetMessage("MenuMultiStack", pipeplugin, player.UserIDString), "0", "1", "1 1 1 0.08", "1 1 1 0.8"),
                            contentright
                        );
                    }
                } else {
                    elements.Add(
                        CreateButton("", (destisstartable) ? 4 + buttonoffset * 3 : 3 + buttonoffset * 2, buttonsize, 18, pipeplugin.lang.GetMessage("MenuMultiStack", pipeplugin, player.UserIDString), "0", "1", "1 1 1 0.08", "1 1 1 0.2"),
                        contentright
                    );
                }

                // disable filter button if filtersize is 0
                if (pipeplugin.filtersizes[(int) pipegrade] == 0 || isWaterPipe) {
                    elements.Add(
                        CreateButton("", (destisstartable) ? 5 + buttonoffset * 4 : 4 + buttonoffset * 3, buttonsize, 18, pipeplugin.lang.GetMessage("MenuItemFilter", pipeplugin, player.UserIDString), "0", "1", "1 1 1 0.08", "1 1 1 0.2"),
                        contentright
                    );
                } else {
                    elements.Add(
                        CreateButton($"jpipes.openfilter {id}", (destisstartable) ? 5 + buttonoffset * 4 : 4 + buttonoffset * 3, buttonsize, 18, pipeplugin.lang.GetMessage("MenuItemFilter", pipeplugin, player.UserIDString), "0", "1", "1 1 1 0.08", "1 1 1 0.8"),
                        contentright
                    );
                }


                CuiHelper.AddUi(player, elements);
                userinfo.isMenuOpen = true;
            }
			
            public void CloseMenu(BasePlayer player, UserInfo userinfo) {
                if (!string.IsNullOrEmpty(userinfo.menu))
                    CuiHelper.DestroyUi(player, userinfo.menu);
                userinfo.isMenuOpen = false;

                playerslookingatmenu.Remove(player);
            }

            // this refreshes the menu for each playerslookingatmenu
            public void RefreshMenu() {
                foreach (BasePlayer p in playerslookingatmenu) {
                    p.SendConsoleCommand($"jpipes.refreshmenu {id}");
                }
            }

            public string GetContIcon(BaseEntity e) {

				if (e is BoxStorage) {
					string panel = e.GetComponent<StorageContainer>().panelName;
					if (panel == "largewoodbox")
						return GetItemIconURL("Large_Wood_Box", 140);
					return GetItemIconURL("Wood_Storage_Box", 140);

				} else if (e is BaseOven) {
					string panel = e.GetComponent<BaseOven>().panelName;

					if (panel == "largefurnace")
						return GetItemIconURL("Large_Furnace", 140);
					else if (panel == "smallrefinery")
						return GetItemIconURL("Small_Oil_Refinery", 140);
					else if (panel == "lantern")
						return GetItemIconURL("Lantern", 140);
					else if (panel == "bbq")
						return GetItemIconURL("BBQ", 140);
					else if (panel == "campfire")
						return GetItemIconURL("Camp_Fire", 140);
					else
						return GetItemIconURL("Furnace", 140);
				} else if (e is AutoTurret) {
					return GetItemIconURL("Auto_Turret", 140);
				} else if (e is Recycler) {
					return GetItemIconURL("Recycler", 140);
				} else if (e is FlameTurret) {
					return GetItemIconURL("Flame_Turret", 140);
				} else if (e is GunTrap) {
					return GetItemIconURL("Shotgun_Trap", 140);
				} else if (e is SearchLight) {
					return GetItemIconURL("Search_Light", 140);
				} else if (e is WaterCatcher) {
					if (e.GetComponent<WaterCatcher>()._collider.ToString().Contains("small"))
						return GetItemIconURL("Small_Water_Catcher", 140);
					return GetItemIconURL("Large_Water_Catcher", 140);
				} else if (e is LiquidContainer) {
					if (e.GetComponent<LiquidContainer>()._collider.ToString().Contains("purifier"))
						return GetItemIconURL("Water_Purifier", 140);
					return GetItemIconURL("Water_Barrel", 140);
				} else if (e is VendingMachine) {
					return GetItemIconURL("Vending_Machine", 140);
				} else if (e is DropBox) {
					return GetItemIconURL("Drop_Box", 140);
				} else if (e is StashContainer) {
					return GetItemIconURL("Small_Stash", 140);
				} else if (e is MiningQuarry) {
					if (e.ToString().Contains("pump"))
						return GetItemIconURL("Pump_Jack", 140);
					return GetItemIconURL("Mining_Quarry", 140);
				} else if (e is BuildingPrivlidge) {
					return GetItemIconURL("Tool_Cupboard", 140);
				}

                return "http://i.imgur.com/BwJN0rt.png";
            }
        }

        // syncbox
        private class jSyncBox {
            private JPipes pipeplugin;

            public ulong id;
            public string initerr = string.Empty;

            public ulong ownerid;
            public string ownername;

            public bool isEnabled = true;
        }

        #endregion

        #region Pipe Parameters 

        // length of a segment
        private static float pipesegdist = 3;

        // every other pipe segment is offset by this to remove z fighting
        private static Vector3 pipefightoffset = new Vector3(0.0001f, 0, 0.0001f);

        // offset of pipe inside different containers
        private static class contoffset {
            public static Vector3 turret = new Vector3(0, -0.58f, 0);
            public static Vector3 refinery = new Vector3(-1, 0, -0.1f);
            public static Vector3 furnace = new Vector3(0, -0.3f, 0);
            public static Vector3 largefurnace = new Vector3(0, -1.5f, 0);
            public static Vector3 searchlight = new Vector3(0, -0.5f, 0);
            public static Vector3 pumpfuel = Vector3.zero;
            public static Vector3 pumpoutput = new Vector3(-1, 2, 0);
            public static Vector3 recycler = Vector3.zero;
            public static Vector3 largewatercatcher = new Vector3(0, -0.6f, 0);
            public static Vector3 smallwatercatcher = new Vector3(0, -0.6f, 0);
            public static Vector3 waterbarrel = new Vector3(0, 0.2f, 0);
            public static Vector3 waterpurifier = new Vector3(0, 0.25f, 0);
            public static Vector3 bbq = Vector3.up * 0.03f;
            //public static Vector3 quarryfuel = new Vector3(1,-0.2f,0);
            //public static Vector3 quarryoutput = new Vector3(1,0,0);
        }

        readonly static Dictionary<string, string> ItemUrls = new Dictionary<string, string>() {
            { "Small_Stocking", "http://vignette2.wikia.nocookie.net/play-rust/images/9/97/Small_Stocking_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "SUPER_Stocking", "http://vignette1.wikia.nocookie.net/play-rust/images/6/6a/SUPER_Stocking_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Small_Present", "http://vignette2.wikia.nocookie.net/play-rust/images/d/da/Small_Present_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Medium_Present", "http://vignette3.wikia.nocookie.net/play-rust/images/6/6b/Medium_Present_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Large_Present", "http://vignette1.wikia.nocookie.net/play-rust/images/9/99/Large_Present_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Pump_Jack", "http://vignette2.wikia.nocookie.net/play-rust/images/c/c9/Pump_Jack_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Shop_Front", "http://vignette4.wikia.nocookie.net/play-rust/images/c/c1/Shop_Front_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Water_Purifier", "http://vignette3.wikia.nocookie.net/play-rust/images/6/6e/Water_Purifier_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Water_Barrel", "http://vignette4.wikia.nocookie.net/play-rust/images/e/e2/Water_Barrel_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Survival_Fish_Trap", "http://vignette2.wikia.nocookie.net/play-rust/images/9/9d/Survival_Fish_Trap_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Research_Table", "http://vignette2.wikia.nocookie.net/play-rust/images/2/21/Research_Table_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Small_Planter_Box", "http://vignette3.wikia.nocookie.net/play-rust/images/a/a7/Small_Planter_Box_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Large_Planter_Box", "http://vignette1.wikia.nocookie.net/play-rust/images/3/35/Large_Planter_Box_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Jack_O_Lantern_Happy", "http://vignette1.wikia.nocookie.net/play-rust/images/9/92/Jack_O_Lantern_Happy_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Jack_O_Lantern_Angry", "http://vignette4.wikia.nocookie.net/play-rust/images/9/96/Jack_O_Lantern_Angry_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Large_Furnace", "http://vignette3.wikia.nocookie.net/play-rust/images/e/ee/Large_Furnace_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Ceiling_Light", "http://vignette3.wikia.nocookie.net/play-rust/images/4/43/Ceiling_Light_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Hammer", "http://vignette4.wikia.nocookie.net/play-rust/images/5/57/Hammer_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Auto_Turret", "http://vignette2.wikia.nocookie.net/play-rust/images/f/f9/Auto_Turret_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Camp_Fire", "http://vignette4.wikia.nocookie.net/play-rust/images/3/35/Camp_Fire_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "BBQ", "http://i.imgur.com/DfCm0EJ.png" },
            { "Furnace", "http://vignette4.wikia.nocookie.net/play-rust/images/e/e3/Furnace_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Lantern", "http://vignette4.wikia.nocookie.net/play-rust/images/4/46/Lantern_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Large_Water_Catcher", "http://vignette2.wikia.nocookie.net/play-rust/images/3/35/Large_Water_Catcher_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Large_Wood_Box", "http://vignette1.wikia.nocookie.net/play-rust/images/b/b2/Large_Wood_Box_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Mining_Quarry", "http://vignette1.wikia.nocookie.net/play-rust/images/b/b8/Mining_Quarry_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Repair_Bench", "http://vignette1.wikia.nocookie.net/play-rust/images/3/3b/Repair_Bench_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Small_Oil_Refinery", "http://vignette2.wikia.nocookie.net/play-rust/images/a/ac/Small_Oil_Refinery_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Small_Stash", "http://vignette2.wikia.nocookie.net/play-rust/images/5/53/Small_Stash_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Small_Water_Catcher", "http://vignette2.wikia.nocookie.net/play-rust/images/0/04/Small_Water_Catcher_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Search_Light", "http://vignette2.wikia.nocookie.net/play-rust/images/c/c6/Search_Light_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Wood_Storage_Box", "http://vignette2.wikia.nocookie.net/play-rust/images/f/ff/Wood_Storage_Box_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Vending_Machine", "http://vignette2.wikia.nocookie.net/play-rust/images/5/5c/Vending_Machine_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Drop_Box", "http://vignette2.wikia.nocookie.net/play-rust/images/4/46/Drop_Box_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Fridge", "http://vignette2.wikia.nocookie.net/play-rust/images/8/88/Fridge_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Shotgun_Trap", "http://vignette2.wikia.nocookie.net/play-rust/images/6/6c/Shotgun_Trap_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Flame_Turret", "http://vignette2.wikia.nocookie.net/play-rust/images/f/f9/Flame_Turret_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Recycler", "http://vignette2.wikia.nocookie.net/play-rust/images/e/ef/Recycler_icon.png/revision/latest/scale-to-width-down/{0}" },
            { "Tool_Cupboard", "http://vignette2.wikia.nocookie.net/play-rust/images/5/57/Tool_Cupboard_icon.png/revision/latest/scale-to-width-down/{0}" }
		};

        #endregion

        #region Pipe Functions

        private void startplacingpipe(BasePlayer player, bool isUsingBind = false) {

            UserInfo userinfo = GetUserInfo(player);

            int playerpipelimit;
            if (!checkplayerpipelimit(player, userinfo, out playerpipelimit)) {
                ShowOverlayText(player, string.Format(lang.GetMessage("ErrorPipeLimitReached", this, player.UserIDString), playerpipelimit.ToString()), "", orangestring);
                HideOverlayText(player, 2f);
                userinfo.placestart = null;
                userinfo.placeend = null;
                return;
            }

            userinfo.state = userinfo.state == UserState.placing ? UserState.none : UserState.placing;
            userinfo.isUsingBind = isUsingBind;
            userinfo.clipboard = null;

            if (userinfo.state == UserState.placing) {
                if (!isUsingBind)
                    PrintToChat(player, string.Format(lang.GetMessage("HelpBindTip", this, player.UserIDString), pipecommandprefix, pipehotkey));

                ShowOverlayText(player, lang.GetMessage("SelectFirst", this, player.UserIDString), string.Format(lang.GetMessage(userinfo.isUsingBind ? "SelectSubtextBind" : "SelectSubtextCmd", this, player.UserIDString), userinfo.isUsingBind ? pipehotkey.ToUpper() : pipecommandprefix));
            } else {
                //ShowOverlayText(player,"",lang.GetMessage("SelectCancel",this,player.UserIDString));
                HideOverlayText(player);
                userinfo.placestart = null;
                userinfo.placeend = null;
            }
        }

        private void startlinking(BasePlayer player, bool isUsingBind = false) {

            UserInfo userinfo = GetUserInfo(player);

            userinfo.state = userinfo.state == UserState.linking ? UserState.none : UserState.linking;
            userinfo.isUsingBind = isUsingBind;
            userinfo.clipboard = null;

            if (userinfo.state == UserState.linking) {
                if (!isUsingBind)
                    PrintToChat(player, string.Format(lang.GetMessage("HelpBindTip", this, player.UserIDString), pipecommandprefix, pipehotkey));

                ShowOverlayText(player, lang.GetMessage("SelectFirst", this, player.UserIDString), string.Format(lang.GetMessage(userinfo.isUsingBind ? "SelectSubtextBind" : "SelectSubtextCmd", this, player.UserIDString), userinfo.isUsingBind ? pipehotkey.ToUpper() : pipecommandprefix));
            } else {
                HideOverlayText(player);
                userinfo.placestart = null;
                userinfo.placeend = null;
            }
        }

        // called after second container has been selected
        private void NewPipe(BasePlayer player, UserInfo userinfo) {

            jPipeData newdata = new jPipeData();
            newdata.setContainers(userinfo.placestart, userinfo.placeend);

            bool destiswater = userinfo.placeend is LiquidContainer;

            if (userinfo.placestart is LiquidContainer == destiswater) {
                if (!checkpipeoverlap(regpipes, newdata)) {

                    float dist = Vector3.Distance(userinfo.placestart.CenterPoint(), userinfo.placeend.CenterPoint());

                    if (dist > maxpipedist) { ShowOverlayText(player, lang.GetMessage("ErrorTooFar", this, player.UserIDString), "", orangestring); } else if (dist <= minpipedist) { ShowOverlayText(player, lang.GetMessage("ErrorTooClose", this, player.UserIDString), "", orangestring); } else {

                        jPipe newpipe = new jPipe();
                        newdata.o = player.userID;
                        newdata.on = player.displayName;

                        // initalize pipe
                        if (newpipe.init(this, pipeidgen(), newdata, RemovePipe, MoveItem)) {
                            // pipe was created so register it
                            RegisterPipe(newpipe);
                            ShowOverlayText(player, lang.GetMessage("PipeCreated", this, player.UserIDString), "", bluestring);
                        } else {
                            // pipe error
                            ShowOverlayText(player, lang.GetMessage(newpipe.initerr, this, player.UserIDString));
                            newpipe = null;
                        }
                    }
                } else {
                    ShowOverlayText(player, lang.GetMessage("ErrorAlreadyConnected", this, player.UserIDString), "", orangestring);
                }
            } else {
                ShowOverlayText(player, lang.GetMessage(destiswater ? "ErrorNotItemCont" : "ErrorNotLiquidCont", this, player.UserIDString), "", orangestring);
            }

            HideOverlayText(player, 3f);
            userinfo.state = UserState.none;
            userinfo.placestart = null;
            userinfo.placeend = null;
        }

        private void NewSyncBox(BasePlayer player, UserInfo userinfo) {

            //jSyncBoxData newdata = new jSyncBoxData();

            HideOverlayText(player, 3f);
            userinfo.state = UserState.none;
            userinfo.placestart = null;
            userinfo.placeend = null;
        }

        private System.Random randomidgen = new System.Random();
        private ulong pipeidgen() {
            ulong id = (ulong) randomidgen.Next(1000000, 9999999);
            if (regpipes.ContainsKey(id))
                return pipeidgen();
            else
                return id;
        }

        private ulong syncboxidgen() {
            ulong id = (ulong) randomidgen.Next(1000000, 9999999);
            if (regsyncboxes.ContainsKey(id))
                return syncboxidgen();
            else
                return id;
        }

        // TODO this could be improved by only compairing ids
        private static bool checkpipeoverlap(Dictionary<ulong, jPipe> rps, jPipeData data) {
            uint s = getcontfromid(data.s, data.cs).net.ID;
            uint e = getcontfromid(data.d, data.cd).net.ID;

            foreach (var p in rps)
                if ((p.Value.sourcecont.net.ID == s && p.Value.destcont.net.ID == e) || (p.Value.sourcecont.net.ID == e && p.Value.destcont.net.ID == s))
                    return true;
            return false;
        }

        private static bool checkcontwhitelist(BaseEntity e) =>
            !(e is BaseFuelLightSource || e is Locker || e is ShopFront || e is RepairBench);

        private bool checkcontprivlage(BaseEntity e, BasePlayer p) => e.GetComponent<StorageContainer>().CanOpenLootPanel(p) && checkbuildingprivlage(p);

        private bool checkbuildingprivlage(BasePlayer p) {
            if (permission.UserHasPermission(p.UserIDString, "jpipes.admin"))
                return true;

			//BuildingPrivlidge priv = p.GetBuildingPrivilege();
			//return (priv != null) ? priv.IsAuthed(p) : true;
			return p.CanBuild();
		}

        private bool checkplayerpipelimit(BasePlayer p, UserInfo user) {
            int limit = getplayerpipelimit(p);
            return (limit >= (user.pipes.Keys.Count + 1)) || limit == -1;
        }
        private bool checkplayerpipelimit(BasePlayer p, UserInfo user, out int pipelimit) {
            pipelimit = getplayerpipelimit(p);
            return (pipelimit >= (user.pipes.Keys.Count + 1)) || pipelimit == -1;
        }

        // TODO combine limit functions into one

        private int getplayerpipelimit(BasePlayer p) {
            string id = p.UserIDString;
            if (permission.UserHasPermission(id, "jpipes.admin"))
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
            if (permission.UserHasPermission(id, "jpipes.admin"))
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
                curlimit = Mathf.Max(curlimit, l);
            }

            return curlimit > 3 ? -1 : curlimit;
        }

        // find storage container from id and child id
        private static StorageContainer getcontfromid(uint id, uint cid = 0) => getchildcont((BaseEntity) BaseNetworkable.serverEntities.Find(id), cid);

        // find storage container from parent and child id
        private static StorageContainer getchildcont(BaseEntity parent, uint id = 0) {
            if (id != 0) {
                BaseResourceExtractor ext = parent?.GetComponent<BaseResourceExtractor>();
                if (ext != null) {
                    foreach (var c in ext.children) {
                        if (c is ResourceExtractorFuelStorage && c.GetComponent<ResourceExtractorFuelStorage>().panelName == ((id == 2) ? "fuelstorage" : "generic"))
                            return c.GetComponent<StorageContainer>();
                    }
                }
                //return parent.GetComponent<StorageContainer>();
            }
            return parent?.GetComponent<StorageContainer>();
        }

        private void RegisterPipe(jPipe pipe) {
            GetUserInfo(pipe.ownerid).pipes.Add(pipe.id, pipe);
            regpipes.Add(pipe.id, pipe);
        }
        private void UnRegisterPipe(jPipe pipe) {
            GetUserInfo(pipe.ownerid).pipes.Remove(pipe.id);
            regpipes.Remove(pipe.id);
        }
        private void UnRegisterPipe(ulong id) {
            jPipe pipe;
            if (regpipes.TryGetValue(id, out pipe)) {
                UnRegisterPipe(pipe);
            }
        }

        public void RemovePipe(ulong id, bool remove = true) {

            jPipe pipe;
            if (regpipes.TryGetValue(id, out pipe))
                UnloadPipe(pipe);

            if (remove)
                Un