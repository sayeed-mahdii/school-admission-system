using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdmissionSystem.Api.Contracts;
using AdmissionSystem.Api.Data;
using AdmissionSystem.Api.Domain;
using AdmissionSystem.Api.Http;
using AdmissionSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdmissionSystem.Tests;

public sealed class HoldsCancelTests(AdmissionApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<ClassCreatedResponse> CreateClassAsync(string name, int totalSeats)
    {
        var response = await Client.PostAsJsonAsync("/classes", new { name, totalSeats });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ClassCreatedResponse>();
        Assert.NotNull(created);
        return created;
    }

    private async Task<HoldCreatedResponse> ApplyAsync(Guid classId, string studentId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        request.Headers.Add(StudentHeader.HeaderName, studentId);
        var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var hold = await response.Content.ReadFromJsonAsync<HoldCreatedResponse>();
        Assert.NotNull(hold);
        return hold;
    }

    private HttpRequestMessage CreateCancelRequest(string holdId, string? studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}");
        if (studentId is not null)
        {
            request.Headers.Add(StudentHeader.HeaderName, studentId);
        }

        return request;
    }

    [Fact]
    public async Task Cancel_ActiveHold_Returns204IsIdempotentFreesSeatAndAllowsReapply()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 1);
        var firstHold = await ApplyAsync(createdClass.Id, "20152691234567890");

        using var cancelReq = CreateCancelRequest(firstHold.HoldId.ToString(), "20152691234567890");
        var cancelRes = await Client.SendAsync(cancelReq);
        Assert.Equal(HttpStatusCode.NoContent, cancelRes.StatusCode);

        // Idempotent second cancel also returns 204
        using var secondCancelReq = CreateCancelRequest(firstHold.HoldId.ToString(), "20152691234567890");
        var secondCancelRes = await Client.SendAsync(secondCancelReq);
        Assert.Equal(HttpStatusCode.NoContent, secondCancelRes.StatusCode);

        // Seat is immediately freed
        var seatsAfterCancel = await Client.GetFromJsonAsync<ClassSeatsResponse>($"/classes/{createdClass.Id}");
        Assert.NotNull(seatsAfterCancel);
        Assert.Equal(0, seatsAfterCancel.HeldSeats);
        Assert.Equal(1, seatsAfterCancel.AvailableSeats);

        // Hold status reads as CANCELLED
        var holdState = await Client.GetFromJsonAsync<HoldResponse>($"/holds/{firstHold.HoldId}");
        Assert.NotNull(holdState);
        Assert.Equal(HoldStatus.Cancelled, holdState.Status);

        // Same student can immediately apply again for the same class
        var secondHold = await ApplyAsync(createdClass.Id, "20152691234567890");
        Assert.NotEqual(firstHold.HoldId, secondHold.HoldId);
    }

    [Fact]
    public async Task Cancel_ConfirmedHold_OrExpiredHold_Returns409Conflict()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 5);

        // 1. Confirmed hold cannot be cancelled
        var confirmedHold = await ApplyAsync(createdClass.Id, "11111111111111111");
        using (var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{confirmedHold.HoldId}/confirm"))
        {
            confirmReq.Headers.Add(StudentHeader.HeaderName, "11111111111111111");
            var confirmRes = await Client.SendAsync(confirmReq);
            Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        }

        using (var cancelConfirmedReq = CreateCancelRequest(confirmedHold.HoldId.ToString(), "11111111111111111"))
        {
            var cancelConfirmedRes = await Client.SendAsync(cancelConfirmedReq);
            Assert.Equal(HttpStatusCode.Conflict, cancelConfirmedRes.StatusCode);
        }

        // 2. Expired hold cannot be cancelled (and persists EXPIRED status)
        var expiringHold = await ApplyAsync(createdClass.Id, "22222222222222222");
        Clock.Advance(TimeSpan.FromSeconds(601));

        using (var cancelExpiredReq = CreateCancelRequest(expiringHold.HoldId.ToString(), "22222222222222222"))
        {
            var cancelExpiredRes = await Client.SendAsync(cancelExpiredReq);
            Assert.Equal(HttpStatusCode.Conflict, cancelExpiredRes.StatusCode);

            using var doc = JsonDocument.Parse(await cancelExpiredRes.Content.ReadAsStringAsync());
            Assert.Equal(ErrorCodes.HoldExpired, doc.RootElement.GetProperty("code").GetString());
        }

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.Holds.AsNoTracking().SingleAsync(h => h.Id == expiringHold.HoldId);
        Assert.Equal(HoldStatus.Expired, persisted.Status);
    }

    [Fact]
    public async Task Cancel_AnotherStudentAttemptingToCancel_Returns403Forbidden()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 5);
        var hold = await ApplyAsync(createdClass.Id, "11111111111111111");

        using var req = CreateCancelRequest(hold.HoldId.ToString(), "22222222222222222");
        var res = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Cancel_MissingHeaderReturns400_AndUnknownOrMalformedIdReturns404()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 5);
        var hold = await ApplyAsync(createdClass.Id, "11111111111111111");

        using var missingHeaderReq = CreateCancelRequest(hold.HoldId.ToString(), null);
        var missingHeaderRes = await Client.SendAsync(missingHeaderReq);
        Assert.Equal(HttpStatusCode.BadRequest, missingHeaderRes.StatusCode);

        using var malformedIdReq = CreateCancelRequest("not-a-guid", "11111111111111111");
        var malformedIdRes = await Client.SendAsync(malformedIdReq);
        Assert.Equal(HttpStatusCode.NotFound, malformedIdRes.StatusCode);

        using var unknownIdReq = CreateCancelRequest(Guid.NewGuid().ToString(), "11111111111111111");
        var unknownIdRes = await Client.SendAsync(unknownIdReq);
        Assert.Equal(HttpStatusCode.NotFound, unknownIdRes.StatusCode);
    }
}
