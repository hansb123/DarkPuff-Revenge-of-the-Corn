using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 화면 위쪽에 "STAGE 1-2" 판을 보여주는 HUD.
/// - 지금 씬 이름을 ChapterData들의 Stage Scene Names에서 찾아서 챕터 번호와 스테이지 번호를 자동으로 알아낸다.
///   (씬 이름이 어디에도 없으면 Manual 값을 쓴다)
/// - 미니게임 페이지(ChapterData.IsMiniGame)면 숫자 대신 "미니게임!" 판을 보여준다.
/// - 숫자는 숫자 스프라이트(0~9, -)를 코드로 늘어놓아서 "3-12"처럼 두 자리도 알아서 맞춘다.
/// - 시작할 때 판이 위에서 툭 내려오고, ChapterData.StageTitles에 이름 리본이 있으면 잠깐 보여준 뒤 사라진다.
/// 판 Image 한 개와 (선택) 이름 리본 Image 한 개만 있으면 된다. 숫자 자리는 코드가 만들어 준다.
/// </summary>
public class StageHud : MonoBehaviour
{
    [Header("챕터 데이터 (ChapterSelectUI의 Pages와 같은 에셋들)")]
    [SerializeField] private ChapterData[] chapters;

    [Header("찾지 못했을 때 / 테스트용")]
    [Tooltip("켜면 씬 이름 대신 아래 번호를 그대로 보여줌")]
    [SerializeField] private bool useManual;
    [SerializeField] private int manualChapter = 1;
    [SerializeField] private int manualStage = 1;

    [Header("판")]
    [SerializeField] private Image plateImage;
    [SerializeField] private Sprite plateSprite;        // plate
    [SerializeField] private Sprite plateMiniSprite;    // plate_mini

    [Header("숫자 (0~9 순서대로 10개, 그리고 하이픈)")]
    [SerializeField] private Sprite[] digitSprites = new Sprite[10];
    [SerializeField] private Sprite dashSprite;
    [Tooltip("숫자 한 글자의 크기 배율")]
    [SerializeField, Range(0.3f, 1.5f)] private float digitScale = 0.8f;
    [Tooltip("글자 사이 간격 배율 (1 = 스프라이트 폭만큼, 작을수록 촘촘)")]
    [SerializeField, Range(0.5f, 1.2f)] private float digitSpacing = 0.85f;

    [Header("이름 리본 (선택)")]
    [SerializeField] private Image titleImage;
    [Tooltip("리본이 보이는 시간(초)")]
    [SerializeField] private float titleShowTime = 2.5f;
    [Tooltip("켜면 리본이 사라지지 않고 계속 보임")]
    [SerializeField] private bool keepTitle;

    [Header("등장 연출")]
    [SerializeField] private bool playIntro = true;
    [SerializeField] private float introDelay = 0.3f;
    [SerializeField] private float dropTime = 0.4f;
    [SerializeField] private bool playSounds = true;

    private RectTransform digitRoot;
    private readonly System.Collections.Generic.List<Image> glyphs = new System.Collections.Generic.List<Image>();

    // 원본 판 이미지(480x120) 기준으로 숫자가 들어갈 자리
    private const float RefPlateWidth = 480f;
    private static readonly Vector2 RefDigitCenter = new Vector2(102f, 6f);
    private static readonly Vector2 RefDigitSize = new Vector2(212f, 84f);

    private void Start()
    {
        if (plateImage == null)
        {
            Debug.LogWarning("[StageHud] Plate Image가 연결되지 않았습니다.", this);
            return;
        }

        ChapterData chapter = null;
        int stageIndex = 0;
        int chapterNumber = manualChapter;

        if (!useManual && TryFindStage(out chapter, out stageIndex))
        {
            chapterNumber = ParseChapterNumber(chapter);
        }
        else if (!useManual)
        {
            Debug.LogWarning($"[StageHud] 씬 '{SceneManager.GetActiveScene().name}'을 어떤 ChapterData의 Stage Scene Names에서도 찾지 못해 Manual 값({manualChapter}-{manualStage})을 씁니다.", this);
        }

        int stageNumber = chapter != null ? stageIndex + 1 : manualStage;
        bool mini = chapter != null && chapter.IsMiniGame;

        BuildPlate(mini, chapterNumber, stageNumber);
        SetupTitle(chapter != null ? chapter.GetStageTitle(stageIndex) : null);

        if (playIntro) StartCoroutine(IntroRoutine());
        else if (titleImage != null && !keepTitle) titleImage.gameObject.SetActive(false);
    }

    // ---------------- 어떤 스테이지인지 알아내기 ----------------

    private bool TryFindStage(out ChapterData found, out int index)
    {
        found = null;
        index = -1;
        if (chapters == null) return false;

        string scene = SceneManager.GetActiveScene().name;
        foreach (var chapter in chapters)
        {
            if (chapter == null || chapter.StageSceneNames == null) continue;

            int i = Array.IndexOf(chapter.StageSceneNames, scene);
            if (i < 0) continue;

            found = chapter;
            index = i;
            return true;
        }
        return false;
    }

