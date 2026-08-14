using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using FXOverdose.Core;
using FXOverdose.UI;

namespace FXOverdose.DatingSim.Store
{
    public enum StoreShiftState
    {
        Intro,
        Working,
        Settling,
        Result
    }

    /// <summary>근무 집계. 일급과 등급의 유일한 입력입니다.</summary>
    [Serializable]
    public struct StoreShiftTally
    {
        public int TotalCustomers;
        public int CheckedOut;
        public int Walkouts;
        public int RemainingDirt;
        public int EmptyShelves;
        public int RemainingLeftovers;
    }

    public struct StoreShiftResult
    {
        public float BasePay;
        public float Pay;
        public char Grade;
        public StoreShiftTally Tally;
        public List<string> Gifts;
        public bool SaveFailed;
    }

    /// <summary>
    /// 편의점 알바 타이쿤의 근무 1회를 관장합니다. 로직과 데이터만 가지며 UI를 참조하지 않습니다.
    ///
    /// 근무는 원자적입니다 — 중간 상태를 저장하지 않고, 이 씬은 ResumableScenes에도 들어가지 않습니다.
    /// 근무 중 종료는 근무 소실이며 슬롯·체력은 환불되지 않습니다.
    /// </summary>
    public sealed class StoreShiftManager : MonoBehaviour
    {
        public static StoreShiftManager Instance { get; private set; }

        // 월드맵에서 넘겨받는 기본급. WorldMapManager는 씬 스코프라 이 씬에서 조회할 수 없습니다.
        // 직접 씬을 열어 디버깅할 때를 위해 기본값을 둡니다.
        public static float PendingBasePay = 1200f;

        public StoreShiftState State { get; private set; } = StoreShiftState.Intro;
        public float TimeRemaining { get; private set; }
        public float CurrentPay { get; private set; }
        public StoreShiftTally Tally => tally;
        public StoreConfig Config => config;

        /// <summary>플레이어 입력을 받는 상태인지. Intro/Settling/Result에서는 잠깁니다.</summary>
        public bool IsInputAllowed => State == StoreShiftState.Working;

        /// <summary>손님이 움직여도 되는 상태인지. Intro에서는 멈춰 있습니다.</summary>
        public bool IsCustomerTickAllowed => State == StoreShiftState.Working;

        public event Action<StoreShiftState> OnStateChanged;
        public event Action<string> OnFeedback;
        public event Action<float> OnPayChanged;
        public event Action<StoreCustomer> OnCustomerSpawned;
        public event Action<StoreShiftResult> OnShiftFinished;

        private StoreConfig config;
        private StorePlayerController player;
        private StoreStation counter;
        private Transform customerParent;
        private Sprite[] customerFrames;
        private Sprite dirtSprite;
        private Vector2 entrance;
        private Vector2[] queueSlots = Array.Empty<Vector2>();

        private readonly List<StoreStation> shelves = new List<StoreStation>();
        private readonly List<StoreCustomer> queue = new List<StoreCustomer>();
        private readonly List<StoreCustomer> alive = new List<StoreCustomer>();
        private readonly List<StoreStation> dirt = new List<StoreStation>();
        private readonly List<StoreStation> leftovers = new List<StoreStation>();
        private readonly List<string> gifts = new List<string>();

        private StoreShiftTally tally;
        private CustomerTier tier;
        private float introRemaining;
        private float spawnTimer;
        private StoreCustomer lastServed;
        private float basePay;
        private string lastDenyReason;
        private float lastDenyTime = -99f;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Configure(StoreConfig storeConfig, StorePlayerController playerController, StoreStation checkout,
            IEnumerable<StoreStation> shelfStations, Vector2 entrancePoint, Vector2[] slots,
            Transform customersRoot, Sprite[] walkFrames, Sprite dirtVisual)
        {
            config = storeConfig;
            player = playerController;
            counter = checkout;
            entrance = entrancePoint;
            queueSlots = slots;
            customerParent = customersRoot;
            customerFrames = walkFrames;
            dirtSprite = dirtVisual;

            shelves.Clear();
            shelves.AddRange(shelfStations);

            basePay = Mathf.Max(0f, PendingBasePay);
            CurrentPay = basePay;

            int totalShifts = SaveLoadManager.Instance?.CurrentData?.StoreTotalShifts ?? 0;
            tier = config.GetTier(totalShifts);

            introRemaining = config.introSeconds;
            TimeRemaining = config.shiftDurationSeconds;
            spawnTimer = 1.5f; // 첫 손님은 조금 일찍 들여보내 조작을 익히게 합니다.

            ChangeState(StoreShiftState.Intro);
        }

