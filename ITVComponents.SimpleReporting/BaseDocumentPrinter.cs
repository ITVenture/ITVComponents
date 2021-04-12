using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Logging;
using ITVComponents.SimpleReporting.Config;
using ITVComponents.SimpleReporting.Helpers;
using ITVComponents.SimpleReporting.Internal;
using ITVComponents.SimpleReporting.ObjectPositioning;
using ITVComponents.SimpleReporting.Objects;
using Image = System.Drawing.Image;

namespace ITVComponents.SimpleReporting
{
    public abstract class BaseDocumentPrinter:IDocument, IInternalDocument
    {
        private List<Page> pages = new List<Page>();
        private Dictionary<string,Font> fonts = new Dictionary<string, Font>();
        public abstract DocumentConfiguration Configuration { get; }
        private Graphics measurer;
        private Bitmap measurementimage;
        private RenderContext renderContext;
        private int objectCounter = 0;
        private bool countObjects = true;
        private float? virtualBottom = null;
        private List<Layer> layers = new List<Layer>();
        protected BaseDocumentPrinter()
        {
            MinimumConfigurationRequirements = new DocumentConfiguration("MinimumConfig", new []
            {
                "PageRectangle",
                "ContentArea",
                "PageCountMeasurementReplacerLength",
                "Landscape"

            }, new []
            {
                typeof(RectangleF),
                typeof(RectangleF),
                typeof(int),
                typeof(bool)
            });
            renderContext = new RenderContext(() => ContentArea);
            measurementimage = new Bitmap(5000, 5000);
            float x = measurementimage.HorizontalResolution;
            measurementimage.SetResolution(300, 300);
            measurer = Graphics.FromImage(measurementimage);
            measurer.PageUnit = GraphicsUnit.Document;
            measurer.PageScale = 300/x;
        }

        /// <summary>
        /// Gets the minimum required configuration for renderers based on BaseDocumentPrinter
        /// </summary>
        protected DocumentConfiguration MinimumConfigurationRequirements { get; } 

        /// <summary>
        /// Gets the most accurate Configuration when requesting page-configurations
        /// </summary>
        protected DocumentConfiguration PageConfiguration  => pages.Count != 0 && CurrentPage > 0 ? pages[CurrentPage-1].Config : Configuration;

        /// <summary>
        /// Gets the Rendering-context for the current document
        /// </summary>
        public RenderContext RenderContext => renderContext;

        /// <summary>
        /// Gets or sets the Page-Size for the current Document or page
        /// </summary>
        public RectangleF PageRectangle
        {
            get
            {
                return
                    PageConfiguration.GetValue<RectangleF>(
                        "PageRectangle");
            }
            set {
                PageConfiguration.SetValue<RectangleF>(
                      "PageRectangle",value);
                SetLandscape(Landscape);
            }
        }

        /// <summary>
        /// Gets all layers
        /// </summary>
        public IEnumerable<Layer> Layers => from t in layers select t;

        /// <summary>
        /// Gets or sets the ContentArea for the current Document or Page
        /// </summary>
        public RectangleF ContentArea
        {
            get
            {
                return
                    PageConfiguration.GetValue<RectangleF>(
                        "ContentArea");
            }
            set
            {
                PageConfiguration.SetValue<RectangleF>(
                    "ContentArea", value);
                SetLandscape(Landscape);
            }
        }

        /// <summary>
        /// Gets or sets the current Page Id
        /// </summary>
        public int CurrentPage { get; private set; }

        /// <summary>
        /// Gets a value indicating whether adding pages is supported by this Printer
        /// </summary>
        protected abstract bool AllowAddingPages { get;  }

