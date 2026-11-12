using System.IO;
using ProtoBuf.Meta;

namespace Obvs.Serialization.ProtoBuf
{
    /// <summary>Reads a <typeparamref name="TMessage"/> as protocol buffers, with protobuf-net.</summary>
    public class ProtoBufMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage> 
        where TMessage : class
    {
        private readonly RuntimeTypeModel _model;

        /// <summary>A deserializer with <paramref name="model"/>.</summary>
        /// <param name="model">protobuf-net's model, or its default</param>
        public ProtoBufMessageDeserializer(RuntimeTypeModel model = null)
        {
            _model = model ?? RuntimeTypeModel.Default;
        }

        /// <inheritdoc />
        public override TMessage Deserialize(Stream source)
        {
            return (TMessage)_model.Deserialize(source, null, typeof(TMessage));
        }
    }
}