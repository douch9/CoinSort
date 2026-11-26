using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoinSlotModel
{
    public const int MAX_CAPACITY = 10;

    public int SlotIndex { get; private set; }

    public bool IsUnlocked { get; private set; }

    private readonly List<CoinData> _coins = new List<CoinData>();

    //外部只读访问槽内所有硬币列表
    public IReadOnlyList<CoinData> Coins => _coins;

    public int Count => _coins.Count;

    public bool IsFull => _coins.Count >= MAX_CAPACITY;

    public bool IsEmpty => _coins.Count == 0;

    //槽位剩余可用容量
    public int RemainingCapacity => MAX_CAPACITY - _coins.Count;

    public CoinSlotModel(int slotIndex, bool isUnlocked = true)
    {
        SlotIndex = slotIndex;
        IsUnlocked = isUnlocked;
    }

    public void Unlock()
    {
        IsUnlocked = true;
    }

    /// <summary>
    /// 获取当前最顶层的一枚硬币
    /// </summary>
    public CoinData GetTopCoin()
    {
        if (IsEmpty)
            return null;
        //返回列表末尾（即最顶层）的硬币
        return _coins[_coins.Count - 1];
    }

    /// <summary>
    /// 获取最顶层的硬币等级
    /// </summary>
    public int GetTopLevel()
    {
        if (IsEmpty)
            return 0;
        return GetTopCoin().Level;
    }

    /// <summary>
    /// 统计槽位顶部连续同等级硬币的数量
    /// （规则：仅允许抓取/转移最顶层连续相同等级的硬币堆）
    /// </summary>
    /// <returns></returns>
    public int GetTopContinuousCount()
    {
        if (IsEmpty)
            return 0;

        int targetLevel = GetTopCoin().Level;
        int count = 0;

        for (int i = _coins.Count - 1; i >= 0; i--)
        {
            if (_coins[i].Level == targetLevel)
                count++;
            else
                break;
        }
        return count;
    }

    /// <summary>
    /// 校验当前槽位是否可以接收指定等级的硬币倒入
    /// </summary>
    /// <param name="incomingLevel">准备倒入的硬币等级</param>
    /// <returns></returns>
    public bool CanAccept(int incomingLevel)
    {
        if (!IsUnlocked || IsFull)
            return false;

        if (IsEmpty)
            return true;

        return GetTopLevel() == incomingLevel;
    }

    /// <summary>
    /// 向槽位压入一枚硬币
    /// </summary>
    /// <param name="coin"></param>
    public void Push(CoinData coin)
    {
        if (coin == null)
            return;

        if (IsFull)
            throw new InvalidOperationException($"硬币槽位{SlotIndex}已满");

        _coins.Add(coin);
    }

    /// <summary>
    /// 弹出最顶层的一枚硬币
    /// </summary>
    /// <returns></returns>
    public CoinData Pop()
    {
        if (IsEmpty)
            return null;

        int lastIndex = _coins.Count - 1;
        CoinData topCoin = _coins[lastIndex];
        _coins.RemoveAt(lastIndex);
        return topCoin;
    }

    /// <summary>
    /// 弹出顶部连续同等级的指定数量硬币（用于槽间移动）
    /// </summary>
    /// <param name="count"></param>
    /// <returns></returns>
    public List<CoinData> PopTopRange(int count)
    {
        //修正弹出数量，不能超过当前总硬币数
        int popCount = Math.Min(count, _coins.Count);
        //准备接收结果的列表容器
        List<CoinData> result = new List<CoinData>(popCount);

        //起始截取位置
        int startIndex = _coins.Count - popCount;

        for (int i = startIndex; i < _coins.Count; i++)
        {
            //添加至结果集
            result.Add(_coins[i]);
        }

        _coins.RemoveRange(startIndex, popCount);

        return result;
    }

    /// <summary>
    /// 检查硬币槽是否满足硬币合成条件
    /// </summary>
    /// <returns></returns>
    public bool CheckCanMerge()
    {
        if (_coins.Count != MAX_CAPACITY)
            return false;

        return GetTopContinuousCount() == MAX_CAPACITY;
    }

    /// <summary>
    /// 执行满容量合成操作
    /// </summary>
    /// <returns></returns>
    public CoinData ExecuteMerge()
    {
        if (!CheckCanMerge())
            throw new InvalidOperationException($"槽位{SlotIndex}不满足合成条件");

        int originalLevel = GetTopLevel();

        _coins.Clear();

        CoinData upgradedCoin = new CoinData(originalLevel + 1);

        _coins.Add(upgradedCoin);

        return upgradedCoin;
    }

    public void Clear()
    {
        _coins.Clear();
    }
}
