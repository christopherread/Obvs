using System;
using Obvs.Types;

namespace Obvs
{
    public interface IServiceEndpointClient : IEndpoint
    {
        IObservable<IEvent> Events { get; }
        void Send(ICommand command);

        IObservable<IResponse> GetResponses(IRequest request);
    }
}