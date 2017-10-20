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



            //start to divide wall at levels
            // start select all walls 
            IList<ElementId> walls_id = new List<ElementId>();
            FilteredElementCollector wall_collector = new FilteredElementCollector(doc).OfClass(typeof(Wall));
            foreach (Element w in wall_collector)
            {
                if (w is Wall)
                {
                    walls_id.Add(w.Id);
                }
            }
            // end select all walls      

            // start select all columns 
            //???
            // end select all columns 

            // start call create parts on wall list
            using (Transaction t = new Transaction(doc, "Create Part"))
            {
                t.Start();
                // Create parts from the selected element
                // There is "CreateParts" but no "CreatePart", so needed to use a list containing the one element
                PartUtils.CreateParts(doc, walls_id);
                t.Commit();
            }
            // end call create parts on wall list
            // start divide parts for each wall in wall list
            //ICollection<ElementId> selE = uidoc.Selection.GetElementIds();
            //foreach (ElementId w_id in walls_id)
            //{
            //    selE.Add(w_id);
            //}



            foreach (ElementId w_id in walls_id)
            {
                ICollection<ElementId> partsList = PartUtils.GetAssociatedParts(doc, w_id, true, true);

                // Get all levels
                ICollection<ElementId> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).OfCategory(BuiltInCategory.OST_Levels).ToElementIds();

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

                // Set the view's "Parts Visibility" parameter so that parts are shown
                Parameter p = doc.ActiveView.get_Parameter(BuiltInParameter.VIEW_PARTS_VISIBILITY);
                using (Transaction t = new Transaction(doc, "Set View Parameter"))
                {
                    t.Start();
                    p.Set(0); // 0 = Show Parts, 1 = Show Original, 2 = Show Both
                    t.Commit();
                }
            }
            // start divide parts for each wall in wall list
            //end of divide wall at levels





            ////start to select all slabs
            IList<ElementId> slabs_id = new List<ElementId>();
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Floor));
            foreach (Element e in collector)
            {
                if (e is Floor)
                {
                    slabs_id.Add(e.Id);
                }
            }
            using (Transaction t = new Transaction(doc, "Create Part"))
            {
                t.Start();
                PartUtils.CreateParts(doc, slabs_id);
                t.Commit();
            }
            // end call create parts on list

            //uidoc.ShowElements(slabs_id);
            // start divide parts for each wall in list
            foreach (ElementId s_id in slabs_id)
            {
                //Selection sel = uidoc.Selection;
                //ISelectionFilter f = new JtElementsOfClassSelectionFilter<Grid>();
                //Reference elemRef = sel.PickObject(ObjectType.Element, f, "Pick a grid");
                //Grid grid = doc.GetElement(elemRef) as Grid;
                //ICollection<ElementId> grid_list = new List<ElementId>();
                //grid_list.Add(grid.Id);
                //IList<Curve> gridCurves = grid.GetCurvesInView(DatumExtentType.Model, view);

                ICollection<ElementId> partsList = PartUtils.GetAssociatedParts(doc, s_id, true, true);

                // Get all levels
                ICollection<ElementId> grids = new FilteredElementCollector(doc).OfClass(typeof(Grid)).OfCategory(BuiltInCategory.OST_Grids).ToElementIds();


                // Create a list of curves which needs to be used in DivideParts but for this example
                // the divide is being done by levels so the curve list will be empty
                IList<Curve> curve_list = new List<Curve>();

                HostObject hostObj = doc.GetElement(s_id) as HostObject;
                Reference r = HostObjectUtils.GetTopFaces(hostObj).First();
                using (Transaction t = new Transaction(doc, "Divide Part at Grids"))
                {
                    t.Start();
                    //Transaction sketchPlaneTransaction = new Transaction(doc, "Create Sketch Plane");
                    SketchPlane grid_sketchPlane = SketchPlane.Create(doc, r);
                    //SketchPlane grid_sketchPlane = null;
                    //sketchPlaneTransaction.Commit();
                    PartUtils.DivideParts(doc, partsList, grids, curve_list, grid_sketchPlane.Id);
                    t.Commit();
                }

                // Set the view's "Parts Visibility" parameter so that parts are shown
                Parameter p = doc.ActiveView.get_Parameter(BuiltInParameter.VIEW_PARTS_VISIBILITY);
                using (Transaction t = new Transaction(doc, "Set View Parameter"))
                {
                    t.Start();
                    p.Set(0); // 0 = Show Parts, 1 = Show Original, 2 = Show Both
                    t.Commit();
                }
            }





            //ICollection<ElementId> elementIdsToDivide = new List<ElementId>();
            //if (PartUtils.AreElementsValidForCreateParts(doc, slabs_id))
            //{
            //    // AreElementsValidForCreateParts returned true, so the selected element is not a part but it is an element that can be used to create a part. 
            //    Transaction createPartTransaction = new Transaction(doc, "Create Part");
            //    createPartTransaction.Start();
            //    PartUtils.CreateParts(doc, slabs_id); // create the parts
            //    createPartTransaction.Commit();
            //    foreach (ElementId e_id in slabs_id)
            //    {
            //        elementIdsToDivide = PartUtils.GetAssociatedParts(doc, e_id, true, true);
            //    }// get the id of the newly created part
            //}
            ////else if (pickedElement is Part)
            ////{
            ////    // The selected element is a part, so that part will be divided. 
            ////    elementIdsToDivide.Add(pickedElement.Id);
            ////}
            //// Create geometry that will be used to divide the part. For this example, a new part will be divided from the main part that is one quarter of the face. More complex intelligence could be coded to divide the part based on construction logistics or the properties of the materials being used to create the part.
            //XYZ pointRight = null;
            //XYZ pointTop = null;
            //XYZ pointCorner = null;
            //XYZ pointCenter = null;

            //SketchPlane sketchPlane = null;
            //Plane plane = null;

            //Options opt = new Options();
            //opt.ComputeReferences = true;
            //foreach (Element e in slabs)
            //{
            //    GeometryElement geomElem = e.get_Geometry(opt);
            //    foreach (GeometryObject geomObject in geomElem)
            //    {
            //        if (geomObject is Solid) // get the solid geometry of the selected element
            //        {
            //            Solid solid = geomObject as Solid;
            //            FaceArray faceArray = solid.Faces;
            //            foreach (Face face in faceArray)
            //            {
            //                // find the center of the face
            //                BoundingBoxUV bbox = face.GetBoundingBox();
            //                UV center = new UV((bbox.Max.U - bbox.Min.U) / 2 + bbox.Min.U, (bbox.Max.V - bbox.Min.V) / 2 + bbox.Min.V);
            //                XYZ faceNormal = face.ComputeNormal(center);
            //                if (faceNormal.IsAlmostEqualTo(XYZ.BasisZ)) // this example is designed to work with a floor or other element with a large face whose normal is in the Z direction
            //                {
            //                    Transaction sketchPlaneTransaction = new Transaction(doc, "Create Sketch Plane");
            //                    sketchPlaneTransaction.Start();
            //                    plane = Plane.CreateByNormalAndOrigin(faceNormal, XYZ.Zero);
            //                    sketchPlane = SketchPlane.Create(doc, plane);
            //                    //sketchPlane = doc.SketchPlane.Create(face as PlanarFace);
            //                    sketchPlaneTransaction.Commit();

            //                    pointCenter = face.Evaluate(center);

            //                    UV top = new UV((bbox.Max.U - bbox.Min.U) / 2 + bbox.Min.U, bbox.Max.V);
            //                    pointTop = face.Evaluate(top);

            //                    UV right = new UV(bbox.Max.U, (bbox.Max.V - bbox.Min.V) / 2 + bbox.Min.V);
            //                    pointRight = face.Evaluate(right);

            //                    UV corner = new UV(bbox.Max.U, bbox.Max.V);
            //                    pointCorner = face.Evaluate(corner);

            //                    break;
            //                }
            //            }
            //        }
            //    }
            //}

            ////Selection sel = uidoc.Selection;
            ////Reference elemRef = sel.PickObject(
            ////ObjectType.Element, f, "Pick a grid");
            ////Grid grid = doc.GetElement(elemRef) as Grid;

            //// Create the curves that will be used for the part division.
            //IList<Curve> curveList = new List<Curve>();
            ////Curve curve1 = app.Create.NewLine(pointCenter, pointRight, true);
            //Curve curve1 = Line.CreateBound(pointCenter, pointRight);
            //curveList.Add(curve1);
            ////Curve curve2 = app.Create.NewLine(pointRight, pointCorner, true);
            //Curve curve2 = Line.CreateBound(pointRight, pointCorner);
            //curveList.Add(curve2);
            ////Curve curve3 = app.Create.NewLine(pointCorner, pointTop, true);
            //Curve curve3 = Line.CreateBound(pointCorner, pointTop);
            //curveList.Add(curve3);
            ////Curve curve4 = app.Create.NewLine(pointTop, pointCenter, true);
            //Curve curve4 = Line.CreateBound(pointTop, pointCenter);
            //curveList.Add(curve4);

            //// intersectingReferenceIds will be empty for this example.
            //ICollection<ElementId> intersectingReferenceIds = new List<ElementId>();

            //// Divide the part
            //Transaction dividePartTransaction = new Transaction(doc, "Divide Part");
            //dividePartTransaction.Start();
            //PartMaker maker = PartUtils.DivideParts(doc, elementIdsToDivide, intersectingReferenceIds, curveList, sketchPlane.Id);
            //dividePartTransaction.Commit();
            ////ICollection<ElementId> divElems = maker.GetSourceElementIds(); // Get the ids of the divided elements

            //// Set the view's "Parts Visibility" parameter so that parts are shown
            //Parameter partVisInView = doc.ActiveView.get_Parameter(BuiltInParameter.VIEW_PARTS_VISIBILITY);
            //Transaction setPartVizTransaction = new Transaction(doc, "Set View Parameter");
            //setPartVizTransaction.Start();
            //partVisInView.Set(0); // 0 = Show Parts, 1 = Show Original, 2 = Show Both
            //setPartVizTransaction.Commit();
            ////// Access current selection






            return Result.Succeeded;
        }
    }
}
