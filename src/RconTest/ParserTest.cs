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

        [TestMethod]
        public void testFrag()
        {
            string weapon = "usp_silencer";
            string headShot = $"L 13:37 spam: \"Prince<12><STEAM_1:1:123338101><CT>\" [2264 19 128] killed \"Bot<11><STEAM_1:0:123371565><TERRORIST>\" [1938 -198 320] with \"{weapon}\" (headshot)";
            string kill = $"L 13:37 spam: \"Prince<12><STEAM_1:1:123338101><CT>\" [2264 19 128] killed \"Bot<11><STEAM_1:0:123371565><TERRORIST>\" [1938 -198 320] with \"{weapon}\"";
            FragParser parser = new FragParser();
            Assert.IsTrue(parser.IsMatch(headShot));
            Assert.IsTrue(parser.IsMatch(kill));
            Frag hsFarg = parser.Parse(headShot);
            Frag frag = parser.Parse(kill);
            Assert.IsTrue(hsFarg.Headshot);
            Assert.IsFalse(frag.Headshot);
            Assert.AreEqual(frag.Weapon, weapon);
        }

        [TestMethod]
        public void testAssist()
        {
            string test = "L 08/29/2018 - 16:59:36: \"Bot<10><STEAM_1:0:12354210><CT>\" assisted killing \"Bot<11><STEAM_1:0:123371565><TERRORIST>\"";
            FragAssistParser parser = new FragAssistParser();
            Assert.IsTrue(parser.IsMatch(test));
        }

        [TestMethod]
        public void testGameOver()
        {
            int ct_score = 1;
            int t_score = 16;
            string test = $"Game Over: competitive mg_active de_cache score {ct_score}:{t_score} after 23 min";
            GameOverScoreParser parser = new GameOverScoreParser();
            Assert.IsTrue(parser.IsMatch(test));
            GameOverScore score = parser.Parse(test);
            Assert.Equals(ct_score, score.CTScore);
            Assert.Equals(t_score, score.TScore);
        }

        [TestMethod]
        public void testTeamSide()
        {
            string team = "Gamma Squad";
            string side = "CT";
            string test = $"Team playing \"{side}\": {team}";
            TeamSideParser parser = new TeamSideParser();
            Assert.IsTrue(parser.IsMatch(test));
            TeamSide data = parser.Parse(test);
            Assert.Equals(team, data.Team);
            Assert.Equals(side, data.CurentSide);
        }

        [TestMethod]
        public void testDamageEvent()
        {
            int dmg = 110;
            int dmg_armor = 0;
            int health = 0;
            int armor = 80;
            string hitgroup = "head";
            string test = "L 08/29/2018 - 16:59:36: \"Prince<12><STEAM_1:1:177338101><CT>\" [2264 19 128] attacked \"Son.Gohan<11><STEAM_1:0:120371565><TERRORIST>\" " +
                "[1938 -198 256] with \"hkp2000\" " +
                $"(damage \"{dmg}\") " +
                $"(damage_armor \"{dmg_armor}\") " +
                $"(health \"{health}\") " +
                $"(armor \"{armor}\") " +
                $"(hitgroup \"{hitgroup}\")";

            DamageEventParser parser = new DamageEventParser();
            Assert.IsTrue(parser.IsMatch(test));
            DamageEvent e = parser.Parse(test);
            Assert.AreEqual(dmg, e.Damage);
            Assert.AreEqual(dmg_armor, e.ArmorDamage);
            Assert.AreEqual(health, e.PostHealth);
            Assert.AreEqual(armor, e.PostArmor);
            Assert.AreEqual(hitgroup, e.HitLocation);
        }
    }
}

