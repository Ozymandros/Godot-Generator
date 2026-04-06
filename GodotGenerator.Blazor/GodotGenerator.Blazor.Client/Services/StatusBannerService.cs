using Microsoft.FluentUI.AspNetCore.Components;

namespace GodotGenerator.Blazor.Client.Services;

/// <summary>
/// Global footer/status line (Unity-Generator StatusBanner parity) updated after generation and key actions.
/// </summary>
public sealed class StatusBannerService
{
    public string Message { get; private set; } = string.Empty;

    public MessageIntent Intent { get; private set; } = MessageIntent.Info;

    public event Action? Changed;

    public void Set(string message, MessageIntent intent = MessageIntent.Info)
    {
        Message = message ?? string.Empty;
        Intent = intent;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Set(string.Empty, MessageIntent.Info);
    }
}
