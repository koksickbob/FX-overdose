"""검수한 PNG를 Unity 경로에 설치하고 편집용 PSD와 배포 ZIP을 만든다."""
from pathlib import Path
import re
import json
import hashlib
import uuid
import zipfile
import numpy as np
from PIL import Image, ImageDraw, ImageCms
from psd_tools import PSDImage
from psd_tools.api.layers import PixelLayer
from psd_tools.constants import Resource
from psd_tools.psd.image_resources import ImageResource

ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parents[3]
ASSETS=PROJECT/'Assets/Resources/DatingSim/YomiRoom/PointClick'
SRGB=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
TEMPLATE=(PROJECT/'Assets/Resources/DatingSim/YomiRoom/UI/SettingsGear.png.meta').read_text()
ASSETS.mkdir(parents=True,exist_ok=True)
report=json.loads((ROOT/'검수_결과.json').read_text())


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL,'FXOverdose:YomiRoom:PointClick:'+str(path.relative_to(PROJECT))).hex


def replace_value(text,key,value):
    return re.sub(r'(?m)^(\s*'+re.escape(key)+r':).*$','\\1 '+str(value),text)


for source in sorted((ROOT/'png').rglob('*.png')):
    target=ASSETS/source.relative_to(ROOT/'png')
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_bytes(source.read_bytes())
    is_object=target.name!='Room.png'
    meta=TEMPLATE
    for key,value in {'guid':guid(target),'isReadable':int(is_object),'filterMode':1,
                      'spriteMode':1,'spriteMeshType':0,'spritePixelsToUnits':100,
                      'textureCompression':0 if is_object else 2,
                      'compressionQuality':100 if not is_object else 50,
                      'spriteID':guid(target)+'',
                      'alphaIsTransparency':int(is_object),
                      'spriteGenerateFallbackPhysicsShape':0}.items():
        meta=replace_value(meta,key,value)
    Path(str(target)+'.meta').write_text(meta)

for folder in [ASSETS,*sorted(p for p in ASSETS.iterdir() if p.is_dir())]:
    Path(str(folder)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid(folder)+
        '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

# 4K는 납품 1080p의 정렬을 유지하는 편집용 확대 캔버스이며 네이티브 4K 원화가 아니다.
psd=PSDImage.new('RGBA',(3840,2160),color=(0,0,0,0))
psd.image_resources[Resource.ICC_PROFILE]=ImageResource(key=Resource.ICC_PROFILE,data=SRGB)
for time in ['Morning','Noon','Evening','Night']:
    image=Image.open(ASSETS/time/'Room.png').resize((3840,2160),Image.Resampling.LANCZOS)
    layer=PixelLayer.frompil(image,psd,name='BG_'+time)
    layer.visible=time=='Morning'
for name in ['Door','Phone','Computer','Bed']:
    image=Image.open(ASSETS/(name+'.png')).resize((3840,2160),Image.Resampling.LANCZOS)
    PixelLayer.frompil(image,psd,name='OBJ_'+name)

perspective=Image.new('RGBA',(1920,1080))
draw=ImageDraw.Draw(perspective)
vp=(960,274*1080/941)
for endpoint in [(0,0),(1920,0),(0,940),(1920,940),(0,1080),(480,1080),(1440,1080),(1920,1080)]:
    draw.line([vp,endpoint],fill=(255,200,80,155),width=2)
draw.line([(0,vp[1]),(1920,vp[1])],fill=(255,200,80,180),width=2)
draw.ellipse((vp[0]-7,vp[1]-7,vp[0]+7,vp[1]+7),fill=(255,200,80,255))
layer=PixelLayer.frompil(perspective.resize((3840,2160),Image.Resampling.NEAREST),psd,name='GUIDE_Perspective')
layer.visible=False
zones=Image.new('RGBA',(1920,1080))
draw=ImageDraw.Draw(zones)
for rect,color in [((700,160,1220,1080),(244,114,182)),((0,0,300,560),(34,211,238)),
                   ((1220,120,1920,1000),(60,140,240)),((1260,370,1420,540),(245,191,90)),
                   ((660,980,1260,1060),(255,255,255))]:
    draw.rectangle(rect,fill=color+(25,),outline=color+(220,),width=3)
layer=PixelLayer.frompil(zones.resize((3840,2160),Image.Resampling.NEAREST),psd,name='GUIDE_Zones')
layer.visible=False
psd_path=ROOT/'YomiRoom_PointClick_Layers_4K.psd'
psd.save(psd_path)
check=PSDImage.open(psd_path)
assert check.size==(3840,2160) and len(check)==10
assert [l.name for l in check]==['BG_Morning','BG_Noon','BG_Evening','BG_Night',
        'OBJ_Door','OBJ_Phone','OBJ_Computer','OBJ_Bed','GUIDE_Perspective','GUIDE_Zones']
assert len([l for l in check if l.visible])==5
for layer in check:
    assert layer.bbox==(0,0,3840,2160),(layer.name,layer.bbox)
    if layer.name.startswith('OBJ_'):
        assert layer.topil().getchannel('A').getextrema()==(0,255)
expected=Image.open(ASSETS/'Morning/Room.png').resize((3840,2160),Image.Resampling.LANCZOS)
for name in ['Door','Phone','Computer','Bed']:
    expected.alpha_composite(Image.open(ASSETS/(name+'.png')).resize((3840,2160),Image.Resampling.LANCZOS))
actual=check.composite().convert('RGB')
error=float(np.abs(np.asarray(expected.convert('RGB')).astype(float)-np.asarray(actual).astype(float)).mean())
assert error<.3,error
report['PSD']={'파일':psd_path.name,'크기':[3840,2160],'레이어': [l.name for l in check],
    '재열기_검증':True,'오브젝트_알파_보존':True,'PNG_합성과의_평균_RGB_오차':error,
    '원화_해상도_주의':'1080p 정렬본을 2배 보간한 편집용 레이어 묶음. 네이티브 4K 생성 원화가 아님.'}
report['Unity_임포트']={'경로':str(ASSETS.relative_to(PROJECT)), '형식':'Sprite Single / Bilinear / Full Rect / Mipmaps Off',
    '오브젝트':'Read/Write On / Compression None / Alpha Is Transparency On',
    '배경':'Read/Write Off / Compression High Quality', '게임_코드_수정':False,'에디터_Play_검증':False}
report['파일_SHA256']={str(p.relative_to(PROJECT)):hashlib.sha256(p.read_bytes()).hexdigest()
    for p in sorted(ASSETS.rglob('*.png'))}
(ROOT/'검수_결과.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))

archive=ROOT/'YomiRoom_PointClick_Assets_20261008.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for path in [Path(str(ASSETS)+'.meta'),*sorted(p for p in ASSETS.rglob('*') if p.is_file())]:
        z.write(path,str(path.relative_to(PROJECT)))
    for path in [psd_path,ROOT/'검수_결과.json',ROOT/'생성_프롬프트.json',
                 ROOT/'컴퓨터_원근_수정_프롬프트.txt',ROOT/'README.md',ROOT/'에셋_검토.html',
                 *sorted((ROOT/'previews').glob('*.jpg'))]:
        z.write(path,str(path.relative_to(PROJECT)))
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    assert len([n for n in z.namelist() if n.endswith('.png')])==8
print(json.dumps({'PNG':8,'PSD_레이어':len(check),'PSD_MB':round(psd_path.stat().st_size/1e6,2),
                  'ZIP':str(archive),'ZIP_MB':round(archive.stat().st_size/1e6,2)},ensure_ascii=False))
