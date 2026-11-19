using System.ComponentModel;
using System.IO;
using System.Windows.Media;

namespace ImageViewer
{
    public sealed class Thumbnail : INotifyPropertyChanged
    {
        public string FileFullPath { get; set; }
        public string FileName { get; set; }

        // 動画ファイルか(一覧のサムネイルはシェルのサムネイル、下の大きな表示は指定した位置のコマになる)
        public bool IsVideo { get; private set; }

        private ImageSource _thumbnailImage;

        // 一覧に出す小さな画像。読み込み用のスレッドで順に作って入れていく(読めなければnullのまま)
        public ImageSource ThumbnailImage
        {
            get { return _thumbnailImage; }
            set
            {
                _thumbnailImage = value;
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs("ThumbnailImage"));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Thumbnail(string fileFullPath)
        {
            FileFullPath = fileFullPath;
            FileName = Path.GetFileName(fileFullPath);
            IsVideo = ImageFiles.IsVideoFile(fileFullPath);
        }
    }
}
