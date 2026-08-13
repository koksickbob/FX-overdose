using UnityEngine;

namespace FXOverdose.P2P.Infrastructure
{
    /// <summary>민감한 Payload 내용 없이 경기별 패킷 수와 전송량만 집계합니다.</summary>
    public sealed class P2PNetworkDiagnostics : MonoBehaviour
    {
        private long sentBytes,receivedBytes;
        private int sentPackets,receivedPackets;
        private float elapsed;

        public void RecordSent(int bytes){if(bytes<=0)return;sentBytes+=bytes;sentPackets++;}
        public void RecordReceived(int bytes){if(bytes<=0)return;receivedBytes+=bytes;receivedPackets++;}

        public void ResetCounters(){sentBytes=receivedBytes=0;sentPackets=receivedPackets=0;elapsed=0;}

        private void Update()
        {
            elapsed+=Time.unscaledDeltaTime;
            if(elapsed<10f)return;
            float seconds=Mathf.Max(.001f,elapsed);
            Debug.Log($"[P2P NetStats][{P2PNetworkSessionManager.Instance?.MatchId}] " +
                $"TX {sentBytes/seconds:F1} B/s ({sentPackets} pkt) · RX {receivedBytes/seconds:F1} B/s ({receivedPackets} pkt)");
            ResetCounters();
        }
    }
}
