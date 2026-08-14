# P2_05 편의점 알바 타이쿤 — UI · 스프라이트 명세서

> **작성일**: 2026-08-14
> **범위**: [ConvenienceStore_Tycoon_MiniGame_Plan.md](../P2_04_System/ConvenienceStore_Tycoon_MiniGame_Plan.md)의 구현에 필요한 **모든** 아트 에셋과 UI 요소
> **근거 실측**: `Assets/Resources/DatingSim/YomiRoom/Morning/` 실물 · `Assets/Scripts/DatingSim/UI/YomiRoomTopDownPrototypeBuilder.cs`(로딩 규격) · [P2_05_UI_Asset_Assembly.md](P2_05_UI_Asset_Assembly.md)(조립 규약)
> **스타일 계승 대상**: 요미의 방 탑다운 (P2_07 캐주얼 일러스트 96종과는 **다른 계열**입니다 — 혼동 금지)

---

## 0. 이 문서를 쓰는 법

- **아트 담당**: §1 스타일 규정 → §3~§6 에셋 표 → §9 생성 프롬프트 → §11 검수 순으로 읽습니다.
- **구현 담당**: §2 임포트 규격과 §7 UI 배치표만 보면 됩니다. 씬은 빌더가 만들므로 인스펙터 조립은 없습니다.
- **신규 제작 물량**: **35장** (월드 18 · 캐릭터 3 · UI 14). 재사용 12종은 §10에 별도 표기.

> 물량을 이 이하로 줄인 판단 근거는 §12에 있습니다. 매대를 5종 각각 3단계로 그리면 15장이지만, **본체 1장 + 상품 오버레이 5종 × 2단계 = 11장**으로 접었습니다.

---

## 1. 스타일 규정

### 1.1 대전제

| 항목 | 값 | 근거 |
| --- | --- | --- |
| 화풍 | **탑다운 픽셀아트** (요미의 방과 동일 계열) | 이동 로직·워크시트를 그대로 재사용하므로 화풍이 갈리면 즉시 이질감 |
| 시점 | **정면 기울인 탑다운 (약 3/4뷰)** — 바닥은 위에서, 가구 정면은 살짝 보이게 | `RoomLeft.png` 실물과 동일 |
| 필터 | `FilterMode.Point` 고정 | 빌더가 로드 시 강제 (`ApplyResourceSprite`) — 안티에일리어싱된 원본을 넣으면 뭉갭니다 |
| 배경 처리 | 오브젝트는 **누끼(투명)**, 바닥/벽은 1장 통짜 | 요미의 방 `Room.png` / `Objects/*.png` 구조 그대로 |
| 조명 톤 | **형광등 아래의 인공적인 밝음** | 요미의 방은 새벽 네이비. 편의점은 그 대비로 "바깥은 밤, 안은 눈부심"을 만듭니다 |

### 1.2 팔레트

요미의 방 빌더 상수(`YomiRoomTopDownPrototypeBuilder.cs:16-21`)를 그대로 계승하고, 편의점 전용 3색만 추가합니다.

| 이름 | RGB | HEX | 용도 |
| --- | --- | --- | --- |
| `Navy` | `7, 16, 31` | `#07101F` | 카메라 배경 (창밖 밤거리) |
| `Wall` | `13, 28, 48` | `#0D1C30` | 벽면 하단, 그림자 |
| `Floor` | `29, 43, 59` | `#1D2B3B` | 바닥 베이스 |
| `Cyan` | `34, 211, 238` | `#22D3EE` | 상호작용 강조, 진행 바, 플레이어 계열 |
| `Pink` | `244, 114, 182` | `#F472B6` | 요미 계열, 경고 이전의 주의 |
| `Text` | `226, 245, 250` | `#E2F5FA` | 본문 텍스트 |
| **`Fluoro`** *(신규)* | `238, 246, 232` | `#EEF6E8` | 형광등 바닥 반사, 편의점 실내 하이라이트 |
| **`ShelfWood`** *(신규)* | `92, 104, 120` | `#5C6878` | 매대·계산대 본체 (차가운 회청색 금속) |
| **`Warn`** *(신규)* | `239, 68, 68` | `#EF4444` | 인내 게이지 위험, 재고 0, 감점 |

