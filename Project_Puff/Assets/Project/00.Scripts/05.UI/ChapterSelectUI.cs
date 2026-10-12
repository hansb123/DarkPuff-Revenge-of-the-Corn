using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 챕터 선택 화면. 위쪽 탭(챕터1 / 미니게임 / 챕터2 / 미니게임 / 챕터3)을 고르면
/// 아래 프레임 안에 삐뚤빼뚤하게 흩어진 스테이지 박스가 나타난다.
/// 탭/버튼은 전부 코드로 만들기 때문에 씬에는 Root 두 개 + 스프라이트 배열만 연결하면 됨.
/// </summary>
public class ChapterSelectUI : MonoBehaviour
{
    [Header("페이지 (순서대로: 챕터1, 미니게임, 챕터2, 미니게임, 챕터3)")]
    [SerializeField] private ChapterData[] pages;

    [Header("UI 루트")]
    [SerializeField] private RectTransform tabRoot;     // 탭이 가로로 놓일 영역
    [SerializeField] private RectTransform stageRoot;   // 프레임 안쪽 영역 (스테이지 박스가 흩어질 곳)

    [Header("스테이지 스프라이트 (1~12번, 이름순 멀티 선택 후 드래그)")]
    [SerializeField] private Sprite[] stageNormal;
    [SerializeField] private Sprite[] stageSelected;
    [SerializeField] private Sprite[] stageCleared;
    [SerializeField] private Sprite[] stageLocked;

    [Header("미니게임 박스 스프라이트")]
    [SerializeField] private Sprite mgNormal;
    [SerializeField] private Sprite mgSelected;
    [SerializeField] private Sprite mgCleared;
    [SerializeField] private Sprite mgLocked;

    [Header("Scenes / Debug")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private bool unlockAll;   // 테스트용: 전부 열기

    private readonly List<Image> tabImages = new();
    private readonly List<GameObject> stageButtons = new();
    private int currentPage = -1;

    private void Start()
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning("[ChapterSelectUI] pages가 비어 있습니다.", this);
            return;
        }

        BuildTabs();
        ShowPage(Mathf.Clamp(StageProgress.LastPage, 0, pages.Length - 1), selectFirstStage: true);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneManager.LoadScene(titleSceneName);
    }

    // ───────────── 탭 ─────────────

    private void BuildTabs()
    {
        // 탭은 크기가 제각각이라 가로로 이어 붙이고 가운데 정렬
        float totalW = 0f;
        foreach (var p in pages) totalW += p.TabNormal != null ? p.TabNormal.rect.width : 0f;

        float x = -totalW * 0.5f;
        for (int i = 0; i < pages.Length; i++)
        {
            int index = i;
            var data = pages[i];
            var size = data.TabNormal != null ? data.TabNormal.rect.size : new Vector2(280, 110);

            var go = new GameObject($"Tab_{data.ChapterId}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(SelectNotifier));
            var rt = (RectTransform)go.transform;
            rt.SetParent(tabRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x + size.x * 0.5f, 0f);
            x += size.x;

            var img = go.GetComponent<Image>();
            img.sprite = data.TabNormal;
            tabImages.Add(img);

            // 현재 페이지 표시는 코드로 직접 하므로 버튼 자체의 전환은 끔
            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => ShowPage(index, false));

            go.GetComponent<SelectNotifier>().Selected += () => ShowPage(index, false);
        }
    }

    // ───────────── 페이지 ─────────────

    private void ShowPage(int index, bool selectFirstStage)
    {
        if (index == currentPage && !selectFirstStage) return;

        currentPage = index;
        StageProgress.LastPage = index;

        for (int i = 0; i < tabImages.Count; i++)
            tabImages[i].sprite = i == index ? pages[i].TabSelected : pages[i].TabNormal;

        foreach (var b in stageButtons) Destroy(b);
        stageButtons.Clear();

        BuildStages(pages[index], index);

        if (selectFirstStage) SelectBestStage();
    }

    private void BuildStages(ChapterData data, int pageIndex)
    {
        int count = data.StageCount;
        if (count == 0) return;

        var sizes = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            var s = GetSprite(data, i, StageVisual.Normal);
            sizes[i] = s != null ? s.rect.size : new Vector2(150, 110);
        }

        // 페이지마다 항상 같은 배치가 나오도록 시드 고정
        var layout = ScatterLayout.Compute(sizes, stageRoot.rect.size, seed: 1000 + pageIndex * 37);

        for (int i = 0; i < count; i++)
        {
            int stageIdx = i;
            string stageId = StageProgress.MakeId(data.ChapterId, i + 1);
            bool cleared = StageProgress.IsCleared(stageId);
            bool unlocked = unlockAll || IsUnlocked(pageIndex, i);

            var go = new GameObject($"Stage_{i + 1:00}", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(stageRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizes[i];
            rt.anchoredPosition = layout[i].position;
            rt.localRotation = Quaternion.Euler(0, 0, layout[i].rotation);

            var img = go.GetComponent<Image>();
            img.sprite = GetSprite(data, i, cleared ? StageVisual.Cleared : StageVisual.Normal);

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.SpriteSwap;
            var sel = GetSprite(data, i, StageVisual.Selected);
            btn.spriteState = new SpriteState
            {
                highlightedSprite = sel,
                selectedSprite = sel,
                pressedSprite = sel,
                disabledSprite = GetSprite(data, i, StageVisual.Locked)
            };
            btn.interactable = unlocked;
            if (!unlocked) img.sprite = GetSprite(data, i, StageVisual.Locked);

            btn.onClick.AddListener(() => EnterStage(data, stageIdx));
            stageButtons.Add(go);
        }
    }

    private void SelectBestStage()
    {
        // 열려 있는 것 중 아직 클리어 못한 첫 스테이지, 없으면 열린 첫 스테이지
        GameObject fallback = null;
        var data = pages[currentPage];
        for (int i = 0; i < stageButtons.Count; i++)
        {
            var btn = stageButtons[i].GetComponent<Button>();
            if (!btn.interactable) continue;
            fallback ??= stageButtons[i];
            if (!StageProgress.IsCleared(StageProgress.MakeId(data.ChapterId, i + 1)))
            {
                EventSystem.current.SetSelectedGameObject(stageButtons[i]);
                return;
            }
        }
        // 이 페이지에 열린 게 없으면 탭에 포커스를 둠
        EventSystem.current.SetSelectedGameObject(fallback != null ? fallback : tabImages[currentPage].gameObject);
    }

    // ───────────── 잠금 규칙 ─────────────

    private bool IsUnlocked(int pageIndex, int stageIndex)
    {
        var data = pages[pageIndex];

        // 같은 페이지 안: 바로 앞 스테이지를 깨야 열림
        if (stageIndex > 0)
            return StageProgress.IsCleared(StageProgress.MakeId(data.ChapterId, stageIndex));

        // 페이지의 첫 스테이지: 앞쪽 "챕터"(미니게임 페이지는 건너뜀)의 마지막 스테이지를 깨야 열림
        for (int p = pageIndex - 1; p >= 0; p--)
        {
            if (pages[p].IsMiniGame) continue;
            return StageProgress.IsCleared(StageProgress.MakeId(pages[p].ChapterId, pages[p].StageCount));
        }
        return true;   // 첫 챕터의 1번
    }

    // ───────────── 입장 ─────────────

    private void EnterStage(ChapterData data, int stageIndex)
    {
        string sceneName = data.StageSceneNames[stageIndex];
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[ChapterSelectUI] 씬 '{sceneName}' 을(를) 불러올 수 없습니다. (ChapterData 이름 / Build Settings 확인)", this);
            return;
        }
        SceneManager.LoadScene(sceneName);
    }

    // ───────────── 스프라이트 조회 ─────────────

    private enum StageVisual { Normal, Selected, Cleared, Locked }

    private Sprite GetSprite(ChapterData data, int index, StageVisual v)
    {
        if (data.IsMiniGame)
        {
            return v switch
            {
                StageVisual.Selected => mgSelected,
                StageVisual.Cleared => mgCleared,
                StageVisual.Locked => mgLocked,
                _ => mgNormal
            };
        }

        Sprite[] arr = v switch
        {
            StageVisual.Selected => stageSelected,
            StageVisual.Cleared => stageCleared,
            StageVisual.Locked => stageLocked,
            _ => stageNormal
        };
        return arr != null && index < arr.Length ? arr[index] : null;
    }
}

