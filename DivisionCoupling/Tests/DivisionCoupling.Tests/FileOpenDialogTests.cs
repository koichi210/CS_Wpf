using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DivisionCoupling.Tests
{
    /// <summary>
    /// FileOpenDialog（MFC版と同じOFN_CREATEPROMPT付きの「開く」ダイアログ）の指定内容のテスト。
    /// ダイアログそのものは出さず、GetOpenFileNameへ渡すフラグ・フィルタ・構造体の形だけを確認する。
    /// </summary>
    [TestClass]
    public class FileOpenDialogTests
    {
        [TestMethod]
        public void 無いファイル名なら作成するか聞くフラグが付いている()
        {
            Assert.AreEqual(FileOpenDialog.OFN_CREATEPROMPT, FileOpenDialog.Flags & FileOpenDialog.OFN_CREATEPROMPT);
        }

        [TestMethod]
        public void MFC版と同じくファイルとパスの存在チェックは付けない()
        {
            // 付けると「作成しますか?」の前に「ファイルが見つかりません」で止められてしまう
            Assert.AreEqual(0, FileOpenDialog.Flags & FileOpenDialog.OFN_FILEMUSTEXIST);
            Assert.AreEqual(0, FileOpenDialog.Flags & FileOpenDialog.OFN_PATHMUSTEXIST);
        }

        [TestMethod]
        public void フックを付けずエクスプローラー形式で出す()
        {
            // フックを付けると古い見た目のダイアログになり、MFC版(Vistaスタイル)と見た目が変わる
            Assert.AreEqual(FileOpenDialog.OFN_EXPLORER, FileOpenDialog.Flags & FileOpenDialog.OFN_EXPLORER);
            Assert.AreEqual(0, FileOpenDialog.Flags & FileOpenDialog.OFN_ENABLEHOOK);
        }

        [TestMethod]
        public void フィルタは縦棒区切りをヌル文字区切りにする()
        {
            Assert.AreEqual("*.*\0*.*\0全て(*.*)\0*.*\0", FileOpenDialog.ToNativeFilter(FileOpenDialog.Filter));
        }

        [TestMethod]
        public void 既定の拡張子は付けない()
        {
            // MFC版の "*.*" を渡すと、拡張子なしの無い名前が「ファイル名は有効ではありません。」で選べなくなる
            FileOpenDialog.OPENFILENAME ofn = FileOpenDialog.CreateOpenFileName(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            Assert.AreEqual(IntPtr.Zero, ofn.lpstrDefExt);
        }

        [TestMethod]
        public void 構造体には渡したハンドルとバッファと決めたフラグが入る()
        {
            // 実メモリは使わないので、見分けの付く値を入れて素通しされるかだけ見る
            IntPtr owner = new IntPtr(0x1111);
            IntPtr filter = new IntPtr(0x2222);
            IntPtr file = new IntPtr(0x3333);

            FileOpenDialog.OPENFILENAME ofn = FileOpenDialog.CreateOpenFileName(owner, filter, file);

            Assert.AreEqual(Marshal.SizeOf(typeof(FileOpenDialog.OPENFILENAME)), ofn.lStructSize);
            Assert.AreEqual(owner, ofn.hwndOwner);
            Assert.AreEqual(filter, ofn.lpstrFilter);
            Assert.AreEqual(1, ofn.nFilterIndex);
            Assert.AreEqual(file, ofn.lpstrFile);
            Assert.AreEqual(FileOpenDialog.FileBufferLength, ofn.nMaxFile);
            Assert.AreEqual(FileOpenDialog.Flags, ofn.Flags);
            // フックは付けない（付けると古い見た目のダイアログになる）
            Assert.AreEqual(IntPtr.Zero, ofn.lpfnHook);
        }

        [TestMethod]
        public void 構造体の大きさがWin32のOPENFILENAMEWと同じ()
        {
            // 大きさがずれるとGetOpenFileNameが失敗してダイアログが出ない（x64は152、x86は88バイト）
            int expected = IntPtr.Size == 8 ? 152 : 88;
            Assert.AreEqual(expected, Marshal.SizeOf(typeof(FileOpenDialog.OPENFILENAME)));
        }
    }
}
