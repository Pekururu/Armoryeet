using Armoryeet;
using FFXIVClientStructs.FFXIV.Client.Game;

internal static class MovementTests
{
    private static readonly ArmouryItem Head = new(InventoryType.ArmoryHead, 0, 10, "Head");
    private static readonly InventoryLocation Source = new(Head.InventoryType, Head.Slot);
    private static readonly InventoryLocation Destination = new(InventoryType.Inventory1, 0);
    private static readonly InventorySlotSnapshot Item = new(InventorySlotState.Occupied, 10, 1, 10);
    private static readonly InventorySlotSnapshot Empty = new(InventorySlotState.Empty);

    public static void Run()
    {
        var cases = new (string Name, Action Run)[]
        {
            ("character identity and logout", CharacterIdentity),
            ("reviewed capacity validation", ReviewedCapacity),
            ("unavailable and refreshed protection", Protection),
            ("unavailable source and inventory", UnavailableInventory),
            ("verified accepted transfer", ConfirmedTransfer),
            ("rejected move and disappearing source", RejectedTransfer),
            ("bounded rejected retries", RetryLimit),
            ("accepted request is not sent twice", ConfirmationTimeout),
            ("unavailable or mismatched destination", DestinationVerification),
            ("changed slot and soul crystal protection", ProtectedSources),
            ("stop cancels pending work", Stop),
            ("blockers and inter-item delay", Timing),
            ("completion without drawing", HiddenCompletion),
            ("disposed window unsubscribes", DisposedWindow),
            ("review state resets across characters", ResetReview),
            ("native container readiness", NativeReadiness),
            ("review controls follow movement and completion", ReviewPresentation),
        };

        foreach (var test in cases)
        {
            test.Run();
            Console.WriteLine($"PASS: {test.Name}");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CharacterIdentity()
    {
        using var f = new Fixture();
        Check(!f.Mover.TryEnqueue([Head], 0), "An anonymous queue must be rejected.");
        Check(!f.Mover.TryEnqueueReviewed([Head], 2), "Another character's review must be rejected.");
        f.Enqueue();
        Check(!f.Mover.TryEnqueue([Head], 1), "An active queue must not be replaced.");
        f.Game.BlockReason = "InCombat";
        f.Tick();
        f.Game.CharacterId = 2;
        f.Mover.Tick(f.Now.AddMilliseconds(-249));
        Check(f.Mover.Snapshot.State == ItemMoverState.Stopped && f.Game.MoveCalls == 0, "Character change must stop even within the throttle interval.");

        f.Game.CharacterId = 1;
        f.Game.BlockReason = null;
        f.Enqueue();
        f.Game.CharacterId = 0;
        f.Tick();
        f.Game.CharacterId = 1;
        f.Tick();
        Check(f.Mover.Snapshot.State == ItemMoverState.Stopped && f.Game.MoveCalls == 0, "Logging back in must not resume old work.");
    }

    private static void ReviewedCapacity()
    {
        using var f = new Fixture();
        f.Game.CapacityAvailable = false;
        Check(!f.Mover.TryEnqueueReviewed([Head], 1), "Unavailable capacity must not enqueue.");
        f.Game.CapacityAvailable = true;
        f.Game.FreeSlots = 0;
        Check(!f.Mover.TryEnqueueReviewed([Head], 1), "Full inventory must not enqueue reviewed moves.");
        f.Game.FreeSlots = 1;
        Check(f.Mover.TryEnqueueReviewed([Head], 1), "A valid reviewed queue must be accepted.");
        f.Game.FreeSlots = 0;
        f.Tick();
        Check(f.Mover.Snapshot.State == ItemMoverState.Failed && f.Game.MoveCalls == 0, "Capacity must be rechecked before moving.");
    }

    private static void Protection()
    {
        using var f = new Fixture();
        f.Enqueue();
        f.Game.ProtectionAvailable = false;
        f.Tick();
        var paused = f.Mover.Snapshot;
        f.Tick(20);
        Check(ReferenceEquals(paused, f.Mover.Snapshot), "Unchanged pauses must not republish snapshots.");
        Check(f.Game.MoveCalls == 0 && paused.ProcessedCount == 0, "Missing protection must neither move nor skip.");
        f.Game.ProtectionAvailable = true;
        f.Game.ProtectedItems.Add(Head.ItemId);
        f.Tick();
        Check(f.Mover.Snapshot.SkipDetails.Single().Reason == ItemSkipReason.GearsetProtected && f.Game.MoveCalls == 0, "Protection must be refreshed after a pause.");
    }

    private static void UnavailableInventory()
    {
        using var f = new Fixture();
        f.Enqueue();
        f.Game.Slots[Source] = default;
        f.Tick(12);
        Check(f.Mover.Snapshot.State == ItemMoverState.Paused && f.Mover.Snapshot.ProcessedCount == 0, "Unreadable source must not count as empty.");
        f.Game.Slots[Source] = Item;
        f.Game.CapacityAvailable = false;
        f.Tick();
        Check(f.Game.MoveCalls == 0 && f.Mover.Snapshot.State == ItemMoverState.Paused, "Unloaded bags must pause.");
        f.Game.CapacityAvailable = true;
        f.Tick();
        Check(f.Game.MoveCalls == 1, "Movement must resume when data becomes available.");
    }

    private static void ConfirmedTransfer()
    {
        using var f = new Fixture();
        f.Enqueue();
        f.Tick();
        Check(f.Mover.MovedCount == 0, "Accepted call is not completion.");
        f.Game.Slots[Source] = default;
        f.Game.Slots[Destination] = Item;
        f.Tick();
        Check(f.Mover.MovedCount == 0, "A matching destination cannot prove an unreadable source is empty.");
        f.Game.Slots[Source] = Empty;
        f.Tick();
        Check(f.Mover.Snapshot.State == ItemMoverState.Completed && f.Mover.MovedCount == 1, "Both sides must confirm the transfer.");
    }

    private static void RejectedTransfer()
    {
        using var f = new Fixture();
        f.Game.AcceptMove = false;
        f.Enqueue();
        f.Tick();
        f.Game.Transfer();
        f.Tick();
        Check(f.Mover.MovedCount == 0 && f.Mover.Snapshot.SkipDetails.Single().Reason == ItemSkipReason.ArmourySlotEmpty, "A rejected call must not claim a later disappearance as success.");
    }

    private static void RetryLimit()
    {
        using var f = new Fixture();
        f.Game.AcceptMove = false;
        f.Enqueue();
        f.Tick(30);
        Check(f.Game.MoveCalls == 10 && f.Mover.Snapshot.SkipDetails.Single().Reason == ItemSkipReason.RetryLimitReached, "Rejected attempts must stop at ten.");
    }

    private static void ConfirmationTimeout()
    {
        using var f = new Fixture();
        f.Enqueue();
        f.Tick(30);
        Check(f.Game.MoveCalls == 1 && f.Mover.Snapshot.SkipDetails.Single().Reason == ItemSkipReason.MoveUnconfirmed, "An accepted request must be observed with a bound, never resent to another slot.");
    }

    private static void DestinationVerification()
    {
        using var f = new Fixture();
        f.Game.Slots[Source] = Item with { ItemId = 1_000_010 };
        f.Enqueue();
        f.Tick();
        f.Game.Slots[Source] = Empty;
        f.Game.Slots[Destination] = default;
        f.Tick(20);
        Check(f.Mover.MovedCount == 0 && f.Mover.SkippedCount == 0, "Unavailable destination must pause without consuming confirmation attempts.");
        f.Game.Slots[Destination] = Item;
        f.Tick();
        Check(f.Mover.MovedCount == 0 && f.Mover.Snapshot.SkipDetails.Single().Reason == ItemSkipReason.MoveUnconfirmed, "NQ gear cannot confirm an HQ transfer.");
    }

    private static void ProtectedSources()
    {
        using var f = new Fixture();
        f.Enqueue();
        f.Game.Slots[Source] = Item with { ItemId = 11, BaseItemId = 11 };
        f.Tick();
        Check(f.Mover.Snapshot.SkipDetails.Single().Reason == ItemSkipReason.ArmourySlotChanged, "Changed source must be skipped.");
        Check(f.Mover.TryEnqueue([Head with { InventoryType = InventoryType.ArmorySoulCrystal }], 1), "Queue should accept the test crystal for final safety checking.");
        f.Tick();
        Check(f.Mover.Snapshot.SkipDetails.Single().Reason == ItemSkipReason.SoulCrystalProtected && f.Game.MoveCalls == 0, "Soul crystals must never move.");
    }

    private static void Stop()
    {
        using var f = new Fixture();
        f.Enqueue();
        f.Tick();
        f.Mover.Stop();
        f.Game.Transfer();
        f.Tick(20);
        Check(f.Mover.Snapshot.State == ItemMoverState.Stopped && f.Mover.MovedCount == 0 && f.Game.MoveCalls == 1, "Stop must discard pending confirmation and remaining work.");
    }

    private static void Timing()
    {
        using var f = new Fixture();
        var second = Head with { Slot = 1 };
        f.Game.Slots[new(second.InventoryType, second.Slot)] = Item;
        f.Configuration.DelayBetweenItemsMs = 1000;
        Check(f.Mover.TryEnqueue([Head, second], 1), "Queue failed.");
        f.Game.BlockReason = "InCombat";
        f.Tick(5);
        Check(f.Game.MoveCalls == 0, "Combat must block movement.");
        f.Game.BlockReason = null;
        f.Tick();
        f.Game.Transfer();
        f.Tick();
        f.Game.FirstEmpty = new(InventoryType.Inventory1, 1);
        f.Game.Slots[f.Game.FirstEmpty] = Empty;
        f.Tick(3);
        Check(f.Game.MoveCalls == 1, "Inter-item delay must be respected.");
        f.Tick();
        Check(f.Game.MoveCalls == 2, "Next item should start after the configured delay.");
    }

    private static PluginUI CreateWindow(Fixture f)
    {
        var window = new PluginUI(f.Configuration, new GearsetScanner(), new ArmouryScanner(null!), f.Mover, null!, () => { });
        window.Session.Replace([Head], 0, 20, 1);
        return window;
    }

    private static void HiddenCompletion()
    {
        using var f = new Fixture();
        f.Configuration.CloseAfterMove = true;
        using var window = CreateWindow(f);
        window.IsOpen = false;
        f.Enqueue();
        f.Tick();
        f.Game.Transfer();
        f.Tick();
        Check(window.Session.Items.Count == 0, "Closed window must reconcile without Draw.");
        window.IsOpen = true;
        f.Tick();
        Check(window.IsOpen, "Completed work must not close a subsequently reopened window.");

        window.Session.Replace([Head], 0, 20, 1);
        f.Game.Slots[Source] = Item;
        f.Game.Slots[Destination] = Empty;
        f.Enqueue();
        f.Tick();
        f.Game.Transfer();
        f.Tick();
        Check(!window.IsOpen, "An open window must close immediately when configured.");
    }

    private static void DisposedWindow()
    {
        using var f = new Fixture();
        var window = CreateWindow(f);
        window.Dispose();
        f.Enqueue();
        f.Tick();
        f.Game.Transfer();
        f.Tick();
        Check(window.Session.Items.Count == 1, "Disposed window must not receive movement callbacks.");
    }

    private static void ResetReview()
    {
        using var f = new Fixture();
        using var window = CreateWindow(f);
        var staleItems = window.Session.SelectedSnapshot();
        var staleCharacter = window.Session.CharacterId;
        window.ResetForCharacter();
        Check(!window.Session.HasScanned && window.Session.SelectedCount == 0 && window.Session.CharacterId == 0, "Reset must discard reviewed state.");
        f.Game.CharacterId = 2;
        Check(!f.Mover.TryEnqueueReviewed(staleItems, staleCharacter), "Captured confirmation from a previous character must be rejected.");
    }

    private static void ReviewPresentation()
    {
        using var f = new Fixture();
        using var window = CreateWindow(f);
        Check(window.HasReviewItems && window.ShowMoveActions && !window.ShowCompletion, "A populated scan must expose review controls.");
        window.Session.ClearAll();
        Check(window.HasReviewItems && window.ShowMoveActions, "Clearing selection must retain controls for selecting items again.");
        window.Session.SelectAll();
        f.Enqueue();
        window.Session.Replace([], 0, 20, 1);
        Check(!window.HasReviewItems && window.ShowMoveActions, "An active queue must keep Stop available even with an empty review.");
        f.Tick();
        f.Game.Transfer();
        f.Tick();
        Check(window.ShowCompletion && !window.HasReviewItems && !window.ShowMoveActions, "Empty completion must show its summary without selection or Move controls.");
        window.ResetForCharacter();
        Check(!window.ShowCompletion && !window.ShowMoveActions, "Character reset must clear completion presentation.");

        using var skipped = new Fixture();
        using var skippedWindow = CreateWindow(skipped);
        skipped.Game.ProtectedItems.Add(Head.ItemId);
        skipped.Enqueue();
        skipped.Tick();
        Check(skippedWindow.ShowCompletion && skippedWindow.HasReviewItems && skippedWindow.ShowMoveActions,
            "Skipped rows must retain review controls alongside the completion summary.");
    }

    private static unsafe void NativeReadiness()
    {
        Check(InventoryItemReader.Read(null).State == InventorySlotState.Unavailable, "Null native slots must not be empty.");
        Check(!InventoryItemReader.IsReady(null), "Null containers must not be ready.");
        InventoryItem item = default;
        var container = new InventoryContainer { Size = 1, Items = &item, IsLoaded = false };
        Check(!InventoryItemReader.IsReady(&container), "Allocated but unloaded inventory must not be read.");
        container.IsLoaded = true;
        Check(InventoryItemReader.IsReady(&container), "Loaded inventory with storage must be readable.");
        container.Items = null;
        Check(!InventoryItemReader.IsReady(&container), "Loaded flag alone must not permit missing storage.");
    }

    private sealed class Fixture : IDisposable
    {
        public readonly FakeGame Game = new();
        public readonly Configuration Configuration = new();
        public readonly ItemMover Mover;
        public DateTimeOffset Now { get; private set; } = DateTimeOffset.UnixEpoch;

        public Fixture() => this.Mover = new ItemMover(this.Game, this.Configuration);
        public void Enqueue() => Check(this.Mover.TryEnqueue([Head], 1), "Queue failed.");
        public void Tick(int count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                this.Mover.Tick(this.Now);
                this.Now = this.Now.AddMilliseconds(250);
            }
        }

        public void Dispose() => this.Mover.Dispose();
    }

    private sealed class FakeGame : IItemMoveGame
    {
        public ulong CharacterId { get; set; } = 1;
        public string? BlockReason;
        public string? MoveBlockReason => this.BlockReason;
        public bool ProtectionAvailable = true;
        public bool CapacityAvailable = true;
        public bool AcceptMove = true;
        public int FreeSlots = 20;
        public int MoveCalls;
        public InventoryLocation FirstEmpty = Destination;
        public readonly HashSet<uint> ProtectedItems = [];
        public readonly Dictionary<InventoryLocation, InventorySlotSnapshot> Slots = new() { [Source] = Item, [Destination] = Empty };
        public bool TryGetProtectedItems(out HashSet<uint> items) { items = this.ProtectedItems; return this.ProtectionAvailable; }
        public InventorySlotSnapshot ReadSlot(InventoryLocation location) => this.Slots.GetValueOrDefault(location);
        public bool TryGetCapacity(out int freeSlots, out InventoryLocation firstEmpty)
        {
            freeSlots = this.FreeSlots;
            firstEmpty = this.FirstEmpty;
            return this.CapacityAvailable;
        }

        public bool Move(InventoryLocation source, InventoryLocation destination)
        {
            Check(this.ReadSlot(source).State == InventorySlotState.Occupied && this.ReadSlot(destination).State == InventorySlotState.Empty, "Only occupied-to-empty transfers are allowed.");
            this.MoveCalls++;
            return this.AcceptMove;
        }

        public void Transfer() { this.Slots[Destination] = this.Slots[Source]; this.Slots[Source] = Empty; }
    }
}
