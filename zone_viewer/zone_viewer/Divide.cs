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
            // Initialize variables allocate memory
            string file_name;
            double right, left, front, back, top, bot;
            double level_height, thick_low, thick_high;
            View docview;
            View3D view3d = null;
            Level this_level, slab_level, area_level, plan_level;
            Floor this_slab;
            Area area_cur;
            ViewPlan view_plan;
            AreaScheme plan_scheme;
            BoundingBoxXYZ area_bound;
            BoundingBoxXYZ section_bound = new BoundingBoxXYZ();
            NavisworksExportOptions opt = new NavisworksExportOptions();

            // check if areas exist
            ICollection<ElementId> areas = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement)).OfCategory(BuiltInCategory.OST_Areas).ToElementIds();
            if (areas.Count == 0) {
                TaskDialog.Show("Process cancelled:", "No zones found in this document. Please define zones first.");
                return Result.Cancelled;
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
            ICollection<ElementId> slabs = new FilteredElementCollector(doc).OfClass(typeof(Floor)).OfCategory(BuiltInCategory.OST_Floors).ToElementIds();
            IList<ElementId> levels_id = new List<ElementId>();
            IList<double> elevations = new List<double>();
            IList<double> slabs_thick = new List<double>();
            foreach (ElementId level_id in levels) {
                levels_id.Add(level_id);
                this_level = doc.GetElement(level_id) as Level;
                elevations.Add(this_level.Elevation);
                foreach (ElementId slab_id in slabs) {
                    this_slab = doc.GetElement(slab_id) as Floor;
                    slab_level = doc.GetElement(this_slab.LevelId) as Level;
                    if (slab_level.Name == this_level.Name) {
                        slabs_thick.Add(this_slab.get_BoundingBox(null).Max.Z - this_slab.get_BoundingBox(null).Min.Z);
                    }
                }
            }
            
            ICollection<ElementId> area_planViews = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).ToElementIds();
            ICollection<ElementId> area_schemes = new FilteredElementCollector(doc).OfClass(typeof(AreaScheme)).OfCategory(BuiltInCategory.OST_AreaSchemes).ToElementIds();
            AreaScheme rentable = doc.GetElement(area_schemes.First()) as AreaScheme;
            
            // loop throu each area
            foreach (ElementId area_id in areas) {
                area_cur = doc.GetElement(area_id) as Area;
                area_level = area_cur.Level;
                foreach (ElementId viewPlan_id in area_planViews) {
                    view_plan = doc.GetElement(viewPlan_id) as ViewPlan;
                    plan_level = doc.GetElement(view_plan.LevelId) as Level;
                    plan_scheme = view_plan.AreaScheme;
                    // find the view that contains area and get the bbox
                    if (view_plan.ViewName == area_level.Name && view_plan.ViewType.ToString() == "AreaPlan") {
                        area_bound = area_cur.get_BoundingBox(view_plan);
                        front = area_bound.Max.Y;
                        right = area_bound.Max.X;
                        back = area_bound.Min.Y;
                        left = area_bound.Min.X;
                        bot = area_level.Elevation;
                        top = 0;
                        thick_low = 0;
                        thick_high = 0;
                        foreach (ElementId level_id in levels_id) {
                            this_level = doc.GetElement(level_id) as Level;
                            if (this_level.Name == area_level.Name) {
                                int cur_idx = levels_id.IndexOf(level_id);
                                if (cur_idx+1 < levels_id.Count) {
                                    level_height = elevations.ElementAt(cur_idx + 1) - elevations.ElementAt(cur_idx);
                                } else {
                                    level_height = 500;
                                }
                                if (cur_idx+1 < slabs_thick.Count) {
                                    thick_low = slabs_thick.ElementAt(cur_idx);
                                    thick_high = slabs_thick.ElementAt(cur_idx + 1);
                                } else {
                                    thick_low = slabs_thick.ElementAt(cur_idx);
                                    thick_high = 0;
                                }
                                top = bot + level_height;
                                break;
                            }
                        }
                        section_bound.Min = new XYZ(left, back, bot - thick_low);
                        section_bound.Max = new XYZ(right, front, top - thick_high);

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
                        //// structure division
                        //FilteredElementCollector slab_filter = new FilteredElementCollector(doc).WherePasses(new ElementClassFilter(typeof(Floor), false));
                        //FilteredElementCollector roof_filter = new FilteredElementCollector(doc).WherePasses(new ElementClassFilter(typeof(FootPrintRoof), false));
                        //FilteredElementCollector wallFoundation_filter = new FilteredElementCollector(doc).WherePasses(new ElementClassFilter(typeof(WallFoundation), false));
                        //FilteredElementCollector foundation_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_StructuralFoundation, false));
                        //FilteredElementCollector column_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns, false));
                        //FilteredElementCollector frame_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming, false));
                        //FilteredElementCollector truss_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_StructuralTruss, false));
                        //FilteredElementCollector massWall_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_MassWallsAll, false));
                        //slab_filter.UnionWith(roof_filter).UnionWith(wallFoundation_filter).UnionWith(foundation_filter).UnionWith(column_filter).UnionWith(frame_filter).UnionWith(truss_filter).UnionWith(massWall_filter);
                        //using (Transaction t = new Transaction(doc, "Filter Element")) {
                        //    t.Start();
                        //    view3d.IsolateElementsTemporary(slab_filter.ToElementIds());
                        //    t.Commit();
                        //}
                        //string file_name_structure = area_level.Name + "_" + area_cur.Name + "_structure";
                        //doc.Export(folder_name, file_name_structure, opt);
                        //using (Transaction t = new Transaction(doc, "Reset View")) {
                        //    t.Start();
                        //    View3D current_view = doc.ActiveView as View3D;
                        //    current_view.TemporaryViewModes.DeactivateAllModes();
                        //    t.Commit();
                        //}


                        //// architecture model
                        //FilteredElementCollector wall_filter = new FilteredElementCollector(doc).WherePasses(new ElementClassFilter(typeof(Wall), false));
                        //FilteredElementCollector wallType_filter = new FilteredElementCollector(doc).WherePasses(new ElementClassFilter(typeof(WallType), false));
                        //FilteredElementCollector ceiling_filter = new FilteredElementCollector(doc).WherePasses(new ElementClassFilter(typeof(Ceiling), false));
                        //FilteredElementCollector curtainSys_filter = new FilteredElementCollector(doc).WherePasses(new ElementClassFilter(typeof(CurtainSystem), false));
                        //FilteredElementCollector archiFloor_filter = new FilteredElementCollector(doc).WherePasses(new ElementClassFilter(typeof(CeilingAndFloor), false));
                        //FilteredElementCollector window_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_Windows, false));
                        //FilteredElementCollector door_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_Doors, false));
                        //FilteredElementCollector stackWall_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_StackedWalls, false));
                        //FilteredElementCollector curtainWall_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_CurtainGrids, false));
                        //FilteredElementCollector CurtainGridsRoof_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_CurtainGridsRoof, false));
                        //FilteredElementCollector CurtainGridsSystem_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_CurtainGridsSystem, false));
                        //FilteredElementCollector CurtainGridsWall_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_CurtainGridsWall, false));
                        //FilteredElementCollector CurtainWallMullions_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallMullions, false));
                        //FilteredElementCollector CurtainWallPanels_filter = new FilteredElementCollector(doc).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallPanels, false));
                        //wall_filter.UnionWith(ceiling_filter).UnionWith(wallType_filter).UnionWith(curtainSys_filter).UnionWith(archiFloor_filter).UnionWith(window_filter).UnionWith(door_filter).UnionWith(stackWall_filter).UnionWith(curtainWall_filter).UnionWith(CurtainGridsRoof_filter).UnionWith(CurtainGridsSystem_filter).UnionWith(CurtainGridsWall_filter).UnionWith(CurtainWallMullions_filter).UnionWith(CurtainWallPanels_filter);
                        //using (Transaction t = new Transaction(doc, "Filter Element"))
                        //{
                        //    t.Start();
                        //    view3d.IsolateElementsTemporary(wall_filter.ToElementIds());
                        //    t.Commit();
                        //}
                        //string file_name_archi = area_level.Name + "_" + area_cur.Name;
                        //doc.Export(folder_name, file_name_archi, opt);
                        //using (Transaction t = new Transaction(doc, "Reset View"))
                        //{
                        //    t.Start();
                        //    View3D current_view = doc.ActiveView as View3D;
                        //    current_view.TemporaryViewModes.DeactivateAllModes();
                        //    t.Commit();
                        //}

                        // MEP to be done



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
                    opt.ExportRoomAsAttribute = true;
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

