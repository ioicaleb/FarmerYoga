namespace FarmerYoga
{    
    public sealed class ModConfig
    {
        private Dictionary<string, SheetOffset[]> _modOffsets = new();
        public Dictionary<string, SheetOffset[]> ModOffsets {get => _modOffsets; set => _modOffsets = value ?? new();}
    }

    public sealed class SheetOffset
    {
        private string _sheetID = "";
        private int[][] _offsets = Array.Empty<int[]>();
        public string SheetID {get => _sheetID; set => _sheetID = value ?? "";}
        public int[][] Offsets { get => _offsets; set => _offsets = value ?? Array.Empty<int[]>();}
    }
}