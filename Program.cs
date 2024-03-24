using Newtonsoft.Json;

class Program
{
    static void Main(string[] args)
    {
        string dataDirectory = "data"; // The directory where the data will be stored

        string? proxyHost = null; // The proxy host can be null if you don't want to use a proxy
        int? proxyPort = null; // The proxy port can be null if you don't want to use a proxy
        string? proxyUsername = null; // The proxy username can be null if you don't want to use a proxy
        string? proxyPassword = null; // The proxy password can be null if you don't want to use a proxy

        Carjet api = new(dataDirectory, proxyHost, proxyPort, proxyUsername, proxyPassword);

        string startDateISO = "2024-03-26"; // The start date of the rental in the ISO 8601 format
        string endDateISO = "2024-03-27"; // The end date of the rental in the ISO 8601 format
        int startHour = 10; // The start hour of the rental
        int startMinute = 30; // The start minute of the rental
        int endHour = 10; // The end hour of the rental
        int endMinute = 30; // The end minute of the rental

        api.FetchListings(
            CarjetLocations.AnkaraAirport,
            startDateISO,
            startHour,
            startMinute,
            endDateISO,
            endHour,
            endMinute
        );
    }
}
