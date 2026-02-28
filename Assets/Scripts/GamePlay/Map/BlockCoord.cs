using UnityEngine;

namespace Dungeon.Map
{
public class BlockCoord : MonoBehaviour
{
    [SerializeField] private string coord;
    public string Coord => coord;

    public void SetCoord(string c) => coord = c;
}
}