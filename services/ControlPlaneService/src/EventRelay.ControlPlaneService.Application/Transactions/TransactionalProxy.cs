using System.Data;
using System.Reflection;
using System.Runtime.ExceptionServices;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;

namespace EventRelay.ControlPlaneService.Application.Transactions;

public class TransactionalProxy : DispatchProxy
{
    private object _target = null!;
    private IUnitOfWork _unitOfWork = null!;

    public static object Wrap(Type serviceType, object target, IUnitOfWork unitOfWork)
    {
        var service = DispatchProxy.Create(
            serviceType,
            typeof(TransactionalProxy));

        var proxy = (TransactionalProxy)service;

        proxy._target = target;
        proxy._unitOfWork = unitOfWork;

        return service;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        args ??= [];

        var implementationMethod =
            ResolveImplementationMethod(targetMethod);

        var attribute =
            implementationMethod
                .GetCustomAttribute<TransactionalAttribute>()
            ?? targetMethod
                .GetCustomAttribute<TransactionalAttribute>();

        if (attribute is null)
        {
            return InvokeTarget(targetMethod, args);
        }

        var cancellationToken =
            GetCancellationToken(targetMethod, args);

        var returnType = targetMethod.ReturnType;

        if (returnType == typeof(Task))
        {
            return InvokeTaskAsync(
                targetMethod,
                args,
                attribute.IsolationLevel,
                cancellationToken);
        }

        if (returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];

            var method = typeof(TransactionalProxy)
                .GetMethod(
                    nameof(InvokeResultAsync),
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)!
                .MakeGenericMethod(resultType);

            return method.Invoke(
                this,
                [
                    targetMethod,
                    args,
                    attribute.IsolationLevel,
                    cancellationToken
                ]);
        }

        throw new NotSupportedException(
            $"Transactional method '{targetMethod.Name}' " +
            "must return Task or Task<TResult>.");
    }

    private MethodInfo ResolveImplementationMethod(MethodInfo interfaceMethod)
    {
        var interfaceType = interfaceMethod.DeclaringType!;

        var interfaceMap =
            _target.GetType().GetInterfaceMap(interfaceType);

        var methodDefinition = interfaceMethod.IsGenericMethod
            ? interfaceMethod.GetGenericMethodDefinition()
            : interfaceMethod;

        var index = Array.IndexOf(
            interfaceMap.InterfaceMethods,
            methodDefinition);

        if (index < 0)
        {
            throw new InvalidOperationException($"Cannot resolve method '{interfaceMethod.Name}'.");
        }

        return interfaceMap.TargetMethods[index];
    }

    private async Task InvokeTaskAsync(MethodInfo method, object?[] args, IsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        await _unitOfWork.ExecuteInTransactionAsync(
            async _ =>
            {
                await (Task)InvokeTarget(method, args)!;

                return true;
            },
            isolationLevel,
            cancellationToken);
    }

    private async Task<TResult> InvokeResultAsync<TResult>(MethodInfo method, object?[] args, IsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(
            async _ =>
            {
                return await (Task<TResult>)InvokeTarget(method, args)!;
            },
            isolationLevel,
            cancellationToken);
    }

    private object? InvokeTarget(MethodInfo method, object?[] args)
    {
        try
        {
            return method.Invoke(_target, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo
                .Capture(exception.InnerException)
                .Throw();

            throw;
        }
    }

    private static CancellationToken GetCancellationToken(MethodInfo method, object?[] args)
    {
        var parameters = method.GetParameters();

        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(CancellationToken))
            {
                return (CancellationToken)args[i]!;
            }
        }

        return CancellationToken.None;
    }
}
