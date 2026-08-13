# P2_07 미연시 전용 24종 감정 테이블 구현 계획

## 1. 목적

트레이딩 파트의 기존 `TraderEmotion`과 별개로, 미연시 대화와 연출에서 사용할 24종 감정 데이터 및 스프라이트 테이블을 구축합니다.

이 문서는 감정 종류와 리소스 구조, 대화 데이터 연결, 저장 및 구현 순서만 정의합니다. 감정 판정 규칙이나 신규 관계 수치는 포함하지 않습니다.

## 2. 핵심 설계 원칙

1. `DatingEmotion`은 미연시 파트에서만 사용합니다.
2. 기존 트레이딩용 `TraderEmotion`은 수정하지 않습니다.
3. 대화·스토리 데이터가 장면에 사용할 감정을 직접 지정합니다.
4. 감정별 이름과 스프라이트는 `ScriptableObject` 테이블에서 관리합니다.
5. 현재 감정은 저장 데이터에 기록하여 씬 이동과 저장·불러오기 이후에도 유지합니다.
6. 감정 이미지가 누락된 경우 기본 감정인 평온 이미지를 표시합니다.

## 3. 미연시 전용 감정 24종

### 3.1 기본 감정 12종

| ID | 코드명 | 한국어 | 표정·대사 방향 |
|---:|---|---|---|
| 0 | `Calm` | 평온 | 편안한 표정과 안정적인 말투 |
| 1 | `Joy` | 기쁨 | 밝은 미소와 적극적인 반응 |
| 2 | `Sadness` | 슬픔 | 처진 표정과 짧아진 대사 |
| 3 | `Anger` | 분노 | 강한 눈빛과 공격적인 말투 |
| 4 | `Fear` | 공포 | 움츠린 자세와 불안정한 말투 |
| 5 | `Surprise` | 놀람 | 눈을 크게 뜨고 즉각 반응 |
| 6 | `Flustered` | 당황 | 말더듬과 시선 회피 |
| 7 | `Anxiety` | 불안 | 초조한 표정과 조심스러운 말투 |
| 8 | `Relief` | 안도 | 긴장이 풀린 표정과 부드러운 말투 |
| 9 | `Disappointment` | 실망 | 힘 빠진 표정과 거리감 있는 말투 |
| 10 | `Fatigue` | 피로 | 반쯤 감긴 눈과 느린 말투 |
| 11 | `Curiosity` | 호기심 | 집중하는 표정과 질문하는 말투 |

### 3.2 연애 감정 12종

| ID | 코드명 | 한국어 | 표정·대사 방향 |
|---:|---|---|---|
| 12 | `Interest` | 관심 | 상대를 관찰하는 표정과 적극적인 질문 |
| 13 | `Fondness` | 호감 | 부드러운 미소와 친근한 말투 |
| 14 | `Excitement` | 설렘 | 홍조와 기대감 있는 반응 |
| 15 | `Affection` | 애정 | 다정한 표정과 돌보는 말투 |
| 16 | `Love` | 사랑 | 진지한 표정과 깊은 애정 표현 |
| 17 | `Trust` | 신뢰 | 경계가 풀린 표정과 솔직한 말투 |
| 18 | `Shyness` | 부끄러움 | 홍조, 시선 회피, 작은 목소리 |
| 19 | `Jealousy` | 질투 | 삐친 표정과 날카로운 말투 |
| 20 | `Hurt` | 서운함 | 섭섭한 표정과 소극적인 반응 |
| 21 | `Loneliness` | 외로움 | 위축된 표정과 관심을 바라는 말투 |
| 22 | `Doubt` | 의심 | 경계하는 표정과 캐묻는 말투 |
| 23 | `Obsession` | 집착 | 강한 시선과 독점적인 표현 |

## 4. 데이터 구조

