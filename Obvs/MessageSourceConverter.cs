using System;
using System.Reactive.Linq;

// v6's message bridging, kept for v6 apps: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs
{
    public class MessageSourceConverter<TFrom, TTo> : IMessageSource<TTo>
        where TTo : class
        where TFrom : class
    {
        private readonly IMessageSource<TFrom> _source;
        private readonly IMessageConverter<TFrom, TTo> _converter;

        public MessageSourceConverter(IMessageSource<TFrom> source, IMessageConverter<TFrom, TTo> converter)
        {
            _source = source;
            _converter = converter;
        }

        public IObservable<TTo> Messages
        {
            get
            {
                return _source.Messages
                              .Select(ConvertedMessage)
                              .Where(MessageIsValid);
            }
        }

        private static bool MessageIsValid(TTo msg)
        {
            return msg != null;
        }

        private TTo ConvertedMessage(TFrom obj)
        {
            return _converter.Convert(obj);
        }

        public void Dispose()
        {
            _source.Dispose();
            _converter.Dispose();
        }
    }
}