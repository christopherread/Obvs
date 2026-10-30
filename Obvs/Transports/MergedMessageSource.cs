using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Obvs.Extensions;

// v6's message merging, which the ActiveMQ transport uses: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.Transports
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