using Boeshiri.Application.Common;
using Boeshiri.Application.OpenCalls;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Notifications;
using Boeshiri.Infrastructure.OpenCalls;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.OpenCalls;

public class OpenCallServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private static OpenCallService NewService(BoeshiriDbContext c) => new(c, new NotificationService(c), new AuditLogger(c));

    private static SaveOpenCallRequest Convocatoria(params QuestionInput[] preguntas) => new()
    {
        Title = "Tallerista para ARCANA",
        Description = "Buscamos quien guíe la edición.",
        Questions = preguntas.Length > 0 ? [.. preguntas] :
        [
            new() { Label = "Tu propuesta", Type = OpenCallQuestionType.LongText, Required = true },
            new() { Label = "Tu trabajo", Type = OpenCallQuestionType.Link },
            new() { Label = "Honorarios", Type = OpenCallQuestionType.Amount, Required = true },
        ],
    };

    /// <summary>Crea y abre una convocatoria; devuelve su id y los ids de sus preguntas en orden.</summary>
    private async Task<(Guid Id, Guid[] Q, Guid Autor)> AbiertaAsync()
    {
        var autor = await UsuarioAsync("junta@ex.com", "Junta");
        await using var c = _db.CreateContext();
        var svc = NewService(c);
        var id = await svc.CreateAsync(autor, Convocatoria());
        await svc.ChangeStatusAsync(id, autor, new() { Status = OpenCallStatus.Open });
        var q = await c.OpenCallQuestions.Where(x => x.OpenCallId == id).OrderBy(x => x.Order).Select(x => x.Id).ToArrayAsync();
        return (id, q, autor);
    }

    private static SubmitResponseRequest Respuesta(Guid[] q, string? nombre = "Ana", string? correo = "ana@ex.com", string? enlace = "https://ana.art", decimal? monto = 150) => new()
    {
        Name = nombre,
        Email = correo,
        Answers =
        [
            new() { QuestionId = q[0], Text = "Un taller de tarot ilustrado." },
            new() { QuestionId = q[1], Text = enlace },
            new() { QuestionId = q[2], Amount = monto },
        ],
    };

    [Fact]
    public async Task SubmitAsync_Anonymous_SavesAnswersAndNotifiesCreator()
    {
        var (id, q, autor) = await AbiertaAsync();
        await using (var c = _db.CreateContext())
            await NewService(c).SubmitAsync(id, null, Respuesta(q));

        await using var check = _db.CreateContext();
        var r = await check.OpenCallResponses.Include(x => x.Answers).SingleAsync();
        Assert.Equal("Ana", r.Name);
        Assert.Null(r.UserId);
        Assert.Equal(3, r.Answers.Count);
        Assert.Equal(150m, r.Answers.Single(a => a.QuestionId == q[2]).Amount);
        Assert.True(await check.Notifications.AnyAsync(n => n.UserId == autor && n.Type == "convocatoria.respuesta"));
    }

    [Fact]
    public async Task SubmitAsync_LoggedIn_TakesNameAndContactFromProfile()
    {
        var (id, q, _) = await AbiertaAsync();
        var miembro = await UsuarioAsync("gio@ex.com", "Gio Miembro", "+50760000000");
        await using (var c = _db.CreateContext())
            await NewService(c).SubmitAsync(id, miembro, Respuesta(q, nombre: null, correo: null));

        await using var check = _db.CreateContext();
        var r = await check.OpenCallResponses.SingleAsync();
        Assert.Equal("Gio Miembro", r.Name);
        Assert.Equal("gio@ex.com", r.Email);
        Assert.Equal("+50760000000", r.Phone);
        Assert.Equal(miembro, r.UserId);
    }

    [Fact]
    public async Task SubmitAsync_AnonymousWithoutContact_ThrowsBadRequest()
    {
        var (id, q, _) = await AbiertaAsync();
        await using var c = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(c).SubmitAsync(id, null, Respuesta(q, nombre: null)));
        Assert.Equal(400, ex.StatusCode);
    }

    [Theory]
    [InlineData("javascript:alert(1)", 150.0)] // enlace que no es web
    [InlineData("https://ana.art", -5.0)]      // monto negativo
    [InlineData("https://ana.art", null)]      // monto obligatorio vacío
    public async Task SubmitAsync_InvalidAnswer_ThrowsBadRequest(string enlace, double? monto)
    {
        var (id, q, _) = await AbiertaAsync();
        await using var c = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(c).SubmitAsync(id, null, Respuesta(q, enlace: enlace, monto: monto is null ? null : (decimal)monto)));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task SubmitAsync_SameEmailTwice_ThrowsConflict()
    {
        var (id, q, _) = await AbiertaAsync();
        await using (var c = _db.CreateContext())
            await NewService(c).SubmitAsync(id, null, Respuesta(q));
        await using var c2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(c2).SubmitAsync(id, null, Respuesta(q, correo: "ANA@ex.com")));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task SubmitAsync_ClosedOrDraft_IsRejected()
    {
        var (id, q, autor) = await AbiertaAsync();
        await using (var c = _db.CreateContext())
            await NewService(c).ChangeStatusAsync(id, autor, new() { Status = OpenCallStatus.Closed });
        await using (var c = _db.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<AppException>(() => NewService(c).SubmitAsync(id, null, Respuesta(q)));
            Assert.Equal(409, ex.StatusCode);
        }

        Guid borrador;
        await using (var c = _db.CreateContext())
            borrador = await NewService(c).CreateAsync(autor, Convocatoria());
        await using var c3 = _db.CreateContext();
        var nf = await Assert.ThrowsAsync<AppException>(() => NewService(c3).GetPublicAsync(borrador));
        Assert.Equal(404, nf.StatusCode);
    }

    [Fact]
    public async Task SubmitAsync_Honeypot_SilentlyDiscards()
    {
        var (id, q, _) = await AbiertaAsync();
        await using (var c = _db.CreateContext())
            await NewService(c).SubmitAsync(id, null, Respuesta(q) with { Website = "http://spam" });
        await using var check = _db.CreateContext();
        Assert.False(await check.OpenCallResponses.AnyAsync());
    }

    [Fact]
    public async Task UpdateAsync_WithResponses_CannotRemoveOrRetypeQuestions_ButCanAdd()
    {
        var (id, q, autor) = await AbiertaAsync();
        await using (var c = _db.CreateContext())
            await NewService(c).SubmitAsync(id, null, Respuesta(q));

        // Quitar la pregunta del enlace → 409
        await using (var c = _db.CreateContext())
        {
            var sinEnlace = Convocatoria(
                new() { Id = q[0], Label = "Tu propuesta", Type = OpenCallQuestionType.LongText, Required = true },
                new() { Id = q[2], Label = "Honorarios", Type = OpenCallQuestionType.Amount, Required = true });
            var ex = await Assert.ThrowsAsync<AppException>(() => NewService(c).UpdateAsync(id, autor, sinEnlace));
            Assert.Equal(409, ex.StatusCode);
        }
        // Cambiar el tipo de una con respuestas → 409
        await using (var c = _db.CreateContext())
        {
            var cambiada = Convocatoria(
                new() { Id = q[0], Label = "Tu propuesta", Type = OpenCallQuestionType.ShortText, Required = true },
                new() { Id = q[1], Label = "Tu trabajo", Type = OpenCallQuestionType.Link },
                new() { Id = q[2], Label = "Honorarios", Type = OpenCallQuestionType.Amount, Required = true });
            var ex = await Assert.ThrowsAsync<AppException>(() => NewService(c).UpdateAsync(id, autor, cambiada));
            Assert.Equal(409, ex.StatusCode);
        }
        // Reescribir y añadir → bien
        await using (var c = _db.CreateContext())
        {
            var ampliada = Convocatoria(
                new() { Id = q[0], Label = "Cuéntanos tu propuesta", Type = OpenCallQuestionType.LongText, Required = true },
                new() { Id = q[1], Label = "Tu trabajo", Type = OpenCallQuestionType.Link },
                new() { Id = q[2], Label = "Honorarios", Type = OpenCallQuestionType.Amount, Required = true },
                new() { Label = "¿Qué materiales necesitas?", Type = OpenCallQuestionType.ShortText });
            await NewService(c).UpdateAsync(id, autor, ampliada);
        }
        await using var check = _db.CreateContext();
        Assert.Equal(4, await check.OpenCallQuestions.CountAsync(x => x.OpenCallId == id));
        Assert.Equal("Cuéntanos tu propuesta", (await check.OpenCallQuestions.SingleAsync(x => x.Id == q[0])).Label);
    }

    [Fact]
    public async Task ReviewAsync_SetsStatusNoteAndShowsInAdminView()
    {
        var (id, q, autor) = await AbiertaAsync();
        await using (var c = _db.CreateContext())
            await NewService(c).SubmitAsync(id, null, Respuesta(q));
        Guid rid;
        await using (var c = _db.CreateContext())
            rid = (await c.OpenCallResponses.SingleAsync()).Id;
        await using (var c = _db.CreateContext())
            await NewService(c).ReviewAsync(rid, autor, new() { Status = OpenCallResponseStatus.Shortlisted, Note = "Buena propuesta, pedir presupuesto" });

        await using var read = _db.CreateContext();
        var svc = NewService(read);
        var detalle = await svc.GetAdminAsync(id);
        var r = Assert.Single(detalle.Responses);
        Assert.Equal(OpenCallResponseStatus.Shortlisted, r.Status);
        Assert.Equal("Buena propuesta, pedir presupuesto", r.Note);
        var resumen = Assert.Single(await svc.ListAsync());
        Assert.Equal(1, resumen.ResponseCount);
        Assert.Equal(0, resumen.NewCount);
    }

    [Fact]
    public async Task DeleteAsync_WithResponses_ThrowsConflict()
    {
        var (id, q, autor) = await AbiertaAsync();
        await using (var c = _db.CreateContext())
            await NewService(c).SubmitAsync(id, null, Respuesta(q));
        await using var c2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(c2).DeleteAsync(id, autor));
        Assert.Equal(409, ex.StatusCode);
    }

    private async Task<Guid> UsuarioAsync(string email, string nombre, string? telefono = null)
    {
        await using var c = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = nombre, Phone = telefono, EmailVerified = true, Status = MemberStatus.Active };
        c.Users.Add(u);
        await c.SaveChangesAsync();
        return u.Id;
    }

    public void Dispose() => _db.Dispose();
}
