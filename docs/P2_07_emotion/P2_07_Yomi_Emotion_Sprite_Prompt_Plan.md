# P2_07 요미 감정 스프라이트 96종 — AI 생성 프롬프트 계획서

> **작성일**: 2026-08-14
> **범위**: 미연시 전용 감정 24종 × 호감도 4구간 = **총 96장**
> **용도**: [EventScene_System_Plan.md](../P2_04_System/EventScene_System_Plan.md) R2 "호감도 단계별 전용 감정 에셋"의 상세 테이블이자, 이미지 생성 AI에 그대로 전달할 프롬프트 원본
> **근거 문서**: [P2_07_DatingSim_Emotion_Table_Implementation_Plan.md](P2_07_DatingSim_Emotion_Table_Implementation_Plan.md) (감정 24종 정의) · [P2_02_Yomi_Character_Bible.md](../P2_02_Worldbuilding/P2_02_Yomi_Character_Bible.md) (성격 5축·표현 수위) · `Assets/Resources/Characters/States/Standard.png` (기본 스킨 실측)

---

## 0. 이 문서를 쓰는 법

최종 프롬프트는 **[A 공통 블록] + [B 호감도 구간 변조] + [C 감정 블록]** 세 조각을 합쳐 만든다.

- **3절 A**: 96장 전부에 들어가는 캐릭터 시트 + 기술 제약. 절대 수정하지 않는다
- **4절 B**: 구간(T1~T4)마다 1개. 같은 구간 24장은 같은 B를 쓴다
- **5절 C**: 감정마다 1개. 같은 감정 4장은 같은 C를 쓰되, 블록 안의 "구간 변조" 줄로 강도만 조절한다
- **6절**: 조합 예시 (완성 프롬프트 3건)
- **7절**: 비율·누끼를 지키는 생성 파이프라인 — **프롬프트만으로는 96장의 크기 일관성이 안 나온다. 반드시 읽을 것**
- **8절**: 작업 순서 / **9절**: 검수 체크리스트 / **10절**: 저장 경로와 코드 연결

---

## 1. 기본 스킨 실측 — 프롬프트의 근거

`Assets/Resources/Characters/States/Standard.png` (600×1180, 투명 배경) 및 감정 19종 실물을 직접 확인한 구조화 결과다. **아래 특징이 96장 전부에서 유지되어야 한다.**

| 부위 | 실측 특징 |
| --- | --- |
| 머리카락 | **흑발, 매우 긴 웨이브 장발** (엉덩이 아래까지). 층이 많고 부스스하게 뻗친 갈래 다수 — 정돈되지 않은 느낌이 핵심 |
| 아호게 | 정수리에 **더듬이형 아호게 1가닥**. 감정 표현의 보조 기관으로 쓴다 (5절 각 블록 참조) |
| 앞머리 | 눈 사이로 갈라져 내려오는 가르마형 앞머리 + 얼굴 양옆으로 길게 흐르는 사이드록 |
| 눈 | **호박색(황금빛 주황) 눈동자**. 기본 스킨은 감은 눈이지만 감정컷들은 뜬 눈 기준 |
| 피부 | 밝고 창백한 톤. 뺨에 옅은 홍조가 기본 탑재 |
| 상의 | **오버사이즈 흰색 반팔 티셔츠** (몸에 비해 낙낙, 목둘레 넓음) |
| 하의 | **검은색 돌핀 쇼츠, 흰색 테두리 라인** |
| 발 | **맨발** (집순이 설정 — 오빠 자취방에 얹혀사는 히로인) |
| 체형 | 마른 소녀 체형, 체력 낮음 설정과 일치 |
| 포즈(기존) | 걸터앉은 자세 — 트레이딩 UI 턱에 앉히는 용도. **신규 96종은 스탠딩으로 전환한다** (2절) |

> 기존 트레이딩 감정 스프라이트는 픽셀아트풍이다. 신규 96종은 **캐주얼 일러스트**로 새로 그리므로 스타일은 계승하지 않고 **캐릭터 디자인(위 표)만 계승**한다.

