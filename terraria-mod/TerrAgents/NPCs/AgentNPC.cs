using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrAgents.Systems;

namespace TerrAgents.NPCs
{
	/// <summary>
	/// An agent of the team. It only shows what the Core says: walks to the station of what it is
	/// doing and shows its name and activity overhead. Borrows the game's own Guide sprite (read from
	/// the player's Terraria at runtime; nothing from the game is shipped in this mod).
	/// </summary>
	public class AgentNPC : ModNPC
	{
		public string AgentId = "";

		public override string Texture => "Terraria/Images/NPC_" + NPCID.Guide;

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Guide];
			NPCID.Sets.ActsLikeTownNPC[Type] = true;
		}

		public override void SetDefaults()
		{
			NPC.width = 18;
			NPC.height = 40;
			NPC.aiStyle = -1;
			NPC.friendly = true;
			NPC.dontTakeDamage = true;
			NPC.damage = 0;
			NPC.defense = 15;
			NPC.lifeMax = 250;
			NPC.knockBackResist = 0f;
			AnimationType = NPCID.Guide;
		}

		public override bool CheckActive() => false; // never despawns on its own; CitySystem removes it

		public override void AI()
		{
			NPC.timeLeft = NPC.activeTime;
			if (!ForemanClient.Agents.TryGetValue(AgentId, out var a)) return;
			NPC.GivenName = a.Name;
			float targetX = CitySystem.StationX(a.Station, Main.LocalPlayer) * 16f;
			float dx = targetX - NPC.Center.X;
			bool moving = !a.Paused && System.Math.Abs(dx) > 24f;
			float speed = a.State is "running" or "testing" ? 2.4f : 1.4f;
			NPC.velocity.X = moving ? System.Math.Sign(dx) * speed : NPC.velocity.X * 0.8f;
			if (moving) NPC.direction = NPC.spriteDirection = System.Math.Sign(dx);
			// hop over one-tile steps
			if (moving && NPC.collideX && NPC.velocity.Y == 0f) NPC.velocity.Y = -6f;
		}

		public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (!ForemanClient.Agents.TryGetValue(AgentId, out var a)) return;
			var top = NPC.Top - screenPos;
			Utils.DrawBorderString(spriteBatch, a.Name, top + new Vector2(0f, -38f), a.Color, 0.9f, 0.5f, 0f);
			var line = a.Paused ? "pausado" : a.Activity;
			if (!string.IsNullOrEmpty(line))
				Utils.DrawBorderString(spriteBatch, line, top + new Vector2(0f, -20f), StateColor(a.State), 0.7f, 0.5f, 0f);
		}

		private static Color StateColor(string state) => state switch
		{
			"editing" => Color.LightGreen,
			"running" or "testing" => Color.Gold,
			"waiting_user" => Color.Orange,
			"blocked" or "error" => Color.OrangeRed,
			"thinking" or "reading" => Color.LightSkyBlue,
			_ => Color.LightGray,
		};
	}
}
