using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("SteamGroups", "Wulf/lukespragg", "0.3.2", ResourceId = 2085)]
    [Description("Automatically adds members of Steam group(s) to a permissions group")]

    class SteamGroups : CovalencePlugin
    {
        #region Initialization

        readonly Dictionary<string, string> steamGroups = new Dictionary<string, string>();
        readonly Dictionary<string, string> groups = new Dictionary<string, string>();
        readonly HashSet<string> members = new HashSet<string>();
        readonly Queue<Member> membersQueue = new Queue<Member>();
        readonly Regex idRegex = new Regex(@"<steamID64>(?<id>.+)</steamID64>");

        class Member
        {
            public readonly string Id;
            public readonly string Group;

            public Member(string id, string group)
            {
                Id = id;
                Group = group;
            }
        }

        private ConfigData config;

        public class ConfigData
        {
            [JsonProperty(PropertyName = "Group Setup")]
            public List<GroupInfo> GroupSetup { get; set; }

            [JsonProperty(PropertyName = "Update Interval (Seconds)")]
            public int UpdateInterval { get; set; } = 300;

            public class GroupInfo
            {
                public string Steam { get; set; } = "OxideMod";
                public string Oxide { get; set; } = "default";
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigData>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData
            {
                GroupSetup = new List<ConfigData.GroupInfo>
                {
                    new ConfigData.GroupInfo { Steam = "CitizenSO", Oxide = "default" },
                    new ConfigData.GroupInfo { Steam = "OxideMod", Oxide = "default" }
                },
                UpdateInterval = 300
            };
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        void OnServerInitialized()
        {
            config = Config.ReadObject<ConfigData>();

            foreach (var group in config.GroupSetup)
            {
                if (!permission.GroupExists(group.Oxide)) permission.CreateGroup(group.Oxide, group.Oxide, 0);
                AddGroup(group.Steam, group.Oxide);
            }
            GetMembers();

            timer.Every(config.UpdateInterval, GetMembers);
        }

        #endregion

        #region Group Handling

        void AddGroup(string steamGroup, string oxideGroup)
        {
            ulong result;

            var url = "http://steamcommunity.com/{0}/{1}/memberslistxml/?xml=1&p=";
            url = string.Format(url, ulong.TryParse(steamGroup, out result) ? "gid" : "groups", steamGroup);

            steamGroups.Add(steamGroup, url);
            groups.Add(steamGroup, oxideGroup);
        }

        void ProcessQueue()
        {
            var member = membersQueue.Dequeue();
            if (permission.UserHasGroup(member.Id, groups[member.Group])) return;

            permission.AddUserGroup(member.Id, groups[member.Group]);
            Puts($"{member.Id} from {member.Group} added to '{groups[member.Group]}' group");
        }

        void OnTick()
        {
            if (membersQueue.Count != 0) ProcessQueue();
            //else RemoveOldMembers();
        }

        bool InSteamGroup(string id) => members.Contains(id);

        #endregion

        #region Group Cleanup

        void RemoveOldMembers()
        {
            foreach (var group in groups)
            {
                foreach (var user in permission.GetUsersInGroup(group.Value))
                {
                    var id = Regex.Replace(user, "[^0-9]", "");
                    //membersQueue.Enqueue(new Member(id, group));
                    if (!members.Contains(id)) permission.RemoveUserGroup(id, group.Value);
                }
            }
        }

        void ProcessRemoveQueue()
        {
            membersQueue.Dequeue();
        }

        #endregion

        #region Member Grabbing

        void GetMembers()
        {
            foreach (var group in steamGroups) GetMembers(group.Value, group.Key);
        }

        void GetMembers(string url, string group, int page = 1)
        {
            webrequest.EnqueueGet(url, (code, response) =>
            {
                if (code == 403 || code == 429)
                {
                    Puts($"Steam is currently not allowing connections from your server. ({code})");
                    Puts("Trying again in 10 minutes...");
                    timer.Once(500f, () => GetMembers(url, group, page));
                    return;
                }

                if (code != 200 || response == null)
                {
                    //Puts($"Checking for Steam group members failed! ({code})");
                    //Puts("Trying again in 1 minute...");
                    //timer.Once(60f, () => GetMembers(url, group, page));
                    return;
                }

                var matches = idRegex.Matches(response);
                foreach (Match match in matches)
                {
                    var id = match.Groups["id"].Value;
                    if (members.Contains(id)) continue;

                    members.Add(id);
                    membersQueue.Enqueue(new Member(id, group));
                }

                if (response.Contains("nextPageLink"))
                {
                    page++;
                    GetMembers(url, group, page);
                }
            }, this);
        }

        [Command("steammembers")]
        void Command(IPlayer player, string command, string[] args) => GetMembers();

        #endregion
    }
}
