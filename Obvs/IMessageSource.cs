using System;
using System.Reactive.Linq;

namespace Obvs
{
    public interface IMessageSource<out TMessage> : IDisposable
        where TMessage : class
    {
        IObservable<TMessage> Messages { get; }
    }

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