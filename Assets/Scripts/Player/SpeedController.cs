using UnityEngine;

namespace Dungeon.Player;

public class SpeedController : MonoBehaviour
{
    [SerializeField] private float baseSpeed = 5f; // PlayerController의 기본속도랑 맞추기
    private float bonus = 0f;

    public float CurrentSpeed => Mathf.Max(0f, baseSpeed + bonus);
    public void AddBonus(float delta) => bonus += delta;
}
