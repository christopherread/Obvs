using System;
using System.Reactive.Linq;

namespace Obvs
{
    public interface IMessageBridge : IDisposable
    {
        void Start();
        void Stop();
    }

    public class MessageBridge<TFrom, TTo> : IMessageBridge 
        where TFrom : class 
        where TTo : class
    {
        private readonly IMessagePublisher<TTo> _publisher;
        private readonly IMessageConverter<TFrom, TTo> _converter;
        private readonly IMessageSource<TFrom> _source;

        private IDisposable _subscription;

        public MessageBridge(IMessagePublisher<TTo> publisher,
                             IMessageConverter<TFrom, TTo> converter,
                             IMessageSource<TFrom> source)
        {
            _publisher = publisher;
            _converter = converter;
            _source = source;
        }

        public void Start()
        {
            Stop();

            _subscription = _source.Messages
                                   .Select(ConvertedMessage)
                                   .Where(MessageIsValid)
                                   .Subscribe(PublishMessage);
        }

        public void Stop()
        {
            if (_subscription != null)
            {
                _subscription.Dispose();
                _subscription = null;
            }
        }

        public void Dispose()
        {
            Stop();
            _publisher.Dispose();
        }

        private void PublishMessage(TTo msg)
        {
            _publisher.PublishAsync(msg);
        }

        private static bool MessageIsValid(TTo msg)
        {
            return msg != null;
        }

        private TTo ConvertedMessage(TFrom obj)
        {
            return _converter.Convert(obj);
        }
    }
}