**색 규칙 3줄**

1. 요미가 **할 수 있는 것**은 `Cyan`, **하지 못하는 것**은 무채색 40% 알파.
2. **감점으로 이어지는 것**만 `Warn`. 남발하면 위험 신호가 안 보입니다 (오염·이탈 임박·재고 0, 이 셋뿐).
3. 매대·계산대·창고는 전부 `ShelfWood` 계열 3톤 안에서 해결합니다. 가구마다 색이 다르면 탑다운에서 시선이 흩어집니다.

### 1.3 픽셀 밀도

| 대상 | PPU | 근거 |
| --- | --- | --- |
| 캐릭터 워크시트 | **220** | `LoadWalkFrames`가 하드코딩 (`Sprite.Create(..., 220f)`) |
| 배경·가구 | **100** | `ApplyResourceSprite`가 100 고정 후 `desiredSize`로 스케일 보정 |
| UI | 스프라이트 원본 그대로 | `LoadUISprite` |

> ⚠️ 배경·가구는 코드가 `desiredSize`로 **강제 스케일**하므로 원본 픽셀 크기는 비율만 맞으면 됩니다. 반대로 **캐릭터는 스케일 보정이 없습니다** — 시트 셀 크기가 틀리면 요미와 손님의 키가 어긋납니다.

---

## 2. 공통 임포트 설정

Unity 임포터에서 아래대로 설정합니다. 빌더가 런타임에 `filterMode`/`wrapMode`를 덮어쓰지만, 에디터 프리뷰와 아틀라스 품질을 위해 임포트 시점에도 맞춰 둡니다.

| 설정 | 값 |
| --- | --- |
| Texture Type | `Sprite (2D and UI)` |
| Sprite Mode | `Single` (워크시트도 **Single** — 코드가 `Sprite.Create`로 직접 자릅니다) |
| Pixels Per Unit | §1.3 표 |
| Filter Mode | `Point (no filter)` |
| Compression | `None` (픽셀아트) |
| Wrap Mode | `Clamp` |
| Alpha Is Transparency | ✅ |
| Generate Mip Maps | ❌ |
| Max Size | 원본 이상 (다운스케일 금지) |

**저장 경로 규칙**: `Assets/Resources/DatingSim/Store/…`
`Resources` 하위여야 합니다 — 빌더가 `Resources.Load<Texture2D>(경로)`로만 접근합니다. 경로 문자열은 확장자 없이 씁니다.

```
Assets/Resources/DatingSim/Store/
├─ Room/          바닥·벽·창문
├─ Objects/       매대·계산대·창고·오염·잔여물
├─ Character/     손님 워크시트
└─ UI/            HUD·진행바·배지·아이콘
```

---

## 3. 월드 스프라이트 — 배경

| # | 파일 | 경로 | 권장 캔버스 | 월드 크기(`desiredSize`) | 내용 |
| --- | --- | --- | --- | --- | --- |
| W1 | `StoreRoom.png` | `Room/` | `1120 × 900` | `13.4 × 10.8` | 바닥·벽·천장 형광등·창문(밤거리)까지 **통짜 1장**. 가구는 포함하지 않음 |

**W1 세부 지시**

- 바닥: `Floor` 베이스에 `Fluoro` 10% 반사 줄무늬. 타일 격자는 **32px 단위**로 정렬 — 요미의 이동 속도(4.2)와 눈으로 맞습니다.
- 상단 1/6은 벽면(`Wall`)이며, 좌상단에 **창고 입구**, 중앙 상단에 **형광등 2줄**.
- 하단 중앙에 **자동문**(투명 유리 너머로 `Navy` 밤거리 실루엣). 출입문은 연출용이며 실제 트리거는 콜라이더가 담당합니다.
- 우하단 계산대 자리는 **비워 둡니다** (W1에 그리지 않음 — O2가 올라감).
- 가장자리 8px는 `Wall`로 어둡게 물려서 카메라 밖 잘림을 감춥니다.