## 2. 신규 세트의 고정 사양

| 항목 | 값 | 근거 |
| --- | --- | --- |
| 스타일 | **캐주얼 애니메 일러스트** — 깔끔한 선화 + 셀 셰이딩. 픽셀아트 아님 | 발주 지시 |
| 포즈 | **스탠딩 전신, 정면**. 하반신은 96장 전부 동일 자세 고정, 감정 표현은 얼굴·손·상체로만 | EventScene `YomiStanding` 용도 |
| 배경 | **없음 (누끼)**. 생성은 단색 배경 → 후처리 제거 (7.3절) | 발주 지시 |
| 캔버스 | 세로형 1:2 근접. **권장 1024×2048 이상 생성 → 게임 임포트 시 다운스케일**. 96장 전 컷 동일 해상도 | 기존 600×1180 비율 계승 |
| 프레이밍 | 캐릭터 전신이 잘리지 않게, 상하좌우 여백 비율 고정. **컷마다 크기·위치가 달라지면 안 된다** — 7절 파이프라인으로 강제 | 발주 지시 |
| 의상 | 기본 스킨 1벌 고정 (흰 티 + 검정 돌핀 쇼츠 + 맨발). 코스튬 변형은 이번 범위 밖 | 물량 관리 |
| 폴백 | `Calm`(평온)이 누락 대체 이미지 — **구간마다 Calm을 가장 먼저 만든다** | 구현 계획 2절 6번 |

### 호감도 4구간 (이 문서에서 확정)

| 구간 | 호감도 | 관계 단계 이름 | 한 줄 정의 |
| --- | --- | --- | --- |
| **T1** | 0~30 | 서먹 | 같이 살지만 아직 조심스럽다. 시선을 잘 못 맞추고 반 발짝 물러서 있다 |
| **T2** | 31~60 | 일상 | 편해졌다. 투정·장난·삐짐이 자연스럽게 나온다 |
| **T3** | 61~90 | 애정 | 마음이 겉으로 샌다. 홍조가 잦고 몸이 화면(오빠) 쪽으로 기운다 |
| **T4** | 90+ | 전면화 | 세계가 오빠 하나로 축소된다. 시선 고정, 거리 제로. **단 수위는 애원·응석까지 — 공포 연출 금지** |

> 현행 코드의 대화 토픽 해금 경계(0/31/61, `YomiTalkTopics.MinAffection`)와 T1~T3 경계가 일치한다. T4(90+)는 이번에 신설되는 연출 전용 구간이며 토픽 해금과는 무관하다.
> T4의 수위 상한은 캐릭터 바이블 4.5절 4번(폭력적 협박 금지)을 그대로 따른다. **얀데레 공포 클리셰(칼·피·광기 조명) 금지** — 집착은 집요함과 응석으로만 그린다.

---

## 3. [A] 공통 프롬프트 블록 — 96장 전부 동일

```
masterpiece, best quality, 1girl, solo, full body, standing, facing viewer,
very long messy black hair, wild voluminous wavy hair, hip-length hair,
single prominent ahoge, long sidelocks, parted bangs, hair between eyes,
amber eyes, golden-orange eyes, pale skin, small blush,
oversized white short-sleeve t-shirt, loose fit, black dolphin shorts with white trim, bare legs, barefoot,
slender teenage girl body, flat casual anime illustration, clean lineart, cel shading, soft pastel colors,
standing straight, feet together, character centered, whole body in frame with even margins,
plain solid green background, no floor shadow, no background objects
```

**네거티브 프롬프트 (96장 공통)**

```
background scenery, floor, shadow on ground, cropped, out of frame, close-up, portrait only,
multiple girls, extra limbs, bad hands, extra fingers, text, watermark, signature, logo,
chibi, deformed, 3d, photorealistic, realistic, pixel art,
different hair color, short hair, twin tails, shoes, socks, pants, skirt, different outfit,
weapon, knife, blood, horror, gore
```

