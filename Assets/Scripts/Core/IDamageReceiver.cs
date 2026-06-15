namespace Dungeon.Core
{
    public interface IDamageReceiver
    {
        int currentHP { get; }
        bool IsInvincible { get; }
        void ApplyDamageDirect(int amount);
        void CountDirectHit();
    }
}
