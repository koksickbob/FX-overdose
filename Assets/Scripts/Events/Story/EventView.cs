using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FXOverdose.DatingSim.Core;
using FXOverdose.DatingSim.Dialogue;

namespace FXOverdose.Events.Story
{
    /// <summary>
    /// 이벤트 화면 <b>한 벌</b>. 오버레이와 전용 씬이 이것을 공유합니다.
    ///
    /// 위젯 트리를 런타임에 만드는 이유: 두 호스트가 같은 코드로 같은 화면을 만들면
    /// "둘이 갈라지지 않았는가"를 검증할 필요 자체가 없어집니다(계획 R12).
    /// 씬에 프리팹을 배치하면 빌더 실행 시점에 따라 조용히 어긋납니다.
    ///
    /// <para>⚠️ 모든 시간 연출은 <b>unscaled</b>입니다. 설정 메뉴가 <c>Time.timeScale = 0</c>을 걸기 때문에
    /// (<c>SettingsMenuController.OpenMenu</c>) 스케일드 시간을 쓰면 설정을 한 번 열었다 닫는 것만으로
    /// 타자기가 영구히 멈춥니다.</para>
    /// </summary>
    public sealed class EventView : MonoBehaviour
    {
        private const int MaxChoiceButtons = 4;

        private static readonly Color32 Ink = new Color32(233, 242, 255, 255);
        private static readonly Color32 PanelFill = new Color32(9, 16, 30, 235);
        private static readonly Color32 Accent = new Color32(34, 211, 238, 255);
        private static readonly Color32 YomiName = new Color32(244, 114, 182, 255);

        private EventRunner runner;
        private Action onFinished;
        private string commitReturnScene;

        private Image backdrop;
        private Image background;
        private Image standing;
        private TMP_Text speakerLabel;
        private TMP_Text bodyLabel;
        private Button clickCatcher;
        private RectTransform choicePanel;
        private readonly Button[] choiceButtons = new Button[MaxChoiceButtons];
        private readonly TMP_Text[] choiceLabels = new TMP_Text[MaxChoiceButtons];
        private GameObject logPanel;
        private RectTransform logContent;

        private bool clickPending;
        private bool logOpen;
        private Coroutine playRoutine;

        /// <summary>
        /// 화면을 만들고 진행을 시작합니다.
        /// </summary>
        /// <param name="isOverlay">true면 밑 화면을 덮는 불투명 백드롭을 켭니다.</param>
        /// <param name="returnScene">전용 씬 호스트만 넘깁니다. 커밋 때 LastSceneName에 적힙니다. (계획 7.5절)</param>
        /// <param name="finishedCallback">커밋까지 끝난 뒤 호출됩니다. 호스트가 뒷정리를 합니다.</param>
        public void Begin(EventRunner eventRunner, bool isOverlay, string returnScene, Action finishedCallback)
        {
            runner = eventRunner;
            onFinished = finishedCallback;
            commitReturnScene = returnScene;

            BuildTree(isOverlay);

            // 이벤트 진행 중에는 저장이 금지되므로 설정 창에서 저장·업적을 숨깁니다. (계획 8.1절)
            SettingsMenuController.RestrictedLayoutRequested = true;

            if (runner == null || runner.IsFinished)
            {
                Finish();
                return;
            }

            playRoutine = StartCoroutine(PlayAll());
        }

        private void OnDestroy()
        {
            // 이벤트가 어떤 경로로 죽든 제한 레이아웃은 반드시 내려갑니다.
            // 세운 쪽이 되돌리지 않으면 이후 모든 화면에서 저장 버튼이 사라집니다.
            SettingsMenuController.RestrictedLayoutRequested = false;
        }

        // ── 진행 ──────────────────────────────────────────────────────

        private IEnumerator PlayAll()
        {
            while (!runner.IsFinished)
            {
                yield return PlayNode(runner.CurrentNode);

                // 선택지 노드는 버튼이 눌릴 때까지 여기서 끝납니다. 이어지는 진행은 OnChoiceClicked가 잡습니다.
                if (runner.Phase == EventPhase.Choice) yield break;

                if (!runner.Advance()) break;
            }

            Finish();
        }

