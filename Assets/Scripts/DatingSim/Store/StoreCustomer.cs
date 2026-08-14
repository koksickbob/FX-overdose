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
    /// 손님 한 명. 경로는 입구 → 매대 → 대기열 → 입구 4점 직선 이동뿐입니다.
    /// 편의점 내부에 장애물이 사실상 없어 길찾기가 필요하지 않습니다.
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

        public void Configure(StoreShiftManager owner, StoreStation shelf, Vector2 exit, float patience, int items, float speed)
        {
            manager = owner;
            targetShelf = shelf;
            exitPoint = exit;
            patienceSeconds = patience;
            patienceRemaining = patience;
            ItemCount = Mathf.Max(1, items);
            moveSpeed = speed;
            destination = shelf != null ? (Vector2)shelf.transform.position + new Vector2(0f, -1.3f) : exit;
        }

        /// <summary>대기열 위치를 갱신합니다. 앞사람이 빠지면 매니저가 당겨 줍니다.</summary>
        public void SetQueueSlot(Vector2 position)
        {
            if (State != StoreCustomerState.Queueing && State != StoreCustomerState.WaitingAtCounter) return;
            destination = position;
            if (State == StoreCustomerState.WaitingAtCounter && Vector2.Distance(transform.position, position) > 0.15f)
                State = StoreCustomerState.Queueing;
        }

        public Vector2 MoveInput => moveDirection;

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
                destination = exitPoint;
                return;
            }

            State = StoreCustomerState.Browsing;
            browseTimer = 1.5f;
        }

        private void TickBrowsing()
        {
            moveDirection = Vector2.zero;
            browseTimer -= Time.deltaTime;
            if (browseTimer > 0f) return;

            int taken = targetShelf.TakeStock(ItemCount);
            if (taken <= 0)
            {
                LeftEmptyHanded = true;
                Report(walkout: true);
                State = StoreCustomerState.Leaving;
                destination = exitPoint;
                return;
            }

            ItemCount = taken;
            manager.NotifyBrowsingFinished(this, targetShelf);
            State = StoreCustomerState.Queueing;
            destination = manager.ReserveQueueSlot(this);
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
            destination = exitPoint;
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
            destination = exitPoint;
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
            Vector2 delta = destination - current;
            if (delta.sqrMagnitude <= 0.02f)
            {
                moveDirection = Vector2.zero;
                return true;
            }

            moveDirection = delta.normalized;
            transform.position = Vector2.MoveTowards(current, destination, moveSpeed * Time.deltaTime);
            return false;
        }
    }
}
