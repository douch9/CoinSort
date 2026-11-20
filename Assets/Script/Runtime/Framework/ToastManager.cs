using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ToastManager : MonoSingleton<ToastManager>
{
    [Header("预制体配置")]
    [SerializeField]
    private string _toastPrefabPath = "UI/ToastItem";

    [SerializeField]
    private float _floatDistance = 100f; //飘字向上浮动的垂直距离像素值

    [SerializeField]
    private float _duration = 0.8f;

    private Transform _topLayerRoot; //缓存UIManager中的Top顶层UI挂载节点

    protected override void OnInit()
    {
        base.OnInit();
    }

    private Transform GetTopLayerRoot()
    { //获取或动态检索UI系统最顶层的根挂载节点
        if (_topLayerRoot == null && UIManager.Instance != null)
        {
            Transform canvasTrans = UIManager.Instance.transform.Find("UICanvas");
            if (canvasTrans != null)
            {
                _topLayerRoot = canvasTrans.Find(UILayer.Top.ToString());
            }
        }
        return _topLayerRoot;
    }

    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }
        Transform parent = GetTopLayerRoot();
        PoolMgr.Instance.GetAsync(
            _toastPrefabPath,
            go =>
            {
                if (go == null)
                {
                    AppLog.LogWarning(
                        "Toast",
                        $"未能成功从对象池加载toast预制体: {_toastPrefabPath}"
                    );
                    return;
                }
                if (parent != null)
                {
                    go.transform.SetParent(parent, false);
                }
                PlayToastAnimation(go, message);
            }
        );
    }

    /// <summary>
    /// 播放用DOTween制作的toast动画
    /// </summary>
    private void PlayToastAnimation(GameObject go, string message)
    {
        Text text = go.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = message;
        }
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = go.AddComponent<CanvasGroup>();
        }
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;
        cg.alpha = 1f;
        go.transform.DOKill(); //杀死物体上可能残留的旧动画Tween防止冲突
        cg.DOKill();
        Sequence seq = DOTween.Sequence(); //创建DOTween动画编排序列
        seq.Append(rect.DOAnchorPosY(_floatDistance, _duration).SetEase(Ease.OutCubic));
        seq.Join(cg.DOFade(0, _duration * 0.4f).SetDelay(_duration * 0.6f));
        seq.SetUpdate(true);
        seq.OnComplete(() =>
        {
            PoolMgr.Instance.Push(go);
        });
    }
}
