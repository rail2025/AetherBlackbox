using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using AetherBlackbox.Serialization;
using Newtonsoft.Json.Linq;

namespace AetherBlackbox.Networking
{
    public class NetworkManager : IDisposable
    {
        public const string ApiBaseUrl = "https://AetherBlackbox-server.onrender.com";

        private ClientWebSocket? webSocket;
        private CancellationTokenSource? cancellationTokenSource;        
        public event Action? OnConnected;
        public event Action? OnDisconnected;
        public event Action<string>? OnError;
        public event Action<NetworkPayload>? OnStateUpdateReceived;
        public event Action? OnRoomClosingWarning;
        public event Action<bool>? OnHostStatusReceived;

        public bool IsConnected => webSocket?.State == WebSocketState.Open;

        public async Task ConnectAsync(string serverUri, string passphrase)
        {
            if (IsConnected) return;

            try
            {
                webSocket = new ClientWebSocket();
                cancellationTokenSource = new CancellationTokenSource();
                Uri connectUri = new Uri($"{serverUri}?passphrase={Uri.EscapeDataString(passphrase)}&client=ad");

                await webSocket.ConnectAsync(connectUri, cancellationTokenSource.Token);

                OnConnected?.Invoke();
                _ = Task.Run(() => StartListening(cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Connection failed: {ex.Message}");
                await DisconnectAsync();
            }
        }

        public async Task DisconnectAsync()
        {
            if (webSocket == null) return;

            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }

            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    // Use a timeout for closing the connection gracefully.
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", cts.Token);
                }
                catch (Exception) { /* This is expected if the connection was abruptly terminated. */ }
            }

            webSocket?.Dispose();
            webSocket = null;
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            OnDisconnected?.Invoke();
        }

        private async Task StartListening(CancellationToken cancellationToken)
        {
            // A larger buffer can be more efficient for receiving potentially large messages.
            var buffer = new byte[8192];
            try
            {
                while (webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await DisconnectAsync();
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        string text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                        HandleTextMessage(text);
                    }
                    else
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        HandleReceivedMessage(ms.ToArray());
                    }
                }
            }
            catch (OperationCanceledException) { /* Expected when disconnecting. */ }
            catch (Exception ex)
            {
                OnError?.Invoke($"Network error: {ex.Message}");
                await DisconnectAsync();
            }
        }

        private void HandleTextMessage(string json)
        {
            try
            {
                var obj = JObject.Parse(json);
                var type = obj["type"]?.ToString();

                if (type == "HOST_STATUS")
                {
                    var payload = obj["payload"];
                    if (payload != null)
                    {
                        var hostToken = payload["isHost"] ?? payload["IsHost"];
                        bool isHost = hostToken?.ToObject<bool>() ?? false;
                        OnHostStatusReceived?.Invoke(isHost);
                    }
                }
                else if (type == "ERROR")
                {
                    string msg = obj["message"]?.ToString() ?? "Unknown Error";
                    OnError?.Invoke(msg);
                }
                else if (type == "ROOM_CLOSING_IMMINENTLY")
                {
                    OnRoomClosingWarning?.Invoke();
                }
            }
            catch (Exception ex)
            {
                AetherBlackbox.Plugin.Log?.Error($"Failed to parse text message: {ex.Message}");
            }
        }

        private void HandleReceivedMessage(byte[] messageBytes)
        {
            if (messageBytes.Length < 1) return;

            MessageType type = (MessageType)messageBytes[0];
            byte[] payloadBytes = new byte[messageBytes.Length - 1];
            Array.Copy(messageBytes, 1, payloadBytes, 0, payloadBytes.Length);

            switch (type)
            {
                case MessageType.STATE_UPDATE:
                    var payload = PayloadSerializer.Deserialize(payloadBytes);
                    if (payload != null)
                    {
                        OnStateUpdateReceived?.Invoke(payload);
                    }
                    break;

                case MessageType.ROOM_CLOSING_IMMINENTLY:
                    OnRoomClosingWarning?.Invoke();
                    break;
            }
        }

        public async Task SendStateUpdateAsync(NetworkPayload payload)
        {
            if (!IsConnected || webSocket == null || cancellationTokenSource == null) return;

            try
            {
                // Serialize the high-level payload object into a byte array.
                byte[] payloadBytes = PayloadSerializer.Serialize(payload);

                // Prepend the message type byte to the payload.
                byte[] messageToSend = new byte[1 + payloadBytes.Length];
                messageToSend[0] = (byte)MessageType.STATE_UPDATE;
                Array.Copy(payloadBytes, 0, messageToSend, 1, payloadBytes.Length);

                await webSocket.SendAsync(new ArraySegment<byte>(messageToSend), WebSocketMessageType.Binary, true, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to send message: {ex.Message}");
                await DisconnectAsync();
            }
        }

        public Task SendSessionLockAsync(bool isLocked)
        {
            var payload = new NetworkPayload { 
                PageIndex = -1,
                Action = PayloadActionType.SessionLock,
                Data = BitConverter.GetBytes(isLocked)
            };
            return SendStateUpdateAsync(payload);
        }

        public void Dispose()
        {
            // Ensure disconnection is awaited to prevent resource leaks.
            DisconnectAsync().GetAwaiter().GetResult();
        }
    }
}
