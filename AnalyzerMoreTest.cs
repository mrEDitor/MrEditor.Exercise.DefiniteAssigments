using Xunit;

namespace MrEditor.Exercise.DefiniteAssigments
{
    public class AnalyzerMoreTest
    {
        private Analyzer _analyzer = new();

        [Fact]
        public void Unreachable()
        {
            // Во время исполнения программы невозможны попытки вывода значения переменной на экран если по мере исполнения программы...
            // Тут исполнение до 'print(a)' никогда не доберётся из-за безусловной рекурсии
            var program = new Program()
            {
                /*
                    var a;
                    func Recursive() {
                        Recursive();
                    }
                    Recursive();
                    print(a);
                */
                new VariableDeclaration("a"),
                new FunctionDeclaration("Recursive")
                {
                    Body =
                    {
                        new Invocation("Recursive", isConditional: false)
                    }
                },
                new Invocation("Recursive", isConditional: false),
                new PrintVariable("a")
            };

            var errors = _analyzer.Analyze(program);
            Assert.Empty(errors);
        }

        [Fact]
        public void UnreachableVariableHiddenByFunctionName()
        {
            // Внутри локальных функций не должно быть деклараций переменных или локальных функций с именами,
            // совпадающими с именами переменных или функций из внешних контекстов (запрещено "сокрытие имен").
            var program = new Program
            {
                new VariableDeclaration("X"),
                new FunctionDeclaration("F") // unused function, not analysed
                {
                    Body =
                    {
                        new FunctionDeclaration("X")
                    }
                }, 
            };

            var errors = _analyzer.Analyze(program);
            Assert.Empty(errors);
        }


        [Fact]
        public void VariableHiddenByFunctionName()
        {
            // Внутри локальных функций не должно быть деклараций переменных или локальных функций с именами,
            // совпадающими с именами переменных или функций из внешних контекстов (запрещено "сокрытие имен").
            var program = new Program
            {
                new VariableDeclaration("X"),
                new FunctionDeclaration("F")
                {
                    Body =
                    {
                        new FunctionDeclaration("X")
                    }
                }, 
                new Invocation("F", true),
            };

            var errors = _analyzer.Analyze(program);
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void MultipleReportsOfTheSameIssue()
        {
            var program = new Program
            {
                /*
                 *  var x;
                 *  func bar(){
                 *    print(x)
                 *  }
                 *
                 *  func F1() { bar() }
                 *  func F2() { bar() }
                 *  F1();
                 *  F2();
                 */

                new VariableDeclaration("x"),
                new FunctionDeclaration("bar")
                {
                    Body = { new PrintVariable("x") }
                },
                new FunctionDeclaration("F1")
                {
                    Body = { new Invocation("bar", isConditional: false) }
                },
                new FunctionDeclaration("F2")
                {
                    Body = { new Invocation("bar", isConditional: false) }
                },
                new Invocation("F1", isConditional: false),
                new Invocation("F2", isConditional: false)
            };

            var error = _analyzer.Analyze(program);
            Assert.Single(error);
        }
    }
}