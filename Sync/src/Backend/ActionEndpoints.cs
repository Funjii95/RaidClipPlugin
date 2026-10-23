using System.Security.Claims;
using System.Text.Json;
using SnapshotProtocol;
namespace Backend;
public static class ActionEndpoints
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static void MapActionEndpoints(this WebApplication app,BackendSettings settings)
    {
        if(!settings.Actions.Enabled)return;
        app.Use(async(context,next)=>{
            try {await next();}
            catch(RelayDenied denied){context.Response.StatusCode=denied.Status;await context.Response.WriteAsJsonAsync(new{error=denied.Code});}
            catch(ArgumentException) when(context.Request.Path.StartsWithSegments("/api/v1/play") || context.Request.Path.StartsWithSegments("/api/v1/plugin/actions"))
            {context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new{error="invalid_request"});}
            catch(JsonException) {context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new{error="invalid_json"});}
        });
        var viewers=app.MapGroup("/api/v1/play").RequireAuthorization("Viewer").RequireRateLimiting("actions");
        viewers.MapGet("/state",async(ActionRelay relay,CancellationToken ct)=>Results.Ok(await relay.AvailabilityAsync(ct)));
        viewers.MapPost("/actions",async(HttpRequest request,ClaimsPrincipal user,ActionRelay relay,CancellationToken ct)=>
            {
            var receipt=await relay.EnqueueAsync(user.FindFirstValue("user_id")!,await Body<RpgIntent>(request,4096,ct),request.Headers["X-Rpg-Session"].Count==1 && Guid.TryParse(request.Headers["X-Rpg-Session"],out var expected)?expected:Guid.Empty,ct);
            return receipt.Outcome?.Code=="not_ready"?(IResult)Results.Conflict(receipt):Results.Accepted(value:receipt);
        });
        viewers.MapGet("/actions/{id:guid}",async(Guid id,ClaimsPrincipal user,ActionRelay relay,CancellationToken ct)=>
            await relay.ReadAsync(user.FindFirstValue("user_id")!,id,ct) is {} value ? Results.Ok(value) : Results.NotFound());
        var plugin=app.MapGroup("/api/v1/plugin/actions").RequireAuthorization("ActionPlugin").RequireRateLimiting("plugin-actions");
        viewers.MapGet("/privacy/deletion",async(ClaimsPrincipal user,ActionRelay relay,CancellationToken ct)=>Results.Ok(new{request=await relay.SelfDeletionStatusAsync(user.FindFirstValue("user_id")!,ct)}));
        viewers.MapPost("/privacy/deletion",async(HttpRequest request,ClaimsPrincipal user,ActionRelay relay,CancellationToken ct)=>Results.Accepted(value:await relay.RequestSelfDeletionAsync(user.FindFirstValue("user_id")!,await Body<SelfDeletionIntent>(request,1024,ct),ct)));
        plugin.MapGet("/privacy-requests",async(ActionRelay relay,CancellationToken ct)=>Results.Ok(await relay.PendingSelfDeletionsAsync(ct)));
        plugin.MapPost("/privacy-requests/{userId}/complete",async(string userId,HttpRequest request,ActionRelay relay,CancellationToken ct)=>Results.Ok(await relay.CompleteSelfDeletionAsync(userId,(await Body<SelfDeletionIntent>(request,1024,ct)).RequestId,ct)));
        plugin.MapPost("/privacy/{userId}",async(string userId,ActionRelay relay,CancellationToken ct)=>{await relay.PurgeUserAsync(userId,ct);return Results.Ok(new{erased=true});});
        plugin.MapPost("/session",async(ActionRelay relay,CancellationToken ct)=>Results.Ok(await relay.OpenAsync(ct)));
        plugin.MapPost("/heartbeat",async(HttpRequest request,ActionRelay relay,CancellationToken ct)=>Results.Ok(await relay.HeartbeatAsync(await Body<RelayHeartbeat>(request,65536,ct),ct)));
        plugin.MapDelete("/session/{id:guid}",async(Guid id,ActionRelay relay,CancellationToken ct)=>Results.Ok(await relay.CloseAsync(id,ct)));
        plugin.MapPost("/claim/{session:guid}",async(Guid session,ActionRelay relay,CancellationToken ct)=>
            await relay.ClaimAsync(session,ct) is {} value ? Results.Ok(value) : Results.NoContent());
        plugin.MapPost("/complete",async(HttpRequest request,ActionRelay relay,CancellationToken ct)=>Results.Ok(await relay.CompleteAsync(await Body<RelayComplete>(request,ActionResultLimits.MaximumCompletionBodyBytes,ct),ct)));
    }
    private static async Task<T> Body<T>(HttpRequest request,int limit,CancellationToken ct)
    {
        if(request.ContentType?.Split(';')[0]!="application/json" || request.ContentLength>limit)throw new RelayDenied(413,"invalid_body");
        using var stream=new MemoryStream();var buffer=new byte[4096];
        int count;while((count=await request.Body.ReadAsync(buffer,ct))>0){stream.Write(buffer,0,count);if(stream.Length>limit)throw new RelayDenied(413,"body_too_large");}
        using var document=JsonDocument.Parse(stream.ToArray(),new(){MaxDepth=20});
        static void Unique(JsonElement item){if(item.ValueKind==JsonValueKind.Object){var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var field in item.EnumerateObject()){if(!keys.Add(field.Name))throw new JsonException();Unique(field.Value);}}else if(item.ValueKind==JsonValueKind.Array)foreach(var child in item.EnumerateArray())Unique(child);}
        Unique(document.RootElement);
        return document.RootElement.Deserialize<T>(Json)??throw new JsonException();
    }
}
