using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
	public class TeamChange
	{
		public Player Player { get; set; }
		public string Team { get; set; }
	}

	public class TeamChangeParser : DefaultParser<TeamChange>
	{
		private static PlayerParser playerParser { get; } = new PlayerParser();
		public override string Pattern { get; } = $"(?<Player>{playerParser.Pattern}) joined team \"(?<Team>.+?)\"";

		public override TeamChange Load(GroupCollection groups)
		{
			return new TeamChange
			{
				Player = playerParser.Parse(groups["Player"]),
				Team = groups["Team"].Value
			};
		}
	}
}
