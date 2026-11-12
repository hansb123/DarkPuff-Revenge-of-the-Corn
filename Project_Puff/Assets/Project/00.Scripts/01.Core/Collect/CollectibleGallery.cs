using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 타이틀의 수집품 도감. 열 때마다 저장 상태를 읽어 슬롯을 갱신한다.
///  - 가진 수집품: 그림 + 이름. 못 가진 것: 회색 실루엣 + "???"
///  - 슬롯을 선택(마우스 올림/클릭, 키보드·패드 선택)하면 상세 정보가 아래에 표시됨
/// 슬롯 오브젝트마다 Image(+Button)를 두고 slots에 순서대로 넣는다. all[i]와 slots[i]가 짝.
/// </summary>
public class CollectibleGallery : MonoBehaviour
{
    [Header("열고 닫기")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("수집품 12개 (순서 = 슬롯 순서)")]
    [SerializeField] private CollectibleData[] all;
    [SerializeField] private Image[] slots;
    [Tooltip("슬롯 아래 이름 글자 (선택)")]
    [SerializeField] private TMP_Text[] nameLabels;

    [Header("표시")]
    [SerializeField] private TMP_Text countText;              // "3 / 12"
    [SerializeField] private string unknownName = "???";
    [SerializeField] private string unknownDesc = "아직 찾지 못했어요.";
    [Header("상세 (선택)")]
    [SerializeField] private Image detailImage;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailDesc;

    private void Awake()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (panelRoot != null) panelRoot.SetActive(false);

        // 슬롯 선택 시 상세 표시
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            int index = i;
            var trigger = slots[i].GetComponent<EventTrigger>();
            if (trigger == null) trigger = slots[i].gameObject.AddComponent<EventTrigger>();
            AddEvent(trigger, EventTriggerType.PointerEnter, () => ShowDetail(index));
            AddEvent(trigger, EventTriggerType.Select, () => ShowDetail(index));
            AddEvent(trigger, EventTriggerType.PointerClick, () => ShowDetail(index));
        }
    }

    private static void AddEvent(EventTrigger t, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(_ => action());
        t.triggers.Add(e);
    }

    public void Open()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        Refresh();
        UISfx.Play(UISfxId.Pop);
        if (EventSystem.current != null && slots != null && slots.Length > 0 && slots[0] != null)
            EventSystem.current.SetSelectedGameObject(slots[0].gameObject);
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        UISfx.Play(UISfxId.Back);
    }

    public void Refresh()
    {
        int owned = 0;
        for (int i = 0; all != null && i < all.Length; i++)
        {
            var c = all[i];
            if (c == null) continue;
            bool has = CollectibleProgress.IsOwned(c.id);
            if (has) owned++;

            if (slots != null && i < slots.Length && slots[i] != null)
                slots[i].sprite = has ? c.sprite : (c.lockedSprite != null ? c.lockedSprite : c.sprite);
            if (nameLabels != null && i < nameLabels.Length && nameLabels[i] != null)
                nameLabels[i].text = has ? c.displayName : unknownName;
        }
        if (countText != null) countText.text = $"{owned} / {(all != null ? all.Length : 0)}";
        ClearDetail();
    }

    private void ShowDetail(int index)
    {
        if (all == null || index >= all.Length || all[index] == null) return;
        var c = all[index];
        bool has = CollectibleProgress.IsOwned(c.id);
        if (detailImage != null)
        {
            detailImage.enabled = true;
            detailImage.sprite = has ? c.sprite : (c.lockedSprite != null ? c.lockedSprite : c.sprite);
        }
        if (detailName != null) detailName.text = has ? c.displayName : unknownName;
        if (detailDesc != null) detailDesc.text = has ? c.description : unknownDesc;
    }

    private void ClearDetail()
    {
        if (detailImage != null) detailImage.enabled = false;
        if (detailName != null) detailName.text = "";
        if (detailDesc != null) detailDesc.text = "";
    }
}
