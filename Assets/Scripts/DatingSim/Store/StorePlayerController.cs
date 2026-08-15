using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FXOverdose.DatingSim.Store
{
    /// <summary>
    /// 편의점의 요미. WASD 이동 + E 홀드 액션만 합니다.
    ///
    /// 요미의 방 컨트롤러를 상속하거나 재사용하지 않은 이유: 그쪽은 모달·YomiRoomInteractable·
    /// YomiRoomManager 상태 조회에 묶여 있어 떼어내는 비용이 다시 쓰는 비용보다 큽니다.
    /// 이동 수치(moveSpeed·interactRadius)는 같은 값을 씁니다.
    /// </summary>
    public sealed class StorePlayerController : MonoBehaviour
    {
        private Rigidbody2D body;
        private StoreShiftManager manager;
        private StoreConfig config;
        private Collider2D playerCollider;
        private Vector2 input;

        private readonly List<StoreStation> stations = new List<StoreStation>();

        public Vector2 MoveInput => input;

        /// <summary>가장 가까운 상호작용 대상. 없으면 null.</summary>
        public StoreStation Nearby { get; private set; }

        /// <summary>지금 E를 눌러 진행 중인 대상. UI가 진행 바를 붙일 곳입니다.</summary>
        public StoreStation Active { get; private set; }

        public StoreCarry Carry { get; private set; } = StoreCarry.None;
        public int CarriedStockUnits { get; private set; }

        public void Configure(Rigidbody2D playerBody, StoreShiftManager owner, StoreConfig storeConfig)
        {
            body = playerBody;
            manager = owner;
            config = storeConfig;
            playerCollider = GetComponent<Collider2D>();
            RefreshStations();
        }

        /// <summary>오염처럼 런타임에 생기는 스테이션이 추가되면 호출합니다.</summary>
        public void RefreshStations()
        {
            stations.Clear();
            stations.AddRange(FindObjectsByType<StoreStation>(FindObjectsInactive.Exclude));
        }

        public void SetCarry(StoreCarry carry, int units)
        {
            Carry = carry;
            CarriedStockUnits = carry == StoreCarry.Stock ? units : 0;
        }

        public void ConsumeStock(int units)
        {
            CarriedStockUnits = Mathf.Max(0, CarriedStockUnits - units);
            if (CarriedStockUnits <= 0) Carry = StoreCarry.None;
        }

        private void Update()
        {
            if (body == null || manager == null) return;

            bool accepting = manager.IsInputAllowed;
            Keyboard keyboard = Keyboard.current;
            bool typing = EventSystem.current != null &&
                          EventSystem.current.currentSelectedGameObject != null &&
                          EventSystem.current.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>() != null;

            Vector2 desired = Vector2.zero;
            if (accepting && !typing && keyboard != null)
            {
                desired.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                desired.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            }
            input = Vector2.ClampMagnitude(desired, 1f);

            UpdateNearby();

            // Alt+Tab 등으로 포커스를 잃으면 키가 눌린 채로 남을 수 있습니다. 홀드를 강제로 놓습니다. (R9)
            bool holding = accepting && !typing && Application.isFocused &&
                           keyboard != null && keyboard.eKey.isPressed;
            UpdateHold(holding, keyboard != null && keyboard.eKey.wasPressedThisFrame);
        }

        private void FixedUpdate()
        {
            if (body == null) return;
            body.MovePosition(body.position + input * (config.moveSpeed * Time.fixedDeltaTime));
        }

        private void UpdateNearby()
        {
            StoreStation best = null;
            float bestDistance = config.interactRadius;
            for (int i = 0; i < stations.Count; i++)
            {
                StoreStation candidate = stations[i];
                if (candidate == null) continue; // 청소로 파괴된 오염

                float distance = Vector2.Distance(body.position, candidate.transform.position);
                Collider2D candidateCollider = candidate.GetComponent<Collider2D>();
                if (playerCollider != null && candidateCollider != null)
                {
                    ColliderDistance2D separation = playerCollider.Distance(candidateCollider);
                    distance = separation.isOverlapped ? 0f : separation.distance;
                }
                // 청소도구를 들었을 때 가까운 오염이 진열대 상호작용에 가려지지 않게 우선합니다.
                if (Carry == StoreCarry.Tool && candidate.Kind == StoreStationKind.Clean)
                    distance = Mathf.Max(0f, distance - 0.45f);
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            Nearby = best;
        }

        private void UpdateHold(bool holding, bool tapped)
        {
            // 대상에서 벗어났거나 손을 뗐으면 진행도를 '유지한 채' 멈춥니다.
            if (Active != null && (Active != Nearby || !holding))
            {
                Active.Pause();
                Active = null;
            }

            if (Nearby == null) return;

            // 창고의 두 가지는 홀드가 아니라 탭입니다. 무엇을 챙길지 고르는 행동이라 시간이 걸릴 이유가 없습니다.
            if (Nearby.Kind == StoreStationKind.PickupStock || Nearby.Kind == StoreStationKind.PickupTool)
            {
                if (tapped) manager.ExecuteStation(Nearby, this);
                return;
            }

            if (!holding) return;
            if (!manager.CanExecute(Nearby, this)) return;

            Active = Nearby;
            if (Active.Advance(Time.deltaTime))
                manager.ExecuteStation(Active, this);

            if (Active != null && Active.Kind == StoreStationKind.Clean) Active.ApplyCleanFade();
        }
    }
}
