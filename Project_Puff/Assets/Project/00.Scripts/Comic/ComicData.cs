using UnityEngine;

/// <summary>
/// 컷 만화 한 편. 페이지(1920x1080 권장)를 순서대로 한 장씩 전체화면으로 보여준다.
/// Project 창 우클릭 > Create > Darkpuff > Comic Data
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Comic Data", fileName = "Comic_")]
public class ComicData : ScriptableObject
{
    [Tooltip("진행 기록용 고유 ID. 예: opening / chapter2 / ending (바꾸면 '본 적 없음'으로 취급됨)")]
    public string comicId = "opening";
    public string displayName = "오프닝";

    [Header("페이지 (순서대로)")]
    public Sprite[] pages;

    [Header("페이지별 연출 (선택, 길이가 달라도 됨)")]
    [Tooltip("페이지가 뜰 때 재생할 효과음. 예: 마지막 만화의 '뻥!'")]
    public AudioClip[] pageSfx;
    [Tooltip("화면이 흔들릴 페이지 번호 (0부터)")]
    public int[] shakePages;

    [Header("옵션")]
    [Tooltip("마지막 페이지를 자동으로 넘기는 시간. 0이면 입력할 때까지 대기")]
    public float lastPageHold = 0f;
    [Tooltip("재생 중 배경음을 잠깐 줄일 비율 (0=그대로, 1=무음). BGM이 SceneBgm일 때만")]
    [Range(0f, 1f)] public float duckBgm = 0f;
}