```csharp
public enum DatingEmotion
{
    Calm, Joy, Sadness, Anger, Fear, Surprise,
    Flustered, Anxiety, Relief, Disappointment, Fatigue, Curiosity,
    Interest, Fondness, Excitement, Affection, Love, Trust,
    Shyness, Jealousy, Hurt, Loneliness, Doubt, Obsession
}
```

감정별 설정은 코드의 거대한 분기문 대신 `ScriptableObject` 테이블로 작성합니다.

```csharp
[CreateAssetMenu(menuName = "FX Overdose/Dating/Emotion Definition")]
public class DatingEmotionDefinition : ScriptableObject
{
    public DatingEmotion emotion;
    public string displayName;
    public Sprite defaultSprite;
    public Color uiAccent;
}
```

감정 통합 테이블은 24개 정의를 배열로 보관하고 감정 enum을 키로 조회합니다.

```csharp
[CreateAssetMenu(menuName = "FX Overdose/Dating/Emotion Table")]
public class DatingEmotionTableSO : ScriptableObject
{
    public DatingEmotionDefinition[] definitions;
}
```

## 5. 권장 에셋 위치

```text
Assets/Resources/DatingSim/Emotions/
├── DatingEmotionTable.asset
├── Definitions/
│   ├── Calm.asset
│   ├── Joy.asset
│   └── ...
└── Sprites/
    ├── Calm.png
    ├── Joy.png
    └── ...
```

## 6. 대화 및 스토리 데이터 연결

미연시 대화 노드와 이벤트 데이터에 표시할 감정을 직접 지정합니다.

```csharp
[Serializable]
public class DatingDialogueNode
{
    public string id;
    public string dialogue;
    public DatingEmotion emotion = DatingEmotion.Calm;
}
```

대화 노드가 열리면 지정된 감정을 캐릭터 UI에 전달하고, 해당 감정의 스프라이트와 표시 설정을 테이블에서 불러옵니다.

감정을 지정하지 않은 기존 대화 데이터는 기본값인 `Calm`을 사용합니다.

## 7. 기존 시스템과의 분리

| 영역 | 사용하는 감정 시스템 |
|---|---|
| 트레이딩 화면과 수익률 반응 | 기존 `TraderEmotion` |
| 미연시 대화와 데이트 장면 | 신규 `DatingEmotion` |
| 트레이딩 감정 스프라이트 | 기존 `Characters/Emotions` |
| 미연시 감정 스프라이트 | 신규 `DatingSim/Emotions/Sprites` |

두 enum은 이름이 비슷하더라도 서로 변환하거나 공유하지 않습니다. 각 파트의 대화 및 화면이 자신의 감정 테이블만 참조하도록 구성합니다.

## 8. 저장 데이터 확장

현재 표시 중인 미연시 감정만 `SaveData`에 추가합니다.

```csharp
public DatingEmotion DatingCurrentEmotion = DatingEmotion.Calm;
```

저장 시 현재 감정을 기록하고, 불러오기 시 미연시 캐릭터 UI에 해당 감정 스프라이트를 다시 적용합니다.

기존 세이브에는 필드가 없으므로 enum 기본값인 `Calm`으로 안전하게 불러옵니다.

## 9. 구현 파일 계획

```text
Assets/Scripts/DatingSim/Emotion/
├── DatingEmotion.cs
├── DatingEmotionDefinition.cs
├── DatingEmotionTableSO.cs
└── DatingEmotionController.cs

Assets/Resources/DatingSim/Emotions/
├── DatingEmotionTable.asset
├── Definitions/
└── Sprites/
```

주요 책임:

- `DatingEmotion.cs`: 미연시 전용 24종 enum
- `DatingEmotionDefinition.cs`: 감정별 표시명, 스프라이트, UI 색상
- `DatingEmotionTableSO.cs`: 24개 감정 정의 조회 및 누락 검증
- `DatingEmotionController.cs`: 현재 감정 보관, 변경 이벤트, UI 스프라이트 적용

