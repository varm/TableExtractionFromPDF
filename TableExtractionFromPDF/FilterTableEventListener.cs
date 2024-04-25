using System;
using System.Collections.Generic;
using System.Linq;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Filter;
using iText.Kernel.Colors;

namespace TableExtractionFromPDF
{
    internal class TableBorder
    {
        public int BorderId;
        public Rectangle Border;
        public List<KeyValuePair<int, Rectangle>> IntersectBorders;
        public int TableId;

        public TableBorder(int borderNumber, Rectangle border)
        {
            this.BorderId = borderNumber;
            this.Border = border;
            this.TableId = -1;
            IntersectBorders = new List<KeyValuePair<int, Rectangle>>();
        }

        public TableBorder(int borderNumber, Rectangle border, List<KeyValuePair<int, Rectangle>> intersectBorders) :
            this(borderNumber, border)
        {
            this.IntersectBorders.AddRange(intersectBorders);
        }

        public void AddIntersectBorder(int borderNumber, Rectangle intersectBorder)
        {
            this.IntersectBorders.Add(new KeyValuePair<int, Rectangle>(borderNumber, intersectBorder));
        }
    }

    public class FilterTableEventListener : IEventListener
    {
        private PdfPage pdfPage = null;
        private int borderCounter = 0, tableCounter = 0;
        private List<TableBorder> sqBorders = new List<TableBorder>();
        private List<TableBorder> hBorders = new List<TableBorder>();
        private List<TableBorder> vBorders = new List<TableBorder>();
        private System.Data.DataSet tableDS = new System.Data.DataSet();
        private IList<IEventData> textRenderList = new List<IEventData>();

        /// <summary>
        /// Process a PDF page to retrieve tables data from it.
        /// </summary>
        /// <param name="pdfPage">the pdf page which to process</param>
        /// <param name="withBorder">true if tables have fully borders, false otherwise</param>
        public FilterTableEventListener(PdfPage pdfPage, bool withBorder)
        {
            if(withBorder)
            {
                this.pdfPage = pdfPage;
                PdfCanvasProcessor processor = new PdfCanvasProcessor(this);
                processor.ProcessPageContent(pdfPage);
                GetTablesFromborders();
            }
        }

