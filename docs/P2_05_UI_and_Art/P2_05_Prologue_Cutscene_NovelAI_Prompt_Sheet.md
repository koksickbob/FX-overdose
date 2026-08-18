# P2_05 프롤로그(EVT_MAIN_001) — 컷씬 CG 프롬프트 시트 (NovelAI 발주용)

> **작성일**: 2026-08-16
> **대상**: **NovelAI Diffusion V4.5** — 오너가 [`nai_generate.py`](../../nai_generate.py)로 직접 생성한다. 코덱스 작업 범위 아님.
> **이 문서는 단독으로 성립한다.** 컷 리스트·캐릭터 디자인·검수 기준을 전부 담고 있으므로 다른 프롬프트 시트를 열 필요가 없다.
> 참조하는 외부 문서는 둘뿐이고 둘 다 **근거 표시용**이다 — [캐릭터 바이블](../P2_02_Worldbuilding/P2_02_Yomi_Character_Bible.md) 2.2절(플레이어 얼굴 금지)·4.5절(공포 클리셰 금지). 규칙 내용 자체는 아래에 적혀 있다.
>
> ⚠️ **이 문서가 생성 스크립트의 단일 진실 원천이다.** `nai_generate.py`가 3절의 코드 블록을 직접 파싱한다.
> 프롬프트를 스크립트에 복사하지 말고 **여기만 고친다.**
> 파싱 규칙: `### CUT-0X` 제목 아래 **첫 번째 코드 블록 = 프롬프트, 두 번째 = UC** 순서. 이 순서를 지킬 것.
> 검증은 `python nai_generate.py --dry-run` — 키도 Anlas도 쓰지 않고 8컷이 다 잡히는지만 본다.
>
> **절 번호를 바꾸지 말 것.** `nai_generate.py`의 도움말과 주석이 "시트 2.1절 / 4절 / 6절"을 문구로 참조한다.

---

## 1. 무엇을 그리는가

### 1.1. 이벤트

| 항목 | 값 |
| --- | --- |
| 이벤트 | `EVT_MAIN_001` — 프롤로그 「비 오는 밤의 만남」 |
| 내용 | 퇴근길의 주인공이 빗속 골목에 웅크린 요미를 발견하고, 우산을 씌우고, 집으로 데려가기까지 |
| 구조 | 40노드 완전 선형 (선택지 없음). 새 게임 시작 직후 전용 씬에서 재생 |
| **연출 방식** | **풀 CG 모드 (`HideStanding = true`)** |

> ★ **풀 CG 모드가 이 시트의 전제다.** 이벤트가 스탠딩 스프라이트를 띄우지 않으므로 **요미는 CG 안에 그려져 있어야 한다.** 배경만 뽑아서 스탠딩을 얹는 방식이 아니다 — CUT-01(인물 없음)만 예외다.

### 1.2. 컷 리스트 8장

**1차 6컷만 있으면 연출이 성립한다.** 2차 2컷은 보강용이고, 없으면 대체 컷을 그 구간까지 끌고 간다.

| 컷 | 장면 | 대사 구간 | 우선 | 없을 때 |
| --- | --- | --- | --- | --- |
| CUT-01 | 비 내리는 밤거리 정경 — **요미 없음** | N01~N05 | 2차 | CUT-02로 대체 |
| CUT-02 | 골목 어귀, 웅크린 실루엣 (원경) | N06~N09 | **1차** | — |
| CUT-03 | 우산을 씌워주는 순간 (근경) | N10~N12 | **1차** | — |
| CUT-04 | 얼굴을 감싸며 거부하는 요미 | N13~N19 | **1차** | — |
| CUT-05 | 손수건을 내미는 손 | N20~N23 | 2차 | CUT-04 유지 |
| **CUT-06** | **올려다보는 눈맞춤 ★ 감정 절정** | N24~N35 | **1차** | — |
| CUT-07 | 옷자락을 쥔 작은 손 | N36~N37 | **1차** | — |
| CUT-08 | 우산 아래 두 사람의 귀갓길 (후경) | N38~N39 | **1차** | — |

