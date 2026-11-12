# Obvs.Serialization.Json

Newtonsoft.Json serialization for [Obvs](https://github.com/christopherread/Obvs).

```bash
dotnet add package Obvs.Serialization.Json
```

Set it on a service with `SerializedWith`:

<!-- snippet: serialized-with-newtonsoft -->
```csharp
service.SerializedWith(new JsonMessageSerializer(settings),
    new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>), settings));
```

Obvs serializes with System.Text.Json without it, to the same JSON for ordinary messages. Use this package when v6 services customize Newtonsoft, with `JsonSerializerSettings` or converters: see [Migrating from v6](https://github.com/christopherread/Obvs/blob/master/docs/migrating-from-v6.md). Add `.Compressed()` to gzip messages as v6's `GzippedJson` serializers did.

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme).
