using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

class Analyzer
{
    public IEnumerable<Problem> Analyze(Program program)
    {
		var ctx = new FunctionContext(program, new HashSet<Problem>());
        AnalyzeIfNeeded(ctx);

		foreach (var (varName, contract) in ctx.LocalVariableContracts)
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
				// unused declaration, grey it out?
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

		ctx.IsAnalyzed = true;

		var localFuncs = new List<FunctionContext>();
		foreach (var statement in ctx.Statements)
		{
			if (statement is FunctionDeclaration fd)
			{
				// Here we do not analyze any functions but just save them
				// to be lazily analyzed in case it will be called somewhere;
				// it also allows to call function before they are actually declared.
				if (TryDeclareFunction(ctx, fd, out var functionCtx))
				{
					localFuncs.Add(functionCtx);
				}
			}
		}

		foreach (var functionCtx in localFuncs)
		{
			// Cross-declare all local functions to each other. Now function body
			// can contain call to not declared function even if it is not local.
			functionCtx.Functions = ctx.Functions;
		}

		foreach (var statement in ctx.Statements)
		{
			switch (statement)
			{
                case FunctionDeclaration:
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
					CheckInvocation(ctx, fi, out var infinitelyRecursive);
					if (infinitelyRecursive)
					{
						// Other imperative statements will are unreachable, do not analyze them.
						ctx.InfinitelyRecursive = true;
						return;
					}
					break;

				default:
					throw new NotImplementedException(
						$"Unknown statement of type '{statement.GetType()}':\n{statement}"
					);
			}
		}
	}

    private static void CheckInvocation(
	    FunctionContext ctx,
	    Invocation invocation,
	    out bool infinitelyRecursive
	)
    {
		if (!ctx.Functions.TryGetValue(invocation.FunctionName, out var funcCtx))
		{
			ctx.Problems.Add(new Problem(
                Problem.UNKNOWN_FUNCTION,
                invocation.FunctionName
            ));
			infinitelyRecursive = false;
			return;
		}

		AnalyzeIfNeeded(funcCtx);

		if (invocation.IsConditional)
		{
			infinitelyRecursive = false;
		}
		else
		{
			ctx.AlwaysInvokes.Add(funcCtx);

			if (ctx == funcCtx)
			{
				infinitelyRecursive = true;
			}
			else
			{
				// Here we collect all unconditional invocations, effectively recursively.
				// The complexity is O(N^2) in worst case: where N is count of required
				// invocations and each invoked function somehow calls each of others.
				// Through such a case sounds really unrealistic
				foreach (var subInv in funcCtx.AlwaysInvokes)
				{
					ctx.AlwaysInvokes.Add(subInv);
				}

				infinitelyRecursive = funcCtx.InfinitelyRecursive || ctx.AlwaysInvokes.Contains(ctx);
			}
		}

		foreach (var (varName, contract) in funcCtx.LocalVariableContracts)
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
					|| contract == VariableContract.ExternallyDeclared
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
				// unused declaration, grey it out?
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
			!ctx.TryGetVariableContract(name, out var pvc)
			|| pvc == VariableContract.ExternallyDeclared
		)
        {
            ctx.LocalVariableContracts[name] = VariableContract.External;
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
        if (!ctx.TryGetVariableContract(name, out var pvc))
        {
            ctx.LocalVariableContracts[name] = VariableContract.ExternallyDeclared;
        }
    }

    private static void TryAssignVariable(FunctionContext ctx, string name)
    {
        if (
            !ctx.TryGetVariableContract(name, out var avc)
            || avc == VariableContract.External
            || avc == VariableContract.ExternallyDeclared
        )
        {
            ctx.LocalVariableContracts[name] =
				VariableContract.ExternallyDeclaredLocallyAssigned;
        }
		else if (avc == VariableContract.LocallyDeclared)
		{
			ctx.LocalVariableContracts[name] =
				VariableContract.Local;
		}
    }

