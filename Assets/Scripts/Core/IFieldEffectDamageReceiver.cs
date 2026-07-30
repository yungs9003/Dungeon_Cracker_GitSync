namespace Dungeon.Core
{
    /// <summary>
    /// 화상·냉기처럼 직접 피격으로 계산하지 않는
    /// 필드 주기 피해를 받는 대상입니다.
    /// </summary>
    public interface IFieldEffectDamageReceiver
    {
        void ApplyBurnFieldDamage(int amount);
        void ApplyIceFieldDamage(int amount);
    }
}