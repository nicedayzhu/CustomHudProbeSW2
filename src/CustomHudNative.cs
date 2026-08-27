using System.Runtime.InteropServices;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Memory;

namespace CustomHudProbeSW2;

/// <summary>
/// Build-locked native bridge for per-player Custom HUD state and button IDs.
/// SwiftlyS2 exposes the layout schema but not these state setters or the
/// CustomHudClicked receiver in its high-level API.
/// </summary>
internal sealed class CustomHudNativeBridge : IDisposable
{
    // server.dll: MD5 e5ce8c6f1544fd63e060482b599d86b9
    // IDA image base: 0x180000000. Revalidate after every CS2 update.
    internal const string ServerBuildSha256 = "67821ebafe22b98dca419433f3ab478413783a907820e3ee5dbde733ac5a763d";

    private const string SetDialogVariableStringForPlayerSignature =
        "40 53 56 57 48 83 EC 20 48 63 DA 49 8B F1 49 8B C0 48 8B F9 3B 99 B0 04 00 00 7D 5E 48 89 6C 24 50 4C 8D 44 24 40 33 ED 48 8B D0 66 89 6C 24 40 E8 0B 3F FF FF 84 C0 74 3C 4C 8D 44 24 48 66 89 6C 24 48 48 8B D6 48 8B CF E8 82 36 FF FF 84 C0 74 23 4C 8B 4C 24 60 44 0F B7 44 24 48 0F B7 54 24 40 48 69 CB A0 01 00 00 48 03 8F B8 04 00 00";

    private const string SetHasClassForPlayerSignature =
        "40 53 56 57 48 83 EC 20 48 63 DA 49 8B F1 49 8B C0 48 8B F9 3B 99 B0 04 00 00 7D 5E 48 89 6C 24 50 4C 8D 44 24 40 33 ED 48 8B D0 66 89 6C 24 40 E8 DB 3B FF FF 84 C0 74 3C 4C 8D 44 24 48 66 89 6C 24 48 48 8B D6 48 8B CF E8 D2 31 FF FF 84 C0 74 23 44 8B 4C 24 60 44 0F B7 44 24 48 0F B7 54 24 40 48 69 CB A0 01 00 00 48 03 8F B8 04 00 00";

    private const string SetInputCaptureEnabledSignature =
        "40 57 48 83 EC 20 41 0F B6 F8 3B 91 B0 04 00 00 7D 39 48 89 5C 24 30 48 63 C2 48 69 D8 A0 01 00 00 48 03 99 B8 04 00 00 44 38 43 30 74 18 BA FF FF FF FF 48 8D 4B 30 41 B8 FF FF FF FF E8 6E C1 FF FF 40 88 7B 30 48 8B 5C 24 30 48 83 C4 20 5F C3";

    private const string CustomHudClickedReceiverSignature =
        "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 20 48 8B 1D E5 81 BC 01 49 8B F9 49 8B F0 48 8B EA 48 85 DB 74 28 0F 1F 80 00 00 00 00 48 8B 03 4C 8B CF 4C 8B C6 48 8B D5 48 8B CB FF 90 10 01 00 00";

    private readonly IUnmanagedFunction<SetDialogVariableStringForPlayerDelegate> _setDialogVariableStringForPlayer;
    private readonly IUnmanagedFunction<SetHasClassForPlayerDelegate> _setHasClassForPlayer;
    private readonly IUnmanagedFunction<SetInputCaptureEnabledDelegate> _setInputCaptureEnabled;
    private readonly IUnmanagedFunction<CustomHudClickedReceiverDelegate> _customHudClickedReceiver;
    private Guid? _customHudClickHook;

    private CustomHudNativeBridge(
        IUnmanagedFunction<SetDialogVariableStringForPlayerDelegate> setDialogVariableStringForPlayer,
        IUnmanagedFunction<SetHasClassForPlayerDelegate> setHasClassForPlayer,
        IUnmanagedFunction<SetInputCaptureEnabledDelegate> setInputCaptureEnabled,
        IUnmanagedFunction<CustomHudClickedReceiverDelegate> customHudClickedReceiver)
    {
        _setDialogVariableStringForPlayer = setDialogVariableStringForPlayer;
        _setHasClassForPlayer = setHasClassForPlayer;
        _setInputCaptureEnabled = setInputCaptureEnabled;
        _customHudClickedReceiver = customHudClickedReceiver;
    }

