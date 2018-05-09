using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using System;
using GeometryExtensions;

namespace Spring_Generator
{
    class BuildPLine
    {

        //**Current issues, this code is contengent upon no errors in lines
        //doesn't handle overalapping lines, open lines.....
        //Code also assumes parts have been added to BTR, doesnt handle parts that arent inserted

        //using the parts of the object collection it will find shared vertices and
        //order them into a poly line
        //if a piece is an arc, it will change the vertex before it to have
        //appropriate bulge
        public static Polyline drawPolyLine2(DBObjectCollection dbObjCol, BlockTableRecord btr, Transaction tr)
        {
            //**********************
            //For testing make a version where if a colleciton isnt sent we can make a selection on screen

            //iterate through the collection adding parts based on if they connect to the previous part
            Polyline pLine = new Polyline();

            //check if collection will end as closed poly line or not
            //if any part has less or more than to intersections with other parts, then it isnt closed.
            bool isClosed = false;
            int startSegment = getFirstSeg(dbObjCol);
            if (startSegment == -1)
            {
                //load the first piece of the poly line into the Poly Line
                startSegment = 0;
                isClosed = true;
            }   
            DBObject lastPart = dbObjCol[startSegment];

            Point3d endPoint = new Point3d();
            Point3d startPoint = new Point3d();
            bool firstArc = false;
            double bulgeHold = new double();

            if (lastPart is Arc)
            {
                Arc a = lastPart as Arc;
                endPoint = a.EndPoint;
                startPoint = a.StartPoint;
            }
            else if (lastPart is Line)
            {
                Line l = lastPart as Line;
                endPoint = l.EndPoint;
                startPoint = l.StartPoint;
            }

            //iterate through the collection to get all vertices
            for (int i = 0; i < dbObjCol.Count; i++)
            {
                //use method to find the next part that touches the previous vertex
                DBObject dbObj = findNextPart(dbObjCol, lastPart, endPoint, startPoint);

                Point3d targetPoint = new Point3d();
                //be sure to use the point that WASNT the last point used
                //as the new vertex

                if (dbObj is Arc)
                {
                    Arc arcDat = dbObj as Arc;
                    Point3d a = arcDat.StartPoint;
                    Point3d b = arcDat.EndPoint;
                    double bulge;
                    if (a == endPoint)
                    {
                        targetPoint = b;
                        bulge = getArcBulge(arcDat.EndAngle, arcDat.StartAngle);

                    }
                    else
                    {
                        targetPoint = a;
                        bulge = getArcBulge(arcDat.EndAngle, arcDat.StartAngle);
                        bulge = 0 - bulge;
                    }

                    pLine.AddVertexAt(i, new Point2d(targetPoint.X, targetPoint.Y),
                        0, 0, 0);
                    //if the first part is an arc, we'll need to hold onto the bulge until the end
                    if (i > 0)
                    { pLine.SetBulgeAt(i - 1, bulge); }
                    else
                    {
                        firstArc = true;
                        bulgeHold = bulge;
                    }

                    pLine.Layer = arcDat.Layer;
                    pLine.Color = arcDat.Color;

                    //load the next parts in
                    startPoint = endPoint;
                    endPoint = targetPoint;

                }
                else if (dbObj is Line)
                {
                    Line lDat = dbObj as Line;
                    Point3d a = lDat.StartPoint;
                    Point3d b = lDat.EndPoint;
                    if (a == endPoint)
                    { targetPoint = b; }
                    else
                    { targetPoint = a; }

                    pLine.AddVertexAt(i, new Point2d(targetPoint.X, targetPoint.Y),
                        0, 0, 0);
                    pLine.Layer = lDat.Layer;
                    pLine.Color = lDat.Color;
                    //load up the next parts' points
                    startPoint = endPoint;
                    endPoint = targetPoint;
                }

                lastPart = dbObj;
            }

            if (firstArc == true)
            { pLine.SetBulgeAt(dbObjCol.Count - 1, bulgeHold); }

            //make the new pLine an obvious color for now
            pLine.ColorIndex = 0;
            
            pLine.Closed = isClosed;
            //btr.AppendEntity(pLine);
            //tr.TransactionManager.AddNewlyCreatedDBObject(pLine, true);

            return pLine;
        }

