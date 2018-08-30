using CoreRCON.Parsers.Standard;
using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Csgo
{
    public class FragAssist : IParseable
    {
        public Player Killed { get; set; }
        public Player Assister { get; set; }
    }

    public class FragAssistParser : DefaultParser<FragAssist>
    {
        public override string Pattern { get; } = $"(?<Assister>{playerParser.Pattern}) assisted killing (?<Killed>{playerParser.Pattern})?";
        private static PlayerParser playerParser { get; } = new PlayerParser();

        public override FragAssist Load(GroupCollection groups)
        {
            return new FragAssist
            {
                Assister = playerParser.Parse(groups["Assister"]),
                Killed = playerParser.Parse(groups["Killed"]),
            };
        }
    }
}