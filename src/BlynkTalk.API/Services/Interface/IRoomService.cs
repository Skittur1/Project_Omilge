using BlynkTalk.API.Models;

namespace BlynkTalk.API.Services.Interface
{
    public interface IRoomService
    {
        // Create a room pairing two connection IDs; returns the new Room
        Room CreateRoom(string connectionIdA, string connectionIdB);

        // Find a room by its ID
        Room? GetRoom(string roomId);

        // Find the room a specific connection ID belongs to (if any)
        Room? GetRoomByConnection(string connectionId);

        // Remove and return the room, cleaning up state
        Room? RemoveRoom(string roomId);
    }
}
