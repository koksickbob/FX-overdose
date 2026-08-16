# 요미 감정 스프라이트 — 작업 원본

여기 있는 파일은 **게임이 쓰지 않습니다.** 크로마키 배경 버전(`*_chroma`)과 리테이크 잔재(`*_redesign_v*`, `*_preview`, `*_source`)입니다.

`Assets/` 밖에 두는 이유는 하나입니다 — 런타임 파일이 `Assets/Resources/`에 있는데, **Resources 폴더는 참조 여부와 무관하게 전부 빌드에 실립니다.** 옆에 두면 쓰지도 않을 105장이 그대로 빌드 용량이 됩니다. Unity는 `Assets/` 밖을 임포트하지 않으므로 여기 두면 에디터 임포트 시간에서도 빠집니다.

**런타임 파일은 `Assets/Resources/DatingSim/Emotions/Sprites/{T1~T4}/{감정}.png` 96장** (24종 × 호감도 4구간). 파일명이 곧 `EventEmotion` enum 이름이고, [EventView.ApplyEmotion](../../../Assets/Scripts/Events/Story/EventView.cs)이 그 이름으로 직접 로드합니다. **이름을 바꾸면 그 감정이 화면에서 사라집니다.**

리테이크한 그림을 게임에 넣으려면 접미사를 떼고 `Assets/Resources/.../{티어}/{감정}.png`로 덮어쓰십시오. 여기 파일을 그 폴더로 옮기는 것이 아닙니다.

검증: 에디터 메뉴 `FXOverdose/Debug/Validate Event Data` — 96장이 다 로드되는지 확인합니다.
