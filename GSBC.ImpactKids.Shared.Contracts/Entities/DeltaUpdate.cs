using System.Diagnostics.CodeAnalysis;

namespace GSBC.ImpactKids.Shared.Contracts.Entities;

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)] // changed to all fields to include _updateValue;
public class DeltaUpdate<T>
{
    private T _updatedValue = default!;
    public T Value
    {
        get => _updatedValue;
        set
        {
            IsUpdated = true;
            _updatedValue = value;
        }
    }

    public void SetInitialValue(T value) => _updatedValue = value;

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsUpdated { get; private set; }
}