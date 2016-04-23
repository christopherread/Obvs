using System;
using NetMQ;
using NetMQ.Sockets;
using Obvs.NetMQ.Configuration;

namespace Obvs.NetMQ.Extensions
{
    internal static class SocketExtensions
    {
        private const int ExpectedFrameCount = 4;
        private static readonly string DataTypeName = typeof(byte[]).Name;

        public static bool TryReceive(this SubscriberSocket socket, TimeSpan timeOut,
            out string topic, out string typeName, out byte[] rawMessage)
        {
            typeName = null;
            rawMessage = null;
            topic = null;

            var message = new NetMQMessage();

            if (socket.TryReceiveMultipartMessage(timeOut, ref message, ExpectedFrameCount))
            {
                message.ParseFrames(out topic, out typeName, out rawMessage);
                return true;
            }

            return false;
        }

        public static void SendToTopic(this PublisherSocket socket, string topic, string typeName, byte[] serializedMessage)
        {
            socket.TrySendMultipartMessage(
                new NetMQMessage(new[]
                {
                    new NetMQFrame(topic),
                    new NetMQFrame(typeName),
                    new NetMQFrame(DataTypeName),
                    new NetMQFrame(serializedMessage),
                }));
        }

        private static void ParseFrames(this NetMQMessage message, out string topic, out string typeName, out byte[] rawMessage)
        {
            if (message.FrameCount != ExpectedFrameCount)
            {
                throw new Exception(string.Format("Expected message with {0} frames (topic,typeName,dataType,data) but received {1}", ExpectedFrameCount, message.FrameCount));
            }

            topic = message[0].ConvertToString();
            typeName = message[1].ConvertToString();
            string dataType = message[2].ConvertToString();

            if (dataType != DataTypeName)
            {
                throw new Exception(string.Format("Unknown serialization data type '{0}'", dataType));
            }

            rawMessage = message[3].ToByteArray(true);
        }

        internal static void Start(this NetMQSocket socket, string address, SocketType socketType)
        {
            switch (socketType)
            {
                case SocketType.Client:
                    socket.Connect(address);
                    break;
                case SocketType.Server:
                    socket.Bind(address);
                    break;
                default:
                    throw new ArgumentException(string.Format("Unknown SocketType {0}", socketType), "socketType");
            }
        }

        internal static void Stop(this NetMQSocket socket, string address, SocketType socketType)
        {
            switch (socketType)
            {
                case SocketType.Client:
                    socket.Disconnect(address);
                    break;
                case SocketType.Server:
                    socket.Unbind(address);
                    break;
                default:
                    throw new ArgumentException(string.Format("Unknown SocketType {0}", socketType), "socketType");
            }
            socket.Close();
        }
    }
}