- 배경색 `green`은 누끼 후처리용 크로마키다. 머리카락이 흑발이라 녹색이 안전하다 (7.3절)
- `pixel art`를 네거티브에 넣는 이유: 참조 이미지를 쓰는 워크플로(7.2절)에서 기존 픽셀 스프라이트의 화풍이 새어 들어오는 것을 막는다

---

## 4. [B] 호감도 구간 변조 블록 — 구간당 1개

구간은 **시선 · 홍조 · 몸의 방향(개방도) · 거리감** 네 가지만 조절한다. 감정 자체를 바꾸지 않는다.

### T1 (0~30) 서먹
```
reserved body language, slightly closed posture, arms staying close to body,
looking slightly away from viewer, hesitant expression, minimal blush,
restrained emotional expression
```
연출 방침: 모든 감정이 **한 톤 눌린 채** 나온다. 기쁨도 조심스럽고, 슬픔도 참는다. 바이블 3.2절의 "위축" 상태가 기본값.

### T2 (31~60) 일상
```
relaxed natural posture, comfortable body language,
making eye contact with viewer, natural blush, honest open expression
```
연출 방침: 감정이 **정직하게** 나온다. 삐짐·투정·장난 같은 응석 계열이 가장 잘 사는 구간. 기존 실측 대사의 평균 온도가 여기다.

### T3 (61~90) 애정
```
warm affectionate body language, leaning slightly toward viewer,
soft loving gaze at viewer, frequent deep blush,
open posture, emotionally expressive
```
연출 방침: 어떤 감정이든 **바닥에 애정이 깔린다.** 화를 내도 서운함이 비치고, 슬퍼도 오빠를 향해 운다.

### T4 (90+) 전면화
```
intense devoted gaze fixed on viewer, deep full-face blush,
very close clingy body language, hands reaching toward viewer or clasped in pleading,
overflowing obsessive affection, slightly unhinged but cute expression
```
연출 방침: 시선이 화면에서 **떨어지지 않는다.** 연애 감정 계열은 하트형 하이라이트 눈동자 허용(`heart-shaped pupils`). 단 광기 연출은 "귀엽게 과한" 선까지 — 네거티브의 horror 계열이 방어선이다.

---

## 5. [C] 감정 블록 24종

각 블록 구성: **표정 핵심**(눈썹·눈·입) / **포즈·손** / **아호게·이펙트** / **구간 변조** 한 줄.
표정 방향은 감정 테이블 구현 계획 3절의 "표정·대사 방향", 수위는 캐릭터 바이블(집착=애원·응석, 자기 비하 금지, 분노=삐짐)을 따른다.

### 5.1 기본 감정 12종

#### 00. Calm 평온 — 폴백 이미지
- 표정: 편안하게 뜬 눈, 옅은 입꼬리 미소
- `gentle relaxed expression, soft smile, calm half-open eyes, arms relaxed at sides, ahoge gently curved`
- 구간 변조: T1 무표정에 가까운 옅은 미소 → T4 미소는 잔잔한데 시선만 고정되어 있는 온도차

#### 01. Joy 기쁨
- 표정: 활짝 웃는 입, 반짝이는 눈. 요미의 기쁨은 곧 자랑으로 이어진다 — 우쭐함 한 스푼
- `bright beaming smile, open mouth happy, sparkling eyes, clenched fists raised in excitement, ahoge springing upward, small sparkle effects`
- 구간 변조: T1 입을 가리고 웃음 → T2 만세 → T4 그대로 안겨들 기세

#### 02. Sadness 슬픔
- 표정: 처진 눈썹, 내리깐 눈, 작게 다문 입. 눈물은 아직 없다 (오열은 별개)
- `sad downcast eyes, drooping eyebrows, small frown, head slightly lowered, hands clasped in front, ahoge drooping down`
- 구간 변조: T1 고개 돌리고 혼자 삭임 → T4 화면을 올려다보며 대놓고 시무룩

