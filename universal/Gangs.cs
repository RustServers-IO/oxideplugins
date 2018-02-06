using System.Collections.Generic;
using Oxide.Core.Plugins;
using System.Text;
using System.Linq;
using Oxide.Core;


namespace Oxide.Plugins
{
		[Info("Gangs", "Gute & Tugamano", "1.0.0")]
		class Gangs : RustLegacyPlugin
		{
		static Plugins.Timer tempoaceita;

		static string permissionCanGang;

		static bool commandGang;
		static bool msgconnect;
		static bool msgdisconnect;

		static int maxnome;
		static int maxtag;
		static int maxtops;
		static int maxmsgs;
		static int maxmembros;

		static int maxLengthStringBuilder;
		static float tempoinvite;

		[PluginReference]
		Plugin AdminControl;

		void Init()
		{
			SetupConfig();
			SetupLang();
			SetupChatCommands();
			SetupPermissions();
			return;
		}

		void SetupPermissions()
		{
			permission.RegisterPermission(permissionCanGang, this);
			return;
		}

		void SetupChatCommands()
		{
			if (commandGang)
			cmd.AddChatCommand("gang", this, "cmdGang");
		}

		void SetupConfig()
		{
			permissionCanGang = Config.Get<string>("SettingsPermission", "permissionCanGang");

			commandGang = Config.Get<bool>("SettingsCommand", "commandGang");
			msgconnect = Config.Get<bool>("SettingsCommand", "msgconnect");
			msgdisconnect = Config.Get<bool>("SettingsCommand", "msgdisconnect");

			maxnome = Config.Get<int>("SettingsMax", "maxnome");
			maxtag = Config.Get<int>("SettingsMax", "maxtag");
			maxtops = Config.Get<int>("SettingsMax", "maxtops");
			maxmsgs = Config.Get<int>("SettingsMax", "maxmsgs");
			maxmembros = Config.Get<int>("SettingsMax", "maxmembros");

			tempoinvite = Config.Get<float>("SettingsExtras", "tempoinvite");
			maxLengthStringBuilder = Config.Get<int>("SettingsExtras", "maxLengthStringBuilder");
		}

