using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RTRHooks.Test
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestShrink()
        {
            var rectToShrink = new Microsoft.Xna.Framework.Rectangle(0, 0, 3840, 2160);
            var newRect = RTRHooks.Xna.ShrinkRectangle(rectToShrink);
            Assert.IsTrue(newRect.Width <= 1920);
            Assert.IsTrue(newRect.Height <= 1200);
        }
    }
}
