using Microsoft.AspNetCore.SignalR.Protocol;
using SignalR.Orleans.Clients;
using SignalR.Orleans.Core;
using SignalR.Orleans.Groups;
using SignalR.Orleans.Users;

// ReSharper disable once CheckNamespace
namespace Orleans;

public static class GrainSignalRExtensions
{
	/// <param name="grain"></param>
	extension(IHubMessageInvoker grain)
	{
		/// <summary>
		/// Invokes a method on the hub.
		/// </summary>
		/// <param name="methodName">Target method name to invoke.</param>
		/// <param name="args">Arguments to pass to the target method.</param>
		public Task Send(string methodName, params object[] args)
		{
			var invocationMessage = new InvocationMessage(methodName, args).AsImmutable();
			return grain.Send(invocationMessage);
		}

		/// <summary>
		/// Invokes a method on the hub (one way).
		/// </summary>
		/// <param name="methodName">Target method name to invoke.</param>
		/// <param name="args">Arguments to pass to the target method.</param>
		public void SendOneWay(string methodName, params object[] args)
		{
			var invocationMessage = new InvocationMessage(methodName, args).AsImmutable();
			grain.SendOneWay(invocationMessage);
		}
	}
}

public static class GrainFactoryExtensions
{
	extension(IGrainFactory grainFactory)
	{
		public HubContext GetHub(string hubName)
			=> new(grainFactory, hubName);

		public HubContext<THub> GetHub<THub>()
			=> new(grainFactory);

		internal IClientGrain GetClientGrain(string hubName, string connectionId)
			=> grainFactory.GetGrain<IClientGrain>(ConnectionGrainKey.Build(hubName, connectionId));

		internal IGroupGrain GetGroupGrain(string hubName, string groupName)
			=> grainFactory.GetGrain<IGroupGrain>(ConnectionGrainKey.Build(hubName, groupName));

		internal IUserGrain GetUserGrain(string hubName, string userId)
			=> grainFactory.GetGrain<IUserGrain>(ConnectionGrainKey.Build(hubName, userId));

		internal IServerDirectoryGrain GetServerDirectoryGrain()
			=> grainFactory.GetGrain<IServerDirectoryGrain>(0);
	}
}