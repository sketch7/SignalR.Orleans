﻿using Microsoft.AspNetCore.SignalR.Protocol;

namespace SignalR.Orleans.Core;

public interface IHubMessageInvoker : IAddressable
{
	/// <summary>
	/// Invokes a method on the hub.
	/// </summary>
	/// <param name="message">Message to invoke.</param>
	Task Send(Immutable<InvocationMessage> message);

	/// <summary>
	/// Invokes a method on the hub.
	/// </summary>
	/// <param name="message">Message to invoke.</param>
	[OneWay]
	Task SendOneWay(Immutable<InvocationMessage> message);
}