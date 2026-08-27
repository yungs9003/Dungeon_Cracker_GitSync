namespace Dungeon.Core
{
    public interface IDamageSystem
    {
        void Initialize(IDamageReceiver damageReceiver);
        void TakeDamage(int amount, DamageType type);
        void StartBleeding(int amount, float duration = 7f, float tickInterval = 0.5f);
        void ClearBleeding();
        void StartPhysicalResistance(int amount, float duration);
        void StartMagicalResistance(int amount, float duration);
    }
}
