using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;
using System.Text;

namespace NNostr.Client
{
    public class NostrClient : IDisposable
    {
        private readonly Uri _relay;
        protected ClientWebSocket? websocket;
        private CancellationTokenSource? _Cts;
        private CancellationTokenSource messageCts = new();

        public NostrClient(Uri relay)
        {
            _relay = relay;
        }

        public Task Disconnect()
        {
            _Cts?.Cancel();
            return Task.CompletedTask;
        }

        public EventHandler<string>? MessageReceived;
        public EventHandler<string>? NoticeReceived;
        public EventHandler<(string subscriptionId, NostrEvent[] events)>? EventsReceived;
        public EventHandler<string>? EoseReceived;

        public async Task Connect(CancellationToken token = default)
        {
            _Cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            while (!_Cts.IsCancellationRequested)
            {
                await ConnectAndWaitUntilConnected(_Cts.Token);
                _ = ListenForMessages();
                websocket!.Abort();
            }
        }

        public async IAsyncEnumerable<string> ListenForRawMessages()
        {
            var buffer = new ArraySegment<byte>(new byte[2048]);
            while (websocket.State == WebSocketState.Open && !_Cts.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                await using var ms = new MemoryStream();
                do
                {
                    result = await websocket!.ReceiveAsync(buffer, _Cts.Token);
                    ms.Write(buffer.Array!, buffer.Offset, result.Count);
                } while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                yield return Encoding.UTF8.GetString(ms.ToArray());

                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }

            websocket.Abort();
        }


        public async Task ListenForMessages()
        {
            await foreach (var message in ListenForRawMessages())
            {
                HandleIncomingMessage(message, _Cts.Token);
                MessageReceived?.Invoke(this, message);
            }
        }

        private void HandleIncomingMessage(string message, CancellationToken token)
        {
            var json = JToken.Parse(message).Root;
            switch (json[0].Value<string>().ToLowerInvariant())
            {
                case "event":
                    var subscriptionId = json[1].Value<string>();
                    var evt = json[2].ToObject<NostrEvent>();

                    if (evt?.Verify() is true)
                    {
                        EventsReceived?.Invoke(this, (subscriptionId, new[] { evt }));
                    }

                    break;
                case "notice":
                    var noticeMessage = json[1].Value<string>();
                    NoticeReceived?.Invoke(this, noticeMessage);
                    break;
                case "eose":
                    subscriptionId = json[1].Value<string>();
                    EoseReceived?.Invoke(this, subscriptionId);
                    break;
            }
        }

        private async Task<bool> HandleOutgoingMessage(string message, CancellationToken token)
        {
            try
            {
                await WaitUntilConnected(token);
                await websocket.SendMessageAsync(message, token);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> PublishEvent(NostrEvent nostrEvent, CancellationToken token = default)
        {
            var payload = JsonConvert.SerializeObject(new object[] { "EVENT", nostrEvent });
            return await HandleOutgoingMessage(payload, token);
        }

        public async Task<bool> CloseSubscription(string subscriptionId, CancellationToken token = default)
        {
            var payload = JsonConvert.SerializeObject(new[] { "CLOSE", subscriptionId });
            return await HandleOutgoingMessage(payload, token);
        }

        public async Task<bool> CreateSubscription(string subscriptionId, NostrSubscriptionFilter[] filters,
            CancellationToken token = default)
        {
            var payload = JsonConvert.SerializeObject(new object[] { "REQ", subscriptionId }.Concat(filters), settings: new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore,
            });
            return await HandleOutgoingMessage(payload, token);
        }

        public async Task ConnectAndWaitUntilConnected(CancellationToken token = default)
        {
            if (websocket?.State == WebSocketState.Open)
            {
                return;
            }

            _Cts ??= CancellationTokenSource.CreateLinkedTokenSource(token);

            websocket?.Dispose();
            websocket = new ClientWebSocket();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            await websocket.ConnectAsync(_relay, cts.Token);
            await WaitUntilConnected(cts.Token);
        }

        private async Task WaitUntilConnected(CancellationToken token)
        {
            while (websocket == null || (websocket.State != WebSocketState.Open && !token.IsCancellationRequested))
            {
                await Task.Delay(100, token);
            }
        }

        #region Dispose
        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Cleanup();
                }

                _disposed = true;
            }
        }

        private void Cleanup()
        {
            messageCts.Cancel();
            Disconnect();
        }
        #endregion
    }
}