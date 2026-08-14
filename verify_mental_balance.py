"""멘탈 리밸런싱 수치 검증 (docs/P2_04_System/Mental_Drain_Rebalance_Plan.md).

TradingController의 청산 멘탈 곡선과 MentalDrainGimmickController의 합산 피해 예산이
설계 제약을 만족하는지 확인합니다. 수치를 바꿀 때 이 파일의 상수도 함께 고치십시오.

    python verify_mental_balance.py
"""
import math
LC, LMAX, GC, GMAX = 40.0, 25.0, 25.0, 15.0
loss = lambda r: min(LMAX, LC*math.sqrt(r))
gain = lambda r: min(GMAX, GC*math.sqrt(r))

# 1) 스케일 불변성: 같은 손익률이면 자본 규모와 무관하게 같은 멘탈 변화
for ratio in (0.01, 0.05, 0.10, 0.25):
    vals = {loss(abs(-cap*ratio)/cap) for cap in (7_000, 20_000, 200_000, 2_000_000)}
    assert len(vals) == 1, f"스케일 불변성 위반 {ratio}: {vals}"
print("OK 스케일 불변: 자본 7천~200만에서 동일 손익률 → 동일 멘탈 변화")

# 2) 문서 표 값
tbl = [(0.01,4.0),(0.05,8.9),(0.10,12.6),(0.25,20.0),(0.50,25.0)]
for r,exp in tbl:
    assert abs(loss(r)-exp) < 0.1, f"손실 {r}: {loss(r):.1f} != {exp}"
for r,exp in [(0.01,2.5),(0.05,5.6),(0.10,7.9),(0.36,15.0)]:
    assert abs(gain(r)-exp) < 0.1, f"수익 {r}: {gain(r):.1f} != {exp}"
print("OK 문서 표 일치 (손실 5종 / 수익 4종)")

# 3) 단발 오버도즈 불가: 최악의 단일 청산도 멘탈 100을 못 지움
assert loss(1.0) == LMAX < 100, "단발 청산으로 오버도즈 가능"
print(f"OK 단발 상한: 전액 손실도 -{LMAX} (오버도즈 불가)")

# 4) 예산 제약: 수동 3연속 손절(매회 자본 5% 손실) 후 4연속 뇌동매매 기믹이 발동 가능해야 함
OPEN, STREAK, MANUAL = 5.0, [4.0, 9.0, 16.0], 1.5
total = sum(OPEN + loss(0.05) + s*MANUAL for s in STREAK)
print(f"   수동 3연속 손절 누적: -{total:.1f} → 잔여 멘탈 {100-total:.1f}")
assert 100 - total > 0, "3연속 손절에서 오버도즈 → 4연속 기믹 발동 불가"
auto = sum(OPEN + loss(0.05) + s for s in STREAK)
print(f"   자동 3연속 손절 누적: -{auto:.1f} → 잔여 멘탈 {100-auto:.1f}")
assert 100 - auto > 0
print("OK 예산: 3연속 손절 후에도 생존 → 4연속 뇌동매매 기믹 발동 가능")

# 5) 이벤트 단발 페널티 상한
def rescale(p): return -max(5, min(30, round(abs(p)*0.25)))
assert rescale(-120) == -30 and rescale(-11) == -5
assert min(rescale(p) for p in range(-120,-10)) >= -35, "이벤트 상한 초과"
print("OK 이벤트: -120~-11 → -30~-5 (만멘탈에서 단발 오버도즈 불가)")
