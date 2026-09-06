using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace CustomHudProbeSW2;

[PluginMetadata(
    Id = "CustomHudProbeSW2",
    Version = "0.5.0",
    Name = "Custom HUD Probe",
    Author = "Swift Menu PoC",
    Description = "Loads one of several CS2 custom_hud_layout resources into a single probe entity.",
    MinimumAPIVersion = "1.4.8"
)]
public sealed class CustomHudProbeSW2(ISwiftlyCore core) : BasePlugin(core)
{
    private const string MenuLayoutResource = "panorama/layout/custom_game/swift_menu_custom_hud.xml";
    private const string CardLayoutResource = "panorama/layout/custom_game/cyber_card_custom_hud.xml";
    private const string GalleryLayoutResource = "panorama/layout/custom_game/hover3d_gallery_custom_hud.xml";
    private const string FlipLayoutResource = "panorama/layout/custom_game/flip_card_custom_hud.xml";
    private const string MenuDialogPanelId = "dialog";
    private const string CardDialogPanelId = "card_dialog";
    private const string GalleryDialogPanelId = "gallery_dialog";
    private const string FlipDialogPanelId = "flip_card_dialog";
    private const string HiddenClass = "SwiftHudHidden";
    private const string AccentClass = "SwiftHudAccent";

    private readonly HashSet<int> _inputCapturedSlots = [];

    private CCSCustomHudLayout? _layoutEntity;
    private HudMode _activeMode;

    private ILogger<CustomHudProbeSW2> Logger => Core.LoggerFactory.CreateLogger<CustomHudProbeSW2>();

    public override void Load(bool hotReload)
    {
        Core.Event.OnCustomHudClicked += OnCustomHudClicked;
        Core.Event.OnClientDisconnected += OnClientDisconnected;
        Logger.LogInformation(
            "[CustomHudProbeSW2] SwiftlyS2 Custom HUD API ready (hotReload={HotReload}). Use !chud_spawn <menu|card|gallery|flip>.",
            hotReload);
    }

    public override void Unload()
    {
        Core.Event.OnCustomHudClicked -= OnCustomHudClicked;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;
        ClearLayout("plugin unload");
        Logger.LogInformation("[CustomHudProbeSW2] Unloaded.");
    }

    [Command("chud_spawn", registerRaw: true, helpText: "Load one Custom HUD layout: chud_spawn <menu|card|gallery|flip>.")]
    public void SpawnCommand(ICommandContext context)
    {
        var modeText = context.Args.Length > 0 ? context.Args[0] : "menu";
        if (!TryParseMode(modeText, out var requestedMode))
        {
            context.Reply("[CustomHudProbeSW2] Usage: !chud_spawn <menu|card|gallery|flip>.");
            return;
        }

        if (_layoutEntity is { IsValid: true } && _activeMode == requestedMode)
        {
            var reopened = OpenHudForConnectedPlayers();
            context.Reply($"[CustomHudProbeSW2] {ModeName(requestedMode)} layout already active; reopened {reopened} HUD(s).");
            return;
        }

        if (_layoutEntity is not null || _activeMode != HudMode.None)
        {
            _ = ClearLayout("layout mode switch");
        }

        var spec = GetLayoutSpec(requestedMode);
        try
        {
            var entity = Core.EntitySystem.CreateEntity<CCSCustomHudLayout>();
            entity.StrLayout = spec.LayoutResource;
            entity.StrLayoutUpdated();
            entity.DispatchSpawn();
            _layoutEntity = entity;
            _activeMode = requestedMode;

            var openedHuds = OpenHudForConnectedPlayers();
            context.Reply($"[CustomHudProbeSW2] Loaded {ModeName(requestedMode)} in entity #{entity.Index}: {spec.LayoutResource}; opened {openedHuds} HUD(s).");
            context.Reply("[CustomHudProbeSW2] Switch with !chud_spawn menu, !chud_spawn card, !chud_spawn gallery, or !chud_spawn flip; only one probe entity is kept alive.");
            Logger.LogInformation(
                "[CustomHudProbeSW2] Loaded mode={Mode} entity={EntityIndex} layout={LayoutResource}; opened={OpenedHuds}.",
                ModeName(requestedMode),
                entity.Index,
                spec.LayoutResource,
                openedHuds);
        }
        catch (Exception ex)
        {
            ClearLayout("spawn failure");
            context.Reply($"[CustomHudProbeSW2] Spawn failed: {ex.Message}");
            Logger.LogError(ex, "[CustomHudProbeSW2] Failed to load {Mode} Custom HUD layout.", ModeName(requestedMode));
        }
    }

