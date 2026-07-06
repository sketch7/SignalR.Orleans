using System.Data.HashFunction.xxHash;
using System.Text;

namespace SignalR.Orleans.Core;

internal static class StringExtensions
{
	private static readonly IxxHash HashFunction = xxHashFactory.Instance.Create();

	/// <param name="text">Value to generate hashcode for.</param>
	extension(string text)
	{
		/// <summary>
		/// Get a consistent hashcode number based on a string.
		/// </summary>
		public int ToHashCode()
		{
			var value = HashFunction.ComputeHash(Encoding.UTF8.GetBytes(text));
			return BitConverter.ToInt32(value.Hash, 0);
		}

		/// <summary>
		/// Get a consistent number between 0-<paramref name="maxValue"/> based on a string value.
		/// </summary>
		/// <param name="maxValue">The exclusive upper bound of the number returned e.g. 12 (0-11)</param>
		public int ToPartitionIndex(int maxValue)
		{
			var hashCode = text.ToHashCode();
			return Math.Abs(hashCode % maxValue);
		}
	}
}
