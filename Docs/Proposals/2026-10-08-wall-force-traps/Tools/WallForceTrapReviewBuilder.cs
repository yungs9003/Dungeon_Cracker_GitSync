using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dungeon.Core;
using Dungeon.Player;
using Dungeon.Traps;
using Dungeon.System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// 격리된 Temp/WallForceTrapReview 프로젝트에서만 실행하는 제작/검증 도구입니다.
public static class WallForceTrapReviewBuilder
{
    private static readonly List<string> checks = new List<string>();
    private static string proposal;
    private static Sprite clip;
    private static Sprite arm;
    private static Sprite wall;

    public static void BuildAndValidate()
    {
        try
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            if (new DirectoryInfo(project).Name != "WallForceTrapReview")
                throw new InvalidOperationException("원본 프로젝트에서는 실행하지 않습니다.");
            proposal = Path.GetFullPath(Path.Combine(project, "../../Docs/Proposals/2026-10-08-wall-force-traps"));
            Directory.CreateDirectory(Path.Combine(proposal, "Validation"));
            clip = MakeClipSprite();
            arm = LoadSprite("224359df27f008a4e955b1720f365be4", "grab_proto_0");
            wall = LoadSprite("acc3e26f2f57dc14d99e15d366a51b3f", null);
            Check(arm != null && wall != null, "원본 PJ-005 및 벽 Sprite 로드");
            BuildPrefab("102-014", WallForceTrap2D.ForceMode.Push, 2);
            BuildPrefab("102-015", WallForceTrap2D.ForceMode.Pull, 4);
            BuildScene();
            RunChecks();
            AssetDatabase.SaveAssets();
            foreach (string folder in new[] { "Resources/Prefabs", "Sprites/Traps", "Scenes" })
            {
                string source = Path.Combine(Application.dataPath, folder);
                foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(Application.dataPath.Length + 1);
                    string destination = Path.Combine(proposal, "New/Assets", relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(file, destination, true);
                }
            }
            File.WriteAllLines(Path.Combine(proposal, "Validation/unity-checks.txt"), checks.Concat(new[] { "ALL CHECKS PASSED" }));
            Debug.Log("WALL FORCE REVIEW: ALL CHECKS PASSED (" + checks.Count + " assertions)");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (proposal != null) File.WriteAllText(Path.Combine(proposal, "Validation/unity-checks.txt"), string.Join("\n", checks) + "\nFAILED: " + error);
            EditorApplication.Exit(1);
        }
    }

    private static Sprite LoadSprite(string guid, string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid))
            .OfType<Sprite>().FirstOrDefault(sprite => name == null || sprite.name == name);
    }

    private static Sprite MakeClipSprite()
    {
        Directory.CreateDirectory("Assets/Sprites/Traps");
        var texture = new Texture2D(8, 8);
        texture.SetPixels(Enumerable.Repeat(Color.white, 64).ToArray());
        texture.Apply();
        File.WriteAllBytes("Assets/Sprites/Traps/WallForceClip.png", texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset("Assets/Sprites/Traps/WallForceClip.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Sprites/Traps/WallForceClip.png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 8f;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Traps/WallForceClip.png");
    }

    private static void BuildPrefab(string code, WallForceTrap2D.ForceMode mode, int cells)
    {
        Directory.CreateDirectory("Assets/Resources/Prefabs");
        var root = new GameObject(code);
        root.layer = LayerMask.NameToLayer("PlayerFront");
        root.AddComponent<BoxCollider2D>().size = Vector2.one * 0.9f;
        var wallVisual = Child(root.transform, "WallVisual");
        var wallRenderer = wallVisual.AddComponent<SpriteRenderer>();
        wallRenderer.sprite = wall;
        wallRenderer.sortingLayerName = "Wall";

        var assembly = Child(root.transform, "ArmAssembly");
        var sorting = assembly.AddComponent<SortingGroup>();
        sorting.sortingLayerName = "Projectile";
        sorting.sortingOrder = 2;
        var tip = Child(assembly.transform, "MovingTip");
        tip.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        var image = Child(tip.transform, "PJ-005");
        image.transform.localRotation = Quaternion.Euler(0, 0, 90);
        Vector3 scale = new Vector3(6f / arm.bounds.size.x, 1f, 1f);
        image.transform.localScale = scale;
        image.transform.localPosition = new Vector3(arm.bounds.center.y, -arm.bounds.max.x * scale.x, 0f);
        var renderer = image.AddComponent<SpriteRenderer>();
        renderer.sprite = arm;
        renderer.sortingLayerName = "Projectile";
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        var window = Child(assembly.transform, "FrontWindow");
        var mask = window.AddComponent<SpriteMask>();
        mask.sprite = clip;
        mask.alphaCutoff = 0.5f;
        var trap = root.AddComponent<WallForceTrap2D>();
        Set(trap, "mode", mode);
        Set(trap, "detectionCells", cells);
        Set(trap, "movingTip", tip.transform);
        Set(trap, "frontWindow", window.transform);
        Call(trap, "OnValidate");
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Prefabs/" + code + ".prefab");
        Object.DestroyImmediate(root);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/" + code + ".prefab");
        Check(prefab.GetComponent<WallForceTrap2D>().DetectionCells == cells, code + " 프리팹 직렬화/스크립트 연결");
        Check(prefab.transform.Find("ArmAssembly/MovingTip/PJ-005").GetComponent<SpriteRenderer>().sprite == arm, code + " 원본 이미지 참조");
    }

    private static GameObject Child(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void Tile(Vector2 position, Color color, string name, string layer = "Floor")
    {
        var tile = new GameObject(name);
        tile.transform.position = position;
        tile.transform.localScale = Vector3.one * 0.96f;
        var renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = clip;
        renderer.color = color;
        renderer.sortingLayerName = layer;
        renderer.sortingOrder = -5;
    }

    private static void Label(string text, Vector2 position, float size = 0.13f)
    {
        var root = new GameObject(text);
        root.transform.position = position;
        root.transform.localScale = Vector3.one * size;
        var label = root.AddComponent<TextMesh>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 48;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        root.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        root.GetComponent<MeshRenderer>().sortingLayerName = "ProjectileFront";
    }

    private static GameObject MakePlayer(Vector2 position, string name, Color color, bool controller = true)
    {
        var player = new GameObject(name);
        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Player");
        player.transform.position = position;
        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        player.AddComponent<BoxCollider2D>().size = Vector2.one * 0.5f;
        var visual = Child(player.transform, "Visual");
        visual.transform.localScale = Vector3.one * 0.5f;
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = clip;
        renderer.color = color;
        renderer.sortingLayerName = "Players";
        var health = player.AddComponent<Health>();
        health.maxHP = 20;
        health.onDied = new UnityEvent();
        health.onHealthChanged = new UnityEvent<int,int>();
        Set(health, "useInvincibility", false);
        Call(health, "Awake");
        player.AddComponent<KnockbackReceiver2D>();
        if (controller) Call(player.AddComponent<PlayerController2D>(), "Awake");
        return player;
    }

    private static WallForceTrap2D SpawnTrap(string code, Vector2 origin, Rigidbody2D body)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/" + code + ".prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = origin;
        var trap = instance.GetComponent<WallForceTrap2D>();
        Set(trap, "playerBody", body);
        Call(trap, "Awake");
        return trap;
    }

    private static void BuildScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        for (int lane = 0; lane < 2; lane++)
        {
            float x = lane == 0 ? -3f : 3f;
            for (int y = 1; y <= 6; y++)
            {
                var color = new Color(0.15f,0.18f,0.23f);
                if (y <= (lane == 0 ? 2 : 4)) color = lane == 0 ? new Color(0.08f,0.31f,0.37f) : new Color(0.38f,0.24f,0.10f);
                Tile(new Vector2(x,y-2), color, "Cell_" + (lane+1) + "_" + y);
                Label(y.ToString(), new Vector2(x-0.8f,y-2),0.07f);
            }
            var player = MakePlayer(new Vector2(x,lane == 0 ? 0 : 2), "ReviewPlayer"+lane,
                lane == 0 ? Color.cyan : new Color(1f,0.65f,0.15f));
            SpawnTrap(lane == 0 ? "102-014" : "102-015",new Vector2(x,-2),player.GetComponent<Rigidbody2D>());
            Label(lane == 0 ? "102-014 PUSH" : "102-015 PULL",new Vector2(x,4.9f),0.11f);
            Label(lane == 0 ? "Detect 1-2 / Move +3" : "Detect 1-4 / Land at 1",new Vector2(x,4.35f),0.07f);
            Label("WALL",new Vector2(x,-2.9f),0.08f);
        }
        Label("WALL FORCE TRAP REVIEW",new Vector2(0,6f),0.12f);
        Label("Original project unchanged / Play to preview",new Vector2(0,5.6f),0.06f);
        var camera = new GameObject("ReviewCamera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0,1.5f,-10);
        camera.orthographic = true;
        camera.orthographicSize = 5.5f;
        camera.backgroundColor = new Color(0.04f,0.055f,0.085f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(camera.gameObject.scene, "Assets/Scenes/WallForceTrapReview.unity");
        Render(camera, "review-idle.png");
        foreach (var trap in Object.FindObjectsByType<WallForceTrap2D>(FindObjectsSortMode.None))
        {
            Call(trap,"Tick",0.02f);
            for (int i = 0; i < 19; i++) Call(trap,"Tick",0.02f);
        }
        Render(camera,"review-moving.png");
        foreach (var trap in Object.FindObjectsByType<WallForceTrap2D>(FindObjectsSortMode.None)) trap.SetTrapEnabled(false);
        // 실행 전 초기 상태만 저장합니다. 렌더링 후 테스트 상태는 씬에 저장하지 않습니다.
    }

    private static void Render(Camera camera, string name)
    {
        var target = new RenderTexture(1600, 1000, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var texture = new Texture2D(1600,1000,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,1600,1000),0,0);
        texture.Apply();
        File.WriteAllBytes(Path.Combine(proposal,"Validation",name),texture.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(target);
    }

    private static void RunChecks()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var previousMode = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
        try
        {
            foreach (string code in new[]{"102-014","102-015"})
            {
                var player = MakePlayer(new Vector2(1000,1001.25f),"TestPlayer",Color.cyan);
                var body = player.GetComponent<Rigidbody2D>();
                var trap = SpawnTrap(code,new Vector2(1000,1000),body);
                int cells = code == "102-014" ? 2 : 4;
                foreach (float angle in new[]{0f,90f,-90f,180f})
                {
                    trap.transform.rotation = Quaternion.Euler(0,0,angle);
                    Check(trap.IsInsideDetection(trap.transform.TransformPoint(new Vector3(0,0.5f,0))), code+" 감지 시작/회전 "+angle);
                    Check(trap.IsInsideDetection(trap.transform.TransformPoint(new Vector3(0,cells+0.49f,0))),code+" 최외곽 타일/회전 "+angle);
                    Check(!trap.IsInsideDetection(trap.transform.TransformPoint(new Vector3(0,cells+0.5f,0))),code+" 범위 초과 배제/회전 "+angle);
                    Check(!trap.IsInsideDetection(trap.transform.TransformPoint(new Vector3(0.5f,1f,0))),code+" 옆 칸 배제/회전 "+angle);
                    Vector2 activation = trap.transform.TransformPoint(new Vector3(0,2,0));
                    Vector2 expected = trap.transform.TransformPoint(new Vector3(0,code=="102-014"?5:1,0));
                    Near(trap.GetDestination(activation),expected,code+" 목적지/회전 "+angle);
                }
                trap.transform.rotation = Quaternion.identity;
                if (code == "102-015") body.position = new Vector2(1000,1004);
                Vector2 destination = trap.GetDestination(body.position);
                Physics2D.SyncTransforms();
                Call(trap,"Tick",0.02f);
                var gate = player.GetComponent<PlayerEffectLock2D>();
                Check(gate.IsProtected && !body.simulated,code+" 발동 즉시 보호/물리 제외");
                Check(Physics2D.OverlapCircle(body.position,0.4f,1<<player.layer)==null,code+" 다른 함정 감지에서 제외");
                var otherOwner = new GameObject("OtherOwner");
                Check(!gate.TryAcquire(otherOwner),code+" 중복 강제 이동 잠금");
                gate.Release(otherOwner);
                Check(gate.IsProtected,code+" 다른 소유자가 잠금 해제 불가");
                Object.DestroyImmediate(otherOwner);
                Check(!trap.CanBeDestroyedByPlayer(),code+" 플레이어 파괴 불가");
                var controller = player.GetComponent<PlayerController2D>();
                Set(controller,"moveInput",Vector2.right);
                Vector2 before = body.position;
                Call(controller,"FixedUpdate");
                Near(body.position,before,code+" 일반 이동 입력의 위치 덮어쓰기 차단");
                var kb = player.GetComponent<KnockbackReceiver2D>();
                kb.AddKnockback(Vector2.right,5,1);
                Check(!kb.IsKnockbacking,code+" 외부 넉백 차단");
                var health = player.GetComponent<Health>();
                int hp = health.currentHP;
                int events = 0;
                Action<TrapDamageRequest> listener = request => events++;
                TrapDamageEvent.Raised += listener;
                try { Check(!TrapDamageEvent.Raise("101-001",trap.gameObject,health),code+" 공통 함정 피해 차단"); }
                finally { TrapDamageEvent.Raised -= listener; }
                Check(events==0,code+" 외부 피격 이벤트 차단");
                health.TakeDamage(5);
                health.ApplyDamageDirect(5);
                health.TakeBleedingDamage(1);
                var damage = player.AddComponent<DamageSystem>();
                damage.Initialize(health);
                damage.TakeDamage(5,Dungeon.Core.DamageType.True);
                damage.StartBleeding(1);
                damage.ApplyBurnFieldDamage(5);
                damage.ApplyIceFieldDamage(5);
                Check(health.currentHP==hp && !(bool)Get(damage,"isBleedingActive"),code+" 직접/True/출혈/화상/냉기 차단 (일반 무적 옵션 OFF)");
                var respawn = player.AddComponent<Respawn2D>();
                Call(respawn,"Awake");
                respawn.SetLastSafePoint(new Vector2(900,900));
                Check(!respawn.ReturnToLastSafePointFromTrap(3),code+" 낙사 복귀 차단");
                var tracker = player.AddComponent<PlayerSafePointTracker2D>();
                Call(tracker,"Awake");
                Call(tracker,"FixedUpdate");
                Near(respawn.GetLastSafePoint(),new Vector2(900,900),code+" 이동 중 안전 지점 보존");
                var pit = new GameObject("100-000");
                pit.transform.position = body.position;
                pit.layer = LayerMask.NameToLayer("Pit");
                pit.AddComponent<BoxCollider2D>().isTrigger = true;
                var cliff = pit.AddComponent<CliffTrap2D>();
                Set(cliff,"playerRb",body);
                Set(cliff,"playerLayer",(LayerMask)(1<<player.layer));
                Call(cliff,"OnEnable");
                Check(!(bool)Call(cliff,"TryAcquirePlayerFallLock",body),code+" 좌표 기반 추락 잠금 차단");
                Call(cliff,"FixedUpdate");
                Call(pit.AddComponent<PitTile2D>(),"OnTriggerEnter2D",player.GetComponent<Collider2D>());
                Check(!CliffTrap2D.IsPlayerFalling(body) && health.currentHP==hp,code+" 낭떠러지/구멍 실제 진입점 차단");
                Object.DestroyImmediate(pit);
                int ticks=0;
                while(trap.IsRunning && ticks++<200)
                {
                    Call(trap,"Tick",0.02f);
                    Physics2D.Simulate(0.02f);
                }
                Check(ticks<200 && !gate.IsProtected && body.simulated,code+" 회수 완료 뒤 보호/충돌 복구");
                Near(body.position,destination,code+" 전체 동작 목적지 도착");
                Check(controller.CanControl && controller.enabled,code+" 기존 조작 가능 상태 보존");
                health.ApplyDamageDirect(1);
                Check(health.currentHP==hp-1,code+" 보호 해제 뒤 정상 피해 복원");
                if(code=="102-015")
                {
                    for(int i=0;i<100;i++) Call(trap,"Tick",0.02f);
                    Check(!trap.IsRunning && !gate.IsProtected,"당기기 종료 위치에서 연속 재발동 방지");
                    body.position=new Vector2(1003,1001);
                    Call(trap,"Tick",0.02f);
                    body.position=new Vector2(1000,1003);
                    Call(trap,"Tick",0.02f);
                    Check(trap.IsRunning,"범위 이탈 후 재진입 재발동");
                    trap.SetTrapEnabled(false);
                    Check(body.simulated && !gate.IsProtected,"중간 Disable 시 잠금/물리 복구");
                }
                Object.DestroyImmediate(trap.gameObject);
                Object.DestroyImmediate(player);
            }
            CheckWallsAndTrapBodies();
        }
        finally { Physics2D.simulationMode = previousMode; }
    }

    private static void CheckWallsAndTrapBodies()
    {
        foreach(bool solidWall in new[]{true,false})
        {
            var player=MakePlayer(new Vector2(2000,2001.25f),"BlockTestPlayer",Color.cyan);
            var body=player.GetComponent<Rigidbody2D>();
            var trap=SpawnTrap("102-014",new Vector2(2000,2000),body);
            var obstacle=new GameObject(solidWall?"001-001_Wall":"RollingBallProjectile");
            obstacle.transform.position=new Vector2(2000,2003);
            obstacle.AddComponent<BoxCollider2D>().size=Vector2.one;
            if(!solidWall)
            {
                obstacle.AddComponent<RollingBallTrap2D>();
                obstacle.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;
            }
            Physics2D.SyncTransforms();
            Call(trap,"Tick",0.02f);
            for(int i=0;trap.IsRunning && i<200;i++) Call(trap,"Tick",0.02f);
            Check(solidWall?body.position.y<2002.26f:Mathf.Abs(body.position.y-2004.25f)<0.001f,
                solidWall?"001 벽 코드 정상 차단/벽 관통 방지":"실제 RollingBallTrap2D 본체 통과 (오브젝트 이름에 함정 코드 없음)");
            Object.DestroyImmediate(obstacle);
            Object.DestroyImmediate(trap.gameObject);
            Object.DestroyImmediate(player);
        }
    }

    private static void Check(bool passed,string name)
    {
        if(!passed) throw new InvalidOperationException(name);
        checks.Add("PASS: "+name);
    }
    private static void Near(Vector2 actual,Vector2 expected,string name) => Check((actual-expected).sqrMagnitude<0.00001f,name+" ["+actual+" -> "+expected+"]");
    private static object Call(object target,string name,params object[] args)
    {
        var method=target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(method==null) throw new MissingMethodException(target.GetType().Name,name);
        return method.Invoke(target,args);
    }
    private static void Set(object target,string name,object value) => target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    private static object Get(object target,string name) => target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
}
