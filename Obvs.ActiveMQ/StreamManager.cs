using Obvs.ActiveMQ.RecyclableMemoryStream;

namespace Obvs.ActiveMQ
{
    internal static class StreamManager
    {
        public static RecyclableMemoryStreamManager Instance = new RecyclableMemoryStreamManager();
    }
}