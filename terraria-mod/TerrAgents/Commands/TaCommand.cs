using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;
using TerrAgents.Systems;

namespace TerrAgents.Commands
{
	/// <summary>/ta status | /ta meta texto | /ta responder id opção | /ta falar agente texto</summary>
	public class TaCommand : ModCommand
	{
		public override string Command => "ta";
		public override CommandType Type => CommandType.Chat;
		public override string Usage => "/ta status | /ta meta <texto> | /ta responder <id> <opção> | /ta falar <agente> <texto>";
		public override string Description => "Fala com o TerrAgents Core";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			string Rest(int from) => string.Join(" ", args.Skip(from));
			switch (args.Length > 0 ? args[0] : "status")
			{
				case "meta" when args.Length > 1:
					ForemanClient.Send(new Dictionary<string, object> { ["type"] = "goal.submit", ["text"] = Rest(1) });
					caller.Reply("Meta enviada ao time.");
					break;
				case "responder" when args.Length > 2:
					ForemanClient.Send(new Dictionary<string, object> { ["type"] = "decision.answer", ["decisionId"] = args[1], ["option"] = Rest(2) });
					caller.Reply($"Resposta enviada para {args[1]}.");
					break;
				case "falar" when args.Length > 2:
					ForemanClient.Send(new Dictionary<string, object> { ["type"] = "user.message", ["to"] = args[1], ["text"] = Rest(2) });
					break;
				default:
					caller.Reply($"TerrAgents: {ForemanClient.Status}, {ForemanClient.Agents.Values.Count(a => a.Active)} agentes no turno, {ForemanClient.OpenDecisions.Count} decisões abertas.");
					foreach (var a in ForemanClient.Agents.Values.Where(a => a.Active))
						caller.Reply($"  {a.Name} ({a.Role}): {a.State} em {a.Station}, {a.Activity}");
					foreach (var d in ForemanClient.OpenDecisions)
						caller.Reply($"  [{d.Key}] {d.Value}");
					break;
			}
		}
	}
}
