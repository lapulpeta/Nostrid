using Microsoft.JSInterop;
using Newtonsoft.Json;
using NNostr.Client;
using Nostrid.Data;

internal class ExtensionSigner : ISigner
{
    private bool init;
    private string? pubKey;

    private readonly Lazy<Task<IJSObjectReference>> moduleTask;
    private readonly SerialQueue serialQueue = new();

    public ExtensionSigner(IJSRuntime jsRuntime)
    {
        moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./signer.js").AsTask());
    }

    public async Task<bool> CanSign(string pubKey)
    {
        return await GetPubKey() == pubKey;
    }

    public Task<string?> GetPubKey()
    {
        return serialQueue.Enqueue(GetPubKeyInternal);
    }

    private async Task<string?> GetPubKeyInternal()
    {
        if (init)
            return pubKey;

        try
        {
            var module = await moduleTask.Value;
            pubKey = await module.InvokeAsync<string>("getPublicKey");
            init = true;
            return pubKey;
        }
        catch
        {
            return null;
        }
    }

    public Task<bool> Sign(NostrEvent ev)
    {
        return serialQueue.Enqueue(async () => await SignInternal(ev));
    }

    private async Task<bool> SignInternal(NostrEvent ev)
    {
        try
        {
            var module = await moduleTask.Value;
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

    public Task<string?> EncryptNip04(string pubkey, string content)
    {
        return serialQueue.Enqueue(async () => await EncryptNip04Internal(pubkey, content));
    }

    private async Task<string?> EncryptNip04Internal(string pubkey, string content)
    {
        try
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<string>("encryptNip04", pubkey, content);
        }
        catch
        {
            return null;
        }
    }


    public Task<string?> DecryptNip04(string pubkey, string content)
    {
        return serialQueue.Enqueue(async () => await DecryptNip04Internal(pubkey, content));
    }

    private async Task<string?> DecryptNip04Internal(string pubkey, string content)
    {
        try
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<string>("decryptNip04", pubkey, content);
        }
        catch
        {
            return null;
        }
    }

}