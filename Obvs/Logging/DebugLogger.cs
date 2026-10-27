using System;

// v6's logging, kept for v6 apps and replaced in v7 by Microsoft.Extensions.Logging: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.Logging
{
    public class DebugLogger : ILogger
    {
        private readonly string _name;

        public DebugLogger(string name)
        {
            _name = name;
        }

        public void Debug(string message, Exception exception = null)
        {
            System.Diagnostics.Debug.WriteLine(message + (exception == null ? "" : Environment.NewLine + exception), _name + ".Debug");
        }

        public void Info(string message, Exception exception = null)
        {
            System.Diagnostics.Debug.WriteLine(message + (exception == null ? "" : Environment.NewLine + exception), _name + ".Info");
        }

        public void Warn(string message, Exception exception = null)
        {
            System.Diagnostics.Debug.WriteLine(message + (exception == null ? "" : Environment.NewLine + exception), _name + ".Warn");
        }

        public void Error(string message, Exception exception = null)
        {
            System.Diagnostics.Debug.WriteLine(message + (exception == null ? "" : Environment.NewLine + exception), _name + ".Error");
        }

        public void Log(LogLevel level, string message, Exception exception = null)
        {
            System.Diagnostics.Debug.WriteLine(message + (exception == null ? "" : Environment.NewLine + exception), _name + "." + level);
        }
    }
}