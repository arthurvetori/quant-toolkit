using System;
using System.IO;
using System.Text;

namespace QuantLib.Excel.Core
{
    /// <summary>
    /// Simple file-based logger for debugging and error tracking.
    /// Logs to .planning/logs/excel-addin.log
    /// </summary>
    public static class Logger
    {
        private static readonly string _logPath;
        private static readonly object _lockObject = new object();

        static Logger()
        {
            // Derive log path: .planning/logs/
            var projectRoot = FindProjectRoot();
            var logsDir = Path.Combine(projectRoot, ".planning", "logs");
            
            if (!Directory.Exists(logsDir))
                Directory.CreateDirectory(logsDir);

            _logPath = Path.Combine(logsDir, "excel-addin.log");
        }

        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public static void Warning(string message)
        {
            WriteLog("WARN", message);
        }

        public static void Error(string message, Exception? ex = null)
        {
            var fullMessage = ex == null 
                ? message 
                : $"{message}\nException: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            
            WriteLog("ERROR", fullMessage);
        }

        private static void WriteLog(string level, string message)
        {
            lock (_lockObject)
            {
                try
                {
                    var logEntry = new StringBuilder();
                    logEntry.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] ");
                    logEntry.Append($"[{level}] ");
                    logEntry.Append(message);

                    File.AppendAllText(_logPath, logEntry.ToString() + Environment.NewLine);
                }
                catch
                {
                    // Silently fail if logging fails
                }
            }
        }

        private static string FindProjectRoot()
        {
            // Start from current directory and traverse up until we find .git or .planning
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (currentDir != null)
            {
                if (Directory.Exists(Path.Combine(currentDir.FullName, ".git")) ||
                    Directory.Exists(Path.Combine(currentDir.FullName, ".planning")))
                {
                    return currentDir.FullName;
                }

                currentDir = currentDir.Parent;
            }

            // Fallback to current directory
            return Directory.GetCurrentDirectory();
        }

        public static string GetLogPath() => _logPath;
    }
}
