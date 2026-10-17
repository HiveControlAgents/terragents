using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TerrAgents.Systems
{
	/// <summary>One agent as the Core describes it (agent.upsert / snapshot).</summary>
	public class AgentInfo
	{
		public string Id = "";
		public string Name = "";
		public string Role = "";
		public string State = "idle";
		public string Station = "lounge";
		public string Activity = "";
		public bool Active;
		public bool Paused;
		public Color Color = Color.White;
		public float Energy = -1f;     // 0..1, -1 when the Core sends none
		public string Say = "";
		public uint SayUntil;           // Main.GameUpdateCount
	}

	/// <summary>
	/// WebSocket client of the TerrAgents Core (protocol v1, see protocol/protocol.md). Frames arrive on a
	/// background task and are applied on the game thread in PostUpdateEverything.
	/// </summary>
	public class ForemanClient : ModSystem
	{
		public const string Url = "ws://127.0.0.1:7878";

		public static readonly Dictionary<string, AgentInfo> Agents = new();
		public static readonly Dictionary<string, string> OpenDecisions = new(); // id -> question
		public static string Status = "desconectado";

		private static readonly ConcurrentQueue<string> Inbox = new();
		private static ClientWebSocket socket;
		private static CancellationTokenSource cancel;
		private static int nextId;

		public override void OnWorldLoad()
		{
			if (Main.netMode == Terraria.ID.NetmodeID.Server) return;
			cancel = new CancellationTokenSource();
			_ = Task.Run(() => Loop(cancel.Token));
		}

		public override void OnWorldUnload()
		{
			cancel?.Cancel();
			try { socket?.Abort(); } catch { }
			socket = null;
			Agents.Clear();
			OpenDecisions.Clear();
			Status = "desconectado";
		}

		/// <summary>Reconnects every 5 s while the world is open.</summary>
		private static async Task Loop(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					socket = new ClientWebSocket();
					await socket.ConnectAsync(new Uri(Url), ct);
					Status = "conectado";
					await SendRaw("{\"v\":1,\"type\":\"hello\",\"modVersion\":\"0.1\",\"protocol\":1,\"client\":\"terraria\"}");
					var buffer = new byte[64 * 1024];
					var message = new StringBuilder();
					while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
					{
						var r = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
						if (r.MessageType == WebSocketMessageType.Close) break;
						message.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
						if (!r.EndOfMessage) continue;
						Inbox.Enqueue(message.ToString());
						message.Clear();
					}
					Status = "desconectado";
				}
				catch (OperationCanceledException) { return; }
				catch (Exception e)
				{
					Status = "sem Core (" + e.Message + ")";
				}
				try { await Task.Delay(5000, ct); } catch (OperationCanceledException) { return; }
			}
		}

		private static async Task SendRaw(string json)
		{
			var s = socket;
			if (s == null || s.State != WebSocketState.Open) throw new InvalidOperationException("TerrAgents Core não está conectado");
			await s.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		/// <summary>Send a client intent (adds v and id). Errors go to chat.</summary>
		public static void Send(Dictionary<string, object> msg)
		{
			msg["v"] = 1;
			msg["id"] = "t" + Interlocked.Increment(ref nextId);
			var json = JsonSerializer.Serialize(msg);
			_ = Task.Run(async () =>
			{
				try { await SendRaw(json); }
				catch (Exception e) { Inbox.Enqueue(JsonSerializer.Serialize(new { type = "error", message = e.Message })); }
			});
		}

		public override void PostUpdateEverything()
		{
			for (int i = 0; i < 50 && Inbox.TryDequeue(out var raw); i++)
			{
				try { Apply(raw); }
				catch (Exception e) { Mod.Logger.Warn("TerrAgents: frame ignorado: " + e.Message); }
			}
		}

		private static void Apply(string raw)
		{
			using var doc = JsonDocument.Parse(raw);
			var m = doc.RootElement;
			switch (Str(m, "type"))
			{
				case "snapshot":
					Agents.Clear();
					foreach (var a in m.GetProperty("agents").EnumerateArray()) Upsert(a);
					OpenDecisions.Clear();
					foreach (var d in m.GetProperty("decisions").EnumerateArray()) Decision(d, quiet: true);
					Say("TerrAgents conectado: " + Agents.Count + " agentes. Projeto de fã, use por sua conta e risco (DISCLAIMER.md).", Color.LightGreen);
					break;
				case "agent.upsert":
					Upsert(m.GetProperty("agent"));
					break;
				case "agent.say":
					var speaker = Str(m, "agentId");
					var to = Str(m, "to");
					var text = (to != "" && to != "all" && to != "user" ? "@" + NameOf(to) + " " : "") + Str(m, "text");
					if (Agents.TryGetValue(speaker, out var who)) { who.Say = text; who.SayUntil = Main.GameUpdateCount + 60 * 7; }
					Say(NameOf(speaker) + ": " + text, Color.White);
					break;
				case "feed.add":
					var item = m.GetProperty("item");
					var kind = Str(item, "kind");
					if (kind is "goal" or "merge" or "error" or "ci" or "task")
						Say(Str(item, "text"), kind == "error" ? Color.OrangeRed : Color.LightSkyBlue);
					break;
				case "decision.upsert":
					Decision(m.GetProperty("decision"), quiet: false);
					break;
				case "error":
					Say("TerrAgents: " + Str(m, "message"), Color.OrangeRed);
					break;
			}
		}

		private static void Upsert(JsonElement a)
		{
			var id = Str(a, "id");
			if (!Agents.TryGetValue(id, out var info)) Agents[id] = info = new AgentInfo { Id = id };
			info.Name = Str(a, "name");
			info.Role = Str(a, "role");
			info.State = Str(a, "state");
			info.Station = Str(a, "station");
			info.Activity = Str(a, "activity");
			info.Active = a.TryGetProperty("active", out var act) && act.ValueKind == JsonValueKind.True;
			info.Paused = a.TryGetProperty("paused", out var p) && p.ValueKind == JsonValueKind.True;
			info.Color = ParseColor(Str(a, "color"));
			info.Energy = a.TryGetProperty("energy", out var en) && en.ValueKind == JsonValueKind.Number ? (float)en.GetDouble() : -1f;
		}

		private static void Decision(JsonElement d, bool quiet)
		{
			var id = Str(d, "id");
			if (Str(d, "status") != "open")
			{
				OpenDecisions.Remove(id);
				return;
			}
			var question = Str(d, "question");
			bool isNew = !OpenDecisions.ContainsKey(id);
			OpenDecisions[id] = question;
			if (quiet || !isNew) return;
			var options = new List<string>();
			foreach (var o in d.GetProperty("options").EnumerateArray()) options.Add(o.GetString());
			Say($"[{id}] {question}  ({string.Join(" | ", options)})  /ta responder {id} <opção>", Color.Gold);
		}

		public static string NameOf(string id) => Agents.TryGetValue(id, out var a) ? a.Name : id;

		private static string Str(JsonElement e, string key) =>
			e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";

		private static Color ParseColor(string hex)
		{
			if (hex.StartsWith("#") && hex.Length == 7 && int.TryParse(hex.Substring(1), System.Globalization.NumberStyles.HexNumber, null, out var rgb))
				return new Color((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
			return Color.White;
		}

		private static void Say(string text, Color color)
		{
			if (Main.netMode != Terraria.ID.NetmodeID.Server) Main.NewText(text, color);
		}
	}
}
