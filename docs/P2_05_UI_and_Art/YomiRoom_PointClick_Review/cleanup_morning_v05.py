"""승인된 후처리로 질감을 정리하고 아침 배경 한 장을 1080p로 내보낸다."""
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageMath, ImageStat, PngImagePlugin

ROOT = Path(__file__).resolve().parent
WORK = ROOT / "lighting_work"
OUT = ROOT / "B1_Morning_v05.png"
if OUT.exists():
    raise FileExistsError("기존 시안을 덮어쓰지 않습니다.")
source = Image.open(WORK / "Room_Ambient_Base_v01.png").convert("RGB")
data = json.loads((ROOT / "Solar_Projection_v01.json").read_text(encoding="utf-8"))
size = (1920,1080)
sx, sy = size[0]/source.width, size[1]/source.height

# 고립된 점을 약하게 줄인 뒤 색 차이가 큰 선을 넘지 않는 양방향 필터를 적용한다.
base = Image.blend(source, source.filter(ImageFilter.MedianFilter(3)), .35)
channels = base.split()
weights = Image.new("F",base.size,1)
sums = [c.convert("F") for c in channels]
for dy in range(-2,3):
    for dx in range(-2,3):
        if dx == dy == 0:
            continue
        shifted = ImageChops.offset(base,dx,dy)
        r,g,b = ImageChops.difference(base,shifted).split()
        delta = ImageChops.lighter(ImageChops.lighter(r,g),b)
        spatial = math.exp(-(dx*dx+dy*dy)/(2*1.5**2))
        lut = [spatial*math.exp(-v*v/(2*13**2)) for v in range(256)]
        weight = delta.point(lut,mode="F")
        weights = ImageMath.lambda_eval(lambda a:a["x"]+a["w"],x=weights,w=weight)
        sums = [ImageMath.lambda_eval(lambda a:a["x"]+a["c"]*a["w"],
                x=total,c=c.convert("F"),w=weight) for total,c in zip(sums,shifted.split())]
clean = Image.merge("RGB",[ImageMath.lambda_eval(
    lambda a:a["s"]/a["w"]+.5,s=total,w=weights).convert("L") for total in sums])
# 오프셋 연산이 반대쪽 끝을 섞지 않도록 맨 바깥 2px는 원래 바탕을 유지한다.
for rect in [(0,0,source.width,2),(0,source.height-2,source.width,source.height),
             (0,0,2,source.height),(source.width-2,0,source.width,source.height)]:
    clean.paste(source.crop(rect),rect[:2])

def fine_variation(im,rect):
    patch = im.convert("L").crop(rect)
    return ImageStat.Stat(ImageChops.difference(patch,patch.filter(ImageFilter.GaussianBlur(1)))).mean[0]
metrics = {}
for name,rect in {"left_wall":(80,180,300,440),"rear_wall":(620,410,950,500),
                  "floor":(450,690,620,805)}.items():
    before,after = fine_variation(source,rect),fine_variation(clean,rect)
    metrics[name] = {"before":before,"after":after,"reduction_percent":100*(1-after/before)}
assert all(v["after"] < v["before"] for v in metrics.values())
ambient = clean.resize(size,Image.Resampling.LANCZOS)

# 기존 광선 계산값을 새 해상도로 직접 매핑하여 직사광 경계를 다시 만든다.
scale = 4
mask = Image.new("L",(size[0]*scale,size[1]*scale))
draw = ImageDraw.Draw(mask)
def polygon(points,fill):
    draw.polygon([(round(x*sx*scale),round(y*sy*scale)) for x,y in points],fill=fill)
morning = data["cases"]["Morning"]
for shape in morning["aperture"].values():
    polygon(shape["pixels"],255)
for blocker in morning["blockers"]:
    for shape in blocker.values():
        polygon(shape["pixels"],0)
mask = mask.resize(size,Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(2*(sx+sy)/2))
floor = Image.new("L",size)
ImageDraw.Draw(floor).polygon([(round(x*sx),round(y*sy)) for x,y in
    [(416,547),(1252,547),(1672,818),(1672,941),(0,941),(0,818)]],fill=255)
mask = ImageChops.multiply(mask,floor)
light = mask.point([i/255 for i in range(256)],mode="F")
out = Image.merge("RGB",[ImageMath.lambda_eval(
    lambda a:a["c"]*(1+a["l"]*a["g"])+.5,
    c=c.convert("F"),l=light,g=gain).convert("L") for c,gain in zip(ambient.split(),(.68,.82,.67))])

px = mask.load()
x = round(750*sx)
expected = 683.6143367601867*sy
crossing = next(y for y in range(750,820) if px[x,y] >= 128)
assert abs(crossing-expected) <= 1.5
assert px[round(1030*sx),round(800*sy)] < 20
assert px[round(850*sx),round(800*sy)] > 245
assert px[x,round(650*sy)] == 0
assert out.size == size and out.mode == "RGB"
meta = PngImagePlugin.PngInfo()
meta.add(b"sRGB",b"\x00")
out.save(OUT,pnginfo=meta)
ambient.save(WORK/"Room_Ambient_Base_v02_1080p.png",pnginfo=meta)
mask.save(WORK/"Morning_SunMask_v02_1080p.png")
report = {"output":OUT.name,"size":list(size),"mode":out.mode,
          "method":"중간값 필터 약하게 혼합 + 경계 보존 양방향 필터 + Lanczos 확대 + 계산 마스크 재적용",
          "target_light_edge_y":expected,"mask_half_edge_y":crossing,
          "fine_variation":metrics,
          "limitation":"미세 변동량은 잡티만 분리한 객관적 화질 점수가 아니며, 새 디테일을 생성하는 AI 초해상도는 사용하지 않음"}
(WORK/"Morning_Cleanup_Check_v05.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(report,ensure_ascii=True,indent=2))
