using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;
using Orleans.Storage;
using SignalR.Orleans.Tests.Models;
using System.Net;
using Xunit;

namespace SignalR.Orleans.Tests;

public sealed class SignalRConfigurationValidatorTests
{
	[Fact]
	public void ValidateConfiguration_Throws_IfNoPubSubProviderIsRegistered()
	{
		var siloHost = new HostBuilder()
			.UseOrleans(builder => builder
				.UseLocalhostClustering()
				.Configure<EndpointOptions>(options => options.AdvertisedIPAddress = IPAddress.Loopback)
				.UseSignalR()
		)
		.Build();

		Assert.Throws<InvalidOperationException>(() => siloHost.Start());
		siloHost.Dispose();
	}

	[Fact]
	public void ValidateConfiguration_DoesNotThrow_IfPubSubProviderIsRegistered_With_Orleans7()
	{
		var siloHost = new HostBuilder()
			.UseOrleans(builder => builder
				.UseLocalhostClustering()
				.Configure<EndpointOptions>(options => options.AdvertisedIPAddress = IPAddress.Loopback)
				.AddMemoryGrainStorage(Constants.PUBSUB_PROVIDER)
				.UseSignalR()
			)
			.Build();

		siloHost.Start();
		siloHost.Dispose();
	}

#if NET8_0_OR_GREATER
	[Fact]
	public void ValidateConfiguration_DoesNotThrow_IfPubSubProviderIsRegistered_With_Net8()
	{
		var sp = new ServiceCollection()
			.AddSingleton<ILoggerFactory, LoggerFactory>()
			.AddKeyedSingleton<IGrainStorage>(
				serviceKey: Constants.PUBSUB_PROVIDER,
				implementationFactory: (_, _) => new DumbGrainStore())
			.BuildServiceProvider();

		var validator = new SignalRConfigurationValidator(sp);

		validator.ValidateConfiguration();
	}
#endif
}