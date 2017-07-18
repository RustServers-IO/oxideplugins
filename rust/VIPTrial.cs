using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
namespace Oxide.Plugins
{
    [Info("VIP Trial", "Maik8", "1.2", ResourceId = 2563)]
    [Description("Plugin that lets Users try VIP functions.")]
    public class VIPTrial : CovalencePlugin
    {
        #region Variables
        StoredData storedData;
        string groupName;
        int days;
        List<object> permlist;
        #endregion

        #region Commands
        [Command("viptrial")]
        void VIPtrialCommand(IPlayer player, string command, string[] args)
        {
            if (checkTrialAllowed(player))
            {
                if (!checkAlreadyUsed(player))
                {
                    if (!checkPlayerForGroup(player))
                    {
                        addUserForTrial(player);
                    }
                    else
                    {
                        Reply(player, "VIPStillRunning");
                    }
                }
                else if (checkPlayerForGroup(player))
                {
                    Reply(player, "VIPStillRunning");
                }
                else
                {
                    Reply(player, "VIPAlreadyUsed");
                }
            }
            else
            {
                Reply(player, "NoPermission");
            }
        }
        #endregion

        #region Methods
        #region ServerHooks
        void Init()
        {
            permission.RegisterPermission("viptrial.allowed", this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Name);
            LoadDefaultConfig();
            checkGroup();
        }

        void OnUserConnected(IPlayer player)
        {
            if (permission.UserHasGroup(player.Id, groupName))
            {
                if (checkExpired(player))
                {
                    permission.RemoveUserGroup(player.Id, groupName);
                    Reply(player, "VIPExpired");
                }
                else
                {
                    Reply(player, "VIPEndsIn", getDaysLeft(player));
                }
            }
        }
        #endregion
        void checkGroup()
        {
            if (!permission.GroupExists(groupName))
            {
                permission.CreateGroup(groupName, string.Empty, 0);
            }               
                checkPerm(permlist, groupName);
        }
        void checkPerm(List<object> perm, string group)
        {
            foreach (object item in perm)
            {
                if (!permission.GroupHasPermission(group, item.ToString()))
                {
                    permission.GrantGroupPermission(group, item.ToString(), null);
                }
            }
            bool check;
            foreach (string item in permission.GetGroupPermissions(group))
            {
                check = false;
                foreach (object perms in perm)
                {
                    if (perms.ToString() == item)
                    {
                        check = true;
                    }
                }
                if (!check)
                {
                    permission.RevokeGroupPermission(group, item);
                }
            }
        }
        private int getDaysLeft(IPlayer player)
        {
            DateTime usedate = DateTime.Now.Date.AddDays(-1);

            foreach (VIPDataSaveFile elm in storedData.VIPDataHash)
            {  
                if (elm.userId.Equals(player.Id))
                {
                    usedate = Convert.ToDateTime(elm.now);
                    usedate = usedate.Date;
                    break;
                }
            }
            return Convert.ToInt32((usedate.Date - DateTime.Now.Date).TotalDays);
        }

        private void addUserForTrial(IPlayer player)
        {
            permission.AddUserGroup(player.Id, groupName);
            storedData.VIPDataHash.Add( new VIPDataSaveFile( player, DateTime.Now.AddDays( days) ) );
            Interface.Oxide.DataFileSystem.WriteObject(this.Name, storedData);
            Reply(player, "VIPStarted", DateTime.Now.Date.AddDays( days).ToShortDateString() );
        }

        private bool checkPlayerForGroup(IPlayer player) => permission.UserHasGroup(player.Id, groupName);

        private bool checkTrialAllowed(IPlayer player) => permission.UserHasPermission(player.Id, "viptrial.allowed");

        bool checkAlreadyUsed(IPlayer player)
        {
            foreach (VIPDataSaveFile elm in storedData.VIPDataHash)
            {
                if (elm.userId == player.Id)
                {
                    return true;
                }
            }
            return false;
        }
        bool checkExpired(IPlayer player)
        {
            DateTime usedate = DateTime.Now.Date.AddDays(-1);
            foreach (VIPDataSaveFile elm in storedData.VIPDataHash)
            {
                if (elm.userId.Equals(player.Id))
                {
                     usedate = Convert.ToDateTime(elm.now);
                }
            }
            if (DateTime.Compare(usedate, DateTime.Now.Date) < 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void Reply(IPlayer player, string langKey, params object[] args) => player.Reply(lang.GetMessage(langKey, this, player.Id), args);

        #endregion

        #region Config
        protected override void LoadDefaultConfig()
        {
            Config["VIP group name: "] = groupName = GetConfig("VIP group name: ", "vip_trial");
            Config["Amount of trial Days: "] = days = GetConfig("Amount of trial Days: ", 3);
            Config["Permissions of the group:"] = permlist = GetConfig("Permissions of the group:", new List<object>
            {
                "Oxide.plugins", "oxide.reload"
            });

            SaveConfig();
        }
        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));


        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "VIPStillRunning", "Your VIP trial is still running!" },
                { "VIPAlreadyUsed", "You have already used your VIP trial." },
                { "NoPermission", "You are not allowed to use this command!" },
                { "VIPExpired", "Your VIP trial is expired." },
                { "VIPEndsIn", "Your VIP trial ends in {0} days." },
                { "VIPStarted", "Your VIP trial started, lasting till: {0}" }
            }, this);
        }

        #endregion

        #region Classes
        class StoredData
        {
            public HashSet<VIPDataSaveFile> VIPDataHash = new HashSet<VIPDataSaveFile>();

            public StoredData()
            {
            }
        }
        class VIPDataSaveFile
        {
            public string userId;
            public string now;

            public VIPDataSaveFile()
            {
            }

            public VIPDataSaveFile(IPlayer player, DateTime now)
            {
                userId = player.Id.ToString();
                this.now = now.ToShortDateString();
            }
        }
        #endregion
    }
}
