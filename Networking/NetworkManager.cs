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
        public const string ApiBaseUrl = "https://aetherdraw-server.onrender.com";

        private ClientWebSocket? webSocket;
        private CancellationTokenSource? cancellationTokenSource;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        public event Action? OnConnected;
        public event Action? OnDisconnected;
        public event Action<string>? OnError;
        public event Action<NetworkPayload>? OnStateUpdateReceived;
        public event Action? OnRoomClosingWarning;
        public event Action<bool>? OnHostStatusReceived;
        public event Action<string>? OnReplayRequested;
        public event Action? OnHeadersRequested;
        public string LocalClientId { get; } = Guid.NewGuid().ToString("N").Substring(0, 9);
        public event Action<string, float>? OnUserTimeReceived;
        public event Action<string, float>? OnUserPingReceived;
        public event Action<string>? OnUserJoined;
        public event Action<string>? OnUserLeft;

        public bool IsConnected => webSocket?.State == WebSocketState.Open;

        public async Task ConnectAsync(string serverUri, string passphrase)
        {
            if (IsConnected) return;

            try
            {
                webSocket = new ClientWebSocket();
                cancellationTokenSource = new CancellationTokenSource();
                Uri connectUri = new Uri($"{serverUri}?passphrase={Uri.EscapeDataString(passphrase)}&client=abb");

                await webSocket.ConnectAsync(connectUri, cancellationTokenSource.Token);

                OnConnected?.Invoke();
                _ = Task.Run(() => StartListening(cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Connection failed: {ex.Message}");
                await DisconnectAsync();

                _ = ReconnectAsync(serverUri, passphrase);
            }
        }

        private async Task ReconnectAsync(string serverUri, string passphrase)
        {
            await Task.Delay(5000);
            await ConnectAsync(serverUri, passphrase);
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
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", cts.Token);
                }
                catch (Exception) {}
            }

            webSocket?.Dispose();
            webSocket = null;
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            OnDisconnected?.Invoke();
        }

        private async Task StartListening(CancellationToken cancellationToken)
        {
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
            catch (OperationCanceledException) {}
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
                var senderId = obj["senderId"]?.ToString();

                if (senderId != null && senderId != LocalClientId)
                {
                    OnUserJoined?.Invoke(senderId);
                }
                if (type == "USER_JOIN" && senderId != null) OnUserJoined?.Invoke(senderId);
                else if (type == "USER_LEAVE" && senderId != null) OnUserLeft?.Invoke(senderId);
                else if (type == "USER_TIME" && senderId != null)
                {
                    if (float.TryParse(obj["time"]?.ToString(), out float time))
                        OnUserTimeReceived?.Invoke(senderId, time);
                }
                else if (type == "USER_PING" && senderId != null)
                {
                    if (float.TryParse(obj["time"]?.ToString(), out float time))
                        OnUserPingReceived?.Invoke(senderId, time);
                }
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
                else if (type == "REQUEST_REPLAY")
                {
                    string? hash = obj["hash"]?.ToString();
                    if (!string.IsNullOrEmpty(hash))
                    {
                        OnReplayRequested?.Invoke(hash);
                    }
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog?.Error($"Failed to parse text message: {ex.Message}");
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

            await _sendLock.WaitAsync();
            try
            {
                byte[] payloadBytes = PayloadSerializer.Serialize(payload);
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
            finally
            {
                _sendLock.Release();
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
        public Task SendHeadersBroadcastAsync(string jsonPayload)
        {
            var payload = new NetworkPayload
            {
                PageIndex = -1,
                Action = PayloadActionType.BroadcastHeaders,
                Data = System.Text.Encoding.UTF8.GetBytes(jsonPayload)
            };
            return SendStateUpdateAsync(payload);
        }

        public async Task SendJsonAsync(string json)
        {
            if (!IsConnected || webSocket == null || cancellationTokenSource == null) return;

            await _sendLock.WaitAsync();
            try
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
                await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to send json: {ex.Message}");
                await DisconnectAsync();
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public Task BroadcastUserTimeAsync(float time)
        {
            var msg = new JObject { ["type"] = "USER_TIME", ["time"] = time, ["senderId"] = LocalClientId };
            return SendJsonAsync(msg.ToString(Newtonsoft.Json.Formatting.None));
        }

        public Task BroadcastUserPingAsync(float time)
        {
            var msg = new JObject { ["type"] = "USER_PING", ["time"] = time, ["senderId"] = LocalClientId };
            return SendJsonAsync(msg.ToString(Newtonsoft.Json.Formatting.None));
        }

        public void Dispose()
        {
            _ = Task.Run(DisconnectAsync);
        }
    }
}
