namespace Quant.Infrastructure.Tests.Support;

internal sealed class TemporaryDirectory : IDisposable
{
    internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"quant-tests-{Guid.NewGuid():N}");

    internal TemporaryDirectory() => Directory.CreateDirectory(Path);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