## 10. 단계별 구현 계획

### Phase 1 — 감정 데이터 기반 구축

- `DatingEmotion` 24종 enum 추가
- 감정 정의 `ScriptableObject` 추가
- 24개 정의를 담는 통합 테이블 추가
- ID 중복 및 누락 검증 코드 작성

완료 조건: 에디터에서 24종 감정 정의를 모두 조회하고 누락이나 중복을 경고할 수 있어야 합니다.

### Phase 2 — 감정 컨트롤러 구현

- 현재 감정 보관 기능 추가
- 감정 변경 메서드와 변경 이벤트 추가
- 동일 감정을 반복 적용할 때 불필요한 UI 갱신 방지
- 정의나 이미지가 없으면 평온 감정으로 대체

완료 조건: 코드에서 지정한 감정이 안정적으로 유지되고 UI에 한 번만 전달되어야 합니다.

### Phase 3 — 저장·불러오기 연결

- `SaveData`에 현재 미연시 감정 추가
- 감정 컨트롤러의 저장 및 복원 연결
- 기존 세이브 기본값 호환 확인

완료 조건: 슬롯 저장 후 다시 불러와도 마지막 미연시 감정이 복원되어야 합니다.

### Phase 4 — 대화·스토리 연동

- 대화 노드에 감정 필드 추가
- 스토리 이벤트 데이터에 감정 필드 추가
- 대사 출력과 동시에 지정 감정 적용
- 기존 대화 데이터는 평온으로 처리

완료 조건: 작성자가 대화 데이터에 지정한 감정이 해당 대사와 함께 표시되어야 합니다.

### Phase 5 — 스프라이트 및 UI 연동

- 24종 기본 스프라이트 연결
- 감정 변경 이벤트를 미연시 캐릭터 UI가 구독
- 미연시 장면별 캐릭터 이미지에 동일한 감정 테이블 적용
- 디버그 메뉴에서 감정 24종 순환 기능 제공

완료 조건: 24종 감정을 순환하며 코드명, 한국어 이름, 스프라이트를 확인할 수 있어야 합니다.

### Phase 6 — 콘텐츠 적용 및 검수

- 주요 대화와 스토리 노드에 감정 지정
- 모든 노드의 감정값 유효성 검사
- 장면 전환과 저장·불러오기 이후 스프라이트 유지 확인
- 트레이딩 감정 시스템과 서로 간섭하지 않는지 확인

완료 조건: 미연시 콘텐츠에 지정된 감정이 일관되게 출력되고 트레이딩 표정에 영향을 주지 않아야 합니다.

## 11. 테스트 체크리스트

- [ ] 감정 테이블에 정확히 24개가 등록되어 있음
- [ ] 코드명과 테이블 ID가 중복되지 않음
- [ ] 미연시 대화 데이터가 감정을 직접 지정할 수 있음
- [ ] 기존 대화 데이터는 평온으로 표시됨
- [ ] 동일 감정의 반복 적용으로 이미지가 깜빡이지 않음
- [ ] 저장 후 현재 감정이 복원됨
- [ ] 기존 세이브는 평온 감정으로 안전하게 시작함
- [ ] 누락된 감정 정의나 스프라이트가 평온 이미지로 대체됨
- [ ] 디버그 메뉴에서 24종을 모두 확인할 수 있음
- [ ] 기존 트레이딩 감정 시스템에 영향이 없음

## 12. 구현 전 확정할 사항

1. 24종 모두 개별 스프라이트를 제작할지, 초기에는 일부 이미지를 공유할지 결정
2. 감정별 UI 강조 색상을 별도로 사용할지 결정
3. 현재 감정을 세이브에 유지할지, 씬 진입 시 항상 평온으로 초기화할지 최종 결정
4. 기존 대화 데이터에 감정을 일괄 지정하는 에디터 도구가 필요한지 결정
