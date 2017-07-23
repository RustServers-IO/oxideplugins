using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("CodeCracker", "Hougan", "1.0.0")]
    [Description("With this plugin you can hack different code-locks")]
    class CodeCracker : RustPlugin
    {

        // TODO: Add more GUI variant.
        #region Variable
        private static string BoxName = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        private static string sBoxName = "assets/prefabs/deployable/woodenbox/box_wooden.item.prefab";
        private static string CupName = "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab";
        private static string EffectName = "assets/bundled/prefabs/fx/ore_break.prefab";
        private Dictionary<ulong, Robber> playerTimers = new Dictionary<ulong, Robber>();
        class Robber
        {
            public bool isUnlocking = false;
            public double amountGUI = 0;
            public Timer GUI;
            public Timer AtAll;
            public Timer Effects;
        }
        private int unlockTime;
        private int unlockChance;
        private int moveAmount;
        private bool unlockEffects;
        private bool cupAllowed;
        private bool damageRestrict;
        private string aMin = "";
        private string aMax = "";
        private string robberyUse = "codecracker.use";
        private string robberyChance = "codecracker.chance";
        #endregion

        #region Core
        [ChatCommand("crack")]
        void RobBox(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, robberyUse)) { SendReply(player, msg("Permission", player.UserIDString)); return; }
            RaycastHit hitinfo;
            if (Physics.Raycast(player.eyes.position, Quaternion.Euler(player.GetNetworkRotation()) * Vector3.forward,
                out hitinfo, 5f, LayerMask.GetMask(new string[] {"Deployed"})))
            {
                if ((hitinfo.GetEntity().PrefabName == sBoxName || hitinfo.GetEntity().PrefabName == BoxName ||
                     (hitinfo.GetEntity().PrefabName == CupName && cupAllowed)) &&
                    hitinfo.GetEntity().HasSlot(BaseEntity.Slot.Lock))
                {
                    BaseLock baseLock = hitinfo.GetEntity().children[0].GetComponent<BaseLock>();
                    if (!baseLock.GetComponent<CodeLock>().whitelistPlayers.Contains(player.userID))
                    {
                        if (!playerTimers.ContainsKey(player.userID)) { playerTimers.Add(player.userID, new Robber()); }
                        if (!playerTimers[player.userID].isUnlocking)
                        {
                            SendReply(player, msg("Start", player.UserIDString));
                            var dPos = player.transform.position;
                            Vector3 cPos;
                            playerTimers[player.userID].isUnlocking = true;
                            playerTimers[player.userID].AtAll = timer.Once(unlockTime, () =>
                            {
                                if (Oxide.Core.Random.Range(0, 100) < unlockChance ||
                                    permission.UserHasPermission(player.UserIDString, robberyChance))
                                {
                                    StopUnlocking(player);
                                    impactLock(baseLock, player, true);
                                    SendReply(player, msg("Success", player.UserIDString));
                                }
                                else
                                {
                                    StopUnlocking(player);
                                    impactLock(baseLock, player, false);
                                    SendReply(player, msg("Failed", player.UserIDString));
                                }
                            });
                            playerTimers[player.userID].GUI = timer.Every(1, () =>
                            {
                                playerTimers[player.userID].amountGUI += (double) 1 / unlockTime;
                                cPos = player.transform.position;
                                if (Vector3.Distance(dPos, cPos) > moveAmount)
                                {
                                    StopUnlocking(player);
                                    SendReply(player, msg("Moved", player.UserIDString));
                                    return;
                                }
                                CuiHelper.DestroyUi(player, "UnlockGUI");
                                UnlockGUI(player, playerTimers[player.userID].amountGUI);
                            });
                            playerTimers[player.userID].Effects = timer.Every(5, () =>
                            {
                                if (unlockEffects)
                                {
                                    Effect.server.Run(EffectName, baseLock.GetComponent<CodeLock>(), 0u, Vector3.zero,
                                        Vector3.forward, null, false);
                                }
                            });
                        }
                        else { SendReply(player, msg("Locking", player.UserIDString)); }
                    }
                    else { SendReply(player, msg("WhiteList", player.UserIDString)); }
                }
                else { SendReply(player, msg("Object", player.UserIDString)); }
            }
            else { SendReply(player, msg("Look", player.UserIDString)); }
        }

        #endregion

        #region Initialize
        protected override void LoadDefaultConfig()
        {
            unlockTime = Convert.ToInt32(GetVariable("Unlocking", "Time to unlock", 30));
            unlockChance = Convert.ToInt32(GetVariable("Unlocking", "Chance to unlock", 50));
            unlockEffects = Convert.ToBoolean(GetVariable("Unlocking", "Enable effects", true));
            cupAllowed = Convert.ToBoolean(GetVariable("Objects", "Allow cupboard crack", true));
            moveAmount = Convert.ToInt32(GetVariable("Checks", "Number of hops for cancellation crack", 2));
            damageRestrict = Convert.ToBoolean(GetVariable("Checks", "Damage cancel cracking", true));
            SaveConfig();
        }
        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(robberyUse, this);
            permission.RegisterPermission(robberyChance, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Permission"] = "You have no <color=#DC143C>permission</color> to do that!",
                ["Cupboard"] = "You have no <color=#DC143C>permission</color> to hack cup-lock!",
                ["Start"] = "You <color=#DC143C>started</color> hacking this code-lock!",
                ["Moved"] = "You <color=#DC143C>moved</color>, hacking <color=#DC143C>aborted</color>!",
                ["Damage"] = "You got <color=#DC143C>damage</color>, hacking <color=#DC143C>aborted</color>!",
                ["Success"] = "<color=#00cc00>SUCCESS:</color> Code unlocked!",
                ["Failed"] = "<color=#DC143C>FAILED:</color> Code still locked!",
                ["Object"] = "<color=#DC143C>FAILED:</color> It is not box, or cupboard! (Or it have not code-lock)",
                ["Look"] = "<color=#DC143C>ERROR:</color> You can't crack this object!",
                ["Locking"] = "<color=#DC143C>ERROR:</color> You already cracking something!",
                ["WhiteList"] = "<color=#DC143C>ERROR:</color> You already have code of this lock!",
                ["Debug"] = "<color=#00cc00>SUCCES:</color> Unwhitelisted!"
            }, this);
        }
        #endregion

        #region Functions
        void StopUnlocking(BasePlayer player)
        {
            playerTimers[player.userID].Effects.Destroy();
            playerTimers[player.userID].GUI.Destroy();
            playerTimers[player.userID].AtAll.Destroy();
            
            playerTimers.Remove(player.userID);
            CuiHelper.DestroyUi(player, "UnlockGUI");
        }
        void impactLock(BaseLock baseLock, BasePlayer player, bool Yes)
        {
            var locker = baseLock.GetComponent<CodeLock>();
            if (Yes)
            {
                locker.whitelistPlayers.Add(player.userID);
                locker.SetFlag(CodeLock.Flags.Locked, false);
                Effect.server.Run(locker.effectUnlocked.resourcePath, baseLock.GetComponent<CodeLock>(), 0u, Vector3.zero, Vector3.forward, null, false);
            }
            else
            {
                Effect.server.Run(locker.effectShock.resourcePath, baseLock.GetComponent<CodeLock>(), 0u, Vector3.zero, Vector3.forward, null, false);
            }
        }
        string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        object GetVariable(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
            }
            return value;
        }
        #endregion

        #region Hooks
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity.net.connection == null) return;
            if (!damageRestrict) return;
            var player = BasePlayer.FindByID(entity.GetComponent<BaseEntity>().net.connection.userid);
            if (playerTimers.ContainsKey(player.userID))
            {
                StopUnlocking(player);
                SendReply(player, msg("Damage", player.UserIDString));
            }
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (playerTimers.ContainsKey(player.userID))
            {
                StopUnlocking(player);
            }
        }
        #endregion

        #region GUI
        public void UnlockGUI(BasePlayer player, double pos)
        {
            var MoveElements = new CuiElementContainer();
            var Choose = MoveElements.Add(new CuiPanel
            {
                Image = { Color = $"0.1 0.1 0.1 0" },
                RectTransform = { AnchorMin = "0.3445 0.115", AnchorMax = "0.6395 0.13" },
                CursorEnabled = false,
            }, "HUD", "UnlockGUI");
            // Cancel Button
            MoveElements.Add(new CuiButton
            {
                Button = { Color = $"0.34 0.34 0.34 0.4", Close = Choose },
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"{pos} 0.95" },
                Text = { Text = "" }
            }, Choose);

            MoveElements.Add(new CuiButton
            {
                Button = { Color = $"0.34 0.34 0.34 0.4", Close = Choose },
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1" },
                Text = { Text = $"<color=#DC143C>UNLOCKING</color> CODE LOCK", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 10 }
            }, Choose);

            CuiHelper.AddUi(player, MoveElements);
        }
        #endregion

        #region Debug
        [ChatCommand("robbery.test")]
        void robbertest(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, msg("Permission", player.UserIDString));
            }
            playerTimers.Add(player.userID, new Robber());
            RaycastHit hitinfo;
            Physics.Raycast(player.eyes.position, Quaternion.Euler(player.GetNetworkRotation()) * Vector3.forward, out hitinfo, 5f);
            CodeLock loc = hitinfo.GetEntity().children[0].GetEntity().GetComponent<CodeLock>();
            loc.whitelistPlayers.Remove(player.userID);

            SendReply(player, msg("Debug", player.UserIDString));
        }
        #endregion
    }
}