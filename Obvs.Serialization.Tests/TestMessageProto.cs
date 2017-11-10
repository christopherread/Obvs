using System;
using Obvs.Types;
using ProtoBuf;

namespace Obvs.Serialization.Tests
{
    [ProtoContract]
    public class TestMessageProto : IMessage, IEquatable<TestMessageProto>
    {
        [ProtoMember(1)]
        public int Id { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public DateTime Date { get; set; }

        public TestMessageProto()
        {
            Date = DateTime.Now;
        }

        public bool Equals(TestMessageProto other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id && string.Equals(Name, other.Name) && Date.Equals(other.Date);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TestMessageProto) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id;
                hashCode = (hashCode*397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Date.GetHashCode();
                return hashCode;
            }
        }
    }
}