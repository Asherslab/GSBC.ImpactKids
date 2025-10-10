using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Conversion;

// ReSharper disable UnusedType.Global

[Mapper]
public partial class SchoolTermConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter,
    IConverter<DbService, Service>       serviceConverter
) : IConverter<DbSchoolTerm, SchoolTerm>
{
    [UseMapper]
    private readonly IConverter<DateTimeOffset, DateTime> _dateTimeConverter = dateTimeConverter;

    [UseMapper]
    private readonly IConverter<DbService, Service> _serviceConverter = serviceConverter;

    public partial SchoolTerm Convert(DbSchoolTerm input);
}

[Mapper]
public partial class ServiceConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbService, Service>
{
    [UseMapper]
    private readonly IConverter<DateTimeOffset, DateTime> _dateTimeConverter = dateTimeConverter;

    public partial Service Convert(DbService input);
}

[Mapper]
public partial class DateTimeMapper : IConverter<DateTimeOffset, DateTime>
{
    public DateTime Convert(DateTimeOffset offset) => offset.DateTime;
}