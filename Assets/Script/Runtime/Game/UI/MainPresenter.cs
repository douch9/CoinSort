using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainPresenter
{
    private readonly MainView _view;

    /// <summary>
    /// 构造函数接收View实例完成中介绑定
    /// </summary>
    public MainPresenter(MainView view)
    {
        _view = view;
    }

    public void OnViewOpen()
    {
        EventCenter.AddListener<CoinChangeEventArgs>(OnCoinChanged);
        _view.UpdateCoinText(PlayerModel.Instance.Coin);
    }

    public void OnViewClose()
    {
        EventCenter.RemoveListener<CoinChangeEventArgs>(OnCoinChanged);
    }

    private void OnCoinChanged(CoinChangeEventArgs args)
    {
        _view.UpdateCoinText(args.NewCoin);
    }

    public void OnClickWatchAd()
    {
        AudioManager.Instance.PlaySFX("Click");
        PlatformManager.Instance.Service.ShowRewardVideoAd(
            "adunit_main_reward",
            () =>
            {
                PlayerModel.Instance.AddCoin(50);
                PlatformManager.Instance.Service.VibrateShort();
            }
        );
    }

    public void OnClickCostCoin()
    {
        AudioManager.Instance.PlaySFX("Click");
        bool success = PlayerModel.Instance.CostCoin(20);
        if (!success)
        {
            ToastManager.Instance.Show("金币不足，购买失败！");
        }
        else
        {
            ToastManager.Instance.Show("购买成功，金币 -20");
        }
    }

    public void OnClickPlaySound()
    {
        AudioManager.Instance.PlaySFX("TestSound");
    }
}
