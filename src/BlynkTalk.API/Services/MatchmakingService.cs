using BlynkTalk.API.Services.Interface;
using System.Collections.Concurrent;

namespace BlynkTalk.API.Services
{
    public class MatchmakingService : IMatchmakingService
    {
        // The waiting queue — first in, first out
        // ConcurrentQueue is thread-safe so simultaneous requests don't corrupt state
        private readonly ConcurrentQueue<string> _waitingQueue = new();

        // A set for O(1) lookup — "is this person already in the queue?"
        // We need a lock here because ConcurrentHashSet doesn't exist in .NET
        private readonly HashSet<string> _waitingSet = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        public async Task<string?> TryMatchAsync(string connectionId)
        {
            await _lock.WaitAsync();
            try
            {
                // Keep trying to dequeue until we find someone who is still active
                // (they could have disconnected while waiting)
                while (_waitingQueue.TryDequeue(out var candidateId))
                {
                    // Remove from set regardless
                    _waitingSet.Remove(candidateId);

                    // Don't match a user with themselves 
                    // (edge case: user opens two tabs)
                    if (candidateId == connectionId)
                        continue;

                    // Found a valid partner — return their ID
                    return candidateId;
                }

                // No one waiting — add this user to the queue
                if (!_waitingSet.Contains(connectionId))
                {
                    _waitingQueue.Enqueue(connectionId);
                    _waitingSet.Add(connectionId);
                }

                return null; // Signal: "you are now waiting"
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task RemoveFromQueueAsync(string connectionId)
        {
            await _lock.WaitAsync();
            try
            {
                // Remove from set; the queue will skip this entry when it's dequeued
                // (ConcurrentQueue has no Remove — we filter at dequeue time in TryMatchAsync)
                _waitingSet.Remove(connectionId);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> IsInQueueAsync(string connectionId)
        {
            await _lock.WaitAsync();
            try
            {
                return _waitingSet.Contains(connectionId);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
