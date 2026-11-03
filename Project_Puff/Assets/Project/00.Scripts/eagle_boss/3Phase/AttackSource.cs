/// <summary>
/// 지금 맞은 공격이 어떤 종류인지 알려 주는 표시. 플레이어 공격 스크립트가 TakeDamage 를 부르기 직전에 설정한다.
/// 보스가 "뻥은 안 맞고 기본 공격만 맞는" 구간(3페이즈 하늘 그로기)을 구분하는 데 쓴다.
///   PlayerAttack        → AttackSource.Current = AttackSource.Kind.Melee;
///   PlayerSpecialAttack → AttackSource.Current = AttackSource.Kind.Special;
/// </summary>
public static class AttackSource
{
    public enum Kind { Unknown, Melee, Special }
    public static Kind Current = Kind.Unknown;
}