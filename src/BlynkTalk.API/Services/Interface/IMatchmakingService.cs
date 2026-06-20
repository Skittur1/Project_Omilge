namespace BlynkTalk.API.Services.Interface
{
    public interface IMatchmakingService
    {
        // Try to find a waiting partner. 
        // Returns their connectionId if found, or null if this user is now waiting.
        Task<string?> TryMatchAsync(string connectionId);

        // Remove a user from the queue (they cancelled or disconnected while waiting)
        Task RemoveFromQueueAsync(string connectionId);

        // Check if a user is currently in the queue
        Task<bool> IsInQueueAsync(string connectionId);
    }
}