	private static void TryDeclareVariable(FunctionContext ctx, VariableDeclaration declaration)
    {
		if (ctx.IsSymbolDeclared(declaration.VariableName, out var contract))
        {
			if (contract == VariableContract.ExternallyDeclaredLocallyAssigned)
			{
				ctx.Problems.Add(new Problem(
					Problem.USED_THEN_DECLARED,
					declaration.VariableName
				));
			}
			else
			{
				ctx.Problems.Add(new Problem(
					Problem.ALREADY_DECLARED,
					declaration.VariableName
				));
				return;
			}
        }

		// It could be VariableContract.Local after Problem.USED_THEN_DECLARED as well,
		// but we assume the declaration and further uses are not the same variable.
		ctx.LocalVariableContracts[declaration.VariableName] =
			VariableContract.LocallyDeclared;
    }

    private static bool TryDeclareFunction(
		FunctionContext ctx,
		FunctionDeclaration declaration,
		[NotNullWhen(true)] out FunctionContext? functionCtx
	)
    {
        if (ctx.IsSymbolDeclared(declaration.FunctionName, out _))
        {
            ctx.Problems.Add(new Problem(
                Problem.ALREADY_DECLARED,
                declaration.FunctionName
            ));
			functionCtx = null;
			return false;
        }
        else
        {
			functionCtx = new FunctionContext(declaration, ctx);
            ctx.Functions = ctx.Functions.Add(
				declaration.FunctionName,
				functionCtx
			);
            ctx.LocalVariableContracts[declaration.FunctionName] =
                VariableContract.Local;
			return true;
        }
    }

    private class FunctionContext
	{
        public FunctionContext(IEnumerable<IStatement> statements, HashSet<Problem> problems)
        {
			Statements = statements;
			Problems = problems;
			Functions = ImmutableDictionary.Create<string, FunctionContext>();
			AlwaysInvokes = new();
			LocalVariableContracts = new();
			ExternalVariableContracts = Enumerable.Empty<Dictionary<string, ContextualContract>>();
        }

        public FunctionContext(FunctionDeclaration fd, FunctionContext ctx)
        {
			Statements = fd.Body;
			Problems = ctx.Problems;
			Functions = ctx.Functions;
			AlwaysInvokes = new();
			LocalVariableContracts = new();
			ExternalVariableContracts = ctx.ExternalVariableContracts.Prepend(ctx.LocalVariableContracts);
        }

		public bool IsAnalyzed { get; set; }

  		public IEnumerable<IStatement> Statements { get; }

        public HashSet<Problem> Problems { get; }

        public HashSet<FunctionContext> AlwaysInvokes { get; }
	
		public ImmutableDictionary<string, FunctionContext> Functions { get; set; }
	
		private IEnumerable<Dictionary<string, ContextualContract>> ExternalVariableContracts { get; }

        public Dictionary<string, ContextualContract> LocalVariableContracts { get; }

        public bool InfinitelyRecursive { get; set; }

        public bool TryGetVariableContract(
			string name,
			[NotNullWhen(true)] out ContextualContract? contract
		)
		{
			if (LocalVariableContracts.TryGetValue(name, out contract))
			{
				return true;
			}

			foreach (var contracts in ExternalVariableContracts)
			{
				if (contracts.TryGetValue(name, out contract))
				{
					if (contract == VariableContract.LocallyDeclared)
					{
						contract = VariableContract.ExternallyDeclared;
					}

					return true;
				}
			}

			contract = null;
			return false;
		}

		public bool IsSymbolDeclared(
			string name,
			[NotNullWhen(true)] out ContextualContract? contract
		)
		{
			if (Functions.ContainsKey(name))
			{
				contract = VariableContract.Local;
				return true;
			}

			return TryGetVariableContract(name, out contract);
		}
	}

	// TODO: here we cane either preserve contract application context (e.g. a statement number)
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
		/// Uninitialized variable is declared locally.
		/// </summary>
		LocallyDeclared,

		/// <summary>
		/// Either function or variable is declared and assigned locally.
		/// </summary>
		Local,
	}
}
