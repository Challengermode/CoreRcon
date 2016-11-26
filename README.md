# CoreRCON
<img src="https://cdn.rawgit.com/ScottKaye/CoreRCON/master/logo.png" align="right">

[![](https://readthedocs.org/projects/corercon/badge/?version=latest)](http://corercon.readthedocs.io/en/latest/)

CoreRCON is an implementation of Valve's [Source RCON Protocol](https://developer.valvesoftware.com/wiki/Source_RCON_Protocol) on .NET Core.  It currently supports connecting to a server, sending commands and receiving their output, and receiving logs from SRCDS' `logaddress` functionality.

## Installation
CoreRCON is available on NuGet, and can be installed with:
```
Install-Package CoreRCON
```

## Quick Start
### Connect to an RCON server and send a command
The IP address supplied here is the server you wish to connect to.
```cs
using CoreRCON;
using CoreRCON.Parsers.Standard;
// ...

// Connect to a server
var rcon = new CoreRCON.RCON(IPAddress.Parse("127.0.0.1"), 27015, "secret-password");

// Send "status"
Status status = rcon.SendCommandAsync<Status>("status");

Console.WriteLine($"Connected to: {status.Hostname}");
```

### Listen for chat messages on the server
This assumes you have been added to the server's `logaddress` list.  You do not need to make an rcon connection to receive logs from a server.

The IP address supplied here must be your public/external IP.  [Find out here](http://checkip.dyndns.it/) if you do not know what your IP is, or implement this check in your own code.

Similarily, the port specified must be open (check your router settings) and unused.  Pass a value of `0` to use the first-available port.  Access `log.ResolvedPort` to see which port it chose.
```cs
using CoreRCON;
using CoreRCON.Parsers.Standard;
// ...

var log = new RCON.LogReceiver(IPAddress.Parse("192.168.1.8"), 50000);
log.Listen<ChatMessage>(chat =>
{
	Console.WriteLine($"Chat message: {chat.Player.Name} said {chat.Message} on channel {chat.Channel}");
});
```