using Obvs.Types;

namespace Obvs.ActiveMQ.Tests.Hosting
{
    public interface IOrderMessage : IMessage { }

    public class PlaceOrder : IOrderMessage, ICommand
    {
        public string OrderId { get; set; }
    }

    public class OrderPlaced : IOrderMessage, IEvent
    {
        public string OrderId { get; set; }
    }

    public class GetQuote : IOrderMessage, IRequest
    {
        public string Sku { get; set; }
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public class Quote : IOrderMessage, IResponse
    {
        public decimal Price { get; set; }
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }
}
