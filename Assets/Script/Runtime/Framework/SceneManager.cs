using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class SceneManager : MonoSingleton<SceneManager>
{
    private CanvasGroup _fadeCanvasGroup;
    private bool _isLoading = false;

    protected override void OnInit()
    {
        base.OnInit();
        InitFadeOverlay();
    }

    /// <summary>
    /// 动态生成不依赖特定场景的全局黑屏渐变遮罩画布
    /// ScrenManager
    ///     SceneFadeOverlay(Canvas\GraphicRaycaster\CanvasGroup)
    ///         BlackImage(RectTransform\Image)
    /// </summary>
    private void InitFadeOverlay()
    {
        GameObject overlayGo = new GameObject("SceneFadeOverlay");
        overlayGo.transform.SetParent(transform);
        Canvas canvas = overlayGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        overlayGo.AddComponent<GraphicRaycaster>();
        _fadeCanvasGroup = overlayGo.AddComponent<CanvasGroup>();
        _fadeCanvasGroup.alpha = 0f;
        _fadeCanvasGroup.blocksRaycasts = false;
        GameObject imageGo = new GameObject("BlackImage");
        imageGo.transform.SetParent(overlayGo.transform, false);
        Image image = imageGo.AddComponent<Image>();
        image.color=Color.black;
        RectTransform rect = imageGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
    }

    /// <summary>
    /// 异步加载场景
    /// </summary>
    public void LoadSceneAsync(
        string sceneName,
        Action onComplete = null,
        Action<float> onProgress = null,
        float fadeDuration = 0.4f
    )
    {
        if (_isLoading)
        {
            Debug.LogWarning($"[SceneManager] 正在加载场景中，请勿重复调用: {sceneName}");
            return;
        }
        StartCoroutine(CoLoadScene(sceneName, onComplete, onProgress, fadeDuration));
    }

    /// <summary>
    /// 场景切换前后黑屏渐变与资源释放的协程
    /// </summary>
    private IEnumerator CoLoadScene(
        string sceneName,
        Action oncomplete,
        Action<float> onProgress,
        float fadeDuration
    )
    {
        _isLoading = true;
        _fadeCanvasGroup.blocksRaycasts = true;
        Tweener fadeInTween = _fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
        yield return fadeInTween.WaitForCompletion();
        UIManager.Instance.CloseAll();
        PoolMgr.Instance.ClearAll();
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false; //禁止场景自动激活以保证能够平滑收集进度并控制转场
        while (op.progress < 0.9f)
        {
            onProgress?.Invoke(op.progress);
            yield return null;
        }
        onProgress?.Invoke(1f);
        op.allowSceneActivation = true;
        while (!op.isDone)
        {
            yield return null;
        }
        Tweener fadeOutTween = _fadeCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        yield return fadeOutTween.WaitForCompletion();
        _fadeCanvasGroup.blocksRaycasts = false;
        _isLoading = false;
        oncomplete?.Invoke();
    }
}
