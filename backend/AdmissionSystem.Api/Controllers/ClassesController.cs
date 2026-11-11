using AdmissionSystem.Api.Contracts;
using AdmissionSystem.Api.Data;
using AdmissionSystem.Api.Domain;
using AdmissionSystem.Api.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdmissionSystem.Api.Controllers;

[ApiController]
[Route("classes")]
public sealed class ClassesController(AppDbContext db, TimeProvider timeProvider) : ControllerBase
{
    private DateTimeOffset UtcNow()
    {
        var t = timeProvider.GetUtcNow();
        return new DateTimeOffset(t.UtcTicks - t.UtcTicks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateClassRequest? request,
        CancellationToken cancellationToken)
    {
        var name = request?.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            return ApiProblem.BadRequest("Class name is required.");
        }

        if (name.Length > ClassEntity.NameMaxLength)
        {
            return ApiProblem.BadRequest($"Class name must be at most {ClassEntity.NameMaxLength} characters.");
        }

        if (request?.TotalSeats is not int totalSeats || totalSeats < 1)
        {
            return ApiProblem.BadRequest("totalSeats must be at least 1.");
        }

        var now = UtcNow();
        var entity = new ClassEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            TotalSeats = totalSeats,
            CreatedAt = now,
        };

        db.Classes.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        var response = new ClassCreatedResponse(entity.Id, entity.Name, entity.TotalSeats);
        return CreatedAtAction(nameof(GetById), new { classId = entity.Id.ToString() }, response);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var now = UtcNow();

        var rows = await db.Classes
            .AsNoTracking()
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.TotalSeats,
                HeldSeats = c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now),
                ConfirmedSeats = c.Holds.Count(h => h.Status == HoldStatus.Confirmed),
            })
            .ToListAsync(cancellationToken);

        var response = rows
            .Select(c => new ClassSeatsResponse(
                c.Id,
                c.Name,
                c.TotalSeats,
                c.HeldSeats,
                c.ConfirmedSeats,
                Math.Max(0, c.TotalSeats - c.HeldSeats - c.ConfirmedSeats)))
            .ToList();

        return Ok(response);
    }

    [HttpGet("{classId}")]
    public async Task<IActionResult> GetById(string classId, CancellationToken cancellationToken)
    {
        if (!RouteId.TryParse(classId, out var id))
        {
            return ApiProblem.NotFound("Class not found.");
        }

        var now = UtcNow();

        var row = await db.Classes
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.TotalSeats,
                HeldSeats = c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now),
                ConfirmedSeats = c.Holds.Count(h => h.Status == HoldStatus.Confirmed),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return ApiProblem.NotFound("Class not found.");
        }

        var response = new ClassSeatsResponse(
            row.Id,
            row.Name,
            row.TotalSeats,
            row.HeldSeats,
            row.ConfirmedSeats,
            Math.Max(0, row.TotalSeats - row.HeldSeats - row.ConfirmedSeats));

        return Ok(response);
    }
}
