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
        private readonly string _subject;

        public MessageSource(Lazy<IConnection> lazyConnection,
            string subjectPrefix,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _lazyConnection = lazyConnection;
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
                        .Select(Deserialize)
                        .Subscribe(observer));
            }
        }
        
        private TMessage Deserialize(MsgHandlerEventArgs args)
        {
            IMessageDeserializer<TMessage> deserializer;
            var messageType = GetMessageType(args);

            if (!_deserializers.TryGetValue(messageType, out deserializer))
            {
                if (_deserializers.Count > 1)
                {
                    throw new Exception(string.Format("Missing deserializer for type '{0}'", messageType));
                }
                // special case for projection streams
                deserializer = _deserializers.Values.Single();
            }
            return deserializer.Deserialize(new MemoryStream(args.Message.Data));
        }

        private static string GetMessageType(MsgHandlerEventArgs args)
        {
            var subject = args.Message.Subject;
            var lastIndexOf = subject.LastIndexOf(".");
            var key = subject.Substring(lastIndexOf, subject.Length - lastIndexOf);
            return key;
        }

        public void Dispose()
        {
        }
    }
}
