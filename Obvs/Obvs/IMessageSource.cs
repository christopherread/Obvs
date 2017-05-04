using System;
using System.Reactive.Linq;
using Obvs.Types;

namespace Obvs
{
    public interface IMessageSource<out TMessage> : IDisposable
        where TMessage : IMessage
    {
        IObservable<TMessage> Messages { get; }
    }

    public class DefaultMessageSource<TMessage> : IMessageSource<TMessage> where TMessage : IMessage
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