---

## 4. 월드 스프라이트 — 가구·오브젝트

전부 **누끼**이며 피벗은 `(0.5, 0.5)`, 정렬은 `sortingOrder`로 처리합니다.

| # | 파일 | 경로 | 캔버스 | 월드 크기 | sortingOrder | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| O1 | `Shelf_Body.png` | `Objects/` | `320 × 260` | `2.6 × 2.1` | `5` | **5개 매대 공용 본체.** 빈 선반 3단 |
| O2 | `Counter.png` | `Objects/` | `420 × 300` | `3.4 × 2.4` | `5` | 계산대. POS 단말 + 봉투걸이 |
| O3 | `StorageRack.png` | `Objects/` | `300 × 320` | `2.4 × 2.6` | `5` | 창고 재고 선반 (상자 쌓임) |
| O4 | `ToolCabinet.png` | `Objects/` | `220 × 300` | `1.8 × 2.4` | `5` | 청소도구함. 대걸레 손잡이가 밖으로 삐져나옴 |
| O5 | `Leftover.png` | `Objects/` | `180 × 120` | `1.4 × 1.0` | `7` | 이탈 손님이 버린 물건 더미. **살짝 `Warn` 테두리** |

### 4.1 상품 오버레이 (재고 표시)

매대 본체(O1) 위에 얹는 **레이어**입니다. `sortingOrder = 6`.

| # | 파일 | 매대 | 캔버스 | 표시 조건 |
| --- | --- | --- | --- | --- |
| O6 | `Stock_Drink_Full.png` / `_Half.png` | 음료 | `300 × 200` | 재고 ≥ 60% / 0 초과 |
| O7 | `Stock_Snack_Full.png` / `_Half.png` | 과자 | 〃 | 〃 |
| O8 | `Stock_Lunch_Full.png` / `_Half.png` | 도시락 | 〃 | 〃 |
| O9 | `Stock_Daily_Full.png` / `_Half.png` | 생활용품 | 〃 | 〃 |
| O10 | `Stock_Ice_Full.png` / `_Half.png` | 아이스크림 | 〃 | 〃 |

= **10장.** 재고 0이면 오버레이를 끄고 O1의 빈 선반이 그대로 보입니다.

> `_Half`는 `_Full`에서 **물건 몇 개를 지운 그림**이어야 합니다. 배치가 흔들리면 보충할 때 상품이 순간이동하는 것처럼 보입니다.

### 4.2 오염 (`DirtSpot`)

| # | 파일 | 캔버스 | 월드 크기 | sortingOrder |
| --- | --- | --- | --- | --- |
| O11 | `Dirt_A.png` (음료 쏟음) | `160 × 140` | `1.3 × 1.1` | `2` (바닥 위, 캐릭터 아래) |
| O12 | `Dirt_B.png` (봉지·쓰레기) | 〃 | 〃 | `2` |
| O13 | `Dirt_C.png` (발자국 얼룩) | 〃 | 〃 | `2` |

스폰 시 3종 중 랜덤. **청소 진행도에 따라 알파를 `1 → 0.25`로 낮춥니다** (별도 프레임 제작 없이 진행 상황을 보여주는 가장 싼 방법).

**월드 신규 물량: W1 + O1~O13 = 18장** (O6~O10은 각 2장이므로 5 슬롯 = 10장)

---

## 5. 캐릭터 스프라이트

### 5.1 시트 규격 (요미 실측 계승 — 절대 변경 금지)

`YomiWalkSheet.png` 실측: **720 × 1200**, 3열 × 4행, 셀 **240 × 300**.

