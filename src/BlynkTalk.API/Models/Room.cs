namespace BlynkTalk.API.Models
{
    public class Room
    {
        // Unique ID for this pairing session
        public string RoomId { get; set; } = Guid.NewGuid().ToString();

        // The two SignalR connection IDs in this room
        public string ConnectionIdA { get; set; } = string.Empty;
        public string ConnectionIdB { get; set; } = string.Empty;

        // When was the room created — useful for cleanup and logging
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Given one connection ID, return the other person's connection ID
        public string? GetPartner(string connectionId)
        {
            if (connectionId == ConnectionIdA) return ConnectionIdB;
            if (connectionId == ConnectionIdB) return ConnectionIdA;
            return null; // connection ID doesn't belong to this room
        }

        // Check if a given connection ID is part of this room
        public bool Contains(string connectionId)
            => ConnectionIdA == connectionId || ConnectionIdB == connectionId;
    }
}
