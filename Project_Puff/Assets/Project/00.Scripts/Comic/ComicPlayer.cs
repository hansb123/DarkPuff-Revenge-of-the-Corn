using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 만화 재생기. 코루틴으로 호출:  yield return ComicPlayer.Run(data);   (또는 StartCoroutine(ComicPlayer.Run(data)))
/// 검은 전체화면 위에 페이지를 한 장씩 페이드로 보여준다. UI는 자동 생성되고 끝나면 사라진다.
/// 넘기기: Space / Z / Enter / 마우스 클릭,  전체 건너뛰기: Esc.
/// Time.timeScale 이 0이어도 동작(전부 unscaled 시간).
/// </summary>
public static class ComicPlayer
{
    public static bool Playing { get; private set; }

    private const float FadeIn = 0.45f, FadeOut = 0.3f, InputLock = 0.5f, BlackGap = 0.2f;

    public static IEnumerator Run(ComicData data, bool markSeen = true)
    {
        if (data == null || data.pages == null || data.pages.Length == 0 || Playing) yield break;
        Playing = true;

        // ---- UI 생성 ----
        var root = new GameObject("ComicCanvas");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7000;

        var back = new GameObject("Back").AddComponent<Image>();
        back.transform.SetParent(root.transform, false);
        back.color = Color.black; back.raycastTarget = true;   // 뒤쪽 UI 클릭 방지
        Stretch(back.rectTransform);

        var shot = new GameObject("Page").AddComponent<Image>();
        shot.transform.SetParent(root.transform, false);
        shot.preserveAspect = true; shot.raycastTarget = false;
        Stretch(shot.rectTransform);
        shot.color = new Color(1, 1, 1, 0);

        var hint = MakeHint(root.transform);
        var sfx = root.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0f;

        bool skipAll = false;
        int count = data.pages.Length;

        for (int i = 0; i < count && !skipAll; i++)
        {
            if (data.pages[i] == null) continue;
            shot.sprite = data.pages[i];
            shot.rectTransform.anchoredPosition = Vector2.zero;
            SetHint(hint, 0f);

            if (data.pageSfx != null && i < data.pageSfx.Length && data.pageSfx[i] != null)
                sfx.PlayOneShot(data.pageSfx[i], GameAudioSettings.SfxVolume);

            bool shake = data.shakePages != null && System.Array.IndexOf(data.shakePages, i) >= 0;
            bool last = i == count - 1;

            for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime) { SetA(shot, t / FadeIn); yield return null; }
            SetA(shot, 1f);

            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                shot.rectTransform.anchoredPosition = shake && elapsed < 0.9f
                    ? Random.insideUnitCircle * (28f * (1f - elapsed / 0.9f))
                    : Vector2.zero;

                if (elapsed > InputLock) SetHint(hint, Mathf.Clamp01((elapsed - InputLock) * 2f));
                if (SkipPressed()) { skipAll = true; break; }
                if (elapsed > InputLock && AdvancePressed()) break;
                if (last && data.lastPageHold > 0f && elapsed >= data.lastPageHold) break;
                yield return null;
            }

            shot.rectTransform.anchoredPosition = Vector2.zero;
            SetHint(hint, 0f);
            UISfx.Play(UISfxId.Pop);
            for (float t = 0f; t < FadeOut; t += Time.unscaledDeltaTime) { SetA(shot, 1f - t / FadeOut); yield return null; }
            SetA(shot, 0f);
            if (!skipAll) yield return new WaitForSecondsRealtime(BlackGap);
        }

        if (markSeen) ComicProgress.MarkSeen(data.comicId);
        Object.Destroy(root);
        Playing = false;
    }

    // ---------------- 보조 ----------------

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void SetA(Image img, float a)
    {
        var c = img.color; c.a = Mathf.Clamp01(a); img.color = c;
    }

    private static Text MakeHint(Transform parent)
    {
        var go = new GameObject("Hint", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 28f); rt.sizeDelta = new Vector2(520f, 40f);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 22; t.alignment = TextAnchor.LowerRight; t.raycastTarget = false;
        t.color = new Color(1, 1, 1, 0);
        t.text = "Space 다음  /  Esc 건너뛰기";
        return t;
    }

    private static void SetHint(Text t, float a)
    {
        if (t == null) return;
        var c = t.color; c.a = a * 0.6f; t.color = c;
    }

    private static bool AdvancePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current; var m = Mouse.current;
        if (k != null && (k.spaceKey.wasPressedThisFrame || k.zKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) return true;
        return m != null && m.leftButton.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
#endif
    }

    private static bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current; return k != null && k.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
