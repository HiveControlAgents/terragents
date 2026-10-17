using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using TerrAgents.NPCs;

namespace TerrAgents.Systems
{
	/// <summary>
	/// Keeps one AgentNPC per agent on shift, and places the stations: a flat city around the world
	/// spawn point, in tiles to the left (thinking places) and right (doing places).
	/// </summary>
	public class CitySystem : ModSystem
	{
		private static readonly Dictionary<string, int> Spawned = new(); // agentId -> npc index

		private static readonly Dictionary<string, int> Offsets = new()
		{
			["meeting"] = -45, ["library"] = -30, ["desk"] = -15, ["lounge"] = 0,
			["terminal"] = 15, ["testbench"] = 30, ["mergestation"] = 45,
		};

		/// <summary>Tile x of a station; "user" means next to the player.</summary>
		public static int StationX(string station, Player player)
		{
			if (station == "user") return (int)(player.Center.X / 16f) + 3;
			return Main.spawnTileX + (Offsets.TryGetValue(station, out var dx) ? dx : 0);
		}

		/// <summary>Position of an agent among those at the same station (0, 1, 2...), so they stand side by side.</summary>
		public static int Slot(AgentInfo agent)
		{
			int slot = 0;
			foreach (var other in ForemanClient.Agents.Values)
				if (other.Active && other.Station == agent.Station && string.CompareOrdinal(other.Id, agent.Id) < 0) slot++;
			return slot;
		}

		public override void OnWorldUnload() => Spawned.Clear();

		public override void PostUpdateWorld()
		{
			if (Main.netMode != Terraria.ID.NetmodeID.SinglePlayer) return; // MVP: single player only
			foreach (var a in ForemanClient.Agents.Values)
			{
				bool alive = Spawned.TryGetValue(a.Id, out var idx) && Main.npc[idx].active && Main.npc[idx].ModNPC is AgentNPC n && n.AgentId == a.Id;
				if (a.Active && !alive)
				{
					int x = StationX(a.Station, Main.LocalPlayer) * 16;
					int y = (Main.spawnTileY - 3) * 16;
					int i = NPC.NewNPC(new EntitySource_Misc("TerrAgents"), x, y, ModContent.NPCType<AgentNPC>());
					if (i < Main.maxNPCs && Main.npc[i].ModNPC is AgentNPC created)
					{
						created.AgentId = a.Id;
						Spawned[a.Id] = i;
					}
				}
				else if (!a.Active && alive)
				{
					Main.npc[idx].active = false;
					Spawned.Remove(a.Id);
				}
			}
			// agents the Core no longer knows (after a new snapshot)
			foreach (var id in new List<string>(Spawned.Keys))
			{
				if (ForemanClient.Agents.ContainsKey(id)) continue;
				Main.npc[Spawned[id]].active = false;
				Spawned.Remove(id);
			}
		}
	}
}
