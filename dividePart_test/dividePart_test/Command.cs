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
using Newtonsoft.Json;
using jsonDeserializer;
#endregion

namespace dividePart_test
{
    [Transaction(TransactionMode.Manual)]
    // copied class from the Building Coder

    public class Command : IExternalCommand
    {

        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            //TaskDialog.Show("Revit", "Hello World");

        // reading JSON file:
            string path = @"D:\Documents\Revit Model\api_input_test.json";
            var res = JsonConvert.DeserializeObject<zoneAllocation>(File.ReadAllText(path));

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
                if (c is FamilyInstance)
                {
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

        // filter out all levels and grids
            ICollection<ElementId> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).OfCategory(BuiltInCategory.OST_Levels).ToElementIds();
            ICollection<ElementId> grids = new FilteredElementCollector(doc).OfClass(typeof(Grid)).OfCategory(BuiltInCategory.OST_Grids).ToElementIds();


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
            foreach (ElementId w_id in walls_id)
            {
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

                using (Transaction t = new Transaction(doc, "Divide Part at Levels"))
                {
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




            // divide slabs
            foreach (ElementId s_id in slabs_id) {

                ICollection<ElementId> partsList = PartUtils.GetAssociatedParts(doc, s_id, true, true);
                // find z of slab's top face
                BoundingBoxXYZ bbox = doc.GetElement(s_id).get_BoundingBox(view);
                double slab_top_face_z = bbox.Max.Z;
                // all grids elementid are now in grids
                IList<Curve> curve_trim = new List<Curve>();
                // loop over all zones
                foreach (var one_of_zone in res.Zones)
                {
                    // find four bounding curve for one zone, not sure if itersection is counted to trim curves
                    IList<Curve> cur_bound_box = new List<Curve>();

                    // not yet used, need for later "mark" feature
                    string cur_zone_name = one_of_zone.Key;

                    // loop over all grids
                    foreach (ElementId grid_id in grids)
                    {
                        
                        Grid cur = doc.GetElement(grid_id) as Grid;
                        string cur_name = cur.Name;
                        if (cur_name == one_of_zone.Value.top){
                            cur_bound_box.Add(cur.Curve);
                        }
                        if (cur_name == one_of_zone.Value.bottom) {
                            cur_bound_box.Add(cur.Curve);
                        }
                        if (cur_name == one_of_zone.Value.left) {
                            cur_bound_box.Add(cur.Curve);
                        }
                        if (cur_name == one_of_zone.Value.right) {
                            cur_bound_box.Add(cur.Curve);
                        }
                    
                    }
                    // now four bounding curve objects are added to cur_bound_box list
                    // try to find XYZ for them:
                    Curve top_ = cur_bound_box[0];
                    Curve bottom_ = cur_bound_box[1];
                    Curve left_ = cur_bound_box[2];
                    Curve right_ = cur_bound_box[3];

                    double topleft_x = left_.GetEndPoint(0).X;
                    double topleft_y = top_.GetEndPoint(0).Y;
                    
                    XYZ topleft = new XYZ(topleft_x, topleft_y, slab_top_face_z);

                    double topright_x = right_.GetEndPoint(0).X;
                    double topright_y = top_.GetEndPoint(0).Y;
                    XYZ topright = new XYZ(topright_x, topright_y, slab_top_face_z);

                    double botleft_x = left_.GetEndPoint(0).X;
                    double botleft_y = bottom_.GetEndPoint(0).Y;
                    XYZ botleft = new XYZ(botleft_x, botleft_y, slab_top_face_z);

                    double botright_x = right_.GetEndPoint(0).X;
                    double botright_y = bottom_.GetEndPoint(0).Y;
                    XYZ botright = new XYZ(botright_x, botright_y, slab_top_face_z);

                    Curve top_trim = Line.CreateBound(topleft, topright);
                    curve_trim.Add(top_trim);
                    Curve bot_trim = Line.CreateBound(botleft, botright);
                    curve_trim.Add(bot_trim);
                    Curve left_trim = Line.CreateBound(topleft, botleft);
                    curve_trim.Add(left_trim);
                    Curve right_trim = Line.CreateBound(topright, botright);
                    curve_trim.Add(right_trim);
                }

                HostObject hostObj = doc.GetElement(s_id) as HostObject;
                Reference r = HostObjectUtils.GetTopFaces(hostObj).First();
                ICollection<ElementId> intersectingReferenceIds = new List<ElementId>();
                using (Transaction t = new Transaction(doc, "Divide Part at Grids")) {
                    t.Start();
                    //Transaction sketchPlaneTransaction = new Transaction(doc, "Create Sketch Plane");
                    SketchPlane grid_sketchPlane = SketchPlane.Create(doc, r);
                    //sketchPlaneTransaction.Commit();
                    PartUtils.DivideParts(doc, partsList, intersectingReferenceIds, curve_trim, grid_sketchPlane.Id);
                    t.Commit();
                }

            }
    // Set the view's "Parts Visibility" parameter so that parts are shown
            Parameter p = doc.ActiveView.get_Parameter(BuiltInParameter.VIEW_PARTS_VISIBILITY);
            using (Transaction t = new Transaction(doc, "Set View Parameter"))
            {
                t.Start();
                p.Set(0); // 0 = Show Parts, 1 = Show Original, 2 = Show Both
                t.Commit();
            }
            return Result.Succeeded;
        }
    }
}
