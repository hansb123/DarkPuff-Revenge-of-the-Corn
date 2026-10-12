using UnityEngine;

/// <summary>
/// 챕터 선택 화면의 "한 페이지" 데이터. 일반 챕터와 미니게임 페이지 모두 이걸로 만듦.
/// 미니게임 페이지는 isMiniGame = true, stageSceneNames 길이 1 (큰 박스 하나).
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Chapter Data", fileName = "Chapter_")]
public class ChapterData : ScriptableObject
{
    [field: SerializeField] public string ChapterId { get; private set; } = "ch1";   // 미니게임은 "mg1", "mg2"
    [field: SerializeField] public string DisplayName { get; private set; } = "챕터 1";
    [field: SerializeField] public bool IsMiniGame { get; private set; }

    [field: Header("탭 스프라이트")]
    [field: SerializeField] public Sprite TabNormal { get; private set; }
    [field: SerializeField] public Sprite TabSelected { get; private set; }

    [field: Header("스테이지 씬 이름 (배열 길이 = 스테이지 수)")]
    [field: Tooltip("챕터1/2는 10칸, 챕터3은 12칸, 미니게임은 1칸. 아직 없는 씬은 빈 문자열로 둬도 됨.")]
    [field: SerializeField] public string[] StageSceneNames { get; private set; } = new string[10];

    public int StageCount => StageSceneNames != null ? StageSceneNames.Length : 0;
}
