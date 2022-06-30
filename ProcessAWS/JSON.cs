using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessAWS
{
    public class JSON
    {
    public string StationNo { get; set; }
    public int ProjectID { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }

        public float Value { get; set; }

        public DateTime DataTime { get; set; }

        public float RainTotal { get; set; }
      

    }
}
