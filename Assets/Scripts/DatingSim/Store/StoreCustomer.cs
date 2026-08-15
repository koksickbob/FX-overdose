using System.Collections.Generic;
using UnityEngine;

namespace FXOverdose.DatingSim.Store
{
    public enum StoreCustomerState
    {
        Entering,         // 입구 → 목표 매대
        Browsing,         // 매대 앞에서 물건 고르는 중
        Queueing,         // 대기열 슬롯으로 이동
        WaitingAtCounter, // 계산 대기 (인내 타이머)
        Leaving           // 퇴장
    }

    /// <summary>
    /// 손님 한 명. 목적지로 이동하되 진열대·계산대 같은 월드 콜라이더를 감지하면 옆으로 우회합니다.
    /// </summary>
    public sealed class StoreCustomer : MonoBehaviour
    {
        public StoreCustomerState State { get; private set; } = StoreCustomerState.Entering;
        public int ItemCount { get; private set; }

        /// <summary>인내 잔여 비율 0~1. UI 게이지가 읽습니다.</summary>
        public float PatienceRatio => patienceSeconds > 0f ? Mathf.Clamp01(patienceRemaining / patienceSeconds) : 1f;

        /// <summary>계산대 앞에 서 있고 계산 가능한 상태인지.</summary>
        public bool IsWaiting => State == StoreCustomerState.WaitingAtCounter;

        /// <summary>재고가 없어 빈손으로 돌아가는 손님인지. 잔여물을 남기지 않는 이탈입니다.</summary>
        public bool LeftEmptyHanded { get; private set; }

        private StoreShiftManager manager;
        private StoreStation targetShelf;
        private Vector2 exitPoint;
        private Vector2 destination;
        private Vector2 moveDirection;
        private float moveSpeed = 3.2f;
        private float browseTimer;
        private float patienceSeconds;
        private float patienceRemaining;
        private bool counted;
        private Collider2D ownCollider;
        private readonly List<Vector2> path = new List<Vector2>();
        private int pathIndex;

        // 진열대 하나를 한 장애물 칸으로 취급하는 매장 배치 전용 격자입니다.
        // 열 간격 5.2 = 2칸, 행 간격 3.1 = 2칸이므로 통로가 항상 한 칸씩 남습니다.
        private static readonly Vector2 GridCellSize = new Vector2(2.6f, 1.55f);
        private static readonly Vector2 GridMin = new Vector2(-7.8f, -4.4f);
        private const int GridWidth = 7;
        private const int GridHeight = 6;
        private static readonly Vector2Int[] GridDirections =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };

        public void Configure(StoreShiftManager owner, StoreStation shelf, Vector2 exit, float patience, int items, float speed)
        {
            manager = owner;
            targetShelf = shelf;
            exitPoint = exit;
            patienceSeconds = patience;
            patienceRemaining = patience;
            ItemCount = Mathf.Max(1, items);
            moveSpeed = speed;
            ownCollider = GetComponent<Collider2D>();
            SetDestination(shelf != null ? (Vector2)shelf.transform.position + new Vector2(0f, -1.85f) : exit);
        }

        /// <summary>대기열 위치를 갱신합니다. 앞사람이 빠지면 매니저가 당겨 줍니다.</summary>
        public void SetQueueSlot(Vector2 position)
        {
            if (State != StoreCustomerState.Queueing && State != StoreCustomerState.WaitingAtCounter) return;
            if ((destination - position).sqrMagnitude > 0.0025f) SetDestination(position);
            if (State == StoreCustomerState.WaitingAtCounter && Vector2.Distance(transform.position, position) > 0.15f)
                State = StoreCustomerState.Queueing;
        }

        public Vector2 MoveInput => moveDirection;
        public bool IsWalking => moveDirection.sqrMagnitude > 0.01f &&
                                 (State == StoreCustomerState.Entering ||
                                  State == StoreCustomerState.Queueing ||
                                  State == StoreCustomerState.Leaving);

        private void Update()
        {
            if (manager == null || !manager.IsCustomerTickAllowed) { moveDirection = Vector2.zero; return; }

            switch (State)
            {
                case StoreCustomerState.Entering: TickEntering(); break;
                case StoreCustomerState.Browsing: TickBrowsing(); break;
                case StoreCustomerState.Queueing: TickQueueing(); break;
                case StoreCustomerState.WaitingAtCounter: TickWaiting(); break;
                case StoreCustomerState.Leaving: TickLeaving(); break;
            }
        }

        private void TickEntering()
        {
            if (!MoveToDestination()) return;

            // 재고가 없으면 아무것도 못 집고 돌아섭니다. 감점 없이 두면 매대를 비워 두는 것이
            // 최적 전략이 되므로 이것도 이탈로 집계합니다. (R11)
            if (targetShelf == null || targetShelf.IsShelfEmpty)
            {
                LeftEmptyHanded = true;
                Report(walkout: true);
                State = StoreCustomerState.Leaving;
                SetDestination(exitPoint);
                return;
            }

            State = StoreCustomerState.Browsing;
            browseTimer = 1.5f;
        }

        private void TickBrowsing()
        {
            // 이동은 멈추되 애니메이터에는 진열대를 향한 방향을 유지시킵니다.
            Vector2 look = targetShelf != null ? (Vector2)targetShelf.transform.position - (Vector2)transform.position : Vector2.up;
            moveDirection = Mathf.Abs(look.x) > Mathf.Abs(look.y)
                ? new Vector2(Mathf.Sign(look.x), 0f)
                : new Vector2(0f, Mathf.Sign(look.y));
            browseTimer -= Time.deltaTime;
            if (browseTimer > 0f) return;

            int taken = targetShelf.TakeStock(ItemCount);
            if (taken <= 0)
            {
                LeftEmptyHanded = true;
                Report(walkout: true);
                State = StoreCustomerState.Leaving;
                SetDestination(exitPoint);
                return;
            }

            ItemCount = taken;
            manager.NotifyBrowsingFinished(this, targetShelf);
            State = StoreCustomerState.Queueing;
            SetDestination(manager.ReserveQueueSlot(this));
        }

