using System.Text;

namespace Obvs.Serialization.Json
{
    /// <summary>Defaults for Newtonsoft JSON serialization.</summary>
    public static class JsonMessageDefaults
    {
        /// <summary>The text encoding messages are written in: UTF-8, without a byte order mark.</summary>
        public static readonly Encoding Encoding = new UTF8Encoding();
    }
}