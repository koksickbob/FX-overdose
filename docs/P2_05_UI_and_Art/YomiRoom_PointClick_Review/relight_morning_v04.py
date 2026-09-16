"""사용자가 승인한 계산 마스크 후처리. 아침 방 한 장만 보정한다."""
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageStat

ROOT = Path(__file__).resolve().parent
WORK = ROOT / "lighting_work"
WORK.mkdir(exist_ok=True)
SOURCE = ROOT / "B1_Morning_v03.png"
OUT = ROOT / "B1_Morning_v04.png"
data = json.loads((ROOT / "Solar_Projection_v01.json").read_text(encoding="utf-8"))
source = Image.open(SOURCE).convert("RGB")
w, h = source.size
assert (w, h) == tuple(data["assumptions"]["canvas"])
floor_poly = [(416,547),(1252,547),(w,818),(w,h),(0,h),(0,818)]
floor = Image.new("L", (w,h))
ImageDraw.Draw(floor).polygon(floor_poly, fill=255)
floor_edge = floor.filter(ImageFilter.GaussianBlur(2))
floor_edge = ImageChops.multiply(floor_edge, floor)

# 광원에 덮인 기존 바닥은 상세한 알베도 복원이 불가능하므로,
# 작은 나뭇결·이음새와 큰 명암을 분리하여 주변의 간접광 색으로 복원한다.
# 벽·창·커튼·몰딩에는 이 처리를 적용하지 않는다.
smooth = source.filter(ImageFilter.MedianFilter(9))
sp, low = source.load(), smooth.load()
baseline = Image.new("RGB", (2,h), (128,94,78))
bp = baseline.load()
for y in range(547,h):
    floor_right = min(w-1, 1252 + max(0,y-547)*(w-1252)/(818-547))
    left_x = 480
    right_x = min(w-25, int(floor_right)-30)
    for k, x in enumerate((left_x, right_x)):
        bp[k,y] = tuple(round(v) for v in ImageStat.Stat(
            smooth.crop((x-10,max(547,y-3),x+11,min(h,y+4)))).mean)
baseline = baseline.filter(ImageFilter.GaussianBlur(9))
bp = baseline.load()
ambient = source.copy()
ap = ambient.load()
fp = floor_edge.load()
for y in range(550,h):
    left, right = bp[0,y], bp[1,y]
    for x in range(w):
        mix = fp[x,y]/255
        if not mix:
            continue
        t = max(0,min(1,(x-480)/1100))
        center_fill = 10*math.exp(-((x-w*.52)/(w*.40))**2)
        base = [left[c]*(1-t) + right[c]*t + center_fill for c in range(3)]
        old, median = sp[x,y], low[x,y]
        # 가는 선의 세부 차이를 유지하고 중간값 바탕으로 기존 조명 경계를 제거한다.
        detail = [max(.72,min(1.28,(old[c]+3)/(median[c]+3))) for c in range(3)]
        # 이전 직사광 모서리의 작은 밝은 잔상만 억제하고 바닥의 어두운 선은 남긴다.
        if 575 <= y <= 606 and any(abs(x-corner) < 13 for corner in (624,854,884,1135)):
            detail = [min(v,1.015) for v in detail]

        clean = [max(0,min(255,base[c]*detail[c])) for c in range(3)]
        ap[x,y] = tuple(round(old[c]*(1-mix)+clean[c]*mix) for c in range(3))

# 초과해상도에서 수학 다각형을 래스터화한 뒤 동일 캔버스로 내려 안티에일리어싱한다.
scale = 4
mask = Image.new("L",(w*scale,h*scale))
draw = ImageDraw.Draw(mask)
def polygon(points, fill):
    draw.polygon([(round(x*scale),round(y*scale)) for x,y in points], fill=fill)
morning = data["cases"]["Morning"]
for region in morning["aperture"].values():
    polygon(region["pixels"],255)
for blocker in morning["blockers"]:
    for region in blocker.values():
        polygon(region["pixels"],0)
mask = mask.resize((w,h),Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(2))
mask = ImageChops.multiply(mask,floor)
mp = mask.load()
out = ambient.copy()
op = out.load()
gains = (.68,.82,.67)
for y in range(547,h):
    for x in range(w):
        sunlight = mp[x,y]/255
        if not sunlight:
            continue
        base = ap[x,y]
        op[x,y] = tuple(round(min(255,base[c]*(1+sunlight*gains[c]))) for c in range(3))

# 유효한 좌표 검수: 부드러운 경계의 50% 지점, 중앙 창틀, 직사광 없는 측벽.
crossing = next(y for y in range(600,750) if mp[750,y] >= 128)
assert abs(crossing-683.6143) <= 2, crossing
assert mp[750,650] == 0
assert mp[1030,800] < 20
assert mp[850,800] > 245 and mp[1200,800] > 245
assert ImageChops.difference(source,out).crop((0,0,w,547)).getbbox() is None
inverse_floor = ImageChops.invert(floor)
assert ImageChops.multiply(ImageChops.difference(source,out).convert("L"),inverse_floor).getbbox() is None
ambient.save(WORK / "Room_Ambient_Base_v01.png")
mask.save(WORK / "Morning_SunMask_v01.png")
out.save(OUT)
report = {"source":SOURCE.name,"output":OUT.name,"size":[w,h],
          "target_edge_y":683.6143,"mask_half_edge_y":crossing,
          "wall_window_curtain_pixels_unchanged":True,
          "outside_floor_pixels_unchanged":True,
          "method":"기존 바닥 조명을 국소 명암 분리로 정리하고 계산된 직사광 마스크를 합성",
          "limitation":"기존 조명을 제거한 바닥은 근사 복원. 실측 3D나 완전한 물리 렌더링은 아님"}
(WORK / "Morning_Correction_Check_v01.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(report,ensure_ascii=True,indent=2))