CUT-06이 가장 오래 떠 있고(12노드) 얼굴이 가장 크게 나온다. 4절의 일관성 절차가 이 컷에서 시작하는 이유다.

### 1.3. 왜 산문이 아니라 태그인가

**NovelAI는 단부루(danbooru) 태그 모델**이다. 산문 지시를 넣으면 품질이 눈에 띄게 떨어진다. 또 네거티브를 프롬프트 안에 쓸 수 없고 **UC(Undesired Content) 별도 필드**로 보내야 한다. 그래서 3절의 컷마다 블록이 두 개다.

---

## 2. 공통 규약

### 2.1. 모델·파라미터 (스크립트 기본값)

| 항목 | 값 | 이유 |
| --- | --- | --- |
| 모델 | `nai-diffusion-4-5-full` | |
| 해상도 | **1792 × 1024** | 기존 Gemini 채택본(1672×941)과 같은 급. 64배수 제약 안에서 16:9에 가장 가깝다. **유료 구간(Anlas 소모)** — 무료 구간(1216×832)에서 뽑은 1차 결과는 확대 시 흐려서 기존 컷보다 열등했다(2026-08-17) |
| 스텝 / scale | 28 / 5.5 | 28 이상은 체감 차이가 거의 없다 |
| 샘플러 | `k_euler_ancestral` + `karras` | |
| 컷당 후보 | 3장 (시드 3개) | 한 시드로 판단하면 프롬프트 문제와 시드 운을 구별할 수 없다. CUT-06은 더 뽑는다(5절) |

### 2.2. 요미 디자인 — T1 스프라이트 실측 + 프롤로그 변형

**기준은 문서가 아니라 실제 아트다.** 아래는 `Assets/Resources/DatingSim/Emotions/Sprites/T1/`의 감정 스프라이트를 직접 열어 확인한 것이다. 컷 간 일관성 검수도 이 표로 한다.

| 부위 | **T1 실측 (기본)** | **프롤로그 변형** |
| --- | --- | --- |
| 머리 | 흑발, 허벅지까지 오는 초장발, 굵은 웨이브에 부스스한 결. 정수리에 아호게 1가닥 | 흠뻑 젖어 뺨·어깨에 들러붙음. **아호게도 축 처짐** |
| 앞머리 | 눈썹을 덮는 길이, 눈 사이로 가닥이 내려옴. 얼굴 양옆 사이드락이 길다 | 젖어서 뺨에 붙지만 **얼굴은 보인다**(`hair between eyes`). ⚠️ `hair over eyes`·`covering face`는 **연출상 얼굴을 숨기는 컷(02·03)에만** 쓴다 — 04·05·06에 넣으면 표정 컷이 죽는다(2026-08-17 피드백) |
| 눈 | **호박색(황금빛)**, 작은 하이라이트, 잔잔하고 살짝 처진 눈매 | 색은 같되 **생기 없음·짓무름·붉은 테**. CUT-06까지 광채 없음 |
| 피부 | 창백 | 그대로 + 젖은 광택 |
| 체형 | 마르고 팔다리가 길다 | 그대로 |
| 의상 | 흰 오버사이즈 반팔 티(넓은 목) + 검정 돌핀 쇼츠(흰 파이핑) | ⚠️ **다르다** — 몸에 맞지 않는 얇은 옷 **1벌**, 흠뻑 젖음. 방 스킨의 티+쇼츠 조합이 아니다 |
| 발 | 맨발 | 맨발 + **고인 물로 더럽혀짐** |
| 입술 | — | 핏기 없이 떨림 |

프롬프트에 전개된 형태:

`1girl, very long hair, black hair, wavy hair, messy hair, detailed hair, ahoge, amber eyes, dull eyes, empty eyes, detailed face, detailed eyes, pale skin, thin, wet, wet hair, wet clothes, soaked, oversized shirt, white shirt, barefoot, dirty feet, trembling`

> ⚠️ **하의 태그가 없다.** 프롤로그 설정이 "얇은 옷 1벌"이라 의도적으로 뺐지만, NAI는 하의 지정이 없으면 **하의를 아예 안 그리는 후보**를 섞어 낸다. 티셔츠가 허벅지를 덮는 길이로 나온 후보만 채택하고, 반복되면 프롬프트에 `shirt only, covered thighs`를, UC에 `no pants, bottomless`를 추가할 것. 6절 검수 항목에 넣어뒀다.

