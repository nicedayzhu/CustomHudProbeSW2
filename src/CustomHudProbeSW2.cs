using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.EntitySystem;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace CustomHudProbeSW2;

[PluginMetadata(
    Id = "CustomHudProbeSW2",
    Version = "0.3.0",
    Name = "Custom HUD Probe",
    Author = "Swift Menu PoC",
    Description = "Loads one of several CS2 custom_hud_layout resources into a single probe entity.",
    MinimumAPIVersion = "1.2.0"
)]
public sealed class CustomHudProbeSW2(ISwiftlyCore core) : BasePlugin(core)
{
    private const string DesignerName = "custom_hud_layout";
    private const string MenuTargetName = "swift_menu_custom_hud";
    private const string CardTargetName = "swift_cyber_card_custom_hud";
    private const string GalleryTargetName = "swift_hover3d_gallery_custom_hud";
    private const string MenuLayoutResource = "panorama/layout/custom_game/swift_menu_custom_hud.xml";
    private const string CardLayoutResource = "panorama/layout/custom_game/cyber_card_custom_hud.xml";
    private const string GalleryLayoutResource = "panorama/layout/custom_game/hover3d_gallery_custom_hud.xml";
    private const string MenuDialogPanelId = "dialog";
    private const string CardDialogPanelId = "card_dialog";
    private const string GalleryDialogPanelId = "gallery_dialog";
    private const string HiddenClass = "SwiftHudHidden";
    private const string AccentClass = "SwiftHudAccent";

    private readonly HashSet<int> _inputCapturedSlots = [];

    private CCSCustomHudLayout? _layoutEntity;
    private CustomHudNativeBridge? _nativeHud;
    private HudMode _activeMode;

    private ILogger<CustomHudProbeSW2> Logger => Core.LoggerFactory.CreateLogger<CustomHudProbeSW2>();

    public override void Load(bool hotReload)
    {
        try
        {
            _nativeHud = CustomHudNativeBridge.Create(Core.GameData, Core.Memory);
            _nativeHud.HookCustomHudClicks(OnNativeCustomHudClicked, exception =>
                Logger.LogError(exception, "[CustomHudProbeSW2] Custom HUD click bridge callback failed."));

            Logger.LogInformation(
                "[CustomHudProbeSW2] Native Custom HUD bridge ready (hotReload={HotReload}). Use !chud_spawn <menu|card|gallery>.",
                hotReload);
        }
        catch (Exception exception)
        {
            _nativeHud?.Dispose();
            _nativeHud = null;
            Logger.LogError(
                exception,
                "[CustomHudProbeSW2] Signature bridge is unavailable. The plugin will not spawn a HUD on an unverified server.dll build.");
        }
    }

    public override void Unload()
    {
        ClearLayout("plugin unload");
        _nativeHud?.Dispose();
        _nativeHud = null;
        Logger.LogInformation("[CustomHudProbeSW2] Unloaded.");
    }

