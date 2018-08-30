using CoreRCON.Parsers.Standard;
using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Csgo
{
    public class Frag : IParseable
    {
        public Player Killed { get; set; }
        public Player Killer { get; set; }
        public bool Headshot { get; set; }
        public string Weapon { get; set; }
    }

    public class FragParser : DefaultParser<Frag>
    {
        //Todo parse position (square bracket contetnt)
        public override string Pattern { get; } = $"(?<Killer>{playerParser.Pattern}) \\[.*?\\] killed (?<Killed>{playerParser.Pattern}) \\[.*?\\] with \"(?<Weapon>.+?)\"\\s?(?<Headshot>\\(headshot\\))?";
        private static PlayerParser playerParser { get; } = new PlayerParser();

        public override Frag Load(GroupCollection groups)
        {
            return new Frag
            {
                Killer = playerParser.Parse(groups["Killer"]),
                Killed = playerParser.Parse(groups["Killed"]),
                Headshot = groups["Headshot"].Success,
                Weapon = groups["Weapon"].Value
            };
        }
    }
}