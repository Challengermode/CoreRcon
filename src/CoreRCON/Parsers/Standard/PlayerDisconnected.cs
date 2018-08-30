using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
    public class PlayerDisconnected : IParseable
    {
        public string Reason { get; set; }
        public Player Player { get; set; }
    }

    public class PlayerDisconnectedParser : DefaultParser<PlayerDisconnected>
    {
        public override string Pattern { get; } = $"(?<Player>{playerParser.Pattern}) disconnected\\s?(\\(reason \"(?<Reason>.*)\"\\))?";
        private static PlayerParser playerParser { get; } = new PlayerParser();

        public override PlayerDisconnected Load(GroupCollection groups)
        {
            return new PlayerDisconnected
            {
                Player = playerParser.Parse(groups["Player"]),
                Reason = groups["Reason"].Value
            };
        }
    }
}