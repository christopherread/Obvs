using Obvs.Types;

namespace Obvs.Samples
{
    // snippet: messages
    // Every message of a service implements its interface, and is one of ICommand, IEvent, IRequest or IResponse.
    // A request names the response that answers it.
    public interface IOrderMessage : IMessage { }

    public record PlaceOrder(string OrderId) : IOrderMessage, ICommand;

    public record OrderPlaced(string OrderId) : IOrderMessage, IEvent;

    public record GetQuote(string Sku) : IOrderMessage, IRequest<Quote>
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public record Quote(string Sku, decimal Price) : IOrderMessage, IResponse
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }
    // end-snippet

    // snippet: generated-registry
    // Obvs's source generator gives this a Types property: every message in this assembly that implements IOrderMessage
    [ObvsMessages(typeof(IOrderMessage))]
    public static partial class OrderMessages { }
    // end-snippet
}
