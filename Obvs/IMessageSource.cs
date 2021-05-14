using System;
using System.Reactive.Linq;

namespace Obvs
{
    /// <inheritdoc cref="Messages"/>
    public interface IMessageSource<out TMessage> : IDisposable
        where TMessage : class
    {
        /// <summary> Subscription to a single ActiveMQ topic </summary>
        IObservable<TMessage> Messages { get; }
    }

    /// <summary> Null-Object <see cref="IMessageSource{TMessage}"/>; actually does NOT return any messages </summary>
    public class DefaultMessageSource<TMessage> : IMessageSource<TMessage>
        where TMessage : class
    {
        public void Dispose()
        {
        }

        public IObservable<TMessage> Messages
        {
            get { return Observable.Empty<TMessage>(); }
        }
    }  
}