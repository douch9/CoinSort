using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoSingleton<UIManager>
{
    private Canvas _rootCanvas;
    private readonly Dictionary<UILayer, Transform> _layerRoots =
        new Dictionary<UILayer, Transform>();
    private readonly Dictionary<string, UIBase> _openedUIs = new Dictionary<string, UIBase>();

    protected override void OnInit()
    {
        base.OnInit();
        InitRootCanvas();
        InitLayers();
    }

    /// <summary>
    /// 初始化UI根节点与Canvas组件
    /// </summary>
    private void InitRootCanvas()
    {
        GameObject canvasGo = new GameObject("UICanvas");
        canvasGo.transform.SetParent(transform);
        _rootCanvas = canvasGo.AddComponent<Canvas>();
        _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay; //设置渲染模式为全屏幕覆盖
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f; //宽高适配比例各占50%保持平衡缩放
        canvasGo.AddComponent<GraphicRaycaster>(); //挂载UI射线检测组件使所有按钮可点击交互
    }

    /// <summary>
    /// 为UILayer枚举中的各个层级创建独立的分类挂载父节点
    /// </summary>
    private void InitLayers()
    {
        foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
        {
            GameObject layerGo = new GameObject(layer.ToString());
            layerGo.transform.SetParent(_rootCanvas.transform, false);
            RectTransform rect = layerGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            _layerRoots[layer] = layerGo.transform;
        }
    }

    public void OpenUIAsync<T>(string path, object args = null, Action<T> callback = null)
        where T : UIBase
    {
        string uiName = typeof(T).Name;
        if (_openedUIs.TryGetValue(uiName, out UIBase existingUI))
        {
            existingUI.OnOpen(args);
            callback?.Invoke(existingUI as T);
            return;
        }
        ResourcesManager.Instance.InstantiatePrefabAsync(
            path,
            go =>
            {
                if (go == null)
                {
                    Debug.LogError($"[UIManager] 加载UI预制体失败: {path}");
                    callback?.Invoke(null);
                    return;
                }
                T ui = go.GetComponent<T>();
                if (ui == null)
                {
                    Debug.LogError($"[UIManager] 预制体 {path} 上找不到组件: {uiName}");
                    Destroy(go);
                    callback?.Invoke(null);
                    return;
                }
                Transform parent = _layerRoots.TryGetValue(ui.Layer, out Transform root)
                    ? root
                    : _rootCanvas.transform;
                go.transform.SetParent(parent, false);
                go.name = uiName;
                ui.OnInit();
                ui.OnOpen(args);
                _openedUIs[uiName] = ui;
                callback?.Invoke(ui);
            }
        );
    }

    /// <summary>
    /// 根据UI类名关闭指定界面并支持动画完成回调
    /// </summary>
    public void CloseUI(string uiName, Action onClosed = null)
    {
        if (_openedUIs.TryGetValue(uiName, out UIBase ui))
        {
            if (ui.IsOpen)
            {
                ui.OnClose(onClosed);
            }
            else
            {
                onClosed?.Invoke();
            }
        }
    }

    /// <summary>
    /// 根据泛型类型关闭界面的快捷调用接口
    /// </summary>
    public void CloseUI<T>(Action onClosed = null)
        where T : UIBase
    {
        CloseUI(typeof(T).Name, onClosed);
    }

    /// <summary>
    /// 查询获取已打开的指定类型UI界面实例
    /// </summary>
    public T GetUI<T>()
        where T : UIBase
    {
        string uiName = typeof(T).Name;
        if (_openedUIs.TryGetValue(uiName, out UIBase ui))
        {
            return ui as T;
        }
        return null;
    }

    /// <summary>
    /// 关闭当前已打开的所有UI界面
    /// </summary>
    public void CloseAll()
    {
        foreach (var pair in _openedUIs)
        {
            if (pair.Value.IsOpen)
            {
                pair.Value.OnClose();
            }
        }
    }
}
