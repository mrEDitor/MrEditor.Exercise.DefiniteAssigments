using System.Collections.Generic;
using System.Text;

interface IStatement { }

class VariableDeclaration : IStatement
{
    public VariableDeclaration(string variableName)
    {
        VariableName = variableName;
    }

    public string VariableName { get; }

    public override string ToString() => $"var {VariableName};";
}

class AssignVariable : IStatement
{
    public AssignVariable(string variableName)
    {
        VariableName = variableName;
    }

    public string VariableName { get; }

    public override string ToString() => $"{VariableName} = smth;";
}

class PrintVariable : IStatement
{
    public PrintVariable(string variableName)
    {
        VariableName = variableName;
    }

    public string VariableName { get; }

    public override string ToString() => $"print({VariableName});";
}

class FunctionDeclaration : IStatement
{
    public FunctionDeclaration(string functionName)
    {
        FunctionName = functionName;
    }

    public string FunctionName { get; }
    public Program Body { get; } = new Program();

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("func ").Append(FunctionName).AppendLine(" {");
        builder.Append(Body);
        builder.Append('}');

        return builder.ToString();
    }
}

class Invocation : IStatement
{
    public Invocation(string functionName, bool isConditional)
    {
        FunctionName = functionName;
        IsConditional = isConditional;
    }

    public string FunctionName { get; }
    public bool IsConditional { get; }

    public override string ToString()
    {
        if (IsConditional)
            return $"if (smth) {FunctionName}();";

        return $"{FunctionName}();";
    }
}

class Program : List<IStatement>
{
    public override string ToString()
    {
        var builder = new StringBuilder();

        foreach (var statement in this)
        {
            builder.AppendLine(statement.ToString());
        }

        return builder.ToString();
    }
}

class CorrectExamples
{
    public static readonly Program SimpleVariableUsage = new Program
  {
    /*
     *  var foo;
     *  foo = ...;
     *  print(foo); // ok, initialized
     */
    new VariableDeclaration("foo"),
    new AssignVariable("foo"),
    new PrintVariable("foo")
  };

    public static readonly Program LocalFunctionInit = new Program
  {
    /*
     *  var foo;
     *  func Boo() {
     *    print(foo);
     *  }
     *  
     *  Bar();
     *  print(foo); // ok, initialized
     *  
     *  func Bar() {
     *    foo = ...;
     *    if (...) {
     *      Boo(); // ok, 'foo' initialized
     *    }
     *  }
     */
    new VariableDeclaration("foo"),
    new FunctionDeclaration("Boo") {
      Body = {
        new PrintVariable("foo")
      }
    },
    new Invocation("Bar", isConditional: false),
    new PrintVariable("foo"),
    new FunctionDeclaration("Bar") {
      Body = {
        new AssignVariable("foo"),
        new Invocation("Boo", isConditional: true),
      }
    }
  };
}
