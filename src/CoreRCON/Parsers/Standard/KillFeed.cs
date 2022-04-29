using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
    public class KillFeed : IParseable
    {
        public Player Killed { get; set; }
        public Player Killer { get; set; }
        public string Weapon { get; set; }
    }

    public class KillFeedParser : DefaultParser<KillFeed>
    {
        public override string Pattern { get; } = $"(?<Killer>{playerParser.Pattern}) killed (?<Killed>{playerParser.Pattern}) with \"(?<Weapon>.+?)\"";
        private static PlayerParser playerParser { get; } = new PlayerParser();

        public override KillFeed Load(GroupCollection groups)
        {
            return new KillFeed
            {
                Killer = playerParser.Parse(groups["Killer"]),
                Killed = playerParser.Parse(groups["Killed"]),
                Weapon = groups["Weapon"].Value
            };
        }
    }
}
