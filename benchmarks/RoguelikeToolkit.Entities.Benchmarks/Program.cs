using BenchmarkDotNet.Running;

namespace RoguelikeToolkit.Entities.Benchmarks;

/// <summary>
/// Benchmark entry point. Run in Release: dotnet run --project benchmarks/RoguelikeToolkit.Entities.Benchmarks -c Release.
/// Supports standard BenchmarkDotNet args, e.g. --filter *Spawn* --job short for a smoke run.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the selected benchmarks.
    /// </summary>
    /// <param name="args">BenchmarkDotNet arguments.</param>
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
