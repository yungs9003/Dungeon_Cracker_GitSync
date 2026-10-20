using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dungeon.Core;
using Dungeon.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dungeon.Tests
{
    public class TileRetileTests
    {
        private TileRulesRoot rules;
        private TileRetileSolver2D solver;
        [SetUp] public void SetUp()
        {
            rules = TileRetileSolver2D.LoadRules(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Data/TileRules.json"));
            solver = new TileRetileSolver2D(rules, new[] { "101-001", "101-004", "101-007" });
        }

        [TestCase("101-001")][TestCase("101-004")][TestCase("101-007")]
        public void WallTrapFamilySharesReplacementAndWallClassification(string code)
        {
            var map = Map(); map.grid.Single(c => c.coord == "0202").code = code;
            var result = solver.DisarmTrapAndResolve(map, "0202");
            Assert.That(result[0].newCode, Is.EqualTo("000-000"));
            Assert.That(solver.TryGetReplacement(code, out var replacement), Is.True);
            Assert.That(TileRetileSolver2D.IsWallCode(code, replacement), Is.True);
            Assert.That(TileRetileSolver2D.IsWallCode("102-001", replacement), Is.False);
        }

        [Test] public void EveryDbWallTrapHasSharedRuleWithoutChangingDestructibility()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Data/db.json").text;
            var go = new GameObject("RetileDatabaseCheck");
            go.SetActive(false);
            try
            {
                var database = go.AddComponent<GameDatabase>();
                database.LoadDatabase();
                foreach (var trap in database.Traps.Where(t => t.code.StartsWith("101-")))
                {
                    Assert.That(solver.TryGetReplacement(trap.code, out _), Is.EqualTo(trap.destructible == true), trap.code);
                    Assert.That(TileRetileSolver2D.IsWallCode(trap.code, rules.TileReplacements[0]), Is.True, trap.code);
                }
                Assert.That(database.TryGetTrap("101-014", out var push), Is.True);
                Assert.That(push.destructible, Is.False);
                Assert.That(database.TryGetTrap("101-015", out var pull), Is.True);
                Assert.That(pull.destructible, Is.False);
                Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Data/db.json").text, Is.EqualTo(json));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase("101-014")][TestCase("101-015")][TestCase("101-999")]
        public void NonDestructibleOrUnknownTrapsAreWallsButNotReplacementTargets(string code)
        {
            var map = Map(); map.grid.Single(c => c.coord == "0202").code = code;
            Assert.That(solver.DisarmTrapAndResolve(map, "0202"), Is.Empty);
            Assert.That(TileRetileSolver2D.IsWallCode(code, rules.TileReplacements[0]), Is.True);
        }
        [Test] public void ANewDbApprovedWallTrapGetsThePolicyWithoutAnotherJsonEntry()
        {
            var future = new TileRetileSolver2D(rules, new[] { "101-999" });
            Assert.That(future.TryGetReplacement("101-999", out _), Is.True);
            Assert.That(future.TryGetReplacement("101-001", out _), Is.False);
        }
        [Test] public void NoDbDestructibleListDoesNotGrantDestruction()
        { Assert.That(new TileRetileSolver2D(rules).TryGetReplacement("101-001", out _), Is.False); }

        [Test] public void CalculationPreservesAllOriginalMapFields()
        {
            var map = Map();
            map.size = "0505"; map.floor3 = new[] { new CellData2D { coord="0202", code="102-001" } };
            map.relation = new[] { new RelationData2D { coord_m="0202",coord_t="0101" } };
            map.player = new PlayerData2D { start="0000" }; map.field_effect = new FieldEffectData2D { type="ice", damage=5 };
            string before = JsonUtility.ToJson(map);
            solver.DisarmTrapAndResolve(map, "0202");
            Assert.That(JsonUtility.ToJson(map), Is.EqualTo(before));
        }

        [Test] public void PreviousSpritesDoNotInfluenceOnePassSelection()
        {
            var map = Map();
            string before = Choices(solver.DisarmTrapAndResolve(map, "0202"));
            foreach (var c in map.grid) { c.assetNo = (c.assetNo + 9) % 14; c.direction = "10"; }
            Assert.That(Choices(solver.DisarmTrapAndResolve(map, "0202")), Is.EqualTo(before));
        }

        [Test] public void ResultsRemainTheSameAfterApplyingNeighbourChoices()
        {
            var map = Map(); var first = solver.DisarmTrapAndResolve(map, "0202");
            foreach (var r in first.Skip(1))
            { var c=map.grid.Single(v=>v.coord==r.coord); c.assetNo=r.newAssetNo; c.direction=r.newDirection; }
            Assert.That(Choices(solver.DisarmTrapAndResolve(map, "0202")), Is.EqualTo(Choices(first)));
        }

        [Test] public void RemainingTrapKeepsItsAttackMetadataInVisualOnlyResult()
        {
            var map=Map(); var neighbour=map.grid.Single(c=>c.coord=="0302");
            neighbour.code="101-004"; neighbour.assetNo=1; neighbour.direction="10";
            var result=solver.DisarmTrapAndResolve(map,"0202").Single(r=>r.coord=="0302");
            Assert.That(result.wallVisualOnly,Is.True);
            Assert.That(result.newCode,Is.EqualTo("101-004"));
            Assert.That(neighbour.direction,Is.EqualTo("10")); Assert.That(neighbour.assetNo,Is.EqualTo(1));
        }

        [Test] public void AllNonIsolatedTopologiesProduceMatchingCardinalOpenings()
        {
            var method=typeof(TileRetileSolver2D).GetMethod("ResolveWall",BindingFlags.NonPublic|BindingFlags.Static);
            int allowed=0,isolated=0;
            for(int bits=0;bits<256;bits++)
            {
                var chars="000010000".ToCharArray(); int bit=0;
                for(int p=0;p<9;p++) if(p!=4) chars[p]=((bits>>bit++)&1)==1?'1':'0';
                string[] mask={new string(chars,0,3),new string(chars,3,3),new string(chars,6,3)};
                var result=(RetileResult2D)method.Invoke(null,new object[]{new CellData2D{coord="0202",code="001-001"},mask,rules.rulesets[0],false});
                bool isIsolated=chars[1]=='0'&&chars[3]=='0'&&chars[5]=='0'&&chars[7]=='0';
                if(isIsolated){isolated++;Assert.That(result,Is.Null);continue;}
                allowed++; Assert.That(result,Is.Not.Null,$"pattern {bits}");
                var tile=rules.rulesets[0].tiles.Single(t=>t.assetNo==result.newAssetNo);
                string[] sides=Enumerable.Range(0,4).Select(i=>tile.signature.Substring(i*2,2)).ToArray();
                int turns=Array.IndexOf(new[]{"00","01","11","10"},result.newDirection);
                int[] positions={1,5,7,3};
                for(int side=0;side<4;side++) Assert.That(sides[(side-turns+4)%4]!="00",Is.EqualTo(chars[positions[side]]=='1'),$"pattern {bits} side {side}");
            }
            Assert.That(allowed,Is.EqualTo(240));Assert.That(isolated,Is.EqualTo(16));
        }

        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)][TestCase(6)]
        [TestCase(8)][TestCase(9)][TestCase(10)][TestCase(11)][TestCase(12)]
        public void EveryAuthoredSpriteMaskCanBeSelected(int asset)
        {
            var pattern=rules.rulesets[0].structureMasks.First(m=>m.candidates.Contains(asset)).mask3x3.Select(s=>s.Replace('?','0')).ToArray();
            var method=typeof(TileRetileSolver2D).GetMethod("ResolveWall",BindingFlags.NonPublic|BindingFlags.Static);
            var result=(RetileResult2D)method.Invoke(null,new object[]{new CellData2D{coord="0202",code="001-001"},pattern,rules.rulesets[0],false});
            Assert.That(result.newAssetNo,Is.EqualTo(asset)); Assert.That(result.reason,Is.EqualTo("structure-mask"));
        }

        [Test] public void InvalidSignatureOrderIsRejected()
        { rules.signatureOrder="NEWS"; Assert.Throws<ArgumentException>(()=>new TileRetileSolver2D(rules)); }
        [Test] public void SignatureAndMaskCardinalsMustAgree()
        { rules.rulesets[0].tiles.Single(t=>t.assetNo==4).signature="06000006"; Assert.Throws<ArgumentException>(()=>new TileRetileSolver2D(rules)); }
        [TestCase(null)][TestCase("")][TestCase("fffff")][TestCase("zzzz")][TestCase("ffff")]
        public void MissingOrInvalidCoordinateReturnsNoChanges(string coord)
        { Assert.That(solver.DisarmTrapAndResolve(Map(),coord),Is.Empty); }
        [Test] public void UnsignedHexCoordinatesAndRotationAreBounded()
        {
            Assert.That(TileRetileSolver2D.TryParseCoord(" Ff00 ",out int x,out int y),Is.True);
            Assert.That(x,Is.EqualTo(255));Assert.That(y,Is.EqualTo(0));
            string[] mask={"100","010","001"};
            Assert.That(TileRetileSolver2D.RotateMask(mask,4),Is.EqualTo(mask));
            Assert.That(TileRetileSolver2D.RotateMask(TileRetileSolver2D.RotateMask(mask,1),3),Is.EqualTo(mask));
        }
        private static string Choices(IEnumerable<RetileResult2D> results)=>string.Join(";",results.Select(r=>$"{r.coord}:{r.newCode}:{r.newAssetNo}:{r.newDirection}:{r.wallVisualOnly}"));
        private static MapData2D Map()
        {
            var cells=new List<CellData2D>();
            for(int y=0;y<5;y++)for(int x=0;x<5;x++)cells.Add(new CellData2D{coord=$"{x:x2}{y:x2}",code=x==2&&y==2?"101-001":y==3&&x>=1&&x<=3?"000-000":"001-001",assetNo=0,direction="00"});
            return new MapData2D{size="0505",grid=cells.ToArray()};
        }
    }
}
