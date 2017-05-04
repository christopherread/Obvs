using System;
using Obvs.Types;

namespace Obvs
{
    public interface IEndpoint
    {
        bool CanHandle(IMessage message);
    }

    public interface IServiceEndpoint : IEndpoint
    {
        IObservable<IRequest> Requests { get; }
        IObservable<ICommand> Commands { get; }

        void Publish(IEvent ev);
        void Reply(IRequest request, IResponse response);
    }
}