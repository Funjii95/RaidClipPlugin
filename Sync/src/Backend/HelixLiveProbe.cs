using System.Net.Http.Headers;
using System.Text.Json;
namespace Backend;
public sealed class HelixLiveProbe(BackendSettings settings,TimeProvider clock) : ITwitchLiveProbe,IDisposable
{
    private readonly HttpClient http=new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(5)};
    private readonly SemaphoreSlim gate=new(1,1);
    private DateTimeOffset cachedAt=DateTimeOffset.MinValue;
    private LiveConfirmation? cached;
    public async Task<LiveConfirmation?> CheckAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try {
            // Brief shared cache bounds Twitch traffic; freshness starts before request issuance.
            if(clock.GetUtcNow()-cachedAt<TimeSpan.FromSeconds(2))return cached;
            var issued=clock.GetUtcNow();
            try {
                using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.twitch.tv/helix/streams?user_id="+settings.Twitch.ChannelId);
                request.Headers.Add("Client-Id",settings.Actions.HelixClientId);
                request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",settings.Actions.HelixAccessToken);
                using var response=await http.SendAsync(request,ct);
                response.EnsureSuccessStatusCode();
                await using var body=await response.Content.ReadAsStreamAsync(ct);
                using var data=await JsonDocument.ParseAsync(body,cancellationToken:ct);
                var streams=data.RootElement.GetProperty("data").EnumerateArray().ToArray();
                cached=streams.Length==1 && streams[0].GetProperty("user_id").GetString()==settings.Twitch.ChannelId && streams[0].GetProperty("type").GetString()=="live" &&
                    streams[0].GetProperty("id").GetString() is {Length:>0} stream && clock.GetUtcNow()-issued<TimeSpan.FromSeconds(5)
                    ? new(stream,issued) : null;
            } catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch{cached=null;}
            cachedAt=issued;return cached;
        } finally {gate.Release();}
    }
    public void Dispose(){http.Dispose();gate.Dispose();}
}
