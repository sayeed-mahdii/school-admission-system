using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AdmissionSystem.Tests.Infrastructure;

/// <summary>
/// Small HTTP helpers shared by the extra tests. Each call builds its own request,
/// so they are safe to fire in parallel.
/// </summary>
internal static class ApiCalls
{
    /// <summary>Hold length the API under test uses (env var, default 600).</summary>
    public static int HoldSeconds =>
        int.TryParse(Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS"), out var s) && s > 0 ? s : 600;

    /// <summary>A 17-digit student id, unique per index.</summary>
    public static string Student(int i) => $"20150000000{i:D6}";

    public static HttpRequestMessage Req(
        HttpMethod method, string url, string? studentId = null, string? json = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (studentId is not null)
        {
            req.Headers.TryAddWithoutValidation("X-Student-Id", studentId);
        }

        if (json is not null)
        {
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return req;
    }

    public static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    public static async Task<string?> Code(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("code").GetString();

    public static async Task<string> CreateClass(HttpClient client, string name, int seats)
    {
        var body = JsonSerializer.Serialize(new { name, totalSeats = seats });
        var res = await client.SendAsync(Req(HttpMethod.Post, "/classes", json: body));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetString()!;
    }

    public static Task<HttpResponseMessage> Apply(HttpClient client, string classId, string student) =>
        client.SendAsync(Req(HttpMethod.Post, $"/classes/{classId}/holds", student));

    public static async Task<string> ApplyOk(HttpClient client, string classId, string student)
    {
        var res = await Apply(client, classId, student);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await Json(res)).GetProperty("holdId").GetString()!;
    }

    public static Task<HttpResponseMessage> Confirm(HttpClient client, string holdId, string student) =>
        client.SendAsync(Req(HttpMethod.Post, $"/holds/{holdId}/confirm", student));

    public static Task<HttpResponseMessage> Cancel(HttpClient client, string holdId, string student) =>
        client.SendAsync(Req(HttpMethod.Delete, $"/holds/{holdId}", student));

    public static Task<HttpResponseMessage> MyHolds(HttpClient client, string student) =>
        client.SendAsync(Req(HttpMethod.Get, "/me/holds", student));

    public static async Task<string> StatusOf(HttpClient client, string holdId)
    {
        var res = await client.GetAsync($"/holds/{holdId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("status").GetString()!;
    }

    public static async Task<JsonElement> ClassState(HttpClient client, string classId)
    {
        var res = await client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await Json(res);
    }

    public static int Int(JsonElement e, string name) => e.GetProperty(name).GetInt32();
}

