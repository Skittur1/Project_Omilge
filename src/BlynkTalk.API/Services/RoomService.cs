using BlynkTalk.API.Models;
using BlynkTalk.API.Services.Interface;
using System.Collections.Concurrent;

namespace BlynkTalk.API.Services
{
    public class RoomService : IRoomService
    {
        // Key: roomId → Room
        private readonly ConcurrentDictionary<string, Room> _rooms = new();

        // Key: connectionId → roomId  (for quick reverse lookup)
        private readonly ConcurrentDictionary<string, string> _connectionToRoom = new();

        public Room CreateRoom(string connectionIdA, string connectionIdB)
        {
            var room = new Room
            {
                ConnectionIdA = connectionIdA,
                ConnectionIdB = connectionIdB
            };

            _rooms[room.RoomId] = room;

            // Register both participants for reverse lookup
            _connectionToRoom[connectionIdA] = room.RoomId;
            _connectionToRoom[connectionIdB] = room.RoomId;

            return room;
        }

        public Room? GetRoom(string roomId)
            => _rooms.TryGetValue(roomId, out var room) ? room : null;

        public Room? GetRoomByConnection(string connectionId)
        {
            if (_connectionToRoom.TryGetValue(connectionId, out var roomId))
                return GetRoom(roomId);
            return null;
        }

        public Room? RemoveRoom(string roomId)
        {
            if (_rooms.TryRemove(roomId, out var room))
            {
                // Clean up reverse lookups for both users
                _connectionToRoom.TryRemove(room.ConnectionIdA, out _);
                _connectionToRoom.TryRemove(room.ConnectionIdB, out _);
                return room;
            }
            return null;
        }
    }
}
