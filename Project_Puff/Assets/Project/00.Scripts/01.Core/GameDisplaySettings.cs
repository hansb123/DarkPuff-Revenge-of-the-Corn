using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면 설정(전체화면 / 창 모드, 창 모드 해상도). PlayerPrefs에 저장되고, 게임을 켤 때 자동으로 적용된다.
/// - 전체화면: 모니터 해상도 그대로 (Fullscreen Window)
/// - 창 모드: 프리셋(16:9) 중 선택. 모니터보다 큰 프리셋은 목록에서 빠진다.
/// 한 번도 설정한 적이 없으면 Player Settings 기본값을 건드리지 않는다.
/// </summary>
public static class GameDisplaySettings
{
    private const string ModeKey = "Darkpuff.DisplayMode";   // 0 = 전체화면, 1 = 창 모드
    private const string ResKey = "Darkpuff.WindowedRes";   // 프리셋 인덱스(너비x높이 문자열로 저장)

    private static readonly Vector2Int[] Presets =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
    };

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Changed = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStart()
    {
        if (PlayerPrefs.HasKey(ModeKey)) Apply();
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(ModeKey, 0) == 0;
        set
        {
            if (value == Fullscreen && PlayerPrefs.HasKey(ModeKey)) return;
            PlayerPrefs.SetInt(ModeKey, value ? 0 : 1);
            Apply();
            Changed?.Invoke();
        }
    }

    /// <summary>모니터 크기에 들어가는 창 모드 해상도 목록 (최소 1개)</summary>
    public static List<Vector2Int> AvailableWindowed()
    {
        var list = new List<Vector2Int>();
        var desk = Screen.currentResolution;
        // 창 모드는 제목줄 등을 고려해 모니터보다 약간 작게 제한
        int maxW = Mathf.Max(640, desk.width), maxH = Mathf.Max(360, desk.height - 80);
        foreach (var p in Presets) if (p.x <= maxW && p.y <= maxH) list.Add(p);
        if (list.Count == 0) list.Add(Presets[0]);
        return list;
    }

    public static Vector2Int WindowedResolution
    {
        get
        {
            var list = AvailableWindowed();
            string saved = PlayerPrefs.GetString(ResKey, "");
            foreach (var r in list) if ($"{r.x}x{r.y}" == saved) return r;
            return list[list.Count - 1];   // 저장값이 없으면 들어가는 것 중 가장 큰 것
        }
        set
        {
            PlayerPrefs.SetString(ResKey, $"{value.x}x{value.y}");
            if (!Fullscreen) Apply();
            Changed?.Invoke();
        }
    }

    public static void Apply()
    {
        if (Fullscreen)
        {
            var d = Screen.currentResolution;
            Screen.SetResolution(d.width, d.height, FullScreenMode.FullScreenWindow);
        }
        else
        {
            var r = WindowedResolution;
            Screen.SetResolution(r.x, r.y, FullScreenMode.Windowed);
        }
    }

    public static void Save() => PlayerPrefs.Save();

    /// <summary>PlayerPrefs를 전부 지운 뒤, 미리 기억해 둔 화면 설정을 다시 기록한다 (진행 초기화에서 유지할 때 사용)</summary>
    public static void Persist(bool fullscreen, Vector2Int windowedResolution)
    {
        PlayerPrefs.SetInt(ModeKey, fullscreen ? 0 : 1);
        PlayerPrefs.SetString(ResKey, $"{windowedResolution.x}x{windowedResolution.y}");
    }
}