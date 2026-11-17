namespace FinalProjectFleetLink.Helpers;

public enum ExpiryState
{
    Expired,
    Soon,
    Ok
}

public static class ExpiryHelper
{
    public static ExpiryState GetExpiryState(DateOnly date)
    {
        return GetExpiryState(date.ToDateTime(TimeOnly.MinValue));
    }

    public static ExpiryState GetExpiryState(DateTime date)
    {
        var today = DateTime.Now.Date;
        if (date.Date < today)
        {
            return ExpiryState.Expired;
        }
        return date.Date <= today.AddDays(30)
            ? ExpiryState.Soon
            : ExpiryState.Ok;
    }

    public static Color GetRowColor(ExpiryState state)
    {
        return state switch
        {
            ExpiryState.Expired => Color.MistyRose,
            ExpiryState.Soon => Color.Moccasin,
            _ => Color.White
        };
    }

    public static Color GetRowColor(DateOnly date) => GetRowColor(GetExpiryState(date));

    public static Color GetRowColor(DateTime date) => GetRowColor(GetExpiryState(date));
}
