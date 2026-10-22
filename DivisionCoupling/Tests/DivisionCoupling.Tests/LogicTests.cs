using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DivisionCoupling.Tests
{
    /// <summary>
    /// Logic（分割ファイル名の組み立て・分割サイズの読み取り・分割と結合）のテスト。
    /// 分割と結合は毎回作る一時フォルダの中だけで行い、終わったらフォルダごと消す。
    /// </summary>
    [TestClass]
    public class LogicTests
    {
        private String _workDir;

        [TestInitialize]
        public void Setup()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "DivisionCouplingTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_workDir))
            {
                Directory.Delete(_workDir, true);
            }
        }

        private String CreateFile(String name, int size)
        {
            String path = Path.Combine(_workDir, name);
            byte[] data = new byte[size];
            new Random(size).NextBytes(data);
            File.WriteAllBytes(path, data);
            return path;
        }

        private static ProcessError SplitAll(String path, int divSize, out String destPath, List<int> positions = null, List<int> ranges = null)
        {
            return Logic.Split(path, divSize, () => true,
                count => { if (ranges != null) ranges.Add(count); },
                index => { if (positions != null) positions.Add(index); },
                out destPath);
        }

        // ---- 前回の分割ファイルの残り ----

        [TestMethod]
        public void FindPartFilesは同じ名前の分割ファイルだけを番号順に返し途切れた番号も拾う()
        {
            String src = CreateFile("fname.jpg", 10);
            foreach (String name in new[] { "fname.jpg.div010", "fname.jpg.div002", "fname.jpg.div001", "fname.jpg.div1000",
                "other.jpg.div001", "fname.jpg.div001.bak", "fname.jpg.divx", "fname.jpg.div01", "xfname.jpg.div003" })
            {
                File.WriteAllBytes(Path.Combine(_workDir, name), new byte[1]);
            }

            CollectionAssert.AreEqual(
                new[] { "fname.jpg.div001", "fname.jpg.div002", "fname.jpg.div010", "fname.jpg.div1000" },
                Logic.FindPartFiles(src).Select(Path.GetFileName).ToArray());
            Assert.AreEqual(0, Logic.FindPartFiles(Path.Combine(_workDir, "none.jpg")).Count);
            Assert.AreEqual(0, Logic.FindPartFiles(Path.Combine(_workDir, "nodir", "fname.jpg")).Count);
        }

        [TestMethod]
        public void 前回の分割ファイルを消してから分割し直せば結合で古いデータが混ざらない()
        {
            String src = CreateFile("data.bin", 10 * 1024);
            byte[] original = File.ReadAllBytes(src);
            String dummy;
            Assert.AreEqual(ProcessError.None, SplitAll(src, 1024, out dummy));
            Assert.AreEqual(10, Logic.FindPartFiles(src).Count);

            String failedPath;
            Assert.IsTrue(Logic.TryDeleteFiles(Logic.FindPartFiles(src), out failedPath));
            Assert.IsNull(failedPath);
            Assert.AreEqual(ProcessError.None, SplitAll(src, 3 * 1024, out dummy));
            Assert.AreEqual(4, Logic.FindPartFiles(src).Count);

            File.Delete(src);
            Assert.AreEqual(ProcessError.None, Logic.Merge(src, () => true));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(src));
        }

        // ---- ファイル名 ----

        [TestMethod]
        public void 分割ファイル名は3桁ゼロ埋めの連番を付ける()
        {
            Assert.AreEqual(@"C:\a\fname.jpg.div001", Logic.MakePartPath(@"C:\a\fname.jpg", 1));
            Assert.AreEqual(@"C:\a\fname.jpg.div012", Logic.MakePartPath(@"C:\a\fname.jpg", 12));
            Assert.AreEqual(@"C:\a\fname.jpg.div1000", Logic.MakePartPath(@"C:\a\fname.jpg", 1000));
        }

        [TestMethod]
        public void 結合後のファイル名は選んだ分割ファイルから最後の拡張子を外したもの()
        {
            Assert.AreEqual(@"C:\a\fname.jpg", Logic.MakeMergeDestPath(@"C:\a\fname.jpg.div001"));
            Assert.AreEqual(@"C:\a\fname.jpg", Logic.MakeMergeDestPath(@"C:\a\fname.jpg.div005"));
        }

        [TestMethod]
        public void 拡張子が無ければ選んだ名前のまま()
        {
            Assert.AreEqual("fname", Logic.MakeMergeDestPath("fname"));
        }

        [TestMethod]
        public void フォルダ名のドットでも切ってしまうのはMFC版と同じ()
        {
            Assert.AreEqual(@"C:\a", Logic.MakeMergeDestPath(@"C:\a.b\fname"));
        }

        // ---- 分割サイズ ----

        [TestMethod]
        public void 分割サイズ欄の数字を読む()
        {
            Assert.AreEqual(100, Logic.ParseDivSizeKb("100"));
            Assert.AreEqual(0, Logic.ParseDivSizeKb(""));
        }

        [TestMethod]
        public void intに収まらない分割サイズは0になる()
        {
            Assert.AreEqual(0, Logic.ParseDivSizeKb("99999999999"));
        }

        [TestMethod]
        public void KBをバイトに直す()
        {
            Assert.AreEqual(1024, Logic.ToDivSize(1));
            Assert.AreEqual(Int32.MaxValue / 1024 * 1024, Logic.ToDivSize(Int32.MaxValue / 1024));
        }

        [TestMethod]
        public void バイトにしてintを超える分割サイズと0以下は0()
        {
            Assert.AreEqual(0, Logic.ToDivSize(Int32.MaxValue / 1024 + 1));
            Assert.AreEqual(0, Logic.ToDivSize(0));
            Assert.AreEqual(0, Logic.ToDivSize(-1));
        }

        // ---- 分割 ----

        [TestMethod]
        public void 分割サイズごとに切り上げた個数のファイルへ分ける()
        {
            String src = CreateFile("data.bin", 2500);
            List<int> positions = new List<int>();
            List<int> ranges = new List<int>();
            String destPath;

            ProcessError error = SplitAll(src, 1000, out destPath, positions, ranges);

            Assert.AreEqual(ProcessError.None, error);
            Assert.AreEqual(1000, new FileInfo(src + ".div001").Length);
            Assert.AreEqual(1000, new FileInfo(src + ".div002").Length);
            Assert.AreEqual(500, new FileInfo(src + ".div003").Length);
            Assert.IsFalse(File.Exists(src + ".div004"));
            CollectionAssert.AreEqual(new[] { 3 }, ranges);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, positions);
            Assert.AreEqual(src + ".div003", destPath);
        }

        [TestMethod]
        public void ちょうど割り切れるときは余りのファイルを作らない()
        {
            String src = CreateFile("data.bin", 2048);
            String destPath;

            Assert.AreEqual(ProcessError.None, SplitAll(src, 1024, out destPath));

            Assert.IsTrue(File.Exists(src + ".div002"));
            Assert.IsFalse(File.Exists(src + ".div003"));
        }

        [TestMethod]
        public void 分割サイズが0以下ならエラー()
        {
            String src = CreateFile("data.bin", 10);
            String destPath;

            Assert.AreEqual(ProcessError.DivSize, SplitAll(src, 0, out destPath));
            Assert.IsFalse(File.Exists(src + ".div001"));
        }

        [TestMethod]
        public void 元ファイルが無ければオープンエラー()
        {
            String destPath;

            Assert.AreEqual(ProcessError.OpenSource, SplitAll(Path.Combine(_workDir, "none.bin"), 1024, out destPath));
            Assert.AreEqual(ProcessError.OpenSource, SplitAll("", 1024, out destPath));
        }

        [TestMethod]
        public void 分割ファイルが作れなければ作成エラーで作った分は消す()
        {
            String src = CreateFile("data.bin", 3000);
            // 2つ目の分割ファイル名でフォルダを作っておき、ファイルとして開けなくする
            Directory.CreateDirectory(src + ".div002");
            String destPath;

            ProcessError error = SplitAll(src, 1000, out destPath);

            Assert.AreEqual(ProcessError.OpenDest, error);
            Assert.AreEqual(src + ".div002", destPath);
            Assert.IsFalse(File.Exists(src + ".div001"));
        }

        [TestMethod]
        public void 停止されたら作りかけの分割ファイルを消して中断扱い()
        {
            String src = CreateFile("data.bin", 3000);
            int calls = 0;
            String destPath;

            // 1つ目を作った後で停止する
            ProcessError error = Logic.Split(src, 1000, () => calls++ < 1, c => { }, i => { }, out destPath);

            Assert.AreEqual(ProcessError.Aborted, error);
            Assert.AreEqual(0, Directory.GetFiles(_workDir, "*.div*").Length);
        }

        [TestMethod]
        public void 空のファイルは何も作らずに成功する()
        {
            String src = CreateFile("empty.bin", 0);
            String destPath;

            Assert.AreEqual(ProcessError.None, SplitAll(src, 1024, out destPath));
            Assert.AreEqual(0, Directory.GetFiles(_workDir, "*.div*").Length);
        }

        // ---- 結合 ----

        [TestMethod]
        public void 分割して結合すると元と同じ内容に戻る()
        {
            String src = CreateFile("photo.jpg", Logic.MergeBufferSize * 2 + 123);
            byte[] original = File.ReadAllBytes(src);
            String destPath;
            Assert.AreEqual(ProcessError.None, SplitAll(src, 700 * 1024, out destPath));
            File.Delete(src);

            ProcessError error = Logic.Merge(Logic.MakeMergeDestPath(src + ".div002"), () => true);

            Assert.AreEqual(ProcessError.None, error);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(src));
        }

        [TestMethod]
        public void 番号が途切れたところまでを結合する()
        {
            String dest = Path.Combine(_workDir, "out.txt");
            File.WriteAllText(dest + ".div001", "AB");
            File.WriteAllText(dest + ".div002", "CD");
            File.WriteAllText(dest + ".div004", "XX");

            Assert.AreEqual(ProcessError.None, Logic.Merge(dest, () => true));

            Assert.AreEqual("ABCD", File.ReadAllText(dest));
        }

        [TestMethod]
        public void 既存の結合先は上書きする()
        {
            String dest = Path.Combine(_workDir, "out.txt");
            File.WriteAllText(dest, "OLD-CONTENT-LONGER");
            File.WriteAllText(dest + ".div001", "NEW");

            Assert.AreEqual(ProcessError.None, Logic.Merge(dest, () => true));

            Assert.AreEqual("NEW", File.ReadAllText(dest));
        }

        [TestMethod]
        public void 結合中に停止されたら結合結果を消す()
        {
            String dest = Path.Combine(_workDir, "out.txt");
            File.WriteAllText(dest + ".div001", "AB");
            File.WriteAllText(dest + ".div002", "CD");
            int calls = 0;

            ProcessError error = Logic.Merge(dest, () => calls++ < 1);

            Assert.AreEqual(ProcessError.Aborted, error);
            Assert.IsFalse(File.Exists(dest));
            Assert.IsTrue(File.Exists(dest + ".div001"));
        }

        [TestMethod]
        public void 結合先が作れなければ作成エラー()
        {
            String dest = Path.Combine(_workDir, "folder");
            Directory.CreateDirectory(dest);
            File.WriteAllText(dest + ".div001", "AB");

            Assert.AreEqual(ProcessError.OpenDest, Logic.Merge(dest, () => true));
        }

        [TestMethod]
        public void フォルダもあるものとして扱う()
        {
            Assert.IsTrue(Logic.PathExists(_workDir));
            Assert.IsFalse(Logic.PathExists(Path.Combine(_workDir, "none")));
            Assert.IsTrue(Logic.PathExists(CreateFile("a.bin", 1)));
        }

        [TestMethod]
        public void 結合したファイルは分割ファイルを順に並べたもの()
        {
            String dest = Path.Combine(_workDir, "seq.bin");
            for (int i = 1; i <= 12; i++)
            {
                File.WriteAllBytes(Logic.MakePartPath(dest, i), new[] { (byte)i });
            }

            Assert.AreEqual(ProcessError.None, Logic.Merge(dest, () => true));

            CollectionAssert.AreEqual(Enumerable.Range(1, 12).Select(i => (byte)i).ToArray(), File.ReadAllBytes(dest));
        }
    }
}
