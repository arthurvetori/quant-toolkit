using System;
using System.Collections.Generic;

namespace QuantLib.Excel.Core
{
    /// <summary>
    /// Error handling wrapper for UDF execution.
    /// Catches exceptions and returns user-friendly error messages or Excel error codes.
    /// </summary>
    public static class SafeExecute
    {
        public class ExecutionResult
        {
            public bool Success { get; set; }
            public object? Result { get; set; }
            public Exception? Exception { get; set; }
            public string? ErrorMessage { get; set; }
        }

        /// <summary>
        /// Executes a function with uniform error handling.
        /// Returns ExecutionResult for analysis.
        /// </summary>
        public static ExecutionResult Execute(Func<object?> func, string functionName)
        {
            try
            {
                if (func == null)
                    throw new ArgumentNullException(nameof(func));

                var result = func();
                return new ExecutionResult { Success = true, Result = result };
            }
            catch (KeyNotFoundException ex)
            {
                var msg = $"Invalid handle: {ex.Message}";
                Logger.Error($"{functionName}: {msg}", ex);
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = msg,
                    Exception = ex
                };
            }
            catch (InvalidCastException ex)
            {
                var msg = $"Type error: {ex.Message}";
                Logger.Error($"{functionName}: {msg}", ex);
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = msg,
                    Exception = ex
                };
            }
            catch (ArgumentException ex)
            {
                var msg = $"Invalid argument: {ex.Message}";
                Logger.Error($"{functionName}: {msg}", ex);
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = msg,
                    Exception = ex
                };
            }
            catch (Exception ex)
            {
                var msg = $"Unexpected error: {ex.GetType().Name}: {ex.Message}";
                Logger.Error($"{functionName}: {msg}", ex);
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = msg,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Executes a generic function with error handling.
        /// </summary>
        public static ExecutionResult Execute<T>(Func<T> func, string functionName)
        {
            try
            {
                if (func == null)
                    throw new ArgumentNullException(nameof(func));

                var result = func();
                return new ExecutionResult { Success = true, Result = result };
            }
            catch (Exception ex)
            {
                Logger.Error($"{functionName}: Exception: {ex.Message}", ex);
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Converts ExecutionResult to an Excel-compatible return value.
        /// </summary>
        public static object ToExcelResult(ExecutionResult result)
        {
            if (result.Success)
                return result.Result ?? "";

            // Return error message as string prefixed with ERROR:
            return $"ERROR: {result.ErrorMessage}";
        }
    }
}
