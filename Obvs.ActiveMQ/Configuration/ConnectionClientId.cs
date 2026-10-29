using System;
using System.Diagnostics;
using System.Net;
using System.Security.Principal;

// v6's ActiveMQ transport building blocks, which UseActiveMq(...) is built on: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.ActiveMQ.Configuration
{
    public static class ConnectionClientId
    {
        public static string CreateWithSuffix(string uniqueSuffix)
        {
#if NETFRAMEWORK
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            string userName = identity == null ? "" : identity.Name.Substring(identity.Name.IndexOf(@"\", System.StringComparison.Ordinal) + 1);
#else
            string userName = Guid.NewGuid().ToString();
#endif
            Process process = Process.GetCurrentProcess();
            string hostName = Dns.GetHostName();
            return string.Format("{0}-{1}-{2}-{3}-{4}", process.ProcessName, hostName, userName, process.Id, uniqueSuffix);
        }
    }
}