#### 03. Anger 분노
- 표정: **삐짐에 가까운 분노.** 볼 부풀리기 + 치켜뜬 눈. 위협적이면 안 된다 (바이블 4.5절)
- `angry pouting face, puffed cheeks, furrowed eyebrows, glaring up at viewer, hands on hips, comic anger vein, ahoge bent sharply`
- 구간 변조: T1 눈만 흘김 → T2 정석 볼빵빵 → T4 "쳐다보지도 마!" 하며 정작 자기가 노려봄

#### 04. Fear 공포
- 표정: 크게 뜬 떨리는 눈, 창백, 움츠림
- `frightened wide trembling eyes, pale face, shrunk posture, hands raised defensively near chest, sweat drops, ahoge wilted flat`
- 구간 변조: T1 혼자 굳음 → T3 이후 화면 쪽으로 반쯤 숨듯 다가옴 (무서울수록 오빠에게 붙는다)

#### 05. Surprise 놀람
- 표정: 동그랗게 커진 눈, 작은 "o" 입
- `surprised round wide eyes, small open mouth, slight jump back, hands raised, ahoge standing straight up like exclamation mark`
- 구간 변조: 전 구간 거의 동일 — 놀람은 반사라 관계 온도를 덜 탄다. 시선 처리만 B 블록을 따름

#### 06. Flustered 당황
- 표정: 흔들리는 눈동자, 홍조, 땀방울, 시선 회피. "어라...?" 하는 순간
- `flustered wavering eyes, averted gaze, blushing, sweat drop, waving both hands in panic denial, stammering open mouth, ahoge zigzag`
- 구간 변조: T1 얼어붙는 쪽 → T2 이후 손사래 치며 변명하는 쪽 (실수 → 즉시 변명이 요미다움)

#### 07. Anxiety 불안
- 표정: 팔자 눈썹, 입술을 깨물거나 손끝을 입가로. 말줄임표의 얼굴
- `anxious worried expression, slanted eyebrows, biting lip, fidgeting fingers together, glancing sideways, small hunched shoulders, ahoge quivering`
- 구간 변조: T1 감추려다 새는 불안 → T4 화면을 붙잡을 듯 "안 갈 거지...?" 하는 불안

#### 08. Relief 안도
- 표정: 감은 눈, 후- 하는 한숨, 가슴에 손
- `relieved sigh, closed eyes, soft exhale, hand on own chest, relaxed shoulders, gentle smile, single sweat drop, ahoge loosening`
- 구간 변조: T1 혼자 조용히 안도 → T3 이후 안도가 곧장 응석으로 이어질 표정

#### 09. Disappointment 실망
- 표정: 반쯤 감긴 김빠진 눈, 삐죽 나온 입, 처진 어깨
- `disappointed half-lidded eyes, deflated pout, drooped shoulders, arms hanging limply, looking away with distant expression, ahoge bent down`
- 구간 변조: T1 거리 두는 실망 → T4 "기대했는데..." 하고 원망 섞인 눈으로 화면을 봄

#### 10. Fatigue 피로
- 표정: 반쯤 감긴 졸린 눈, 옅은 다크서클, 느릿한 자세. 체력 낮은 설정의 얼굴
- `sleepy half-closed eyes, tired expression, faint dark circles, slouched posture, rubbing one eye with fist, small yawn, ahoge limp and drooping`
- 구간 변조: T1 꾸벅꾸벅 참음 → T3 이후 기대 잘 곳을 찾는 눈빛

#### 11. Curiosity 호기심
- 표정: 반짝이며 집중하는 눈, 고개 갸웃, 상체 앞으로
- `curious sparkling attentive eyes, head tilt, leaning forward with interest, finger on chin, slight smile, ahoge curled like question mark`
- 구간 변조: T1 곁눈질로 관찰 → T2 이후 대놓고 들여다봄

### 5.2 연애 감정 12종

> 이 12종이 호감도 구간을 가장 강하게 탄다. T1에서는 전부 "티 내지 않으려다 실패한" 형태로, T4에서는 전부 "숨길 생각이 없는" 형태로 그린다.

