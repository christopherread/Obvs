using System;
using Obvs.Types;

namespace Obvs
{
    public interface IEndpoint<in TMessage> : IDisposable
        where TMessage : class
    {
        bool CanHandle(TMessage message);
        string Name { get; }
    }

    public interface IEndpoint : IEndpoint<IMessage>
    {
    }
}