    [Command("chud_open", registerRaw: true, helpText: "Open or reset the currently loaded Custom HUD for yourself.")]
    public void OpenCommand(ICommandContext context)
    {
        var player = context.Sender;
        if (player?.IsValid != true)
        {
            context.Reply("[CustomHudProbeSW2] This command must be used by a connected player.");
            return;
        }

        if (!TryGetLayout(out _) || _activeMode == HudMode.None)
        {
            context.Reply("[CustomHudProbeSW2] The probe is inactive. Use !chud_spawn <menu|card|gallery|flip> first.");
            return;
        }

        OpenHud(player.Slot);
        context.Reply($"[CustomHudProbeSW2] Your {ModeName(_activeMode)} HUD is ready.");
    }

    [Command("chud_close", registerRaw: true, helpText: "Hide the currently loaded Custom HUD for yourself.")]
    public void CloseCommand(ICommandContext context)
    {
        var player = context.Sender;
        if (player?.IsValid != true)
        {
            context.Reply("[CustomHudProbeSW2] This command must be used by a connected player.");
            return;
        }

        context.Reply(CloseHud(player.Slot)
            ? $"[CustomHudProbeSW2] Your {ModeName(_activeMode)} HUD was closed."
            : "[CustomHudProbeSW2] No open Custom HUD session was found for you.");
    }

    [Command("chud_clear", registerRaw: true, helpText: "Remove the single active custom_hud_layout probe entity.")]
    public void ClearCommand(ICommandContext context)
    {
        if (ClearLayout("command"))
        {
            context.Reply("[CustomHudProbeSW2] Active probe entity removal requested.");
            return;
        }

        context.Reply("[CustomHudProbeSW2] No live probe entity is tracked.");
    }

    [Command("chud_status", registerRaw: true, helpText: "Show the active Custom HUD layout and entity state.")]
    public void StatusCommand(ICommandContext context)
    {
        if (_layoutEntity is { IsValid: true } entity && _activeMode != HudMode.None)
        {
            var spec = GetLayoutSpec(_activeMode);
            context.Reply($"[CustomHudProbeSW2] Active: mode={ModeName(_activeMode)}, entity=#{entity.Index}, layout={spec.LayoutResource}, sessions={_inputCapturedSlots.Count}.");
            return;
        }

        context.Reply("[CustomHudProbeSW2] Probe inactive. Use !chud_spawn <menu|card|gallery|flip>.");
    }

    private int OpenHudForConnectedPlayers()
    {
        var opened = 0;
        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (!player.IsValid || player.IsFakeClient || player.Controller?.IsValid != true)
            {
                continue;
            }

            OpenHud(player.Slot);
            opened++;
        }

