using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FoodApp.Database;
using FoodApp.Models;

namespace FoodApp.Services
{
    public class SyncService
    {
        private readonly AppDatabase _database;
        private readonly MeshNetworkService _networkService;
        private readonly HashSet<string> _processedSyncIds = new();
        private readonly string _deviceId;
        private bool _isInitialized = false;

        public SyncService(AppDatabase database, MeshNetworkService networkService)
        {
            _database = database;
            _networkService = networkService;
            _deviceId = Preferences.Get("DeviceId", Guid.NewGuid().ToString());
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            // Subscribe to network events
            _networkService.MealPlanReceived += OnMealPlanReceived;
            _networkService.UserReceived += OnUserReceived;
            _networkService.ChatMessageReceived += OnChatMessageReceived;
            _networkService.SyncRequestReceived += OnSyncRequestReceived;
            _networkService.PeerDiscovered += OnPeerDiscovered;

            _isInitialized = true;
        }

        // ========== Send Methods ==========

        public async Task BroadcastMealPlanAsync(MealPlan plan)
        {
            // ✅ اول TCP به همه peerهای شناخته شده
            var peers = _networkService.GetKnownPeers().Keys.ToList();
            foreach (var peerId in peers)
            {
                await _networkService.SendTcpPacketAsync(new SyncPacket
                {
                    SenderId = _deviceId,
                    DataType = SyncDataType.MealPlan,
                    JsonData = JsonSerializer.Serialize(plan),
                    Version = plan.Version
                }, peerId);
            }

            // ✅ بعد UDP Broadcast
            await _networkService.BroadcastMealPlanAsync(plan);
        }

        public async Task BroadcastUserAsync(User user)
        {
            // ✅ اول TCP به همه peerهای شناخته شده
            var peers = _networkService.GetKnownPeers().Keys.ToList();
            foreach (var peerId in peers)
            {
                await _networkService.SendTcpPacketAsync(new SyncPacket
                {
                    SenderId = _deviceId,
                    DataType = SyncDataType.User,
                    JsonData = JsonSerializer.Serialize(user),
                    Version = 1
                }, peerId);
            }

            // ✅ بعد UDP Broadcast
            await _networkService.BroadcastUserAsync(user);
        }

        public async Task BroadcastChatMessageAsync(string content)
        {
            var currentUser = AuthService.GetCurrentUser();
            if (currentUser == null) return;

            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = currentUser.Id,
                SenderName = currentUser.Name,
                Content = content,
                Timestamp = DateTime.Now,
                Version = 1
            };

            // Chat فقط UDP (real-time)
            await _networkService.BroadcastChatMessageAsync(message);
        }

        public async Task RequestFullSyncAsync()
        {
            await _networkService.RequestSyncAsync();
        }

        // ========== Receive Handlers ==========

        private async void OnMealPlanReceived(object? sender, MealPlan remotePlan)
        {
            try
            {
                if (remotePlan?.Id == null) return;

                var syncId = $"meal_{remotePlan.Id}_{remotePlan.Version}";
                if (_processedSyncIds.Contains(syncId)) return;
                _processedSyncIds.Add(syncId);

                if (_processedSyncIds.Count > 1000)
                {
                    var toRemove = _processedSyncIds.Take(500).ToList();
                    foreach (var id in toRemove) _processedSyncIds.Remove(id);
                }

                var localPlan = await _database.GetMealPlanByIdAsync(remotePlan.Id);

                if (localPlan == null)
                {
                    remotePlan.LastModified = DateTime.Now;
                    await _database.SaveMealPlanAsync(remotePlan);
                }
                else
                {
                    if (remotePlan.LastModified > localPlan.LastModified)
                    {
                        remotePlan.LastModified = DateTime.Now;
                        await _database.UpdateMealPlanAsync(remotePlan);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sync MealPlan error: {ex.Message}");
            }
        }

        private async void OnUserReceived(object? sender, User remoteUser)
        {
            try
            {
                if (remoteUser?.Id == null) return;

                var syncId = $"user_{remoteUser.Id}_{remoteUser.CreatedAt.Ticks}";
                if (_processedSyncIds.Contains(syncId)) return;
                _processedSyncIds.Add(syncId);

                var localUser = await _database.GetUserAsync(remoteUser.Id);

                if (localUser == null)
                {
                    await _database.SaveUserAsync(remoteUser);
                    System.Diagnostics.Debug.WriteLine($"کاربر جدید دریافت شد: {remoteUser.Username}");
                }
                else
                {
                    await _database.UpdateUserAsync(remoteUser);
                    System.Diagnostics.Debug.WriteLine($"کاربر آپدیت شد: {remoteUser.Username}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sync User error: {ex.Message}");
            }
        }

        private void OnChatMessageReceived(object? sender, ChatMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"پیام دریافت شد از {message.SenderName}: {message.Content}");
        }

        private async void OnSyncRequestReceived(object? sender, string requesterId)
        {
            try
            {
                var mealPlans = await _database.GetMealPlansAsync();
                var users = await _database.GetUsersAsync();

                foreach (var plan in mealPlans)
                {
                    await _networkService.SendMealPlanToPeerAsync(plan, requesterId);
                    await Task.Delay(50);
                }

                foreach (var user in users)
                {
                    await _networkService.SendUserToPeerAsync(user, requesterId);
                    await Task.Delay(50);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sync request error: {ex.Message}");
            }
        }

        private void OnPeerDiscovered(object? sender, string peerId)
        {
            _ = RequestFullSyncAsync();
        }
    }
}
