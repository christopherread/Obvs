using System.Text;
using NetJSON;

namespace Obvs.Serialization.NetJson
{
    public static class NetJsonDefaults
    {
        public static readonly Encoding Encoding = new UTF8Encoding(false);
        public static void Set()
        {
            NetJSON.NetJSON.DateFormat = NetJSONDateFormat.ISO;
            NetJSON.NetJSON.UseEnumString = true;
            NetJSON.NetJSON.TimeZoneFormat = NetJSONTimeZoneFormat.Local;
        }
    }
}