    // ChapterId "ch2" → 2.  숫자가 없으면 1
    private static int ParseChapterNumber(ChapterData chapter)
    {
        var match = Regex.Match(chapter.ChapterId ?? "", @"\d+");
        return match.Success && int.TryParse(match.Value, out int n) ? n : 1;
    }

    // ---------------- 판 / 숫자 ----------------

    private void BuildPlate(bool mini, int chapterNumber, int stageNumber)
    {
        plateImage.sprite = mini && plateMiniSprite != null ? plateMiniSprite : plateSprite;

        EnsureDigitRoot();
        foreach (var g in glyphs) if (g != null) Destroy(g.gameObject);
        glyphs.Clear();

        digitRoot.gameObject.SetActive(!mini);
        if (mini) return;

        // "1-2" 처럼 글자 목록 만들기
        var sprites = new System.Collections.Generic.List<Sprite>();
        AddNumber(sprites, chapterNumber);
        sprites.Add(dashSprite);
        AddNumber(sprites, stageNumber);

        // 폭 계산 → 영역보다 넓으면 줄이기
        float total = 0f;
        foreach (var s in sprites) total += s != null ? s.rect.width * digitSpacing : 0f;
        float scale = digitScale;
        float maxWidth = digitRoot.rect.width;
        if (total * scale > maxWidth && total > 0f) scale = maxWidth / total;

        float x = -total * scale * 0.5f;
        foreach (var s in sprites)
        {
            if (s == null) continue;

            var go = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(digitRoot, false);

            var img = go.GetComponent<Image>();
            img.sprite = s;
            img.raycastTarget = false;
            img.preserveAspect = true;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = s.rect.size * scale;

            float advance = s.rect.width * digitSpacing * scale;
            rt.anchoredPosition = new Vector2(x + advance * 0.5f, 0f);
            x += advance;

            glyphs.Add(img);
        }
    }

    private void AddNumber(System.Collections.Generic.List<Sprite> list, int number)
    {
        foreach (char ch in Mathf.Max(0, number).ToString())
        {
            int d = ch - '0';
            list.Add(digitSprites != null && d >= 0 && d < digitSprites.Length ? digitSprites[d] : null);
        }
    }

    // 숫자가 들어갈 자리(RectTransform)를 판 아래에 자동으로 만든다. (판 크기를 바꿔도 비율대로 따라감)
    private void EnsureDigitRoot()
    {
        if (digitRoot != null) return;

        var go = new GameObject("Digits", typeof(RectTransform));
        go.transform.SetParent(plateImage.transform, false);
        digitRoot = (RectTransform)go.transform;

        float k = plateImage.rectTransform.rect.width / RefPlateWidth;
        if (k <= 0f) k = 1f;

        digitRoot.anchorMin = digitRoot.anchorMax = new Vector2(0.5f, 0.5f);
        digitRoot.pivot = new Vector2(0.5f, 0.5f);
        digitRoot.sizeDelta = RefDigitSize * k;
        digitRoot.anchoredPosition = RefDigitCenter * k;
    }

    // ---------------- 이름 리본 ----------------

    private void SetupTitle(Sprite sprite)
    {
        if (titleImage == null) return;

        if (sprite == null)
        {
            titleImage.gameObject.SetActive(false);
            return;
        }

        titleImage.sprite = sprite;
        titleImage.gameObject.SetActive(true);
    }

    // ---------------- 등장 연출 ----------------

    private IEnumerator IntroRoutine()
    {
        var plate = plateImage.rectTransform;
        Vector2 home = plate.anchoredPosition;
        Vector2 start = home + Vector2.up * (plate.rect.height + 80f);

        bool hasTitle = titleImage != null && titleImage.gameObject.activeSelf;
        CanvasGroup titleGroup = null;
        RectTransform title = null;
        if (hasTitle)
        {
            title = titleImage.rectTransform;
            titleGroup = titleImage.GetComponent<CanvasGroup>();
            if (titleGroup == null) titleGroup = titleImage.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
        }

        plate.anchoredPosition = start;
        yield return WaitUnscaled(introDelay);

        if (playSounds) UISfx.Play(UISfxId.Pop);
        yield return Animate(dropTime, t => plate.anchoredPosition = Vector2.LerpUnclamped(start, home, EaseOutBack(t)));
        plate.anchoredPosition = home;

        if (!hasTitle) yield break;

        // 이름 리본: 작게 시작해서 커지며 등장
        if (playSounds) UISfx.Play(UISfxId.Stamp);
        yield return Animate(0.3f, t =>
        {
            float s = Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(t));
            title.localScale = new Vector3(s, s, 1f);
            titleGroup.alpha = Mathf.Clamp01(t * 3f);
        });
        title.localScale = Vector3.one;
        titleGroup.alpha = 1f;

        if (keepTitle) yield break;

        yield return WaitUnscaled(titleShowTime);
        yield return Animate(0.4f, t => titleGroup.alpha = 1f - t);
        titleImage.gameObject.SetActive(false);
    }

    private static IEnumerator Animate(float duration, Action<float> step)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            step(Mathf.Clamp01(t / duration));
            yield return null;
        }
        step(1f);
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
