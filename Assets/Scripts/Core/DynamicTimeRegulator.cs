using System.Collections;
using UnityEngine;

namespace FXOverdose.Core
{
    public class DynamicTimeRegulator : MonoBehaviour
    {
        public static DynamicTimeRegulator Instance { get; private set; }

        [SerializeField] private GameManager gameManager;
        [SerializeField] private float baseSecondsPerMinute = 1.0f;
        private float targetSecondsPerMinute = 1.0f;
        private float foodTimeMultiplier = 1f;
        private bool isDramaticOverrideActive;
        private Coroutine slowMotionCoroutine;
        public float BaseSecondsPerMinute => baseSecondsPerMinute;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // 씬 전환 시 파괴를 막고 싶다면 DontDestroyOnLoad(gameObject); 추가 가능
            // 하지만 GameManager에 부착될 경우 GameManager의 생명주기를 따릅니다.
        }

        private void Start()
        {
            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }
            
            if (gameManager != null)
            {
                baseSecondsPerMinute = gameManager.SecondsPerGameMinute;
                targetSecondsPerMinute = baseSecondsPerMinute;
            }
        }

        private void Update()
        {
            if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
            {
                return;
            }

            // 부드러운 시간 배속 전환 (Lerp)
            float current = gameManager.SecondsPerGameMinute;
            
            // 만약 목표 시간과 현재 시간이 다르면 보간하여 이동합니다.
            if (!Mathf.Approximately(current, targetSecondsPerMinute))
            {
                float newSpeed = Mathf.Lerp(current, targetSecondsPerMinute, Time.unscaledDeltaTime * 3f);
                gameManager.SetSecondsPerGameMinute(newSpeed);
            }
        }

        /// <summary>
        /// 일정 시간 동안 목표 속도(targetSpeed)로 슬로우 모션을 가동합니다.
        /// </summary>
        /// <param name="targetSpeed">인게임 1분이 흐르는데 걸리는 목표 현실 시간 (예: 3.0f = 슬로우 모션)</param>
        /// <param name="durationSeconds">현실 시간 기준 지속 시간 (초)</param>
        public void TriggerDramaticSlowMotion(float targetSpeed, float durationSeconds)
        {
            if (slowMotionCoroutine != null)
            {
                StopCoroutine(slowMotionCoroutine);
            }
            slowMotionCoroutine = StartCoroutine(SlowMotionRoutine(targetSpeed, durationSeconds));
        }

        private IEnumerator SlowMotionRoutine(float targetSpeed, float duration)
        {
            isDramaticOverrideActive = true;
            targetSecondsPerMinute = targetSpeed;
            yield return new WaitForSecondsRealtime(duration);
            
            isDramaticOverrideActive = false;
            targetSecondsPerMinute = baseSecondsPerMinute / foodTimeMultiplier;
            slowMotionCoroutine = null;
        }

        public void SetFoodTimeMultiplier(float multiplier)
        {
            foodTimeMultiplier = Mathf.Max(0.01f, multiplier);
            if (!isDramaticOverrideActive)
                targetSecondsPerMinute = baseSecondsPerMinute / foodTimeMultiplier;
        }
    }
}
