using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Obvs
{
    public class MessageSourceConverter<TFrom, TTo> : BaseMessageSource<TTo>
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


        /// <inheritdoc />
        public override IObservable<TTo> GetMessages(IScheduler scheduler) {
            return _source.GetMessages(scheduler)
                            .Select(ConvertedMessage)
                            .Where(MessageIsValid);
        }

        private static bool MessageIsValid(TTo msg)
        {
            return msg != null;
        }

        private TTo ConvertedMessage(TFrom obj)
        {
            return _converter.Convert(obj);
        }

        public override void Dispose()
        {
            _source.Dispose();
            _converter.Dispose();
        }
    }
}