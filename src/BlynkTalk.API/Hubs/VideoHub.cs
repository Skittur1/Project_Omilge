using BlynkTalk.API.Models;
using BlynkTalk.API.Services.Interface;
using Microsoft.AspNetCore.SignalR;

namespace BlynkTalk.API.Hubs
{
    public class VideoHub : Hub
    {
        private readonly IMatchmakingService _matchmaking;
        private readonly IRoomService _rooms;
        private readonly ILogger<VideoHub> _logger;

        public VideoHub(
            IMatchmakingService matchmaking,
            IRoomService rooms,
            ILogger<VideoHub> logger)
        {
            _matchmaking = matchmaking;
            _rooms = rooms;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 1: User clicks "Start"
        // Angular calls: hub.invoke("FindPartner")
        // ─────────────────────────────────────────────────────────────
        public async Task FindPartner()
        {
            var myConnectionId = Context.ConnectionId;

            // Safety check: don't queue someone who is already in a room
            var existingRoom = _rooms.GetRoomByConnection(myConnectionId);
            if (existingRoom != null)
            {
                await Clients.Caller.SendAsync(ClientEvents.Error, "ALREADY_IN_ROOM", "You are already in a room. Click Next or Cancel first.");
                return;
            }

            _logger.LogInformation("User {ConnectionId} looking for a partner", myConnectionId);

            // Try to match with someone already waiting
            var partnerId = await _matchmaking.TryMatchAsync(myConnectionId);

            if (partnerId == null)
            {
                // No one waiting — this user is now in the queue
                // Tell Angular to show a "Looking for someone..." spinner
                await Clients.Caller.SendAsync(ClientEvents.Waiting);
                _logger.LogInformation("User {ConnectionId} is now waiting", myConnectionId);
                return;
            }

            // Found a partner — create the room
            var room = _rooms.CreateRoom(myConnectionId, partnerId);

            _logger.LogInformation(
                "Matched {UserA} with {UserB} in room {RoomId}",
                myConnectionId, partnerId, room.RoomId);

            // Add both users to the SignalR group for this room
            // Groups let us broadcast to both users at once later
            await Groups.AddToGroupAsync(myConnectionId, room.RoomId);
            await Groups.AddToGroupAsync(partnerId, room.RoomId);

            // Tell the caller (who found the match) to be the WebRTC INITIATOR
            // This means Angular will call createOffer() for this user
            await Clients.Caller.SendAsync(ClientEvents.StartCall, room.RoomId);

            // Tell the partner (who was waiting) to be the WebRTC RECEIVER
            // This means Angular will wait for the offer and then call createAnswer()
            await Clients.Client(partnerId).SendAsync(ClientEvents.IncomingCall, room.RoomId);
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 2: WebRTC Signaling — Offer
        // The initiator creates an SDP offer and sends it here.
        // We relay it to the partner.
        // Angular calls: hub.invoke("SendOffer", roomId, sdp)
        // ─────────────────────────────────────────────────────────────
        public async Task SendOffer(string roomId, string sdp)
        {
            var room = _rooms.GetRoom(roomId);
            if (room == null || !room.Contains(Context.ConnectionId))
            {
                await Clients.Caller.SendAsync(ClientEvents.Error, "ROOM_NOT_FOUND", "Room does not exist.");
                return;
            }

            // Forward the SDP offer to everyone else in the room (the partner)
            await Clients.OthersInGroup(roomId).SendAsync(ClientEvents.ReceiveOffer, sdp);
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 3: WebRTC Signaling — Answer
        // The receiver creates an SDP answer and sends it here.
        // We relay it back to the initiator.
        // Angular calls: hub.invoke("SendAnswer", roomId, sdp)
        // ─────────────────────────────────────────────────────────────
        public async Task SendAnswer(string roomId, string sdp)
        {
            var room = _rooms.GetRoom(roomId);
            if (room == null || !room.Contains(Context.ConnectionId))
            {
                await Clients.Caller.SendAsync(ClientEvents.Error, "ROOM_NOT_FOUND", "Room does not exist.");
                return;
            }

            await Clients.OthersInGroup(roomId).SendAsync(ClientEvents.ReceiveAnswer, sdp);
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 4: WebRTC Signaling — ICE Candidates
        // Both users continuously send ICE candidates as they are discovered.
        // These are network path options (IP:port combinations).
        // Angular calls: hub.invoke("SendIceCandidate", roomId, candidate)
        // candidate is a JSON string of RTCIceCandidate
        // ─────────────────────────────────────────────────────────────
        public async Task SendIceCandidate(string roomId, string candidate)
        {
            var room = _rooms.GetRoom(roomId);
            if (room == null || !room.Contains(Context.ConnectionId))
                return; // Silently drop — race conditions are common here

            await Clients.OthersInGroup(roomId).SendAsync(ClientEvents.ReceiveIceCandidate, candidate);
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 5: Chat
        // Either user sends a text message.
        // We relay it to the partner in the same room.
        // Angular calls: hub.invoke("SendMessage", roomId, text)
        // ─────────────────────────────────────────────────────────────
        public async Task SendMessage(string roomId, string message)
        {
            // Validate message
            if (string.IsNullOrWhiteSpace(message))
                return;

            // Truncate to prevent abuse
            if (message.Length > 500)
                message = message[..500];

            var room = _rooms.GetRoom(roomId);
            if (room == null || !room.Contains(Context.ConnectionId))
                return;

            // Send only to the OTHER person — the caller sees their own message already
            await Clients.OthersInGroup(roomId).SendAsync(ClientEvents.ReceiveMessage, message);
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 6: User clicks "Next"
        // BOTH users get re-queued. The current room is destroyed.
        // The user who clicked Next AND their partner both go back to FindPartner.
        // Angular calls: hub.invoke("Next", roomId)
        // ─────────────────────────────────────────────────────────────
        public async Task Next(string roomId)
        {
            var myConnectionId = Context.ConnectionId;
            var room = _rooms.GetRoom(roomId);

            if (room == null || !room.Contains(myConnectionId))
                return;

            var partnerId = room.GetPartner(myConnectionId);

            // Destroy the room first
            _rooms.RemoveRoom(roomId);

            // Remove both from the SignalR group
            await Groups.RemoveFromGroupAsync(myConnectionId, roomId);
            if (partnerId != null)
                await Groups.RemoveFromGroupAsync(partnerId, roomId);

            _logger.LogInformation(
                "User {ConnectionId} clicked Next in room {RoomId}",
                myConnectionId, roomId);

            // Tell the partner their match left — Angular shows "Partner left, finding new match..."
            if (partnerId != null)
                await Clients.Client(partnerId).SendAsync(ClientEvents.PartnerLeft);

            // Re-queue BOTH users for a new match
            // We call FindPartner internally for both — reusing the same logic
            await FindPartner(); // re-queues the caller

            if (partnerId != null)
            {
                // For the partner we cannot call FindPartner() directly (it uses Context.ConnectionId)
                // so we invoke it as a separate action using a helper
                await RequeuePartner(partnerId);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 7: User clicks "Cancel"
        // Only this user leaves. Their partner stays — but gets notified.
        // The partner can then decide to wait again or leave.
        // Angular calls: hub.invoke("Cancel", roomId)
        // ─────────────────────────────────────────────────────────────
        public async Task Cancel(string roomId)
        {
            var myConnectionId = Context.ConnectionId;
            var room = _rooms.GetRoom(roomId);

            if (room == null || !room.Contains(myConnectionId))
                return;

            var partnerId = room.GetPartner(myConnectionId);

            // Destroy the room
            _rooms.RemoveRoom(roomId);

            // Remove this user from the SignalR group
            await Groups.RemoveFromGroupAsync(myConnectionId, roomId);
            if (partnerId != null)
                await Groups.RemoveFromGroupAsync(partnerId, roomId);

            _logger.LogInformation(
                "User {ConnectionId} cancelled in room {RoomId}",
                myConnectionId, roomId);

            // Notify the partner — Angular shows "Stranger disconnected" and reveals Start button again
            if (partnerId != null)
                await Clients.Client(partnerId).SendAsync(ClientEvents.PartnerLeft);

            // The cancelling user goes back to the Start screen — no action needed from server
            // Angular simply shows the Start button again on receiving no further events
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 8: Browser closes / network drops
        // This fires automatically when a SignalR connection is lost.
        // We must clean up the room and notify the partner.
        // ─────────────────────────────────────────────────────────────
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var myConnectionId = Context.ConnectionId;

            _logger.LogInformation(
                "User {ConnectionId} disconnected. Exception: {Exception}",
                myConnectionId, exception?.Message ?? "none");

            // Remove from queue if they were waiting
            await _matchmaking.RemoveFromQueueAsync(myConnectionId);

            // Handle if they were in an active room
            var room = _rooms.GetRoomByConnection(myConnectionId);
            if (room != null)
            {
                var partnerId = room.GetPartner(myConnectionId);
                _rooms.RemoveRoom(room.RoomId);

                // Notify partner
                if (partnerId != null)
                    await Clients.Client(partnerId).SendAsync(ClientEvents.PartnerLeft);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // ─────────────────────────────────────────────────────────────
        // Private helper: re-queue a specific connection ID
        // Used by Next() to re-queue the partner without faking a Context
        // ─────────────────────────────────────────────────────────────
        private async Task RequeuePartner(string partnerId)
        {
            var existingRoom = _rooms.GetRoomByConnection(partnerId);
            if (existingRoom != null) return; // already matched with someone else

            var newPartnerId = await _matchmaking.TryMatchAsync(partnerId);

            if (newPartnerId == null)
            {
                // Partner is now waiting
                await Clients.Client(partnerId).SendAsync(ClientEvents.Waiting);
                return;
            }

            // Partner found a new match immediately
            var newRoom = _rooms.CreateRoom(partnerId, newPartnerId);

            await Groups.AddToGroupAsync(partnerId, newRoom.RoomId);
            await Groups.AddToGroupAsync(newPartnerId, newRoom.RoomId);

            // Partner is the initiator this time
            await Clients.Client(partnerId).SendAsync(ClientEvents.StartCall, newRoom.RoomId);
            await Clients.Client(newPartnerId).SendAsync(ClientEvents.IncomingCall, newRoom.RoomId);

            _logger.LogInformation(
                "Re-queued partner {PartnerId} — matched with {NewPartnerId} in room {RoomId}",
                partnerId, newPartnerId, newRoom.RoomId);
        }
    }
}
