using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
	public class Player : IParseable
	{
		public int ClientId { get; set; }
		public string Name { get; set; }
		public string SteamId { get; set; }
		public string Team { get; set; }
	}

	public class PlayerParser : DefaultParser<Player>
	{
		public override string Pattern { get; } = "\"(?<Name>.+?(?:<.*>)*)<(?<ClientID>\\d+?)><(?<SteamID>.+?)><(?<Team>.+?)?>\"";

		public override Player Load(GroupCollection groups)
		{
			return new Player
			{
				Name = groups["Name"].Value,
				SteamId = groups["SteamID"].Value,
				ClientId = int.Parse(groups["ClientID"].Value),
				Team = groups["Team"].Value
			};
		}
	}
}