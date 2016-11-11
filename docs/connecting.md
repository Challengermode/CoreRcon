# Connecting

To connect to an RCON server, first create a new `RCON` instance, and await the `ConnectAsync` method.

```cs
var rcon = new RCON();
await rcon.ConnectAsync("127.0.0.1", 27015, "rcon_password");
```

`ConnectAsync` will continue after a successful connection is made.  If the connection failed for some reason, an `AuthenticationException` will be thrown.

To keep the connection alive, simply await `KeepAliveAsync` at the end of your program.  `KeepAliveAsync` will attempt to reconnect if the connection is ever lost.

# Executing Commands
Provide a command string and response callback to `SendCommandAsync` to execute a command.  When the server responds to that command, the callback will be called once with the raw data returned from the server.

```cs
await rcon.SendCommandAsync("status", stats => {
	Console.WriteLine($"Status is: {stats}");
});
```