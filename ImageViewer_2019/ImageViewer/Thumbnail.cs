using System.IO;

namespace ImageViewer
{
    public sealed class Thumbnail
    {
        public string FileFullpath { get; set; }
        public string FileName { get; set; }

        public Thumbnail(string fileFullPath)
        {
            this.FileFullpath = fileFullPath;
            this.FileName = Path.GetFileName(fileFullPath);
        }
    }
}
