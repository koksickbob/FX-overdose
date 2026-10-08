"""승인된 계산 마스크 후처리로 생성 원화를 정렬하고 납품본을 조립한다."""
from pathlib import Path
import json
import hashlib
import numpy as np
from scipy import ndimage
from PIL import Image, ImageDraw, ImageFilter, ImageCms

ROOT = Path(__file__).resolve().parent
REVIEW = ROOT.parent
PROJECT = ROOT.parents[3]
SOURCE = ROOT / 'sources'
OUTPUT = ROOT / 'png'
PREVIEW = ROOT / 'previews'
SIZE = (1920, 1080)
SRGB = ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
OUTPUT.mkdir(exist_ok=True)
PREVIEW.mkdir(exist_ok=True)
REPORT = {'기준': 'B1_Morning_v05 / Room_Ambient_Base_v02_1080p',
          '출력_캔버스': list(SIZE), '후처리': '기존 승인된 계산 마스크·정렬·알파 정리',
          '소스': {}, '오브젝트': {}, '배경': {}}


def save_png(image, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, icc_profile=SRGB)


def clean_alpha(image):
    """물체 실루엣과 2px AA만 유지하고 멀리 흩어진 미세 알파를 정리한다."""
    arr = np.array(image.convert('RGBA'))
    body = arr[:, :, 3] >= 26
    near = ndimage.distance_transform_edt(~body) <= 2
    arr[~near] = 0
    arr[arr[:, :, 3] == 0] = 0
    return Image.fromarray(arr)


def load_object(name):
    image = Image.open(SOURCE / (name + '.png')).convert('RGBA')
    body = np.asarray(image)[:, :, 3] >= 26
    yy, xx = np.where(body)
    REPORT['소스'][name] = {'크기': list(image.size), '모드': image.mode,
                            '클릭_외곽': [int(xx.min()), int(yy.min()), int(xx.max()+1), int(yy.max()+1)]}
    return clean_alpha(image)


