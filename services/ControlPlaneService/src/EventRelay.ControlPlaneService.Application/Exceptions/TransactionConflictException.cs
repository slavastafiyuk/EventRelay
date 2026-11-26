namespace EventRelay.ControlPlaneService.Application.Exceptions;

public sealed class TransactionConflictException(string message, Exception innerException) : Exception(message, innerException);
