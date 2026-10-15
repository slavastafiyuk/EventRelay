namespace EventRelay.ProjectService.Domain.Interfaces;

public interface ISecondaryKey<out T>
{
    T SecondaryKey { get; }
}
