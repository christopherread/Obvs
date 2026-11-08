namespace Obvs.ActiveMQ
{
    /// <summary>When an ActiveMQ <see cref="MessageSource{TMessage}"/> acknowledges the messages it receives.</summary>
    public enum AcknowledgementMode
    {
        /// <summary>The session acknowledges each message as it's delivered</summary>
        AutoAcknowledge,

        /// <summary>The source acknowledges each message once it has deserialized it</summary>
        ClientAcknowledge
    }
}