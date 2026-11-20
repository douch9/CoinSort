using System;
using System.Collections.Generic;
using UnityEngine;
using WeChatWASM;

public class WXPlatformService : IPlatformService
{
    //缓存已创建的激励广告实例防止重复创建泄漏
    private readonly Dictionary<string, WXRewardedVideoAd> _adCache =
        new Dictionary<string, WXRewardedVideoAd>();
    private Action _currentAdRewardCallback;
    private Action _currentAdCloseCallback;
    private Action<string> _currentAdFailCallback;

    public void Init(Action<bool> onComplete)
    {
        WX.InitSDK(
            (code) =>
            {
                AppLog.Log("Platform", "微信小游戏SDK初始化完成，状态码: " + code);
                InitLifecycleHooks();
                onComplete?.Invoke(true);
            }
        );
    }

    public void Login(Action<bool, string> onComplete)
    {
        WX.Login(
            new LoginOption
            {
                //类型为 Action<LoginSuccessCallbackResult>
                success = (res) =>
                {
                    AppLog.Log("Platform", "微信静默登录成功，Code: " + res.code);
                    onComplete?.Invoke(true, res.code);
                },
                fail = (res) =>
                {
                    AppLog.LogError("Platform", "微信静默登录失败: " + res.errMsg);
                    onComplete?.Invoke(false, res.errMsg);
                },
            }
        );
    }

    public void Share(string title, string imageUrl, Action onComplete = null)
    {
        WX.ShareAppMessage(new ShareAppMessageOption { title = title, imageUrl = imageUrl });
        onComplete?.Invoke();
    }

    public void ShowRewardVideoAd(
        string adUnitId,
        Action onReward,
        Action onClose = null,
        Action<string> onFail = null
    )
    {
        _currentAdRewardCallback = onReward;
        _currentAdCloseCallback = onClose;
        _currentAdFailCallback = onFail;
        WXRewardedVideoAd ad = GetOrCreateAd(adUnitId);
        if (ad == null)
        {
            _currentAdFailCallback?.Invoke("创建激励广告实例失败");
            return;
        }
        ad.Show(
            (res) =>
            {
                AppLog.Log("Platform", "激励视频广告展示成功");
            },
            (err) =>
            {
                AppLog.LogError("Platform", "展示激励视频广告失败: " + err?.errMsg);
                _currentAdFailCallback?.Invoke(err?.errMsg);
            }
        );
    }

    private WXRewardedVideoAd GetOrCreateAd(string adUnitId)
    {
        if (_adCache.TryGetValue(adUnitId, out WXRewardedVideoAd cacheAd))
        {
            return cacheAd;
        }
        WXRewardedVideoAd newAd = WX.CreateRewardedVideoAd(
            new WXCreateRewardedVideoAdParam { adUnitId = adUnitId, multiton = false } //设为单实例模式以节省运行内存
        );
        if (newAd == null)
        {
            return null;
        }
        newAd.OnClose(
            (res) =>
            {
                if (res != null && res.isEnded || res == null)
                { //判断用户是否完整看完广告
                    AppLog.Log("Platform", "用户完整观看激励视频广告，发放奖励");
                    _currentAdRewardCallback?.Invoke();
                }
                else
                {
                    AppLog.LogWarning("Platform", "用户未完整观看广告，不予发放奖励");
                }
                _currentAdCloseCallback?.Invoke();
            }
        );
        newAd.OnError(
            (err) =>
            {
                AppLog.LogError("Platform", "激励视频广告发生异常: " + err.errMsg);
                _currentAdFailCallback?.Invoke(err.errMsg);
            }
        );
        _adCache[adUnitId] = newAd;
        return newAd;
    }

    public void VibrateShort()
    {
        WX.VibrateShort(
            new VibrateShortOption
            {
                type = "medium", //指定震动强弱程度为中等强度
            }
        );
    }

    private void InitLifecycleHooks()
    {
        WX.OnShow(
            (res) =>
            {
                AppLog.Log("Platform", "小游戏切回前台，启动参数: " + res?.query);
                AudioManager.Instance.ResumeAll();
            }
        );
        WX.OnHide(
            (res) =>
            {
                AppLog.Log("Platform", "小游戏切入后台");
                AudioManager.Instance.PauseAll();
            }
        );
    }
}
