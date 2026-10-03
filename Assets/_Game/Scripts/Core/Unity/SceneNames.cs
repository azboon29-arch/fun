namespace Game.App
{
    /// <summary>Scene names, matching the files built by Tools > Merge Command > Build Scenes.</summary>
    public static class SceneNames
    {
        /// <summary>Entry scene that creates services and loads the save.</summary>
        public const string Boot = "Boot";

        /// <summary>Merge board and base.</summary>
        public const string Base = "Base";

        /// <summary>Planning and battle.</summary>
        public const string Battle = "Battle";
    }
}
