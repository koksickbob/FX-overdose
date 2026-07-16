using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

namespace FXOverdose.UI
{
    public class GameOverUIController : MonoBehaviour
    {
        [Header("UI 연동 대상")]
        [Tooltip("게임 오버 화면 패널 (기본 비활성화 상태여야 합니다)")]
        [SerializeField] private GameObject gameOverPanel;
        
        [Tooltip("게임 오버 사유를 표시할 TextMeshPro 텍스트 컴포넌트")]
        [SerializeField] private TextMeshProUGUI reasonText;
        
        [Header("타이밍 설정")]
        [Tooltip("멘헤라 루프가 시작된 후 UI를 활성화할 지연 시간 (절반인 13초 권장)")]
        [SerializeField] private float displayDelay = 13f;

        private void OnEnable()
        {
            // 이벤트 구독
            GameManager.OnGameOverEvent += HandleGameOver;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            GameManager.OnGameOverEvent -= HandleGameOver;
        }

        private void HandleGameOver(GameManager.EndingType endingType)
        {
            // Success 엔딩일 경우 다른 연출이 있을 수 있으나, 일단 UI 트리거
            // 필요 시 if (endingType == GameManager.EndingType.Success) return; 등의 분기 추가 가능
            StartCoroutine(ShowGameOverUI(endingType));
        }

        private IEnumerator ShowGameOverUI(GameManager.EndingType endingType)
        {
            // 멘헤라 독백 루프의 절반 시점 대기
            yield return new WaitForSeconds(displayDelay);
            
            // 패널 활성화
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }

            // 사유 텍스트 반영
            if (reasonText != null)
            {
                reasonText.text = GetEndingReason(endingType);
            }
        }

        // 엔딩 타입에 따른 텍스트 설정
        private string GetEndingReason(GameManager.EndingType endingType)
        {
            switch (endingType)
            {
                case GameManager.EndingType.Bankruptcy:
                    return "자본금 전액 손실 (강제 청산 및 파산)";
                case GameManager.EndingType.Overdose:
                    return "도파민 과다로 인한 치명적 멘탈 붕괴";
                case GameManager.EndingType.Success:
                    return "목표 수익 달성 완료 (게임 클리어)";
                default:
                    return "게임 오버";
            }
        }

        // 버튼 OnClick 이벤트 등에 연결하여 사용
        public void ReturnToTitle()
        {
            // TitleScene 씬을 로드합니다.
            // 빌드 세팅에 등록된 씬 이름(TitleScene)과 정확히 일치해야 합니다.
            SceneManager.LoadScene("TitleScene");
        }
    }
}
