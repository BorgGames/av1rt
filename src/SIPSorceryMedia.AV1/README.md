# SIPSorceryMedia.AV1

A realtime AV1 video encoder for SIPSorcery, built on av1rt (a safe C# port of libaom's realtime encoder).

```csharp
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.AV1;

var video = new VideoTestPatternSource(new AV1VideoEncoder());
var pc = new RTCPeerConnection();
pc.addTrack(new MediaStreamTrack(video.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly));
pc.OnVideoFormatsNegotiated += formats => video.SetVideoSourceFormat(formats.First());
video.OnVideoSourceEncodedSample += pc.SendVideo;
```
