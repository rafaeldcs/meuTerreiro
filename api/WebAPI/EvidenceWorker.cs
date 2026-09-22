using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
using Terreiro.Service;
namespace Terreiro.WebAPI;

public sealed class EvidenceWorker(ModuleStore m,EvidenceService files,IConfiguration cfg,ILogger<EvidenceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseUrl=cfg["Processor:Url"]??"http://processor:8090";var key=cfg["Processor:Key"]??"";
        if(key.Length<32)throw new InvalidOperationException("Configure Processor__Key com pelo menos 32 caracteres.");
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new Uri(baseUrl),Timeout=TimeSpan.FromSeconds(50)};http.DefaultRequestHeaders.Add("X-Processor-Key",key);
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now=DateTime.UtcNow;var e=await m.Set<Evidence>().FindOneAndUpdateAsync(x=>x.ScanState=="Quarantine" && x.Attempts<6 && (x.LeaseUntil==null||x.LeaseUntil<now),Builders<Evidence>.Update.Set(x=>x.LeaseUntil,now.AddMinutes(2)).Inc(x=>x.Attempts,1),new FindOneAndUpdateOptions<Evidence>{ReturnDocument=ReturnDocument.After},stoppingToken);
                if(e==null){await Task.Delay(3000,stoppingToken);continue;}
                try
                {
                    var data=await File.ReadAllBytesAsync(Path.Combine(files.DirectoryPath,e.ObjectKey),stoppingToken);using var content=new ByteArrayContent(data);content.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    using var response=await http.PostAsync("/inspect",content,stoppingToken);response.EnsureSuccessStatusCode();var result=await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:stoppingToken);
                    var clean=Fields.Bool(result,"clean");var parse=Fields.Text(result,"analysisState","NeedsReview");var text=Fields.Text(result,"text");if(text.Length>30000)text=text[..30000];
                    var update=Builders<Evidence>.Update.Set(x=>x.ScanState,clean?"Clean":"Blocked").Set(x=>x.AnalysisState,parse).Set(x=>x.ExtractedText,text).Set(x=>x.AnalysisNote,Fields.Text(result,"note")).Set(x=>x.ExtractedAmount,Fields.Optional(result,"amount")).Set(x=>x.ExtractedReference,Fields.Optional(result,"reference")).Set(x=>x.PossibleScheduled,Fields.Bool(result,"scheduled")).Set(x=>x.PageCount,(int)Fields.Number(result,"pages")).Set(x=>x.LeaseUntil,null).Inc(x=>x.Revision,1);
                    await m.Set<Evidence>().UpdateOneAsync(x=>x.Id==e.Id&&x.LeaseUntil==e.LeaseUntil,update,cancellationToken:stoppingToken);
                }
                catch(Exception ex) when(ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning("Evidence processing unavailable ({Type}); file remains quarantined",ex.GetType().Name);
                    await m.Set<Evidence>().UpdateOneAsync(x=>x.Id==e.Id&&x.LeaseUntil==e.LeaseUntil,Builders<Evidence>.Update.Set(x=>x.LeaseUntil,DateTime.UtcNow.AddMinutes(2)).Set(x=>x.AnalysisNote,"Processamento indisponível; arquivo mantido em quarentena."),cancellationToken:stoppingToken);
                }
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogWarning("Evidence worker failed ({Type})",ex.GetType().Name);await Task.Delay(5000,stoppingToken);}
        }
    }
}
