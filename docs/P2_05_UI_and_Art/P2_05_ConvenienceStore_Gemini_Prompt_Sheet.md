# P2_05 편의점 스프라이트 — Gemini 전달용 완성 프롬프트 시트

> **작성일**: 2026-08-14
> **용도**: [P2_05_ConvenienceStore_UI_And_Sprite_Spec.md](P2_05_ConvenienceStore_UI_And_Sprite_Spec.md) §3~§6 에셋 35장 제작. **각 코드 블록이 완성 프롬프트다 — 조립·치환 없이 블록 통째로 복사해서 Gemini에 붙여넣는다.**
> **주의**: [P2_07 캐주얼 일러스트 프롬프트](../P2_07_emotion/P2_07_Yomi_Emotion_Sprite_Prompt_Plan.md)와 섞지 말 것 — 이쪽은 탑다운 픽셀아트 계열이다.

---

## 0. 사용 규칙

- **[생성]** 블록: 텍스트만 붙여넣으면 된다.
- **[편집]** 블록: 반드시 **지정된 원본 이미지를 첨부한 상태**에서 텍스트를 붙여넣는다. 편집 체인은 위치·크기 일관성을 프롬프트보다 잘 지키므로 순서를 바꾸지 않는다.
- 각 에셋은 2~3장 후보 생성 → 1장 선별.
- **U3 `ProgressFill.png`는 Gemini에 시키지 않는다** — 8×8 단색 사각형, 에디터 수작업.
- 생성물은 바로 못 쓴다. **후처리 필수**: 크로마키 제거(녹색 `#00FF00`, U6만 마젠타 `#FF00FF`) → 각 블록에 적힌 `목표 크기`로 최근접(nearest) 다운스케일 → 팔레트 양자화 + 알파 0/255 이진화. 검수는 [명세 §11](P2_05_ConvenienceStore_UI_And_Sprite_Spec.md), 임포트 설정은 명세 §2.

---

## 1. 월드 — 배경

### [01] W1 `StoreRoom.png` — [생성] · `Room/` · 목표 1120×900 · 통짜(크로마키 없음)

```
Create a full-frame video game background in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4, landscape aspect ratio close to 5:4.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.

Subject: the interior of a small convenience store at night, floor and back wall only,
completely empty of furniture.
- Glossy tiled floor in slate blue #1D2B3B with subtle off-white #EEF6E8 fluorescent
  light reflection streaks, floor tiles aligned to a strict 32-pixel grid.
- The top sixth of the image is the back wall in dark navy, with a storage room doorway
  at the top left and two rows of ceiling fluorescent light strips along the upper area.
- At the bottom center, an automatic glass sliding door showing a dark navy night street
  silhouette outside.
- Leave the bottom right area of the floor completely empty (a checkout counter sprite
  will be placed there later).
- Darken the outer 8-pixel border of the whole image toward the wall color.
Do not include: furniture, shelves, people, products, text, signs, logos, watermarks.
```

---

## 2. 월드 — 가구·오브젝트

### [02] O1 `Shelf_Body.png` — [생성] · `Objects/` · 목표 320×260

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4 so the floor is seen from above and object fronts
are slightly visible.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: an empty three-tier retail gondola shelf unit for a convenience store,
cool gray-blue #5C6878 metal frame, all three shelf tiers completely empty,
seen from a front-top angle. Aspect ratio slightly wider than tall, about 5:4.
```

### [03] O2 `Counter.png` — [생성] · `Objects/` · 목표 420×300

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4 so the floor is seen from above and object fronts
are slightly visible.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: a convenience store checkout counter, cool gray-blue #5C6878 body,
with a small POS terminal and monitor on top and a plastic bag holder at the side.
Aspect ratio wider than tall, about 7:5.
```

### [04] O3 `StorageRack.png` — [생성] · `Objects/` · 목표 300×320

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4 so the floor is seen from above and object fronts
are slightly visible.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: a stockroom metal rack, cool gray-blue #5C6878 frame, with plain cardboard
boxes stacked on its shelves. Aspect ratio slightly taller than wide, about 15:16.
```

### [05] O4 `ToolCabinet.png` — [생성] · `Objects/` · 목표 220×300

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4 so the floor is seen from above and object fronts
are slightly visible.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: a narrow janitor supply cabinet, cool gray-blue #5C6878, with a mop handle
sticking out of the top. Aspect ratio taller than wide, about 11:15.
```

