using System.ComponentModel.DataAnnotations;

namespace Olvanta.Application;

public sealed record ParkingSiteInput([Required, MaxLength(100)] string Name);
public sealed record ParkingZoneInput([Required, MaxLength(100)] string Name, [Range(1, 10000)] int Capacity);
public sealed record ParkingRateInput(int ServiceId, [Range(1, 1440)] int BlockMinutes, [Range(0, 1440)] int GraceMinutes, decimal BlockPrice);
public sealed record ParkingEntryInput(Guid RequestId, int ZoneId, [Required, MaxLength(24)] string Plate);
public sealed record ParkingExitInput(Guid RequestId, decimal ExpectedAmount, int? SessionId = null, string? Method = null, int? CustomerId = null);
public sealed record ParkingPassInput(Guid RequestId, [Required, MaxLength(24)] string Plate, DateOnly From, DateOnly Through, decimal Price, int SessionId, string Method, int? CustomerId = null);
public sealed record ParkingSiteView(int Id, string Name, string Currency);
public sealed record ParkingZoneView(int Id, string Name, int Capacity, int Occupied);
public sealed record ParkingRateView(int Id, int ServiceId, int BlockMinutes, int GraceMinutes, decimal BlockPrice);
public sealed record ParkingStayView(int Id, int ZoneId, string Plate, DateTimeOffset Entered, DateTimeOffset? Exited, int? PassId, int BlockMinutes, int GraceMinutes, decimal BlockPrice, int? Blocks, decimal? Amount, string? Method, int? SaleId, long? PaymentId);
public sealed record ParkingPassView(int Id, string Plate, DateOnly From, DateOnly Through, decimal Price, string? Method, int? SaleId, long PaymentId);
public sealed record ParkingQuote(int StayId, int Blocks, decimal Amount, string Currency);
public sealed record ParkingDeskView(DateOnly Today, List<ParkingSiteView> Sites, int? SiteId, List<ParkingZoneView> Zones, ParkingRateView? Rate, List<ParkingStayView> Stays, List<ParkingPassView> Passes, int? CashSessionId, List<InventoryOption> Services, List<InventoryOption> Customers);
