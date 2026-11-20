using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public enum UILayer
{
    Normal = 0,
    PopUp = 10,
    Top = 20,
}

public class UIBase : MonoBehaviour
{
    [SerializeField]
    private UILayer _layer = UILayer.Normal;
    public UILayer Layer => _layer; //等价于只包含 get 访问器的传统属性定义

    [SerializeField]
    protected bool _useAnimation = true;

    [SerializeField]
    protected float _animDuration = 0.25f;

    public bool IsOpen { get; private set; }

    public virtual void OnInit() { }

    public virtual void OnOpen(object args = null)
    {
        IsOpen = true;
        gameObject.SetActive(true);
        transform.DOKill();
        if (_useAnimation)
        {
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, _animDuration).SetEase(Ease.OutBack).SetUpdate(true);
        }
        else
        {
            transform.localScale = Vector3.one;
        }
    }

    public virtual void OnClose(Action onClosed = null)
    {
        IsOpen = false;
        transform.DOKill();
        if (_useAnimation && gameObject.activeInHierarchy) //检查是否开启关闭动效且物体当前仍在渲染层级中
        {
            transform
                .DOScale(Vector3.zero, _animDuration * 0.8f)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    onClosed?.Invoke();
                });
        }
        else
        {
            gameObject.SetActive(false);
            onClosed?.Invoke();
        }
    }

    public void CloseSelf()
    {
        UIManager.Instance.CloseUI(GetType().Name);
    }

    protected virtual void OnDestroy()
    {
        transform.DOKill();
    }
}
