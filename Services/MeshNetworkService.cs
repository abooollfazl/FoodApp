using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FoodApp.Models;

namespace FoodApp.Services
{
    public enum SyncDataType
    {
        MealPlan,
        ChatMessage,
        User,
        SyncRequest,
        PeerDiscovery
    }

    public class SyncPacket
    {
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string? TargetId { get; set; } // null = broadcast
        public SyncDataType DataType { get; set; }
        public string JsonData { get; set; } = string.Empty;
        public long Version { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class MeshNetworkService
    {
        private UdpClient? _udpClient;
        private readonly int _port = 8888;
        private bool _isRunning = false;
        private string _deviceId = string.Empty;
        private string _deviceName = string.Empty;

        // Thread-safe collections
        private readonly ConcurrentDictionary<string, IPEndPoint> _knownPeers = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastSeen = new();
        private readonly HashSet<string> _processedPackets = new();
        private readonly object _lockObj = new();

        // Events
        public event EventHandler<MealPlan>? MealPlanReceived;
        public event EventHandler<ChatMessage>? ChatMessageReceived;
        public event EventHandler<User>? UserReceived;
        public event EventHandler<string>? SyncRequestReceived;
        public event EventHandler<string>? PeerDiscovered;
        public event EventHandler<string>? LogMessage;

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
                _ = CleanupStalePeersAsync();

                Log("شبکه Mesh استارت شد");
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
                    if (packet?.SenderId == null || packet.SenderId == _deviceId) continue;

                    // Update peer info
                    _knownPeers[packet.SenderId] = result.RemoteEndPoint;
                    _lastSeen[packet.SenderId] = DateTime.Now;

                    // Check for duplicate packets (prevent loops)
                    var packetHash = $"{packet.SenderId}_{packet.Timestamp.Ticks}_{packet.DataType}";
                    lock (_lockObj)
                    {
                        if (_processedPackets.Contains(packetHash)) continue;
                        _processedPackets.Add(packetHash);
                        
                        // Cleanup old entries
                        if (_processedPackets.Count > 5000)
                        {
                            var toRemove = _processedPackets.Take(3000).ToList();
                            foreach (var p in toRemove) _processedPackets.Remove(p);
                        }
                    }

                    // Process packet
                    ProcessPacket(packet, result.RemoteEndPoint);
                }
                catch (Exception ex)
                {
                    Log($"خطای دریافت: {ex.Message}");
                }
            }
        }

        private void ProcessPacket(SyncPacket packet, IPEndPoint senderEndpoint)
        {
            // Targeted packet and not for us? Relay it
            if (!string.IsNullOrEmpty(packet.TargetId) && packet.TargetId != _deviceId)
            {
                _ = RelayPacketAsync(packet, senderEndpoint);
                return;
            }

            switch (packet.DataType)
            {
                case SyncDataType.MealPlan:
                    var mealPlan = JsonSerializer.Deserialize<MealPlan>(packet.JsonData);
                    if (mealPlan != null)
                    {
                        MealPlanReceived?.Invoke(this, mealPlan);
                        // Relay to others (mesh)
                        _ = RelayPacketAsync(packet, senderEndpoint);
                    }
                    break;

                case SyncDataType.ChatMessage:
                    var chatMsg = JsonSerializer.Deserialize<ChatMessage>(packet.JsonData);
                    if (chatMsg != null)
                    {
                        ChatMessageReceived?.Invoke(this, chatMsg);
                        // Relay to others (mesh)
                        _ = RelayPacketAsync(packet, senderEndpoint);
                    }
                    break;

                case SyncDataType.User:
                    var user = JsonSerializer.Deserialize<User>(packet.JsonData);
                    if (user != null)
                    {
                        UserReceived?.Invoke(this, user);
                        // Relay to others (mesh)
                        _ = RelayPacketAsync(packet, senderEndpoint);
                    }
                    break;

                case SyncDataType.SyncRequest:
                    SyncRequestReceived?.Invoke(this, packet.SenderId);
                    break;

                case SyncDataType.PeerDiscovery:
                    PeerDiscovered?.Invoke(this, packet.SenderId);
                    break;
            }
        }

        private async Task RelayPacketAsync(SyncPacket packet, IPEndPoint originalSender)
        {
            try
            {
                // Don't relay targeted packets
                if (!string.IsNullOrEmpty(packet.TargetId)) return;

                var json = JsonSerializer.Serialize(packet);
                var bytes = Encoding.UTF8.GetBytes(json);

                // Relay to all known peers except original sender
                foreach (var peer in _knownPeers)
                {
                    if (!peer.Value.Equals(originalSender))
                    {
                        await _udpClient!.SendAsync(bytes, bytes.Length, peer.Value);
                    }
                }
            }
            catch { /* Ignore relay errors */ }
        }

