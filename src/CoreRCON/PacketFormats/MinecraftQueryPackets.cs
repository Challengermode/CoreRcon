using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CoreRCON.PacketFormats
{
    public class MinecraftQueryInfo : IQueryInfo
    {
        public string MessageOfTheDay { get; private set; }
        public string Gametype { get; private set; }
        public string GameId { get; private set; }
        public string Version { get; private set; }
        public string Plugins { get; private set; }
        public string Map { get; private set; }
        public string NumPlayers { get; private set; }
        public string MaxPlayers { get; private set; }
        public string HostPort { get; private set; }
        public string HostIp { get; private set; }

        public IEnumerable<string> Players { get; private set; }

        public static MinecraftQueryInfo FromBytes(byte[] buffer)
        {
            int i = 16; // 1x type, 4x session, 11x padding
            var serverinfo = buffer.ReadNullTerminatedStringDictionary(i, ref i);
            i += 10;
            var players = buffer.ReadNullTerminatedStringArray(i, ref i);

            return new MinecraftQueryInfo
            {
                MessageOfTheDay = serverinfo["hostname"],
                Gametype = serverinfo["gametype"],
                GameId = serverinfo["game_id"],
                Version = serverinfo["version"],
                Plugins = serverinfo["plugins"],
                Map = serverinfo["map"],
                NumPlayers = serverinfo["numplayers"],
                MaxPlayers = serverinfo["maxplayers"],
                HostPort = serverinfo["hostport"],
                HostIp = serverinfo["hostip"],
                Players = players
            };
        }
    }
}
