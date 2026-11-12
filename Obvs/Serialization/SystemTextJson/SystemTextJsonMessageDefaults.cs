using System.Text.Json;

namespace Obvs.Serialization.SystemTextJson
{
    /// <summary>The System.Text.Json options Obvs uses unless it's given others.</summary>
    public static class SystemTextJsonMessageDefaults
    {
        /// <summary>
        /// Reads what Newtonsoft.Json (Obvs.Serialization.Json, v6's usual choice) writes, and writes what it reads:
        /// public fields as well as properties, and property names in any case.
        /// </summary>
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };
    }
}