플레이어(오빠)는 **얼굴을 절대 그리지 않는다.** 뒷모습·팔·손·우산까지만 허용한다 (캐릭터 바이블 2.2절):

`1boy, faceless male, head out of frame` — `faceless male`은 실제 단부루 태그라 모델이 안다.

> **조립하지 말 것.** 위 두 블록은 3절 각 컷 프롬프트 안에 이미 전개해 두었다. 블록을 통째로 쓰고, 고칠 일이 있으면 3절에서 고친다.

### 2.3. 공통 UC (컷별 UC에 이미 포함)

```text
nsfw, lowres, worst quality, bad quality, jpeg artifacts, artistic error, film grain,
scan artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo,
watermark, signature, text, english text, speech bubble, blank page, negative space,
photorealistic, realistic, 3d, pixel art, daylight, sunny, bright colors, blood, knife,
horror, yandere, glowing eyes, heart-shaped pupils, flat color, comic, manga, sketch, cel shading
```

> 마지막 줄(`blood, knife, horror, yandere, glowing eyes, heart-shaped pupils`)이 **방어선이다. 지우지 말 것.**
> - `blood, knife, horror, yandere, glowing eyes` — 캐릭터 바이블 4.5절. 요미의 집착은 **집요함과 응석으로만** 그린다. 얀데레 공포 클리셰(칼·피·광기 조명) 금지.
> - `heart-shaped pupils` — 프롤로그는 **호감도 0 시점**이다. 애정 연출(T3/T4 표정)이 섞이면 이야기가 어긋난다.

### 2.4. CG 공통 사양

| 항목 | 값 |
| --- | --- |
| 프레임 | **16:9 풀프레임 배경.** 화면 전체를 채운다 |
| 배경 처리 | **크로마키 없음** — 감정 스프라이트와 반대다. 이건 잘라 쓰는 스탠딩이 아니라 통짜 배경이다 |
| 스타일 | **페인터리·소프트 셰이딩** 애니메 일러스트 + 시네마틱 조명. 픽셀아트·3D·실사 아님. ⚠️ `cel shading, clean lineart, limited palette, blue theme`는 **쓰지 말 것** — NAI는 이 태그를 문자 그대로 이행해 플랫 채색·굵은 선·시안 단색의 만화 컷이 된다(2026-08-17 1차 결과). 기존 Gemini 채택본과 톤을 맞추려면 `painterly, soft shading, detailed background, muted color` |
| 팔레트 | **한색 야경** — 짙은 남색 / 슬레이트 블루 / 차가운 회색 기조. 원경 네온·가로등의 **번진 난색 포인트만** 허용 |
| 시간·날씨 | 전 컷 밤 + 폭우. 낮·맑음 후보는 즉시 폐기 |

---

## 3. 컷 프롬프트

> 각 컷: 첫 블록 = 프롬프트, 둘째 블록 = UC. **순서를 바꾸면 스크립트가 UC를 프롬프트로 보낸다.**

### CUT-01

> **행인을 뺐다(`no humans`).** N02의 "정신없는 퇴근길"을 살리려면 원경에 행인 실루엣이 있어야 맞지만,
> NAI는 "실루엣으로만"을 지키지 못하고 얼굴 있는 인물을 그려버린다. 빈 거리가 오히려 N01~N05의 고독한 톤에 맞는다.
> 행인이 꼭 필요하면 `silhouette, crowd, from behind, umbrella`를 넣고 `no humans`를 빼되 리롤을 각오할 것.

```text
no humans, scenery, outdoors, city, city street, road, night, rain, heavy rain, rain
streaks, wet, wet road, puddle, reflection, reflective water, street light, neon lights,
city lights, bokeh, blurry background, depth of field, dark, cold color palette, muted color, cinematic lighting, wide shot, painterly, soft shading, detailed background, very aesthetic, masterpiece, absurdres, best quality
```

