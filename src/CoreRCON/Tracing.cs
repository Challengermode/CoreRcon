using System;
using System.Diagnostics;

namespace CoreRCON
{
    /// <summary>
    /// 
    internal static class Tracing
    {
        private static readonly Version AssemblyVersion = typeof(RCON).Assembly.GetName().Version;
        private static string LibraryVersion => $"{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}";

        public static readonly ActivitySource ActivitySource = new("CoreRcon.Rcon", LibraryVersion);


        public static class Tags
        {
            // Same Semantic Conventions for HTTP Spans 
            public const string Address = "host.address";
            public const string Port = "host.port";

            // RCON Specific tags
            public const string PacketId = "rcon.packet_id";
            public const string CommandLength = "rcon.command.length";
            public const string CommandCount = "rcon.command.count";
            public const string CommandFirst = "rcon.command.first";
            public const string ResponseLength = "rcon.response.length";
        }
    }
}