#### 12. Interest 관심
- 표정: 상대를 관찰하는 시선, 알 듯 말 듯한 미소
- `attentive observing gaze at viewer, slight knowing smile, hands behind back, leaning in slightly, ahoge tilted toward viewer`
- 구간 변조: T1 몰래 훔쳐보다 들킨 각도 → T4 관찰이 아니라 주시

#### 13. Fondness 호감
- 표정: 부드러운 미소, 순한 눈, 옅은 홍조
- `soft warm smile, gentle kind eyes, light blush, tucking hair strand behind ear, relaxed friendly posture`
- 구간 변조: T1 어색하게 웃어 보임 → T3 자연스럽게 배어나는 미소

#### 14. Excitement 설렘
- 표정: 진한 홍조, 두근거림을 누르는 손, 흔들리는 눈
- `strong blush, hands pressed on own cheeks, fluttering sparkling eyes, shy excited smile, small floating heart effects, ahoge bouncing`
- 구간 변조: T1 홍조를 들키지 않으려 고개 숙임 → T4 하트 이펙트 증량 허용

#### 15. Affection 애정
- 표정: 다정하게 풀린 눈매, 돌보는 사람의 미소
- `tender caring smile, warm half-lidded eyes, arms slightly open toward viewer, one hand extended gently, soft blush`
- 구간 변조: T1 손을 내밀다 마는 어중간함 → T4 두 팔을 벌려 맞이함

#### 16. Love 사랑
- 표정: 진지하고 깊은 눈. 장난기가 빠진 얼굴. 요미가 제일 조용해지는 감정
- `sincere deep loving gaze, serious soft expression, both hands over own heart, full blush, slightly parted lips`
- 구간 변조: T1 사용 자제(데이터에서 안 쓰는 걸 권장) → T3 정면 응시 → T4 `heart-shaped pupils` 추가 허용

#### 17. Trust 신뢰
- 표정: 경계가 완전히 풀린 얼굴, 잔잔한 정면 응시
- `serene trusting smile, completely relaxed open posture, steady direct eye contact, arms relaxed, peaceful expression`
- 구간 변조: T1 사용 자제 → T2 이후부터 자연스러움. T4는 "오빠니까"라는 무조건성이 보이게

#### 18. Shyness 부끄러움
- 표정: 진한 홍조, 시선 회피, 움츠린 어깨, 만지작거리는 손
- `deep blush, averted shy eyes, hunched shoulders, fidgeting index fingers together, small pursed mouth, steam puff above head, ahoge curling inward`
- 구간 변조: T1 등을 반쯤 돌림 → T4 부끄러워하면서도 시선은 화면에서 못 뗌 (이 모순이 T4다움)

#### 19. Jealousy 질투
- 표정: 볼 부풀린 삐짐 + 눈꼬리에 그렁한 눈물 + 흘겨보기. 질투의 대상은 사람만이 아니다 (게임, 잠, 차트까지)
- `jealous pouting face, puffed cheeks, sidelong glare at viewer, arms crossed, small tears at eye corners, comic anger tick, ahoge whipping sideways`
- 구간 변조: T1 토라져서 외면 → T4 "요미만 봐" 하며 정면에서 흘겨봄

#### 20. Hurt 서운함
- 표정: 그렁그렁하지만 흐르지는 않는 눈물, 원망스럽게 올려다보는 시선
- `teary glistening eyes not yet crying, downturned mouth, looking up at viewer reproachfully, tugging hem of own shirt, ahoge sagging`
- 구간 변조: T1 삼키는 서운함 → T4 "왜 몰라줘" 가 얼굴 전체에 써 있음

#### 21. Loneliness 외로움
- 표정: 위축된 자세로 자기 팔을 감싸 안음, 관심을 바라는 눈
- `lonely withdrawn expression, hugging own arms, small vulnerable posture, glancing up at viewer longingly, ahoge drooping low, muted atmosphere`
- 구간 변조: T1 혼자 견디는 그림 → T4 화면 쪽으로 반 발짝 다가와 옷자락이라도 잡을 듯한 그림

