using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
	public class NameChange : IParseable
	{
		public Player Player { get; set; }
		public string NewName { get; set; }
	}

	public class NameChangeParser : DefaultParser<NameChange>
	{
		private static PlayerParser playerParser { get; } = new PlayerParser();
		public override string Pattern { get; } = $"(?<Player>{playerParser.Pattern}) changed name to \"(?<Name>.+?)\"$";

		public override NameChange Load(GroupCollection groups)
		{
			return new NameChange
			{
				Player = playerParser.Parse(groups["Player"]),
				NewName = groups["Name"].Value
			};
		}
	}
}
