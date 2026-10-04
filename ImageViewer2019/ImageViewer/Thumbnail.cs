using System.IO;

namespace ImageViewer
{
    public sealed class Thumbnail
    {
        public string FileFullPath { get; set; }
        public string FileName { get; set; }

        public Thumbnail(string fileFullPath)
        {
            FileFullPath = fileFullPath;
            FileName = Path.GetFileName(fileFullPath);
        }
    }
}