def place(image, xy, scale):
    image = image.crop(image.getbbox())
    image = image.resize((round(image.width*scale[0]), round(image.height*scale[1])), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA', SIZE)
    canvas.alpha_composite(image, xy)
    return clean_alpha(canvas)


def warp_quad(image, source, destination):
    """문 사각면을 공통 방의 오른쪽 벽 원근에 대응한다."""
    matrix, values = [], []
    for (x, y), (u, v) in zip(destination, source):
        matrix.extend([[x,y,1,0,0,0,-u*x,-u*y], [0,0,0,x,y,1,-v*x,-v*y]])
        values.extend([u,v])
    coeff = np.linalg.solve(np.asarray(matrix), np.asarray(values))
    result = image.transform(SIZE, Image.Transform.PERSPECTIVE, coeff, Image.Resampling.BICUBIC)
    return clean_alpha(result)


objects = {}
objects['Door'] = warp_quad(load_object('Door'),
    [(1167,178),(1416,96),(1416,842),(1167,731)],
    [(1450,153),(1582,109),(1582,692),(1450,609)])
objects['Phone'] = place(load_object('Phone'), (1248, 535), (.345,.345))
objects['Computer'] = place(load_object('Computer_v02'), (114,435), (.49,.49))
objects['Bed'] = place(load_object('Bed'), (1477,584), (.92,.97))

# 뒤쪽 레이어의 가려진 부분도 완전히 제거한다. 가림 비율은 제거 전 실제 알파로 잰다.
order = ['Door','Phone','Computer','Bed']
masks = {k: np.asarray(v)[:,:,3] >= 26 for k,v in objects.items()}
for i, name in enumerate(order):
    front = np.zeros((1080,1920), dtype=bool)
    for other in order[i+1:]:
        front |= masks[other]
    ratio = float((front & masks[name]).sum() / masks[name].sum())
    REPORT['오브젝트'][name] = {'뒤쪽_실면적_가림_비율':ratio}
    assert ratio <= .3, (name,ratio)
    if front.any():
        arr = np.array(objects[name])
        arr[front] = 0
        objects[name] = Image.fromarray(arr)
    save_png(objects[name], OUTPUT/(name+'.png'))

ambient = np.asarray(Image.open(REVIEW/'lighting_work/Room_Ambient_Base_v02_1080p.png').convert('RGB'), dtype=np.float32)
base_low = ndimage.gaussian_filter(ambient, sigma=(24,24,0))
base_window = ndimage.gaussian_filter(ambient, sigma=(8,8,0))
yy,xx = np.mgrid[:1080,:1920]


def ellipse(cx,cy,rx,ry):
    return np.exp(-2*((xx-cx)/rx)**2-2*((yy-cy)/ry)**2)


# 접지 그림자는 오브젝트 레이어가 아닌 배경에만 둔다.
contacts = (ellipse(210,1005,140,18)*.16 + ellipse(562,995,110,22)*.15
            + ellipse(1334,715,92,10)*.15 + ellipse(1492,914,26,9)*.13
            + ellipse(1740,1080,110,17)*.12)
contacts = np.clip(contacts,0,.22)


def recolor(name):
    """새 선을 합성하지 않고 원본 RGB에 부드러운 조명장만 곱해 위치를 고정한다."""
    source = Image.open(SOURCE/(name+'.png')).convert('RGB').resize(SIZE,Image.Resampling.LANCZOS)
    target = np.asarray(source,dtype=np.float32)
    light = (ndimage.gaussian_filter(target,sigma=(24,24,0))+1)/(base_low+1)
    # 창 내부에서도 원래 창틀·도시·화분 선을 유지하며 더 국소적인 색변화만 적용한다.
    detail_light = (ndimage.gaussian_filter(target,sigma=(8,8,0))+1)/(base_window+1)
    region = Image.new('L',SIZE)
    ImageDraw.Draw(region).rectangle((738,92,1165,408),fill=255)
    weight = np.asarray(region.filter(ImageFilter.GaussianBlur(6)),dtype=np.float32)/255
    light = light*(1-weight[:,:,None])+detail_light*weight[:,:,None]
    out = ambient*light
    if name == 'Noon':
        # 원본 램프 중앙에 남은 자체발광만 부드러운 마스크로 낮춘다.
        glass = ellipse(1135,380,20,27)[:,:,None]
        out = out*(1-glass)+((out*np.array([.40,.44,.52]))+9)*glass
    if name == 'Night':
        # 건물 윤곽을 교체하지 않고 생성본의 작은 창문 점등만 추가한다.
        luminance = target@np.array([.2126,.7152,.0722])
        local = ndimage.gaussian_filter(luminance, sigma=6)
        city = (xx>764)&(xx<1040)&(yy>245)&(yy<382)
        emitted = city&(luminance>65)&((luminance-local)>24)
        emission = np.clip((luminance-local-18)/35,0,1)*emitted
        out = out*(1-emission[:,:,None])+target*emission[:,:,None]
    REPORT['배경'][name] = {'기하학_변환': '없음 — 공통 1080p 원본 픽셀에 RGB 조명 배율만 적용',
                           '조명_참고_생성본': name+'.png', '조명장_블러_px': [24,8]}
    return out


solar = json.loads((REVIEW/'Solar_Projection_v01.json').read_text())


def solar_mask(name):
    scale = 4
    image = Image.new('L',(1920*scale,1080*scale))
    draw = ImageDraw.Draw(image)
    def paint(shape,value):
        draw.polygon([(round(x*1920/1672*scale), round(y*1080/941*scale)) for x,y in shape['pixels']],fill=value)
    case = solar['cases'][name]
    for shape in case['aperture'].values():
        paint(shape,255)
    for blocker in case['blockers']:
        for shape in blocker.values():
            paint(shape,0)
    return image.resize(SIZE,Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(1.4))


backgrounds = {}
morning = np.asarray(Image.open(REVIEW/'B1_Morning_v05.png').convert('RGB'),dtype=np.float32)
backgrounds['Morning'] = morning
REPORT['배경']['Morning'] = {'기준':'기존 v05를 보존하고 접지 음영만 추가', '기하학_변환':'없음'}
for time in ['Noon','Evening','Night']:
    backgrounds[time] = recolor(time)
noon_mask = solar_mask('Noon')
save_png(noon_mask, ROOT/'Noon_SunMask_1080p.png')
sun = np.asarray(noon_mask,dtype=np.float32)/255
backgrounds['Noon'] *= 1+sun[:,:,None]*np.array([.32,.30,.20])
REPORT['배경']['Noon']['태양_마스크'] = '기존 남동향·북위37도·춘추분·태양시12시 계산 재사용'
REPORT['배경']['Evening']['태양_표현'] = '남동향 창의 해질녘은 하늘의 간접광. 잘못된 긴 직사광 패턴을 추가하지 않음.'

for time, arr in list(backgrounds.items()):
    arr = arr*(1-contacts[:,:,None])
    image = Image.fromarray(np.uint8(np.clip(np.rint(arr),0,255))).convert('RGBA')
    backgrounds[time] = image
    save_png(image,OUTPUT/time/'Room.png')

# 기존 캐릭터는 미리보기에만 올린다. 원본 그림과 게임 캐릭터 에셋은 수정하지 않는다.
yomi = Image.open(PROJECT/'Assets/Resources/DatingSim/Emotions/Sprites/T1/Calm.png').convert('RGBA')
yomi_bbox = yomi.getchannel('A').point(lambda a:255 if a>=26 else 0).getbbox()
yomi = yomi.crop(yomi_bbox)
yomi = yomi.resize((round(yomi.width*760/yomi.height),760),Image.Resampling.LANCZOS)
REPORT['합성_요미'] = {'원본_수정':False, '실루엣_검토용_높이':760,'발바닥_y':940,
                      '비고':'원본 투명 여백을 제외한 표시 영역 기준. 런타임 빌더의 전체 스프라이트 사각형 배치와 미세하게 다를 수 있음.'}
for time, bg in backgrounds.items():
    composite = bg.copy()
    for name in order:
        composite.alpha_composite(objects[name])
    # 발 그림자는 배경 납품본에 포함하지 않는 합성 검토용이다.
    shadow = Image.new('RGBA',SIZE)
    ImageDraw.Draw(shadow).ellipse((914,931,1006,948),fill=(25,31,44,48))
    composite.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(4)))
    composite.alpha_composite(yomi,(960-yomi.width//2,180))
    composite.convert('RGB').save(PREVIEW/(time+'.jpg'),quality=95,subsampling=0,icc_profile=SRGB)

for name,image in objects.items():
    a=np.asarray(image)[:,:,3]
    body=a>=26
    y,x=np.where(body)
    count=int(body.sum())
    bbox=[int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)]
    labels,n=ndimage.label(body)
    sizes=np.bincount(labels.ravel())[1:]
    REPORT['오브젝트'][name].update({'클릭_영역_픽셀':count,'클릭_외곽':bbox,
        '요미_예약_영역_픽셀':int((a[160:1080,700:1220]>0).sum()),
        '알파_범위':[int(a.min()),int(a.max())], '연결_성분_크기':sorted([int(n) for n in sizes],reverse=True)})
    assert not (a[160:1080,700:1220]>0).any(),name
    assert bbox[2]-bbox[0]>=120 and bbox[3]-bbox[1]>=120,name
    assert a.min()==0 and a.max()==255,name

for path in OUTPUT.rglob('*.png'):
    image=Image.open(path)
    assert image.size==SIZE and image.mode=='RGBA'
    assert image.info.get('icc_profile')
    if path.name=='Room.png':
        assert image.getchannel('A').getextrema()==(255,255)
REPORT['검수']={'PNG_개수':len(list(OUTPUT.rglob('*.png'))),'알파_클릭_임계':.1,
    '주의':'투영은 리뷰의 근사 모델. 모든 가구 선의 동일 소실점 수렴 및 Unity 실제 클릭/화면비 검수는 별도.'}
(ROOT/'검수_결과.json').write_text(json.dumps(REPORT,ensure_ascii=False,indent=2))
print(json.dumps(REPORT,ensure_ascii=False,indent=2))
