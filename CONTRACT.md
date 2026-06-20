# SignalR Hub Contract — /hub/v1

## Hub methods (Angular calls these)
| Method | Parameters | Description |
|---|---|---|
| FindPartner | — | Enter queue or get matched |
| SendOffer | roomId: string, sdp: string | Send WebRTC offer SDP |
| SendAnswer | roomId: string, sdp: string | Send WebRTC answer SDP |
| SendIceCandidate | roomId: string, candidate: string | Send ICE candidate (JSON) |
| SendMessage | roomId: string, message: string | Send chat message |
| Next | roomId: string | Both users re-queue |
| Cancel | roomId: string | This user leaves |

## Client events (Angular listens for these)
| Event | Payload | Meaning |
|---|---|---|
| Waiting | — | You are in queue, show spinner |
| StartCall | roomId: string | You are INITIATOR — call createOffer() |
| IncomingCall | roomId: string | You are RECEIVER — wait for offer |
| ReceiveOffer | sdp: string | Got partner's offer — call setRemoteDescription + createAnswer |
| ReceiveAnswer | sdp: string | Got partner's answer — call setRemoteDescription |
| ReceiveIceCandidate | candidate: string | Add via addIceCandidate() |
| ReceiveMessage | message: string | Show in chat panel |
| PartnerLeft | — | Partner gone — show re-queue or Start button |
| Error | code: string, message: string | Show error to user |