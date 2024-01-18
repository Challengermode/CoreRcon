namespace CoreRCON
{
    // SERVERDATA_AUTH_RESPONSE and SERVERDATA_EXECCOMMAND are both 2
    public enum  PacketType
    {
        // SERVERDATA_RESPONSE_VALUE
        Response = 0,

        // SERVERDATA_AUTH_RESPONSE
        AuthResponse = 2,

#pragma warning disable CA1069
        // SERVERDATA_EXECCOMMAND
        ExecCommand = 2,
#pragma warning restore CA1069

        // SERVERDATA_AUTH
        Auth = 3
    }
}
