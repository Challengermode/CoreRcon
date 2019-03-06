using System.Text.RegularExpressions;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Csgo
{
    public class TeamSide : IParseable
    {
        public string Team { get; set; }
        public string CurentSide { get; set; }
    }

    public class TeamSideParser : DefaultParser<TeamSide>
    {
        public override string Pattern { get; } = @"Team playing ""(?<side>.+?)"": (?<team>.*)";

        public override TeamSide Load(GroupCollection groups)
        {
            return new TeamSide
            {
                Team = groups["team"].Value,
                CurentSide = groups["side"].Value
            };
        }
    }
}