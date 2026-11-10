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

public sealed class HoldsReadTests(AdmissionApiFactory factory) : IntegrationTestBase(factory)
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

    [Fact]
    public async Task GetHoldById_AndGetMyHolds_ReflectEffectiveExpiredStatusImmediatelyWhenClockAdvances()
    {
        var createdClass = await CreateClassAsync("Class 6 - Morning Shift", 1);
        var hold = await ApplyAsync(createdClass.Id, "20152691234567890");

        // Before expiry: GET /holds/{id} requires no header and reports HELD with exact contract
        var beforeRes = await Client.GetAsync($"/holds/{hold.HoldId}");
        Assert.Equal(HttpStatusCode.OK, beforeRes.StatusCode);

        using (var doc = JsonDocument.Parse(await beforeRes.Content.ReadAsStringAsync()))
        {
            var props = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
            Assert.Equal(["classId", "expiresAt", "holdId", "serverTime", "status"], props);
            Assert.Equal(HoldStatus.Held, doc.RootElement.GetProperty("status").GetString());
            Assert.EndsWith("Z", doc.RootElement.GetProperty("expiresAt").GetString()!);
            Assert.EndsWith("Z", doc.RootElement.GetProperty("serverTime").GetString()!);
        }

        // Advance fake clock past expiresAt (600s) with zero write calls
        Clock.Advance(TimeSpan.FromSeconds(601));

        // 1. GET /holds/{id} immediately reports EXPIRED
        var afterHold = await Client.GetFromJsonAsync<HoldResponse>($"/holds/{hold.HoldId}");
        Assert.NotNull(afterHold);
        Assert.Equal(HoldStatus.Expired, afterHold.Status);

        // 2. GET /me/holds immediately reports EXPIRED with exact contract
        using var myHoldsReq = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        myHoldsReq.Headers.Add(StudentHeader.HeaderName, "20152691234567890");
        var myHoldsRes = await Client.SendAsync(myHoldsReq);
        Assert.Equal(HttpStatusCode.OK, myHoldsRes.StatusCode);

        using (var myDoc = JsonDocument.Parse(await myHoldsRes.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, myDoc.RootElement.GetArrayLength());
            var item = myDoc.RootElement[0];
            var props = item.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
            Assert.Equal(["classId", "className", "expiresAt", "holdId", "serverTime", "status"], props);
            Assert.Equal("Class 6 - Morning Shift", item.GetProperty("className").GetString());
            Assert.Equal(HoldStatus.Expired, item.GetProperty("status").GetString());
            Assert.EndsWith("Z", item.GetProperty("expiresAt").GetString()!);
            Assert.EndsWith("Z", item.GetProperty("serverTime").GetString()!);
        }

        // 3. GET /classes/{id} immediately reports the seat as available
        var classAfter = await Client.GetFromJsonAsync<ClassSeatsResponse>($"/classes/{createdClass.Id}");
        Assert.NotNull(classAfter);
        Assert.Equal(0, classAfter.HeldSeats);
        Assert.Equal(1, classAfter.AvailableSeats);

        // Confirm zero write calls occurred during all three GETs (DB row is still HELD)
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rawHold = await db.Holds.AsNoTracking().SingleAsync(h => h.Id == hold.HoldId);
        Assert.Equal(HoldStatus.Held, rawHold.Status);
    }

    [Fact]
    public async Task GetMyHolds_ReturnsOnlyRequestingStudentsHoldsNewestFirst_AndReturns400WhenHeaderMissing()
    {
        var morning = await CreateClassAsync("Class 6 - Morning Shift", 10);
        var day = await CreateClassAsync("Class 6 - Day Shift", 10);

        var firstHold = await ApplyAsync(morning.Id, "11111111111111111");
        Clock.Advance(TimeSpan.FromSeconds(10));
        var secondHold = await ApplyAsync(day.Id, "11111111111111111");
        Clock.Advance(TimeSpan.FromSeconds(10));
        _ = await ApplyAsync(morning.Id, "22222222222222222");

        using var req = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        req.Headers.Add(StudentHeader.HeaderName, "11111111111111111");
        var res = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var holds = await res.Content.ReadFromJsonAsync<List<MyHoldResponse>>();
        Assert.NotNull(holds);
        Assert.Equal(2, holds.Count);
        Assert.Equal(secondHold.HoldId, holds[0].HoldId);
        Assert.Equal("Class 6 - Day Shift", holds[0].ClassName);
        Assert.Equal(firstHold.HoldId, holds[1].HoldId);
        Assert.Equal("Class 6 - Morning Shift", holds[1].ClassName);

        // Missing or blank header returns 400
        var missingHeaderRes = await Client.GetAsync("/me/holds");
        Assert.Equal(HttpStatusCode.BadRequest, missingHeaderRes.StatusCode);

        using var blankHeaderReq = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        blankHeaderReq.Headers.Add(StudentHeader.HeaderName, "   ");
        var blankHeaderRes = await Client.SendAsync(blankHeaderReq);
        Assert.Equal(HttpStatusCode.BadRequest, blankHeaderRes.StatusCode);
    }

    [Theory]
    [InlineData("does-not-exist")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000099")]
    public async Task GetHoldById_UnknownOrMalformedId_Returns404(string rawId)
    {
        var response = await Client.GetAsync($"/holds/{rawId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
