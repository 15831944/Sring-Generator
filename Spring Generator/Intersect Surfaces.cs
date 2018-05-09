using System;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.Colors;

namespace Spring_Generator
{
    class Intersect_Surfaces
    {
        public static DBObjectCollection IntersectSurf(Autodesk.AutoCAD.DatabaseServices.Surface surf1, Autodesk.AutoCAD.DatabaseServices.Surface surf2)
        {
            Brep surfBrep1 = new Brep(surf1);
            Brep surfBrep2 = new Brep(surf2);
            SurfaceSurfaceIntersector ssi = new SurfaceSurfaceIntersector();

            DBObjectCollection dboCol = new DBObjectCollection();
                foreach (Autodesk.AutoCAD.BoundaryRepresentation.Face fc1 in surfBrep1.Faces)
                {
                    ExternalBoundedSurface[] ebSurfs1 = fc1.GetSurfaceAsTrimmedNurbs();
                    Autodesk.AutoCAD.Geometry.Surface sur1 = ebSurfs1[0];
                    foreach (Autodesk.AutoCAD.BoundaryRepresentation.Face fc2 in surfBrep2.Faces)
                    {
                        ExternalBoundedSurface[] ebSurfs2 = fc2.GetSurfaceAsTrimmedNurbs();
                        Autodesk.AutoCAD.Geometry.Surface sur2 = ebSurfs2[0];

                        ssi.Set(sur1, sur2);
                        if (ssi.NumResults < 1)
                        { break; }

                        for (int i = 0; i < ssi.NumResults; i++)
                        {
                            Curve3d c3d = ssi.IntersectCurve(i, true);
                            Curve crv = GenCurve(c3d);
                            crv.SetDatabaseDefaults();
                            crv.Color = Color.FromRgb(255, 255, 255);
                            dboCol.Add(crv);

                            ////adding for now need instead to conver to single Spline                            
                            //BlockTableRecord btr = (BlockTableRecord)(trans.GetObject(db.CurrentSpaceId, OpenMode.ForWrite));
                            //btr.AppendEntity(crv);
                            //trans.AddNewlyCreatedDBObject(crv, true);
                        }
                    }
            }
            return dboCol;
        }

        static private Curve GenCurve(Curve3d crv3d)
        {

            Tolerance tol = new Tolerance();
            ExternalCurve3d extCrv = crv3d as ExternalCurve3d;
            Line3d ln3d;

            if (extCrv.IsLinear(out ln3d))
            {
                Line ln = new Line(crv3d.StartPoint, crv3d.EndPoint);
                return ln;
            }

            else
            {
                Double per;
                KnotCollection kc;
                NurbCurve3d nc3d = extCrv.NativeCurve as NurbCurve3d;
                kc = nc3d.Knots;
                Double[] dblKnots = new Double[kc.Count];
                kc.CopyTo(dblKnots, 0);
                DoubleCollection dc = new DoubleCollection(dblKnots);
                NurbCurve3dData nc3dData = nc3d.DefinitionData;

                return new Spline(nc3d.Degree, nc3d.IsRational, nc3d.IsClosed(), nc3d.IsPeriodic(out per),
                nc3dData.ControlPoints, dc, nc3dData.Weights, tol.EqualPoint, tol.EqualVector);
            }
        }

        static public Spline mergeSpline(DBObjectCollection dboCol)
        {
            //load spline with a segment
            Spline springSline = convertSpline(dboCol[0]);
            dboCol.Remove(dboCol[0]);

            do
            {
                //find the part that intersects
                DBObject seg = findIntersectingPart(dboCol, springSline);
                //convert to spline
                Spline addSeg = convertSpline(seg);
                //add part to spline
                springSline.JoinEntity(addSeg);
                //remove part from collection
                dboCol.Remove(seg);
            } while (dboCol.Count > 0);

            springSline.Color = Color.FromRgb(51, 255, 255);

            return springSline;
        }

