using UnityEngine;

public class TrapZoneHighlight : MonoBehaviour
{
    [SerializeField] private SimpleOutline outline;

    private bool isHighlighted;

    private void Awake()
    {
        if (outline == null)
            outline = GetComponent<SimpleOutline>();

        SetHighlighted(false);
    }

    public void SetHighlighted(bool value)
    {
        if (isHighlighted == value) return;
        isHighlighted = value;

        if (outline != null)
            outline.SetOutlineEnabled(value);
    }

    private void OnDisable()
    {
        if (outline != null)
            outline.SetOutlineEnabled(false);

        isHighlighted = false;
    }
}