### [06] O5 `Leftover.png` — [생성] · `Objects/` · 목표 180×120

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4 so the floor is seen from above and object fronts
are slightly visible.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: a small messy pile of abandoned grocery items (a drink can, a snack bag,
a bento box) left on a surface, with a faint thin red #EF4444 outline glow around
the pile. Aspect ratio wider than tall, about 3:2.
```

---

## 3. 상품 오버레이 — `_Full` 5장 생성 → `_Half` 5장은 편집

### [07] O6 `Stock_Drink_Full.png` — [생성] · `Objects/` · 목표 300×200

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4 so the floor is seen from above and object fronts
are slightly visible.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: rows of canned drinks and small plastic beverage bottles neatly arranged
on three invisible shelf tiers, products only, drawn as if sitting on a three-tier
shelf whose frame is not drawn, fully stocked with no gaps, product layer for
overlaying on a separate shelf sprite. Aspect ratio 3:2.
```

### [08] O7 `Stock_Snack_Full.png` — [생성] · `Objects/` · 목표 300×200

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4 so the floor is seen from above and object fronts
are slightly visible.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: rows of colorful snack bags and chip packets neatly arranged on three
invisible shelf tiers, products only, drawn as if sitting on a three-tier shelf
whose frame is not drawn, fully stocked with no gaps, product layer for overlaying
on a separate shelf sprite. Aspect ratio 3:2.
```

### [09] O8 `Stock_Lunch_Full.png` — [생성] · `Objects/` · 목표 300×200

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4 so the floor is seen from above and object fronts
are slightly visible.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: rows of convenience store bento lunch boxes with clear lids neatly arranged
on three invisible shelf tiers, products only, drawn as if sitting on a three-tier
shelf whose frame is not drawn, fully stocked with no gaps, product layer for
overlaying on a separate shelf sprite. Aspect ratio 3:2.
```

### [10] O9 `Stock_Daily_Full.png` — [생성] · `Objects/` · 목표 300×200

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4 so the floor is seen from above and object fronts
are slightly visible.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: rows of daily necessities (tissue boxes, toothbrush packs, small detergent
bottles) neatly arranged on three invisible shelf tiers, products only, drawn as if
sitting on a three-tier shelf whose frame is not drawn, fully stocked with no gaps,
product layer for overlaying on a separate shelf sprite. Aspect ratio 3:2.
```

### [11] O10 `Stock_Ice_Full.png` — [생성] · `Objects/` · 목표 300×200

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4 so the floor is seen from above and object fronts
are slightly visible.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: rows of ice cream bars and frozen dessert cups neatly arranged on three
invisible shelf tiers, products only, drawn as if sitting on a three-tier shelf
whose frame is not drawn, fully stocked with no gaps, product layer for overlaying
on a separate shelf sprite. Aspect ratio 3:2.
```

### [12] O6~O10 `_Half` 5장 — [편집] · 해당 `_Full` 결과물을 첨부하고 5회 반복 · 목표 300×200

> `Stock_Drink_Full` 첨부 → `Stock_Drink_Half`, `Stock_Snack_Full` 첨부 → `Stock_Snack_Half` … 식으로 5회. 프롬프트 텍스트는 5회 모두 동일.

```
Using the attached image, erase roughly half of the products to make the shelves
look half-stocked, leaving natural-looking gaps. Every remaining product must stay
in exactly the same position, same size, same colors, same pixel art style.
Where products are erased, fill with the same flat green background color.
Change absolutely nothing else.
```

---

## 4. 오염 (DirtSpot)

### [13] O11 `Dirt_A.png` — [생성] · `Objects/` · 목표 160×140

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
seen straight from above (pure top-down for a floor decal).
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: a spilled drink puddle on the floor, flat dark stain with a subtle dark red
tint, irregular round shape. Aspect ratio about 8:7.
```

### [14] O12 `Dirt_B.png` — [생성] · `Objects/` · 목표 160×140

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
seen straight from above (pure top-down for a floor decal).
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: crumpled trash, a plastic bag and a snack wrapper scattered in a small
cluster on the floor, flat, with a subtle dark red tint. Aspect ratio about 8:7.
```

### [15] O13 `Dirt_C.png` — [생성] · `Objects/` · 목표 160×140

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
seen straight from above (pure top-down for a floor decal).
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single object, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, people, gradients, blur, photo textures.

Subject: smudged dirty footprint stains on the floor, flat, with a subtle dark red
tint. Aspect ratio about 8:7.
```

---

## 5. UI — 프레임

### [16] U1 `HUDBar.png` — [생성] · `UI/` · 목표 600×96 · 9-slice border `28,28,28,28`

```
Create a single flat game UI element, clean 2D vector-like style with pixel-crisp edges.
Color language: dark navy #07101F base, cyan #22D3EE accent, off-white #E2F5FA details.
Flat shading, no gradients, no glow bleeding, centered with even margins.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: any text, watermarks, drop shadows on the background, extra decorations.

