using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
	public class NameChange : IParseable
	{
		public string NewName { get; set; }
		public Player Player { get; set; }
	}

	public class NameChangeParser : DefaultParser<NameChange>
	{
		public override string Pattern { get; } = $"(?<Player>{playerParser.Pattern}) changed name to \"(?<Name>.+?)\"$";
		private static PlayerParser playerParser { get; } = new PlayerParser();

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