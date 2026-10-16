using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using Microsoft.Xna.Framework.Graphics;

namespace FarmerYoga
{
    internal class ModEntry : Mod
    {
        public static ModEntry? Instance { get; private set; }
        private string BaseTexture = string.Empty;
        private Item? OriginalShirt;
        private Item? OriginalPants;
        public static string? ActiveCustomSuffix = null;
        public static bool TextureChanged = false;
        private ModConfig Config = new();
        private int[]? BaseOffsetsX;
        private int[]? BaseOffsetsY;

        public override void Entry(IModHelper helper)
        {
            Instance = this;

            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            Helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.ConsoleCommands.Add("setFrameOffset", "Sets the x and y offset of the farmer sprite at indicated index.\n \nUsage: setFrameOffset <sheet-name> <frame-index> <x-offset> <y-offset>\n<sheet-name> The label of the sprite sheet file (e.g. farmer_<sheet-name> but without the farmer_)" +
                "<frame-index> The index of the targeted sprite on the farmer's sprite sheet\n<x-offset> The number of pixels to horizontally offset the farmer's hair and clothes (positive right, negative left)\n" +
                "<y-offset> The number of pixels to vertically offset the farmer's hair and clothes (positive down, negative up)", SetFrameOffset);
            helper.ConsoleCommands.Add("getFrameOffset", "Gets the x and y offset of the farmer sprite at indicated index.\n \nUsage: getFrameOffset <frame-index>\n" +
                "<frame-index> The index of the targeted sprite on the farmer's sprite sheet", GetFrameOffset);
            helper.ConsoleCommands.Add("setDimensions", "Sets the width and height of the farmer sprite sheet.\n Usage: setDimensions <sheet-name> <pants cell width> <pants cell height> <shirt cell width> <shirt cell height>\n<sheet-name> The label of the sprite sheet file (e.g. farmer_<sheet-name> but without the farmer_)" +
                "<width> The width of each frame on the sprite sheet\n<height> The height of each frame on the sprite sheet", SetSheetDimensions);

            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();

            Config = GetConfig();
        }

        private void GameLoop_SaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            BaseTexture = Game1.player.FarmerRenderer.textureName.Value;
            BaseOffsetsX = FarmerRenderer.featureXOffsetPerFrame;
            BaseOffsetsY = FarmerRenderer.featureYOffsetPerFrame;
        }

        private void GameLoop_GameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            Event.RegisterCommand("changeFarmerSprite", ChangeFarmerSprite);
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (ActiveCustomSuffix is null)
                return;

