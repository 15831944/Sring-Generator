using System;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;

using Autodesk.AutoCAD.BoundaryRepresentation;
//[assembly: CommandClass(typeof(Spring_Generator.STSCCommands))]

    //code works but only if surfaces are NURBS
namespace Spring_Generator
{
    public class STSCCommands
    {
        public STSCCommands()
        {
        }

        [CommandMethod("SurfInt")]
        public void SurfInt()
        {
            Database db = HostApplicationServices.WorkingDatabase;
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ObjectId ObjID;

            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                try
                {
                    Autodesk.AutoCAD.DatabaseServices.Surface surf1;
                    Autodesk.AutoCAD.DatabaseServices.Surface surf2;

                    SurfGeomConstruct sgc1;
                    SurfGeomConstruct sgc2;

                    PromptEntityOptions peo = new PromptEntityOptions("Select first surface for intersection: ");
                    peo.SetRejectMessage("\nPlease select only a Surface");
                    peo.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Surface), false);
                    PromptEntityResult per = ed.GetEntity(peo);
                    if (per.Status != PromptStatus.OK) return;
                    ObjID = per.ObjectId;
                    surf1 = (Autodesk.AutoCAD.DatabaseServices.Surface)trans.GetObject(ObjID, OpenMode.ForRead, false);
                    surf1.Highlight();

                    peo.Message = "Select intersecting surface: ";
                    per = ed.GetEntity(peo);
                    if (per.Status != PromptStatus.OK) return;
                    surf1.Unhighlight();

                    sgc1 = new SurfGeomConstruct(surf1);
                    ObjID = per.ObjectId;
                    surf2 = (Autodesk.AutoCAD.DatabaseServices.Surface)trans.GetObject(ObjID, OpenMode.ForRead, false);
                    sgc2 = new SurfGeomConstruct(surf2);

                    SurfaceSurfaceIntersector ssi = new SurfaceSurfaceIntersector();
                    //ssi.Set(sgc1.GeomSurf, sgc2.GeomSurf);
                    //int count = ssi.NumResults;
                    //if (count < 1) return;

                    BlockTableRecord btr = (BlockTableRecord)(trans.GetObject(db.CurrentSpaceId, OpenMode.ForWrite));

                    //for (int i = 0; i < count; i++)
                    //{
                    //    Curve3d c3d = ssi.IntersectCurve(i, true);
                    //    Curve crv = GenCurve(c3d);
                    //    crv.SetDatabaseDefaults();
                    //    btr.AppendEntity(crv);
                    //    trans.AddNewlyCreatedDBObject(crv, true);
                    //}

                    Brep surfBrep1 = new Brep(surf1);
                    Brep surfBrep2 = new Brep(surf2);
                    foreach(Autodesk.AutoCAD.BoundaryRepresentation.Face fc1 in surfBrep1.Faces)
                    {
                        ExternalBoundedSurface[] ebSurfs1 = fc1.GetSurfaceAsTrimmedNurbs();
                        Autodesk.AutoCAD.Geometry.Surface sur1 = ebSurfs1[0];
                        foreach (Autodesk.AutoCAD.BoundaryRepresentation.Face fc2 in surfBrep2.Faces)
                        {
                            ExternalBoundedSurface[] ebSurfs2 = fc2.GetSurfaceAsTrimmedNurbs();
                            Autodesk.AutoCAD.Geometry.Surface sur2 = ebSurfs2[0];

                            ssi.Set(sur1, sur2);
                            if(ssi.NumResults < 1)
                            { break; }

                            for(int i = 0; i<ssi.NumResults; i++)
                            {
                                Curve3d c3d = ssi.IntersectCurve(i, true);
                                Curve crv = GenCurve(c3d);
                                crv.SetDatabaseDefaults();
                                btr.AppendEntity(crv);
                                trans.AddNewlyCreatedDBObject(crv, true);
                            }
                        }
                    }

                    if (ssi != null) ssi.Dispose();
                    if (sgc1.GeomSurf != null) sgc1.GeomSurf.Dispose();
                    if (sgc2.GeomSurf != null) sgc2.GeomSurf.Dispose();
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage("Error: " + ex.Message);
                }
                finally
                {
                    trans.Commit();
                }
            }
        }

        //Internal
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
    }

    class SurfGeomConstruct
    {
        private Autodesk.AutoCAD.Geometry.Surface m_geomSurf;
        //Constructors
        public SurfGeomConstruct(Autodesk.AutoCAD.DatabaseServices.Surface Surf)
        {
            GeomSurfGenerator(Surf);
            
        }

        //Properties
        public Autodesk.AutoCAD.Geometry.Surface GeomSurf
        {
            get { return m_geomSurf; }
        }

        //Methods
        private void GeomSurfGenerator(Autodesk.AutoCAD.DatabaseServices.Surface Surf)
        {
            Brep Br = new Brep(Surf);
            foreach (Autodesk.AutoCAD.BoundaryRepresentation.Face fc in Br.Faces)
            {
                ExternalBoundedSurface[] ebSurfs = fc.GetSurfaceAsTrimmedNurbs();
                m_geomSurf = ebSurfs[0];
            }
            Br.Dispose();
        }
    }
}
