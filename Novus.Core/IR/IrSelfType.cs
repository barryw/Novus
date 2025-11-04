namespace Novus.IR;

/// <summary>
/// Self type - refers to the implementing type in a trait context
/// This is a placeholder type that gets resolved to the actual implementing type
/// during trait monomorphization.
///
/// Example:
/// trait From<T> {
///     fn convert(value: T) -> Self  // Self refers to the implementing type
/// }
///
/// impl From<DosError> for NovusError {
///     fn convert(err: DosError) -> NovusError {  // Self resolves to NovusError
///         return NovusError::Dos(err)
///     }
/// }
/// </summary>
public class IrSelfType : IrType
{
    // Self types are resolved during trait implementation, so they don't have a concrete size
    // This will be replaced with the actual type during monomorphization
    public override int SizeInBytes => throw new InvalidOperationException(
        "Self type must be resolved to concrete type before querying size");

    public override string Name => "Self";

    private static IrSelfType? _instance;

    // Singleton pattern - there's only one Self type
    public static IrSelfType Instance => _instance ??= new IrSelfType();

    private IrSelfType() { }

    public override string ToString() => "Self";
}
