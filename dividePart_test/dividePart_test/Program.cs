using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace jsonDeserializer
{
    public class zoneAllocation
    {
        public Dictionary<string, zone> Zones { set; get; }
    }

    public class zone
    {
        public string top { get; set; }
        public string bottom { get; set; }
        public string left { get; set; }
        public string right { get; set; }
    }

    //class Program
    //{
    //    static void Main(string[] args)
    //    {
    //        // import a file into code
    //        // change path to load different files
    //        string path = @"D:\Documents\Revit Model\api_input_test.json";

    //        try
    //        {   // Open the text file using a stream reader.
    //            using (StreamReader file = File.OpenText(path))
    //            {
    //                Console.WriteLine("inside file");
    //                var res = JsonConvert.DeserializeObject<zoneAllocation>(File.ReadAllText(path));
    //                Console.WriteLine("res created");
    //                foreach (var f in res.Zones)
    //                {
    //                    Console.WriteLine("inside forloop");
    //                    Console.WriteLine("zone: {0}",f.Key);
    //                    Console.WriteLine("top: {0}", f.Value.top);
    //                    Console.WriteLine("bottom: {0}", f.Value.bottom);
    //                    Console.WriteLine("left: {0}", f.Value.left);
    //                    Console.WriteLine("right: {0}", f.Value.right);
    //                }

    //            }
    //        }
    //        catch (Exception e)
    //        {
    //            Console.WriteLine("The file could not be read:");
    //            Console.WriteLine(e.Message);
    //        }


    //    }
    //}
}
