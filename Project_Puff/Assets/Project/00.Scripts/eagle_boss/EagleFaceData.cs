using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 포즈(스프라이트 이름)별 눈 위치 표. 그림 파일(2400×1800, 왼쪽 위가 원점)에서 눈(정면은 두 눈의 가운데)이 있는 픽셀 좌표.
/// EaglePoseController 가 이 값으로 얼굴 피격 콜라이더와 눈 번쩍임 위치를 포즈마다 자동으로 맞춘다.
/// 새 포즈를 그리면 여기에 한 줄 추가. 스프라이트 이름은 파일 이름(확장자 제외)과 같아야 한다.
/// </summary>
public static class EagleFaceData
{
    public const float Width = 2400f;
    public const float Height = 1800f;

    private static readonly Dictionary<string, Vector2> eye = new Dictionary<string, Vector2>
    {
        { "eagle_idle", new Vector2(756f, 560f) },
        { "eagle_flap_up", new Vector2(756f, 685f) },
        { "eagle_flap_down", new Vector2(756f, 590f) },
        { "eagle_spread", new Vector2(752f, 526f) },
        { "eagle_dive", new Vector2(702f, 985f) },
        { "eagle_stand", new Vector2(776f, 730f) },
        { "eagle_swing", new Vector2(749f, 837f) },
        { "eagle_windup", new Vector2(798f, 636f) },
        { "eagle_hurt", new Vector2(796f, 650f) },
        { "eagle_roar", new Vector2(836f, 654f) },
        { "eagle_groggy", new Vector2(790f, 1428f) },
        { "eagle_sky_groggy", new Vector2(826f, 982f) },
        { "eagle_front_idle_1", new Vector2(1200f, 572f) },
        { "eagle_front_idle_2", new Vector2(1200f, 595f) },
        { "eagle_front_roar", new Vector2(1192f, 549f) },
        { "eagle_front_windup", new Vector2(1200f, 634f) },
        { "eagle_front_groggy", new Vector2(1200f, 696f) },
        { "eagle_front_strike", new Vector2(1200f, 551f) },
    };

    // 스프라이트에서 가장 아래 불투명 픽셀의 y (위에서부터, 2400x1800 기준) — 바닥에 닿는 위치 계산용
    private static readonly Dictionary<string, float> bottom = new Dictionary<string, float>
    {
        { "eagle_idle", 1451f },
        { "eagle_flap_up", 1576f },
        { "eagle_flap_down", 1481f },
        { "eagle_spread", 1421f },
        { "eagle_dive", 1553f },
        { "eagle_stand", 1639f },
        { "eagle_swing", 1752f },
        { "eagle_windup", 1492f },
        { "eagle_hurt", 1461f },
        { "eagle_roar", 1514f },
        { "eagle_groggy", 1663f },
        { "eagle_sky_groggy", 1561f },
        { "eagle_front_idle_1", 1650f },
        { "eagle_front_idle_2", 1673f },
        { "eagle_front_roar", 1638f },
        { "eagle_front_windup", 1685f },
        { "eagle_front_groggy", 1703f },
        { "eagle_front_strike", 1703f },
    };

    public static bool TryGetBottom(string spriteName, out float pixel) => bottom.TryGetValue(Base(spriteName), out pixel);

    // 화난(_rage) 스프라이트는 기본 스프라이트와 머리/눈/바닥 위치가 같다
    private static string Base(string n) => n != null && n.EndsWith("_rage") ? n.Substring(0, n.Length - 5) : n;

    public static bool TryGetEye(string spriteName, out Vector2 pixel) => eye.TryGetValue(Base(spriteName), out pixel);
}