        private void Update()
        {
            if (config == null) return;

            switch (State)
            {
                case StoreShiftState.Intro:
                    introRemaining -= Time.deltaTime;
                    if (introRemaining <= 0f) ChangeState(StoreShiftState.Working);
                    break;

                case StoreShiftState.Working:
                    TickWorking();
                    break;
            }
        }

        private void TickWorking()
        {
            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                Settle();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // 중도 포기. 슬롯·체력은 환불하지 않고 그 시점까지의 성과로 정산합니다.
                OnFeedback?.Invoke("근무를 중단했습니다.");
                Settle();
                return;
            }

            TickSpawn();
            TickQueue();
        }

        private void TickSpawn()
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f) return;

            // ±20% 흔들기. 규칙적인 리듬은 곧 암기가 됩니다.
            spawnTimer = tier.spawnIntervalSeconds * UnityEngine.Random.Range(0.8f, 1.2f);

            if (alive.Count >= tier.maxConcurrent) return;
            // 대기열이 꽉 찼으면 아예 들이지 않습니다. 감당 못 할 벌점이 쌓이는 것을 막습니다. (R3)
            if (queue.Count >= queueSlots.Length) return;
            if (shelves.Count == 0 || customerParent == null) return;

            SpawnCustomer();
        }

        private void SpawnCustomer()
        {
            GameObject go = new GameObject("Customer");
            go.transform.SetParent(customerParent, false);
            go.transform.position = entrance;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 11;
            if (customerFrames != null && customerFrames.Length >= 12)
            {
                renderer.sprite = customerFrames[1];
            }
            else
            {
                renderer.sprite = StoreVisuals.Pixel;
                go.transform.localScale = new Vector3(0.7f, 1.2f, 1f);
            }
            // 손님을 요미와 색으로 구분합니다. 아트 교체 전까지 실루엣이 같기 때문입니다.
            renderer.color = Color.HSVToRGB(UnityEngine.Random.value, 0.45f, 0.95f);

            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;

            StoreCustomer customer = go.AddComponent<StoreCustomer>();
            StoreStation shelf = shelves[UnityEngine.Random.Range(0, shelves.Count)];
            int items = UnityEngine.Random.Range(tier.minItems, tier.maxItems + 1);
            customer.Configure(this, shelf, entrance, tier.patienceSeconds, items, 3.2f);

            if (customerFrames != null && customerFrames.Length >= 12)
            {
                var animator = go.AddComponent<FXOverdose.DatingSim.YomiRoom.YomiTopDownWalkAnimator>();
                animator.Configure(body, renderer, customerFrames, () => customer.MoveInput);
            }

            alive.Add(customer);
            tally.TotalCustomers++;
            OnCustomerSpawned?.Invoke(customer);
        }

        private void TickQueue()
        {
            // 앞사람이 빠졌으면 뒷사람을 당깁니다.
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i] == null) continue;
                queue[i].SetQueueSlot(QueueSlot(i));
            }

            // 계산대 앞 손님이 바뀌면 진행도를 버립니다. 승계하면 무료 계산이 됩니다.
            StoreCustomer front = FrontCustomer();
            if (front != lastServed)
            {
                if (counter != null && counter.Progress > 0f) counter.ResetProgress();
                lastServed = front;
            }
        }

        private Vector2 QueueSlot(int index)
        {
            if (queueSlots.Length == 0) return entrance;
            return queueSlots[Mathf.Clamp(index, 0, queueSlots.Length - 1)];
        }

        private StoreCustomer FrontCustomer()
        {
            for (int i = 0; i < queue.Count; i++)
                if (queue[i] != null && queue[i].IsWaiting) return queue[i];
            return null;
        }

        // ─── 손님 콜백 ────────────────────────────────────────────────

        public Vector2 ReserveQueueSlot(StoreCustomer customer)
        {
            if (!queue.Contains(customer)) queue.Add(customer);
            return QueueSlot(queue.IndexOf(customer));
        }

        public void NotifyBrowsingFinished(StoreCustomer customer, StoreStation shelf)
        {
            if (UnityEngine.Random.value > config.dirtSpawnChance) return;
            SpawnDirt(shelf.transform.position);
        }

        public void NotifyCheckedOut(StoreCustomer customer)
        {
            tally.CheckedOut++;
            queue.Remove(customer);
            RollGift();
        }

        public void NotifyWalkout(StoreCustomer customer)
        {
            tally.Walkouts++;
            queue.Remove(customer);
            ApplyPenalty(config.walkoutPenalty);

            // 재고가 없어 빈손으로 돌아간 손님은 두고 갈 물건이 없습니다.
            if (!customer.LeftEmptyHanded) SpawnLeftover();

            OnFeedback?.Invoke(customer.LeftEmptyHanded
                ? "물건이 없어서 손님이 그냥 갔어요..."
                : "손님이 기다리다 지쳐 가버렸어요!");
        }

        public void ReleaseCustomer(StoreCustomer customer)
        {
            alive.Remove(customer);
            queue.Remove(customer);
            if (lastServed == customer) lastServed = null;
        }

        // ─── 스테이션 실행 ────────────────────────────────────────────

        /// <summary>
        /// 지금 이 스테이션을 진행할 수 있는지. 매 프레임 홀드 직전에 호출되므로
        /// 계산대의 소요 시간(품목 수에 비례)도 여기서 갱신합니다.
        /// </summary>
        public bool CanExecute(StoreStation station, StorePlayerController actor)
        {
            switch (station.Kind)
            {
                case StoreStationKind.Checkout:
                {
                    StoreCustomer front = FrontCustomer();
                    if (front == null) return Deny("계산할 손님이 없어요.");
                    station.RequiredSeconds = config.checkoutBaseSeconds + config.checkoutPerItemSeconds * front.ItemCount;
                    return true;
                }

                case StoreStationKind.Shelf:
                    if (actor.Carry != StoreCarry.Stock || actor.CarriedStockUnits <= 0) return Deny("채울 물건이 없어요.");
                    if (station.IsShelfFull) return Deny("이미 가득 찼어요.");
                    return true;

                case StoreStationKind.Clean:
                    if (actor.Carry != StoreCarry.Tool) return Deny("청소도구가 필요해요.");
                    return true;

                case StoreStationKind.CleanupLeftover:
                    return true;

                default:
                    return true;
            }
        }

        public void ExecuteStation(StoreStation station, StorePlayerController actor)
        {
            switch (station.Kind)
            {
                case StoreStationKind.PickupStock:
                    // 들고 있던 것은 버려집니다. 인벤토리 UI를 만들지 않기 위한 의도적 단순화입니다.
                    actor.SetCarry(StoreCarry.Stock, config.stockUnitsPerPickup);
                    OnFeedback?.Invoke($"재고 상자를 챙겼습니다. ({config.stockUnitsPerPickup}개)");
                    break;

                case StoreStationKind.PickupTool:
                    actor.SetCarry(StoreCarry.Tool, 0);
                    OnFeedback?.Invoke("청소도구를 챙겼습니다.");
                    break;

                case StoreStationKind.Shelf:
                {
                    int shelfIndex = Mathf.Clamp(station.ShelfIndex, 0, Mathf.Max(0, config.shelves.Count - 1));
                    int units = config.shelves.Count > 0 ? config.shelves[shelfIndex].unitsPerRefill : 2;
                    units = Mathf.Min(units, actor.CarriedStockUnits, station.MaxStock - station.Stock);
                    if (units <= 0) return;

                    station.AddStock(units);
                    actor.ConsumeStock(units);
                    break;
                }

                case StoreStationKind.Checkout:
                {
                    StoreCustomer front = FrontCustomer();
                    if (front == null) return;
                    front.CompleteCheckout();
                    break;
                }

                case StoreStationKind.Clean:
                    dirt.Remove(station);
                    Destroy(station.gameObject);
                    actor.RefreshStations();
                    break;

                case StoreStationKind.CleanupLeftover:
                    leftovers.Remove(station);
                    Destroy(station.gameObject);
                    actor.RefreshStations();
                    break;
            }
        }

        // ─── 오염 / 잔여물 ────────────────────────────────────────────

        private void SpawnDirt(Vector2 origin)
        {
            // 기존 스테이션과 겹치면 청소가 불가능해집니다. 3회까지 재추첨하고 실패하면 포기합니다. (R4)
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Vector2 candidate = origin + new Vector2(UnityEngine.Random.Range(-1.6f, 1.6f), UnityEngine.Random.Range(-1.8f, -0.8f));
                if (IsTooCloseToStation(candidate)) continue;

                StoreStation station = CreateStation("Dirt", candidate, dirtSprite, new Color(0.55f, 0.2f, 0.2f, 1f), 2);
                station.Configure(StoreStationKind.Clean, "오염", config.cleanSeconds);
                dirt.Add(station);
                player?.RefreshStations();
                return;
            }
        }

        private void SpawnLeftover()
        {
            if (counter == null) return;
            Vector2 position = (Vector2)counter.transform.position + new Vector2(UnityEngine.Random.Range(-0.9f, 0.9f), -1.5f);
            StoreStation station = CreateStation("Leftover", position, dirtSprite, new Color(0.94f, 0.27f, 0.27f, 1f), 7);
            station.Configure(StoreStationKind.CleanupLeftover, "두고 간 물건", config.leftoverSeconds);
            leftovers.Add(station);
            player?.RefreshStations();
        }

        private bool IsTooCloseToStation(Vector2 point)
        {
            foreach (StoreStation station in FindObjectsByType<StoreStation>(FindObjectsInactive.Exclude))
                if (Vector2.Distance(point, station.transform.position) < 1.0f) return true;
            return false;
        }

        private StoreStation CreateStation(string name, Vector2 position, Sprite sprite, Color color, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : StoreVisuals.Pixel;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (sprite == null) go.transform.localScale = new Vector3(1.1f, 0.9f, 1f);
            return go.AddComponent<StoreStation>();
        }

        // ─── 정산 ─────────────────────────────────────────────────────

        private void ApplyPenalty(float amount)
        {
            CurrentPay = Mathf.Max(basePay * config.minPayRatio, CurrentPay - amount);
            OnPayChanged?.Invoke(CurrentPay);
        }

        private void RollGift()
        {
            if (gifts.Count >= config.dailyGiftCap) return;
            if (UnityEngine.Random.value > config.giftChance) return;

            GiftEntry entry = config.RollGift();
            if (string.IsNullOrEmpty(entry.itemId) || entry.amount <= 0) return;

            for (int i = 0; i < entry.amount; i++) gifts.Add(entry.itemId);
            OnFeedback?.Invoke("손님이 선물을 주고 갔어요!");
        }

        private void Settle()
        {
            ChangeState(StoreShiftState.Settling);

            // 남은 손님은 즉시 퇴장 처리합니다. 계산대 앞에서 기다리던 손님은 이탈로 치지 않습니다 —
            // 근무 종료는 손님 탓이 아닙니다.
            for (int i = alive.Count - 1; i >= 0; i--)
                if (alive[i] != null) Destroy(alive[i].gameObject);
            alive.Clear();
            queue.Clear();

            tally.RemainingDirt = CountAlive(dirt);
            tally.RemainingLeftovers = CountAlive(leftovers);
            tally.EmptyShelves = 0;
            for (int i = 0; i < shelves.Count; i++)
                if (shelves[i] != null && shelves[i].IsShelfEmpty) tally.EmptyShelves++;

            StoreShiftResult result = Settle(tally, basePay, config);
            result.Gifts = new List<string>(gifts);
            result.SaveFailed = !Commit(result);

            CurrentPay = result.Pay;
            OnPayChanged?.Invoke(CurrentPay);

            ChangeState(StoreShiftState.Result);
            OnShiftFinished?.Invoke(result);
        }

        /// <summary>
        /// 일급과 등급. 순수 함수 — 씬도 시간도 참조하지 않습니다.
        ///
        /// 요미는 시급제입니다. 매출·판매가·인센티브는 없고, 일급은 기본급에서 감점만큼 깎여 내려갑니다.
        /// 상한은 언제나 기본급이며, 하한은 기본급 × minPayRatio 입니다.
        /// </summary>
        public static StoreShiftResult Settle(in StoreShiftTally t, float basePay, StoreConfig cfg)
        {
            float penalty = t.Walkouts * cfg.walkoutPenalty
                          + t.RemainingDirt * cfg.dirtPenaltyPerSpot
                          + t.EmptyShelves * cfg.emptyShelfPenalty
                          + t.RemainingLeftovers * cfg.leftoverPenalty;

            float floor = basePay * cfg.minPayRatio;
            float pay = Mathf.Max(floor, basePay - penalty);

            // 등급은 "들어온 손님 대비 계산해 준 손님" 비율입니다. 분모에는 재고가 없어 발길을 돌린
            // 손님도 포함됩니다 — 매대를 비워 두는 것이 등급을 올리는 편법이 되면 안 됩니다.
            float served = t.TotalCustomers > 0 ? (float)t.CheckedOut / t.TotalCustomers : 1f;
            char grade = served >= 0.95f && t.RemainingDirt == 0 ? 'S'
                       : served >= 0.85f ? 'A'
                       : served >= 0.65f ? 'B'
                       : served >= 0.40f ? 'C' : 'D';

            return new StoreShiftResult { BasePay = basePay, Pay = pay, Grade = grade, Tally = t };
        }

        /// <summary>일급·선물·누적 근무를 세이브에 확정합니다. 디스크 기록에 성공하면 true.</summary>
        private bool Commit(StoreShiftResult result)
        {
            SaveLoadManager save = SaveLoadManager.Instance;
            if (save == null) return false;

            WorldMap.WorldMapManager.PayWage(result.Pay);

            for (int i = 0; i < gifts.Count; i++) save.GrantItemToSave(gifts[i]);

            if (save.CurrentData != null) save.CurrentData.StoreTotalShifts++;

            // 오버도즈 중에는 저장이 거부됩니다. CurrentData의 변경은 메모리에 남아 다음 성공 저장에
            // 딸려 가지만, 그 사이 강제 종료하면 사라지므로 결과 패널에 경고를 띄웁니다.
            return save.SaveCurrentGame();
        }

        private static int CountAlive(List<StoreStation> list)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) count++;
            return count;
        }

        /// <summary>결과 확인 후 월드맵으로 복귀합니다.</summary>
        public void ReturnToWorldMap()
        {
            LoadingScreenController.TargetSceneToLoad = "WorldMapScene";
            SceneManager.LoadScene("LoadingScene");
        }

        /// <summary>
        /// 거절 사유를 알립니다.
        ///
        /// ⚠️ CanExecute는 E를 누르고 있는 <b>매 프레임</b> 호출됩니다. 그대로 흘리면 같은 문구가
        ///    초당 수십 번 재발행되어 피드백 줄이 영원히 지워지지 않습니다. 같은 사유는 2초에 한 번만 냅니다.
        /// </summary>
        private bool Deny(string reason)
        {
            if (reason != lastDenyReason || Time.time - lastDenyTime > 2f)
            {
                lastDenyReason = reason;
                lastDenyTime = Time.time;
                OnFeedback?.Invoke(reason);
            }
            return false;
        }

        private void ChangeState(StoreShiftState next)
        {
            if (State == next) return;
            State = next;
            OnStateChanged?.Invoke(State);
        }
    }

    /// <summary>스프라이트가 아직 없는 동안 쓰는 1픽셀 대체 이미지입니다. 아트가 들어오면 참조가 자연히 사라집니다.</summary>
    public static class StoreVisuals
    {
        private static Sprite pixel;

        public static Sprite Pixel
        {
            get
            {
                if (pixel != null) return pixel;
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                pixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                return pixel;
            }
        }
    }
}
