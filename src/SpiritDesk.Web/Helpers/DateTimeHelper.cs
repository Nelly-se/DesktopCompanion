namespace SpiritDesk.Web.Helpers;

public static class DateTimeHelper
{
    public static string GetGreetingByHour(DateTime now)
    {
        return now.Hour switch
        {
            < 6 => "夜深了",
            < 12 => "早上好",
            < 18 => "下午好",
            _ => "晚上好"
        };
    }
}
