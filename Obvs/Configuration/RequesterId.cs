using System.Diagnostics;
using System.Net;

namespace Obvs.Configuration
{
    /// <summary>Identifies this process as the sender of requests, so responses find their way back to it.</summary>
    public static class RequesterId
    {
        /// <summary>An ID made of the process's name and ID, the host's name and, on .NET Framework, the user's name.</summary>
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