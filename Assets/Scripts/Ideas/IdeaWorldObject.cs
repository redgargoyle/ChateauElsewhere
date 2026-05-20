using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class IdeaWorldObject : MonoBehaviour, IPointerClickHandler
{
    public static event Action<IdeaWorldObject> SelectedObjectChanged;
    public static IdeaWorldObject SelectedObject { get; private set; }

    [SerializeField] private string displayName;
    [TextArea(2, 4)]
    [SerializeField] private string description;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ObjectNameToLabel(name) : displayName.Trim();
    public string Description => description ?? string.Empty;

    public void Configure(string newDisplayName, string newDescription)
    {
        displayName = newDisplayName;
        description = newDescription;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Select();
    }

    public void Select()
    {
        if (SelectedObject == this)
        {
            return;
        }

        SelectedObject = this;
        SelectedObjectChanged?.Invoke(this);
    }

    public static void ClearSelection(IdeaWorldObject selectedObject)
    {
        if (SelectedObject != selectedObject)
        {
            return;
        }

        SelectedObject = null;
        SelectedObjectChanged?.Invoke(null);
    }

    private void OnDestroy()
    {
        ClearSelection(this);
    }

    private static string ObjectNameToLabel(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return "Object";
        }

        string label = objectName.Replace('_', ' ').Trim();
        return string.IsNullOrEmpty(label) ? "Object" : label;
    }
}
