using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Armoryeet;

public sealed class ScanSession
{
    private readonly List<ArmouryItem> items = [];
    private readonly HashSet<ArmouryItem> selected = [];

    public bool HasScanned { get; private set; }
    public int ProtectedCount { get; private set; }
    public int FreeInventorySlots { get; private set; }
    public IReadOnlyList<ArmouryItem> Items => new ReadOnlyCollection<ArmouryItem>(this.items);
    public int SelectedCount => this.selected.Count;

    public IEnumerable<(InventoryType Type, IReadOnlyList<ArmouryItem> Items)> Groups =>
        ArmouryScanner.ArmouryTypes
            .Select(type => (type, (IReadOnlyList<ArmouryItem>)this.items.Where(item => item.InventoryType == type).OrderBy(item => item.Slot).ToArray()))
            .Where(group => group.Item2.Count != 0);

    public void Replace(IEnumerable<ArmouryItem> results, int protectedCount, int freeInventorySlots)
    {
        this.items.Clear();
        this.items.AddRange(results.OrderBy(item => Array.IndexOf(ArmouryScanner.ArmouryTypes, item.InventoryType)).ThenBy(item => item.Slot));
        this.selected.Clear();
        this.selected.UnionWith(this.items);
        this.ProtectedCount = protectedCount;
        this.FreeInventorySlots = Math.Max(0, freeInventorySlots);
        this.HasScanned = true;
    }

    public void RefreshCapacity(int freeInventorySlots) => this.FreeInventorySlots = Math.Max(0, freeInventorySlots);
    public bool IsSelected(ArmouryItem item) => this.selected.Contains(item);
    public void Toggle(ArmouryItem item) { if (!this.selected.Remove(item)) this.selected.Add(item); }
    public void SelectAll() => this.selected.UnionWith(this.items);
    public void ClearAll() => this.selected.Clear();

    public void ToggleGroup(InventoryType type)
    {
        var group = this.items.Where(item => item.InventoryType == type).ToArray();
        if (group.Length == 0) return;
        if (group.All(this.selected.Contains)) this.selected.ExceptWith(group);
        else this.selected.UnionWith(group);
    }

    public IReadOnlyList<ArmouryItem> SelectedSnapshot() => Array.AsReadOnly(this.items.Where(this.selected.Contains).ToArray());

    public void Reconcile(ItemMoverSnapshot snapshot)
    {
        if (snapshot.CompletedItems.Count == 0) return;
        this.items.RemoveAll(snapshot.CompletedItems.Contains);
        this.selected.RemoveWhere(snapshot.CompletedItems.Contains);
        foreach (var skipped in snapshot.SkippedItems.Where(this.items.Contains)) this.selected.Add(skipped);
    }
}
