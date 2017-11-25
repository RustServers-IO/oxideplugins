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
				{"AcceptInvitation3", "{0} aceitou seu 