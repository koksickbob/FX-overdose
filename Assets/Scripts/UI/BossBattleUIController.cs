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
        private const float FallbackTopOffset = 65f;
        private const float RightMargin = UIStrokeStyle.ScreenEdgeMargin;
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
        private RectTransform characterLevelHudRect;
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
        private RectTransform entranceRoot;
        private RectTransform entranceBand;
        private RectTransform entranceVsBadge;
        private CanvasGroup entranceGroup;
        private TMP_Text entranceDay;
        private TMP_Text entranceBossName;
        private TMP_Text entranceDescription;
        private BossData displayedBoss;
        private bool subscribed;
        private bool bankruptAnimationPlayed;
        private Coroutine bankruptRoutine;
        private Coroutine entranceRoutine;

        private void Awake()
        {
            gameManager = GameManager.Instance;
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
            while (this != null && bossManager == null)
            {
                BossManager existing = FindAnyObjectByType<BossManager>(FindObjectsInactive.Include);
                if (existing != null)
                {
                    BindManager(existing);
                    break;
                }

                yield return null;
            }

            RefreshFromManager();
        }

        private void Update()
        {
            if (gameManager == null)
                gameManager = GameManager.Instance;

            BossManager currentManager = bossManager != null
                ? bossManager
                : FindAnyObjectByType<BossManager>(FindObjectsInactive.Include);
            if (currentManager != bossManager)
                BindManager(currentManager);

            BossData currentBoss = bossManager != null ? bossManager.CurrentBoss : null;
            if (currentBoss != displayedBoss)
                RefreshFromManager();

            AlignWithCharacterLevelHud();

            if (liveDot != null && panelRect != null && panelRect.gameObject.activeSelf &&
                bossManager != null && !bossManager.IsBossBankrupt)
            {
                float pulse = 0.68f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.32f;
                Color color = Cyan;
                color.a = pulse;
                liveDot.color = color;
            }

            UpdateLiveAssetDisplay();
        }

        private void UpdateLiveAssetDisplay()
        {
            if (bossManager == null || bossManager.CurrentBoss == null) return;
            if (bossManager.IsBossBankrupt || bankruptAnimationPlayed) return;

            float startingAsset = bossManager.BossStartingAsset;
            float currentRealizedAsset = bossManager.BossCurrentAsset;
            
            bool isHolding = false;
            float unrealizedPnL = 0f;
            string posTypeString = "";
            
            if (bossManager.CurrentBossAI != null && bossManager.CurrentBossAI.IsHoldingPosition)
            {
                isHolding = true;
                unrealizedPnL = bossManager.CurrentBossAI.CurrentRealTimePnL;
                posTypeString = bossManager.CurrentBossAI.CurrentPositionType == FXOverdose.Trading.TradingController.PositionType.Long ? "LONG" : "SHORT";
            }

            float totalCurrentAsset = currentRealizedAsset + unrealizedPnL;
            float totalReturnRate = startingAsset > 0f
                ? ((totalCurrentAsset - startingAsset) / startingAsset) * 100f
                : 0f;

            float remainingRatio = startingAsset > 0f
                ? Mathf.Clamp01(totalCurrentAsset / startingAsset)
                : 0f;

            assetValue.text = $"${Mathf.Max(0f, totalCurrentAsset):N0}";
            returnValue.text = $"{(totalReturnRate >= 0f ? "+" : string.Empty)}{totalReturnRate:F2}%";
            returnValue.color = totalReturnRate >= 0f ? Green : Red;
            
            assetFill.fillAmount = remainingRatio;
            assetFill.color = remainingRatio > 0.55f ? Gold :
                remainingRatio > 0.25f ? new Color32(249, 115, 22, 255) : Red;

            if (isHolding)
            {
                statusValue.text = $"{posTypeString} POS";
                statusValue.color = posTypeString == "LONG" ? Green : Red;
            }
            else
            {
                statusValue.text = "WAITING";
                statusValue.color = Muted;
            }

            // 너무 빈번한 텍스트 갱신 렌더링 호출을 방지하기 위해 내용이 바뀔 때만 Refresh하면 좋지만,
            // 실시간 갱신이므로 Update마다 RefreshTextMeshes를 호출합니다.
            RefreshTextMeshes();
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
            // CurrentBoss는 실제 스폰 성공 여부를 나타내는 권위 상태입니다.
            // 비동기 씬 로딩 중 GameManager의 일차 복구보다 보스 스폰 이벤트가 먼저 오더라도
            // 이미 생성된 보스 HUD를 다시 숨기지 않습니다.
            bool isBossDay = boss != null;

            bool shouldPlayEntrance = boss != null && boss != displayedBoss;
            displayedBoss = boss;
            SetVisible(isBossDay);
            if (!isBossDay) return;

            dayBadge.text = $"{BossDateText(boss)}  /  BOSS";
            bossName.text = boss.IsFinalBoss ? $"FINAL · {boss.Name}" : boss.Name;
            bossDescription.text = boss.Description;
            bankruptAnimationPlayed = false;
            bankruptStamp.SetActive(false);
            HandleBossAssetChanged(bossManager.BossCurrentAsset, bossManager.BossStartingAsset);

            if (bossManager.IsBossBankrupt)
                ShowBankruptState(false);
            else if (shouldPlayEntrance)
                PlayBossEntrance(boss);
        }

        /// <summary>
        /// 보스 배지는 '현재 날짜'가 아니라 <b>예정된 일차</b>를 표시하므로 서수를 날짜로 환산합니다.
        /// 현재 날짜를 쓰면 위(204행)에 적힌 순서 문제 — 일차 복구보다 보스 스폰이 먼저 오는 경우 —
        /// 에 걸려 엉뚱한 날짜가 찍힙니다.
        /// </summary>
        private string BossDateText(BossData boss)
        {
            System.DateTime date = gameManager != null
                ? gameManager.DateForDay(boss.Day)
                : FXOverdose.Core.GameCalendar.DefaultStartDate.AddDays(Mathf.Max(1, boss.Day) - 1);
            return FXOverdose.Core.GameCalendar.ToKoreanShort(date);
        }

        private void PlayBossEntrance(BossData boss)
        {
            if (entranceRoot == null || boss == null) return;

            entranceDay.text = $"{BossDateText(boss)} · BOSS DETECTED";
            entranceBossName.text = boss.IsFinalBoss ? $"FINAL BOSS  /  {boss.Name}" : boss.Name;
            entranceDescription.text = boss.Description;
            RefreshTextMeshes();

            if (entranceRoutine != null)
                StopCoroutine(entranceRoutine);
            entranceRoutine = StartCoroutine(AnimateBossEntrance());
            
            TriggerYomiBossDialogue(boss);
        }

        private void TriggerYomiBossDialogue(BossData boss)
        {
            var aiVisual = FindAnyObjectByType<FXOverdose.AI.AIVisualController>(FindObjectsInactive.Include);
            if (aiVisual != null)
            {
                string dialogue = boss.Day switch
                {
                    3 => "“저, 저 인간은...! 내 알바비 떼먹고 도망간 그 악덕 편의점 사장!! 오빠, 저 자식 돈 다 털어버려!!”",
                    6 => "“앗... 저 사람, 나한테 맨날 컵 닦으라고 소리 지르던 카페 사장이잖아! 으으... 오빠가 혼내줘!!”",
                    9 => "“뭐야, 야간 수당도 안 주고 도망갔던 PC방 사장이잖아?! 저런 놈은 시장에서 퇴출당해야 해! 가즈아!!”",
                    12 => "“윽... 불판 닦다가 손 덴 거 생각나네... 저 고깃집 사장, 오빠 실력으로 완전히 숯덩이로 만들어버려!”",
                    15 => "“콜센터 사장...? 나한테 말도 안 되는 진상 손님들 다 떠넘기더니! 오빠, 저 사람 멘탈 좀 박살내줘!!”",
                    18 => "“어라? 내 외모 지적하던 그 매장 사장이네? 오늘 오빠의 매매 실력으로 누가 진짜 강자인지 보여주자구!”",
                    20 => "“드, 드디어 이 날이 왔어... 우리를 이렇게 괴롭혔던 모든 빚의 원흉... 오빠...! 이번엔 절대 물러설 수 없어! 요미도 모든 걸 걸게!!”",
                    _ => $"“{boss.Name} 등장!! 오빠, 우리 실력을 보여주자!!”"
                };
                
                aiVisual.DisplayDialogueBalloon(dialogue, FXOverdose.AI.DialoguePriority.Critical, FXOverdose.AI.EventCategory.GimmickTriggered);
            }
        }

        private IEnumerator AnimateBossEntrance()
        {
            entranceRoot.gameObject.SetActive(true);
            entranceRoot.SetAsLastSibling();
            entranceGroup.alpha = 0f;
            entranceBand.anchoredPosition = new Vector2(-760f, 0f);
            entranceVsBadge.localScale = Vector3.one * 2.2f;

            const float revealDuration = 0.42f;
            float elapsed = 0f;
            while (elapsed < revealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / revealDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                entranceGroup.alpha = Mathf.Clamp01(t * 2.2f);
                entranceBand.anchoredPosition = Vector2.LerpUnclamped(
                    new Vector2(-760f, 0f), Vector2.zero, eased);
                entranceVsBadge.localScale = Vector3.one *
                    Mathf.Lerp(2.2f, 1f, 1f - Mathf.Pow(1f - t, 4f));
                yield return null;
            }

            entranceGroup.alpha = 1f;
            entranceBand.anchoredPosition = Vector2.zero;
            entranceVsBadge.localScale = Vector3.one;

            const float holdDuration = 1.25f;
            elapsed = 0f;
            while (elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 11f) * 0.035f;
                entranceVsBadge.localScale = Vector3.one * pulse;
                yield return null;
            }

            const float exitDuration = 0.34f;
            elapsed = 0f;
            while (elapsed < exitDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / exitDuration);
                entranceGroup.alpha = 1f - t;
                entranceBand.anchoredPosition = Vector2.Lerp(
                    Vector2.zero, new Vector2(760f, 0f), t * t);
                yield return null;
            }

            entranceRoot.gameObject.SetActive(false);
            entranceRoutine = null;
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

            if (bossManager != null && bossManager.IsBossBankrupt)
            {
                ShowBankruptState(true);
            }
            
            UpdateLiveAssetDisplay();
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

        private void AlignWithCharacterLevelHud()
        {
            if (panelRect == null) return;

            if (characterLevelHudRect == null)
            {
                GameObject levelHud = GameObject.Find("CharacterLevelExpHUD");
                characterLevelHudRect = levelHud != null ? levelHud.GetComponent<RectTransform>() : null;
            }

            RectTransform parentRect = panelRect.parent as RectTransform;
            if (characterLevelHudRect == null || parentRect == null) return;

            Vector3[] levelCorners = new Vector3[4];
            characterLevelHudRect.GetWorldCorners(levelCorners);
            Vector3 levelTopRight = parentRect.InverseTransformPoint(levelCorners[2]);

            panelRect.anchoredPosition = new Vector2(
                -RightMargin,
                levelTopRight.y - parentRect.rect.yMax);
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
                -FallbackTopOffset);

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

            dayBadge = CreateText(header.transform, "DayBadge", "0월 0일  /  BOSS",
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
            BuildEntranceUI();
        }

        private void BuildEntranceUI()
        {
            GameObject root = new("BossEntranceOverlay",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            entranceRoot = root.GetComponent<RectTransform>();
            Stretch(entranceRoot);

            Image dim = root.GetComponent<Image>();
            dim.color = new Color32(2, 8, 18, 226);
            dim.raycastTarget = false;
            entranceGroup = root.GetComponent<CanvasGroup>();
            entranceGroup.interactable = false;
            entranceGroup.blocksRaycasts = false;

            Image topLine = CreateImage(entranceRoot, "TopWarningLine", Red);
            SetRect(topLine.rectTransform, new Vector2(0f, 0.72f), new Vector2(1f, 0.72f),
                new Vector2(0f, -3f), new Vector2(0f, 3f));
            Image bottomLine = CreateImage(entranceRoot, "BottomWarningLine", Red);
            SetRect(bottomLine.rectTransform, new Vector2(0f, 0.28f), new Vector2(1f, 0.28f),
                new Vector2(0f, -3f), new Vector2(0f, 3f));

            Image band = CreateImage(entranceRoot, "WarningBand", new Color32(10, 25, 42, 250));
            entranceBand = band.rectTransform;
            SetRect(entranceBand, new Vector2(0f, 0.28f), new Vector2(1f, 0.72f),
                new Vector2(120f, 0f), new Vector2(-120f, 0f));
            Outline bandOutline = band.gameObject.AddComponent<Outline>();
            bandOutline.effectColor = new Color32(239, 68, 68, 210);
            bandOutline.effectDistance = new Vector2(5f, -5f);

            entranceDay = CreateText(entranceBand, "DetectedLabel", "0월 0일 · BOSS DETECTED",
                25f, Red, TextAlignmentOptions.Center);
            entranceDay.fontStyle = FontStyles.Bold;
            SetRect(entranceDay.rectTransform, new Vector2(0f, 1f), Vector2.one,
                new Vector2(36f, -64f), new Vector2(-36f, -18f));

            entranceBossName = CreateText(entranceBand, "EntranceBossName", "BOSS",
                66f, Text, TextAlignmentOptions.Center);
            entranceBossName.fontStyle = FontStyles.Bold;
            SetRect(entranceBossName.rectTransform, new Vector2(0f, 0.28f), new Vector2(1f, 0.78f),
                new Vector2(150f, 0f), new Vector2(-150f, 0f));

            entranceDescription = CreateText(entranceBand, "EntranceDescription", string.Empty,
                19f, Muted, TextAlignmentOptions.Center);
            SetRect(entranceDescription.rectTransform, Vector2.zero, new Vector2(1f, 0.28f),
                new Vector2(60f, 12f), new Vector2(-60f, 0f));

            GameObject vsObject = new("VsBadge",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            vsObject.transform.SetParent(entranceBand, false);
            entranceVsBadge = vsObject.GetComponent<RectTransform>();
            entranceVsBadge.anchorMin = entranceVsBadge.anchorMax = new Vector2(0.5f, 0.5f);
            entranceVsBadge.pivot = new Vector2(0.5f, 0.5f);
            entranceVsBadge.sizeDelta = new Vector2(104f, 104f);
            entranceVsBadge.anchoredPosition = new Vector2(-470f, 0f);
            Image vsBackground = vsObject.GetComponent<Image>();
            vsBackground.color = new Color32(127, 29, 29, 255);
            vsBackground.raycastTarget = false;
            Outline vsOutline = vsObject.GetComponent<Outline>();
            vsOutline.effectColor = Gold;
            vsOutline.effectDistance = new Vector2(4f, -4f);

            TMP_Text vsText = CreateText(entranceVsBadge, "VsText", "VS",
                43f, Color.white, TextAlignmentOptions.Center);
            vsText.fontStyle = FontStyles.Bold;
            Stretch(vsText.rectTransform);

            entranceRoot.gameObject.SetActive(false);
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
            GlobalPFStardustFont.RefreshCompactHudText(entranceDay);
            GlobalPFStardustFont.RefreshCompactHudText(entranceBossName);
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
            EnsureInstalled(scene);
        }

        /// <summary>
        /// 씬 로드 콜백과 보스 스폰 양쪽에서 호출할 수 있는 멱등 설치 진입점입니다.
        /// Endless의 비동기 로딩 순서에서도 HUD가 반드시 준비되도록 합니다.
        /// </summary>
        public static void EnsureInstalled(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != "GameScene") return;

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
