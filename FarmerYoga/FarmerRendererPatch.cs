using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewModdingAPI;

namespace FarmerYoga
{
    [HarmonyPatch(typeof(FarmerRenderer), "draw", new Type[] {
        typeof(SpriteBatch),
        typeof(FarmerSprite.AnimationFrame),
        typeof(int),
        typeof(Rectangle),
        typeof(Vector2),
        typeof(Vector2),
        typeof(float),
        typeof(int),
        typeof(Color),
        typeof(float),
        typeof(float),
        typeof(Farmer)
    })]
    public static class FarmerRendererPatch
    {
        private static Texture2D? cachedShirt;
        private static Texture2D? cachedPants;
        private static string? cachedSuffix;
        private static int[] cachedPantsDimensions = new int[] { 16, 32 };
        private static int[] cachedShirtDimensions = new int[] { 8, 8 };

        public static void ClearTextureCache()
        {
            cachedShirt = null;
            cachedPants = null;
            cachedSuffix = null;
        }
        public static void SetFrameDimensions(int[] clothingDimensions)
        {
            clothingDimensions ??= new int[] {16, 32, 8, 8};
            cachedPantsDimensions = new int[] { clothingDimensions[0], clothingDimensions[1] };
            cachedShirtDimensions = new int[] { clothingDimensions[2], clothingDimensions[3] };
        }

        [HarmonyPatch(typeof(FarmerRenderer), "draw", new System.Type[] {
        typeof(SpriteBatch),
        typeof(FarmerSprite.AnimationFrame),
        typeof(int),
        typeof(Rectangle),
        typeof(Vector2),
        typeof(Vector2),
        typeof(float),
        typeof(int),
        typeof(Color),
        typeof(float),
        typeof(float),
        typeof(Farmer)
        })]
        public static class FarmerRenderer_HidePants_Patch
        {
            private static Texture2D? blankPantsTexture;
            private static Texture2D? realPantsTexture;
            private static bool swapped;
    
            [HarmonyPrefix]
            public static void Prefix(Farmer who)
            {
                swapped = false;
    
                if (!ModEntry.TextureChanged || ModEntry.ActiveCustomSuffix is null || who != Game1.player)
                    return;
    
                Texture2D real = FarmerRenderer.pantsTexture;
                if (real is null)
                    return;
    
                if (blankPantsTexture is null || blankPantsTexture.Width != real.Width || blankPantsTexture.Height != real.Height)
                    blankPantsTexture = CreateBlankTexture(real.Width, real.Height);
    
                realPantsTexture = real;
                FarmerRenderer.pantsTexture = blankPantsTexture;
                swapped = true;
            }

            [HarmonyPostfix]
            public static void Postfix()
            {
                if (swapped)
                {
                    FarmerRenderer.pantsTexture = realPantsTexture!;
                    swapped = false;
                }
            }
    
            private static Texture2D CreateBlankTexture(int width, int height)
            {
                var texture = new Texture2D(Game1.graphics.GraphicsDevice, width, height);
                var blankPixels = new Color[width * height]; // defaults to (0,0,0,0) — fully transparent
                texture.SetData(blankPixels);
                return texture;
            }
        }


        [HarmonyPostfix]
        public static void Postfix(SpriteBatch b, FarmerSprite.AnimationFrame animationFrame, int currentFrame, Rectangle sourceRect, Vector2 position, Vector2 origin, float layerDepth, int facingDirection, Color overrideColor, float rotation, float scale, Farmer who)
        {

            if (!ModEntry.TextureChanged || ModEntry.ActiveCustomSuffix is null || who != Game1.player)
                return;

            string suffix = ModEntry.ActiveCustomSuffix;
            if (cachedSuffix != suffix)
            {
                ClearTextureCache();
                cachedSuffix = suffix;
                cachedShirt = TryLoad($"Characters/Farmer/shirts_{suffix}");
                cachedPants = TryLoad($"Characters/Farmer/pants_{suffix}");

                Texture2D bodyTex = Game1.content.Load<Texture2D>(who.FarmerRenderer.textureName.Value);

            }

            if (cachedShirt is null && cachedPants is null)
                return;

            SpriteEffects effects = (facingDirection == 3 || animationFrame.flip)
                ? SpriteEffects.FlipHorizontally
                : SpriteEffects.None;

            float renderScale = scale * Game1.pixelZoom;
            
            if (cachedPants != null)
            {
                int[] pantsDims = SetPantsDimensions();
                int pantsWidth = pantsDims[0];
                int pantsHeight = pantsDims[1];
                Vector2 pantsOrigin = new(origin.X, origin.Y - 8f);
                Color pantsColor = overrideColor == Color.White ? who.pantsColor.Value : overrideColor;
                int pantsColumns = cachedPants.Width / pantsWidth;
                int pantsRows = cachedPants.Height / pantsHeight;
                int pantsFrame = currentFrame % Math.Max(1, pantsColumns * pantsRows);
                int pantsCol = pantsFrame % pantsColumns;
                int pantsRow = pantsFrame / pantsColumns;
                Rectangle pantsRect = new(pantsCol * pantsWidth, pantsRow * pantsHeight, pantsWidth, pantsHeight);

                b.Draw(cachedPants, position, pantsRect, pantsColor, rotation, pantsOrigin, renderScale, effects, layerDepth + 0.0001f);
            }

            if (cachedShirt != null)
            {
                int[] shirtDims = SetShirtDimensions();
                int shirtWidth = shirtDims[0];
                int shirtHeight = shirtDims[1];
                int shirtColumns = cachedShirt.Width / shirtWidth;
                int shirtRows = cachedShirt.Height / shirtHeight;
                int shirtFrame = currentFrame % Math.Max(1, shirtColumns * shirtRows);
                int shirtCol = shirtFrame % shirtColumns;
                int shirtRow = shirtFrame / shirtColumns;
                Rectangle shirtRect = new(shirtCol * shirtWidth, shirtRow * shirtHeight, shirtWidth, shirtHeight);

                Vector2 shirtOrigin = new(origin.X - 4f, origin.Y - 24f);
                Color shirtColor = overrideColor == Color.White ? who.GetShirtColor() : overrideColor;
                b.Draw(cachedShirt, position, shirtRect, shirtColor, rotation, shirtOrigin, renderScale, effects, layerDepth + 0.0002f);
            }
        }

        private static Texture2D? TryLoad(string assetName)
        {
            try { return ModEntry.Instance!.Helper.GameContent.Load<Texture2D>(assetName); }
            catch { return null; }
        }
    }
}

