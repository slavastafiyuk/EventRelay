namespace EventRelay.ControlPlaneService.Application.Exceptions;

public sealed class TransactionRollbackOnlyException(string message, Exception innerException) : InvalidOperationException(message, innerException);
