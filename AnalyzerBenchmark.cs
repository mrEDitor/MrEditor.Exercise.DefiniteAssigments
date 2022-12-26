using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Xunit;

namespace MrEditor.Exercise.DefiniteAssigments
{
    public class AnalyzerBenchmark
    {
        // use `dotnet run --configuration=release`
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<Benchmarks>();
            System.Console.WriteLine(summary.ResultsDirectoryPath);
        }

        public class Benchmarks
        {
            [Benchmark]
            public void DoBenchmark10() => DoBenchmark(10);

            [Benchmark]
            public void DoBenchmark100() => DoBenchmark(100);

            [Benchmark]
            public void DoBenchmark1000() => DoBenchmark(1000);

            [Benchmark]
            public void DoBenchmark5000() => DoBenchmark(5000);

            [Benchmark]
            public void DoBenchmark10000() => DoBenchmark(10000);

            [Benchmark]
            public void DoBenchmark50000() => DoBenchmark(50000);

            public void DoBenchmark(int functionsCount)
            {
                var program = new Program()
                {
                    new VariableDeclaration("foo"),
                    new AssignVariable("foo"),
                };
                for (int i = 0; i < functionsCount; ++i)
                {
                    program.Add(new FunctionDeclaration("Bar" + i)
                    {
                        Body =
                        {
                            new PrintVariable("foo"),
                        }
                    });
                    program.Add(
                        new Invocation("Bar" + i, isConditional: false)
                    );
                };

                var analyzer = new Analyzer();
                Assert.Empty(analyzer.Analyze(program));
            }
        }
    }
}