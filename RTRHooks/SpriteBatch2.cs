using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace RTRHooks
{

    public class SpriteBatch2 : SpriteBatch
    {
        private const int MaxBatchSize = 2048 * 1; // Multiplier must max out at 31
        private DynamicVertexBuffer vertexBuffer;
        private DynamicIndexBuffer indexBuffer;
        private readonly VertexPositionColorTexture[] outputVertices = new VertexPositionColorTexture[MaxBatchSize * 4];
        private int vertexBufferPosition;
        private static readonly float[] XCornerOffsets = new float[4]
        {
      0.0f,
      1f,
      1f,
      0.0f
        };
        private static readonly float[] YCornerOffsets = new float[4]
        {
      0.0f,
      0.0f,
      1f,
      1f
        };
        private readonly Effect spriteEffect;
        private readonly EffectParameter effectMatrixTransform;
        private SpriteSortMode spriteSortMode;
        private BlendState blendState;
        private DepthStencilState depthStencilState;
        private RasterizerState rasterizerState;
        private SamplerState samplerState;
        private Effect customEffect;
        private Matrix transformMatrix;
        private bool inBeginEndPair;
        private SpriteInfo[] spriteQueue = new SpriteInfo[MaxBatchSize];
        private int spriteQueueCount;
        private Texture2D[] spriteTextures;
        private int[] sortIndices;
        private SpriteInfo[] sortedSprites;
        private TextureComparer textureComparer;
        private BackToFrontComparer backToFrontComparer;
        private FrontToBackComparer frontToBackComparer;
        private static Vector2 _vector2Zero = Vector2.Zero;
        private static Rectangle? _nullRectangle = new Rectangle?();

        private readonly GraphicsDevice graphicsDevice;
        private ushort spriteBeginCount;
        private ushort spriteImmediateBeginCount;

        private readonly MethodInfo spriteFontInternalDraw;
        private readonly ConstructorInfo spriteFontStringProxy;

        private void DisposePlatformData()
        {
            this.vertexBuffer?.Dispose();
            this.indexBuffer?.Dispose();
        }

        private void AllocateBuffers()
        {
            if (this.vertexBuffer == null || this.vertexBuffer.IsDisposed)
            {
                this.vertexBuffer = new DynamicVertexBuffer(graphicsDevice, typeof(VertexPositionColorTexture), MaxBatchSize * 4, BufferUsage.WriteOnly);
                this.vertexBufferPosition = 0;
                this.vertexBuffer.ContentLost += (EventHandler<EventArgs>)((sender, e) => this.vertexBufferPosition = 0);
            }
            if (this.indexBuffer != null && !this.indexBuffer.IsDisposed)
                return;
            this.indexBuffer = new DynamicIndexBuffer(graphicsDevice, typeof(short), MaxBatchSize * 6, BufferUsage.WriteOnly);
            this.indexBuffer.SetData<short>(CreateIndexData());
            this.indexBuffer.ContentLost += (EventHandler<EventArgs>)((sender, e) => this.indexBuffer.SetData<short>(CreateIndexData()));
        }

        private static short[] CreateIndexData()
        {
            short[] indexData = new short[MaxBatchSize * 6];
            for (int index = 0; index < MaxBatchSize; ++index)
            {
                indexData[index * 6] = (short)(index * 4);
                indexData[index * 6 + 1] = (short)(index * 4 + 1);
                indexData[index * 6 + 2] = (short)(index * 4 + 2);
                indexData[index * 6 + 3] = (short)(index * 4);
                indexData[index * 6 + 4] = (short)(index * 4 + 2);
                indexData[index * 6 + 5] = (short)(index * 4 + 3);
            }
            return indexData;
        }

        private void SetPlatformRenderState()
        {
            this.AllocateBuffers();
            graphicsDevice.SetVertexBuffer((VertexBuffer)this.vertexBuffer);
            graphicsDevice.Indices = (IndexBuffer)this.indexBuffer;
        }

        private unsafe void PlatformRenderBatch(
          Texture2D texture,
          SpriteInfo[] sprites,
          int offset,
          int count)
        {
            float num1 = 1f / (float)texture.Width;
            float num2 = 1f / (float)texture.Height;
            int num3;
            for (; count > 0; count -= num3)
            {
                SetDataOptions options = SetDataOptions.NoOverwrite;
                num3 = count;
                if (num3 > MaxBatchSize - this.vertexBufferPosition)
                {
                    num3 = MaxBatchSize - this.vertexBufferPosition;
                    if (num3 < 256)
                    {
                        this.vertexBufferPosition = 0;
                        options = SetDataOptions.Discard;
                        num3 = count;
                        if (num3 > MaxBatchSize)
                            num3 = MaxBatchSize;
                    }
                }
                fixed (SpriteInfo* spriteInfoPtr1 = &sprites[offset])
                fixed (VertexPositionColorTexture* positionColorTexturePtr1 = &this.outputVertices[0])
                {
                    SpriteInfo* spriteInfoPtr2 = spriteInfoPtr1;
                    VertexPositionColorTexture* positionColorTexturePtr2 = positionColorTexturePtr1;
                    for (int index1 = 0; index1 < num3; ++index1)
                    {
                        float num4;
                        float num5;
                        if ((double)spriteInfoPtr2->Rotation != 0.0)
                        {
                            num4 = (float)Math.Cos((double)spriteInfoPtr2->Rotation);
                            num5 = (float)Math.Sin((double)spriteInfoPtr2->Rotation);
                        }
                        else
                        {
                            num4 = 1f;
                            num5 = 0.0f;
                        }
                        float num6 = (double)spriteInfoPtr2->Source.Z != 0.0 ? spriteInfoPtr2->Origin.X / spriteInfoPtr2->Source.Z : spriteInfoPtr2->Origin.X * 2E+32f;
                        float num7 = (double)spriteInfoPtr2->Source.W != 0.0 ? spriteInfoPtr2->Origin.Y / spriteInfoPtr2->Source.W : spriteInfoPtr2->Origin.Y * 2E+32f;
                        for (int index2 = 0; index2 < 4; ++index2)
                        {
                            float num8 = XCornerOffsets[index2];
                            float num9 = YCornerOffsets[index2];
                            float num10 = (num8 - num6) * spriteInfoPtr2->Destination.Z;
                            float num11 = (num9 - num7) * spriteInfoPtr2->Destination.W;
                            float num12 = (float)((double)spriteInfoPtr2->Destination.X + (double)num10 * (double)num4 - (double)num11 * (double)num5);
                            float num13 = (float)((double)spriteInfoPtr2->Destination.Y + (double)num10 * (double)num5 + (double)num11 * (double)num4);
                            if ((spriteInfoPtr2->Effects & SpriteEffects.FlipHorizontally) != SpriteEffects.None)
                                num8 = 1f - num8;
                            if ((spriteInfoPtr2->Effects & SpriteEffects.FlipVertically) != SpriteEffects.None)
                                num9 = 1f - num9;
                            positionColorTexturePtr2->Position.X = num12;
                            positionColorTexturePtr2->Position.Y = num13;
                            positionColorTexturePtr2->Position.Z = spriteInfoPtr2->Depth;
                            positionColorTexturePtr2->Color = spriteInfoPtr2->Color;
                            positionColorTexturePtr2->TextureCoordinate.X = (spriteInfoPtr2->Source.X + num8 * spriteInfoPtr2->Source.Z) * num1;
                            positionColorTexturePtr2->TextureCoordinate.Y = (spriteInfoPtr2->Source.Y + num9 * spriteInfoPtr2->Source.W) * num2;
                            ++positionColorTexturePtr2;
                        }
                        ++spriteInfoPtr2;
                    }
                }
                int vertexStride = sizeof(VertexPositionColorTexture);
                this.vertexBuffer.SetData<VertexPositionColorTexture>(this.vertexBufferPosition * vertexStride * 4, this.outputVertices, 0, num3 * 4, vertexStride, options);
                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, this.vertexBufferPosition * 4, num3 * 4, this.vertexBufferPosition * 6, num3 * 2);
                this.vertexBufferPosition += num3;
                offset += num3;
            }
        }

        public SpriteBatch2(GraphicsDevice graphicsDevice) : base(graphicsDevice)
        {
            this.graphicsDevice = graphicsDevice;
            var xnaAssembly = typeof(SpriteBatch).Assembly;
            var spriteEffectCodeType = xnaAssembly.GetType("Microsoft.Xna.Framework.Graphics.SpriteEffectCode", true, true);
            var effectCode = (byte[])spriteEffectCodeType.GetField("Code", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            spriteFontInternalDraw =
                typeof(SpriteFont).GetMethod("InternalDraw", BindingFlags.Instance | BindingFlags.NonPublic);
            var spriteProxyType =
                xnaAssembly.GetType("Microsoft.Xna.Framework.Graphics.SpriteFont+StringProxy", true, true);
            spriteFontStringProxy = spriteProxyType.TypeInitializer;
            this.spriteEffect = new Effect(graphicsDevice, effectCode);
            this.effectMatrixTransform = this.spriteEffect.Parameters["MatrixTransform"];
            this.AllocateBuffers();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || this.IsDisposed)
                    return;

                this.spriteEffect?.Dispose();
                this.DisposePlatformData();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public new void Begin()
        {
            this.Begin(SpriteSortMode.Deferred, (BlendState)null, (SamplerState)null, (DepthStencilState)null, (RasterizerState)null, (Effect)null, Matrix.Identity);
        }

        public new void Begin(SpriteSortMode sortMode, BlendState blendState)
        {
            this.Begin(sortMode, blendState, (SamplerState)null, (DepthStencilState)null, (RasterizerState)null, (Effect)null, Matrix.Identity);
        }

        public new void Begin(
          SpriteSortMode sortMode,
          BlendState blendState,
          SamplerState samplerState,
          DepthStencilState depthStencilState,
          RasterizerState rasterizerState)
        {
            this.Begin(sortMode, blendState, samplerState, depthStencilState, rasterizerState, (Effect)null, Matrix.Identity);
        }

        public new void Begin(
          SpriteSortMode sortMode,
          BlendState blendState,
          SamplerState samplerState,
          DepthStencilState depthStencilState,
          RasterizerState rasterizerState,
          Effect effect)
        {
            this.Begin(sortMode, blendState, samplerState, depthStencilState, rasterizerState, effect, Matrix.Identity);
        }

        public new void Begin(
          SpriteSortMode sortMode,
          BlendState blendState,
          SamplerState samplerState,
          DepthStencilState depthStencilState,
          RasterizerState rasterizerState,
          Effect effect,
          Matrix transformMatrix)
        {
            if (this.inBeginEndPair)
                throw new InvalidOperationException("FrameworkResources.EndMustBeCalledBeforeBegin");
            this.spriteSortMode = sortMode;
            this.blendState = blendState;
            this.samplerState = samplerState;
            this.depthStencilState = depthStencilState;
            this.rasterizerState = rasterizerState;
            this.customEffect = effect;
            this.transformMatrix = transformMatrix;
            if (sortMode == SpriteSortMode.Immediate)
            {
                if (spriteBeginCount > (ushort)0)
                    throw new InvalidOperationException("FrameworkResources.CannotNextSpriteBeginImmediate");
                this.SetRenderState();
                ++spriteImmediateBeginCount;
            }
            else if (spriteImmediateBeginCount > (ushort)0)
                throw new InvalidOperationException("FrameworkResources.CannotNextSpriteBeginImmediate");
            ++spriteBeginCount;
            this.inBeginEndPair = true;
        }

        public new void End()
        {
            if (!this.inBeginEndPair)
                throw new InvalidOperationException("FrameworkResources.BeginMustBeCalledBeforeEnd");
            if (this.spriteSortMode != SpriteSortMode.Immediate)
                this.SetRenderState();
            else
                --spriteImmediateBeginCount;
            if (this.spriteQueueCount > 0)
                this.Flush();
            this.inBeginEndPair = false;
            --spriteBeginCount;
        }

        public new void Draw(Texture2D texture, Vector2 position, Color color)
        {
            var vec4 = new Vector4()
            {
                X = position.X,
                Y = position.Y,
                Z = 1f,
                W = 1f
            };
            this.InternalDraw(texture, ref vec4, true, ref _nullRectangle, color, 0.0f, ref _vector2Zero, SpriteEffects.None, 0.0f);
        }

        public new void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color)
        {
            var vec4 = new Vector4()
            {
                X = position.X,
                Y = position.Y,
                Z = 1f,
                W = 1f
            };
            this.InternalDraw(texture, ref vec4, true, ref sourceRectangle, color, 0.0f, ref _vector2Zero, SpriteEffects.None, 0.0f);
        }

        public new void Draw(
          Texture2D texture,
          Vector2 position,
          Rectangle? sourceRectangle,
          Color color,
          float rotation,
          Vector2 origin,
          float scale,
          SpriteEffects effects,
          float layerDepth)
        {
            var vec4 = new Vector4()
            {
                X = position.X,
                Y = position.Y,
                Z = scale,
                W = scale
            };
            this.InternalDraw(texture, ref vec4, true, ref sourceRectangle, color, rotation, ref origin, effects, layerDepth);
        }

        public new void Draw(
          Texture2D texture,
          Vector2 position,
          Rectangle? sourceRectangle,
          Color color,
          float rotation,
          Vector2 origin,
          Vector2 scale,
          SpriteEffects effects,
          float layerDepth)
        {
            var vec4 = new Vector4()
            {
                X = position.X,
                Y = position.Y,
                Z = scale.X,
                W = scale.Y
            };
            this.InternalDraw(texture, ref vec4, true, ref sourceRectangle, color, rotation, ref origin, effects, layerDepth);
        }

        public new void Draw(Texture2D texture, Rectangle destinationRectangle, Color color)
        {
            var vec4 = new Vector4()
            {
                X = (float)destinationRectangle.X,
                Y = (float)destinationRectangle.Y,
                Z = (float)destinationRectangle.Width,
                W = (float)destinationRectangle.Height
            };
            this.InternalDraw(texture, ref vec4, false, ref _nullRectangle, color, 0.0f, ref _vector2Zero, SpriteEffects.None, 0.0f);
        }

        public new void Draw(
          Texture2D texture,
          Rectangle destinationRectangle,
          Rectangle? sourceRectangle,
          Color color)
        {
            var vec4 = new Vector4()
            {
                X = (float)destinationRectangle.X,
                Y = (float)destinationRectangle.Y,
                Z = (float)destinationRectangle.Width,
                W = (float)destinationRectangle.Height
            };
            this.InternalDraw(texture, ref vec4, false, ref sourceRectangle, color, 0.0f, ref _vector2Zero, SpriteEffects.None, 0.0f);
        }

        public new void Draw(
          Texture2D texture,
          Rectangle destinationRectangle,
          Rectangle? sourceRectangle,
          Color color,
          float rotation,
          Vector2 origin,
          SpriteEffects effects,
          float layerDepth)
        {
            var vec4 = new Vector4()
            {
                X = (float)destinationRectangle.X,
                Y = (float)destinationRectangle.Y,
                Z = (float)destinationRectangle.Width,
                W = (float)destinationRectangle.Height
            };
            this.InternalDraw(texture, ref vec4, false, ref sourceRectangle, color, rotation, ref origin, effects, layerDepth);
        }

        public new void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color)
        {
            if (spriteFont == null)
                throw new ArgumentNullException(nameof(spriteFont));

            /*SpriteFont.StringProxy*/
            object text1 = text != null ? spriteFontStringProxy.Invoke(new object[] { text }) : throw new ArgumentNullException(nameof(text));
            Vector2 one = Vector2.One;
            var args =
                new object[]
                {
                    /*ref*/ text1, this, position, color, 0.0f, Vector2.Zero, /*ref*/ one, SpriteEffects.None, 0.0f
                };
            spriteFontInternalDraw.Invoke(spriteFont, args);
            //spriteFont.InternalDraw(ref text1, this, position, color, 0.0f, Vector2.Zero, ref one, SpriteEffects.None, 0.0f);
        }

        public new void DrawString(
          SpriteFont spriteFont,
          StringBuilder text,
          Vector2 position,
          Color color)
        {
            if (spriteFont == null)
                throw new ArgumentNullException(nameof(spriteFont));
            /*SpriteFont.StringProxy*/
            object text1 = text != null ? spriteFontStringProxy.Invoke(new object[] { text }) : throw new ArgumentNullException(nameof(text));
            Vector2 one = Vector2.One;
            var args =
                new object[]
                {
                    /*ref*/ text1, this, position, color, 0.0f, Vector2.Zero, /*ref*/ one, SpriteEffects.None, 0.0f
                };
            spriteFontInternalDraw.Invoke(spriteFont, args);
            //spriteFont.InternalDraw(ref text1, this, position, color, 0.0f, Vector2.Zero, ref one, SpriteEffects.None, 0.0f);
        }

        public new void DrawString(
          SpriteFont spriteFont,
          string text,
          Vector2 position,
          Color color,
          float rotation,
          Vector2 origin,
          float scale,
          SpriteEffects effects,
          float layerDepth)
        {
            if (spriteFont == null)
                throw new ArgumentNullException(nameof(spriteFont));
            /*SpriteFont.StringProxy*/
            object text1 = text != null ? spriteFontStringProxy.Invoke(new object[] { text }) : throw new ArgumentNullException(nameof(text));
            var vec2 = new Vector2()
            {
                X = scale,
                Y = scale
            };
            var args =
                new object[]
                {
                    /*ref*/ text1, this, position, color, rotation, origin, /*ref*/ vec2, effects, layerDepth
                };
            spriteFontInternalDraw.Invoke(spriteFont, args);
            //spriteFont.InternalDraw(ref text1, this, position, color, rotation, origin, ref vec2, effects, layerDepth);
        }

        public new void DrawString(
          SpriteFont spriteFont,
          StringBuilder text,
          Vector2 position,
          Color color,
          float rotation,
          Vector2 origin,
          float scale,
          SpriteEffects effects,
          float layerDepth)
        {
            if (spriteFont == null)
                throw new ArgumentNullException(nameof(spriteFont));
            /*SpriteFont.StringProxy*/
            object text1 = text != null ? spriteFontStringProxy.Invoke(new object[] { text }) : throw new ArgumentNullException(nameof(text));
            var vec2 = new Vector2()
            {
                X = scale,
                Y = scale
            };
            var args =
                new object[]
                {
                    /*ref*/ text1, this, position, color, rotation, origin, /*ref*/ vec2, effects, layerDepth
                };
            spriteFontInternalDraw.Invoke(spriteFont, args);
            //spriteFont.InternalDraw(ref text1, this, position, color, rotation, origin, ref vec2, effects, layerDepth);
        }

        public new void DrawString(
          SpriteFont spriteFont,
          string text,
          Vector2 position,
          Color color,
          float rotation,
          Vector2 origin,
          Vector2 scale,
          SpriteEffects effects,
          float layerDepth)
        {
            if (spriteFont == null)
                throw new ArgumentNullException(nameof(spriteFont));
            /*SpriteFont.StringProxy*/
            object text1 = text != null ? spriteFontStringProxy.Invoke(new object[] { text }) : throw new ArgumentNullException(nameof(text));
            var args =
                new object[]
                {
                    /*ref*/ text1, this, position, color, rotation, origin, /*ref*/ scale, effects, layerDepth
                };
            spriteFontInternalDraw.Invoke(spriteFont, args);
            //spriteFont.InternalDraw(ref text1, this, position, color, rotation, origin, ref scale, effects, layerDepth);
        }

        public new void DrawString(
          SpriteFont spriteFont,
          StringBuilder text,
          Vector2 position,
          Color color,
          float rotation,
          Vector2 origin,
          Vector2 scale,
          SpriteEffects effects,
          float layerDepth)
        {
            if (spriteFont == null)
                throw new ArgumentNullException(nameof(spriteFont));
            /*SpriteFont.StringProxy*/
            object text1 = text != null ? spriteFontStringProxy.Invoke(new object[] { text }) : throw new ArgumentNullException(nameof(text));
            var args =
                new object[]
                {
                    /*ref*/ text1, this, position, color, rotation, origin, /*ref*/ scale, effects, layerDepth
                };
            spriteFontInternalDraw.Invoke(spriteFont, args);
            //spriteFont.InternalDraw(ref text1, this, position, color, rotation, origin, ref scale, effects, layerDepth);
        }

        private unsafe void InternalDraw(
          Texture2D texture,
          ref Vector4 destination,
          bool scaleDestination,
          ref Rectangle? sourceRectangle,
          Color color,
          float rotation,
          ref Vector2 origin,
          SpriteEffects effects,
          float depth)
        {
            if (texture == null)
                throw new ArgumentNullException(nameof(texture), "FrameworkResources.NullNotAllowed");
            if (!this.inBeginEndPair)
                throw new InvalidOperationException("FrameworkResources.BeginMustBeCalledBeforeDraw");
            if (this.spriteQueueCount >= this.spriteQueue.Length)
                Array.Resize<SpriteInfo>(ref this.spriteQueue, this.spriteQueue.Length * 2);
            fixed (SpriteInfo* spriteInfoPtr = &this.spriteQueue[this.spriteQueueCount])
            {
                float z = destination.Z;
                float w = destination.W;
                if (sourceRectangle.HasValue)
                {
                    Rectangle rectangle = sourceRectangle.Value;
                    spriteInfoPtr->Source.X = (float)rectangle.X;
                    spriteInfoPtr->Source.Y = (float)rectangle.Y;
                    spriteInfoPtr->Source.Z = (float)rectangle.Width;
                    spriteInfoPtr->Source.W = (float)rectangle.Height;
                    if (scaleDestination)
                    {
                        z *= (float)rectangle.Width;
                        w *= (float)rectangle.Height;
                    }
                }
                else
                {
                    float width = (float)texture.Width;
                    float height = (float)texture.Height;
                    spriteInfoPtr->Source.X = 0.0f;
                    spriteInfoPtr->Source.Y = 0.0f;
                    spriteInfoPtr->Source.Z = width;
                    spriteInfoPtr->Source.W = height;
                    if (scaleDestination)
                    {
                        z *= width;
                        w *= height;
                    }
                }
                spriteInfoPtr->Destination.X = destination.X;
                spriteInfoPtr->Destination.Y = destination.Y;
                spriteInfoPtr->Destination.Z = z;
                spriteInfoPtr->Destination.W = w;
                spriteInfoPtr->Origin.X = origin.X;
                spriteInfoPtr->Origin.Y = origin.Y;
                spriteInfoPtr->Rotation = rotation;
                spriteInfoPtr->Depth = depth;
                spriteInfoPtr->Effects = effects;
                spriteInfoPtr->Color = color;
            }
            if (this.spriteSortMode == SpriteSortMode.Immediate)
            {
                this.RenderBatch(texture, this.spriteQueue, 0, 1);
            }
            else
            {
                if (this.spriteTextures == null || this.spriteTextures.Length != this.spriteQueue.Length)
                    Array.Resize<Texture2D>(ref this.spriteTextures, this.spriteQueue.Length);
                this.spriteTextures[this.spriteQueueCount] = texture;
                ++this.spriteQueueCount;
            }
        }

        private void Flush()
        {
            SpriteInfo[] sprites;
            if (this.spriteSortMode == SpriteSortMode.Deferred)
            {
                sprites = this.spriteQueue;
            }
            else
            {
                this.SortSprites();
                sprites = this.sortedSprites;
            }
            int offset = 0;
            Texture2D texture = (Texture2D)null;
            for (int index = 0; index < this.spriteQueueCount; ++index)
            {
                Texture2D spriteTexture;
                if (this.spriteSortMode == SpriteSortMode.Deferred)
                {
                    spriteTexture = this.spriteTextures[index];
                }
                else
                {
                    int sortIndex = this.sortIndices[index];
                    sprites[index] = this.spriteQueue[sortIndex];
                    spriteTexture = this.spriteTextures[sortIndex];
                }
                if (spriteTexture != texture)
                {
                    if (index > offset)
                        this.RenderBatch(texture, sprites, offset, index - offset);
                    offset = index;
                    texture = spriteTexture;
                }
            }
            this.RenderBatch(texture, sprites, offset, this.spriteQueueCount - offset);
            Array.Clear((Array)this.spriteTextures, 0, this.spriteQueueCount);
            this.spriteQueueCount = 0;
        }

        private void SortSprites()
        {
            if (this.sortIndices == null || this.sortIndices.Length < this.spriteQueueCount)
            {
                this.sortIndices = new int[this.spriteQueueCount];
                this.sortedSprites = new SpriteInfo[this.spriteQueueCount];
            }
            IComparer<int> comparer;
            switch (this.spriteSortMode)
            {
                case SpriteSortMode.Texture:
                    if (this.textureComparer == null)
                        this.textureComparer = new TextureComparer(this);
                    comparer = (IComparer<int>)this.textureComparer;
                    break;
                case SpriteSortMode.BackToFront:
                    if (this.backToFrontComparer == null)
                        this.backToFrontComparer = new BackToFrontComparer(this);
                    comparer = (IComparer<int>)this.backToFrontComparer;
                    break;
                case SpriteSortMode.FrontToBack:
                    if (this.frontToBackComparer == null)
                        this.frontToBackComparer = new FrontToBackComparer(this);
                    comparer = (IComparer<int>)this.frontToBackComparer;
                    break;
                default:
                    throw new NotSupportedException();
            }
            for (int index = 0; index < this.spriteQueueCount; ++index)
                this.sortIndices[index] = index;
            Array.Sort<int>(this.sortIndices, 0, this.spriteQueueCount, comparer);
        }

        private void RenderBatch(
          Texture2D texture,
          SpriteInfo[] sprites,
          int offset,
          int count)
        {
            if (this.customEffect != null)
            {
                var passesCount = this.customEffect.CurrentTechnique.Passes.Count;
                for (var index = 0; index < passesCount; ++index)
                {
                    this.customEffect.CurrentTechnique.Passes[index].Apply();
                    graphicsDevice.Textures[0] = (Texture)texture;
                    this.PlatformRenderBatch(texture, sprites, offset, count);
                }
            }
            else
            {
                graphicsDevice.Textures[0] = (Texture)texture;
                this.PlatformRenderBatch(texture, sprites, offset, count);
            }
        }

        private void SetRenderState()
        {
            if (this.blendState != null)
                graphicsDevice.BlendState = this.blendState;
            else
                graphicsDevice.BlendState = BlendState.AlphaBlend;
            if (this.depthStencilState != null)
                graphicsDevice.DepthStencilState = this.depthStencilState;
            else
                graphicsDevice.DepthStencilState = DepthStencilState.None;
            if (this.rasterizerState != null)
                graphicsDevice.RasterizerState = this.rasterizerState;
            else
                graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            if (this.samplerState != null)
                graphicsDevice.SamplerStates[0] = this.samplerState;
            else
                graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            Viewport viewport = graphicsDevice.Viewport;
            var viewportWidth = viewport.Width > 0 ? 1f / (float)viewport.Width : 0.0f;
            var viewportHeight = viewport.Height > 0 ? -1f / (float)viewport.Height : 0.0f;
            Matrix matrix = new Matrix
            {
                M11 = viewportWidth * 2f,
                M22 = viewportHeight * 2f,
                M33 = 1f,
                M44 = 1f,
                M41 = -1f,
                M42 = 1f
            };
            matrix.M41 -= viewportWidth;
            matrix.M42 -= viewportHeight;
            this.effectMatrixTransform.SetValue(this.transformMatrix * matrix);
            this.spriteEffect.CurrentTechnique.Passes[0].Apply();
            this.SetPlatformRenderState();
        }

        private struct SpriteInfo
        {
            public Vector4 Source;
            public Vector4 Destination;
            public Vector2 Origin;
            public float Rotation;
            public float Depth;
            public SpriteEffects Effects;
            public Color Color;
        }

        private class TextureComparer : IComparer<int>
        {
            private readonly SpriteBatch2 parent;

            public TextureComparer(SpriteBatch2 parent) => this.parent = parent;

            public int Compare(int x, int y)
            {
                return string.Compare(parent.spriteTextures[x].Name, parent.spriteTextures[y].Name, StringComparison.Ordinal);
            }
        }

        private class BackToFrontComparer : IComparer<int>
        {
            private readonly SpriteBatch2 parent;

            public BackToFrontComparer(SpriteBatch2 parent) => this.parent = parent;

            public int Compare(int x, int y)
            {
                var depth1 = this.parent.spriteQueue[x].Depth;
                var depth2 = this.parent.spriteQueue[y].Depth;
                if ((double)depth1 > (double)depth2)
                    return -1;
                return (double)depth1 < (double)depth2 ? 1 : 0;
            }
        }

        private class FrontToBackComparer : IComparer<int>
        {
            private readonly SpriteBatch2 parent;

            public FrontToBackComparer(SpriteBatch2 parent) => this.parent = parent;

            public int Compare(int x, int y)
            {
                var depth1 = this.parent.spriteQueue[x].Depth;
                var depth2 = this.parent.spriteQueue[y].Depth;
                if ((double)depth1 > (double)depth2)
                    return 1;
                return (double)depth1 < (double)depth2 ? -1 : 0;
            }
        }
    }
}
