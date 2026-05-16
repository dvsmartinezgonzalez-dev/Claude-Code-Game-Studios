using System.Runtime.CompilerServices;

// Grants internal access to test assemblies so they can use testing seams
// (LoadCatalogueTextAsync on LevelDataSystem, ClearInstanceForTesting, etc.)
[assembly: InternalsVisibleTo("Tests.Unit.LevelData")]
[assembly: InternalsVisibleTo("Tests.Integration.LevelData")]
[assembly: InternalsVisibleTo("Tests.Unit.GameStateManager")]
[assembly: InternalsVisibleTo("Tests.Integration.GameStateManager")]
