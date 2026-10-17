using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TerrAgents.NPCs;

namespace TerrAgents.Systems
{
	/// <summary>
	/// The city: one office per sector (station), side by side, shared by every agent working there.
	/// `/ta construir` builds it where the player stands; its position is saved with the world.
	/// Also keeps one AgentNPC per agent on shift.
	/// </summary>
	public class CitySystem : ModSystem
	{
		public static readonly string[] Order = { "meeting", "library", "desk", "lounge", "terminal", "testbench", "mergestation" };

		public static readonly Dictionary<string, string> Names = new()
		{
			["meeting"] = "Sala de reunião", ["library"] = "Biblioteca", ["desk"] = "Escritório", ["lounge"] = "Descanso",
			["terminal"] = "Terminal", ["testbench"] = "Bancada de testes", ["mergestation"] = "Sala de merge",
		};

		public const int Room = 14;   // tiles per office, including one wall
		public const int Height = 8;  // floor to roof
		public static int Width => Order.Length * Room + 1;

		public static bool Built;
		public static Point Origin; // left edge and floor row of the city

		private static readonly Dictionary<string, int> Spawned = new(); // agentId -> npc index

		/// <summary>Tile x of the middle of a sector's office; "user" means next to the player.</summary>
		public static int StationX(string station, Player player)
		{
			if (station == "user") return (int)(player.Center.X / 16f) + 3;
			int i = Array.IndexOf(Order, station);
			if (i < 0) i = Array.IndexOf(Order, "lounge");
			return Built ? Origin.X + i * Room + Room / 2 : Main.spawnTileX + (i - 3) * 15;
		}

		/// <summary>Where an agent stands inside its office when others share it.</summary>
		public static int Slot(AgentInfo agent)
		{
			int n = Rank(agent);
			int side = n % 2 == 1 ? -1 : 1;
			int step = (n + 1) / 2;
			return side * Math.Min(step * 2, 5); // 0, -2, +2, -4, +4, -5, +5: up to 7 fit in one office
		}

		/// <summary>0 for the first agent in an office, 1 for the second... (by id, so it is stable).</summary>
		public static int Rank(AgentInfo agent)
		{
			int n = 0;
			foreach (var other in ForemanClient.Agents.Values)
				if (other.Active && other.Station == agent.Station && string.CompareOrdinal(other.Id, agent.Id) < 0) n++;
			return n;
		}

		public static int FloorY => Built ? Origin.Y : Main.spawnTileY;

		// ---- building ---------------------------------------------------------------------------

		/// <summary>Builds the offices centred on the given tile, floor on that row. Overwrites what is there.</summary>
		public static void Build(int centerX, int floorY)
		{
			int left = centerX - Width / 2;
			int top = floorY - Height;
			for (int x = left; x < left + Width; x++)
			{
				bool wall = (x - left) % Room == 0;
				for (int y = top; y <= floorY + 1; y++)
				{
					WorldGen.KillTile(x, y, noItem: true);
					WorldGen.KillWall(x, y);
					bool solid = y >= floorY || y == top || (wall && y < floorY - 3); // doorways are 3 tiles tall
					if (solid) WorldGen.PlaceTile(x, y, TileID.WoodBlock, mute: true, forced: true);
					if (y > top && y < floorY) WorldGen.PlaceWall(x, y, WallID.Wood, mute: true);
				}
			}
			for (int i = 0; i < Order.Length; i++)
			{
				int x0 = left + i * Room;
				WorldGen.PlaceTile(x0 + 2, top + 2, TileID.Torches, mute: true);
				WorldGen.PlaceTile(x0 + Room - 2, top + 2, TileID.Torches, mute: true);
				Furnish(Order[i], x0, floorY - 1);
			}
			WorldGen.RangeFrame(left - 1, top - 1, left + Width + 1, floorY + 2);
			Origin = new Point(left, floorY);
			Built = true;
			// everyone moves in: respawned inside their offices on the next tick
			foreach (var idx in Spawned.Values) Main.npc[idx].active = false;
			Spawned.Clear();
		}

		private static void Furnish(string station, int x0, int y)
		{
			void Put(int dx, int type) => WorldGen.PlaceObject(x0 + dx, y, type, mute: true);
			switch (station)
			{
				case "meeting": Put(5, TileID.Chairs); Put(7, TileID.Tables); Put(9, TileID.Chairs); break;
				case "library": Put(3, TileID.Bookcases); Put(10, TileID.Bookcases); break;
				case "desk": Put(3, TileID.Tables); Put(5, TileID.Chairs); Put(9, TileID.Tables); Put(11, TileID.Chairs); break;
				case "lounge": Put(4, TileID.Beds); Put(10, TileID.Chairs); break;
				case "terminal": Put(4, TileID.WorkBenches); Put(9, TileID.WorkBenches); break;
				case "testbench": Put(4, TileID.Anvils); Put(9, TileID.WorkBenches); break;
				case "mergestation": Put(6, TileID.Tables); Put(8, TileID.Chairs); break;
			}
		}

		public override void SaveWorldData(TagCompound tag)
		{
			if (!Built) return;
			tag["ox"] = Origin.X;
			tag["oy"] = Origin.Y;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Built = tag.ContainsKey("ox");
			if (Built) Origin = new Point(tag.GetInt("ox"), tag.GetInt("oy"));
		}

		public override void OnWorldUnload()
		{
			Spawned.Clear();
			Built = false;
		}

		/// <summary>Office names over the roofs.</summary>
		public override void PostDrawTiles()
		{
			if (!Built) return;
			var sb = Main.spriteBatch;
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			for (int i = 0; i < Order.Length; i++)
			{
				var pos = new Vector2((Origin.X + i * Room + Room / 2f) * 16f, (Origin.Y - Height - 1) * 16f) - Main.screenPosition;
				Utils.DrawBorderString(sb, Names[Order[i]], pos, Color.White, 1f, 0.5f, 1f);
			}
			sb.End();
		}

		// ---- agents -----------------------------------------------------------------------------

		public override void PostUpdateWorld()
		{
			if (Main.netMode != NetmodeID.SinglePlayer) return; // MVP: single player only
			foreach (var a in ForemanClient.Agents.Values)
			{
				bool alive = Spawned.TryGetValue(a.Id, out var idx) && Main.npc[idx].active && Main.npc[idx].ModNPC is AgentNPC n && n.AgentId == a.Id;
				if (a.Active && !alive)
				{
					int x = StationX(a.Station, Main.LocalPlayer) * 16;
					int y = (FloorY - 3) * 16;
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