        private void GetTablesFromborders()
        {
            vBorders.Sort(delegate (TableBorder border1, TableBorder border2)
            {
                if (border1 == null && border2 == null) return 0;
                else if (border1 == null) return -1;
                else if (border2 == null) return 1;
                else
                {
                    int comparerInt = border1.Border.GetX().CompareTo(border2.Border.GetX());
                    if (comparerInt == 0)
                    {
                        comparerInt = border1.Border.GetY().CompareTo(border2.Border.GetY());
                    }
                    return comparerInt;
                }
            });
            hBorders.Sort(delegate (TableBorder border1, TableBorder border2)
            {
                if (border1 == null && border2 == null) return 0;
                else if (border1 == null) return -1;
                else if (border2 == null) return 1;
                else
                {
                    int comparerInt = border1.Border.GetY().CompareTo(border2.Border.GetY());
                    if (comparerInt == 0)
                    {
                        comparerInt = border1.Border.GetX().CompareTo(border2.Border.GetX());
                    }
                    return comparerInt;
                }
            });

            foreach (TableBorder vBorder in vBorders)
            {
                List<TableBorder> hIntersectBorders = GetHorizontalIntersectBorders(vBorder);
                hIntersectBorders.Sort(delegate (TableBorder border1, TableBorder border2)
                {
                    if (border1 == null && border2 == null) return 0;
                    else if (border1 == null) return -1;
                    else if (border2 == null) return 1;
                    else return border1.Border.GetY().CompareTo(border2.Border.GetY());
                });

                List<string> cols = new List<string>();
                int front = 0;

                for (int rear = 0; rear < hIntersectBorders.Count(); rear++)
                {
                    hIntersectBorders[rear].AddIntersectBorder(vBorder.BorderId, vBorder.Border);

                    if (front < rear)
                    {
                        List<KeyValuePair<int, Rectangle>> frontIntersects = hIntersectBorders[front].IntersectBorders;
                        List<KeyValuePair<int, Rectangle>> rearIntersects = hIntersectBorders[rear].IntersectBorders;
                        frontIntersects.Sort(delegate (KeyValuePair<int, Rectangle> rect1, KeyValuePair<int, Rectangle> rect2)
                        {
                            if (rect1.Value == null && rect2.Value == null) return 0;
                            else if (rect1.Value == null) return -1;
                            else if (rect2.Value == null) return 1;
                            else
                            {
                                int comparerInt = rect1.Value.GetX().CompareTo(rect2.Value.GetX()) * -1;
                                if (comparerInt == 0)
                                {
                                    comparerInt = rect1.Value.GetY().CompareTo(rect2.Value.GetY()) * -1;
                                }
                                return comparerInt;
                            }
                        });
                        rearIntersects.Sort(delegate (KeyValuePair<int, Rectangle> rect1, KeyValuePair<int, Rectangle> rect2)
                        {
                            if (rect1.Value == null && rect2.Value == null) return 0;
                            else if (rect1.Value == null) return -1;
                            else if (rect2.Value == null) return 1;
                            else
                            {
                                int comparerInt = rect1.Value.GetX().CompareTo(rect2.Value.GetX()) * -1;
                                if (comparerInt == 0)
                                {
                                    comparerInt = rect1.Value.GetY().CompareTo(rect2.Value.GetY()) * -1;
                                }
                                return comparerInt;
                            }
                        });

                        bool isMatchFlag = false;
                        for (int fIndex = 0; fIndex < frontIntersects.Count(); fIndex++)
                        {
                            for (int rIndex = 0; rIndex < rearIntersects.Count(); rIndex++)
                            {
                                if (frontIntersects[fIndex].Key == rearIntersects[rIndex].Key
                                    && frontIntersects[fIndex].Key != vBorder.BorderId)
                                {
                                    Rectangle rectangle = GetSingleRectangleFromBorder(
                                        vBorder.Border, hIntersectBorders[front].Border,
                                        frontIntersects[fIndex].Value, hIntersectBorders[rear].Border);
                                    if (rectangle != null)
                                    {
                                        cols.Add(GetTextFromRectangle(rectangle));
                                        TableBorder firstVBorder = vBorders.FirstOrDefault(l => l.BorderId == frontIntersects[fIndex].Key);
                                        vBorder.TableId = firstVBorder.TableId = GetTableIdForRectangle(firstVBorder, vBorder);
                                    }
                                    front = rear;
                                    isMatchFlag = true;
                                    break;
                                }
                            }
                            if (isMatchFlag)
                            {
                                break;
                            }
                        }
                    }
                }
                PopulateTable(vBorder, cols);
            }
        }

        private void PopulateTable(TableBorder vBorder, List<string> cols)
        {
            if (!(tableDS.Tables.Count < vBorder.TableId) && tableDS.Tables.Count > 0 && vBorder.TableId > 0)
            {
                if (tableDS.Tables[vBorder.TableId - 1].Rows.Count > 0)
                {
                    tableDS.Tables[vBorder.TableId - 1].Columns.Add();
                    cols.Reverse();
                    int columIndex = tableDS.Tables[vBorder.TableId - 1].Columns.Count - 1;
                    for (int rowIndex = 0; rowIndex < cols.Count(); rowIndex++)
                    {
                        if (rowIndex < tableDS.Tables[vBorder.TableId - 1].Rows.Count)
                        {
                            tableDS.Tables[vBorder.TableId - 1].Rows[rowIndex][columIndex] = cols[rowIndex];
                        }
                        else
                        {
                            System.Data.DataRow newRow = tableDS.Tables[vBorder.TableId - 1].NewRow();
                            newRow[columIndex] = cols[rowIndex];
                            tableDS.Tables[vBorder.TableId - 1].Rows.Add(newRow);
                        }
                    }
                }
                else
                {
                    tableDS.Tables[vBorder.TableId - 1].Columns.Add();
                    cols.Reverse();
                    foreach (string col in cols)
                    {
                        System.Data.DataRow newRow = tableDS.Tables[vBorder.TableId - 1].NewRow();
                        newRow[0] = col;
                        tableDS.Tables[vBorder.TableId - 1].Rows.Add(newRow);
                    }
                }
            }
        }