        return opened;
    }

    private void OpenHud(int playerSlot)
    {
        if (!TryGetLayout(out var layout) || _activeMode == HudMode.None)
        {
            return;
        }

        var spec = GetLayoutSpec(_activeMode);
        if (_activeMode == HudMode.Menu)
        {
            SetDialogValue(playerSlot, layout, "kicker", "SWIFT MENU / CUSTOM HUD");
            SetDialogValue(playerSlot, layout, "title", "Custom HUD validation");
            SetDialogValue(playerSlot, layout, "status", "Click a button to verify the SwiftlyS2 server callback.");
            SetDialogValue(playerSlot, layout, "primary-action", "Primary action");
            SetDialogValue(playerSlot, layout, "secondary-action", "Toggle accent");
            SetDialogValue(playerSlot, layout, "close-action", "Close");
            SetClassForPlayer(layout, playerSlot, spec.DialogPanelId, AccentClass, false);
        }

        SetClassForPlayer(layout, playerSlot, spec.DialogPanelId, HiddenClass, false);
        layout.SetInputCaptureEnabledForPlayer(playerSlot, true);
        _inputCapturedSlots.Add(playerSlot);
    }

    private bool CloseHud(int playerSlot)
    {
        if (!TryGetLayout(out var layout) ||
            _activeMode == HudMode.None ||
            !_inputCapturedSlots.Remove(playerSlot))
        {
            return false;
        }

        var spec = GetLayoutSpec(_activeMode);
        layout.SetInputCaptureEnabledForPlayer(playerSlot, false);
        SetClassForPlayer(layout, playerSlot, spec.DialogPanelId, HiddenClass, true);
        if (_activeMode == HudMode.Menu)
        {
            SetClassForPlayer(layout, playerSlot, spec.DialogPanelId, AccentClass, false);
        }

        return true;
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        if (_inputCapturedSlots.Contains(@event.PlayerId))
        {
            _ = CloseHud(@event.PlayerId);
        }
    }

    private void OnCustomHudClicked(IOnCustomHudClickedEvent @event)
    {
        if (_activeMode != HudMode.Menu ||
            !TryGetLayout(out var layout) ||
            @event.CustomHudLayout.Address != layout.Address)
        {
            return;
        }

        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player?.IsValid != true ||
            player.Controller?.IsValid != true ||
            !_inputCapturedSlots.Contains(@event.PlayerId))
        {
            return;
        }

        switch (@event.ButtonId)
        {
            case "swift_menu_primary":
                SetDialogValue(@event.PlayerId, layout, "status", "Primary callback reached the server.");
                break;
            case "swift_menu_secondary":
                SetClassForPlayer(layout, @event.PlayerId, MenuDialogPanelId, AccentClass, true);
                SetDialogValue(@event.PlayerId, layout, "status", "Per-player CSS class update applied.");
                break;
            case "swift_menu_close":
                _ = CloseHud(@event.PlayerId);
                break;
            default:
                return;
        }

        Logger.LogInformation("[CustomHudProbeSW2] Custom HUD click: slot={PlayerSlot}, button={ButtonId}.", @event.PlayerId, @event.ButtonId);
    }

    private static void SetDialogValue(int playerSlot, CCSCustomHudLayout layout, string variableName, string value) =>
        layout.SetDialogVariableStringForPlayer(playerSlot, MenuDialogPanelId, variableName, value);

    private static void SetClassForPlayer(
        CCSCustomHudLayout layout,
        int playerSlot,
        string panelId,
        string className,
        bool hasClass) =>
        layout.SetHasClassForPlayer(
            playerSlot,
            panelId,
            className,
            hasClass
                ? EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass
                : EHudPanelClassStatus_t.k_eHudPanelClassStatus_DoesNotHaveClass);

    private bool TryGetLayout(out CCSCustomHudLayout layout)
    {
        if (_layoutEntity is { IsValid: true } activeLayout)
        {
            layout = activeLayout;
            return true;
        }

        layout = null!;
        return false;
    }

    private bool ClearLayout(string reason)
    {
        var entity = _layoutEntity;
        var clearedMode = _activeMode;
        _layoutEntity = null;
        _activeMode = HudMode.None;

        if (entity is { IsValid: true })
        {
            foreach (var playerSlot in _inputCapturedSlots.ToArray())
            {
                try
                {
                    entity.SetInputCaptureEnabledForPlayer(playerSlot, false);
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(exception, "[CustomHudProbeSW2] Failed to release input capture for slot {PlayerSlot}.", playerSlot);
                }
            }
        }

        _inputCapturedSlots.Clear();
        if (entity is not { IsValid: true })
        {
            return false;
        }

        try
        {
            entity.AcceptInput("Kill", string.Empty);
            Logger.LogInformation(
                "[CustomHudProbeSW2] Requested removal for {Mode} entity #{EntityIndex} ({Reason}).",
                ModeName(clearedMode),
                entity.Index,
                reason);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[CustomHudProbeSW2] Failed to remove entity #{EntityIndex} ({Reason}).", entity.Index, reason);
            return false;
        }
    }

    private static bool TryParseMode(string value, out HudMode mode)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "menu":
            case "button":
            case "buttons":
                mode = HudMode.Menu;
                return true;
            case "card":
            case "cyber":
                mode = HudMode.Card;
                return true;
            case "flip":
            case "flipcard":
            case "turn":
                mode = HudMode.Flip;
                return true;
            case "gallery":
            case "hover3d":
            case "images":
                mode = HudMode.Gallery;
                return true;
            default:
                mode = HudMode.None;
                return false;
        }
    }

    private static LayoutSpec GetLayoutSpec(HudMode mode) => mode switch
    {
        HudMode.Menu => new LayoutSpec(MenuLayoutResource, MenuDialogPanelId),
        HudMode.Card => new LayoutSpec(CardLayoutResource, CardDialogPanelId),
        HudMode.Gallery => new LayoutSpec(GalleryLayoutResource, GalleryDialogPanelId),
        HudMode.Flip => new LayoutSpec(FlipLayoutResource, FlipDialogPanelId),
        _ => throw new InvalidOperationException("No Custom HUD layout is active.")
    };

    private static string ModeName(HudMode mode) => mode switch
    {
        HudMode.Menu => "menu",
        HudMode.Card => "card",
        HudMode.Gallery => "gallery",
        HudMode.Flip => "flip",
        _ => "none"
    };

    private enum HudMode
    {
        None,
        Menu,
        Card,
        Gallery,
        Flip
    }

    private readonly record struct LayoutSpec(string LayoutResource, string DialogPanelId);
}
