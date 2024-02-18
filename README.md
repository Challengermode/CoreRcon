![CoreRcon](https://raw.githubusercontent.com/Challengermode/CoreRCON/master/logo.png)

[![Nuget](https://img.shields.io/nuget/v/CoreRCON)](https://www.nuget.org/packages/CoreRCON/) [![Nuget](https://img.shields.io/nuget/dt/CoreRCON)](https://www.nuget.org/packages/CoreRCON/)

CoreRCON is an implementation of the RCON protocol on .NET Core. It currently supports connecting to a server, sending commands and receiving their output, [multi-packat responses](https://developer.valvesoftware.com/wiki/Source_RCON_Protocol#Multiple-packet_Responses), and receiving logs from `logaddress`.

### Supports:
* **CS2**, **TF2** - (see [Source RCON Protocol](https://developer.valvesoftware.com/wiki/Source_RCON_Protocol))
* **Minecraft** - Thanks to [CodingContraption](https://github.com/ScottKaye/CoreRCON/pull/7)
* **ARK: Survival Evolved** - Confirmed working in 3.0.0 by [tgardner851](https://github.com/ScottKaye/CoreRCON/issues/10)
* **Project Zomboid Multiplayer** - Confirmed working by [captainqwerty](https://github.com/Challengermode/CoreRcon/issues/26)
* **Palworld** - Thanks to [ExusAltimus](https://github.com/Challengermode/CoreRcon/pull/57)
* Potentially other Source-based RCON implementations (untested)

## Quick Start
### Connect to an RCON server and send a command
The IP address supplied here is the server you wish to connect to.
```cs
using CoreRCON;
using CoreRCON.Parsers.Standard;
using System.Net;
// ...

// Connect to a server
var rcon = new RCON(IPAddress.Parse("10.0.0.1"), 27015, "secret-password");
await rcon.ConnectAsync();

// Send a simple command and retrive response as string
string respnose = await rcon.SendCommandAsync("echo hi");

// Send "status" and try to parse the response
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
See [Github Releases](https://github.com/Challengermode/CoreRcon/releases/tag/v5.2.0)

Big thanks to [ScottKaye](https://github.com/ScottKaye) for developing the [original version](https://github.com/ScottKaye/CoreRCON)
