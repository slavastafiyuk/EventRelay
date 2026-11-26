using System.Reflection;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventRelay.ControlPlaneService.Application.Transactions;

public static class TransactionalServiceCollectionExtensions
{
    public static IServiceCollection EnableTransactionalInterception(
        this IServiceCollection services)
    {
        var descriptors = services
            .Where(x =>
                !x.IsKeyedService &&
                x.ImplementationType is not null &&
                x.ServiceType.IsInterface &&
                !x.ServiceType.ContainsGenericParameters)
            .ToArray();

        foreach (var descriptor in descriptors)
        {
            var implementationType =
                descriptor.ImplementationType!;

            if (!HasTransactionalMethods(
                    descriptor.ServiceType,
                    implementationType))
            {
                continue;
            }

            if (descriptor.Lifetime != ServiceLifetime.Scoped)
            {
                throw new InvalidOperationException(
                    $"Transactional service " +
                    $"'{descriptor.ServiceType.Name}' must be scoped.");
            }

            if (typeof(IDisposable)
                    .IsAssignableFrom(descriptor.ServiceType) ||
                typeof(IAsyncDisposable)
                    .IsAssignableFrom(descriptor.ServiceType))
            {
                throw new NotSupportedException(
                    "Disposable service interfaces are not supported.");
            }

            var index = services.IndexOf(descriptor);

            var serviceKey = new object();

            services.AddKeyedScoped(
                descriptor.ServiceType,
                serviceKey,
                implementationType);

            services[index] = ServiceDescriptor.Scoped(
                descriptor.ServiceType,
                provider =>
                {
                    var target =
                        provider.GetRequiredKeyedService(
                            descriptor.ServiceType,
                            serviceKey);

                    var unitOfWork =
                        provider.GetRequiredService<IUnitOfWork>();

                    return TransactionalProxy.Wrap(
                        descriptor.ServiceType,
                        target,
                        unitOfWork);
                });
        }

        return services;
    }

    private static bool HasTransactionalMethods(
        Type serviceType,
        Type implementationType)
    {
        var found = false;

        foreach (var interfaceType in
                 serviceType.GetInterfaces().Append(serviceType))
        {
            var map = implementationType
                .GetInterfaceMap(interfaceType);

            for (var i = 0; i < map.TargetMethods.Length; i++)
            {
                var implementationMethod = map.TargetMethods[i];
                var interfaceMethod = map.InterfaceMethods[i];

                var annotated =
                    implementationMethod.IsDefined(
                        typeof(TransactionalAttribute), true)
                    || interfaceMethod.IsDefined(
                        typeof(TransactionalAttribute), true);

                if (!annotated)
                    continue;

                ValidateTransactionalMethod(interfaceMethod);

                found = true;
            }
        }

        return found;
    }

    private static void ValidateTransactionalMethod(
        MethodInfo method)
    {
        var returnType = method.ReturnType;

        var supported =
            returnType == typeof(Task) ||
            returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>);

        if (!supported ||
            method.IsGenericMethod ||
            method.GetParameters()
                .Any(x => x.ParameterType.IsByRef))
        {
            throw new NotSupportedException($"Unsupported transactional method: {method.Name}");
        }
    }
}