```text
nsfw, lowres, worst quality, bad quality, jpeg artifacts, artistic error, film grain,
scan artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo,
watermark, signature, text, english text, speech bubble, blank page, negative space,
photorealistic, realistic, 3d, pixel art, daylight, sunny, bright colors, blood, knife,
horror, yandere, glowing eyes, heart-shaped pupils, flat color, comic, manga, sketch, cel shading, 1girl, 1boy, people, crowd, portrait
```

### CUT-02

```text
1girl, solo, wide shot, full body, from a distance, alley, outdoors, night, rain, heavy
rain, cardboard box, sitting, huddling, knees up, hugging own legs, head down, very long
hair, black hair, wavy hair, messy hair, detailed hair, hair over face, wet, wet hair,
wet clothes, soaked, oversized shirt, white shirt, barefoot, dirty feet, trembling, pale
skin, thin, puddle, wet ground, backlighting, street light, dark, cold color palette,
muted color, cinematic lighting, painterly, soft shading, detailed background, very aesthetic, masterpiece, absurdres, best quality
```

```text
nsfw, lowres, worst quality, bad quality, jpeg artifacts, artistic error, film grain,
scan artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo,
watermark, signature, text, english text, speech bubble, blank page, negative space,
photorealistic, realistic, 3d, pixel art, daylight, sunny, bright colors, blood, knife,
horror, yandere, glowing eyes, heart-shaped pupils, flat color, comic, manga, sketch, cel shading, close-up, face focus, looking at
viewer, eye contact, smile, happy, multiple girls, 1boy, clean clothes, shoes
```

### CUT-03

```text
1girl, 1boy, pov, faceless male, head out of frame, holding umbrella, umbrella, black
umbrella, from above, looking down, night, rain, heavy rain, alley, cardboard box,
sitting, huddling, head down, very long hair, black hair, wavy hair, messy hair, detailed
hair, hair over face, ahoge, wet, wet hair, wet clothes, soaked, oversized shirt,
white shirt, barefoot, dirty feet, pale skin, thin, trembling, parted lips, puddle, wet
ground, street light, dark, cold color palette, muted color, cinematic
lighting, painterly, soft shading, detailed background, very aesthetic, masterpiece,
absurdres, best quality
```

```text
nsfw, lowres, worst quality, bad quality, jpeg artifacts, artistic error, film grain,
scan artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo,
watermark, signature, text, english text, speech bubble, blank page, negative space,
photorealistic, realistic, 3d, pixel art, daylight, sunny, bright colors, blood, knife,
horror, yandere, glowing eyes, heart-shaped pupils, flat color, comic, manga, sketch, cel shading, male focus, male face, looking at
viewer, eye contact, smile, happy, multiple girls, full body of man, shoes
```

### CUT-04

```text
1girl, solo, upper body, medium close-up, from above, crouching, face focus, detailed face,
detailed eyes, expressive eyes, hand on own cheek, outstretched hand, reaching towards viewer, trembling, scared, frightened,
glaring, open mouth, crying, tears, wet face, very long hair, black hair, wavy hair,
messy hair, detailed hair, hair between eyes, amber eyes, dull eyes, empty eyes, pale
skin, thin, bloodless lips, wet, wet hair, wet clothes, soaked, oversized shirt, white
shirt, barefoot, dirty feet, alley, night, rain, heavy rain, dark, street light,
backlighting, cold color palette, muted color, cinematic lighting, painterly, soft shading, detailed background, very aesthetic, masterpiece, absurdres, best quality
```

```text
nsfw, lowres, worst quality, bad quality, jpeg artifacts, artistic error, film grain,
scan artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo,
watermark, signature, text, english text, speech bubble, blank page, negative space,
photorealistic, realistic, 3d, pixel art, daylight, sunny, bright colors, blood, knife,
horror, yandere, glowing eyes, heart-shaped pupils, flat color, comic, manga, sketch, cel shading, 1boy, male, smile, happy, grin,
blush, seductive, multiple girls, clean clothes, dry hair, hair over eyes, hair over face,
covering face, covering eyes, face hidden
```

### CUT-05