#### 22. Doubt 의심
- 표정: 가늘게 뜬 눈, 한쪽 눈썹 올림, 캐묻는 얼굴. "세력", "함정"을 말할 때의 그 눈
- `suspicious narrowed eyes, one eyebrow raised, scrutinizing sidelong look, arms crossed, slight frown, leaning back skeptically, ahoge in sharp angle`
- 구간 변조: T1 경계에 가까움 → T4 의심조차 독점욕 문맥("누구랑 있었어?")으로 읽히는 표정

#### 23. Obsession 집착
- 표정: **깜빡임 없는 큰 눈 + 진한 홍조 + 애원하듯 모은 손.** 무섭게가 아니라 과하게 사랑스럽게. 소유격의 얼굴
- `intense unblinking wide eyes fixed on viewer, deep blush, hands clasped together in pleading, leaning close toward viewer, overflowing devotion, slightly heavy eyelids, cute yandere expression without menace, ahoge perfectly still`
- 구간 변조: T1~T2 사용 자제 → T3 애원형 → T4 이 세트의 정점. `heart-shaped pupils` 허용. **칼·피·검은 오라 등 공포 소품은 전 구간 금지** (네거티브가 방어)

---

## 6. 조합 예시 — 완성 프롬프트

**예시 1: T2 × Joy (일상 구간의 기쁨)**
```
masterpiece, best quality, 1girl, solo, full body, standing, facing viewer,
very long messy black hair, wild voluminous wavy hair, hip-length hair,
single prominent ahoge, long sidelocks, parted bangs, hair between eyes,
amber eyes, golden-orange eyes, pale skin, small blush,
oversized white short-sleeve t-shirt, loose fit, black dolphin shorts with white trim, bare legs, barefoot,
slender teenage girl body, flat casual anime illustration, clean lineart, cel shading, soft pastel colors,
standing straight, feet together, character centered, whole body in frame with even margins,
plain solid green background, no floor shadow, no background objects,
relaxed natural posture, comfortable body language, making eye contact with viewer, natural blush, honest open expression,
bright beaming smile, open mouth happy, sparkling eyes, clenched fists raised in excitement, ahoge springing upward, small sparkle effects
```

**예시 2: T1 × Shyness (서먹 구간의 부끄러움)**
```
[A 공통 블록 전문],
reserved body language, slightly closed posture, arms staying close to body,
looking slightly away from viewer, hesitant expression, minimal blush, restrained emotional expression,
deep blush, averted shy eyes, hunched shoulders, fidgeting index fingers together,
small pursed mouth, steam puff above head, ahoge curling inward,
half turned away from viewer
```
> B가 `minimal blush`, C가 `deep blush`로 충돌하면 **C(감정)가 이긴다.** 조합 시 B에서 겹치는 단어를 지우고 C를 남긴다. 마지막 줄처럼 구간 변조 노트를 짧은 구문으로 덧붙인다.

**예시 3: T4 × Obsession (세트의 정점)**
```
[A 공통 블록 전문],
intense devoted gaze fixed on viewer, deep full-face blush, very close clingy body language,
overflowing obsessive affection, slightly unhinged but cute expression,
intense unblinking wide eyes fixed on viewer, heart-shaped pupils,
hands clasped together in pleading, leaning close toward viewer,
overflowing devotion, cute yandere expression without menace, ahoge perfectly still
```
> 네거티브 프롬프트는 3절의 것을 96장 전부 그대로 쓴다.

---

## 7. 생성 파이프라인 — 비율·크기·누끼를 지키는 방법

### 7.1 원칙: 96장을 각각 생성하지 않는다

텍스트 프롬프트만으로 96번 생성하면 **컷마다 체형·크기·서 있는 위치가 전부 달라진다.** 반드시 앵커 기반으로 간다.

