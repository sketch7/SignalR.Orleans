using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Streams;
using SignalR.Orleans.Clients;
using Utils = SignalR.Orleans.Core.Utils;

namespace SignalR.Orleans;

public class OrleansHubLifetimeManager<THub> : HubLifetimeManager<THub>, IAsyncDisposable, ILifecycleParticipant<ISiloLifecycle>
	where THub : Hub
{
	private Timer _timer;
	private readonly HubConnectionStore _connections = new();
	private readonly ILogger _logger;
	private readonly IClusterClient _clusterClient;
	private readonly Guid _serverId;
	private IStreamProvider _streamProvider;
	private IAsyncStream<AllMessage> _allStream;
	private readonly string _hubName;
	private readonly SemaphoreSlim _streamSetupLock = new(1);
	private StreamReplicaContainer<ClientMessage> _serverStreamsReplicaContainer;

	public OrleansHubLifetimeManager(
		ILogger<OrleansHubLifetimeManager<THub>> logger,
		IClusterClient clusterClient
	)
	{
		var hubType = typeof(THub).BaseType?.GenericTypeArguments.FirstOrDefault() ?? typeof(THub);
		_hubName = hubType.IsInterface && hubType.Name.StartsWith('I')
			? hubType.Name[1..]
			: hubType.Name;
		_serverId = Guid.NewGuid(); // todo: include machine name
		_logger = logger;
		_clusterClient = clusterClient;
	}

	public void Participate(ISiloLifecycle lifecycle) => lifecycle.Subscribe(
		observerName: nameof(OrleansHubLifetimeManager<>),
		stage: ServiceLifecycleStage.Active,
		onStart: async cts => await Task.Run(EnsureStreamSetup, cts));

	private Task HeartbeatCheck()
	{
		var client = _clusterClient.GetServerDirectoryGrain();
		return client.Heartbeat(_serverId);
	}

	private async Task EnsureStreamSetup()
	{
		if (_streamProvider != null)
			return;

		try
		{
			await _streamSetupLock.WaitAsync();

			if (_streamProvider != null)
				return;
			await SetupStreams();
		}
		finally
		{
			_streamSetupLock.Release();
		}
	}

	private async Task SetupStreams()
	{
		_logger.LogInformation("Initializing: Orleans HubLifetimeManager {hubName} (serverId: {serverId})...", _hubName, _serverId);

		try
		{
			_streamProvider = _clusterClient.GetStreamProvider(Constants.STREAM_PROVIDER);
			_serverStreamsReplicaContainer = new(_streamProvider, _serverId, Constants.SERVERS_STREAM, Constants.STREAM_SEND_REPLICAS);

			_allStream = _streamProvider.GetStream<AllMessage>(StreamId.Create(Utils.BuildStreamHubName(_hubName), Constants.ALL_STREAM_ID));
			_timer = new(_ => Task.Run(HeartbeatCheck), null, TimeSpan.FromSeconds(0), TimeSpan.FromMinutes(Constants.HEARTBEAT_PULSE_IN_MINUTES));

			var subscribeTasks = new List<Task>
			{
				_allStream.SubscribeAsync((msg, _) => ProcessAllMessage(msg)),
				_serverStreamsReplicaContainer.SubscribeAsync((msg, _) => ProcessServerMessage(msg))
			};

			await Task.WhenAll(subscribeTasks);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Initialization failed: An error has occurred while initializing Orleans HubLifetimeManager {hubName} (serverId: {serverId})", _hubName, _serverId);

			_streamProvider = null;
			_serverStreamsReplicaContainer = null;
			_allStream = null;
			if (_timer != null)
			{
				await _timer.DisposeAsync();
				_timer = null;
			}

			throw;
		}

		_logger.LogInformation("Initialized complete: Orleans HubLifetimeManager {hubName} (serverId: {serverId})", _hubName, _serverId);
	}

	private Task ProcessAllMessage(AllMessage message)
	{
		var allTasks = new List<Task>(_connections.Count);
		var payload = message.Payload;

		foreach (var connection in _connections)
		{
			if (connection.ConnectionAborted.IsCancellationRequested)
				continue;

			if (message.ExcludedIds == null || !message.ExcludedIds.Contains(connection.ConnectionId))
				allTasks.Add(SendLocal(connection, payload));
		}
		return Task.WhenAll(allTasks);
	}

	private Task ProcessServerMessage(ClientMessage message)
	{
		var connection = _connections[message.ConnectionId];
		if (connection == null)
		{
			_logger.LogWarning("Connection {connectionId} not found when sending {hubName}.{targetMethod} (serverId: {serverId})",
				message.ConnectionId, _hubName, message.Payload.Target, _serverId);
			return Task.CompletedTask;
		}

		return SendLocal(connection, message.Payload);
	}

	public override async Task OnConnectedAsync(HubConnectionContext connection)
	{
		await EnsureStreamSetup();

		try
		{
			_connections.Add(connection);

			var client = _clusterClient.GetClientGrain(_hubName, connection.ConnectionId);
			await client.OnConnect(_serverId);

			_logger.LogInformation("Connected {connectionId} on hub {hubName} with userId {userId} (serverId: {serverId})",
				connection.ConnectionId, _hubName, connection.UserIdentifier, _serverId);

			if (connection.User.Identity?.IsAuthenticated is true)
			{
				var user = _clusterClient.GetUserGrain(_hubName, connection.UserIdentifier);
				await user.Add(connection.ConnectionId);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An error has occurred 'OnConnectedAsync' while adding connection {connectionId} on hub {hubName} with userId {userId} (serverId: {serverId})",
				connection.ConnectionId, _hubName, connection.UserIdentifier, _serverId);
			_connections.Remove(connection);
			throw;
		}
	}

	public override async Task OnDisconnectedAsync(HubConnectionContext connection)
	{
		try
		{
			_logger.LogInformation("Disconnection {connectionId} on hub {hubName} with userId {userId} (serverId: {serverId})",
				connection.ConnectionId, _hubName, connection.UserIdentifier, _serverId);
			var client = _clusterClient.GetClientGrain(_hubName, connection.ConnectionId);
			await client.OnDisconnect(ClientDisconnectReasons.HubDisconnect);
		}
		finally
		{
			_connections.Remove(connection);
		}
	}

	public override Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default)
	{
		var message = new InvocationMessage(methodName, args);
		return _allStream.OnNextAsync(new() { Payload = message });
	}

	public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds,
		CancellationToken cancellationToken = default)
	{
		var message = new InvocationMessage(methodName, args);
		return _allStream.OnNextAsync(new() { Payload = message, ExcludedIds = excludedConnectionIds });
	}

	public override Task SendConnectionAsync(string connectionId, string methodName, object[] args,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(connectionId)) throw new ArgumentNullException(nameof(connectionId));
		if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentNullException(nameof(methodName));

		var message = new InvocationMessage(methodName, args);

		var connection = _connections[connectionId];
		if (connection != null)
			return SendLocal(connection, message);

		return SendExternal(connectionId, message);
	}

	public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args,
		CancellationToken cancellationToken = default)
	{
		var tasks = connectionIds.Select(c => SendConnectionAsync(c, methodName, args, cancellationToken));
		return Task.WhenAll(tasks);
	}

	public override Task SendGroupAsync(string groupName, string methodName, object[] args,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(groupName)) throw new ArgumentNullException(nameof(groupName));
		if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentNullException(nameof(methodName));

		var group = _clusterClient.GetGroupGrain(_hubName, groupName);
		return group.Send(methodName, args);
	}

	public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args,
		CancellationToken cancellationToken = default)
	{
		var tasks = groupNames.Select(g => SendGroupAsync(g, methodName, args, cancellationToken));
		return Task.WhenAll(tasks);
	}

	public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(groupName)) throw new ArgumentNullException(nameof(groupName));
		if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentNullException(nameof(methodName));

		var group = _clusterClient.GetGroupGrain(_hubName, groupName);
		return group.SendExcept(methodName, args, excludedConnectionIds);
	}

	public override Task SendUserAsync(string userId, string methodName, object[] args,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
		if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentNullException(nameof(methodName));

		var user = _clusterClient.GetUserGrain(_hubName, userId);
		return user.Send(methodName, args);
	}

	public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args,
		CancellationToken cancellationToken = default)
	{
		var tasks = userIds.Select(u => SendGroupAsync(u, methodName, args, cancellationToken));
		return Task.WhenAll(tasks);
	}

	public override Task AddToGroupAsync(string connectionId, string groupName,
		CancellationToken cancellationToken = default)
	{
		var group = _clusterClient.GetGroupGrain(_hubName, groupName);
		return group.Add(connectionId);
	}

	public override Task RemoveFromGroupAsync(string connectionId, string groupName,
		CancellationToken cancellationToken = default)
	{
		var group = _clusterClient.GetGroupGrain(_hubName, groupName);
		return group.Remove(connectionId);
	}

	private Task SendLocal(HubConnectionContext connection, HubInvocationMessage hubMessage)
	{
		_logger.LogDebug("Sending local message to connection {connectionId} on hub {hubName} (serverId: {serverId})",
			connection.ConnectionId, _hubName, _serverId);
		return connection.WriteAsync(hubMessage).AsTask();
	}

	private Task SendExternal(string connectionId, InvocationMessage hubMessage)
	{
		var client = _clusterClient.GetClientGrain(_hubName, connectionId);
		return client.Send(hubMessage.AsImmutable());
	}

	public async ValueTask DisposeAsync()
	{
		var toUnsubscribe = new List<Task>();
		if (_serverStreamsReplicaContainer != null)
		{
			var subscriptions = await _serverStreamsReplicaContainer.GetAllSubscriptionHandles();
			toUnsubscribe.AddRange(subscriptions.Select(s => s.UnsubscribeAsync()));
		}

		if (_allStream != null)
		{
			var subscriptions = await _allStream.GetAllSubscriptionHandles();
			toUnsubscribe.AddRange(subscriptions.Select(s => s.UnsubscribeAsync()));
		}

		var serverDirectoryGrain = _clusterClient.GetServerDirectoryGrain();
		toUnsubscribe.Add(serverDirectoryGrain.Unregister(_serverId));

		await Task.WhenAll(toUnsubscribe.ToArray());

		if (_timer != null)
			await _timer.DisposeAsync();
	}
}
