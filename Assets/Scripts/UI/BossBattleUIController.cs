using System.Collections;
using FXOverdose.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FXOverdose.UI
{
    /// <summary>
    /// 보스가 등장한 날에만 우측 상단에 표시되는 실시간 대전 전광판입니다.
    /// BossManager의 자산 변경 및 파산 이벤트를 받아 즉시 갱신합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossBattleUIController : MonoBehaviour
    {
        private const float TopBarHeight = 108f;
        private const float TopBarGap = 12f;
        private const float SkillRowHeight = UIStrokeStyle.CompactHudHeight;
        private const float SkillRowGap = 12f;
        private const float RightMargin = 24f;
        private const float PanelWidth = 460f;
        private const float PanelHeight = 194f;

        private static readonly Color Panel = new(0.035f, 0.075f, 0.13f, 0.98f);
        private static readonly Color Header = new(0.055f, 0.12f, 0.20f, 1f);
        private static readonly Color Border = new Color32(31, 61, 86, 255);
        private static readonly Color Cyan = new Color32(6, 182, 212, 255);
        private static readonly Color Gold = new Color32(234, 179, 8, 255);
        private static readonly Color Green = new Color32(34, 197, 94, 255);
        private static readonly Color Red = new Color32(239, 68, 68, 255);
        private static readonly Color Text = new Color32(221, 247, 250, 255);
        private static readonly Color Muted = new Color32(111, 143, 165, 255);

        private GameManager gameManager;
        private BossManager bossManager;
        private RectTransform panelRect;
        private CanvasGroup panelGroup;
        private Image liveDot;
        private TMP_Text dayBadge;
        private TMP_Text bossName;
        private TMP_Text bossDescription;
        private TMP_Text assetValue;
        private TMP_Text returnValue;
        private TMP_Text statusValue;
        private Image assetFill;
        private GameObject bankruptStamp;
        private RectTransform bankruptStampRect;
        private BossData displayedBoss;
        private bool subscribed;
        private bool bankruptAnimationPlayed;
        private Coroutine bankruptRoutine;

        private void Awake()
        {
            gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            BuildRuntimeUI();
            SetVisible(false);
        }

        private void OnEnable()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private IEnumerator SubscribeWhenReady()
        {
            yield return null;
            BindManager(BossManager.Instance);
            RefreshFromManager();
        }

        private void Update()
        {
            if (gameManager == null)
                gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);

            BossManager currentManager = BossManager.Instance;
            if (currentManager != bossManager)
                BindManager(currentManager);

            BossData currentBoss = bossManager != null ? bossManager.CurrentBoss : null;
            if (currentBoss != displayedBoss)
                RefreshFromManager();

            if (liveDot != null && panelRect != null && panelRect.gameObject.activeSelf &&
                bossManager != null && !bossManager.IsBossBankrupt)
            {
                float pulse = 0.68f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.32f;
                Color color = Cyan;
                color.a = pulse;
                liveDot.color = color;
            }
        }

        private void BindManager(BossManager manager)
        {
            Unsubscribe();
            bossManager = manager;
            if (bossManager == null) return;

            bossManager.OnBossAssetChanged += HandleBossAssetChanged;
            bossManager.OnBossBankrupted += HandleBossBankrupted;
            subscribed = true;
            RefreshFromManager();
        }

        private void Unsubscribe()
        {
            if (!subscribed || bossManager == null) return;
            bossManager.OnBossAssetChanged -= HandleBossAssetChanged;
            bossManager.OnBossBankrupted -= HandleBossBankrupted;
            subscribed = false;
        }

        private void RefreshFromManager()
        {
            BossData boss = bossManager != null ? bossManager.CurrentBoss : null;
            bool isBossDay = boss != null &&
                (gameManager == null || bossManager.HasBossToday(gameManager.CurrentDay));

            displayedBoss = boss;
            SetVisible(isBossDay);
            if (!isBossDay) return;

            dayBadge.text = $"DAY {boss.Day:00}  /  BOSS";
            bossName.text = boss.IsFinalBoss ? $"FINAL · {boss.Name}" : boss.Name;
            bossDescription.text = boss.Description;
            bankruptAnimationPlayed = false;
            bankruptStamp.SetActive(false);
            HandleBossAssetChanged(bossManager.BossCurrentAsset, bossManager.BossStartingAsset);

            if (bossManager.IsBossBankrupt)
                ShowBankruptState(false);
        }

        private void HandleBossAssetChanged(float currentAsset, float startingAsset)
        {
            if (bossManager != null && displayedBoss != bossManager.CurrentBoss)
            {
                RefreshFromManager();
                return;
            }

            if (displayedBoss == null) return;

            SetVisible(true);

            float returnRate = startingAsset > 0f
                ? ((currentAsset - startingAsset) / startingAsset) * 100f
                : 0f;
            float remainingRatio = startingAsset > 0f
                ? Mathf.Clamp01(currentAsset / startingAsset)
                : 0f;

            assetValue.text = $"${Mathf.Max(0f, currentAsset):N0}";
            returnValue.text = $"{(returnRate >= 0f ? "+" : string.Empty)}{returnRate:F2}%";
            returnValue.color = returnRate >= 0f ? Green : Red;
            assetFill.fillAmount = remainingRatio;
            assetFill.color = remainingRatio > 0.55f ? Gold :
                remainingRatio > 0.25f ? new Color32(249, 115, 22, 255) : Red;

            if (bossManager != null && bossManager.IsBossBankrupt)
            {
                ShowBankruptState(true);
            }
            else
            {
                statusValue.text = returnRate >= 0f ? "PROFIT" : "DRAWDOWN";
                statusValue.color = returnRate >= 0f ? Green : Red;
            }

            RefreshTextMeshes();
        }

        private void HandleBossBankrupted()
        {
            ShowBankruptState(true);
        }

        private void ShowBankruptState(bool animate)
        {
            statusValue.text = "BANKRUPT";
            statusValue.color = Red;
            liveDot.color = Red;
            assetFill.fillAmount = 0f;
            assetFill.color = Red;
            bankruptStamp.SetActive(true);

            if (!animate || bankruptAnimationPlayed)
            {
                bankruptStampRect.localScale = Vector3.one;
                return;
            }

            bankruptAnimationPlayed = true;
            if (bankruptRoutine != null) StopCoroutine(bankruptRoutine);
            bankruptRoutine = StartCoroutine(AnimateBankruptStamp());
        }

        private IEnumerator AnimateBankruptStamp()
        {
            const float duration = 0.28f;
            float elapsed = 0f;
            bankruptStampRect.localScale = Vector3.one * 1.8f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
                bankruptStampRect.localScale = Vector3.one *
                    Mathf.Lerp(1.8f, overshoot, 1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }

            bankruptStampRect.localScale = Vector3.one;
            bankruptRoutine = null;
        }

        private void SetVisible(bool visible)
        {
            if (panelRect == null) return;
            panelRect.gameObject.SetActive(visible);
            if (panelGroup == null) return;
            panelGroup.alpha = visible ? 1f : 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        private void BuildRuntimeUI()
        {
            GameObject panelObject = new("BossBattleLivePanel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(CanvasGroup));
            panelObject.transform.SetParent(transform, false);

            panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelRect.anchoredPosition = new Vector2(
                -RightMargin,
                -(TopBarHeight + TopBarGap + SkillRowHeight + SkillRowGap));

            Image background = panelObject.GetComponent<Image>();
            background.color = Panel;
            background.raycastTarget = false;
            Outline outline = panelObject.GetComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = UIStrokeStyle.EffectDistance;
            panelGroup = panelObject.GetComponent<CanvasGroup>();

            Image topAccent = CreateImage(panelRect, "TopAccent", Cyan);
            SetRect(topAccent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(0f, 4f));

            Image header = CreateImage(panelRect, "Header", Header);
            SetRect(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -44f), Vector2.zero);

            liveDot = CreateImage(header.transform, "LiveDot", Cyan);
            SetRect(liveDot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(14f, -5f), new Vector2(24f, 5f));

            TMP_Text liveLabel = CreateText(header.transform, "LiveLabel", "BOSS BATTLE · LIVE",
                16f, Text, TextAlignmentOptions.Left);
            SetRect(liveLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(32f, 0f), new Vector2(-165f, 0f));

            dayBadge = CreateText(header.transform, "DayBadge", "DAY 00  /  BOSS",
                14f, Gold, TextAlignmentOptions.Right);
            SetRect(dayBadge.rectTransform, new Vector2(0.62f, 0f), Vector2.one,
                new Vector2(0f, 0f), new Vector2(-14f, 0f));

            bossName = CreateText(panelRect, "BossName", "BOSS",
                25f, Text, TextAlignmentOptions.Left);
            bossName.fontStyle = FontStyles.Bold;
            SetRect(bossName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -80f), new Vector2(-118f, -47f));

            statusValue = CreateText(panelRect, "Status", "LIVE",
                14f, Cyan, TextAlignmentOptions.Center);
            SetRect(statusValue.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-112f, -76f), new Vector2(-16f, -50f));

            bossDescription = CreateText(panelRect, "BossDescription", string.Empty,
                13f, Muted, TextAlignmentOptions.Left);
            bossDescription.textWrappingMode = TextWrappingModes.NoWrap;
            bossDescription.overflowMode = TextOverflowModes.Ellipsis;
            SetRect(bossDescription.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -104f), new Vector2(-16f, -80f));

            CreateLabel(panelRect, "AssetLabel", "BOSS ASSET", new Vector2(16f, 48f), new Vector2(140f, 70f));
            assetValue = CreateText(panelRect, "AssetValue", "$0",
                23f, Gold, TextAlignmentOptions.Left);
            SetRect(assetValue.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(16f, 15f), new Vector2(242f, 48f));

            CreateLabel(panelRect, "ReturnLabel", "RETURN", new Vector2(260f, 48f), new Vector2(340f, 70f));
            returnValue = CreateText(panelRect, "ReturnValue", "+0.00%",
                23f, Green, TextAlignmentOptions.Right);
            SetRect(returnValue.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(250f, 15f), new Vector2(444f, 48f));

            Image track = CreateImage(panelRect, "AssetTrack", new Color32(5, 13, 25, 255));
            SetRect(track.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(16f, 6f), new Vector2(444f, 12f));
            assetFill = CreateImage(track.transform, "Fill", Gold);
            assetFill.type = Image.Type.Filled;
            assetFill.fillMethod = Image.FillMethod.Horizontal;
            assetFill.fillOrigin = 0;
            assetFill.fillAmount = 1f;
            Stretch(assetFill.rectTransform);

            bankruptStamp = new GameObject("BankruptStamp", typeof(RectTransform));
            bankruptStamp.transform.SetParent(panelRect, false);
            bankruptStampRect = bankruptStamp.GetComponent<RectTransform>();
            bankruptStampRect.anchorMin = bankruptStampRect.anchorMax = new Vector2(0.5f, 0.5f);
            bankruptStampRect.pivot = new Vector2(0.5f, 0.5f);
            bankruptStampRect.sizeDelta = new Vector2(250f, 72f);
            bankruptStampRect.anchoredPosition = new Vector2(0f, -5f);
            bankruptStampRect.localRotation = Quaternion.Euler(0f, 0f, -7f);

            Image stampBackground = CreateImage(bankruptStampRect, "StampBackground",
                new Color32(63, 16, 28, 238));
            Stretch(stampBackground.rectTransform);
            Outline stampOutline = stampBackground.gameObject.AddComponent<Outline>();
            stampOutline.effectColor = Red;
            stampOutline.effectDistance = new Vector2(4f, -4f);
            TMP_Text stampText = CreateText(bankruptStampRect, "StampText", "BANKRUPT",
                34f, Red, TextAlignmentOptions.Center);
            stampText.fontStyle = FontStyles.Bold;
            Stretch(stampText.rectTransform);
            bankruptStamp.SetActive(false);

            panelRect.SetAsLastSibling();
        }

        private static void CreateLabel(Transform parent, string name, string value, Vector2 min, Vector2 max)
        {
            TMP_Text label = CreateText(parent, name, value, 12f, Muted, TextAlignmentOptions.Left);
            SetRect(label.rectTransform, Vector2.zero, Vector2.zero, min, max);
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string value,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private void RefreshTextMeshes()
        {
            GlobalPFStardustFont.RefreshCompactHudText(dayBadge);
            GlobalPFStardustFont.RefreshCompactHudText(assetValue);
            GlobalPFStardustFont.RefreshCompactHudText(returnValue);
            GlobalPFStardustFont.RefreshCompactHudText(statusValue);
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>GameScene의 메인 TradingView Canvas에 보스전 HUD를 자동 설치합니다.</summary>
    public static class BossBattleUIBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "GameScene") return;

            Canvas fallback = null;
            Canvas target = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas) continue;
                    if (canvas.name == "TradingViewCanvas")
                    {
                        target = canvas;
                        break;
                    }

                    if (fallback == null || canvas.sortingOrder > fallback.sortingOrder)
                        fallback = canvas;
                }

                if (target != null) break;
            }

            target ??= fallback;
            if (target == null || target.GetComponent<BossBattleUIController>() != null) return;

            target.gameObject.AddComponent<BossBattleUIController>();
            Debug.Log("[BossBattleUI] GameScene 메인 Canvas에 보스전 실시간 HUD 설치 완료");
        }
    }
}
