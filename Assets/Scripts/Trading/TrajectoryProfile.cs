using System.Collections.Generic;
using UnityEngine;

namespace FXOverdose.Trading
{
    /// <summary>
    /// 확정 신호 구간(GuaranteedOverride)의 가격 궤적 한 종류입니다. (SIG-B1)
    ///
    /// 예전에는 트랩 3종(V/W/Slow Bleed)이 엔진 안에 구간별 if/else로 하드코딩돼 있었고, 정상 신호는 직선 하나뿐이었습니다.
    /// 이제 궤적은 진행률 커브로 표현합니다. 메뉴에서 에셋을 만들어 <see cref="MarketSimulationEngine"/>의 궤적 목록에 넣으면
    /// 가중 추첨 대상이 되고, 목록이 비어 있으면 <see cref="TrajectoryLibrary"/>의 내장 궤적을 씁니다.
    ///
    /// 엔진은 매 인게임 분마다 <b>그 1분 동안의 진행률 변화량</b> <c>progress(t+1/D) − progress(t)</c>를 드리프트로 씁니다.
    /// 합이 망원급수가 되므로 구간 전체의 총 이동량은 커브 모양·시간 비틀림과 무관하게
    /// <c>목표 변동률 × (progress(1) − progress(0))</c>으로 정확히 보존됩니다.
    /// (기울기를 수치 미분해 쓰면 꺾인 지점과 비틀림에서 최대 38%까지 어긋났습니다.)
    /// </summary>
    [CreateAssetMenu(fileName = "TrajectoryProfile", menuName = "FX Overdose/Trading/Trajectory Profile")]
    public class TrajectoryProfile : ScriptableObject
    {
        [Tooltip("x: 경과 비율(0~1) → y: 목표 변동률의 몇 배 지점에 있어야 하는가. 1을 넘거나 음수여도 됩니다. (0, 0)에서 시작하십시오.")]
        public AnimationCurve progress = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("x: 경과 비율(0~1) → 노이즈 배수.")]
        public AnimationCurve noise = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("경과 비율을 t^e로 비틀 지수의 폭. 0.2면 e ∈ [1/1.2, 1.2]. 같은 패턴이라도 꺾이는 시점이 매번 달라집니다.")]
        [Range(0f, 0.5f)] public float timeWarpJitter = 0.15f;

        [Tooltip("경로 중심선 평균 회귀(OU)를 쓸지. 꺾임이 핵심인 패턴은 끄는 편이 모양이 잘 삽니다.")]
        public bool trackPathWithOu = true;

        [Tooltip("같은 목록 안에서의 추첨 가중치.")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("시장 신호에서 이 궤적을 쓸 셋업. 정상 경로 목록에 넣으면 같은 셋업 신호에서만 추첨됩니다. 이벤트 빔에서는 무시됩니다. (SIG-A4)")]
        public SignalSetup setup = SignalSetup.Breakout;

        /// <summary>신호마다 1회 추첨하는 시간 비틀림 지수. 1보다 크면 늦게, 작으면 일찍 꺾입니다.</summary>
        public float RollWarp()
        {
            float e = 1f + Random.Range(0f, timeWarpJitter);
            return Random.value < 0.5f ? e : 1f / e;
        }

        private static float Warp(float t, float warp) => Mathf.Pow(Mathf.Clamp01(t), warp);

        public float ProgressAt(float t, float warp) => progress.Evaluate(Warp(t, warp));

        public float NoiseAt(float t, float warp) => noise.Evaluate(Warp(t, warp));
    }

    /// <summary>
    /// 에셋이 지정되지 않았을 때 쓰는 내장 궤적입니다.
    /// 트랩 3종은 예전 하드코딩 패턴과 모양·총 이동량이 같습니다(V 0.9Δ / W 1.3Δ / Slow Bleed 1.35Δ).
    /// </summary>
    public static class TrajectoryLibrary
    {
        private static List<TrajectoryProfile> traps;
        private static List<TrajectoryProfile> paths;

        /// <summary>가짜 이벤트 빔(트랩)용. 꺾임이 핵심이라 경로 OU를 쓰지 않습니다.</summary>
        public static IReadOnlyList<TrajectoryProfile> Traps => traps ??= new List<TrajectoryProfile>
        {
            Make("V-Shape", Piecewise((0f, 0f), (0.7f, 1.35f), (1f, 0.9f)), null, 0.25f, false),
            Make("W-Shape Double Trap", Piecewise((0f, 0f), (0.4f, 1.5f), (0.6f, 0.7f), (0.85f, 1.9f), (1f, 1.3f)), null, 0.2f, false),
            Make("Slow Bleed + Flash Spike", Piecewise((0f, 0f), (0.85f, 0.9f), (1f, 1.35f)),
                 Piecewise((0f, 0.3f), (0.84f, 0.3f), (0.86f, 2f), (1f, 2f)), 0.2f, false),
        };

        /// <summary>정상 확정 경로용(진짜 신호, AI 신호 전반). 경로 OU로 눌림목이 자연 발생합니다.</summary>
        public static IReadOnlyList<TrajectoryProfile> Paths => paths ??= new List<TrajectoryProfile>
        {
            Make("Linear", Piecewise((0f, 0f), (1f, 1f)), null, 0f, true),
            Make("Breakout (ease-out)", Piecewise((0f, 0f), (0.3f, 0.65f), (1f, 1f)), null, 0.15f, true),
            Make("Squeeze (ease-in)", Piecewise((0f, 0f), (0.7f, 0.35f), (1f, 1f)), null, 0.15f, true),
            Make("Staircase", Piecewise((0f, 0f), (0.25f, 0.4f), (0.4f, 0.33f), (0.65f, 0.75f), (0.78f, 0.68f), (1f, 1f)), null, 0.1f, true),
        };

        private static Dictionary<SignalSetup, List<TrajectoryProfile>> bySetup;

        /// <summary>
        /// 시장 신호 셋업별 내장 궤적 (SIG-A4). 가짜 계열은 진행률이 먼저 음수로 내려가 <b>유인 방향으로 찌른 뒤</b>
        /// 목표(반대) 방향으로 반전합니다. 모든 궤적은 progress(1) = 1이라 총 이동량은 목표 변동률 그대로입니다.
        /// </summary>
        public static IReadOnlyList<TrajectoryProfile> ForSetup(SignalSetup setup)
        {
            bySetup ??= new Dictionary<SignalSetup, List<TrajectoryProfile>>
            {
                [SignalSetup.Breakout] = new List<TrajectoryProfile> { Paths[0], Paths[1], Paths[3] },
                [SignalSetup.VolatilitySqueeze] = new List<TrajectoryProfile> { Paths[2] },
                [SignalSetup.TrendContinuation] = One(Make("Trend Continuation", Piecewise((0f, 0f), (0.2f, -0.15f), (1f, 1f)), null, 0.15f, true), SignalSetup.TrendContinuation),
                [SignalSetup.FalseBreakout] = One(Make("False Breakout", Piecewise((0f, 0f), (0.25f, -0.3f), (1f, 1f)), null, 0.2f, true), SignalSetup.FalseBreakout),
                [SignalSetup.StopRun] = One(Make("Stop Run", Piecewise((0f, 0f), (0.1f, -0.45f), (0.25f, 0.25f), (1f, 1f)), null, 0.15f, true), SignalSetup.StopRun),
                [SignalSetup.LiquidityGrab] = One(Make("Liquidity Grab", Piecewise((0f, 0f), (0.15f, -0.35f), (0.3f, 0.3f), (0.45f, -0.15f), (1f, 1f)), null, 0.15f, true), SignalSetup.LiquidityGrab),
                [SignalSetup.RangeRejection] = One(Make("Range Rejection", Piecewise((0f, 0f), (0.3f, -0.2f), (0.55f, 0.55f), (0.7f, 0.45f), (1f, 1f)), null, 0.15f, true), SignalSetup.RangeRejection),
                [SignalSetup.Capitulation] = One(Make("Capitulation", Piecewise((0f, 0f), (0.2f, -0.15f), (0.35f, -0.6f), (1f, 1f)), null, 0.15f, true), SignalSetup.Capitulation),
                [SignalSetup.Distribution] = One(Make("Distribution", Piecewise((0f, 0f), (0.2f, -0.08f), (0.4f, 0.04f), (0.55f, -0.04f), (1f, 1f)), null, 0.15f, true), SignalSetup.Distribution),
                [SignalSetup.NewsSpike] = One(Make("News Spike", Piecewise((0f, 0f), (0.06f, 2f), (1f, 1f)), null, 0.1f, true), SignalSetup.NewsSpike),
            };
            return bySetup.TryGetValue(setup, out List<TrajectoryProfile> list) ? list : Paths;
        }

        /// <summary>지정 목록에서 이 셋업으로 태그된 궤적을 가중 추첨하고, 없으면 셋업의 내장 궤적에서 고릅니다.</summary>
        public static TrajectoryProfile PickForSetup(IReadOnlyList<TrajectoryProfile> custom, SignalSetup setup)
        {
            float total = 0f;
            if (custom != null)
            {
                for (int i = 0; i < custom.Count; i++)
                {
                    TrajectoryProfile p = custom[i];
                    if (p != null && p.weight > 0f && p.setup == setup) total += p.weight;
                }
            }
            if (total <= 0f) return Pick(null, ForSetup(setup));

            float r = Random.value * total;
            TrajectoryProfile last = null;
            for (int i = 0; i < custom.Count; i++)
            {
                TrajectoryProfile p = custom[i];
                if (p == null || p.weight <= 0f || p.setup != setup) continue;
                last = p;
                r -= p.weight;
                if (r <= 0f) return p;
            }
            return last;
        }

        private static List<TrajectoryProfile> One(TrajectoryProfile p, SignalSetup setup)
        {
            p.setup = setup;
            return new List<TrajectoryProfile> { p };
        }

        /// <summary>지정 목록에 쓸 만한 프로필이 있으면 거기서, 없으면 내장 목록에서 가중 추첨합니다.</summary>
        public static TrajectoryProfile Pick(IReadOnlyList<TrajectoryProfile> custom, IReadOnlyList<TrajectoryProfile> builtIn)
        {
            IReadOnlyList<TrajectoryProfile> pool = TotalWeight(custom) > 0f ? custom : builtIn;
            float r = Random.value * TotalWeight(pool);
            TrajectoryProfile last = null;
            for (int i = 0; i < pool.Count; i++)
            {
                TrajectoryProfile p = pool[i];
                if (p == null || p.weight <= 0f) continue;
                last = p;
                r -= p.weight;
                if (r <= 0f) return p;
            }
            return last != null ? last : builtIn[0];
        }

        private static float TotalWeight(IReadOnlyList<TrajectoryProfile> list)
        {
            if (list == null) return 0f;
            float total = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].weight > 0f) total += list[i].weight;
            }
            return total;
        }

        private static TrajectoryProfile Make(string name, AnimationCurve progress, AnimationCurve noise, float jitter, bool ou)
        {
            var p = ScriptableObject.CreateInstance<TrajectoryProfile>();
            p.name = name;
            p.progress = progress;
            if (noise != null) p.noise = noise;
            p.timeWarpJitter = jitter;
            p.trackPathWithOu = ou;
            // 정적 필드만 붙잡고 있는 런타임 인스턴스라 씬 전환 시 UnloadUnusedAssets에 회수되지 않게 합니다.
            p.hideFlags = HideFlags.HideAndDontSave;
            return p;
        }

        /// <summary>꺾은선 커브. 접선을 양옆 구간의 기울기로 맞추면 Hermite 보간이 정확한 직선이 됩니다.</summary>
        private static AnimationCurve Piecewise(params (float t, float v)[] k)
        {
            var keys = new Keyframe[k.Length];
            for (int i = 0; i < k.Length; i++)
            {
                float inSlope = i > 0 ? (k[i].v - k[i - 1].v) / (k[i].t - k[i - 1].t) : 0f;
                float outSlope = i < k.Length - 1 ? (k[i + 1].v - k[i].v) / (k[i + 1].t - k[i].t) : 0f;
                keys[i] = new Keyframe(k[i].t, k[i].v, inSlope, outSlope);
            }
            return new AnimationCurve(keys);
        }
    }
}
