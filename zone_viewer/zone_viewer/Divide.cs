#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
#endregion

namespace zone_viewer {

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class HelloWorld : IExternalCommand {
        public Result Execute(ExternalCommandData cmdData, ref string msg, ElementSet elems) {
            TaskDialog.Show("API description: ", "Click 'Define Zones' to define Zones\n" +
                "Click 'Export Zones' to export Zones as NWC\n" +
                "Click 'Export Rooms' to export Rooms as NWC\n");
            return Result.Succeeded;
        }
    }


    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateZonePlan : IExternalCommand {
        public Result Execute(ExternalCommandData cmdData, ref string msg, ElementSet elems) {
            UIApplication uiapp = cmdData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            ViewPlan area_plan;

            ICollection<ElementId> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).OfCategory(BuiltInCategory.OST_Levels).ToElementIds();
            if (levels.Count == 0) {
                TaskDialog.Show("Process cancelled:", "No levels found in this document. Please create levels first.");
                return Result.Cancelled;
            }

            ICollection<ElementId> area_scheme = new FilteredElementCollector(doc).OfClass(typeof(AreaScheme)).OfCategory(BuiltInCategory.OST_AreaSchemes).ToElementIds();
            IList<ViewPlan> area_plans = new List<ViewPlan>();
            foreach (ElementId l_id in levels) {
                using (Transaction t = new Transaction(doc, "Create Zone Plans")) {
                    t.Start();
                    area_plan = ViewPlan.CreateAreaPlan(doc, area_scheme.First(), l_id);
                    area_plans.Add(area_plan);
                    t.Commit();
                }
            }

            View first_area_view = area_plans[0] as View;
            uidoc.RequestViewChange(first_area_view);
            TaskDialog.Show(levels.Count.ToString() + "Area Plans created:", "Please define zones in area plans.");
            return Result.Succeeded;
        }

    }





    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DivideBox : IExternalCommand {

        public Result Execute(ExternalCommandData cmdData, ref string msg, ElementSet elems) {
            UIApplication uiapp = cmdData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            string path = doc.PathName;
            string title = doc.Title;
            string folder_name1 = path.Replace(title + ".rvt", "");
            string folder_name = folder_name1.Replace(title + ".RVT", "");
            //string folder_name = "C:\\Users\\dyang\\Documents\\test_models\\";
            // Initialize variables allocate memory
            string file_name;
            double right, left, front, back;
            double top = 1;
            double bot = 0;
            View docview;
            View3D view3d = null;
            Level this_level, area_level, next_level;
            Area area_cur;
            ViewPlan view_plan;
            BoundingBoxXYZ area_bound;
            BoundingBoxXYZ section_bound = new BoundingBoxXYZ();
            NavisworksExportOptions opt = new NavisworksExportOptions();

            // check if areas exist
            ICollection<ElementId> areas = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement)).OfCategory(BuiltInCategory.OST_Areas).ToElementIds();
            if (areas.Count == 0) {
                TaskDialog.Show("Process cancelled:", "No zones found in this document. Please define zones first.");
                return Result.Cancelled;
            } else {
                TaskDialog.Show("area count", areas.Count + " areas found");
            }
            // find the default 3D view
            ICollection<ElementId> views = new FilteredElementCollector(doc).OfClass(typeof(View3D)).ToElementIds();
            foreach (ElementId docview_id in views) {
                docview = doc.GetElement(docview_id) as View;
                if (docview.Name.ToString() == "{3D}") {
                    view3d = docview as View3D;
                    uidoc.ActiveView = view3d;
                    break;
                }
            }
            if (view3d == null) {
                TaskDialog.Show("Process cancelled:", "Cannot find the default 3D View, the export cannot proceed.");
                return Result.Cancelled;
            }

            // prepare levels, areas, views, thickness
            ICollection<ElementId> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).OfCategory(BuiltInCategory.OST_Levels).ToElementIds();
            IList<ElementId> levels_list = levels.ToList<ElementId>();
            ICollection<ElementId> area_planViews = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).ToElementIds();

            // loop throu each area
            int a_count = 0;
            foreach (ElementId area_id in areas) {
                a_count += 1;
                area_cur = doc.GetElement(area_id) as Area;
                area_level = area_cur.Level;
                //TaskDialog.Show("!", "found area on "+area_level.Name);
                foreach (ElementId viewPlan_id in area_planViews) {
                    view_plan = doc.GetElement(viewPlan_id) as ViewPlan;
                    // find the view that contains area and get the bbox
                    //if (view_plan.ViewName == area_level.Name && view_plan.ViewType.ToString() == "AreaPlan") {
                    if (view_plan.LevelId == area_level.LevelId && view_plan.ViewType.ToString() == "AreaPlan") {

                        //view_plan = doc.GetElement(area_level.FindAssociatedPlanViewId()) as View;
                        area_bound = area_cur.get_BoundingBox(view_plan);
                        //area_bound = area_cur.get_BoundingBox(null);
                        //TaskDialog.Show("!", area_cur.Name + " " + area_cur.Area + " " + view_plan.Name);
                        Debug.Print(view_plan.Name);
                        if (area_bound == null) { // check if bound is null by any reason
                            Debug.Print(a_count.ToString() + ". " + area_cur.Name + " has no bound");
                            continue;
                        }

                        bot = area_level.Elevation;
                        int cur_idx = levels_list.IndexOf(area_level.Id);
                        next_level = doc.GetElement(levels_list.ElementAt(cur_idx + 1)) as Level;
                        top = next_level.Elevation;

                        front = area_bound.Max.Y;
                        right = area_bound.Max.X;
                        back = area_bound.Min.Y;
                        left = area_bound.Min.X;
                        section_bound.Min = new XYZ(left, back, bot);
                        section_bound.Max = new XYZ(right, front, top);
                        TaskDialog.Show("box height", (top - bot).ToString());
                        // set section box and export
                        using (Transaction t = new Transaction(doc, "Set View")) {
                            t.Start();
                            view3d.SetSectionBox(section_bound);
                            t.Commit();
                        }
                        opt.DivideFileIntoLevels = true;
                        opt.ExportElementIds = true;
                        opt.ExportParts = true;
                        opt.ExportRoomAsAttribute = false;
                        opt.ExportRoomGeometry = false;
                        opt.ExportScope = NavisworksExportScope.View;
                        opt.ViewId = view3d.Id;
                        file_name = area_level.Name + "_" + area_cur.Name;
                        doc.Export(folder_name, file_name, opt);
                    }

                }
            }
            using (Transaction t = new Transaction(doc, "Reset View")) {
                t.Start();
                view3d.IsSectionBoxActive = false;
                //current_view.TemporaryViewModes.DeactivateAllModes();
                t.Commit();
            }
            TaskDialog.Show("Zones exported successfully", areas.Count.ToString() + " NWC files are stored at " + folder_name);
            return Result.Succeeded;
        }
    }



    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DivideRoom : IExternalCommand
    {

        public Result Execute(ExternalCommandData cmdData, ref string msg, ElementSet elems)
        {
            UIApplication uiapp = cmdData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            string path = doc.PathName;
            string title = doc.Title;
            string folder_name1 = path.Replace(title + ".rvt", "");
            string folder_name = folder_name1.Replace(title + ".RVT", "");

            // initialize variables and allocate memory
            string file_name;
            SpatialElement room;
            View docview;
            View3D view3d = null;
            BoundingBoxXYZ section_bound = new BoundingBoxXYZ();
            NavisworksExportOptions opt = new NavisworksExportOptions();

            // find the default 3D view
            ICollection<ElementId> views = new FilteredElementCollector(doc).OfClass(typeof(View3D)).ToElementIds();
            foreach (ElementId docview_id in views) {
                docview = doc.GetElement(docview_id) as View;
                if (docview.Name.ToString() == "{3D}"){
                    view3d = docview as View3D;
                    uidoc.ActiveView = view3d;
                    break;
                }
            }
            if (view3d == null) {
                TaskDialog.Show("Process cancelled:", "Cannot find the default 3D View, the export cannot proceed.");
                return Result.Cancelled;
            }

            // find rooms
            ICollection<ElementId> room_ids = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement)).OfCategory(BuiltInCategory.OST_Rooms).ToElementIds();
            if (room_ids.Count == 0) {
                TaskDialog.Show("Process cancelled:", "No rooms found in this document. Please create rooms first.");
                return Result.Cancelled;
            }
            // export rooms
            foreach (ElementId room_id in room_ids){
                room = doc.GetElement(room_id) as SpatialElement;
                //TaskDialog.Show("room name", room.Name);
                section_bound = room.get_BoundingBox(null);
                if (section_bound == null){
                    continue;
                }else{
                    //TaskDialog.Show("xMax", room.);
                    using (Transaction t = new Transaction(doc, "Set View"))
                    {
                        t.Start();
                        view3d.SetSectionBox(section_bound);
                        t.Commit();
                    }
                    opt.DivideFileIntoLevels = true;
                    opt.ExportElementIds = true;
                    opt.ExportParts = true;
                    opt.ExportRoomAsAttribute = false;
                    opt.ExportRoomGeometry = false;
                    opt.ExportScope = NavisworksExportScope.View;
                    opt.ViewId = view3d.Id;
                    file_name = title + "_" + room.Name;
                    doc.Export(folder_name, file_name, opt);
                }
            }

            // reset view to default 3d
            using (Transaction t = new Transaction(doc, "Reset View"))
            {
                t.Start();
                view3d.IsSectionBoxActive = false;
                t.Commit();
            }
            TaskDialog.Show("Rooms exported successfully", room_ids.Count.ToString()+" NWC files are stored at " +folder_name);
            return Result.Succeeded;
        }
    }
}

