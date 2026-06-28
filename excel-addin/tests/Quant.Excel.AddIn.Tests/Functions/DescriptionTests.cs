using System.Reflection;
using ExcelDna.Integration;
using Quant.Excel.AddIn.Functions.Information;
using Xunit;

namespace Quant.Excel.AddIn.Tests.Functions;

public sealed class DescriptionTests
{
    [Fact]
    public void EveryExportHasCompleteFunctionAndArgumentHelp()
    {
        var exportedMethods = typeof(DiscoveryFunctions).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(method => (Method: method, Function: method.GetCustomAttribute<ExcelFunctionAttribute>()))
            .Where(item => item.Function is not null)
            .ToArray();

        Assert.NotEmpty(exportedMethods);

        foreach (var (method, function) in exportedMethods)
        {
            Assert.False(string.IsNullOrWhiteSpace(function!.Name));
            Assert.False(string.IsNullOrWhiteSpace(function.Description));

            foreach (var parameter in method.GetParameters())
            {
                var argument = parameter.GetCustomAttribute<ExcelArgumentAttribute>();
                Assert.NotNull(argument);
                Assert.False(string.IsNullOrWhiteSpace(argument!.Name));
                Assert.False(string.IsNullOrWhiteSpace(argument.Description));
            }
        }
    }

    [Fact]
    public void EveryCommandHasANonEmptyDescription()
    {
        var exportedCommands = typeof(DiscoveryFunctions).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(method => method.GetCustomAttribute<ExcelCommandAttribute>())
            .Where(command => command is not null)
            .ToArray();

        Assert.NotEmpty(exportedCommands);

        foreach (var command in exportedCommands)
        {
            Assert.False(string.IsNullOrWhiteSpace(command!.Name));
            Assert.False(string.IsNullOrWhiteSpace(command.Description));
        }
    }
}
