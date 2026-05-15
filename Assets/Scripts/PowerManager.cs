using System;
using System.Collections.Generic;
using UnityEngine;

public class PowerManager : MonoBehaviour
{
    [Header("Power")]
    [SerializeField] private float startingValue = 100f;
    [SerializeField] private float baseDrainRate = 0.15f;
    [SerializeField] private float drainSpeedMultiplier = 2f;
    [SerializeField] private bool startRunningOnAwake;

    [Header("Runtime")]
    [SerializeField] private float currentPower;
    [SerializeField] private int currentPowerInteger;

    public event Action OnPowerOut;
    public event Action<int> OnPowerChanged;
    public event Action<int> OnUsageChanged;

    private readonly Dictionary<string, float> activeRates = new Dictionary<string, float>();
    private bool isRunning;
    private bool powerOutageRaised;

    public float CurrentPower => currentPower;
    public int CurrentPowerInteger => currentPowerInteger;
    public bool IsRunning => isRunning;
    public bool IsPowerOut => powerOutageRaised;

    private void Awake()
    {
        ResetPower(false);
        isRunning = startRunningOnAwake;
    }

    private void Start()
    {
        PublishPowerChanged();
        PublishUsageChanged();
    }

    private void Update()
    {
        if (!isRunning || powerOutageRaised)
        {
            return;
        }

        float powerDrawRate = baseDrainRate;

        foreach (float drainRate in activeRates.Values)
        {
            powerDrawRate += Mathf.Max(0f, drainRate);
        }

        currentPower = Mathf.Max(0f, currentPower - powerDrawRate * Mathf.Max(0f, drainSpeedMultiplier) * Time.deltaTime);
        CheckPowerIntChange();

        if (!powerOutageRaised && currentPower <= 0f)
        {
            RaisePowerOut();
        }
    }

    public void StartPowerDrain()
    {
        if (powerOutageRaised)
        {
            return;
        }

        isRunning = true;
        PublishUsageChanged();
    }

    public void StopPowerDrain()
    {
        isRunning = false;
        PublishUsageChanged();
    }

    public void AddDraw(string id, float drainPerSecond)
    {
        if (string.IsNullOrEmpty(id) || powerOutageRaised)
        {
            return;
        }

        activeRates[id] = Mathf.Max(0f, drainPerSecond);
        PublishUsageChanged();
    }

    public void AddDraw()
    {
        AddDraw("Default", baseDrainRate);
    }

    public void RemoveDraw(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        if (activeRates.Remove(id))
        {
            PublishUsageChanged();
        }
    }

    public void RemoveDraw()
    {
        RemoveDraw("Default");
    }

    public void ResetPower()
    {
        ResetPower(true);
    }

    public void Reset()
    {
        ResetPower(true);
    }

    public void ResetPower(bool startRunning)
    {
        activeRates.Clear();
        powerOutageRaised = false;
        currentPower = Mathf.Clamp(startingValue, 0f, 100f);
        currentPowerInteger = Mathf.RoundToInt(currentPower);
        isRunning = startRunning;
        PublishPowerChanged();
        PublishUsageChanged();
    }

    public void ForcePowerOut()
    {
        currentPower = 0f;
        CheckPowerIntChange();
        RaisePowerOut();
    }

    public int GetUsage()
    {
        if (!isRunning || powerOutageRaised)
        {
            return 0;
        }

        return Mathf.Clamp(1 + activeRates.Count, 1, 5);
    }

    private void CheckPowerIntChange()
    {
        int rounded = Mathf.RoundToInt(currentPower);

        if (rounded == currentPowerInteger)
        {
            return;
        }

        currentPowerInteger = rounded;
        PublishPowerChanged();
    }

    private void RaisePowerOut()
    {
        if (powerOutageRaised)
        {
            return;
        }

        powerOutageRaised = true;
        isRunning = false;
        activeRates.Clear();
        PublishPowerChanged();
        PublishUsageChanged();
        OnPowerOut?.Invoke();
    }

    private void PublishPowerChanged()
    {
        OnPowerChanged?.Invoke(currentPowerInteger);
    }

    private void PublishUsageChanged()
    {
        OnUsageChanged?.Invoke(GetUsage());
    }
}
