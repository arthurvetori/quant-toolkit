using ExcelDna.Integration;

namespace TestExcelDNA
{
    public static class BasicUDFs
    {
        /// <summary>
        /// Simple hello-world UDF to test ExcelDNA registration in .NET Core
        /// </summary>
        [ExcelFunction(Description = "Hello from .NET Core ExcelDNA")]
        public static string HelloCore()
        {
            return "Hello from .NET Core!";
        }

        /// <summary>
        /// Simple echo function that demonstrates basic parameter passing
        /// </summary>
        [ExcelFunction(Description = "Echo back the input value")]
        public static object Echo(object input)
        {
            return input ?? "null";
        }

        /// <summary>
        /// Add two numbers - tests numeric computation
        /// </summary>
        [ExcelFunction(Description = "Add two numbers")]
        public static double Add(double a, double b)
        {
            return a + b;
        }
    }
}
