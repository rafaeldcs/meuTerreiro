using System.Globalization;
using System.Text;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;
public sealed class ReconciliationService(ModuleStore m)
{
    public async Task<object> Command(User actor,string action,Command input,CancellationToken ct)
    {
        Access.Demand(actor,"finance.write");
        return await m.Execute<object>(actor,"reconciliation."+action,input.OperationId,input,async(s,c)=>
        {
            var d=input.Data;var h=actor.HouseId;
            if(action=="import")
            {
                var account=await m.Get<Account>(s,h,Fields.Required(d,"accountId"),c);Rules.Require(account.Kind=="Bank","account_type","Extrato é destinado a uma conta bancária.");
                var raw=Fields.Text(d,"csv");Rules.Require(raw.Length is >0 and <=1_000_000,"csv_size","CSV deve ter até 1 MB.");var parsed=CsvRows(raw);
                Rules.Require(parsed.Count is >1 and <=5001,"csv_rows","Importe de 1 a 5.000 linhas por lote.");
                Rules.Require(string.Join(",",parsed[0]).ToLowerInvariant()=="reference,date,amount,description","csv_header","Cabeçalho obrigatório: reference,date,amount,description. Valor decimal com ponto; use aspas para textos com vírgula.");
                var hash=Rules.Hash(raw);var existing=await m.Set<StatementBatch>().Find(s,x=>x.HouseId==h&&x.AccountId==account.Id&&x.FileHash==hash).FirstOrDefaultAsync(c);if(existing!=null)return new{existing.Id,existing.ImportedCount,existing.SkippedCount};
                var lines=new List<StatementLine>();
                foreach(var row in parsed.Skip(1))
                {
                    Rules.Require(row.Length==4,"csv_columns","Cada linha deve conter quatro colunas.");var reference=BusinessRules.Reference(row[0]);var day=BusinessRules.Date(row[1]);BusinessRules.NotFuture(day);
                    Rules.Require(decimal.TryParse(row[2],NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var amount)&&amount*100==decimal.Truncate(amount*100)&&amount!=0&&amount<=BusinessRules.MaxCents/100m&&amount>=-BusinessRules.MaxCents/100m,"csv_amount","Valor inválido no extrato. Use ponto e no máximo duas casas decimais.");
                    lines.Add(new StatementLine{HouseId=h,AccountId=account.Id,FinancialReference=reference,OccurredOn=day,SignedCents=decimal.ToInt64(amount*100),Description=Rules.Text(row[3],0,300,"Descrição")});
                }
                Rules.Require(lines.Select(x=>x.FinancialReference).Distinct().Count()==lines.Count,"csv_duplicate","O lote repete identificadores de transação.");
                var opening=Fields.Number(d,"openingCents");var closing=Fields.Number(d,"closingCents");Rules.Require(checked(opening+lines.Sum(x=>x.SignedCents))==closing,"statement_totals","Saldo inicial + movimentações não coincide com o saldo final informado.");
                var batch=new StatementBatch{HouseId=h,AccountId=account.Id,FileHash=hash,ImportedBy=actor.Id,OpeningCents=opening,ClosingCents=closing,OriginalCsv=raw};
                foreach(var line in lines)
                {
                    var prior=await m.Set<StatementLine>().Find(s,x=>x.HouseId==h&&x.AccountId==account.Id&&x.FinancialReference==line.FinancialReference).FirstOrDefaultAsync(c);
                    if(prior!=null){Rules.Require(prior.SignedCents==line.SignedCents&&prior.OccurredOn==line.OccurredOn,"statement_conflict","Identificador já importado com data ou valor diferente.",409);batch.SkippedCount++;continue;}
                    line.BatchId=batch.Id;await m.Insert(s,line,c);batch.ImportedCount++;
                }
                await m.Insert(s,batch,c);return new{batch.Id,batch.ImportedCount,batch.SkippedCount};
            }
            Rules.Require(action=="match","action_unknown","Operação de conciliação desconhecida.",404);
            var bank=await m.Get<StatementLine>(s,h,Fields.Required(d,"statementLineId"),c);BusinessRules.Revision(bank.Revision,input.Revision);var ledger=await m.Get<LedgerEntry>(s,h,Fields.Required(d,"ledgerId"),c);
            Rules.Require(bank.LedgerId==null&&ledger.StatementLineId==null,"already_matched","Uma das operações já está conciliada.",409);
            Rules.Require(bank.AccountId==ledger.AccountId&&bank.SignedCents==ledger.SignedCents&&bank.OccurredOn==ledger.OccurredOn,"match_difference","Conta, direção, valor e data precisam coincidir.",409);
            Rules.Require(Fields.Bool(d,"independentlyChecked"),"match_verification","Confirme a conferência na conta recebedora; arquivo importado não autentica a origem.");
            if(ledger.FinancialReference!=bank.FinancialReference)Rules.Text(Fields.Text(d,"reason"),8,500,"Justificativa para referências diferentes");
            bank.LedgerId=ledger.Id;bank.MatchedBy=actor.Id;ledger.StatementLineId=bank.Id;await m.Save(s,bank,c);await m.Save(s,ledger,c);return bank;
        },ct);
    }
    public static List<string[]> CsvRows(string raw)
    {
        var result=new List<string[]>();var row=new List<string>();var cell=new StringBuilder();bool quoted=false;
        for(int i=0;i<raw.Length;i++)
        {
            var ch=raw[i];if(ch=='"'){if(quoted&&i+1<raw.Length&&raw[i+1]=='"'){cell.Append('"');i++;}else quoted=!quoted;}
            else if(!quoted&&ch==','){row.Add(cell.ToString());cell.Clear();}
            else if(!quoted&&(ch=='\r'||ch=='\n')){if(ch=='\r'&&i+1<raw.Length&&raw[i+1]=='\n')i++;row.Add(cell.ToString());cell.Clear();if(row.Any(x=>x.Length>0))result.Add(row.ToArray());row.Clear();}
            else cell.Append(ch);
            Rules.Require(cell.Length<=10000&&result.Count<=5001,"csv_size","CSV fora do limite.");
        }
        Rules.Require(!quoted,"csv_quotes","CSV contém aspas sem fechamento.");row.Add(cell.ToString());if(row.Any(x=>x.Length>0))result.Add(row.ToArray());return result;
    }
}
