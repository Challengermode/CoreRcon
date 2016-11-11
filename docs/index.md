# CoreRCON
CoreRCON is an implementation of Valve's [Source RCON Protocol](https://developer.valvesoftware.com/wiki/Source_RCON_Protocol) on .NET Core.  It currently supports connecting to a server, sending commands and receiving their output, and receiving logs from SRCDS' `logaddress` functionality.

An example program for using CoreRCON can be found in [`Program.cs`](../src/CoreRCON/Program.cs).

## Installation
CoreRCON is available on NuGet, and can be installed with:
```
Install-Package CoreRCON
```