# Obvs.Serialization.MessagePack

[MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp) serialization for [Obvs](https://github.com/christopherread/Obvs).

```bash
dotnet add package Obvs.Serialization.MessagePack
```

Set it on a service with `SerializedWith`:

<!-- snippet: serialized-with-messagepack -->
```csharp
service.SerializedWith(new MessagePackCSharpMessageSerializer(), new MessagePackCSharpMessageDeserializerFactory());
```

Both take an `IFormatterResolver` for messages that need one. Add `.Compressed()` to gzip messages.

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme).
