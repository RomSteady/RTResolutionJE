using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RTRHooks
{
    public static class Xna
    {
        public static Microsoft.Xna.Framework.Rectangle ShrinkRectangle(Microsoft.Xna.Framework.Rectangle rectToShrink)
        {
            rectToShrink.Inflate(
                Math.Min(0, (1920 - rectToShrink.Width) / 2),
                Math.Min(0, (1200 - rectToShrink.Height) / 2));
            return rectToShrink;
        }
    }
}
