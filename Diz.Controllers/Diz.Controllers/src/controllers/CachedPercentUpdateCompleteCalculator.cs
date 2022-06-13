#nullable enable
using System;
using System.ComponentModel;
using System.Data;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Cpu._65816;

namespace Diz.Controllers.controllers;

public interface IPercentDisassembledCalculator : INotifyPropertyChanged
{
    void StartCooldown(int ticksLeft);
    void Tick();

    public record PercentageReachedStatus(
        bool FinishedCalculating = false, 
        int Reached = 0, 
        int Size = 0
    );

    PercentageReachedStatus PercentageStatus { get; }
}

public static class PercentageDisassembledExtensions
{
    public static string GetText(this IPercentDisassembledCalculator.PercentageReachedStatus @this)
    {
        var (finishedCalculating, reached, size) = @this;
        
        var reCalcMsg = finishedCalculating ? "[recalculating...]" : "";
        return reached == -1 
            ? reCalcMsg 
            : $"{reached * 100.0 / size:N3}% ({reached:D}/{size:D}) {reCalcMsg}";
    }
}
    
// the point of this timer is to throttle the ROM% calculator
// since it is an expensive calculation. letting it happen attached to UI events
// would significantly slow the user down.
//
// TODO: this is the kind of thing that Rx.net's Throttle function, or 
// an async task would handle much better. For now, this is fine.
public class CachedPercentCompleteCalculator : IPercentDisassembledCalculator
{
    public IPercentDisassembledCalculator.PercentageReachedStatus PercentageStatus
    {
        get => percentageStatus;
        private set => this.SetField(PropertyChanged, ref percentageStatus, value);
    }
    private IPercentDisassembledCalculator.PercentageReachedStatus percentageStatus;

    private readonly ISnesData? snesData;

    private int cooldownTicksRemaining;

    public CachedPercentCompleteCalculator(ISnesData snesData)
    {
        this.snesData = snesData;
        percentageStatus = new IPercentDisassembledCalculator.PercentageReachedStatus();
    }

    public void StartCooldown(int ticksLeft)
    {
        cooldownTicksRemaining = ticksLeft;
        UpdateStatus();
    }

    public void Tick()
    {
        --cooldownTicksRemaining;
        if (cooldownTicksRemaining < 0)
            cooldownTicksRemaining = 0;

        UpdateStatus();
    }
    
    public void UpdateStatus(bool forceRefresh = false) 
    {
        if (snesData?.Data == null)
            return;

        var shouldDoIt = forceRefresh || cooldownTicksRemaining == 0 || PercentageStatus.FinishedCalculating;
        if (!shouldDoIt)
            return;
        
        var size = snesData?.GetRomSize();
        if (size <= 0)
            return;
        
        // entire point: this is crazy expensive, cache as often as we can
        var reached = snesData?.CalculateTotalBytesReached() ?? -1;

        PercentageStatus = new IPercentDisassembledCalculator.PercentageReachedStatus(true, reached, size.Value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