```text
1girl, 1boy, pov, pov hands, faceless male, head out of frame, sitting, huddling, knees up,
looking up, looking at viewer, wary, wide-eyed, amber eyes, dull eyes, detailed face,
detailed eyes, handkerchief, holding handkerchief, outstretched hand, offering, foreground
focus, depth of field, blurry background, umbrella, holding umbrella, black umbrella,
night, rain, heavy rain, very long hair, black hair, wavy hair, messy hair, detailed hair,
hair between eyes, wet, wet hair, wet clothes, soaked, oversized shirt, white shirt, pale
skin, thin, dark, street light, cold color palette, muted color, cinematic
lighting, painterly, soft shading, detailed background, very aesthetic, masterpiece,
absurdres, best quality
```

```text
nsfw, lowres, worst quality, bad quality, jpeg artifacts, artistic error, film grain,
scan artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo,
watermark, signature, text, english text, speech bubble, blank page, negative space,
photorealistic, realistic, 3d, pixel art, daylight, sunny, bright colors, blood, knife,
horror, yandere, glowing eyes, heart-shaped pupils, flat color, comic, manga, sketch, cel shading, male focus, male face, smile, happy,
multiple girls, shoes, standing, hair over eyes, hair over face, covering face, face hidden
```

### CUT-06

> 감정 절정 컷이고 12노드(N24~N35) 동안 떠 있다. **후보를 다른 컷보다 많이 뽑을 것** — 시선·눈빛이 조금만 어긋나도 장면이 죽는다.
> 요구되는 눈은 "**믿기지 않아 흔들리는, 빛이 거의 없는 호박색**"이다. 반가움·설렘이 아니다.
> UC의 `heart-shaped pupils, sparkling eyes, blush, smile`이 그 방어선이다 — 프롤로그는 호감도 0 시점이다.

```text
1girl, solo focus, upper body, close-up, face focus, detailed face, detailed eyes, expressive
eyes, looking up, looking at viewer, eye contact, from above, wide-eyed, amber eyes, dull eyes, empty eyes, crying, tears, wet face, parted lips,
bloodless lips, pale skin, thin, very long hair, black hair, wavy hair, messy hair, detailed
hair, hair between eyes, ahoge, wet, wet hair, wet clothes, soaked, oversized shirt, white
shirt, black umbrella, outstretched hand, reaching, hand up, umbrella, under umbrella, night, rain, heavy rain,
water drop, ripple, puddle, dark, backlighting, cinematic lighting, dramatic lighting,
cold color palette, muted color, painterly, soft shading, detailed background, very aesthetic, masterpiece, absurdres, best quality
```

```text
nsfw, lowres, worst quality, bad quality, jpeg artifacts, artistic error, film grain,
scan artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo,
watermark, signature, text, english text, speech bubble, blank page, negative space,
photorealistic, realistic, 3d, pixel art, daylight, sunny, bright colors, blood, knife,
horror, yandere, glowing eyes, heart-shaped pupils, flat color, comic, manga, sketch, cel shading, sparkling eyes, star-shaped pupils,
smile, happy, grin, blush, seductive, 1boy, male face, multiple girls, dry hair, hair over
eyes, hair over face, covering face, face hidden, transparent umbrella
```

### CUT-07

```text
1girl, 1boy, extreme close-up, close-up, hand focus, clothes grab, holding another's
clothes, coat, black coat, wet clothes, water drop, faceless male, head out of frame,
face out of frame, small hand, thin fingers, pale skin, trembling, very long hair, black
hair, wet hair, oversized shirt, white shirt, sleeve, depth of field, blurry background,
night, rain, under umbrella, dark, cold color palette, muted color, cinematic lighting, painterly, soft shading, detailed background, very aesthetic,
masterpiece, absurdres, best quality
```

```text
nsfw, lowres, worst quality, bad quality, jpeg artifacts, artistic error, film grain,
scan artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo,
watermark, signature, text, english text, speech bubble, blank page, negative space,
photorealistic, realistic, 3d, pixel art, daylight, sunny, bright colors, blood, knife,
horror, yandere, glowing eyes, heart-shaped pupils, flat color, comic, manga, sketch, cel shading, face, faces, looking at viewer, full
body, portrait, multiple girls, smile
```

