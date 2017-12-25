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
//using Newtonsoft.Json;
//using jsonDeserializer;
#endregion

namespace zone_viewer {
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    // copied class from the Building Coder

    public class Divide : IExternalCommand {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements) {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            //TaskDialog.Show("Revit", "Hello World");


            // reading JSON file:
            //string path = @"D:\Documents\Revit Model\parsed.json";
            //var res = JsonConvert.DeserializeObject<levelAllocation>(File.ReadAllText(path));

            // Create filters and collect walls/ slabs/ columns id in corresponding collector
            // walls
            IList<ElementId> walls_id = new List<ElementId>();
            FilteredElementCollector wall_collector = new FilteredElementCollector(doc).OfClass(typeof(Wall));
            foreach (Element w in wall_collector) {
                if (w is Wall) {
                    walls_id.Add(w.Id);
                }
            }
            // columns
            IList<ElementId> columns_id = new List<ElementId>();
            FilteredElementCollector column_collector = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns);
            foreach (Element c in column_collector) {
                if (c is FamilyInstance) {
                    columns_id.Add(c.Id);
                }
            }
            // slabs
            IList<ElementId> slabs_id = new List<ElementId>();
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Floor));
            foreach (Element e in collector) {
                if (e is Floor) {
                    slabs_id.Add(e.Id);
                }
            }

            // filter out all levels and grids and areas
            ICollection<ElementId> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).OfCategory(BuiltInCategory.OST_Levels).ToElementIds();
            ICollection<ElementId> grids = new FilteredElementCollector(doc).OfClass(typeof(Grid)).OfCategory(BuiltInCategory.OST_Grids).ToElementIds();
            ICollection<ElementId> areas = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement)).OfCategory(BuiltInCategory.OST_Areas).ToElementIds();

            //call create parts on walls/ slabs/ columns collectors
            using (Transaction t = new Transaction(doc, "Create Part")) {
                t.Start();
                // Create parts from the selected element
                // There is "CreateParts" but no "CreatePart", so needed to use a list containing the one element
                PartUtils.CreateParts(doc, walls_id);
                PartUtils.CreateParts(doc, columns_id);
                PartUtils.CreateParts(doc, slabs_id);
                t.Commit();
            }

            // start divide parts for walls, columns, slabs
            // divide walls
            foreach (ElementId w_id in walls_id) {
                ICollection<ElementId> partsList = PartUtils.GetAssociatedParts(doc, w_id, true, true);

                // Create a list of curves which needs to be used in DivideParts but for this example
                // the divide is being done by levels so the curve list will be empty
                IList<Curve> curve_list = new List<Curve>();

                // Get the host object corresponding to the selected element
                // HostObject is the parent class for walls, roof, floors, etc.
                HostObject hostObj = doc.GetElement(w_id) as HostObject;

                // Get the reference of one of the major faces of the selected element
                // Will be used to create a sketch plane
                Reference r = HostObjectUtils.GetSideFaces(hostObj, ShellLayerType.Exterior).First();

                using (Transaction t = new Transaction(doc, "Divide Part at Levels")) {
                    t.Start();
                    //Plane ref_plane = Plane.CreateByNormalAndOrigin(faceNormal, XYZ.Zero);
                    SketchPlane wall_sketchPlane = SketchPlane.Create(doc, r);
                    //SketchPlane sketchPlane = doc.Create.NewSketchPlane(r);
                    // Divide the parts
                    PartUtils.DivideParts(doc, partsList, levels, curve_list, wall_sketchPlane.Id);
                    t.Commit();
                }
            }

            // divide columns
            // since walls and columns are all divided by all levels, so just use the sketch-plane of the last wall element
            ElementId borrow_from_wall = walls_id[0];
            foreach (ElementId c_id in columns_id) {

                ICollection<ElementId> partsList = PartUtils.GetAssociatedParts(doc, c_id, true, true);
                IList<Curve> curve_list = new List<Curve>();
                HostObject hostObj = doc.GetElement(borrow_from_wall) as HostObject;
                Reference r = HostObjectUtils.GetSideFaces(hostObj, ShellLayerType.Exterior).First();

                using (Transaction t = new Transaction(doc, "Divide Part at Levels")) {
                    t.Start();
                    SketchPlane column_sketchPlane = SketchPlane.Create(doc, r);
                    PartUtils.DivideParts(doc, partsList, levels, curve_list, column_sketchPlane.Id);
                    t.Commit();
                }
            }

            //divide slabs by area
            foreach (ElementId s_id in slabs_id) {
                Floor this_slab = doc.GetElement(s_id) as Floor;
                ElementId slab_level_id = this_slab.LevelId;
                string slab_level = doc.GetElement(slab_level_id).Name;
                ICollection<ElementId> partsList = PartUtils.GetAssociatedParts(doc, s_id, true, true);
                BoundingBoxXYZ bbox = doc.GetElement(s_id).get_BoundingBox(view);
                double slab_top_face_z = bbox.Max.Z;

                IList<Curve> bound_box = new List<Curve>();
                foreach (ElementId area_id in areas) {
                    Area cur = doc.GetElement(area_id) as Area;
                    string area_level = cur.Level.Name;
                    if (area_level == slab_level) {
                        SpatialElementBoundaryOptions opt = new SpatialElementBoundaryOptions();
                        var boundaries = cur.GetBoundarySegments(opt);
                        foreach (var bl in boundaries) {
                            foreach (var s in bl) {
                                Curve c = s.GetCurve();
                                bound_box.Add(c);
                            }
                        }
                    }
                }
                HostObject hostObj = doc.GetElement(s_id) as HostObject;
                Reference r = HostObjectUtils.GetTopFaces(hostObj).First();
                ICollection<ElementId> intersectingReferenceIds = new List<ElementId>();
                if (bound_box.Count() == 0 && intersectingReferenceIds.Count() == 0) {
                    continue;
                }

                using (Transaction t = new Transaction(doc, "Divide Part by Areas")) {
                    t.Start();
                    //Transaction sketchPlaneTransaction = new Transaction(doc, "Create Sketch Plane");
                    SketchPlane grid_sketchPlane = SketchPlane.Create(doc, r);
                    //sketchPlaneTransaction.Commit();
                    PartUtils.DivideParts(doc, partsList, intersectingReferenceIds, bound_box, grid_sketchPlane.Id);
                    t.Commit();
                }
            }

            // Set the view's "Parts Visibility" parameter so that parts are shown
            Parameter p = doc.ActiveView.get_Parameter(BuiltInParameter.VIEW_PARTS_VISIBILITY);
            using (Transaction t = new Transaction(doc, "Set View Parameter")) {
                t.Start();
                p.Set(0); // 0 = Show Parts, 1 = Show Original, 2 = Show Both
                t.Commit();
            }
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

            ICollection<ElementId> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).OfCategory(BuiltInCategory.OST_Levels).ToElementIds();
            ICollection<ElementId> area_scheme = new FilteredElementCollector(doc).OfClass(typeof(AreaScheme)).OfCategory(BuiltInCategory.OST_AreaSchemes).ToElementIds();
            IList<ViewPlan> area_plans = new List<ViewPlan>();
            foreach (ElementId l_id in levels) {
                using (Transaction t = new Transaction(doc, "Create Zone Plans")) {
                    t.Start();
                    ViewPlan area_plan = ViewPlan.CreateAreaPlan(doc, area_scheme.First(), l_id);
                    area_plans.Add(area_plan);
                    t.Commit();
                }
            }
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
            View view = doc.ActiveView;

            ICollection<ElementId> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).OfCategory(BuiltInCategory.OST_Levels).ToElementIds();
            IList<ElementId> levels_id = new List<ElementId>();
            IList<double> elevations = new List<double>();
            foreach (ElementId level_id in levels) {
                levels_id.Add(level_id);
                Level this_level = doc.GetElement(level_id) as Level;
                elevations.Add(this_level.Elevation);
            }
            ICollection<ElementId> areas = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement)).OfCategory(BuiltInCategory.OST_Areas).ToElementIds();
            ICollection<ElementId> area_planViews = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).ToElementIds();
            ICollection<ElementId> area_schemes = new FilteredElementCollector(doc).OfClass(typeof(AreaScheme)).OfCategory(BuiltInCategory.OST_AreaSchemes).ToElementIds();
            AreaScheme rentable = doc.GetElement(area_schemes.First()) as AreaScheme;
            foreach (ElementId area_id in areas) {
                Area area_cur = doc.GetElement(area_id) as Area;
                Level area_level = area_cur.Level;
                double right, left, front, back, top, bot;
                BoundingBoxXYZ section_bound = new BoundingBoxXYZ();
                foreach (ElementId viewPlan_id in area_planViews) {
                    ViewPlan view_plan = doc.GetElement(viewPlan_id) as ViewPlan;
                    Level plan_level = doc.GetElement(view_plan.LevelId) as Level;
                    AreaScheme plan_scheme = view_plan.AreaScheme;
                    if (view_plan.ViewName == area_level.Name && view_plan.ViewType.ToString() == "AreaPlan") {
                        BoundingBoxXYZ area_bound = area_cur.get_BoundingBox(view_plan);
                        front = area_bound.Max.Y;
                        right = area_bound.Max.X;
                        back = area_bound.Min.Y;
                        left = area_bound.Min.X;
                        bot = area_level.Elevation;
                        top = 0;
                        double level_height;
                        foreach (ElementId level_id in levels_id) {
                            Level this_level = doc.GetElement(level_id) as Level;
                            if (this_level.Name == area_level.Name) {
                                int cur_idx = levels_id.IndexOf(level_id);
                                if (cur_idx+1 < levels_id.Count) {
                                    level_height = elevations.ElementAt(cur_idx + 1) - elevations.ElementAt(cur_idx);
                                } else {
                                    level_height = 10;
                                }
                                top = bot + level_height;
                                break;
                            }
                        }
                        section_bound.Min = new XYZ(left, back, bot-1);
                        section_bound.Max = new XYZ(right, front, top-1);
                        View3D view3d = doc.ActiveView as View3D;

                        using (Transaction t = new Transaction(doc, "Set View")) {
                            t.Start();
                            view3d.SetSectionBox(section_bound);
                            t.Commit();
                        }
                        NavisworksExportOptions opt = new NavisworksExportOptions();
                        opt.DivideFileIntoLevels = true;
                        opt.ExportElementIds = true;
                        opt.ExportParts = true;
                        opt.ExportRoomAsAttribute = false;
                        opt.ExportRoomGeometry = false;
                        opt.ExportScope = NavisworksExportScope.View;
                        opt.ViewId = view3d.Id;
                        string folder_name = "D:\\Documents\\Revit Model\\";
                        TaskDialog.Show("folder: ", folder_name);
                        string file_name = area_level.Name + "_" + area_cur.Name;
                        doc.Export(folder_name, file_name, opt);
                        TaskDialog.Show("folder: ", "reach");
                    }
                }
            }
            return Result.Succeeded;
        }
        
    }
}

