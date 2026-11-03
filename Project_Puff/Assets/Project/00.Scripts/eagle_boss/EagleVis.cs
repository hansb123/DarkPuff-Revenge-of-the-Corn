using UnityEngine;

/// <summary>스프라이트 없이 사각형으로 경고/번개 등을 그리기 위한 도우미 (임시 비주얼 → 나중에 아트로 교체 가능)</summary>
public static class EagleVis
{
    private static Sprite white;

    /// <summary>1×1 유닛 크기의 흰색 사각형 스프라이트 (중심 피벗)</summary>
    public static Sprite White
    {
        get
        {
            if (white != null) return white;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return white;
        }
    }

    public static SpriteRenderer MakeRect(Transform parent, string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = White;
        sr.sortingOrder = sortingOrder;
        sr.enabled = false;
        return sr;
    }

    /// <summary>중심 (cx, cy), 크기 (w, h) 로 배치</summary>
    public static void Place(SpriteRenderer sr, float cx, float cy, float w, float h)
    {
        sr.transform.position = new Vector3(cx, cy, 0f);
        sr.transform.localScale = new Vector3(w, h, 1f);
    }
}
