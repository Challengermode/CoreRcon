using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using CoreRCON.Parsers.Standard;
using CoreRCON.Parsers.Csgo;

namespace CoreRCON.Tests
{
    [TestClass]
    public class ParserTest
    {
        [TestMethod]
        public void testRoundEndParser()
        {
            string team = "TERRORIST";
            int tRounds = 0;
            int ctRounds = 16;
            string testString = $"12:00 junk: Team \"{team}\" triggered \"SFUI_Notice_Terrorists_Win\" (CT \"{ctRounds}\") (T \"{tRounds}\")";
            RoundEndScoreParser parser = new RoundEndScoreParser();
            Assert.IsTrue(parser.IsMatch(testString));
            RoundEndScore score = parser.Parse(testString);
            Assert.AreEqual(team, score.WinningTeam);
            Assert.AreEqual(ctRounds, score.CTScore);
            Assert.AreEqual(tRounds, score.TScore);
        }


        [TestMethod]
        public void testDisconnectParser()
        {
            string reason = "test123   (oj)";
            string withReason = $"12:00 junk: \"Xavier<2><BOT><TERRORIST>\" disconnected (reason \"{reason}\")";
            string noREason = $"12:00 junk: \"Xavier<2><BOT><TERRORIST>\" disconnected";
            PlayerDisconnectedParser parser = new PlayerDisconnectedParser();
            Assert.IsTrue(parser.IsMatch(withReason));
            Assert.IsTrue(parser.IsMatch(noREason));
            PlayerDisconnected disconnection = parser.Parse(withReason);
            Assert.AreEqual(reason, disconnection.Reason);
        }
    }
}

