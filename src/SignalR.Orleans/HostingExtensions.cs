using Microsoft.Extensions.DependencyInjection;
using Orleans.Storage;
using SignalR.Orleans;

// ReSharper disable once CheckNamespace
namespace Orleans.Hosting;

public static class SiloBuilderExtensions
{
	/// <summary>
	/// Configures the Silo to use SignalR.
	/// </summary>
	/// <param name="builder"></param>
	/// <param name="configure"></param>
	/// <returns></returns>
	public static ISiloBuilder UseSignalR(this ISiloBuilder builder, Action<SignalrOrleansSiloConfigBuilder> configure = null)
	{
		var cfg = new SignalrOrleansSiloConfigBuilder();
		configure?.Invoke(cfg);

		builder.AddMemoryGrainStorage(Constants.STORAGE_PROVIDER);
		// builder.AddMemoryGrainStorage(Constants.PUBSUB_PROVIDER);

		cfg.ConfigureBuilder?.Invoke(builder, new());

		builder.ConfigureServices(services => services.AddSingleton<IConfigurationValidator, SignalRConfigurationValidator>());

		return builder.AddMemoryStreams(Constants.STREAM_PROVIDER);
	}
}

internal sealed class SignalRConfigurationValidator : IConfigurationValidator
{
	private readonly IServiceProvider _sp;
	private readonly ILogger _logger;

	public SignalRConfigurationValidator(IServiceProvider serviceProvider)
	{
		_logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<SignalRConfigurationValidator>();
		_sp = serviceProvider;
	}

	public void ValidateConfiguration()
	{
		_logger.LogInformation("SignalR.Orleans: Verifying PubSub storage provider '{PubSubProvider}' is registered...", Constants.PUBSUB_PROVIDER);

		var pubSubProvider = _sp.GetKeyedService<IGrainStorage>(Constants.PUBSUB_PROVIDER);
		if (pubSubProvider == null)
		{
			const string err = $"SignalR.Orleans: No PubSub storage provider '{Constants.PUBSUB_PROVIDER}' was found. "
				+ "UseSignalR() registers it automatically with in-memory storage. "
				+ "If you overrode it, ensure you call 'siloBuilder.AddMemoryGrainStorage(\"PubSubStore\")' (or another provider) when building your silo.";
			_logger.LogError(err);
			throw new InvalidOperationException(err);
		}

		_logger.LogInformation("SignalR.Orleans: PubSub storage provider '{PubSubProvider}' found ({TypeName}).", Constants.PUBSUB_PROVIDER, pubSubProvider.GetType().FullName);
	}
}