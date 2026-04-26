using SignalR.Orleans;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class OrleansClientExtensions
{
	extension(IClientBuilder builder)
	{
		public IClientBuilder UseSignalR(Action<SignalrClientConfig> config)
		{
			var cfg = new SignalrClientConfig();
			config?.Invoke(cfg);

			return builder.UseSignalR(cfg);
		}

		public IClientBuilder UseSignalR(SignalrClientConfig config = null)
		{
			if (config == null)
				config = new();

			return builder.AddMemoryStreams(Constants.STREAM_PROVIDER);
		}
	}
}