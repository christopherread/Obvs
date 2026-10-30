namespace Obvs.Transports
{
    /// <summary> Well-known Message-Property-Names </summary>
    public static class MessagePropertyNames
    {
        /// <summary>The property that carries a message's type name, which says how to deserialize it.</summary>
        public const string TypeName = "TypeName";
        /// <summary>The property that carries a request's or response's request ID.</summary>
        public const string RequestId = "RequestId";
        /// <summary>The property that carries a request's or response's requester ID.</summary>
        public const string RequesterId = "RequesterId";
    }
}