using System.IO;
using ProtoBuf.Meta;

namespace Obvs.Serialization.ProtoBuf
{
    public class ProtoBufMessageSerializer : IMessageSerializer
    {
        private readonly RuntimeTypeModel _model;

        public ProtoBufMessageSerializer(RuntimeTypeModel model = null)
        {
            _model = model ?? RuntimeTypeModel.Default;
        }

        public void Serialize(Stream destination, object message)
        {
            if (message == null) return;

            _model.Serialize(destination, message);
        }
    }
}