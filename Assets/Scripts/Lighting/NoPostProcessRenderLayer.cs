using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class NoPostProcessRenderLayer : MonoBehaviour
{
    public const string DefaultLayerName = "NoPostProcessFlame";

    [SerializeField] private string layerName = DefaultLayerName;
    [SerializeField] private bool includeChildren = true;
    [SerializeField] private bool keepAppliedInEditMode = true;

    private void OnEnable()
    {
        ApplyNow();
    }

    private void OnValidate()
    {
        ApplyNow();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying && keepAppliedInEditMode)
        {
            ApplyNow();
        }
    }

    public void ApplyNow()
    {
        int layer = ResolveLayer();

        if (layer < 0)
        {
            Debug.LogWarning($"Layer '{layerName}' does not exist. Add it before using no-post-process flame rendering.", this);
            return;
        }

        if (includeChildren)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == FlameLocalLight.LightObjectName)
                {
                    continue;
                }

                children[i].gameObject.layer = layer;
            }
        }
        else
        {
            gameObject.layer = layer;
        }
    }

    private int ResolveLayer()
    {
        return LayerMask.NameToLayer(string.IsNullOrWhiteSpace(layerName) ? DefaultLayerName : layerName.Trim());
    }
}
