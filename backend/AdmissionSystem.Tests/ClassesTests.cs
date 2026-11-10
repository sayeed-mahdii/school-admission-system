using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdmissionSystem.Api.Contracts;
using AdmissionSystem.Api.Data;
using AdmissionSystem.Api.Domain;
using AdmissionSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdmissionSystem.Tests;

public sealed class ClassesTests(AdmissionApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task PostClasses_ValidInput_Returns201WithExactContractAndTrimmedName()
    {
        var response = await Client.PostAsJsonAsync("/classes", new
        {
            name = "  Class 6 - Morning Shift  ",
            totalSeats = 120,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var propertyNames = root.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["id", "name", "totalSeats"], propertyNames);

        Assert.True(Guid.TryParse(root.GetProperty("id").GetString(), out var id));
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Class 6 - Morning Shift", root.GetProperty("name").GetString());
        Assert.Equal(120, root.GetProperty("totalSeats").GetInt32());
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("   ", 10)]
    [InlineData(null, 10)]
    [InlineData("Class 6 - Day Shift", 0)]
    [InlineData("Class 6 - Day Shift", -5)]
    public async Task PostClasses_InvalidInput_Returns400ProblemDetails(string? name, int totalSeats)
    {
        var response = await Client.PostAsJsonAsync("/classes", new
        {
            name,
            totalSeats,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("code", out var code));
        Assert.Equal("VALIDATION_ERROR", code.GetString());
        Assert.True(doc.RootElement.TryGetProperty("message", out var message));
        Assert.False(string.IsNullOrWhiteSpace(message.GetString()));
    }

    [Fact]
    public async Task GetClasses_AndGetClassById_ReturnExactContractAndStableOrder()
    {
        var createFirst = await Client.PostAsJsonAsync("/classes", new
        {
            name = "Class 6 - Morning Shift",
            totalSeats = 120,
        });
        var first = await createFirst.Content.ReadFromJsonAsync<ClassCreatedResponse>();
        Assert.NotNull(first);

        Clock.Advance(TimeSpan.FromSeconds(1));

        var createSecond = await Client.PostAsJsonAsync("/classes", new
        {
            name = "Class 6 - Day Shift",
            totalSeats = 80,
        });
        var second = await createSecond.Content.ReadFromJsonAsync<ClassCreatedResponse>();
        Assert.NotNull(second);

        var listResponse = await Client.GetAsync("/classes");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listDoc = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, listDoc.RootElement.ValueKind);
        Assert.Equal(2, listDoc.RootElement.GetArrayLength());

        var firstElement = listDoc.RootElement[0];
        var listPropertyNames = firstElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            ["availableSeats", "confirmedSeats", "heldSeats", "id", "name", "totalSeats"],
            listPropertyNames);

        Assert.Equal(first.Id, firstElement.GetProperty("id").GetGuid());
        Assert.Equal(second.Id, listDoc.RootElement[1].GetProperty("id").GetGuid());

        var singleResponse = await Client.GetAsync($"/classes/{first.Id}");
        Assert.Equal(HttpStatusCode.OK, singleResponse.StatusCode);

        using var singleDoc = JsonDocument.Parse(await singleResponse.Content.ReadAsStringAsync());
        var singlePropertyNames = singleDoc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            ["availableSeats", "confirmedSeats", "heldSeats", "id", "name", "totalSeats"],
            singlePropertyNames);

        Assert.Equal(120, singleDoc.RootElement.GetProperty("totalSeats").GetInt32());
        Assert.Equal(0, singleDoc.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, singleDoc.RootElement.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(120, singleDoc.RootElement.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task GetClasses_DerivesAccurateCountsWithSeededHolds_WithoutMutatingExpiredRows()
    {
        var now = Clock.GetUtcNow().ToUniversalTime();
        var classId = Guid.NewGuid();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Classes.Add(new ClassEntity
            {
                Id = classId,
                Name = "Class 7 - Morning Shift",
                TotalSeats = 10,
                CreatedAt = now,
            });

            db.Holds.AddRange(
                // 2 active HELD holds (ExpiresAt > now)
                new Hold
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    StudentId = "10000000000000001",
                    Status = HoldStatus.Held,
                    CreatedAt = now,
                    ExpiresAt = now.AddMinutes(5),
                },
                new Hold
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    StudentId = "10000000000000002",
                    Status = HoldStatus.Held,
                    CreatedAt = now,
                    ExpiresAt = now.AddMinutes(5),
                },
                // 2 expired HELD holds (status is still HELD in DB, but ExpiresAt <= now)
                new Hold
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    StudentId = "10000000000000003",
                    Status = HoldStatus.Held,
                    CreatedAt = now.AddMinutes(-10),
                    ExpiresAt = now.AddMinutes(-1),
                },
                new Hold
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    StudentId = "10000000000000004",
                    Status = HoldStatus.Held,
                    CreatedAt = now.AddMinutes(-10),
                    ExpiresAt = now,
                },
                // 3 CONFIRMED holds
                new Hold
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    StudentId = "10000000000000005",
                    Status = HoldStatus.Confirmed,
                    CreatedAt = now.AddMinutes(-5),
                    ExpiresAt = now.AddMinutes(5),
                },
                new Hold
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    StudentId = "10000000000000006",
                    Status = HoldStatus.Confirmed,
                    CreatedAt = now.AddMinutes(-5),
                    ExpiresAt = now.AddMinutes(-1),
                },
                new Hold
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    StudentId = "10000000000000007",
                    Status = HoldStatus.Confirmed,
                    CreatedAt = now.AddMinutes(-5),
                    ExpiresAt = now.AddMinutes(5),
                },
                // 1 CANCELLED hold and 1 EXPIRED hold
                new Hold
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    StudentId = "10000000000000008",
                    Status = HoldStatus.Cancelled,
                    CreatedAt = now,
                    ExpiresAt = now.AddMinutes(5),
                },
                new Hold
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    StudentId = "10000000000000009",
                    Status = HoldStatus.Expired,
                    CreatedAt = now.AddMinutes(-10),
                    ExpiresAt = now.AddMinutes(-5),
                });

            await db.SaveChangesAsync();
        }

        // Check GET /classes/{id}
        var single = await Client.GetFromJsonAsync<ClassSeatsResponse>($"/classes/{classId}");
        Assert.NotNull(single);
        Assert.Equal(10, single.TotalSeats);
        Assert.Equal(2, single.HeldSeats);
        Assert.Equal(3, single.ConfirmedSeats);
        Assert.Equal(5, single.AvailableSeats);

        // Check GET /classes
        var list = await Client.GetFromJsonAsync<List<ClassSeatsResponse>>("/classes");
        Assert.NotNull(list);
        var item = Assert.Single(list);
        Assert.Equal(2, item.HeldSeats);
        Assert.Equal(3, item.ConfirmedSeats);
        Assert.Equal(5, item.AvailableSeats);

        // Verify GET did NOT mutate any HELD rows in the database
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var heldRowsInDb = await db.Holds.CountAsync(h => h.ClassId == classId && h.Status == HoldStatus.Held);
            Assert.Equal(4, heldRowsInDb);
        }

        // Advance fake clock past the remaining 2 active holds and confirm seats free up with zero writes
        Clock.Advance(TimeSpan.FromMinutes(6));

        var afterExpiry = await Client.GetFromJsonAsync<ClassSeatsResponse>($"/classes/{classId}");
        Assert.NotNull(afterExpiry);
        Assert.Equal(0, afterExpiry.HeldSeats);
        Assert.Equal(3, afterExpiry.ConfirmedSeats);
        Assert.Equal(7, afterExpiry.AvailableSeats);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("does-not-exist")]
    [InlineData("00000000-0000-0000-0000-000000000099")]
    public async Task GetClassById_UnknownOrUnparseableId_Returns404(string rawId)
    {
        var response = await Client.GetAsync($"/classes/{rawId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("NOT_FOUND", doc.RootElement.GetProperty("code").GetString());
    }
}
