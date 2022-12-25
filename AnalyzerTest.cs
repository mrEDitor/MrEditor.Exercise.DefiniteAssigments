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

        public void TestDeclarationFirst()
        {
            var program = new Program()
            {
                /*
                 *  Bar();
                 *  
                 *  func Bar() {
                 *  }
                 */
                new Invocation("Bar", isConditional: false),
                new FunctionDeclaration("Bar"),
            };
            Assert.Empty(_analyzer.Analyze(program));
        }

        [Fact]
        public void TestRecursiveFunc()
        {
            var program = new Program()
            {
                /*
                 *  Bar();
                 *  
                 *  func Bar() {
                 *    Bar();
                 *  }
                 */
                new Invocation("Bar", isConditional: false),
                new FunctionDeclaration("Bar")
                {
                    Body = 
                    {
                        new Invocation("Bar", isConditional: true),
                    },
                },
            };
            Assert.Empty(_analyzer.Analyze(program));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestMultiRecursiveFunc(bool isConditional)
        {
            var program = new Program()
            {
                /*
                 *  Bar1();
                 *  Bar2();
                 *  
                 *  func Bar1() {
                 *    Bar2();
                 *  }
                 *  func Bar2() {
                 *    Bar2();
                 *  }
                 */
                new Invocation("Bar1", isConditional: false),
                new FunctionDeclaration("Bar1")
                {
                    Body = 
                    {
                        new Invocation("Bar2", isConditional),
                    },
                },
                new FunctionDeclaration("Bar2")
                {
                    Body = 
                    {
                        new Invocation("Bar2", isConditional: true),
                    },
                },
            };
            Assert.Empty(_analyzer.Analyze(program));
        }

        #endregion

        #region Incorrect samples

        [Fact]
        public void TestAssigningUnknown()
        {
            var program = new Program
            {
                /*
                 *  foo = ...; // not OK, not declared yet
                 */
                new AssignVariable("foo"),
            };
            Assert.Equal(
                new[] { new Problem(Problem.VARIABLE_NOT_DECLARED, "foo") },
                _analyzer.Analyze(program)
            );
        }

        [Fact]
        public void TestPrintingUnknown()
        {
            var program = new Program
            {
                /*
                 *  print(foo); // not OK, not declared yet
                 */
                new PrintVariable("foo"),
            };
            Assert.Equal(
                new[] { new Problem(Problem.VARIABLE_NOT_DECLARED, "foo") },
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
                 *  print(foo); // not OK, not assigned yet
                 */
                new VariableDeclaration("foo"),
                new PrintVariable("foo"),
            };
            Assert.Equal(
                new[] { new Problem(Problem.VARIABLE_NOT_ASSIGNED, "foo") },
                _analyzer.Analyze(program)
            );
        }

        [Fact]
        public void TestAlreadyDeclared()
        {
            var program = new Program
            {
                /*
                 *  var foo;
                 *  foo() {} // not OK, already declared
                 */
                new VariableDeclaration("foo"),
                new FunctionDeclaration("foo"),
            };
            Assert.Equal(
                new[] { new Problem(Problem.SYMBOL_NAME_ALREADY_EXISTS, "foo") },
                _analyzer.Analyze(program)
            );
        }

        #endregion
    }
}
