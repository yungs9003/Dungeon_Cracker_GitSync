using UnityEngine;

public class UIButtonClickProbe : MonoBehaviour
{
    public void LogClick()
    {
        Debug.Log($"[UIButtonClickProbe] Clicked: {gameObject.name}");
    }
}