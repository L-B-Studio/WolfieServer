using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMessenger.Model
{
    public class JsonPackage
    {
        public string header { get; set; }
        public Dictionary<string, string> body { get; set; }
    }
}