```
        frame0    frame1    frame2
row0  │  아래(정면) 걷기 3프레임      │  ← direction 0
row1  │  왼쪽 걷기                   │  ← direction 1
row2  │  오른쪽 걷기                 │  ← direction 2
row3  │  위(뒷모습) 걷기             │  ← direction 3
```

| 항목 | 값 | 근거 |
| --- | --- | --- |
| 시트 크기 | `720 × 1200` | 코드가 `width/3`, `height/4`로 자름 |
| 피벗 | `(0.5, 0.42)` | 발밑이 아니라 살짝 위 — 탑다운 깊이감 |
| PPU | `220` | 하드코딩 |
| 재생 순서 | `0 → 1 → 2 → 1` | `YomiTopDownWalkAnimator.walkOrder` |
| 재생 속도 | `7.5 fps` 상당 | `animationTime += dt * 7.5f` |
| 정지 프레임 | `frame1` (가운데) | 정지 시 항상 이 칸이 나오므로 **frame1이 서 있는 포즈**여야 합니다 |

> ⚠️ `frame1`이 걷는 중간 자세면 요미가 서 있을 때 다리를 벌리고 얼어붙습니다. **frame1 = 기본 스탠딩, frame0/frame2 = 좌우 발 내딛기**로 그리십시오.

### 5.2 신규 캐릭터

| # | 파일 | 경로 | 규격 | 디자인 |
| --- | --- | --- | --- | --- |
| C1 | `CustomerWalkSheet_A.png` | `Character/` | 요미 시트와 **완전 동일** | 직장인 (코트, 서류가방). 차분한 남색 |
| C2 | `CustomerWalkSheet_B.png` | 〃 | 〃 | 학생 (후드, 백팩). 채도 높은 초록 |
| C3 | `CustomerWalkSheet_C.png` | 〃 | 〃 | 노인 (카디건, 장바구니). 따뜻한 갈색 |

**요미 워크시트는 재사용합니다** (초안 명시). 손님 3종은 스폰 시 랜덤 선택하며, 같은 시트에 **색조(HSV Hue) ±0.1 랜덤 틴트**를 적용해 겉보기 종류를 늘립니다 — 시트를 더 그리지 않고 다양성을 얻는 방법입니다.

> 손님은 요미보다 **머리 하나만큼 작거나 크게** 그려 실루엣으로 구분되게 하십시오. 같은 크기·같은 실루엣이면 혼잡할 때 요미를 놓칩니다.

**캐릭터 신규 물량: 3장**

---

## 6. UI 스프라이트

### 6.1 9-slice 프레임

기존 요미의 방 UI와 같은 `border` 관례를 씁니다 (패널 32 / 버튼 24 / 입력 28 / 작은 프레임 25).

| # | 파일 | 경로 | 캔버스 | border | 용도 |
| --- | --- | --- | --- | --- | --- |
| U1 | `HUDBar.png` | `UI/` | `600 × 96` | `28,28,28,28` | 상단 HUD 배경 (반투명 `Navy` 85%) |
| U2 | `ProgressFrame.png` | `UI/` | `240 × 44` | `18,18,18,18` | 홀드 진행 바 외곽 |
| U3 | `ProgressFill.png` | `UI/` | `8 × 8` | — | 단색 채움 (틴트로 색 변경, 1장 공용) |

**진행 바 / 인내 게이지는 U2+U3 한 세트를 색만 바꿔 돌려씁니다.** 게이지마다 프레임을 그리지 않습니다.

| 게이지 | Fill 색 | 조건 |
| --- | --- | --- |
| 홀드 진행 (진행 중) | `Cyan` | `E` 누르는 중 |
| 홀드 진행 (중단됨) | `#5C6878` 회색 | 값이 남아 있으나 손을 뗌 — **재개 가능 신호** |
| 손님 인내 | `#4ADE80` → `#FACC15` → `Warn` | 잔여 60% / 30% / 15% 경계에서 전환 |
| 근무 남은 시간 | `Pink` | 잔여 15% 미만이면 `Warn` + 1Hz 점멸 |

