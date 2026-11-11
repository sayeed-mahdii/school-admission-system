using System.Net;
using AdmissionSystem.Api.Domain;
using AdmissionSystem.Api.Http;
using AdmissionSystem.Tests.Infrastructure;

namespace AdmissionSystem.Tests;

public sealed class MultiInstanceConcurrencyTests(AdmissionApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Apply_TwoHostsSharingOneDatabase_100RequestsEachFor20SeatClass_YieldsExactly20CreatedAnd180Conflict()
    {
        const int totalSeats = 20;
        const int requestsPerHost = 100;
        const int totalRequests = requestsPerHost * 2;

        await using var secondFactory = new AdmissionApiFactory();
        using var secondClient = secondFactory.CreateClient();

        // Warm up the second host before opening the concurrency gate
        var healthRes = await secondClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthRes.StatusCode);

        var classId = await ApiCalls.CreateClass(Client, "Class 6 - Two-Host Shift", totalSeats);

        using var startGate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(1, totalRequests).Select(async index =>
        {
            await Task.Yield();
            startGate.Wait();

            var targetClient = index <= requestsPerHost ? Client : secondClient;
            var studentId = ApiCalls.Student(index);
            var response = await ApiCalls.Apply(targetClient, classId, studentId);
            var code = response.StatusCode == HttpStatusCode.Conflict
                ? await ApiCalls.Code(response)
                : null;
            response.Dispose();
            return (response.StatusCode, Code: code);
        }).ToArray();

        startGate.Set();
        var results = await Task.WhenAll(tasks);

        var createdCount = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        var serverErrorCount = results.Count(r => (int)r.StatusCode >= 500);

        Assert.Equal(0, serverErrorCount);
        Assert.Equal(totalSeats, createdCount);
        Assert.Equal(totalRequests - totalSeats, conflictCount);
        Assert.All(
            results.Where(r => r.StatusCode == HttpStatusCode.Conflict),
            r => Assert.Equal(ErrorCodes.NoSeats, r.Code));

        var stateOnFirstHost = await ApiCalls.ClassState(Client, classId);
        Assert.Equal(totalSeats, ApiCalls.Int(stateOnFirstHost, "totalSeats"));
        Assert.Equal(totalSeats, ApiCalls.Int(stateOnFirstHost, "heldSeats"));
        Assert.Equal(0, ApiCalls.Int(stateOnFirstHost, "confirmedSeats"));
        Assert.Equal(0, ApiCalls.Int(stateOnFirstHost, "availableSeats"));

        var stateOnSecondHost = await ApiCalls.ClassState(secondClient, classId);
        Assert.Equal(totalSeats, ApiCalls.Int(stateOnSecondHost, "totalSeats"));
        Assert.Equal(totalSeats, ApiCalls.Int(stateOnSecondHost, "heldSeats"));
        Assert.Equal(0, ApiCalls.Int(stateOnSecondHost, "confirmedSeats"));
        Assert.Equal(0, ApiCalls.Int(stateOnSecondHost, "availableSeats"));
    }

    [Fact]
    public async Task Lifecycle_AcrossTwoHostsSharingOneDatabase_ApplyCancelAndConfirmAreConsistent()
    {
        await using var secondFactory = new AdmissionApiFactory();
        using var secondClient = secondFactory.CreateClient();

        var classId = await ApiCalls.CreateClass(Client, "Class 7 - Cross-Host Lifecycle", 1);

        var student1 = ApiCalls.Student(1);
        var student2 = ApiCalls.Student(2);

        // Student 1 applies on Host 1; Host 2 immediately observes the hold and 0 available seats
        var hold1 = await ApiCalls.ApplyOk(Client, classId, student1);
        Assert.Equal(HoldStatus.Held, await ApiCalls.StatusOf(secondClient, hold1));

        using var myHoldsRes = await ApiCalls.MyHolds(secondClient, student1);
        Assert.Equal(HttpStatusCode.OK, myHoldsRes.StatusCode);
        var myHoldsJson = await ApiCalls.Json(myHoldsRes);
        Assert.Equal(1, myHoldsJson.GetArrayLength());
        Assert.True(ApiCalls.HoldSeconds > 0);

        // Student 2 applies on Host 2 while seat is held on Host 1 -> 409 NO_SEATS
        using var blockedRes = await ApiCalls.Apply(secondClient, classId, student2);
        Assert.Equal(HttpStatusCode.Conflict, blockedRes.StatusCode);
        Assert.Equal(ErrorCodes.NoSeats, await ApiCalls.Code(blockedRes));

        // Student 1 cancels on Host 2 -> seat frees on Host 1
        using var cancelRes = await ApiCalls.Cancel(secondClient, hold1, student1);
        Assert.Equal(HttpStatusCode.NoContent, cancelRes.StatusCode);
        Assert.Equal(HoldStatus.Cancelled, await ApiCalls.StatusOf(Client, hold1));

        // Student 2 applies on Host 2 and confirms on Host 1
        var hold2 = await ApiCalls.ApplyOk(secondClient, classId, student2);
        using var confirmRes = await ApiCalls.Confirm(Client, hold2, student2);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        Assert.Equal(HoldStatus.Confirmed, await ApiCalls.StatusOf(secondClient, hold2));

        var finalState = await ApiCalls.ClassState(secondClient, classId);
        Assert.Equal(1, ApiCalls.Int(finalState, "totalSeats"));
        Assert.Equal(0, ApiCalls.Int(finalState, "heldSeats"));
        Assert.Equal(1, ApiCalls.Int(finalState, "confirmedSeats"));
        Assert.Equal(0, ApiCalls.Int(finalState, "availableSeats"));
    }
}

