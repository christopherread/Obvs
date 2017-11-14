using System.Diagnostics;
using System.Net;

#if NET45
using System.Security.Principal;
#endif

namespace Obvs.Configuration
{
    public static class RequesterId
    {
        public static string Create()
        {
    #if NET45
            string identity = WindowsIdentity.GetCurrent()?.Name.Substring(identity.Name.IndexOf(@"\", System.StringComparison.Ordinal) + 1);
    #else
            string identity = "";
    #endif
            
            Process process = Process.GetCurrentProcess();
            string userName = identity;
            string hostName = Dns.GetHostName();
            return string.Format("{0}-{1}-{2}-{3}", process.ProcessName, hostName, userName, process.Id);
        }
    }
}