        private IEnumerator PlayNode(EventNode node)
        {
            ApplyVisuals(node);

            string[] lines = node.Lines ?? Array.Empty<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                yield return ShowLine(node.Speaker, lines[i]);

                bool lastLine = i == lines.Length - 1;
                if (lastLine) break;

                yield return WaitForClick();
            }

            runner.MarkRevealed();

            if (runner.Phase == EventPhase.Choice)
            {
                // 마지막 글자와 동시에 버튼이 튀어나오지 않게 한 박자 둡니다. (D-5)
                yield return WaitUnscaled(DialogueTypewriter.ChoiceDelay);
                ShowChoices(node.Choices);
                yield break;
            }

            yield return WaitForClick();
        }

        /// <summary>한 줄을 출력합니다. 지문("※")은 타자기를 쓰지 않습니다. (D-4)</summary>
        private IEnumerator ShowLine(string speaker, string line)
        {
            bool narration = TalkNode.IsNarration(line);
            string shown = narration ? TalkNode.StripMark(line) : line;

            if (speakerLabel != null)
            {
                speakerLabel.text = narration || string.IsNullOrEmpty(speaker) ? string.Empty : speaker;
                speakerLabel.color = speaker == "요미" ? (Color)YomiName : (Color)Ink;
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = shown;
                bodyLabel.maxVisibleCharacters = int.MaxValue;
            }

            runner.RecordLine(narration ? null : speaker, shown);

            if (narration)
            {
                yield return WaitUnscaled(DialogueTypewriter.NarrationTail);
                yield break;
            }

            yield return DialogueTypewriter.TypeLine(bodyLabel, shown, ConsumeClick, useUnscaledTime: true);
        }

        private void ApplyVisuals(EventNode node)
        {
            if (background != null && !string.IsNullOrEmpty(node.BackgroundId))
            {
                Sprite sprite = Resources.Load<Sprite>($"DatingSim/Events/{node.BackgroundId}");
                if (sprite != null)
                {
                    background.sprite = sprite;
                    background.color = Color.white;
                }
                else
                {
                    // 아트가 아직 없어도 진행은 막지 않습니다. 플레이스홀더로 계속 갑니다.
                    Debug.LogWarning($"[EventView] 배경 '{node.BackgroundId}'을(를) 찾지 못했습니다. 플레이스홀더로 진행합니다.");
                }
            }

            if (standing == null) return;

            bool showStanding = !runner.Definition.HideStanding;
            standing.gameObject.SetActive(showStanding);
            if (showStanding) ApplyEmotion(node.Emotion);
        }

        /// <summary>
        /// 호감도 구간 × 감정으로 스탠딩 스프라이트를 갈아 끼웁니다.
        ///
        /// <para>⚠️ <c>Resources.Load&lt;Sprite&gt;</c>가 아니라 <c>LoadAll</c>인 이유: 감정 PNG들이
        /// <b>Multiple 스프라이트 모드</b>로 임포트돼 있어(시트당 서브 스프라이트 1장) 메인 에셋이 Texture2D입니다.
        /// <c>Load&lt;Sprite&gt;</c>는 여기서 null을 돌려줍니다. LoadAll은 Single/Multiple 양쪽 다 동작합니다.</para>
        /// </summary>
        private void ApplyEmotion(EventEmotion emotion)
        {
            string tier = AffectionTierFolder();
            Sprite sprite = LoadEmotionSprite(tier, emotion.ToString())
                            ?? LoadEmotionSprite(tier, nameof(EventEmotion.Calm));

            if (sprite == null)
            {
                // 아트가 없어도 진행은 막지 않습니다. 배경과 같은 방침입니다.
                standing.color = new Color(1f, 1f, 1f, 0f);
                Debug.LogWarning($"[EventView] 감정 스프라이트 '{tier}/{emotion}'을(를) 찾지 못했습니다. 스탠딩 없이 진행합니다.");
                return;
            }

            standing.sprite = sprite;
            standing.color = Color.white;
        }

