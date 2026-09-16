"""아침 v02의 창 좌표로 직사광 투영을 계산한다. 게임 에셋을 편집하지 않는다."""
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
W, H = 1672, 941
CX, CY, F, EYE, BACK = 836, 274, 910, 1.35, 4.5
HALF = 416 * BACK / F
WINDOW = [(650, 93), (1005, 93), (1005, 333), (650, 333)]
# 원본의 윗부분에만 내려온 블라인드를 얇은 불투명 띠로 근사한다.
SLATS = [(93, 98), (103, 108), (113, 118), (123, 128),
         (133, 138), (143, 148), (153, 160)]
MULLION = (821, 835)

def unproject(p):
    u, v = p
    return [(u - CX) * BACK / F, EYE + (CY - v) * BACK / F, BACK]

def project(p):
    x, y, z = p
    assert z > 0
    return [CX + F * x / z, CY + F * (EYE - y) / z]

def clip(poly, distance):
    result = []
    for a, b in zip(poly, poly[1:] + poly[:1]):
        da, db = distance(a), distance(b)
        if da >= -1e-9:
            result.append(a)
        if (da >= 0) != (db >= 0):
            t = da / (da - db)
            result.append([x + t * (y - x) for x, y in zip(a, b)])
    return result

def cast(poly, direction):
    dx, dy, dz = direction
    floor_x = lambda p: p[0] - p[1] * dx / dy
    floor = clip(clip(poly, lambda p: floor_x(p) + HALF),
                 lambda p: HALF - floor_x(p))
    regions = [("floor", floor, lambda p: -p[1] / dy)]
    if abs(dx) > 1e-9:
        side = HALF if dx > 0 else -HALF
        wall = clip(poly, lambda p: (floor_x(p) - side) * (1 if dx > 0 else -1))
        regions.append(("right_wall" if dx > 0 else "left_wall",
                        wall, lambda p: (side - p[0]) / dx))
    result = {}
    for name, shape, time in regions:
        if len(shape) < 3:
            continue
        hits = [[v + time(p) * d for v, d in zip(p, direction)] for p in shape]
        assert all(p[1] >= -1e-8 and p[2] > 0 for p in hits)
        result[name] = {"world": hits, "pixels": [project(p) for p in hits]}
    return result

phi = math.radians(37)
cases = {}
for name, hour, angle in [("Morning", 9, -45), ("Noon", 12, 0)]:
    ha = math.radians(angle)
    east, south, up = -math.sin(ha), math.sin(phi) * math.cos(ha), math.cos(phi) * math.cos(ha)
    d = [(east - south) / math.sqrt(2), -up, -(east + south) / math.sqrt(2)]
    assert abs(sum(v * v for v in d) - 1) < 1e-9
    aperture = cast([unproject(p) for p in WINDOW], d)
    blockers = []
    for y0, y1 in SLATS:
        blockers.append(cast([unproject(p) for p in [(650, y0), (1005, y0), (1005, y1), (650, y1)]], d))
    blockers.append(cast([unproject(p) for p in [(MULLION[0], 93), (MULLION[1], 93), (MULLION[1], 333), (MULLION[0], 333)]], d))
    cases[name] = {"solar_hour": hour, "altitude_deg": math.degrees(math.asin(up)),
                   "azimuth_deg": 180 - math.degrees(math.atan2(east, south)),
                   "ray_direction": d, "aperture": aperture, "blockers": blockers}
assert set(cases["Morning"]["aperture"]) == {"floor"}
assert all(p[1] > H for strip in cases["Morning"]["blockers"][:-1] for p in strip["floor"]["pixels"])
# 정오는 창 가까운 바닥으로 짧아지고, 왼쪽 벽의 최하단에만 일부 걸친다.
assert max(p[1] for p in cases["Noon"]["aperture"]["left_wall"]["world"]) < 0.12

report = {"assumptions": {"latitude_deg": 37, "solar_declination_deg": 0,
          "window_outward_azimuth_deg": 135, "time_basis": "현지 태양시",
          "camera": {"cx": CX, "cy": CY, "f": F, "eye": EYE, "back": BACK, "half_width": HALF},
          "canvas": [W, H], "window_pixels": WINDOW, "slats_pixels": SLATS,
          "limitations": "이미지에서 추정한 방과 창. 블라인드 깊이·각도, 화분 잎, 창턱 깊이, 반사광은 정밀 모델링하지 않음"},
          "cases": cases}
(ROOT / "Solar_Projection_v01.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

# 숫자 확인용 독립 도식이며 원본 이미지 위에 합성하거나 원본을 보정하지 않는다.
im = Image.new("RGB", (W, H), "#24313f")
draw = ImageDraw.Draw(im)
font = ImageFont.truetype("C:/Windows/Fonts/malgun.ttf", 22)
small = ImageFont.truetype("C:/Windows/Fonts/malgun.ttf", 18)
draw.polygon([(0, 818), (416, 547), (1252, 547), (W, 818), (W, H), (0, H)], fill="#566270")
for line in [((416, 20), (416, 547)), ((1252, 20), (1252, 547)),
             ((416, 547), (1252, 547)), ((416, 547), (0, 818)), ((1252, 547), (W, 818))]:
    draw.line(line, fill="#a8b1bd", width=2)
for shape in cases["Morning"]["aperture"].values():
    draw.polygon([tuple(p) for p in shape["pixels"]], fill="#e7bc72", outline="#fff5c5", width=3)
for blocker in cases["Morning"]["blockers"]:
    for shape in blocker.values():
        draw.polygon([tuple(p) for p in shape["pixels"]], fill="#566270")
draw.rectangle((650, 93, 1005, 333), outline="#d5eaff", width=3)
for y0, y1 in SLATS:
    draw.rectangle((650, y0, 1005, y1), fill="#101923")
draw.rectangle((MULLION[0], 93, MULLION[1], 333), fill="#101923")
for u in range(100, W, 100):
    draw.text((u, H - 27), str(u), font=small, fill="#142332")
for v in range(100, H, 100):
    draw.text((5, v), str(v), font=small, fill="#bdcbd9")
draw.text((35, 35), "아침 09:00 · 태양 고도 34.4° · 남동향 창", font=font, fill="white")
draw.text((35, 80), "벽에는 직사광 무늬 없음", font=font, fill="#cdd9e9")
draw.text((1040, 80), "블라인드 그림자: 화면 아래 밖", font=font, fill="#cdd9e9")
draw.text((685, 620), "직사광 시작: y684 부근", font=font, fill="#fff5c5")
draw.text((1180, 850), "한 줄의 중앙 창틀 그림자", font=font, fill="#192533")
im.save(ROOT / "Morning_Solar_Guide_v01.png")
print(json.dumps({name: {"altitude_deg": c["altitude_deg"], "azimuth_deg": c["azimuth_deg"],
      "lit_regions": list(c["aperture"]), "aperture_pixels": {k: v["pixels"] for k, v in c["aperture"].items()}}
      for name, c in cases.items()}, ensure_ascii=True, indent=2))
