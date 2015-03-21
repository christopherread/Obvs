using System.Diagnostics;
using System.Net;
using System.Security.Principal;

namespace Obvs.Configuration
{
    public static class RequesterId
    {
        public static string Create()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            Process process = Process.GetCurrentProcess();
            string userName = identity == null ? "" : identity.Name.Substring(identity.Name.IndexOf(@"\", System.StringComparison.Ordinal) + 1);
            string hostName = Dns.GetHostName();
            return string.Format("{0}-{1}-{2}-{3}", process.ProcessName, hostName, userName, process.Id);
        }
    }
}