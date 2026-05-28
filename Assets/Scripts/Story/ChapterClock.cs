using UnityEngine;

[DisallowMultipleComponent]
public class ChapterClock : MonoBehaviour
{
    [SerializeField] private bool logClockStateChanges;

    private float elapsedSeconds;
    private bool isRunning;

    public float ElapsedSeconds => elapsedSeconds;
    public bool IsRunning => isRunning;

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        elapsedSeconds += Time.deltaTime;
    }

    public void ResetClock()
    {
        elapsedSeconds = 0f;
    }

    public void StartClock()
    {
        isRunning = true;

        if (logClockStateChanges)
        {
            Debug.Log("Chapter clock started.", this);
        }
    }

    public void StopClock()
    {
        isRunning = false;

        if (logClockStateChanges)
        {
            Debug.Log("Chapter clock stopped.", this);
        }
    }
}
