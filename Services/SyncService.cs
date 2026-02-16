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
        private bool _isInitialized = false;

        public SyncService(AppDatabase database, MeshNetworkService networkService)
        {
            _database = database;
            _networkService = networkService;
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            // Subscribe to network events
            _networkService.MealPlanReceived += OnMealPlanReceived;
            _networkService.UserReceived += OnUserReceived;
            _networkService.SyncRequestReceived += OnSyncRequestReceived;
            _networkService.PeerDiscovered += OnPeerDiscovered;

            _isInitialized = true;
        }

        // ========== Send Methods ==========

        public async Task BroadcastMealPlanAsync(MealPlan plan)
        {
            await _networkService.BroadcastMealPlanAsync(plan);
        }

        public async Task BroadcastUserAsync(User user)
        {
            await _networkService.BroadcastUserAsync(user);
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

                // Prevent duplicate processing
                var syncId = $"meal_{remotePlan.Id}_{remotePlan.Version}";
                if (_processedSyncIds.Contains(syncId)) return;
                _processedSyncIds.Add(syncId);

                // Clean old entries if too many
                if (_processedSyncIds.Count > 1000)
                {
                    var toRemove = _processedSyncIds.Take(500).ToList();
                    foreach (var id in toRemove) _processedSyncIds.Remove(id);
                }

                var localPlan = await _database.GetMealPlanByIdAsync(remotePlan.Id);

                if (localPlan == null)
                {
                    // New plan - insert
                    remotePlan.LastModified = DateTime.Now;
                    await _database.SaveMealPlanAsync(remotePlan);
                }
                else
                {
                    // Existing plan - conflict resolution (Last Write Wins)
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

                // Prevent duplicate processing
                var syncId = $"user_{remoteUser.Id}_{remoteUser.CreatedAt.Ticks}";
                if (_processedSyncIds.Contains(syncId)) return;
                _processedSyncIds.Add(syncId);

                var localUser = await _database.GetUserAsync(remoteUser.Id);

                if (localUser == null)
                {
                    // New user - insert
                    await _database.SaveUserAsync(remoteUser);
                }
                else
                {
                    // Existing user - update if newer
                    if (remoteUser.CreatedAt > localUser.CreatedAt)
                    {
                        await _database.UpdateUserAsync(remoteUser);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sync User error: {ex.Message}");
            }
        }

        private async void OnSyncRequestReceived(object? sender, string requesterId)
        {
            try
            {
                // Send all local data to requester
                var mealPlans = await _database.GetMealPlansAsync();
                var users = await _database.GetUsersAsync();

                foreach (var plan in mealPlans)
                {
                    await _networkService.SendMealPlanToPeerAsync(plan, requesterId);
                    await Task.Delay(50); // Prevent flooding
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
            // New peer discovered - request sync from them
            _ = RequestFullSyncAsync();
        }

        // ========== Chat ==========

        public async Task SendChatMessageAsync(string content)
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

            // Save locally (optional - since you said no history needed)
            // await _database.SaveChatMessageAsync(message);

            // Broadcast to network
            await _networkService.BroadcastChatMessageAsync(message);
        }
    }
}
