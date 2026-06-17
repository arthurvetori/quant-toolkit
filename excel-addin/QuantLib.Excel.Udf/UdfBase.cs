using System;
using System.Reflection;
using System.Collections.Generic;

namespace QuantLib.Excel.Udf
{
    /// <summary>
    /// Base class for QuantLib Excel UDFs.
    /// Provides common patterns: error handling, logging, handle management.
    /// </summary>
    public abstract class UdfBase
    {
        /// <summary>
        /// Safe execution wrapper for UDFs.
        /// Catches exceptions and returns user-friendly error messages.
        /// </summary>
        protected static object SafeExecute(Func<object?> func)
        {
            try
            {
                return func() ?? "";
            }
            catch (Exception ex)
            {
                Core.Logger.Error($"UDF Error in {GetCallerName()}: {ex.Message}", ex);
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Safe execution wrapper for UDFs that return strings (handles).
        /// </summary>
        protected static object SafeExecuteHandle(Func<string> func)
        {
            try
            {
                var handle = func();
                Core.Logger.Info($"Created handle: {handle} in {GetCallerName()}");
                return handle;
            }
            catch (Exception ex)
            {
                Core.Logger.Error($"Handle creation failed in {GetCallerName()}: {ex.Message}", ex);
                return $"ERROR: {ex.Message}";
            }
        }

        private static string GetCallerName()
        {
            var frame = new System.Diagnostics.StackFrame(2);
            return frame?.GetMethod()?.Name ?? "Unknown";
        }
    }

    /// <summary>
    /// Registry and manager for UDFs.
    /// </summary>
    public static class UdfRegistry
    {
        private static readonly List<(string Name, Type Type)> _registeredUdfs 
            = new List<(string, Type)>();

        public static void Register(string name, Type udfType)
        {
            _registeredUdfs.Add((name, udfType));
            Core.Logger.Info($"Registered UDF: {name} ({udfType.Name})");
        }

        public static IReadOnlyList<(string Name, Type Type)> GetRegisteredUdfs()
            => _registeredUdfs.AsReadOnly();

        public static void LogRegistration()
        {
            Core.Logger.Info($"Total UDFs registered: {_registeredUdfs.Count}");
            foreach (var (name, type) in _registeredUdfs)
            {
                Core.Logger.Info($"  - {name}: {type.FullName}");
            }
        }
    }
}
