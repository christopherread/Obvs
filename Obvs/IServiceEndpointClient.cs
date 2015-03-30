using System;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs
{
    public interface IServiceEndpointClient : IEndpoint
    {
        IObservable<IEvent> Events { get; }
        Task SendAsync(ICommand command);

        IObservable<IResponse> GetResponses(IRequest request);
    }
}