### 6.2 등급 배지

| # | 파일 | 캔버스 | 색 |
| --- | --- | --- | --- |
| U4 | `Grade_S.png` | `160 × 160` | 금색 `#FFD35C` + 발광 테두리 |
| U5 | `Grade_A.png` | 〃 | `Cyan` |
| U6 | `Grade_B.png` | 〃 | `#4ADE80` |
| U7 | `Grade_C.png` | 〃 | `#94A3B8` |
| U8 | `Grade_D.png` | 〃 | `Warn` |

원형 도장 형태, 안에 알파벳 1자. 결과 패널 중앙 상단에 1장만 뜹니다.

### 6.3 아이콘

| # | 파일 | 캔버스 | 용도 |
| --- | --- | --- | --- |
| U9 | `Icon_Box.png` | `128 × 128` | 요미 머리 위 — 재고 상자 소지 중 |
| U10 | `Icon_Mop.png` | 〃 | 요미 머리 위 — 청소도구 소지 중 |
| U11 | `Icon_Money.png` | 〃 | HUD 현재 일급 |
| U12 | `Icon_Customer.png` | 〃 | HUD 처리/이탈 손님 수 |
| U13 | `Icon_Gift.png` | 〃 | 선물 획득 팝업 |
| U14 | `KeyCap_E.png` | `96 × 96` | `E` 키 프롬프트. 홀드 중에는 **눌린 상태로 2px 내려간 변형**을 같은 파일에 담지 말고 코드에서 오프셋 처리 |

**UI 신규 물량: 14장**

---

## 7. UI 배치 명세

캔버스는 **Screen Space - Overlay**, 기준 해상도 `1920 × 1080`, `ScreenMatchMode.MatchWidthOrHeight = 0.5`. 요미의 방과 동일합니다.

> ⚠️ **카메라 주의**: 요미의 방 빌더는 `camera.rect = Rect(0, 0, 0.5f, 1f)`로 화면 좌측 절반만 씁니다 (우측은 채팅 패널). 편의점에는 채팅 패널이 없으므로 빌더에서 **`rect`를 `(0,0,1,1)`로 되돌리고** `orthographicSize`를 `5.4`로 설정해야 합니다. 이걸 빠뜨리면 화면 절반이 검게 남습니다.

| 요소 | 앵커 | 크기 | 구성 |
| --- | --- | --- | --- |
| **상단 HUD** | Top-Center | `1100 × 96` | U1 배경 위에 3구획 |
| ├ 남은 시간 | HUD 중앙 | — | `MM:SS` 28pt + U2/U3 게이지 (폭 320) |
| ├ 현재 일급 | HUD 우측 | — | U11 + 숫자 32pt. 기본급에서 시작해 **내려가기만 합니다.** 감점 시 `Warn`으로 0.4초 점멸 + `-60` 낙하 텍스트 |
| └ 손님 카운터 | HUD 좌측 | — | U12 + `처리 7 / 이탈 2`. 이탈 수치만 `Warn` |
| **홀드 진행 바** | 월드 추적 | `240 × 44` | 요미 머리 위 `+1.1` 오프셋. `WorldToScreenPoint`로 매 프레임 갱신 |
| **소지품 아이콘** | 월드 추적 | `64 × 64` | 진행 바 우측에 붙임. 없으면 숨김 |
| **`E` 프롬프트** | 월드 추적 | `72 × 72` | 대상 스테이션 위. **상호작용 가능할 때만 표시**, 홀드 중에는 아래로 4px |
| **손님 인내 게이지** | 월드 추적 | `140 × 26` | 손님 머리 위 `+1.0`. `WaitingAtCounter`에서만 |
| **매대 재고** | 월드 추적 | `90 × 30` | 매대 위. `5/8` 텍스트. 0이면 `Warn` + 배경 없음 |
| **피드백 토스트** | Bottom-Center `y +140` | 자동 | 2.5초 후 소멸. 요미의 방 `ShowFeedback`과 동일 규격 |
| **결과 패널** | Center | `900 × 620` | §7.1 |
| **중도 포기 모달** | Center | `640 × 300` | 재사용: `Modal/PanelFrame` + `ButtonCancel` |

