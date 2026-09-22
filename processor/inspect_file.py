"""Bounded subprocess. Document text is untrusted data, never an instruction or payment proof."""
from __future__ import annotations
import json, re, sys, warnings, resource
from pathlib import Path
from PIL import Image
from pypdf import PdfReader

MAX_PAGES=5
MAX_PIXELS=20_000_000

def inspect(path: Path) -> dict:
    head=path.read_bytes()[:8]; text=''; pages=1
    if head.startswith(b'%PDF-'):
        reader=PdfReader(str(path), strict=True)
        if reader.is_encrypted: raise ValueError('encrypted')
        pages=len(reader.pages)
        if not 1 <= pages <= MAX_PAGES: raise ValueError('pages')
        root=reader.trailer['/Root']
        # Reject embedded actions/attachments, not merely a misleading extension.
        if root.get('/OpenAction') or root.get('/AA'): raise ValueError('active_content')
        names=root.get('/Names')
        if names:
            names=names.get_object()
            if names.get('/JavaScript') or names.get('/EmbeddedFiles'): raise ValueError('active_content')
        for page in reader.pages:
            if page.get('/AA'): raise ValueError('active_content')
            for annot in page.get('/Annots', []):
                a=annot.get_object().get('/A')
                if a and a.get_object().get('/S') in ['/JavaScript','/Launch','/SubmitForm','/ImportData']: raise ValueError('active_content')
            text += (page.extract_text() or '')[:30000] + '\n'
        text=text[:30000]
    elif head.startswith(b'\x89PNG\r\n\x1a\n') or head[:3]==b'\xff\xd8\xff':
        Image.MAX_IMAGE_PIXELS=MAX_PIXELS
        warnings.simplefilter('error', Image.DecompressionBombWarning)
        with Image.open(path) as image:
            if image.format not in ['JPEG','PNG'] or image.width*image.height>MAX_PIXELS: raise ValueError('image')
            image.verify()
        # No invented OCR output. Scans/images remain explicitly ready for manual review.
    else: raise ValueError('type')
    amount=re.search(r'R\$\s*([0-9]{1,3}(?:\.[0-9]{3})*,[0-9]{2}|[0-9]+,[0-9]{2})', text)
    reference=re.search(r'\bE[0-9]{8}[0-9A-Za-z]{23}\b', text)
    return {'clean': True, 'analysisState': 'Extracted' if text.strip() else 'NeedsReview',
            'text': text, 'pages': pages, 'amount': amount.group(1) if amount else None,
            'reference': reference.group(0) if reference else None,
            'scheduled': bool(re.search(r'agendad[oa]|agendamento', text, re.I)),
            'note': 'Texto extraído não autentica o documento nem confirma crédito bancário.' if text.strip() else 'Arquivo legível pelo processador, sem texto extraível; requer leitura humana. Nenhuma quitação automática.'}

if __name__=='__main__':
    resource.setrlimit(resource.RLIMIT_CPU,(20,20))
    resource.setrlimit(resource.RLIMIT_AS,(768*1024*1024,768*1024*1024))
    resource.setrlimit(resource.RLIMIT_FSIZE,(20*1024*1024,20*1024*1024))
    print(json.dumps(inspect(Path(sys.argv[1])),ensure_ascii=False))
