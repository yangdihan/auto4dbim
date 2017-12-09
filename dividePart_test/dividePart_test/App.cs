#region Namespaces
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion

namespace dividePart_test
{
    class App : IExternalApplication
    {
        //static string ExecutingAssemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        //private AppDocEvents m_appDocEvents;
        public Result OnStartup(UIControlledApplication app)
        {
            //AddMenu(app);
            //AddAppDocEvents(app.ControlledApplication);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }



        //private void AddMenu(UIControlledApplication app) {
        //    RibbonPanel rvtRibbonPanel = app.CreateRibbonPanel("Divide Zones");
        //    PulldownButtonData data = new PulldownButtonData("Options", "Divide Zones");

        //    RibbonItem item = rvtRibbonPanel.AddItem(data);
        //    PulldownButton optionsBtn = item as PulldownButton;

        //    // Add Icons to main RevitLookup Menu
        //    optionsBtn.Image = GetEmbeddedImage("dividePart_test.Resources.logo_large.png");
        //    //optionsBtn.LargeImage = GetEmbeddedImage("RevitLookup.Resources.RLookup-32.png");

        //    optionsBtn.AddPushButton(new PushButtonData("HelloWorld", "Hello World...", ExecutingAssemblyPath, "RevitLookup.HelloWorld"));
        //}

        //private void AddAppDocEvents(ControlledApplication app) {
        //    m_appDocEvents = new AppDocEvents(app);
        //    m_appDocEvents.EnableEvents();
        //}




        //static BitmapSource GetEmbeddedImage(string name) {
        //    try {
        //        Assembly a = Assembly.GetExecutingAssembly();
        //        Stream s = a.GetManifestResourceStream(name);
        //        return BitmapFrame.Create(s);
        //    }
        //    catch {
        //        return null;
        //    }
        //}
    }
}
