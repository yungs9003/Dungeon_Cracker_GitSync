using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.Core
{
[Serializable]
public class DungeonCatalog
{
    public List<DungeonInfo> dungeons = new();
}

[Serializable]
public class DungeonInfo
{
    public string id;
    public string name;
    public string sceneName;
    public string previewSpritePath;
    public string description;
    public List<string> tags = new();
    public List<FarmItemInfo> farmableItems = new();
    public List<string> clearCriteria = new(); // 길이 4 권장
}

[Serializable]
public class FarmItemInfo
{
    public string id;
    public string iconSpritePath;
}

// -------- Save --------

[Serializable]
public class PlayerSaveData
{
    public int version = 1;
    public List<DungeonProgress> dungeons = new();
}

[Serializable]
public class DungeonProgress
{
    public string dungeonId;
    public bool[] criteria = new bool[4]; // 항상 4칸
    public long bestTimeMs;
    public int clearCount;
    public string lastPlayedUtc;
}
}