Subject: a horizontal rounded-rectangle UI panel frame, dark navy #07101F fill,
thin cyan #22D3EE border line, subtle darker inner edge, all four corners identical
and uniform so it can be 9-sliced. Aspect ratio very wide, about 25:4.
```

### [17] U2 `ProgressFrame.png` — [생성] · `UI/` · 목표 240×44 · 9-slice border `18,18,18,18`

```
Create a single flat game UI element, clean 2D vector-like style with pixel-crisp edges.
Color language: dark navy #07101F base, cyan #22D3EE accent, off-white #E2F5FA details.
Flat shading, no gradients, no glow bleeding, centered with even margins.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: any text, watermarks, drop shadows on the background, extra decorations.

Subject: a small horizontal progress bar frame, hollow in the middle, dark navy
#07101F rim with a thin cyan #22D3EE border, all four corners identical and uniform
so it can be 9-sliced. Aspect ratio wide, about 11:2.
```

> **U3 `ProgressFill.png`(8×8 단색)는 생성하지 않는다** — 에디터 수작업. 반투명 85%도 코드/임포트 처리이므로 U1은 불투명으로 받는다.

---

## 6. UI — 등급 배지 (U4 생성 → U5~U8 편집)

### [18] U4 `Grade_S.png` — [생성] · `UI/` · 목표 160×160

```
Create a single flat game UI element, clean 2D vector-like style with pixel-crisp edges.
Color language: dark navy #07101F base, cyan #22D3EE accent, off-white #E2F5FA details.
Flat shading, no glow bleeding, centered with even margins.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: any text other than the single letter requested below, watermarks,
drop shadows on the background, extra decorations.

Subject: a circular rank stamp badge, square aspect ratio, flat style,
a thick gold #FFD35C metallic ring with a subtle glowing rim,
and one single large bold capital letter "S" perfectly centered inside, gold colored.
```

### [19] U5 `Grade_A.png` — [편집] · U4 결과물 첨부 · 목표 160×160

```
Using the attached badge image, change only two things:
replace the letter "S" with a single large bold capital letter "A",
keeping the exact same letter size, weight and centered position,
and recolor the ring and letter to cyan #22D3EE. Remove the glow the "S" badge had.
Keep the badge shape, size, position and background exactly the same.
```

### [20] U6 `Grade_B.png` — [편집] · U4 결과물 첨부 · 목표 160×160 · **크로마키는 마젠타**

```
Using the attached badge image, change only three things:
replace the letter "S" with a single large bold capital letter "B",
keeping the exact same letter size, weight and centered position,
recolor the ring and letter to green #4ADE80, and change the flat background color
to magenta #FF00FF. Remove the glow the "S" badge had.
Keep the badge shape, size and position exactly the same.
```

### [21] U7 `Grade_C.png` — [편집] · U4 결과물 첨부 · 목표 160×160

```
Using the attached badge image, change only two things:
replace the letter "S" with a single large bold capital letter "C",
keeping the exact same letter size, weight and centered position,
and recolor the ring and letter to gray #94A3B8. Remove the glow the "S" badge had.
Keep the badge shape, size, position and background exactly the same.
```

### [22] U8 `Grade_D.png` — [편집] · U4 결과물 첨부 · 목표 160×160

```
Using the attached badge image, change only two things:
replace the letter "S" with a single large bold capital letter "D",
keeping the exact same letter size, weight and centered position,
and recolor the ring and letter to red #EF4444. Remove the glow the "S" badge had.
Keep the badge shape, size, position and background exactly the same.
```

---

## 7. UI — 아이콘 (전부 정사각)

### [23] U9 `Icon_Box.png` — [생성] · `UI/` · 목표 128×128

```
Create a single flat game UI element, clean 2D vector-like style with pixel-crisp edges.
Color language: dark navy #07101F base, cyan #22D3EE accent, off-white #E2F5FA details.
Flat shading, no gradients, no glow bleeding, centered with even margins.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: any text, watermarks, drop shadows on the background, extra decorations.

