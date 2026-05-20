using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-80)]
public class IdeaManager : MonoBehaviour
{
    public const string ElsewhereIdeaId = "elsewhere";

    public static event Action<IdeaDefinition> AnyCurrentIdeaChanged;

    [SerializeField] private List<IdeaDefinition> ideas = new List<IdeaDefinition>();
    [SerializeField] private string startingIdeaId;
    [SerializeField] private bool keepAcrossSceneLoads = true;
    [SerializeField] private bool logWarnings = true;

    private IdeaDefinition currentIdea;
    private bool createdAtRuntime;

    public static IdeaManager Instance { get; private set; }
    public IdeaDefinition CurrentIdea => currentIdea;
    public string CurrentIdeaId => currentIdea != null ? currentIdea.Id : string.Empty;
    public bool IsExploringIdea => currentIdea != null && !currentIdea.IsElsewhere;
    public bool IsElsewhere => currentIdea != null && currentIdea.IsElsewhere;
    public IReadOnlyList<IdeaDefinition> Ideas => ideas;

    public event Action<IdeaDefinition> CurrentIdeaChanged;

    public static IdeaManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        IdeaManager existingManager = UnityEngine.Object.FindObjectOfType<IdeaManager>(true);

        if (existingManager != null)
        {
            Instance = existingManager;
            existingManager.EnsureDefaultIdeas();
            return existingManager;
        }

        GameObject managerObject = new GameObject("IdeaManager");
        IdeaManager manager = managerObject.AddComponent<IdeaManager>();
        manager.createdAtRuntime = true;
        return manager;
    }

    public static List<IdeaDefinition> CreateDefaultIdeas()
    {
        return new List<IdeaDefinition>
        {
            new IdeaDefinition(
                "inheritance",
                "The Inherited Shape",
                "Rooms read like obligations. Objects seem handed down by someone who expected you to become them.",
                new Color(0.74f, 0.37f, 0.18f, 1f),
                0.16f),
            new IdeaDefinition(
                "appetite",
                "The Patient Appetite",
                "The house waits with its mouth closed. Useful things feel slightly too eager to be used.",
                new Color(0.75f, 0.08f, 0.13f, 1f),
                0.14f),
            new IdeaDefinition(
                "witness",
                "The Witness in the Glass",
                "Frames, mirrors, and windows begin to agree with each other. The room feels aware before it feels hostile.",
                new Color(0.34f, 0.68f, 0.88f, 1f),
                0.13f),
            new IdeaDefinition(
                ElsewhereIdeaId,
                "Elsewhere",
                "The Odd Place. Not another world exactly; more like the house remembering that it was never only a house.",
                new Color(0.62f, 0.48f, 0.82f, 1f),
                0.22f,
                true)
        };
    }

    private void Reset()
    {
        EnsureDefaultIdeas();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Instance.createdAtRuntime && !createdAtRuntime)
            {
                Destroy(Instance.gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        Instance = this;
        EnsureDefaultIdeas();

        if (keepAcrossSceneLoads && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (!string.IsNullOrWhiteSpace(startingIdeaId))
        {
            TrySetCurrentIdea(startingIdeaId);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ExploreIdea(string ideaId)
    {
        TrySetCurrentIdea(ideaId);
    }

    public bool TryExploreIdea(string ideaId)
    {
        return TrySetCurrentIdea(ideaId);
    }

    public void EnterElsewhere()
    {
        TrySetCurrentIdea(ElsewhereIdeaId);
    }

    public void ClearIdea()
    {
        TrySetCurrentIdea(string.Empty);
    }

    public bool TryGetIdea(string ideaId, out IdeaDefinition idea)
    {
        EnsureDefaultIdeas();

        string normalizedId = IdeaDefinition.NormalizeId(ideaId);

        for (int i = 0; i < ideas.Count; i++)
        {
            IdeaDefinition candidate = ideas[i];

            if (candidate == null)
            {
                continue;
            }

            if (candidate.Id == normalizedId || IdeaDefinition.NormalizeId(candidate.DisplayName) == normalizedId)
            {
                idea = candidate;
                return true;
            }
        }

        idea = null;
        return false;
    }

    public bool HasIdea(string ideaId)
    {
        return TryGetIdea(ideaId, out _);
    }

    private bool TrySetCurrentIdea(string ideaId)
    {
        EnsureDefaultIdeas();

        string normalizedId = IdeaDefinition.NormalizeId(ideaId);
        IdeaDefinition nextIdea = null;

        if (!string.IsNullOrEmpty(normalizedId) && !TryGetIdea(normalizedId, out nextIdea))
        {
            Warn($"Idea '{ideaId}' does not exist on {name}.");
            return false;
        }

        if ((currentIdea == null && nextIdea == null) ||
            (currentIdea != null && nextIdea != null && currentIdea.Id == nextIdea.Id))
        {
            return true;
        }

        currentIdea = nextIdea;
        CurrentIdeaChanged?.Invoke(currentIdea);
        AnyCurrentIdeaChanged?.Invoke(currentIdea);
        return true;
    }

    private void EnsureDefaultIdeas()
    {
        if (ideas == null)
        {
            ideas = new List<IdeaDefinition>();
        }

        if (ideas.Count > 0)
        {
            return;
        }

        ideas = CreateDefaultIdeas();
    }

    private void Warn(string message)
    {
        if (logWarnings)
        {
            Debug.LogWarning(message, this);
        }
    }
}
