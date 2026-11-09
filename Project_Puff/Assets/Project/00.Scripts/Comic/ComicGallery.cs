using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 챕터 선택 화면의 '만화 다시 보기'. 이미 본 만화(또는 모든 챕터 해금 치트)만 버튼이 눌린다.
/// 버튼은 만화 수만큼 Button 을 만들어 슬롯에 끌어다 넣으면 된다. 재생 중에는 다른 입력을 막는다.
/// </summary>
public class ComicGallery : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public ComicData comic;
        public Button button;
        [Tooltip("잠겨 있을 때 보여줄 오브젝트 (자물쇠 등, 선택)")]
        public GameObject lockedMark;
    }

    [SerializeField] private Slot[] slots;

    private void OnEnable() => Refresh();

    private void Start()
    {
        foreach (var s in slots)
        {
            if (s.button == null || s.comic == null) continue;
            var comic = s.comic;
            s.button.onClick.AddListener(() =>
            {
                if (ComicPlayer.Playing || !ComicProgress.Unlocked(comic.comicId)) { UISfx.Play(UISfxId.Denied); return; }
                UISfx.Play(UISfxId.Submit);
                StartCoroutine(ComicPlayer.Run(comic, markSeen: false));
            });
        }
        Refresh();
    }

    public void Refresh()
    {
        if (slots == null) return;
        foreach (var s in slots)
        {
            if (s.comic == null) continue;
            bool open = ComicProgress.Unlocked(s.comic.comicId);
            if (s.button != null) s.button.interactable = open;
            if (s.lockedMark != null) s.lockedMark.SetActive(!open);
        }
    }
}