### 7.1 결과 패널 구성

```
┌──────────────── 근무 종료 ────────────────┐
│                                          │
│               ╭───────╮                  │
│               │   A   │  ← U4~U8 배지     │
│               ╰───────╯                  │
│                                          │
│   기본급                          400 원  │
│   ──────────────────────────────────     │
│   계산 완료      14 명              0     │  ← 감점 없음 (회색)
│   손님 이탈       2 명          - 120  ←Warn
│   남은 오염       1 곳          -  25  ←Warn
│   빈 매대         0 곳             0     │
│   ══════════════════════════════════     │
│   오늘의 일급                    255 원   │  ← 400에서 깎여 내려가는 0.6초
│                                          │
│   🎁 받은 선물   에너지 드링크 × 1        │  ← U13. 없으면 행 자체 숨김
│                                          │
│              [ 확 인 ]                   │
└──────────────────────────────────────────┘
```

- 프레임·버튼은 **기존 `DatingSim/YomiRoom/UI/Modal/` 에셋 재사용** (신규 제작 없음).
- **기본급이 맨 위, 감점이 그 아래, 결과가 맨 밑**입니다. 요미는 시급제라 이 패널은 "얼마를 벌었나"가 아니라 **"얼마를 깎였나"**를 읽는 표입니다.
- 항목은 위에서부터 **0.12초 간격으로 순차 등장**. 전부 동시에 뜨면 감점 항목이 눈에 안 들어옵니다.
- 선물 행은 획득이 있을 때만 표시하고, **등장 시 0.3초 스케일 팝**으로 강조합니다 (5% 확률 보상이므로 사건처럼 보여야 합니다).

---

## 8. 연출 규정

| 상황 | 연출 | 길이 |
| --- | --- | --- |
| 홀드 시작 | 진행 바 페이드 인 | `0.12s` |
| 홀드 중단 | Fill이 `Cyan` → 회색으로 전환, 바는 **유지** | `0.2s` |
| 홀드 완료 | 바가 `Cyan`으로 1회 플래시 후 소멸 | `0.25s` |
| 계산 성공 | 계산대 위로 `Cyan` 체크 표시가 떠오르며 소멸. **금액은 띄우지 않습니다** — 요미는 시급제라 계산 성공으로 일급이 오르지 않습니다 | `0.7s` |
| 손님 이탈 | 손님이 `Warn`으로 1회 점멸 → 잔여물 드롭 → 퇴장. **진행 중이던 계산 바는 빨강 점멸 후 소멸** | `0.5s` |
| 오염 발생 | 스폰 위치에 원형 파문 1회 | `0.4s` |
| 선물 획득 | 손님 위에 U13이 튀어오른 뒤 HUD 방향으로 날아감 | `0.8s` |
| 근무 종료 15초 전 | 남은 시간 게이지 `Warn` 점멸 시작 | 1Hz |

**소리는 이 문서 범위 밖입니다.** `Assets/Resources/Audio/SFX/UI` 기존 클립을 재사용하고, 부족하면 별도 발주하십시오.

---

## 9. AI 생성 프롬프트

P2_07과 동일하게 **[A 공통] + [B 대상별]** 조합으로 씁니다. 요미 감정 96종의 캐주얼 일러스트 프롬프트와 **섞어 쓰지 마십시오** — 계열이 다릅니다.

### 9.1 [A] 공통 블록 — 월드/오브젝트 전부에 삽입

