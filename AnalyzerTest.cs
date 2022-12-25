using Xunit;

namespace MrEditor.Exercise.DefiniteAssigments
{
    public class AnalyzerTest
    {
        private Analyzer _analyzer = new();

#region correct samples
        [Fact]
        public void TestSimpleVariableUsage()
        {
            Assert.Empty(_analyzer.Analyze(CorrectExamples.SimpleVariableUsage));
        }

        [Fact]
        public void TestLocalFunctionInit()
        {
            Assert.Empty(_analyzer.Analyze(CorrectExamples.LocalFunctionInit));
        }

        [Fact]
        public void TestEmptyProgram()
        {
            var program = new Program();
            Assert.Empty(_analyzer.Analyze(program));
        }
#endregion

        [Fact]
        public void TestAssigningUnknown()
        {
            var program = new Program
            {
                /*
                 *  foo = ...;
                 */
                new AssignVariable("foo"),
            };
            Assert.Equal(
                new [] { new Problem("Unknown variable", "foo") },
                _analyzer.Analyze(program)
            );
        }

        [Fact]
        public void TestPrintingUnknown()
        {
            var program = new Program
            {
                /*
                 *  print(foo);
                 */
                new PrintVariable("foo"),
            };
            Assert.Equal(
                new [] { new Problem("Unknown variable", "foo") },
                _analyzer.Analyze(program)
            );
        }
        [Fact]
        public void TestUnassignedUnknown()
        {
            var program = new Program
            {
                /*
                 *  var foo;
                 *  print(foo);
                 */
                new VariableDeclaration("foo"),
                new PrintVariable("foo"),
            };
            Assert.Equal(
                new [] { new Problem("Unassigned variable", "foo") },
                _analyzer.Analyze(program)
            );
        }
    }
}
