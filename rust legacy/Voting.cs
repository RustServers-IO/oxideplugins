using System.Collections.Generic;

namespace Oxide.Plugins{
        [Info("Voting", "mcnovinho08", "1.0.0")]
        class Voting : RustLegacyPlugin{

	  static bool VotingSystem = true;
	  static bool VotingEnq = false;
	  
	  static bool MensagemGlobal = true;
	  
	  static string chatPrefix = "Voting";
	
	  static float TimeToEnd = 30f;
	
      const string permiAdmin = "voting.use";
  
	  static int MinVotes = 1;
	  
	  static int VoteYes = 0;
	  static int VoteNo = 0;
	  
	  static List <string> PlayersQueVotaram = new List<string>();
	  
        void OnServerInitialized(){
			CheckCfg<string>("Settings: Chat Prefix: ", ref chatPrefix);
			CheckCfg<bool>("Settings: System Status: ", ref VotingSystem);
			CheckCfg<bool>("Settings: Announcer Voting Global: ", ref MensagemGlobal);
			CheckCfg<int>("Settings: Min Votes: ", ref MinVotes);
			CheckCfg<float>("Settings: Time To End Voting: ", ref TimeToEnd);
			permission.RegisterPermission(permiAdmin, this);
			Lang();
			SaveConfig();
        }

		protected override void LoadDefaultConfig(){} 
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}
	
        void Lang(){
			
			// english
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"NoPermission", "You are not allowed to use this command!"},
				{"SystemOFF", "System is disabled, this action can not be performed!"},
				{"VotingMSG", "There is already a vote in progress!"},
				{"VotingMSG1", "You have not set any messages to be announced in the poll!"},
				{"VotingMSG2", "There are no polls currently playing!"},
				{"VotingMSG3", "You already voted, wait until the voting is complete!"},
				{"VotingMSG4", "Use /vote yes | no - to vote in the poll!"},
				{"VotingMSG5", "Player [color orange]{0} [color clear] voted [color green] YES [color clear] to Poll!"},
				{"VotingMSG6", "Player [color orange]{0} [color clear] voted [color red] NOT [color clear] to Poll!"},
				{"VotingMSG7", "We have not reached the minimum of {0} votes, to perform this action"},
				{"VotingMSG8", "There was a tie in the vote!"},
				
				{"VotingHelp", "=-=-=-=-=-=-=-= [ [color green]VOTING [color clear] =-=-=-=-=-=-=-="},
				{"VotingHelp1", "To vote [color green] YES [color clear], Use /vote 'yes'"},
				{"VotingHelp2", "To vote [color red] DO NOT [color clear], Use /vote 'no'"},
				{"VotingHelp3", "Note: It is only possible to vote once, in each poll!"},
				
