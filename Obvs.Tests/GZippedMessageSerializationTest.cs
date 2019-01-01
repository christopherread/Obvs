using System;
using System.Text;
using System.IO;

using FakeItEasy;

using Obvs.Types;
using Obvs.Serialization;

using Xunit;

namespace Obvs.Tests {

    /// <summary>
    /// Test Gzipped message serialization
    /// </summary>
    public class GZippedMessageSerializationTest {

        public class TestMessage: IMessage {
            public string Content {get; set;} = null;

            public override bool Equals(object obj)
            {
                //
                // See the full list of guidelines at
                //   http://go.microsoft.com/fwlink/?LinkID=85237
                // and also the guidance for operator== at
                //   http://go.microsoft.com/fwlink/?LinkId=85238
                //
                
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }
                
                return this.Content.Equals(((TestMessage) obj).Content);
            }
            
            // override object.GetHashCode
            public override int GetHashCode()
            {
                return Content.GetHashCode();
            }

        }

        #region "Serialization tests"
        [Fact]
        public void Test_GZippedMessageSerializerNullActionConstruction_Fails() {
            Assert.Throws(typeof(ArgumentNullException),
            () => new GZippedMessageSerializer(null as Action<Stream, object>));
        }

        [Fact]
        public void Test_GZippedMessageSerializerNullGzippedSerializerConstruction_Fails() {
            Assert.Throws(typeof(ArgumentNullException),
            () => new GZippedMessageSerializer(null as GZippedMessageSerializer));
        }

        [Fact]
        public void Test_GZippedMessageSerializerGzippedSerializerConstruction_Fails() {
            Assert.Throws(typeof(ArgumentException),
                () => new GZippedMessageSerializer(new GZippedMessageSerializer( (Stream stream, object obj) => {})));
        }

        #endregion

        #region "Deserialization tests"

        [Fact]
        public void Test_GZippedMessageDeserializerNullActionConstruction_Fails() {
            Assert.Throws(
                typeof(ArgumentNullException),
                () => new GZippedMessageDeserializer<TestMessage>(null as Func<System.IO.Stream, TestMessage>));
        }


        [Fact]
        public void Test_GZippedMessageDeserializerNullGzippedSerializerConstruction_Fails() {
            Assert.Throws(typeof(ArgumentNullException),
            () => new GZippedMessageDeserializer<Obvs.Types.IMessage>(null as GZippedMessageDeserializer<TestMessage>));
        }
        #endregion

       [Fact]
        public void Test_GZippedMessageDeserializerGzippedSerializerConstruction_Fails() {
            Assert.Throws(typeof(ArgumentException),
                () => new GZippedMessageDeserializer<TestMessage>(new GZippedMessageDeserializer<TestMessage>( (Stream stream) => new TestMessage{})));
        }

        [Fact]
        public void Test_GzippedMessageFullSerialization_Succeeds() {
            var content = "test";
            var wasSerializeActionCalled = false;
            Action<Stream, object> serializeAction = (stream, obj) => {
                wasSerializeActionCalled = true;
                var message = obj as TestMessage;
                var contentBytes = Encoding.UTF8.GetBytes(message.Content);
                stream.Write(contentBytes, 0, contentBytes.Length);
            };
            var messageSerializer = new GZippedMessageSerializer(serializeAction);
            
            var wasDeserializeFuncCalled = false;
            Func<Stream, TestMessage> deserializeFunction = stream => {
                wasDeserializeFuncCalled = true;
                var buffer = new byte[Encoding.UTF8.GetByteCount(content)];
                var numBytesRead = stream.Read(buffer, 0, buffer.Length);
                var deserializedMessage = new TestMessage {
                    Content = Encoding.UTF8.GetString(buffer)
                };
                return deserializedMessage;
            };
            var messageDeserializer = new GZippedMessageDeserializer<TestMessage>(deserializeFunction);

            var testMessage = new TestMessage {
                Content = content
            };
            var messageStream = new MemoryStream();
            messageSerializer.Serialize(messageStream, testMessage);
            Assert.True(wasSerializeActionCalled);
            messageStream.Position = 0;
            var deserializedTestMessage = messageDeserializer.Deserialize(messageStream);
            Assert.True(wasDeserializeFuncCalled);

            Assert.Equal(testMessage, deserializedTestMessage);
        }

        #region "IMessage serializer extensions"
        [Fact]
        public void Test_SerializeGZipped_Fails() {
            var messageSerializer = new GZippedMessageSerializer((stream, obj) => {});
            Assert.Throws(typeof(ArgumentException), () => messageSerializer.SerializeGZipped());
        }

        [Fact]
        public void Test_DeserializeGZipped_Fails() {
            var messageDeserializer = new GZippedMessageDeserializer<TestMessage>((stream) => new TestMessage{});
            Assert.Throws(typeof(ArgumentException), () => messageDeserializer.DeserializeGZipped());
        }

        [Fact]
        public void Test_SerializeGZipped_Success() {
            var messageSerializer = A.Fake<IMessageSerializer>();
            var gzippedMessageSerializer = messageSerializer.SerializeGZipped();
            Assert.NotNull(gzippedMessageSerializer);
        }

        [Fact]
        public void Test_DeserializeGZipped_Success() {
            var messageDeserializer = A.Fake<IMessageDeserializer<TestMessage>>();
            var gzippedMessageDeserializer = messageDeserializer.DeserializeGZipped<TestMessage>();
            Assert.NotNull(gzippedMessageDeserializer);
        }
        #endregion

    }
}