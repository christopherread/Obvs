using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using NATS.Client;
using Obvs.Serialization;

namespace Obvs.NATS
{
    public class MessageSource<TMessage> : IMessageSource<TMessage>
        where TMessage : class
    {
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly Lazy<IConnection> _lazyConnection;
        private readonly Func<Dictionary<string, string>, bool> _filter;
        private readonly string _subject;

        public MessageSource(Lazy<IConnection> lazyConnection,
            string subjectPrefix,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            Func<Dictionary<string, string>, bool> filter = null)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _lazyConnection = lazyConnection;
            _filter = filter;
            _subject = string.Format("{0}.*", subjectPrefix);
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                    _lazyConnection.Value
                        .SubscribeAsync(_subject)
                        .ToObservable()
                        .Select(Unwrap)
                        .Where(MatchesFilter)
                        .Select(Deserialize)
                        .Subscribe(observer));
            }
        }

        private bool MatchesFilter(Tuple<string, MessageWrapper> tuple)
        {
            return _filter == null || _filter(tuple.Item2.Properties);
        }

        private static Tuple<string, MessageWrapper> Unwrap(MsgHandlerEventArgs args)
        {
            using (var stream = new MemoryStream(args.Message.Data))
            {
                var wrapper = ProtoBuf.Serializer.Deserialize<MessageWrapper>(stream);
                return Tuple.Create(args.Message.Subject, wrapper);
            }
        }

        private TMessage Deserialize(Tuple<string, MessageWrapper> tuple)
        {
            IMessageDeserializer<TMessage> deserializer;
            var messageType = GetMessageType(tuple.Item1);

            if (!_deserializers.TryGetValue(messageType, out deserializer))
            {
                if (_deserializers.Count > 1)
                {
                    throw new Exception(string.Format("Missing deserializer for type '{0}'", messageType));
                }
                
                deserializer = _deserializers.Values.Single();
            }
            return deserializer.Deserialize(new MemoryStream(tuple.Item2.Body));
        }

        private static string GetMessageType(string subject)
        {
            return subject.Split('.').Last();
        }

        public void Dispose()
        {
        }
    }
}
