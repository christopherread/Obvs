using System;

namespace Obvs.Logging
{
    public interface ILogger
    {
        void Debug(string message, Exception exception = null);
        void Info(string message, Exception exception = null);
        void Warn(string message, Exception exception = null);
        void Error(string message, Exception exception = null);
    }

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
    }
}