            if (e.NameWithoutLocale.IsEquivalentTo($"Characters/Farmer/farmer_{ActiveCustomSuffix}"))
                TryLoadFromModFile(e, $"assets/farmer_{ActiveCustomSuffix}.png");
            else if (e.NameWithoutLocale.IsEquivalentTo($"Characters/Farmer/shirts_{ActiveCustomSuffix}"))
                TryLoadFromModFile(e, $"assets/shirts_{ActiveCustomSuffix}.png");
            else if (e.NameWithoutLocale.IsEquivalentTo($"Characters/Farmer/pants_{ActiveCustomSuffix}"))
                TryLoadFromModFile(e, $"assets/pants_{ActiveCustomSuffix}.png");
        }

        private void TryLoadFromModFile(AssetRequestedEventArgs e, string relativePath)
        {
            string fullPath = Path.Combine(Helper.DirectoryPath, relativePath);
            if (File.Exists(fullPath))
                e.LoadFromModFile<Texture2D>(relativePath, AssetLoadPriority.Medium);
            else
                Monitor.Log($"No local texture at '{relativePath}' for suffix '{ActiveCustomSuffix}' — expecting another mod to supply '{e.NameWithoutLocale}'.", LogLevel.Trace);
        }

        private void RevertFarmer()
        {
            ActiveCustomSuffix = null;
            TextureChanged = false;
            FarmerRendererPatch.ClearTextureCache();

            Helper.GameContent.InvalidateCache(BaseTexture);
            Game1.player.FarmerRenderer.textureName.Set(BaseTexture);
            FarmerRenderer.featureXOffsetPerFrame = BaseOffsetsX;
            FarmerRenderer.featureYOffsetPerFrame = BaseOffsetsY;

            Game1.player.bathingClothes.Set(false);
            Game1.player.shirtItem.Value = (Clothing?)OriginalShirt;
            Game1.player.pantsItem.Value = (Clothing?)OriginalPants;
            FarmerRendererPatch.SetFrameDimensions(GetClothingDimensions("default"));
            OriginalShirt = null;
            OriginalPants = null;

            Game1.player.FarmerRenderer.MarkSpriteDirty();
        }

        private int[] GetClothingDimensions(string fileName)
        {
            if (fileName == "default" || fileName == null)
            {
                return new int[] { 16, 32, 8, 8 };
            }
            SheetDetails? match = null;
            string? matchedModId = null;

            foreach (var kvp in Config.ModDetails!)
            {
                foreach (SheetDetails offsets in kvp.Value)
                {
                    if (offsets.SheetID != fileName)
                        continue;

                    if (match != null)
                    {
                        Monitor.Log($"Sheet ID '{fileName}' is defined more than once (in '{matchedModId}' and '{kvp.Key}') — using the first one found. Sheet IDs should be unique across all loaded content packs.", LogLevel.Warn);
                        continue;
                    }

                    match = offsets;
                    matchedModId = kvp.Key;
                }
            }

            return match?.ClothingDimensions ?? new int[] { 16, 32, 8, 8 };
        }

        private int[][] GetAllOffsetPairs(string fileName)
        {
            SheetDetails? match = null;
            string? matchedModId = null;

            foreach (var kvp in Config.ModDetails!)
            {
                foreach (SheetDetails offsets in kvp.Value)
                {
                    if (offsets.SheetID != fileName)
                        continue;

                    if (match != null)
                    {
                        Monitor.Log($"Sheet ID '{fileName}' is defined more than once (in '{matchedModId}' and '{kvp.Key}') — using the first one found. Sheet IDs should be unique across all loaded content packs.", LogLevel.Warn);
                        continue;
                    }

                    match = offsets;
                    matchedModId = kvp.Key;
                }
            }

            return match?.Offsets ?? Array.Empty<int[]>();
        }

        private int[] GetOffsetPair(string fileName, int frameIndex)
        {
            int[] offsetPair = new int[2] { 0, 0 };
            int[][] offsetPairs = GetAllOffsetPairs(fileName);
            try
            {
                if (offsetPairs.Length <= frameIndex)
                {
                    throw new ArgumentOutOfRangeException(nameof(frameIndex), $"Frame index {frameIndex} is not currently added to values. Check input or increase Number of Sprites to Add in config.");
                }
                else
                {
                    return offsetPairs[frameIndex];
                }
            }
            catch (ArgumentOutOfRangeException e)
            {
                Monitor.Log(e.Message);
            }
            return offsetPair;
        }

        private void ChangeToCustomOffsets(string fileName)
        {
            List<int> newXOffsets = new();
            List<int> newYOffsets = new();
            int[][] offsetPairs = GetAllOffsetPairs(fileName);
            foreach (int[] offsetPair in offsetPairs)
            {
                newXOffsets.Add(offsetPair[0]);
                newYOffsets.Add(offsetPair[1]);
            }
            FarmerRenderer.featureXOffsetPerFrame = newXOffsets.ToArray();
            FarmerRenderer.featureYOffsetPerFrame = newYOffsets.ToArray();
        }

        public void ChangeFarmerSprite(Event @event, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGet(args, 1, out string fileName, out string err))
            {
                context.LogErrorAndSkip(err);
                return;
            }

            if (fileName == "base")
            {
                RevertFarmer();
                @event.CurrentCommand++;
                return;
            }

            ActiveCustomSuffix = fileName;
            TextureChanged = true;
            FarmerRendererPatch.ClearTextureCache();

            string customBodyPath = $"Characters/Farmer/farmer_{(Game1.player.IsMale ? "" : "girl_")}{fileName}";
            Helper.GameContent.InvalidateCache(customBodyPath);
            Helper.GameContent.InvalidateCache($"Characters/Farmer/shirts_{fileName}");
            Helper.GameContent.InvalidateCache($"Characters/Farmer/pants_{fileName}");

            FarmerRendererPatch.SetFrameDimensions(GetClothingDimensions(fileName));
            Game1.player.FarmerRenderer.textureName.Set(customBodyPath);
            OriginalShirt = Game1.player.shirtItem.Value;
            OriginalPants = Game1.player.pantsItem.Value;
            Game1.player.bathingClothes.Set(true);
            Game1.player.pantsItem.Value = null;


            ChangeToCustomOffsets(fileName);
            Game1.player.FarmerRenderer.MarkSpriteDirty();

            @event.CurrentCommand++;
        }

        private void GameLoop_UpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Game1.eventUp && TextureChanged)
            {
                RevertFarmer();
            }
        }

        private void SetFrameOffset(string command, string[] args)
        {
            try
            {
                if (args.Length == 4)
                {
                    if (int.TryParse(args[1], out int frameIndex) && int.TryParse(args[2], out int xOffset) && int.TryParse(args[3], out int yOffset))
                    {
                        string fileName = args[0];
                        foreach (SheetDetails[] modOffset in Config.ModDetails!.Values)
                        {
                            foreach (SheetDetails offsets in modOffset)
                            {
                                if (offsets.SheetID == fileName)
                                {
                                    if (offsets.Offsets is null || frameIndex < 0 || frameIndex >= offsets.Offsets.Length)
                                    {
                                        Monitor.Log($"Frame index {frameIndex} doesn't exist for sheet '{fileName}' ({offsets.Offsets?.Length ?? 0} frames available). Make sure the sheet's FY_Offsets.json has been generated.", LogLevel.Error);
                                        return;
                                    }

                                    offsets.Offsets[frameIndex][0] = xOffset;
                                    offsets.Offsets[frameIndex][1] = yOffset;
                                    Monitor.Log($"Sprite at index {frameIndex} has been set to offset [{xOffset}, {yOffset}].", LogLevel.Info);
                                    WriteConfig();
                                    return;
                                }
                            }
                        }
                        Monitor.Log($"No sheet named '{fileName}' found in any loaded FarmerYoga content pack.", LogLevel.Error);
                    }
                    else
                    {
                        throw new FormatException($"Arguments 2, 3, and 4 must be integers.");
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(args), $"Arguments require 4 indexes. {args.Length} indexes found.");
                }
            }
            catch (Exception e)
            {
                Monitor.Log(e.Message, LogLevel.Error);
            }
        }

        private void SetSheetDimensions(string command, string[] args)
        {
            try
            {
                if (args.Length == 5)
                {
                    if (int.TryParse(args[1], out int pantsWidth) && int.TryParse(args[2], out int pantsHeight) && int.TryParse(args[3], out int shirtWidth) && int.TryParse(args[4], out int shirtHeight))
                    {
                        string fileName = args[0];
                        foreach (SheetDetails[] detailsList in Config.ModDetails!.Values)
                        {
                            foreach (SheetDetails details in detailsList)
                            {
                                if (details.SheetID == fileName)
                                {
                                    details.ClothingDimensions = new int[] { pantsWidth, pantsHeight, shirtWidth, shirtHeight };
                                    Monitor.Log($"Sheet '{fileName}' clothing dimensions have been updated.", LogLevel.Info);
                                    WriteConfig();
                                    return;
                                }
                            }
                        }
                        Monitor.Log($"No sheet named '{fileName}' found in any loaded FarmerYoga content pack.", LogLevel.Error);
                    }
                    else
                    {
                        throw new FormatException($"Arguments 2, 3, and 4 must be integers.");
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(args), $"Arguments require 4 indexes. {args.Length} indexes found.");
                }
            }
            catch (Exception e)
            {
                Monitor.Log(e.Message, LogLevel.Error);
            }
        }

        private void GetFrameOffset(string command, string[] args)
        {
            if (int.TryParse(args[1], out int frameIndex))
            {
                string fileName = args[0];
                int[] offsetPair = GetOffsetPair(fileName, frameIndex);
                if (offsetPair != null)
                {
                    Monitor.Log($"Current offset for frame {frameIndex}:\nx = {offsetPair[0]}\ny = {offsetPair[1]}", LogLevel.Debug);
                }
                else
                {
                    Monitor.Log($"No frame at index {frameIndex}.", LogLevel.Warn);
                }
            }
            else
            {
                Monitor.Log("Invalid input.", LogLevel.Error);
            }
        }

        private ModConfig GetConfig()
        {
            ModConfig newConfig = new ModConfig();
            foreach (IModInfo mod in Helper.ModRegistry.GetAll())
            {
                if (!mod.IsContentPack || !mod.Manifest.Dependencies.Any(d => d.UniqueID == "ioicaleb.farmeryoga") || !Helper.ModRegistry.IsLoaded(mod.Manifest.UniqueID))
                    continue;

                IContentPack? target = (IContentPack?)mod.GetType().GetProperty("ContentPack")!.GetValue(mod);
                if (target is null)
                    continue;

                Dictionary<string, SheetDetails[]>? modDetails = target.ReadJsonFile<Dictionary<string, SheetDetails[]>>("FY_Offsets.json");

                if (modDetails is null)
                {
                    modDetails = BuildDefaultOffsets(target);
                    if (modDetails.Count > 0)
                    {
                        target.WriteJsonFile("Fy_config/FY_Offsets.json", modDetails);
                        Monitor.Log($"Generated default FY_Offsets.json for '{mod.Manifest.UniqueID}' with {modDetails.Values.Sum(v => v.Length)} sheet(s).", LogLevel.Info);
                    }
                }

                try
                {
                    foreach (var kvp in modDetails)
                        newConfig.ModDetails![kvp.Key] = kvp.Value;
                }
                catch (Exception e)
                {
                    Monitor.Log(e.Message, LogLevel.Error);
                }
            }
            return newConfig;
        }

        private Dictionary<string, SheetDetails[]> BuildDefaultOffsets(IContentPack pack)
        {
            var result = new Dictionary<string, SheetDetails[]>();

            string configFolder = Path.Combine(pack.DirectoryPath, "FY_config");
            if (!Directory.Exists(configFolder))
                Directory.CreateDirectory(configFolder);

            var sheets = new List<SheetDetails>();
            var suffixesSeen = new HashSet<string>();

            foreach (string filePath in Directory.GetFiles(pack.DirectoryPath, "farmer_*.png", SearchOption.AllDirectories))
            {
                string nameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                string suffix = nameWithoutExtension.StartsWith("farmer_girl_")
                    ? nameWithoutExtension["farmer_girl_".Length..]
                    : nameWithoutExtension["farmer_".Length..];

                    if (!suffixesSeen.Add(suffix))
                    continue;

                int frameCount = GetFrameCount(filePath, GetClothingDimensions(suffix));
                int[][] offsets = new int[frameCount][];
                for (int i = 0; i < frameCount; i++)
                    offsets[i] = new int[] { 0, 0 };

                sheets.Add(new SheetDetails { SheetID = suffix, Offsets = offsets });
            }

            if (sheets.Count > 0)
                result[pack.Manifest.UniqueID] = sheets.ToArray();

            return result;
        }

        private int GetFrameCount(string pngPath, int[] clothingDimensions)
        {
            using FileStream stream = File.OpenRead(pngPath);
            using Texture2D probe = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
            int frameWidth = clothingDimensions[0];
            int frameHeight = clothingDimensions[1];
            int rows = Math.Max(1, probe.Height / frameHeight);
            int columns = Math.Max(1, probe.Width / frameWidth);
            return columns * rows;
        }

        private void WriteConfig()
        {
            foreach (IModInfo mod in Helper.ModRegistry.GetAll())
            {
                if (!mod.IsContentPack || !mod.Manifest.Dependencies.Any(d => d.UniqueID == "ioicaleb.farmeryoga") || !Helper.ModRegistry.IsLoaded(mod.Manifest.UniqueID))
                    continue;

                IContentPack? target = (IContentPack?)mod.GetType().GetProperty("ContentPack")!.GetValue(mod);
                if (target is null)
                    continue;

                if (!Config.ModDetails!.TryGetValue(mod.Manifest.UniqueID, out SheetDetails[]? details))
                    continue;

                try
                {
                    Dictionary<string, SheetDetails[]> modDetails = new()
                    {
                        { mod.Manifest.UniqueID, details }
                    };
                    target.WriteJsonFile("FY_config/FY_Offsets.json", modDetails);
                }
                catch (Exception e)
                {
                    Monitor.Log(e.Message, LogLevel.Error);
                }
            }
        }
    }
}