        //convert parts to splines (works for line and arcs*Not arcs yet)
        static private Spline convertSpline(DBObject dbo)
        {
            if (dbo is Spline)
            { return dbo as Spline; }
            if (dbo is Arc)
            {
                Arc arcDat = dbo as Arc;
                Spline seg = new Spline();
                //whatever that is
                return seg;
            }
            else if (dbo is Line)
            {
                Line lDat = dbo as Line;
                Point3dCollection vertices = new Point3dCollection();
                vertices.Add(lDat.StartPoint);
                vertices.Add(lDat.EndPoint);
                Polyline3d tempPline = new Polyline3d();
                //polyine 3D has to be in btr before adding vertices
                Database db = HostApplicationServices.WorkingDatabase;
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)(trans.GetObject(db.CurrentSpaceId, OpenMode.ForWrite));
                    btr.AppendEntity(tempPline);
                    foreach(Point3d pnt in vertices)
                    {
                        using (PolylineVertex3d poly3dVert = new PolylineVertex3d(pnt))
                        {
                            tempPline.AppendVertex(poly3dVert);
                        }
                    }
                    trans.Commit();
                }

                Spline seg = tempPline.Spline;
                tempPline.Erase(true);
                return seg;
            }
            return null;
        }

        private static DBObject findIntersectingPart(DBObjectCollection dbObjCol, Spline springSpline)
        {
            Point3d firstPartStartPt = new Point3d();
            Point3d firstPartEndPt = new Point3d();
            Point3d secPartStartPt = new Point3d();
            Point3d secPartEndPt = new Point3d();
  
            Spline splineSeg = springSpline as Spline;
            firstPartStartPt = splineSeg.StartPoint;
            firstPartEndPt = splineSeg.EndPoint;         

            foreach (DBObject part in dbObjCol)
            {
                if(part != springSpline)
                {
                    if(part is Line)
                    {
                        Line secSeg = part as Line;
                        secPartStartPt = secSeg.StartPoint;
                        secPartEndPt = secSeg.EndPoint;
                    }
                    if(part is Spline)
                    {
                        Spline secSplSeg = part as Spline;
                        secPartStartPt = secSplSeg.StartPoint;
                        secPartEndPt = secSplSeg.EndPoint;
                    }

                    //compare endpoints return if something hits
                    if (firstPartStartPt == secPartStartPt ||
                        firstPartStartPt == secPartEndPt ||
                        firstPartEndPt == secPartStartPt ||
                        firstPartEndPt == secPartEndPt)
                    { return part; } 
                }
            }
            return null;    
        }


        //------------------ code from ADN------------------
//        static private void StitchOpening(ref Spline spline, Curve curveEntToAdd, bool buildforward, Database currentDb)
//        {
//            Point3d pntStart;
//            Point3d pntEnd;

//            pntStart = buildforward ? spline.EndPoint : spline.StartPoint;
//            pntEnd = buildforward ? curveEntToAdd.StartPoint : curveEntToAdd.EndPoint;

//            if (curveEntToAdd != null)
//            {
//                AddStitchMessage(pntStart, pntEnd);
//                //ObjectId idExtra = CreateStraightSpline(pntStart, pntEnd, currentDb);
//                ObjectId idExtra = CreateStitchLine(pntStart, pntEnd, currentDb);
//                using (Transaction trans = currentDb.TransactionManager.StartTransaction())
//                {
//                    Entity ent = trans.GetObject(idExtra, OpenMode.ForWrite) as Entity;

//                    //***** The next two lines have been replaced, in some case joining the two enties in two steps would fail ****//
//                    //spline.JoinEntity(ent); 
//                    //spline.JoinEntity(curveEntToAdd);
//                    //*****

//                    spline.JoinEntities(new Entity[] { ent, curveEntToAdd });
//                    ent.Erase();
//                    curveEntToAdd.Erase();
//                    trans.Commit();
//                }
//            }
//        }


//        static private ObjectId CreateStitchLine(Point3d pntStart, Point3d pntEnd, Database currentDb)
//        {
//            ObjectId lineId = ObjectId.Null;
//            using (Line line = new Line(pntStart, pntEnd))
//            {
//                line.SetDatabaseDefaults();
//                line.ColorIndex = 1;
//                lineId = acCommon.AddEntityToModelSpace(line, currentDb);
//            }

//            return lineId;
//        }
    }
}
