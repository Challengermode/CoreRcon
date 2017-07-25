using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CoreRCON.PacketFormats
{
    public class MinecraftQueryInfo : IQueryInfo
    {
        private static Dictionary<string, string> _keyValues;
        private static List<string> _players;

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

        //public static MinecraftQueryInfo FromBytes(byte[] message)
        //{
        //    var keyValues = new Dictionary<string, string>();
        //    var players = new List<string>();

        //    var buffer = new byte[256];
        //    Stream stream = new MemoryStream(message);

        //    stream.Read(buffer, 0, 5);// Read Type + SessionID
        //    stream.Read(buffer, 0, 11); // Padding: 11 bytes constant
        //    var constant1 = new byte[] { 0x73, 0x70, 0x6C, 0x69, 0x74, 0x6E, 0x75, 0x6D, 0x00, 0x80, 0x00 };
        //    for (int i = 0; i < constant1.Length; i++) Debug.Assert(constant1[i] == buffer[i], "Byte mismatch at " + i + " Val :" + buffer[i]);

        //    var sb = new StringBuilder();
        //    string lastKey = string.Empty;
        //    int currentByte;
        //    while ((currentByte = stream.ReadByte()) != -1)
        //    {
        //        if (currentByte == 0x00)
        //        {
        //            if (!string.IsNullOrEmpty(lastKey))
        //            {
        //                _keyValues.Add(lastKey, sb.ToString());
        //                lastKey = string.Empty;
        //            }
        //            else
        //            {
        //                lastKey = sb.ToString();
        //                if (string.IsNullOrEmpty(lastKey)) break;
        //            }
        //            sb.Clear();
        //        }
        //        else sb.Append((char)currentByte);
        //    }

        //    stream.Read(buffer, 0, 10); // Padding: 10 bytes constant
        //    var constant2 = new byte[] { 0x01, 0x70, 0x6C, 0x61, 0x79, 0x65, 0x72, 0x5F, 0x00, 0x00 };
        //    for (int i = 0; i < constant2.Length; i++) Debug.Assert(constant2[i] == buffer[i], "Byte mismatch at " + i + " Val :" + buffer[i]);

        //    while ((currentByte = stream.ReadByte()) != -1)
        //    {
        //        if (currentByte == 0x00)
        //        {
        //            var player = sb.ToString();
        //            if (string.IsNullOrEmpty(player)) break;
        //            _players.Add(player);
        //            sb.Clear();
        //        }
        //        else sb.Append((char)currentByte);
        //    }

        //    return new MinecraftQueryInfo
        //    {
        //        MessageOfTheDay = keyValues["hostname"],
        //        Gametype = keyValues["gametype"],
        //        GameId = keyValues["game_id"],
        //        Version = keyValues["version"],
        //        Plugins = keyValues["plugins"],
        //        Map = keyValues["map"],
        //        NumPlayers = keyValues["numplayers"],
        //        MaxPlayers = keyValues["maxplayers"],
        //        HostPort = keyValues["hostport"],
        //        HostIp = keyValues["hostip"],
        //        Players = players
        //    };
        //}
    }
}
