using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
	public class PlayerDisconnected
	{
		public Player Player { get; set; }
	}

	public class PlayerDisconnectedParser : DefaultParser<PlayerDisconnected>
	{
		private static PlayerParser playerParser { get; } = new PlayerParser();
		public override string Pattern { get; } = $"(?<Player>{playerParser.Pattern}) disconnected";

		public override PlayerDisconnected Load(GroupCollection groups)
		{
			return new PlayerDisconnected
			{
				Player = playerParser.Parse(groups["Player"])
			};
		}
	}
}
