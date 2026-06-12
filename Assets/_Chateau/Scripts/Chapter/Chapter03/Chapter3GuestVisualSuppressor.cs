using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Chapter3GuestVisualSuppressor : MonoBehaviour
{
    [SerializeField] private ActorRoomState actorState;
    [SerializeField] private bool suppressRenderers = true;
    [SerializeField] private bool suppressGraphics = true;
    [SerializeField] private bool suppressAnimators = true;
    [SerializeField] private bool suppressColliders = true;

    private readonly Dictionary<Renderer, bool> rendererStates = new Dictionary<Renderer, bool>();
    private readonly Dictionary<Graphic, bool> graphicStates = new Dictionary<Graphic, bool>();
    private readonly Dictionary<Animator, bool> animatorStates = new Dictionary<Animator, bool>();
    private readonly Dictionary<Collider, bool> colliderStates = new Dictionary<Collider, bool>();
    private readonly Dictionary<Collider2D, bool> collider2DStates = new Dictionary<Collider2D, bool>();
    private bool initialized;
    private bool loggedSuppress;

    public void Initialize(ActorRoomState actor)
    {
        actorState = actor != null ? actor : GetComponent<ActorRoomState>();
        CacheOriginalStates();
        initialized = true;
    }

    public void Suppress()
    {
        if (!initialized)
        {
            Initialize(actorState != null ? actorState : GetComponent<ActorRoomState>());
        }

        if (suppressRenderers)
        {
            foreach (Renderer renderer in rendererStates.Keys)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        if (suppressGraphics)
        {
            foreach (Graphic graphic in graphicStates.Keys)
            {
                if (graphic != null)
                {
                    graphic.enabled = false;
                    graphic.raycastTarget = false;
                }
            }
        }

        if (suppressAnimators)
        {
            foreach (Animator animator in animatorStates.Keys)
            {
                if (animator != null)
                {
                    animator.enabled = false;
                }
            }
        }

        if (suppressColliders)
        {
            foreach (Collider collider in colliderStates.Keys)
            {
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }

            foreach (Collider2D collider in collider2DStates.Keys)
            {
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        if (!loggedSuppress)
        {
            loggedSuppress = true;
            string actorId = actorState != null ? actorState.ActorId : name;
            Debug.Log($"[Ch3Dining] Suppressed original standing guest visual: {actorId}", this);
        }
    }

    public void Restore()
    {
        foreach (KeyValuePair<Renderer, bool> pair in rendererStates)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        foreach (KeyValuePair<Graphic, bool> pair in graphicStates)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        foreach (KeyValuePair<Animator, bool> pair in animatorStates)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        foreach (KeyValuePair<Collider, bool> pair in colliderStates)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        foreach (KeyValuePair<Collider2D, bool> pair in collider2DStates)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }
    }

    private void CacheOriginalStates()
    {
        rendererStates.Clear();
        graphicStates.Clear();
        animatorStates.Clear();
        colliderStates.Clear();
        collider2DStates.Clear();

        Transform root = actorState != null ? actorState.transform : transform;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && !IsCh3Transform(renderers[i].transform))
            {
                rendererStates[renderers[i]] = renderers[i].enabled;
            }
        }

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null && !IsCh3Transform(graphics[i].transform))
            {
                graphicStates[graphics[i]] = graphics[i].enabled;
            }
        }

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null && !IsCh3Transform(animators[i].transform))
            {
                animatorStates[animators[i]] = animators[i].enabled;
            }
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !IsCh3Transform(colliders[i].transform))
            {
                colliderStates[colliders[i]] = colliders[i].enabled;
            }
        }

        Collider2D[] colliders2D = root.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders2D.Length; i++)
        {
            if (colliders2D[i] != null && !IsCh3Transform(colliders2D[i].transform))
            {
                collider2DStates[colliders2D[i]] = colliders2D[i].enabled;
            }
        }
    }

    private static bool IsCh3Transform(Transform candidate)
    {
        Transform current = candidate;

        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.name) &&
                current.name.StartsWith("Ch3_", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
