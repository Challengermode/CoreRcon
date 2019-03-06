using System.Text.RegularExpressions;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Csgo
{
    public class GameOverScore : IParseable
    {
        public int TScore { get; set; }
        public int CTScore { get; set; }
    }

    public class GameOverScoreParser : DefaultParser<GameOverScore>
    {
        public override string Pattern { get; } = @"Game Over: .*? .*? .*? score (?<ct_score>\d+):(?<t_score>\d+) (after \d+ min)?";

        public override GameOverScore Load(GroupCollection groups)
        {
            return new GameOverScore
            {
                TScore = int.Parse(groups["t_score"].Value),
                CTScore = int.Parse(groups["ct_score"].Value),
            };
        }
    }
}