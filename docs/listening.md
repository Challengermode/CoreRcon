# Listening
Pass your public IP into `StartLogging` to tell SRCDS to send you log packets.

> Note that this **does not** set `log on` or enable any specific types of logging within SRCDS.  It only adds the supplied IP to the logaddresses with `logaddress_add`.

```cs
await rcon.StartLogging("127.0.0.1");
```

Also note that the IP supplied here is what SRCDS will try to send packets to.  Unless SRCDS is running locally, you **must** provide a public/external IP address to receive any logs.  The port is `7744` - at the present time there is no way to change this.

## Receiving logs
Once you are listening, you are free to set up listeners for various forms of parseable packets.  This allows you to receive strongly-typed data from the server, parsed by some intense regular expressions.

For example, to recieve chat messages sent on the server:

```cs
rcon.Listen<ChatMessage>(chat =>
{
	Console.WriteLine($"Chat message: {chat.Player.Name} said {chat.Message} on channel {chat.Channel}");
});
```

Similarily, for the kill feed:

```cs
rcon.Listen<KillFeed>(kill =>
{
	// ...
});
```

If a strongly typed parser for your data doesn't exist, you can receive log packets directly, either as a string, or by the full packet:

```
rcon.Listen(raw =>
{
	// ...
});

rcon.Listen((LogAddressPacket packet) =>
{
	// ...
});
```

Receiving `LogAddressPacket` directly contains some more information that may be useful, but most of the time, a raw packet listener will suffice.