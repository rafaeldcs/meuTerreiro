using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Terreiro.Domain;
namespace Terreiro.WebAPI;

// Optional provider adapter. Disabled by default; never accepts a host or credentials from the member.
public sealed class EfiPixClient : IDisposable
{
    private readonly IConfiguration cfg;private readonly HttpClient? http;private readonly SemaphoreSlim tokenLock=new(1,1);private string token="";private DateTime tokenExpires;
    public bool Enabled { get; }
    public string Environment { get; }
    public string AccountId=>cfg["Pix:AccountId"]??"";
    public string Recipient=>cfg["Pix:RecipientLabel"]??"Recebedor a conferir no seu banco";
    public EfiPixClient(IConfiguration config,IHostEnvironment host)
    {
        cfg=config;Enabled=cfg.GetValue<bool>("Pix:Enabled");Environment=cfg["Pix:Environment"]??"Sandbox";if(!Enabled)return;
        if(Environment!="Sandbox"||host.IsProduction())throw new InvalidOperationException("Este pacote permite apenas Pix Sandbox. A homologação do fluxo é obrigatória antes de habilitar dinheiro real.");
        if(cfg["Pix:Provider"]!="Efi")throw new InvalidOperationException("O adaptador configurável desta entrega é Efi; não é uma integração universal com bancos.");
        foreach(var field in new[]{"ClientId","ClientSecret","CertificatePath","Key","AccountId","RecipientLabel"})if(string.IsNullOrWhiteSpace(cfg["Pix:"+field]))throw new InvalidOperationException("Configuração Pix incompleta: "+field);
        var certificate=X509CertificateLoader.LoadPkcs12FromFile(cfg["Pix:CertificatePath"]!,cfg["Pix:CertificatePassword"]??"",X509KeyStorageFlags.EphemeralKeySet);
        var handler=new HttpClientHandler{AllowAutoRedirect=false};handler.ClientCertificates.Add(certificate);
        http=new HttpClient(handler){BaseAddress=new Uri("https://pix-h.api.efipay.com.br"),Timeout=TimeSpan.FromSeconds(20),MaxResponseContentBufferSize=1024*1024};
    }
    public async Task<JsonElement?> GetCharge(string txid,CancellationToken ct)=>await Send(HttpMethod.Get,"/v2/cob/"+Uri.EscapeDataString(txid),null,ct,true);
    public async Task<JsonElement> CreateCharge(string txid,long amount,CancellationToken ct)=>(await Send(HttpMethod.Put,"/v2/cob/"+Uri.EscapeDataString(txid),new{calendario=new{expiracao=3600},valor=new{original=(amount/100m).ToString("0.00",CultureInfo.InvariantCulture)},chave=cfg["Pix:Key"],solicitacaoPagador="Referencia administrativa "+txid},ct))!.Value;
    public async Task<JsonElement> GetPix(string id,CancellationToken ct)=>(await Send(HttpMethod.Get,"/v2/pix/"+Uri.EscapeDataString(id),null,ct))!.Value;
    private async Task<JsonElement?> Send(HttpMethod method,string path,object? data,CancellationToken ct,bool allow404=false)
    {
        Rules.Require(Enabled&&http!=null,"pix_unconfigured","Pix não configurado. A conferência manual continua disponível.",409);
        for(var attempt=0;attempt<2;attempt++)
        {
            await Authorize(ct);using var request=new HttpRequestMessage(method,path);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);if(data!=null)request.Content=JsonContent.Create(data);
            using var response=await http.SendAsync(request,ct);if(response.StatusCode==HttpStatusCode.Unauthorized&&attempt==0){tokenExpires=DateTime.MinValue;continue;}
            if(allow404&&response.StatusCode==HttpStatusCode.NotFound)return null;
            if(!response.IsSuccessStatusCode)throw new HttpRequestException("Pix provider returned HTTP "+(int)response.StatusCode,null,response.StatusCode);
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return doc.RootElement.Clone();
        }
        throw new HttpRequestException("Pix authentication could not be confirmed.");
    }
    private async Task Authorize(CancellationToken ct)
    {
        if(tokenExpires>DateTime.UtcNow)return;await tokenLock.WaitAsync(ct);
        try
        {
            if(tokenExpires>DateTime.UtcNow)return;using var request=new HttpRequestMessage(HttpMethod.Post,"/oauth/token");request.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(cfg["Pix:ClientId"]+":"+cfg["Pix:ClientSecret"])));request.Content=JsonContent.Create(new{grant_type="client_credentials"});using var response=await http!.SendAsync(request,ct);response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));token=doc.RootElement.GetProperty("access_token").GetString()??throw new HttpRequestException("Missing token.");tokenExpires=DateTime.UtcNow.AddSeconds(Math.Clamp(doc.RootElement.GetProperty("expires_in").GetInt32()-60,1,3500));
        }finally{tokenLock.Release();}
    }
    public void Dispose(){http?.Dispose();tokenLock.Dispose();}
}
