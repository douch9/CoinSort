using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 树状红点系统单个节点数据模型
/// </summary>
public class RedDotNode
{
    public string Name { get; private set; }
    public string FullPath { get; private set; } //当前节点完整路径属性
    public int Count { get; private set; } //当前节点的红点计数值
    public RedDotNode Parent { get; private set; } //指向父节点的引用指针
    public readonly Dictionary<string, RedDotNode> Children = new Dictionary<string, RedDotNode>();
    public Action<int> OnCountChanged; //节点红点数量变化时的委托监听器

    public RedDotNode(string name, string fullPath, RedDotNode parent)
    {
        Name = name;
        FullPath = fullPath;
        Parent = parent;
        Count = 0;
    }

    public void SetCount(int count)
    {
        if (Children.Count > 0)
        {
            AppLog.LogWarning(
                "RedDot",
                $"节点 {FullPath} 是复合父节点，不应手动设置数值，将由子节点自动汇总！"
            );
        }
        int safeCount = Mathf.Max(0, count);
        if (Count == safeCount)
        {
            return;
        }
        Count = safeCount;
        NotifyChange();
        Parent?.UpdateCountFromChildren();
    }

    public void UpdateCountFromChildren()
    {
        int sum = 0;
        foreach (var child in Children.Values)
        {
            sum += child.Count; //累加子节点计数值
        }
        if (Count != sum)
        {
            Count = sum;
            NotifyChange();
            Parent?.UpdateCountFromChildren();
        }
    }

    public void NotifyChange()
    {
        OnCountChanged?.Invoke(Count);
    }
}

public class RedDotManager : Singleton<RedDotManager>
{
    private readonly RedDotNode _root = new RedDotNode("Root", "Root", null);

    //扁平化全局节点路径速查字典
    private readonly Dictionary<string, RedDotNode> _nodeDict =
        new Dictionary<string, RedDotNode>();

    public RedDotNode GetOrAddNode(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        if (_nodeDict.TryGetValue(path, out RedDotNode cachedNode))
            return cachedNode;

        string[] parts = path.Split('/'); //按照斜杠分割各层级路径名
        RedDotNode current = _root;
        string currentPath = string.Empty;

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            currentPath = i == 0 ? part : $"{currentPath}/{part}";
            if (!current.Children.TryGetValue(part, out RedDotNode child))
            {
                child = new RedDotNode(part, currentPath, current);
                current.Children.Add(part, child);
                _nodeDict[currentPath] = child;
            }
            current = child;
        }
        return current;
    }

    public void SetCount(string path, int count)
    {
        RedDotNode node = GetOrAddNode(path);
        node?.SetCount(count);
    }

    public int GetCount(string path)
    {
        if (_nodeDict.TryGetValue(path, out RedDotNode node))
            return node.Count;
        return 0;
    }

    public void AddListener(string path, Action<int> callback)
    {
        if (callback == null)
            return;
        RedDotNode node = GetOrAddNode(path);
        if (node != null)
        {
            node.OnCountChanged += callback;
            callback.Invoke(node.Count);
        }
    }

    public void RemoveListener(string path, Action<int> callback)
    {
        if (callback == null)
            return;
        if (_nodeDict.TryGetValue(path, out RedDotNode node))
            node.OnCountChanged -= callback;
    }

    public void Clear()
    {
        _root.Children.Clear();
        _nodeDict.Clear();
    }
}
