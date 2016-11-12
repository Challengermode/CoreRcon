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

You can optionally returned a strongly-typed result, if a parser exists for it:

```cs
await rcon.SendCommandAsync<Status>("status", stats => {
	Console.WriteLine($"Status hostname is: {stats.Hostname}");
});
```

In case you're not a fan of the callback pattern, `SendCommandAsync` can be used as a block by simply not providing any callback:

```cs
var status = await rcon.SendCommandAsync<Status>("status");
Console.WriteLine($"Blocked status: {status.Hostname}");
```