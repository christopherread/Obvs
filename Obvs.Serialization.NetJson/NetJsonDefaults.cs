using NetJSON;

namespace Obvs.Serialization.NetJson
{
    public static class NetJsonDefaults
    {
        public static void Set()
        {
            NetJSON.NetJSON.DateFormat = NetJSONDateFormat.JsonNetISO;
            NetJSON.NetJSON.UseEnumString = true;
            NetJSON.NetJSON.TimeZoneFormat = NetJSONTimeZoneFormat.Utc;
        }
    }
}