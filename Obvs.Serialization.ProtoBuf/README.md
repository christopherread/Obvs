# Obvs.Serialization.ProtoBuf

[protobuf-net](https://github.com/protobuf-net/protobuf-net) serialization for [Obvs](https://github.com/christopherread/Obvs).

```bash
dotnet add package Obvs.Serialization.ProtoBuf
```

Set it on a service with `SerializedWith`:

<!-- snippet: serialized-with-protobuf -->
```csharp
service.SerializedWith(new ProtoBufMessageSerializer(), new ProtoBufMessageDeserializerFactory());
```

Both take a `RuntimeTypeModel` for messages without protobuf-net's attributes. Add `.Compressed()` to gzip messages.

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme).
