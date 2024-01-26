using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;

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

        [TestMethod]
        public void LoadSpriteProxy()
        {
            var xnaAssembly = typeof(SpriteBatch).Assembly;
            var spriteProxyType =
                xnaAssembly.GetType("Microsoft.Xna.Framework.Graphics.SpriteFont+StringProxy", true, true);
            Assert.IsNotNull(spriteProxyType);
        }

        [TestMethod]
        public void LoadSpriteBatchEffect()
        {
            var xnaAssembly = typeof(SpriteBatch).Assembly;
            var spriteEffectCodeType = xnaAssembly.GetType("Microsoft.Xna.Framework.Graphics.SpriteEffectCode", true, true);
            var effectCode = (byte[])spriteEffectCodeType.GetField("Code", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.IsNotNull(effectCode);
        }
    }
}
