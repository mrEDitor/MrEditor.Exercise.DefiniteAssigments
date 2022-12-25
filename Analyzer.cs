using System;
using System.Collections.Generic;
using System.Collections.Immutable;

class Analyzer
{
    public IEnumerable<Problem> Analyze(Program program)
    {
		var ctx = new FunctionContext(program)
		{
			Problems = new List<Problem>(),
		};
        AnalyzeIfNeeded(ctx);

		foreach (var (varName, contract) in ctx.VariableContracts)
		{
			if (
				contract == VariableContract.External
				|| contract == VariableContract.ExternallyDeclared
				|| contract == VariableContract.ExternallyDeclaredLocallyAssigned
			)
			{
				ctx.Problems.Add(new Problem(
					Problem.VARIABLE_NOT_DECLARED,
					varName
				));
			}
			else if (contract == VariableContract.LocallyDeclared)
			{
				// unused declaration, huh?
			}
			else if (contract == VariableContract.Local)
			{
				// just a local variable, ok
			}
			else
			{
				throw new NotImplementedException(
					$"Unknown constraint {contract} for '{varName}'"
				);
			}
		}

        return ctx.Problems;
    }
    
    private static void AnalyzeIfNeeded(FunctionContext ctx)
    {
		if (ctx.IsAnalyzed)
		{
			return;
		}

		// TODO: how is about recursion?
		ctx.IsAnalyzed = true;

		foreach (var statement in ctx.Statements)
		{
			switch (statement)
			{
                case FunctionDeclaration fd:
                    TryDeclareFunction(ctx, fd);
                    break;

                case VariableDeclaration vd:
                    TryDeclareVariable(ctx, vd);
                    break;

                case AssignVariable av:
					if (ctx.Functions.ContainsKey(av.VariableName))
					{
						ctx.Problems.Add(new Problem(
							Problem.CAN_NOT_ASSIGN_TO_FUNCTION,
							av.VariableName
						));
						break;
					}
        
                    TryAssignVariable(ctx, av.VariableName);
                    break;

                case PrintVariable pv:
                    CheckVariableAssigned(ctx, pv.VariableName);
                    break;

                case Invocation fi:
					CheckInvocation(ctx, fi);
					break;

				default:
					throw new NotImplementedException(
						$"Unknown statement of type '{statement.GetType()}':\n{statement}"
					);
			}
		}
	}

    private static void CheckInvocation(FunctionContext ctx, Invocation invocation)
	{
		if (!ctx.Functions.TryGetValue(invocation.FunctionName, out var funcCtx))
		{
			ctx.Problems.Add(new Problem(
                Problem.UNKNOWN_FUNCTION,
                invocation.FunctionName
            ));
			return;
		}

		AnalyzeIfNeeded(funcCtx);

		foreach (var (varName, contract) in funcCtx.VariableContracts)
		{
			if (contract == VariableContract.External)
			{
				CheckVariableAssigned(ctx, varName);
			}
			else if (
				contract == VariableContract.ExternallyDeclared
				|| contract == VariableContract.ExternallyDeclaredLocallyAssigned
			)
			{
				if (
					invocation.IsConditional
					|| contract == VariableContract.ExternallyDeclaredLocallyAssigned
				)
				{
					CheckVariableDeclared(ctx, varName);
				}
				else
				{
					TryAssignVariable(ctx, varName);
				}
			}
			else if (contract == VariableContract.LocallyDeclared)
			{
				// unused declaration, huh?
			}
			else if (contract == VariableContract.Local)
			{
				// just a local variable, ok
			}
			else
			{
				throw new NotImplementedException(
					$"Unknown constraint {contract} for '{varName}' inside '{invocation.FunctionName}'"
				);
			}
		}
	}

    private static void CheckVariableAssigned(FunctionContext ctx, string name)
    {
        if (
			!ctx.VariableContracts.TryGetValue(name, out var pvc)
			|| pvc == VariableContract.ExternallyDeclared
		)
        {
            ctx.VariableContracts = ctx.VariableContracts.SetItem(
                name,
                VariableContract.External
            );
        }
        else if (pvc == VariableContract.LocallyDeclared)
        {
            ctx.Problems.Add(new Problem(
                Problem.VARIABLE_NOT_ASSIGNED,
                name
            ));
        }
    }

