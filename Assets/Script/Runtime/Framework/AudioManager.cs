using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoSingleton<AudioManager>
{
    private const string BGM_VOL_KEY = "Audio_BGM_Volume";
    private const string SFX_VOL_KEY = "Audio_SFX_Volume";
    private const string BGM_MUTE_KEY = "Audio_BGM_Mute";
    private const string SFX_MUTE_KEY = "Audio_SFX_Mute";

    private AudioSource _bgmSource;
    private AudioSource _sfxSource;

    private float _bgmVolume = 1f;
    private float _sfxVolume = 1f;
    private bool _isBgmMute = false;
    private bool _isSfxMute = false;

    private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();

    protected override void OnInit()
    {
        base.OnInit();
        InitAudioSources();
        LoadAudioSettings();
    }

    /// <summary>
    /// 内部初始化并配置两个独立AudioSource组件的方法
    /// </summary>
    private void InitAudioSources()
    {
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;
    }

    /// <summary>
    /// 从StorageManager加载保存的音频偏好设置
    /// </summary>
    private void LoadAudioSettings()
    {
        _bgmVolume = StorageManager.Instance.GetFloat(BGM_VOL_KEY, 1f);
        _sfxVolume = StorageManager.Instance.GetFloat(SFX_VOL_KEY, 1f);
        _isBgmMute = StorageManager.Instance.GetBool(BGM_MUTE_KEY, false);
        _isSfxMute = StorageManager.Instance.GetBool(SFX_MUTE_KEY, false);

        ApplyVolumeSettings(); //将读取出的配置即时应用到底层音源组件
    }

    /// <summary>
    /// 将音量与静音逻辑运算后统一同步给AudioSource组件
    /// </summary>
    private void ApplyVolumeSettings()
    {
        _bgmSource.volume = _isBgmMute ? 0f : _bgmVolume;
        _sfxSource.volume = _isSfxMute ? 0f : _sfxVolume;
    }

    /// <summary>
    /// 播放BGM
    /// </summary>
    public void PlayBGM(string path, bool isLoop = true)
    {
        path = "Audio/BGM/" + path;
        LoadClipAsync(
            path,
            clip =>
            {
                if (clip == null)
                {
                    return;
                }
                _bgmSource.clip = clip;
                _bgmSource.loop = isLoop;
                _bgmSource.Play();
            }
        );
    }

    public void StopBGM()
    {
        if (_bgmSource != null && _bgmSource.isPlaying)
        {
            _bgmSource.Stop();
        }
    }

    public void PauseBGM()
    {
        if (_bgmSource != null && _bgmSource.isPlaying)
        {
            _bgmSource.Pause();
        }
    }

    public void ResumeBGM()
    {
        if (_bgmSource != null)
        {
            _bgmSource.UnPause();
        }
    }

    /// <summary>
    /// 播放音效
    /// </summary>
    public void PlaySFX(string path)
    {
        path = "Audio/SFX/" + path;
        if (_isSfxMute)
        {
            return;
        }
        LoadClipAsync(
            path,
            clip =>
            {
                if (clip != null)
                {
                    _sfxSource.PlayOneShot(clip, _sfxVolume);
                }
            }
        );
    }

    public void SetBGMVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        StorageManager.Instance.SetFloat(BGM_VOL_KEY, _bgmVolume);
        ApplyVolumeSettings(); //实时刷新播放器响度
    }

    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        StorageManager.Instance.SetFloat(SFX_VOL_KEY, _sfxVolume);
        ApplyVolumeSettings();
    }

    public void SetBGMMute(bool isMute)
    {
        _isBgmMute = isMute;
        StorageManager.Instance.SetBool(BGM_MUTE_KEY, isMute);
        ApplyVolumeSettings();
    }

    public void SetSFXMute(bool isMute)
    {
        _isSfxMute = isMute;
        StorageManager.Instance.SetBool(SFX_MUTE_KEY, isMute);
        ApplyVolumeSettings();
    }

    private void LoadClipAsync(string path, System.Action<AudioClip> callback)
    {
        if (_clipCache.TryGetValue(path, out AudioClip cachedClip))
        {
            callback?.Invoke(cachedClip);
            return;
        }
        ResourcesManager.Instance.LoadAsync<AudioClip>(
            path,
            clip =>
            {
                if (clip != null)
                {
                    _clipCache[path] = clip;
                }
                callback?.Invoke(clip);
            }
        );
    }

    public void ClearClipCache()
    {
        _clipCache.Clear();
    }

    public void PauseAll()
    {
        _bgmSource.Pause();
        _sfxSource.Pause();
    }

    public void ResumeAll()
    {
        _bgmSource.UnPause();
        _sfxSource.UnPause();
    }
}
