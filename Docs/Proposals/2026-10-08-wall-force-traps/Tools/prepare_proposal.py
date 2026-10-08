"""기존 Assets를 읽어서 검토용 Before/After와 unified diff만 작성합니다."""
from pathlib import Path
import hashlib, json, difflib, uuid

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def snapshot():
    paths = []
    for folder in ('Assets', 'ProjectSettings', 'Packages'):
        paths.extend(p for p in (ROOT / folder).rglob('*') if p.is_file())
    return {str(p.relative_to(ROOT)).replace('\\', '/'): digest(p) for p in paths}

baseline = OUT / 'Validation' / 'original-project-sha256.json'
baseline.parent.mkdir(parents=True, exist_ok=True)
if not baseline.exists():
    baseline.write_text(json.dumps(snapshot(), ensure_ascii=False, indent=2), encoding='utf-8')

changes = []

def modify(rel, replacements):
    original = (ROOT / rel).read_bytes()
    text = original.decode('utf-8-sig').replace('\r\n', '\n')
    before = text
    for old, new in replacements:
        assert text.count(old) == 1, (rel, old[:90], text.count(old))
        text = text.replace(old, new)
    for category, content in [('Before', original), ('After', text.encode('utf-8'))]:
        path = OUT / 'Integration' / category / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)
    changes.append({'path':rel, 'before_sha256':hashlib.sha256(original).hexdigest(),
                    'after_sha256':hashlib.sha256(text.encode('utf-8')).hexdigest()})
    return ''.join(difflib.unified_diff(before.splitlines(True), text.splitlines(True),
                                      fromfile='a/'+rel, tofile='b/'+rel))

diff = ''
diff += modify('Assets/Scripts/GamePlay/Player/PlayerController2D.cs', [
    ('        private void FixedUpdate()\n        {',
     '        private void FixedUpdate()\n        {\n            if (PlayerEffectLock2D.IsProtectedTarget(this)) return;')])
diff += modify('Assets/Scripts/GamePlay/Player/KnockbackReceiver2D.cs', [
    ('using UnityEngine;', 'using UnityEngine;\nusing Dungeon.Core;'),
    ('        public void AddKnockback(Vector2 dir, float distance, float duration)\n        {',
     '        public void AddKnockback(Vector2 dir, float distance, float duration)\n        {\n            if (PlayerEffectLock2D.IsProtectedTarget(this)) return;')])
diff += modify('Assets/Scripts/GamePlay/Player/PlayerSafePointTracker2D.cs', [
    ('using UnityEngine;', 'using UnityEngine;\nusing Dungeon.Core;'),
    ('        private void FixedUpdate()\n        {',
     '        private void FixedUpdate()\n        {\n            if (PlayerEffectLock2D.IsProtectedTarget(this)) return;')])
diff += modify('Assets/Scripts/GamePlay/Player/Respawn2D.cs', [
    ('        public bool ReturnToLastSafePointFromTrap(int damage = 0)\n        {',
     '        public bool ReturnToLastSafePointFromTrap(int damage = 0)\n        {\n            if (PlayerEffectLock2D.IsProtectedTarget(this)) return false;')])
diff += modify('Assets/Scripts/GamePlay/Player/Health.cs', [
    ('        public bool IsInvincible => useInvincibility && Time.time < invincibleUntil;',
     '        public bool IsInvincible => PlayerEffectLock2D.IsProtectedTarget(this) ||\n            (useInvincibility && Time.time < invincibleUntil);'),
    ('        public void TakeDamage(int amount, DamageType type)\n        {',
     '        public void TakeDamage(int amount, DamageType type)\n        {\n            if (PlayerEffectLock2D.IsProtectedTarget(this)) return;'),
    ('        public void ApplyDamageDirect(int finalDamage)\n        {',
     '        public void ApplyDamageDirect(int finalDamage)\n        {\n            if (PlayerEffectLock2D.IsProtectedTarget(this)) return;'),
    ('        public void TakeBleedingDamage(int amount, float duration = 7f, float tickInterval = 0.5f)\n        {',
     '        public void TakeBleedingDamage(int amount, float duration = 7f, float tickInterval = 0.5f)\n        {\n            if (PlayerEffectLock2D.IsProtectedTarget(this)) return;'),
    ('                if (isBleedingActive && Time.time >= lastBleedingDamageTime)\n                {',
     '                if (isBleedingActive && Time.time >= lastBleedingDamageTime)\n                {\n                    if (PlayerEffectLock2D.IsProtectedTarget(this))\n                    {\n                        lastBleedingDamageTime = Time.time + bleedingTickInterval;\n                        return;\n                    }')])
diff += modify('Assets/Scripts/GamePlay/Traps/TrapDamageEvent.cs', [
    ('        public static bool Raise(string trapCode, GameObject source, Health target)\n        {',
     '        public static bool Raise(string trapCode, GameObject source, Health target)\n        {\n            if (PlayerEffectLock2D.IsProtectedTarget(target)) return false;')])
diff += modify('Assets/Scripts/GamePlay/Traps/CliffTrap2D.cs', [
    ('using Dungeon.Player;', 'using Dungeon.Player;\nusing Dungeon.Core;'),
    ('            if (playerRb == null) return;',
     '            if (playerRb == null) return;\n            if (PlayerEffectLock2D.IsProtectedTarget(playerRb)) return;'),
    ('        private bool TryAcquirePlayerFallLock(Rigidbody2D rb)\n        {',
     '        public static bool IsPlayerFalling(Rigidbody2D rb) => rb != null && FallingPlayers.Contains(rb);\n\n        private bool TryAcquirePlayerFallLock(Rigidbody2D rb)\n        {\n            if (PlayerEffectLock2D.IsProtectedTarget(rb)) return false;')])
diff += modify('Assets/Scripts/GamePlay/Traps/PitTile2D.cs', [
    ('using Dungeon.Player;', 'using Dungeon.Player;\nusing Dungeon.Core;'),
    ('        private void OnTriggerEnter2D(Collider2D other)\n        {',
     '        private void OnTriggerEnter2D(Collider2D other)\n        {\n            if (PlayerEffectLock2D.IsProtectedTarget(other)) return;')])
diff += modify('Assets/Scripts/GamePlay/Traps/ElectricTileTrap2D.cs', [
    ('                if (health == null) continue;',
     '                if (health == null || PlayerEffectLock2D.IsProtectedTarget(health)) continue;')])
diff += modify('Assets/Scripts/System/DamageSystem.cs', [
    ('            float tickInterval = 0.5f)\n        {',
     '            float tickInterval = 0.5f)\n        {\n            if (PlayerEffectLock2D.IsProtectedTarget(this)) return;')])
diff += modify('Assets/Scripts/GamePlay/Map/JsonBlockBuilder2D.cs', [
    ('            actualCode = actualCode.Trim().ToUpperInvariant();',
     '            actualCode = actualCode.Trim().ToUpperInvariant();\n\n            // 벽형 이동 함정은 끝부분이 항상 보이며 기존 바닥 함정 Sprite 교체를 사용하지 않습니다.\n            if (actualCode == "102-014" || actualCode == "102-015") return false;')])
(OUT / 'Integration' / 'integration.patch').write_text(diff, encoding='utf-8')
(OUT / 'Integration' / 'manifest.json').write_text(json.dumps(changes,ensure_ascii=False,indent=2), encoding='utf-8')
for source in (OUT / 'New').rglob('*.cs'):
    meta = source.with_suffix(source.suffix+'.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n', encoding='utf-8')
print(f'Prepared {len(changes)} integration copies; original Assets unchanged.')
