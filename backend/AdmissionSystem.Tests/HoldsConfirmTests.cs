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

public sealed class HoldsConfirmTests(AdmissionApiFactory factory) : IntegrationTestBase(factory)
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

    private HttpRequestMessage CreateConfirmRequest(string holdId, string? studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
        if (studentId is not null)
        {
            request.Headers.Add(StudentHeader.HeaderName, studentId);
        }

        return request;
    }

    [Fact]
    public async Task Confirm_HappyPath_ConfirmsHoldWithExactContract_AndShiftsCountFromHeldToConfirmed()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 5);
        var hold = await ApplyAsync(createdClass.Id, "20152691234567890");

        var beforeSeats = await Client.GetFromJsonAsync<ClassSeatsResponse>($"/classes/{createdClass.Id}");
        Assert.NotNull(beforeSeats);
        Assert.Equal(1, beforeSeats.HeldSeats);
        Assert.Equal(0, beforeSeats.ConfirmedSeats);
        Assert.Equal(4, beforeSeats.AvailableSeats);

        using var confirmReq = CreateConfirmRequest(hold.HoldId.ToString(), "20152691234567890");
        var confirmRes = await Client.SendAsync(confirmReq);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);

        using (var doc = JsonDocument.Parse(await confirmRes.Content.ReadAsStringAsync()))
        {
            var props = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
            Assert.Equal(["holdId", "status"], props);
            Assert.Equal(hold.HoldId, doc.RootElement.GetProperty("holdId").GetGuid());
            Assert.Equal(HoldStatus.Confirmed, doc.RootElement.GetProperty("status").GetString());
        }

        var afterSeats = await Client.GetFromJsonAsync<ClassSeatsResponse>($"/classes/{createdClass.Id}");
        Assert.NotNull(afterSeats);
        Assert.Equal(0, afterSeats.HeldSeats);
        Assert.Equal(1, afterSeats.ConfirmedSeats);
        Assert.Equal(4, afterSeats.AvailableSeats);
    }

    [Fact]
    public async Task Confirm_Idempotent_ConfirmingSameHoldTwiceReturns200BothTimes_AndIncrementsConfirmedSeatsOnce()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 5);
        var hold = await ApplyAsync(createdClass.Id, "20152691234567890");

        using var firstReq = CreateConfirmRequest(hold.HoldId.ToString(), "20152691234567890");
        var firstRes = await Client.SendAsync(firstReq);
        Assert.Equal(HttpStatusCode.OK, firstRes.StatusCode);

        // Even after the original hold expiry window passes, a CONFIRMED hold stays CONFIRMED
        Clock.Advance(TimeSpan.FromMinutes(20));

        using var secondReq = CreateConfirmRequest(hold.HoldId.ToString(), "20152691234567890");
        var secondRes = await Client.SendAsync(secondReq);
        Assert.Equal(HttpStatusCode.OK, secondRes.StatusCode);

        var body = await secondRes.Content.ReadFromJsonAsync<HoldConfirmedResponse>();
        Assert.NotNull(body);
        Assert.Equal(HoldStatus.Confirmed, body.Status);

        var seats = await Client.GetFromJsonAsync<ClassSeatsResponse>($"/classes/{createdClass.Id}");
        Assert.NotNull(seats);
        Assert.Equal(0, seats.HeldSeats);
        Assert.Equal(1, seats.ConfirmedSeats);
        Assert.Equal(4, seats.AvailableSeats);
    }

    [Fact]
    public async Task Confirm_AnotherStudentAttemptingToConfirm_Returns403Forbidden()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 5);
        var hold = await ApplyAsync(createdClass.Id, "11111111111111111");

        using var req = CreateConfirmRequest(hold.HoldId.ToString(), "22222222222222222");
        var res = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.Forbidden, doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Confirm_AfterHoldExpires_PersistsExpiredStatusAndReturns409Conflict()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 5);
        var hold = await ApplyAsync(createdClass.Id, "20152691234567890");

        Clock.Advance(TimeSpan.FromSeconds(601));

        using var req = CreateConfirmRequest(hold.HoldId.ToString(), "20152691234567890");
        var res = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        using (var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync()))
        {
            Assert.Equal(ErrorCodes.HoldExpired, doc.RootElement.GetProperty("code").GetString());
        }

        // Verify the flip to EXPIRED was committed in the database before returning 409
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persisted = await db.Holds.AsNoTracking().SingleAsync(h => h.Id == hold.HoldId);
            Assert.Equal(HoldStatus.Expired, persisted.Status);
        }

        var holdAfter = await Client.GetFromJsonAsync<HoldResponse>($"/holds/{hold.HoldId}");
        Assert.NotNull(holdAfter);
        Assert.Equal(HoldStatus.Expired, holdAfter.Status);
    }

    [Fact]
    public async Task Confirm_MissingHeaderReturns400_AndUnknownOrMalformedIdReturns404()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 5);
        var hold = await ApplyAsync(createdClass.Id, "20152691234567890");

        using var missingHeaderReq = CreateConfirmRequest(hold.HoldId.ToString(), null);
        var missingHeaderRes = await Client.SendAsync(missingHeaderReq);
        Assert.Equal(HttpStatusCode.BadRequest, missingHeaderRes.StatusCode);

        using var malformedIdReq = CreateConfirmRequest("not-a-guid", "20152691234567890");
        var malformedIdRes = await Client.SendAsync(malformedIdReq);
        Assert.Equal(HttpStatusCode.NotFound, malformedIdRes.StatusCode);

        using var unknownIdReq = CreateConfirmRequest(Guid.NewGuid().ToString(), "20152691234567890");
        var unknownIdRes = await Client.SendAsync(unknownIdReq);
        Assert.Equal(HttpStatusCode.NotFound, unknownIdRes.StatusCode);
    }
}
