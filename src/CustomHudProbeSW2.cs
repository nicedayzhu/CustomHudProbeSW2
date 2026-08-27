using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.EntitySystem;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace CustomHudProbeSW2;

[PluginMetadata(
    Id = "CustomHudProbeSW2",
    Version = "0.1.0",
    Name = "Custom HUD Probe",
    Author = "Swift Menu PoC",
    Description = "Dynamically creates a CS2 custom_hud_layout entity for CCSCustomHudLayout validation.",
    MinimumAPIVersion = "1.2.0"
)]
public sealed class CustomHudProbeSW2(ISwiftlyCore core) : BasePlugin(core)
{
    private const string DesignerName = "custom_hud_layout";
    private const string TargetName = "swift_menu_custom_hud";
    private const string LayoutResource = "panorama/layout/custom_game/swift_menu_custom_hud.xml";
    private const string DialogPanelId = "dialog";
    private const string HiddenClass = "SwiftHudHidden";
    private const string AccentClass = "SwiftHudAccent";

    private readonly HashSet<int> _inputCapturedSlots = [];

    private CCSCustomHudLayout? _layoutEntity;
    private CustomHudNativeBridge? _nativeHud;

    private ILogger<CustomHudProbeSW2> Logger => Core.LoggerFactory.CreateLogger<CustomHudProbeSW2>();

