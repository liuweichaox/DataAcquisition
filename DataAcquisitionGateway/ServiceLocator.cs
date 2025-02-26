namespace DataAcquisitionGateway;

public static class ServiceLocator
{
    private static IServiceProvider _serviceProvider;

    public static void Configure(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public static T GetService<T>() where T : class
    {
        return _serviceProvider.GetService<T>();
    }

    public static T GetRequiredService<T>() where T : class
    {
        return _serviceProvider.GetRequiredService<T>();
    }
}