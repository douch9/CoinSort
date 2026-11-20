using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 通过 OpenAI 兼容的中转站接口生成图片，并保存到指定目录。
/// 打开方式：菜单栏 Tools/AI/图片生成工具
/// </summary>
public class AIImageGeneratorWindow : EditorWindow
{
    private enum ApiMode
    {
        Auto = 0,   // 先试 Images 接口，失败自动转 Chat 接口
        Images = 1, // POST {base}/images/generations
        Chat = 2,   // POST {base}/chat/completions
    }

    private const string PrefBaseUrl = "AIImageGen.BaseUrl";
    private const string PrefApiKey = "AIImageGen.ApiKey";
    private const string PrefModel = "AIImageGen.Model";
    private const string PrefMode = "AIImageGen.Mode";
    private const string PrefSize = "AIImageGen.Size";
    private const string PrefCount = "AIImageGen.Count";
    private const string PrefFolder = "AIImageGen.Folder";
    private const string PrefSprite = "AIImageGen.ImportAsSprite";
    private const string PrefRememberKey = "AIImageGen.RememberKey";
    private const string PrefRefFieldName = "AIImageGen.RefFieldName";
    private const string PrefRefPaths = "AIImageGen.RefPaths";

    private const string DefaultFolder = "Assets/Generated/AIImage";

    private static readonly string[] ModeLabels =
    {
        "自动：Images 优先，失败自动转 Chat",
        "Images：/images/generations 或 /images/edits",
        "Chat：POST {base}/chat/completions",
    };

