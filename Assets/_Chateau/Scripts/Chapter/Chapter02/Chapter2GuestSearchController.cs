using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Chapter2GuestSearchController : MonoBehaviour
{
    private const string PersistentActorRootName = "ChapterActors_Runtime";

    [Serializable]
    public class GuestSearchEntry
    {
        public string guestId;
        public string displayName;
        public ActorRoomState actorState;
        public RoomAnchor hideAnchor;
        public string mealPreference;
        public string smokingPreference;
        public string spiritBottle;
        public bool found;
        public int foundOrder;
    }

    [SerializeField] private List<GuestSearchEntry> guests = new List<GuestSearchEntry>();
    [SerializeField] private string hideAnchorPrefix = "Ch2_Hide_";
    [SerializeField] private string hideRoomId = "Ballroom";
    [SerializeField] private string diningSeatPrefix = "Ch2_DiningSeat_";
    [SerializeField] private int foundOrderCounter;

    private Chapter2Controller chapter2Controller;

    public string DiningSeatPrefix => diningSeatPrefix;
    public string HideRoomId => hideRoomId;

    public bool AllGuestsFound
    {
        get
        {
            if (guests == null || guests.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < guests.Count; i++)
            {
                if (guests[i] == null || !guests[i].found)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public void Initialize(Chapter2Controller controller)
    {
        chapter2Controller = controller;
    }

    public void BeginSearch()
    {
        AutoDiscoverGuestsIfNeeded();
        AutoAssignHideAnchorsIfNeeded();
        foundOrderCounter = 0;

        for (int i = 0; i < guests.Count; i++)
        {
            GuestSearchEntry guest = guests[i];

            if (guest == null)
            {
                continue;
            }

            guest.found = false;
            guest.foundOrder = 0;

            if (guest.actorState == null)
            {
                Debug.LogWarning($"Chapter 2 guest search missing ActorRoomState for guest '{guest.guestId}'.", this);
                continue;
            }

            if (guest.hideAnchor == null)
            {
                Debug.LogWarning($"Chapter 2 guest search missing hide anchor for guest '{guest.guestId}'.", this);
                continue;
            }

            EnsureGuestUsesPersistentActorRoot(guest);

            guest.actorState.enabled = true;
            guest.actorState.PlaceAt(guest.hideAnchor.transform);
            guest.actorState.SetCurrentRoom(GetGuestHideRoomId(guest.hideAnchor));
            guest.actorState.SetAvailableInCurrentChapter(true);
            guest.actorState.SetVisibleByChapterState(true);
            guest.actorState.SetInteractable(true);
            guest.actorState.SetSeated(false);
            guest.actorState.ApplyState();
        }
    }

    public void AutoDiscoverGuestsIfNeeded()
    {
        if (guests == null)
        {
            guests = new List<GuestSearchEntry>();
        }

        if (guests.Count > 0)
        {
            return;
        }

        ActorRoomState[] actorStates = FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include);

        for (int i = 0; i < actorStates.Length; i++)
        {
            ActorRoomState actorState = actorStates[i];

            if (!IsLikelyChapterGuest(actorState))
            {
                continue;
            }

            guests.Add(new GuestSearchEntry
            {
                guestId = actorState.ActorId,
                displayName = actorState.gameObject.name,
                actorState = actorState
            });
        }
    }

    public void AutoAssignHideAnchorsIfNeeded()
    {
        if (guests == null || guests.Count == 0)
        {
            return;
        }

        RoomAnchor[] hideAnchors = FindHideAnchors();
        int anchorIndex = 0;

        for (int i = 0; i < guests.Count; i++)
        {
            GuestSearchEntry guest = guests[i];

            if (guest == null || guest.hideAnchor != null)
            {
                continue;
            }

            if (anchorIndex >= hideAnchors.Length)
            {
                Debug.LogWarning($"Chapter 2 guest search has no hide anchor for guest '{guest.guestId}'.", this);
                continue;
            }

            guest.hideAnchor = hideAnchors[anchorIndex];
            anchorIndex++;
        }
    }

    public bool MarkGuestFound(string guestId)
    {
        GuestSearchEntry guest = FindGuest(guestId);

        if (guest == null)
        {
            return false;
        }

        if (!guest.found)
        {
            foundOrderCounter++;
            guest.found = true;
            guest.foundOrder = foundOrderCounter;
        }

        return true;
    }

    public List<string> GetFoundGuestIdsInOrder()
    {
        List<GuestSearchEntry> foundGuests = GetFoundGuestsInOrder();
        List<string> foundGuestIds = new List<string>(foundGuests.Count);

        for (int i = 0; i < foundGuests.Count; i++)
        {
            string guestId = foundGuests[i].guestId;
            foundGuestIds.Add(string.IsNullOrWhiteSpace(guestId) ? foundGuests[i].displayName : guestId);
        }

        return foundGuestIds;
    }

    public List<ActorRoomState> GetFoundActorsInOrder()
    {
        List<GuestSearchEntry> foundGuests = GetFoundGuestsInOrder();
        List<ActorRoomState> foundActors = new List<ActorRoomState>(foundGuests.Count);

        for (int i = 0; i < foundGuests.Count; i++)
        {
            if (foundGuests[i].actorState != null)
            {
                foundActors.Add(foundGuests[i].actorState);
            }
        }

        return foundActors;
    }

    private void EnsureGuestUsesPersistentActorRoot(GuestSearchEntry guest)
    {
        if (guest == null || guest.actorState == null || guest.actorState.gameObject == null)
        {
            return;
        }

        GameObject actorObject = guest.actorState.gameObject;
        Transform actorTransform = actorObject.transform;

        if (actorTransform.GetComponentInParent<RoomContentGroup>(true) == null)
        {
            return;
        }

        RectTransform rectTransform = actorTransform as RectTransform;

        if (rectTransform != null && actorTransform.GetComponentInParent<Canvas>(true) != null)
        {
            Debug.LogWarning($"Chapter 2 guest search left UI guest '{actorObject.name}' under its Canvas instead of reparenting out of RoomContentGroup.", this);
            return;
        }

        Transform persistentActorRoot = GetOrCreatePersistentActorRoot();
        actorTransform.SetParent(persistentActorRoot, true);
    }

    private Transform GetOrCreatePersistentActorRoot()
    {
        GameObject rootObject = GameObject.Find(PersistentActorRootName);

        if (rootObject == null)
        {
            rootObject = new GameObject(PersistentActorRootName);
        }

        if (rootObject.transform.GetComponentInParent<RoomContentGroup>(true) != null)
        {
            rootObject.transform.SetParent(null, true);
        }

        return rootObject.transform;
    }

    private GuestSearchEntry FindGuest(string guestId)
    {
        if (string.IsNullOrWhiteSpace(guestId) || guests == null)
        {
            return null;
        }

        for (int i = 0; i < guests.Count; i++)
        {
            GuestSearchEntry guest = guests[i];

            if (guest == null)
            {
                continue;
            }

            if (SameId(guest.guestId, guestId) ||
                SameId(guest.displayName, guestId) ||
                (guest.actorState != null && SameId(guest.actorState.ActorId, guestId)) ||
                (guest.actorState != null && SameId(guest.actorState.gameObject.name, guestId)))
            {
                return guest;
            }
        }

        return null;
    }

    private List<GuestSearchEntry> GetFoundGuestsInOrder()
    {
        List<GuestSearchEntry> foundGuests = new List<GuestSearchEntry>();

        if (guests == null)
        {
            return foundGuests;
        }

        for (int i = 0; i < guests.Count; i++)
        {
            if (guests[i] != null && guests[i].found)
            {
                foundGuests.Add(guests[i]);
            }
        }

        foundGuests.Sort(CompareFoundOrder);
        return foundGuests;
    }

    private RoomAnchor[] FindHideAnchors()
    {
        RoomAnchor[] anchors = FindObjectsByType<RoomAnchor>(FindObjectsInactive.Include);
        List<RoomAnchor> hideAnchors = new List<RoomAnchor>();

        for (int i = 0; i < anchors.Length; i++)
        {
            RoomAnchor anchor = anchors[i];

            if (anchor == null)
            {
                continue;
            }

            if (StartsWithPrefix(anchor.name, hideAnchorPrefix) ||
                StartsWithPrefix(anchor.AnchorId, hideAnchorPrefix))
            {
                hideAnchors.Add(anchor);
            }
        }

        hideAnchors.Sort(CompareAnchorName);
        return hideAnchors.ToArray();
    }

    private string GetGuestHideRoomId(RoomAnchor hideAnchor)
    {
        if (!string.IsNullOrWhiteSpace(hideRoomId))
        {
            return hideRoomId.Trim();
        }

        return hideAnchor != null ? hideAnchor.RoomId : string.Empty;
    }

    private static bool IsLikelyChapterGuest(ActorRoomState actorState)
    {
        if (actorState == null)
        {
            return false;
        }

        string actorId = actorState.ActorId;
        string objectName = actorState.gameObject != null ? actorState.gameObject.name : string.Empty;

        if (ContainsAny(actorId, "Player", "Butler", "Monster") ||
            ContainsAny(objectName, "Player", "Butler", "Monster"))
        {
            return false;
        }

        return ContainsAny(actorId, "Guest") || ContainsAny(objectName, "Guest");
    }

    private static int CompareAnchorName(RoomAnchor left, RoomAnchor right)
    {
        string leftName = left != null ? left.name : string.Empty;
        string rightName = right != null ? right.name : string.Empty;
        return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareFoundOrder(GuestSearchEntry left, GuestSearchEntry right)
    {
        int leftOrder = left != null ? left.foundOrder : int.MaxValue;
        int rightOrder = right != null ? right.foundOrder : int.MaxValue;
        return leftOrder.CompareTo(rightOrder);
    }

    private static bool StartsWithPrefix(string value, string prefix)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(prefix) &&
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameId(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string value, params string[] fragments)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (int i = 0; i < fragments.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(fragments[i]) &&
                value.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
