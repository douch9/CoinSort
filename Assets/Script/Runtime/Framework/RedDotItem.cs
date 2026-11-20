using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RedDotItem : MonoBehaviour
{
    [Header("红点唯一路径（如: Main/Welfare 或 Main/Mail）")]
    [SerializeField]
    private string _redDotPath;

    [Header("UI表现节点绑定（缺省时默认使用自身物体）")]
    [SerializeField]
    private GameObject _dotGo;

    [SerializeField]
    private Text _countText;

    private bool _isListening = false; //标记当前组件是否已注册监听以防重复

    private void Awake()
    {
        if (_dotGo == null)
            _dotGo = gameObject;
    }

    private void OnEnable()
    {
        RegisterListener();
    }

    private void OnDisable()
    {
        UnregisterListener();
    }

    public void SetPath(string newPath)
    {
        if (_redDotPath == newPath)
            return;
        UnregisterListener(); //解绑旧事件监听
        _redDotPath = newPath;
        if (gameObject.activeInHierarchy) //判断物体当前是否处于激活状态
            RegisterListener();
    }

    /// <summary>
    /// 注册红点树监听内部实现
    /// </summary>
    private void RegisterListener()
    {
        if (_isListening || string.IsNullOrEmpty(_redDotPath))
            return;
        RedDotManager.Instance.AddListener(_redDotPath, OnRedDotCountChanged);
        _isListening = true;
    }

    private void UnregisterListener()
    {
        if (!_isListening || string.IsNullOrEmpty(_redDotPath))
            return;
        RedDotManager.Instance.RemoveListener(_redDotPath, OnRedDotCountChanged);
        _isListening = false;
    }

    private void OnRedDotCountChanged(int count)
    {
        if (_dotGo != null)
            _dotGo.SetActive(count > 0);
        if (_countText != null)
        {
            if (count > 0)
            {
                _countText.text = count > 99 ? "99+" : count.ToString();
            }
            else
            {
                _countText.text = string.Empty;
            }
        }
    }
}