    private static readonly string[] ImageExtensions =
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".tga",
    };

    private static readonly string[] SizePresets = { "1024x1024", "1024x1536", "1536x1024", "512x512" };

    private static readonly HttpClient HttpClientInstance = CreateHttpClient();

    // --- 界面字段 ---
    private string baseUrl = "https://api.openai.com/v1";
    private string apiKey = "";
    private string model = "gpt-image-1";
    private ApiMode mode = ApiMode.Auto;
    private string size = "1024x1024";
    private int count = 1;
    private string prompt = "";
    private string outputFolder = DefaultFolder;
    private string fileName = "";
    private bool importAsSprite;
    private bool rememberKey = true;
    private bool showKey;

    // --- 模型列表 ---
    private readonly List<string> allModels = new List<string>();
    private string modelFilter = "";
    private bool showAllModels;
    private Vector2 modelListScroll;

    // --- 图生图参考图 ---
    private readonly List<string> referencePaths = new List<string>();
    private readonly List<Texture2D> referenceThumbs = new List<Texture2D>();
    private string referenceFieldName = "image";
    private Vector2 referenceScroll;

    // --- 运行状态 ---
    private bool busy;
    private string status = "";
    private bool statusIsError;
    private string lastSavedPath = "";
    private Texture2D preview;
    private Vector2 scroll;
    private CancellationTokenSource cts;

    private readonly ConcurrentQueue<Action> mainActions = new ConcurrentQueue<Action>();

    [MenuItem("Tools/AI/图片生成工具")]
    public static void ShowWindow()
    {
        var window = GetWindow<AIImageGeneratorWindow>("AI 图片生成");
        window.minSize = new Vector2(460f, 560f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += Pump;
        LoadSettings();
    }

    private void OnDisable()
    {
        EditorApplication.update -= Pump;
        SaveSettings();

        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }

        if (preview != null)
        {
            DestroyImmediate(preview);
            preview = null;
        }

        ClearReferences();
    }

    #region 设置持久化

    private void LoadSettings()
    {
        baseUrl = EditorPrefs.GetString(PrefBaseUrl, baseUrl);
        model = EditorPrefs.GetString(PrefModel, model);
        mode = (ApiMode)EditorPrefs.GetInt(PrefMode, (int)mode);
        size = EditorPrefs.GetString(PrefSize, size);
        count = EditorPrefs.GetInt(PrefCount, count);
        outputFolder = EditorPrefs.GetString(PrefFolder, outputFolder);
        importAsSprite = EditorPrefs.GetBool(PrefSprite, importAsSprite);
        rememberKey = EditorPrefs.GetBool(PrefRememberKey, rememberKey);
        apiKey = EditorPrefs.GetString(PrefApiKey, "");
        referenceFieldName = EditorPrefs.GetString(PrefRefFieldName, referenceFieldName);
        LoadStoredReferences(EditorPrefs.GetString(PrefRefPaths, ""));
    }

    private void LoadStoredReferences(string stored)
    {
        ClearReferences();

        if (string.IsNullOrEmpty(stored))
        {
            return;
        }

        foreach (string raw in stored.Split('\n'))
        {
            string path = raw.Trim();

            if (path.Length == 0)
            {
                continue;
            }

            string resolved = ResolveReferencePath(path);

            if (!File.Exists(resolved)
                || referencePaths.Exists(p => string.Equals(p, resolved, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            referencePaths.Add(resolved);
            referenceThumbs.Add(LoadThumbnail(resolved));
        }
    }

    private void SaveSettings()
    {
        EditorPrefs.SetString(PrefBaseUrl, baseUrl ?? "");
        EditorPrefs.SetString(PrefModel, model ?? "");
        EditorPrefs.SetInt(PrefMode, (int)mode);
        EditorPrefs.SetString(PrefSize, size ?? "");
        EditorPrefs.SetInt(PrefCount, count);
        EditorPrefs.SetString(PrefFolder, outputFolder ?? "");
        EditorPrefs.SetBool(PrefSprite, importAsSprite);
        EditorPrefs.SetBool(PrefRememberKey, rememberKey);
        EditorPrefs.SetString(PrefApiKey, rememberKey ? (apiKey ?? "") : "");
        EditorPrefs.SetString(PrefRefFieldName, referenceFieldName ?? "");
        EditorPrefs.SetString(PrefRefPaths, string.Join("\n", referencePaths));
    }

    #endregion

    #region 主线程消息泵

    private void Pump()
    {
        bool didWork = false;

        while (mainActions.TryDequeue(out Action action))
        {
            didWork = true;

            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                busy = false;
                SetStatus("失败：" + e.Message, true);
            }
        }

        if (didWork)
        {
            Repaint();
        }
    }

    private void Post(Action action)
    {
        mainActions.Enqueue(action);
    }

    private void SetStatus(string text, bool isError)
    {
        status = text;
        statusIsError = isError;
        Repaint();
    }

    #endregion

    #region 界面

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawApiSection();
        GUILayout.Space(6f);
        DrawPromptSection();
        GUILayout.Space(6f);
        DrawReferenceSection();
        GUILayout.Space(6f);
        DrawOutputSection();
        GUILayout.Space(10f);
        DrawActionSection();
        GUILayout.Space(6f);
        DrawPreviewSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawApiSection()
    {
        GUILayout.Label("1. 接口配置", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Base URL 填到 /v1 这一层，例如 https://your-relay.com/v1 。\n" +
            "若地址里没有 /v数字 会被自动补上 /v1。\n" +
            "API 密钥保存在 EditorPrefs（注册表明文），不想保留就取消下面的勾选。",
            MessageType.Info);

        baseUrl = EditorGUILayout.TextField("API Base URL", baseUrl);

        using (new EditorGUILayout.HorizontalScope())
        {
            apiKey = showKey
                ? EditorGUILayout.TextField("API 密钥", apiKey)
                : EditorGUILayout.PasswordField("API 密钥", apiKey);

            showKey = GUILayout.Toggle(showKey, "显示", GUILayout.Width(48f));
        }

        mode = (ApiMode)EditorGUILayout.Popup("接口模式", (int)mode, ModeLabels);
        model = EditorGUILayout.TextField("模型名称", model);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(busy || string.IsNullOrWhiteSpace(baseUrl)))
            {
                if (GUILayout.Button("获取可用模型", GUILayout.Width(110f)))
                {
                    FetchModels();
                }
            }

            GUILayout.Label(
                allModels.Count > 0
                    ? $"已获取 {allModels.Count} 个模型"
                    : "模型名报错时点这里，从密钥实际可用的列表里挑一个",
                EditorStyles.miniLabel);
        }

        if (allModels.Count > 0)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                modelFilter = EditorGUILayout.TextField("筛选", modelFilter);
                showAllModels = EditorGUILayout.ToggleLeft("显示全部", showAllModels, GUILayout.Width(80f));
            }

            DrawModelList();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            size = EditorGUILayout.TextField("图片尺寸", size);

            foreach (var preset in SizePresets)
            {
                if (GUILayout.Button(preset, EditorStyles.miniButton, GUILayout.Width(72f)))
                {
                    size = preset;
                    GUI.FocusControl(null);
                }
            }
        }

        count = EditorGUILayout.IntSlider("生成数量", count, 1, 4);
        rememberKey = EditorGUILayout.ToggleLeft("记住 API 密钥（明文写入 EditorPrefs）", rememberKey);
    }

    private void DrawPromptSection()
    {
        GUILayout.Label("2. 提示词", EditorStyles.boldLabel);
        prompt = EditorGUILayout.TextArea(prompt, EditorStyles.textArea, GUILayout.Height(88f));
    }

    private void DrawReferenceSection()
    {
        GUILayout.Label("3. 参考图（图生图，可留空）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "留空 = 纯文生图；放了参考图 = 图生图。\n" +
            "Images 模式会把参考图以 multipart 上传到 /images/edits；\n" +
            "Chat 模式会把参考图编码成 data URL 塞进 messages 的 content 数组。\n" +
            "建议最多 4 张，单张不要超过十几 MB。",
            MessageType.Info);

        Rect dropArea = GUILayoutUtility.GetRect(0f, 44f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea,
            referencePaths.Count == 0
                ? "把 png / jpg / webp 文件拖到这里（支持一次拖入多张）"
                : $"已添加 {referencePaths.Count} 张参考图，可继续拖入",
            EditorStyles.helpBox);
        HandleDragAndDrop(dropArea);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("添加文件…", GUILayout.Width(90f)))
            {
                AddReferenceFiles();
            }

            using (new EditorGUI.DisabledScope(referencePaths.Count == 0))
            {
                if (GUILayout.Button("清空", GUILayout.Width(60f)))
                {
                    ClearReferences();
                    GUI.FocusControl(null);
                }
            }

            GUILayout.Label($"共 {referencePaths.Count} 张", EditorStyles.miniLabel);
        }

        if (referencePaths.Count > 0)
        {
            referenceFieldName = EditorGUILayout.TextField(
                new GUIContent("表单字段名", "仅 Images/edits 用。默认 image；若中转站要求数组写法可改成 image[]"),
                referenceFieldName);

            DrawReferenceList();
        }
    }

    private void DrawReferenceList()
    {
        float height = Mathf.Min(150f, 44f * referencePaths.Count + 8f);
        referenceScroll = EditorGUILayout.BeginScrollView(referenceScroll, GUILayout.Height(height));

        int removeIndex = -1;

        for (int i = 0; i < referencePaths.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Texture2D thumb = i < referenceThumbs.Count ? referenceThumbs[i] : null;
                Rect thumbRect = GUILayoutUtility.GetRect(40f, 40f, GUILayout.Width(40f), GUILayout.Height(40f));

                if (thumb != null)
                {
                    EditorGUI.DrawPreviewTexture(thumbRect, thumb, null, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(thumbRect, new Color(0.3f, 0.3f, 0.3f, 0.4f));
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(Path.GetFileName(referencePaths[i]), EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(Shorten(referencePaths[i], 68), EditorStyles.miniLabel);
                }

                if (GUILayout.Button("移除", EditorStyles.miniButton, GUILayout.Width(44f)))
                {
                    removeIndex = i;
                }
            }
        }

        EditorGUILayout.EndScrollView();

        if (removeIndex >= 0)
        {
            RemoveReferenceAt(removeIndex);
            GUI.FocusControl(null);
        }
    }

    private void HandleDragAndDrop(Rect area)
    {
        Event evt = Event.current;

        if (evt == null || !area.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.type != UnityEngine.EventType.DragUpdated && evt.type != UnityEngine.EventType.DragPerform)
        {
            return;
        }

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

        if (evt.type == UnityEngine.EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            AddReferences(DragAndDrop.paths);
            evt.Use();
        }
        else
        {
            Repaint();
        }
    }

    private void AddReferenceFiles()
    {
        string start = referencePaths.Count > 0
            ? Path.GetDirectoryName(referencePaths[referencePaths.Count - 1])
            : null;

        if (string.IsNullOrEmpty(start))
        {
            start = Directory.GetParent(Application.dataPath).FullName;
        }

        string picked = EditorUtility.OpenFilePanelWithFilters(
            "选择参考图",
            start,
            new[] { "图片", "png,jpg,jpeg,webp,bmp,gif,tga" });

        if (string.IsNullOrEmpty(picked))
        {
            return;
        }

        AddReferences(new[] { picked });
        GUI.FocusControl(null);
    }

    private void AddReferences(IEnumerable<string> paths)
    {
        int added = 0;

        foreach (string raw in paths)
        {
            if (string.IsNullOrEmpty(raw) || !IsImageFile(raw))
            {
                continue;
            }

            string resolved = ResolveReferencePath(raw);

            if (!File.Exists(resolved)
                || referencePaths.Exists(p => string.Equals(p, resolved, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            referencePaths.Add(resolved);
            referenceThumbs.Add(LoadThumbnail(resolved));
            added++;
        }

        if (added > 0)
        {
            GUI.FocusControl(null);
            Repaint();
        }
    }

    private void RemoveReferenceAt(int index)
    {
        if (index < 0 || index >= referencePaths.Count)
        {
            return;
        }

        if (index < referenceThumbs.Count)
        {
            if (referenceThumbs[index] != null)
            {
                DestroyImmediate(referenceThumbs[index]);
            }

            referenceThumbs.RemoveAt(index);
        }

        referencePaths.RemoveAt(index);
    }

    private void ClearReferences()
    {
        foreach (Texture2D thumb in referenceThumbs)
        {
            if (thumb != null)
            {
                DestroyImmediate(thumb);
            }
        }

        referenceThumbs.Clear();
        referencePaths.Clear();
    }

    private void DrawOutputSection()
    {
        GUILayout.Label("4. 输出设置", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            outputFolder = EditorGUILayout.TextField("输出目录", outputFolder);

            if (GUILayout.Button("浏览", GUILayout.Width(56f)))
            {
                BrowseFolder();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(outputFolder)))
            {
                if (GUILayout.Button("打开", GUILayout.Width(56f)))
                {
                    var resolved = ResolveFolder(outputFolder);
                    Directory.CreateDirectory(resolved);
                    EditorUtility.RevealInFinder(resolved);
                }
            }
        }

        fileName = EditorGUILayout.TextField("文件名（留空用时间戳）", fileName);
        importAsSprite = EditorGUILayout.ToggleLeft("导入为 Sprite (2D and UI)", importAsSprite);
    }

    private void DrawActionSection()
    {
        using (new EditorGUI.DisabledScope(busy))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("生成图片", GUILayout.Height(30f)))
                {
                    Generate();
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(baseUrl)))
                {
                    if (GUILayout.Button("测试连接", GUILayout.Height(30f), GUILayout.Width(90f)))
                    {
                        TestConnection();
                    }
                }

                using (new EditorGUI.DisabledScope(cts == null))
                {
                    if (GUILayout.Button("取消", GUILayout.Height(30f), GUILayout.Width(60f)))
                    {
                        cts.Cancel();
                        SetStatus("正在取消…", false);
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(status))
        {
            EditorGUILayout.HelpBox(status, statusIsError ? MessageType.Error : MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(lastSavedPath)))
        {
            if (GUILayout.Button("在文件管理器中显示上一张图片"))
            {
                EditorUtility.RevealInFinder(lastSavedPath);
            }
        }
    }

    private void DrawPreviewSection()
    {
        GUILayout.Label("5. 预览", EditorStyles.boldLabel);

        if (preview == null)
        {
            EditorGUILayout.HelpBox("还没有生成结果。", MessageType.None);
            return;
        }

        float maxWidth = Mathf.Max(64f, position.width - 40f);
        float maxHeight = 320f;
        float aspect = (float)preview.height / Mathf.Max(1, preview.width);
        float width = Mathf.Min(maxWidth, maxHeight / Mathf.Max(0.01f, aspect));
        float height = width * aspect;

        Rect rect = GUILayoutUtility.GetRect(width, height);
        EditorGUI.DrawPreviewTexture(rect, preview, null, ScaleMode.ScaleToFit);
        EditorGUILayout.LabelField($"{preview.width} x {preview.height}", EditorStyles.miniLabel);
    }

    private void BrowseFolder()
    {
        string start = ResolveFolder(outputFolder);
        string picked = EditorUtility.OpenFolderPanel("选择图片输出目录", start, "");

        if (string.IsNullOrEmpty(picked))
        {
            return;
        }

        outputFolder = ToProjectRelative(picked);
        GUI.FocusControl(null);
    }

    #endregion

    #region 生成流程

    private void Generate()
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            SetStatus("请先填写 API Base URL。", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            SetStatus("请先填写模型名称。", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            SetStatus("请先填写提示词。", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            SetStatus("请先指定输出目录。", true);
            return;
        }

        var missing = referencePaths.FindAll(p => !File.Exists(p));

        if (missing.Count > 0)
        {
            SetStatus("这些参考图找不到了，请移除后重新添加：" + Shorten(string.Join("；", missing), 200), true);
            return;
        }

        var ctx = new RequestContext
        {
            BaseUrl = NormalizeBaseUrl(baseUrl),
            ApiKey = (apiKey ?? "").Trim(),
            Model = model.Trim(),
            Prompt = prompt,
            Size = (size ?? "").Trim(),
            Count = Mathf.Clamp(count, 1, 4),
            Mode = mode,
            Folder = outputFolder,
            FileName = fileName,
            ImportAsSprite = importAsSprite,
            ReferencePaths = new List<string>(referencePaths),
            ReferenceFieldName = string.IsNullOrWhiteSpace(referenceFieldName) ? "image" : referenceFieldName.Trim(),
        };

        SaveSettings();

        busy = true;
        BeginRequest();
        SetStatus(ctx.ReferencePaths.Count > 0
            ? $"正在请求 {ctx.BaseUrl}（图生图，{ctx.ReferencePaths.Count} 张参考图）…"
            : "正在请求 " + ctx.BaseUrl + " …", false);

        CancellationToken token = cts.Token;

        Task.Run(async () =>
        {
            try
            {
                // 参考图只读一次，多张生成结果共用
                ctx.References = LoadReferences(ctx.ReferencePaths);

                for (int i = 0; i < ctx.Count; i++)
                {
                    int index = i;
                    PostStatus($"生成中 {index + 1}/{ctx.Count} …");

                    byte[] bytes = await GenerateImageAsync(ctx, token);
                    Post(() => SaveImage(bytes, ctx, index));
                }

                Post(() =>
                {
                    busy = false;
                    SetStatus($"完成，共 {ctx.Count} 张。", false);
                });
            }
            catch (OperationCanceledException)
            {
                Post(() =>
                {
                    busy = false;
                    SetStatus("已取消。", true);
                });
            }
            catch (Exception e)
            {
                Debug.LogError("[AI生图] " + e);

                bool modelIssue = LooksLikeModelError(e.Message);

                Post(() =>
                {
                    busy = false;
                    SetStatus("生成失败：" + e.Message, true);

                    if (modelIssue && allModels.Count == 0)
                    {
                        FetchModels(true);
                    }
                });
            }
        }, token);
    }

    private void TestConnection()
    {
        string url = NormalizeBaseUrl(baseUrl);
        string key = (apiKey ?? "").Trim();

        busy = true;
        BeginRequest();
        SetStatus("正在测试 " + url + " …", false);

        CancellationToken token = cts.Token;

        Task.Run(async () =>
        {
            try
            {
                JObject json = await GetJsonAsync(CombineUrl(url, "models"), key, token);
                JArray list = Child(json, "data") as JArray;
                string message = list != null
                    ? $"连接成功，接口返回 {list.Count} 个模型。"
                    : "连接成功，响应：" + Shorten(json.ToString(Formatting.None), 200);

                Post(() =>
                {
                    busy = false;
                    SetStatus(message, false);
                });
            }
            catch (OperationCanceledException)
            {
                Post(() =>
                {
                    busy = false;
                    SetStatus("已取消。", true);
                });
            }
            catch (Exception e)
            {
                Post(() =>
                {
                    busy = false;
                    SetStatus("连接失败：" + e.Message, true);
                });
            }
        }, token);
    }

    private static readonly string[] ImageModelKeywords =
    {
        "image", "img", "dall", "flux", "banana", "seedream", "seededit", "imagen",
        "kolors", "midjourney", "sdxl", "stable-diffusion", "ideogram", "recraft",
        "janus", "cogview", "hunyuan", "qwen-image", "wanx", "grok-image", "z-image",
    };

    private static bool LooksLikeImageModel(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        foreach (string keyword in ImageModelKeywords)
        {
            if (id.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeModelError(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        bool mentionsModel = message.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0;

        return mentionsModel
               && (message.IndexOf("not_found", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("not supported", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("不存在", StringComparison.Ordinal) >= 0
                   || message.IndexOf("404", StringComparison.Ordinal) >= 0);
    }

    private void DrawModelList()
    {
        var filtered = new List<string>();

        foreach (string id in allModels)
        {
            if (!showAllModels && !LooksLikeImageModel(id))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(modelFilter)
                && id.IndexOf(modelFilter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            filtered.Add(id);
        }

        EditorGUILayout.LabelField(
            $"匹配 {filtered.Count} 个（疑似图片模型，勾『显示全部』看完整列表）",
            EditorStyles.miniLabel);

        modelListScroll = EditorGUILayout.BeginScrollView(modelListScroll, GUILayout.Height(118f));

        const int maxRows = 300;

        for (int i = 0; i < filtered.Count && i < maxRows; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool isCurrent = string.Equals(filtered[i], model, StringComparison.OrdinalIgnoreCase);

                EditorGUILayout.LabelField(
                    isCurrent ? filtered[i] + "  ← 当前" : filtered[i],
                    isCurrent ? EditorStyles.boldLabel : EditorStyles.label);

                if (GUILayout.Button("选用", EditorStyles.miniButton, GUILayout.Width(44f)))
                {
                    model = filtered[i];
                    GUI.FocusControl(null);
                }
            }
        }

        if (filtered.Count == 0)
        {
            EditorGUILayout.LabelField("没有匹配项，改一下『筛选』或勾上『显示全部』。", EditorStyles.miniLabel);
        }
        else if (filtered.Count > maxRows)
        {
            EditorGUILayout.LabelField("列表过长已截断，请用『筛选』缩小范围。", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndScrollView();
    }

    private void FetchModels(bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            SetStatus("请先填写 API Base URL。", true);
            return;
        }

        string url = NormalizeBaseUrl(baseUrl);
        string key = (apiKey ?? "").Trim();

        busy = true;
        BeginRequest();

        if (!silent)
        {
            SetStatus("正在获取模型列表 " + url + " …", false);
        }

        CancellationToken token = cts.Token;

        Task.Run(async () =>
        {
            try
            {
                JObject json = await GetJsonAsync(CombineUrl(url, "models"), key, token);

                JArray list = Child(json, "data") as JArray ?? Child(json, "models") as JArray;
                var ids = new List<string>();

                if (list != null)
                {
                    foreach (JToken item in list)
                    {
                        string id = Child(item, "id")?.ToString()
                                    ?? Child(item, "name")?.ToString()
                                    ?? (item.Type == JTokenType.String ? item.ToString() : null);

                        if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                        {
                            ids.Add(id);
                        }
                    }
                }

                ids.Sort(StringComparer.OrdinalIgnoreCase);

                Post(() =>
                {
                    allModels.Clear();
                    allModels.AddRange(ids);
                    busy = false;

                    if (ids.Count == 0)
                    {
                        string fail = "接口没有返回模型列表，响应：" + Shorten(json.ToString(Formatting.None), 200);
                        SetStatus(silent && !string.IsNullOrEmpty(status) ? status + "\n" + fail : fail, true);
                    }
                    else
                    {
                        string ok = $"获取到 {ids.Count} 个模型，疑似图片模型已列在『模型名称』下方，点『选用』即可。";
                        SetStatus(silent && !string.IsNullOrEmpty(status) ? status + "\n" + ok : ok, silent && statusIsError);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                Post(() =>
                {
                    busy = false;
                    SetStatus("已取消。", true);
                });
            }
            catch (Exception e)
            {
                Post(() =>
                {
                    busy = false;
                    string fail = "获取模型列表失败：" + e.Message;
                    SetStatus(silent && !string.IsNullOrEmpty(status) ? status + "\n" + fail : fail, true);
                });
            }
        }, token);
    }

    private void BeginRequest()
    {
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        cts = new CancellationTokenSource();
    }

    private void PostStatus(string text)
    {
        Post(() => SetStatus(text, false));
    }

    private async Task<byte[]> GenerateImageAsync(RequestContext ctx, CancellationToken token)
    {
        if (ctx.Mode == ApiMode.Chat)
        {
            return await ChatAsync(ctx, token);
        }

        if (ctx.Mode == ApiMode.Images)
        {
            return await ImagesAsync(ctx, token);
        }

        // Auto：优先 Images 接口
        try
        {
            return await ImagesAsync(ctx, token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            bool hasRefs = ctx.References != null && ctx.References.Count > 0;

            PostStatus((hasRefs ? "Images/edits 图生图接口不可用" : "Images 接口不可用")
                       + "，改用 Chat 接口重试（" + Shorten(e.Message, 120) + "）");

            return await ChatAsync(ctx, token);
        }
    }

    private static async Task<byte[]> ImagesAsync(RequestContext ctx, CancellationToken token)
    {
        // 有参考图时改走图生图端点
        if (ctx.References != null && ctx.References.Count > 0)
        {
            return await EditsAsync(ctx, token);
        }

        string url = CombineUrl(ctx.BaseUrl, "images/generations");

        var body = new JObject
        {
            ["model"] = ctx.Model,
            ["prompt"] = ctx.Prompt,
            ["n"] = 1,
        };

        if (!string.IsNullOrEmpty(ctx.Size))
        {
            body["size"] = ctx.Size;
        }

        // gpt-image-1 固定返回 b64_json，不接受 response_format 参数
        if (!ctx.Model.StartsWith("gpt-image", StringComparison.OrdinalIgnoreCase))
        {
            body["response_format"] = "b64_json";
        }

        JObject json = await PostJsonAsync(url, ctx.ApiKey, body, token);

        return await ExtractImageFromImagesPayload(json, ctx.ApiKey, token);
    }

    /// <summary>
    /// 解析 images/* 端点返回体：data[0].b64_json 或 data[0].url
    /// </summary>
    private static async Task<byte[]> ExtractImageFromImagesPayload(JObject json, string apiKey, CancellationToken token)
    {
        JArray data = Child(json, "data") as JArray;
        if (data == null || data.Count == 0)
        {
            throw new Exception("响应中没有 data 数组：" + Shorten(json.ToString(Formatting.None), 200));
        }

        JToken item = data[0];

        string b64 = Child(item, "b64_json")?.ToString();
        if (!string.IsNullOrEmpty(b64))
        {
            return Convert.FromBase64String(StripWhitespace(b64));
        }

        string imageUrl = Child(item, "url")?.ToString();
        if (!string.IsNullOrEmpty(imageUrl))
        {
            return await DownloadAsync(imageUrl, apiKey, token);
        }

        throw new Exception("响应中没有 b64_json 或 url：" + Shorten(item.ToString(Formatting.None), 200));
    }

    /// <summary>
    /// 图生图：multipart/form-data 上传参考图到 /images/edits
    /// </summary>
    private static async Task<byte[]> EditsAsync(RequestContext ctx, CancellationToken token)
    {
        string url = CombineUrl(ctx.BaseUrl, "images/edits");

        // 注意：这里不能用 using 包 form。
        // form 赋给 request.Content 后，request 释放时会连带释放 form；
        // Mono 的 MultipartContent.Dispose 第二次执行会抛 NullReferenceException。
        var form = new MultipartFormDataContent();

        form.Add(PlainTextContent(ctx.Model), "model");
        form.Add(PlainTextContent(ctx.Prompt), "prompt");
        form.Add(PlainTextContent("1"), "n");

        if (!string.IsNullOrEmpty(ctx.Size))
        {
            form.Add(PlainTextContent(ctx.Size), "size");
        }

        // gpt-image-1 固定返回 b64_json，不接受 response_format 参数
        if (!ctx.Model.StartsWith("gpt-image", StringComparison.OrdinalIgnoreCase))
        {
            form.Add(PlainTextContent("b64_json"), "response_format");
        }

        string fieldName = string.IsNullOrEmpty(ctx.ReferenceFieldName) ? "image" : ctx.ReferenceFieldName;

        foreach (ReferenceImage reference in ctx.References)
        {
            var content = new ByteArrayContent(reference.Bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(reference.MimeType);
            form.Add(content, fieldName, reference.FileName);
        }

        using (var request = new HttpRequestMessage(HttpMethod.Post, url))
        {
            request.Content = form;
            JObject json = await SendAsync(request, ctx.ApiKey, token);

            return await ExtractImageFromImagesPayload(json, ctx.ApiKey, token);
        }
    }

    private static async Task<byte[]> ChatAsync(RequestContext ctx, CancellationToken token)
    {
        string url = CombineUrl(ctx.BaseUrl, "chat/completions");

        var body = new JObject
        {
            ["model"] = ctx.Model,
            ["stream"] = false,
            ["messages"] = new JArray(new JObject
            {
                ["role"] = "user",
                ["content"] = BuildChatContent(ctx),
            }),
        };

        JObject json = await PostJsonAsync(url, ctx.ApiKey, body, token);

        string text = ExtractMessageText(json);
        Exception lastError = null;

        // 先解析文本内容，解析不到再从整段 JSON 里捞（兼容 responses 风格与内嵌 base64 的返回）
        foreach (string candidate in new[] { text, json.ToString(Formatting.None) })
        {
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            try
            {
                return await ExtractImageAsync(candidate, ctx.ApiKey, token);
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                lastError = e;
            }
        }

        throw lastError ?? new Exception("未能从模型回复中解析出图片。");
    }

    private static async Task<byte[]> ExtractImageAsync(string text, string apiKey, CancellationToken token)
    {
        // 1) data:image/png;base64,xxxx
        Match match = Regex.Match(text, @"data:image/[a-zA-Z0-9.+-]+;base64,([A-Za-z0-9+/=\s]+)");
        if (match.Success)
        {
            return Convert.FromBase64String(StripWhitespace(match.Groups[1].Value));
        }

        // 2) b64_json 字段
        match = Regex.Match(text, "\"b64_json\"\\s*:\\s*\"([^\"]+)\"");
        if (match.Success)
        {
            return Convert.FromBase64String(StripWhitespace(match.Groups[1].Value));
        }

        // 3) markdown 图片 / <img src> / 裸链接
        Match urlMatch = Regex.Match(text, @"!\[[^\]]*\]\((https?://[^)\s]+)\)");

        if (!urlMatch.Success)
        {
            urlMatch = Regex.Match(text, "<img[^>]+src\\s*=\\s*[\"'](https?://[^\"']+)[\"']", RegexOptions.IgnoreCase);
        }

        if (!urlMatch.Success)
        {
            urlMatch = Regex.Match(text, @"(https?://[^\s""'<>)\]]+\.(?:png|jpe?g|webp|gif))", RegexOptions.IgnoreCase);
        }

        if (urlMatch.Success)
        {
            return await DownloadAsync(urlMatch.Groups[urlMatch.Groups.Count - 1].Value, apiKey, token);
        }

        throw new Exception("未能从模型回复中解析出图片：" + Shorten(text, 200));
    }

    private static JToken BuildChatContent(RequestContext ctx)
    {
        if (ctx.References == null || ctx.References.Count == 0)
        {
            return ctx.Prompt;
        }

        var parts = new JArray();

        // 参考图在前、文字指令在后，这是多数图生图模型的惯用顺序
        foreach (ReferenceImage reference in ctx.References)
        {
            parts.Add(new JObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JObject
                {
                    ["url"] = "data:" + reference.MimeType + ";base64," + Convert.ToBase64String(reference.Bytes),
                },
            });
        }

        parts.Add(new JObject
        {
            ["type"] = "text",
            ["text"] = ctx.Prompt,
        });

        return parts;
    }

    private static string ExtractMessageText(JObject json)
    {
        JToken message = Child(Child(Child(json, "choices"), 0), "message");
        JToken content = Child(message, "content");

        if (content == null)
        {
            return null;
        }

        if (content.Type != JTokenType.Array)
        {
            return content.ToString();
        }

        var sb = new StringBuilder();

        foreach (JToken part in (JArray)content)
        {
            if (part.Type != JTokenType.Object)
            {
                sb.AppendLine(part.ToString());
                continue;
            }

            string partText = Child(part, "text")?.ToString();
            if (!string.IsNullOrEmpty(partText))
            {
                sb.AppendLine(partText);
            }

            string partUrl = Child(Child(part, "image_url"), "url")?.ToString() ?? Child(part, "url")?.ToString();
            if (!string.IsNullOrEmpty(partUrl))
            {
                sb.AppendLine(partUrl);
            }

            // Gemini / 部分中转站把图片放在 inline_data.data 里
            JToken inline = Child(part, "inline_data") ?? Child(part, "inlineData");
            if (inline != null)
            {
                string data = Child(inline, "data")?.ToString();
                string mime = Child(inline, "mime_type")?.ToString()
                              ?? Child(inline, "mimeType")?.ToString()
                              ?? "image/png";

                if (!string.IsNullOrEmpty(data))
                {
                    sb.AppendLine("data:" + mime + ";base64," + data);
                }
            }
        }

        return sb.ToString();
    }

    #endregion

    #region 保存与导入

    private void SaveImage(byte[] bytes, RequestContext ctx, int index)
    {
        string folder = ResolveFolder(ctx.Folder);
        Directory.CreateDirectory(folder);

        string name = string.IsNullOrWhiteSpace(ctx.FileName)
            ? $"ai_{DateTime.Now:yyyyMMdd_HHmmss}"
            : Path.GetFileNameWithoutExtension(ctx.FileName.Trim());

        if (ctx.Count > 1)
        {
            name += "_" + (index + 1);
        }

        string fullPath = EnsureUniquePath(Path.Combine(folder, name + ".png"));
        File.WriteAllBytes(fullPath, bytes);

        string assetPath = ToAssetPath(fullPath);
        if (!string.IsNullOrEmpty(assetPath))
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (ctx.ImportAsSprite)
            {
                ApplySpriteImport(assetPath);
            }
        }

        AssetDatabase.Refresh();

        lastSavedPath = fullPath;
        SetPreview(bytes);
        SetStatus("已保存：" + fullPath, false);
        Debug.Log("[AI生图] 已保存 " + fullPath);
    }

    private static void ApplySpriteImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private void SetPreview(byte[] bytes)
    {
        if (preview != null)
        {
            DestroyImmediate(preview);
            preview = null;
        }

        var texture = new Texture2D(2, 2);
        if (texture.LoadImage(bytes))
        {
            preview = texture;
        }
        else
        {
            DestroyImmediate(texture);
        }
    }

    #endregion

    #region HTTP 与路径工具

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "UnityAIImageGenerator/1.0");
        return client;
    }

    private static async Task<JObject> PostJsonAsync(string url, string apiKey, JObject body, CancellationToken token)
    {
        using (var request = new HttpRequestMessage(HttpMethod.Post, url))
        {
            request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            return await SendAsync(request, apiKey, token);
        }
    }

    private static async Task<JObject> GetJsonAsync(string url, string apiKey, CancellationToken token)
    {
        using (var request = new HttpRequestMessage(HttpMethod.Get, url))
        {
            return await SendAsync(request, apiKey, token);
        }
    }

    private static async Task<JObject> SendAsync(HttpRequestMessage request, string apiKey, CancellationToken token)
    {
        AddAuth(request, apiKey);

        using (HttpResponseMessage response = await HttpClientInstance.SendAsync(request, token))
        {
            string text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} @ {request.RequestUri}："
                    + Shorten(text, 300)
                    + BuildErrorHint((int)response.StatusCode, text));
            }

            try
            {
                return JObject.Parse(text);
            }
            catch (Exception)
            {
                throw new Exception("接口返回的不是 JSON：" + Shorten(text, 300));
            }
        }
    }

    /// <summary>
    /// multipart 里的普通文本字段。显式去掉 charset，部分中转站会因 charset 解析失败。
    /// </summary>
    private static StringContent PlainTextContent(string value)
    {
        var content = new StringContent(value ?? "");
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        return content;
    }

    private static async Task<byte[]> DownloadAsync(string url, string apiKey, CancellationToken token)
    {
        using (var request = new HttpRequestMessage(HttpMethod.Get, url))
        {
            AddAuth(request, apiKey);

            using (HttpResponseMessage response = await HttpClientInstance.SendAsync(request, token))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"下载图片失败 HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                return await response.Content.ReadAsByteArrayAsync();
            }
        }
    }

    private static void AddAuth(HttpRequestMessage request, string apiKey)
    {
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
        }
    }

    private static string NormalizeBaseUrl(string url)
    {
        url = (url ?? "").Trim().TrimEnd('/');

        if (!string.IsNullOrEmpty(url) && !Regex.IsMatch(url, @"/v\d"))
        {
            url += "/v1";
        }

        return url;
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        return (baseUrl ?? "").TrimEnd('/') + "/" + path.TrimStart('/');
    }

    private static string ResolveFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = DefaultFolder;
        }

        folder = folder.Trim().Trim('"');

        if (Path.IsPathRooted(folder))
        {
            return Path.GetFullPath(folder);
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, folder));
    }

    private static string ToAssetPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            return null;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName
            .Replace('\\', '/')
            .TrimEnd('/') + "/";

        string normalized = Path.GetFullPath(fullPath).Replace('\\', '/');

        return normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
            ? normalized.Substring(projectRoot.Length)
            : null;
    }

    private static string ToProjectRelative(string absolutePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName
            .Replace('\\', '/')
            .TrimEnd('/') + "/";

        string normalized = Path.GetFullPath(absolutePath).Replace('\\', '/');

        return normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
            ? normalized.Substring(projectRoot.Length)
            : normalized;
    }

    private static string EnsureUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        string dir = Path.GetDirectoryName(path);
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);

        for (int i = 1; i < 1000; i++)
        {
            string candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(dir, $"{name}_{DateTime.Now:HHmmss}{ext}");
    }

    private static JToken Child(JToken token, string name)
    {
        return token is JObject obj ? obj[name] : null;
    }

    private static JToken Child(JToken token, int index)
    {
        if (!(token is JArray array) || index < 0 || index >= array.Count)
        {
            return null;
        }

        return array[index];
    }

    private static string StripWhitespace(string value)
    {
        return Regex.Replace(value ?? "", @"\s", "");
    }

    private static string Shorten(string value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        value = value.Replace("\r", " ").Replace("\n", " ");
        return value.Length <= max ? value : value.Substring(0, max) + "…";
    }

    private static bool IsImageFile(string path)
    {
        string ext = Path.GetExtension(path) ?? "";

        foreach (string known in ImageExtensions)
        {
            if (string.Equals(ext, known, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 工程外的绝对路径直接用；拖进来的工程资源是 Assets/... 这种相对路径，按项目根拼回绝对路径。
    /// </summary>
    private static string ResolveReferencePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        path = path.Trim().Trim('"');

        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    private static string GuessMimeType(string path)
    {
        switch ((Path.GetExtension(path) ?? "").ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
                return "image/jpeg";
            case ".webp":
                return "image/webp";
            case ".gif":
                return "image/gif";
            case ".bmp":
                return "image/bmp";
            case ".tga":
                return "image/x-tga";
            default:
                return "image/png";
        }
    }

    private static List<ReferenceImage> LoadReferences(List<string> paths)
    {
        var result = new List<ReferenceImage>();

        if (paths == null)
        {
            return result;
        }

        foreach (string path in paths)
        {
            result.Add(new ReferenceImage
            {
                Path = path,
                FileName = Path.GetFileName(path),
                MimeType = GuessMimeType(path),
                Bytes = File.ReadAllBytes(path),
            });
        }

        return result;
    }

    private static Texture2D LoadThumbnail(string path)
    {
        try
        {
            var texture = new Texture2D(2, 2);

            if (texture.LoadImage(File.ReadAllBytes(path)))
            {
                return texture;
            }

            UnityEngine.Object.DestroyImmediate(texture);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AI生图] 无法读取参考图缩略图 " + path + "：" + e.Message);
        }

        return null;
    }

    private static string BuildErrorHint(int statusCode, string body)
    {
        string text = body ?? "";
        bool modelIssue = text.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0
                          && (statusCode == 404
                              || text.IndexOf("not_found", StringComparison.OrdinalIgnoreCase) >= 0
                              || text.IndexOf("not supported", StringComparison.OrdinalIgnoreCase) >= 0);

        if (!modelIssue)
        {
            return "";
        }

        return "\n\n提示：这是『模型名』不被该密钥所在分组受理，接口地址本身是对的（请求已经打到 images 端点并返回了业务错误）。" +
               "点上面的『获取可用模型』，从列表里挑一个（图片模型一般带 image / flux / banana / seedream 等字样），它会自动填回『模型名称』。\n" +
               "要注意有些模型只支持纯文生图、不支持参考图：这类模型在带参考图时会被中转站拒绝，去掉参考图就能用。";
    }

    #endregion

    private class RequestContext
    {
        public string BaseUrl;
        public string ApiKey;
        public string Model;
        public string Prompt;
        public string Size;
        public int Count;
        public ApiMode Mode;
        public string Folder;
        public string FileName;
        public bool ImportAsSprite;
        public List<string> ReferencePaths;
        public string ReferenceFieldName;
        public List<ReferenceImage> References;
    }

    private class ReferenceImage
    {
        public string Path;
        public string FileName;
        public string MimeType;
        public byte[] Bytes;
    }
}