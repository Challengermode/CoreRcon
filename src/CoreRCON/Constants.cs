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
        /// Size of a rcon packet header
        /// </summary>
        internal const int HEADER_SIZE = 10;
    }
}
