namespace CoreRCON
{
    internal class Constants
    {
        /// <summary>
        /// The maximum size of an RCON packet.
        /// (Officially 4096, larger packet where received when running cvarlist)
        /// </summary>
        internal const int MAX_PACKET_SIZE = 4096;

        /// <summary>
        /// Minimum size of a rcon packet
        /// </summary>
        internal const int MIN_PACKET_SIZE = 14;
        
        
        /// <summary>
        /// The size of the header of an RCON packet.
        /// </summary>
        internal const int PACKET_HEADER_SIZE = 12;

        /// <summary>
        /// The size of the header of an RCON packet.
        /// </summary>
        internal const int PACKET_PADDING_SIZE = 2;

        /// <summary>
        /// Special response value when you send a Response.Response to the server.
        /// Used to finde the end of a multi packet response.
        /// </summary>
        internal const string MULTI_PACKET_END_RESPONSE = "\0\u0001\0\0";
        
    }
}
