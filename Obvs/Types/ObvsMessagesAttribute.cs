using System;

namespace Obvs.Types
{
    /// <summary>
    /// Marks a partial class for Obvs's source generator, which adds a static <c>Types</c> property listing every
    /// concrete class in this assembly that implements <see cref="ServiceMessageType"/>, for the service's
    /// <c>AddMessages(...)</c>: <c>service.AddMessages(OrderMessages.Types)</c>. Abstract classes and interfaces are
    /// left out; the bus checks the rest when it starts.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ObvsMessagesAttribute : Attribute
    {
        public ObvsMessagesAttribute(Type serviceMessageType)
        {
            ServiceMessageType = serviceMessageType;
        }

        /// <summary>The interface the service's messages implement, such as <c>IOrderMessage</c>.</summary>
        public Type ServiceMessageType { get; }
    }
}