```
[1] 마스터 앵커 1장 확정 (T2 × Calm 권장 — 가장 중립적)
       │  오너 검수: 디자인·비율·프레이밍 승인. 이 1장이 96장의 기준
       ▼
[2] 앵커에서 포즈 고정 정보 추출 (사용 도구에 따라 택1)
       · Stable Diffusion 계열: ControlNet OpenPose + reference_only
       · NovelAI: Vibe Transfer + 시드 고정
       · Midjourney: --cref (character reference) + --sref
       ▼
[3] 같은 포즈 조건 아래 얼굴·손·상체만 프롬프트로 변주
       · SD라면 얼굴/손 영역 인페인팅이 가장 확실 — 하반신은 픽셀 단위로 동일해짐
       ▼
[4] 크로마키 누끼 → 트리밍 없이 동일 캔버스 유지 → PNG 저장
```

### 7.2 도구별 권장 세팅

| 도구 | 캐릭터 고정 수단 | 비고 |
| --- | --- | --- |
| Stable Diffusion (SDXL/Illustrious 등) | ControlNet OpenPose(전신 골격 1개를 96장 공유) + 앵커 이미지 reference / 얼굴 인페인팅 | **일관성 최상.** 로컬 처리 가능. 권장 1순위 |
| NovelAI v4 | Vibe Transfer에 앵커 등록, 시드 고정, 프롬프트만 교체 | 애니 화풍 품질 높음. 포즈 미세 편차는 감수 |
| Midjourney v7 | `--cref [앵커 URL] --cw 100`, `--ar 1:2` | 포즈 고정력이 약함 — 후반 크롭 정렬 필수 |
| 기존 픽셀 스프라이트를 img2img 원본으로 | **비추천** | 픽셀 화풍이 섞여 나옴. 캐릭터 시트(3절)로 신규 생성이 깨끗하다 |

### 7.3 누끼(배경 제거)와 정렬

1. 생성 배경은 **단색 녹색** (A 블록에 명시). 흑발·흰 티와 겹치지 않는 유일한 안전색
2. 제거는 `rembg`(로컬, 무료) 또는 Photoshop 색상 범위. 머리카락 가장자리의 녹색 프린지 1px 정리
3. **트리밍 금지.** 96장 모두 같은 캔버스 크기를 유지한 채로 저장해야 게임에서 위치가 흔들리지 않는다
4. 정렬 검증: 96장을 레이어로 겹쳐 놓고 발끝·정수리 위치가 흔들리는 컷을 골라낸다 (9절 체크리스트)

---

## 8. 작업 순서

배치는 **구간 단위**로 돈다. 같은 구간을 몰아서 만들어야 구간의 온도가 24장에서 균일해진다.

| 단계 | 산출물 | 게이트 |
| --- | --- | --- |
| **0. 마스터 앵커** | T2 × Calm 1장 | **오너 승인 필수.** 디자인·비율·화풍이 여기서 확정. 이후 전 컷의 기준 |
| **1. Calm 4종** | T1·T2·T3·T4 × Calm | 구간별 룩 확정 + 폴백 이미지 4장 우선 확보. 4장을 나란히 놓고 구간 온도차가 보이는지 확인 |
| **2. T2 전체** | T2 × 나머지 23종 | 가장 많이 쓰일 일상 구간부터. 감정 블록 24개의 품질을 여기서 튜닝 |
| **3. T1 전체** | T1 × 23종 | 2단계에서 확정된 감정 블록에 T1 변조만 적용 |
| **4. T3 전체** | T3 × 23종 | 동일 |
| **5. T4 전체** | T4 × 23종 | 수위 검수 병행 — 공포 클리셰 유입 여부를 컷마다 확인 |
| **6. 통합 검수** | 96장 | 9절 체크리스트 전항목. 겹쳐보기 정렬 검증 |
| **7. 임포트** | Unity 반영 | 10절 경로에 배치, 디버그 순환 메뉴(구현 계획 Phase 5)로 인게임 확인 |

