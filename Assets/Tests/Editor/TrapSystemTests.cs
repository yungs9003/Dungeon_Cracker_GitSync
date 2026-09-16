using System.Reflection;
using Dungeon.Core;
using Dungeon.Map;
using Dungeon.Traps;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Tests
{
    public class TrapSystemTests
    {
        [TestCase("101-001_0305", "101-001")]
        [TestCase("3F_103-012_0A0B", "103-012")]
        [TestCase("Arrow2D(Clone)", "")]
        public void ExtractTrapCode_ReadsPrefabCodeFromGeneratedNames(string objectName, string expected)
        {
            Assert.That(TrapDamageEvent.ExtractTrapCode(objectName), Is.EqualTo(expected));
        }

        [Test]
        public void GameDatabase_LoadsDamageAndOptionalKnockback()
        {
            GameObject go = new GameObject("GameDatabase_Test");
            go.SetActive(false);

            try
            {
                GameDatabase database = go.AddComponent<GameDatabase>();
                database.LoadDatabase();

                Assert.That(database.TryGetTrap("101-001", out TrapDefinition arrow), Is.True);
                Assert.That(arrow.damage, Is.EqualTo(4));
                Assert.That(arrow.knockback_distance, Is.Null);

                Assert.That(database.TryGetTrap("102-002", out TrapDefinition thorn), Is.True);
                Assert.That(thorn.damage, Is.Null);
                Assert.That(thorn.knockback_distance, Is.EqualTo(0.1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [TestCase("102-009")]
        [TestCase("102-010")]
        [TestCase("102-011")]
        public void FloorOneTrapPrefab_HasOneChildRevealVariant(string trapCode)
        {
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{trapCode}");
            Assert.That(prefab, Is.Not.Null);

            AssetVariantSprite2D[] variants =
                prefab.GetComponentsInChildren<AssetVariantSprite2D>(true);

            Assert.That(variants, Has.Length.EqualTo(1));
            Assert.That(variants[0].transform, Is.Not.SameAs(prefab.transform));
            Assert.That(variants[0].HasVariant(0), Is.True);
            Assert.That(variants[0].HasVariant(1), Is.True);
        }

        [TestCase("103-012")]
        [TestCase("103-013")]
        public void FloorThreeTrapPrefab_UsesChildSpriteRenderers(string trapCode)
        {
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{trapCode}");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<SpriteRenderer>(), Is.Null);
            Assert.That(
                prefab.GetComponentsInChildren<SpriteRenderer>(true),
                Is.Not.Empty
            );
        }

        [TestCase("102-009")]
        [TestCase("103-012")]
        public void LaserTrapPrefab_LeavesLaserBeamOutsideRevealMask(string trapCode)
        {
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{trapCode}");
            Assert.That(prefab, Is.Not.Null);

            TrapRevealAlwaysVisible2D[] markers =
                prefab.GetComponentsInChildren<TrapRevealAlwaysVisible2D>(true);

            Assert.That(markers, Has.Length.EqualTo(1));
            Assert.That(markers[0].name, Is.EqualTo("LaserBeam"));
            Assert.That(markers[0].GetComponent<SpriteRenderer>(), Is.Not.Null);
        }

        [Test]
        public void HiddenTrapVisual_SwitchesChildVariantButLeavesMarkedEffectVisible()
        {
            GameObject root = new GameObject("102-009_Test");
            GameObject baseVisual = new GameObject("CrystalBase");
            GameObject dynamicVisual = new GameObject("LaserBeam");
            Texture2D texture = new Texture2D(2, 1);
            Sprite hidden = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            Sprite revealed = Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero);

            try
            {
                baseVisual.transform.SetParent(root.transform);
                dynamicVisual.transform.SetParent(root.transform);

                SpriteRenderer renderer = baseVisual.AddComponent<SpriteRenderer>();
                SpriteRenderer dynamicRenderer = dynamicVisual.AddComponent<SpriteRenderer>();
                dynamicRenderer.enabled = false;
                dynamicVisual.AddComponent<TrapRevealAlwaysVisible2D>();

                AssetVariantSprite2D variants = baseVisual.AddComponent<AssetVariantSprite2D>();
                SetPrivateField(variants, "spriteRenderer", renderer);
                SetPrivateField(variants, "variants", new[] { hidden, revealed });

                HiddenTrapVisual2D reveal = root.AddComponent<HiddenTrapVisual2D>();
                Assert.That(reveal.ConfigureFromAssetVariants(variants, true), Is.True);
                Assert.That(renderer.sprite, Is.SameAs(hidden));
                Assert.That(dynamicRenderer.forceRenderingOff, Is.False);

                dynamicRenderer.enabled = true;
                reveal.ApplyForwardZoneState(false);

                Assert.That(dynamicRenderer.enabled, Is.True);
                Assert.That(dynamicRenderer.forceRenderingOff, Is.False);

                reveal.ApplyForwardZoneState(true);
                Assert.That(renderer.sprite, Is.SameAs(revealed));
                Assert.That(dynamicRenderer.forceRenderingOff, Is.False);

                reveal.ApplyForwardZoneState(false);

                Assert.That(renderer.sprite, Is.SameAs(revealed));
                Assert.That(dynamicRenderer.enabled, Is.True);
                Assert.That(dynamicRenderer.forceRenderingOff, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(hidden);
                Object.DestroyImmediate(revealed);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void HiddenTrapVisual_VisibilityOnlyKeepsRootActiveAndStaysRevealed()
        {
            GameObject root = new GameObject("103-012_Test");
            GameObject baseVisual = new GameObject("CrystalBase");
            GameObject dynamicVisual = new GameObject("LaserBeam");

            try
            {
                BoxCollider2D rootCollider = root.AddComponent<BoxCollider2D>();
                baseVisual.transform.SetParent(root.transform);
                dynamicVisual.transform.SetParent(root.transform);

                SpriteRenderer baseRenderer = baseVisual.AddComponent<SpriteRenderer>();
                SpriteRenderer dynamicRenderer = dynamicVisual.AddComponent<SpriteRenderer>();
                dynamicRenderer.enabled = false;
                dynamicVisual.AddComponent<TrapRevealAlwaysVisible2D>();

                HiddenTrapVisual2D reveal = root.AddComponent<HiddenTrapVisual2D>();
                Assert.That(
                    reveal.ConfigureVisibilityOnly(
                        new[] { baseRenderer, dynamicRenderer },
                        true
                    ),
                    Is.True
                );

                Assert.That(root.activeSelf, Is.True);
                Assert.That(rootCollider.enabled, Is.True);
                Assert.That(baseRenderer.enabled, Is.True);
                Assert.That(dynamicRenderer.enabled, Is.False);
                Assert.That(baseRenderer.forceRenderingOff, Is.True);
                Assert.That(dynamicRenderer.forceRenderingOff, Is.False);

                dynamicRenderer.enabled = true;
                reveal.ApplyForwardZoneState(false);

                Assert.That(dynamicRenderer.enabled, Is.True);
                Assert.That(dynamicRenderer.forceRenderingOff, Is.False);

                reveal.ApplyForwardZoneState(true);

                Assert.That(baseRenderer.enabled, Is.True);
                Assert.That(dynamicRenderer.enabled, Is.True);
                Assert.That(baseRenderer.forceRenderingOff, Is.False);
                Assert.That(dynamicRenderer.forceRenderingOff, Is.False);

                reveal.ApplyForwardZoneState(false);

                Assert.That(root.activeSelf, Is.True);
                Assert.That(rootCollider.enabled, Is.True);
                Assert.That(baseRenderer.enabled, Is.True);
                Assert.That(dynamicRenderer.enabled, Is.True);
                Assert.That(baseRenderer.forceRenderingOff, Is.False);
                Assert.That(dynamicRenderer.forceRenderingOff, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CliffTrapDetection_ExtendsOnlyTowardAdjacentPit()
        {
            GameObject left = new GameObject("100-000_Left");
            GameObject right = new GameObject("100-000_Right");
            Vector2 origin = new Vector2(1000f, 1000f);

            try
            {
                left.transform.position = origin;
                right.transform.position = origin + Vector2.right;

                CliffTrap2D leftTrap = left.AddComponent<CliffTrap2D>();
                CliffTrap2D rightTrap = right.AddComponent<CliffTrap2D>();
                SetPrivateField(leftTrap, "triggerBoxSize", new Vector2(0.6f, 0.6f));
                SetPrivateField(rightTrap, "triggerBoxSize", new Vector2(0.6f, 0.6f));

                Assert.That(InvokeInsideDetection(leftTrap, origin + new Vector2(-0.31f, 0f)), Is.False,
                    "외곽 방향은 기존 0.6 판정을 유지해야 합니다.");
                Assert.That(InvokeInsideDetection(leftTrap, origin + new Vector2(0.49f, 0f)), Is.True,
                    "인접 구멍 방향은 공유 경계까지 확장되어야 합니다.");
                Assert.That(InvokeInsideDetection(rightTrap, origin + new Vector2(0.51f, 0f)), Is.True,
                    "반대편 구멍도 공유 경계까지 확장되어야 합니다.");
                Assert.That(InvokeInsideDetection(leftTrap, origin + new Vector2(0f, 0.31f)), Is.False,
                    "구멍이 없는 위쪽 방향은 확장되면 안 됩니다.");
            }
            finally
            {
                Object.DestroyImmediate(left);
                Object.DestroyImmediate(right);
            }
        }

        [Test]
        public void CliffTrapFallLock_AllowsOnlyOneTrapPerPlayer()
        {
            GameObject first = new GameObject("100-000_First");
            GameObject second = new GameObject("100-000_Second");
            GameObject player = new GameObject("Player");

            try
            {
                CliffTrap2D firstTrap = first.AddComponent<CliffTrap2D>();
                CliffTrap2D secondTrap = second.AddComponent<CliffTrap2D>();
                Rigidbody2D rb = player.AddComponent<Rigidbody2D>();

                Assert.That(InvokeFallLock(firstTrap, rb), Is.True);
                Assert.That(InvokeFallLock(secondTrap, rb), Is.False,
                    "같은 플레이어의 중복 추락 처리는 잠겨야 합니다.");

                InvokePrivate(firstTrap, "ReleasePlayerFallLock");
                Assert.That(InvokeFallLock(secondTrap, rb), Is.True,
                    "첫 추락 처리가 끝난 뒤에는 다음 추락이 가능해야 합니다.");
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(player);
            }
        }

        private static bool InvokeInsideDetection(CliffTrap2D target, Vector2 point)
        {
            return (bool)InvokePrivate(target, "IsInsideDetectionBounds", point);
        }

        private static bool InvokeFallLock(CliffTrap2D target, Rigidbody2D rb)
        {
            return (bool)InvokePrivate(target, "TryAcquirePlayerFallLock", rb);
        }

        private static object InvokePrivate(object target, string name, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.That(method, Is.Not.Null, $"Missing private method: {name}");
            return method.Invoke(target, args);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field: {name}");
            field.SetValue(target, value);
        }
    }
}
