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

    private CEntityInstance? _layoutEntity;

    private ILogger<CustomHudProbeSW2> Logger => Core.LoggerFactory.CreateLogger<CustomHudProbeSW2>();

    public override void Load(bool hotReload)
    {
        Logger.LogInformation(
            "[CustomHudProbeSW2] Loaded (hotReload={HotReload}). Use !chud_spawn after the HUD resource VPK is mounted.",
            hotReload);
    }

    public override void Unload()
    {
        ClearLayout("plugin unload");
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

        try
        {
            using var keyValues = new CEntityKeyValues();
            keyValues.SetString("targetname", TargetName);
            keyValues.SetString("layout", LayoutResource);

            // The new derived schema is not generated yet. CEntityInstance is the
            // supported generated base wrapper and still lets us supply the exact
            // designer name plus spawn key values without using private SDK APIs.
            var entity = Core.EntitySystem.CreateEntityByDesignerName<CEntityInstance>(DesignerName, -1);
            entity.DispatchSpawn(keyValues);
            _layoutEntity = entity;

            context.Reply($"[CustomHudProbeSW2] Spawned {DesignerName} #{entity.Index}: {LayoutResource}");
            context.Reply("[CustomHudProbeSW2] If no HUD appears, verify the probe VPK is mounted and run dev_report_info_hud_layout.");
            Logger.LogInformation(
                "[CustomHudProbeSW2] Spawned {DesignerName} index={EntityIndex} target={TargetName} layout={LayoutResource}.",
                DesignerName,
                entity.Index,
                TargetName,
                LayoutResource);
        }
        catch (Exception ex)
        {
            _layoutEntity = null;
            context.Reply($"[CustomHudProbeSW2] Spawn failed: {ex.Message}");
            Logger.LogError(ex, "[CustomHudProbeSW2] Failed to spawn custom HUD probe.");
        }
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
        if (_layoutEntity is { IsValid: true } entity)
        {
            context.Reply($"[CustomHudProbeSW2] Active: #{entity.Index}, designer={entity.DesignerName}, target={TargetName}.");
            return;
        }

        context.Reply("[CustomHudProbeSW2] Inactive. Use !chud_spawn.");
    }

    private bool ClearLayout(string reason)
    {
        var entity = _layoutEntity;
        _layoutEntity = null;

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
