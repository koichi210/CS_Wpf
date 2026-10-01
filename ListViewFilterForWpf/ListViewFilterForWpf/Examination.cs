using System;

namespace ListViewFilterForWpf
{
    public class Examination
    {
        public Int32 Id { get; set; }
        public String Subject { get; set; }
        public Int32 Point { get; set; }
        public String UserName { get; set; }
        public String ClassName { get; set; }

        public override String ToString()
        {
            return "${Id} - ${Subject} - ${Point} - ${UserName} - ${ClassName}";
        }
    }
}
