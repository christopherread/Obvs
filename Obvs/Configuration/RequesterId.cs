using System.Diagnostics;
using System.Net;

namespace Obvs.Configuration
{
    public static class RequesterId
    {
        public static string Create()
        {
    #if NETFRAMEWORK
            var windowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
            string identity = windowsIdentity.Name.Substring(windowsIdentity.Name.IndexOf(@"\", System.StringComparison.Ordinal) + 1);
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