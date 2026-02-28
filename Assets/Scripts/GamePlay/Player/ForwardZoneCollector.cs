using System.Collections.Generic;
using UnityEngine;
using Dungeon.Traps;
using Dungeon.Core;

namespace Dungeon.Player
{
public class ForwardZoneCollector : MonoBehaviour
{
    // ✅ 여러 투사체 타입을 한 리스트로 관리 (Arrow, Bullet, Fireball 등)
    public readonly List<MonoBehaviour> projectiles = new();

    public readonly List<Destructible> destructibles = new();

    private void OnTriggerEnter(Collider other)
    {
        // 1) 투사체 수집 (부모까지 포함)
        var p = FindProjectileComponent(other);
        if (p != null && !projectiles.Contains(p))
            projectiles.Add(p);

        // 2) 파괴 가능한 블록(함정) 수집 (부모까지 포함 추천)
        var d = other.GetComponentInParent<Destructible>();
        if (d != null && !destructibles.Contains(d))
            destructibles.Add(d);
    }

    private void OnTriggerExit(Collider other)
    {
        var p = FindProjectileComponent(other);
        if (p != null) projectiles.Remove(p);

        var d = other.GetComponentInParent<Destructible>();
        if (d != null) destructibles.Remove(d);
    }

    private void LateUpdate()
    {
        // ✅ 파괴된 오브젝트 정리
        projectiles.RemoveAll(p => p == null);
        destructibles.RemoveAll(d => d == null);
    }

    // ✅ Collider가 자식에 있어도 “투사체 루트”를 찾게 한다
    private MonoBehaviour FindProjectileComponent(Collider other)
    {
        // parent 체인에서 MonoBehaviour들 중 IPlayerDestroyableProjectile 구현한 걸 찾기
        var mbs = other.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < mbs.Length; i++)
        {
            if (mbs[i] is IPlayerDestroyableProjectile)
                return mbs[i];
        }
        return null;
    }
}
}