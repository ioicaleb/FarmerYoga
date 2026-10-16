namespace FarmerYoga
{    
    public sealed class ModConfig
    {
        private Dictionary<string, SheetDetails[]> _modDetails = new();
        public Dictionary<string, SheetDetails[]> ModDetails {get => _modDetails; set => _modDetails = value ?? new();}
    }

    public sealed class SheetDetails
    {
        private string _sheetID = "";
        private int[][] _offsets = Array.Empty<int[]>();
        public int[] _clothingDimensions = {16, 32, 8, 8};
        public string SheetID {get => _sheetID; set => _sheetID = value ?? "";}
        public int[][] Offsets { get => _offsets; set => _offsets = value ?? Array.Empty<int[]>();}
        public int[] ClothingDimensions {get => _clothingDimensions; set => _clothingDimensions = value ?? new int[] {16, 32, 8, 8};}
    }
}