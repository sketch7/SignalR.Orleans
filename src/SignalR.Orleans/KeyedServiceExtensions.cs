using Microsoft.Extensions.DependencyInjection;

namespace Sketch7;

/// <summary>
/// Extension method for <see cref="IServiceProvider"/> Orleans 7 compatibility for .NET 8.0.
/// </summary>
internal static class KeyedServiceExtensions
{
	private static Type _keyedCollectionType;
	private static Type _keyedServiceType;

	/// <summary>
	/// Acquire a service by key.
	/// </summary>
	/// <typeparam name="TKey">
	/// The service key type.
	/// </typeparam>
	/// <typeparam name="TService">
	/// The service type.
	/// </typeparam>
	/// <param name="services">
	/// The service provider.
	/// </param>
	/// <param name="key">
	/// The service key.
	/// </param>
	/// <returns>The service.</returns>
	public static TService GetServiceByKey<TKey, TService>(this IServiceProvider services, TKey key)
		where TService : class
	{
		var service = services.GetKeyedService<TService>(key);
		if (service is not null)
			return service;

		// Looks like we don't have the service in the .NET 8 collection (Orleans 8 maybe not in use...), so we'll try to get it from the Orleans 7 collection.
		_keyedServiceType ??= Type.GetType("Orleans.Runtime.IKeyedService`2, Orleans.Core.Abstractions");
		_keyedCollectionType ??= Type.GetType("Orleans.Runtime.IKeyedServiceCollection`2, Orleans.Core.Abstractions");

		if (_keyedCollectionType is null && _keyedServiceType is null)
			return null;

		var keyedServiceType = _keyedServiceType?.MakeGenericType(typeof(TKey), typeof(TService));
		if (keyedServiceType is not null)
		{
			var keyedService = (dynamic)services.GetService(keyedServiceType);
			var keyService = keyedService is not null
				? (TService)keyedService.GetService(services)
				: null;

			if (keyService is not null && keyedService.Key.Equals(key))
				return keyService;
		}

		var keyedCollectionType = _keyedCollectionType?.MakeGenericType(typeof(TKey), typeof(TService));
		if (keyedCollectionType is null)
			return null;

		var keyedCollection = (dynamic)services.GetService(keyedCollectionType);
		return keyedCollection is not null
			? (TService)keyedCollection.GetService(services, key)
			: null;
	}

	/// <summary>
	/// Acquire a service by key, throwing if the service is not found.
	/// </summary>
	/// <typeparam name="TKey">
	/// The service key type.
	/// </typeparam>
	/// <typeparam name="TService">
	/// The service type.
	/// </typeparam>
	/// <param name="services">
	/// The service provider.
	/// </param>
	/// <param name="key">
	/// The service key.
	/// </param>
	/// <returns>The service.</returns>
	public static TService GetRequiredServiceByKey<TKey, TService>(this IServiceProvider services, TKey key)
		where TService : class => services.GetServiceByKey<TKey, TService>(key) ?? throw new KeyNotFoundException(key?.ToString());

	/// <summary>
	/// Acquire a service by name.
	/// </summary>
	/// <typeparam name="TService">
	/// The service type.
	/// </typeparam>
	/// <param name="services">
	/// The service provider.
	/// </param>
	/// <param name="name">
	/// The service name.
	/// </param>
	/// <returns>The service.</returns>
	public static TService GetServiceByName<TService>(this IServiceProvider services, string name)
		where TService : class => services.GetServiceByKey<string, TService>(name);

	/// <summary>
	/// Acquire a service by name, throwing if it is not found.
	/// </summary>
	/// <typeparam name="TService">
	/// The service type.
	/// </typeparam>
	/// <param name="services">
	/// The service provider.
	/// </param>
	/// <param name="name">
	/// The service name.
	/// </param>
	/// <returns>The service.</returns>
	public static TService GetRequiredServiceByName<TService>(this IServiceProvider services, string name)
		where TService : class => services.GetRequiredServiceByKey<string, TService>(name);
}