		protected override void LoadDefaultConfig()
		{
			Config["SettingsPermission"] = new Dictionary<string, object>
			{
				{"permissionCanGang", "gangs.allow"}
			};
				Config["SettingsCommand"] = new Dictionary<string, object>
			{
				{"commandGang", true},
				{"msgconnect", true},
				{"msgdisconnect", true}
			};
				Config["SettingsMax"] = new Dictionary<string, object>
			{
				{"maxnome", "9"},
				{"maxtag", "4"},
				{"maxtops", "5"},
				{"maxmsgs", "5"},
				{"maxmembros", "10"}
			};
				Config["SettingsExtras"] = new Dictionary<string, object>
			{
				{"tempoinvite", 40f},
				{"maxLengthStringBuilder", "25"}
			};
		}
		string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
		void SetupLang()
		{
            // English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"ChatTag", "Gangs"},
				{"NoPermission", "You are not allowed to use this command."},
				{"NoExistent", "No player found with the existing name '{0}'."},
				{"NotHaveClanName", "There is no gang named '{0}' in the data."},
				{"PlayerNotMemberOfClan", "Player is not a member color clear]of you gang!"},
				{"NotMemberOfAClan", "You are not part of a gang!"},
				{"NotOwnerOfClan", "You not the [color red]owner [color clear]of the gang!"},
				{"NotOwnerOrChiefOfClan", "You not the [color red]owner or chief [color clear]the gang!"},
				{"ClanNameAlreadyExists", "There is already a gang with the name [color red]{0}[color clear]! You must choose another name."},
				{"ClanTagAlreadyExists", "There is already a gang with the tag [color red]{0}[color clear]! You must choose another tag."},
				{"MaxLengthsOfName", "You must choose a gang name with a [color red]maximum of {0} [color clear]letters."},
				{"MaxLengthsOfTag", "You must choose a gang tag with a [color red]maximum of {0} [color clear]letters."},
				{"OnPlayerConnected", "{0} owner of his gang is now online!"},
				{"OnPlayerConnected1", "{0} chief of his gang is now online!"},
				{"OnPlayerConnected2", "{0} member of his gang is now online!"},
				{"OnPlayerDisconnected", "{0} owner of his gang is now offline!"},
				{"OnPlayerDisconnected1", "{0} chief of his gang is now offline!"},
				{"OnPlayerDisconnected2", "{0} member of his gang is now offline!"},
				{"HelpsAdmins", "=================== [color lime]Helps gang Admins [color clear]==================="},
				{"HelpsAdmins1", "Use /gang delete (GangName) => Delet a gang."},
				{"HelpsAdmins2", "Use /gang name (GangName) (NewName) => Change name a gang"},
				{"HelpsAdmins3", "Use /gang tag (GangName) (NewTag) => Change tag a gang."},
				{"HelpsAdmins4", "Use /gang info (GangName) => See infos a gang."},
				{"HelpsAdmins5", "Use /gang stats (GangName) => See stats a gang."},
				{"HelpsAdmins6", "======================================================"},
				{"HelpsPlayers", "========================== [color lime]Helps gang Players [color clear]=========================="},
				{"HelpsPlayers1", "Use /gang create (GangName) (GangTag) => Creat a v."},
				{"HelpsPlayers2", "Use /gang delete => Delet you gang."},
				{"HelpsPlayers3", "Use /gang name (NewName) => Change name you gang."},
				{"HelpsPlayers4", "Use /gang tag (NewTag) => Change tag you gang."},
				{"HelpsPlayers5", "Use /gang invite (PlayerName) => Invite a player to his gang."},
				{"HelpsPlayers6", "Use /gang accept => Accept invitation to gang."},
				{"HelpsPlayers7", "Use /gang promote owner (PlayerName) => Promote a member to owner the gang."},
				{"HelpsPlayers8", "Use /gang promote chief (PlayerName) => Promote a member to chief the gang."},
				{"HelpsPlayers9", "Use /gang demote (PlayerName) => Demote a chief of you gang."},
				{"HelpsPlayers10", "Use /gang kick (PlayerName) => Kick a member of you gang."},
				{"HelpsPlayers11", "Use /gang leave => Leave your gang."},
				{"HelpsPlayers12", "Use /gang online => View online members of your gang."},
				{"HelpsPlayers13", "Use /gang createmsg (DescriptionMessage) (Message) => Create a message for your gang."},
				{"HelpsPlayers14", "Use /gang deletemsg (DescriptionMessage) => Delet a message for your gang."},
				{"HelpsPlayers15", "Use /gang msgs => View gang messagues."},
				{"HelpsPlayers16", "Use /gang info => View informations of you gang."},
				{"HelpsPlayers17", "Use /gang stats => View stats of you gang."},
				{"HelpsPlayers18", "Use /gang tops => View the gang tops."},
				{"HelpsPlayers19", "Use /gang pvp => Enable disable gang pvp."},
				{"HelpsPlayers20", "Use /g (Chat) => Send a message to all members of you gang."},
				{"HelpsPlayers21", "Use /gang list => View gang online."},
				{"HelpsPlayers22", "===================================================================="},
				{"CreatClan", "You [color res]are already [color clear]a member of a gang!"},
				{"CreatClan1", "Success you have created gang name {0} tag {1}."},
				{"DeletClan", "gang {0} was deleted successfully."},
				{"ChangeNameClan", "Success {0} gang name was changed to {1}."},
				{"ChangeTagClan", "Success {0} gang tag was changed to {1}."},
				{"InviteClan", "Your gang already has a [color red]maximum {0} [color clear]member allowed."},
				{"InviteClan1", "This player is [color red]already color clear]invited to join a gang."},
				{"InviteClan2", "This player is [color red]already [color clear]a part of a gang."},
				{"InviteClan3", "You invited {0} to join the gang."},
				{"InviteClan4", "{0} invited you to join the gang {1}! You have {2} second/s to accept /gang accept."},
				{"InviteClan5", "{0} not accept your invitation to join the gang."},
				{"InviteClan6", "Time finished to accept the invitation to enter the gang {0}."},
				{"AcceptInvitation", "You [color red]not have [color clear]gang invitation to accept."},
				{"AcceptInvitation1", "The player who invited you is [color red]not online! [color clear]You can not accept the invitation."},
				{"AcceptInvitation2", "This gang [color red]no longer [color clear]exists!"},
				{"AcceptInvitation3", "{0} accepted his invitation to enter the gang."},
				{"AcceptInvitation4", "You have agreed to enter the gang {0}!"},
				{"PromotePlayer", "You promoted {0} to the gang owner! You are now chief of the gang."},
				{"PromotePlayer1", "{0} promoted you to the gang owner!"},
				{"PromotePlayer2", "This member is [color red]already the chief [color clear]of the gang!"},
				{"PromotePlayer3", "You promoted {0} chief the gang!"},
				{"PromotePlayer4", "{0} promoted you chief the gang!"},
				{"DemotePlayer", "You can only [color red]demote chiefs [color clear]of your gang!"},
				{"DemotePlayer1", "his player is [color red]not the chief [color clear]of your gang."},
				{"DemotePlayer2", "Success demote {0}! Now is simple meber of gang."},
				{"DemotePlayer3", "{0} demote you chief of gang!"},
				{"KickPlayerOfClan", "You can not kick a [color red]owner [color clear]of you v!"},
				{"KickPlayerOfClan1", "Success you exposed {0} from the gang."},
				{"KickPlayerOfClan2", "{0} exposed you from the gang."},
				{"LeaveClan", "You left the gang [color red]{0}[color clear]."},
				{"ClanOnline", "=============== [color lime]gang Online [color clear]==============="},
				{"ClanOnline1", "========================================"},
				{"CreatMessage", "Your gang has already [color red]allowed maximum [color clear]messages!"},
				{"CreatMessage1", "Created message for gang! [color red]{0} [color clear]Message [color lime]{1}"},
				{"DeletMessage", "You gang not have message with the description [color red]\"{0}\"[color clear]."},
				{"DeletMessage1", "Success messaguem [color red]{0} [color clear]removed."},
				{"SeeMessages", "Your gang does [color red]not have [color clear]any message!"},
				{"SeeMessages1", "=============================== [color lime]gang Messages [color clear]==============================="},
				{"SeeMessages2", "Description [color red]{0}  [color clear]Messages [color cyan]{1}"},
				{"SeeMessages3", "==========================================================================="},
				{"InfoClan", "================ [color lime]gang Infos [color clear]================"},
				{"InfoClan1", "Infos of gang {0}."},
				{"InfoClan2", "Owner {0}."},
				{"InfoClan3", "Chief/s {0}"},
				{"InfoClan4", "Member/s {0}"},
				{"InfoClan5", "======================================="},
				{"ClanStats", "============ [color lime]gang Stats [color clear]============"},
				{"ClanStats1", "Stats of gang {0}."},
				{"ClanStats2", "Score {0} Place {1}."},
				{"ClanStats3", "Kills {0} Deaths {1} Suicides {2}."},
				{"ClanStats4", "================================="},
				{"TopsClans", "There are no gang created!"},
				{"TopsClans1", "=========================== [color lime]Tops gang [color clear]==========================="},
				{"TopsClans2", "Place {0}  gang {1}  Tag {2}  Score {3}  Kills {4}  Deaths {5}  Suicides {6}"},
				{"TopsClans3", "================================================================"},
				{"ClanPvp", "Pvp gang is now {0}."},
				{"ModifyDamage", "Stop {0} is of his gang and the gang has pvp disabled!"},
				{"ClansOnline", "Does [color red]not have [color clear]any gang online."},
				{"ClansOnline1", "========== [color lime]Clans Online [color clear]=========="},
				{"ClansOnline2", "gang [color cyan]{0} [color clear]Members Online [color red]{1}[color clear]."},
				{"ClansOnline3", "==============================="},
				{"ClanChat", "[color red]{0} say [color lime]{1}"}
			}, this);

