using System;
using System.Collections.Generic;
using System.Text.Json;
using NetMQ;
using NetMQ.Sockets;
using Obvs.NetMQ.Configuration;

namespace Obvs.NetMQ.Extensions
{
    internal static class SocketExtensions
    {
        // Topic, type name, data type and body, as v6 sends and reads them, then optionally the headers, which v6 can't read
        private const int ExpectedFrameCount = 4;
        private const int FrameCountWithHeaders = 5;
        private static readonly string DataTypeName = typeof(byte[]).Name;

        public static bool TryReceive(this SubscriberSocket socket, TimeSpan timeOut,
            out string topic, out string typeName, out byte[] rawMessage, out IReadOnlyDictionary<string, string> headers)
        {
            typeName = null;
            rawMessage = null;
            topic = null;
            headers = null;

            var message = new NetMQMessage();

            if (socket.TryReceiveMultipartMessage(timeOut, ref message, FrameCountWithHeaders))
            {
                message.ParseFrames(out topic, out typeName, out rawMessage, out headers);
                return true;
            }

            return false;
        }

        // Sends the headers only if there are any
        public static void SendToTopic(this NetMQSocket socket, string topic, string typeName, byte[] serializedMessage,
            IReadOnlyDictionary<string, string> headers = null)
        {
            var frames = new List<NetMQFrame>
            {
                new NetMQFrame(topic),
                new NetMQFrame(typeName),
                new NetMQFrame(DataTypeName),
                new NetMQFrame(serializedMessage),
            };
            if (headers != null && headers.Count > 0)
            {
                frames.Add(new NetMQFrame(JsonSerializer.SerializeToUtf8Bytes(headers)));
            }
            socket.TrySendMultipartMessage(new NetMQMessage(frames));
        }

        private static void ParseFrames(this NetMQMessage message, out string topic, out string typeName, out byte[] rawMessage,
            out IReadOnlyDictionary<string, string> headers)
        {
            if (message.FrameCount != ExpectedFrameCount && message.FrameCount != FrameCountWithHeaders)
            {
                throw new Exception(string.Format("Expected message with {0} frames (topic,typeName,dataType,data), or {1} with headers, but received {2}",
                    ExpectedFrameCount, FrameCountWithHeaders, message.FrameCount));
            }

            topic = message[0].ConvertToString();
            typeName = message[1].ConvertToString();
            string dataType = message[2].ConvertToString();

            if (dataType != DataTypeName)
            {
                throw new Exception(string.Format("Unknown serialization data type '{0}'", dataType));
            }

            rawMessage = message[3].ToByteArray(true);
            headers = message.FrameCount == FrameCountWithHeaders
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(message[4].ToByteArray())
                : null;
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