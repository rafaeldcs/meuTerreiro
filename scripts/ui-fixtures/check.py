"""Chromium layout checks of isolated REAL JSX component fixtures.
No API, React hydration, authentication, push delivery or complete Next build is asserted here.
"""
from __future__ import annotations
import json, threading, functools
from pathlib import Path
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[2]
DIRECTORY=ROOT/'docs/reports/layout-fixtures'
SHOTS=ROOT/'docs/reports/screenshots'; SHOTS.mkdir(exist_ok=True)
class Quiet(SimpleHTTPRequestHandler):
    def log_message(self,*args): pass
# No navigation or external request: render already-available local component HTML.
css=(DIRECTORY/'fixture.css').read_text()
results=[]
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='/usr/bin/chromium',args=['--no-sandbox'])
    page=browser.new_page()
    for file in sorted(DIRECTORY.glob('*.html')):
        for width,height in [(320,740),(390,844),(768,1024),(1024,768),(1440,1000)]:
            page.set_viewport_size({'width':width,'height':height})
            page.set_content(file.read_text().replace('<link rel="stylesheet" href="fixture.css">','<style>'+css+'</style>'))
            metrics=page.evaluate('''() => {
                const w=innerWidth;
                const box=document.querySelector('[role=dialog]');
                const rect=box?.getBoundingClientRect();
                const controls=[...document.querySelectorAll((box?'[role=dialog] ':'')+'input, '+(box?'[role=dialog] ':'')+'select, '+(box?'[role=dialog] ':'')+'textarea, '+(box?'[role=dialog] ':'')+'button')].filter(e=>e.offsetParent!==null&&!['checkbox','radio'].includes(e.type));
                const fieldsOutside=controls.filter(e=>{let r=e.getBoundingClientRect();return r.left<-1||r.right>w+1;}).map(e=>({tag:e.tagName,text:(e.getAttribute('aria-label')||e.textContent||e.name).slice(0,90),width:e.getBoundingClientRect().width}));
                const smallTargets=controls.filter(e=>e.getBoundingClientRect().height<43).map(e=>({tag:e.tagName,label:e.getAttribute('aria-label')||e.textContent,height:e.getBoundingClientRect().height}));
                return {viewport:w,pageWidth:document.documentElement.scrollWidth,fieldsOutside,smallTargets,
                    modalWithinViewport:!rect||(rect.left>=0&&rect.right<=w+1&&rect.top>=0&&rect.bottom<=innerHeight+1),
                    sidebarVisible:getComputedStyle(document.querySelector('.sidebar')).display!=='none',
                    bottomNavVisible:getComputedStyle(document.querySelector('.mobile-nav')).display!=='none'};
            }''')
            ok=metrics['pageWidth']<=width+1 and not metrics['fieldsOutside'] and not metrics['smallTargets'] and metrics['modalWithinViewport'] and metrics['sidebarVisible']==(width>=1024) and metrics['bottomNavVisible']==(width<1024)
            results.append({'fixture':file.stem,'width':width,'height':height,'passed':ok,**metrics})
            if width in [390,1440] and file.stem in ['inicio','recebimentos','form-compras','mensalidades','mensalidade-isenta','form-isencao','isencao-cadastro','form-isencao-cadastro']:
                page.screenshot(path=str(SHOTS/f'{file.stem}-{width}.png'),full_page=True)
    browser.close()

report={'scope':'Isolated component layout; synthetic data and simplified icons. NOT a Next/React runtime or API end-to-end test.','executed':len(results),'passed':sum(x['passed'] for x in results),'failed':sum(not x['passed'] for x in results),'results':results}
(ROOT/'docs/reports/responsive.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='results'},ensure_ascii=False,indent=2))
for result in results:
    if not result['passed']:print(json.dumps(result,ensure_ascii=False))
raise SystemExit(1 if report['failed'] else 0)
