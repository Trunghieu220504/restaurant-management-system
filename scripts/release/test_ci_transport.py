import io,tarfile,tempfile,unittest
from pathlib import Path
from ci_transport import receive
class TransportTests(unittest.TestCase):
    def archive(self, names, kind=None):
        data=io.BytesIO()
        with tarfile.open(fileobj=data,mode='w') as t:
            for n in names:
                i=tarfile.TarInfo(n);i.size=1
                if kind:i.type=kind;i.linkname='/etc/passwd';i.size=0
                t.addfile(i,io.BytesIO(b'x') if not kind else None)
        data.seek(0);return data
    def test_path_traversal_and_links_rejected(self):
        for name,kind in [('../escape',None),('/etc/passwd',None),('images.tar.gz',tarfile.SYMTYPE),('images.tar.gz',tarfile.LNKTYPE)]:
            with self.subTest(name=name,kind=kind),tempfile.TemporaryDirectory() as tmp:
                with self.assertRaises(ValueError):receive(self.archive([name],kind),Path(tmp),'a'*40)
                self.assertEqual([],list(Path(tmp).iterdir()))
    def test_duplicate_and_incomplete_upload_rejected(self):
        for names in [['release.json','release.json'],['release.json']]:
            with tempfile.TemporaryDirectory() as tmp:
                with self.assertRaises(ValueError):receive(self.archive(names),Path(tmp),'a'*40)
    def test_valid_upload_is_immutable(self):
        names=['release.json','images.tar.gz','SHA256SUMS','migrations/order.txt']
        with tempfile.TemporaryDirectory() as tmp:
            receive(self.archive(names),Path(tmp),'a'*40)
            self.assertTrue((Path(tmp)/('a'*40)/'release.json').is_file())
            with self.assertRaises(ValueError):receive(self.archive(names),Path(tmp),'a'*40)
if __name__=='__main__':unittest.main()
