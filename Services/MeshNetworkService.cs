using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FoodApp.Models;

namespace FoodApp.Services
{
    public enum SyncDataType
    {
        MealPlan,
        ChatMessage,
        User,
        UserList,
        SyncRequest,
        SyncAck
    }

    public class SyncPacket
    {
        public string PacketId { get; set; } = Guid.NewGuid().ToString();
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = "Unknown";
        public SyncDataType DataType { get; set; }
        public string JsonData { get; set; } = string.Empty;
        public long Version { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int HopCount { get; set; } = 0;
        public int MaxHops { get; set; } = 10;
    }

    public class PeerInfo
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public IPEndPoint? Endpoint { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public bool IsDirectlyConnected { get; set; } = true;
    }

    public class MeshNetworkService
    {
        private UdpClient? _udpClient;
        private readonly int _port = 8888;
        private readonly int _relayPort = 8889;
        private bool _isRunning = false;
        private string _deviceId = string.Empty;
        private string _deviceName = string.Empty;
        
        private readonly Dictionary<string, PeerInfo> _knownPeers = new();
        private readonly HashSet<string> _processedPackets = new();
        private readonly object _lockObj = new object();
        
        private readonly TimeSpan _peerTimeout = TimeSpan.FromMinutes(2);
        private readonly TimeSpan _packetCacheTimeout = TimeSpan.FromMinutes(5);

        public event EventHandler<MealPlan>? MealPlanReceived;
        public event EventHandler<ChatMessage>? ChatMessageReceived;
        public event EventHandler<User>? UserReceived;
        public event EventHandler<List<User>>? UserListReceived;
        public event EventHandler<string>? LogMessage;
        public event EventHandler<List<PeerInfo>>? PeersChanged;

        public MeshNetworkService()
        {
            _deviceId = Preferences.Get("DeviceId", Guid.NewGuid().ToString());
            Preferences.Set("DeviceId", _deviceId);
            _deviceName = DeviceInfo.Name ?? "Unknown";
        }

        public async Task StartAsync()
        {
            try
            {
                _udpClient = new UdpClient();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
                _udpClient.EnableBroadcast = true;
                
                _isRunning = true;

                _ = ReceiveLoopAsync();
                _ = BroadcastPresenceAsync();
                _ = CleanupLoopAsync();
                _ = RelayListenerAsync();

                Log("شبکه Mesh استارت شد");
                Log($"Device ID: {_deviceId}");
            }
            catch (Exception ex)
            {
                Log($"خطای شبکه: {ex.Message}");
            }
        }

        private async Task ReceiveLoopAsync()
        {
            while (_isRunning && _udpClient != null)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    var json = Encoding.UTF8.GetString(result.Buffer);
                    
                    if (string.IsNullOrWhiteSpace(json)) continue;
                    
                    var packet = JsonSerializer.Deserialize<SyncPacket>(json);
                    if (packet == null || packet.SenderId == _deviceId) continue;
                    
                    if (packet.HopCount > packet.MaxHops) continue;

                    await ProcessPacketAsync(packet, result.RemoteEndPoint);
                }
                catch (Exception ex)
                {
                    Log($"خطای دریافت: {ex.Message}");
                }
            }
        }

        private async Task ProcessPacketAsync(SyncPacket packet, IPEndPoint senderEndpoint)
        {
            lock (_lockObj)
            {
                if (_processedPackets.Contains(packet.PacketId)) return;
                _processedPackets.Add(packet.PacketId);
            }

            UpdatePeer(packet.SenderId, packet.SenderName, senderEndpoint, true);

            switch (packet.DataType)
            {
                case SyncDataType.MealPlan:
                    var mealPlan = JsonSerializer.Deserialize<MealPlan>(packet.JsonData);
                    if (mealPlan != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() => MealPlanReceived?.Invoke(this, mealPlan));
                    }
                    break;

                case SyncDataType.ChatMessage:
                    var chatMsg = JsonSerializer.Deserialize<ChatMessage>(packet.JsonData);
                    if (chatMsg != null && !AuthService.IsManager())
                    {
                        MainThread.BeginInvokeOnMainThread(() => ChatMessageReceived?.Invoke(this, chatMsg));
                    }
                    break;

                case SyncDataType.User:
                    var user = JsonSerializer.Deserialize<User>(packet.JsonData);
                    if (user != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() => UserReceived?.Invoke(this, user));
                    }
                    break;

