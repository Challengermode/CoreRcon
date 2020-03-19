using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
    public class Status : IParseable
    {
        [Obsolete("No longer part of status message")]
        public string Account { get; set; }
        public byte Bots { get; set; }
        public ulong CommunityID { get; set; }
        public string Hostname { get; set; }
        public byte Humans { get; set; }
        public string LocalHost { get; set; }
        public string Map { get; set; }
        public byte MaxPlayers { get; set; }
        public string PublicHost { get; set; }
        public string SteamID { get; set; }
        [Obsolete("No longer part of status message")]
        public string[] Tags { get; set; }
        public string Version { get; set; }
        public bool Hibernating { get; set; }
        public string Type { get; set; }
    }

    public class StatusParser : IParser<Status>
    {
        public string Pattern => throw new System.NotImplementedException();

        public bool IsMatch(string input)
        {
            return input.Contains("hostname: ") | input.Contains("hibernating");

        }

        public Status Load(GroupCollection groups)
        {
            throw new System.NotImplementedException();
        }

        public Status Parse(string input)
        {
            Dictionary<string, string> groups = input.Split('\n')
                .Select(x => x.Split(':'))
                .Where(x => x.Length > 1 && !string.IsNullOrEmpty(x[0].Trim())
                    && !string.IsNullOrEmpty(x[1].Trim()))
                .ToDictionary(x => x[0].Trim(), x => string.Join(":", x.ToList().Skip(1)).Trim());

            string hostname = null;
            groups.TryGetValue("hostname", out hostname);
            string version = null;
            groups.TryGetValue("version", out version);
            string steamId = null;
            if (version != null)
            {
                Match match = Regex.Match(version, ".*(\\[.*\\]).*");
                if (match.Success)
                {
                    steamId = match.Groups[1].Value;
                }
            }
            string map = null;
            groups.TryGetValue("map", out map);
            string type = null;
            groups.TryGetValue("type", out type);

            byte players = 0, bots = 0, maxPlayers = 0;
            string playerString = null;
            bool hibernating = false;
            groups.TryGetValue("players", out playerString);
            if (playerString != null)
            {
                Match oldMatch = Regex.Match(playerString, "(\\d+) \\((\\d+) max\\).*"); //Old pattern
                Match newMatch = Regex.Match(playerString, "(\\d+) humans, (\\d+) bots\\((\\d+)/\\d+ max\\) (\\(not hibernating\\))?.*");
                if (oldMatch.Success)
                {
                    players = byte.Parse(oldMatch.Groups[1].Value);
                    maxPlayers = byte.Parse(oldMatch.Groups[2].Value);
                    bots = 0;
                }
                else if (newMatch.Success)
                {
                    players = byte.Parse(newMatch.Groups[1].Value);
                    maxPlayers = byte.Parse(newMatch.Groups[3].Value);
                    bots = byte.Parse(newMatch.Groups[2].Value);
                    if (newMatch.Groups[4].Success)
                    {
                        hibernating = !newMatch.Groups[4].Value.Contains("not hibernating");
                    }
                }
            }
            else
            {
                hibernating = input.Contains("hibernating") && !input.Contains("not hibernating");
            }

            string localIp = null, publicIp = null, ipString = null;
            groups.TryGetValue("udp / ip", out ipString);
            if (ipString != null)
            {
                Match oldMatch = Regex.Match(ipString, "((\\d|\\.)+:(\\d|\\.)+)\\(public ip: (.*)\\).*"); //Old pattern
                Match newMatch = Regex.Match(ipString, "\\((.*:.*)\\)\\s+\\(public ip: (.*)\\).*");
                if (oldMatch.Success)
                {
                    localIp = oldMatch.Groups[1].Value;
                    publicIp = oldMatch.Groups[4].Value;
                }
                else if (newMatch.Success)
                {
                    localIp = newMatch.Groups[1].Value;
                    publicIp = newMatch.Groups[2].Value;
                }
            }

            return new Status()
            {
                Hostname = hostname,
                Version = version,
                SteamID = steamId,
                Map = map,
                Type = type,
                Humans = players,
                MaxPlayers = maxPlayers,
                Bots = bots,
                Hibernating = hibernating,
                LocalHost = localIp,
                PublicHost = publicIp
            };
        }

        public Status Parse(Group group)
        {
            throw new System.NotImplementedException();
        }
    }
}