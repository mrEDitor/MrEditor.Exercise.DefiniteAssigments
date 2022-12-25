using System.Linq;
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

        [Fact]
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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestRecursiveFunc(bool isConditional)
        {
            // isConditional=false gurantees endless recursion
            var program = new Program()
            {
                /*
                 *  Bar();
                 *  
                 *  func Bar() {
                 *    if (!isConditional || ...) {
                 *      Bar();
                 *    }
                 *  }
                 */
                new Invocation("Bar", isConditional: false),
                new FunctionDeclaration("Bar")
                {
                    Body =
                    {
                        new Invocation("Bar", isConditional),
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
                 *    if (!isConditional || ...) {
                 *      Bar2();
                 *    }
                 *  }
                 *  func Bar2() {
                 *    if (...) {
                 *      Bar2();
                 *    }
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
        public void TestAssignCapturedFirst()
        {
            var program = new Program()
            {
                /*
                 *  var foo;
                 *  Bar();
                 *
                 *  foo = ...;
                 *  func Bar() {
                 *    print(foo);
                 *  }
                 */
                new VariableDeclaration("foo"),
                new Invocation("Bar", isConditional: false),
                new AssignVariable("foo"),
                new FunctionDeclaration("Bar")
                {
                    Body =
                    {
                        new PrintVariable("foo"),
                    }
                },
            };
            Assert.Equal(
                new[] { new Problem(Problem.VARIABLE_NOT_ASSIGNED, "foo") },
                _analyzer.Analyze(program)
            );
        }

        // TODO: tricky one, should it be a problem at all?
        [Fact]
        public void TestDeclareCapturedFirst()
        {
            var program = new Program()
            {
                /*
                 *  Bar();
                 *
                 *  var foo;
                 *  func Bar() {
                 *    foo = ...;
                 *    print(foo);
                 *  }
                 */
                new Invocation("Bar", isConditional: false),
                new VariableDeclaration("foo"),
                new FunctionDeclaration("Bar")
                {
                    Body =
                    {
                        new AssignVariable("foo"),
                        new PrintVariable("foo"),
                    }
                },
            };
            Assert.Equal(
                new[] { new Problem(Problem.USED_THEN_DECLARED, "foo") },
                _analyzer.Analyze(program)
            );
        }

        [Fact]
        public void TestNestedFunction()
        {
            var program = new Program()
            {
                /*
                 *  Bar();
                 *
                 *  func Bar() {
                 *    Foo();
                 *    func Foo() {}
                 *  }
                 */
                new Invocation("Bar", isConditional: false),
                new FunctionDeclaration("Bar")
                {
                    Body =
                    {
                        new Invocation("Foo", isConditional: false),
                        new FunctionDeclaration("Foo")
                    }
                },
            };
            Assert.Equal(
                new[] { new Problem(Problem.NESTED_FUNCTION, "Foo") },
                _analyzer.Analyze(program)
            );
        }

        // TODO: if we allow nested functions, seems like we should also deny Bar{Bar} case.
        [Fact]
        public void TestHidenByNestedFunction()
        {
            var program = new Program()
            {
                /*
                 *  Bar();
                 *
                 *  func Bar() {
                 *    Foo();
                 *    func Foo() {}
                 *  }
                 */
                new Invocation("Bar", isConditional: false),
                new FunctionDeclaration("Bar")
                {
                    Body =
                    {
                        new Invocation("Bar", isConditional: false),
                        new FunctionDeclaration("Bar")
                    }
                },
            };
            Assert.Equal(
                new[]
                {
                    new Problem(Problem.NESTED_FUNCTION, "Bar"),
                    new Problem(Problem.ALREADY_DECLARED, "Bar"),
                },
                _analyzer.Analyze(program)
            );
        }

        #endregion

        // TODO: there are some tricky cases with nested, especially not-called ones
        [Theory]
        [InlineData("a", "b", "c", "d", 1)] // none, success test
        [InlineData("a", "foo", "foo", "d")] // var+var
        [InlineData("foo", "foo", "c", "d")] // func+var
        [InlineData("a", "b", "foo", "foo")] // var+func
        [InlineData("foo", "b", "c", "foo")] // func+func
        // [InlineData("foo", "b", "c", "d", 2, "foo", "foo")] // func+nested (called)
        // [InlineData("foo", "b", "c", "d", 1, "foo", "b")] // func+nested (not called => no problem)
        // [InlineData("a", "b", "c", "foo", 2, "foo", "foo")] // func+it's nested (called => no problem)
        // [InlineData("a", "b", "c", "foo", 1, "foo", "b")] // func+it's nested (not called)
        // [InlineData("a", "b", "foo", "d", "foo")] // var+nested
        // [InlineData("foo", "foo", "foo", "foo", "i", "i", 4)] // many similar declarations (nested not called)
        // [InlineData("foo", "foo", "foo", "foo", "foo", "foo", 5)] // many similar declarations (nested called)
        public void TestAlreadyDeclared(
            string a,
            string b,
            string c,
            string d,
            int fooCount = 2
            // string h = "i",
            // string i = "i"
        )
        {
            var program = new Program
            {
                /* // assume we rename some of these to foo
                 *  var a;
                 *  var b;
                 *  c() {}
                 *  d() {}
                 */
                new FunctionDeclaration(a),
                new VariableDeclaration(b),
                new VariableDeclaration(c),
                new FunctionDeclaration(d)
                // {
                //     Body =
                //     {
                //         new FunctionDeclaration(h),
                //         new Invocation(i, isConditional: true),
                //     },
                // },
            };

            var problems = _analyzer.Analyze(program).ToList();
            Assert.Equal(fooCount - 1, problems.Count);
            Assert.Equal(
                Enumerable.Repeat(
                    new Problem(Problem.ALREADY_DECLARED, "foo"),
                    fooCount - 1
                ),
                problems
            );
        }
    }
}
