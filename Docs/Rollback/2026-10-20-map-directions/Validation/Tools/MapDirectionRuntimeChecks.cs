using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dungeon.Core;
using Dungeon.Map;
using Dungeon.Traps;
using UnityEngine;

public sealed class MapDirectionRuntimeChecks : MonoBehaviour
{
    [Serializable] private sealed class Audit { public Entry[] entries; }
    [Serializable] private sealed class Entry { public string path,layer,coord,code,after; }
    private readonly List<string> checks=new();
    private int failures;
    private string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../Docs/Rollback/2026-10-20-map-directions/Validation"));
    private void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);if(!value)failures++;}
    private IEnumerator Start()
    {
        Application.logMessageReceived+=OnLog;
        yield return null;
        new GameObject("Registry").AddComponent<BlockRegistry>();
        var builder=new GameObject("MapBuilder").AddComponent<JsonBlockBuilder2D>();
        builder.autoBuildOnStart=false;
        builder.player=new GameObject("PlayerAnchor").transform;
        var vision=new GameObject("Vision").AddComponent<ForwardZoneVisionMask2D>();
        vision.SetDarkAlpha(0);builder.visionSystem=vision;
        var audit=JsonUtility.FromJson<Audit>(Resources.Load<TextAsset>("CodexMapDirectionAudit").text);
        foreach(var group in audit.entries.GroupBy(e=>e.path))
        {
            string resource=group.Key.Substring("Assets/Resources/".Length);
            resource=resource.Substring(0,resource.Length-5);
            builder.BuildFromJsonPath(resource);
            foreach(var trap in builder.GetComponentsInChildren<MonoBehaviour>(true).OfType<ITrapToggle>())trap.SetTrapEnabled(false);
            foreach(var visual in builder.GetComponentsInChildren<HiddenTrapVisual2D>())visual.ForceReveal();
            yield return null;yield return null;
            Physics2D.SyncTransforms();
            foreach(var item in group)
            {
                string label=resource+" "+item.code+"@"+item.coord;
                var data=builder.LoadedMapData.grid.Single(c=>string.Equals(c.coord,item.coord,StringComparison.OrdinalIgnoreCase));
                Check(data.direction==item.after,label+" JSON direction");
                Check(BlockRegistry.Instance.TryGet(item.coord,out var block),label+" actual prefab");
                if(block==null)continue;
                Vector2 expected=Direction(item.after);
                Check(Vector2.Dot(block.transform.up,expected)>0.9999f,label+" sprite forward");
                TileRetileSolver2D.TryParseCoord(item.coord,out int x,out int y);
                int nx=x+Mathf.RoundToInt(expected.x),ny=y+Mathf.RoundToInt(expected.y);
                string neighbour=$"{nx:x2}{ny:x2}";
                var front=builder.LoadedMapData.grid.FirstOrDefault(c=>string.Equals(c.coord,neighbour,StringComparison.OrdinalIgnoreCase));
                Check(front!=null&&(front.code.StartsWith("000-")||front.code.StartsWith("102-")),label+" faces floor corridor");
                var variant=block.GetComponent<AssetVariantSprite2D>();
                Check(variant!=null&&variant.Renderer.sprite==variant.GetSpriteForAssetNo(item.code=="101-007"?1:0,false),label+" original sprite reference");
                if(item.code=="101-007")
                {
                    var existing=FindObjectsByType<RollingBallTrap2D>(FindObjectsSortMode.None).Select(b=>b.GetInstanceID()).ToHashSet();
                    block.GetComponent<BallLauncher2D>().Spawn();
                    var ball=FindObjectsByType<RollingBallTrap2D>(FindObjectsSortMode.None).SingleOrDefault(b=>!existing.Contains(b.GetInstanceID()));
                    Check(ball!=null,label+" actual rolling ball spawn");
                    if(ball!=null)
                    {
                        Vector2 velocity=ball.GetComponent<Rigidbody2D>().linearVelocity;
                        Vector2 offset=ball.transform.position-block.transform.position;
                        Check(Vector2.Dot(velocity.normalized,expected)>0.9999f,label+" projectile follows sprite forward");
                        Check(Vector2.Dot(offset,expected)>0,label+" projectile starts in front");
                        Check(Mathf.Abs(offset.x*expected.y-offset.y*expected.x)<0.01f,label+" projectile has no sideways spawn offset");
                        Check(Mathf.Abs(velocity.magnitude-3)<0.001f,label+" authored speed preserved");
                        Destroy(ball.gameObject);
                    }
                }
            }
            if(resource=="Maps/DUNGEON_001/map_001")
            {Capture("map001-portal.png",new Vector2(7,1),2);Capture("map001-ball-launcher.png",new Vector2(8,4),2);}
            if(resource=="Maps/CheckpointScene/checkpoint")Capture("checkpoint-exit.png",new Vector2(3,7),2);
        }
        Finish();
    }
    private static Vector2 Direction(string direction)=>direction=="01"?Vector2.right:direction=="10"?Vector2.left:direction=="11"?Vector2.down:Vector2.up;
    private void Capture(string name,Vector2 centre,float size)
    {
        var go=new GameObject("Camera");var camera=go.AddComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=size;camera.transform.position=new Vector3(centre.x,centre.y,-10);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.2f,0.2f,0.2f);
        var rt=new RenderTexture(900,600,24);rt.Create();camera.targetTexture=rt;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var texture=new Texture2D(900,600,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,900,600),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(Output,name),texture.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Destroy(rt);Destroy(texture);Destroy(go);
    }
    private void OnLog(string text,string stack,LogType type)
    {
        if(type!=LogType.Exception)return;
        checks.Add("FAIL UNHANDLED "+text+"\n"+stack);failures++;
        UnityEditor.EditorApplication.delayCall+=Finish;
    }
    private void Finish()
    {
        Application.logMessageReceived-=OnLog;
        File.WriteAllLines(Path.Combine(Output,"runtime-checks.txt"),checks.Concat(new[]{$"TOTAL {checks.Count}, FAILED {failures}"}));
        UnityEditor.EditorApplication.Exit(failures==0?0:1);
    }
}
