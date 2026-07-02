using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Dining Room Seated Guest Occlusion Controller")]
public sealed class DiningRoomSeatedGuestOcclusionController : MonoBehaviour
{
    private const string DiningSeatPrefix = "Ch2_DiningSeat_";

    [Serializable]
    public sealed class SeatBinding
    {
        public RoomAnchor seatAnchor;
        public GameObject assignedChair;
        public SpriteRenderer assignedChairRenderer;
    }

    [SerializeField] private string diningRoomName = "Dining Room";
    [SerializeField] private string butlerExclusionObjectName = "Butler";
    [SerializeField] private GameObject diningTable;
    [SerializeField] private SpriteRenderer diningTableRenderer;
    [SerializeField] private Transform diningTableSortAnchor;
    [SerializeField] private List<SeatBinding> seatBindings = new List<SeatBinding>();

    public string DiningRoomName => diningRoomName;
    public string ButlerExclusionObjectName => butlerExclusionObjectName;
    public GameObject DiningTable => diningTable;
    public SpriteRenderer DiningTableRenderer => diningTableRenderer;
    public Transform DiningTableSortAnchor => diningTableSortAnchor;
    public IReadOnlyList<SeatBinding> SeatBindings => seatBindings;

    public static DiningRoomSeatedGuestOcclusionController FindInScene()
    {
        return FindAnyObjectByType<DiningRoomSeatedGuestOcclusionController>(FindObjectsInactive.Include);
    }

    public void ActivateForGuest(ActorRoomState actorState, RoomAnchor diningSeat)
    {
        if (actorState == null || diningSeat == null)
        {
            return;
        }

        if (IsButlerActor(actorState))
        {
            return;
        }

        SeatBinding binding = FindBinding(diningSeat);

        if (binding == null)
        {
            Debug.LogError(
                $"Dining seated occlusion has no binding for seat '{diningSeat.name}'. " +
                $"Expected a serialized {DiningSeatPrefix} binding.",
                this);
            return;
        }

        if (binding.assignedChair == null ||
            binding.assignedChairRenderer == null ||
            diningTableRenderer == null)
        {
            Debug.LogError($"Dining seated occlusion binding for seat '{diningSeat.name}' is incomplete.", this);
            return;
        }

        DiningRoomSeatedGuestOcclusionException exception =
            actorState.GetComponent<DiningRoomSeatedGuestOcclusionException>();

        if (exception == null)
        {
            exception = actorState.gameObject.AddComponent<DiningRoomSeatedGuestOcclusionException>();
        }

        exception.ActivateForDiningSeat(
            actorState,
            binding.seatAnchor,
            binding.assignedChair,
            binding.assignedChairRenderer,
            diningTableRenderer,
            diningRoomName,
            butlerExclusionObjectName);
    }

    public void DeactivateForGuest(ActorRoomState actorState)
    {
        if (actorState == null)
        {
            return;
        }

        DiningRoomSeatedGuestOcclusionException exception =
            actorState.GetComponent<DiningRoomSeatedGuestOcclusionException>();

        if (exception != null)
        {
            exception.DeactivateForDiningSeat();
        }
    }

    private SeatBinding FindBinding(RoomAnchor diningSeat)
    {
        if (diningSeat == null || seatBindings == null)
        {
            return null;
        }

        for (int i = 0; i < seatBindings.Count; i++)
        {
            SeatBinding binding = seatBindings[i];

            if (binding == null || binding.seatAnchor == null)
            {
                continue;
            }

            if (binding.seatAnchor == diningSeat ||
                SameId(binding.seatAnchor.AnchorId, diningSeat.AnchorId) ||
                SameId(binding.seatAnchor.name, diningSeat.name))
            {
                return binding;
            }
        }

        return null;
    }

    private bool IsButlerActor(ActorRoomState actorState)
    {
        return actorState != null &&
            (ContainsOrdinalIgnoreCase(actorState.ActorId, butlerExclusionObjectName) ||
            ContainsOrdinalIgnoreCase(actorState.name, butlerExclusionObjectName));
    }

    private static bool SameId(string left, string right)
    {
        return string.Equals(Clean(left), Clean(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool ContainsOrdinalIgnoreCase(string value, string fragment)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(fragment) &&
            value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
