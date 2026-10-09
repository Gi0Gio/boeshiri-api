using Boeshiri.Application.Abstractions;

namespace Boeshiri.Tests.Support;

/// <summary>Fake de <see cref="IEmailSender"/> que captura los correos enviados.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body, string? Text)> Sent { get; } = [];
    public List<string?> ReplyTo { get; } = [];

    public Task SendAsync(string to, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default, string? replyTo = null)
    {
        Sent.Add((to, subject, htmlBody, textBody));
        ReplyTo.Add(replyTo);
        return Task.CompletedTask;
    }
}