    public static CustomHudNativeBridge Create(IMemoryService memory) => new(
        Resolve<SetDialogVariableStringForPlayerDelegate>(memory, nameof(SetDialogVariableStringForPlayer), SetDialogVariableStringForPlayerSignature),
        Resolve<SetHasClassForPlayerDelegate>(memory, nameof(SetHasClassForPlayer), SetHasClassForPlayerSignature),
        Resolve<SetInputCaptureEnabledDelegate>(memory, nameof(SetInputCaptureEnabled), SetInputCaptureEnabledSignature),
        Resolve<CustomHudClickedReceiverDelegate>(memory, nameof(HookCustomHudClicks), CustomHudClickedReceiverSignature));

    public void SetDialogVariableStringForPlayer(nint layout, int playerSlot, string dialogId, string variableName, string value)
    {
        using var dialog = new UtlStringArgument(dialogId);
        using var variable = new UtlStringArgument(variableName);
        using var text = new UtlStringArgument(value);
        _ = _setDialogVariableStringForPlayer.Call(layout, playerSlot, dialog.Address, variable.Address, text.Address);
    }

    public void SetHasClassForPlayer(nint layout, int playerSlot, string dialogId, string className, bool hasClass)
    {
        using var dialog = new UtlStringArgument(dialogId);
        using var classArgument = new UtlStringArgument(className);
        _ = _setHasClassForPlayer.Call(layout, playerSlot, dialog.Address, classArgument.Address, hasClass ? 1u : 0u);
    }

    public void SetInputCaptureEnabled(nint layout, int playerSlot, bool enabled) =>
        _ = _setInputCaptureEnabled.Call(layout, playerSlot, enabled ? (byte)1 : (byte)0);

    public void HookCustomHudClicks(Action<nint, nint, string> handler, Action<Exception> exceptionHandler)
    {
        if (_customHudClickHook is not null)
        {
            throw new InvalidOperationException("The Custom HUD click dispatch is already hooked.");
        }

        _customHudClickHook = _customHudClickedReceiver.AddHook(next =>
            (pulseBinding, playerController, layout, buttonId) =>
            {
                next()(pulseBinding, playerController, layout, buttonId);
                try
                {
                    handler(playerController, layout, ReadUtlString(buttonId));
                }
                catch (Exception exception)
                {
                    exceptionHandler(exception);
                }
            });
    }

    public void Dispose()
    {
        if (_customHudClickHook is { } hook)
        {
            _customHudClickedReceiver.RemoveHook(hook);
            _customHudClickHook = null;
        }
    }

    private static IUnmanagedFunction<TDelegate> Resolve<TDelegate>(IMemoryService memory, string name, string signature)
        where TDelegate : Delegate
    {
        var address = memory.GetAddressBySignature(Library.Server, signature);
        if (address is null)
        {
            throw new InvalidOperationException($"CCSCustomHudLayout signature '{name}' was not found. Expected server.dll SHA-256: {ServerBuildSha256}.");
        }

        return memory.GetUnmanagedFunctionByAddress<TDelegate>(address.Value);
    }

    private static string ReadUtlString(nint stringObject)
    {
        if (stringObject == nint.Zero)
        {
            return string.Empty;
        }

        var chars = Marshal.ReadIntPtr(stringObject);
        return chars == nint.Zero ? string.Empty : Marshal.PtrToStringUTF8(chars) ?? string.Empty;
    }

    // CUtlString has a pointer-sized first field in this build. IDA confirms
    // all three setters only read and copy their arguments during the call.
    private sealed class UtlStringArgument : IDisposable
    {
        private readonly nint _utf8;
        public nint Address { get; }

        public UtlStringArgument(string value)
        {
            _utf8 = Marshal.StringToCoTaskMemUTF8(value);
            Address = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(Address, _utf8);
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(Address);
            Marshal.FreeCoTaskMem(_utf8);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SetDialogVariableStringForPlayerDelegate(nint layout, int playerSlot, nint dialogId, nint variableName, nint value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SetHasClassForPlayerDelegate(nint layout, int playerSlot, nint dialogId, nint className, uint hasClass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SetInputCaptureEnabledDelegate(nint layout, int playerSlot, byte enabled);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CustomHudClickedReceiverDelegate(nint pulseBinding, nint playerController, nint layout, nint buttonId);
}
