using UnityEngine;

/// <summary>
/// 수집품 하나의 정보. Project 창 우클릭 → Create → Game → Collectible 로 12개를 만든다.
/// id는 저장 키로 쓰이므로 한 번 정하면 바꾸지 말 것 (예: corn, rice, barley ...).
/// </summary>
[CreateAssetMenu(menuName = "Game/Collectible", fileName = "Collectible_")]
public class CollectibleData : ScriptableObject
{
    [Tooltip("저장 키. 영문 소문자, 중복 금지, 만든 뒤에는 바꾸지 않기")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    [Tooltip("1~3 (도감에서 챕터별로 정렬하는 용도)")]
    public int chapter = 1;
    [Tooltip("collect_NN_xxx.png")]
    public Sprite sprite;
    [Tooltip("collect_NN_xxx_locked.png (못 먹었을 때 도감에 보이는 실루엣)")]
    public Sprite lockedSprite;
}
