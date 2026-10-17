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
	/// The city: a guild hall with one office per sector (station), side by side, shared by every agent
	/// working there. Stone-brick foundation and pillars, timber-framed plank walls with glass windows,
	/// a red-shingle gable roof over each office, arches between offices, chandeliers and themed furniture.
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

		public const int Room = 18;    // tiles per office, including one pillar
		public const int Pillar = 2;   // stone pillar width between offices
		public const int Height = 10;  // floor to ceiling beam
		public const int Roof = 9;     // gable height above the ceiling beam
		public static int Width => Order.Length * Room + Pillar;

		public static bool Built;
		public static Point Origin; // left edge and floor row of the city

		private static readonly Dictionary<string, int> Spawned = new(); // agentId -> npc index

		/// <summary>Tile x of the middle of a sector's office; "user" means next to the player.</summary>
		public static int StationX(string station, Player player)
		{
			if (station == "user") return (int)(player.Center.X / 16f) + 3;
			int i = Array.IndexOf(Order, station);
			if (i < 0) i = Array.IndexOf(Order, "lounge");
			return Built ? Origin.X + i * Room + Pillar + (Room - Pillar) / 2 : Main.spawnTileX + (i - 3) * 15;
		}

		/// <summary>Where an agent stands inside its office when others share it.</summary>
		public static int Slot(AgentInfo agent)
		{
			int n = Rank(agent);
			int side = n % 2 == 1 ? -1 : 1;
			int step = (n + 1) / 2;
			return side * Math.Min(step * 2, 6); // 0, -2, +2, -4, +4, -6, +6: up to 7 fit in one office
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

		private static void Solid(int x, int y, int type)
		{
			WorldGen.KillTile(x, y, noItem: true);
			WorldGen.PlaceTile(x, y, type, mute: true, forced: true);
		}

		private static void Back(int x, int y, int wall)
		{
			WorldGen.KillWall(x, y);
			WorldGen.PlaceWall(x, y, wall, mute: true);
		}

		/// <summary>Builds the hall centred on the given tile, floor on that row. Overwrites what is there.</summary>
		public static void Build(int centerX, int floorY)
		{
			int left = centerX - Width / 2;
			int ceiling = floorY - Height;          // the beam row
			int top = ceiling - Roof - 1;

			// clear the whole area (and a margin) so nothing of the old terrain or an older hall remains
			for (int x = left - 3; x < left + Width + 3; x++)
				for (int y = top - 2; y <= floorY + 3; y++)
				{
					WorldGen.KillTile(x, y, noItem: true);
					WorldGen.KillWall(x, y);
				}

			// foundation: three rows of stone brick, one tile wider on each side, with steps up
			for (int x = left - 2; x < left + Width + 2; x++)
				for (int y = floorY; y <= floorY + 2; y++) Solid(x, y, TileID.GrayBrick);
			Solid(left - 3, floorY + 1, TileID.GrayBrick);
			Solid(left + Width + 2, floorY + 1, TileID.GrayBrick);

			for (int i = 0; i < Order.Length; i++)
			{
				int x0 = left + i * Room;               // this office's left pillar
				int inner0 = x0 + Pillar, inner1 = x0 + Room - 1;
				BuildOffice(Order[i], x0, inner0, inner1, floorY, ceiling);
			}
			// closing pillar on the right
			BuildPillar(left + Width - Pillar, floorY, ceiling, arch: true);

			// wooden floor planks over the foundation, inside the hall
			for (int x = left; x < left + Width; x++) Solid(x, floorY, TileID.WoodBlock);

			WorldGen.RangeFrame(left - 4, top - 3, left + Width + 4, floorY + 4);
			Origin = new Point(left, floorY);
			Built = true;
			// everyone moves in: respawned inside their offices on the next tick
			foreach (var idx in Spawned.Values) Main.npc[idx].active = false;
			Spawned.Clear();
		}

		private static void BuildPillar(int x0, int floorY, int ceiling, bool arch)
		{
			for (int x = x0; x < x0 + Pillar; x++)
				for (int y = ceiling; y < floorY; y++)
				{
					bool doorway = arch && y >= floorY - 4; // 4-tile arch so agents (and you) walk through
					if (!doorway) Solid(x, y, TileID.GrayBrick);
					else Back(x, y, WallID.GrayBrick);
				}
			// rounded arch top
			Solid(x0, floorY - 5, TileID.GrayBrick);
			Solid(x0 + Pillar - 1, floorY - 5, TileID.GrayBrick);
		}

		private static void BuildOffice(string station, int x0, int inner0, int inner1, int floorY, int ceiling)
		{
			BuildPillar(x0, floorY, ceiling, arch: true);

			// back wall: planks below, wood above a trim line, glass windows framed in wood
			int trim = floorY - 4;
			for (int x = inner0; x <= inner1; x++)
				for (int y = ceiling + 1; y < floorY; y++)
					Back(x, y, y > trim ? WallID.Planked : WallID.Wood);
			int mid = (inner0 + inner1) / 2;
			foreach (int wx in new[] { inner0 + 2, inner1 - 4 })
				for (int x = wx; x < wx + 3; x++)
					for (int y = ceiling + 2; y <= ceiling + 4; y++)
						Back(x, y, WallID.Glass);

			// ceiling beam of wood with a stone lintel over the pillar
			for (int x = x0; x <= inner1; x++) Solid(x, ceiling, x < inner0 ? TileID.GrayBrick : TileID.WoodBlock);

			// gable roof of red shingles, attic filled with wood wall behind it
			int roofL = x0 - 1, roofR = inner1 + 1;
			for (int step = 0; step <= Roof; step++)
			{
				int y = ceiling - 1 - step;
				int a = roofL + step, b = roofR - step;
				if (a > b) break;
				Solid(a, y, TileID.RedDynastyShingles);
				Solid(b, y, TileID.RedDynastyShingles);
				if (a + 1 < b) Solid(a + 1, y, TileID.RedDynastyShingles);
				if (b - 1 > a) Solid(b - 1, y, TileID.RedDynastyShingles);
				for (int x = a + 1; x < b; x++) Back(x, y, WallID.Wood);
			}

			// light: a chandelier in the middle, lanterns by the windows
			WorldGen.PlaceObject(mid, ceiling + 1, TileID.Chandeliers, mute: true);
			WorldGen.PlaceObject(inner0 + 1, ceiling + 1, TileID.HangingLanterns, mute: true);
			WorldGen.PlaceObject(inner1 - 1, ceiling + 1, TileID.HangingLanterns, mute: true);

			Furnish(station, inner0, inner1, floorY - 1);
		}

		private static void Furnish(string station, int inner0, int inner1, int y)
		{
			void Put(int x, int type, int style = 0) => WorldGen.PlaceObject(x, y, type, mute: true, style: style);
			int mid = (inner0 + inner1) / 2;
			switch (station)
			{
				case "meeting":
					Put(mid - 5, TileID.Chairs); Put(mid - 3, TileID.Tables); Put(mid, TileID.Tables); Put(mid + 3, TileID.Tables); Put(mid + 5, TileID.Chairs);
					Put(inner1 - 1, TileID.GrandfatherClocks);
					break;
				case "library":
					Put(inner0 + 1, TileID.Bookcases); Put(inner0 + 5, TileID.Bookcases); Put(inner1 - 6, TileID.Bookcases); Put(inner1 - 2, TileID.Bookcases);
					Put(mid, TileID.Chairs);
					break;
				case "desk":
					Put(inner0 + 2, TileID.Tables); Put(inner0 + 5, TileID.Chairs); Put(inner1 - 5, TileID.Tables); Put(inner1 - 2, TileID.Chairs);
					Put(mid, TileID.Bookcases);
					break;
				case "lounge":
					Put(inner0 + 2, TileID.Beds); Put(mid, TileID.Pianos); Put(inner1 - 3, TileID.Benches);
					break;
				case "terminal":
					Put(inner0 + 2, TileID.WorkBenches); Put(inner0 + 5, TileID.Chairs); Put(inner1 - 5, TileID.WorkBenches); Put(inner1 - 2, TileID.Chairs);
					break;
				case "testbench":
					Put(inner0 + 2, TileID.Anvils); Put(mid - 1, TileID.WorkBenches); Put(inner1 - 3, TileID.Anvils);
					break;
				case "mergestation":
					Put(inner0 + 2, TileID.GrandfatherClocks); Put(mid - 1, TileID.Tables); Put(mid + 2, TileID.Chairs); Put(inner1 - 2, TileID.Bookcases);
					break;
			}
		}

		public override void SaveWorldData(TagCompound tag)
		{
			if (!Built) return;
			tag["ox"] = Origin.X;
			tag["oy"] = Origin.Y;
			tag["room"] = Room;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			// a hall built by an older version (other room size) is ignored: build again with /ta construir
			Built = tag.ContainsKey("ox") && tag.ContainsKey("room") && tag.GetInt("room") == Room;
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
				float cx = Origin.X + i * Room + Pillar + (Room - Pillar) / 2f;
				var pos = new Vector2(cx * 16f, (Origin.Y - Height - Roof - 3) * 16f) - Main.screenPosition;
				Utils.DrawBorderString(sb, Names[Order[i]], pos, Color.White, 1.1f, 0.5f, 1f);
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
