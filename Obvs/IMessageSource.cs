using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Obvs
{
    public interface IMessageSource<out TMessage> : IDisposable
        where TMessage : class
    {
        IObservable<TMessage> Messages { get; }

        /// <summary>
        /// Observe messages from source with a specific scheduler
        /// </summary>
        /// <param name="scheduler">The scheduler implementation to use</param>
        /// <returns>Observable of messages in the source</returns>
        IObservable<TMessage> GetMessages(IScheduler scheduler);
    }

    public abstract class BaseMessageSource<TMessage> : IMessageSource<TMessage> where TMessage: class
    {
        public virtual IObservable<TMessage> Messages {
            get => GetMessages(DefaultScheduler.Instance);
        }

        public virtual void Dispose() {}

        public abstract IObservable<TMessage> GetMessages(IScheduler scheduler);
    }



    public class DefaultMessageSource<TMessage> : BaseMessageSource<TMessage>
        where TMessage : class
    {

        /// <inheritdoc />
        public override IObservable<TMessage> GetMessages(IScheduler scheduler)
        {
            return Observable.Empty<TMessage>(scheduler);
        }
    }  
}