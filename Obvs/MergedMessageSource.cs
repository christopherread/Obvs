using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Obvs.Extensions;

namespace Obvs
{
    public class MergedMessageSource<TMessage> : IMessageSource<TMessage> 
        where TMessage : class
    {
        private readonly IObservable<TMessage> _messages;

        public MergedMessageSource(IEnumerable<IMessageSource<TMessage>> sources)
        {
            _messages = sources.Select(source => source.Messages).Merge().PublishRefCountRetriable();
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                return _messages;
            }
        }

        public void Dispose()
        {
        }
    }
}