```
top-down 3/4 view game asset, pixel art, 16-bit SNES JRPG style,
limited cool palette: deep navy #07101F, slate blue #1D2B3B, cool gray-blue #5C6878,
fluorescent off-white #EEF6E8, cyan accent #22D3EE,
crisp hard pixel edges, no anti-aliasing, no gradients, dithering for shading only,
clean silhouette readable at small size, transparent background, centered, even margins,
single object, no text, no logo, no watermark
```

### 9.2 [B] 대상별 블록

| 대상 | 프롬프트 추가분 |
| --- | --- |
| **W1 배경** | `convenience store interior floor and back wall, glossy tiled floor with fluorescent light reflection streaks, ceiling fluorescent light strips, automatic glass sliding door at bottom showing dark night street outside, storage room doorway at top left, empty floor space in the middle, no furniture, no shelves` — *배경만은 `transparent background` 대신 `full frame illustration`으로 교체* |
| **O1 매대** | `empty three-tier retail shelf unit, cool gray-blue metal frame, seen from front-top angle, no products on it` |
| **O2 계산대** | `convenience store checkout counter, POS terminal and small monitor, plastic bag holder, cool gray-blue` |
| **O3 창고 선반** | `stockroom metal rack with stacked cardboard boxes, cool gray-blue` |
| **O4 도구함** | `narrow janitor cabinet, mop handle sticking out of the top, cool gray-blue` |
| **O5 잔여물** | `small pile of abandoned grocery items on a counter, slightly messy, faint red outline glow` |
| **O6~O10 상품** | `row of <음료 캔과 페트병 / 과자 봉지 / 편의점 도시락 / 생활용품 / 아이스크림 냉동고 상품>, neatly arranged on shelf tiers, product layer only, transparent where shelf would be` |
| **O11~O13 오염** | `<spilled drink puddle / crumpled trash and wrapper / smudged footprint stain> on floor, top-down, subtle dark red tint, flat` |
| **C1~C3 손님** | 9.3 참조 |
| **U1~U3 프레임** | `UI panel frame, dark navy translucent, thin cyan border, subtle inner shadow, 9-slice friendly with uniform corners` |
| **U4~U8 배지** | `circular rank stamp badge with a single large letter <S/A/B/C/D>, <금색/시안/초록/회색/빨강> metallic ring, flat` |
| **U9~U14 아이콘** | `simple flat icon of <cardboard box / mop / coin / person silhouette / gift box / keyboard key labeled E>, single color with cyan accent, thick readable strokes, 128px icon` |

### 9.3 캐릭터 워크시트 프롬프트

```
[A 공통]
+ character walk cycle sprite sheet, 3 columns x 4 rows grid,
row order top to bottom: facing down, facing left, facing right, facing up,
each row is 3 frames: left step, IDLE STANDING POSE, right step,
middle column must be a neutral standing pose,
same character height and pixel scale in every cell, feet aligned on the same baseline,
character: <직장인/학생/노인 설명>,
plain single-color background per cell, no grid lines, no numbers
```

> **프롬프트만으로 3×4 격자와 셀 정렬은 절대 안 나옵니다.** 4방향 × 3프레임을 **개별 생성한 뒤 스크립트로 720×1200에 합성**하십시오 (P2_07 7절과 같은 사유). 합성 시 셀 크기 `240 × 300`, 발 기준선을 셀 하단에서 위로 `36px` 지점에 고정합니다 — 피벗 `0.42`와 맞는 값입니다.

---

## 10. 재사용 목록 (신규 제작 금지)

