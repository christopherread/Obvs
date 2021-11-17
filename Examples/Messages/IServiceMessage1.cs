using Obvs.Types;

namespace Obvs.Example.Messages
{
    /// <summary>
    /// These message contracts are in their own class lib for convenience,
    /// but you can also copy/paste into each project if you don't want a compile-time dependency,
    /// as long as serialization contract is the same.
    /// </summary>
    public interface IServiceMessage1 : IMessage { }

    public class Command1 : IServiceMessage1, ICommand
    {
        public string Data { get; set; }
        public override string ToString()
        {
            return Data;
        }
    }

    public class Event1 : IServiceMessage1, IEvent
    {
        public string Data { get; set; }
        public override string ToString()
        {
            return Data;
        }
    }

    public class Request1 : IServiceMessage1, IRequest
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public class Response1 : IServiceMessage1, IResponse
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }
}
