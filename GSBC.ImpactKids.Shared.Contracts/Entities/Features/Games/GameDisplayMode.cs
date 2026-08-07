namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

/// <summary>What the wall display puts on screen.</summary>
public enum GameDisplayMode
{
    /// <summary>Every game plus behaviour points - the running score for the night.</summary>
    Totals = 0,

    /// <summary>Just the game currently being played.</summary>
    CurrentGame = 1
}
