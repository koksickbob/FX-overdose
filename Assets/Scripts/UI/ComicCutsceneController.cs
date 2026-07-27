using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FXOverdose.UI
{
    /// <summary>
    /// 스토리 모드의 컷툰(만화) 연출을 담당하는 UI 컨트롤러입니다.
    /// 게임 중 팝업 형태로 화면 최상단에 나타나며, 클릭 시 다음 컷으로 넘어갑니다.
    /// </summary>
    public class ComicCutsceneController : MonoBehaviour
    {
        private static ComicCutsceneController _instance;
        public static ComicCutsceneController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<ComicCutsceneController>();
                }
                return _instance;
            }
        }

        [Header("UI References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image comicImageDisplay;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button nextPanelButton; // 전체 화면을 덮는 투명 버튼 (클릭 시 다음 컷)
        [SerializeField] private TextMeshProUGUI instructionText; // "화면을 터치하세요" 등

        [Header("Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private List<Sprite> _currentPanels;
        private int _currentIndex = 0;
        private Action _onCompleteCallback;
        private bool _isPlaying = false;
        private bool _isFading = false;
        private float _fadeTimer = 0f;
        private float _targetAlpha = 0f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (skipButton != null)
                skipButton.onClick.AddListener(SkipCutscene);

            if (nextPanelButton != null)
                nextPanelButton.onClick.AddListener(ShowNextPanel);
        }

        private void Update()
        {
            if (_isFading && canvasGroup != null)
            {
                _fadeTimer += Time.unscaledDeltaTime; // TimeScale=0 상태에서도 작동하도록
                float t = Mathf.Clamp01(_fadeTimer / fadeDuration);
                
                // 알파값 보간
                float startAlpha = canvasGroup.alpha;
                canvasGroup.alpha = Mathf.MoveTowards(startAlpha, _targetAlpha, Time.unscaledDeltaTime / fadeDuration);

                if (Mathf.Approximately(canvasGroup.alpha, _targetAlpha))
                {
                    _isFading = false;
                    if (_targetAlpha == 0f)
                    {
                        canvasGroup.interactable = false;
                        canvasGroup.blocksRaycasts = false;
                        _isPlaying = false;
                        _onCompleteCallback?.Invoke();
                        _onCompleteCallback = null;
                    }
                }
            }
        }

        /// <summary>
        /// 만화(컷툰) 패널 리스트를 순차적으로 띄웁니다. 재생이 끝나면 callback이 호출됩니다.
        /// </summary>
        public void PlayCutscene(List<Sprite> panels, Action onCompleteCallback)
        {
            if (panels == null || panels.Count == 0)
            {
                Debug.LogWarning("[ComicCutsceneController] 전달된 컷툰 패널이 없습니다. 즉시 콜백을 실행합니다.");
                onCompleteCallback?.Invoke();
                return;
            }

            _currentPanels = panels;
            _currentIndex = 0;
            _onCompleteCallback = onCompleteCallback;
            _isPlaying = true;

            if (comicImageDisplay != null)
                comicImageDisplay.sprite = _currentPanels[_currentIndex];

            // UI 켜기
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                _targetAlpha = 1f;
                _fadeTimer = 0f;
                _isFading = true;
            }
        }

        private void ShowNextPanel()
        {
            if (!_isPlaying || _isFading) return;

            _currentIndex++;
            if (_currentIndex < _currentPanels.Count)
            {
                // 다음 컷 띄우기
                if (comicImageDisplay != null)
                {
                    comicImageDisplay.sprite = _currentPanels[_currentIndex];
                }
            }
            else
            {
                // 만화 끝남
                EndCutscene();
            }
        }

        private void SkipCutscene()
        {
            if (!_isPlaying || _isFading) return;
            EndCutscene();
        }

        private void EndCutscene()
        {
            // UI 끄기 (Fade Out)
            if (canvasGroup != null)
            {
                _targetAlpha = 0f;
                _fadeTimer = 0f;
                _isFading = true;
                canvasGroup.interactable = false;
            }
            else
            {
                _isPlaying = false;
                _onCompleteCallback?.Invoke();
                _onCompleteCallback = null;
            }
        }
    }
}
