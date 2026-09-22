"""Synthetic document and protocol tests. No real bank data; no live ClamAV claim."""
import io, json, struct, tempfile, unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
from pypdf import PdfWriter
from pypdf.generic import DictionaryObject, NameObject, TextStringObject, DecodedStreamObject
from inspect_file import inspect
from server import scan

class InspectionTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.path=Path(self.tmp.name)/'input.bin'
    def tearDown(self):self.tmp.cleanup()
    def pdf(self,count=1):
        writer=PdfWriter()
        for _ in range(count):writer.add_blank_page(width=300,height=300)
        return writer
    def save(self,writer):
        with self.path.open('wb') as f:writer.write(f)
    def test_png_needs_manual_reading(self):
        Image.new('RGB',(16,16)).save(self.path,format='PNG')
        x=inspect(self.path);self.assertTrue(x['clean']);self.assertEqual(x['analysisState'],'NeedsReview');self.assertIsNone(x['amount'])
    def test_jpeg_recognized_by_content(self):
        Image.new('RGB',(16,16)).save(self.path,format='JPEG');self.assertEqual(inspect(self.path)['pages'],1)
    def test_blank_pdf_needs_review(self):
        self.save(self.pdf());self.assertEqual(inspect(self.path)['analysisState'],'NeedsReview')
    def test_max_five_pages(self):
        self.save(self.pdf(5));self.assertEqual(inspect(self.path)['pages'],5)
    def test_six_pages_rejected(self):
        self.save(self.pdf(6))
        with self.assertRaisesRegex(ValueError,'pages'):inspect(self.path)
    def test_empty_pdf_rejected(self):
        self.save(self.pdf(0))
        with self.assertRaisesRegex(ValueError,'pages'):inspect(self.path)
    def test_encrypted_pdf_rejected(self):
        w=self.pdf();w.encrypt('not-a-real-password');self.save(w)
        with self.assertRaisesRegex(ValueError,'encrypted'):inspect(self.path)
    def test_javascript_rejected(self):
        w=self.pdf();w.add_js('app.alert("synthetic test")');self.save(w)
        with self.assertRaisesRegex(ValueError,'active_content'):inspect(self.path)
    def test_attachment_rejected(self):
        w=self.pdf();w.add_attachment('example.txt',b'synthetic');self.save(w)
        with self.assertRaisesRegex(ValueError,'active_content'):inspect(self.path)
    def test_open_action_rejected(self):
        w=self.pdf();w._root_object.update({NameObject('/OpenAction'):DictionaryObject({NameObject('/S'):NameObject('/JavaScript'),NameObject('/JS'):TextStringObject('test')})});self.save(w)
        with self.assertRaisesRegex(ValueError,'active_content'):inspect(self.path)
    def test_text_is_not_pdf(self):
        self.path.write_bytes(b'comprovante sem assinatura')
        with self.assertRaisesRegex(ValueError,'type'):inspect(self.path)
    def test_truncated_pdf_rejected(self):
        self.path.write_bytes(b'%PDF-1.7\n')
        with self.assertRaises(Exception):inspect(self.path)
    def test_fake_png_rejected(self):
        self.path.write_bytes(b'\x89PNG\r\n\x1a\nnot_an_image')
        with self.assertRaises(Exception):inspect(self.path)
    def test_extracted_data_does_not_quit_anything(self):
        w=self.pdf();page=w.pages[0]
        font=DictionaryObject({NameObject('/Type'):NameObject('/Font'),NameObject('/Subtype'):NameObject('/Type1'),NameObject('/BaseFont'):NameObject('/Helvetica')})
        page[NameObject('/Resources')]=DictionaryObject({NameObject('/Font'):DictionaryObject({NameObject('/F1'):w._add_object(font)})})
        stream=DecodedStreamObject();stream.set_data(b'BT /F1 12 Tf 20 200 Td (Pix agendado - R$ 100,00) Tj ET')
        page[NameObject('/Contents')]=w._add_object(stream);self.save(w)
        x=inspect(self.path);self.assertEqual(x['amount'],'100,00');self.assertTrue(x['scheduled']);self.assertEqual(x['analysisState'],'Extracted');self.assertNotIn('paid',x)

class ScannerProtocolTests(unittest.TestCase):
    def connection(self,response):
        obj=unittest.mock.MagicMock();obj.__enter__.return_value=obj;obj.recv.return_value=response;return obj
    def test_framing_and_ok(self):
        conn=self.connection(b'stream: OK\0')
        with patch('server.socket.create_connection',return_value=conn):scan(b'abc')
        parts=[a.args[0] for a in conn.sendall.call_args_list]
        self.assertEqual(parts,[b'zINSTREAM\0',struct.pack('!I',3)+b'abc',struct.pack('!I',0)])
    def test_found_blocks(self):
        with patch('server.socket.create_connection',return_value=self.connection(b'stream: Test FOUND\0')):
            with self.assertRaisesRegex(ValueError,'blocked'):scan(b'synthetic')
    def test_failure_is_not_clean(self):
        with patch('server.socket.create_connection',return_value=self.connection(b'stream: ERROR\0')):
            with self.assertRaises(RuntimeError):scan(b'synthetic')
    def test_unavailable_not_clean(self):
        with patch('server.socket.create_connection',side_effect=OSError('test')):
            with self.assertRaises(OSError):scan(b'synthetic')
if __name__=='__main__':unittest.main(verbosity=2)
