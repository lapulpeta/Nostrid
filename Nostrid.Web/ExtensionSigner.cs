using Microsoft.JSInterop;
using Newtonsoft.Json;
using NNostr.Client;
using Nostrid.Data;

internal class ExtensionSigner : ISigner
{
	private bool init;
	private string? _pubKey;
	private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

	public ExtensionSigner(IJSRuntime jsRuntime)
	{
		_moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./signer.js").AsTask());
	}

	public async Task<bool> CanSign(string pubKey)
	{
		return await GetPubKey() == pubKey;
	}

	public async Task<string?> GetPubKey()
	{
		if (init)
			return _pubKey;

		var module = await _moduleTask.Value;
		_pubKey = await module.InvokeAsync<string>("getPublicKey");
		init = true;
		return _pubKey;
	}

	public async Task<bool> Sign(NostrEvent ev)
	{
		try
		{
			var module = await _moduleTask.Value;
			var extJson = JsonConvert.SerializeObject(ev);
			extJson = await module.InvokeAsync<string>("signEvent", extJson);
			var evSigned = JsonConvert.DeserializeObject<NostrEvent>(extJson);
			ev.Id = evSigned.Id;
			ev.Signature = evSigned.Signature;
			ev.PublicKey = evSigned.PublicKey;
			return ev != null && !string.IsNullOrEmpty(ev.Signature);
		}
		catch
		{
			return false;
		}
	}
}