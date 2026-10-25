using Obvs.Types;

namespace Obvs.Testing.Tests
{
    public interface IOrderMessage : IMessage { }

    public interface IShippingMessage : IMessage { }

    public record PlaceOrder(string OrderId) : IOrderMessage, ICommand;

    public record OrderPlaced(string OrderId) : IOrderMessage, IEvent;

    public record GetQuote(string Sku) : IOrderMessage, IRequest
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public record Quote(string Sku, decimal Price) : IOrderMessage, IResponse
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public record ShipOrder(string OrderId) : IShippingMessage, ICommand;
}
