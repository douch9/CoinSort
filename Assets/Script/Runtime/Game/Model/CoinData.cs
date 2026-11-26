using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CoinData
{
    //全局硬币自增ID分配，保证每枚硬币实例全局唯一
    private static long _idCounter = 0;

    public long Id { get; private set; }

    public int Level { get; private set; }

    // 硬币面额数值（基于 2048 进阶规则：Lv.1 为 1，Lv.2 为 10，Lv.3 为 100，以此类推）
    public long Value => (long)Math.Pow(10, Level - 1);

    public CoinData(int level = 1)
    {
        Id = ++_idCounter;
        Level = Math.Max(1, level);
    }

    /// <summary>
    /// 硬币升级
    /// </summary>
    public void Upgrade()
    {
        Level++;
    }

    /// <summary>
    /// 重置ID计数器
    /// </summary>
    public static void ResetIdCounter()
    {
        _idCounter = 0;
    }
}
