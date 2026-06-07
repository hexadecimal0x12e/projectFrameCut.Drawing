using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance
    .AddDiagnoser(MemoryDiagnoser.Default)
    .AddColumnProvider(DefaultColumnProviders.Statistics)
    .WithOptions(ConfigOptions.DisableOptimizationsValidator)
    .AddJob(Job.Default.WithPowerPlan(PowerPlan.UltimatePerformance))
    .WithArtifactsPath(Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkResults"));

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
