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
    public int version = 2;
    public List<DungeonProgress> dungeons = new();

    // -------- Checkpoint respawn --------
    // true면 HP 0 리스폰 시 현재 맵 시작점이 아니라 checkpointSceneName으로 이동한다.
    public bool hasCheckpoint = false;

    // 실제 체크포인트 씬 이름
    public string checkpointSceneName = "CheckpointScene";

    // 체크포인트에서 나갔을 때 돌아갈 던전/맵 씬 이름
    public string checkpointReturnSceneName = "";

    // 돌아갈 씬에서 바로 로드할 JSON 맵 index.
    // 체크포인트가 currentIndex 맵에서 발동했다면 보통 currentIndex + 1을 저장한다.
    public int checkpointNextMapIndex = 0;

    // 체크포인트 씬에서 나가기 직전에만 사용하는 1회성 예약값
    public string pendingReturnSceneName = "";
    public int pendingMapIndex = -1;
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
