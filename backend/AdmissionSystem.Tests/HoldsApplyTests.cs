using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdmissionSystem.Api.Contracts;
using AdmissionSystem.Api.Http;
using AdmissionSystem.Tests.Infrastructure;

namespace AdmissionSystem.Tests;

public sealed class HoldsApplyTests(AdmissionApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<ClassCreatedResponse> CreateClassAsync(string name, int totalSeats)
    {
        var response = await Client.PostAsJsonAsync("/classes", new { name, totalSeats });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ClassCreatedResponse>();
        Assert.NotNull(created);
        return created;
    }

    private HttpRequestMessage CreateHoldRequest(string classId, string? studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        if (studentId is not null)
        {
            request.Headers.Add(StudentHeader.HeaderName, studentId);
        }

        return request;
    }

    [Fact]
    public async Task Apply_ValidRequest_Returns201WithExactContractAndTrailingZTimestamps()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 10);

        using var request = CreateHoldRequest(createdClass.Id.ToString(), "  20152691234567890  ");
        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var propertyNames = root.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            ["classId", "expiresAt", "holdId", "serverTime", "status", "studentId"],
            propertyNames);

        Assert.True(Guid.TryParse(root.GetProperty("holdId").GetString(), out var holdId));
        Assert.NotEqual(Guid.Empty, holdId);
        Assert.Equal(createdClass.Id, root.GetProperty("classId").GetGuid());
        Assert.Equal("20152691234567890", root.GetProperty("studentId").GetString());
        Assert.Equal("HELD", root.GetProperty("status").GetString());

        var expiresAtRaw = root.GetProperty("expiresAt").GetString();
        var serverTimeRaw = root.GetProperty("serverTime").GetString();
        Assert.NotNull(expiresAtRaw);
        Assert.NotNull(serverTimeRaw);
        Assert.EndsWith("Z", expiresAtRaw);
        Assert.EndsWith("Z", serverTimeRaw);
        Assert.Equal("2026-11-10T12:10:00Z", expiresAtRaw);
        Assert.Equal("2026-11-10T12:00:00Z", serverTimeRaw);
    }

    [Fact]
    public async Task Apply_DuplicateActiveHoldForSameStudentAndClass_Returns409DuplicateHold()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 5);

        using var firstReq = CreateHoldRequest(createdClass.Id.ToString(), "20152691234567890");
        var firstRes = await Client.SendAsync(firstReq);
        Assert.Equal(HttpStatusCode.Created, firstRes.StatusCode);

        using var secondReq = CreateHoldRequest(createdClass.Id.ToString(), "20152691234567890");
        var secondRes = await Client.SendAsync(secondReq);
        Assert.Equal(HttpStatusCode.Conflict, secondRes.StatusCode);

        using var doc = JsonDocument.Parse(await secondRes.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.DuplicateHold, doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Apply_AfterHoldExpires_SameStudentCanReapplyForSameClassWithoutUniqueIndexFailure()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 1);

        using var firstReq = CreateHoldRequest(createdClass.Id.ToString(), "20152691234567890");
        var firstRes = await Client.SendAsync(firstReq);
        Assert.Equal(HttpStatusCode.Created, firstRes.StatusCode);
        var firstHold = await firstRes.Content.ReadFromJsonAsync<HoldCreatedResponse>();
        Assert.NotNull(firstHold);

        // Advance fake clock past the 600-second expiry window
        Clock.Advance(TimeSpan.FromSeconds(601));

        using var secondReq = CreateHoldRequest(createdClass.Id.ToString(), "20152691234567890");
        var secondRes = await Client.SendAsync(secondReq);
        Assert.Equal(HttpStatusCode.Created, secondRes.StatusCode);
        var secondHold = await secondRes.Content.ReadFromJsonAsync<HoldCreatedResponse>();
        Assert.NotNull(secondHold);
        Assert.NotEqual(firstHold.HoldId, secondHold.HoldId);

        var classSeats = await Client.GetFromJsonAsync<ClassSeatsResponse>($"/classes/{createdClass.Id}");
        Assert.NotNull(classSeats);
        Assert.Equal(1, classSeats.HeldSeats);
        Assert.Equal(0, classSeats.AvailableSeats);
    }

    [Fact]
    public async Task Apply_SameStudentCanHoldSeatsInTwoDifferentClassesSimultaneously()
    {
        var morningShift = await CreateClassAsync("Class 6 - Morning Shift", 10);
        var dayShift = await CreateClassAsync("Class 6 - Day Shift", 10);

        using var req1 = CreateHoldRequest(morningShift.Id.ToString(), "20152691234567890");
        var res1 = await Client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        using var req2 = CreateHoldRequest(dayShift.Id.ToString(), "20152691234567890");
        var res2 = await Client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.Created, res2.StatusCode);
    }

    [Fact]
    public async Task Apply_UnknownOrMalformedClassIdReturns404_AndMissingHeaderReturns400()
    {
        using var malformedIdReq = CreateHoldRequest("not-a-guid", "20152691234567890");
        var malformedIdRes = await Client.SendAsync(malformedIdReq);
        Assert.Equal(HttpStatusCode.NotFound, malformedIdRes.StatusCode);

        using var unknownIdReq = CreateHoldRequest(Guid.NewGuid().ToString(), "20152691234567890");
        var unknownIdRes = await Client.SendAsync(unknownIdReq);
        Assert.Equal(HttpStatusCode.NotFound, unknownIdRes.StatusCode);

        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 5);

        using var missingHeaderReq = CreateHoldRequest(createdClass.Id.ToString(), null);
        var missingHeaderRes = await Client.SendAsync(missingHeaderReq);
        Assert.Equal(HttpStatusCode.BadRequest, missingHeaderRes.StatusCode);

        using var blankHeaderReq = CreateHoldRequest(createdClass.Id.ToString(), "   ");
        var blankHeaderRes = await Client.SendAsync(blankHeaderReq);
        Assert.Equal(HttpStatusCode.BadRequest, blankHeaderRes.StatusCode);
    }

    [Fact]
    public async Task Apply_Concurrency_200ParallelRequestsFor20SeatClass_YieldsExactly20CreatedAnd180Conflict()
    {
        const int totalSeats = 20;
        const int totalRequests = 200;

        var createdClass = await CreateClassAsync("Class 6 - High Concurrency Shift", totalSeats);

        using var startGate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(1, totalRequests).Select(async index =>
        {
            await Task.Yield();
            startGate.Wait();

            var studentId = index.ToString("D17");
            using var request = CreateHoldRequest(createdClass.Id.ToString(), studentId);
            var response = await Client.SendAsync(request);
            return response.StatusCode;
        }).ToArray();

        startGate.Set();
        var statuses = await Task.WhenAll(tasks);

        var createdCount = statuses.Count(s => s == HttpStatusCode.Created);
        var conflictCount = statuses.Count(s => s == HttpStatusCode.Conflict);
        var serverErrorCount = statuses.Count(s => (int)s >= 500);

        Assert.Equal(0, serverErrorCount);
        Assert.Equal(totalSeats, createdCount);
        Assert.Equal(totalRequests - totalSeats, conflictCount);

        var classState = await Client.GetFromJsonAsync<ClassSeatsResponse>($"/classes/{createdClass.Id}");
        Assert.NotNull(classState);
        Assert.Equal(totalSeats, classState.TotalSeats);
        Assert.Equal(totalSeats, classState.HeldSeats);
        Assert.Equal(0, classState.ConfirmedSeats);
        Assert.Equal(0, classState.AvailableSeats);
    }
}
