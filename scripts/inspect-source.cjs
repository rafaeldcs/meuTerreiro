/* Static syntax/config checks only. No assertion of React rendering or C# compilation. */
const fs=require('node:fs'),path=require('node:path');
let ts;try{ts=require('typescript')}catch{ts=require('/opt/nvm/versions/node/v22.16.0/lib/node_modules/typescript')}
const root=path.resolve(__dirname,'..'),find=d=>fs.readdirSync(d,{withFileTypes:true}).flatMap(x=>x.isDirectory()&&!['node_modules','.next','.git','docs','__pycache__'].includes(x.name)?find(path.join(d,x.name)):x.isFile()?[path.join(d,x.name)]:[]);
const files=[...find(path.join(root,'app')),...find(path.join(root,'scripts'))].filter(x=>/\.(js|jsx|mjs|cjs)$/.test(x));let diagnostics=[];
for(const file of files){const src=fs.readFileSync(file,'utf8');const parsed=ts.createSourceFile(file,src,ts.ScriptTarget.Latest,true,file.endsWith('.jsx')?ts.ScriptKind.JSX:ts.ScriptKind.JS);for(const d of parsed.parseDiagnostics){const at=parsed.getLineAndCharacterOfPosition(d.start||0);diagnostics.push({file:path.relative(root,file),line:at.line+1,message:ts.flattenDiagnosticMessageText(d.messageText,' ')});}}
const result={scope:'Syntax parsing only, not Next build, React runtime, type-check or C# compilation',files:files.length,errors:diagnostics};fs.writeFileSync(path.join(root,'docs/reports/syntax.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result,null,2));if(diagnostics.length)process.exit(1);
