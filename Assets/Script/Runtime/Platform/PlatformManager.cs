using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EditorPlatformService : IPlatformService
{
    public void Init(Action<bool> onComplete)
    {
        Debug.Log("[Platform][Editor] 平台服务初始化成功（模拟模式）");
        onComplete?.Invoke(true);
    }

    /// <summary>
    /// 编辑器模拟登录流程
    /// </summary>
    public void Login(Action<bool, string> onComplete)
    {
        string mockOpenId = "mock_editor_openid_123456";
        Debug.Log($"[Platform][Editor] 模拟微信登录成功，OpenID: {mockOpenId}");
        onComplete?.Invoke(true, mockOpenId);
    }

    public void Share(string title, string imageUrl, Action onComplete = null)
    {
        Debug.Log($"[Platform][Editor] 模拟触发分享成功 标题: {title}, 图片: {imageUrl}");
        onComplete?.Invoke();
    }

    /// <summary>
    /// 编辑器模拟激励视频广告播放
    /// </summary>
    public void ShowRewardVideoAd(
        string adUnitId,
        Action onReward,
        Action onClose = null,
        Action<string> onFail = null
    )
    {
        Debug.Log($"[Platform][Editor] 开始模拟播放激励视频广告: {adUnitId}，将在1秒后自动发奖");
        TimerManager.Instance.AddTimer(
            1f,
            () =>
            {
                Debug.Log("[Platform][Editor] 模拟广告播放完毕，触发发奖回调");
                onReward?.Invoke();
                onClose?.Invoke();
            },
            true
        );
    }

    public void VibrateShort()
    {
        Debug.Log("[Platform][Editor] 触发设备轻微震动反馈（模拟）");
    }
}

public class PlatformManager : MonoSingleton<PlatformManager>
{
    private IPlatformService _service;
    public IPlatformService Service => _service;

    protected override void OnInit()
    {
        base.OnInit();
#if UNITY_EDITOR
        _service = new EditorPlatformService();
#else
        _service = new WXPlatformService(); // 当前阶段默认回退，接入微信SDK后替换为真实WXPlatformService
#endif
        _service.Init(null);
    }
}
