using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DivisionCoupling
{
    /// <summary>
    /// 分割・結合の結果。MFC版DivisionCouplingDlg::Errorと同じ並び。
    /// </summary>
    internal enum ProcessError
    {
        None,
        OpenSource,     // 元ファイルが開けない
        OpenDest,       // 出力ファイルが作れない
        Alloc,          // 作業メモリが確保できない
        DivSize,        // 分割サイズが不正
        Aborted,        // 「停止」または終了で中断した
    }

    /// <summary>
    /// MFC版division_coupling_dlg.ccのSplit/Mergeと、ファイル名の組み立てを移したもの。
    /// 画面に依存しないようにして、一時フォルダを使ったテストで確認できるようにしている。
    /// </summary>
    internal static class Logic
    {
        public const int Kilobyte = 1024;

        // 結合で1回に読み書きする大きさ
        public const int MergeBufferSize = 1024 * Kilobyte;

        // 分割ファイル名（例: fname.jpg → fname.jpg.div001）
        public static String MakePartPath(String path, int index)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}.div{1:000}", path, index);
        }

        // 選んだ分割ファイルから最後の拡張子を外したものが結合後のファイル名
        // 例: fname.jpg.div001 → fname.jpg
        // （MFC版と同じく、フォルダ名の '.' も含めてパス全体の最後の '.' で切る）
        public static String MakeMergeDestPath(String selectedPath)
        {
            int dot = selectedPath.LastIndexOf('.');
            return (dot >= 0) ? selectedPath.Substring(0, dot) : selectedPath;
        }

        // 「分割サイズ（KB）」欄の値。MFC版のGetDlgItemIntと同じく、読めない値・intに収まらない値は0にする
        public static int ParseDivSizeKb(String text)
        {
            int value;
            if (!Int32.TryParse(text.TrimStart(' '), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
            {
                return 0;
            }
            return value;
        }

        // KB → バイト。intに収まらない値は分割サイズエラー(0)にする
        public static int ToDivSize(int divSizeKb)
        {
            return (0 < divSizeKb && divSizeKb <= Int32.MaxValue / Kilobyte) ? divSizeKb * Kilobyte : 0;
        }

        // MFC版のPathFileExistsと同じく、フォルダでも「ある」とみなす
        public static Boolean PathExists(String path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        /// <summary>
        /// srcPathをdivSizeバイトごとに srcPath.div001, .div002 … へ分割する。
        /// isRunningがfalseを返したら中断し、途中まで作った分割ファイルは消す
        /// （欠けたまま残ると、結合したときに欠けたファイルが黙ってできてしまうため）。
        /// destPathには最後に作ろうとした分割ファイル名を返す（エラー表示に使う）。
        /// </summary>
        public static ProcessError Split(String srcPath, int divSize, Func<Boolean> isRunning,
            Action<int> setRange, Action<int> setPos, out String destPath)
        {
            destPath = String.Empty;
            if (divSize <= 0)
            {
                return ProcessError.DivSize;
            }

            FileStream srcFile;
            if (!TryOpen(srcPath, FileMode.Open, FileAccess.Read, out srcFile))
            {
                return ProcessError.OpenSource;
            }

            ProcessError error = ProcessError.None;
            using (srcFile)
            {
                // 読み込みバッファは分割サイズで1回だけ確保する
                byte[] buffer;
                if (!TryAlloc(divSize, out buffer))
                {
                    return ProcessError.Alloc;
                }

                long restSize = srcFile.Length;
                int partCount = (int)((restSize + divSize - 1) / divSize);    // 切り上げ
                setRange(partCount);

                // 途中で終わったときに消せるよう、作った分割ファイルを覚えておく
                List<String> createdParts = new List<String>();

                for (int index = 1; restSize > 0 && isRunning(); index++)
                {
                    destPath = MakePartPath(srcPath, index);
                    FileStream destFile;
                    if (!TryOpen(destPath, FileMode.Create, FileAccess.Write, out destFile))
                    {
                        error = ProcessError.OpenDest;
                        break;
                    }
                    createdParts.Add(destPath);

                    int size = (restSize > divSize) ? divSize : (int)restSize;

                    // 小さい分割ファイルはFileStreamのバッファに溜まり、閉じるときに書き出されるので、
                    // usingごと囲って閉じるときの書き込み失敗も受ける
                    try
                    {
                        using (destFile)
                        {
                            int read;
                            try
                            {
                                read = ReadFully(srcFile, buffer, size);
                            }
                            catch (IOException)
                            {
                                // 元ファイルが読めなくなった(リムーバブルディスクが抜かれた等)
                                error = ProcessError.OpenSource;
                                break;
                            }
                            destFile.Write(buffer, 0, read);
                        }
                    }
                    catch (IOException)
                    {
                        // 書き込めない(ディスクがいっぱい等)のも、出力ファイルが作れなかった扱いにする
                        error = ProcessError.OpenDest;
                        break;
                    }
                    restSize -= size;

                    setPos(index);
                }

                // 分割ファイルが欠けたまま残ると、結合したときに欠けたファイルが黙ってできてしまう
                if (restSize > 0)
                {
                    if (error == ProcessError.None)
                    {
                        error = ProcessError.Aborted;
                    }
                    foreach (String part in createdParts)
                    {
                        DeleteQuietly(part);
                    }
                }
            }
            return error;
        }

        /// <summary>
        /// destPath.div001 から番号順に、ファイルが無くなるまで destPath へ連結する。
        /// 結合先の決定と上書きの確認は呼び出し側(画面)で済ませておくこと。
        /// 途中で終わった結合結果は欠けているので残さない。
        /// </summary>
        public static ProcessError Merge(String destPath, Func<Boolean> isRunning)
        {
            // 分割ファイルの大きさによらず、決まった大きさのバッファで少しずつ写す
            byte[] buffer;
            if (!TryAlloc(MergeBufferSize, out buffer))
            {
                return ProcessError.Alloc;
            }

            FileStream destFile;
            if (!TryOpen(destPath, FileMode.Create, FileAccess.Write, out destFile))
            {
                return ProcessError.OpenDest;
            }

            ProcessError error = ProcessError.None;
            try
            {
                using (destFile)
                {
                    for (int index = 1; ; index++)
                    {
                        if (!isRunning())
                        {
                            error = ProcessError.Aborted;
                            break;
                        }

                        FileStream partFile;
                        if (!TryOpen(MakePartPath(destPath, index), FileMode.Open, FileAccess.Read, out partFile))
                        {
                            break;
                        }

                        using (partFile)
                        {
                            try
                            {
                                int readSize;
                                while ((readSize = partFile.Read(buffer, 0, MergeBufferSize)) > 0)
                                {
                                    destFile.Write(buffer, 0, readSize);
                                }
                            }
                            catch (IOException)
                            {
                                error = ProcessError.OpenDest;
                                break;
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
                // 最後の書き込みはFileStreamのバッファに残っていて、閉じるときに書き出される。
                // そこで失敗(ディスクがいっぱい等)しても、欠けた結合結果を残さない
                error = ProcessError.OpenDest;
            }

            if (error != ProcessError.None)
            {
                DeleteQuietly(destPath);
            }
            return error;
        }

        // CFile::Openと同じく、開けなければ例外ではなくfalseで返す
        private static Boolean TryOpen(String path, FileMode mode, FileAccess access, out FileStream stream)
        {
            stream = null;
            try
            {
                FileShare share = (access == FileAccess.Read) ? FileShare.ReadWrite : FileShare.Read;
                stream = new FileStream(path, mode, access, share);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                || ex is ArgumentException || ex is NotSupportedException || ex is System.Security.SecurityException)
            {
                return false;
            }
        }

        // mallocの失敗(NULL)に相当するのは、C#ではOutOfMemoryException
        private static Boolean TryAlloc(int size, out byte[] buffer)
        {
            try
            {
                buffer = new byte[size];
                return true;
            }
            catch (OutOfMemoryException)
            {
                buffer = null;
                return false;
            }
        }

        // FileStream.Readは要求より少なく返すことがあるので、size分そろうまで読む
        private static int ReadFully(Stream stream, byte[] buffer, int size)
        {
            int total = 0;
            while (total < size)
            {
                int read = stream.Read(buffer, total, size - total);
                if (read <= 0)
                {
                    break;
                }
                total += read;
            }
            return total;
        }

        private static void DeleteQuietly(String path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // ::DeleteFileと同じく、消せなくても続ける
            }
        }
    }
}