        // ========== Public Send Methods ==========

        public async Task BroadcastMealPlanAsync(MealPlan mealPlan)
        {
            var currentUser = AuthService.GetCurrentUser();
            var packet = new SyncPacket
            {
                SenderId = _deviceId,
                SenderName = currentUser?.Name ?? _deviceName,
                DataType = SyncDataType.MealPlan,
                JsonData = JsonSerializer.Serialize(mealPlan),
                Version = mealPlan.Version
            };
            await BroadcastPacketAsync(packet);
        }

        public async Task SendMealPlanToPeerAsync(MealPlan mealPlan, string targetPeerId)
        {
            var currentUser = AuthService.GetCurrentUser();
            var packet = new SyncPacket
            {
                SenderId = _deviceId,
                SenderName = currentUser?.Name ?? _deviceName,
                TargetId = targetPeerId,
                DataType = SyncDataType.MealPlan,
                JsonData = JsonSerializer.Serialize(mealPlan),
                Version = mealPlan.Version
            };
            await SendToPeerAsync(packet, targetPeerId);
        }

        public async Task BroadcastChatMessageAsync(ChatMessage message)
        {
            var currentUser = AuthService.GetCurrentUser();
            var packet = new SyncPacket
            {
                SenderId = _deviceId,
                SenderName = currentUser?.Name ?? _deviceName,
                DataType = SyncDataType.ChatMessage,
                JsonData = JsonSerializer.Serialize(message),
                Version = message.Version
            };
            await BroadcastPacketAsync(packet);
        }

        public async Task BroadcastUserAsync(User user)
        {
            var currentUser = AuthService.GetCurrentUser();
            var packet = new SyncPacket
            {
                SenderId = _deviceId,
                SenderName = currentUser?.Name ?? _deviceName,
                DataType = SyncDataType.User,
                JsonData = JsonSerializer.Serialize(user),
                Version = 1
            };
            await BroadcastPacketAsync(packet);
        }

        public async Task SendUserToPeerAsync(User user, string targetPeerId)
        {
            var currentUser = AuthService.GetCurrentUser();
            var packet = new SyncPacket
            {
                SenderId = _deviceId,
                SenderName = currentUser?.Name ?? _deviceName,
                TargetId = targetPeerId,
                DataType = SyncDataType.User,
                JsonData = JsonSerializer.Serialize(user),
                Version = 1
            };
            await SendToPeerAsync(packet, targetPeerId);
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
            await BroadcastPacketAsync(packet);
        }

        // ========== Private Send Helpers ==========

        private async Task BroadcastPacketAsync(SyncPacket packet)
        {
            try
            {
                var json = JsonSerializer.Serialize(packet);
                var bytes = Encoding.UTF8.GetBytes(json);

                // Broadcast to local network
                await _udpClient!.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, _port));

                // Also send to all known peers directly (more reliable)
                foreach (var peer in _knownPeers.Values)
                {
                    await _udpClient.SendAsync(bytes, bytes.Length, peer);
                }
            }
            catch (Exception ex)
            {
                Log($"خطای ارسال: {ex.Message}");
            }
        }

        private async Task SendToPeerAsync(SyncPacket packet, string peerId)
        {
            try
            {
                if (_knownPeers.TryGetValue(peerId, out var endpoint))
                {
                    var json = JsonSerializer.Serialize(packet);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await _udpClient!.SendAsync(bytes, bytes.Length, endpoint);
                }
            }
            catch { }
        }

        // ========== Background Tasks ==========

        private async Task BroadcastPresenceAsync()
        {
            while (_isRunning)
            {
                await Task.Delay(5000); // Every 5 seconds

                var packet = new SyncPacket
                {
                    SenderId = _deviceId,
                    SenderName = _deviceName,
                    DataType = SyncDataType.PeerDiscovery,
                    JsonData = "{}"
                };

                await BroadcastPacketAsync(packet);
            }
        }

        private async Task CleanupStalePeersAsync()
        {
            while (_isRunning)
            {
                await Task.Delay(30000); // Every 30 seconds

                var cutoff = DateTime.Now.AddMinutes(-2);
                var stalePeers = _lastSeen.Where(x => x.Value < cutoff).Select(x => x.Key).ToList();

                foreach (var peerId in stalePeers)
                {
                    _knownPeers.TryRemove(peerId, out _);
                    _lastSeen.TryRemove(peerId, out _);
                }
            }
        }

        private void Log(string message) => LogMessage?.Invoke(this, message);

        public void Stop()
        {
            _isRunning = false;
            _udpClient?.Close();
            _udpClient?.Dispose();
        }

        public IReadOnlyDictionary<string, IPEndPoint> GetKnownPeers() => _knownPeers;
    }
}
