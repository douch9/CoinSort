using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPlatformService
{
    void Init(Action<bool> onComplete); //初始化平台SDK环境接口
    void Login(Action<bool, string> onComplete); //用户静默登录获取唯一OpenId或Code接口
    void ShowRewardVideoAd(
        string adUnitId,
        Action onReward,
        Action onClose = null,
        Action<string> onFail = null
    ); //播放激励视频广告并在看完后触发发奖回调
    void Share(string title, string imageUrl, Action onComplete = null); //唤起主动分享转发卡片接口
    void VibrateShort(); //触发手机设备短震动触觉反馈接口
}
