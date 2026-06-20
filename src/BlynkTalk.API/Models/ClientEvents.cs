namespace BlynkTalk.API.Models
{
    // These are the events the SERVER sends TO the client
    // The Angular dev listens for these exact strings via hub.on("EventName", ...)
    public class ClientEvents
    {
        // Sent to the user who initiates the WebRTC offer
        // Payload: roomId (string)
        public const string StartCall = "StartCall";

        // Sent to the user who receives the WebRTC offer
        // Payload: roomId (string)
        public const string IncomingCall = "IncomingCall";

        // User is in queue, no partner yet
        public const string Waiting = "Waiting";

        // Relay WebRTC offer SDP to the partner
        // Payload: sdp (string)
        public const string ReceiveOffer = "ReceiveOffer";

        // Relay WebRTC answer SDP to the partner
        // Payload: sdp (string)
        public const string ReceiveAnswer = "ReceiveAnswer";

        // Relay ICE candidate to the partner
        // Payload: candidate (string — JSON serialized RTCIceCandidate)
        public const string ReceiveIceCandidate = "ReceiveIceCandidate";

        // Relay a chat message to the partner
        // Payload: message (string)
        public const string ReceiveMessage = "ReceiveMessage";

        // Notify user that their partner clicked Next or disconnected
        public const string PartnerLeft = "PartnerLeft";

        // Generic error event — Angular displays this to user
        // Payload: code (string), message (string)
        public const string Error = "Error";
    }
}
