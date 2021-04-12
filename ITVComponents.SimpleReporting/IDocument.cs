using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.SimpleReporting.Config;
using ITVComponents.SimpleReporting.Helpers;
using ITVComponents.SimpleReporting.Objects;
using Image = System.Drawing.Image;

namespace ITVComponents.SimpleReporting
{
    public interface IDocument:IDisposable
    {
        /// <summary>
        /// Gets the DocumentConfiguration that is used to render a document
        /// </summary>
        DocumentConfiguration Configuration { get; }

        /// <summary>
        /// Gets the rendering-context of the current Document
        /// </summary>
        RenderContext RenderContext { get; }

        /// <summary>
        /// Initializes a document using the attached configuration
        /// </summary>
        void InitializeDocument();

        /// <summary>
        /// Adds a layer object to the list of layers
        /// </summary>
        /// <param name="layer"></param>
        void AddLayer(Layer layer);

        /// <summary>
        /// Gets the layer objects for the given Page
        /// </summary>
        /// <param name="page">the page object for which to get the layer-objects</param>
        /// <returns>an enumerable containing all layer-objects</returns>
        IEnumerable<LayerObject> GetLayerObjects(Page page);

        /// <summary>
        /// Draws a Frame
        /// </summary>
        /// <param name="rect">the coordinates and measures of the rectangle</param>
        /// <param name="lineColor">the line color for this frame</param>
        /// <param name="fillColor">the filling-color for this frame</param>
        /// <param name="layerName">the layer on which t add the object</param>
        void DrawFrame(RectangleF rect, Color? lineColor = null, Color? fillColor = null, string layerName = null);

        /// <summary>
        /// Draws a line
        /// </summary>
        /// <param name="start">the starting-poing of the line</param>
        /// <param name="end">the end-point of the line</param>
        /// <param name="lineColor">the color of the drawn line</param>
        /// <param name="layerName">the layer on which t add the object</param>
        void DrawLine(PointF start, PointF end, Color? lineColor, string layerName = null);

        /// <summary>
        /// Fills a rectangle
        /// </summary>
        /// <param name="rect">the coordinates and measures of the rectangle</param>
        /// <param name="color">the color that is used to fill the rectangle</param>
        /// <param name="layerName">the layer on which t add the object</param>
        void FillFrame(RectangleF rect, Color color, string layerName = null);

        /// <summary>
        /// Draws a rectangle.
        /// </summary>
        /// <param name="rect">the coordinates and measures of the rectangle</param>
        /// <param name="borderColor">the desired border-color</param>
        /// <param name="fillColor">the desired fill-color</param>
        /// <param name="layerName">the layer on which t add the object</param>
        void Frame(RectangleF rect, Color borderColor, Color fillColor, string layerName = null);

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
        SizeF MeasureText(string text, string fontName, float fontSize, FontStyle fontStyle, SizeF maxSize,
            StringAlignment horizontalAlignment, StringAlignment verticalAlignment, float desiredLineHeight = 0);

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
        RectangleF WriteText(string text, string fontName, float fontSize, FontStyle fontStyle, RectangleF position,
            StringAlignment horizontalAlignment, StringAlignment verticalAlignment, float desiredLineHeight = 0F, float rotation = 0F, Color textColor = default(Color), bool measure = true, string layerName = null);

        /// <summary>
        /// Puts an image at the desired position and scales the image accurately
        /// </summary>
        /// <param name="image">the image to put into the given rectangle</param>
        /// <param name="position">the rectangle where to place the image</param>
        /// <param name="layerName">the layer on which t add the object</param>
        void DrawImage(Image image, RectangleF position, string layerName = null);

        /// <summary>
        /// Draws a range of objects in the same positionrect
        /// </summary>
        /// <param name="positionRect">the positionrect in which to draw the requested data</param>
        /// <param name="renderers">all rendering-callbacks that must be fired</param>
        /// <param name="layerName">the layer on which t add the object</param>
        void DrawBlock(RectangleF positionRect, params Action<RectangleF>[] renderers);

        /// <summary>
        /// Draws a range of objects in the same positionrect
        /// </summary>
        /// <param name="positionRect">the positionrect in which to draw the requested data</param>
        /// <param name="renderers">all rendering-callbacks that must be fired</param>
        /// <param name="layerName">the layer on which t add the object</param>
        void DrawBlock(RectangleF positionRect, string layerName, params Action<string, RectangleF>[] renderers);

        /// <summary>
        /// Draws a range of objects in the same positionrect and moves the Render-Context to the bottom of the provided rect
        /// </summary>
        /// <param name="positionRect">the positionrect in which to draw the requested data</param>
        /// <param name="renderers">all rendering-callbacks that must be fired</param>
        void DrawBlockMove(RectangleF positionRect, params Func<RectangleF,float,float>[] renderers);