    private static void CheckVariableDeclared(FunctionContext ctx, string name)
    {
        if (!ctx.VariableContracts.TryGetValue(name, out var pvc))
        {
            ctx.VariableContracts = ctx.VariableContracts.SetItem(
                name,
                VariableContract.ExternallyDeclared
            );
        }
    }

    private static void TryAssignVariable(FunctionContext ctx, string name)
    {
        if (
            !ctx.VariableContracts.TryGetValue(name, out var avc)
            || avc == VariableContract.External
            || avc == VariableContract.ExternallyDeclared
        )
        {
            ctx.VariableContracts = ctx.VariableContracts.SetItem(
                name,
                VariableContract.ExternallyDeclaredLocallyAssigned
            );
        }
		else if (avc == VariableContract.LocallyDeclared)
		{
			ctx.VariableContracts = ctx.VariableContracts.SetItem(
                name,
                VariableContract.Local
            );
		}
    }

	private static void TryDeclareVariable(FunctionContext ctx, VariableDeclaration declaration)
    {
        if (ctx.IsSymbolDeclared(declaration.VariableName))
        {
            ctx.Problems.Add(new Problem(
                Problem.SYMBOL_NAME_ALREADY_EXISTS,
                declaration.VariableName
            ));
        }
        else
        {
            ctx.VariableContracts = ctx.VariableContracts.Add(
                declaration.VariableName,
                VariableContract.LocallyDeclared
            );
        }
    }

    private static void TryDeclareFunction(FunctionContext ctx, FunctionDeclaration declaration)
    {
        if (ctx.IsSymbolDeclared(declaration.FunctionName))
        {
            ctx.Problems.Add(new Problem(
                Problem.SYMBOL_NAME_ALREADY_EXISTS,
                declaration.FunctionName
            ));
        }
        else
        {
            ctx.Functions = ctx.Functions.Add(
				declaration.FunctionName,
				new FunctionContext(declaration, ctx)
			);
            ctx.VariableContracts = ctx.VariableContracts.Add(
                declaration.FunctionName,
                VariableContract.Local
            );
        }
    }

    private class FunctionContext
	{
        public FunctionContext(IEnumerable<IStatement> statements)
        {
			Statements = statements;
			Functions = ImmutableDictionary.Create<string, FunctionContext>();
			VariableContracts = ImmutableDictionary.Create<string, ContextualContract>();
        }

        public FunctionContext(FunctionDeclaration fd, FunctionContext ctx)
        {
			Statements = fd.Body;
			Functions = ctx.Functions;
			VariableContracts = ctx.VariableContracts;
        }

		public bool IsAnalyzed { get; set; }

  		public IEnumerable<IStatement> Statements { get; }
		
		public ImmutableDictionary<string, FunctionContext> Functions { get; set; }

		public ICollection<Problem> Problems { get; init; }

		public ImmutableDictionary<string, ContextualContract> VariableContracts { get; set; }

		public bool IsSymbolDeclared(string name)
		{
			return Functions.ContainsKey(name) || VariableContracts.ContainsKey(name);
		}
	}

	// TODO: here we cane either preserve conract application context (e.g. a statement number)
	// or speed the things up if drop the class by replacing with VariableContract.
	private record ContextualContract
	{
		private ContextualContract(VariableContract contract)
		{
			Contract = contract;
		}

		private VariableContract Contract { get; }

		public static implicit operator ContextualContract(VariableContract c)
		{
			return new(c);
		}
	}

	private enum VariableContract
	{
		/// <summary>
		/// Variable must be declared and assigned externally.
		/// </summary>
		External,

		/// <summary>
		/// Variable must be declared externally; no assigment constraint.
		/// </summary>
		ExternallyDeclared,

		/// <summary>
		/// Variable must be declared externally; guaranteed to be assigned locally.
		/// </summary>
		ExternallyDeclaredLocallyAssigned,

		/// <summary>
		/// Either function or uninitialized variable is declared locally.
		/// </summary>
		LocallyDeclared,

		/// <summary>
		/// Variable is declared and assigned locally.
		/// </summary>
		Local,
	}
}
