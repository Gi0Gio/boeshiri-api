using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Notifications;
using Boeshiri.Application.OpenCalls;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Common;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.OpenCalls;

/// <summary>Convocatorias: la Junta las arma y evalúa; el público las responde.</summary>
public class OpenCallService(
    BoeshiriDbContext db,
    INotificationService notifications,
    IAuditLogger audit) : IOpenCallService
{
    private const int MaxCorto = 300;
    private const int MaxLargo = 4000;
    private const int MaxEnlace = 500;
    private const decimal MaxMonto = 1_000_000m;

    // ── Junta ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<OpenCallSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        return await db.OpenCalls
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new OpenCallSummaryDto(
                c.Id, c.Title, c.Status, c.ClosesAt,
                c.Status == OpenCallStatus.Open && (c.ClosesAt == null || c.ClosesAt > ahora),
                c.EventId, c.Event != null ? c.Event.Title : null,
                c.Responses.Count,
                c.Responses.Count(r => r.Status == OpenCallResponseStatus.New),
                c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<OpenCallAdminDto> GetAdminAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.OpenCalls
            .Include(x => x.Event)
            .Include(x => x.Questions)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("La convocatoria no existe.");

        var respuestas = await db.OpenCallResponses
            .Where(r => r.OpenCallId == id)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ResponseDto(
                r.Id, r.UserId,
                r.UserId != null && db.Users.Any(u => u.Id == r.UserId && u.Status == MemberStatus.Active),
                r.Name, r.Email, r.Phone, r.CreatedAt, r.Status, r.Note,
                r.Answers.Select(a => new AnswerDto(a.QuestionId, a.Text, a.Amount)).ToList()))
            .ToListAsync(ct);

        return new OpenCallAdminDto(
            c.Id, c.Title, c.Description, c.Status, c.ClosesAt, EstaAbierta(c),
            c.EventId, c.Event?.Title, Preguntas(c), respuestas);
    }

    public async Task<Guid> CreateAsync(Guid userId, SaveOpenCallRequest request, CancellationToken ct = default)
    {
        await ValidarAsync(request, ct);
        var c = new OpenCall
        {
            Title = request.Title.Trim(),
            Description = Limpio(request.Description),
            EventId = request.EventId,
            ClosesAt = request.ClosesAt is DateTime d ? Fechas.Utc(d) : null,
            CreatedBy = userId,
        };
        var orden = 0;
        foreach (var q in request.Questions)
            c.Questions.Add(Nueva(q, orden++));

        db.OpenCalls.Add(c);
        audit.Log(userId, "convocatoria.creada", "OpenCall", c.Id.ToString(), c.Title);
        await db.SaveChangesAsync(ct);
        return c.Id;
    }

    public async Task UpdateAsync(Guid id, Guid userId, SaveOpenCallRequest request, CancellationToken ct = default)
    {
        await ValidarAsync(request, ct);
        var c = await db.OpenCalls.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("La convocatoria no existe.");
        var conRespuestas = await db.OpenCallResponses.AnyAsync(r => r.OpenCallId == id, ct);

        var existentes = c.Questions.ToDictionary(q => q.Id);
        var mantenidas = request.Questions.Where(q => q.Id is not null).Select(q => q.Id!.Value).ToHashSet();
        if (mantenidas.Any(qid => !existentes.ContainsKey(qid)))
            throw AppException.BadRequest("Una de las preguntas no es de esta convocatoria.");

        var quitadas = existentes.Values.Where(q => !mantenidas.Contains(q.Id)).ToList();
        if (conRespuestas && quitadas.Count > 0)
            throw AppException.Conflict("Ya hay respuestas: no se puede quitar una pregunta. Puedes reescribirla o añadir otras.");

        var orden = 0;
        foreach (var q in request.Questions)
        {
            if (q.Id is Guid qid)
            {
                var actual = existentes[qid];
                if (conRespuestas && actual.Type != q.Type)
                    throw AppException.Conflict($"Ya hay respuestas: «{actual.Label}» no puede cambiar de tipo.");
                actual.Label = q.Label.Trim();
                actual.Help = Limpio(q.Help);
                actual.Type = q.Type;
                actual.Required = q.Required;
                actual.Order = orden++;
            }
            else
            {
                // Por el DbSet y no por la colección: con el Id ya generado (v7), EF
                // tomaría la pregunta como existente e intentaría actualizarla.
                var nueva = Nueva(q, orden++);
                nueva.OpenCallId = c.Id;
                db.OpenCallQuestions.Add(nueva);
            }
        }
        db.OpenCallQuestions.RemoveRange(quitadas);

        c.Title = request.Title.Trim();
        c.Description = Limpio(request.Description);
        c.EventId = request.EventId;
        c.ClosesAt = request.ClosesAt is DateTime d ? Fechas.Utc(d) : null;
        c.UpdatedAt = DateTime.UtcNow;

        audit.Log(userId, "convocatoria.editada", "OpenCall", c.Id.ToString(), c.Title);
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(Guid id, Guid userId, ChangeOpenCallStatusRequest request, CancellationToken ct = default)
    {
        var c = await db.OpenCalls.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("La convocatoria no existe.");

        if (request.Status == OpenCallStatus.Open)
        {
            if (c.Questions.Count == 0)
                throw AppException.BadRequest("Añade al menos una pregunta antes de abrirla.");
            if (c.ClosesAt is DateTime cierre && cierre <= DateTime.UtcNow)
                throw AppException.BadRequest("La fecha de cierre ya pasó: cámbiala antes de abrirla.");
        }
        if (request.Status == OpenCallStatus.Draft && await db.OpenCallResponses.AnyAsync(r => r.OpenCallId == id, ct))
            throw AppException.Conflict("Ya tiene respuestas: no puede volver a borrador. Ciérrala si no quieres recibir más.");

        c.Status = request.Status;
        c.UpdatedAt = DateTime.UtcNow;
        audit.Log(userId, "convocatoria.estado", "OpenCall", c.Id.ToString(), request.Status.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var c = await db.OpenCalls.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("La convocatoria no existe.");
        if (await db.OpenCallResponses.AnyAsync(r => r.OpenCallId == id, ct))
            throw AppException.Conflict("Ya tiene respuestas: ciérrala en lugar de borrarla.");

        db.OpenCalls.Remove(c);
        audit.Log(userId, "convocatoria.eliminada", "OpenCall", c.Id.ToString(), c.Title);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReviewAsync(Guid responseId, Guid userId, ReviewResponseRequest request, CancellationToken ct = default)
    {
        var r = await db.OpenCallResponses.FirstOrDefaultAsync(x => x.Id == responseId, ct)
            ?? throw AppException.NotFound("La respuesta no existe.");

        r.Status = request.Status;
        r.Note = Limpio(request.Note);
        r.ReviewedBy = userId;
        r.ReviewedAt = DateTime.UtcNow;
        audit.Log(userId, "convocatoria.respuesta_evaluada", "OpenCallResponse", r.Id.ToString(), request.Status.ToString());
        await db.SaveChangesAsync(ct);
    }

    // ── Público ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<PublicOpenCallDto>> ListOpenAsync(CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var abiertas = await db.OpenCalls
            .Include(x => x.Event)
            .Include(x => x.Questions)
            .Where(c => c.Status == OpenCallStatus.Open && (c.ClosesAt == null || c.ClosesAt > ahora))
            .OrderBy(c => c.ClosesAt == null).ThenBy(c => c.ClosesAt)
            .ToListAsync(ct);
        return abiertas.Select(Publica).ToList();
    }

    public async Task<PublicOpenCallDto> GetPublicAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.OpenCalls
            .Include(x => x.Event)
            .Include(x => x.Questions)
            .FirstOrDefaultAsync(x => x.Id == id && x.Status != OpenCallStatus.Draft, ct)
            ?? throw AppException.NotFound("Esta convocatoria no existe o aún no se ha abierto.");
        return Publica(c);
    }

    public async Task SubmitAsync(Guid id, Guid? userId, SubmitResponseRequest request, CancellationToken ct = default)
    {
        // Trampa para robots: una persona no ve ese campo. Se responde como si todo
        // hubiera ido bien, para no enseñarle al robot qué lo delató.
        if (!string.IsNullOrWhiteSpace(request.Website))
            return;

        var c = await db.OpenCalls.Include(x => x.Questions)
            .FirstOrDefaultAsync(x => x.Id == id && x.Status != OpenCallStatus.Draft, ct)
            ?? throw AppException.NotFound("Esta convocatoria no existe o aún no se ha abierto.");
        if (!EstaAbierta(c))
            throw AppException.Conflict("Esta convocatoria ya cerró.");

        // Quién responde: con sesión, del perfil; sin sesión, lo que escribió.
        string nombre, correo;
        string? telefono;
        if (userId is Guid uid)
        {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == uid, ct)
                ?? throw AppException.Unauthorized("Tu sesión no es válida. Vuelve a entrar.");
            nombre = u.FullName;
            correo = u.Email;
            telefono = u.Phone;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
                throw AppException.BadRequest("Escribe tu nombre y tu correo para que podamos contactarte.");
            nombre = request.Name.Trim();
            correo = request.Email.Trim();
            telefono = Limpio(request.Phone);
        }

        var correoNorm = correo.ToLowerInvariant();
        if (await db.OpenCallResponses.AnyAsync(r => r.OpenCallId == id && r.Email.ToLower() == correoNorm, ct))
            throw AppException.Conflict("Ya respondiste a esta convocatoria con ese correo.");

        var respuesta = new OpenCallResponse
        {
            OpenCallId = id,
            UserId = userId,
            Name = nombre,
            Email = correo,
            Phone = telefono,
        };

        var porPregunta = request.Answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.Last());
        var preguntas = c.Questions.ToDictionary(q => q.Id);
        if (porPregunta.Keys.Any(k => !preguntas.ContainsKey(k)))
            throw AppException.BadRequest("Una de las respuestas no corresponde a esta convocatoria.");

        foreach (var q in c.Questions.OrderBy(q => q.Order))
        {
            porPregunta.TryGetValue(q.Id, out var a);
            var texto = Limpio(a?.Text);
            switch (q.Type)
            {
                case OpenCallQuestionType.Amount:
                    if (a?.Amount is null)
                    {
                        if (q.Required) throw AppException.BadRequest($"«{q.Label}»: escribe un monto.");
                        continue;
                    }
                    if (a.Amount < 0 || a.Amount > MaxMonto)
                        throw AppException.BadRequest($"«{q.Label}»: el monto tiene que estar entre 0 y {MaxMonto:N0}.");
                    respuesta.Answers.Add(new OpenCallAnswer { QuestionId = q.Id, Amount = Math.Round(a.Amount.Value, 2) });
                    break;

                default:
                    if (texto is null)
                    {
                        if (q.Required) throw AppException.BadRequest($"«{q.Label}» es obligatoria.");
                        continue;
                    }
                    var max = q.Type switch
                    {
                        OpenCallQuestionType.ShortText => MaxCorto,
                        OpenCallQuestionType.Link => MaxEnlace,
                        _ => MaxLargo,
                    };
                    if (texto.Length > max)
                        throw AppException.BadRequest($"«{q.Label}»: como mucho {max} caracteres.");
                    if (q.Type == OpenCallQuestionType.Link)
                        Enlaces.ExigirWeb(texto, $"«{q.Label}»");
                    respuesta.Answers.Add(new OpenCallAnswer { QuestionId = q.Id, Text = texto });
                    break;
            }
        }

        db.OpenCallResponses.Add(respuesta);
        // Quien la creó se entera de cada respuesta nueva.
        notifications.Notify(c.CreatedBy, "convocatoria.respuesta", $"Nueva respuesta a «{c.Title}» de {nombre}.");
        await db.SaveChangesAsync(ct);
    }

    // ── Ayudas ───────────────────────────────────────────────────

    private async Task ValidarAsync(SaveOpenCallRequest request, CancellationToken ct)
    {
        if (request.Questions.Count == 0)
            throw AppException.BadRequest("Añade al menos una pregunta.");
        if (request.EventId is Guid ev && !await db.Events.AnyAsync(e => e.Id == ev, ct))
            throw AppException.BadRequest("El evento elegido no existe.");
    }

    private static OpenCallQuestion Nueva(QuestionInput q, int orden) => new()
    {
        Label = q.Label.Trim(),
        Help = Limpio(q.Help),
        Type = q.Type,
        Required = q.Required,
        Order = orden,
    };

    private static bool EstaAbierta(OpenCall c) =>
        c.Status == OpenCallStatus.Open && (c.ClosesAt is null || c.ClosesAt > DateTime.UtcNow);

    private static List<QuestionDto> Preguntas(OpenCall c) =>
        c.Questions.OrderBy(q => q.Order).Select(q => new QuestionDto(q.Id, q.Label, q.Help, q.Type, q.Required)).ToList();

    private static PublicOpenCallDto Publica(OpenCall c) =>
        new(c.Id, c.Title, c.Description, c.ClosesAt, EstaAbierta(c), c.Event?.Title, c.Event?.Date, Preguntas(c));

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
