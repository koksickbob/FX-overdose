using System.Collections;
using System.Collections.Generic;
using FXOverdose.Trading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FXOverdose.Core
{
    public enum AudioCue
    {
        UiClick,
        LongManual,
        LongAuto,
        ShortManual,
        ShortAuto,
        Profit,
        Loss,
        EventAppear,
        TraderLevelUp,
        SkillLevelUp,
        EnergyDrink,
        Dessert,
        Supplement,
        Sedative,
        ShopPurchase,
        CostumePurchase,
        CostumeEquip,
        InsufficientBalance,
        MentalUp,
        MentalDown,
        HealthUp,
        HealthDown,
        SettlementProfit,
        SettlementNonProfit,
        Overdose,
        GameOver
    }

    /// <summary>
    /// Resources/Audio에 약속된 이름의 WAV를 넣으면 자동으로 게임 이벤트에 연결되는 전역 오디오 서비스입니다.
    /// 씬에 배치하지 않아도 런타임에 자동 생성됩니다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class AudioManager : MonoBehaviour
    {
        private string currentNormalBgmPath = "Audio/BGM/bgm_night";
        private const string OverdoseBgmPath = "Audio/BGM/bgm_overdose";
        private const string ProfitLayerPath = "Audio/BGM/layer_profit";
        private const string DangerLayerPath = "Audio/BGM/layer_danger";
        private const string TitleBgmPath = "Audio/BGM/bgm_title";

        private static readonly Dictionary<AudioCue, string> CuePaths = new()
        {
            { AudioCue.UiClick, "Audio/SFX/UI/ui_click" },
            { AudioCue.LongManual, "Audio/SFX/Trading/long_manual" },
            { AudioCue.LongAuto, "Audio/SFX/Trading/long_auto" },
            { AudioCue.ShortManual, "Audio/SFX/Trading/short_manual" },
            { AudioCue.ShortAuto, "Audio/SFX/Trading/short_auto" },
            { AudioCue.Profit, "Audio/SFX/Trading/profit" },
            { AudioCue.Loss, "Audio/SFX/Trading/loss" },
            { AudioCue.EventAppear, "Audio/SFX/Event/event_appear" },
            { AudioCue.TraderLevelUp, "Audio/SFX/Growth/trader_level_up" },
            { AudioCue.SkillLevelUp, "Audio/SFX/Growth/skill_level_up" },
            { AudioCue.EnergyDrink, "Audio/SFX/Item/energy_drink" },
            { AudioCue.Dessert, "Audio/SFX/Item/dessert" },
            { AudioCue.Supplement, "Audio/SFX/Item/supplement" },
            { AudioCue.Sedative, "Audio/SFX/Item/sedative" },
            { AudioCue.ShopPurchase, "Audio/SFX/Shop/item_purchase" },
            { AudioCue.CostumePurchase, "Audio/SFX/Shop/costume_purchase" },
            { AudioCue.CostumeEquip, "Audio/SFX/Shop/costume_equip" },
            { AudioCue.InsufficientBalance, "Audio/SFX/UI/insufficient_balance" },
            { AudioCue.MentalUp, "Audio/SFX/Status/mental_up" },
            { AudioCue.MentalDown, "Audio/SFX/Status/mental_down" },
            { AudioCue.HealthUp, "Audio/SFX/Status/health_up" },
            { AudioCue.HealthDown, "Audio/SFX/Status/health_down" },
            { AudioCue.SettlementProfit, "Audio/SFX/Settlement/profit" },
            { AudioCue.SettlementNonProfit, "Audio/SFX/Settlement/non_profit" },
            { AudioCue.Overdose, "Audio/SFX/State/overdose" },
            { AudioCue.GameOver, "Audio/SFX/State/game_over" }
        };

        public static AudioManager Instance { get; private set; }

        [Header("볼륨 설정")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 0.7f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        [SerializeField, Min(4)] private int sfxPoolSize = 12;
        [SerializeField, Range(0f, 1f)] private float duckedBgmRatio = 0.35f;

        private readonly Dictionary<string, AudioClip> clipCache = new();
        private readonly HashSet<string> missingClipPaths = new();
        private readonly List<AudioSource> sfxPool = new();
        private AudioSource bgmSource;
        private AudioSource profitLayerSource;
        private AudioSource dangerLayerSource;
        private Coroutine bgmFadeRoutine;
        private Coroutine duckRoutine;
        private TradingController boundTrading;
        private TraderLevelSystem boundLevelSystem;
        private TraderStatus boundStatus;
        private Inventory boundInventory;
        private bool overdoseActive;
        private float targetProfitLayer;
        private float targetDangerLayer;
        private float statusSfxReadyAt;
        private float nextTitleButtonScanAt;
        private float lastHealthRatio = 1f;
        private float lastMentalRatio = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static AudioManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            AudioManager existing = FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            return new GameObject(nameof(AudioManager)).AddComponent<AudioManager>();
        }

        public static void Play(AudioCue cue, bool important = false)
        {
            EnsureInstance().PlayCue(cue, important);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            LoadSettings();
            CreateSources();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            GameManager.OnGameOverEvent += HandleGameOver;
        }

        private void Start()
        {
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            UnbindGameplayEvents();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            GameManager.OnGameOverEvent -= HandleGameOver;
            Instance = null;
        }

        private void Update()
        {
            RebindMissingSystems();
            if (Time.unscaledTime >= nextTitleButtonScanAt)
            {
                nextTitleButtonScanAt = Time.unscaledTime + 1f;
                BindTitleButtons(SceneManager.GetActiveScene());
            }
            UpdateMusicState();
            float fadeSpeed = 1.5f * Time.unscaledDeltaTime;
            profitLayerSource.volume = Mathf.MoveTowards(profitLayerSource.volume, targetProfitLayer * bgmVolume * masterVolume, fadeSpeed);
            dangerLayerSource.volume = Mathf.MoveTowards(dangerLayerSource.volume, targetDangerLayer * bgmVolume * masterVolume, fadeSpeed);
        }

        private void CreateSources()
        {
            bgmSource = CreateSource("BGM", true);
            profitLayerSource = CreateSource("Profit Layer", true);
            dangerLayerSource = CreateSource("Danger Layer", true);
            for (int i = 0; i < sfxPoolSize; i++) sfxPool.Add(CreateSource($"SFX {i + 1:00}", false));
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnbindGameplayEvents();
            BindGameplayEvents();
            BindTitleButtons(scene);

            bool isGameplay = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include) != null;
            if (isGameplay) StartNormalMusic();
            else StartTitleMusic();
        }

        private void RebindMissingSystems()
        {
            if (boundTrading == null || boundLevelSystem == null || boundStatus == null || boundInventory == null)
                BindGameplayEvents();
        }

        private void BindGameplayEvents()
        {
            TradingController trading = FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
            if (trading != null && trading != boundTrading)
            {
                if (boundTrading != null)
                {
                    boundTrading.OnPositionOpened -= HandlePositionOpened;
                    boundTrading.OnPositionClosed -= HandlePositionClosed;
                }
                boundTrading = trading;
                boundTrading.OnPositionOpened += HandlePositionOpened;
                boundTrading.OnPositionClosed += HandlePositionClosed;
            }

            TraderLevelSystem levels = FindAnyObjectByType<TraderLevelSystem>(FindObjectsInactive.Include);
            if (levels != null && levels != boundLevelSystem)
            {
                if (boundLevelSystem != null)
                {
                    boundLevelSystem.OnProtagonistLeveledUp -= HandleTraderLevelUp;
                    boundLevelSystem.OnSkillLevelChanged -= HandleSkillLevelUp;
                }
                boundLevelSystem = levels;
                boundLevelSystem.OnProtagonistLeveledUp += HandleTraderLevelUp;
                boundLevelSystem.OnSkillLevelChanged += HandleSkillLevelUp;
            }

            TraderStatus status = TraderStatus.CanonicalInstance;
            if (status != null && status != boundStatus)
            {
                if (boundStatus != null)
                {
                    boundStatus.OnHealthChanged -= HandleHealthChanged;
                    boundStatus.OnMentalValueChanged -= HandleMentalChanged;
                    boundStatus.OnMentalStateChanged -= HandleMentalStateChanged;
                }
                boundStatus = status;
                boundStatus.OnHealthChanged += HandleHealthChanged;
                boundStatus.OnMentalValueChanged += HandleMentalChanged;
                boundStatus.OnMentalStateChanged += HandleMentalStateChanged;
                overdoseActive = boundStatus.CurrentMentalState == TraderStatus.MentalState.Overdose;
                lastHealthRatio = boundStatus.MaxHealth > 0f ? boundStatus.CurrentHealth / boundStatus.MaxHealth : 1f;
                lastMentalRatio = boundStatus.MaxMental > 0f ? boundStatus.CurrentMental / boundStatus.MaxMental : 1f;
            }

            Inventory inventory = FindAnyObjectByType<Inventory>(FindObjectsInactive.Include);
            if (inventory != null && inventory != boundInventory)
            {
                if (boundInventory != null) boundInventory.ItemConsumed -= HandleItemConsumed;
                boundInventory = inventory;
                boundInventory.ItemConsumed += HandleItemConsumed;
            }
        }

        private void UnbindGameplayEvents()
        {
            if (boundTrading != null)
            {
                boundTrading.OnPositionOpened -= HandlePositionOpened;
                boundTrading.OnPositionClosed -= HandlePositionClosed;
            }
            if (boundLevelSystem != null)
            {
                boundLevelSystem.OnProtagonistLeveledUp -= HandleTraderLevelUp;
                boundLevelSystem.OnSkillLevelChanged -= HandleSkillLevelUp;
            }
            if (boundStatus != null)
            {
                boundStatus.OnHealthChanged -= HandleHealthChanged;
                boundStatus.OnMentalValueChanged -= HandleMentalChanged;
                boundStatus.OnMentalStateChanged -= HandleMentalStateChanged;
            }
            if (boundInventory != null) boundInventory.ItemConsumed -= HandleItemConsumed;
            boundTrading = null;
            boundLevelSystem = null;
            boundStatus = null;
            boundInventory = null;
        }

        private void BindTitleButtons(Scene scene)
        {
            if (!scene.name.Contains("Title") && FindAnyObjectByType<FXOverdose.UI.MainMenuController>(FindObjectsInactive.Include) == null)
                return;

            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (Button button in buttons)
            {
                if (button.GetComponent<AudioClickRelay>() == null)
                    button.gameObject.AddComponent<AudioClickRelay>();
            }
        }

        private void HandlePositionOpened(TradingController.PositionType type, float margin, int leverage)
        {
            bool manual = boundTrading != null && boundTrading.ActiveTradingMode == TradingController.TradingMode.Player_Manual;
            PlayCue(type == TradingController.PositionType.Long
                ? (manual ? AudioCue.LongManual : AudioCue.LongAuto)
                : (manual ? AudioCue.ShortManual : AudioCue.ShortAuto));
        }

        private void HandlePositionClosed(float returned, float pnl)
        {
            PlayCue(pnl > 0f ? AudioCue.Profit : AudioCue.Loss);
        }

        private void HandleTraderLevelUp(int level) => PlayCue(AudioCue.TraderLevelUp, true);
        private void HandleSkillLevelUp(SkillType skill, int level) => PlayCue(AudioCue.SkillLevelUp, true);

        private void HandleHealthChanged(float delta)
        {
            float currentRatio = boundStatus != null && boundStatus.MaxHealth > 0f ? boundStatus.CurrentHealth / boundStatus.MaxHealth : 1f;
            bool playedThreshold = false;

            if (lastHealthRatio >= 0.5f && currentRatio < 0.5f)
            {
                PlayCue(AudioCue.HealthDown, true);
                playedThreshold = true;
            }
            lastHealthRatio = currentRatio;

            if (playedThreshold) return;

            if (Mathf.Abs(delta) < 0.5f || Time.unscaledTime < statusSfxReadyAt) return;
            statusSfxReadyAt = Time.unscaledTime + 0.2f;
            PlayCue(delta > 0f ? AudioCue.HealthUp : AudioCue.HealthDown);
        }

        private void HandleMentalChanged(float delta)
        {
            float currentRatio = boundStatus != null && boundStatus.MaxMental > 0f ? boundStatus.CurrentMental / boundStatus.MaxMental : 1f;
            bool playedThreshold = false;

            if (lastMentalRatio >= 0.5f && currentRatio < 0.5f)
            {
                PlayCue(AudioCue.MentalDown, true);
                playedThreshold = true;
            }
            else if (lastMentalRatio >= 0.2f && currentRatio < 0.2f)
            {
                PlayCue(AudioCue.MentalDown, true);
                playedThreshold = true;
            }
            lastMentalRatio = currentRatio;

            if (playedThreshold) return;

            if (Mathf.Abs(delta) < 0.5f || Time.unscaledTime < statusSfxReadyAt) return;
            statusSfxReadyAt = Time.unscaledTime + 0.2f;
            PlayCue(delta > 0f ? AudioCue.MentalUp : AudioCue.MentalDown);
        }

        private void HandleMentalStateChanged(TraderStatus.MentalState state)
        {
            bool nowOverdose = state == TraderStatus.MentalState.Overdose;
            if (nowOverdose && !overdoseActive) PlayCue(AudioCue.Overdose, true);
            overdoseActive = nowOverdose;
            
            AudioClip nextClip = nowOverdose ? LoadClip(OverdoseBgmPath) : GetValidNormalBgmClip();
            CrossFadeBgmByClip(nextClip, 0.8f);
        }

        private void HandleItemConsumed(ItemData item)
        {
            if (item == null) return;
            AudioCue cue = item.ItemId switch
            {
                "energy_drink" => AudioCue.EnergyDrink,
                "dessert" => AudioCue.Dessert,
                "supplement" => AudioCue.Supplement,
                "sedative" => AudioCue.Sedative,
                _ => AudioCue.UiClick
            };
            PlayCue(cue);
        }

        private void HandleGameOver(GameManager.EndingType ending) => PlayCue(AudioCue.GameOver, true);

        private void UpdateMusicState()
        {
            if (boundStatus == null)
            {
                targetProfitLayer = 0f;
                targetDangerLayer = 0f;
                return;
            }

            if (overdoseActive)
            {
                targetProfitLayer = 0f;
                targetDangerLayer = 0f;
                // 오버도즈 상태일 때 즉시 레이어 볼륨을 0으로 만들어 겹침 현상을 방지
                profitLayerSource.volume = 0f;
                dangerLayerSource.volume = 0f;
                return;
            }

            float profitFactor = 0f;
            float dangerFactor = 0f;

            if (boundTrading != null && boundTrading.IsActive)
            {
                float roe = boundTrading.CalculateROEPercentage();
                if (roe > 0f)
                {
                    // 20% 수익일 때 최대 레이어 볼륨
                    profitFactor = Mathf.Clamp01(roe / 20f);
                }
                else if (roe < 0f)
                {
                    // -20% 손실일 때 최대 레이어 볼륨
                    dangerFactor = Mathf.Clamp01(-roe / 20f);
                }
            }

            // 멘탈 비율이 40% 미만일 때 위기 레이어 점진적 활성화 (멘탈 10%에서 최대)
            float mentalRatio = boundStatus.CurrentMental / boundStatus.MaxMental;
            if (mentalRatio < 0.4f)
            {
                float mentalDanger = Mathf.Clamp01((0.4f - mentalRatio) / 0.3f);
                dangerFactor = Mathf.Max(dangerFactor, mentalDanger);
            }

            targetProfitLayer = profitFactor;
            targetDangerLayer = dangerFactor;
        }

        private void StartNormalMusic()
        {
            overdoseActive = boundStatus != null && boundStatus.CurrentMentalState == TraderStatus.MentalState.Overdose;
            
            AudioClip nextClip = overdoseActive ? LoadClip(OverdoseBgmPath) : GetValidNormalBgmClip();
            if (nextClip != null)
            {
                if (bgmSource.clip != nextClip)
                {
                    bgmSource.clip = nextClip;
                    bgmSource.time = 0f;
                }
                bgmSource.volume = bgmVolume * masterVolume;
                if (!bgmSource.isPlaying) bgmSource.Play();
            }

            PlayLoop(profitLayerSource, ProfitLayerPath, 0f);
            PlayLoop(dangerLayerSource, DangerLayerPath, 0f);
            SyncLayerPlayback();
        }

        private void StartTitleMusic()
        {
            overdoseActive = false;
            PlayLoop(bgmSource, TitleBgmPath, bgmVolume * masterVolume);
            profitLayerSource.Stop();
            dangerLayerSource.Stop();
        }

        private void StopMusic()
        {
            bgmSource.Stop();
            profitLayerSource.Stop();
            dangerLayerSource.Stop();
        }

        private void PlayLoop(AudioSource source, string path, float volume)
        {
            AudioClip clip = LoadClip(path);
            if (clip == null) return;
            if (source.clip != clip)
            {
                source.clip = clip;
                source.time = 0f;
            }
            source.volume = volume;
            if (!source.isPlaying) source.Play();
        }

        private void SyncLayerPlayback()
        {
            if (bgmSource.clip == null) return;
            float normalizedTime = bgmSource.clip.length > 0f ? bgmSource.time / bgmSource.clip.length : 0f;
            SyncLayer(profitLayerSource, normalizedTime);
            SyncLayer(dangerLayerSource, normalizedTime);
        }

        private static void SyncLayer(AudioSource layer, float normalizedTime)
        {
            if (layer.clip == null) return;
            layer.time = Mathf.Repeat(normalizedTime * layer.clip.length, layer.clip.length);
        }

        public void PlayCue(AudioCue cue, bool important = false)
        {
            if (!CuePaths.TryGetValue(cue, out string path)) return;
            AudioClip clip = LoadClip(path);
            if (clip == null) return;

            AudioSource source = sfxPool.Find(candidate => !candidate.isPlaying) ?? sfxPool[0];
            source.volume = sfxVolume * masterVolume;
            source.PlayOneShot(clip);
            if (important) DuckBgm(0.25f, 0.7f);
        }

        public void PlayBGM(AudioClip clip, bool crossFade = false)
        {
            if (clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;
            bgmSource.clip = clip;
            bgmSource.volume = bgmVolume * masterVolume;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;
            AudioSource source = sfxPool.Find(candidate => !candidate.isPlaying) ?? sfxPool[0];
            source.volume = sfxVolume * masterVolume;
            source.PlayOneShot(clip);
        }

        private AudioClip LoadClip(string resourcesPath)
        {
            if (clipCache.TryGetValue(resourcesPath, out AudioClip cached)) return cached;
            if (missingClipPaths.Contains(resourcesPath)) return null;
            AudioClip clip = Resources.Load<AudioClip>(resourcesPath);
            if (clip != null) clipCache[resourcesPath] = clip;
            else missingClipPaths.Add(resourcesPath);
            return clip;
        }

        private void CrossFadeBgm(string path, float duration)
        {
            AudioClip clip = LoadClip(path);
            CrossFadeBgmByClip(clip, duration);
        }

        private void CrossFadeBgmByClip(AudioClip clip, float duration)
        {
            if (clip == null || bgmSource.clip == clip) return;
            if (bgmFadeRoutine != null) StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = StartCoroutine(CrossFadeRoutine(clip, duration));
        }

        private AudioClip GetValidNormalBgmClip()
        {
            AudioClip clip = LoadClip(currentNormalBgmPath);
            if (clip == null) clip = LoadClip("Audio/BGM/bgm_normal");
            return clip;
        }

        public void SetTimeOfDayBgm(string bgmPath, bool crossFade = true)
        {
            if (string.IsNullOrEmpty(bgmPath) || currentNormalBgmPath == bgmPath) return;
            currentNormalBgmPath = bgmPath;

            if (!overdoseActive && bgmSource != null)
            {
                AudioClip clip = GetValidNormalBgmClip();
                if (clip != null && bgmSource.clip != clip)
                {
                    if (crossFade)
                        CrossFadeBgmByClip(clip, 0.8f);
                    else
                    {
                        bgmSource.clip = clip;
                        bgmSource.time = 0f;
                        bgmSource.volume = bgmVolume * masterVolume;
                        if (!bgmSource.isPlaying) bgmSource.Play();
                        SyncLayerPlayback();
                    }
                }
            }
        }

        private IEnumerator CrossFadeRoutine(AudioClip next, float duration)
        {
            float normalVolume = bgmVolume * masterVolume;
            for (float t = 0f; t < duration * 0.5f; t += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(normalVolume, 0f, t / (duration * 0.5f));
                yield return null;
            }
            bgmSource.clip = next;
            bgmSource.Play();
            SyncLayerPlayback();
            for (float t = 0f; t < duration * 0.5f; t += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(0f, normalVolume, t / (duration * 0.5f));
                yield return null;
            }
            bgmSource.volume = normalVolume;
            bgmFadeRoutine = null;
        }

        private void DuckBgm(float fadeTime, float holdTime)
        {
            if (duckRoutine != null) StopCoroutine(duckRoutine);
            duckRoutine = StartCoroutine(DuckRoutine(fadeTime, holdTime));
        }

        private IEnumerator DuckRoutine(float fadeTime, float holdTime)
        {
            float normal = bgmVolume * masterVolume;
            float ducked = normal * duckedBgmRatio;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(normal, ducked, t / fadeTime);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(holdTime);
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(ducked, normal, t / fadeTime);
                yield return null;
            }
            bgmSource.volume = normal;
            duckRoutine = null;
        }

        private void LoadSettings()
        {
            masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 0.7f);
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
            PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
            PlayerPrefs.Save();
        }

        public void SetMasterVolume(float value) { masterVolume = Mathf.Clamp01(value); UpdateVolumes(); }
        public void SetBGMVolume(float value) { bgmVolume = Mathf.Clamp01(value); UpdateVolumes(); }
        public void SetSFXVolume(float value) { sfxVolume = Mathf.Clamp01(value); UpdateVolumes(); }

        private void UpdateVolumes()
        {
            bgmSource.volume = bgmVolume * masterVolume;
            foreach (AudioSource source in sfxPool) source.volume = sfxVolume * masterVolume;
        }
    }

    internal sealed class AudioClickRelay : MonoBehaviour
    {
        private Button button;
        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(PlayClick);
        }
        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(PlayClick);
        }
        private static void PlayClick() => AudioManager.Play(AudioCue.UiClick);
    }
}
