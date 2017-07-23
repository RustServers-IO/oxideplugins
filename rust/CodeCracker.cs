using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("CodeCracker", "Hougan", "1.1.0")]
    [Description("With this plugin you can hack different code-locks")]
    class CodeCracker : RustPlugin
    {
        #region Variable
        private static string BoxName = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        private static string sBoxName = "assets/prefabs/deployable/woodenbox/box_wooden.item.prefab";
        private static string CupName = "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab";
        private static string EffectName = "assets/bundled/prefabs/fx/ore_break.prefab"; // Set other to change unlock effect
        private Dictionary<ulong, Cracker> playerTimers = new Dictionary<ulong, Cracker>();
        class Cracker
        {
            public bool isUnlocking = false;
            public double amountGUI = 0;
            public Timer GUI;
            public Timer AtAll;
            public Timer Effects;
        }
        private int unlockTime;
        private int unlockChance;
        private int unlockItem;
        private bool unlockEffects;
        private int moveAmount;
        private bool damageRestrict;
        private bool cupAllowed;
        private string aMin = "0.3445 0.115"; // Set different to change GUI position
        private string aMax = "0.6395 0.13"; // Set different to change GUI position
        private string crackeryUse = "codecracker.use";
        private string crackerChance = "codecracker.chance";
        #endregion

        #region Core
        [ChatCommand("crack")]
        void RobBox(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, crackeryUse)) { SendReply(player, msg("Permission", player.UserIDString)); return; }
            RaycastHit hitinfo;
            if (Physics.Raycast(player.eyes.position, Quaternion.Euler(player.GetNetworkRotation()) * Vector3.forward,
                out hitinfo, 5f, LayerMask.GetMask(new string[] {"Deployed"})))
            {
                if ((hitinfo.GetEntity().PrefabName == sBoxName || hitinfo.GetEntity().PrefabName == BoxName || (hitinfo.GetEntity().PrefabName == CupName && cupAllowed)) && hitinfo.GetEntity().HasSlot(BaseEntity.Slot.Lock))
                {
                    BaseLock baseLock = hitinfo.GetEntity().children[0].GetComponent<BaseLock>();
                    if (!baseLock.GetComponent<CodeLock>().whitelistPlayers.Contains(player.userID))
                    {
                        if (!playerTimers.ContainsKey(player.userID)) { playerTimers.Add(player.userID, new Cracker()); }
                        if (!playerTimers[player.userID].isUnlocking)
                        {
                            if (unlockItem != 0)
                            {
                                if (player.inventory.containerMain.FindItemsByItemID(unlockItem).Count != 0) { player.inventory.containerMain.Take(null, unlockItem, 1); }
                                else { SendReply(player, msg("Item", player.UserIDString), ItemManager.CreateByItemID(unlockItem).info.displayName.english); return; }
                            }
                            SendReply(player, msg("Start", player.UserIDString));
                            var dPos = player.transform.position;
                            Vector3 cPos;
                            playerTimers[player.userID].isUnlocking = true;
                            playerTimers[player.userID].AtAll = timer.Once(unlockTime, () =>
                            {
                                FinishCrack(player, baseLock);
                            });
                            playerTimers[player.userID].GUI = timer.Every(1, () =>
                            {
                                playerTimers[player.userID].amountGUI += (double) 1 / unlockTime;
                                cPos = player.transform.position;
                                if (Vector3.Distance(dPos, cPos) > moveAmount)
                                {
                                    StopUnlocking(player);
                                    SendReply(player, msg("Moved", player.UserIDString)); 
                                    Effect.server.Run("assets/prefabs/deployable/research table/effects/research-fail.prefab",
                                        player, 0u, Vector3.zero, Vector3.forward, null, false);
                                    return;
                                }
                                CuiHelper.DestroyUi(player, "UnlockGUI");
                                UnlockGUI(player, playerTimers[player.userID].amountGUI);
                            });
                            playerTimers[player.userID].Effects = timer.Every(11, () =>
                            {
                                Effect.server.Run("assets/prefabs/deployable/research table/effects/research-start.prefab",
                                    player, 0u, Vector3.zero, Vector3.forward, null, false);
                                if (unlockEffects) Effect.server.Run(EffectName, baseLock.GetComponent<CodeLock>(), 0u, Vector3.zero, Vector3.forward, null, false);
                            });
                            playerTimers[player.userID].Effects.Callback();
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
            unlockTime = Convert.ToInt32(GetVariable("Unlocking", "Time to unlock", 10));
            unlockChance = Convert.ToInt32(GetVariable("Unlocking", "Chance to unlock", 50));
            unlockEffects = Convert.ToBoolean(GetVariable("Unlocking", "Enable effects", true));
            unlockItem = Convert.ToInt32(GetVariable("Unlocking", "ItemID that player need (0 - for disable)", 1200628767));
            cupAllowed = Convert.ToBoolean(GetVariable("Objects", "Allow cupboard crack", true));
            moveAmount = Convert.ToInt32(GetVariable("Checks", "Number of hops for cancellation crack", 2));
            damageRestrict = Convert.ToBoolean(GetVariable("Checks", "Damage cancel cracking", true));
            SaveConfig();
        }
        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(crackeryUse, this);
            permission.RegisterPermission(crackerChance, this);
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
                ["WhiteList"] = "<color=#DC143C>ERROR:</color> You already authed on this lock!",
                ["Item"] = "<color=#DC143C>ERROR:</color> You have not {0}!",
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
        void impactLock(BaseLock baseLock, BasePlayer player, bool Result)
        {
            var locker = baseLock.GetComponent<CodeLock>();
            if (Result)
            {
                locker.whitelistPlayers.Add(player.userID);
                locker.SetFlag(CodeLock.Flags.Locked, false);
                Effect.server.Run(locker.effectUnlocked.resourcePath, baseLock.GetComponent<CodeLock>(), 0u, Vector3.zero, Vector3.forward, null, false);
                Effect.server.Run("assets/prefabs/deployable/research table/effects/research-success.prefab",
                    player, 0u, Vector3.zero, Vector3.forward, null, false);
            }
            else
            {
                Effect.server.Run(locker.effectShock.resourcePath, baseLock.GetComponent<CodeLock>(), 0u, Vector3.zero, Vector3.forward, null, false);
                Effect.server.Run("assets/prefabs/deployable/research table/effects/research-fail.prefab",
                    player, 0u, Vector3.zero, Vector3.forward, null, false);
            }
        }

        void FinishCrack(BasePlayer player, BaseLock baseLock)
        {
            StopUnlocking(player);
            if (Oxide.Core.Random.Range(0, 100) < unlockChance || permission.UserHasPermission(player.UserIDString, crackerChance))
            {
                impactLock(baseLock, player, true);
                SendReply(player, msg("Success", player.UserIDString));
            }
            else
            {
                impactLock(baseLock, player, false);
                SendReply(player, msg("Failed", player.UserIDString));
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
                RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
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
        [ChatCommand("cc.test")] // TODO: Fix name
        void robbertest(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, msg("Permission", player.UserIDString));
                return;
            }
            playerTimers.Add(player.userID, new Cracker());
            RaycastHit hitinfo;
            Physics.Raycast(player.eyes.position, Quaternion.Euler(player.GetNetworkRotation()) * Vector3.forward, out hitinfo, 5f);
            CodeLock loc = hitinfo.GetEntity().children[0].GetEntity().GetComponent<CodeLock>();
            loc.whitelistPlayers.Remove(player.userID);

            SendReply(player, msg("Debug", player.UserIDString));
        }
        #endregion
    }
}