        private int GetTableIdForRectangle(TableBorder vBorder1, TableBorder vBorder2)
        {
            int tblId = (vBorder1 != null && vBorder1.TableId > 0) ? vBorder1.TableId :
                        (vBorder2 != null && vBorder2.TableId > 0 ? vBorder2.TableId : ++tableCounter);
            if (tableDS.Tables.Count < tableCounter)
            {
                tableDS.Tables.Add(new System.Data.DataTable());
            }
            return tblId;
        }

        private string GetTextFromRectangle(Rectangle rectangle)
        {
            string rectText = String.Empty;

            TextRegionEventFilter textRegionEventFilter = new TextRegionEventFilter(rectangle);
            LocationTextExtractionStrategy extractionStrategy = new LocationTextExtractionStrategy();

            foreach (IEventData textRender in textRenderList)
            {
                if (textRegionEventFilter.IsInsideRectangle(textRender, EventType.RENDER_TEXT))
                {
                    extractionStrategy.EventOccurred(textRender, EventType.RENDER_TEXT);
                }
                else if (textRegionEventFilter.Accept(textRender, EventType.RENDER_TEXT))
                {
                    TextRenderInfo textRenderInfo = (TextRenderInfo)textRender;
                    IList<TextRenderInfo> renderInfoList = textRenderInfo.GetCharacterRenderInfos();
                    for (int index = 0; index < renderInfoList.Count(); index++)
                    {
                        if (textRegionEventFilter.IsInsideRectangle(renderInfoList[index], EventType.RENDER_TEXT))
                        {
                            extractionStrategy.EventOccurred(renderInfoList[index], EventType.RENDER_TEXT);
                        }
                    }
                }
            }
            rectText = extractionStrategy.GetResultantText();
            return rectText;
        }

