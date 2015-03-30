using System;
using System.Threading.Tasks;
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

        Task PublishAsync(IEvent ev);
        Task ReplyAsync(IRequest request, IResponse response);
    }
}