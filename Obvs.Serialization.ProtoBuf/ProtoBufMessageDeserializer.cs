using System.IO;
using ProtoBuf.Meta;

namespace Obvs.Serialization.ProtoBuf
{
    public class ProtoBufMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage> 
        where TMessage : class
    {
        private readonly RuntimeTypeModel _model;

        public ProtoBufMessageDeserializer(RuntimeTypeModel model = null)
        {
            _model = model ?? RuntimeTypeModel.Default;
        }

        public override TMessage Deserialize(Stream source)
        {
            return (TMessage)_model.Deserialize(source, null, typeof(TMessage));
        }
    }
}