using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.GraphicsInterface;

namespace Spring_Generator
{
    public class Class1
    {
        [CommandMethod("AddSpring")]
        public void drawSpring()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            //??ask about gauge of spring back vs seat
            //seat 8 gauge .1285"
            //back 12 gauge .0808"

            #region get ends of spring to get total distance
            //ask user for points for spring to exist
            PromptPointOptions ppo = new PromptPointOptions("First end of spring:");
            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK)
                return;
            Point3d startPoint = ppr.Value;

            ppo.Message = "Second end of spring";
            ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK)
                return;

            //calculate distance from chosen points
            #endregion
            double totalLength = startPoint.DistanceTo(ppr.Value);

            #region get spring Type (length)
            //given total length, calculate ideal spring length which is total minus some percentage
            double theorySpgLength = (Math.Round(.9 * totalLength *2))/2;
            //find the nearest spring length that is less than or equal to total length(springs tend to come in 1/2" increments)
            //compare to list of spring lengths in stock **(eventually get springs from SQL)

            //present any springs withen an accaptable margin of diviation (maybe 8%)??
            //if none are easily exceptable but might work within a special circumstance, present that with warning (10%)??
            //if none are accaptable at all, then give different warning and end command (user will need to move spring rail or order diff springs)

            //**idealy replace with a user dialoge to choose from springs in system
            //ask user for spring length desired(may prompt with options from list) based on orignal distance
            PromptDoubleOptions pdo = new PromptDoubleOptions("Enter spring length:");
            //needs to pull these dynamically***************
            pdo.Keywords.Add(theorySpgLength.ToString());
            pdo.Keywords.Add((theorySpgLength - .5).ToString());
            PromptDoubleResult pdr = ed.GetDouble(pdo);
            if (pdr.Status != PromptStatus.OK)
                return;
            #endregion
            double springLength = pdr.Value;

            #region Create top view of spring
            //1 Calculate rungs
            //spring length / average gap
            int rungCount = Convert.ToInt32(springLength / .875);//guessing at the avg rung gap for starters
            double rungGap = totalLength / rungCount;
            double rungWidth = 2 - rungGap;
            //springs tend to be approx 2" wide
            //rung widths are 2" - rung gap (the two radii of bends)

            //add all parts to object collection then convert to polyline
            DBObjectCollection springParts = new DBObjectCollection();
            //construct first rung (has hooked end)            
            springParts = createEnd(springParts, startPoint, rungGap, rungWidth, true, true);

            //construct rungs/bends in middle            
            //and bends on alternating runs
            for (int i = 0; i < rungCount; i++)
            {
                Line rung = new Line(
                    new Point3d(startPoint.X - rungWidth / 2, startPoint.Y + i * rungGap, startPoint.Z),
                    new Point3d(startPoint.X + rungWidth / 2, startPoint.Y + i * rungGap, startPoint.Z));
                //add rungs except for either end
                if(i != 0 && i != rungCount)
                { springParts.Add(rung); }

                //add bends to either side depending on if it is an even or odd rung
                if(i % 2 == 0)
                {
                    //even
                    Arc leftBend = new Arc(new Point3d(startPoint.X -1 + rungGap/2,startPoint.Y + rungGap * i + rungGap/2, startPoint.Z), rungGap/2, Math.PI/2, 3* Math.PI/2);
                    springParts.Add(leftBend);
                }
                else
                {
                    //odd
                    Arc rightBend = new Arc(new Point3d(startPoint.X + 1 - rungGap / 2, startPoint.Y + rungGap * i + rungGap / 2, startPoint.Z), rungGap / 2, 3 * Math.PI / 2,  Math.PI / 2);
                    springParts.Add(rightBend);
                }
            }

            //construct end
            //if rungCount is even it opens same as first
            bool secondOpen = true;
            if (rungCount % 2 == 0)
            { secondOpen = false; }                                       
            springParts = createEnd(springParts, new Point3d(startPoint.X, startPoint.Y + totalLength, startPoint.Z), rungGap, rungWidth, secondOpen, false);

            ////just for testing ** 
            //using (Transaction trans = db.TransactionManager.StartTransaction())
            //{
            //    BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
            //    BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
            //    foreach (DBObject dbo in springParts)
            //    {
            //        Entity ent = dbo as Entity;
            //        btr.AppendEntity(ent);
            //        trans.AddNewlyCreatedDBObject(ent, true);
            //    }

            //    trans.Commit();
            //}

            Polyline pLine = BuildPLine.drawPolyLine(springParts);

                //move spring polyline to the correct Z coordinate, converting to a polyline cuts off z coords leaving it at 0****
                //***************************
            #endregion

            #region create side view (crown)
            //need to account for creating arc along different plane
            //arc is start point, and then total length vertical from first point (same plane) *we'll rotate later
            //for now all springs will use same crown, need a formula later and need an element that accounts for flat vs curly springs
            //create arc flat along same plane as spring polyline, then rotate it??
            Arc crown = new Arc();
            Point3d startArc = new Point3d(startPoint.X - 2, startPoint.Y, startPoint.Z);
            Point3d endArc = new Point3d(startArc.X, startArc.Y + totalLength, startArc.Z);
            Point3d arcMid = new Point3d(startArc.X - 1.5, startArc.Y + (totalLength / 2), startArc.Z);
                
            //assuming crown is 1.5 until we derive a diff system
            //radius = height/2 + width^2/height(8)
            crown.Radius = .75 + (Math.Pow(startArc.DistanceTo(endArc),2) / 12);
                
            //given that we always will have our arc aligned vertically center is easter to calculate
            crown.Center = new Point3d(arcMid.X + crown.Radius, arcMid.Y, arcMid.Z);

            Matrix3d ocs2wcs = Matrix3d.PlaneToWorld(crown.Normal);
            Plane plane = new Plane(ocs2wcs.CoordinateSystem3d.Origin, ocs2wcs.CoordinateSystem3d.Xaxis, ocs2wcs.CoordinateSystem3d.Yaxis);

            //need start and end angles                
            //double startAngle = tanAngle(arcCenter, startArc);
            //double endAngle = tanAngle(arcCenter, endArc);         
            crown.EndAngle = (startArc - crown.Center).AngleOnPlane(plane);
            crown.StartAngle = (endArc - crown.Center).AngleOnPlane(plane);

            //Arc crown = new Arc(arcCenter,radius,startAngle,endAngle);

            // Rotate the 3D solid 30 degrees around the axis that is defined by the points
            Vector3d turnArc = crown.StartPoint.GetVectorTo(crown.EndPoint);
            crown.TransformBy(Matrix3d.Rotation(3*(Math.PI / 2), turnArc, crown.StartPoint));
            
        #endregion

            //convert collection to polyline
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                //Have to have both views inserted to turn into extruded surface
                btr.AppendEntity(pLine);
                tr.AddNewlyCreatedDBObject(pLine, true);
                btr.AppendEntity(crown);
                tr.AddNewlyCreatedDBObject(crown, true);

                #region Extrude surfaces from open curves, polylines
                //extrude two shapes
                Profile3d profileCrown = new Profile3d(crown);
                Profile3d profileSpring = new Profile3d(pLine);
                Vector3d polylineDir = new Vector3d(0, 0, 4);
                Vector3d crownDir = (crown.StartPoint).GetVectorTo(new Point3d(crown.StartPoint.X + 4, crown.StartPoint.Y, crown.StartPoint.Z));
                SweepOptions sweepOp = new SweepOptions();

                //need a different vector for crown
                ObjectId surfaceId = Autodesk.AutoCAD.DatabaseServices.Surface.CreateExtrudedSurface(
                    profileCrown, crownDir, sweepOp, true);
                ObjectId surfaceSpringId = Autodesk.AutoCAD.DatabaseServices.Surface.CreateExtrudedSurface(
                    profileSpring, polylineDir, sweepOp, true);

                //remove original lines
                pLine.Erase(true);
                crown.Erase(true);
                #endregion

                //intersect both regions(observe how extrusions work with ucs)
                Autodesk.AutoCAD.DatabaseServices.Surface crownEnt = tr.GetObject(surfaceId, OpenMode.ForWrite) as Autodesk.AutoCAD.DatabaseServices.Surface;
                Autodesk.AutoCAD.DatabaseServices.Surface springEnt = tr.GetObject(surfaceSpringId, OpenMode.ForWrite) as Autodesk.AutoCAD.DatabaseServices.Surface;
                //convert both surfaces to nurbs
                //the polyline extrusion creates many surfaces in nurb form, loop through intersections creating splines and lines
                springParts = Intersect_Surfaces.IntersectSurf(crownEnt, springEnt);
                //delete surfaces
                crownEnt.Erase(true);
                springEnt.Erase(true);

                //join intersection pieces as spline (maynot be possible)
                //convert collection of splines/lines to single 3Dspline
                Spline springSpline = Intersect_Surfaces.mergeSpline(springParts);
                btr.AppendEntity(springSpline);
                tr.AddNewlyCreatedDBObject(springSpline, true);

                //loft along spline
                try
                {
                    //create circle to to sweep
                    Circle wireGauge = new Circle();
                    wireGauge.Center = springSpline.StartPoint;
                    wireGauge.Radius = .06425;//diameter .1285
                    //Entity sweepEnt = tr.GetObject();

                    Curve pathEnt = tr.GetObject(springSpline.Id, OpenMode.ForRead) as Curve;
                    //Curve pathEnt = tr.GetObject(pLine.Id, OpenMode.ForRead) as Curve;
                    if (wireGauge == null || pathEnt == null)
                    {
                        ed.WriteMessage("\nProblem getting spline made");
                        return;
                    }

                    //builder object to create sweepoptions
                    SweepOptionsBuilder sob = new SweepOptionsBuilder();

                    //align the entity to sweep to the path
                    sob.Align = SweepOptionsAlignOption.AlignSweepEntityToPath;

                    //the base point is the start of the path
                    sob.BasePoint = pathEnt.StartPoint;

                    //the profile will rotate to follow the path
                    sob.Bank = true;

                    Entity ent;
                    Solid3d sol = new Solid3d();
                    sol.CreateSweptSolid(wireGauge, pathEnt, sob.ToSweepOptions());
                    ent = sol;

                    //and add it to the modelspace
                    btr.AppendEntity(ent);
                    tr.AddNewlyCreatedDBObject(ent, true);
                }
                catch { }

                //re-align spring to first points chosen by user
                tr.Commit();
            }
        }

        //creates spring end
        private DBObjectCollection createEnd(DBObjectCollection springParts,Point3d startPoint, double rungGap, double rungWidth, bool openRight, bool start)
        {
            //hook is always same shape, the rung length will vary with the radius of bend
            //orientation differs based on if it opens right or left, is a start or end
            double xAlign = 1;
            double yAlign = 1;
            if (!openRight)
                xAlign = -1;
            if (!start)
                yAlign = -1;

            //start at end of hook
            Point3d startLinestart = new Point3d(startPoint.X + 1 * xAlign, startPoint.Y + .1875 * yAlign, startPoint.Z);
            Point3d startlineEnd = new Point3d(startLinestart.X - .3125 * xAlign, startLinestart.Y, startPoint.Z);
            Line startLine = new Line(startLinestart, startlineEnd);

            //angled line of  hook
            Line descendLine = new Line(startlineEnd,
                new Point3d(startlineEnd.X - .3125 * xAlign, startlineEnd.Y - .1875 * yAlign, startPoint.Z));

            //hook is  a constant length
            //end rung length is 2" - half(rung gap) - constant hook
            double endLength = 2 - rungGap / 2 - .625;
            Point3d startEndRung = new Point3d(startPoint.X + .375 * (xAlign), startPoint.Y, startPoint.Z);
            Point3d endEndRung = new Point3d(startEndRung.X - endLength * xAlign,startPoint.Y,startPoint.Z);
            Line endRungWidth = new Line(startEndRung, endEndRung);

            Polyline innerFillet = Fillet.fillet(descendLine, endRungWidth, .125);

            //need to add radius and to change the lengths of the other lines to account for this
            Polyline outerFillet = Fillet.fillet(startLine, descendLine, .125);
            //find endpoint of "arc" on correct line and move x
            if (outerFillet.StartPoint.Y == startLine.EndPoint.Y)
            {
                //change end of startline to line up with outer fillet
                startLine.EndPoint = new Point3d(outerFillet.StartPoint.X, startLine.EndPoint.Y, startLine.EndPoint.Z);
                //change start of descend line to other end of fillet
                descendLine.StartPoint = new Point3d(outerFillet.EndPoint.X, outerFillet.EndPoint.Y, descendLine.StartPoint.Z);
            }
            else if (outerFillet.EndPoint.Y == startLine.EndPoint.Y)
            {
                startLine.EndPoint = new Point3d(outerFillet.EndPoint.X, startLine.EndPoint.Y, startLine.EndPoint.Z);
                descendLine.StartPoint = new Point3d(outerFillet.StartPoint.X, outerFillet.StartPoint.Y, descendLine.StartPoint.Z);
            }

            //apply fillet to inner vertex
            if(innerFillet.StartPoint.Y == endRungWidth.EndPoint.Y)
            {
                //change start of final rung
                endRungWidth.StartPoint = new Point3d(innerFillet.StartPoint.X, endRungWidth.StartPoint.Y, endRungWidth.StartPoint.Z);
                //change end of descend line to other end of fillet
                descendLine.EndPoint = new Point3d(innerFillet.EndPoint.X, innerFillet.EndPoint.Y, descendLine.EndPoint.Z);
            }
            else if (innerFillet.EndPoint.Y == endRungWidth.EndPoint.Y)
            {
                //chage start of final run
                endRungWidth.StartPoint = new Point3d(innerFillet.EndPoint.X, endRungWidth.StartPoint.Y, endRungWidth.StartPoint.Z);
                //change end of descend line to other end of fillet
                descendLine.EndPoint = new Point3d(innerFillet.StartPoint.X, innerFillet.StartPoint.Y, descendLine.EndPoint.Z);
            }

            //add crap to collection
            springParts.Add(startLine);
            springParts.Add(outerFillet);
            springParts.Add(descendLine);
            springParts.Add(innerFillet);
            springParts.Add(endRungWidth);

            return springParts;     
        }

        //aqcuired this information online, not sure how tall this surface is supposed to be or how to insert it into drawing
        //creates an extruded surface from an open polyline
        private ExtrudedSurface extrudeOpenPolyline(Polyline pPoly, Editor ed)
        {
            ExtrudedSurface extrSurf = new ExtrudedSurface();
            Face face = new Face(pPoly.StartPoint, pPoly.EndPoint, pPoly.EndPoint + new Vector3d(0, 0, 1000), pPoly.StartPoint + new Vector3d(0, 0, 1000), true, true, true, true);

            Autodesk.AutoCAD.DatabaseServices.Surface sweepEnt = new Autodesk.AutoCAD.DatabaseServices.Surface();
            sweepEnt.SetDatabaseDefaults();

            try
            {sweepEnt= Autodesk.AutoCAD.DatabaseServices.Surface.CreateFrom(face);}
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {}

            SweepOptions sweepOpts = new SweepOptions();

            try
            { extrSurf.CreateExtrudedSurface(sweepEnt, (pPoly.EndPoint - pPoly.StartPoint).GetPerpendicularVector(), sweepOpts);}
            catch
            { ed.WriteMessage("\nFailed with CreateExtrudedSurface.");}

           return extrSurf;
        }

        //get Tangent angle doesnt garauntee with complimentary angle it needs
        private double tanAngle(Point3d center, Point3d point)
        {
            //angle of tanget line at end point
            //should be perpendicular to line of center to point
            //slope of point to center => )(Yc-Yo)/(Xc-Xo
            //perpendicular is the reciprical
            //angle is => tan^-1(perp slope)
            //need to give angle in radians not degrees

            //double angle = Math.Atan((center.X - point.X) / (center.Y - point.Y));
            double angle = Math.Atan((center.X - point.X)/(center.Y - point.Y));
            return angle;
        }
    }
}
