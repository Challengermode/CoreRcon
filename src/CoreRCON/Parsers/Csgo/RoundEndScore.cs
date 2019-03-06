using System.Text.RegularExpressions;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Csgo
{
    public class RoundEndScore : IParseable
    {
        public string WinningTeam { get; set; }
        public int TScore { get; set; }
        public int CTScore { get; set; }
    }

    public class RoundEndScoreParser : DefaultParser<RoundEndScore>
    {
        public override string Pattern { get; } = @"Team ""(?<winning_team>.+?)"" triggered ""SFUI_Notice_.+?_Win"" \(CT ""(?<ct_score>\d+)""\) \(T ""(?<t_score>\d+)""\)";

        public override RoundEndScore Load(GroupCollection groups)
        {
            return new RoundEndScore
            {
                WinningTeam = groups["winning_team"].Value,
                TScore = int.Parse(groups["t_score"].Value),
                CTScore = int.Parse(groups["ct_score"].Value),
            };
        }
    }
}