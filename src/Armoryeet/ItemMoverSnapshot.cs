using System;
using System.Collections.Generic;

namespace Armoryeet;

public enum ItemMoverState { Idle, Moving, Paused, Completed, Stopped, Failed }
public enum ItemSkipReason { SoulCrystalProtected, ArmourySlotEmpty, ArmourySlotChanged, GearsetProtected, RetryLimitReached }
public sealed record ItemSkip(ArmouryItem Item, ItemSkipReason Reason);

public sealed record ItemMoverSnapshot(
    ItemMoverState State,
    string PausedReason,
    int MovedCount,
    int SkippedCount,
    int TotalCount,
    IReadOnlyList<ArmouryItem> CompletedItems,
    IReadOnlyList<ArmouryItem> SkippedItems,
    IReadOnlyList<ItemSkip> SkipDetails)
{
    public int ProcessedCount => this.MovedCount + this.SkippedCount;
    public float Progress => this.TotalCount == 0 ? 0 : Math.Clamp((float)this.ProcessedCount / this.TotalCount, 0, 1);
    public bool IsActive => this.State is ItemMoverState.Moving or ItemMoverState.Paused;
}