        //only works if parts are in correct order
        public static Polyline drawPolyLine3(DBObjectCollection dbObjCol)
        {
            Polyline pLine = new Polyline();
            PolylineSegmentCollection collection = new PolylineSegmentCollection();
            foreach(DBObject dbo in dbObjCol)
            {
                //convert part to polyline
                if (dbo is Arc)
                {
                    Arc arcDat = dbo as Arc;
                    double bulge = getArcBulge(arcDat.EndAngle, arcDat.StartAngle);
                    PolylineSegment pSeg = new PolylineSegment(new Point2d(arcDat.StartPoint.X, arcDat.StartPoint.Y), new Point2d(arcDat.EndPoint.X, arcDat.EndPoint.Y), bulge);
                    collection.Add(pSeg);
                }
                else if (dbo is Line)
                {
                    Line lDat = dbo as Line;
                    PolylineSegment pSeg = new PolylineSegment(new Point2d(lDat.StartPoint.X, lDat.StartPoint.Y), new Point2d(lDat.EndPoint.X, lDat.EndPoint.Y));
                    //add to collection
                    collection.Add(pSeg);
                }
            }
            //convert Collection to polyline
            collection.Join();
            pLine = collection.ToPolyline();
            return pLine;
        }     

        //use collection to create a polyline
        public static void drawPolyLine4(DBObjectCollection dbObjCol = null)
        {
            //**********************
            //For testing make a version where if a colleciton isnt sent we can make a selection on screen
            if(dbObjCol == null)
            {
                //have editor get selection of just arcs, polylines, lines
            }

            //find first line to use(if closed it wont find a "first) so just use array 0
            //loop through collection of pieces starting with "start" segment
            //add vertex to polygon for each piece
            //"loop" shouldnt iterate by order but by finding next intersecting segment


        }

        //uses join entity to create a polyline.
        //if there are parts in collection that arent connected it needs to be updated to create a different polyline
        public static Polyline drawPolyLine(DBObjectCollection dbObjCol)
        {
            //check if collection will end as closed poly line or not
            //if any part has less or more than to intersections with other parts, then it isnt closed.
            bool isClosed = false;
            int startSegment = getFirstSeg(dbObjCol);
            if (startSegment == -1)
            {
                //load the first piece of the poly line into the Poly Line
                startSegment = 0;
                isClosed = true;
            }

            //get first part to start building polyline
            DBObject firstSeg = dbObjCol[startSegment];
            Polyline pLine = makePolyLIne(firstSeg);
            dbObjCol.Remove(firstSeg);

            do
            {
                //find part that intersects
                DBObject seg = findIntersectingPart(dbObjCol, pLine as DBObject);
                //convert part to polyline
                Polyline pSeg = makePolyLIne(seg);
                //add part to polyline
                pLine.JoinEntity(pSeg);
                //remove part from collection
                dbObjCol.Remove(seg);
                //**Need to break if some parts exist but don't intersect with anything
                //prob need at that point to make into a diff polyline at some point
            } while (dbObjCol.Count > 0);

            pLine.Closed = isClosed;
            return pLine;
        }

        //create a polyline from a given segment
        private static Polyline makePolyLIne(DBObject dbo)
        {
            //convert part to polyline
            if (dbo is Arc)
            {
                Arc arcDat = dbo as Arc;
                double bulge = getArcBulge(arcDat.EndAngle, arcDat.StartAngle);
                //convert to polyline
                Polyline seg = new Polyline();
                seg.AddVertexAt(0, new Point2d(arcDat.StartPoint.X, arcDat.StartPoint.Y), bulge, 0, 0);
                seg.AddVertexAt(1, new Point2d(arcDat.EndPoint.X, arcDat.EndPoint.Y), 0, 0, 0);
                return seg;
            }
            else if (dbo is Line)
            {
                Polyline seg = new Polyline();
                Line lDat = dbo as Line;
                seg.AddVertexAt(0, new Point2d(lDat.StartPoint.X, lDat.StartPoint.Y), 0, 0, 0);
                seg.AddVertexAt(1, new Point2d(lDat.EndPoint.X, lDat.EndPoint.Y), 0, 0, 0);
                return seg;
            }
            else if (dbo is Polyline)
            { return dbo as Polyline; }
            else if (dbo is Spline)
            {
                Polyline seg = new Polyline();
                Spline spDat = dbo as Spline;
                Curve crv = spDat.ToPolyline();
                //something something
                return null;
            }
            else
            { return null; }           
        }

