using Boeshiri.Application.Abstractions;

namespace Boeshiri.Tests.Support;

/// <summary>Fake de <see cref="IEmailSender"/> que captura los correos enviados.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body, string? Text)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default)
    {
        Sent.Add((to, subject, htmlBody, textBody));
        return Task.CompletedTask;
    }
}
