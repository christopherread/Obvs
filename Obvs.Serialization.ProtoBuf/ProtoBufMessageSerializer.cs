using System.IO;
using ProtoBuf.Meta;

namespace Obvs.Serialization.ProtoBuf
{
    /// <summary>Writes messages as protocol buffers, with protobuf-net. Use it with <c>SerializedWith</c>.</summary>
    public class ProtoBufMessageSerializer : IMessageSerializer
    {
        private readonly RuntimeTypeModel _model;

        /// <summary>A serializer with <paramref name="model"/>.</summary>
        /// <param name="model">protobuf-net's model, or its default</param>
        public ProtoBufMessageSerializer(RuntimeTypeModel model = null)
        {
            _model = model ?? RuntimeTypeModel.Default;
        }

        /// <inheritdoc />
        public void Serialize(Stream destination, object message)
        {
            if (message == null) return;

            _model.Serialize(destination, message);
        }
    }
}