# CoreRCON
<img src="https://cdn.rawgit.com/ScottKaye/CoreRCON/master/logo.png" align="right">

[![](https://readthedocs.org/projects/corercon/badge/?version=latest)](http://corercon.readthedocs.io/en/latest/)
[![Nuget](https://img.shields.io/nuget/v/CoreRCON)](https://www.nuget.org/packages/CoreRCON/)
[![Nuget](https://img.shields.io/nuget/dt/CoreRCON)](https://www.nuget.org/packages/CoreRCON/)

CoreRCON is an implementation of the RCON protocol on .NET Core.  It currently supports connecting to a server, sending commands and receiving their output, [multi-packat responses](https://developer.valvesoftware.com/wiki/Source_RCON_Protocol#Multiple-packet_Responses), and receiving logs from `logaddress`.

### Supports:
* **CS:GO**, **TF2** - (see [Source RCON Protocol](https://developer.valvesoftware.com/wiki/Source_RCON_Protocol))
* **Minecraft** - Thanks to [CodingContraption](https://github.com/ScottKaye/CoreRCON/pull/7)
* **ARK: Survival Evolved** - Confirmed working in 3.0.0 by [tgardner851](https://github.com/ScottKaye/CoreRCON/issues/10)
* Potentially other Source-based RCON implementations (untested)

## Quick Start
### Connect to an RCON server and send a command
The IP address supplied here is the server you wish to connect to.
```cs
using CoreRCON;
using CoreRCON.Parsers.Standard;
// ...

// Connect to a server
var rcon = new RCON(IPAddress.Parse("10.0.0.1"), 27015, "secret-password");
await rcon.ConnectAsync();

// Send "status"
Status status = await rcon.SendCommandAsync<Status>("status");

Console.WriteLine($"Connected to: {status.Hostname}");
```

### Listen for chat messages on the server
This assumes you have been added to the server's `logaddress` list.  You do not need to make an rcon connection to receive logs from a server.

The port specified must be open (check your router settings) and unused.  Pass a value of `0` to use the first-available port.  Access `log.ResolvedPort` to see which port it chose.

Finally, pass an array (or list of params) of `IPEndPoints` to express which servers you would like to receive logs from.  This is because any server can send your server logs if they know which port you are listening on, as it's just UDP.
```cs
using CoreRCON;
using CoreRCON.Parsers.Standard;
// ...

// Listen on port 50000 for log packets coming from 10.0.0.1:27015
var log = new LogReceiver(50000, new IPEndPoint(IPAddress.Parse("10.0.0.1"), 27015));
log.Listen<ChatMessage>(chat =>
{
	Console.WriteLine($"Chat message: {chat.Player.Name} said {chat.Message} on channel {chat.Channel}");
});
```

## Troubleshooting
### Can't install via NuGet
> "Could not install package 'CoreRCON X.X.X'. You are trying to install this package into a project that targets '.NETFramework,Version=vy.y.y', but the package does not contain any assembly references or content files that are compatible with that framework. For more information, contact the package author."

If you are seeing an error similar to this, try changing your project's targeted .NET Framework version [[#11]](https://github.com/ScottKaye/CoreRCON/issues/11).  If you are using Visual Studio 2015, the minimum resolvable framework version is **4.7**.  Visual Studio 2017 has improved support for .NET Core packages, allowing CoreRCON to resolve for versions as low as **4.6.1**.

## Changelog
### Version 4.0.0
* Rewrote RCON client to use Pipeline networking
* Add support for  multi-packet responses using [Koraktors trick](https://developer.valvesoftware.com/wiki/Source_RCON_Protocol#Multiple-packet_Responses)
* ConnectAsync now has to be called after construction. 
### Version 3.0.0
* [Supports Minecraft](https://github.com/ScottKaye/CoreRCON/pull/7)
* Some [`ServerQuery`](https://github.com/ScottKaye/CoreRCON/blob/master/src/CoreRCON/ServerQuery.cs#L17) methods now require a server type to differentiate between Source and Minecraft


Big thanks to [ScottKaye](https://github.com/ScottKaye) for developing the [original version](https://github.com/ScottKaye/CoreRCON)