/// <summary>키보드 포커스(Select)가 들어오면 알려주는 작은 도우미. 탭에 붙음.</summary>
public class SelectNotifier : MonoBehaviour, ISelectHandler
{
    public event System.Action Selected;
    public void OnSelect(BaseEventData eventData) => Selected?.Invoke();
}

/// <summary>
/// 삐뚤빼뚤 흩뿌리기 배치. 행 수(1~3)를 개수로 정하고, 행마다 같은 간격 + 약간의 지터.
/// 같은 시드면 항상 같은 결과.
/// </summary>
public static class ScatterLayout
{
    public struct Slot { public Vector2 position; public float rotation; }

    public static Slot[] Compute(Vector2[] sizes, Vector2 area, int seed)
    {
        int n = sizes.Length;
        var result = new Slot[n];
        var rng = new System.Random(seed);

        int rows = n <= 4 ? 1 : (n <= 8 ? 2 : 3);
        float slotH = area.y / rows;

        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            int cnt = n / rows + (r < n % rows ? 1 : 0);

            float sumW = 0f;
            for (int k = 0; k < cnt; k++) sumW += sizes[idx + k].x;
            float gap = Mathf.Max(8f, (area.x - sumW) / (cnt + 1));

            float x = -area.x * 0.5f + gap;
            float rowCenterY = area.y * 0.5f - slotH * (r + 0.5f);

            for (int k = 0; k < cnt; k++)
            {
                var s = sizes[idx];
                float jx = Range(rng, -gap * 0.25f, gap * 0.25f);
                float maxJy = Mathf.Max(0f, (slotH - s.y) * 0.5f - 6f);
                float jy = Range(rng, -maxJy, maxJy);

                result[idx] = new Slot
                {
                    position = new Vector2(x + s.x * 0.5f + jx, rowCenterY + jy),
                    rotation = Range(rng, -3f, 3f)
                };

                x += s.x + gap;
                idx++;
            }
        }
        return result;
    }

    private static float Range(System.Random rng, float min, float max) =>
        min + (float)rng.NextDouble() * (max - min);
}
