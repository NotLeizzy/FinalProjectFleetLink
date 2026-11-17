namespace FinalProjectFleetLink.Helpers;

public static class TimeWindowHelper
{
    public static bool Overlaps(
        DateTime startA,
        DateTime endA,
        DateTime startB,
        DateTime endB)
    {
        return startA < endB && startB < endA;
    }
}