| 에셋 | 경로 | 용도 |
| --- | --- | --- |
| 요미 워크시트 | `DatingSim/YomiRoom/Morning/Character/YomiWalkSheet` | 플레이어 캐릭터 그대로 |
| 모달 프레임 | `DatingSim/YomiRoom/UI/Modal/PanelFrame` | 결과 패널·포기 확인 |
| 버튼 3종 | `DatingSim/YomiRoom/UI/Modal/ButtonPrimary` / `ButtonSecondary` / `ButtonCancel` | 확인·취소 |
| 딤 배경 | `DatingSim/YomiRoom/UI/Modal/Dim` | 모달 뒤 암전 |
| 설정 기어 | `DatingSim/YomiRoom/UI/SettingsGear` | 우상단 설정 |
| 문 | `DatingSim/YomiRoom/Morning/Objects/Door` | 창고 입구 (편의점 자동문은 W1에 포함) |
| 상태 아이콘 4종 | `DatingSim/YomiRoom/UI/StatusIcons/*` | 근무 진입 전 슬롯·체력 표기 |

---

## 11. 검수 체크리스트

**아트 납품 시**

- [ ] 전 파일 `Assets/Resources/DatingSim/Store/` 하위, 경로·파일명이 §3~§6 표와 **철자까지 일치** (오타 = 런타임 경고 + 그림 없음)
- [ ] 워크시트 3종 전부 **720 × 1200**, 셀 240 × 300, **가운데 열이 서 있는 자세**
- [ ] 워크시트 4행의 발 기준선이 전부 같은 높이 (요미 시트와 겹쳐 확인)
- [ ] 오브젝트 전부 투명 배경, 반투명 픽셀 없음 (알파는 0 또는 255)
- [ ] `_Half`가 `_Full`에서 물건만 뺀 것 — 남은 물건의 위치가 동일
- [ ] 팔레트 §1.2 밖의 색이 20% 이상 차지하는 파일 없음
- [ ] 안티에일리어싱된 가장자리 없음 (Point 필터에서 지저분해짐)
- [ ] 등급 배지 5종의 알파벳 크기·위치 동일

**구현 확인 시**

- [ ] 카메라 `rect`가 `(0,0,1,1)`로 복원됨 (§7 주의)
- [ ] 요미와 손님이 나란히 섰을 때 키 비율이 어색하지 않음
- [ ] 홀드 중단 시 회색 바가 **남아 있음** (재개 가능 신호)
- [ ] 손님 4명이 줄 섰을 때 인내 게이지가 서로 겹치지 않음
- [ ] **`Tools/Prebake All Scripts Text into Font` 재실행 완료** — 신규 한국어 문자열이 □로 나오지 않는지 결과 패널에서 확인
- [ ] 결과 패널의 감점 항목이 `Warn` 색으로만, 그 외에는 `Text` 색으로 나옴

---

## 12. 의도적으로 제외한 것

| 제외 | 사유 | 추가할 시점 |
| --- | --- | --- |
| 매대 5종 × 3단계 개별 제작 (15장) | 본체 공용 + 오버레이 10장으로 동일한 정보량 | 매대별 형태가 실제로 달라져야 할 때 |
| 손님 4종 이상 | 3종 + 색조 랜덤으로 체감 다양성 확보 | 손님이 개별 캐릭터로 승격되면 |
| 손님 감정 표현 / 말풍선 | 인내 게이지가 같은 정보를 더 빨리 전달 | 손님별 특수 요구가 생기면 |
| 오염 청소 단계별 프레임 | 알파 감소로 대체 | 청소가 핵심 재미가 되면 |
| 인내 게이지 전용 프레임 | 홀드 진행 바를 색만 바꿔 재사용 | — |
| 요미 액션 전용 포즈(계산·청소 모션) | 걷기 시트 + 진행 바로 충분히 읽힘 | M1~M2 플레이 확인 후 재검토 |
| 편의점 낮/밤 배경 분리 | 알바는 시간대 개념이 없음 (슬롯만 소모) | 시간대별 손님 구성이 생기면 |
| 사운드 | 이 문서 범위 밖 | 별도 발주 |

> 요미의 **액션 전용 포즈**는 제외 목록에서 가장 되돌아오기 쉬운 항목입니다. 걷기 시트만으로 "지금 계산 중"이 안 읽히면 4방향 아이들 1프레임씩(4장)만 추가하는 것이 최소 대응입니다.
