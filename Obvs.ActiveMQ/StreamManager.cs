using Microsoft.IO;

namespace Obvs.ActiveMQ
{
    internal static class StreamManager
    {
        public static RecyclableMemoryStreamManager Instance = new RecyclableMemoryStreamManager();
    }
}