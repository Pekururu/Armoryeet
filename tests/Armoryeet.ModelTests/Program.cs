using Armoryeet;
using FFXIVClientStructs.FFXIV.Client.Game;

static void Assert(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

var head = new ArmouryItem(InventoryType.ArmoryHead, 2, 10, "Head", 1);
var head2 = new ArmouryItem(InventoryType.ArmoryHead, 1, 11, "Head 2", 2);
var ring = new ArmouryItem(InventoryType.ArmoryRings, 0, 12, "Ring", 3);
var session = new ScanSession();
session.Replace([ring, head, head2], 7, 20);

Assert(session.HasScanned && session.SelectedCount == 3, "A scan must select every result by default.");
Assert(session.Groups.Select(group => group.Type).SequenceEqual([InventoryType.ArmoryHead, InventoryType.ArmoryRings]), "Groups must omit empties and use canonical order.");
Assert(session.Groups.First().Items.SequenceEqual([head2, head]), "Items must use armoury slot order.");

session.Toggle(head);
Assert(session.SelectedCount == 2, "Item toggle must clear a selected item.");
session.ToggleGroup(InventoryType.ArmoryHead);
Assert(session.IsSelected(head) && session.IsSelected(head2), "A partial group toggle must select the full group.");
session.ToggleGroup(InventoryType.ArmoryHead);
Assert(!session.IsSelected(head) && !session.IsSelected(head2) && session.IsSelected(ring), "A fully selected group toggle must clear the group.");
session.ClearAll(); Assert(session.SelectedCount == 0, "Clear All failed.");
session.SelectAll(); Assert(session.SelectedCount == 3, "Select All failed.");

var snapshot = session.SelectedSnapshot();
session.ClearAll();
Assert(snapshot.Count == 3, "Selected snapshots must be immutable copies.");
session.Replace([head], 2, 4);
Assert(session.Items.Count == 1 && session.SelectedCount == 1 && session.ProtectedCount == 2 && session.FreeInventorySlots == 4, "Rescan must replace state and select results.");

var completed = new ItemMoverSnapshot(ItemMoverState.Completed, string.Empty, 1, 0, 1, Array.AsReadOnly(new[] { head }), Array.Empty<ArmouryItem>(), Array.Empty<ItemSkip>());
session.Reconcile(completed);
Assert(session.Items.Count == 0, "Completed rows must be removed.");

var movement = new ItemMoverSnapshot(ItemMoverState.Paused, "InCombat", 1, 1, 4, Array.Empty<ArmouryItem>(), Array.Empty<ArmouryItem>(), [new ItemSkip(ring, ItemSkipReason.GearsetProtected)]);
Assert(movement.ProcessedCount == 2 && Math.Abs(movement.Progress - .5f) < .001f && movement.IsActive, "Movement snapshot calculations failed.");
Assert(movement.SkipDetails[0].Reason == ItemSkipReason.GearsetProtected, "Skip details must preserve item-specific reasons.");

Console.WriteLine("Armoryeet model tests passed.");