Subject: one simple flat game icon of a closed cardboard box, square aspect ratio,
single main color with a cyan #22D3EE accent, thick readable strokes,
bold simplified shape that stays clear at 32 pixels.
```

### [24] U10 `Icon_Mop.png` — [생성] · `UI/` · 목표 128×128

```
Create a single flat game UI element, clean 2D vector-like style with pixel-crisp edges.
Color language: dark navy #07101F base, cyan #22D3EE accent, off-white #E2F5FA details.
Flat shading, no gradients, no glow bleeding, centered with even margins.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: any text, watermarks, drop shadows on the background, extra decorations.

Subject: one simple flat game icon of a mop, square aspect ratio,
single main color with a cyan #22D3EE accent, thick readable strokes,
bold simplified shape that stays clear at 32 pixels.
```

### [25] U11 `Icon_Money.png` — [생성] · `UI/` · 목표 128×128

```
Create a single flat game UI element, clean 2D vector-like style with pixel-crisp edges.
Color language: dark navy #07101F base, cyan #22D3EE accent, off-white #E2F5FA details.
Flat shading, no gradients, no glow bleeding, centered with even margins.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: any text, watermarks, drop shadows on the background, extra decorations.

Subject: one simple flat game icon of a single coin, square aspect ratio,
single main color with a cyan #22D3EE accent, thick readable strokes,
bold simplified shape that stays clear at 32 pixels.
```

### [26] U12 `Icon_Customer.png` — [생성] · `UI/` · 목표 128×128

```
Create a single flat game UI element, clean 2D vector-like style with pixel-crisp edges.
Color language: dark navy #07101F base, cyan #22D3EE accent, off-white #E2F5FA details.
Flat shading, no gradients, no glow bleeding, centered with even margins.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: any text, watermarks, drop shadows on the background, extra decorations.

Subject: one simple flat game icon of a person silhouette from the shoulders up,
square aspect ratio, single main color with a cyan #22D3EE accent, thick readable
strokes, bold simplified shape that stays clear at 32 pixels.
```

### [27] U13 `Icon_Gift.png` — [생성] · `UI/` · 목표 128×128

```
Create a single flat game UI element, clean 2D vector-like style with pixel-crisp edges.
Color language: dark navy #07101F base, cyan #22D3EE accent, off-white #E2F5FA details.
Flat shading, no gradients, no glow bleeding, centered with even margins.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: any text, watermarks, drop shadows on the background, extra decorations.

Subject: one simple flat game icon of a gift box with a ribbon, square aspect ratio,
single main color with a cyan #22D3EE accent, thick readable strokes,
bold simplified shape that stays clear at 32 pixels.
```

### [28] U14 `KeyCap_E.png` — [생성] · `UI/` · 목표 96×96

```
Create a single flat game UI element, clean 2D vector-like style with pixel-crisp edges.
Color language: dark navy #07101F base, cyan #22D3EE accent, off-white #E2F5FA details.
Flat shading, no gradients, no glow bleeding, centered with even margins.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: any text other than the single letter requested below, watermarks,
drop shadows on the background, extra decorations.

Subject: one simple flat game icon of a keyboard keycap with the single capital
letter "E" printed on top, square aspect ratio, dark navy keycap with a cyan #22D3EE
accent edge, thick readable strokes, bold simplified shape that stays clear at 32 pixels.
```

> 눌린 상태 변형은 그리지 않는다 — 코드에서 오프셋 처리 (명세 §6.3).

---

## 8. 손님 워크시트 (C1~C3) — 기준 프레임 생성 → 편집 파생 → 합성

**통짜 3×4 시트를 시키지 않는다** (명세 §9.3 경고). 캐릭터당: 기준 1장 생성 → 방향 3장 편집 → 방향별 걷기 2장씩 편집 = 12프레임. 이후 스크립트로 셀 `240×300`, 발 기준선 셀 하단 36px 고정, `720×1200` 3열×4행 합성. **기준 프레임은 오너 검수 후에 파생을 시작한다.**

### 8.1 기준 프레임 [생성] — 정면 스탠딩 (row0 · frame1)

#### [29] C1 직장인 — `CustomerWalkSheet_A` 기준 프레임

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE,
plus the character's own outfit colors described below.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single character, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, other people, gradients, blur, photo textures.

Subject: one pixel art game character for a top-down JRPG, full body, standing still,
facing the viewer (facing down in top-down terms), neutral standing pose, feet
together, arms relaxed at the sides, proportions of a small sprite character seen
slightly from above, slightly taller than an average sprite.
Character: an office worker in a calm dark navy coat, carrying a briefcase.
The character must fit fully in frame with even margins, feet near the bottom.
Aspect ratio 4:5.
```

#### [30] C2 학생 — `CustomerWalkSheet_B` 기준 프레임

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE,
plus the character's own outfit colors described below.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single character, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, other people, gradients, blur, photo textures.

