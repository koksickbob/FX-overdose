using System.Collections.Generic;

namespace FXOverdose.P2P.Core
{
    public enum P2PRequestGuardResult : byte
    {
        Allowed,
        RateLimited,
        Blocked
    }

    /// <summary>플레이어별 요청 빈도와 비정상 패킷 누적을 제한하는 호스트 측 방어 상태입니다.</summary>
    public sealed class P2PRequestSecurityGuard
    {
        private sealed class State
        {
            public double WindowStartedAt;
            public int RequestsInWindow;
            public int Violations;
            public bool Blocked;
        }

        private readonly Dictionary<ulong,State> states=new();
        private readonly int requestsPerSecond;
        private readonly int violationLimit;

        public P2PRequestSecurityGuard(int requestsPerSecond=8,int violationLimit=6)
        {
            this.requestsPerSecond=requestsPerSecond;
            this.violationLimit=violationLimit;
        }

        public P2PRequestGuardResult TryConsume(ulong playerId,double now)
        {
            State state=Get(playerId,now);
            if(state.Blocked)return P2PRequestGuardResult.Blocked;
            if(now-state.WindowStartedAt>=1d)
            {
                state.WindowStartedAt=now;
                state.RequestsInWindow=0;
            }
            state.RequestsInWindow++;
            if(state.RequestsInWindow<=requestsPerSecond)return P2PRequestGuardResult.Allowed;
            return RecordViolation(playerId,now);
        }

        public P2PRequestGuardResult RecordViolation(ulong playerId,double now)
        {
            State state=Get(playerId,now);
            if(state.Blocked)return P2PRequestGuardResult.Blocked;
            state.Violations++;
            if(state.Violations<violationLimit)return P2PRequestGuardResult.RateLimited;
            state.Blocked=true;
            return P2PRequestGuardResult.Blocked;
        }

        public void Clear(ulong playerId)=>states.Remove(playerId);
        public void Reset()=>states.Clear();

        private State Get(ulong playerId,double now)
        {
            if(states.TryGetValue(playerId,out State state))return state;
            state=new State{WindowStartedAt=now};
            states.Add(playerId,state);
            return state;
        }
    }
}
