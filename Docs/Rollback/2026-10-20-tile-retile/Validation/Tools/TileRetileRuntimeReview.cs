using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dungeon.Core;
using Dungeon.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TileRetileRuntimeReview
{
    public static void Run()
    {
        Directory.CreateDirectory("Assets/Resources/CodexRetileChecks");
        foreach (string code in new[] {"101-001","101-004","101-007","101-014","101-015"})
            foreach(string dir in new[]{"00","01","11","10"})
                Write(code+"_"+dir,Fixture(code,dir));
        Write("rubble",Fixture("001-005","00"));
        var map=Fixture("101-001","00"); map.floor3=map.grid;
        map.grid=map.floor3.Select(c=>new CellData2D{coord=c.coord,code="000-000",assetNo=0,direction="00"}).ToArray();
        Write("floor3",map);
        var neighbour=Fixture("101-001","00");
        var cell=neighbour.grid.Single(c=>c.coord=="0403");cell.code="101-004";cell.assetNo=0;cell.direction="10";Write("neighbour",neighbour);
        cell.code="101-014";Write("forceNeighbour",neighbour);
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"savefile.test.json"),StatusSaveDefaults.Serialize(StatusSaveDefaults.Create()));
        AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("TileRetileRuntimeChecks").AddComponent<TileRetileRuntimeChecks>();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/CodexTileRetileCheck.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/CodexTileRetileCheck.unity",true)};
        EditorApplication.isPlaying=true;
    }
    private static void Write(string name,MapData2D map)=>File.WriteAllText("Assets/Resources/CodexRetileChecks/"+name+".json",JsonUtility.ToJson(map,true).Replace("\"floor3\"","\"3F\""));
    private static MapData2D Fixture(string code,string dir)
    {
        var grid=new List<CellData2D>();
        int turns=Array.IndexOf(new[]{"00","01","11","10"},dir);
        for(int y=0;y<7;y++)for(int x=0;x<7;x++)
        {
            int dx=x-3,dy=y-3;
            for(int i=0;i<turns;i++){int old=dx;dx=dy;dy=-old;}
            grid.Add(new CellData2D{coord=$"{dx+3:x2}{dy+3:x2}",code=x==3&&y==3?code:y==4&&x>=2&&x<=4?"000-000":"001-001",direction=x==3&&y==3?dir:"00",assetNo=0});
        }
        var rules=TileRetileSolver2D.LoadRules(Resources.Load<TextAsset>("Data/TileRules"));
        var solve=typeof(TileRetileSolver2D).GetMethod("ResolveWall",BindingFlags.NonPublic|BindingFlags.Static);
        foreach(var wall in grid.Where(c=>c.code=="001-001"))
        {
            TileRetileSolver2D.TryParseCoord(wall.coord,out int x,out int y);
            var mask=new string[3];
            for(int r=0;r<3;r++)
            {
                var row=new char[3];
                for(int c=0;c<3;c++)
                {string coord=$"{x+c-1:x2}{y+1-r:x2}";var n=grid.FirstOrDefault(v=>v.coord==coord);row[c]=n==null||n.code=="001-001"||n.code.StartsWith("101-")?'1':'0';}
                mask[r]=new string(row);
            }
            var choice=(RetileResult2D)solve.Invoke(null,new object[]{wall,mask,rules.rulesets[0],false});
            if(choice!=null){wall.assetNo=choice.newAssetNo;wall.direction=choice.newDirection;}
        }
        return new MapData2D{size="0707",grid=grid.ToArray()};
    }
}