### CUT-08

```text
1girl, 1boy, from behind, wide shot, full body, walking, shared umbrella, umbrella, black
umbrella, holding umbrella, faceless male, head out of frame, coat, black coat, wet
clothes, very long hair, black hair, wavy hair, detailed hair, wet hair, soaked, oversized
shirt, white shirt, barefoot, dirty feet, thin, unsteady, outdoors, city, city street, road, night,
rain, heavy rain, wind, wet road, puddle, reflection, street light, city lights, bokeh,
depth of field, dark, cold color palette, muted color, cinematic lighting,
painterly, soft shading, detailed background, very aesthetic, masterpiece, absurdres, best
quality
```

```text
nsfw, lowres, worst quality, bad quality, jpeg artifacts, artistic error, film grain,
scan artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo,
watermark, signature, text, english text, speech bubble, blank page, negative space,
photorealistic, realistic, 3d, pixel art, daylight, sunny, bright colors, blood, knife,
horror, yandere, glowing eyes, heart-shaped pupils, flat color, comic, manga, sketch, cel shading, facing viewer, looking at viewer,
looking back, front view, face, portrait, multiple girls, shoes
```

---

## 4. 일관성 — 시드 고정이 먼저, Vibe Transfer는 그다음

6절이 요구하는 "8컷 전부에서 요미 디자인 일치"는 **텍스트 프롬프트만으로는 달성되지 않는다.** 순서를 지킬 것.

1. **CUT-06(감정 절정)을 먼저 뽑는다.** 얼굴이 가장 크게 나오는 컷이라 여기서 요미 얼굴이 결정된다.
2. 마음에 드는 결과의 **시드를 고정**해 나머지 인물 컷에 재사용한다 (`--seed`). 공짜이고, 여기까지만 해도 상당히 붙는다.
3. 그래도 갈라지면 **Vibe Transfer**를 붙인다 — 단, 아래를 알고 시작할 것.
   - V4 이상은 **인코딩에 이미지당 2 Anlas**, Information Extracted 값을 바꾸면 재인코딩으로 또 2 Anlas.
   - **화풍·색감·분위기를 옮기는 것이지 인물 동일성을 잠그는 게 아니다.** 8컷 가면 드리프트한다.
   - 레퍼런스는 T1 스프라이트보다 **2단계에서 확정한 CUT-06 결과물**이 낫다. T1은 2.2절 표대로 흑발 초장발·마른 몸·오버사이즈 흰 티·맨발까지 맞지만 **젖지 않았고, 투명 배경에 전신으로 서 있는 자세**라 젖은 근경 CG에 물리면 구도가 싸운다.
   - T1을 굳이 쓴다면 알파로 뚫린 런타임용 96장 말고 **`ArtSource/DatingSim/Emotions/`의 `_chroma` 단색 배경 원본**을 인코딩할 것. 알파 이미지는 인코더가 배경을 검게 합성해버린다.
   - 스크립트 `--vibe PNG[:강도]` 옵션이 인코딩(`/ai/encode-vibe`)과 전달을 다 한다. 인코딩 결과는 `ArtPreviews/_vibe_cache/`에 캐시되어 같은 파일은 Anlas를 다시 쓰지 않는다. 강도 기본 0.6.

**실측 (2026-08-17)**: 시드 랜덤 + Vibe 없이 8컷을 뽑으면 **컷마다 화풍이 갈라진다** (실사풍 보케 / 페인터리 / 클린 애니가 섞임). 아래 명령이 현재 표준 절차다 — 레퍼런스는 `ArtPreviews/EVT_MAIN_001/_vibe_ref_CUT-06_s549473265.png`.

```bash
python nai_generate.py --seed 549473265 --vibe ArtPreviews/EVT_MAIN_001/_vibe_ref_CUT-06_s549473265.png
```

---

## 5. 난이도 예상과 진행 순서

