using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct CoinChangeEventArgs
{
    public int NewCoin; //变更后的最新金币总数
    public int Delta; //本次增减的金币差值（正数为增加，负数为扣除）
}

public class PlayerModel : Singleton<PlayerModel>
{
    private const string COIN_KEY = "Player_Coin_Amount";
    private int _coin;

    public int Coin => _coin;

    protected override void OnInit()
    {
        base.OnInit();
        _coin = StorageManager.Instance.GetInt(COIN_KEY, 100);
    }

    public void AddCoin(int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        _coin += amount;
        StorageManager.Instance.SetInt(COIN_KEY, _coin);
        //通过EventCenter派发金币变更事件解耦通知表现层
        EventCenter.Broadcast(new CoinChangeEventArgs { NewCoin = _coin, Delta = amount });
    }

    public bool CostCoin(int amount)
    {
        if (amount <= 0 || _coin < amount)
        {
            return false;
        }
        _coin -= amount;
        StorageManager.Instance.SetInt(COIN_KEY, _coin);
        EventCenter.Broadcast(new CoinChangeEventArgs { NewCoin = _coin, Delta = -amount });
        return true;
    }
}
