﻿using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
	public class PlayerDisconnected : IParseable
	{
		public Player Player { get; set; }
	}

	public class PlayerDisconnectedParser : DefaultParser<PlayerDisconnected>
	{
		public override string Pattern { get; } = $"(?<Player>{playerParser.Pattern}) disconnected";
		private static PlayerParser playerParser { get; } = new PlayerParser();

		public override PlayerDisconnected Load(GroupCollection groups)
		{
			return new PlayerDisconnected
			{
				Player = playerParser.Parse(groups["Player"])
			};
		}
	}
}