        //using a formula I found in forums. find what CAD considers the bulge of the arc
        //bulge = tan(included angle/4)
        //positive is counterclockwise
        public static double getArcBulge(double endAngle, double startAngle)
        {
            double deltaAng = endAngle - startAngle;
            if (deltaAng < 0)
                deltaAng += 2 * Math.PI;

            return Math.Tan(deltaAng * .25);
        }

        //check if parts make a open or closed polyline
        //return the first part if it is open, return null if it is closed
        private static int getFirstSeg(DBObjectCollection dbObjCol)
        {
            int oneIntersect = -1;
            int moreIntersec = -1;//if more than 2 intersects then there is a point or part overlapping and throwing the build, this will not make a functional polyline.
            Point3d firstStartPoint = new Point3d();
            Point3d firstEndPoint = new Point3d();
            Point3d secStartPoint = new Point3d();
            Point3d secEndPOint = new Point3d();
            
            for(int i = 0; i< dbObjCol.Count; i++)
            {
                int intersections = 0;
                DBObject dboFirst = dbObjCol[i];
                #region endpoints first
                if (dboFirst is Arc)
                {
                    Arc a = dboFirst as Arc;
                    firstEndPoint = a.EndPoint;
                    firstStartPoint = a.StartPoint;
                }
                else if (dboFirst is Line)
                {
                    Line l = dboFirst as Line;
                    firstEndPoint = l.EndPoint;
                    firstStartPoint = l.StartPoint;
                }
                #endregion

                for (int j = i+1; j<dbObjCol.Count; j++)
                {
                    DBObject dboSecond = dbObjCol[j];
                    #region endpoints second
                    if (dboSecond is Arc)
                    {
                        Arc a = dboSecond as Arc;
                        secEndPOint = a.EndPoint;
                        secStartPoint = a.StartPoint;
                    }
                    else if (dboSecond is Line)
                    {
                        Line l = dboSecond as Line;
                        secEndPOint = l.EndPoint;
                        secStartPoint = l.StartPoint;
                    }
                    #endregion

                    //compare endpoints add all end points
                    if (firstStartPoint == secStartPoint || firstStartPoint == secEndPOint)
                    { intersections++; }
                    if(firstEndPoint == secStartPoint || firstEndPoint == secEndPOint)
                    { intersections++; }
                }

                if (intersections < 2)
                {
                    oneIntersect = i;
                    return oneIntersect;//go ahead and break to stop waste of cycles
                }
                if(intersections >2)
                { moreIntersec = i; }//not sure what to do with this information yet
            }
            return oneIntersect;
        }

        //iterates through a collection of segments from an exploded polyline
        //to find which part connects to the one listed
        private static DBObject findNextPart(DBObjectCollection dbObjCol, DBObject dbObj, Point3d sharedPoint, Point3d oldPoint)
        {
            Point3d startPoint = new Point3d();
            Point3d endPoint = new Point3d();
            foreach (DBObject part in dbObjCol)
            {
                if (part != dbObj)
                {
                    //get the endPoint/start points
                    if (part is Arc)
                    {
                        Arc a = part as Arc;
                        startPoint = a.StartPoint;
                        endPoint = a.EndPoint;
                    }
                    if (part is Line)
                    {
                        Line l = part as Line;
                        startPoint = l.StartPoint;
                        endPoint = l.EndPoint;
                    }

                    if (sharedPoint == startPoint && endPoint != oldPoint)
                        return part;
                    else if (sharedPoint == endPoint && startPoint != oldPoint)
                        return part;
                }
            }
            return null;
        }

        private static DBObject findIntersectingPart(DBObjectCollection dbObjCol, DBObject dbObj)
        {
            Polyline pLine = makePolyLIne(dbObj);
            foreach (DBObject part in dbObjCol)
            {
                if (part != dbObj)
                {
                    Polyline seg = makePolyLIne(part);

                    if (pLine.StartPoint == seg.StartPoint ||
                        pLine.StartPoint == seg.EndPoint ||
                        pLine.EndPoint == seg.StartPoint ||
                        pLine.EndPoint == seg.EndPoint)
                    { return part; }
                }
            }
            return null;
        }
    }
}