        private void TickQueueing()
        {
            if (!MoveToDestination()) return;
            State = StoreCustomerState.WaitingAtCounter;
        }

        private void TickWaiting()
        {
            moveDirection = Vector2.zero;
            patienceRemaining -= Time.deltaTime;
            if (patienceRemaining > 0f) return;

            Report(walkout: true);
            State = StoreCustomerState.Leaving;
            SetDestination(exitPoint);
        }

        private void TickLeaving()
        {
            if (!MoveToDestination()) return;
            manager.ReleaseCustomer(this);
            Destroy(gameObject);
        }

        /// <summary>계산 완료. 매니저만 호출합니다.</summary>
        public void CompleteCheckout()
        {
            Report(walkout: false);
            State = StoreCustomerState.Leaving;
            SetDestination(exitPoint);
        }

        /// <summary>결과를 매니저에 한 번만 보고합니다. 이탈과 계산이 이중 집계되면 등급이 틀어집니다.</summary>
        private void Report(bool walkout)
        {
            if (counted) return;
            counted = true;
            if (walkout) manager.NotifyWalkout(this);
            else manager.NotifyCheckedOut(this);
        }

        private bool MoveToDestination()
        {
            Vector2 current = transform.position;
            Vector2 target = pathIndex < path.Count ? path[pathIndex] : destination;
            Vector2 delta = target - current;
            if (delta.sqrMagnitude <= 0.02f)
            {
                if (pathIndex < path.Count)
                {
                    pathIndex++;
                    return false;
                }
                moveDirection = Vector2.zero;
                return true;
            }

            // 탑다운 워크시트 방향과 통로 이동을 맞추기 위해 한 번에 한 축으로만 걷습니다.
            Vector2 desired = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Sign(delta.x), 0f)
                : new Vector2(0f, Mathf.Sign(delta.y));
            float step = moveSpeed * Time.deltaTime;
            moveDirection = desired;
            transform.position = current + desired * Mathf.Min(step, delta.magnitude);
            return false;
        }

        private void SetDestination(Vector2 target)
        {
            destination = target;
            BuildPath((Vector2)transform.position, target);
        }

        private void BuildPath(Vector2 startWorld, Vector2 goalWorld)
        {
            path.Clear();
            pathIndex = 0;

            Vector2Int start = WorldToGrid(startWorld);
            Vector2Int goal = WorldToGrid(goalWorld);
            int[,] parent = new int[GridWidth, GridHeight];
            for (int x = 0; x < GridWidth; x++)
                for (int y = 0; y < GridHeight; y++)
                    parent[x, y] = -2;

            Queue<Vector2Int> open = new Queue<Vector2Int>();
            open.Enqueue(start);
            parent[start.x, start.y] = -1;

            while (open.Count > 0 && parent[goal.x, goal.y] == -2)
            {
                Vector2Int current = open.Dequeue();
                for (int i = 0; i < GridDirections.Length; i++)
                {
                    Vector2Int next = current + GridDirections[i];
                    if (!InsideGrid(next) || parent[next.x, next.y] != -2) continue;
                    if (next != goal && !IsGridCellWalkable(next)) continue;
                    parent[next.x, next.y] = current.y * GridWidth + current.x;
                    open.Enqueue(next);
                }
            }

            if (parent[goal.x, goal.y] == -2)
            {
                path.Add(goalWorld);
                return;
            }

            List<Vector2> reversed = new List<Vector2>();
            Vector2Int cursor = goal;
            while (cursor != start)
            {
                reversed.Add(GridToWorld(cursor));
                int packed = parent[cursor.x, cursor.y];
                cursor = new Vector2Int(packed % GridWidth, packed / GridWidth);
            }
            reversed.Reverse();

            // 같은 방향의 연속 셀은 끝점 하나로 접어 불필요한 미세 정지를 줄입니다.
            Vector2Int previousDirection = new Vector2Int(int.MinValue, int.MinValue);
            for (int i = 0; i < reversed.Count; i++)
            {
                Vector2Int direction = i + 1 < reversed.Count
                    ? WorldToGrid(reversed[i + 1]) - WorldToGrid(reversed[i])
                    : previousDirection;
                if (i + 1 == reversed.Count || direction != previousDirection)
                    path.Add(reversed[i]);
                previousDirection = direction;
            }
            path.Add(goalWorld);
        }

        private bool IsGridCellWalkable(Vector2Int cell)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(GridToWorld(cell), 0.31f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || hit == ownCollider || hit.isTrigger) continue;
                if (hit.GetComponent<StorePlayerController>() != null || hit.GetComponent<StoreCustomer>() != null)
                    continue;
                return false;
            }
            return true;
        }

        private static Vector2Int WorldToGrid(Vector2 world)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((world.x - GridMin.x) / GridCellSize.x), 0, GridWidth - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((world.y - GridMin.y) / GridCellSize.y), 0, GridHeight - 1);
            return new Vector2Int(x, y);
        }

        private static Vector2 GridToWorld(Vector2Int cell) =>
            GridMin + new Vector2(cell.x * GridCellSize.x, cell.y * GridCellSize.y);

        private static bool InsideGrid(Vector2Int cell) =>
            cell.x >= 0 && cell.x < GridWidth && cell.y >= 0 && cell.y < GridHeight;
    }
}
