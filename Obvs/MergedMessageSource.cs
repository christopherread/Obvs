using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Obvs.Extensions;

namespace Obvs
{
    public class MergedMessageSource<TMessage> : BaseMessageSource<TMessage> 
        where TMessage : class
    {
        private readonly IEnumerable<IMessageSource<TMessage>> _sources;
        
        public MergedMessageSource(IEnumerable<IMessageSource<TMessage>> sources)
        {
            if (sources == null) {
                throw new ArgumentNullException(nameof(sources));
            }
            if (!sources.Any()) {
                throw new ArgumentException("At least one source must be specified", nameof(sources));
            }
            _sources = sources;
        }

        /// <inheritdoc />
        public override IObservable<TMessage> GetMessages(IScheduler scheduler) {
            return _sources.Select(source => source.GetMessages(scheduler))
                            .Merge()
                            .PublishRefCountRetriable();
        }

    }
}