        /// <summary>
        /// Draws an entire Recordset at the provided position
        /// </summary>
        /// <param name="data">the data to draw</param>
        /// <param name="drawHeaderCallback">a callback that is used to draw the header</param>
        /// <param name="drawDataCallback">a callback that is used to draw the next result</param>
        /// <param name="drawTableBottomCallback">a callback that is called when the table is rendered completly or when the page ends</param>
        void DrawRecordset(DynamicResult[] data, Action<RenderContext, bool> drawHeaderCallback, Action<DynamicResult, int, RenderContext> drawDataCallback, Action<RenderContext, bool> drawTableBottomCallback);

        /// <summary>
        /// Gets a Rectangle instance that is relative to the parent rect
        /// </summary>
        /// <param name="rectangle">the rectangle that has a relative position inside the parent rect</param>
        /// <param name="parent">the parent rectangle</param>
        /// <returns>a Rectangle that is absulute positioned in a 0,0 based coordiate system</returns>
        RectangleF GetRelativeRect(RectangleF rectangle, RectangleF parent);

        /// <summary>
        /// Gets an inner-fit rectangle of the given rectangle
        /// </summary>
        /// <param name="rectangle">the rectangle for which to get the inner fitting rect</param>
        /// <param name="innerPadding">the inner padding to leave as space to the outer rect</param>
        /// <returns>a rectangle that fits inside the parent rectangle and has a border that can be freely defined</returns>
        RectangleF GetInnerFittingRect(RectangleF rectangle, float innerPadding = 10);

        /// <summary>
        /// Gets an inner-fit rectangle of the given rectangle
        /// </summary>
        /// <param name="rectangle">the rectangle for which to get the inner fitting rect</param>
        /// <param name="paddingX">the inner padding to leave as space to the outer rect on the X axis</param>
        /// <param name="paddingY">the inner padding to leave as space to the outer rect on the Y axis</param>
        /// <returns>a rectangle that fits inside the parent rectangle and has a border that can be freely defined</returns>
        RectangleF GetInnerFittingRect(RectangleF rectangle, float paddingX, float paddingY);

        /// <summary>
        /// Gets the outer rectangle of the given inner rectangle
        /// </summary>
        /// <param name="rectangle">the rectangle for which to get the outer enclosing-rect</param>
        /// <param name="innerPadding">the padding that was used to create the inner rect</param>
        /// <returns>the outer rect for the provided rectangle</returns>
        RectangleF GetOuterEnclosingRect(RectangleF rectangle, float innerPadding = 10);

        /// <summary>
        /// Gets an outer rectangle of the given rectangle
        /// </summary>
        /// <param name="rectangle">the rectangle for which to get the outer enclosing-rect</param>
        /// <param name="paddingX">the padding that was used to create the inner rect on the X axis</param>
        /// <param name="paddingY">the padding that was used to create the inner rect on the Y axis</param>
        /// <returns>a rectangle that fits inside the parent rectangle and has a border that can be freely defined</returns>
        RectangleF GetOuterEnclosingRect(RectangleF rectangle, float paddingX, float paddingY);

        /// <summary>
        /// Adds a new page
        /// </summary>
        void NextPage();

        /// <summary>
        /// Moves to the given page
        /// </summary>
        void MoveToPage(int pageNumber);

        /// <summary>
        /// Flushes the document and saves all objects that have been created on the current rendering process
        /// </summary>
        void Flush();

        /// <summary>
        /// Handles the event when a new page is created
        /// </summary>
        event NewPageEventHandler NewPage;

        /// <summary>
        /// Handles the event before a new page is created
        /// </summary>
        event NewPageEventHandler QueryNewPageSettings;
    }

    /// <summary>
    /// EventHandler for the NewPage event
    /// </summary>
    /// <param name="sender">the sender of the event</param>
    /// <param name="e">the event-arguments that are used to handle the NewPage event</param>
    public delegate void NewPageEventHandler(object sender, NewPageEventArgs e);


    public class NewPageEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the NewPage event args
        /// </summary>
        /// <param name="pageRectangle">the page rectangle that represents the entire page</param>
        /// <param name="contentRectangle">the rectnagle that represents the content area</param>
        /// <param name="pageId">the index of the current page</param>
        /// <param name="pageConfiguration">the configuration for the current page</param>
        public NewPageEventArgs(int pageId, DocumentConfiguration pageConfiguration)
        {
            PageId = pageId;
            PageConfiguration = pageConfiguration;
        }

        /// <summary>
        /// Gets the rectangle that represents the current page
        /// </summary>
        public RectangleF PageRectangle => PageConfiguration.GetValue<RectangleF>("PageRectangle");

        /// <summary>
        /// Gets or sets the rectangle that represents the content area
        /// </summary>
        public RectangleF ContentArea
        {
            get { return PageConfiguration.GetValue<RectangleF>("ContentArea"); }
            set { PageConfiguration.SetValue<RectangleF>("ContentArea", value); }
        }

        /// <summary>
        /// Gets the configuration for the current page
        /// </summary>
        public DocumentConfiguration PageConfiguration { get; }

        /// <summary>
        /// Gets the current page id
        /// </summary>
        public int PageId { get; }
    }
}
