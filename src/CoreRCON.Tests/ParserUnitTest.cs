using System;
using System.Collections.Generic;
using System.Text;
using CoreRCON.Parsers.Csgo;
using CoreRCON.Parsers.Standard;
using Xunit;

namespace CoreRCON.Tests
{
    public class ParserUnitTest
    {
        [Fact]
        public void testRoundEndParser()
        {
            string team = "TERRORIST";
            int tRounds = 0;
            int ctRounds = 16;
            string testString = $"12:00 junk: Team \"{team}\" triggered \"SFUI_Notice_Terrorists_Win\" (CT \"{ctRounds}\") (T \"{tRounds}\")";
            RoundEndScoreParser parser = new RoundEndScoreParser();
            Assert.True(parser.IsMatch(testString));
            RoundEndScore score = parser.Parse(testString);
            Assert.Equal(team, score.WinningTeam);
            Assert.Equal(ctRounds, score.CTScore);
            Assert.Equal(tRounds, score.TScore);
        }


        [Fact]
        public void testDisconnectParser()
        {
            string reason = "test123   (oj)";
            string withReason = $"12:00 junk: \"Xavier<2><BOT><TERRORIST>\" disconnected (reason \"{reason}\")";
            string noREason = $"12:00 junk: \"Xavier<2><BOT><TERRORIST>\" disconnected";
            PlayerDisconnectedParser parser = new PlayerDisconnectedParser();
            Assert.True(parser.IsMatch(withReason));
            Assert.True(parser.IsMatch(noREason));
            PlayerDisconnected disconnection = parser.Parse(withReason);
            Assert.Equal(reason, disconnection.Reason);
        }

        [Fact]
        public void testFrag()
        {
            string weapon = "usp_silencer";
            string headShot = $"L 13:37 spam: \"Prince<12><STEAM_1:1:123338101><CT>\" [2264 19 128] killed \"Bot<11><STEAM_1:0:123371565><TERRORIST>\" [1938 -198 320] with \"{weapon}\" (headshot)";
            string kill = $"L 13:37 spam: \"Prince<12><STEAM_1:1:123338101><CT>\" [2264 19 128] killed \"Bot<11><STEAM_1:0:123371565><TERRORIST>\" [1938 -198 320] with \"{weapon}\"";
            FragParser parser = new FragParser();
            Assert.True(parser.IsMatch(headShot));
            Assert.True(parser.IsMatch(kill));
            Frag hsFarg = parser.Parse(headShot);
            Frag frag = parser.Parse(kill);
            Assert.True(hsFarg.Headshot);
            Assert.False(frag.Headshot);
            Assert.Equal(frag.Weapon, weapon);
        }

        [Fact]
        public void testAssist()
        {
            string test = "L 08/29/2018 - 16:59:36: \"Bot<10><STEAM_1:0:12354210><CT>\" assisted killing \"Bot<11><STEAM_1:0:123371565><TERRORIST>\"";
            FragAssistParser parser = new FragAssistParser();
            Assert.True(parser.IsMatch(test));
        }

        [Fact]
        public void testGameOver()
        {
            int ct_score = 1;
            int t_score = 16;
            string test = $"Game Over: competitive mg_active de_cache score {ct_score}:{t_score} after 23 min";
            GameOverScoreParser parser = new GameOverScoreParser();
            Assert.True(parser.IsMatch(test));
            GameOverScore score = parser.Parse(test);
            Assert.Equal(ct_score, score.CTScore);
            Assert.Equal(t_score, score.TScore);
        }

        [Fact]
        public void testTeamSide()
        {
            string team = "Gamma Squad";
            string side = "CT";
            string test = $"Team playing \"{side}\": {team}";
            TeamSideParser parser = new TeamSideParser();
            Assert.True(parser.IsMatch(test));
            TeamSide data = parser.Parse(test);
            Assert.Equal(team, data.Team);
            Assert.Equal(side, data.CurentSide);
        }

        [Fact]
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
            Assert.True(parser.IsMatch(test));
            DamageEvent e = parser.Parse(test);
            Assert.Equal(dmg, e.Damage);
            Assert.Equal(dmg_armor, e.ArmorDamage);
            Assert.Equal(health, e.PostHealth);
            Assert.Equal(armor, e.PostArmor);
            Assert.Equal(hitgroup, e.HitLocation);
        }

        [Fact]
        public void testL4DStatus()
        {
            string test = "hostname: l4d hostname\n" +
                "version: 2019.11.12 / 24 7671 secure\n" +
                "udp / ip  : (127.0.0.1:1234)  (public ip: 1.1.1.1)\n" +
                "map     : deathrun_portal at: 0 x, 0 y, 0 z\n" +
                "players : 5 (24 max)\n" +
                "# userid name                uniqueid            connected ping loss state  adr\n" +
                "id id id";
            StatusParser parser = new StatusParser();
            Assert.True(parser.IsMatch(test));
            Status status = parser.Parse(test);
            Assert.False(status.Hibernating);
            Assert.Equal("1.1.1.1", status.PublicHost);
            Assert.Equal("127.0.0.1:1234", status.LocalHost);
            Assert.Equal("l4d hostname", status.Hostname);
            Assert.Equal("deathrun_portal at: 0 x, 0 y, 0 z", status.Map);
            Assert.Equal(5, status.Humans);
            Assert.Equal(24, status.MaxPlayers);
        }

        [Fact]
        public void testCsgoStatus()
        {
            string test = "hostname: long hostname\n" +
                "version: 1.37.4.4 / 13744 1081 / 7776 secure[G: 1:3091169]\n" +
                "udp / ip  : 127.0.0.1:27711(public ip: 1.1.1.1)\n" +
                "os      :  Linux\n" +
                "type    :  community dedicated\n" +
                "map     : de_dust2\n" +
                "gotv[0]:  port 27712, delay 90.0s, rate 32.0\n" +
                "players : 2 humans, 1 bots(6/0 max) (not hibernating)\n\n" +
                "# userid name uniqueid connected ping loss state rate adr\n" +
                "# 2 \"GOTV\" BOT active 32\n";
            StatusParser parser = new StatusParser();
            Assert.True(parser.IsMatch(test));
            Status status = parser.Parse(test);
            Assert.False(status.Hibernating);
            Assert.Equal("1.1.1.1", status.PublicHost);
            Assert.Equal("127.0.0.1:27711", status.LocalHost);
            Assert.Equal("long hostname", status.Hostname);
            Assert.Equal("de_dust2", status.Map);
            Assert.Equal(2, status.Humans);
            Assert.Equal(6, status.MaxPlayers);
            Assert.Equal(1, status.Bots);

        }

        [Fact]
        public void testHibernatingStatus()
        {
            string test = "Server hibernating";
            StatusParser parser = new StatusParser();
            Assert.True(parser.IsMatch(test));
            Status status = parser.Parse(test);
            Assert.True(status.Hibernating);
        }
    }
}

