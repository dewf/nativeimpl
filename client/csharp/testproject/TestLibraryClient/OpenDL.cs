namespace TestLibraryClient
{
    public struct CGPoint
    {
        public double X;
        public double Y;
        public CGPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
        internal TestLibrary.CGPoint ToNative()
        {
            return new TestLibrary.CGPoint(X, Y);
        }
    }
    public struct CGSize
    {
        public double Width;
        public double Height;
        public CGSize(double width, double height)
        {
            Width = width;
            Height = height;
        }
        internal TestLibrary.CGSize ToNative()
        {
            return new TestLibrary.CGSize(Width, Height);
        }
    }
    public struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;
        public CGRect(CGPoint origin, CGSize size)
        {
            Origin = origin;
            Size = size;
        }
        public CGRect(double x, double y, double width, double height)
        {
            Origin = new CGPoint(x, y);
            Size = new CGSize(width, height);
        }
        public static CGRect OfSize(double width, double height)
        {
            return new CGRect(0, 0, width, height);
        }
        internal TestLibrary.CGRect ToNative()
        {
            return new TestLibrary.CGRect(Origin.ToNative(), Size.ToNative());
        }
    }
    public class CGContext
    {
        private readonly TestLibrary.CGContext _dlContext;
        public CGContext(TestLibrary.CGContext dlContext)
        {
            _dlContext = dlContext;
        }
        public void SetRGBFillColor(double red, double green, double blue, double alpha)
        {
            TestLibrary.CGContextSetRGBFillColor(_dlContext, red, green, blue, alpha);
        }
        public void FillRect(CGRect rect)
        {
            TestLibrary.CGContextFillRect(_dlContext, rect.ToNative());
        }
    }
}