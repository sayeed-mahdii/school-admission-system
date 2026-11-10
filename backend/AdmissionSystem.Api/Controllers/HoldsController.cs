using AdmissionSystem.Api.Configuration;
using AdmissionSystem.Api.Contracts;
using AdmissionSystem.Api.Data;
using AdmissionSystem.Api.Domain;
using AdmissionSystem.Api.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdmissionSystem.Api.Controllers;

[ApiController]
public sealed class HoldsController(
    AppDbContext db,
    TimeProvider timeProvider,
    IOptions<HoldOptions> holdOptions) : ControllerBase
{
    [HttpPost("/classes/{classId}/holds")]
    public async Task<IActionResult> Apply(string classId, CancellationToken cancellationToken)
    {
        if (!StudentHeader.TryGetStudentId(Request, out var studentId, out var headerError))
        {
            return headerError!;
        }

        if (!RouteId.TryParse(classId, out var parsedClassId))
        {
            return ApiProblem.NotFound("Class not found.");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var lockedClasses = await db.Classes
            .FromSqlInterpolated($"SELECT * FROM \"Classes\" WHERE \"Id\" = {parsedClassId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var targetClass = lockedClasses.SingleOrDefault();
        if (targetClass is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return ApiProblem.NotFound("Class not found.");
        }

        // First inside the lock: flip any expired HELD rows for this class to EXPIRED so a
        // re-applying student never trips the partial unique index on (ClassId, StudentId).
        await db.Holds
            .Where(h => h.ClassId == parsedClassId && h.Status == HoldStatus.Held && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(h => h.Status, HoldStatus.Expired),
                cancellationToken);

        var hasActiveOrConfirmed = await db.Holds.AnyAsync(
            h => h.ClassId == parsedClassId
                 && h.StudentId == studentId
                 && (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed),
            cancellationToken);
        if (hasActiveOrConfirmed)
        {
            await tx.CommitAsync(cancellationToken);
            return ApiProblem.Conflict(
                ErrorCodes.DuplicateHold,
                "This student already has an active or confirmed application for this class.");
        }

        var occupiedSeats = await db.Holds.CountAsync(
            h => h.ClassId == parsedClassId
                 && ((h.Status == HoldStatus.Held && h.ExpiresAt > now)
                     || h.Status == HoldStatus.Confirmed),
            cancellationToken);
        if (occupiedSeats >= targetClass.TotalSeats)
        {
            await tx.CommitAsync(cancellationToken);
            return ApiProblem.Conflict(
                ErrorCodes.NoSeats,
                "No seats are currently available in this class.");
        }

        var hold = new Hold
        {
            Id = Guid.NewGuid(),
            ClassId = parsedClassId,
            StudentId = studentId,
            Status = HoldStatus.Held,
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(holdOptions.Value.DurationSeconds),
        };

        db.Holds.Add(hold);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var response = new HoldCreatedResponse(
            hold.Id,
            hold.ClassId,
            hold.StudentId,
            hold.Status,
            hold.ExpiresAt.UtcDateTime,
            now.UtcDateTime);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("/holds/{holdId}")]
    public async Task<IActionResult> GetById(string holdId, CancellationToken cancellationToken)
    {
        if (!RouteId.TryParse(holdId, out var parsedHoldId))
        {
            return ApiProblem.NotFound("Hold not found.");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();

        var hold = await db.Holds
            .AsNoTracking()
            .Where(h => h.Id == parsedHoldId)
            .Select(h => new
            {
                h.Id,
                h.ClassId,
                h.Status,
                h.ExpiresAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (hold is null)
        {
            return ApiProblem.NotFound("Hold not found.");
        }

        var response = new HoldResponse(
            hold.Id,
            hold.ClassId,
            HoldStatus.Effective(hold.Status, hold.ExpiresAt, now),
            hold.ExpiresAt.UtcDateTime,
            now.UtcDateTime);

        return Ok(response);
    }

    [HttpGet("/me/holds")]
    public async Task<IActionResult> ListMyHolds(CancellationToken cancellationToken)
    {
        if (!StudentHeader.TryGetStudentId(Request, out var studentId, out var headerError))
        {
            return headerError!;
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();

        var rows = await db.Holds
            .AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ThenByDescending(h => h.Id)
            .Select(h => new
            {
                h.Id,
                h.ClassId,
                ClassName = h.Class != null ? h.Class.Name : string.Empty,
                h.Status,
                h.ExpiresAt,
            })
            .ToListAsync(cancellationToken);

        var response = rows
            .Select(h => new MyHoldResponse(
                h.Id,
                h.ClassId,
                h.ClassName,
                HoldStatus.Effective(h.Status, h.ExpiresAt, now),
                h.ExpiresAt.UtcDateTime,
                now.UtcDateTime))
            .ToList();

        return Ok(response);
    }
}
