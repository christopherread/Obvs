using System;
using Obvs.Types;

namespace Obvs
{
    /// <summary>An endpoint of a service: one side, server or client, of its transport.</summary>
    public interface IEndpoint<in TMessage> : IDisposable
        where TMessage : class
    {
        /// <summary>Whether <paramref name="message"/> belongs to this endpoint's service.</summary>
        bool CanHandle(TMessage message);
        /// <summary>The endpoint's name, for logs.</summary>
        string Name { get; }
    }

    /// <summary>An endpoint for the bus's standard message interfaces.</summary>
    public interface IEndpoint : IEndpoint<IMessage>
    {
    }
}