    [Command("chud_spawn", registerRaw: true, helpText: "Load one Custom HUD layout: chud_spawn <menu|card|gallery>.")]
    public void SpawnCommand(ICommandContext context)
    {
        var modeText = context.Args.Length > 0 ? context.Args[0] : "menu";
        if (!TryParseMode(modeText, out var requestedMode))
        {
            context.Reply("[CustomHudProbeSW2] Usage: !chud_spawn <menu|card|gallery>.");
            return;
        }

        if (_nativeHud is null)
        {
            context.Reply("[CustomHudProbeSW2] Native Custom HUD bridge is unavailable; verify the server.dll build and server log.");
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
            using var keyValues = new CEntityKeyValues();
            keyValues.SetString("targetname", spec.TargetName);
            keyValues.SetString("layout", spec.LayoutResource);

            var entity = Core.EntitySystem.CreateEntityByDesignerName<CCSCustomHudLayout>(DesignerName, -1);
            entity.DispatchSpawn(keyValues);
            _layoutEntity = entity;
            _activeMode = requestedMode;

            var openedHuds = OpenHudForConnectedPlayers();
            context.Reply($"[CustomHudProbeSW2] Loaded {ModeName(requestedMode)} in entity #{entity.Index}: {spec.LayoutResource}; opened {openedHuds} HUD(s).");
            context.Reply("[CustomHudProbeSW2] Switch with !chud_spawn menu, !chud_spawn card, or !chud_spawn gallery; only one probe entity is kept alive.");
            Logger.LogInformation(
                "[CustomHudProbeSW2] Loaded mode={Mode} entity={EntityIndex} target={TargetName} layout={LayoutResource}; opened={OpenedHuds}.",
                ModeName(requestedMode),
                entity.Index,
                spec.TargetName,
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

        if (!TryGetLayoutAddress(out _) || _activeMode == HudMode.None)
        {
            context.Reply("[CustomHudProbeSW2] The probe is inactive. Use !chud_spawn <menu|card|gallery> first.");
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
        if (_nativeHud is null)
        {
            context.Reply("[CustomHudProbeSW2] Native Custom HUD bridge unavailable; the server.dll build did not pass the signature contract.");
            return;
        }

        if (_layoutEntity is { IsValid: true } entity && _activeMode != HudMode.None)
        {
            var spec = GetLayoutSpec(_activeMode);
            context.Reply($"[CustomHudProbeSW2] Active: mode={ModeName(_activeMode)}, entity=#{entity.Index}, layout={spec.LayoutResource}, sessions={_inputCapturedSlots.Count}.");
            return;
        }

        context.Reply("[CustomHudProbeSW2] Probe inactive. Use !chud_spawn <menu|card|gallery>.");
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
        if (!TryGetLayoutAddress(out var layoutAddress) || _nativeHud is null || _activeMode == HudMode.None)
        {
            return;
        }

        var spec = GetLayoutSpec(_activeMode);
        if (_activeMode == HudMode.Menu)
        {
            SetDialogValue(playerSlot, layoutAddress, "kicker", "SWIFT MENU / CUSTOM HUD");
            SetDialogValue(playerSlot, layoutAddress, "title", "Custom HUD validation");
            SetDialogValue(playerSlot, layoutAddress, "status", "Click a button to verify the native server callback.");
            SetDialogValue(playerSlot, layoutAddress, "primary-action", "Primary action");
            SetDialogValue(playerSlot, layoutAddress, "secondary-action", "Toggle accent");
            SetDialogValue(playerSlot, layoutAddress, "close-action", "Close");
            _nativeHud.SetHasClassForPlayer(layoutAddress, playerSlot, spec.DialogPanelId, AccentClass, false);
        }

        _nativeHud.SetHasClassForPlayer(layoutAddress, playerSlot, spec.DialogPanelId, HiddenClass, false);
        _nativeHud.SetInputCaptureEnabled(layoutAddress, playerSlot, true);
        _inputCapturedSlots.Add(playerSlot);
    }

    private bool CloseHud(int playerSlot)
    {
        if (!TryGetLayoutAddress(out var layoutAddress) ||
            _nativeHud is null ||
            _activeMode == HudMode.None ||
            !_inputCapturedSlots.Remove(playerSlot))
        {
            return false;
        }

        var spec = GetLayoutSpec(_activeMode);
        _nativeHud.SetInputCaptureEnabled(layoutAddress, playerSlot, false);
        _nativeHud.SetHasClassForPlayer(layoutAddress, playerSlot, spec.DialogPanelId, HiddenClass, true);
        if (_activeMode == HudMode.Menu)
        {
            _nativeHud.SetHasClassForPlayer(layoutAddress, playerSlot, spec.DialogPanelId, AccentClass, false);
        }

        return true;
    }

    private void OnNativeCustomHudClicked(nint playerControllerAddress, nint layoutAddress, string buttonId)
    {
        Core.Scheduler.NextWorldUpdate(() =>
            ProcessNativeCustomHudClick(playerControllerAddress, layoutAddress, buttonId));
    }

    private void ProcessNativeCustomHudClick(nint playerControllerAddress, nint layoutAddress, string buttonId)
    {
        if (_activeMode != HudMode.Menu ||
            !TryGetLayoutAddress(out var expectedLayoutAddress) ||
            layoutAddress != expectedLayoutAddress)
        {
            return;
        }

        var player = Core.PlayerManager.GetAllPlayers().FirstOrDefault(candidate =>
            candidate.IsValid &&
            candidate.Controller is { IsValid: true } controller &&
            controller.Address == playerControllerAddress);
        if (player is null || !_inputCapturedSlots.Contains(player.Slot) || _nativeHud is null)
        {
            return;
        }

        switch (buttonId)
        {
            case "swift_menu_primary":
                SetDialogValue(player.Slot, expectedLayoutAddress, "status", "Primary callback reached the server.");
                break;
            case "swift_menu_secondary":
                _nativeHud.SetHasClassForPlayer(expectedLayoutAddress, player.Slot, MenuDialogPanelId, AccentClass, true);
                SetDialogValue(player.Slot, expectedLayoutAddress, "status", "Per-player CSS class update applied.");
                break;
            case "swift_menu_close":
                _ = CloseHud(player.Slot);
                break;
            default:
                return;
        }

        Logger.LogInformation("[CustomHudProbeSW2] Custom HUD click: slot={PlayerSlot}, button={ButtonId}.", player.Slot, buttonId);
    }

    private void SetDialogValue(int playerSlot, nint layoutAddress, string variableName, string value) =>
        _nativeHud!.SetDialogVariableStringForPlayer(layoutAddress, playerSlot, MenuDialogPanelId, variableName, value);

    private bool TryGetLayoutAddress(out nint layoutAddress)
    {
        if (_layoutEntity is { IsValid: true } layout)
        {
            layoutAddress = layout.Address;
            return true;
        }

        layoutAddress = nint.Zero;
        return false;
    }

    private bool ClearLayout(string reason)
    {
        var entity = _layoutEntity;
        var clearedMode = _activeMode;
        _layoutEntity = null;
        _activeMode = HudMode.None;

        if (entity is { IsValid: true } && _nativeHud is not null)
        {
            foreach (var playerSlot in _inputCapturedSlots.ToArray())
            {
                try
                {
                    _nativeHud.SetInputCaptureEnabled(entity.Address, playerSlot, false);
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
        HudMode.Menu => new LayoutSpec(MenuTargetName, MenuLayoutResource, MenuDialogPanelId),
        HudMode.Card => new LayoutSpec(CardTargetName, CardLayoutResource, CardDialogPanelId),
        HudMode.Gallery => new LayoutSpec(GalleryTargetName, GalleryLayoutResource, GalleryDialogPanelId),
        _ => throw new InvalidOperationException("No Custom HUD layout is active.")
    };

    private static string ModeName(HudMode mode) => mode switch
    {
        HudMode.Menu => "menu",
        HudMode.Card => "card",
        HudMode.Gallery => "gallery",
        _ => "none"
    };

    private enum HudMode
    {
        None,
        Menu,
        Card,
        Gallery
    }

    private readonly record struct LayoutSpec(string TargetName, string LayoutResource, string DialogPanelId);
}
