#region Namespaces
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion

namespace zone_viewer {
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    class App : IExternalApplication {
        //static AddInId m_appId = new AddInId(new Guid("356CDA5A-E6C5-4c2f-A9EF-B3222116B8C8"));

        // get the absolute path of this assembly
        static string ExecutingAssemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

        //private AppDocEvents m_appDocEvents;



        public Result OnStartup(UIControlledApplication app) {

            //Snoop.Collectors.CollectorObj.InitializeCollectors();
            AddMenu(app);
            //AddAppDocEvents(app.ControlledApplication);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) {

            //RemoveAppDocEvents();
            return Result.Succeeded;
        }




        private void AddMenu(UIControlledApplication app) {
            RibbonPanel rvtRibbonPanel = app.CreateRibbonPanel("Zone Viewer");
            PulldownButtonData data = new PulldownButtonData("Options", "Zone Viewer");

            RibbonItem item = rvtRibbonPanel.AddItem(data);
            PulldownButton optionsBtn = item as PulldownButton;

            // Add Icons to main zone_viewer Menu

            optionsBtn.Image = new BitmapImage(new Uri(@"D:\Documents\auto4dbim\zone_viewer\zone_viewer\Resources\raamac-16.png"));

            optionsBtn.LargeImage = new BitmapImage(new Uri(@"D:\Documents\auto4dbim\zone_viewer\zone_viewer\Resources\raamac-32.png"));

            optionsBtn.AddPushButton(new PushButtonData("API Description", "API Description...", ExecutingAssemblyPath, "zone_viewer.HelloWorld"));
           
            optionsBtn.AddPushButton(new PushButtonData("Create Zones", "Create Zones...", ExecutingAssemblyPath, "zone_viewer.CreateZonePlan"));
            optionsBtn.AddPushButton(new PushButtonData("Divide Box", "Divide Box...", ExecutingAssemblyPath, "zone_viewer.DivideBox"));
          
        }

        static BitmapSource GetEmbeddedImage(string name) {
            try {
                Assembly a = Assembly.GetExecutingAssembly();
                Stream s = a.GetManifestResourceStream(name);
                return BitmapFrame.Create(s);
            }
            catch {
                return null;
            }
        }
    }
}
