using UnityEngine;
[System.Serializable]
public class CellData
{
    public string coord; // "0000"
    public string code;  // "100-000", "000-001", "001-001"
}

[System.Serializable]
public class MapData
{
    public string size;   // "0202" → 2x2
    public CellData[] grid;
}