using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class HttpResult<T> //通用标准化业务网络响应数据包模型
{
    public int code;
    public string message;
    public T data;
}

public class NetworkManager : MonoSingleton<NetworkManager>
{
    [Header("网络超时配置")]
    [SerializeField]
    private int _defaultTimeout = 10;

    private string _token; //暂存当前用户登录鉴权Token
    private readonly Dictionary<string, string> _commonHeaders = new Dictionary<string, string>();

    public string Token => _token;

    public void SetToken(string token)
    {
        _token = token;
        if (!string.IsNullOrEmpty(token))
        {
            SetHeader("Authorization", $"Bearer {token}");
        }
        else
        {
            _commonHeaders.Remove("Authorization");
        }
    }

    public void SetHeader(string key, string value)
    {
        _commonHeaders[key] = value;
    }

    public void RemoveHeader(string key)
    {
        _commonHeaders.Remove(key);
    }

    /// <summary>
    /// 发送标准化JSON业务请求并自动解析返回结果接口
    /// </summary>
    public void PostJson<TResponse>(
        string url,
        object postData,
        Action<TResponse> onSuccess,
        Action<string> onFail = null,
        bool autoShowToastOnFail = true,
        int timeout = -1
    )
    {
        string json = postData != null ? JsonConvert.SerializeObject(postData) : "{}";
        Post(
            url,
            json,
            (isHttpSuccess, responseText) =>
            {
                HandleResponse(isHttpSuccess, responseText, onSuccess, onFail, autoShowToastOnFail);
            },
            timeout
        );
    }

    public void GetJson<TResponse>(
        string url,
        Action<TResponse> onSuccess,
        Action<string> onFail = null,
        bool autoShowToastOnFail = true,
        int timeout = -1
    )
    {
        Get(
            url,
            (isHttpSuccess, responseText) =>
            {
                HandleResponse(isHttpSuccess, responseText, onSuccess, onFail, autoShowToastOnFail);
            },
            timeout
        );
    }

    /// <summary>
    /// 集中反序列化处理网络响应与业务状态码过滤
    /// </summary>
    private void HandleResponse<TResponse>(
        bool isHttpSuccess,
        string responseText,
        Action<TResponse> onSuccess,
        Action<string> onFail,
        bool autoShowToastOnFail
    )
    {
        if (!isHttpSuccess)
        {
            string error = string.IsNullOrEmpty(responseText)
                ? "网络连接异常，请检查网络"
                : responseText;
            if (autoShowToastOnFail)
            {
                ToastManager.Instance.Show(error);
            }
            onFail?.Invoke(error);
            return;
        }

        try
        {
            HttpResult<TResponse> result = JsonConvert.DeserializeObject<HttpResult<TResponse>>(
                responseText
            );
            if (result == null)
            {
                string parseErr = "服务器响应数据格式异常";
                if (autoShowToastOnFail)
                    ToastManager.Instance.Show(parseErr);
                onFail?.Invoke(parseErr);
                return;
            }

            if (result.code == 200 || result.code == 0)
            {
                onSuccess?.Invoke(result.data);
            }
            else
            {
                string businessErr = string.IsNullOrEmpty(result.message)
                    ? $"业务操作失败(错误码:{result.code})"
                    : result.message;
                if (autoShowToastOnFail)
                    ToastManager.Instance.Show(businessErr);
                onFail?.Invoke(businessErr);
            }
        }
        catch (Exception ex)
        {
            AppLog.LogError("Network", $"反序列化响应数据失败: {ex.Message}, 原文: {responseText}");
            string exMsg = "数据解析异常";
            if (autoShowToastOnFail)
                ToastManager.Instance.Show(exMsg);
            onFail?.Invoke(exMsg);
        }
    }

    public void Get(string url, Action<bool, string> callback, int timeout = -1)
    {
        StartCoroutine(CoGet(url, callback, timeout > 0 ? timeout : _defaultTimeout));
    }

    private IEnumerator CoGet(string url, Action<bool, string> callback, int timeout)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            ApplyHeaders(request);
            request.timeout = timeout;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                callback?.Invoke(true, request.downloadHandler.text);
            }
            else
            {
                Debug.LogError(
                    $"[NetworkManager] GET请求失败 URL: {url}, 错误信息: {request.error}"
                );
                callback?.Invoke(false, request.error);
            }
        }
    }

    /// <summary>
    /// 发送HTTP POST异步请求接口（携带JSON数据体）
    /// </summary>
    public void Post(string url, string jsonData, Action<bool, string> callback, int timeout = -1)
    {
        StartCoroutine(CoPost(url, jsonData, callback, timeout > 0 ? timeout : _defaultTimeout));
    }

    private IEnumerator CoPost(
        string url,
        string jsonData,
        Action<bool, string> callback,
        int timeout
    )
    {
        //将文本字符串按 UTF-8 字符编码格式转换成二进制字节流
        //jsonData ?? string.Empty（空合并运算）当 jsonData 不为 null 时，直接使用 jsonData否则使用空字符串 string.Empty（即 ""）
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData ?? string.Empty);
        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json"); //设置标准JSON数据类型请求头
            ApplyHeaders(request);
            request.timeout = timeout;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                callback?.Invoke(true, request.downloadHandler.text);
            }
            else
            {
                Debug.LogError(
                    $"[NetworkManager] POST请求失败 URL: {url}, 错误信息: {request.error}"
                );
                callback?.Invoke(false, request.error);
            }
        }
    }

    private void ApplyHeaders(UnityWebRequest request)
    {
        foreach (var pair in _commonHeaders)
        {
            request.SetRequestHeader(pair.Key, pair.Value);
        }
    }
}