				{"VotingWinYes", "[color green]YES [color clear], won with {0} votes!"},
				{"VotingWinNo", "[color red]NO [color clear], won with {0} votes!"}

			}, this);

			// brazilian
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"NoPermission", "Você não tem permissão para usar este comando!"},
				{"SystemOFF", "O Sistema esta desativado, esta ação não pode ser realizada!"},
				{"VotingMSG", "Já existe uma votação em andamento!"},
				{"VotingMSG1", "Você não definiu nenhuma mensagem para ser anunciada na enquete!"},
				{"VotingMSG2", "Não existe nenhuma votação rolando neste momento!"},
				{"VotingMSG3", "Você ja votou, espere ate a finalização da votação!"},
				{"VotingMSG4", "Use /vote yes|no - para votar na enquete!"},
				{"VotingMSG5", "Player [color orange]{0} [color clear]votou [color green]SIM [color clear]para a Enquete!"},
				{"VotingMSG6", "Player [color orange]{0} [color clear]votou [color red]NÃO [color clear]para a Enquete!"},
				{"VotingMSG7", "Não foram alcançado o minimo de {0} votos, para realizar esta ação"},
				{"VotingMSG8", "Houve um empate na votação!"},
				
				{"VotingHelp", "=-=-=-=-=-=-=-= [ [color green]VOTING [color clear] =-=-=-=-=-=-=-="},
				{"VotingHelp1", "Para votar [color green]SIM[color clear], Use /vote 'yes'"},
				{"VotingHelp2", "Para votar [color red]NÃO[color clear], Use /vote 'no'"},
				{"VotingHelp3", "OBS: So e possivel votar uma vez, em cada enquete!"},
				
				{"VotingWinYes", "[color green]SIM[color clear], ganhou com {0} votos!"},
				{"VotingWinNo", "[color red]NÃO[color clear], ganhou com {0} votos!"}

			}, this, "pt-br");

			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"NoPermission", "Usted no tiene permiso para utilizar este comando!"},
				{"SystemOFF", "El sistema está desactivada, esta acción no se hace!"},
				{"VotingMSG", "Ya existe un voto en curso!"},
				{"VotingMSG1", "No se ha establecido ningún mensaje que se anunciarán en la encuesta!"},
				{"VotingMSG2", "No hay ninguna votación pasando en este momento!"},
				{"VotingMSG3", "Ya has votado esperar hasta la finalización de la votación!"},
				{"VotingMSG4", "Uso /vote yes | no - a votar en la encuesta!"},
				{"VotingMSG5", "Player [color orange]{0} [color claro] votó [color green] SÍ [color clear] al voto!"},
				{"VotingMSG6", "Player [color orange]{0} [color clear]votados [color red] NO [color clear] al voto!"},
				{"VotingMSG7", "No se alcanzaron los 0} {califican mínimo, para llevar a cabo esta acción"},
				{"VotingMSG8", "Hubo un empate!"},
				
				{"VotingHelp", "=-=-=-=-=-=-=-= [ [color green]VOTING [color clear] =-=-=-=-=-=-=-="},
				{"VotingHelp1", "Para votar [color green] SÍ [color clear], uso /vote 'yes'"},
				{"VotingHelp2", "Para votar [color red]NO[color clear], Use /vote 'no'"},
				{"VotingHelp3", "Nota: Sólo se puede votar una vez en cada sondeo!"},
				
				{"VotingWinYes", "[color green]SI [color clear], ganó con {0} califican!"},
				{"VotingWinNo", "[color red]NO [color clear], ganaron con {0} votos!"}

			}, this, "spanish");
			return;
        }
		
	  [ChatCommand("voting")]
	  void CommandOpen(NetUser netuser, string command, string[] args)
	  {
	  string ID = netuser.userID.ToString();
	  if (!AcessAdmin(netuser)) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("NoPermission", this, ID)); return;}
	  if (!VotingSystem) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("SystemOFF", this, ID)); return; }
	  string message = "";
	  foreach (string arg in args)
      {
          message = message + " " + arg;
      }
	  if (VotingEnq) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("VotingMSG", this, ID)); return;}
	  if (message == "") { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("VotingMSG1", this, ID)); return; }
	  AnuncioMSG(message);
	  Finalizar();
	  }

        [ChatCommand("vote")]
        void Command(NetUser netuser, string command, string[] args) {
            string ID = netuser.userID.ToString();
            if (!VotingSystem) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("SystemOFF!", this, ID)); return; }
            if (!VotingEnq) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("VotingMSG2", this, ID)); return; }
            if (PlayersQueVotaram.Contains(ID)) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("VotingMSG3", this, ID)); return; }
            if (args.Length == 0) { rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("VotingMSG4", this, ID)); return; }
            switch (args[0].ToLower()) {
                case "yes":
                    VoteYes++;
                    PlayersQueVotaram.Add(ID);
                    if (MensagemGlobal) { rust.BroadcastChat(chatPrefix, string.Format(lang.GetMessage("VotingMSG5", this), netuser.displayName)); return; }
                    break;
                case "no":
                    VoteNo++;
                    PlayersQueVotaram.Add(ID);
                    if (MensagemGlobal) { rust.BroadcastChat(chatPrefix, string.Format(lang.GetMessage("VotingMSG6", this), netuser.displayName)); return; }
                    break;
                default: {
                        HelpCommand(netuser);
                        break;
                    }
            } }
	 
	  void HelpCommand(NetUser netuser){
		string ID = netuser.userID.ToString();
		rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("VotingHelp", this, ID));
		rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("VotingHelp1", this, ID));
		rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("VotingHelp2", this, ID));
		rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("VotingHelp3", this, ID));
		rust.SendChatMessage(netuser, chatPrefix, lang.GetMessage("VotingHelp", this, ID));
	 }

	  void AnuncioMSG(string mensagem){
      rust.BroadcastChat(chatPrefix, lang.GetMessage("VotingHelp", this));
      rust.BroadcastChat(chatPrefix, string.Format(lang.GetMessage("{0}", this), mensagem));
      rust.BroadcastChat(chatPrefix, lang.GetMessage("VotingMSG4", this));
      rust.BroadcastChat(chatPrefix, lang.GetMessage("VotingHelp", this));
	  VotingEnq = true;
	  }

	  void Finalizar()
	  {
            timer.Once(TimeToEnd, () =>
            {
                int Votos = VoteYes + VoteNo;
                if (Votos < MinVotes)
                {
                    VoteYes = 0;
                    VoteNo = 0;
                    PlayersQueVotaram.Clear();
                    rust.BroadcastChat(chatPrefix, string.Format(lang.GetMessage("VotingMSG7", this), MinVotes));
					VotingEnq = false;
                    return;
                }
                int VotosComp = VoteYes.CompareTo(VoteNo);
                if (VotosComp == 0) {
                rust.BroadcastChat(chatPrefix, lang.GetMessage("VotingMSG8", this));
				VoteYes = 0;
                VoteNo = 0;
                PlayersQueVotaram.Clear();
				VotingEnq = false; }
                else if (VotosComp == 1) {
                rust.BroadcastChat(chatPrefix, string.Format(lang.GetMessage("VotingWinYes", this), VoteYes));
				VoteYes = 0;
                VoteNo = 0;
                PlayersQueVotaram.Clear();
				VotingEnq = false; }
                else if (VotosComp == -1) {
                rust.BroadcastChat(chatPrefix, string.Format(lang.GetMessage("VotingWinNo", this), VoteNo));
                VoteYes = 0;
                VoteNo = 0;
                PlayersQueVotaram.Clear();
				VotingEnq = false; }
            });
	  }
	  
	 	bool AcessAdmin(NetUser netuser){
		if(netuser.CanAdmin())return true; 
		if(permission.UserHasPermission(netuser.userID.ToString(), permiAdmin))return true;
		return false;
		}	

    }
}