        /// <summary>
        /// Gets a value indicating whether Block-Move operations are supported by this Printer
        /// </summary>
        protected abstract bool AllowBlockMoveOperations { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the Page-Layout is Landscape (true) or Portrait (false)
        /// </summary>
        public bool Landscape
        {
            get { return PageConfiguration.GetValue<bool>("Landscape"); }
            set
            {
                PageConfiguration.SetValue("Landscape", value);
                SetLandscape(value);
            }
        }

        /// <summary>
        /// Gets or sets the String that is used as replacer when measuring the String-Size containing a PageCounter mark
        /// </summary>
        public int PageCountMeasurementReplacerLength
        {
            get
            {
                var tmp =
                    Configuration.GetValue<int>(
                        "PageCountMeasurementReplacerLength");
                if (tmp < 3)
                {
                    tmp = 3;
                }

                return tmp;
            }
            set
            {
                if (value >= 3)
                {
                    Configuration.SetValue<int>(
                        "PageCountMeasurementReplacerLength", value);
                }
            }
        }

        /// <summary>
        /// Initializes a document using the attached configuration
        /// </summary>
        public void InitializeDocument()
        {
            objectCounter = 0;
            countObjects = true;
            pages.Clear();
            NextPage();
        }

        /// <summary>
        /// Adds a layer object to the list of layers
        /// </summary>
        /// <param name="layer"></param>
        public void AddLayer(Layer layer)
        {
            if (layers.Any(n => n.LayerName == layer.LayerName))
            {
                throw new ArgumentException("LayerName must be unique");
            }

            layers.Add(layer);
        }

        /// <summary>
        /// Adds an object to the specified layer
        /// </summary>
        /// <param name="layerName">the layer on which to add the object</param>
        /// <param name="page">the page on which the object was created</param>
        /// <param name="drawingObject">the drawing-object to add on the given page</param>
        void IInternalDocument.AddLayerObject(string layerName, Page page, IDrawingObject drawingObject)
        {
            Layer layer = layers.First(n => n.LayerName == layerName);
            layer.Objects.Add(new LayerObject {Layer = layer, Page = page, Object = drawingObject});
        }

        /// <summary>
        /// Adds an object to the specified layer
        /// </summary>
        /// <param name="layerName">the layer on which to add the object</param>
        /// <param name="page">the page on which the object was created</param>
        /// <param name="drawingObjects">the set of drawing-objects to add on the given page</param>
        void IInternalDocument.AddLayerObjects(string layerName, Page page, IEnumerable<IDrawingObject> drawingObjects)
        {
            Layer layer = layers.First(n => n.LayerName == layerName);
            layer.Objects.AddRange(from t in drawingObjects
                select new LayerObject {Layer = layer, Page = page, Object = t});
        }

        /// <summary>
        /// Gets the layer objects for the given Page
        /// </summary>
        /// <param name="page">the page object for which to get the layer-objects</param>
        /// <returns>an enumerable containing all layer-objects</returns>
        public IEnumerable<LayerObject> GetLayerObjects(Page page)
        {
            return (from t in layers select t.Objects.Where(n => n.Page == page)).SelectMany(o => o);
        }

        /// <summary>
        /// Draws a line
        /// </summary>
        /// <param name="start">the starting-poing of the line</param>
        /// <param name="end">the end-point of the line</param>
        /// <param name="lineColor">the color of the drawn line</param>
        /// <param name="layerName">the layer on which t add the Line object</param>
        public void DrawLine(PointF start, PointF end, Color? lineColor = null, string layerName = null)
        {
            pages[CurrentPage - 1].AddObject(new Line
            {
                Point1 = start,
                Point2 = end,
                ObjectId = countObjects ? ++objectCounter : objectCounter,
                LineColor = lineColor.GetValueOrDefault(Color.Black)
            }, layerName);
        }

        /// <summary>
        /// Fills a rectangle
        /// </summary>
        /// <param name="rect">the coordinates and measures of the rectangle</param>
        /// <param name="color">the color that is used to fill the rectangle</param>
        /// <param name="layerName">the layer on which t add the object</param>
        public void FillFrame(RectangleF rect, Color color, string layerName = null)
        {
            pages[CurrentPage-1].AddObject(new Frame
            {
                FillColor = color,
                ObjectRegion = rect,
                BorderColor = Color.Transparent,
                ObjectId = countObjects ? ++objectCounter : objectCounter
            }, layerName);
        }

        /// <summary>
        /// Draws a Frame
        /// </summary>
        /// <param name="rect">the coordinates and measures of the rectangle</param>
        /// <param name="lineColor">the line color for this frame</param>
        /// <param name="fillColor">the filling-color for this frame</param>
        /// <param name="layerName">the layer on which t add the object</param>
        public void DrawFrame(RectangleF rect, Color? lineColor = null, Color? fillColor = null, string layerName = null)
        {
            pages[CurrentPage-1].AddObject(new Frame
            {
                BorderColor = lineColor.GetValueOrDefault(Color.Black),
                FillColor = fillColor.GetValueOrDefault(Color.Transparent),
                ObjectRegion = rect,
                ObjectId = countObjects ? ++objectCounter : objectCounter
            },layerName);
        }

        /// <summary>
        /// Draws a rectangle.
        /// </summary>
        /// <param name="rect">the coordinates and measures of the rectangle</param>
        /// <param name="borderColor">the desired border-color</param>
        /// <param name="fillColor">the desired fill-color</param>
        /// <param name="layerName">the layer on which t add the object</param>
        public void Frame(RectangleF rect, Color borderColor, Color fillColor, string layerName = null)
        {
            pages[CurrentPage-1].AddObject(new Frame
            {
                FillColor = fillColor,
                ObjectRegion = rect,
                BorderColor = borderColor,
                ObjectId = countObjects ? ++objectCounter : objectCounter
            }, layerName);
        }

        /// <summary>
        /// Measures a specified text
        /// </summary>
        /// <param name="text">the text to measure</param>
        /// <param name="fontName">the font to use for drawing</param>
        /// <param name="fontSize">the fontsize</param>
        /// <param name="fontStyle">the fontstyle</param>
        /// <param name="maxSize">the max size for the textblock</param>
        /// <param name="horizontalAlignment">the horizontal string alignment</param>
        /// <param name="verticalAlignment">the vertical string alignment</param>
        /// <param name="desiredLineHeight">the desired Line-Height for a single line of text</param>
        /// <returns>the resulting size of the text</returns>
        public SizeF MeasureText(string text, string fontName, float fontSize, FontStyle fontStyle, SizeF maxSize, StringAlignment horizontalAlignment, StringAlignment verticalAlignment, float desiredLineHeight = 0)
        {
            text = ReplacePageIdTextValue(text, CurrentPage.ToString());
            Font f = GetFont(fontName, fontSize, fontStyle);
            var format = new StringFormat() { Alignment = horizontalAlignment, LineAlignment = verticalAlignment, Trimming = StringTrimming.Word, FormatFlags = StringFormatFlags.MeasureTrailingSpaces };
            text = ReplacePageCounterTextValue(text, PageCountMeasurementReplacerLength);
            if (char.IsWhiteSpace(text, text.Length - 1))
            {
                text += "#";
            }
            var retVal = measurer.MeasureString(text, f, maxSize, format);
            float height = retVal.Height;
            float width = retVal.Width;
            if (Math.Abs(desiredLineHeight) > 0.1F)
            {
                height = height/f.FontFamily.GetLineSpacing(fontStyle)*desiredLineHeight;
            }

            width = (float)Math.Round(width, 1, MidpointRounding.AwayFromZero);
            height = (float)Math.Round(height, 1, MidpointRounding.AwayFromZero);
            retVal = new SizeF(width, height);
            return retVal;
        }

        /// <summary>
        /// Draws the specified text at the provided position
        /// </summary>
        /// <param name="text">the text to draw</param>
        /// <param name="fontName">the font to use for drawing</param>
        /// <param name="fontSize">the fontsize</param>
        /// <param name="fontStyle">the fontstyle</param>
        /// <param name="position">the position where to draw</param>
        /// <param name="horizontalAlignment">the horizontal alignment for drawing</param>
        /// <param name="verticalAlignment">the vertical alignment for drawing</param>
        /// <param name="desiredLineHeight">the desired line-height for a single line of the provided text</param>
        /// <param name="rotation">the rotation of the printed text</param>
        /// <param name="textColor">the text-color of the added text</param>
        /// <param name="measure">indicates whether to measure the text and calculating an appropriate rectangle for it</param>
        /// <param name="layerName">the layer on which t add the object</param>
        /// <returns>the effectieve measures of the drawed text</returns>
        public RectangleF WriteText(string text, string fontName, float fontSize, FontStyle fontStyle, RectangleF position,
            StringAlignment horizontalAlignment, StringAlignment verticalAlignment, float desiredLineHeight = 0F, float rotation = 0F, Color textColor = default(Color), bool measure = true, string layerName = null)
        {
            return WriteTextInternal(text, fontName, fontSize, fontStyle, position, horizontalAlignment,
                verticalAlignment, desiredLineHeight, Math.Abs(desiredLineHeight) > 0.2F, rotation, textColor, measure, layerName);
        }

        /// <summary>
        /// Puts an image at the desired position and scales the image accurately
        /// </summary>
        /// <param name="image">the image to put into the given rectangle</param>
        /// <param name="position">the rectangle where to place the image</param>
        /// <param name="layerName">the layer on which t add the object</param>
        public void DrawImage(Image image, RectangleF position, string layerName = null)
        {
            Objects.Image img = new Objects.Image
            {
                ImageValue = image,
                ObjectRegion = position,
                ObjectId = countObjects ? ++objectCounter : objectCounter
            };

            pages[CurrentPage-1].AddObject(img, layerName);
        }

        /// <summary>
        /// Draws an entire Recordset at the provided position
        /// </summary>
        /// <param name="data">the data to draw</param>
        /// <param name="drawHeaderCallback">a callback that is used to draw the header</param>
        /// <param name="drawDataCallback">a callback that is used to draw the next result</param>
        /// <param name="drawTableBottomCallback">a callback that is called when the table is rendered completly or when the page ends</param>
        public void DrawRecordset(DynamicResult[] data, Action<RenderContext, bool> drawHeaderCallback, Action<DynamicResult, int, RenderContext> drawDataCallback, Action<RenderContext, bool> drawTableBottomCallback)
        {
            if (!AllowAddingPages)
            {
                throw new InvalidOperationException("The ability to Add new pages must be given in order to draw a recordset.");
            }

            if (!AllowBlockMoveOperations)
            {
                throw new InvalidOperationException("The ability to perform Block-Move - Operations must be given in order to draw a recordset");
            }

            bool popHeader;
            bool popBottom;
            if (popHeader=(drawHeaderCallback != null))
            {
                drawHeaderCallback(renderContext, false);
                renderContext.PushHeaderEvent(drawHeaderCallback);
            }
            if (popBottom = (drawTableBottomCallback != null))
            {
                renderContext.PushTableBottomEvent(drawTableBottomCallback);
            }
            try
            {
                int id = 0;
                foreach (DynamicResult item in data)
                {
                    drawDataCallback(item, id, renderContext);
                    id++;
                }
            }
            finally
            {
                if (popHeader)
                {
                    renderContext.PopHeaderEvent();
                }
                if (popBottom)
                {
                    renderContext.PopTableBottomEvent();
                    drawTableBottomCallback(renderContext, false);
                }
            }
        }

        /// <summary>
        /// Draws a range of objects in the same positionrect
        /// </summary>
        /// <param name="positionRect">the positionrect in which to draw the requested data</param>
        /// <param name="renderers">all rendering-callbacks that must be fired</param>
        public void DrawBlock(RectangleF positionRect, params Action<RectangleF>[] renderers)
        {
            foreach (var renderer in renderers)
            {
                renderer(positionRect);
            }
        }

        /// <summary>
        /// Draws a range of objects in the same positionrect
        /// </summary>
        /// <param name="positionRect">the positionrect in which to draw the requested data</param>
        /// <param name="renderers">all rendering-callbacks that must be fired</param>
        /// <param name="layerName">the layer on which t add the object</param>
        public void DrawBlock(RectangleF positionRect, string layerName, params Action<string, RectangleF>[] renderers)
        {
            foreach (var renderer in renderers)
            {
                renderer(layerName, positionRect);
            }
        }

        /// <summary>
        /// Draws a range of objects in the same positionrect and moves the Render-Context to the bottom of the provided rect
        /// </summary>
        /// <param name="positionRect">the positionrect in which to draw the requested data</param>
        /// <param name="renderers">all rendering-callbacks that must be fired</param>
        public void DrawBlockMove(RectangleF positionRect, params Func<RectangleF,float, float>[] renderers)
        {
            if (!AllowBlockMoveOperations)
            {
                throw new InvalidOperationException("Block-Move operations are not supported by this Document printer");
            }

            int objectId = ++objectCounter;
            countObjects = false;
            try
            {
                float maxHeight = 0;
                float retVal = 0;
                foreach (var renderer in renderers)
                {
                    try
                    {
                        float tmpt = renderer(positionRect, maxHeight);
                        if (tmpt > maxHeight)
                        {
                            maxHeight = tmpt;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
                    }
                }

                RenderContext.CurrentYPos = positionRect.Top+maxHeight;
                if (Math.Abs(renderContext.CurrentYPos - renderContext.ContentRect.Bottom) < 1)
                {
                    NextPage();
                }
                else if (renderContext.CurrentYPos > renderContext.ContentRect.Bottom)
                {
                    IDrawingObject[] moveObjects = pages[CurrentPage - 1].PopObjects(objectId);
                    float y = moveObjects.Min(n => n.ObjectRegion.Y);
                    float newY =
                        moveObjects.Max(
                            n =>
                                !(n is IVirtualPositionElement)
                                    ? n.ObjectRegion.Bottom
                                    : ((IVirtualPositionElement) n).VirtualBottom ?? n.ObjectRegion.Bottom);
                    NextPage();
                    float diff = y - renderContext.CurrentYPos;
                    newY -= diff;
                    foreach (var obj in moveObjects)
                    {
                        obj.ObjectRegion = new RectangleF(obj.ObjectRegion.X, obj.ObjectRegion.Y - diff,
                            obj.ObjectRegion.Width, obj.ObjectRegion.Height);
                    }

                    pages[CurrentPage - 1].AddObjects(moveObjects, null);
                    renderContext.CurrentYPos = newY;
                }
            }
            finally
            {
                countObjects = true;
            }
        }

        /// <summary>
        /// Gets a Rectangle instance that is relative to the parent rect
        /// </summary>
        /// <param name="rectangle">the rectangle that has a relative position inside the parent rect</param>
        /// <param name="parent">the parent rectangle</param>
        /// <returns>a Rectangle that is absulute positioned in a 0,0 based coordiate system</returns>
        public RectangleF GetRelativeRect(RectangleF rectangle, RectangleF parent)
        {
            return new RectangleF(parent.X + rectangle.X, parent.Y + rectangle.Y, rectangle.Width, rectangle.Height);
        }

        /// <summary>
        /// Gets an inner-fit rectangle of the given rectangle
        /// </summary>
        /// <param name="rectangle">the rectangle for which to get the inner fitting rect</param>
        /// <param name="innerPadding">the inner padding to leave as space to the outer rect</param>
        /// <returns>a rectangle that fits inside the parent rectangle and has a border that can be freely defined</returns>
        public RectangleF GetInnerFittingRect(RectangleF rectangle, float innerPadding = 10)
        {
            return GetInnerFittingRect(rectangle, innerPadding, innerPadding);
        }

        /// <summary>
        /// Gets an inner-fit rectangle of the given rectangle
        /// </summary>
        /// <param name="rectangle">the rectangle for which to get the inner fitting rect</param>
        /// <param name="paddingX">the inner padding to leave as space to the outer rect on the X axis</param>
        /// <param name="paddingY">the inner padding to leave as space to the outer rect on the Y axis</param>
        /// <returns>a rectangle that fits inside the parent rectangle and has a border that can be freely defined</returns>
        public RectangleF GetInnerFittingRect(RectangleF rectangle, float paddingX, float paddingY)
        {
            return new RectangleF(rectangle.X + paddingX, rectangle.Y + paddingY,
                rectangle.Width - 2 * paddingX, rectangle.Height - 2 * paddingY);
        }

        /// <summary>
        /// Gets the outer rectangle of the given inner rectangle
        /// </summary>
        /// <param name="rectangle">the rectangle for which to get the outer enclosing-rect</param>
        /// <param name="innerPadding">the padding that was used to create the inner rect</param>
        /// <returns>the outer rect for the provided rectangle</returns>
        public RectangleF GetOuterEnclosingRect(RectangleF rectangle, float innerPadding = 10)
        {
            return GetOuterEnclosingRect(rectangle,innerPadding,innerPadding);
        }

        /// <summary>
        /// Gets an outer rectangle of the given rectangle
        /// </summary>
        /// <param name="rectangle">the rectangle for which to get the outer enclosing-rect</param>
        /// <param name="paddingX">the padding that was used to create the inner rect on the X axis</param>
        /// <param name="paddingY">the padding that was used to create the inner rect on the Y axis</param>
        /// <returns>a rectangle that fits inside the parent rectangle and has a border that can be freely defined</returns>
        public RectangleF GetOuterEnclosingRect(RectangleF rectangle, float paddingX, float paddingY)
        {
            return new RectangleF(rectangle.X - paddingX, rectangle.Y - paddingY,
                rectangle.Width + 2 * paddingX, rectangle.Height + 2 * paddingY);
        }

        /// <summary>
        /// Adds a new page
        /// </summary>
        public void NextPage()
        {
            if (!AllowAddingPages)
            {
                throw new InvalidOperationException("Adding pages is not supported by this Document printer");
            }

            if (CurrentPage == pages.Count)
            {
                Page p = new Page(this);
                p.Config = GetPageConfiguration(MinimumConfigurationRequirements);
                p.ContentArea = ContentArea;
                p.PageRectangle = PageRectangle;
                NewPageEventArgs e = new NewPageEventArgs(CurrentPage + 1, p.Config);
                OnQueryNewPageSettings(e);
                pages.Add(p);
                CurrentPage++;
                //renderContext.ContentRect = ContentArea;
                renderContext.CurrentYPos = ContentArea.Top;
                OnNewPage(e);
                renderContext.PrintHeaders();
            }
            else
            {
                throw new InvalidOperationException("Unable to perform a NextPage operation in this status!");
            }
        }

        /// <summary>
        /// Moves to the given page
        /// </summary>
        public void MoveToPage(int pageNumber)
        {
            if (pages.Count < pageNumber)
            {
                CurrentPage = pages.Count;
                while (CurrentPage < pageNumber)
                {
                    NextPage();
                }
            }

            CurrentPage = pageNumber;
        }

        /// <summary>
        /// Flushes the document and saves all objects that have been created on the current rendering process
        /// </summary>
        public void Flush()
        {
            PrepareDocument();
            BuildPageStrings();
            WritePages(pages.ToArray());
            FinalizeDocument();
            pages.Clear();
            CurrentPage = 0;
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose()
        {
            if (pages.Count != 0)
            {
                Flush();
            }
        }

        /// <summary>
        /// Creates Page-Configuration object for a specific page
        /// </summary>
        /// <returns>the configuration object containing all possible settings for a specific page</returns>
        protected abstract DocumentConfiguration GetPageConfiguration(DocumentConfiguration baseConfig);

        /// <summary>
        /// Writes all pages to the destination device
        /// </summary>
        /// <param name="pages">all pages that have been generated by the client objects</param>
        protected abstract void WritePages(Page[] pages);

        /// <summary>
        /// Prepares the document before the general initialization is done
        /// </summary>
        protected abstract void PrepareDocument();

        /// <summary>
        /// Finalizes the document when all work is done
        /// </summary>
        protected abstract void FinalizeDocument();

        /// <summary>
        /// creates a new page for this printer
        /// </summary>
        /// <returns>a new page instance</returns>
        protected Page GetPage()
        {
            return new Page(this);
        }

        /// <summary>
        /// Registers a page that already exists to the list of existing pages
        /// </summary>
        /// <param name="page">the page to add to the list of pages</param>
        protected void RegisterExistingPage(Page page)
        {
            pages.Add(page);
            if (CurrentPage == 0)
            {
                CurrentPage = 1;
            }
        }

        /// <summary>
        /// Raises the NewPage event
        /// </summary>
        /// <param name="e">the arguments for the NewPage event</param>
        protected virtual void OnNewPage(NewPageEventArgs e)
        {
            NewPage?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the QueryNewPageSettings event
        /// </summary>
        /// <param name="e">the arguments for the NewPage event</param>
        protected virtual void OnQueryNewPageSettings(NewPageEventArgs e)
        {
            QueryNewPageSettings?.Invoke(this, e);
        }

        private RectangleF WriteTextInternal(string text, string fontName, float fontSize, FontStyle fontStyle, RectangleF position, StringAlignment horizontalAlignment, StringAlignment verticalAlignment, float desiredLineHeight, bool useLineHeight, float rotation, Color textColor, bool measure, string layerName)
        {
            SizeF sz = new SizeF(position.Width - 10F, position.Height);
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (!useLineHeight || !measure)
                {
                    text = ReplacePageIdTextValue(text, CurrentPage.ToString());
                    RectangleF retVal = position;
                    if (measure)
                    {
                        SizeF size = MeasureText(text, fontName, fontSize, fontStyle, sz, horizontalAlignment,
                            verticalAlignment);
                        size = new SizeF(size.Width + 10F, size.Height);
                        float x = position.X;
                        float y = position.Y;
                        if (horizontalAlignment == StringAlignment.Center)
                        {
                            x = position.X + position.Width / 2 - size.Width / 2;
                        }
                        else if (horizontalAlignment == StringAlignment.Far)
                        {
                            x = position.X + position.Width - size.Width;
                        }

                        if (verticalAlignment == StringAlignment.Center)
                        {
                            y = position.Y + position.Height / 2 - size.Height / 2;
                        }
                        else if (verticalAlignment == StringAlignment.Far)
                        {
                            y = position.Y + position.Height - size.Height;
                        }

                        retVal = new RectangleF(new PointF(x, y), size);
                    }

                    pages[CurrentPage-1].AddObject(new Text
                    {
                        ObjectRegion = retVal,
                        TextValue = text,
                        FontName = fontName,
                        FontSize = fontSize,
                        FontStyle = fontStyle,
                        HorizontalAlignment = horizontalAlignment,
                        VerticalAlignment = verticalAlignment,
                        ObjectId = countObjects ? ++objectCounter : objectCounter,
                        LineSpacing = desiredLineHeight,
                        VirtualBottom = virtualBottom,
                        Rotation = rotation,
                        TextColor = textColor==default(Color)?Color.Black:textColor
                    },layerName);

                    return retVal;
                }

                float descentHeight;

                var lines = SplitMeasureString(ReplacePageCounterTextValue(text, PageCountMeasurementReplacerLength),
                    fontName, fontSize, fontStyle, sz, horizontalAlignment,
                    verticalAlignment, out descentHeight);
                float finalHeight = desiredLineHeight*lines.Length + descentHeight;
                float maxWidth = 0;
                float nextY = position.Y; // - spacingDiffer;
                try
                {
                    virtualBottom = position.Top + finalHeight;
                    foreach (string line in lines)
                    {
                        RectangleF rect = new RectangleF(position.X, nextY, position.Width, position.Height);
                        nextY += desiredLineHeight;
                        var posrect =
                            WriteTextInternal(RevertPageCounterPlaceholder(line, PageCountMeasurementReplacerLength),
                                fontName, fontSize, fontStyle, rect, horizontalAlignment,
                                verticalAlignment, desiredLineHeight, false, rotation, textColor, true, layerName);
                        if (posrect.Width > maxWidth)
                        {
                            maxWidth = posrect.Width;
                        }
                    }
                }
                finally
                {
                    virtualBottom = null;
                }

                return new RectangleF(position.X, position.Y, maxWidth, finalHeight);
            }

            return RectangleF.Empty;
        }

        /// <summary>
        /// Splits the provided string into lines that can be uniquely placed
        /// </summary>
        /// <param name="text">the text that needs to be splitted into lines</param>
        /// <param name="fontName">the font-name</param>
        /// <param name="fontSize">the font-size</param>
        /// <param name="fontStyle">the font-style</param>
        /// <param name="maxSize">the maximum rectangle</param>
        /// <param name="horizontalAlignment">the horizontal alignment</param>
        /// <param name="verticalAlignment">the vertical alignment</param>
        /// <param name="descent">the descent size of the font. This is required to fit a text at its bottom into a textbox</param>
        /// <returns>an array of lines that represents the provided string</returns>
        private string[] SplitMeasureString(string text, string fontName, float fontSize, FontStyle fontStyle, SizeF maxSize, StringAlignment horizontalAlignment, StringAlignment verticalAlignment, out float descent)
        {
            text = ReplacePageIdTextValue(text, CurrentPage.ToString());
            Font f = GetFont(fontName, fontSize, fontStyle);
            decimal units = f.FontFamily.GetLineSpacing(fontStyle);
            decimal descentUnits = f.FontFamily.GetCellDescent(fontStyle);
            decimal emh = f.FontFamily.GetEmHeight(fontStyle);
            decimal lineSize = units/emh*((decimal) fontSize/72)*100;
            decimal descentPoints = descentUnits/emh*((decimal) fontSize/72)*100;
            descent = (float) descentPoints;
            float epsilon = (float) lineSize/2F;
            var ranges =
                (from t in text.Select((c, i) => new {Character = c, Index = i}) where char.IsLetterOrDigit(t.Character) select t)
                    .ToArray();
            List<CharacterRange[]> allranges = new List<CharacterRange[]>();
            List<CharacterRange> singleList = new List<CharacterRange>();
            int lastStart = -2;
            int lastId = -2;
            for (int i = 0; i < ranges.Length; i++)
            {
                if (singleList.Count == 31)
                {
                    allranges.Add(singleList.ToArray());
                    singleList.Clear();
                }

                if (lastId < ranges[i].Index - 1)
                {
                    if (lastId >= 0)
                    {
                        var rng = new CharacterRange(lastStart, lastId - lastStart + 1);
                        var ln = ranges[i].Index - lastId -1;
                        string tmp = text.Substring(lastId + 1, ln);
                        if (tmp.Any(n => !char.IsWhiteSpace(n)))
                        {
                            rng.Length += ln;
                        }

                        singleList.Add(rng);
                    }

                    lastStart = ranges[i].Index;
                }

                lastId = ranges[i].Index;
            }

            if (lastId < 0)
            {
                lastId = text.Length - 1;
                lastStart = 0;
            }

            singleList.Add(new CharacterRange(lastStart, lastId - lastStart + 1));
            allranges.Add(singleList.ToArray());
            List<Region> regions = new List<Region>();
            List<CharacterRange> measuredRanges = new List<CharacterRange>();
            bool restVisible = true;
            foreach (var config in allranges)
            {
                var format = new StringFormat() {Alignment = horizontalAlignment, LineAlignment = verticalAlignment, Trimming = StringTrimming.Word, FormatFlags = StringFormatFlags.MeasureTrailingSpaces};
                format.SetMeasurableCharacterRanges(config);
                var tmp =
                    measurer.MeasureCharacterRanges(
                        text, f,
                        new RectangleF(new PointF(10F, 10F), maxSize),
                        format);
                regions.AddRange(tmp);
                measuredRanges.AddRange(config);
                if (tmp.Any(n => n.IsEmpty(measurer)||Math.Abs(n.GetBounds(measurer).Width) < 0.2F))
                {
                    restVisible = false;
                    break;
                }
            }

            float lastTop = -1;
            lastStart = -1;
            var matched = (from n in regions.Select((r, i) => new {Index = i, Region = r})
                join m in measuredRanges.Select((r, i) => new {Index = i, Range = r}) on n.Index equals m.Index
                select new {n.Index, n.Region, m.Range.First, m.Range.Length}).ToArray();
            List<string> lines = new List<string>();
            var lastMatch = matched.FirstOrDefault();
            lastMatch = null;
            for (int i = 0; i < matched.Length; i++)
            {
                var reg = matched[i];
                var top = reg.Region.GetBounds(measurer).Top;
                if (Math.Abs(top - lastTop) > epsilon)
                {
                    if (lastTop >= -0.2F)
                    {
                        int ln = reg.First - 1 - lastStart;
                        if (lastMatch != null)
                        {
                            int tmpL = reg.First - 1 - lastMatch.First;
                            if (tmpL < lastMatch.Length)
                            {
                                ln += lastMatch.Length - tmpL;
                            }
                        }

                        lines.Add(text.Substring(lastStart,ln));
                    }

                    lastStart = reg.First;
                    lastTop = top;
                }

                lastMatch = reg;
            }

            if (restVisible)
            {
                lines.Add(text.Substring(lastStart));
            }

            return lines.ToArray();
        }

        /// <summary>
        /// Replaces the PageCounter mark 
        /// </summary>
        private void BuildPageStrings()
        {
            string pageCount = pages.Count.ToString();
            foreach (Page p in pages)
            {
                foreach (Text t in p.GetObjects().Where(n => n.Object is Text).Select(n => n.Object).Cast<Text>())
                {
                    t.TextValue = ReplacePageCounterTextValue(t.TextValue, pageCount);
                }
            }
        }

        /// <summary>
        /// Replaces the pagecounter-mark in the provided string with the provided counterstring value
        /// </summary>
        /// <param name="original">the original string</param>
        /// <param name="counterStringLength">the expected counter-string length</param>
        /// <returns>the string that results on the given values</returns>
        private string ReplacePageCounterTextValue(string original, int counterStringLength)
        {
            return Regex.Replace(original, @"(?<pre>[^\\\w])%pc(?<post>\W|$)",
                $@"${{pre}}/{new string('*', counterStringLength-2)}\${{post}}");
        }

        /// <summary>
        /// Removes the PageCounter-replacement-string and puts the original pagecount-marker back in place
        /// </summary>
        /// <param name="replaced">the string containing a placeholderstring for the pagecounter</param>
        /// <param name="counterStringLength">the desired length of the placeholder</param>
        /// <returns>the string with the counter-placeholder replaced by the %pc - indicator</returns>
        private string RevertPageCounterPlaceholder(string replaced, int counterStringLength)
        {
            return Regex.Replace(replaced, $@"(?<pre>[^\\\w/])/\*{{{counterStringLength-2}}}\\(?<post>\W|$)",
                $@"${{pre}}%pc${{post}}");
        }

        /// <summary>
        /// Replaces the pagecounter-mark in the provided string with the provided counterstring value
        /// </summary>
        /// <param name="original">the original string</param>
        /// <param name="counterString">the counter-string value</param>
        /// <returns>the string that results on the given values</returns>
        private string ReplacePageCounterTextValue(string original, string counterString)
        {
            return Regex.Replace(original, @"(?<pre>[^\\\w])%pc(?<post>\W|$)", $"${{pre}}{counterString}${{post}}");
        }

        /// <summary>
        /// Replaces the pagecounter-mark in the provided string with the provided counterstring value
        /// </summary>
        /// <param name="original">the original string</param>
        /// <param name="counterString">the counter-string value</param>
        /// <returns>the string that results on the given values</returns>
        private string ReplacePageIdTextValue(string original, string counterString)
        {
            return Regex.Replace(original, @"(?<pre>[^\\\w])%cp(?<post>\W|$)", $"${{pre}}{counterString}${{post}}");
        }

        /// <summary>
        /// Creates a font that can be used for measuring strings
        /// </summary>
        /// <param name="fontName">the fontName that is used for the given font</param>
        /// <param name="fontSize">the size of the given font</param>
        /// <param name="fontStyle">the font-Style to apply for the measurement</param>
        /// <returns>a Font-Object that reflets the given information</returns>
        private Font GetFont(string fontName, float fontSize, FontStyle fontStyle)
        {
            string font = $"{fontName}.{fontSize}.{fontStyle}";
            if (fonts.ContainsKey(font))
            {
                return fonts[font];
            }

            Font f = new Font(fontName,fontSize,fontStyle);
            fonts.Add(font, f);
            return f;
        }

        /// <summary>
        /// Sets the Landscape value correctly
        /// </summary>
        /// <param name="value">the value indicating whether to use Landscape or Portrait</param>
        private void SetLandscape(bool value)
        {
            float w = PageRectangle.Width;
            float h = PageRectangle.Height;
            float iw = ContentArea.Width;
            float ih = ContentArea.Height;
            bool requireOr = w > h != value;
            bool requireIr = iw > ih != value;
            if (requireOr)
            {
                PageRectangle = new RectangleF(PageRectangle.X, PageRectangle.Y, h, w);
            }

            if (requireIr)
            {
                ContentArea = new RectangleF(ContentArea.X, ContentArea.Y, ih, iw);
            }
        }

        /// <summary>
        /// Handles the event when a new page is created
        /// </summary>
        public event NewPageEventHandler NewPage;

        /// <summary>
        /// Handles the event before a new page is created
        /// </summary>
        public event NewPageEventHandler QueryNewPageSettings;
    }
}
