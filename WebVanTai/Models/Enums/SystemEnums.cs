namespace WebVanTai.Models.Enums
{
    public enum StationType
    {
        Pickup = 0,
        Dropoff = 1,
        Both = 2
    }

    public enum TripStatus
    {
        Scheduled = 0,
        Running = 1,
        Completed = 2,
        Cancelled = 3
    }

    public enum BookingStatus
    {
        Pending = 0,
        Paid = 1,
        Expired = 2,
        Cancelled = 3
    }

    public enum TicketStatus
    {
        Valid = 0,
        CheckedIn = 1,
        Void = 2
    }

    public enum PaymentMethod
    {
        VnPay = 0,
        Cash = 1
    }

    public enum CrewRole
    {
        Driver = 0,
        Assistant = 1
    }
}
