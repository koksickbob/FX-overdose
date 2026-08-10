# 보스전 실시간 UI 연동 가이드 (UI 제작자용)

이 문서는 새로 추가된 보스전 시스템(3, 6, 9, 12, 15, 18, 20일차 보스)의 실시간 자산/수익률 상태를 UI 스크립트와 연동하는 방법을 안내합니다.

## 핵심 매니저
모든 보스 관련 상태와 이벤트는 `FXOverdose.System.BossManager.Instance` 를 통해 접근할 수 있습니다.

### 1. 현재 보스 정보 가져오기
UI 초기화 시, 오늘 보스가 있는지 확인하고 정보를 표시하려면 아래 코드를 참고하세요.

```csharp
using FXOverdose.System;

void Start()
{
    var bossManager = BossManager.Instance;
    
    // 현재 날짜(GameManager.CurrentDay)에 보스가 있는지 확인
    int currentDay = GameManager.CurrentDay;
    if (bossManager.HasBossToday(currentDay))
    {
        // 보스 데이터 로드
        var boss = bossManager.CurrentBoss;
        
        string bossName = boss.Name;              // 예: "편의점 사장"
        string bossDesc = boss.Description;       // 보스 설명
        float startingAsset = bossManager.BossStartingAsset; // 보스 시작 자본
        float currentAsset = bossManager.BossCurrentAsset;   // 보스 현재 자본
        
        // TODO: UI 텍스트에 초기값 할당
    }
}
```

### 2. 실시간 자산 및 파산 이벤트 바인딩
보스가 백그라운드에서 거래를 진행하여 자산이 변동하거나, 자산이 0이 되어 파산했을 때 UI를 업데이트하기 위해 다음 이벤트를 구독(Subscribe)하세요.

```csharp
using FXOverdose.System;
using UnityEngine;
using UnityEngine.UI;

public class BossBattleUIController : MonoBehaviour
{
    public Text BossNameText;
    public Text BossAssetText;
    public Text BossReturnRateText;
    public GameObject BankruptStampUI; // 파산 시 띄울 도장 이미지 같은 것

    private void OnEnable()
    {
        if (BossManager.Instance != null)
        {
            BossManager.Instance.OnBossAssetChanged += UpdateBossAssetUI;
            BossManager.Instance.OnBossBankrupted += ShowBankruptUI;
        }
    }

    private void OnDisable()
    {
        if (BossManager.Instance != null)
        {
            BossManager.Instance.OnBossAssetChanged -= UpdateBossAssetUI;
            BossManager.Instance.OnBossBankrupted -= ShowBankruptUI;
        }
    }

    // 자산이 변동될 때마다 자동 호출됩니다.
    private void UpdateBossAssetUI(float currentAsset, float startingAsset)
    {
        BossAssetText.text = $"자산: {currentAsset:N0} 원";
        
        float returnRate = 0f;
        if (startingAsset > 0) 
            returnRate = ((currentAsset - startingAsset) / startingAsset) * 100f;
            
        BossReturnRateText.text = $"수익률: {returnRate:F2}%";
    }

    // 보스가 무리한 매매로 파산(자산 0 이하)했을 때 1회 호출됩니다.
    private void ShowBankruptUI()
    {
        if (BankruptStampUI != null)
        {
            BankruptStampUI.SetActive(true);
            // TODO: 폭발 효과나 파산 도장 쾅 찍히는 연출 추가
        }
    }
}
```

### 요약
- **`BossManager.Instance.OnBossAssetChanged`** : 보스의 거래 수익/손실 발생 시 실시간으로 발동합니다. (현재 자산, 시작 자산)을 넘겨주므로 수익률 계산에 용이합니다.
- **`BossManager.Instance.OnBossBankrupted`** : 보스의 자산이 0원이 되어 파산했을 때 발동합니다.
- 이 이벤트들을 활용하여 화면 우측 상단 등에 실시간 중계 전광판 형태의 UI를 구성해 주시면 됩니다.
