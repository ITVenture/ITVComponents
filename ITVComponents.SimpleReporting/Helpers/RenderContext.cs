using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;

namespace ITVComponents.SimpleReporting.Helpers
{
    public class RenderContext
    {
        private Func<RectangleF> getContentRectCallback;

        private Stack<Action<RenderContext,bool>> headEventStack = new Stack<Action<RenderContext,bool>>();

        private Stack<Action<RenderContext,bool>> bottomEventStack = new Stack<Action<RenderContext, bool>>();

        public RenderContext(Func<RectangleF> getContentRectCallback)
        {
            this.getContentRectCallback = getContentRectCallback;
        }

        public RectangleF ContentRect => getContentRectCallback();

        public float CurrentYPos { get; set; }

        public RectangleF RemainingContentRect => new RectangleF(ContentRect.X,CurrentYPos,ContentRect.Width,ContentRect.Height+ContentRect.Top-CurrentYPos);

        public void PushHeaderEvent(Action<RenderContext, bool> handler)
        {
            headEventStack.Push(handler);
        }

        public void PopHeaderEvent()
        {
            headEventStack.Pop();
        }

        public void PushTableBottomEvent(Action<RenderContext, bool> handler)
        {
            bottomEventStack.Push(handler);
        }

        public void PopTableBottomEvent()
        {
            bottomEventStack.Pop();
        }

        public void PrintHeaders()
        {
            headEventStack.ForEach(n => n(this, true));
        }

        public void PrintTableBottoms()
        {
            bottomEventStack.ForEach(n => n(this, true));
        }
    }
}
