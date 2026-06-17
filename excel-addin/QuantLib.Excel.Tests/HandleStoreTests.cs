using Xunit;
using QuantLib.Excel.Core;

namespace QuantLib.Excel.Tests
{
    public class HandleStoreConcurrencyTests
    {
        [Fact]
        public void TestConcurrentCreationAndRetrieval()
        {
            HandleStore.Clear();
            var handles = new List<string>();
            var lockObj = new object();

            var tasks = new Task[8];
            for (int t = 0; t < 8; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var handle = HandleStore.Create(new TestObject { Value = i });
                        lock (lockObj)
                            handles.Add(handle);
                    }
                });
            }
            System.Threading.Tasks.Task.WaitAll(tasks);

            Assert.Equal(800, handles.Count);
            Assert.Equal(800, HandleStore.Count);

            foreach (var handle in handles)
            {
                var obj = HandleStore.GetObject(handle);
                Assert.NotNull(obj);
                Assert.IsType<TestObject>(obj);
            }
        }

        [Fact]
        public void TestHandleStoreTypeConversionSafety()
        {
            HandleStore.Clear();
            var handle1 = HandleStore.Create(new TestObject { Value = 1 });
            var handle2 = HandleStore.Create("test-string");

            var obj1 = HandleStore.Get<TestObject>(handle1);
            Assert.Equal(1, obj1.Value);

            var str = HandleStore.Get<string>(handle2);
            Assert.Equal("test-string", str);

            Assert.Throws<InvalidCastException>(() => HandleStore.Get<TestObject>(handle2));
        }

        private class TestObject
        {
            public int Value { get; set; }
        }
    }

    public class SkeletonUdfTests
    {
        [Fact]
        public void TestHelloCoreReturnsValidMessage()
        {
            var result = QuantLib.Excel.Udf.SkeletonUdfs.HelloCore();

            Assert.IsType<string>(result);
            var msg = result.ToString();
            Assert.Contains("QuantLib Excel Add-in", msg);
        }

        [Fact]
        public void TestBuildDICurveCreatesHandle()
        {
            HandleStore.Clear();
            var rates = new[] { 0.98, 0.96, 0.94 };
            var tenors = new[] { 1.0, 2.0, 3.0 };

            var result = QuantLib.Excel.Udf.SkeletonUdfs.BuildDICurve(rates, tenors);

            Assert.IsType<string>(result);
            var handle = result.ToString();
            Assert.False(handle!.StartsWith("ERROR"));
            Assert.True(HandleStore.Exists(handle));
        }

        [Fact]
        public void TestGetCurveRateAcceptsHandle()
        {
            HandleStore.Clear();
            var rates = new[] { 0.98, 0.96, 0.94 };
            var tenors = new[] { 1.0, 2.0, 3.0 };
            var handleResult = QuantLib.Excel.Udf.SkeletonUdfs.BuildDICurve(rates, tenors);
            var handle = handleResult.ToString()!;

            var result = QuantLib.Excel.Udf.SkeletonUdfs.GetCurveRate(handle, 1.5);

            Assert.IsType<double>(result);
            var rate = (double)result;
            Assert.InRange(rate, 0.90, 0.99);
        }
    }
}