        private Rectangle GetSingleRectangleFromBorder(Rectangle vBorder1, Rectangle hBorder1,
                                                       Rectangle vBorder2, Rectangle hBorder2)
        {
            try
            {
                Rectangle resltRect = Rectangle.CreateBoundingRectangleFromQuadPoint(new PdfArray(new double[]
                { vBorder2.GetX(), hBorder1.GetY(), vBorder1.GetX() + vBorder1.GetWidth(), hBorder1.GetY(),
                vBorder2.GetX(), hBorder2.GetY() + hBorder2.GetHeight(),
                vBorder1.GetX() + vBorder1.GetWidth(), hBorder2.GetY() + hBorder2.GetHeight() })); ;

                return resltRect;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private List<TableBorder> GetHorizontalIntersectBorders(TableBorder vBorder)
        {
            List<TableBorder> hIntersectBorders = new List<TableBorder>();
            Rectangle mRect = new Rectangle(vBorder.Border.GetX() - 1, vBorder.Border.GetY() - 1,
                vBorder.Border.GetWidth() + 2, vBorder.Border.GetHeight() + 2);

            hIntersectBorders = hBorders.Where(l => l.Border.Overlaps(mRect)).ToList();

            return hIntersectBorders;
        }

        public virtual void EventOccurred(IEventData iEventData, EventType eventType)
        {
            if (eventType == EventType.RENDER_PATH)
            {
                PathRenderInfo pathInfo = (PathRenderInfo)iEventData;
                Color colorFill = iEventData.GetGraphicsState().GetFillColor();
                Color colorStroke = iEventData.GetGraphicsState().GetStrokeColor();
                int pathOperation = pathInfo.GetOperation();

                if (!pathInfo.IsPathModifiesClippingPath() && pathOperation != PathRenderInfo.NO_OP)
                {
                    if (pathOperation == PathRenderInfo.STROKE)
                    {
                        if (!Color.IsWhite(colorStroke.GetColorSpace(), colorStroke.GetColorValue()))
                        {
                            Path path = pathInfo.GetPath().TransformPath(pathInfo.GetCtm(), false);
                            ProcessPathAsline(path, pathInfo.GetLineWidth());
                        }
                    }
                    else if (pathOperation == PathRenderInfo.FILL)
                    {
                        if (!Color.IsWhite(colorFill.GetColorSpace(), colorFill.GetColorValue()))
                        {
                            Path path = pathInfo.GetPath().TransformPath(pathInfo.GetCtm(), false);
                            ProcessPathAsRectangle(path);
                        }
                    }
                    else if (pathOperation == (int)(PathRenderInfo.STROKE | PathRenderInfo.FILL))
                    {
                        if (!Color.IsWhite(colorFill.GetColorSpace(), colorFill.GetColorValue()))
                        {
                            Path path = pathInfo.GetPath().TransformPath(pathInfo.GetCtm(), false);
                            ProcessPathAsRectangle(path);
                        }
                        else if (!Color.IsWhite(colorStroke.GetColorSpace(), colorStroke.GetColorValue()) &&
                            Color.IsWhite(colorFill.GetColorSpace(), colorFill.GetColorValue()))
                        {
                            Path path = pathInfo.GetPath().TransformPath(pathInfo.GetCtm(), false);
                            ProcessPathAsline(path, pathInfo.GetLineWidth());
                        }
                    }
                }
            }
            else if (eventType == EventType.RENDER_TEXT)
            {
                if (iEventData is AbstractRenderInfo)
                {
                    ((AbstractRenderInfo)iEventData).PreserveGraphicsState();
                    textRenderList.Add(iEventData);
                }
            }
        }

        private void ProcessPathAsRectangle(Path path)
        {
            foreach (Subpath subpath in path.GetSubpaths())
            {
                IList<IShape> segments = subpath.GetSegments();

                if (segments.Count == 3 && subpath.IsClosed())
                {

                    Line line1 = segments[0].GetType() == typeof(Line) ? (Line)segments[0] : null;
                    Point p11 = line1 != null ? line1.GetBasePoints()[0] : null;
                    Point p12 = line1 != null ? line1.GetBasePoints()[1] : null;

                    Line line2 = segments[1].GetType() == typeof(Line) ? (Line)segments[1] : null;
                    Point p21 = line2 != null ? line2.GetBasePoints()[0] : null;
                    Point p22 = line2 != null ? line2.GetBasePoints()[1] : null;

                    Line line3 = segments[2].GetType() == typeof(Line) ? (Line)segments[2] : null;
                    Point p31 = line3 != null ? line3.GetBasePoints()[0] : null;
                    Point p32 = line3 != null ? line3.GetBasePoints()[1] : null;

                    if (line1 != null && line2 != null && line3 != null
                        && (p11.x == p12.x || p11.y == p12.y)
                        && (p21.x == p22.x || p21.y == p22.y)
                        && (p31.x == p32.x || p31.y == p32.y))
                    {
                        Rectangle rect = Rectangle.CreateBoundingRectangleFromQuadPoint(
                            new PdfArray(new double[] { p11.x, p11.y, p12.x, p12.y, p22.x, p22.y, p32.x, p32.y }));

                        ProcessBorder(rect);
                    }
                }
            }
        }

        private Rectangle FullJoinRectangle(Rectangle tempRect, Rectangle border)
        {
            try
            {
                double minX, minY, maxX, maxY;
                minX = Math.Min(tempRect.GetX(), border.GetX());
                minY = Math.Min(tempRect.GetY(), border.GetY());
                maxX = Math.Max(tempRect.GetX() + tempRect.GetWidth(), border.GetX() + border.GetWidth());
                maxY = Math.Max(tempRect.GetY() + tempRect.GetHeight(), border.GetY() + border.GetHeight());

                return Rectangle.CreateBoundingRectangleFromQuadPoint(
                        new PdfArray(new double[] { minX, minY, maxX, minY, minX, maxY, maxX, maxY }));
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private void ProcessPathAsline(Path path, float lineWidth)
        {
            foreach (Subpath subpath in path.GetSubpaths())
            {
                if (subpath.IsClosed())
                {
                    path.ReplaceCloseWithLine();
                }

                IList<IShape> segments = subpath.GetSegments();
                double resltWidth = lineWidth <= 0 ? 0.2d : lineWidth;

                foreach (IShape segment in segments)
                {
                    Line line = segment.GetType() == typeof(Line) ? (Line)segment : null;
                    Point p1 = line != null ? line.GetBasePoints()[0] : null;
                    Point p2 = line != null ? line.GetBasePoints()[1] : null;

                    if (line != null)
                    {
                        if (p1.x == p2.x)
                        {
                            Rectangle rect = Rectangle.CreateBoundingRectangleFromQuadPoint(
                            new PdfArray(new double[] { p1.x - (resltWidth / 2), p1.y, p2.x - (resltWidth / 2), p2.y,
                                p1.x + (resltWidth / 2), p1.y, p2.x + (resltWidth / 2), p2.y }));
                            ProcessBorder(rect);
                        }
                        else if (p1.y == p2.y)
                        {
                            Rectangle rect = Rectangle.CreateBoundingRectangleFromQuadPoint(
                            new PdfArray(new double[] { p1.x, p1.y - (resltWidth / 2), p2.x, p2.y - (resltWidth / 2),
                                p1.x, p1.y + (resltWidth / 2), p2.x, p2.y + (resltWidth / 2) }));
                            ProcessBorder(rect);
                        }
                    }
                }
            }
        }

        private void ProcessBorder(Rectangle rect)
        {
            Rectangle mRect = new Rectangle(rect.GetX() - 1, rect.GetY() - 1, rect.GetWidth() + 2, rect.GetHeight() + 2);
            //////////////////////////VB///////////////////
            if (rect.GetHeight() > rect.GetWidth() * 1.5)
            {
                List<TableBorder> sqList = sqBorders.Where(sq => mRect.Overlaps(sq.Border)).ToList();
                List<TableBorder> tempSqList = new List<TableBorder>();

                foreach (TableBorder sq in sqList)
                {
                    rect = VJoinRectangle(rect, sq.Border);
                    //TODO m SQ
                    TableBorder tempSq = sq;
                    sqBorders.Remove(sq);

                    if (IsHInside(rect, tempSq.Border))
                    {
                        tempSq.Border = HJoinRectangle(tempSq.Border, rect);
                        tempSq.Border = ProcessPartialyJoinRect(tempSq.Border, ref tempSqList, "F", "Sq");
                        tempSqList.Add(tempSq);
                    }
                }
                ////////hl///////
                mRect = new Rectangle(rect.GetX() - 1, rect.GetY() - 1, rect.GetWidth() + 2, rect.GetHeight() + 2);
                List<TableBorder> hList = hBorders.Where(hb => mRect.Overlaps(hb.Border)).ToList();
                List<TableBorder> tempHlList = new List<TableBorder>();

                foreach (TableBorder hl in hList)
                {
                    rect = VJoinRectangle(rect, hl.Border);
                    //TODO m hl
                    TableBorder tempHl = hl;
                    hBorders.Remove(hl);

                    if (IsHInside(rect, tempHl.Border))
                    {
                        tempHl.Border = HJoinRectangle(tempHl.Border, rect);
                        tempHl.Border = ProcessPartialyJoinRect(tempHl.Border, ref tempHlList, "F", "HL");
                        tempHl.Border = ProcessPartialyJoinRect(tempHl.Border, ref tempSqList, "H", "Sq");
                        tempHlList.Add(tempHl);
                    }
                }
                ///////////VL////////////
                mRect = new Rectangle(rect.GetX() - 1, rect.GetY() - 1, rect.GetWidth() + 2, rect.GetHeight() + 2);
                List<TableBorder> vList = vBorders.Where(vb => mRect.Overlaps(vb.Border)).ToList();
                AddPBorder(ref rect, ref vList, ref tempHlList, ref tempSqList, false);
            }
            //////////////////////////HB///////////////////
            else if (rect.GetWidth() > rect.GetHeight() * 1.5)
            {
                /////SQ/////
                List<TableBorder> sqList = sqBorders.Where(sq => mRect.Overlaps(sq.Border)).ToList();
                List<TableBorder> tempSqList = new List<TableBorder>();

                foreach (TableBorder sq in sqList)
                {
                    rect = HJoinRectangle(rect, sq.Border);
                    //TODO m SQ
                    TableBorder tempSq = sq;
                    sqBorders.Remove(sq);

                    if (IsVInside(rect, tempSq.Border))
                    {
                        tempSq.Border = VJoinRectangle(tempSq.Border, rect);
                        tempSq.Border = ProcessPartialyJoinRect(tempSq.Border, ref tempSqList, "F", "Sq");
                        tempSqList.Add(tempSq);
                    }
                }
                /////VL///////
                mRect = new Rectangle(rect.GetX() - 1, rect.GetY() - 1, rect.GetWidth() + 2, rect.GetHeight() + 2);
                List<TableBorder> vList = vBorders.Where(vb => mRect.Overlaps(vb.Border)).ToList();
                List<TableBorder> tempVlList = new List<TableBorder>();

                foreach (TableBorder vl in vList)
                {
                    rect = HJoinRectangle(rect, vl.Border);
                    //TODO m hl
                    TableBorder tempVl = vl;
                    vBorders.Remove(vl);

                    if (IsVInside(rect, tempVl.Border))
                    {
                        tempVl.Border = VJoinRectangle(tempVl.Border, rect);
                        tempVl.Border = ProcessPartialyJoinRect(tempVl.Border, ref tempVlList, "F", "VL");
                        tempVl.Border = ProcessPartialyJoinRect(tempVl.Border, ref tempSqList, "V", "Sq");
                        tempVlList.Add(tempVl);
                    }
                }
                //////HL////////
                mRect = new Rectangle(rect.GetX() - 1, rect.GetY() - 1, rect.GetWidth() + 2, rect.GetHeight() + 2);
                List<TableBorder> hList = hBorders.Where(hb => mRect.Overlaps(hb.Border)).ToList();

                AddPBorder(ref rect, ref hList, ref tempVlList, ref tempSqList, true);
            }
            /////////////////////////SqB///////////////////
            else
            {
                ProcessSQBorder(ref rect, mRect);
            }
        }

        private Rectangle ProcessPartialyJoinRect(Rectangle rect, ref List<TableBorder> bList, string joinType, string listType)
        {
            Rectangle mR = new Rectangle(rect.GetX() - 1, rect.GetY() - 1, rect.GetWidth() + 2, rect.GetHeight() + 2);

            foreach (TableBorder bl in bList)
            {
                if (mR.Overlaps(bl.Border))
                {
                    rect = joinType.Equals("F") ? FullJoinRectangle(rect, bl.Border) : joinType.Equals("V") ?
                        VJoinRectangle(rect, bl.Border) : HJoinRectangle(rect, bl.Border);

                    if (joinType.Equals("F") || (joinType.Equals("V") && !IsHInside(rect, bl.Border))
                       || (joinType.Equals("H") && !IsVInside(rect, bl.Border)))
                    {
                        bList.Remove(bl);
                        if (listType.Equals("VL"))
                        {
                            if (vBorders.Any(t => t.BorderId == bl.BorderId))
                            {
                                vBorders.Remove(bl);
                            }
                        }
                        else if (listType.Equals("HL"))
                        {
                            if (hBorders.Any(t => t.BorderId == bl.BorderId))
                            {
                                hBorders.Remove(bl);
                            }
                        }
                        else
                        {
                            if (sqBorders.Any(t => t.BorderId == bl.BorderId))
                            {
                                sqBorders.Remove(bl);
                            }
                        }
                        break;
                    }
                }
            }
            return rect;
        }

        private bool IsVInside(Rectangle rect1, Rectangle rect2)
        {
            return !(rect1.GetY() <= rect2.GetY() && rect2.GetY() <= rect1.GetY() + rect1.GetHeight()) ||
            !(rect1.GetY() <= rect2.GetY() + rect2.GetHeight() && rect2.GetY() + rect2.GetHeight() <= rect1.GetY() + rect1.GetHeight());
        }

        private bool IsHInside(Rectangle rect1, Rectangle rect2)
        {
            return !(rect1.GetX() <= rect2.GetX() && rect2.GetX() <= rect1.GetX() + rect1.GetWidth()) || !(rect1.GetX() <=
                rect2.GetX() + rect2.GetWidth() && rect2.GetX() + rect2.GetWidth() <= rect1.GetX() + rect1.GetWidth());
        }

        private Rectangle HJoinRectangle(Rectangle hB, Rectangle jB)
        {
            float rx = hB.GetX();
            hB.SetX(Math.Min(rx, jB.GetX()));
            hB.SetWidth(Math.Max(rx + hB.GetWidth() - hB.GetX(), jB.GetX() + jB.GetWidth() - hB.GetX()));

            return hB;
        }

        private Rectangle VJoinRectangle(Rectangle vB, Rectangle jB)
        {
            float sry = vB.GetY();
            vB.SetY(Math.Min(jB.GetY(), sry));
            vB.SetHeight(Math.Max(jB.GetY() + jB.GetHeight() - vB.GetY(), sry + vB.GetHeight() - vB.GetY()));

            return vB;
        }

        private void ProcessSQBorder(ref Rectangle rect, Rectangle mRect)
        {
            List<TableBorder> sqList = sqBorders.Where(sq => mRect.Overlaps(sq.Border)).ToList();
            List<TableBorder> tempSqList = new List<TableBorder>();

            foreach (TableBorder sq in sqList)
            {
                rect = FullJoinRectangle(rect, sq.Border);
                sqBorders.Remove(sq);
            }

            mRect = new Rectangle(rect.GetX() - 1, rect.GetY() - 1, rect.GetWidth() + 2, rect.GetHeight() + 2);
            List<TableBorder> hList = hBorders.Where(hb => mRect.Overlaps(hb.Border)).ToList();

            if (hList != null && hList.Count > 1)
            {
                Rectangle rHl = hList[0].Border;

                foreach (TableBorder hl in hList)
                {
                    rHl = FullJoinRectangle(rHl, hl.Border);
                    hBorders.Remove(hl);
                }
                hBorders.Add(new TableBorder(borderCounter++, rHl));
            }

            List<TableBorder> vList = vBorders.Where(vb => mRect.Overlaps(vb.Border)).ToList();
            if (vList != null && vList.Count > 1)
            {
                Rectangle rvl = vList[0].Border;

                foreach (TableBorder vl in vList)
                {
                    rvl = FullJoinRectangle(rvl, vl.Border);
                    vBorders.Remove(vl);
                }
                vBorders.Add(new TableBorder(borderCounter++, rvl));
            }
            sqBorders.Add(new TableBorder(borderCounter++, rect));
        }

        private void AddPBorder(ref Rectangle rect, ref List<TableBorder> mList, ref List<TableBorder> pList, ref List<TableBorder> sqList, bool isVOrH)
        {
            foreach (TableBorder ml in mList)
            {
                rect = FullJoinRectangle(rect, ml.Border);

                if (isVOrH)
                {
                    hBorders.Remove(ml);
                }
                else
                {
                    vBorders.Remove(ml);
                }
            }
            if (isVOrH)
            {
                hBorders.Add(new TableBorder(borderCounter++, rect));
            }
            else
            {
                vBorders.Add(new TableBorder(borderCounter++, rect));
            }


            foreach (TableBorder pl in pList)
            {
                if (isVOrH)
                {
                    vBorders.Add(pl);
                }
                else
                {
                    hBorders.Add(pl);
                }

            }
            foreach (TableBorder sq in sqList)
            {
                sqBorders.Add(sq);
            }
        }

        public virtual ICollection<EventType> GetSupportedEvents()
        {
            return null;
        }

        /// <summary>
        /// Returns Dataset of retrieved tables from the PDF page.
        /// </summary>
        /// <returns></returns>
        public System.Data.DataSet GetTables()
        {
            return tableDS;
        }
    }
    
}
