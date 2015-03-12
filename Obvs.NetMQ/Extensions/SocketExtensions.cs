using System;
using NetMQ;
using NetMQ.Sockets;

namespace Obvs.NetMQ.Extensions
{
    internal static class SocketExtensions
    {
        public static bool TryReceive(this SubscriberSocket socket, TimeSpan timeOut,
            out string topic, out string typeName, out object rawMessage)
        {
            typeName = null;
            rawMessage = null;
            topic = null;

            NetMQMessage message = socket.ReceiveMessage(timeOut);

            if (message != null)
            {
                message.ParseFrames(out topic, out typeName, out rawMessage);
                return true;
            }

            return false;
        }

        public static void SendToTopic(this PublisherSocket socket, string topic, string typeName, object serializedMessage)
        {
            NetMQMessage netMQMessage = new NetMQMessage();
            netMQMessage.Append(topic);
            netMQMessage.Append(typeName);
            netMQMessage.Append(serializedMessage.GetType().Name);
            netMQMessage.AppendMessage(serializedMessage);

            socket.SendMessage(netMQMessage, true);
        }

        private static void AppendMessage(this NetMQMessage netMQMessage, object serializedMessage)
        {
            if (serializedMessage is string)
            {
                netMQMessage.Append((string)serializedMessage);
            }
            else if (serializedMessage is byte[])
            {
                netMQMessage.Append((byte[])serializedMessage);
            }
        }

        private static void ParseFrames(this NetMQMessage message, out string topic, out string typeName, out object rawMessage)
        {
            if (message.FrameCount != 4)
            {
                throw new Exception("Expected message with 4 frames (topic,typeName,dataType,data) but received " + message.FrameCount);
            }

            topic = message[0].ConvertToString();
            typeName = message[1].ConvertToString();
            string dataType = message[2].ConvertToString();

            if (dataType == typeof(string).Name)
            {
                rawMessage = message[3].ConvertToString();
            }
            else if (dataType == typeof(byte[]).Name)
            {
                rawMessage = message[3].ToByteArray(true);
            }
            else
            {
                throw new Exception("Unknown serialization data type " + dataType);
            }
        }
    }
}