            // Portugues Brasileiro
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"ChatTag", "Gangs"},
				{"NoPermission", "Você não tem permissão para usar esse comando."},
				{"NoExistent", "Nenhum jogador encontrado com o nome '{0}' existente."},
				{"NotHaveClanName", "Não há nenhuma gang chamada '{0}' nos dados."},
				{"PlayerNotMemberOfClan", "O jogador '{0}' não é membro da gang."},
				{"NotMemberOfAClan", "Você não faz parte de nenhuma gang."},
				{"NotOwnerOfClan", "Você não é dono da gang."},
				{"NotOwnerOrChiefOfClan", "Você não dono e nen mod da gang."},
				{"ClanNameAlreadyExists", "Já existe uma gang com o nome '{0}' você deve escolher outro nome."},
				{"ClanTagAlreadyExists", "Já existe uma gang com a tag '{0}' você deve escolher outra tag."},
				{"MaxLengthsOfName", "Você deve escolher um nome da gang com um máximo de '{0}' letras."},
				{"MaxLengthsOfTag", "Você deve escolher uma tag da gang com um máximo de '{0}' letras."},
				{"OnPlayerConnected", "Dono : {0} conectou-se"},
				{"OnPlayerConnected1", "Mod : {0} conectou-se"},
				{"OnPlayerConnected2", "Membro : {0} conectou-se"},
				{"OnPlayerDisconnected", "Dono : {0} desconectou-se"},
				{"OnPlayerDisconnected1", "Mod : {0} desconectou-se"},
				{"OnPlayerDisconnected2", "Membro : {0} desconectou-se"},
				{"HelpsAdmins", "--------------Helps Gang Admins------------"},
				{"HelpsAdmins1", "/gang delete <nomegang> - para apagar a gang."},
				{"HelpsAdmins2", "/gang name <nomegang> <novonome> - para alterar o nome da gang."},
				{"HelpsAdmins3", "/gang tag <nomegang> <novatag> - para alterar a tag da gang."},
				{"HelpsAdmins4", "/gang info <nomegang> - ver as informaçoes da gang."},
				{"HelpsAdmins5", "/gang stats <nomegang> - ver o status da gang."},
				{"HelpsAdmins6", "-------------------------------------------"},
				{"HelpsPlayers", "----------------------------Helps Gang Players----------------------------"},
				{"HelpsPlayers1", "/gang create <nomedagang> <tagdagang> - criar uma gang."},
				{"HelpsPlayers2", "/gang delete - para apagar a gang."},
				{"HelpsPlayers3", "/gang name - <novonomegang> - para lterar o nome da gang."},
				{"HelpsPlayers4", "/gang tag <novataggang> - para alterar a tag da gang."},
				{"HelpsPlayers5", "/gang invite <nomedojogador> - convidar para a gang."},
				{"HelpsPlayers6", "/gang accept - aceitar o convite para gang."},
				{"HelpsPlayers7", "/gang promote owner <nomedojogador> - promover o jogador a dono da gang."},
				{"HelpsPlayers8", "/gang promote mod <nomedojogador> - promote o membro para mod da gang."},
				{"HelpsPlayers9", "/gang demote <nomedojogador> - rebaixar a membro."},
				{"HelpsPlayers10", "/gang kick <nomedojogador> - kick o jogador da gang."},
				{"HelpsPlayers11", "/gang leave => saiu da gang."},
				{"HelpsPlayers12", "/gang online - ver jogadores da gang online."},
				{"HelpsPlayers21", "/gang list - ver as gangs online."},
				{"HelpsPlayers13", "/gang createmsg (descrisão) (msg) - para criar uma mensagem para a gang."},
				{"HelpsPlayers14", "/gang deletemsg (descrisão) - para apaga a mensagem da gang."},
				{"HelpsPlayers15", "/gang msgs - ver as mensagens da gang."},
				{"HelpsPlayers16", "/gang info - ver as informaçoes da gang."},
				{"HelpsPlayers17", "/gang stats - ver o status da gang."},
				{"HelpsPlayers18", "/gang tops - Ver a gang mais tops."},
				{"HelpsPlayers19", "/gang pvp - ativar e desativar o pvp entre membros."},
				{"HelpsPlayers20", "/g (Chat) - falar no chat da gang."},
				{"HelpsPlayers22", "---------------------------------------------------------------------------"},
				{"CreatClan", "Você ja é membro de uma gang."},
				{"CreatClan1", "Você criou uma gang com o nome '{0}' e a tag '{1}'"},
				{"DeletClan", "A gang '{0}' foi apagada."},
				{"ChangeNameClan", "O nome da gang '{0}' foi alterado para '{1}'"},
				{"ChangeTagClan", "A tag da gang '{0}' foi alterada para '{1}'"},
				{"InviteClan", " Sua gang ja tem o maximo {0} membros permitido."},
				{"InviteClan1", "Este jogador já foi convidado a participar de uma gang."},
				{"InviteClan2", "Este jogador já faz parte de uma gang."},
				{"InviteClan3", "Você convidou {0} para se juntar a gang."},
				{"InviteClan4", "{0} convidou você para se juntar a gang '{1}' você tem {2} segundo/s para aceitar /gang accept."},
				{"InviteClan5", "{0} não aceitou o seu convite para se juntar a gang."},
				{"InviteClan6", "O tempo terminou para aceitar o convite para entrar na gang '{0}'"},
				{"AcceptInvitation", "Você não tem nenhum convite para aceitar uma gang."},
				{"AcceptInvitation1", "O jogador que o convidou para gang não esta online, Você não pode aceitar o convite."},
				{"AcceptInvitation2", "Este clã não mais existe."},
				{"AcceptInvitation3", "{0} aceitou seu convite para entrar na gang."},
				{"AcceptInvitation4", "Você concordou em entrar na gang '{0}'"},
				{"PromotePlayer", "Você promoveu {0} ao dono da gang, Você é agora mod da gang."},
				{"PromotePlayer1", "{0} promovido você ao dono da gang."},
				{"PromotePlayer2", "Este membro já é mod da gang."},
				{"PromotePlayer3", "Você promoveu o {0} mod da gang"},
				{"PromotePlayer4", "{0} você foi promovido mod da gang."},
				{"DemotePlayer", "Você só pode rebaixar mod da sua gang."},
				{"DemotePlayer1", "O jogador não é mais mod da gang."},
				{"DemotePlayer2", "Você rebaixo '{0}' para membro da gang."},
				{"DemotePlayer3", "{0} rebaixo você para mod da gang."},
				{"KickPlayerOfClan", "você não pode kick o dono da gang."},
				{"KickPlayerOfClan1", "Voce kick '{0}' da gang."},
				{"KickPlayerOfClan2", "{0} Explusou você da gang."},
				{"LeaveClan", "Você deixou o clã '{0}'"},
				{"ClanOnline", "-----------------Gang Online-------------"},
				{"ClanOnline1", "-----------------------------"},
				{"CreatMessage", "Sua gang já recebeu o maximo de mensagens"},
				{"CreatMessage1", "Mensagem criada para a gang '{0}' Msg : {1}"},
				{"DeletMessage", "Nenhuma mensagem com a descrição '{0}'"},
				{"DeletMessage1", "Voce remover a msg: {0}"},
				{"SeeMessages", "Sua gang não tem nenhuma msg."},
				{"SeeMessages1", "--------------------Gangs Msgs--------------------"},
				{"SeeMessages2", "Descrição : {0} msg : {1}"},
				{"SeeMessages3", "-------------------------------------------------------"},
				{"InfoClan", "--------------------Gangs Infos------------------"},
				{"InfoClan1", "Informação da gang : {0}."},
				{"InfoClan2", "Dono : {0}"},
				{"InfoClan3", "Mod : {0}"},
				{"InfoClan4", "Membros : {0}"},
				{"InfoClan5", "-------------------------------------------"},
				{"ClanStats", "-----------------Gangs Stats---------------"},
				{"ClanStats1", "Stats da gang: {0}."},
				{"ClanStats2", "Score : {0} Placar : {1}"},
				{"ClanStats3", "Matou : {0} Mortes : {1} Suicidios {2}"},
				{"ClanStats4", "----------------------------------------"},
				{"TopsClans", "Não há nenhuma gang criada"},
				{"TopsClans1", "--------------------Tops Gangs----------------------"},
				{"TopsClans2", "Placar : {0}  Gang : {1}  Tag : {2} Score : {3}  Matou : {4} Morreu : {5} Suicidios : {6}"},
				{"TopsClans3", "--------------------------------------------------------------"},
				{"ClanPvp", "o combate da gang agora é {0}"},
				{"ModifyDamage", "Pare {0} é um membro da sua gang, pvp desativado."},
				{"ClansOnline", "Não ha gangs online neste momento."},
				{"ClansOnline1", "---------------Gangs Online----------------"},
				{"ClansOnline2", "Gang : {0} Membros online : {1}"},
				{"ClansOnline3", "----------------------------------------------------"},
				{"ClanChat", "{0} : {1}"}
			}, this, "pt-br");
			return;
		}

		static Dictionary<string, GangData> Data = new Dictionary<string, GangData>();
		static Dictionary<NetUser, NetUser> GangInvitations = new Dictionary<NetUser, NetUser>(); 

		void Loaded(){Data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, GangData>>("Gangs");}
		void SaveData(){Interface.Oxide.DataFileSystem.WriteObject("Gangs", Data);}

		GangData gang;
			public class GangData{public string tag {get; set;}
			public bool pvp {get; set;}public int kills {get; set;}
			public int deaths {get; set;}public int suicides {get; set;}
			public KeyValuePair<string, string> owner = new KeyValuePair<string, string>();
			public Dictionary<string, string> mods = new Dictionary<string, string>();
			public Dictionary<string, string> members = new Dictionary<string, string>();
			public Dictionary<string, string> messages = new Dictionary<string, string>();
		}

		string GetPlayerGang(string userid)
		{
			foreach (var pair in Data){
			if(pair.Value.members.ContainsKey(userid))
			return pair.Key;}return null;
		}

		string GetGangName(string args)
		{
			args = args.ToLower();foreach (var pair in Data){
			if(pair.Key.ToLower() == args || pair.Value.tag.ToLower() == args)
			return pair.Key;}return null;
		}

		private void OnRemovePlayerData(string userid)
		{
			string gangName = GetPlayerGang(userid);
			if(gangName != null)RemovePlayerOrGang(userid);
		}

		bool GangNameAlreadyExists(string gangname)
		{
			foreach(var pair in Data){
			if(pair.Key.ToLower() == gangname.ToLower())
			return true;}return false;
		}

		bool GangTagAlreadyExists(string gangtag)
		{
			foreach(var pair in Data){
			if(pair.Value.tag.ToLower() == gangtag.ToLower())
			return true;}return false;
		}

		private bool IsPluginsOnChat(NetUser netuser)
		{
			string userid = netuser.userID.ToString();
			if(permission.UserHasPermission(userid, "owner_chat"))return true;
			if(permission.UserHasPermission(userid, "mod_chat"))return true;
			if(permission.UserHasPermission(userid, "vip_chat"))return true;
			if(permission.UserHasPermission(userid, "youtuber_chat"))return true;
			return false;
		}

		private void OnKilled(TakeDamage takedamage, DamageEvent damage)
		{
			if(!(takedamage is HumanBodyTakeDamage))return;
			NetUser Attacker = damage.attacker.client?.netUser ?? null;
			NetUser Victim = damage.victim.client?.netUser ?? null;
			if(Attacker == null || Victim == null)return;
			string gangAttacker = GetPlayerGang(Attacker.userID.ToString());
			string gangVictim = GetPlayerGang(Victim.userID.ToString());
			if(gangAttacker != null){if(Attacker == Victim){
			Data[gangVictim].suicides++;Data[gangVictim].deaths++;
			SaveData();return;}Data[gangAttacker].kills++;if(gangVictim != null)
			Data[gangVictim].deaths++;SaveData();}
		}

		object ModifyDamage(TakeDamage takedamage, DamageEvent damage)
		{
			if(!(takedamage is HumanBodyTakeDamage))return null;
			NetUser Attacker = damage.attacker.client?.netUser ?? null;
			NetUser Victim = damage.victim.client?.netUser ?? null;
			if(Attacker == null || Victim == null)return null;
			if(Attacker == Victim)return null;
			var userid = Attacker.userID.ToString();
			string gangAttacker = GetPlayerGang(Attacker.userID.ToString());
			string gangVictim = GetPlayerGang(Victim.userID.ToString());
			if(gangAttacker == gangVictim && !Data[gangAttacker].pvp){
			object thereturn = Interface.GetMod().CallHook("canClansPvp", new object[]{Attacker});
			if(thereturn != null)return null;
			rust.SendChatMessage(Attacker, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ModifyDamage", this, userid), Victim.displayName));
			return CancelDamage(damage);}return null;
		}

		object CancelDamage(DamageEvent damage){
		damage.amount = 0f;damage.status = LifeStatus.IsAlive;return damage;}

		void RemovePlayerOrGang(string memberId)
		{
			var gangName = GetPlayerGang(memberId);
			if(gangName == null)return;
			gang = Data[gangName];
			if(gang.owner.Key == memberId){
			gang.mods.Remove(memberId);
			gang.members.Remove(memberId);SaveData();
			if(gang.mods.Count > 0){var mods = gang.mods.ToList();
			foreach(var pair in mods){if(pair.Key != memberId){
			gang.owner =  new KeyValuePair<string, string>(pair.Key, pair.Value);
			SaveData();return;}}}
			else if(gang.members.Count > 0){
			var members = gang.members.ToList();
			foreach(var pair in members){if(pair.Key != memberId){
			gang.owner =  new KeyValuePair<string, string>(pair.Key, pair.Value);
			SaveData();return;}}}
			else{Data.Remove(gangName);
			SaveData();}}else{
			gang.mods.Remove(memberId);
			gang.members.Remove(memberId);SaveData();}
		}

		object OnPlayerChat(NetUser netUser, string message)
		{
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			if(IsPluginsOnChat(netUser))return false;
			var simplemute = plugins.Find("simplemute");
			if(simplemute != null){
			bool isMuted = (bool) simplemute.Call("isMuted", netUser);
			if(isMuted)return null;}
			var gangName = GetPlayerGang(userid);
			if(gangName != null){
			object thereturn = Interface.GetMod().CallHook("canGangChat", new object[] {netUser});
			if(thereturn != null)return null;
			string name = rust.QuoteSafe("[" + Data[gangName].tag + "]" + username);
			string msg = rust.QuoteSafe(message);
			ConsoleNetworker.Broadcast(string.Concat("chat.add ", name, " ", msg));return false;}
			return null;
		}

		private void OnPlayerConnected(NetUser netUser)
		{
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			var gangName = GetPlayerGang(userid);
			if(gangName != null){gang = Data[gangName];
			if(gang.members[userid] != username){
			if(gang.owner.Key == userid)
			gang.owner =  new KeyValuePair<string, string>(userid, username);
			if(gang.mods.ContainsKey(userid))
			gang.mods[userid] = username;
			gang.members[userid] = username;SaveData();}
			if(msgconnect){
			foreach(PlayerClient player in PlayerClient.All){
			var useridplayer = player.userID.ToString();
			if(useridplayer == userid)continue;
			if(gang.members.ContainsKey(useridplayer)){if(gang.owner.Key == userid)
			rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("OnPlayerConnected", this, userid), username));
			else if(gang.mods.ContainsKey(userid))
			rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("OnPlayerConnected1", this, userid), username));
			else
			rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("OnPlayerConnected2", this, userid), username)); }}}}
		}

		private void OnPlayerDisconnected(uLink.NetworkPlayer netPlayer)
		{
			NetUser netUser = netPlayer.GetLocalData() as NetUser;
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			var gangName = GetPlayerGang(userid);
			if(gangName != null){if(msgdisconnect){gang = Data[gangName];
			foreach(PlayerClient player in PlayerClient.All){
			var useridplayer = player.userID.ToString();
			if(useridplayer == userid)continue;
			if(gang.members.ContainsKey(useridplayer)){if(gang.owner.Key == userid)
			rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("OnPlayerDisconnected", this, userid), username));
			else if(gang.mods.ContainsKey(userid))
			rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("OnPlayerDisconnected1", this, userid), username));
			else
			rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("OnPlayerDisconnected2", this, userid), username));}}}}
		}

		private void HelpsAdmins(NetUser netUser)
		{
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			bool IsAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
			if(netUser.CanAdmin() || IsAdmin || permission.UserHasPermission(userid, permissionCanGang))
			{
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NoPermission", this, userid));return;}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins1", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins2", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins3", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins4", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins5", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins6", this, userid));
		}


		private void HelpsPlayers(NetUser netUser)
		{
			var userid = netUser.userID.ToString();
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers1", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers2", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers3", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers4", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers5", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers6", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers7", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers8", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers9", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers10", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers11", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers12", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers13", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers14", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers15", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers16", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers17", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers18", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers19", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers20", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers21", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers22", this, userid));
		}

		void GangCreate(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			if(args.Length < 3){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers1", this, userid));return;}
			var gangName = args[1].ToString();
			var gangTag = args[2].ToString();
			if(GetPlayerGang(userid) != null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("CreatClan", this, userid));return;}
			if(GangNameAlreadyExists(gangName)){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ClanNameAlreadyExists", this, userid), gangName));return;}
			if(GangTagAlreadyExists(gangTag)){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ClanTagAlreadyExists", this, userid), gangTag));return;}
			if(gangName.Length > maxnome){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("MaxLengthsOfName", this, userid), maxnome));return;}
			if(gangTag.Length > maxtag){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("MaxLengthsOfTag", this, userid), maxtag));return;}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("CreatClan1", this, userid), gangName, gangTag));
			gang = new GangData();gang.tag = gangTag;
			gang.owner =  new KeyValuePair<string, string>(userid, username);
			gang.members.Add(userid, username);Data.Add(gangName, gang);SaveData();
		}

		void GangDelete(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			string gangName = string.Empty;
			bool IsAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
			if(netUser.CanAdmin() || IsAdmin || permission.UserHasPermission(userid, permissionCanGang))
			{if(args.Length < 2){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins1", this, userid)); return;}
			gangName = GetGangName(args[1].ToString());
			if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("NotHaveClanName", this, userid), args[1]));return;}}else{
			gangName = GetPlayerGang(userid);if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid)); return;}
			if(Data[gangName].owner.Key != userid){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotOwnerOfClan", this, userid)); return;}}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("DeletClan", this, userid), gangName));
			Data.Remove(gangName);SaveData();
		}

		void GangChangeName(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			string gangName = null;
			string newName = null;
			if(args.Length < 2){
			bool IsAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
			if(netUser.CanAdmin() || IsAdmin || permission.UserHasPermission(userid, permissionCanGang))
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins2", this, userid));
			else
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers3", this, userid));return;}
			bool IssAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
			if(netUser.CanAdmin() || IssAdmin || permission.UserHasPermission(userid, permissionCanGang))
			{if(args.Length < 3){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins2", this, userid)); return;}
			gangName = GetGangName(args[1].ToString());
			if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("NotHaveClanName", this, userid), args[1]));return;}
			if(GangNameAlreadyExists(newName)){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ClanNameAlreadyExists", this, userid), newName));return;}}else{
			gangName = GetPlayerGang(userid);if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			if(Data[gangName].owner.Key != userid){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotOwnerOfClan", this, userid));return;}
			newName = args[1].ToString();
			if(GangNameAlreadyExists(newName)){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ClanNameAlreadyExists", this, userid), newName));return;}
			if(newName.Length > maxnome){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("MaxLengthsOfName", this, userid), maxnome)); return;}}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ChangeNameClan", this, userid), gangName, newName));
			gang = new GangData();
			gang.tag = Data[gangName].tag;gang.pvp = Data[gangName].pvp;
			gang.kills = Data[gangName].kills;gang.deaths = Data[gangName].deaths;
			gang.suicides = Data[gangName].suicides;gang.owner = Data[gangName].owner;
			gang.mods = Data[gangName].mods;gang.members = Data[gangName].members;
			gang.messages = Data[gangName].messages;Data.Add(newName, gang);
			Data.Remove(gangName);SaveData();
		}

		void GangChangeTag(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			string gangName = string.Empty;
			string newTag = string.Empty;
			if(args.Length < 2){
			bool IsAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
			if(netUser.CanAdmin() || IsAdmin || permission.UserHasPermission(userid, permissionCanGang))
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins3", this, userid));else
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers4", this, userid));return;}
			bool IssAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
			if(netUser.CanAdmin() || IssAdmin || permission.UserHasPermission(userid, permissionCanGang))
			{if(args.Length < 3){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins3", this, userid));return;}
			gangName = GetGangName(args[1].ToString());if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("NotHaveClanName", this, userid), args[1]));return;}
			newTag = args[1].ToString();
			if(GangTagAlreadyExists(newTag)){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ClanTagAlreadyExists", this, userid), newTag));return;}}else{
			gangName = GetPlayerGang(userid);if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			if(Data[gangName].owner.Key != userid){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotOwnerOfClan", this, userid));return;}
			newTag = args[1].ToString();if(GangTagAlreadyExists(newTag)){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ClanTagAlreadyExists", this, userid), newTag));return;}
			if(newTag.Length > maxtag){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("MaxLengthsOfTag", this, userid), maxtag)); return;}}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ChangeTagClan", this, userid), gangName, newTag));
			Data[gangName].tag = newTag;SaveData();
		}

		void GangInvite(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			if(args.Length < 2){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers5", this, userid));return;}
			var gangName = GetPlayerGang(userid);if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			if(gang.owner.Key == userid || gang.mods.ContainsKey(userid)){if(gang.members.Count > maxmembros){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InviteClan", this, userid), maxmembros));return;}
			NetUser tragetUser = rust.FindPlayer(args[1]);if(tragetUser == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("NoExistent", this, userid), args[1]));return;}
			var tragetID = tragetUser.userID.ToString();
			if(GangInvitations.ContainsKey(tragetUser)){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("InviteClan1", this, userid));return;}
			if(GetPlayerGang(tragetID) != null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("InviteClan2", this, userid));return;}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InviteClan3", this, userid), tragetUser.displayName));
			rust.SendChatMessage(tragetUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InviteClan3", this, userid), netUser.displayName, gangName, tempoinvite));
			GangInvitations.Add(tragetUser, netUser);
			tempoaceita = timer.Once(tempoinvite, ()=>{GangInvitations.Remove(tragetUser);
			if(netUser.playerClient != null)
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InviteClan5", this, userid), tragetUser.displayName));
			if(tragetUser.playerClient != null)
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InviteClan6", this, userid), gangName));});}else
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotOwnerOrChiefOfClan", this, userid));
		}

		void GangAccept(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			if(!GangInvitations.ContainsKey(netUser)){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("AcceptInvitation", this, userid));return;}
			NetUser tragetUser = GangInvitations[netUser];
			if(tragetUser.playerClient == null){
			tempoaceita.Destroy();GangInvitations.Remove(netUser);
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("AcceptInvitation1", this, userid));return;}
			var gangName = GetPlayerGang(tragetUser.userID.ToString());if(gangName == null){
			tempoaceita.Destroy();GangInvitations.Remove(netUser);
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("AcceptInvitation2", this, userid));return;}
			tempoaceita.Destroy();
			GangInvitations.Remove(netUser);Data[gangName].members.Add(userid, username);
			rust.SendChatMessage(tragetUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("AcceptInvitation3", this, userid), username));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("AcceptInvitation4", this, userid), gangName));
			SaveData();
		}

		void GangPromote(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			NetUser tragetUser = null;
			string tragetID = null;
			if(args.Length < 3){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers7", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers8", this, userid));return;}
			var gangName = GetPlayerGang(userid);if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			gang = Data[gangName];if(gang.owner.Key != userid){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotOwnerOfClan", this, userid));return;}
			if(args[1].ToString().ToLower() == "owner"){
			tragetUser = rust.FindPlayer(args[2]);if(tragetUser == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("NoExistent", this, userid), args[2]));return;}
			tragetID = tragetUser.userID.ToString();
			if(GetPlayerGang(tragetID) != gangName){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("PlayerNotMemberOfClan", this, userid), tragetUser.displayName));return;}
			gang.owner =  new KeyValuePair<string, string>(tragetID, tragetUser.displayName.ToString());
			if(gang.mods.ContainsKey(tragetID))gang.mods.Remove(tragetID);
			if(!gang.mods.ContainsKey(userid))gang.mods.Add(userid, username);
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("PromotePlayer", this, userid), tragetUser.displayName));
			rust.SendChatMessage(tragetUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("PromotePlayer1", this, userid), username));SaveData();}
			else if(args[1].ToString().ToLower() == "mod"){
			tragetUser = rust.FindPlayer(args[2]);if(tragetUser == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("NoExistent", this, userid), args[2]));return;}
			tragetID = tragetUser.userID.ToString();if(GetPlayerGang(tragetID) != gangName){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("PlayerNotMemberOfClan", this, userid), tragetUser.displayName));return;}
			if(gang.mods.ContainsKey(tragetID)){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("PromotePlayer2", this, userid));return;}
			gang.mods.Add(tragetID, tragetUser.displayName);
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("PromotePlayer3", this, userid), tragetUser.displayName));
			rust.SendChatMessage(tragetUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("PromotePlayer4", this, userid), username));
			SaveData();}else{
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers7", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers8", this, userid));}
		}

		void GangDemote(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			if(args.Length < 2){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers9", this, userid));return;}
			string gangName = GetPlayerGang(userid);if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			gang = Data[gangName];if(gang.owner.Key != userid){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotOwnerOfClan", this, userid));return;}
			NetUser tragetUser = rust.FindPlayer(args[1]);if(tragetUser == null){
			rust.SendChatMessage(tragetUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("NoExistent", this, userid), args[1]));return;}
			var tragetID = tragetUser.userID.ToString();if(GetPlayerGang(tragetID) != gangName){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("DemotePlayer", this, userid));return;}
			if(!gang.mods.ContainsKey(tragetID)){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("DemotePlayer1", this, userid));return;}
			gang.mods.Remove(tragetID);SaveData();
			rust.SendChatMessage(tragetUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("DemotePlayer2", this, userid), tragetUser.displayName));
			rust.SendChatMessage(tragetUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("DemotePlayer3", this, userid), netUser.displayName));
		}

		void GangKick(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			if(args.Length < 2){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers10", this, userid));return;}
			var gangName = GetPlayerGang(userid);if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			gang = Data[gangName];
			if(gang.owner.Key == userid || gang.mods.ContainsKey(userid)){
			NetUser tragetUser = rust.FindPlayer(args[1]);if(tragetUser == null){
			rust.SendChatMessage(tragetUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("NoExistent", this, userid), args[1]));return;}
			var tragetID = tragetUser.userID.ToString();if(GetPlayerGang(tragetID) != gangName){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("PlayerNotMemberOfClan", this, userid), tragetUser.displayName));return;}
			if(gang.owner.Key == tragetID){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("KickPlayerOfClan", this, userid));return;}
			if(gang.mods.ContainsKey(tragetID))
			gang.mods.Remove(tragetID);gang.members.Remove(tragetID);SaveData();
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("KickPlayerOfClan1", this, userid), tragetUser.displayName));
			rust.SendChatMessage(tragetUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("KickPlayerOfClan2", this, userid), netUser.displayName));}else
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotOwnerOrChiefOfClan", this, userid));
		}

		void GangLeave(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var gangName = GetPlayerGang(userid);
			if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			RemovePlayerOrGang(userid);
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("LeaveClan", this, userid), gangName));
		}

		void GangOnline(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var gangName = GetPlayerGang(userid);
			if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			gang = Data[gangName];
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("ClanOnline", this, userid));
			NetUser owner = rust.FindPlayer(gang.owner.Key);if(owner != null)
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InfoClan2", this, userid), owner.displayName));
			StringBuilder sbMods = new StringBuilder();
			foreach(PlayerClient player in PlayerClient.All){
			if(gang.mods.ContainsKey(player.userID.ToString())){
			sbMods.Append(" || " + player.userName);if(sbMods.Length > maxLengthStringBuilder){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InfoClan3", this, userid), sbMods + " ||"));
			sbMods.Length = 0;}}}if(sbMods.Length > 0)
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InfoClan3", this, userid), sbMods + " ||"));
			StringBuilder sbMembers = new StringBuilder();
			foreach(PlayerClient player in PlayerClient.All){
			if(gang.members.ContainsKey(player.userID.ToString())){
			sbMembers.Append(" || " + player.userName);if(sbMembers.Length > maxLengthStringBuilder){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InfoClan4", this, userid), sbMembers + " ||"));
			sbMembers.Length = 0;}}}if(sbMembers.Length > 0)
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InfoClan4", this, userid), sbMembers + " ||"));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("ClanOnline1", this, userid));
		}

		void GangCreateMsg(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var gangName = GetPlayerGang(userid);
			if(args.Length < 3){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers13", this, userid));return;}
			if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			gang = Data[gangName];
			if(gang.owner.Key == userid || gang.mods.ContainsKey(userid)){if(gang.messages.Count > maxmsgs){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("CreatMessage", this, userid));return;}
			var description = args[1].ToString();
			string message = string.Join(" ", args).Replace("creatmessage", "").Replace(description, "");
			if(gang.messages.ContainsKey(description))
			gang.messages.Remove(description);gang.messages.Add(description, message);
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("CreatMessage1", this, userid), description, message));SaveData();}else
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotOwnerOrChiefOfClan", this, userid));
		}

		void GangDeleteMsg(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var gangName = GetPlayerGang(userid);
			if(args.Length < 2){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers14", this, userid));return;}
			if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			gang = Data[gangName];
			if(gang.owner.Key == userid || gang.mods.ContainsKey(userid)){
			var description = args[1].ToString();if(!gang.messages.ContainsKey(description)){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("DeletMessage", this, userid), description));return;}
			gang.messages.Remove(description);
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("DeletMessage1", this, userid), description));
			SaveData();}else
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotOwnerOrChiefOfClan", this, userid));
		}

		void GangMsgs(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var gangName = GetPlayerGang(userid);
			if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			gang = Data[gangName];if(gang.messages.Count == 0){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("SeeMessages", this, userid));return;}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("SeeMessages1", this, userid));
			foreach(var pair in gang.messages)
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("SeeMessages2", this, userid), pair.Key, pair.Value));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("SeeMessages3", this, userid));
		}

		void GangInfo(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var gangName = GetPlayerGang(userid);
			bool IsAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
			if(netUser.CanAdmin() || IsAdmin || permission.UserHasPermission(userid, permissionCanGang)){
			if(args.Length < 2){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins4", this, userid));return;}
			gangName = GetPlayerGang(args[1].ToString());
			if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("NotHaveClanName", this, userid), args[1]));return;}}else{
			if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}}
			gang = Data[gangName];
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("InfoClan", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InfoClan1", this, userid), gangName));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InfoClan2", this, userid), gang.owner.Value));
			if(gang.mods.Count > 0){
			StringBuilder sbMods = new StringBuilder();
			foreach(var pair in gang.mods){
			sbMods.Append(" || " + pair.Value);if(sbMods.Length > maxLengthStringBuilder){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InfoClan3", this, userid), sbMods + " ||"));
			sbMods.Length = 0;}}if(sbMods.Length > 0)
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InfoClan3", this, userid), sbMods + " ||"));}
			if(gang.members.Count > 0){
			StringBuilder sbMembers = new StringBuilder();
			foreach(var pair in gang.members){
			sbMembers.Append(" || " + pair.Value);
			if(sbMembers.Length > maxLengthStringBuilder){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InfoClan4", this, userid), sbMembers + " ||"));
			sbMembers.Length = 0;}}if(sbMembers.Length > 0)
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("InfoClan4", this, userid), sbMembers + " ||"));}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("InfoClan5", this, userid));
		}

		void GangStats(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var gangName = GetPlayerGang(userid);
			bool IsAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
			if(netUser.CanAdmin() || IsAdmin || permission.UserHasPermission(userid, permissionCanGang)){
			if(args.Length < 2){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsAdmins5", this, userid));return;}
			gangName = GetGangName(args[1].ToString());if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("NotHaveClanName", this, userid), args[1]));return;}}
			else{if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}}
			gang = Data[gangName];int score = gang.kills - gang.deaths;
			var topsGangs = Data.Values.OrderByDescending(a => a.kills).ToList();int place = 0;
			for (int i = 0; i < topsGangs.Count; i++){
			if(topsGangs[i].tag == gang.tag)place = i + 1;}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("ClanStats", this, userid));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ClanStats1", this, userid), gangName));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ClanStats2", this, userid), score, place));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("ClanStats3", this, userid), gang.kills, gang.deaths, gang.suicides));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("ClanStats4", this, userid));
		}

		void GangTops(NetUser netUser)
		{
			var userid = netUser.userID.ToString();
			if(Data.Count == 0){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("TopsClans", this, userid));return;}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("TopsClans1", this, userid));
			var topsGangs = Data.Values.OrderByDescending(a => a.kills).ToList();
			for(int i = 0; i < maxtops; i++){if(i >= topsGangs.Count)break;
			int score = topsGangs[i].kills - topsGangs[i].deaths;
			var gangName = GetPlayerGang(topsGangs[i].owner.Key);
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("TopsClans2", this, userid), (i + 1), gangName, topsGangs[i].tag, score, topsGangs[i].kills, topsGangs[i].deaths, topsGangs[i].suicides));}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("TopsClans3", this, userid));
		}

		void GangPvp(NetUser netUser)
		{
			var userid = netUser.userID.ToString();
			var gangName = GetPlayerGang(userid);
			if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return;}
			gang = Data[gangName];if(gang.owner.Key != userid){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotOwnerOfClan", this, userid));return;}
			if(gang.pvp)gang.pvp = false;else gang.pvp = true;SaveData();
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), string.Format(lang.GetMessage("TopsClans2", this, userid), gang.pvp));
		}

		void GangList(NetUser netUser, string[] args)
		{
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			var gangsOnline = new Dictionary<string, int>();
			foreach(PlayerClient player in PlayerClient.All){
			string gangName = GetPlayerGang(player.userID.ToString());
			if(gangName != null){if(gangsOnline.ContainsKey(gangName))
			gangsOnline[gangName]++;else
			gangsOnline.Add(gangName, 1);}}if(gangsOnline.Count == 0){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("ClansOnline", this, userid));return;}
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("ClansOnline1", this, userid));
			foreach(var pair in gangsOnline)
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid) , string.Format(lang.GetMessage("ClansOnline2", this, userid), pair.Key, pair.Value));
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("ClansOnline3", this, userid));
		}

		[ChatCommand("g")]
		void cmdChatG(NetUser netUser, string command, string[] args)
		{
			var userid = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			var gangName = GetPlayerGang(userid);
			if(args.Length == 0){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("HelpsPlayers20", this, userid));return;}
			if(gangName == null){
			rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, userid), lang.GetMessage("NotMemberOfAClan", this, userid));return; } gang = Data[gangName];
			foreach(PlayerClient player in PlayerClient.All){if(gang.members.ContainsKey(player.userID.ToString()))
			rust.SendChatMessage(player.netUser, "[GANG]" , string.Format(lang.GetMessage("ClanChat", this, userid), username, string.Join(" ", args)));}
		}

		void cmdGang(NetUser netUser, string command, string[] args)
		{
			var userid = netUser.userID.ToString();
			if(args.Length == 0)
		{
			HelpsPlayers(netUser);
			return;
		}
			switch(args[0].ToLower())
			{
				case "help":
					HelpsAdmins(netUser);
					break;
				case "create":
					GangCreate(netUser, args);
					break;
				case "delete":
					GangDelete(netUser, args);
					break;
				case "name":
					GangChangeName(netUser, args);
					break;
				case "tag":
					GangChangeTag(netUser, args);
					break;
				case "invite":
					GangInvite(netUser, args);
					break;
				case "accept":
					GangAccept(netUser, args);
					break;
				case "promote":
					GangPromote(netUser, args);
					break;
				case "demote":
					GangDemote(netUser, args);
					break;
				case "kick":
					GangKick(netUser, args);
					break;
				case "leave":
					GangLeave(netUser, args);
					break;
				case "online":
					GangOnline(netUser, args);
					break;
				case "list":
					GangList(netUser, args);
					break;
				case "createmsg":
					GangCreateMsg(netUser, args);
					break;
				case "deletemsg":
					GangDeleteMsg(netUser, args);
					break;
				case "msgs":
					GangMsgs(netUser, args);
					break;
				case "info":
					GangInfo(netUser, args);
					break;
				case "stats":
					GangStats(netUser, args);
					break;
				case "tops":
					GangTops(netUser);
					break;
				case "pvp":
					GangPvp(netUser);
					break;
				default:{
					HelpsPlayers(netUser);
					break;
				}
			}
		}
	}
}