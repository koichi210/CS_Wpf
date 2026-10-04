using System;

namespace ListViewFilter
{
    public class Examination
    {
        public int Id { get; set; }
        public string Subject { get; set; }
        public int Point { get; set; }
        public string UserName { get; set; }
        public string ClassName { get; set; }

        public override string ToString()
        {
            return $"{Id} - {Subject} - {Point} - {UserName} - {ClassName}";
        }
    }
}
