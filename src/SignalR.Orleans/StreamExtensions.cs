using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignalR.Orleans
{
    // todo: move to Stream Utils?
    internal static class StreamExtensions
    {
        private static readonly Random Randomizer = new Random();

        /// <summary>
        /// Get stream sharded by replicas.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="streamProvider"></param>
        /// <param name="streamId"></param>
        /// <param name="streamNamespace"></param>
        /// <param name="replicas">Max replicas to obtain stream from.</param>
        /// <returns></returns>
        public static IAsyncStream<T> GetStream<T>(this IStreamProvider streamProvider, Guid streamId, string streamNamespace, int replicas)
            => streamProvider.GetStream<T>($"{streamId}:{Randomizer.Next(1, replicas + 1)}", streamNamespace);

    }

    /// <summary>
    /// Contains streams replicated for sharding used for listening multiple replicas easier.
    /// </summary>
    internal class StreamReplicaContainer<T>
    {
        public string StreamId { get; }
        public string StreamNamespace { get; }
        public int MaxReplicas { get; }
        public int Count { get; }

        private List<IAsyncStream<T>> _streams = new List<IAsyncStream<T>>();


        /// <summary>
        /// 
        /// </summary>
        /// <param name="streamProvider"></param>
        /// <param name="streamId"></param>
        /// <param name="streamNamespace"></param>
        /// <param name="maxReplicas">Max replicas</param>

        public StreamReplicaContainer(IStreamProvider streamProvider, string streamId, string streamNamespace, int maxReplicas)
        {
            StreamId = streamId;
            StreamNamespace = streamNamespace;
            MaxReplicas = maxReplicas;

            for (int i = 0; i < maxReplicas; i++)
                _streams.Add(streamProvider.GetStream<T>(streamId, streamNamespace));
        }


        public StreamReplicaContainer(IStreamProvider streamProvider, Guid streamId, string streamNamespace,
            int maxReplicas) : this(streamProvider, streamId.ToString(), streamNamespace, maxReplicas)
        {

        }

        public async Task SubscribeAsync(Func<T, StreamSequenceToken, Task> onNextAsync)
        {
            var tasks = new List<Task>(MaxReplicas);

            foreach (var stream in _streams)
                tasks.Add(stream.SubscribeAsync(onNextAsync));

            await Task.WhenAll(tasks);
        }
    }
}