using ExcelDna.Integration;
using QuantLib.Excel.Core;

namespace QuantLib.Excel.Udf
{
    public static class SkeletonUdfs
    {
        private static object SafeExecute(System.Func<object?> func, string name = "UDF")
        {
            try
            {
                return func() ?? "";
            }
            catch (Exception ex)
            {
                Logger.Error($"{name}: {ex.Message}", ex);
                return $"ERROR: {ex.Message}";
            }
        }

        private static object SafeExecuteHandle(System.Func<string> func, string name = "UDF")
        {
            try
            {
                var handle = func();
                Logger.Info($"Created handle in {name}: {handle}");
                return handle;
            }
            catch (Exception ex)
            {
                Logger.Error($"{name}: {ex.Message}", ex);
                return $"ERROR: {ex.Message}";
            }
        }

        [ExcelFunction(Name = "QL_HelloCore", Description = "Returns a hello message", Category = "QuantLib")]
        public static object HelloCore()
        {
            return SafeExecute(() =>
            {
                Logger.Info("QL_HelloCore called");
                return "Hello from QuantLib Excel Add-in! Foundation active.";
            }, "QL_HelloCore");
        }

        [ExcelFunction(Name = "QL_BuildDICurve", Description = "Build a DI/CDI yield curve", Category = "QuantLib | Curves")]
        public static object BuildDICurve(
            [ExcelArgument(Description = "Array of discount factors")] double[] rates,
            [ExcelArgument(Description = "Array of tenors (years)")] double[] tenors)
        {
            return SafeExecuteHandle(() =>
            {
                if (rates == null || tenors == null)
                    throw new ArgumentNullException("rates and tenors cannot be null");
                if (rates.Length != tenors.Length)
                    throw new ArgumentException("rates and tenors must have same length");
                if (rates.Length == 0)
                    throw new ArgumentException("arrays cannot be empty");

                var curve = new SimpleCurve { Rates = rates, Tenors = tenors };
                return HandleStore.Create(curve);
            }, "QL_BuildDICurve");
        }

        [ExcelFunction(Name = "QL_GetCurveRate", Description = "Get discount factor from curve", Category = "QuantLib | Curves")]
        public static object GetCurveRate(
            [ExcelArgument(Description = "Curve handle")] string curveHandle,
            [ExcelArgument(Description = "Time in years")] double time)
        {
            return SafeExecute(() =>
            {
                var curve = HandleStore.Get<SimpleCurve>(curveHandle);
                var df = Interpolate(curve.Tenors, curve.Rates, time);
                Logger.Info($"Retrieved rate: DF({time}y) = {df}");
                return df;
            }, "QL_GetCurveRate");
        }

        [ExcelFunction(Name = "QL_BuildVolSurface", Description = "Build a volatility surface", Category = "QuantLib | Surfaces")]
        public static object BuildVolSurface(
            [ExcelArgument(Description = "Array of strikes")] double[] strikes,
            [ExcelArgument(Description = "Array of tenors")] double[] tenors,
            [ExcelArgument(Description = "2D array of volatilities")] double[] vols)
        {
            return SafeExecuteHandle(() =>
            {
                if (strikes == null || tenors == null || vols == null)
                    throw new ArgumentNullException("All arrays must be non-null");
                if (strikes.Length * tenors.Length != vols.Length)
                    throw new ArgumentException("vols size must equal strikes.Length × tenors.Length");

                var surface = new SimpleSurface { Strikes = strikes, Tenors = tenors, Vols = vols };
                return HandleStore.Create(surface);
            }, "QL_BuildVolSurface");
        }

        [ExcelFunction(Name = "QL_GetVolatility", Description = "Get volatility from surface", Category = "QuantLib | Surfaces")]
        public static object GetVolatility(
            [ExcelArgument(Description = "Vol surface handle")] string volHandle,
            [ExcelArgument(Description = "Strike price")] double strike,
            [ExcelArgument(Description = "Tenor in years")] double tenor)
        {
            return SafeExecute(() =>
            {
                var surface = HandleStore.Get<SimpleSurface>(volHandle);
                var vol = BilinearInterpolate(surface.Strikes, surface.Tenors, surface.Vols, strike, tenor);
                Logger.Info($"Retrieved vol: Vol({strike}, {tenor}y) = {vol}");
                return vol;
            }, "QL_GetVolatility");
        }

        private static double Interpolate(double[] x, double[] y, double xi)
        {
            if (xi <= x[0]) return y[0];
            if (xi >= x[x.Length - 1]) return y[y.Length - 1];

            for (int i = 0; i < x.Length - 1; i++)
            {
                if (xi >= x[i] && xi <= x[i + 1])
                {
                    double t = (xi - x[i]) / (x[i + 1] - x[i]);
                    return y[i] + t * (y[i + 1] - y[i]);
                }
            }
            return y[y.Length - 1];
        }

        private static double BilinearInterpolate(double[] strikes, double[] tenors, double[] vols, double strike, double tenor)
        {
            int si = 0, ti = 0;
            for (int i = 0; i < strikes.Length - 1; i++)
                if (strike >= strikes[i] && strike <= strikes[i + 1]) si = i;
            for (int i = 0; i < tenors.Length - 1; i++)
                if (tenor >= tenors[i] && tenor <= tenors[i + 1]) ti = i;

            si = System.Math.Min(si, strikes.Length - 2);
            ti = System.Math.Min(ti, tenors.Length - 2);

            double sx = (strike - strikes[si]) / (strikes[si + 1] - strikes[si]);
            double ty = (tenor - tenors[ti]) / (tenors[ti + 1] - tenors[ti]);

            int idx00 = ti * strikes.Length + si;
            int idx10 = ti * strikes.Length + si + 1;
            int idx01 = (ti + 1) * strikes.Length + si;
            int idx11 = (ti + 1) * strikes.Length + si + 1;

            double v0 = vols[idx00] + sx * (vols[idx10] - vols[idx00]);
            double v1 = vols[idx01] + sx * (vols[idx11] - vols[idx01]);

            return v0 + ty * (v1 - v0);
        }

        private class SimpleCurve
        {
            public double[] Rates { get; set; } = System.Array.Empty<double>();
            public double[] Tenors { get; set; } = System.Array.Empty<double>();
        }

        private class SimpleSurface
        {
            public double[] Strikes { get; set; } = System.Array.Empty<double>();
            public double[] Tenors { get; set; } = System.Array.Empty<double>();
            public double[] Vols { get; set; } = System.Array.Empty<double>();
        }
    }
}