    public override void Load(bool hotReload)
    {
        try
        {
            _nativeHud = CustomHudNativeBridge.Create(Core.Memory);
            _nativeHud.HookCustomHudClicks(OnNativeCustomHudClicked, exception =>
                Logger.LogError(exception, "[CustomHudProbeSW2] Custom HUD click bridge callback failed."));

            Logger.LogInformation(
                "[CustomHudProbeSW2] Native CustomHudClicked receiver and state setters are ready for server.dll build {BuildHash} (hotReload={HotReload}). Use !chud_spawn.",
                CustomHudNativeBridge.ServerBuildSha256,
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

    [Command("chud_spawn", registerRaw: true, helpText: "Dynamically create the Swift Menu custom_hud_layout probe.")]
    public void SpawnCommand(ICommandContext context)
    {
        if (_layoutEntity is { IsValid: true })
        {
            context.Reply("[CustomHudProbeSW2] Probe already exists. Use !chud_clear before spawning it again.");
            return;
        }

        if (_nativeHud is null)
        {
            context.Reply("[CustomHudProbeSW2] Native Custom HUD bridge is unavailable; verify the server.dll build and server log.");
            return;
        }

        try
        {
            using var keyValues = new CEntityKeyValues();
            keyValues.SetString("targetname", TargetName);
            keyValues.SetString("layout", LayoutResource);

            var entity = Core.EntitySystem.CreateEntityByDesignerName<CCSCustomHudLayout>(DesignerName, -1);
            entity.DispatchSpawn(keyValues);
            _layoutEntity = entity;

            var openedMenus = OpenMenusForConnectedPlayers();
            context.Reply($"[CustomHudProbeSW2] Spawned {DesignerName} #{entity.Index}: {LayoutResource}; opened {openedMenus} menu(s).");
            context.Reply("[CustomHudProbeSW2] If no HUD appears, verify the probe VPK is mounted and run dev_report_info_hud_layout.");
            Logger.LogInformation(
                "[CustomHudProbeSW2] Spawned {DesignerName} index={EntityIndex} target={TargetName} layout={LayoutResource}; opened {OpenedMenus} menu(s).",
                DesignerName,
                entity.Index,
                TargetName,
                LayoutResource,
                openedMenus);
        }
        catch (Exception ex)
        {
            ClearLayout("spawn failure");
            context.Reply($"[CustomHudProbeSW2] Spawn failed: {ex.Message}");
            Logger.LogError(ex, "[CustomHudProbeSW2] Failed to spawn custom HUD probe.");
        }
    }

    [Command("chud_open", registerRaw: true, helpText: "Open or reset your Custom HUD probe menu.")]
    public void OpenCommand(ICommandContext context)
    {
        var player = context.Sender;
        if (player?.IsValid != true)
        {
            context.Reply("[CustomHudProbeSW2] This command must be used by a connected player.");
            return;
        }

        if (!TryGetLayoutAddress(out _))
        {
            context.Reply("[CustomHudProbeSW2] The probe is inactive. Use !chud_spawn first.");
            return;
        }

        OpenMenu(player.Slot);
        context.Reply("[CustomHudProbeSW2] Your Custom HUD probe menu is ready.");
    }

    [Command("chud_clear", registerRaw: true, helpText: "Remove the dynamically created custom_hud_layout probe.")]
    public void ClearCommand(ICommandContext context)
    {
        if (ClearLayout("command"))
        {
            context.Reply("[CustomHudProbeSW2] Probe removal requested.");
            return;
        }

        context.Reply("[CustomHudProbeSW2] No live probe entity is tracked.");
    }

    [Command("chud_status", registerRaw: true, helpText: "Show the Custom HUD probe entity state.")]
    public void StatusCommand(ICommandContext context)
    {
        if (_nativeHud is null)
        {
            context.Reply("[CustomHudProbeSW2] Native Custom HUD bridge unavailable; the server.dll build did not pass the signature contract.");
            return;
        }

        if (_layoutEntity is { IsValid: true } entity)
        {
            context.Reply($"[CustomHudProbeSW2] Active: #{entity.Index}, designer={entity.DesignerName}, target={TargetName}, menus={_inputCapturedSlots.Count}, native click receiver installed.");
            return;
        }

        context.Reply("[CustomHudProbeSW2] Native click receiver is ready; probe inactive. Use !chud_spawn.");
    }

    private int OpenMenusForConnectedPlayers()
    {
        var opened = 0;
        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (!player.IsValid || player.IsFakeClient || player.Controller?.IsValid != true)
            {
                continue;
            }

            OpenMenu(player.Slot);
            opened++;
        }

        return opened;
    }

    private void OpenMenu(int playerSlot)
    {
        if (!TryGetLayoutAddress(out var layoutAddress) || _nativeHud is null)
        {
            return;
        }

        SetDialogValue(playerSlot, layoutAddress, "kicker", "SWIFT MENU / CUSTOM HUD");
        SetDialogValue(playerSlot, layoutAddress, "title", "Custom HUD validation");
        SetDialogValue(playerSlot, layoutAddress, "status", "Click a button to verify the native server callback.");
        SetDialogValue(playerSlot, layoutAddress, "primary-action", "Primary action");
        SetDialogValue(playerSlot, layoutAddress, "secondary-action", "Toggle accent");
        SetDialogValue(playerSlot, layoutAddress, "close-action", "Close");
        _nativeHud.SetHasClassForPlayer(layoutAddress, playerSlot, DialogPanelId, HiddenClass, false);
        _nativeHud.SetHasClassForPlayer(layoutAddress, playerSlot, DialogPanelId, AccentClass, false);
        _nativeHud.SetInputCaptureEnabled(layoutAddress, playerSlot, true);
        _inputCapturedSlots.Add(playerSlot);
    }

    private bool CloseMenu(int playerSlot)
    {
        if (!TryGetLayoutAddress(out var layoutAddress) || _nativeHud is null || !_inputCapturedSlots.Remove(playerSlot))
        {
            return false;
        }

        _nativeHud.SetInputCaptureEnabled(layoutAddress, playerSlot, false);
        _nativeHud.SetHasClassForPlayer(layoutAddress, playerSlot, DialogPanelId, HiddenClass, true);
        _nativeHud.SetHasClassForPlayer(layoutAddress, playerSlot, DialogPanelId, AccentClass, false);
        return true;
    }

    private void OnNativeCustomHudClicked(nint playerControllerAddress, nint layoutAddress, string buttonId)
    {
        // This receiver is entered from the networking path. Defer entity and
        // schema work to SwiftlyS2's next world update.
        Core.Scheduler.NextWorldUpdate(() =>
            ProcessNativeCustomHudClick(playerControllerAddress, layoutAddress, buttonId));
    }

    private void ProcessNativeCustomHudClick(nint playerControllerAddress, nint layoutAddress, string buttonId)
    {
        if (!TryGetLayoutAddress(out var expectedLayoutAddress) || layoutAddress != expectedLayoutAddress)
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
                _nativeHud.SetHasClassForPlayer(expectedLayoutAddress, player.Slot, DialogPanelId, AccentClass, true);
                SetDialogValue(player.Slot, expectedLayoutAddress, "status", "Per-player CSS class update applied.");
                break;
            case "swift_menu_close":
                _ = CloseMenu(player.Slot);
                break;
            default:
                return;
        }

        Logger.LogInformation("[CustomHudProbeSW2] Custom HUD click: slot={PlayerSlot}, button={ButtonId}.", player.Slot, buttonId);
    }

    private void SetDialogValue(int playerSlot, nint layoutAddress, string variableName, string value) =>
        _nativeHud!.SetDialogVariableStringForPlayer(layoutAddress, playerSlot, DialogPanelId, variableName, value);

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
        _layoutEntity = null;

        if (entity is { IsValid: true } && _nativeHud is not null)
        {
            foreach (var playerSlot in _inputCapturedSlots.ToArray())
            {
                try { _nativeHud.SetInputCaptureEnabled(entity.Address, playerSlot, false); }
                catch (Exception exception) { Logger.LogWarning(exception, "[CustomHudProbeSW2] Failed to release input capture for slot {PlayerSlot}.", playerSlot); }
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
            Logger.LogInformation("[CustomHudProbeSW2] Requested removal for entity #{EntityIndex} ({Reason}).", entity.Index, reason);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[CustomHudProbeSW2] Failed to remove entity #{EntityIndex} ({Reason}).", entity.Index, reason);
            return false;
        }
    }
}
