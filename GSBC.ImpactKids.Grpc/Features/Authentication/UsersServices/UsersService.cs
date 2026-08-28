using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Authentication;

namespace GSBC.ImpactKids.Grpc.Features.Authentication.UsersServices;

public partial class UsersService(
    GsbcDbContext       db,
    IEventService<User> eventService,
    IConverter<DbUser, User> converter
) : IUsersService;