using System.Text;
using NetJSON;

namespace Obvs.Serialization.NetJson
{
    public static class NetJsonDefaults
    {
        public static readonly Encoding Encoding = new UTF8Encoding(false);
        public static void Set()
        {
            NetJSONSettings.CurrentSettings.DateFormat = NetJSONDateFormat.ISO;
            NetJSONSettings.CurrentSettings.UseEnumString = true;
            NetJSONSettings.CurrentSettings.TimeZoneFormat = NetJSONTimeZoneFormat.Local;
        }
    }
}