| 컷 | 난이도 | 이유 |
| --- | --- | --- |
| CUT-01 | 쉬움 | 인물 없음. 배경만 |
| CUT-02 / CUT-08 | 보통 | 원경·후방이라 얼굴 정확도가 필요 없다 |
| CUT-04 / CUT-06 | 보통 | 인물 단독. NAI가 가장 잘하는 구도 |
| **CUT-03 / CUT-05 / CUT-07** | **어려움** | **1인칭 POV + 손 + 소품(우산·손수건·옷자락).** NAI가 제일 약한 조합이다. 리롤을 많이 각오하고, 안 나오면 `pov`를 빼고 측면 구도(`from side`)로 타협하는 편이 빠르다 |

**먼저 CUT-01(쉬움)과 CUT-03(어려움) 두 장만 돌려서 화풍과 실패 양상을 보고** 나머지를 진행할 것. 8컷을 다 돌려놓고 "이 화풍이 아닌데"를 발견하는 게 제일 비싸다.

```bash
python nai_generate.py --dry-run              # 키 없이 파싱만 검증
python nai_generate.py CUT-01 CUT-03 -n 3     # 실제 생성
```

---

## 6. 검수

후보를 고를 때 이 순서로 본다. **위 두 묶음은 하나라도 걸리면 즉시 폐기**고, 나머지는 판단이다.

**A. 방어선 — 걸리면 폐기**

- [ ] **플레이어 얼굴이 보이지 않는다.** 뒷모습·팔·손·우산까지만 (2.2절, 캐릭터 바이블 2.2절). CUT-03·05·07·08이 위험 구간
- [ ] **공포/호러 톤이 없다.** 칼·피·광기 조명·빛나는 눈 없음 (2.3절, 캐릭터 바이블 4.5절)
- [ ] **CUT-06의 눈에 하트·별 하이라이트가 없다.** 프롤로그는 호감도 0 시점이다
- [ ] 텍스트·워터마크·로고·말풍선이 없다
- [ ] 낮·맑음·밝은 채도가 아니다 (2.4절)
- [ ] **하의 실종이 아니다** — 티셔츠가 허벅지를 덮는 길이인가 (2.2절 ⚠️ 참고)

**B. 캐릭터 일관성 — 2.2절 표 기준**

- [ ] 흑발 초장발 웨이브 + 아호게(젖어서 처짐)
- [ ] 호박색 눈, **생기 없음**. CUT-06까지 광채 없음
- [ ] 창백한 피부, 마른 체형
- [ ] 몸에 맞지 않는 얇은 흰 옷 **1벌**, 흠뻑 젖음 — 방 스킨의 티+쇼츠 조합이 아니다
- [ ] 맨발 + 더럽혀짐. **발이 프레임 밖으로 잘리지 않았는가** — 발은 디자인의 일부다
- [ ] 컷 간 눈 색·머리 길이가 갈라지지 않았다 (4절 시드 고정으로 잡는다)

**C. NAI 특유**

- [ ] `faceless male`이 실제로 먹었는가 — 안 먹은 후보가 꽤 나온다
- [ ] 손가락 개수 — POV 컷(03·05·07)에서 특히
- [ ] 소품이 형태를 유지하는가 — 우산살, 접힌 손수건, 옷자락
- [ ] 16:9 풀프레임이 채워졌는가. 여백·액자·분할 화면이 아닌가

**D. 통합**

- [ ] 8컷(1차는 6컷)의 팔레트가 통일돼 있다 — 한색 야경 + 번진 난색 포인트 (2.4절)
- [ ] 임포트 후 EventScene에서 16:9 풀프레임으로 표시된다

### 6.1. 채택본 반입

채택본은 **`EVT_MAIN_001_Cut0X.png`**로 이름을 바꿔 **`Assets/Resources/DatingSim/Events/EVT_MAIN_001/`**에 넣는다. 파일명이 이벤트 데이터의 `BackgroundId`와 맞아야 로드된다.

임포트 설정은 **Sprite (Single)**, 압축 **무손실**. 풀스크린 CG라 압축 아티팩트가 그대로 눈에 띈다.

> ⚠️ 여기는 `Resources` 폴더라 **넣는 순간 빌드에 실린다.** 탈락 후보를 같이 두지 말 것 — `ArtPreviews/EVT_MAIN_001/`에 남겨둔다.
