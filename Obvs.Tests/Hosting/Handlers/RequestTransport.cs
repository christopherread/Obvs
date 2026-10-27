using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.Tests.Hosting.Handlers
{
    // A client transport over the real ServiceEndpointClient. Sending a request fails with SendFailure while it's
    // set, and otherwise answers with a Quote at once, during the send, as the in-memory transport can.
    public sealed class RequestTransport : IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly Subject<IResponse> _responses = new Subject<IResponse>();

        public Exception SendFailure { get; set; }

        public IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpoint()
        {
            throw new NotSupportedException("RequestTransport is client-only; configure the service AsClient().");
        }

        public IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpointClient()
        {
            return new ServiceEndpointClient(new Source<IEvent>(Observable.Never<IEvent>()), new Source<IResponse>(_responses),
                new RequestPublisher(this), new DefaultMessagePublisher<ICommand>(), typeof(IOrderMessage));
        }

        private sealed class RequestPublisher : IMessagePublisher<IRequest>
        {
            private readonly RequestTransport _transport;

            public RequestPublisher(RequestTransport transport) => _transport = transport;

            public async Task PublishAsync(IRequest request, CancellationToken cancellationToken = default)
            {
                await Task.Yield(); // Fails asynchronously, as a broker does
                if (_transport.SendFailure != null)
                {
                    throw _transport.SendFailure;
                }
                var getQuote = (GetQuote)request;
                _transport._responses.OnNext(new Quote(getQuote.Sku, 9.99m) { RequestId = request.RequestId, RequesterId = request.RequesterId });
            }

            public void Dispose()
            {
            }
        }

        private sealed class Source<T> : IMessageSource<T> where T : class
        {
            public Source(IObservable<T> messages) => Messages = messages;

            public IObservable<T> Messages { get; }

            public void Dispose()
            {
            }
        }
    }
}
