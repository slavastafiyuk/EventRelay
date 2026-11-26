using System.Data;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Persistence;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class TransactionalAttribute(IsolationLevel isolationLevel = IsolationLevel.Serializable) : Attribute
{
    public IsolationLevel IsolationLevel { get; } = isolationLevel;
}
