using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace jsonDeserializer
{
    public class levelAllocation
    {
        public Dictionary<string, zoneAllocation> Levels { set; get; }
    }

    public class zoneAllocation
    {
        public Dictionary<string, zone> Zones { set; get; }
    }

    public class zone
    {
        public string bottom { get; set; }
        public string left { get; set; }
        public string right { get; set; }
        public string top { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // import a file into code
            // change path to load different files
            string path = @"C:\Users\raamac\auto4dbim\jsonDeserializer\jsonDeserializer\parsed.json";

            try
            {   // Open the text file using a stream reader.
                using (StreamReader file = File.OpenText(path))
                {
                    Console.WriteLine("inside file");
                    var res = JsonConvert.DeserializeObject<levelAllocation>(File.ReadAllText(path));
                    Console.WriteLine("res created");
                    foreach (var f in res.Levels)
                    {
                        Console.WriteLine("inside forloop");
                        Console.WriteLine("level: {0}", f.Key);
                        foreach (var fi in f.Value.Zones)
                        {
                            Console.WriteLine("zone: {0}", fi.Key);
                            Console.WriteLine("top: {0}", fi.Value.top);
                            Console.WriteLine("bottom: {0}", fi.Value.bottom);
                            Console.WriteLine("left: {0}", fi.Value.left);
                            Console.WriteLine("right: {0}", fi.Value.right);
                        }
                    }

                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }

        }
    }
}