        private static Sprite LoadEmotionSprite(string tier, string emotionName)
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>($"DatingSim/Emotions/Sprites/{tier}/{emotionName}");
            return loaded != null && loaded.Length > 0 ? loaded[0] : null;
        }

        /// <summary>
        /// 호감도 → T1~T4. 경계의 단일 진실 원천은 <c>docs/P2_04_System/Affection_Tier_Table.md</c>이고,
        /// <b>피크가 아니라 현재치</b>로 판정합니다 — 호감도가 깎였는데 T4 표정이 나오면 거짓말이 됩니다.
        /// </summary>
        private static string AffectionTierFolder()
        {
            // 씬 단독 재생(계획 7.6절)에서는 매니저가 없습니다. 그때는 T1으로 떨어집니다.
            int affection = DatingTimeManager.Instance != null ? DatingTimeManager.Instance.CurrentAffection : 0;

            if (affection >= 91) return "T4";
            if (affection >= 61) return "T3";
            if (affection >= 31) return "T2";
            return "T1";
        }

        // ── 입력 ──────────────────────────────────────────────────────

        /// <summary>
        /// 클릭 1회를 소비합니다. 타자기의 스킵 판정이자 줄 넘김 판정입니다.
        /// <b>한 번의 클릭이 두 곳에서 소비되지 않도록</b> 읽으면서 지웁니다.
        /// </summary>
        private bool ConsumeClick()
        {
            if (!clickPending) return false;
            clickPending = false;
            return true;
        }

        private IEnumerator WaitForClick()
        {
            clickPending = false;
            while (!ConsumeClick()) yield return null;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float waited = 0f;
            while (waited < seconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void Update()
        {
            // ClickCatcher 버튼과 별개로 물리 클릭도 받습니다. 버튼만 쓰면 로그 패널을 닫는 클릭이
            // 그대로 다음 대사까지 넘겨 버립니다.
            if (runner == null || runner.Phase == EventPhase.Choice || logOpen) return;
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame) clickPending = true;
        }

        // ── 선택지 ────────────────────────────────────────────────────

        private void ShowChoices(EventChoice[] choices)
        {
            // ⚠️ 선택지가 뜨는 순간 ClickCatcher를 반드시 끕니다. 켜 둔 채로는 선택지를 누르려는 클릭이
            //    먼저 대사 넘김으로 먹혀 "선택하려는데 이야기가 진행되는" 고전적 버그가 납니다. (계획 7.7절)
            SetClickCatcherActive(false);
            if (choicePanel != null) choicePanel.gameObject.SetActive(true);

            for (int i = 0; i < MaxChoiceButtons; i++)
            {
                bool used = choices != null && i < choices.Length;
                if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(used);
                if (used && choiceLabels[i] != null) choiceLabels[i].text = choices[i].Text;
            }
        }

        private void HideChoices()
        {
            if (choicePanel != null) choicePanel.gameObject.SetActive(false);
            for (int i = 0; i < MaxChoiceButtons; i++)
            {
                if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(false);
            }
            SetClickCatcherActive(true);
        }

        private void OnChoiceClicked(int index)
        {
            if (runner == null || runner.Phase != EventPhase.Choice) return;

            string reply = runner.SelectChoice(index);
            HideChoices();

            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(ContinueAfterChoice(reply));
        }

        /// <summary>고른 선택지에 요미가 먼저 반응하고, 그 다음에 분기된 노드로 갑니다. (R-2)</summary>
        private IEnumerator ContinueAfterChoice(string reply)
        {
            if (!string.IsNullOrEmpty(reply))
            {
                yield return ShowLine("요미", reply);
                yield return WaitForClick();
            }

            yield return PlayAll();
        }

        private void SetClickCatcherActive(bool active)
        {
            if (clickCatcher != null) clickCatcher.gameObject.SetActive(active && !logOpen);
        }

        // ── 로그 ──────────────────────────────────────────────────────

        private void ToggleLog()
        {
            logOpen = !logOpen;
            if (logPanel != null) logPanel.SetActive(logOpen);

            // 로그가 열려 있는 동안은 진행 입력을 통째로 막습니다.
            if (logOpen) SetClickCatcherActive(false);
            else if (runner != null && runner.Phase != EventPhase.Choice) SetClickCatcherActive(true);

            if (logOpen) RebuildLog();
        }

        private void RebuildLog()
        {
            if (logContent == null) return;

            for (int i = logContent.childCount - 1; i >= 0; i--)
                Destroy(logContent.GetChild(i).gameObject);

            IReadOnlyList<(string Speaker, string Line)> backlog = runner.Backlog;
            for (int i = 0; i < backlog.Count; i++)
            {
                string speaker = backlog[i].Speaker;
                string text = string.IsNullOrEmpty(speaker) ? backlog[i].Line : $"{speaker}: {backlog[i].Line}";

                TMP_Text entry = CreateText(logContent, $"Entry{i}", text, 22f, TextAlignmentOptions.TopLeft);
                entry.color = string.IsNullOrEmpty(speaker) ? new Color32(150, 168, 190, 255) : (Color)Ink;

                var layout = entry.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = 30f;
                entry.rectTransform.anchorMin = Vector2.zero;
                entry.rectTransform.anchorMax = Vector2.one;
            }
        }

        // ── 종료 ──────────────────────────────────────────────────────

        private void Finish()
        {
            // 커밋은 여기 딱 한 번입니다. 진행 중에는 어떤 경로로도 저장이 일어나지 않습니다. (계획 6.2절)
            runner?.Commit(commitReturnScene);

            SettingsMenuController.RestrictedLayoutRequested = false;

            Action callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        }

        // ── 위젯 트리 ─────────────────────────────────────────────────

        private void BuildTree(bool isOverlay)
        {
            RectTransform root = GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            Stretch(root);

            backdrop = CreateImage(root, "Backdrop", new Color32(4, 9, 18, 255));
            backdrop.raycastTarget = isOverlay; // 오버레이는 밑 씬의 클릭을 반드시 막습니다 (계획 7.7절)
            backdrop.gameObject.SetActive(true);

            background = CreateImage(root, "Background", new Color32(12, 20, 36, 255));
            background.raycastTarget = false;
            background.preserveAspect = false;

            standing = CreateImage(root, "YomiStanding", new Color(1f, 1f, 1f, 0f));
            standing.raycastTarget = false;
            standing.preserveAspect = true;
            Stretch(standing.rectTransform, new Vector2(0.30f, 0.06f), new Vector2(0.70f, 0.98f));

            // ClickCatcher는 대사창보다 <b>먼저</b> 붙습니다. 뒤에 붙이면 선택지·로그 버튼을 덮습니다.
            clickCatcher = CreateButton(root, "ClickCatcher", null, Vector2.zero, Vector2.one, Color.clear);
            clickCatcher.onClick.AddListener(() => clickPending = true);

            BuildDialoguePanel(root);
            BuildChoicePanel(root);
            BuildLogPanel(root);
            BuildTopButtons(root);
        }

        private void BuildDialoguePanel(RectTransform root)
        {
            RectTransform panel = CreatePanel(root, "DialoguePanel",
                new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.30f), PanelFill);

            speakerLabel = CreateText(panel, "Speaker", string.Empty, 26f, TextAlignmentOptions.MidlineLeft);
            Stretch(speakerLabel.rectTransform, new Vector2(0.03f, 0.74f), new Vector2(0.6f, 0.98f));
            speakerLabel.fontStyle = FontStyles.Bold;

            bodyLabel = CreateText(panel, "Body", string.Empty, 28f, TextAlignmentOptions.TopLeft);
            Stretch(bodyLabel.rectTransform, new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.72f));
            bodyLabel.color = Ink;
            // ⚠️ 자동 크기 조절을 끕니다. 켜면 글자가 늘어날 때마다 폰트 크기가 흔들려 읽기가 어려워집니다.
            bodyLabel.enableAutoSizing = false;
        }

        private void BuildChoicePanel(RectTransform root)
        {
            choicePanel = CreatePanel(root, "ChoicePanel",
                new Vector2(0.18f, 0.34f), new Vector2(0.82f, 0.76f), new Color(0f, 0f, 0f, 0f));
            choicePanel.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0f);

            for (int i = 0; i < MaxChoiceButtons; i++)
            {
                float top = 1f - i * 0.25f;
                choiceButtons[i] = CreateButton(choicePanel, $"Choice{i}", "-",
                    new Vector2(0f, top - 0.22f), new Vector2(1f, top), Accent);
                choiceLabels[i] = choiceButtons[i].GetComponentInChildren<TMP_Text>();

                int index = i; // 클로저 캡처 주의
                choiceButtons[i].onClick.AddListener(() => OnChoiceClicked(index));
                choiceButtons[i].gameObject.SetActive(false);
            }

            choicePanel.gameObject.SetActive(false);
        }

        private void BuildLogPanel(RectTransform root)
        {
            RectTransform panel = CreatePanel(root, "LogPanel",
                new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.92f), new Color32(6, 11, 22, 250));
            logPanel = panel.gameObject;

            CreateText(panel, "Title", "대사 기록", 30f, TextAlignmentOptions.Center)
                .rectTransform.anchorMin = new Vector2(0.05f, 0.90f);

            GameObject viewport = CreateUIObject("Viewport", panel, typeof(Image), typeof(Mask));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.88f));
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = CreateUIObject("Content", viewportRect.transform);
            logContent = content.GetComponent<RectTransform>();
            logContent.anchorMin = new Vector2(0f, 1f);
            logContent.anchorMax = new Vector2(1f, 1f);
            logContent.pivot = new Vector2(0.5f, 1f);

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
            layout.padding = new RectOffset(12, 12, 12, 12);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = logContent;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;

            Button close = CreateButton(panel, "CloseLog", "닫기",
                new Vector2(0.36f, 0.03f), new Vector2(0.64f, 0.12f), Accent);
            close.onClick.AddListener(ToggleLog);

            logPanel.SetActive(false);
        }

        private void BuildTopButtons(RectTransform root)
        {
            Button log = CreateButton(root, "LogButton", "기록",
                new Vector2(0.80f, 0.93f), new Vector2(0.88f, 0.985f), Accent);
            log.onClick.AddListener(ToggleLog);

            // 설정 메뉴는 기존 컨트롤러를 그대로 씁니다. 비활성 상태에서 컴포넌트를 붙여야
            // Awake가 트레이딩 전용 AUTO/USER 버튼을 만들지 않습니다 (요미 방 빌더와 같은 순서).
            Button settings = CreateButton(root, "SettingsButton", "설정",
                new Vector2(0.895f, 0.93f), new Vector2(0.975f, 0.985f), Accent);
            settings.gameObject.SetActive(false);
            SettingsMenuController controller = settings.gameObject.AddComponent<SettingsMenuController>();
            controller.ConfigureRoomButton(settings);
            settings.gameObject.SetActive(true);
        }

        // ── UI 헬퍼 ───────────────────────────────────────────────────

        private static GameObject CreateUIObject(string name, Transform parent, params Type[] extra)
        {
            var types = new List<Type> { typeof(RectTransform), typeof(CanvasRenderer) };
            types.AddRange(extra);

            GameObject go = new GameObject(name, types.ToArray());
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            return go;
        }

        private static void Stretch(RectTransform rect) => Stretch(rect, Vector2.zero, Vector2.one);

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject go = CreateUIObject(name, parent, typeof(Image));
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = CreateUIObject(name, parent, typeof(Image), typeof(Outline));
            RectTransform rect = go.GetComponent<RectTransform>();
            Stretch(rect, min, max);
            go.GetComponent<Image>().color = color;

            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color32(35, 73, 99, 255);
            outline.effectDistance = new Vector2(2f, -2f);
            return rect;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, float size,
            TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Ink;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label,
            Vector2 min, Vector2 max, Color accent)
        {
            RectTransform panel = CreatePanel(parent, name, min, max,
                label == null ? Color.clear : new Color32(17, 31, 51, 245));

            Button button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            panel.GetComponent<Outline>().effectColor = accent;

            if (label != null)
            {
                TMP_Text text = CreateText(panel, "Label", label, 24f, TextAlignmentOptions.Center);
                text.fontStyle = FontStyles.Bold;
            }

            return button;
        }
    }
}