각 단계에서 감정당 **4~6장 후보 생성 → 1장 선별**을 기본으로 한다. 24장 × 4후보 = 구간당 약 100장 생성 물량으로 잡는다.

## 9. 검수 체크리스트 (컷 단위)

**캐릭터 일관성**
- [ ] 흑발 장발 + 아호게 1가닥 + 사이드록 유지 (아호게 2가닥 이상·소실은 탈락)
- [ ] 눈동자가 호박색 (갈색·빨강으로 새면 탈락)
- [ ] 의상: 흰 오버사이즈 티 + 검정 돌핀 쇼츠(흰 테두리) + 맨발 (신발·양말·긴바지 탈락)
- [ ] 앵커와 같은 얼굴로 보이는가 (다른 사람이면 탈락)

**규격**
- [ ] 전신이 프레임 안에 완전히 들어옴 (손끝·발끝·뻗친 머리카락 잘림 없음)
- [ ] 96장 동일 캔버스 크기, 겹쳐보기에서 발끝·정수리 오차가 캔버스 높이의 2% 이내
- [ ] 배경 완전 투명, 가장자리 녹색 프린지 없음, 바닥 그림자 없음

**감정·수위**
- [ ] 표정이 5절 블록의 정의와 일치하고, 옆 구간 컷과 나란히 놓았을 때 온도차가 보임
- [ ] 자기 비하형 연출 없음 (움츠림은 되지만 비굴한 그림은 요미가 아니다 — 바이블 4.5절 3번)
- [ ] T4 포함 전 컷에서 공포·폭력 소품 없음 (칼·피·검은 오라·광기 조명)
- [ ] Anger·Jealousy가 위협이 아니라 삐짐으로 읽힘

## 10. 저장 경로와 코드 연결

```text
Assets/Resources/DatingSim/Emotions/Sprites/
├── T1/   ├── Calm.png … Obsession.png   (24장, 호감도 0~30)
├── T2/   ├── …                          (24장, 31~60)
├── T3/   ├── …                          (24장, 61~90)
└── T4/   └── …                          (24장, 90+)
```

- 파일명은 `DatingEmotion` enum 코드명과 정확히 일치시킨다 (구현 계획 4절)
- **구현 계획서에 대한 파급**: [P2_07_DatingSim_Emotion_Table_Implementation_Plan.md](P2_07_DatingSim_Emotion_Table_Implementation_Plan.md)의 `DatingEmotionDefinition.defaultSprite`(단일)로는 4구간을 담을 수 없다. `Sprite[] tierSprites`(길이 4) 확장과, 호감도 → 구간 인덱스 변환(0~30→0, 31~60→1, 61~90→2, 91+→3) 한 곳이 필요하다. 구간 판정의 입력은 `DatingTimeManager.CurrentAffection`(현재치)을 쓴다 — 토픽 해금(PeakAffection 기준)과 달리 연출은 현재 관계 온도를 반영해야 한다
- 누락 시 폴백은 **같은 구간의 Calm** → 그것도 없으면 T2 Calm (구현 계획 2절 6번의 구체화)
- 스프라이트에는 텍스트가 없으므로 폰트 프리베이크는 불필요

## 11. 미결정 사항

| ID | 내용 | 권장 |
| --- | --- | --- |
| Q-S1 | T4 연애 감정의 `heart-shaped pupils`를 데이터 지정으로 켤지, 항상 켤지 | 항상 켜기 — T4 컷 수가 적고 구간 정체성이 명확해진다 |
| Q-S2 | Love·Trust·Obsession의 T1 컷을 실제로 제작할지 (연출상 쓸 일이 없음) | **제작한다.** 폴백 로직을 타지 않게 96장 풀세트가 안전하다. 단 제작 순서는 각 배치의 마지막 |
| Q-S3 | EventScene 외 요미 방(YomiRoom) 대화에도 이 세트를 쓸지 | 쓴다 — 감정 테이블이 미연시 파트 공용이 되도록 구현 계획 Phase 5가 이미 상정 |