                case SyncDataType.UserList:
                    var userList = JsonSerializer.Deserialize<List<User>>(packet.JsonData);
                    if (userList != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() => UserListReceived?.Invoke(this, userList));
                    }
                    break;

                case SyncDataType.SyncRequest:
                    await HandleSyncRequestAsync(packet.SenderId);
                    break;
            }

            await RelayPacketAsync(packet, senderEndpoint);
        }

        private async Task RelayPacketAsync(SyncPacket packet, IPEndPoint originalSender)
        {
            if (packet.HopCount >= packet.MaxHops) return;

            packet.HopCount++;

            try
            {
                var json = JsonSerializer.Serialize(packet);
                var bytes = Encoding.UTF8.GetBytes(json);

                List<PeerInfo> peersToRelay;
                lock (_lockObj)
                {
                    peersToRelay = _knownPeers.Values
                        .Where(p => p.Endpoint != null && !p.Endpoint.Equals(originalSender))
                        .ToList();
                }

                foreach (var peer in peersToRelay)
                {
                    if (peer.Endpoint != null)
                    {
                        try
                        {
                            await _udpClient!.SendAsync(bytes, bytes.Length, peer.Endpoint);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"خطای relay: {ex.Message}");
            }
        }

        private async Task RelayListenerAsync()
        {
            try
            {
                using var relayClient = new UdpClient(_relayPort);
                while (_isRunning)
                {
                    var result = await relayClient.ReceiveAsync();
                    var json = Encoding.UTF8.GetString(result.Buffer);
                    var packet = JsonSerializer.Deserialize<SyncPacket>(json);
                    
                    if (packet != null && packet.SenderId != _deviceId)
                    {
                        await ProcessPacketAsync(packet, result.RemoteEndPoint);
                    }
                }
            }
            catch { }
        }

        private void UpdatePeer(string deviceId, string deviceName, IPEndPoint endpoint, bool isDirect)
        {
            lock (_lockObj)
            {
                _knownPeers[deviceId] = new PeerInfo
                {
                    DeviceId = deviceId,
                    DeviceName = deviceName,
                    Endpoint = endpoint,
                    LastSeen = DateTime.Now,
                    IsDirectlyConnected = isDirect
                };
            }
            MainThread.BeginInvokeOnMainThread(() => PeersChanged?.Invoke(this, GetPeers()));
        }

        private async Task BroadcastPresenceAsync()
        {
            while (_isRunning)
            {
                await Task.Delay(10000);
                
                var packet = new SyncPacket
                {
                    SenderId = _deviceId,
                    SenderName = _deviceName,
                    DataType = SyncDataType.SyncRequest,
                    JsonData = "{}"
                };
                
                await SendPacketAsync(packet, true);
            }
        }

        private async Task CleanupLoopAsync()
        {
            while (_isRunning)
            {
                await Task.Delay(30000);
                
                lock (_lockObj)
                {
                    var now = DateTime.Now;
                    var expiredPeers = _knownPeers
                        .Where(p => now - p.Value.LastSeen > _peerTimeout)
                        .Select(p => p.Key)
                        .ToList();
                    
                    foreach (var peerId in expiredPeers)
                    {
                        _knownPeers.Remove(peerId);
                    }
                    
                    if (expiredPeers.Count > 0)
                    {
                        MainThread.BeginInvokeOnMainThread(() => PeersChanged?.Invoke(this, GetPeers()));
                    }
                }
            }
        }

        private async Task HandleSyncRequestAsync(string requesterId)
        {
            await Task.Delay(100);
        }

        public async Task BroadcastMealPlanAsync(MealPlan mealPlan)
        {
            var packet = new SyncPacket
            {
                SenderId = _deviceId,
                SenderName = AuthService.GetCurrentUser()?.Name ?? "Unknown",
                DataType = SyncDataType.MealPlan,
                JsonData = JsonSerializer.Serialize(mealPlan),
                Version = mealPlan.Version
            };
            
            await SendPacketAsync(packet, true);
        }

        public async Task BroadcastChatMessageAsync(ChatMessage message)
        {
            if (AuthService.IsManager()) return;

            var packet = new SyncPacket
            {
                SenderId = _deviceId,
                SenderName = AuthService.GetCurrentUser()?.Name ?? "Unknown",
                DataType = SyncDataType.ChatMessage,
                JsonData = JsonSerializer.Serialize(message),
                Version = message.Version
            };
            
            await SendPacketAsync(packet, true);
        }

        public async Task BroadcastUserAsync(User user)
        {
            var packet = new SyncPacket
            {
                SenderId = _deviceId,
                SenderName = AuthService.GetCurrentUser()?.Name ?? "Unknown",
                DataType = SyncDataType.User,
                JsonData = JsonSerializer.Serialize(user),
                Version = DateTime.Now.Ticks
            };
            
            await SendPacketAsync(packet, true);
        }

        public async Task BroadcastUserListAsync(List<User> users)
        {
            var packet = new SyncPacket
            {
                SenderId = _deviceId,
                SenderName = AuthService.GetCurrentUser()?.Name ?? "Unknown",
                DataType = SyncDataType.UserList,
                JsonData = JsonSerializer.Serialize(users),
                Version = DateTime.Now.Ticks
            };
            
            await SendPacketAsync(packet, true);
        }

        public async Task RequestSyncAsync()
        {
            var packet = new SyncPacket
            {
                SenderId = _deviceId,
                SenderName = _deviceName,
                DataType = SyncDataType.SyncRequest,
                JsonData = "{}"
            };
            
            await SendPacketAsync(packet, true);
        }

        private async Task SendPacketAsync(SyncPacket packet, bool broadcast)
        {
            if (_udpClient == null) return;
            
            try
            {
                var json = JsonSerializer.Serialize(packet);
                var bytes = Encoding.UTF8.GetBytes(json);

                if (broadcast)
                {
                    await _udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, _port));
                }

                List<PeerInfo> peers;
                lock (_lockObj)
                {
                    peers = _knownPeers.Values.Where(p => p.Endpoint != null).ToList();
                }

                foreach (var peer in peers)
                {
                    if (peer.Endpoint != null)
                    {
                        try
                        {
                            await _udpClient.SendAsync(bytes, bytes.Length, peer.Endpoint);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"خطای ارسال: {ex.Message}");
            }
        }

        public List<PeerInfo> GetPeers()
        {
            lock (_lockObj)
            {
                return _knownPeers.Values.ToList();
            }
        }

        private void Log(string message) => LogMessage?.Invoke(this, message);

        public void Stop()
        {
            _isRunning = false;
            _udpClient?.Close();
            _udpClient?.Dispose();
        }
    }
}
