using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class AppLog
{
    //宏定义
    //当未开启宏时，编译器会在编译阶段直接从 IL 二进制中将整个方法调用代码彻底抹除
    //游戏发布并在 Player Settings -> Scripting Define Symbols 中移除了 ENABLE_LOG 宏，所有写在项目里的 AppLog.Log(...) 在打包编译时会被编译器直接蒸发删掉，不产生任何字符串分配与函数调用
    [Conditional("ENABLE_LOG")]
    [Conditional("UNITY_EDITOR")]
    public static void Log(string message)
    {
        Debug.Log($"[LOG] {message}");
    }

    [Conditional("ENABLE_LOG")]
    [Conditional("UNITY_EDITOR")]
    public static void Log(string tag, string message)
    {
        Debug.Log($"<color=#409EFF>[{tag}]</color> {message}");
    }

    public static void LogWarning(string message)
    {
        Debug.LogWarning($"<color=#E6A23C>[WARN]</color> {message}");
    }

    public static void LogWarning(string tag, string message)
    {
        Debug.LogWarning($"<color=#E6A23C>[{tag} WARN]</color> {message}");
    }

    public static void LogError(string message)
    {
        Debug.LogError($"<color=#F56C6C>[ERROR]</color> {message}");
    }

    public static void LogError(string tag, string message)
    {
        Debug.LogError($"<color=#F56C6C>[{tag} ERROR]</color> {message}");
    }
}
