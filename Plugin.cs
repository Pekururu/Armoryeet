using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Armoryeet;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/armoryeet";
    private const string BritishCommandName = "/armouryeet";
    private const string YeetCommandName = "/yeet";
    private const string YeetSettingsCommandName = "/yeetsettings";

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IFramework Framework { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static ICondition Condition { get; private set; } = null!;

    [PluginService]
    internal static IDataManager DataManager { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    [PluginService]
    internal static IChatGui ChatGui { get; private set; } = null!;

    private readonly GearsetScanner gearsetScanner;
    private readonly ArmouryScanner armouryScanner;
    private readonly ItemMover itemMover;
    private readonly PluginUI mainWindow;
    private readonly ConfigUI configWindow;

    public Plugin()
    {
        this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Configuration.Initialize(PluginInterface);

        this.gearsetScanner = new GearsetScanner();
        this.armouryScanner = new ArmouryScanner();
        this.itemMover = new ItemMover(Framework, ClientState, Condition, Log, this.gearsetScanner, this.Configuration);
        this.mainWindow = new PluginUI(
            this.Configuration,
            this.gearsetScanner,
            this.armouryScanner,
            this.itemMover,
            DataManager,
            this.Configuration.Save,
            this.OpenConfigUi);
        this.configWindow = new ConfigUI(this.Configuration, this.Configuration.Save);

        this.WindowSystem.AddWindow(this.mainWindow);
        this.WindowSystem.AddWindow(this.configWindow);
        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open Armoryeet.",
        });
        CommandManager.AddHandler(BritishCommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open Armoryeet.",
        });
        CommandManager.AddHandler(YeetCommandName, new CommandInfo(this.OnYeetCommand)
        {
            HelpMessage = "Open Armoryeet. Use /yeet help for actions.",
        });
        CommandManager.AddHandler(YeetSettingsCommandName, new CommandInfo(this.OnSettingsCommand)
        {
            HelpMessage = "Open settings.",
        });

        PluginInterface.UiBuilder.Draw += this.WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += this.ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += this.ToggleConfigUi;
        ClientState.Login += this.OnLogin;

        if (this.Configuration.ScanOnLogin && ClientState.IsLoggedIn)
            this.mainWindow.Scan();
    }

    public Configuration Configuration { get; }

    public WindowSystem WindowSystem { get; } = new("Armoryeet");

    public void Dispose()
    {
        ClientState.Login -= this.OnLogin;
        PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= this.ToggleMainUi;
        PluginInterface.UiBuilder.Draw -= this.WindowSystem.Draw;
        CommandManager.RemoveHandler(YeetSettingsCommandName);
        CommandManager.RemoveHandler(YeetCommandName);
        CommandManager.RemoveHandler(BritishCommandName);
        CommandManager.RemoveHandler(CommandName);
        this.WindowSystem.RemoveAllWindows();
        this.configWindow.Dispose();
        this.mainWindow.Dispose();
        this.itemMover.Dispose();
    }

    private void OnCommand(string command, string arguments)
    {
        this.ToggleMainUi();
    }

    private void OnSettingsCommand(string command, string arguments)
    {
        this.ToggleConfigUi();
    }

    private void OnYeetCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "":
                this.ToggleMainUi();
                break;
            case "scan":
                this.ScanAndOpen(ArmouryScanner.GetEnabledArmouryTypes(this.Configuration));
                break;
            case "chest":
                this.ScanAndMove(ArmouryScanner.GetEnabledArmouryTypes(this.Configuration), "enabled armoury categories");
                break;
            case "weapons":
                this.ScanAndMove(ArmouryScanner.WeaponTypes, "weapons");
                break;
            case "mainhand":
            case "main":
            case "mhand":
            case "mh":
                this.ScanAndMove([InventoryType.ArmoryMainHand], "main hand");
                break;
            case "offhand":
            case "off":
            case "ohand":
            case "oh":
                this.ScanAndMove([InventoryType.ArmoryOffHand], "off hand");
                break;
            case "armor":
            case "armour":
                this.ScanAndMove(ArmouryScanner.ArmorTypes, "armor");
                break;
            case "head":
                this.ScanAndMove([InventoryType.ArmoryHead], "head");
                break;
            case "body":
                this.ScanAndMove([InventoryType.ArmoryBody], "body");
                break;
            case "hands":
            case "gloves":
                this.ScanAndMove([InventoryType.ArmoryHands], "hands");
                break;
            case "waist":
            case "belt":
                this.ScanAndMove([InventoryType.ArmoryWaist], "waist");
                break;
            case "legs":
                this.ScanAndMove([InventoryType.ArmoryLegs], "legs");
                break;
            case "feet":
            case "boots":
                this.ScanAndMove([InventoryType.ArmoryFeets], "feet");
                break;
            case "accessories":
            case "accessory":
                this.ScanAndMove(ArmouryScanner.AccessoryTypes, "accessories");
                break;
            case "ears":
            case "ear":
            case "earrings":
                this.ScanAndMove([InventoryType.ArmoryEar], "ears");
                break;
            case "neck":
            case "necklace":
                this.ScanAndMove([InventoryType.ArmoryNeck], "neck");
                break;
            case "wrist":
            case "wrists":
            case "bracelet":
            case "bracelets":
                this.ScanAndMove([InventoryType.ArmoryWrist], "wrist");
                break;
            case "rings":
            case "ring":
                this.ScanAndMove([InventoryType.ArmoryRings], "rings");
                break;
            case "settings":
            case "config":
                this.ToggleConfigUi();
                break;
            case "stop":
                this.itemMover.Clear();
                this.Print("Move queue stopped.");
                break;
            case "status":
                this.PrintStatus();
                break;
            case "help":
                this.PrintHelp();
                break;
            default:
                this.Print($"Unknown command: /yeet {arguments.Trim()}. Use /yeet help.");
                break;
        }
    }

    private void ToggleMainUi()
    {
        this.mainWindow.Toggle();
    }

    private void ToggleConfigUi()
    {
        this.configWindow.Toggle();
    }

    private void OpenConfigUi()
    {
        this.configWindow.IsOpen = true;
    }

    private void OnLogin()
    {
        if (this.Configuration.ScanOnLogin)
            this.mainWindow.Scan();
    }

    private void ScanAndOpen(IEnumerable<InventoryType> inventoryTypes)
    {
        var items = this.Scan(inventoryTypes);
        this.mainWindow.IsOpen = true;
        this.Print($"Scan found {items.Count} item(s).");
    }

    private void ScanAndMove(IEnumerable<InventoryType> inventoryTypes, string label)
    {
        if (this.itemMover.IsMoving)
        {
            this.Print("Already moving. Use /yeet stop first.");
            return;
        }

        var items = this.Scan(inventoryTypes);
        if (items.Count == 0)
        {
            this.Print($"No unregistered {label} item(s) found.");
            return;
        }

        this.itemMover.Enqueue(items);
        this.mainWindow.IsOpen = true;
        this.Print($"Moving {items.Count} unregistered {label} item(s) to inventory.");
    }

    private List<ArmouryItem> Scan(IEnumerable<InventoryType> inventoryTypes)
    {
        var protectedIds = this.gearsetScanner.BuildGearsetItemIds();
        var items = this.armouryScanner.FindOrphans(protectedIds, inventoryTypes);
        this.mainWindow.SetScanResults(items, protectedIds.Count);
        return items;
    }

    private void PrintStatus()
    {
        if (this.itemMover.IsMoving)
        {
            var completed = this.itemMover.MovedCount + this.itemMover.SkippedCount;
            this.Print($"Moving {completed}/{this.itemMover.TotalCount}. Moved {this.itemMover.MovedCount}, skipped {this.itemMover.SkippedCount}.");
            return;
        }

        if (this.mainWindow.PreviewItems.Count == 0)
        {
            this.Print("Idle. No scan results.");
            return;
        }

        var groupCount = this.mainWindow.PreviewItems.Select(item => item.InventoryType).Distinct().Count();
        this.Print($"Idle. Last scan found {this.mainWindow.PreviewItems.Count} item(s) in {groupCount} group(s). {this.mainWindow.ProtectedItemCount} protected.");
    }

    private void PrintHelp()
    {
        this.Print("Commands: /yeet, /yeet scan, /yeet chest, /yeet weapons, /yeet armor, /yeet accessories, /yeet mainhand, /yeet offhand, /yeet head, /yeet body, /yeet hands, /yeet waist, /yeet legs, /yeet feet, /yeet ears, /yeet neck, /yeet wrist, /yeet rings, /yeet settings, /yeet stop, /yeet status.");
    }

    private void Print(string message)
    {
        ChatGui.Print($"[Armoryeet] {message}");
    }
}