Subject: one pixel art game character for a top-down JRPG, full body, standing still,
facing the viewer (facing down in top-down terms), neutral standing pose, feet
together, arms relaxed at the sides, proportions of a small sprite character seen
slightly from above.
Character: a student in a saturated green hoodie, wearing a backpack.
The character must fit fully in frame with even margins, feet near the bottom.
Aspect ratio 4:5.
```

#### [31] C3 노인 — `CustomerWalkSheet_C` 기준 프레임

```
Create a single video game sprite asset in 16-bit SNES JRPG pixel art style,
top-down view tilted about 3/4.
Strictly limit the color palette to: deep navy #07101F, slate blue #1D2B3B,
cool gray-blue #5C6878, fluorescent off-white #EEF6E8, cyan accent #22D3EE,
plus the character's own outfit colors described below.
Crisp hard pixel edges, no anti-aliasing, no gradients; use dithering only for shading.
Clean silhouette that stays readable at small size. One single character, centered,
even margins on all sides.
Background: one flat solid green color #00FF00 covering the entire background,
for chroma key removal.
Do not include: text, letters, logos, watermarks, cast shadows on the background,
extra objects, other people, gradients, blur, photo textures.

Subject: one pixel art game character for a top-down JRPG, full body, standing still,
facing the viewer (facing down in top-down terms), neutral standing pose, feet
together, arms relaxed at the sides, proportions of a small sprite character seen
slightly from above, slightly shorter than an average sprite and a little hunched.
Character: an elderly person in a warm brown cardigan, holding a small shopping basket.
The character must fit fully in frame with even margins, feet near the bottom.
Aspect ratio 4:5.
```

### 8.2 방향 파생 [편집] — 캐릭터별 기준 프레임을 첨부하고 3회 (3캐릭터 × 3회 = 9회)

#### [32] row1 — 왼쪽

```
Using the attached pixel art character, redraw the exact same character in the exact
same standing pose, same size, same colors, same style, but now facing left
(left side profile). Keep the feet at the same baseline height. Change nothing else.
```

#### [33] row2 — 오른쪽

```
Using the attached pixel art character, redraw the exact same character in the exact
same standing pose, same size, same colors, same style, but now facing right
(right side profile). Keep the feet at the same baseline height. Change nothing else.
```

#### [34] row3 — 뒷모습

```
Using the attached pixel art character, redraw the exact same character in the exact
same standing pose, same size, same colors, same style, but now facing away from
the viewer (back view, facing up in top-down terms). Keep the feet at the same
baseline height. Change nothing else.
```

### 8.3 걷기 파생 [편집] — 방향별 스탠딩 프레임을 첨부하고 2회 (3캐릭터 × 4방향 × 2회 = 24회)

#### [35] frame0 — 왼발

```
Using the attached standing pixel art character, redraw the exact same character,
same size, same colors, same style, mid-walk with the left foot stepping forward,
a natural small walking stride. Keep the feet baseline and head height the same.
Change nothing else.
```

#### [36] frame2 — 오른발

```
Using the attached standing pixel art character, redraw the exact same character,
same size, same colors, same style, mid-walk with the right foot stepping forward,
a natural small walking stride. Keep the feet baseline and head height the same.
Change nothing else.
```

> frame1(가운데 열) = 8.1~8.2의 스탠딩 프레임 그대로. 명세 §5.1의 "가운데 열 = 서 있는 자세" 조건이 이 순서에서 자동으로 지켜진다.

---

## 9. 발주 순서 요약

| 순서 | 작업 | 블록 | 물량 |
| --- | --- | --- | --- |
| 1 | W1 배경 — 전체 톤 기준. **오너 검수 먼저** | [01] | 생성 1 |
| 2 | 가구·오염 | [02]~[06], [13]~[15] | 생성 8 |
| 3 | 상품 Full → Half | [07]~[11] → [12]×5 | 생성 5 + 편집 5 |
| 4 | UI 프레임·배지·아이콘 | [16]~[28] | 생성 9 + 편집 4 (+U3 수작업) |
| 5 | 손님 기준 3장 → 검수 → 방향 9장 → 걷기 24장 → 합성 | [29]~[31] → [32]~[34] → [35]~[36] | 생성 3 + 편집 33 |

검수: [명세 §11](P2_05_ConvenienceStore_UI_And_Sprite_Spec.md) · 임포트: 명세 §2 · 저장 경로: `Assets/Resources/DatingSim/Store/{Room,Objects,Character,UI}/`
