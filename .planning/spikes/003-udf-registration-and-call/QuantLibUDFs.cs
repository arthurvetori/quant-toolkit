using ExcelDna.Integration;
using System;

namespace ExcelQuantLib
{
    /// <summary>
    /// Integration test UDFs combining ExcelDNA + QuantLib
    /// These UDFs demonstrate the full stack working end-to-end
    /// </summary>
    public static class QuantLibUDFs
    {
        /// <summary>
        /// Baseline UDF: Pure .NET Core, no QuantLib dependency
        /// Tests if ExcelDNA registration works at all
        /// </summary>
        [ExcelFunction(
            Name = "QL_HelloCore",
            Description = "Sanity check: confirm .NET Core UDF can run in Excel",
            Category = "QuantLib"
        )]
        public static string HelloCore()
        {
            return "Hello from .NET Core ExcelDNA";
        }

        /// <summary>
        /// Test QuantLib Date instantiation from Excel
        /// This is the critical validation: can we create QuantLib objects?
        /// </summary>
        [ExcelFunction(
            Name = "QL_TodayDate",
            Description = "Create a QuantLib Date for today",
            Category = "QuantLib"
        )]
        public static string TodayDate()
        {
            try
            {
                // In real implementation:
                // var today = new QuantLib.Date(new DateTime.Now);
                // or
                // var today = QuantLib.Date.todaysDate();
                // return today.ToString(); // e.g., "June 16, 2026"
                
                // For now, mock:
                return $"Date: {DateTime.Now:MMM dd, yyyy}";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Test QuantLib date arithmetic through Excel
        /// </summary>
        [ExcelFunction(
            Name = "QL_AddDays",
            Description = "Add days to a date using QuantLib",
            Category = "QuantLib"
        )]
        public static object AddDays(double days)
        {
            try
            {
                if (days < 0 || days > 100000)
                    return "ERROR: Days must be between 0 and 100000";

                // In real implementation:
                // var today = QuantLib.Date.todaysDate();
                // var future = today + (int)days;
                // return future.ToString();
                
                // Mock:
                var result = DateTime.Now.AddDays(days);
                return result.ToString("MMM dd, yyyy");
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Test QuantLib yield curve creation and queries
        /// More complex: demonstrates Handle pattern working through Excel boundary
        /// </summary>
        [ExcelFunction(
            Name = "QL_FlatCurveRate",
            Description = "Query rate from a flat yield curve",
            Category = "QuantLib"
        )]
        public static object FlatCurveRate(double rate, double yearsForward)
        {
            try
            {
                if (rate < -0.05 || rate > 1.0)
                    return "ERROR: Rate must be between -5% and 100%";
                
                if (yearsForward <= 0 || yearsForward > 50)
                    return "ERROR: Years must be between 0 and 50";

                // In real implementation:
                // var curve = new QuantLib.FlatForwardCurve(
                //     QuantLib.Date.todaysDate(),
                //     new QuantLib.Handle<QuantLib.YieldTermStructure>(
                //         new QuantLib.SimpleQuote(rate)
                //     )
                // );
                // var discountFactor = curve.discount(yearsForward);
                // return discountFactor;
                
                // Mock: discount factor = exp(-rate * time)
                var discountFactor = Math.Exp(-rate * yearsForward);
                return Math.Round(discountFactor, 6);
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Test concurrent UDF calls (via multiple cells)
        /// Each call should maintain its own QuantLib object state
        /// </summary>
        [ExcelFunction(
            Name = "QL_StatefulComputation",
            Description = "Test stateful computation with unique IDs",
            Category = "QuantLib"
        )]
        public static object StatefulComputation(string uniqueId)
        {
            try
            {
                // In real implementation, this would use the Handle store
                // Each unique ID would retrieve a persisted QuantLib object
                // from a static cache
                
                // Mock:
                return $"Computed for ID: {uniqueId} at {DateTime.Now:HH:mm:ss.fff}";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }
    }
}
