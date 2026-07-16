using UnityEngine;
using System.Collections.Generic;

namespace FXOverdose.Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("오디오 소스")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private GameObject sfxSourcePrefab;
        [SerializeField] private int sfxPoolSize = 10;

        private List<AudioSource> sfxPool = new List<AudioSource>();

        [Header("볼륨 설정")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

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

            InitializeAudio();
            LoadSettings();
        }

        private void InitializeAudio()
        {
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
            }

            for (int i = 0; i < sfxPoolSize; i++)
            {
                AudioSource sfx = gameObject.AddComponent<AudioSource>();
                sfx.playOnAwake = false;
                sfxPool.Add(sfx);
            }
        }

        private void LoadSettings()
        {
            masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 1f);
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
            UpdateVolumes();
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
            PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
            PlayerPrefs.Save();
        }

        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            UpdateVolumes();
        }

        public void SetBGMVolume(float value)
        {
            bgmVolume = Mathf.Clamp01(value);
            UpdateVolumes();
        }

        public void SetSFXVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            UpdateVolumes();
        }

        private void UpdateVolumes()
        {
            if (bgmSource != null)
            {
                bgmSource.volume = bgmVolume * masterVolume;
            }

            foreach (var sfx in sfxPool)
            {
                if (sfx != null)
                {
                    sfx.volume = sfxVolume * masterVolume;
                }
            }
        }

        public void PlayBGM(AudioClip clip, bool crossFade = false)
        {
            if (clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            // 크로스페이드 로직은 추후 확장을 위해 예약
            bgmSource.clip = clip;
            bgmSource.volume = bgmVolume * masterVolume;
            bgmSource.Play();
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;

            AudioSource availableSource = sfxPool.Find(s => !s.isPlaying);
            if (availableSource == null)
            {
                // 풀이 꽉 찼다면 가장 오래된 재생 중인 것을 강제로 사용하거나 무시
                availableSource = sfxPool[0];
            }

            availableSource.volume = sfxVolume * masterVolume;
            availableSource.PlayOneShot(clip);
        }
    }
}
