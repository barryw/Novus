using Novus.IR;

namespace Novus.Frontend;

/// <summary>
/// IrBuilder partial class containing disposable state scope helpers for exception-safe state management.
/// These helpers ensure that builder state is properly restored even when exceptions occur.
/// </summary>
public partial class IrBuilder
{
    /// <summary>
    /// RAII scope for managing type substitution state with automatic restoration on dispose.
    /// Ensures type substitutions are properly restored even if an exception occurs.
    /// </summary>
    private sealed class TypeSubstitutionScope : IDisposable
    {
        private readonly IrBuilder _builder;
        private readonly Dictionary<string, IrType>? _savedSubstitutions;

        public TypeSubstitutionScope(IrBuilder builder, Dictionary<string, IrType>? newSubstitutions)
        {
            _builder = builder;
            _savedSubstitutions = builder._currentTypeSubstitutions;
            builder._currentTypeSubstitutions = newSubstitutions;
        }

        public void Dispose()
        {
            _builder._currentTypeSubstitutions = _savedSubstitutions;
        }
    }

    /// <summary>
    /// RAII scope for managing Self type state with automatic restoration on dispose.
    /// </summary>
    private sealed class SelfTypeScope : IDisposable
    {
        private readonly IrBuilder _builder;
        private readonly IrType? _savedSelfType;

        public SelfTypeScope(IrBuilder builder, IrType? newSelfType)
        {
            _builder = builder;
            _savedSelfType = builder._currentSelfType;
            builder._currentSelfType = newSelfType;
        }

        public void Dispose()
        {
            _builder._currentSelfType = _savedSelfType;
        }
    }

    /// <summary>
    /// RAII scope for managing constants state with automatic restoration on dispose.
    /// </summary>
    private sealed class ConstantsScope : IDisposable
    {
        private readonly IrBuilder _builder;
        private readonly Dictionary<string, (IrType Type, object Value)> _savedConstants;

        public ConstantsScope(IrBuilder builder, Dictionary<string, (IrType Type, object Value)> templateConstants)
        {
            _builder = builder;
            _savedConstants = builder.GetConstantsAsTuples();

            // Merge template constants with current module constants
            builder.RestoreConstantsFromTuples(templateConstants);
            builder.RestoreConstantsFromTuples(_savedConstants);
        }

        public void Dispose()
        {
            _builder.RestoreConstantsFromTuples(_savedConstants);
        }
    }

    /// <summary>
    /// RAII scope for managing generic parameters with automatic restoration on dispose.
    /// </summary>
    private sealed class GenericParametersScope : IDisposable
    {
        private readonly IrBuilder _builder;
        private readonly Dictionary<string, IrGenericType> _savedGenericParams;
        private readonly List<string> _registeredParams;

        public GenericParametersScope(IrBuilder builder, List<string> genericParams)
        {
            _builder = builder;
            _savedGenericParams = new Dictionary<string, IrGenericType>();
            _registeredParams = genericParams;

            // Save existing and register new generic parameters
            foreach (var paramName in genericParams)
            {
                if (builder._symbols.HasGenericParameter(paramName))
                {
                    var existingParam = builder._symbols.LookupGenericParameter(paramName);
                    if (existingParam != null)
                    {
                        _savedGenericParams[paramName] = existingParam;
                    }
                }
                builder._symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
            }
        }

        public void Dispose()
        {
            // Clear all registered generic parameters
            _builder._symbols.ClearGenericParameters();

            // Restore previously saved parameters
            foreach (var kvp in _savedGenericParams)
            {
                _builder._symbols.RegisterGenericParameter(kvp.Key, kvp.Value);
            }
        }
    }

    /// <summary>
    /// RAII scope for managing function body state with automatic restoration on dispose.
    /// Handles current function, block, local variables, and emitted defer blocks.
    /// </summary>
    private sealed class FunctionBodyScope : IDisposable
    {
        private readonly IrBuilder _builder;
        private readonly IrFunction? _savedFunction;
        private readonly IrBasicBlock? _savedBlock;
        private readonly Dictionary<string, IrLocalVariable> _savedLocalVars;
        private readonly HashSet<IrBasicBlock> _savedEmittedDefers;

        public FunctionBodyScope(IrBuilder builder, IrFunction newFunction)
        {
            _builder = builder;
            _savedFunction = builder._currentFunction;
            _savedBlock = builder._currentBlock;
            _savedLocalVars = new Dictionary<string, IrLocalVariable>(builder._localVariables);
            _savedEmittedDefers = new HashSet<IrBasicBlock>(builder._emittedDeferBlocks);

            // Set up new function context
            builder._currentFunction = newFunction;
            builder._localVariables.Clear();
            builder._emittedDeferBlocks.Clear();
        }

        public void Dispose()
        {
            // Restore all state
            _builder._currentFunction = _savedFunction;
            _builder._currentBlock = _savedBlock;
            _builder._localVariables.Clear();
            foreach (var kvp in _savedLocalVars)
            {
                _builder._localVariables[kvp.Key] = kvp.Value;
            }
            _builder._emittedDeferBlocks.Clear();
            foreach (var defer in _savedEmittedDefers)
            {
                _builder._emittedDeferBlocks.Add(defer);
            }
        }
    }
}
