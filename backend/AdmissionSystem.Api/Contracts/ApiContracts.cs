namespace AdmissionSystem.Api.Contracts;

// Response/request DTOs are kept separate from the EF entities so the JSON contract
// (brief section 1.4) never changes by accident when the entity shape changes.
// Property names serialize as camelCase. Timestamps are UTC DateTime values so they
// serialize with a trailing "Z" (a DateTimeOffset would serialize as "+00:00").

/// <summary>Request body for <c>POST /classes</c>.</summary>
public sealed record CreateClassRequest(string? Name, int? TotalSeats);

/// <summary>201 body for <c>POST /classes</c>: exactly <c>{ id, name, totalSeats }</c>.</summary>
public sealed record ClassCreatedResponse(Guid Id, string Name, int TotalSeats);

/// <summary>Item of <c>GET /classes</c> and body of <c>GET /classes/{classId}</c>.</summary>
public sealed record ClassSeatsResponse(
    Guid Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);

/// <summary>201 body for <c>POST /classes/{classId}/holds</c>.</summary>
public sealed record HoldCreatedResponse(
    Guid HoldId,
    Guid ClassId,
    string StudentId,
    string Status,
    DateTime ExpiresAt,
    DateTime ServerTime);

/// <summary>200 body for <c>GET /holds/{holdId}</c>.</summary>
public sealed record HoldResponse(
    Guid HoldId,
    Guid ClassId,
    string Status,
    DateTime ExpiresAt,
    DateTime ServerTime);

/// <summary>200 body for <c>POST /holds/{holdId}/confirm</c>.</summary>
public sealed record HoldConfirmedResponse(Guid HoldId, string Status);

/// <summary>Item of <c>GET /me/holds</c>.</summary>
public sealed record MyHoldResponse(
    Guid HoldId,
    Guid ClassId,
    string ClassName,
    string Status,
    DateTime ExpiresAt,
    DateTime ServerTime);

