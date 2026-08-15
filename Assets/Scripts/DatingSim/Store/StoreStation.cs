using UnityEngine;

namespace FXOverdose.DatingSim.Store
{
    public enum StoreStationKind
    {
        Checkout,      // 계산대
        Shelf,         // 매대 보충
        PickupStock,   // 창고: 재고 상자 챙기기
        PickupTool,    // 창고: 청소도구 챙기기
        Clean,         // 오염 청소
        CleanupLeftover // 이탈 손님이 두고 간 물건 정리
    }

    /// <summary>요미가 들고 있는 것. 동시 소지는 없습니다.</summary>
    public enum StoreCarry
    {
        None,
        Stock,
        Tool
    }

    /// <summary>
    /// 홀드 액션 하나의 대상. 진행도의 주인은 이 컴포넌트 자신입니다.
    ///
    /// 전역 진행도 레지스트리를 두지 않는 것이 핵심입니다. 값이 오브젝트에 붙어 있으므로
    /// "떠났다가 돌아와서 이어하기"(초안 요구사항)가 별도 코드 없이 성립합니다.
    /// </summary>
    public sealed class StoreStation : MonoBehaviour
    {
        public StoreStationKind Kind { get; private set; }
        public string DisplayName { get; private set; }

        /// <summary>매대일 때만 유효한 테이블 인덱스입니다.</summary>
        public int ShelfIndex { get; private set; } = -1;

        public int Stock { get; private set; }
        public int MaxStock { get; private set; }

        /// <summary>0~1. E를 떼면 이 값에서 그대로 멈춥니다(감쇠 없음).</summary>
        public float Progress { get; private set; }

        public float RequiredSeconds { get; set; } = 1f;

        /// <summary>이번 프레임에 진행 중인지. UI가 회색/시안을 가르는 기준입니다.</summary>
        public bool IsRunning { get; private set; }

        private SpriteRenderer stockOverlay;
        private Sprite stockFullSprite;
        private Sprite stockHalfSprite;

        public void Configure(StoreStationKind kind, string displayName, float requiredSeconds, int shelfIndex = -1)
        {
            Kind = kind;
            DisplayName = displayName;
            RequiredSeconds = Mathf.Max(0.01f, requiredSeconds);
            ShelfIndex = shelfIndex;
        }

        public void ConfigureShelfStock(int initial, int max, SpriteRenderer overlay,
            Sprite fullSprite = null, Sprite halfSprite = null)
        {
            MaxStock = Mathf.Max(1, max);
            Stock = Mathf.Clamp(initial, 0, MaxStock);
            stockOverlay = overlay;
            stockFullSprite = fullSprite;
            stockHalfSprite = halfSprite;
            RefreshStockVisual();
        }

        public bool IsShelfFull => Kind == StoreStationKind.Shelf && Stock >= MaxStock;
        public bool IsShelfEmpty => Kind == StoreStationKind.Shelf && Stock <= 0;

        /// <summary>손님이 물건을 집어 갑니다. 실제로 집은 개수를 돌려줍니다.</summary>
        public int TakeStock(int amount)
        {
            int taken = Mathf.Clamp(amount, 0, Stock);
            Stock -= taken;
            RefreshStockVisual();
            return taken;
        }

        public void AddStock(int amount)
        {
            Stock = Mathf.Clamp(Stock + amount, 0, MaxStock);
            RefreshStockVisual();
        }

        /// <summary>진행도를 dt만큼 올립니다. 1에 도달하면 true를 돌려주고 0으로 되감습니다.</summary>
        public bool Advance(float deltaTime)
        {
            IsRunning = true;
            Progress += deltaTime / RequiredSeconds;
            if (Progress < 1f) return false;

            Progress = 0f;
            return true;
        }

        /// <summary>손을 뗐습니다. 값은 유지하고 표시만 정지 상태로 바꿉니다.</summary>
        public void Pause() => IsRunning = false;

        /// <summary>진행도를 버립니다. 계산 중이던 손님이 바뀌었을 때만 씁니다 — 무료 계산 방지.</summary>
        public void ResetProgress()
        {
            Progress = 0f;
            IsRunning = false;
        }

        /// <summary>청소 진행도를 알파로 비춥니다. 단계별 스프라이트를 그리지 않기 위한 대체 표현입니다.</summary>
        public void ApplyCleanFade()
        {
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            Color color = renderer.color;
            color.a = Mathf.Lerp(1f, 0.25f, Progress);
            renderer.color = color;
        }

        private void RefreshStockVisual()
        {
            if (stockOverlay == null) return;
            // 재고 0이면 오버레이를 끄고 빈 선반이 그대로 보이게 합니다.
            stockOverlay.enabled = Stock > 0;
            if (Stock <= 0) return;

            float ratio = MaxStock > 0 ? (float)Stock / MaxStock : 0f;
            if (stockFullSprite != null && stockHalfSprite != null)
            {
                stockOverlay.sprite = ratio >= 0.6f ? stockFullSprite : stockHalfSprite;
                stockOverlay.color = Color.white;
                return;
            }
            Color color = stockOverlay.color;
            color.a = ratio >= 0.6f ? 1f : 0.55f;
            stockOverlay.color = color;
        }
    }
}
