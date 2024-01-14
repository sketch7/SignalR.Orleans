using Orleans.Runtime;
using Orleans.Storage;

namespace SignalR.Orleans.Tests.Models;

internal sealed class DumbGrainStore : IGrainStorage
{
	public Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState) => Task.CompletedTask;

	public Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState) => Task.CompletedTask;

	public Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState) => Task.CompletedTask;
}
